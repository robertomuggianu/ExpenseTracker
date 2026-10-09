using CommunityToolkit.Mvvm.Input;
namespace ExpenseTracker.Presentation.ViewModels;
public abstract partial class ViewModelBase
{
    [RelayCommand]
    private void OpenMenu() => Shell.Current.FlyoutIsPresented = true;

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

}
