using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed class CategoryService(IExpenseRepository repository)
{
    public async Task<IReadOnlyList<MacroCategoryGroup>> GetGroupsAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var macroCategories = await repository.GetMacroCategoriesAsync(cancellationToken);
        var categories = await repository.GetCategoriesAsync(cancellationToken: cancellationToken);

        return macroCategories
            .Where(item => !activeOnly || item.IsActive)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Name)
            .Select(macro => new MacroCategoryGroup(
                macro,
                categories
                    .Where(category => category.MacroCategoryId == macro.Id && (!activeOnly || category.IsActive))
                    .OrderBy(category => category.Name)
                    .ToArray()))
            .ToArray();
    }

    public Task<IReadOnlyList<MacroCategory>> GetMacroCategoriesAsync(CancellationToken cancellationToken = default) =>
        repository.GetMacroCategoriesAsync(cancellationToken);

    public Task<IReadOnlyList<Category>> GetCategoriesAsync(
        Guid? macroCategoryId = null,
        CancellationToken cancellationToken = default) =>
        repository.GetCategoriesAsync(macroCategoryId, cancellationToken);

    public async Task<MacroCategory> SaveMacroCategoryAsync(
        MacroCategoryInput input,
        CancellationToken cancellationToken = default)
    {
        var item = input.Id.HasValue
            ? await repository.GetMacroCategoryAsync(input.Id.Value, cancellationToken)
                ?? throw new DomainValidationException("La macro-categoria da modificare non esiste.")
            : new MacroCategory(Guid.NewGuid(), input.Name, input.Type, input.Color);

        if (input.Id.HasValue)
        {
            if (item.Type != input.Type)
            {
                var categoryIds = (await repository.GetCategoriesAsync(item.Id, cancellationToken))
                    .Select(category => category.Id)
                    .ToHashSet();
                var hasHistoricalTransactions = (await repository.GetTransactionsAsync(cancellationToken: cancellationToken))
                    .Any(transaction => categoryIds.Contains(transaction.CategoryId));
                if (hasHistoricalTransactions)
                {
                    throw new DomainValidationException("Non puoi cambiare il tipo di una macro-categoria già usata nei movimenti.");
                }
            }

            item.Update(input.Name, input.Type, input.Color);
        }

        item.SetActive(input.IsActive);
        await repository.SaveMacroCategoryAsync(item, cancellationToken);
        return item;
    }

    public async Task<Category> SaveCategoryAsync(CategoryInput input, CancellationToken cancellationToken = default)
    {
        var macroCategory = await repository.GetMacroCategoryAsync(input.MacroCategoryId, cancellationToken)
            ?? throw new DomainValidationException("La macro-categoria selezionata non esiste.");

        if (!macroCategory.IsActive && input.IsActive)
        {
            throw new DomainValidationException("Non puoi attivare una categoria appartenente a una macro-categoria disattivata.");
        }

        var item = input.Id.HasValue
            ? await repository.GetCategoryAsync(input.Id.Value, cancellationToken)
                ?? throw new DomainValidationException("La categoria da modificare non esiste.")
            : new Category(Guid.NewGuid(), input.MacroCategoryId, input.Name);

        if (input.Id.HasValue)
        {
            if (item.MacroCategoryId != input.MacroCategoryId)
            {
                throw new DomainValidationException("La macro-categoria di una categoria esistente non può essere modificata.");
            }

            item.Update(input.Name);
        }

        item.SetActive(input.IsActive);
        await repository.SaveCategoryAsync(item, cancellationToken);
        return item;
    }
}
