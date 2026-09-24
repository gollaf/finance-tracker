using System.Globalization;
using System.Text;
using FinanceTracker.Domain.Imports;
using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Turns raw CSV text into ImportJobRows, the shape StartImportCommand
    /// expects, each tagged with its line number in the file. Parsing the
    /// uploaded file is I/O and belongs to the API layer (ADR 0006) --
    /// Application only ever sees already-parsed rows.
    ///
    /// Expects a header line (skipped, not validated) followed by data rows
    /// shaped "Amount,Type,Description,OccurredOn", with OccurredOn as
    /// yyyy-MM-dd. A field may be wrapped in double quotes to contain a
    /// literal comma; a doubled "" inside a quoted field is an escaped
    /// literal quote. Blank lines are skipped everywhere, including inside
    /// the data, so RowNumber below counts surviving non-blank lines, not
    /// strictly the file's own line count if it has gaps.
    ///
    /// A row that fails to parse is reported as an error and simply left
    /// out of the rows to import -- the same "one bad row doesn't sink the
    /// batch" philosophy ProcessImportJobCommandHandler applies to rows that
    /// parse fine but fail a domain rule.
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
                // i is 0-based and lines[0] is the header, so the header is
                // row 1 and the first data row is row 2 -- as a user
                // counting lines in their spreadsheet would number them.
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
