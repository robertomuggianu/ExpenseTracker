using System.IO.Compression;
using System.Text;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Application.Tests;

public sealed class TransactionImportTests
{
    [Fact]
    public async Task CsvImport_ReadsNativeExpenseTrackerExport()
    {
        const string csv = "Id;Data;Tipo;Conto;Categoria;Descrizione;Importo;Valuta;Note\n"
                           + "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa;2026-07-14T18:30:00Z;Uscita;Conto principale;Groceries;Spesa;25.78;EUR;Settimanale";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var parsed = await new TransactionImportParser().ParseAsync(stream, "movimenti.csv");

        var row = Assert.Single(parsed.Transactions);
        Assert.Equal("Groceries", row.CategoryName);
        Assert.Equal(25.78m, row.Amount);
        Assert.Equal(ExpenseTracker.Domain.TransactionType.Expense, row.Type);
    }

    [Fact]
    public async Task XlsxImport_ImportsIncomeExpensesAndTransferPair_AndIsIdempotent()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"expense-import-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repository = new SqliteExpenseRepository(databasePath);
            var service = new TransactionImportService(repository, new TransactionImportParser());
            var workbook = CreateWorkbook();

            await using var firstStream = new MemoryStream(workbook);
            var first = await service.ImportAsync(firstStream, "backup.xlsx");
            Assert.Equal(4, first.Created);
            Assert.Equal(0, first.Updated);
            Assert.Equal(0, first.SkippedTransfers);
            Assert.Equal(2, first.AccountsCreated);

            await using var secondStream = new MemoryStream(workbook);
            var second = await service.ImportAsync(secondStream, "backup.xlsx");
            Assert.Equal(0, second.Created);
            Assert.Equal(4, second.Updated);
            Assert.Equal(4, (await repository.GetTransactionsAsync()).Count);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static byte[] CreateWorkbook()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "xl/workbook.xml",
                """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Expenses" sheetId="1" r:id="rId1" />
                    <sheet name="Income" sheetId="2" r:id="rId2" />
                    <sheet name="Transfers" sheetId="3" r:id="rId3" />
                  </sheets>
                </workbook>
                """);
            Write(archive, "xl/_rels/workbook.xml.rels",
                """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Target="worksheets/sheet1.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" />
                  <Relationship Id="rId2" Target="worksheets/sheet2.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" />
                  <Relationship Id="rId3" Target="worksheets/sheet3.xml" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" />
                </Relationships>
                """);
            Write(archive, "xl/worksheets/sheet1.xml", Sheet("Groceries", 25.78m, "Spesa settimanale"));
            Write(archive, "xl/worksheets/sheet2.xml", Sheet("Paycheck", 1200m, "Stipendio"));
            Write(archive, "xl/worksheets/sheet3.xml",
                """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
                  <row r="1"><c r="A1" t="inlineStr"><is><t>transfers list</t></is></c></row>
                  <row r="2"><c r="A2" t="inlineStr"><is><t>Date and time</t></is></c></row>
                  <row r="3">
                    <c r="A3"><v>46081</v></c>
                    <c r="B3" t="inlineStr"><is><t>Deposit</t></is></c>
                    <c r="C3" t="inlineStr"><is><t>Roberto Muggianu</t></is></c>
                    <c r="D3"><v>180</v></c>
                    <c r="E3" t="inlineStr"><is><t>EUR</t></is></c>
                  </row>
                </sheetData></worksheet>
                """);
        }

        return output.ToArray();
    }

    private static string Sheet(string category, decimal amount, string comment) =>
        $$"""
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
          <row r="1"><c r="A1" t="inlineStr"><is><t>list</t></is></c></row>
          <row r="2"><c r="A2" t="inlineStr"><is><t>Date and time</t></is></c></row>
          <row r="3">
            <c r="A3"><v>46204</v></c>
            <c r="B3" t="inlineStr"><is><t>{{category}}</t></is></c>
            <c r="C3" t="inlineStr"><is><t>Roberto Muggianu</t></is></c>
            <c r="D3"><v>{{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}</v></c>
            <c r="E3" t="inlineStr"><is><t>EUR</t></is></c>
            <c r="I3" t="inlineStr"><is><t>{{comment}}</t></is></c>
          </row>
        </sheetData></worksheet>
        """;

    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
