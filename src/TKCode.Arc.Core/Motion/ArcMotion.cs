using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Motion;

// ArcMotionLevel (min of all sources wins) now comes from TKCode.Arc.Foundation, same name, namespace and values.

/// <summary>
/// Motion level policy. <see cref="LevelProperty"/> is inherited: set it on a window or any container.
/// Controls whose theme sets <c>arc:ArcMotion.Tracks="True"</c> get the pseudo-classes <c>:motion-full</c> or
/// <c>:motion-reduced</c> after they are loaded (so the first frame never animates); themes declare their
/// transitions under those pseudo-classes. With level Off neither class is present and nothing animates.
/// </summary>
public sealed class ArcMotion
{
    public const string FullPseudoClass = ":motion-full";
    public const string ReducedPseudoClass = ":motion-reduced";

    private ArcMotion()
    {
    }

    public static readonly AttachedProperty<ArcMotionLevel> LevelProperty =
        AvaloniaProperty.RegisterAttached<ArcMotion, AvaloniaObject, ArcMotionLevel>("Level", ArcMotionLevel.Full, inherits: true);

    public static readonly AttachedProperty<bool> TracksProperty =
        AvaloniaProperty.RegisterAttached<ArcMotion, Control, bool>("Tracks");

    /// <summary>The theme's MotionMode on a tracking control (internal so <see cref="LiveLoop"/> can follow it).</summary>
    internal static readonly AttachedProperty<ArcMotionLevel> TokenLevelProperty =
        AvaloniaProperty.RegisterAttached<ArcMotion, Control, ArcMotionLevel>("TokenLevel", ArcMotionLevel.Full);

    private static readonly AttachedProperty<bool> ActiveProperty =
        AvaloniaProperty.RegisterAttached<ArcMotion, Control, bool>("Active");

    // The token source a tracking control is registered with while the MotionMode token can still change its effective
    // level. A control whose inherited level is Off is Off whatever the token says, so it stays unregistered and never
    // pays for the lookup.
    private static readonly AttachedProperty<TokenSource?> SourceProperty =
        AvaloniaProperty.RegisterAttached<ArcMotion, Control, TokenSource?>("Source");

    private static readonly AttachedProperty<int> SettlingProperty =
        AvaloniaProperty.RegisterAttached<ArcMotion, Control, int>("Settling");

    static ArcMotion()
    {
        TracksProperty.Changed.AddClassHandler<Control>(OnTracksChanged);
        LevelProperty.Changed.AddClassHandler<Control>((c, _) =>
        {
            // The inherited level decides whether the MotionMode token can still change the effective level.
            EnsureTokenTracking(c);
            Refresh(c);
        });
        // A theme scope that appears or goes around a tracking control moves it to the source that holds its tokens.
        ArcThemeScope.RootProperty.Changed.AddClassHandler<Control>((c, _) =>
        {
            EnsureTokenTracking(c);
            Refresh(c);
        });
    }

    public static ArcMotionLevel GetLevel(AvaloniaObject element) => element.GetValue(LevelProperty);

    public static void SetLevel(AvaloniaObject element, ArcMotionLevel value) => element.SetValue(LevelProperty, value);

    public static bool GetTracks(Control element) => element.GetValue(TracksProperty);

    public static void SetTracks(Control element, bool value) => element.SetValue(TracksProperty, value);

    /// <summary>Effective level for <paramref name="visual"/>: min(inherited level, theme MotionMode).</summary>
    public static ArcMotionLevel GetEffectiveLevel(AvaloniaObject visual)
    {
        var level = visual.GetValue(LevelProperty);
        ArcMotionLevel tokenLevel;
        if (visual is Control control && control.GetValue(TracksProperty))
        {
            tokenLevel = control.GetValue(TokenLevelProperty);
        }
        else
        {
            tokenLevel = visual is Control c ? ThemeLevel(c) : ArcMotionLevel.Full;
        }
        return (ArcMotionLevel)Math.Min((int)level, (int)tokenLevel);
    }

    // MotionMode is published per resource root: the application (read through the TopLevel) or an ArcThemeScope element
    // for its subtree. The walk up the resource scopes it needs is done once per root and kept until the root's
    // resources or theme variant change; the tracking controls under a root register with it and are refreshed together
    // by the root's one pair of event subscriptions. Code-driven motion asks for the level on every retarget.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StyledElement, TokenSource> s_sources = new();

    private sealed class TokenSource
    {
        private readonly StyledElement _root;
        private ArcMotionLevel _level;
        private bool _valid;

        public TokenSource(StyledElement root) => _root = root;

        /// <summary>The tracking controls registered with this root (they leave on unload, so plain references).</summary>
        public HashSet<Control> Tracking { get; } = new();

        public ArcMotionLevel Level
        {
            get
            {
                if (!_valid)
                {
                    _level = _root.TryFindResource(ArcDesignTokenKind.MotionMode, _root.ActualThemeVariant, out var mode)
                        ? Parse(mode as string)
                        : ArcMotionLevel.Full;
                    _valid = true;
                }
                return _level;
            }
        }

        public void Invalidate() => _valid = false;
    }

    private static readonly EventHandler<ResourcesChangedEventArgs> s_onSourceResourcesChanged = (sender, _) => OnSourceChanged(sender);
    private static readonly EventHandler s_onSourceVariantChanged = (sender, _) => OnSourceChanged(sender);

    /// <summary>The element whose resources hold <paramref name="control"/>'s MotionMode: its theme scope, else its TopLevel.</summary>
    private static StyledElement? ResourceRoot(Control control)
        => ArcThemeScope.GetRoot(control) ?? (StyledElement?)TopLevel.GetTopLevel(control);

    private static TokenSource SourceFor(StyledElement root)
    {
        if (!s_sources.TryGetValue(root, out var source))
        {
            source = new TokenSource(root);
            s_sources.Add(root, source);
            root.ResourcesChanged += s_onSourceResourcesChanged;
            root.ActualThemeVariantChanged += s_onSourceVariantChanged;
        }
        return source;
    }

    private static void OnSourceChanged(object? sender)
    {
        if (sender is not StyledElement root || !s_sources.TryGetValue(root, out var source))
        {
            return;
        }
        source.Invalidate();
        if (source.Tracking.Count == 0)
        {
            return;
        }
        // A snapshot: refreshing a control's pseudo-classes may load or unload controls that change the set.
        var controls = new Control[source.Tracking.Count];
        source.Tracking.CopyTo(controls);
        var level = source.Level;
        foreach (var control in controls)
        {
            if (control.GetValue(ActiveProperty) && ReferenceEquals(control.GetValue(SourceProperty), source))
            {
                control.SetValue(TokenLevelProperty, level);
                Refresh(control);
            }
        }
    }

    private static ArcMotionLevel ThemeLevel(Control control)
    {
        if (ResourceRoot(control) is not { } root)
        {
            return control.TryFindResource(ArcDesignTokenKind.MotionMode, out var detached) ? Parse(detached as string) : ArcMotionLevel.Full;
        }
        return SourceFor(root).Level;
    }

    /// <summary>True when spatial motion (springs, travel, scale) should run.</summary>
    public static bool IsFull(AvaloniaObject visual) => GetEffectiveLevel(visual) == ArcMotionLevel.Full;

    internal static ArcMotionLevel Parse(string? mode) => mode switch
    {
        "off" => ArcMotionLevel.Off,
        "reduced" => ArcMotionLevel.Reduced,
        _ => ArcMotionLevel.Full
    };

    private static void OnTracksChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            control.Loaded += OnLoaded;
            control.Unloaded += OnUnloaded;
            if (control.IsLoaded)
            {
                Activate(control);
            }
        }
        else
        {
            control.Loaded -= OnLoaded;
            control.Unloaded -= OnUnloaded;
            Deactivate(control);
        }
    }

    // Controls loaded in one pass are activated together by a single posted job instead of one post each.
    private static readonly List<Control> s_pending = new();
    private static bool s_flushPosted;

    private static void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            // Posted so that values applied while loading settle before transitions are enabled.
            s_pending.Add(control);
            if (!s_flushPosted)
            {
                s_flushPosted = true;
                Dispatcher.UIThread.Post(FlushPending, DispatcherPriority.Background);
            }
        }
    }

    private static void FlushPending()
    {
        s_flushPosted = false;
        // Activation can load more controls (a pseudo-class that shows a part): they queue for the next post.
        var count = s_pending.Count;
        for (var i = 0; i < count; i++)
        {
            Activate(s_pending[i]);
        }
        s_pending.RemoveRange(0, count);
    }

    private static void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            Deactivate(control);
        }
    }

    private static void Activate(Control control)
    {
        if (!control.IsLoaded || control.GetValue(ActiveProperty) || !control.GetValue(TracksProperty))
        {
            return;
        }
        control.SetValue(ActiveProperty, true);
        EnsureTokenTracking(control);
        Refresh(control);
    }

    private static void Deactivate(Control control)
    {
        if (control.GetValue(SourceProperty) is { } source)
        {
            source.Tracking.Remove(control);
            control.ClearValue(SourceProperty);
        }
        control.SetValue(ActiveProperty, false);
        var pseudo = (IPseudoClasses)control.Classes;
        pseudo.Set(FullPseudoClass, false);
        pseudo.Set(ReducedPseudoClass, false);
    }

    /// <summary>
    /// Registers the control with the token source of its resource root (its theme scope, else its TopLevel) only while
    /// the MotionMode token can still change the effective level; the source reads the token once per root and refreshes
    /// every registered control when its resources or theme variant change, so a tracking control holds no event
    /// subscription and makes no lookup of its own. A tracking control whose inherited <see cref="LevelProperty"/> is Off
    /// is Off whatever the token says, so it stays unregistered: the common "motion off" run (the benchmark, screenshot
    /// passes) pays nothing per control. When the inherited level goes Off it leaves the source again, and when it comes
    /// back (or its scope changes) the token is re-read, so an enabled run behaves exactly as before.
    /// </summary>
    private static void EnsureTokenTracking(Control control)
    {
        var needed = control.GetValue(ActiveProperty)
            && control.GetValue(TracksProperty)
            && control.GetValue(LevelProperty) != ArcMotionLevel.Off;
        var current = control.GetValue(SourceProperty);
        TokenSource? wanted = null;
        if (needed && ResourceRoot(control) is { } root)
        {
            wanted = SourceFor(root);
        }
        if (ReferenceEquals(current, wanted))
        {
            return;
        }
        current?.Tracking.Remove(control);
        if (wanted is null)
        {
            control.ClearValue(SourceProperty);
            return;
        }
        control.SetValue(SourceProperty, wanted);
        wanted.Tracking.Add(control);
        control.SetValue(TokenLevelProperty, wanted.Level);
    }

    /// <summary>
    /// Holds a tracking control's transitions off while values arrive without user intent (a new data context in a
    /// recycled template, a reused detail view), so the new state shows at once instead of animating from the old
    /// item's. Pair with <see cref="EndSettle"/>; calls nest.
    /// </summary>
    internal static void BeginSettle(Control control)
    {
        control.SetValue(SettlingProperty, control.GetValue(SettlingProperty) + 1);
        var pseudo = (IPseudoClasses)control.Classes;
        pseudo.Set(FullPseudoClass, false);
        pseudo.Set(ReducedPseudoClass, false);
    }

    /// <inheritdoc cref="BeginSettle"/>
    internal static void EndSettle(Control control)
    {
        var count = Math.Max(0, control.GetValue(SettlingProperty) - 1);
        control.SetValue(SettlingProperty, count);
        if (count == 0)
        {
            Refresh(control);
        }
    }

    /// <summary>True between <see cref="BeginSettle"/> and <see cref="EndSettle"/>: code-driven motion jumps too.</summary>
    internal static bool IsSettling(Control control) => control.GetValue(SettlingProperty) > 0;

    private static void Refresh(Control control)
    {
        if (!control.GetValue(TracksProperty) || !control.GetValue(ActiveProperty) || control.GetValue(SettlingProperty) > 0)
        {
            return;
        }
        var level = GetEffectiveLevel(control);
        var pseudo = (IPseudoClasses)control.Classes;
        pseudo.Set(FullPseudoClass, level == ArcMotionLevel.Full);
        pseudo.Set(ReducedPseudoClass, level == ArcMotionLevel.Reduced);
    }
}
