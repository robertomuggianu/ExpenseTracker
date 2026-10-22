using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class ChoiceLabelFormatterTests
{
    [Fact]
    public void SelectorLabels_AreAvailableWithoutReflection()
    {
        foreach (var type in TransactionTypeOption.Optional)
            Assert.Equal(type.Label, ChoiceLabelFormatter.Format(type, "Label", "Tipo"));
        foreach (var period in PeriodOption.All)
            Assert.Equal(period.Label, ChoiceLabelFormatter.Format(period, "Label", "Periodo"));
        Assert.Equal("Ogni mese", ChoiceLabelFormatter.Format(new FrequencyOption(RepeatFrequency.Monthly, "Ogni mese"), "Label", "Frequenza"));
        Assert.Equal("Tutti i conti", ChoiceLabelFormatter.Format(new AccountFilterOption(null, "Tutti i conti"), "Name", "Conto"));
        Assert.Equal("Tutte le categorie", ChoiceLabelFormatter.Format(new CategoryFilterOption(null, "Tutte le categorie"), "Name", "Categoria"));
        var account = new AccountSummary(new Account(Guid.NewGuid(), "Principale", 10m), 10m);
        Assert.Equal(account.DisplayName, ChoiceLabelFormatter.Format(account, "DisplayName", "Conto"));
        Assert.Equal("Stipendio", ChoiceLabelFormatter.Format(new Category(Guid.NewGuid(), "Stipendio", TransactionType.Income, "#123456"), "Name", "Categoria"));
        Assert.Equal("EUR", ChoiceLabelFormatter.Format("EUR", "", "Valuta"));
    }
}
