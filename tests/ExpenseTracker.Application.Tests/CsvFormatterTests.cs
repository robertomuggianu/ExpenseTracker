using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Application.Tests;

public sealed class CsvFormatterTests
{
    [Fact]
    public void Csv_HasHeaderInvariantAmountIsoDateAndEscapedText()
    {
        var row = new CsvTransactionRow(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new DateTime(2026, 7, 14, 10, 30, 0, DateTimeKind.Utc),
            TransactionType.Expense,
            "Conto; famiglia",
            "Spesa",
            "Pane \"speciale\"",
            12.50m,
            "EUR",
            "Riga uno\r\nRiga due");

        var csv = CsvFormatter.Format([row]);

        Assert.StartsWith(CsvFormatter.Header, csv);
        Assert.Contains("2026-07-14T10:30:00.0000000Z", csv);
        Assert.Contains("12.50", csv);
        Assert.Contains("\"Conto; famiglia\"", csv);
        Assert.Contains("\"Pane \"\"speciale\"\"\"", csv);
        Assert.Contains("\"Riga uno\r\nRiga due\"", csv);
    }
}
