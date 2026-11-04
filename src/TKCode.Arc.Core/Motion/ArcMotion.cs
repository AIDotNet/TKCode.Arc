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

    private static readonly AttachedProperty<int> SettlingProperty =
        AvaloniaProperty.RegisterAttached<ArcMotion, Control, int>("Settling");

    static ArcMotion()
    {
        TracksProperty.Changed.AddClassHandler<Control>(OnTracksChanged);
        LevelProperty.Changed.AddClassHandler<Control>((c, _) => Refresh(c));
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

    // MotionMode is an application-wide token, so the walk up the resource scopes it needs is done once per TopLevel and
    // kept until the resources or the theme variant change. Code-driven motion asks for the level on every retarget.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TopLevel, ThemeLevelCache> s_themeLevels = new();

    private sealed class ThemeLevelCache
    {
        public ArcMotionLevel Level;
        public bool Valid;

        public void Invalidate() => Valid = false;
    }

    private static ArcMotionLevel ThemeLevel(Control control)
    {
        if (TopLevel.GetTopLevel(control) is not { } top)
        {
            return control.TryFindResource(ArcDesignTokenKind.MotionMode, out var detached) ? Parse(detached as string) : ArcMotionLevel.Full;
        }
        if (!s_themeLevels.TryGetValue(top, out var cache))
        {
            cache = new ThemeLevelCache();
            s_themeLevels.Add(top, cache);
            top.ResourcesChanged += (_, _) => cache.Invalidate();
            top.ActualThemeVariantChanged += (_, _) => cache.Invalidate();
        }
        if (!cache.Valid)
        {
            cache.Level = top.TryFindResource(ArcDesignTokenKind.MotionMode, out var mode) ? Parse(mode as string) : ArcMotionLevel.Full;
            cache.Valid = true;
        }
        return cache.Level;
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

    // Shared handlers: a tracking control costs two event subscriptions, not an observable, an observer and a disposable.
    private static readonly EventHandler<ResourcesChangedEventArgs> s_onResourcesChanged = (sender, _) => OnTokenSourceChanged(sender);
    private static readonly EventHandler s_onThemeVariantChanged = (sender, _) => OnTokenSourceChanged(sender);

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
        control.ResourcesChanged += s_onResourcesChanged;
        control.ActualThemeVariantChanged += s_onThemeVariantChanged;
        ReadTokenLevel(control);
        Refresh(control);
    }

    private static void Deactivate(Control control)
    {
        if (control.GetValue(ActiveProperty))
        {
            control.ResourcesChanged -= s_onResourcesChanged;
            control.ActualThemeVariantChanged -= s_onThemeVariantChanged;
        }
        control.SetValue(ActiveProperty, false);
        var pseudo = (IPseudoClasses)control.Classes;
        pseudo.Set(FullPseudoClass, false);
        pseudo.Set(ReducedPseudoClass, false);
    }

    private static void OnTokenSourceChanged(object? sender)
    {
        if (sender is Control control && control.GetValue(ActiveProperty))
        {
            ReadTokenLevel(control);
            Refresh(control);
        }
    }

    private static void ReadTokenLevel(Control control)
    {
        var level = control.TryFindResource(ArcDesignTokenKind.MotionMode, control.ActualThemeVariant, out var mode)
            ? Parse(mode as string)
            : ArcMotionLevel.Full;
        control.SetValue(TokenLevelProperty, level);
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
