namespace ExpenseTracker.Domain;

public sealed class Category
{
    public Category(
        Guid id,
        string name,
        TransactionType type,
        string color,
        bool isActive = true,
        string? icon = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("L'identificativo della categoria è obbligatorio.");
        }

        Id = id;
        Update(name, type, color, icon ?? CategoryAppearance.InferIcon(name));
        IsActive = isActive;
    }

    public Guid Id { get; }

    public string Name { get; private set; } = string.Empty;

    public TransactionType Type { get; private set; }

    public string Color { get; private set; } = "#512BD4";

    public bool IsActive { get; private set; }

    public string Icon { get; private set; } = "tag";

    public string IconSource => $"category_{Icon}.png";

    public string TypeLabel => Type == TransactionType.Income ? "Entrata" : "Uscita";

    public void Update(string name, TransactionType type, string color, string? icon = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Il nome della categoria è obbligatorio.");
        }

        if (string.IsNullOrWhiteSpace(color) || color.Length != 7 || color[0] != '#'
            || !color.AsSpan(1).ToArray().All(Uri.IsHexDigit))
        {
            throw new DomainValidationException("Scegli un colore valido dalla palette.");
        }

        if (icon is not null && !CategoryAppearance.Icons.Any(item => item.Key == icon))
            throw new DomainValidationException("Scegli un'icona valida.");

        Name = name.Trim();
        Type = type;
        Color = color.Trim();
        if (icon is not null) Icon = icon;
    }

    public void SetActive(bool value) => IsActive = value;
}
