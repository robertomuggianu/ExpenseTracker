using System.Globalization;
using System.Text;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Infrastructure;

public sealed record CsvTransactionRow(
    Guid Id,
    DateTime DateUtc,
    TransactionType Type,
    string Account,
    string Category,
    string? Description,
    decimal Amount,
    string Currency,
    string? Notes);

public static class CsvFormatter
{
    public const string Header = "Id;Data;Tipo;Conto;Categoria;Descrizione;Importo;Valuta;Note";

    public static string Format(IEnumerable<CsvTransactionRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder().AppendLine(Header);
        foreach (var row in rows)
        {
            var values = new[]
            {
                row.Id.ToString("D"),
                row.DateUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                row.Type == TransactionType.Income ? "Entrata" : "Uscita",
                row.Account,
                row.Category,
                row.Description ?? string.Empty,
                row.Amount.ToString("0.00################", CultureInfo.InvariantCulture),
                row.Currency,
                row.Notes ?? string.Empty
            };
            builder.AppendLine(string.Join(';', values.Select(Escape)));
        }

        return builder.ToString();
    }

    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

public sealed class CsvExportService(IExpenseRepository repository) : ICsvExportService
{
    public async Task<CsvExportResult> ExportAsync(
        TransactionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var transactions = await repository.GetTransactionsAsync(filter, cancellationToken);
        var accounts = (await repository.GetAccountsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var categories = (await repository.GetCategoriesAsync(cancellationToken: cancellationToken)).ToDictionary(item => item.Id);

        var rows = transactions.Select(item => new CsvTransactionRow(
            item.Id,
            item.OccurredAtUtc,
            item.Type,
            accounts[item.AccountId].Name,
            categories[item.CategoryId].Name,
            item.Description,
            item.Amount,
            item.Currency,
            item.Notes));

        var fileName = $"movimenti-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return new CsvExportResult(fileName, CsvFormatter.Format(rows));
    }
}
