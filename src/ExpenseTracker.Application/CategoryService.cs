using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed class CategoryService(IExpenseRepository repository)
{
    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var categories = await repository.GetCategoriesAsync(cancellationToken);
        return categories
            .Where(item => !activeOnly || item.IsActive)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Name)
            .ToArray();
    }

    public async Task<Category> SaveCategoryAsync(
        CategoryInput input,
        CancellationToken cancellationToken = default)
    {
        var item = input.Id.HasValue
            ? await repository.GetCategoryAsync(input.Id.Value, cancellationToken)
                ?? throw new DomainValidationException("La categoria da modificare non esiste.")
            : new Category(Guid.NewGuid(), input.Name, input.Type, input.Color);

        if (input.Id.HasValue)
        {
            if (item.Type != input.Type)
            {
                var hasHistoricalTransactions = (await repository.GetTransactionsAsync(cancellationToken: cancellationToken))
                    .Any(transaction => transaction.CategoryId == item.Id);
                if (hasHistoricalTransactions)
                {
                    throw new DomainValidationException("Non puoi cambiare il tipo di una categoria già usata nei movimenti.");
                }
            }

            item.Update(input.Name, input.Type, input.Color);
        }

        item.SetActive(input.IsActive);
        await repository.SaveCategoryAsync(item, cancellationToken);
        return item;
    }
}
