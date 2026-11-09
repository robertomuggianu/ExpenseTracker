using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed class CategoryService(IExpenseRepository repository, PlanningService? planningService = null)
{
    private readonly PlanningService planning = planningService ?? new(repository);

    public Task<CategoryDeletionImpact> GetDeletionImpactAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetCategoryDeletionImpactAsync(id, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? replacementId, CategoryDeletionImpact confirmedImpact,
        CancellationToken cancellationToken = default) =>
        planning.DeleteCategoryAsync(id, replacementId, confirmedImpact, cancellationToken);

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
            : new Category(Guid.NewGuid(), input.Name, input.Type, input.Color, icon: input.Icon);

        if (input.Id.HasValue)
        {
            if (item.Type != input.Type && (await repository.GetCategoryDeletionImpactAsync(item.Id, cancellationToken)).RequiresReplacement)
                throw new DomainValidationException("Non puoi cambiare il tipo di una categoria usata da movimenti, ricorrenze o budget.");

            item.Update(input.Name, input.Type, input.Color, input.Icon);
        }

        item.SetActive(input.IsActive);
        await repository.SaveCategoryAsync(item, cancellationToken);
        return item;
    }
}
