using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Styling;

namespace TKCode.Arc;

/// <summary>
/// One theme file (<c>Themes/XxxTheme.axaml</c>) as the generator describes it: an id, the keys it defines, and a factory
/// that instantiates the compiled dictionary (no XAML parsing or URI loading at run time).
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArcThemeAsset
{
    /// <summary>Creates the description. Called by generated code.</summary>
    /// <param name="id">The asset id: <c>{assembly}:{path in the project}</c>.</param>
    /// <param name="keys">The keys of the dictionary's top-level resources (<see cref="Type"/> for <c>{x:Type …}</c>, or strings).</param>
    /// <param name="load">Creates the compiled dictionary.</param>
    public ArcThemeAsset(string id, object[] keys, Func<ResourceDictionary> load)
    {
        Id = id;
        Keys = keys;
        Load = load;
    }

    /// <summary>The asset id: <c>{assembly}:{path in the project}</c>.</summary>
    public string Id { get; }

    /// <summary>The keys of the dictionary's top-level resources.</summary>
    public IReadOnlyList<object> Keys { get; }

    /// <summary>Creates the compiled dictionary.</summary>
    public Func<ResourceDictionary> Load { get; }
}

/// <summary>Event data of <see cref="ArcThemeProvider.AssetLoaded"/>.</summary>
public sealed class ArcThemeAssetLoadedEventArgs : EventArgs
{
    internal ArcThemeAssetLoadedEventArgs(ArcThemeAsset asset, object? trigger, TimeSpan duration)
    {
        Asset = asset;
        Trigger = trigger;
        Duration = duration;
    }

    /// <summary>The asset that was loaded.</summary>
    public ArcThemeAsset Asset { get; }

    /// <summary>The resource key whose lookup loaded it (null for <see cref="ArcThemeProvider.LoadAll"/>).</summary>
    public object? Trigger { get; }

    /// <summary>Time spent creating the dictionary.</summary>
    public TimeSpan Duration { get; }
}

/// <summary>
/// Resource provider that loads control-theme dictionaries on demand: it knows every key of every registered theme file
/// (from the generated table) and instantiates a file's dictionary the first time one of its keys is looked up, for
/// example when the first control of that type looks for its implicit <see cref="ControlTheme"/>.
/// </summary>
/// <remarks>
/// <para>
/// Sits in <see cref="Avalonia.Application.Resources"/>' merged dictionaries. <see cref="HasResources"/> is true as soon
/// as one asset is registered, so Avalonia always queries it; a lookup that does not hit a registered key is one
/// dictionary probe.
/// </para>
/// <para>
/// Loading does not raise ResourcesChanged: before the load the key was answered by this provider all the same (the
/// value is created, not changed), so nothing that resolved earlier can be stale. The loaded dictionaries get no owner
/// for the same reason (adding an owner notifies the whole tree).
/// </para>
/// </remarks>
public sealed class ArcThemeProvider : ResourceProvider
{
    private readonly Dictionary<object, Entry> _index = new();
    private readonly List<Entry> _entries = new();
    private int _loadedCount;

    /// <summary>Number of registered theme files.</summary>
    public int AssetCount => _entries.Count;

    /// <summary>Number of theme files loaded so far.</summary>
    public int LoadedCount => _loadedCount;

    /// <summary>Total time spent loading theme files.</summary>
    public TimeSpan TotalLoadDuration { get; private set; }

    /// <inheritdoc />
    public override bool HasResources => _entries.Count > 0;

    /// <summary>Raised after a theme file was loaded.</summary>
    public event EventHandler<ArcThemeAssetLoadedEventArgs>? AssetLoaded;

    /// <summary>Whether the theme file <paramref name="assetId"/> has been loaded.</summary>
    /// <param name="assetId">The asset id.</param>
    public bool IsLoaded(string assetId)
    {
        foreach (var entry in _entries)
        {
            if (entry.Asset.Id == assetId)
            {
                return entry.Dictionary is not null;
            }
        }
        return false;
    }

    /// <summary>Whether a registered theme file defines <paramref name="key"/>.</summary>
    /// <param name="key">The resource key.</param>
    public bool Defines(object key) => _index.ContainsKey(key);

    /// <summary>Loads every registered theme file now (eager mode, and for measurements).</summary>
    public void LoadAll()
    {
        foreach (var entry in _entries)
        {
            GetOrLoad(entry, null);
        }
    }

    /// <inheritdoc />
    public override bool TryGetResource(object key, ThemeVariant? theme, out object? value)
    {
        if (_index.TryGetValue(key, out var entry))
        {
            var dictionary = GetOrLoad(entry, key);
            if (dictionary is not null)
            {
                return dictionary.TryGetResource(key, theme, out value);
            }
        }
        value = null;
        return false;
    }

    internal void Add(ArcThemeAsset asset, Action? onFirstLoad)
    {
        var entry = new Entry(asset, onFirstLoad);
        foreach (var key in asset.Keys)
        {
            if (_index.TryGetValue(key, out var other))
            {
                throw new InvalidOperationException(
                    $"Theme key '{key}' is defined by both '{other.Asset.Id}' and '{asset.Id}'.");
            }
            _index.Add(key, entry);
        }
        _entries.Add(entry);
        if (_entries.Count == 1)
        {
            // HasResources just turned true.
            RaiseResourcesChanged();
        }
    }

    private ResourceDictionary? GetOrLoad(Entry entry, object? trigger)
    {
        if (entry.Dictionary is not null)
        {
            return entry.Dictionary;
        }
        if (entry.IsLoading)
        {
            // A resource of the file is looked up while the file itself is being built (for example a StaticResource
            // that Avalonia resolves upwards before the file's own dictionary is complete); let the lookup continue.
            return null;
        }
        entry.IsLoading = true;
        var watch = Stopwatch.StartNew();
        try
        {
            entry.Dictionary = entry.Asset.Load();
        }
        finally
        {
            entry.IsLoading = false;
        }
        var duration = watch.Elapsed;
        TotalLoadDuration += duration;
        _loadedCount++;
        entry.OnFirstLoad?.Invoke();
        AssetLoaded?.Invoke(this, new ArcThemeAssetLoadedEventArgs(entry.Asset, trigger, duration));
        return entry.Dictionary;
    }

    private sealed class Entry
    {
        public Entry(ArcThemeAsset asset, Action? onFirstLoad)
        {
            Asset = asset;
            OnFirstLoad = onFirstLoad;
        }

        public ArcThemeAsset Asset { get; }
        public Action? OnFirstLoad { get; }
        public ResourceDictionary? Dictionary { get; set; }
        public bool IsLoading { get; set; }
    }
}
