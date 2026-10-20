using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What the recorder shows: caps (or a placeholder), the recording light and the tone of the caps.</summary>
internal readonly record struct ShortcutRecorderView(
    IReadOnlyList<ShortcutKeyCap> Caps,
    bool IsRecording,
    bool IsPressed,
    bool IsWarning,
    string Placeholder,
    string Prompt);

/// <summary>
/// The recorder of a <see cref="ShortcutRecorder"/>: a focusable button (toggle semantics, pressed while recording)
/// that fills the field row and draws the key caps, the placeholder and the recording light in <see cref="Render"/>.
/// </summary>
/// <remarks>
/// Code-driven motion (<see cref="MotionValue{T}"/>) because Arc's caps are an <c>AnimatePresence mode="popLayout"</c>
/// list: per-cap enter / exit with a stagger, leaving caps that keep drawing outside the layout, layout slides on the
/// snappy spring and per-cap colour tweens. None of that maps to ControlTheme transitions on a fixed template.
/// </remarks>
internal sealed class ShortcutRecorderButton : AvaloniaButton
{
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, bool>(nameof(IsOn));

    public static readonly StyledProperty<IBrush?> KeyCapEdgeBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(KeyCapEdgeBrush));

    public static readonly StyledProperty<IBrush?> KeyCapFillProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(KeyCapFill));

    public static readonly StyledProperty<IBrush?> KeyCapForegroundProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(KeyCapForeground));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentSubtleBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(AccentSubtleBrush));

    public static readonly StyledProperty<IBrush?> AccentStrongBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(AccentStrongBrush));

    public static readonly StyledProperty<IBrush?> KeyCapWarningEdgeProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(KeyCapWarningEdge));

    public static readonly StyledProperty<IBrush?> KeyCapWarningFillProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(KeyCapWarningFill));

    public static readonly StyledProperty<IBrush?> PlaceholderForegroundProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(PlaceholderForeground));

    public static readonly StyledProperty<IBrush?> PromptForegroundProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, IBrush?>(nameof(PromptForeground));

    public static readonly StyledProperty<double> KeyCapFontSizeProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(KeyCapFontSize), 12);

    public static readonly StyledProperty<FontWeight> KeyCapFontWeightProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, FontWeight>(nameof(KeyCapFontWeight), FontWeight.Medium);

    public static readonly StyledProperty<double> PlaceholderFontSizeProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(PlaceholderFontSize), 14);

    public static readonly StyledProperty<double> KeyCapHeightProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(KeyCapHeight), 26);

    public static readonly StyledProperty<double> KeyCapMinWidthProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(KeyCapMinWidth), 26);

    public static readonly StyledProperty<Thickness> KeyCapPaddingProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, Thickness>(nameof(KeyCapPadding), new Thickness(7, 0, 7, 1));

    public static readonly StyledProperty<CornerRadius> KeyCapRadiusProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, CornerRadius>(nameof(KeyCapRadius), new CornerRadius(8));

    public static readonly StyledProperty<double> KeyCapBottomEdgeProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(KeyCapBottomEdge), 2);

    public static readonly StyledProperty<double> KeyCapGapProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(KeyCapGap), 4);

    public static readonly StyledProperty<double> RecordingDotDiameterProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(RecordingDotDiameter), 7);

    public static readonly StyledProperty<Thickness> RecordingDotMarginProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, Thickness>(nameof(RecordingDotMargin), new Thickness(3, 0, 6, 0));

    public static readonly StyledProperty<double> PlaceholderInsetProperty =
        AvaloniaProperty.Register<ShortcutRecorderButton, double>(nameof(PlaceholderInset), 3);

    /// <summary>Pressed (<c>aria-pressed</c>) while recording.</summary>
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    public IBrush? KeyCapEdgeBrush { get => GetValue(KeyCapEdgeBrushProperty); set => SetValue(KeyCapEdgeBrushProperty, value); }
    public IBrush? KeyCapFill { get => GetValue(KeyCapFillProperty); set => SetValue(KeyCapFillProperty, value); }
    public IBrush? KeyCapForeground { get => GetValue(KeyCapForegroundProperty); set => SetValue(KeyCapForegroundProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? AccentSubtleBrush { get => GetValue(AccentSubtleBrushProperty); set => SetValue(AccentSubtleBrushProperty, value); }
    public IBrush? AccentStrongBrush { get => GetValue(AccentStrongBrushProperty); set => SetValue(AccentStrongBrushProperty, value); }
    public IBrush? KeyCapWarningEdge { get => GetValue(KeyCapWarningEdgeProperty); set => SetValue(KeyCapWarningEdgeProperty, value); }
    public IBrush? KeyCapWarningFill { get => GetValue(KeyCapWarningFillProperty); set => SetValue(KeyCapWarningFillProperty, value); }
    public IBrush? PlaceholderForeground { get => GetValue(PlaceholderForegroundProperty); set => SetValue(PlaceholderForegroundProperty, value); }
    public IBrush? PromptForeground { get => GetValue(PromptForegroundProperty); set => SetValue(PromptForegroundProperty, value); }
    public double KeyCapFontSize { get => GetValue(KeyCapFontSizeProperty); set => SetValue(KeyCapFontSizeProperty, value); }
    public FontWeight KeyCapFontWeight { get => GetValue(KeyCapFontWeightProperty); set => SetValue(KeyCapFontWeightProperty, value); }
    public double PlaceholderFontSize { get => GetValue(PlaceholderFontSizeProperty); set => SetValue(PlaceholderFontSizeProperty, value); }
    public double KeyCapHeight { get => GetValue(KeyCapHeightProperty); set => SetValue(KeyCapHeightProperty, value); }
    public double KeyCapMinWidth { get => GetValue(KeyCapMinWidthProperty); set => SetValue(KeyCapMinWidthProperty, value); }
    public Thickness KeyCapPadding { get => GetValue(KeyCapPaddingProperty); set => SetValue(KeyCapPaddingProperty, value); }
    public CornerRadius KeyCapRadius { get => GetValue(KeyCapRadiusProperty); set => SetValue(KeyCapRadiusProperty, value); }
    public double KeyCapBottomEdge { get => GetValue(KeyCapBottomEdgeProperty); set => SetValue(KeyCapBottomEdgeProperty, value); }
    public double KeyCapGap { get => GetValue(KeyCapGapProperty); set => SetValue(KeyCapGapProperty, value); }
    public double RecordingDotDiameter { get => GetValue(RecordingDotDiameterProperty); set => SetValue(RecordingDotDiameterProperty, value); }
    public Thickness RecordingDotMargin { get => GetValue(RecordingDotMarginProperty); set => SetValue(RecordingDotMarginProperty, value); }
    public double PlaceholderInset { get => GetValue(PlaceholderInsetProperty); set => SetValue(PlaceholderInsetProperty, value); }

    /// <summary>The control that owns the recording logic (keys, clicks, focus).</summary>
    internal ShortcutRecorder? Owner { get; set; }

    /// <summary>The live caps, for tests and diagnostics (leaving caps excluded).</summary>
    internal IReadOnlyList<string> CapLabels => _caps.Select(c => c.Label).ToList();

    /// <summary>The placeholder currently shown (null while caps are shown).</summary>
    internal string? PlaceholderText => _hint?.Text;

    /// <summary>True while the recording light breathes.</summary>
    internal bool IsBreathing => _breathing;

    private const double BlurSubtle = 2;
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    // motion's default tween ease ("easeInOut", cubic-bezier(.42, 0, .58, 1)) for transitions given only a duration.
    private static readonly Easing s_easeInOut = new SplineEasing(0.42, 0, 0.58, 1);
    private static readonly TimeSpan s_capOpacity = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan s_capBlur = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan s_capExit = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_capFade = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_capFadeExit = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan s_hintExit = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan s_hintFade = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_press = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan s_breath = TimeSpan.FromMilliseconds(700);

    private sealed class Cap
    {
        public required string Id { get; init; }
        public string Label { get; set; } = string.Empty;
        public FormattedText? Text { get; set; }
        public double Width { get; set; }
        public double TargetX { get; set; }
        public required MotionValue<double> X { get; init; }
        public required MotionValue<double> Pop { get; init; }
        public required MotionValue<double> Fade { get; init; }
        public required MotionValue<double> Sharpen { get; init; }
        public required MotionValue<double> Exit { get; init; }
        public required MotionValue<double> Press { get; init; }
        public required MotionValue<double> Drop { get; init; }
        public required MotionValue<double> Warn { get; init; }
        public bool FadeOnly { get; set; }
        public bool Placed { get; set; }
        public bool Leaving { get; set; }
        public double FromOpacity { get; set; } = 1;
        public double FromScale { get; set; } = 1;
        public double FromY { get; set; }
        public double FromBlur { get; set; }
        public double FrozenX { get; set; }
        public StreamGeometry? Ring { get; set; }
        public double RingWidth { get; set; }
        public double RingEdge { get; set; }
        public Color EdgeColor { get; set; }
        public Color FillColor { get; set; }
        public Color TextColor { get; set; }
        public IBrush? Edge { get; set; }
        public IBrush? Fill { get; set; }
        public IBrush? Ink { get; set; }

        public void Dispose()
        {
            X.Dispose();
            Pop.Dispose();
            Fade.Dispose();
            Sharpen.Dispose();
            Exit.Dispose();
            Press.Dispose();
            Drop.Dispose();
            Warn.Dispose();
        }
    }

    private sealed class Hint
    {
        public required string Key { get; init; }
        public string Text { get; set; } = string.Empty;
        public bool IsPrompt { get; init; }
        public FormattedText? Shaped { get; set; }
        public double X { get; set; }
        public required MotionValue<double> Enter { get; init; }
        public required MotionValue<double> Exit { get; init; }
        public bool FadeOnly { get; set; }
        public bool Leaving { get; set; }
        public double FromOpacity { get; set; } = 1;
        public double FromY { get; set; }
        public double FromBlur { get; set; }
    }

    private List<Cap> _caps = new();
    private readonly List<Cap> _leavingCaps = new();
    private Hint? _hint;
    private readonly List<Hint> _leavingHints = new();
    private ShortcutRecorderView _view = new(Array.Empty<ShortcutKeyCap>(), false, false, false, string.Empty, string.Empty);
    private MotionValue<double>? _dot;
    private bool _breathing;
    private int _breathGeneration;
    private Color _edge, _fill, _ink, _pressedEdge, _pressedFill, _pressedInk, _warnEdge, _warnFill;
    private bool _colorsDirty = true;
    private bool _shapeDirty = true;

    static ShortcutRecorderButton()
    {
        AffectsMeasure<ShortcutRecorderButton>(KeyCapFontSizeProperty, KeyCapFontWeightProperty, PlaceholderFontSizeProperty,
            KeyCapHeightProperty, KeyCapMinWidthProperty, KeyCapPaddingProperty, KeyCapGapProperty, RecordingDotDiameterProperty,
            RecordingDotMarginProperty, PlaceholderInsetProperty, FontFamilyProperty, PaddingProperty);
        AffectsRender<ShortcutRecorderButton>(KeyCapRadiusProperty, KeyCapBottomEdgeProperty);
    }

    public ShortcutRecorderButton()
    {
        // Tunnel on the recorder itself (after its ancestors, before any bubble handler and Button's own Enter /
        // Space handling), with handled events too: while recording the chord belongs to the recorder.
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, OnTunnelKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override Type StyleKeyOverride => typeof(ShortcutRecorderButton);

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        Owner?.OnRecorderKeyDown(e);
    }

    private void OnTunnelKeyUp(object? sender, KeyEventArgs e)
    {
        Owner?.OnRecorderKeyUp(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnRecorderFocusChanged();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnRecorderFocusChanged();
        Owner?.OnRecorderLostFocus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == KeyCapEdgeBrushProperty || property == KeyCapFillProperty || property == KeyCapForegroundProperty
            || property == AccentBrushProperty || property == AccentSubtleBrushProperty || property == AccentStrongBrushProperty
            || property == KeyCapWarningEdgeProperty || property == KeyCapWarningFillProperty)
        {
            _colorsDirty = true;
            InvalidateVisual();
        }
        else if (property == PlaceholderForegroundProperty || property == PromptForegroundProperty)
        {
            _shapeDirty = true;
            InvalidateVisual();
        }
        else if (property == KeyCapFontSizeProperty || property == KeyCapFontWeightProperty || property == PlaceholderFontSizeProperty
                 || property == FontFamilyProperty || property == KeyCapHeightProperty || property == KeyCapMinWidthProperty
                 || property == KeyCapPaddingProperty || property == KeyCapGapProperty || property == RecordingDotDiameterProperty
                 || property == RecordingDotMarginProperty || property == PlaceholderInsetProperty || property == PaddingProperty)
        {
            // Metrics changed (theme, font): lay the row out again without motion.
            _shapeDirty = true;
            Relayout(ArcMotionLevel.Off);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateBreathing();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopBreathing();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ShortcutRecorderButtonPeer(this);

    // ---- state ----------------------------------------------------------------------------------------------------

    /// <summary>Shows <paramref name="view"/>; animates the differences once loaded (first state: no motion).</summary>
    public void Present(ShortcutRecorderView view)
    {
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var previous = _view;
        _view = view;
        EnsureShaped();

        // Caps: AnimatePresence keyed by the physical key id.
        var existing = new Dictionary<string, Cap>(_caps.Count, StringComparer.Ordinal);
        foreach (var cap in _caps)
        {
            existing[cap.Id] = cap;
        }
        var next = new List<Cap>(view.Caps.Count);
        var entering = new List<(Cap Cap, int Index)>();
        for (var i = 0; i < view.Caps.Count; i++)
        {
            var token = view.Caps[i];
            if (existing.Remove(token.Id, out var cap))
            {
                if (cap.Label != token.Label)
                {
                    cap.Label = token.Label;
                    cap.Text = null;
                    ShapeCap(cap);
                }
                next.Add(cap);
                continue;
            }
            cap = CreateCap(token);
            next.Add(cap);
            entering.Add((cap, i));
        }
        foreach (var gone in existing.Values)
        {
            LeaveCap(gone, level);
        }
        _caps = next;

        // Placeholder: keyed "prompt" while recording, "placeholder" otherwise; only while there are no caps.
        var hintKey = view.Caps.Count > 0 ? null : view.IsRecording ? "prompt" : "placeholder";
        if (_hint is not null && _hint.Key != hintKey)
        {
            LeaveHint(_hint, level);
            _hint = null;
        }
        if (hintKey is not null)
        {
            var text = view.IsRecording ? view.Prompt : view.Placeholder;
            if (_hint is null)
            {
                _hint = new Hint
                {
                    Key = hintKey,
                    IsPrompt = view.IsRecording,
                    Text = text,
                    Enter = CreateValue(1),
                    Exit = CreateValue(0)
                };
                ShapeHint(_hint);
                EnterHint(_hint, level);
            }
            else if (_hint.Text != text)
            {
                _hint.Text = text;
                _hint.Shaped = null;
                ShapeHint(_hint);
            }
        }

        Relayout(level);

        // Entering caps start at their place and pop in, staggered by 35ms when a combination settles.
        foreach (var (cap, index) in entering)
        {
            EnterCap(cap, level, view.IsRecording ? TimeSpan.Zero : ArcMotionPresets.StaggerItem * index);
        }
        foreach (var cap in _caps)
        {
            SetTone(cap, view.IsPressed, view.IsWarning, level, entering.Any(e => ReferenceEquals(e.Cap, cap)));
        }

        if (previous.IsRecording != view.IsRecording)
        {
            UpdateBreathing();
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private Cap CreateCap(ShortcutKeyCap token)
    {
        var cap = new Cap
        {
            Id = token.Id,
            Label = token.Label,
            X = CreateValue(0),
            Pop = CreateValue(1),
            Fade = CreateValue(1),
            Sharpen = CreateValue(1),
            Exit = CreateValue(0),
            Press = CreateValue(0),
            Drop = CreateValue(0),
            Warn = CreateValue(0)
        };
        ShapeCap(cap);
        return cap;
    }

    private void EnterCap(Cap cap, ArcMotionLevel level, TimeSpan delay)
    {
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            // chipFade: opacity only, 0.1s, no stagger.
            cap.FadeOnly = true;
            cap.Fade.Jump(0);
            _ = cap.Fade.AnimateTo(1, MotionSpec.Tween(s_capFade, s_easeInOut));
            return;
        }
        // chip: opacity 0, scale .72, y 4, subtle blur → snappy (opacity 0.14s, blur 0.16s), all delayed by the stagger.
        cap.Pop.Jump(0);
        cap.Fade.Jump(0);
        cap.Sharpen.Jump(0);
        _ = cap.Pop.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, delay));
        _ = cap.Fade.AnimateTo(1, MotionSpec.Tween(s_capOpacity, s_easeInOut, delay));
        _ = cap.Sharpen.AnimateTo(1, MotionSpec.Tween(s_capBlur, s_easeInOut, delay));
    }

    private void LeaveCap(Cap cap, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            cap.Dispose();
            return;
        }
        // popLayout: the cap leaves the row at once and fades where it stood.
        var (opacity, scale, y, blur) = CapPresence(cap);
        cap.FromOpacity = opacity;
        cap.FromScale = scale;
        cap.FromY = y;
        cap.FromBlur = blur;
        cap.FrozenX = cap.X.Value;
        cap.Leaving = true;
        cap.FadeOnly = level == ArcMotionLevel.Reduced;
        cap.Pop.Stop();
        cap.Fade.Stop();
        cap.Sharpen.Stop();
        cap.X.Stop();
        _leavingCaps.Add(cap);
        var duration = cap.FadeOnly ? s_capFadeExit : s_capExit;
        _ = RemoveWhenDone(cap, cap.Exit.AnimateTo(1, MotionSpec.Tween(duration, s_easeInOut)));
    }

    private async Task RemoveWhenDone(Cap cap, Task<bool> exit)
    {
        await exit;
        if (_leavingCaps.Remove(cap))
        {
            cap.Dispose();
            InvalidateVisual();
        }
    }

    private void SetTone(Cap cap, bool pressed, bool warning, ArcMotionLevel level, bool jump)
    {
        var p = pressed ? 1.0 : 0.0;
        var w = warning ? 1.0 : 0.0;
        // Reduced: Arc sets the cap transitions to 0ms and drops the pressed translate.
        var drop = level == ArcMotionLevel.Reduced ? 0 : p;
        if (jump || level != ArcMotionLevel.Full)
        {
            cap.Press.Jump(p);
            cap.Warn.Jump(w);
            cap.Drop.Jump(drop);
            return;
        }
        // Becoming pressed uses the held cap's 90ms; letting go uses the base transitions (fast colours, CSS spring lift).
        var colors = pressed ? s_press : ArcMotionPresets.Fast;
        if (Math.Abs(cap.Press.Target - p) > 0.001)
        {
            _ = cap.Press.AnimateTo(p, MotionSpec.Tween(colors, ArcEasings.Standard));
        }
        if (Math.Abs(cap.Warn.Target - w) > 0.001)
        {
            _ = cap.Warn.AnimateTo(w, MotionSpec.Tween(colors, ArcEasings.Standard));
        }
        if (Math.Abs(cap.Drop.Target - drop) > 0.001)
        {
            _ = cap.Drop.AnimateTo(drop, MotionSpec.Tween(pressed ? s_press : ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
    }

    private void EnterHint(Hint hint, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        hint.FadeOnly = level == ArcMotionLevel.Reduced;
        hint.Enter.Jump(0);
        _ = hint.Enter.AnimateTo(1, hint.FadeOnly
            ? MotionSpec.Tween(s_hintFade, s_easeInOut)
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
    }

    private void LeaveHint(Hint hint, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            hint.Enter.Dispose();
            hint.Exit.Dispose();
            return;
        }
        var (opacity, y, blur) = HintPresence(hint);
        hint.FromOpacity = opacity;
        hint.FromY = y;
        hint.FromBlur = blur;
        hint.Leaving = true;
        hint.Enter.Stop();
        _leavingHints.Add(hint);
        _ = RemoveWhenDone(hint, hint.Exit.AnimateTo(1, MotionSpec.Tween(s_hintExit, s_easeInOut)));
    }

    private async Task RemoveWhenDone(Hint hint, Task<bool> exit)
    {
        await exit;
        if (_leavingHints.Remove(hint))
        {
            hint.Enter.Dispose();
            hint.Exit.Dispose();
            InvalidateVisual();
        }
    }

    /// <summary>Places the caps in a row after the recording light; caps that stay slide on the snappy spring.</summary>
    private void Relayout(ArcMotionLevel level)
    {
        EnsureShaped();
        var x = RowStart(_view.IsRecording);
        var gap = KeyCapGap;
        foreach (var cap in _caps)
        {
            var moved = Math.Abs(cap.TargetX - x) > 0.01;
            cap.TargetX = x;
            if (!cap.Placed || level != ArcMotionLevel.Full)
            {
                // New caps start where they belong (they pop in place); without full motion layout jumps.
                cap.Placed = true;
                cap.X.Jump(x);
            }
            else if (moved)
            {
                // layout="position": a cap that stays slides to its new place on the snappy spring.
                _ = cap.X.AnimateTo(x, MotionSpec.Spring(ArcSpring.Snappy));
            }
            x += cap.Width + gap;
        }
        if (_hint is not null)
        {
            _hint.X = RowStart(_view.IsRecording) + PlaceholderInset;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private double RowStart(bool recording)
    {
        var x = Padding.Left;
        if (recording)
        {
            var dot = RecordingDotMargin;
            x += dot.Left + RecordingDotDiameter + dot.Right + KeyCapGap;
        }
        return x;
    }

    // ---- the recording light (P21) -----------------------------------------------------------------------------------

    private void UpdateBreathing()
    {
        if (_view.IsRecording && this.IsAttachedToVisualTree() && ArcMotion.IsFull(this))
        {
            if (!_breathing)
            {
                _breathing = true;
                _ = BreatheAsync(++_breathGeneration);
            }
            return;
        }
        StopBreathing();
    }

    private void StopBreathing()
    {
        _breathGeneration++;
        _breathing = false;
        _dot?.Jump(1);
    }

    /// <summary>Opacity 1 → .35 → 1 over 1.4s on the in-out ease, only while recording, attached and in full motion.</summary>
    private async Task BreatheAsync(int generation)
    {
        _dot ??= CreateValue(1);
        while (generation == _breathGeneration && _view.IsRecording && this.IsAttachedToVisualTree() && ArcMotion.IsFull(this))
        {
            var dim = _dot.AnimateTo(0.35, MotionSpec.Tween(s_breath, ArcEasings.InOut));
            if (dim.IsCompleted || !await dim || generation != _breathGeneration)
            {
                break;
            }
            var lift = _dot.AnimateTo(1, MotionSpec.Tween(s_breath, ArcEasings.InOut));
            if (lift.IsCompleted || !await lift)
            {
                break;
            }
        }
        if (generation == _breathGeneration)
        {
            _breathing = false;
        }
    }

    // ---- shaping and layout -----------------------------------------------------------------------------------------

    private Typeface CapTypeface => new(FontFamily, FontStyle.Normal, KeyCapFontWeight);

    private void EnsureShaped()
    {
        if (!_shapeDirty)
        {
            return;
        }
        _shapeDirty = false;
        foreach (var cap in _caps)
        {
            cap.Text = null;
            ShapeCap(cap);
        }
        if (_hint is not null)
        {
            _hint.Shaped = null;
            ShapeHint(_hint);
        }
    }

    private void ShapeCap(Cap cap)
    {
        if (cap.Text is null)
        {
            var text = new FormattedText(cap.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, CapTypeface,
                KeyCapFontSize, cap.Ink ?? KeyCapForeground);
            text.SetFontFeatures(s_tabular);
            cap.Text = text;
        }
        var padding = KeyCapPadding;
        cap.Width = Math.Max(KeyCapMinWidth, cap.Text.WidthIncludingTrailingWhitespace + padding.Left + padding.Right + 2);
    }

    private void ShapeHint(Hint hint)
    {
        hint.Shaped ??= new FormattedText(hint.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily), PlaceholderFontSize, hint.IsPrompt ? PromptForeground : PlaceholderForeground);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureShaped();
        var padding = Padding;
        double content = 0;
        if (_caps.Count > 0)
        {
            content = _caps[^1].TargetX + _caps[^1].Width - padding.Left;
        }
        else if (_hint?.Shaped is { } shaped)
        {
            content = _hint.X + shaped.WidthIncludingTrailingWhitespace - padding.Left;
        }
        return new Size(padding.Left + Math.Max(0, content) + padding.Right, Math.Max(KeyCapHeight, PlaceholderFontSize * 1.4));
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    // ---- rendering ----------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole row is the button (hit testing follows drawn pixels).
        context.FillRectangle(Brushes.Transparent, bounds);
        EnsureColors();
        var cy = bounds.Height / 2;

        if (_view.IsRecording && AccentBrush is { } accent)
        {
            var d = RecordingDotDiameter;
            var x = Padding.Left + RecordingDotMargin.Left;
            using (context.PushOpacity(Math.Clamp(_dot?.Value ?? 1, 0, 1)))
            {
                context.DrawEllipse(accent, null, new Rect(x, cy - d / 2, d, d));
            }
        }

        foreach (var hint in _leavingHints)
        {
            DrawHint(context, hint, cy);
        }
        if (_hint is not null)
        {
            DrawHint(context, _hint, cy);
        }
        foreach (var cap in _leavingCaps)
        {
            DrawCap(context, cap, cy);
        }
        foreach (var cap in _caps)
        {
            DrawCap(context, cap, cy);
        }
    }

    private (double Opacity, double Y, double Blur) HintPresence(Hint hint)
    {
        if (hint.Leaving)
        {
            var q = Math.Clamp(hint.Exit.Value, 0, 1);
            return (hint.FromOpacity * (1 - q), hint.FromY, hint.FromBlur);
        }
        var p = Math.Clamp(hint.Enter.Value, 0, 1);
        return hint.FadeOnly ? (p, 0, 0) : (p, 4 * (1 - p), BlurSubtle * (1 - p));
    }

    private void DrawHint(DrawingContext context, Hint hint, double cy)
    {
        if (hint.Shaped is not { } text)
        {
            return;
        }
        var (opacity, y, blur) = HintPresence(hint);
        if (opacity <= 0.001)
        {
            return;
        }
        var origin = new Point(hint.X, cy - text.Height / 2 + y);
        using (context.PushOpacity(opacity))
        using (PushBlur(context, blur, new Rect(origin, new Size(text.WidthIncludingTrailingWhitespace, text.Height))))
        {
            context.DrawText(text, origin);
        }
    }

    private (double Opacity, double Scale, double Y, double Blur) CapPresence(Cap cap)
    {
        if (cap.Leaving)
        {
            var q = Math.Clamp(cap.Exit.Value, 0, 1);
            if (cap.FadeOnly)
            {
                return (cap.FromOpacity * (1 - q), cap.FromScale, cap.FromY, cap.FromBlur);
            }
            return (cap.FromOpacity * (1 - q),
                    FieldDrawing.Lerp(cap.FromScale, 0.8, q),
                    cap.FromY,
                    FieldDrawing.Lerp(cap.FromBlur, BlurSubtle, q));
        }
        var opacity = Math.Clamp(cap.Fade.Value, 0, 1);
        if (cap.FadeOnly)
        {
            return (opacity, 1, 0, 0);
        }
        var pop = cap.Pop.Value;
        return (opacity, 0.72 + 0.28 * pop, 4 * (1 - pop), BlurSubtle * (1 - Math.Clamp(cap.Sharpen.Value, 0, 1)));
    }

    private void DrawCap(DrawingContext context, Cap cap, double cy)
    {
        if (cap.Text is not { } text)
        {
            return;
        }
        var (opacity, scale, y, blur) = CapPresence(cap);
        if (opacity <= 0.001)
        {
            return;
        }
        var w = cap.Width;
        var h = KeyCapHeight;
        var x = cap.Leaving ? cap.FrozenX : cap.X.Value;
        var press = Math.Clamp(cap.Press.Value, 0, 1);
        var warn = Math.Clamp(cap.Warn.Value, 0, 1);
        var edge = KeyCapBottomEdge - (KeyCapBottomEdge - 1) * press;
        UpdateCapBrushes(cap, press, warn);

        // The slot scales around its centre; the held cap drops a pixel inside it.
        var matrix = Matrix.CreateTranslation(-w / 2, -h / 2 + cap.Drop.Value)
                     * Matrix.CreateScale(scale, scale)
                     * Matrix.CreateTranslation(x + w / 2, cy + y);
        var local = new Rect(0, 0, w, h);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(matrix))
        using (PushBlur(context, blur, local))
        {
            var radius = KeyCapRadius.TopLeft;
            context.DrawRectangle(cap.Fill, null, new RoundedRect(local, radius, radius));
            context.DrawGeometry(cap.Edge, null, RingFor(cap, w, h, edge, radius));
            // Content box: 1px top edge, the bottom edge and 1px bottom padding; the label centres in it.
            var contentTop = 1.0;
            var contentHeight = h - 1 - edge - KeyCapPadding.Bottom;
            var tx = (w - text.WidthIncludingTrailingWhitespace) / 2;
            var ty = contentTop + (contentHeight - text.Height) / 2;
            context.DrawText(text, new Point(tx, ty));
        }
    }

    private static DrawingContext.PushedState? PushBlur(DrawingContext context, double radius, Rect bounds)
    {
        var effect = ArcBlur.Get(radius);
        return effect is null ? null : context.PushEffect(effect, bounds.Inflate(radius * 3));
    }

    private void EnsureColors()
    {
        if (!_colorsDirty)
        {
            return;
        }
        _colorsDirty = false;
        static Color C(IBrush? brush) => brush is ISolidColorBrush solid ? Oklch.WithAlpha(solid.Color, solid.Color.A / 255.0 * solid.Opacity) : Colors.Transparent;
        _edge = C(KeyCapEdgeBrush);
        _fill = C(KeyCapFill);
        _ink = C(KeyCapForeground);
        var accent = C(AccentBrush);
        _pressedEdge = Oklch.MixOklab(_edge, accent, 0.55);
        _pressedFill = C(AccentSubtleBrush);
        _pressedInk = C(AccentStrongBrush);
        _warnEdge = C(KeyCapWarningEdge);
        _warnFill = C(KeyCapWarningFill);
        foreach (var cap in _caps.Concat(_leavingCaps))
        {
            cap.Edge = null;
            cap.Fill = null;
            cap.Ink = null;
        }
    }

    private void UpdateCapBrushes(Cap cap, double press, double warn)
    {
        var edge = Blend(_edge, _pressedEdge, _warnEdge, press, warn);
        var fill = Blend(_fill, _pressedFill, _warnFill, press, warn);
        var ink = Blend(_ink, _pressedInk, _ink, press, warn);
        // New immutable brushes only while a colour is actually changing (a 90–160ms tween), never per idle frame.
        if (cap.Edge is null || cap.EdgeColor != edge)
        {
            cap.EdgeColor = edge;
            cap.Edge = new ImmutableSolidColorBrush(edge);
        }
        if (cap.Fill is null || cap.FillColor != fill)
        {
            cap.FillColor = fill;
            cap.Fill = new ImmutableSolidColorBrush(fill);
        }
        if (cap.Ink is null || cap.TextColor != ink)
        {
            cap.TextColor = ink;
            cap.Ink = new ImmutableSolidColorBrush(ink);
            cap.Text?.SetForegroundBrush(cap.Ink);
        }
    }

    /// <summary>Premultiplied blend of the rest, held and warning colours (the states never overlap; tweens cross-fade).</summary>
    private static Color Blend(Color rest, Color held, Color warning, double p, double w)
    {
        static double Ch(double r, double h, double wa, double p, double w) => r + (h - r) * p + (wa - r) * w;
        double ra = rest.A / 255.0, ha = held.A / 255.0, wa = warning.A / 255.0;
        var a = Math.Clamp(Ch(ra, ha, wa, p, w), 0, 1);
        if (a <= 0.0001)
        {
            return Colors.Transparent;
        }
        byte Channel(byte r, byte h, byte wc)
        {
            var premultiplied = Ch(r * ra, h * ha, wc * wa, p, w);
            return (byte)Math.Clamp(Math.Round(premultiplied / a), 0, 255);
        }
        return Color.FromArgb((byte)Math.Round(a * 255), Channel(rest.R, held.R, warning.R), Channel(rest.G, held.G, warning.G),
            Channel(rest.B, held.B, warning.B));
    }

    /// <summary>The cap edge: 1px sides and top, a heavier bottom (2px at rest, 1px held), cached per width and edge.</summary>
    private static Geometry RingFor(Cap cap, double w, double h, double edge, double radius)
    {
        edge = Math.Round(edge * 8) / 8;
        if (cap.Ring is not null && Math.Abs(cap.RingWidth - w) < 0.01 && Math.Abs(cap.RingEdge - edge) < 0.001)
        {
            return cap.Ring;
        }
        var ring = new StreamGeometry();
        using (var ctx = ring.Open())
        {
            ctx.SetFillRule(FillRule.EvenOdd);
            AddRoundedRect(ctx, new Rect(0, 0, w, h), new Vector(radius, radius), new Vector(radius, radius));
            var inner = new Rect(1, 1, Math.Max(0, w - 2), Math.Max(0, h - 1 - edge));
            AddRoundedRect(ctx, inner, new Vector(radius - 1, radius - 1), new Vector(radius - 1, Math.Max(0, radius - edge)));
        }
        cap.Ring = ring;
        cap.RingWidth = w;
        cap.RingEdge = edge;
        return ring;
    }

    private static void AddRoundedRect(StreamGeometryContext ctx, Rect r, Vector top, Vector bottom)
    {
        ctx.BeginFigure(new Point(r.Left + top.X, r.Top), true);
        ctx.LineTo(new Point(r.Right - top.X, r.Top));
        ctx.ArcTo(new Point(r.Right, r.Top + top.Y), new Size(top.X, top.Y), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(r.Right, r.Bottom - bottom.Y));
        ctx.ArcTo(new Point(r.Right - bottom.X, r.Bottom), new Size(bottom.X, bottom.Y), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(r.Left + bottom.X, r.Bottom));
        ctx.ArcTo(new Point(r.Left, r.Bottom - bottom.Y), new Size(bottom.X, bottom.Y), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(r.Left, r.Top + top.Y));
        ctx.ArcTo(new Point(r.Left + top.X, r.Top), new Size(top.X, top.Y), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(true);
    }

    // ---- automation -----------------------------------------------------------------------------------------------------

    /// <summary>A button with a pressed state (<c>aria-pressed</c>): Toggle pattern, On while recording.</summary>
    private sealed class ShortcutRecorderButtonPeer : ButtonAutomationPeer, IToggleProvider
    {
        public ShortcutRecorderButtonPeer(ShortcutRecorderButton owner)
            : base(owner)
        {
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsOnProperty)
                {
                    RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                        ToState(e.GetOldValue<bool>()), ToState(e.GetNewValue<bool>()));
                }
            };
        }

        private new ShortcutRecorderButton Owner => (ShortcutRecorderButton)base.Owner;

        public ToggleState ToggleState => ToState(Owner.IsOn);

        public void Toggle()
        {
            if (Owner.IsEffectivelyEnabled)
            {
                Owner.Owner?.ToggleFromAutomation();
            }
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        private static ToggleState ToState(bool on) => on ? ToggleState.On : ToggleState.Off;
    }
}
