// Ported from vscode (src/vs/editor/common/services/languagesRegistry.ts: LanguageIdCodec and NULL_LANGUAGE_ID,
// src/vs/editor/common/languages.ts: ILanguageIdCodec, src/vs/editor/common/languages/modesRegistry.ts:
// PLAINTEXT_LANGUAGE_ID, commit b57d5c9e), MIT license.
// Differences from upstream: the two module constants live in the static class LanguageIds.

using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>The language ids every codec knows.</summary>
internal static class LanguageIds
{
    /// <summary>The language of a model whose language is unknown.</summary>
    public const string NullLanguageId = "vs.editor.nullLanguage";

    /// <summary>Plain text.</summary>
    public const string PlaintextLanguageId = "plaintext";
}

/// <summary>Maps language id strings to the small numbers stored in token metadata and back.</summary>
internal interface ILanguageIdCodec
{
    LanguageId EncodeLanguageId(string languageId);

    string DecodeLanguageId(LanguageId languageId);
}

/// <summary>
/// Assigns a number to each registered language. It belongs to one language registry; nothing is shared between
/// instances.
/// </summary>
internal sealed class LanguageIdCodec : ILanguageIdCodec
{
    private int _nextLanguageId;
    private readonly List<string?> _languageIdToLanguage = new();
    private readonly Dictionary<string, int> _languageToLanguageId = new(StringComparer.Ordinal);

    public LanguageIdCodec()
    {
        Register(LanguageIds.NullLanguageId, (int)LanguageId.Null);
        Register(LanguageIds.PlaintextLanguageId, (int)LanguageId.PlainText);
        _nextLanguageId = 2;
    }

    private void Register(string language, int languageId)
    {
        while (_languageIdToLanguage.Count <= languageId)
        {
            _languageIdToLanguage.Add(null);
        }
        _languageIdToLanguage[languageId] = language;
        _languageToLanguageId[language] = languageId;
    }

    public void Register(string language)
    {
        if (_languageToLanguageId.ContainsKey(language))
        {
            return;
        }
        var languageId = _nextLanguageId++;
        Register(language, languageId);
    }

    public LanguageId EncodeLanguageId(string languageId)
    {
        return _languageToLanguageId.TryGetValue(languageId, out var result) ? (LanguageId)result : LanguageId.Null;
    }

    public string DecodeLanguageId(LanguageId languageId)
    {
        var index = (int)languageId;
        if (index >= 0 && index < _languageIdToLanguage.Count && _languageIdToLanguage[index] is { Length: > 0 } language)
        {
            return language;
        }
        return LanguageIds.NullLanguageId;
    }
}
