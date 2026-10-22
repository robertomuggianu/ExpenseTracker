using System.Globalization;
using System.Text;

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
        new("fuel", "Carburante"), new("plane", "Voli"), new("hotel", "Hotel"), new("beach", "Vacanze"), new("luggage", "Viaggi"),
        new("shirt", "Abbigliamento"), new("shoe", "Scarpe"), new("sport", "Palestra"), new("ball", "Sport"), new("bike", "Bicicletta"),
        new("pet", "Animali"), new("phone", "Telefono"), new("computer", "Tecnologia"), new("wifi", "Internet"),
        new("movie", "Cinema"), new("music", "Musica"), new("game", "Videogiochi"), new("beauty", "Cura personale"),
        new("medical", "Farmacia"), new("insurance", "Assicurazione"), new("tax", "Tasse"), new("savings", "Risparmio"),
        new("bank", "Banca"), new("transfer", "Trasferimenti"), new("baby", "Bambini"), new("cigarette", "Tabacco"),
        new("parking", "Parcheggio"), new("groceries", "Alimentari"), new("jewelry", "Gioielli"),
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
        "cafe" or "caffè" => "coffee", "benzina" or "carburante" or "fuel" => "fuel", "spese per macchina" or "auto" => "car",
        "trasporto pubblico" or "transportation" or "trasporti" => "bus",
        "affitto" or "home" or "casa" => "home", "bollette" => "bolt", "health" or "salute" => "heart",
        "gift" or "gifts" or "regali" => "gift", "leisure" or "svago" or "uscite" => "ticket",
        "education" or "studio" => "book", "paycheck" or "retribuzione" or "stipendio" => "work",
        "manutenzione" or "strumenti" => "tools", "subscription" or "abbonamenti" => "repeat",
        "trasferimento ricevuto" => "wallet", "trasferimenti" or "transfers" => "transfer",
        "voli" or "flights" => "plane", "hotel" or "albergo" => "hotel", "vacanze" or "holidays" => "beach", "viaggi" or "travel" => "luggage",
        "abbigliamento" or "vestiti" or "clothing" or "shopping" => "shirt", "scarpe" or "shoes" => "shoe",
        "palestra" or "gym" or "fitness" => "sport", "sport" => "ball", "bicicletta" or "bike" => "bike",
        "animali" or "pets" => "pet", "telefono" or "phone" => "phone", "tecnologia" or "technology" => "computer",
        "internet" => "wifi", "cinema" or "movies" => "movie", "musica" or "music" => "music", "videogiochi" or "gaming" => "game",
        "cura personale" or "parrucchiere" or "personal care" => "beauty", "farmacia" or "medicine" => "medical",
        "assicurazione" or "insurance" => "insurance", "tasse" or "taxes" => "tax", "risparmio" or "savings" => "savings",
        "banca" or "bank" => "bank", "bambini" or "children" => "baby", "tabacco" or "sigarette" => "cigarette",
        "parcheggio" or "parking" => "parking", "alimentari" => "groceries", "gioielli" or "jewelry" => "jewelry", _ => InferFromWords(name)
    };

    // Match whole words/phrases, longest first (e.g. "assicurazione auto" before "auto").
    // A wallet represents money when the name has no more specific meaning.
    private static string InferFromWords(string name)
    {
        var normalized = $" {Normalize(name)} ";
        return IconAliases.FirstOrDefault(item => normalized.Contains($" {item.Alias} ", StringComparison.Ordinal))?.Icon ?? "wallet";
    }

    private sealed record IconAlias(string Alias, string Icon);
    private static readonly IconAlias[] IconAliases = BuildAliases();

    private static IconAlias[] BuildAliases()
    {
        (string Icon, string Names)[] groups =
        [
            ("insurance", "assicurazione auto|assicurazione casa|assicurazione|assicurazioni|insurance|car insurance"),
            ("tax", "bollo auto|tassa|tasse|tax|taxes|imposte|tributi"),
            ("tools", "manutenzione auto|riparazioni auto|manutenzione|riparazioni|meccanico|strumenti|tools|maintenance"),
            ("work", "stipendio|stipendi|retribuzione|paycheck|salary|salario|bonus|lavoro|freelance|pensione|pension"),
            ("bank", "interessi|interests|dividendi|dividends|investimenti|investments|banca|bank|commissioni bancarie"),
            ("transfer", "rimborso|rimborsi|refund|refunds|trasferimento|trasferimenti|transfer|transfers|bonifico"),
            ("gift", "regalo|regali|gift|gifts|donazioni|donations"),
            ("cigarette", "vape|svapo|sigarette|sigaretta elettronica|tabacco|tobacco|smoking"),
            ("home", "affitto|mutuo|rent|mortgage|casa|home|arredamento"),
            ("bolt", "bollette|utenze|elettricita|luce|gas|acqua|utilities|bills"),
            ("food", "ristorante|ristoranti|restaurant|restaurants|pranzo|cena|pizza|pizzeria|delivery|takeaway"),
            ("coffee", "cafe|caffe|coffee|bar|colazione"),
            ("cart", "spesa|groceries|supermercato|supermarket"),
            ("groceries", "alimentari|alimentazione|panetteria"),
            ("fuel", "benzina|carburante|fuel|diesel|gpl"),
            ("car", "auto|macchina|automobile|car"),
            ("bus", "trasporto pubblico|trasporti|transportation|treno|train|metro|bus|taxi"),
            ("parking", "parcheggio|parking|pedaggio|autostrada"),
            ("plane", "volo|voli|flight|flights|aereo"),
            ("hotel", "hotel|albergo|alloggio"),
            ("beach", "vacanza|vacanze|holiday|holidays"),
            ("luggage", "viaggio|viaggi|travel"),
            ("medical", "farmacia|farmaci|medicine|pharmacy|dentista|dental|visite mediche"),
            ("heart", "salute|health|medico|doctor"),
            ("book", "studio|education|scuola|school|universita|libri|books|corsi"),
            ("shirt", "abbigliamento|vestiti|clothing|shopping"),
            ("shoe", "scarpe|shoes"),
            ("sport", "palestra|gym|fitness"),
            ("ball", "sport|calcio|tennis"),
            ("bike", "bicicletta|bici|bike|cycling"),
            ("pet", "animali|pets|pet|veterinario|cane|gatto"),
            ("phone", "telefono|phone|cellulare|telefonia|ricarica telefonica"),
            ("computer", "tecnologia|technology|informatica|elettronica|computer"),
            ("wifi", "internet|wifi|fibra"),
            ("movie", "abbonamento netflix|cinema|movies|netflix"),
            ("music", "abbonamento spotify|musica|music|spotify|concerti"),
            ("game", "videogiochi|gaming|games"),
            ("beauty", "cura personale|personal care|parrucchiere|barbiere|estetista|cosmetici"),
            ("repeat", "abbonamento|abbonamenti|subscription|subscriptions"),
            ("savings", "risparmio|risparmi|savings"),
            ("baby", "bambini|children|baby|asilo"),
            ("jewelry", "gioielli|jewelry"),
            ("ticket", "svago|leisure|uscite|divertimento|entertainment")
        ];
        return groups.SelectMany(group => group.Names.Split('|').Select(alias => new IconAlias(Normalize(alias), group.Icon)))
            .OrderByDescending(item => item.Alias.Length).ToArray();
    }

    private static string Normalize(string value)
    {
        var text = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            text.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }
        return string.Join(' ', text.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
