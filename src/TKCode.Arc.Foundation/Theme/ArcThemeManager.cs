using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Event data of <see cref="ArcThemeManager.StateChanged"/>.</summary>
public sealed class ArcThemeChangedEventArgs : EventArgs
{
    internal ArcThemeChangedEventArgs(ArcThemeState oldState, ArcThemeState newState, bool wasDark, bool isDark)
    {
        OldState = oldState;
        NewState = newState;
        WasDark = wasDark;
        IsDark = isDark;
    }

    /// <summary>The state before the change.</summary>
    public ArcThemeState OldState { get; }

    /// <summary>The state now applied.</summary>
    public ArcThemeState NewState { get; }

    /// <summary>Whether the previous state resolved to dark.</summary>
    public bool WasDark { get; }

    /// <summary>Whether the new state resolves to dark.</summary>
    public bool IsDark { get; }
}

/// <summary>
/// Owns the theme of one application: the current <see cref="ArcThemeState"/>, the token dictionary published in
/// <see cref="Application.Resources"/>, and the switch between states. Created by <c>UseArc(...)</c>.
/// </summary>
public sealed class ArcThemeManager
{
    private readonly Application _application;
    private readonly ArcTokenDescriptor[] _descriptors;
    private readonly int _tokenCount;
    private IPlatformSettings? _platformSettings;

    internal ArcThemeManager(Application application, IReadOnlyList<ArcTokenDescriptor> descriptors)
    {
        _application = application;
        _descriptors = descriptors.ToArray();
        foreach (var descriptor in _descriptors)
        {
            _tokenCount += descriptor.Keys.Count;
        }
        State = ArcThemeState.Default;
    }

    /// <summary>The theme manager of <see cref="Application.Current"/>, or null before <c>UseArc(...)</c> set it up.</summary>
    public static ArcThemeManager? Current
        => Application.Current is { } application ? ArcRuntime.Find(application)?.Theme : null;

    /// <summary>The applied state.</summary>
    public ArcThemeState State { get; private set; }

    /// <summary>Whether the applied state resolves to dark.</summary>
    public bool IsDark { get; private set; }

    /// <summary>The registered token identities, global identity first.</summary>
    public IReadOnlyList<ArcTokenDescriptor> Descriptors => _descriptors;

    /// <summary>Total number of published tokens.</summary>
    public int TokenCount => _tokenCount;

    /// <summary>
    /// The dictionary the tokens are published into. It sits in <see cref="Application.Resources"/>' merged
    /// dictionaries for the whole lifetime of the application; a state change rewrites the values that changed.
    /// </summary>
    public ResourceDictionary Resources { get; } = new();

    /// <summary>Time the last <see cref="Apply(ArcThemeState)"/> spent computing tokens.</summary>
    public TimeSpan LastComputeDuration { get; private set; }

    /// <summary>Time the last <see cref="Apply(ArcThemeState)"/> spent writing them (including the change notification).</summary>
    public TimeSpan LastPublishDuration { get; private set; }

    /// <summary>Number of keys the last apply rewrote.</summary>
    public int LastPublishedCount { get; private set; }

    /// <summary>Raised on the UI thread after a new state was published.</summary>
    public event EventHandler<ArcThemeChangedEventArgs>? StateChanged;

    /// <summary>Whether the platform currently reports a dark appearance.</summary>
    public bool SystemIsDark
        => _application.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;

    /// <summary>Applies a state changed from the current one; what <paramref name="configure"/> does not set is kept.</summary>
    /// <param name="configure">Changes to make.</param>
    public void Apply(Action<ArcThemeBuilder> configure) => Apply(State.With(configure));

    /// <summary>
    /// Computes and publishes <paramref name="state"/>. All or nothing: a bad override throws
    /// <see cref="ArcThemeException"/> and the current theme stays. Must run on the UI thread.
    /// </summary>
    /// <param name="state">The state to apply.</param>
    public void Apply(ArcThemeState state)
    {
        Dispatcher.UIThread.VerifyAccess();
        var isDark = state.ResolveIsDark(SystemIsDark);
        var watch = Stopwatch.StartNew();
        var values = Compute(state, isDark);
        LastComputeDuration = watch.Elapsed;
        Commit(state, isDark, values);
    }

    /// <summary>
    /// Like <see cref="Apply(Action{ArcThemeBuilder})"/>, but computes the tokens on a worker thread and only publishes
    /// on the UI thread. Token calculations must then be thread safe (they are pure functions of the state).
    /// </summary>
    /// <remarks>
    /// Await the returned task; never block on it on the UI thread (<c>.Wait()</c>, <c>.Result</c>,
    /// <c>.GetAwaiter().GetResult()</c>): the publish step needs the UI thread, so blocking it deadlocks. Code that must
    /// finish synchronously calls <see cref="Apply(Action{ArcThemeBuilder})"/>, which takes a few milliseconds for Arc's
    /// full token set.
    /// </remarks>
    /// <param name="configure">Changes to make.</param>
    public async Task ApplyAsync(Action<ArcThemeBuilder> configure)
    {
        Dispatcher.UIThread.VerifyAccess();
        var state = State.With(configure);
        var isDark = state.ResolveIsDark(SystemIsDark);
        var watch = Stopwatch.StartNew();
        var values = await Task.Run(() => Compute(state, isDark));
        LastComputeDuration = watch.Elapsed;
        Commit(state, isDark, values);
    }

    /// <summary>
    /// Computes the token values of <paramref name="state"/> for one appearance without publishing them: resource key →
    /// value (colors as brushes). Used by scopes and by token snapshot exports.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="isDark">The appearance to compute.</param>
    public Dictionary<object, object?> Compute(ArcThemeState state, bool isDark)
        => ArcTokenCalculator.Compute(_descriptors, _tokenCount, state, isDark);

    /// <summary>The values of the applied state for one appearance, computed once per state and appearance.</summary>
    internal Dictionary<object, object?> GetCachedValues(bool isDark)
    {
        var slot = isDark ? 1 : 0;
        if (_cacheStates[slot] != State || _cacheValues[slot] is null)
        {
            _cacheValues[slot] = Compute(State, isDark);
            _cacheStates[slot] = State;
        }
        return _cacheValues[slot]!;
    }

    private readonly ArcThemeState?[] _cacheStates = new ArcThemeState?[2];
    private readonly Dictionary<object, object?>?[] _cacheValues = new Dictionary<object, object?>?[2];

    private void Commit(ArcThemeState state, bool isDark, Dictionary<object, object?> values)
    {
        var slot = isDark ? 1 : 0;
        _cacheStates[slot] = state;
        _cacheValues[slot] = values;
        var watch = Stopwatch.StartNew();
        var oldState = State;
        var wasDark = IsDark;
        State = state;
        IsDark = isDark;
        // The variant first: built-in theme dictionaries keyed by variant and ActualThemeVariant readers agree with the
        // tokens by the time the resource notification goes out.
        _application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        LastPublishedCount = ArcTokenCalculator.Publish(Resources, values);
        LastPublishDuration = watch.Elapsed;
        FollowSystem(state.Appearance == ArcAppearance.System);
        StateChanged?.Invoke(this, new ArcThemeChangedEventArgs(oldState, state, wasDark, isDark));
    }

    private void FollowSystem(bool follow)
    {
        if (follow && _platformSettings is null)
        {
            _platformSettings = _application.PlatformSettings;
            if (_platformSettings is not null)
            {
                _platformSettings.ColorValuesChanged += OnColorValuesChanged;
            }
        }
        else if (!follow && _platformSettings is not null)
        {
            _platformSettings.ColorValuesChanged -= OnColorValuesChanged;
            _platformSettings = null;
        }
    }

    private void OnColorValuesChanged(object? sender, PlatformColorValues e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (State.Appearance == ArcAppearance.System && State.ResolveIsDark(SystemIsDark) != IsDark)
            {
                Apply(State);
            }
        });
    }

    internal void Detach() => FollowSystem(false);
}
