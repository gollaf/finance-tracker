using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Imports
{
    /// <summary>
    /// A CSV import that was accepted but runs later, in the background:
    /// the rows to import, and -- once processed -- what happened to them.
    /// Its own aggregate, referencing its Account by id only (ADR 0005).
    /// See docs/adr/0016-asynchronous-csv-import.md.
    /// </summary>
    /// <remarks>
    /// The lifecycle is one-way: Pending, then exactly one of Completed or
    /// Failed, never back. That is what lets processing be retried safely --
    /// a job that's no longer Pending has already been processed, and is
    /// skipped instead of imported a second time.
    /// </remarks>
    public sealed class ImportJob
    {
        public const int MaxRows = 10_000;
        public const int MaxFailureReasonLength = 1000;

        public ImportJobId Id { get; }

        public AccountId AccountId { get; }

        public ImportJobStatus Status { get; private set; }

        /// <summary>The parsed rows to import, in file order.</summary>
        public IReadOnlyList<ImportJobRow> Rows { get; private set; }

        /// <summary>
        /// Rows that were not imported, ordered by line number: rows the
        /// file parser already rejected when the job was created, plus rows
        /// that failed a domain rule during processing.
        /// </summary>
        public IReadOnlyList<ImportJobRowError> Errors { get; private set; }

        public int ImportedCount { get; private set; }

        public string? FailureReason { get; private set; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset? CompletedAt { get; private set; }

        public bool IsPending => Status == ImportJobStatus.Pending;

        // Used by Create below and, through constructor binding, by EF Core
        // when loading a job. Everything else is set through the private
        // setters.
        private ImportJob(ImportJobId id, AccountId accountId, DateTimeOffset createdAt)
        {
            Id = id;
            AccountId = accountId;
            CreatedAt = createdAt;
            Status = ImportJobStatus.Pending;
            Rows = [];
            Errors = [];
        }

        /// <param name="rows">The rows that parsed successfully -- at least one.</param>
        /// <param name="parseErrors">Rows the file parser already rejected; reported as-is at the end.</param>
        public static ImportJob Create(
            AccountId accountId,
            IEnumerable<ImportJobRow> rows,
            IEnumerable<ImportJobRowError> parseErrors,
            DateTimeOffset? createdAt = null)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(parseErrors);

            var rowList = rows.ToList();

            if (rowList.Count == 0)
                throw new ArgumentException("An import job needs at least one row to import.", nameof(rows));

            if (rowList.Count > MaxRows)
                throw new ArgumentException($"An import job cannot contain more than {MaxRows} rows.", nameof(rows));

            return new ImportJob(ImportJobId.New(), accountId, createdAt ?? DateTimeOffset.UtcNow)
            {
                Rows = rowList,
                Errors = parseErrors.OrderBy(e => e.RowNumber).ToList(),
            };
        }

        public void Complete(int importedCount, IEnumerable<ImportJobRowError> rowErrors, DateTimeOffset? completedAt = null)
        {
            ArgumentNullException.ThrowIfNull(rowErrors);
            EnsurePending();

            if (importedCount < 0 || importedCount > Rows.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(importedCount), $"Imported count must be between 0 and {Rows.Count}.");
            }

            ImportedCount = importedCount;
            Errors = Errors.Concat(rowErrors).OrderBy(e => e.RowNumber).ToList();
            Status = ImportJobStatus.Completed;
            CompletedAt = completedAt ?? DateTimeOffset.UtcNow;
        }

        public void Fail(string reason, DateTimeOffset? failedAt = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A failure reason is required.", nameof(reason));

            EnsurePending();

            var trimmed = reason.Trim();
            FailureReason = trimmed.Length <= MaxFailureReasonLength ? trimmed : trimmed[..MaxFailureReasonLength];
            Status = ImportJobStatus.Failed;
            CompletedAt = failedAt ?? DateTimeOffset.UtcNow;
        }

        private void EnsurePending()
        {
            if (Status != ImportJobStatus.Pending)
                throw new InvalidOperationException($"Import job {Id} has already been processed ({Status}).");
        }
    }
}
