using System.Globalization;
using System.Text;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Turns raw CSV text into ImportJobRows, each tagged with its row
    /// number (ADR 0006: parsing the upload belongs to the Api).
    ///
    /// Expects a header line (skipped, not validated), then rows shaped
    /// "Amount,Type,Description,OccurredOn" with OccurredOn as yyyy-MM-dd.
    /// Quoted fields may contain commas, and "" is an escaped quote. Blank
    /// lines are skipped and not counted in RowNumber.
    ///
    /// A row that fails to parse is reported as an error and left out; the
    /// rest are still imported.
    /// </summary>
    internal static class CsvTransactionRowParser
    {
        internal static (IReadOnlyList<ImportJobRow> Rows, IReadOnlyList<ImportJobRowError> ParseErrors) Parse(
            string csvContent)
        {
            var rows = new List<ImportJobRow>();
            var parseErrors = new List<ImportJobRowError>();

            var lines = csvContent
                .Replace("\r\n", "\n")
                .Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            // lines[0] is the header -- skipped, not validated.
            for (var i = 1; i < lines.Count; i++)
            {
                // The header is row 1, so the first data row is row 2.
                var rowNumber = i + 1;
                var fields = SplitCsvLine(lines[i]);

                if (fields.Count != 4)
                {
                    parseErrors.Add(new ImportJobRowError(rowNumber, $"Expected 4 columns, found {fields.Count}."));
                    continue;
                }

                if (!decimal.TryParse(fields[0].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    parseErrors.Add(new ImportJobRowError(rowNumber, $"Invalid amount: '{fields[0]}'."));
                    continue;
                }

                if (!Enum.TryParse<TransactionType>(fields[1].Trim(), ignoreCase: true, out var type))
                {
                    parseErrors.Add(new ImportJobRowError(rowNumber, $"Invalid transaction type: '{fields[1]}'."));
                    continue;
                }

                if (!DateOnly.TryParseExact(
                    fields[3].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredOn))
                {
                    parseErrors.Add(new ImportJobRowError(rowNumber, $"Invalid date: '{fields[3]}'. Expected yyyy-MM-dd."));
                    continue;
                }

                var description = fields[2].Trim();
                rows.Add(new ImportJobRow(rowNumber, amount, type, description, occurredOn));
            }

            return (rows, parseErrors);
        }

        private static IReadOnlyList<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }
    }
}
