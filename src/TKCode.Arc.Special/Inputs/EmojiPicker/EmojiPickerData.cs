using System.Globalization;

namespace TKCode.Arc;

/// <summary>One emoji of the table: the default form, its English name, search words and skin tone forms.</summary>
internal sealed class EmojiPickerEmoji
{
    public EmojiPickerEmoji(string emoji, string name, string[] words, int nameWordCount, double version, double toneVersion,
        string[]? tones, EmojiCategory category, int order)
    {
        NameWordCount = nameWordCount;
        Emoji = emoji;
        Name = name;
        Words = words;
        Version = version;
        ToneVersion = toneVersion;
        Tones = tones;
        Category = category;
        Order = order;
    }

    /// <summary>The fully qualified emoji without a skin tone.</summary>
    public string Emoji { get; }

    /// <summary>The Unicode short name ("grinning face").</summary>
    public string Name { get; }

    /// <summary>The words of the name first, then the CLDR keywords, lower case.</summary>
    public string[] Words { get; }

    /// <summary>How many of <see cref="Words"/> come from the name.</summary>
    public int NameWordCount { get; }

    /// <summary>The Unicode emoji version that introduced it.</summary>
    public double Version { get; }

    /// <summary>The emoji version of its skin tone forms (0 without tones).</summary>
    public double ToneVersion { get; }

    /// <summary>The five skin tone forms, light to dark; null when it has none.</summary>
    public string[]? Tones { get; }

    public EmojiCategory Category { get; }

    /// <summary>Its position in the whole table (CLDR order).</summary>
    public int Order { get; }

    /// <summary>The form to show for <paramref name="tone"/> (the default form when it has no tones, or they are too new).</summary>
    public string Display(EmojiSkinTone tone, double maxVersion)
        => tone == EmojiSkinTone.Default || Tones is null || ToneVersion > maxVersion ? Emoji : Tones[(int)tone - 1];
}

/// <summary>
/// The emoji table of <see cref="EmojiPicker"/>, read once from the package's resource: Unicode emoji-test.txt (order,
/// groups, names, versions, skin tones) and the English CLDR annotations (keywords), both under the Unicode License v3.
/// </summary>
internal static class EmojiPickerData
{
    internal const string ResourceName = "TKCode.Arc.Special.EmojiPicker.Emoji";

    private static readonly Lazy<Table> s_table = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Every emoji in CLDR order.</summary>
    public static IReadOnlyList<EmojiPickerEmoji> All => s_table.Value.All;

    /// <summary>Finds the emoji of any form (with or without a skin tone, with or without the variation selector).</summary>
    public static EmojiPickerEmoji? Find(string emoji)
    {
        var map = s_table.Value.ByForm;
        if (map.TryGetValue(emoji, out var hit))
        {
            return hit;
        }
        return map.GetValueOrDefault(emoji.Replace("️", string.Empty));
    }

    private sealed record Table(IReadOnlyList<EmojiPickerEmoji> All, Dictionary<string, EmojiPickerEmoji> ByForm);

    private static Table Load()
    {
        using var stream = typeof(EmojiPickerData).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException("The emoji table resource is missing from TKCode.Arc.Special.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return Parse(reader);
    }

    internal static IReadOnlyList<EmojiPickerEmoji> ParseForTests(TextReader reader) => Parse(reader).All;

    private static Table Parse(TextReader reader)
    {
        var all = new List<EmojiPickerEmoji>(2048);
        var byForm = new Dictionary<string, EmojiPickerEmoji>(StringComparer.Ordinal);
        var category = EmojiCategory.Smileys;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            if (line[0] == '@')
            {
                category = CategoryOf(line.AsSpan(1));
                continue;
            }
            var fields = line.Split('\t');
            if (fields.Length < 6)
            {
                continue;
            }
            var name = fields[1];
            var words = new List<string>();
            foreach (var word in name.ToLowerInvariant().Split([' ', ':', ',', '-', '.', '!', '’', '\'', '(', ')', '&'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!words.Contains(word))
                {
                    words.Add(word);
                }
            }
            var nameWordCount = words.Count;
            foreach (var word in fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!words.Contains(word))
                {
                    words.Add(word);
                }
            }
            var version = double.Parse(fields[3], CultureInfo.InvariantCulture);
            var toneVersion = fields[4].Length > 0 ? double.Parse(fields[4], CultureInfo.InvariantCulture) : 0;
            var tones = fields[5].Length > 0 ? fields[5].Split(' ') : null;
            if (tones is { Length: not 5 })
            {
                tones = null;
            }
            var entry = new EmojiPickerEmoji(fields[0], name, words.ToArray(), nameWordCount, version, toneVersion, tones, category, all.Count);
            all.Add(entry);
            byForm.TryAdd(entry.Emoji, entry);
            byForm.TryAdd(entry.Emoji.Replace("️", string.Empty), entry);
            if (tones is not null)
            {
                foreach (var tone in tones)
                {
                    byForm.TryAdd(tone, entry);
                }
            }
        }
        return new Table(all, byForm);
    }

    private static EmojiCategory CategoryOf(ReadOnlySpan<char> key) => key switch
    {
        "smileys" => EmojiCategory.Smileys,
        "people" => EmojiCategory.People,
        "nature" => EmojiCategory.Nature,
        "food" => EmojiCategory.Food,
        "travel" => EmojiCategory.Travel,
        "activities" => EmojiCategory.Activities,
        "objects" => EmojiCategory.Objects,
        "symbols" => EmojiCategory.Symbols,
        _ => EmojiCategory.Flags
    };

    /// <summary>
    /// The emoji that match every word of <paramref name="query"/>, best first: a word that starts the name, then one
    /// that starts a word of the name, then one that starts a keyword; ties keep the table's order.
    /// </summary>
    public static List<EmojiPickerEmoji> Search(string query, IReadOnlyList<EmojiPickerEmoji> source, double maxVersion)
    {
        var terms = query.ToLowerInvariant().Split([' ', ',', ':'], StringSplitOptions.RemoveEmptyEntries);
        var hits = new List<(EmojiPickerEmoji Emoji, int Score)>();
        if (terms.Length == 0)
        {
            return [];
        }
        foreach (var emoji in source)
        {
            if (emoji.Version > maxVersion)
            {
                continue;
            }
            var total = 0;
            var matched = true;
            foreach (var term in terms)
            {
                var score = Score(emoji, term);
                if (score < 0)
                {
                    matched = false;
                    break;
                }
                total += score;
            }
            if (matched)
            {
                hits.Add((emoji, total));
            }
        }
        // A stable sort: equal scores keep the table's order.
        return hits.OrderBy(h => h.Score).ThenBy(h => h.Emoji.Order).Select(h => h.Emoji).ToList();
    }

    /// <summary>0: the name starts with the term; 1: a word of the name does; 2: a keyword does; -1: no match.</summary>
    private static int Score(EmojiPickerEmoji emoji, string term)
    {
        if (emoji.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        var nameWords = emoji.NameWordCount;
        var best = -1;
        for (var i = 0; i < emoji.Words.Length; i++)
        {
            if (emoji.Words[i].StartsWith(term, StringComparison.Ordinal))
            {
                var score = i < nameWords ? 1 : 2;
                if (best < 0 || score < best)
                {
                    best = score;
                }
            }
        }
        return best;
    }
}
