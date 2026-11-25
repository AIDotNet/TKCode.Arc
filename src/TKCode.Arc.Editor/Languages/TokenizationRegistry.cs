// Ported from vscode (src/vs/editor/common/tokenizationRegistry.ts, commit b57d5c9e), MIT license.
//
// Differences:
// - An instance, not upstream's global TokenizationRegistry: an app (or one editor) owns one and hands it to the models
//   and tokenizers that use it, so that two hosts in one process do not share tokenizers or colors (AGENTS.md: no static
//   mutable state shared between buffers).
// - The maps are read under a lock, because a background tokenizer may ask for an embedded language's tokenizer off the
//   UI thread; the change event fires on the thread that made the change.

using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>The tokenizers by language id, the factories of lazy ones, and the color map of the token colors.</summary>
internal sealed class TokenizationRegistry<TSupport>
    where TSupport : class
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TSupport> _tokenizationSupports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FactoryData> _factories = new(StringComparer.Ordinal);
    private readonly Emitter<TokenizationSupportChangedEvent> _onDidChange = new();
    private RgbaColor[]? _colorMap;

    /// <summary>A tokenizer is registered, unregistered or changed, or the color map changed.</summary>
    public event Action<TokenizationSupportChangedEvent> OnDidChange
    {
        add => _onDidChange.Add(value);
        remove => _onDidChange.Remove(value);
    }

    /// <summary>Fires a change event for languages (useful for languages that embed other languages).</summary>
    public void HandleChange(IReadOnlyList<string> languageIds) =>
        _onDidChange.Fire(new TokenizationSupportChangedEvent(languageIds, false));

    public IDisposable Register(string languageId, TSupport support)
    {
        lock (_gate)
        {
            _tokenizationSupports[languageId] = support;
        }

        HandleChange([languageId]);
        return RegistrationDisposable.ToDisposable(() =>
        {
            lock (_gate)
            {
                if (!_tokenizationSupports.TryGetValue(languageId, out var current) || !ReferenceEquals(current, support))
                {
                    return;
                }

                _tokenizationSupports.Remove(languageId);
            }

            HandleChange([languageId]);
        });
    }

    public TSupport? Get(string languageId)
    {
        lock (_gate)
        {
            return _tokenizationSupports.GetValueOrDefault(languageId);
        }
    }

    public IDisposable RegisterFactory(string languageId, ILazyTokenizationSupport<TSupport> factory)
    {
        FactoryData myData;
        lock (_gate)
        {
            if (_factories.TryGetValue(languageId, out var previous))
            {
                previous.Dispose();
            }

            myData = new FactoryData(this, languageId, factory);
            _factories[languageId] = myData;
        }

        return RegistrationDisposable.ToDisposable(() =>
        {
            lock (_gate)
            {
                if (!_factories.TryGetValue(languageId, out var v) || !ReferenceEquals(v, myData))
                {
                    return;
                }

                _factories.Remove(languageId);
            }

            myData.Dispose();
        });
    }

    /// <summary>The tokenizer of a language, creating it from its factory first if needed; null when there is none.</summary>
    public async Task<TSupport?> GetOrCreateAsync(string languageId)
    {
        // check first if the support is already set
        if (Get(languageId) is { } tokenizationSupport)
        {
            return tokenizationSupport;
        }

        FactoryData? factory;
        lock (_gate)
        {
            _factories.TryGetValue(languageId, out factory);
        }

        if (factory is null || factory.IsResolved)
        {
            // no factory or factory.resolve already finished
            return null;
        }

        await factory.ResolveAsync().ConfigureAwait(false);
        return Get(languageId);
    }

    /// <summary>False while a factory of the language is still pending.</summary>
    public bool IsResolved(string languageId)
    {
        if (Get(languageId) is not null)
        {
            return true;
        }

        lock (_gate)
        {
            return !_factories.TryGetValue(languageId, out var factory) || factory.IsResolved;
        }
    }

    /// <summary>Sets the colors that the foreground and background color ids of all tokens index.</summary>
    public void SetColorMap(RgbaColor[] colorMap)
    {
        string[] languages;
        lock (_gate)
        {
            _colorMap = colorMap;
            languages = [.. _tokenizationSupports.Keys];
        }

        _onDidChange.Fire(new TokenizationSupportChangedEvent(languages, true));
    }

    public RgbaColor[]? GetColorMap()
    {
        lock (_gate)
        {
            return _colorMap;
        }
    }

    public RgbaColor? GetDefaultBackground()
    {
        var colorMap = GetColorMap();
        if (colorMap is not null && colorMap.Length > (int)ColorId.DefaultBackground)
        {
            return colorMap[(int)ColorId.DefaultBackground];
        }

        return null;
    }

    private sealed class FactoryData(TokenizationRegistry<TSupport> registry, string languageId, ILazyTokenizationSupport<TSupport> factory) : IDisposable
    {
        private readonly object _gate = new();
        private bool _isDisposed;
        private Task? _resolveTask;
        private IDisposable? _registration;

        public bool IsResolved { get; private set; }

        public void Dispose()
        {
            IDisposable? registration;
            lock (_gate)
            {
                _isDisposed = true;
                registration = _registration;
                _registration = null;
            }

            registration?.Dispose();
        }

        public Task ResolveAsync()
        {
            lock (_gate)
            {
                return _resolveTask ??= CreateAsync();
            }
        }

        private async Task CreateAsync()
        {
            var value = await factory.GetTokenizationSupportAsync().ConfigureAwait(false);
            IsResolved = true;
            if (value is not null)
            {
                lock (_gate)
                {
                    if (_isDisposed)
                    {
                        return;
                    }
                }

                var registration = registry.Register(languageId, value);
                lock (_gate)
                {
                    if (_isDisposed)
                    {
                        registration.Dispose();
                    }
                    else
                    {
                        _registration = registration;
                    }
                }
            }
        }
    }
}
