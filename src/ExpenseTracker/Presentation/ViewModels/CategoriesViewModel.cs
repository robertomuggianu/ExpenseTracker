using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class CategoriesViewModel(CategoryService categoryService) : ViewModelBase
{
    private bool iconChosen;
    partial void OnCategoryNameChanged(string value)
    {
        if (EditingCategoryId is null && !iconChosen)
            SelectedIcon = Icons.Single(icon => icon.Key == CategoryAppearance.InferIcon(value));
    }
    public ObservableCollection<Category> Categories { get; } = [];
    public ObservableCollection<Category> VisibleCategories { get; } = [];
    public IReadOnlyList<CategoryTile> CategoryTiles { get; private set; } = [];
    [ObservableProperty] public partial bool IsIncomeSelected { get; set; }
    public bool IsExpenseSelected => !IsIncomeSelected;
    [RelayCommand] private void SelectExpenses() => IsIncomeSelected = false;
    [RelayCommand] private void SelectIncome() => IsIncomeSelected = true;
    partial void OnIsIncomeSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsExpenseSelected));
        ApplySearch();
    }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    public bool HasNoCategories => VisibleCategories.Count == 0;
    partial void OnSearchTextChanged(string value) => ApplySearch();
    private void ApplySearch()
    {
        VisibleCategories.Clear();
        var type = IsIncomeSelected ? TransactionType.Income : TransactionType.Expense;
        foreach (var category in Categories.Where(c => c.Type == type && c.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)))
            VisibleCategories.Add(category);
        CategoryTiles = VisibleCategories.Select(c => new CategoryTile(c)).Append(new CategoryTile(null)).ToArray();
        OnPropertyChanged(nameof(CategoryTiles));
        OnPropertyChanged(nameof(HasNoCategories));
    }

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
        ApplySearch();
    }

    [RelayCommand]
    private void NewCategory()
    {
        EditingCategoryId = null;
        iconChosen = false;
        CategoryName = string.Empty;
        SelectedCategoryType = Types.Single(option => option.Value == (IsIncomeSelected ? TransactionType.Income : TransactionType.Expense));
        CategoryColor = CategoryAppearance.SuggestColor(Categories.Select(item => item.Color));
        SelectedIcon = Icons.Single(icon => icon.Key == CategoryAppearance.InferIcon(CategoryName));
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
        IsIncomeSelected = SelectedCategoryType.Value == TransactionType.Income;
        await LoadCoreAsync();
    });

    [RelayCommand]
    private void SelectColor(string color) => CategoryColor = color;

    [RelayCommand]
    private void SelectIcon(CategoryIcon icon) { iconChosen = true; SelectedIcon = icon; }

    [RelayCommand]
    private void CancelEdit() => IsEditorVisible = false;
}

public sealed record CategoryTile(Category? Category)
{
    public bool IsCreate => Category is null;
    public bool IsCategory => Category is not null;
    public string Name => Category?.Name ?? "Crea";
    public string Color => Category?.Color ?? "#FFC83D";
    public string? IconSource => Category?.IconSource;
    public double Opacity => Category is { IsActive: false } ? 0.4 : 1;
    public string AccessibilityDescription => Category is { IsActive: false } ? $"{Name}, disattivata" : Name;
    public string AccessibilityHint => IsCreate ? "Crea una nuova categoria" : "Tocca per modificare la categoria";
}
