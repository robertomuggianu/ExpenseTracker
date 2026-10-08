using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class CategoriesViewModel(CategoryService categoryService) : ViewModelBase
{
    public ObservableCollection<Category> Categories { get; } = [];

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Required;
    public IReadOnlyList<CategoryIcon> Icons => CategoryAppearance.Icons;
    public IReadOnlyList<string> Palette => CategoryAppearance.Palette;

    [ObservableProperty]
    public partial CategoryIcon SelectedIcon { get; set; } = CategoryAppearance.Icons[^1];

    [ObservableProperty]
    public partial bool IsEditorVisible { get; set; }

    [ObservableProperty]
    public partial Guid? EditingCategoryId { get; set; }

    [ObservableProperty]
    public partial string CategoryName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial TransactionTypeOption SelectedCategoryType { get; set; } = TransactionTypeOption.Required[1];

    [ObservableProperty]
    public partial string CategoryColor { get; set; } = "#E2383F";

    [ObservableProperty]
    public partial bool CategoryIsActive { get; set; } = true;

    [RelayCommand]
    public Task LoadAsync() => RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        Categories.Clear();
        foreach (var item in await categoryService.GetCategoriesAsync())
        {
            Categories.Add(item);
        }
    }

    [RelayCommand]
    private void NewCategory()
    {
        EditingCategoryId = null;
        CategoryName = string.Empty;
        SelectedCategoryType = Types[1];
        CategoryColor = CategoryAppearance.SuggestColor(Categories.Select(item => item.Color));
        SelectedIcon = Icons[^1];
        CategoryIsActive = true;
        IsEditorVisible = true;
    }

    [RelayCommand]
    private void EditCategory(Category category)
    {
        EditingCategoryId = category.Id;
        CategoryName = category.Name;
        SelectedCategoryType = Types.Single(option => option.Value == category.Type);
        CategoryColor = category.Color;
        SelectedIcon = Icons.Single(item => item.Key == category.Icon);
        CategoryIsActive = category.IsActive;
        IsEditorVisible = true;
    }

    [RelayCommand]
    private Task SaveCategoryAsync() => RunAsync(async () =>
    {
        await categoryService.SaveCategoryAsync(new CategoryInput(
            EditingCategoryId,
            CategoryName,
            SelectedCategoryType.Value!.Value,
            CategoryColor,
            CategoryIsActive,
            SelectedIcon.Key));
        IsEditorVisible = false;
        await LoadCoreAsync();
    });

    [RelayCommand]
    private void SelectColor(string color) => CategoryColor = color;

    [RelayCommand]
    private void SelectIcon(CategoryIcon icon) => SelectedIcon = icon;

    [RelayCommand]
    private void CancelEdit() => IsEditorVisible = false;
}
