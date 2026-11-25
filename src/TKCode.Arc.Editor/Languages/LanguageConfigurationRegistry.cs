// Ported from vscode (src/vs/editor/common/languages/languageConfigurationRegistry.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - no dependency injection and no IConfigurationService: LanguageConfigurationService leaves out the
//    `editor.language.brackets` and `editor.language.colorizedBracketPairs` settings, and a language without a
//    configuration resolves to an empty one (upstream asks ILanguageService whether the language is registered and
//    resolves to an empty configuration either way);
//  - the onDidChange events are .NET events named DidChange over an Emitter, as TextModel's are;
//  - the module functions getIndentationAtPosition and combineLanguageConfigurations are in the static class
//    LanguageConfigurations; getIndentationAtPosition takes the TextModel;
//  - `a || b` on the members of a configuration becomes `a ?? b`, except for AutoCloseBefore, where an empty string
//    is falsy upstream and so does not override.
// ResolvedLanguageConfiguration is in ResolvedLanguageConfiguration.cs.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Raised when the configuration of a language (or of every language, when the id is null) changed.</summary>
internal sealed class LanguageConfigurationServiceChangeEvent
{
    public LanguageConfigurationServiceChangeEvent(string? languageId)
    {
        LanguageId = languageId;
    }

    public string? LanguageId { get; }

    public bool Affects(string languageId)
    {
        return LanguageId is null || LanguageId == languageId;
    }
}

/// <summary>Resolves the configuration of each language from the configurations registered for it.</summary>
internal sealed class LanguageConfigurationService : IDisposable
{
    private readonly LanguageConfigurationRegistry _registry = new();
    private readonly Emitter<LanguageConfigurationServiceChangeEvent> _onDidChangeEmitter = new();
    private readonly Dictionary<string, ResolvedLanguageConfiguration> _configurations = new(StringComparer.Ordinal);

    public LanguageConfigurationService()
    {
        _registry.DidChange += e =>
        {
            _configurations.Remove(e.LanguageId);
            _onDidChangeEmitter.Fire(new LanguageConfigurationServiceChangeEvent(e.LanguageId));
        };
    }

    public event Action<LanguageConfigurationServiceChangeEvent>? DidChange
    {
        add
        {
            if (value is not null)
            {
                _onDidChangeEmitter.Add(value);
            }
        }
        remove => _onDidChangeEmitter.Remove(value);
    }

    /// <param name="languageId">The language.</param>
    /// <param name="configuration">The configuration to add.</param>
    /// <param name="priority">Use a higher number for higher priority.</param>
    public IDisposable Register(string languageId, LanguageConfiguration configuration, int priority = 0)
    {
        return _registry.Register(languageId, configuration, priority);
    }

    public ResolvedLanguageConfiguration GetLanguageConfiguration(string languageId)
    {
        if (!_configurations.TryGetValue(languageId, out var result))
        {
            result = ComputeConfig(languageId, _registry);
            _configurations[languageId] = result;
        }
        return result;
    }

    private static ResolvedLanguageConfiguration ComputeConfig(string languageId, LanguageConfigurationRegistry registry)
    {
        // Upstream combines the registered configuration with the user's bracket settings here.
        return registry.GetLanguageConfiguration(languageId)
            ?? new ResolvedLanguageConfiguration(languageId, new LanguageConfiguration());
    }

    public void Dispose()
    {
        _onDidChangeEmitter.Clear();
        _registry.Dispose();
    }
}

/// <summary>The module functions of languageConfigurationRegistry.ts.</summary>
internal static class LanguageConfigurations
{
    /// <summary>The leading whitespace of a line, cut at <paramref name="column"/>.</summary>
    public static string GetIndentationAtPosition(TextModel model, int lineNumber, int column)
    {
        var lineText = model.GetLineContent(lineNumber);
        var indentation = GetLeadingWhitespace(lineText);
        if (indentation.Length > column - 1)
        {
            indentation = indentation.Substring(0, Math.Max(0, column - 1));
        }
        return indentation;
    }

    /// <summary>Combines configurations; a member set in a later one wins.</summary>
    public static LanguageConfiguration CombineLanguageConfigurations(IEnumerable<LanguageConfiguration> configs)
    {
        var result = new LanguageConfiguration();
        foreach (var entry in configs)
        {
            result = new LanguageConfiguration
            {
                Comments = entry.Comments ?? result.Comments,
                Brackets = entry.Brackets ?? result.Brackets,
                WordPattern = entry.WordPattern ?? result.WordPattern,
                IndentationRules = entry.IndentationRules ?? result.IndentationRules,
                OnEnterRules = entry.OnEnterRules ?? result.OnEnterRules,
                AutoClosingPairs = entry.AutoClosingPairs ?? result.AutoClosingPairs,
                SurroundingPairs = entry.SurroundingPairs ?? result.SurroundingPairs,
                AutoCloseBefore = string.IsNullOrEmpty(entry.AutoCloseBefore) ? result.AutoCloseBefore : entry.AutoCloseBefore,
                Folding = entry.Folding ?? result.Folding,
                ColorizedBracketPairs = entry.ColorizedBracketPairs ?? result.ColorizedBracketPairs,
                ElectricCharacterSupport = entry.ElectricCharacterSupport ?? result.ElectricCharacterSupport,
            };
        }

        return result;
    }

    // strings.getLeadingWhitespace
    private static string GetLeadingWhitespace(string str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            var chCode = str[i];
            if (chCode != ' ' && chCode != '\t')
            {
                return str.Substring(0, i);
            }
        }
        return str;
    }
}

/// <summary>The configurations registered for one language, combined by priority.</summary>
internal sealed class ComposedLanguageConfiguration
{
    private readonly List<LanguageConfigurationContribution> _entries;
    private int _order;
    private ResolvedLanguageConfiguration? _resolved;

    public ComposedLanguageConfiguration(string languageId)
    {
        LanguageId = languageId;
        _entries = new List<LanguageConfigurationContribution>();
        _order = 0;
        _resolved = null;
    }

    public string LanguageId { get; }

    public IDisposable Register(LanguageConfiguration configuration, int priority)
    {
        var entry = new LanguageConfigurationContribution(configuration, priority, ++_order);
        _entries.Add(entry);
        _resolved = null;
        return new CallbackDisposable(() =>
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] == entry)
                {
                    _entries.RemoveAt(i);
                    _resolved = null;
                    break;
                }
            }
        });
    }

    public ResolvedLanguageConfiguration? GetResolvedConfiguration()
    {
        if (_resolved is null)
        {
            var config = Resolve();
            if (config is not null)
            {
                _resolved = new ResolvedLanguageConfiguration(LanguageId, config);
            }
        }
        return _resolved;
    }

    private LanguageConfiguration? Resolve()
    {
        if (_entries.Count == 0)
        {
            return null;
        }
        // List.Sort is not stable, but no two entries compare equal: the order is unique.
        _entries.Sort(LanguageConfigurationContribution.Cmp);
        return LanguageConfigurations.CombineLanguageConfigurations(_entries.Select(e => e.Configuration));
    }
}

/// <summary>One registered configuration with its priority and registration order.</summary>
internal sealed class LanguageConfigurationContribution
{
    public LanguageConfigurationContribution(LanguageConfiguration configuration, int priority, int order)
    {
        Configuration = configuration;
        Priority = priority;
        Order = order;
    }

    public LanguageConfiguration Configuration { get; }

    public int Priority { get; }

    public int Order { get; }

    public static int Cmp(LanguageConfigurationContribution a, LanguageConfigurationContribution b)
    {
        if (a.Priority == b.Priority)
        {
            // higher order last
            return a.Order - b.Order;
        }
        // higher priority last
        return a.Priority - b.Priority;
    }
}

/// <summary>Raised when a configuration of a language was registered or removed.</summary>
internal sealed class LanguageConfigurationChangeEvent
{
    public LanguageConfigurationChangeEvent(string languageId)
    {
        LanguageId = languageId;
    }

    public string LanguageId { get; }
}

/// <summary>The language configurations registered for each language id.</summary>
internal sealed class LanguageConfigurationRegistry : IDisposable
{
    private readonly Dictionary<string, ComposedLanguageConfiguration> _entries = new(StringComparer.Ordinal);
    private readonly Emitter<LanguageConfigurationChangeEvent> _onDidChange = new();
    private readonly IDisposable _plaintextRegistration;

    public LanguageConfigurationRegistry()
    {
        _plaintextRegistration = Register(LanguageIds.PlaintextLanguageId, new LanguageConfiguration
        {
            Brackets =
            [
                ("(", ")"),
                ("[", "]"),
                ("{", "}"),
            ],
            SurroundingPairs =
            [
                new AutoClosingPair("{", "}"),
                new AutoClosingPair("[", "]"),
                new AutoClosingPair("(", ")"),
                new AutoClosingPair("<", ">"),
                new AutoClosingPair("\"", "\""),
                new AutoClosingPair("'", "'"),
                new AutoClosingPair("`", "`"),
            ],
            ColorizedBracketPairs = [],
            Folding = new FoldingRules
            {
                OffSide = true,
            },
        }, 0);
    }

    public event Action<LanguageConfigurationChangeEvent>? DidChange
    {
        add
        {
            if (value is not null)
            {
                _onDidChange.Add(value);
            }
        }
        remove => _onDidChange.Remove(value);
    }

    /// <param name="languageId">The language.</param>
    /// <param name="configuration">The configuration to add.</param>
    /// <param name="priority">Use a higher number for higher priority.</param>
    public IDisposable Register(string languageId, LanguageConfiguration configuration, int priority = 0)
    {
        if (!_entries.TryGetValue(languageId, out var entries))
        {
            entries = new ComposedLanguageConfiguration(languageId);
            _entries[languageId] = entries;
        }

        var disposable = entries.Register(configuration, priority);
        _onDidChange.Fire(new LanguageConfigurationChangeEvent(languageId));

        return new CallbackDisposable(() =>
        {
            disposable.Dispose();
            _onDidChange.Fire(new LanguageConfigurationChangeEvent(languageId));
        });
    }

    public ResolvedLanguageConfiguration? GetLanguageConfiguration(string languageId)
    {
        return _entries.TryGetValue(languageId, out var entries) ? entries.GetResolvedConfiguration() : null;
    }

    public void Dispose()
    {
        _onDidChange.Clear();
        _plaintextRegistration.Dispose();
    }
}

/// <summary>Runs an action on the first Dispose (upstream's <c>toDisposable</c>).</summary>
internal sealed class CallbackDisposable : IDisposable
{
    private Action? _callback;

    public CallbackDisposable(Action callback)
    {
        _callback = callback;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _callback, null)?.Invoke();
    }
}
