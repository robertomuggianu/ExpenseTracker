using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace ExpenseTracker.Presentation.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    public AccountSelection AccountScope { get; }

    protected ViewModelBase(AccountSelection? accountSelection = null)
    {
        AccountScope = accountSelection ?? new AccountSelection();
        WeakReferenceMessenger.Default.Register<ViewModelBase, AccountSelectionChanged>(this, static (vm, message) =>
        {
            if (ReferenceEquals(vm.AccountScope, message.Selection)) vm.OnAccountScopeChanged();
        });
    }

    protected virtual void OnAccountScopeChanged() { }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await action();
        }
        catch (DomainValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Operazione non riuscita: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed record PeriodOption(DateRangePreset Value, string Label)
{
    public static IReadOnlyList<PeriodOption> All { get; } =
    [
        new(DateRangePreset.CurrentWeek, "Settimana corrente"),
        new(DateRangePreset.CurrentMonth, "Mese corrente"),
        new(DateRangePreset.CurrentYear, "Anno corrente"),
        new(DateRangePreset.Custom, "Periodo personalizzato"),
        new(DateRangePreset.SelectedMonth, "Mese scelto"),
        new(DateRangePreset.SelectedYear, "Anno scelto")
    ];
}

public sealed record TransactionTypeOption(TransactionType? Value, string Label)
{
    public static IReadOnlyList<TransactionTypeOption> Required { get; } =
    [
        new(TransactionType.Income, "Entrata"),
        new(TransactionType.Expense, "Uscita")
    ];

    public static IReadOnlyList<TransactionTypeOption> Optional { get; } =
    [
        new(null, "Tutti i tipi"),
        .. Required
    ];
}

public sealed record AccountFilterOption(Guid? Id, string Name, string IconSource = "category_wallet.png", string? Currency = null, bool IsPrimary = false);

public sealed record CategoryFilterOption(Guid? Id, string Name);
