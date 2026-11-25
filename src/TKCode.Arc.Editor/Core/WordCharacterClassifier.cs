// Ported from vscode (src/vs/editor/common/core/wordCharacterClassifier.ts, commit b57d5c9e), MIT license.
// Not ported: the Intl.Segmenter word lookup (intlSegmenterLocales); .NET has no word segmenter to stand in for it yet.

namespace TKCode.Arc.Editor;

internal enum WordCharacterClass : byte
{
    Regular = 0,
    Whitespace = 1,
    WordSeparator = 2
}

/// <summary>Classifies characters as regular, whitespace or one of a set of word separators. Immutable once built.</summary>
internal sealed class WordCharacterClassifier
{
    private const int CacheLimit = 10;
    private static readonly Dictionary<string, WordCharacterClassifier> s_cache = new();
    private static readonly SyncLock s_cacheLock = new();

    private readonly CharacterClassifier _classifier = new((byte)WordCharacterClass.Regular);

    public WordCharacterClassifier(string wordSeparators)
    {
        foreach (var ch in wordSeparators)
        {
            _classifier.Set(ch, (byte)WordCharacterClass.WordSeparator);
        }
        _classifier.Set(' ', (byte)WordCharacterClass.Whitespace);
        _classifier.Set('\t', (byte)WordCharacterClass.Whitespace);
    }

    public WordCharacterClass Get(int charCode) => (WordCharacterClass)_classifier.Get(charCode);

    /// <summary>The classifier for a set of word separators; a few recent ones are kept (upstream's LRU of 10).</summary>
    public static WordCharacterClassifier GetMapForWordSeparators(string wordSeparators)
    {
        lock (s_cacheLock)
        {
            if (s_cache.TryGetValue(wordSeparators, out var result))
            {
                return result;
            }
            if (s_cache.Count >= CacheLimit)
            {
                s_cache.Clear();
            }
            result = new WordCharacterClassifier(wordSeparators);
            s_cache[wordSeparators] = result;
            return result;
        }
    }
}
