using System.Reflection;
using System.Text;

namespace Innovation.Core;

public static class CardDataLoader
{
    private const int ExpectedCardCount = 105;

    static CardDataLoader()
    {
        // cards.tsv is stored in Windows-1252. On .NET 5+ the BCL only
        // ships UTF-8 / ASCII / a couple of Unicode flavors out of the
        // box; every legacy single-byte code page (including 1252) has
        // to be registered explicitly or Encoding.GetEncoding(1252)
        // throws NotSupportedException. Registering once at type init
        // guarantees both Load* entry points are safe under any host
        // (WPF, WinForms, xUnit runner, etc.).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static IReadOnlyList<Card> LoadFromEmbeddedResource()
    {
        var asm = typeof(CardDataLoader).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("cards.tsv", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}");
        using var reader = new StreamReader(stream, Encoding.GetEncoding(1252));
        return Parse(reader.ReadToEnd());
    }

    public static IReadOnlyList<Card> LoadFromFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = Encoding.GetEncoding(1252).GetString(bytes);
        return Parse(text);
    }

    public static IReadOnlyList<Card> Parse(string text)
    {
        var rows = TsvParser.Parse(text);
        var cards = new List<Card>(ExpectedCardCount);

        foreach (var row in rows)
        {
            if (row.Count < 13) continue;
            if (!int.TryParse(row[0], out var id)) continue;
            if (!int.TryParse(row[1], out var age)) continue;

            var color = ParseColor(row[2]);
            var title = row[3];
            var (top, left, middle, right, hexSlot) = ParseIconPositions(row[4], row[5], row[6], row[7]);
            var hex = row[8];
            var dogmaIcon = ParseIcon(row[9]);
            var effects = new List<string>(3);
            for (int i = 10; i < row.Count && i <= 12; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                    effects.Add(row[i]);
            }

            // Internal IDs are 0-indexed to match the original VB6 representation
            // (e.g. Agriculture is card 0). The TSV itself uses 1-based row IDs.
            cards.Add(new Card(
                Id: id - 1,
                Age: age,
                Color: color,
                Title: title,
                Top: top,
                Left: left,
                Middle: middle,
                Right: right,
                HexagonSlot: hexSlot,
                HexagonDescription: hex,
                DogmaIcon: dogmaIcon,
                DogmaEffects: effects));
        }

        if (cards.Count != ExpectedCardCount)
            throw new InvalidDataException(
                $"Expected {ExpectedCardCount} cards, parsed {cards.Count}.");
        return cards;
    }

    private static CardColor ParseColor(string s) => s.Trim().ToLowerInvariant() switch
    {
        "blue" => CardColor.Blue,
        "red" => CardColor.Red,
        "yellow" => CardColor.Yellow,
        "green" => CardColor.Green,
        "purple" => CardColor.Purple,
        _ => throw new InvalidDataException($"Unknown color: {s}"),
    };

    private static Icon ParseIcon(string s)
    {
        var t = s.Trim();
        if (t.Length == 0 || t.Equals("x", StringComparison.OrdinalIgnoreCase))
            return Icon.None;
        // Tolerate the "LIghtbulb" typo on card 43 (Perspective).
        return t.ToLowerInvariant() switch
        {
            "castle" => Icon.Castle,
            "leaf" => Icon.Leaf,
            "lightbulb" => Icon.Lightbulb,
            "crown" => Icon.Crown,
            "factory" => Icon.Factory,
            "clock" => Icon.Clock,
            _ => throw new InvalidDataException($"Unknown icon: {s}"),
        };
    }

    private static (Icon top, Icon left, Icon middle, Icon right, IconSlot hexSlot)
        ParseIconPositions(string top, string left, string middle, string right)
    {
        IconSlot? slot = null;
        IconSlot CheckX(string s, IconSlot candidate)
        {
            if (s.Trim().Equals("x", StringComparison.OrdinalIgnoreCase))
            {
                if (slot is not null)
                    throw new InvalidDataException("Multiple 'x' markers in icon row");
                slot = candidate;
            }
            return candidate;
        }
        CheckX(top, IconSlot.Top);
        CheckX(left, IconSlot.Left);
        CheckX(middle, IconSlot.Middle);
        CheckX(right, IconSlot.Right);

        if (slot is null)
            throw new InvalidDataException("No 'x' hexagon marker in icon row");

        return (ParseIcon(top), ParseIcon(left), ParseIcon(middle), ParseIcon(right), slot.Value);
    }
}
