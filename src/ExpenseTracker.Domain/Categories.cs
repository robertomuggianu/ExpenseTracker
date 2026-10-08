namespace ExpenseTracker.Domain;

public sealed class Category
{
    public Category(
        Guid id,
        string name,
        TransactionType type,
        string color,
        bool isActive = true)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("L'identificativo della categoria è obbligatorio.");
        }

        Id = id;
        Update(name, type, color);
        IsActive = isActive;
    }

    public Guid Id { get; }

    public string Name { get; private set; } = string.Empty;

    public TransactionType Type { get; private set; }

    public string Color { get; private set; } = "#512BD4";

    public bool IsActive { get; private set; }

    public string TypeLabel => Type == TransactionType.Income ? "Entrata" : "Uscita";

    public void Update(string name, TransactionType type, string color)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Il nome della categoria è obbligatorio.");
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            throw new DomainValidationException("Il colore della categoria è obbligatorio.");
        }

        Name = name.Trim();
        Type = type;
        Color = color.Trim();
    }

    public void SetActive(bool value) => IsActive = value;
}
