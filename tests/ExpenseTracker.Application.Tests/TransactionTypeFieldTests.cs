using CommunityToolkit.Mvvm.ComponentModel;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Controls;
using ExpenseTracker.Presentation.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace ExpenseTracker.Application.Tests;

public sealed class TransactionTypeFieldTests : IDisposable
{
    private readonly IDispatcherProvider previousDispatcher = DispatcherProvider.Current;
    public TransactionTypeFieldTests() => DispatcherProvider.SetCurrent(new TestDispatcher());
    public void Dispose() => DispatcherProvider.SetCurrent(previousDispatcher);

    [Fact]
    public void ClickingTypeButtons_UpdatesTwoWayBindingAndSelection()
    {
        var model = new SelectionModel();
        var field = new TransactionTypeField { BindingContext = model };
        field.SetBinding(TransactionTypeField.SelectedItemProperty, new Binding(nameof(SelectionModel.SelectedType)));
        var income = field.Children.OfType<Button>().Single(b => b.Text == "Entrata");
        var expense = field.Children.OfType<Button>().Single(b => b.Text == "Uscita");

        ((IButtonController)income).SendClicked();
        Assert.Equal(TransactionType.Income, model.SelectedType.Value);
        Assert.Equal("Entrata, selezionata", SemanticProperties.GetDescription(income));
        Assert.Equal("Uscita", SemanticProperties.GetDescription(expense));

        ((IButtonController)expense).SendClicked();
        Assert.Equal(TransactionType.Expense, model.SelectedType.Value);
        Assert.Equal("Uscita, selezionata", SemanticProperties.GetDescription(expense));

        model.SelectedType = TransactionTypeOption.Required[0];
        Assert.Equal(TransactionType.Income, field.SelectedItem.Value);
        Assert.Equal("Entrata, selezionata", SemanticProperties.GetDescription(income));
    }

    private sealed class SelectionModel : ObservableObject
    {
        private TransactionTypeOption selectedType = TransactionTypeOption.Required[1];
        public TransactionTypeOption SelectedType
        {
            get => selectedType;
            set => SetProperty(ref selectedType, value);
        }
    }

    // The real controls/binding engine run synchronously; no native window is needed.
    private sealed class TestDispatcher : IDispatcherProvider, IDispatcher
    {
        public IDispatcher GetForCurrentThread() => this;
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) => throw new NotSupportedException();
        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }
}
