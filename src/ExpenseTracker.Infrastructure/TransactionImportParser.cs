using System.Globalization;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Infrastructure;

public sealed class TransactionImportParser : ITransactionImportParser
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public Task<ImportParseResult> ParseAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Task.Run(() => ParseWorkbook(stream, cancellationToken), cancellationToken);
        }

        if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCsvAsync(stream, fileName, cancellationToken);
        }

        throw new DomainValidationException("Formato non supportato. Seleziona un file .xlsx o .csv.");
    }

    private static ImportParseResult ParseWorkbook(Stream stream, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        var relationshipTargets = relationships.Root?
            .Elements(PackageRelationshipNamespace + "Relationship")
            .ToDictionary(
                item => (string?)item.Attribute("Id") ?? string.Empty,
                item => (string?)item.Attribute("Target") ?? string.Empty)
            ?? new Dictionary<string, string>();

        var rows = new List<ImportedTransactionRow>();
        var warnings = new List<string>();
        var adjustedTransfers = 0;
        var duplicateCounters = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var sheet in workbook.Root?
                     .Element(SpreadsheetNamespace + "sheets")?
                     .Elements(SpreadsheetNamespace + "sheet") ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sheetName = (string?)sheet.Attribute("name") ?? string.Empty;
            var relationshipId = (string?)sheet.Attribute(RelationshipNamespace + "id") ?? string.Empty;
            if (!relationshipTargets.TryGetValue(relationshipId, out var target))
            {
                continue;
            }

            var normalizedTarget = target.Replace('\\', '/').TrimStart('/');
            var entryPath = normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                ? normalizedTarget
                : $"xl/{normalizedTarget}";
            var sheetDocument = ReadXml(archive, entryPath);
            var sheetRows = sheetDocument.Descendants(SpreadsheetNamespace + "row").Skip(2).ToArray();

            if (sheetName.Equals("Transfers", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var row in sheetRows)
                {
                    var rowNumber = (int?)row.Attribute("r") ?? 0;
                    var cells = row.Elements(SpreadsheetNamespace + "c")
                        .ToDictionary(
                            cell => GetColumnIndex((string?)cell.Attribute("r")),
                            cell => ReadCell(cell, sharedStrings));
                    if (!cells.TryGetValue(0, out var dateValue) || string.IsNullOrWhiteSpace(dateValue))
                    {
                        continue;
                    }

                    try
                    {
                        var date = ParseDate(dateValue);
                        var source = Required(cells, 1, "origine trasferimento");
                        var destination = Required(cells, 2, "conto destinatario");
                        var amount = ParseAmount(Required(cells, 3, "importo trasferimento"));
                        var currency = Required(cells, 4, "valuta trasferimento");
                        var originalComment = NormalizeOptional(cells.GetValueOrDefault(7));
                        var comment = originalComment is null
                            ? $"Trasferimento importato da {source}"
                            : $"Trasferimento importato da {source} Ã‚Â· {originalComment}";
                        var received = CreateImportedRow(
                            date,
                            TransactionType.Income,
                            destination,
                            "Trasferimento ricevuto",
                            amount,
                            currency,
                            comment,
                            duplicateCounters);
                        var transferId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("transfer|" + received.SourceKey)).AsSpan(0, 16));
                        rows.Add(received with { TransferId = transferId });
                        rows.Add(CreateImportedRow(date, TransactionType.Expense, source, "Trasferimento inviato", amount,
                            currency, originalComment, duplicateCounters) with { TransferId = transferId });
                        adjustedTransfers++;
                    }
                    catch (Exception exception) when (exception is FormatException or DomainValidationException)
                    {
                        warnings.Add($"Transfers, riga {rowNumber}: {exception.Message}");
                    }
                }

                continue;
            }

            if (!TryGetSheetType(sheetName, out var type))
            {
                continue;
            }

            foreach (var row in sheetRows)
            {
                var rowNumber = (int?)row.Attribute("r") ?? 0;
                var cells = row.Elements(SpreadsheetNamespace + "c")
                    .ToDictionary(
                        cell => GetColumnIndex((string?)cell.Attribute("r")),
                        cell => ReadCell(cell, sharedStrings));

                if (!TryCreateRow(cells, type, duplicateCounters, out var imported, out var error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        warnings.Add($"{sheetName}, riga {rowNumber}: {error}");
                    }

                    continue;
                }

                rows.Add(imported!);
            }
        }

        if (adjustedTransfers > 0)
        {
            warnings.Add($"{adjustedTransfers} trasferimento importato con addebito e accredito collegati, escluso dai report entrate e spese.");
        }

        return new ImportParseResult(rows, 0, warnings);
    }

    private static async Task<ImportParseResult> ParseCsvAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var firstLine = content.Split('\n').FirstOrDefault() ?? "";
        var delimiter = firstLine.Contains(';') ? ';' : ',';
        var csvRows = ParseDelimited(content, delimiter);
        var headerIndex = csvRows.FindIndex(row =>
            row.Any(cell => cell.Equals("Data", StringComparison.OrdinalIgnoreCase)
                            || cell.Equals("Date and time", StringComparison.OrdinalIgnoreCase)));
        if (headerIndex < 0)
        {
            throw new DomainValidationException("Intestazioni CSV non riconosciute.");
        }

        var headers = csvRows[headerIndex]
            .Select((value, index) => new { value, index })
            .ToDictionary(item => item.value.Trim(), item => item.index, StringComparer.OrdinalIgnoreCase);
        var rows = new List<ImportedTransactionRow>();
        var warnings = new List<string>();
        var duplicateCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        var hasNativeType = headers.ContainsKey("Tipo");
        var inferredType = InferTypeFromFileName(fileName);

        for (var index = headerIndex + 1; index < csvRows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = csvRows[index];
            try
            {
                var type = hasNativeType
                    ? ParseType(Get(values, headers, "Tipo"))
                    : inferredType ?? throw new DomainValidationException("Il CSV non indica se contiene entrate o uscite.");
                var dateValue = Get(values, headers, hasNativeType ? "Data" : "Date and time");
                var date = ParseDate(dateValue);
                var category = Get(values, headers, "Categoria", "Category");
                var account = Get(values, headers, "Conto", "Account");
                var amount = ParseAmount(Get(values, headers, "Importo", "Amount in account currency"));
                var currency = Get(values, headers, "Valuta", "Account currency");
                var comment = NormalizeOptional(Get(values, headers, "Note", "Comment", optional: true));
                var idText = Get(values, headers, "Id", optional: true);
                Guid? nativeId = Guid.TryParse(idText, out var id) && id != Guid.Empty ? id : null;
                if (headers.ContainsKey("Id") && nativeId is null) throw new DomainValidationException("identificativo movimento non valido");
                var transferText = Get(values, headers, "TransferId", optional: true);
                Guid? transferId = Guid.TryParse(transferText, out var tid) && tid != Guid.Empty ? tid : null;
                if (!string.IsNullOrWhiteSpace(transferText) && transferId is null) throw new DomainValidationException("identificativo trasferimento non valido");
                rows.Add(CreateImportedRow(date, type, account, category, amount, currency, comment, duplicateCounters)
                    with { NativeId = nativeId, Description = NormalizeOptional(Get(values, headers, "Descrizione", optional: true)), TransferId = transferId });
            }
            catch (Exception exception) when (exception is FormatException or DomainValidationException)
            {
                warnings.Add($"CSV, riga {index + 1}: {exception.Message}");
            }
        }

        return new ImportParseResult(rows, 0, warnings);
    }

    private static bool TryCreateRow(
        IReadOnlyDictionary<int, string> cells,
        TransactionType type,
        Dictionary<string, int> duplicateCounters,
        out ImportedTransactionRow? row,
        out string? error)
    {
        row = null;
        error = null;
        if (!cells.TryGetValue(0, out var dateValue) || string.IsNullOrWhiteSpace(dateValue))
        {
            return false;
        }

        try
        {
            var date = ParseDate(dateValue);
            var category = Required(cells, 1, "categoria");
            var account = Required(cells, 2, "conto");
            var amount = ParseAmount(Required(cells, 3, "importo"));
            var currency = Required(cells, 4, "valuta");
            var comment = NormalizeOptional(cells.GetValueOrDefault(8));
            row = CreateImportedRow(date, type, account, category, amount, currency, comment, duplicateCounters);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or DomainValidationException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static ImportedTransactionRow CreateImportedRow(
        DateTime date,
        TransactionType type,
        string account,
        string category,
        decimal amount,
        string currency,
        string? comment,
        Dictionary<string, int> duplicateCounters)
    {
        var canonical = string.Join('|',
            type,
            date.ToString("O", CultureInfo.InvariantCulture),
            account.Trim().ToUpperInvariant(),
            category.Trim().ToUpperInvariant(),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            currency.Trim().ToUpperInvariant(),
            comment?.Trim().ToUpperInvariant() ?? string.Empty);
        var occurrence = duplicateCounters.GetValueOrDefault(canonical);
        duplicateCounters[canonical] = occurrence + 1;
        return new ImportedTransactionRow(
            $"{canonical}|{occurrence}",
            date,
            type,
            account.Trim(),
            category.Trim(),
            amount,
            currency.Trim().ToUpperInvariant(),
            comment);
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path)
            ?? throw new DomainValidationException($"File Excel non valido: manca {path}.");
        using var entryStream = entry.Open();
        return XDocument.Load(entryStream);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(item => item.Value));
        }

        var value = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        return value;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        var index = 0;
        foreach (var character in cellReference?.TakeWhile(char.IsLetter) ?? [])
        {
            index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return index - 1;
    }

    private static string Required(IReadOnlyDictionary<int, string> cells, int index, string label)
    {
        var value = cells.GetValueOrDefault(index);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{label} mancante");
        }

        return value;
    }

    private static DateTime ParseDate(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            return DateTime.FromOADate(serial);
        }

        // Native exports use ISO; external Italian dates use day/month/year explicitly.
        if (DateTimeOffset.TryParseExact(value, ["O", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso)) return iso.LocalDateTime;
        if (DateTime.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss"],
                CultureInfo.GetCultureInfo("it-IT"), DateTimeStyles.None, out var local)) return local;
        throw new FormatException("data non valida: usa ISO oppure giorno/mese/anno");
    }

    private static decimal ParseAmount(string value)
    {
        var normalized = value.Trim().Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var amount) && decimal.Round(Math.Abs(amount), 2) > 0)
            return Math.Abs(amount);
        throw new FormatException("importo non valido: usa virgola o punto decimale, senza separatori delle migliaia");
    }

    private static TransactionType ParseType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "entrata" or "income" => TransactionType.Income,
        "uscita" or "expense" => TransactionType.Expense,
        _ => throw new DomainValidationException($"tipo movimento non riconosciuto: {value}")
    };

    private static bool TryGetSheetType(string sheetName, out TransactionType type)
    {
        if (sheetName.Equals("Income", StringComparison.OrdinalIgnoreCase)
            || sheetName.Equals("Entrate", StringComparison.OrdinalIgnoreCase))
        {
            type = TransactionType.Income;
            return true;
        }

        if (sheetName.Equals("Expenses", StringComparison.OrdinalIgnoreCase)
            || sheetName.Equals("Uscite", StringComparison.OrdinalIgnoreCase))
        {
            type = TransactionType.Expense;
            return true;
        }

        type = default;
        return false;
    }

    private static TransactionType? InferTypeFromFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (name.Contains("income", StringComparison.OrdinalIgnoreCase)
            || name.Contains("entrate", StringComparison.OrdinalIgnoreCase))
        {
            return TransactionType.Income;
        }

        if (name.Contains("expense", StringComparison.OrdinalIgnoreCase)
            || name.Contains("uscite", StringComparison.OrdinalIgnoreCase))
        {
            return TransactionType.Expense;
        }

        return null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Get(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> headers,
        string primary,
        string? secondary = null,
        bool optional = false)
    {
        if (!headers.TryGetValue(primary, out var index)
            && (secondary is null || !headers.TryGetValue(secondary, out index)))
        {
            if (optional)
            {
                return string.Empty;
            }

            throw new DomainValidationException($"colonna {primary} mancante");
        }

        return index < values.Count ? values[index] : string.Empty;
    }

    private static List<List<string>> ParseDelimited(string content, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (quoted && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }

                continue;
            }

            if (!quoted && character == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (!quoted && (character == '\r' || character == '\n'))
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    rows.Add(row);
                }

                row = [];
                continue;
            }

            field.Append(character);
        }

        row.Add(field.ToString());
        if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            rows.Add(row);
        }

        return rows;
    }
}
