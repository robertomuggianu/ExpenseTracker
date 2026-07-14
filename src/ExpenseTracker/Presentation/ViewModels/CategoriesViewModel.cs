using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class CategoriesViewModel(CategoryService categoryService) : ViewModelBase
{
    public ObservableCollection<MacroCategoryGroup> Groups { get; } = [];

    public ObservableCollection<MacroCategory> MacroCategories { get; } = [];

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Required;

    [ObservableProperty]
    public partial bool IsMacroEditorVisible { get; set; }

    [ObservableProperty]
    public partial Guid? EditingMacroId { get; set; }

    [ObservableProperty]
    public partial string MacroName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial TransactionTypeOption SelectedMacroType { get; set; } = TransactionTypeOption.Required[1];

    [ObservableProperty]
    public partial string MacroColor { get; set; } = "#E2383F";

    [ObservableProperty]
    public partial bool MacroIsActive { get; set; } = true;

    [ObservableProperty]
    public partial bool IsCategoryEditorVisible { get; set; }

    [ObservableProperty]
    public partial Guid? EditingCategoryId { get; set; }

    [ObservableProperty]
    public partial MacroCategory? SelectedParentMacro { get; set; }

    [ObservableProperty]
    public partial string CategoryName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CategoryIsActive { get; set; } = true;

    [RelayCommand]
    public Task LoadAsync() => RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        Groups.Clear();
        foreach (var item in await categoryService.GetGroupsAsync())
        {
            Groups.Add(item);
        }

        MacroCategories.Clear();
        foreach (var item in await categoryService.GetMacroCategoriesAsync())
        {
            MacroCategories.Add(item);
        }
    }

    [RelayCommand]
    private void NewMacro()
    {
        EditingMacroId = null;
        MacroName = string.Empty;
        SelectedMacroType = TransactionTypeOption.Required[1];
        MacroColor = "#E2383F";
        MacroIsActive = true;
        IsMacroEditorVisible = true;
        IsCategoryEditorVisible = false;
    }

    [RelayCommand]
    private void EditMacro(MacroCategoryGroup group)
    {
        var item = group.MacroCategory;
        EditingMacroId = item.Id;
        MacroName = item.Name;
        SelectedMacroType = Types.Single(option => option.Value == item.Type);
        MacroColor = item.Color;
        MacroIsActive = item.IsActive;
        IsMacroEditorVisible = true;
        IsCategoryEditorVisible = false;
    }

    [RelayCommand]
    private Task SaveMacroAsync() => RunAsync(async () =>
    {
        await categoryService.SaveMacroCategoryAsync(new MacroCategoryInput(
            EditingMacroId,
            MacroName,
            SelectedMacroType.Value!.Value,
            MacroColor,
            MacroIsActive));
        IsMacroEditorVisible = false;
        await LoadCoreAsync();
    });

    [RelayCommand]
    private void NewCategory(MacroCategoryGroup group)
    {
        EditingCategoryId = null;
        SelectedParentMacro = MacroCategories.Single(item => item.Id == group.MacroCategory.Id);
        CategoryName = string.Empty;
        CategoryIsActive = true;
        IsCategoryEditorVisible = true;
        IsMacroEditorVisible = false;
    }

    [RelayCommand]
    private void EditCategory(Category category)
    {
        EditingCategoryId = category.Id;
        SelectedParentMacro = MacroCategories.Single(item => item.Id == category.MacroCategoryId);
        CategoryName = category.Name;
        CategoryIsActive = category.IsActive;
        IsCategoryEditorVisible = true;
        IsMacroEditorVisible = false;
    }

    [RelayCommand]
    private Task SaveCategoryAsync() => RunAsync(async () =>
    {
        if (SelectedParentMacro is null)
        {
            throw new DomainValidationException("Seleziona una macro-categoria.");
        }

        await categoryService.SaveCategoryAsync(new CategoryInput(
            EditingCategoryId,
            SelectedParentMacro.Id,
            CategoryName,
            CategoryIsActive));
        IsCategoryEditorVisible = false;
        await LoadCoreAsync();
    });
}
