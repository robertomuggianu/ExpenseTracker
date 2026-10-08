namespace ExpenseTracker.Domain;

public sealed record CategoryIcon(string Key, string Label)
{
    public string ImageSource => $"category_{Key}.png";
}

public static class CategoryAppearance
{
    public static IReadOnlyList<CategoryIcon> Icons { get; } =
    [
        new("cart", "Spesa"), new("food", "Ristoranti"), new("coffee", "Caffè"),
        new("car", "Auto"), new("bus", "Trasporti"), new("home", "Casa"),
        new("bolt", "Bollette"), new("heart", "Salute"), new("gift", "Regali"),
        new("ticket", "Svago"), new("book", "Studio"), new("work", "Stipendio"),
        new("tools", "Manutenzione"), new("repeat", "Abbonamenti"), new("wallet", "Portafoglio"),
        new("tag", "Altro")
    ];

    public static IReadOnlyList<string> Palette { get; } =
    [
        "#A78BFA", "#34D399", "#FB923C", "#38BDF8", "#F472B6", "#FBBF24",
        "#2DD4BF", "#818CF8", "#FB7185", "#A3E635", "#C084FC", "#22D3EE",
        "#F97316", "#4ADE80", "#60A5FA", "#E879F9", "#FACC15", "#14B8A6",
        "#6366F1", "#F43F5E", "#84CC16", "#D946EF", "#06B6D4", "#EA580C",
        "#10B981", "#3B82F6", "#EC4899", "#EAB308", "#8B5CF6", "#0EA5E9",
        "#D97706", "#7DD3FC"
    ];

    public static string SuggestColor(IEnumerable<string> usedColors) =>
        Palette.FirstOrDefault(color => !usedColors.Contains(color, StringComparer.OrdinalIgnoreCase)) ?? Palette[0];

    public static string InferIcon(string name) => name.Trim().ToLowerInvariant() switch
    {
        "spesa" or "groceries" => "cart", "ristoranti" or "restaurants" => "food",
        "cafe" or "caffè" => "coffee", "benzina" or "carburante" or "spese per macchina" => "car",
        "trasporto pubblico" or "transportation" or "trasporti" => "bus",
        "affitto" or "home" or "casa" => "home", "bollette" => "bolt", "health" or "salute" => "heart",
        "gift" or "gifts" or "regali" => "gift", "leisure" or "svago" or "uscite" => "ticket",
        "education" or "studio" => "book", "paycheck" or "retribuzione" or "stipendio" => "work",
        "manutenzione" or "strumenti" => "tools", "subscription" or "abbonamenti" => "repeat",
        "trasferimento ricevuto" => "wallet", _ => "tag"
    };
}
