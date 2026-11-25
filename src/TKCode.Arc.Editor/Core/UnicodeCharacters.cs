// Ported from vscode (src/vs/base/common/strings.ts: AmbiguousCharacters and InvisibleCharacters,
// commit b57d5c9e), MIT license.
// Differences: the data are int arrays (UnicodeCharacterData.Data.cs, generated) instead of JSON parsed at run time; the
// per-locale instances are memoized in a concurrent dictionary of immutable instances (upstream: an LRU cache of one).

using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace TKCode.Arc.Editor;

/// <summary>
/// The characters that can be confused with basic ASCII ones in a set of locales (upstream's AmbiguousCharacters).
/// Immutable.
/// </summary>
internal sealed class AmbiguousCharacters
{
    private static readonly Lazy<FrozenDictionary<string, int[]>> s_data = new(static () =>
        UnicodeCharacterData.AmbiguousRawData.ToFrozenDictionary(static e => e.Locale, static e => e.Pairs, StringComparer.Ordinal));

    private static readonly Lazy<string[]> s_locales = new(static () =>
        [.. UnicodeCharacterData.AmbiguousRawData.Select(static e => e.Locale).Where(static l => !l.StartsWith('_'))]);

    private static readonly ConcurrentDictionary<string, AmbiguousCharacters> s_cache = new(StringComparer.Ordinal);

    private readonly FrozenDictionary<int, int> _confusableDictionary;

    private AmbiguousCharacters(FrozenDictionary<int, int> confusableDictionary)
    {
        _confusableDictionary = confusableDictionary;
    }

    /// <summary>The instance for <paramref name="locales"/> (upstream's getInstance).</summary>
    public static AmbiguousCharacters GetInstance(IEnumerable<string> locales) =>
        s_cache.GetOrAdd(string.Join(',', locales), static localesStr => Create(localesStr));

    /// <summary>The locales with their own data (upstream's getLocales).</summary>
    public static IReadOnlyList<string> GetLocales() => s_locales.Value;

    private static AmbiguousCharacters Create(string localesStr)
    {
        var data = s_data.Value;
        var locales = localesStr.Split(',');

        var filteredLocales = locales.Where(l => !l.StartsWith('_') && data.ContainsKey(l)).ToList();
        if (filteredLocales.Count == 0)
        {
            filteredLocales = ["_default"];
        }

        Dictionary<int, int>? languageSpecificMap = null;
        foreach (var locale in filteredLocales)
        {
            var map = ArrayToMap(data[locale]);
            languageSpecificMap = IntersectMaps(languageSpecificMap, map);
        }

        var commonMap = ArrayToMap(data["_common"]);
        foreach (var (key, value) in languageSpecificMap!)
        {
            commonMap[key] = value;
        }

        return new AmbiguousCharacters(commonMap.ToFrozenDictionary());
    }

    private static Dictionary<int, int> ArrayToMap(int[] arr)
    {
        var result = new Dictionary<int, int>(arr.Length / 2);
        for (var i = 0; i < arr.Length; i += 2)
        {
            result[arr[i]] = arr[i + 1];
        }

        return result;
    }

    private static Dictionary<int, int> IntersectMaps(Dictionary<int, int>? map1, Dictionary<int, int> map2)
    {
        if (map1 is null)
        {
            return map2;
        }

        var result = new Dictionary<int, int>();
        foreach (var (key, value) in map1)
        {
            if (map2.ContainsKey(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    public bool IsAmbiguous(int codePoint) => _confusableDictionary.ContainsKey(codePoint);

    public bool ContainsAmbiguousCharacter(string str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            if (IsAmbiguous(UnicodeCharacterHelpers.CodePointAt(str, i)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The basic ASCII code point <paramref name="codePoint"/> can be confused with, or null (upstream's getPrimaryConfusable).</summary>
    public int? GetPrimaryConfusable(int codePoint) => _confusableDictionary.TryGetValue(codePoint, out var value) ? value : null;

    public IEnumerable<int> GetConfusableCodePoints() => _confusableDictionary.Keys;
}

/// <summary>Characters that draw nothing visible (upstream's InvisibleCharacters).</summary>
internal static class InvisibleCharacters
{
    private static readonly Lazy<FrozenSet<int>> s_data = new(static () => UnicodeCharacterData.InvisibleRawData.ToFrozenSet());

    public static bool IsInvisibleCharacter(int codePoint) => s_data.Value.Contains(codePoint);

    public static bool ContainsInvisibleCharacter(string str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            var codePoint = UnicodeCharacterHelpers.CodePointAt(str, i);
            if (IsInvisibleCharacter(codePoint) || codePoint == ' ')
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlySet<int> CodePoints => s_data.Value;
}

/// <summary>Code point helpers for the unicode tables.</summary>
internal static class UnicodeCharacterHelpers
{
    /// <summary>The code point at <paramref name="index"/>, as JavaScript's codePointAt.</summary>
    public static int CodePointAt(string str, int index)
    {
        var ch = str[index];
        if (char.IsHighSurrogate(ch) && index + 1 < str.Length && char.IsLowSurrogate(str[index + 1]))
        {
            return char.ConvertToUtf32(ch, str[index + 1]);
        }

        return ch;
    }
}
