using System.Globalization;
using System.Text;
using FinanceTracker.Application.Transactions.ImportTransactionsFromCsv;
using FinanceTracker.Domain.Transactions;

namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Turns raw CSV text into CsvTransactionRow, the shape
    /// ImportTransactionsFromCsvCommand expects. This is exactly the I/O
    /// step CsvTransactionRow's own doc comment calls out as belonging to
    /// the API layer -- Application only ever sees already-parsed rows.
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
    /// out of what's sent to the command -- the same "one bad row doesn't
    /// sink the batch" philosophy ImportTransactionsFromCsvCommandHandler
    /// already applies to rows that parse fine but fail a domain rule.
    /// </summary>
    internal static class CsvTransactionRowParser
    {
        internal static (IReadOnlyList<ParsedCsvRow> ParsedRows, IReadOnlyList<ImportRowErrorResponse> ParseErrors) Parse(
            string csvContent)
        {
            var parsedRows = new List<ParsedCsvRow>();
            var parseErrors = new List<ImportRowErrorResponse>();

            var lines = csvContent
                .Replace("\r\n", "\n")
                .Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            // lines[0] is the header -- skipped, not validated.
            for (var i = 1; i < lines.Count; i++)
            {
                var originalIndex = i - 1;
                var rowNumber = originalIndex + 2;
                var fields = SplitCsvLine(lines[i]);

                if (fields.Count != 4)
                {
                    parseErrors.Add(new ImportRowErrorResponse(rowNumber, $"Expected 4 columns, found {fields.Count}."));
                    continue;
                }

                if (!decimal.TryParse(fields[0].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    parseErrors.Add(new ImportRowErrorResponse(rowNumber, $"Invalid amount: '{fields[0]}'."));
                    continue;
                }

                if (!Enum.TryParse<TransactionType>(fields[1].Trim(), ignoreCase: true, out var type))
                {
                    parseErrors.Add(new ImportRowErrorResponse(rowNumber, $"Invalid transaction type: '{fields[1]}'."));
                    continue;
                }

                if (!DateOnly.TryParseExact(
                    fields[3].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredOn))
                {
                    parseErrors.Add(new ImportRowErrorResponse(rowNumber, $"Invalid date: '{fields[3]}'. Expected yyyy-MM-dd."));
                    continue;
                }

                var description = fields[2].Trim();
                var row = new CsvTransactionRow(amount, type, description, occurredOn);
                parsedRows.Add(new ParsedCsvRow(row, originalIndex));
            }

            return (parsedRows, parseErrors);
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
