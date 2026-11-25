// Ported from vscode (src/vs/editor/common/services/languagesRegistry.ts and languageService.ts, the parts a host and the
// Monarch tokenizer use; commit b57d5c9e), MIT license.
//
// Differences:
// - The file associations (extensions, file names, first lines) are this registry's own tables; upstream registers them
//   in the process-wide languagesAssociations module. The lookup follows its order: an exact file name, then the
//   longest matching extension, then the first line.
// - No configuration files, icons, file name patterns (globs) or the language name sorting for the UI.
// - RequestBasicLanguageFeatures raises an event the host answers by loading the language's configuration (upstream:
//   onDidRequestBasicLanguageFeatures, which the standalone languages contribution handles).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.Standalone.Monarch;

namespace TKCode.Arc.Editor.Languages;

/// <summary>A language to register (upstream's ILanguageExtensionPoint).</summary>
internal sealed record LanguageExtensionPoint(string Id)
{
    public IReadOnlyList<string>? Extensions { get; init; }

    public IReadOnlyList<string>? Filenames { get; init; }

    /// <summary>The names people know the language by; the first is its display name. Empty: no name.</summary>
    public IReadOnlyList<string>? Aliases { get; init; }

    public IReadOnlyList<string>? Mimetypes { get; init; }

    /// <summary>A JavaScript regular expression for the first line of a file in this language.</summary>
    public string? FirstLine { get; init; }
}

/// <summary>The registered languages: ids, names, mime types and file associations (upstream's LanguagesRegistry).</summary>
internal sealed class LanguageService : IMonarchLanguageService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ResolvedLanguage> _languages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lowercaseNameMap = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _mimeTypesMap = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _filenames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Extension, string LanguageId)> _extensions = new();
    private readonly List<(Regex FirstLine, string LanguageId)> _firstLines = new();
    private readonly Emitter<string> _onDidRequestBasicLanguageFeatures = new();
    private readonly HashSet<string> _requestedBasicLanguageFeatures = new(StringComparer.Ordinal);

    public LanguageService(LanguageIdCodec? languageIdCodec = null)
    {
        Codec = languageIdCodec ?? new LanguageIdCodec();
        RegisterLanguage(new LanguageExtensionPoint(LanguageIds.PlaintextLanguageId)
        {
            Extensions = [".txt"],
            Aliases = ["Plain Text", "text"],
            Mimetypes = ["text/plain"],
        });
    }

    /// <summary>The codec that numbers the registered languages for token metadata.</summary>
    public LanguageIdCodec Codec { get; }

    ILanguageIdCodec IMonarchLanguageService.LanguageIdCodec => Codec;

    /// <summary>A language was asked for its basic features (configuration, tokenizer) the first time.</summary>
    public event Action<string> DidRequestBasicLanguageFeatures
    {
        add => _onDidRequestBasicLanguageFeatures.Add(value);
        remove => _onDidRequestBasicLanguageFeatures.Remove(value);
    }

    private sealed class ResolvedLanguage(string identifier)
    {
        public string Identifier { get; } = identifier;

        public string? Name { get; set; }

        public List<string> Mimetypes { get; } = new();

        public List<string> Aliases { get; } = new();

        public List<string> Extensions { get; } = new();

        public List<string> Filenames { get; } = new();
    }

    /// <summary>Registers a language, or adds to one that is registered.</summary>
    public IDisposable RegisterLanguage(LanguageExtensionPoint lang)
    {
        lock (_gate)
        {
            if (!_languages.TryGetValue(lang.Id, out var resolvedLanguage))
            {
                Codec.Register(lang.Id);
                resolvedLanguage = new ResolvedLanguage(lang.Id);
                _languages[lang.Id] = resolvedLanguage;
            }

            MergeLanguage(resolvedLanguage, lang);
            RebuildMaps();
        }

        // Upstream unregisters by re-registering the remaining definitions; a registered id stays known to the codec.
        return RegistrationDisposable.ToDisposable(() => { });
    }

    private void MergeLanguage(ResolvedLanguage resolvedLanguage, LanguageExtensionPoint lang)
    {
        var langId = lang.Id;

        string? primaryMime = null;
        if (lang.Mimetypes is { Count: > 0 } mimetypes)
        {
            resolvedLanguage.Mimetypes.AddRange(mimetypes);
            primaryMime = mimetypes[0];
        }

        if (primaryMime is null)
        {
            primaryMime = "text/x-" + langId;
            resolvedLanguage.Mimetypes.Add(primaryMime);
        }

        if (lang.Extensions is { } extensions)
        {
            resolvedLanguage.Extensions.AddRange(extensions);
            foreach (var extension in extensions)
            {
                _extensions.Add((extension, langId));
            }
        }

        if (lang.Filenames is { } filenames)
        {
            foreach (var filename in filenames)
            {
                _filenames[filename] = langId;
                resolvedLanguage.Filenames.Add(filename);
            }
        }

        if (!string.IsNullOrEmpty(lang.FirstLine))
        {
            var firstLineRegexStr = lang.FirstLine[0] == '^' ? lang.FirstLine : "^" + lang.FirstLine;
            try
            {
                var regex = new Regex(
                    Model.JsRegex.TranslateMonarch(firstLineRegexStr, unicode: false),
                    RegexOptions.ECMAScript | RegexOptions.CultureInvariant,
                    Model.JsRegex.MatchTimeout);
                _firstLines.Add((regex, langId));
            }
            catch (ArgumentException)
            {
                // Most likely, the regex was bad
            }
        }

        resolvedLanguage.Aliases.Add(langId);

        IReadOnlyList<string?>? langAliases = null;
        if (lang.Aliases is { } aliases)
        {
            // an empty list signals that this language should not get a name
            langAliases = aliases.Count == 0 ? new string?[] { null } : aliases.Cast<string?>().ToArray();
        }

        if (langAliases is not null)
        {
            foreach (var langAlias in langAliases)
            {
                if (!string.IsNullOrEmpty(langAlias))
                {
                    resolvedLanguage.Aliases.Add(langAlias);
                }
            }
        }

        var containsAliases = langAliases is { Count: > 0 };
        if (containsAliases && langAliases![0] is null)
        {
            // signal that this language should not get a name
        }
        else
        {
            var bestName = (containsAliases ? langAliases![0] : null) ?? langId;
            if (containsAliases || resolvedLanguage.Name is null)
            {
                resolvedLanguage.Name = bestName;
            }
        }
    }

    private void RebuildMaps()
    {
        _lowercaseNameMap.Clear();
        _mimeTypesMap.Clear();
        foreach (var language in _languages.Values)
        {
            foreach (var alias in language.Aliases)
            {
                _lowercaseNameMap[alias.ToLowerInvariant()] = language.Identifier;
            }

            foreach (var mimetype in language.Mimetypes)
            {
                _mimeTypesMap[mimetype] = language.Identifier;
            }
        }
    }

    public bool IsRegisteredLanguageId(string? languageId)
    {
        if (string.IsNullOrEmpty(languageId))
        {
            return false;
        }

        lock (_gate)
        {
            return _languages.ContainsKey(languageId);
        }
    }

    public IReadOnlyList<string> GetRegisteredLanguageIds()
    {
        lock (_gate)
        {
            return [.. _languages.Keys];
        }
    }

    /// <summary>The display name of a language (its first alias), or null.</summary>
    public string? GetLanguageName(string languageId)
    {
        lock (_gate)
        {
            return _languages.TryGetValue(languageId, out var language) ? language.Name : null;
        }
    }

    public IReadOnlyList<string> GetExtensions(string languageId)
    {
        lock (_gate)
        {
            return _languages.TryGetValue(languageId, out var language) ? [.. language.Extensions] : [];
        }
    }

    public string? GetLanguageIdByLanguageName(string languageName)
    {
        lock (_gate)
        {
            return _lowercaseNameMap.GetValueOrDefault(languageName.ToLowerInvariant());
        }
    }

    public string? GetLanguageIdByMimeType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
        {
            return null;
        }

        lock (_gate)
        {
            return _mimeTypesMap.GetValueOrDefault(mimeType);
        }
    }

    /// <summary>
    /// The language of a file by its name (an exact file name first, then the longest extension that ends the name, case
    /// insensitive) or else by its first line; null when nothing matches.
    /// </summary>
    public string? GuessLanguageIdByFilepathOrFirstLine(string? path, string? firstLine = null)
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var name = System.IO.Path.GetFileName(path);
                if (_filenames.TryGetValue(name, out var byName))
                {
                    return byName;
                }

                string? best = null;
                var bestLength = 0;
                foreach (var (extension, languageId) in _extensions)
                {
                    if (extension.Length > bestLength && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        best = languageId;
                        bestLength = extension.Length;
                    }
                }

                if (best is not null)
                {
                    return best;
                }
            }

            if (!string.IsNullOrEmpty(firstLine))
            {
                foreach (var (regex, languageId) in _firstLines)
                {
                    if (regex.IsMatch(firstLine))
                    {
                        return languageId;
                    }
                }
            }

            return null;
        }
    }

    public void RequestBasicLanguageFeatures(string languageId)
    {
        bool first;
        lock (_gate)
        {
            first = _requestedBasicLanguageFeatures.Add(languageId);
        }

        if (first)
        {
            _onDidRequestBasicLanguageFeatures.Fire(languageId);
        }
    }
}
