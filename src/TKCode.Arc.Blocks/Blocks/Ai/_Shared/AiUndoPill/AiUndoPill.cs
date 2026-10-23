using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pill above the side panel's box after a change is applied (spec 3.8, F13, F16): "<see cref="Text"/> │ ↺
/// <see cref="UndoText"/>" ("Summary updated │ ↺ Undo"). The whole pill is one button: 32 high, padding 0 12, parts 6px
/// apart (the label 12 / 16.8 in TextMuted, a 1 × 14 Border line with 2px more on each side, a 13px RotateCcw and "Undo"
/// 12 / 500, both Foreground), a 1px Border on SurfaceRaised with ShadowRaised; hover SurfaceMuted. Enter or Space (or a
/// press) raises <see cref="UndoRequested"/>. It never goes away by itself: <see cref="IsActive"/> shows it (opacity
/// 0 → 1 in 160ms standard while it rises 4px) and hides it (100ms ease-out, then out of the tab order). A new
/// <see cref="Text"/> while it shows replaces the pill the way Arc swaps it: out in 100ms, then in with the new label, in
/// place (the focus stays). Reduced motion fades 120ms without rising; Off switches at once. The accessible name is
/// <see cref="AccessibleName"/> ("Undo: Summary updated"); every word comes from the owner.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>): the way in and the way out run different durations and curves, the swap
/// chains an exit and an entry, and the pill leaves the tab order only after its fade.
/// </para>
/// </summary>
internal sealed class AiUndoPill : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";

    /// <summary>How far the pill rises while it enters.</summary>
    public const double Rise = 4;

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly Easing s_linear = new LinearEasing();
    private static readonly TimeSpan s_exit = TimeSpan.FromMilliseconds(100);

    /// <summary>The label ("Summary updated").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiUndoPill, string?>(nameof(Text));

    /// <summary>The action ("Undo").</summary>
    public static readonly StyledProperty<string?> UndoTextProperty =
        AvaloniaProperty.Register<AiUndoPill, string?>(nameof(UndoText));

    /// <summary>The accessible name ("Undo: Summary updated"); null reads the action and the label.</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<AiUndoPill, string?>(nameof(AccessibleName));

    /// <summary>Whether the pill shows.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AiUndoPill, bool>(nameof(IsActive));

    /// <summary>The label on screen (it changes after the old one faded during a swap).</summary>
    public static readonly DirectProperty<AiUndoPill, string?> ShownTextProperty =
        AvaloniaProperty.RegisterDirect<AiUndoPill, string?>(nameof(ShownText), o => o.ShownText);

    /// <summary>Raised (bubbling) when the person presses the pill.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> UndoRequestedEvent =
        RoutedEvent.Register<AiUndoPill, RoutedEventArgs>(nameof(UndoRequested), RoutingStrategies.Bubble);

    private readonly TranslateTransform _shift = new();
    private MotionValue<double>? _progress;
    private string? _shownText;
    private bool _entering;
    private int _generation;

    static AiUndoPill()
    {
        IsVisibleProperty.OverrideDefaultValue<AiUndoPill>(false);
    }

    public AiUndoPill()
    {
        RenderTransform = _shift;
        Opacity = 0;
        RefreshName();
    }

    protected override Type StyleKeyOverride => typeof(AiUndoPill);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="UndoTextProperty"/>
    public string? UndoText { get => GetValue(UndoTextProperty); set => SetValue(UndoTextProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    /// <inheritdoc cref="ShownTextProperty"/>
    public string? ShownText { get => _shownText; private set => SetAndRaise(ShownTextProperty, ref _shownText, value); }

    /// <inheritdoc cref="UndoRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? UndoRequested
    {
        add => AddHandler(UndoRequestedEvent, value);
        remove => RemoveHandler(UndoRequestedEvent, value);
    }

    /// <summary>The opacity on screen (0 hidden, 1 shown).</summary>
    internal double Progress => _progress?.Value ?? (IsActive ? 1 : 0);

    /// <summary>True while a fade or a swap runs.</summary>
    internal bool IsFading => _progress?.IsAnimating == true;

    private MotionValue<double> Value
    {
        get
        {
            if (_progress is null)
            {
                // From what is on screen: the first show starts transparent.
                _progress = new MotionValue<double>(this, IsVisible ? Opacity : 0, MotionVectors.Double);
                _progress.Changed += Apply;
            }
            return _progress;
        }
    }

    private ArcMotionLevel Level => TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    protected override void OnClick()
    {
        base.OnClick();
        if (IsActive)
        {
            RaiseEvent(new RoutedEventArgs(UndoRequestedEvent, this));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            SetShown(change.GetNewValue<bool>());
        }
        else if (change.Property == TextProperty)
        {
            OnTextChanged();
        }
        else if (change.Property == UndoTextProperty || change.Property == AccessibleNameProperty)
        {
            RefreshName();
        }
    }

    private void RefreshName()
    {
        var name = AccessibleName;
        if (string.IsNullOrEmpty(name))
        {
            name = string.IsNullOrEmpty(UndoText) ? ShownText : string.IsNullOrEmpty(ShownText) ? UndoText : UndoText + ": " + ShownText;
        }
        AutomationProperties.SetName(this, name);
    }

    private void SetShown(bool shown)
    {
        var generation = ++_generation;
        var level = Level;
        if (shown)
        {
            ShownText = Text;
            RefreshName();
            IsVisible = true;
            Enter(level);
            return;
        }
        _entering = false;
        if (level == ArcMotionLevel.Off)
        {
            Value.Jump(0);
            IsVisible = false;
            return;
        }
        var spec = level == ArcMotionLevel.Full ? MotionSpec.Tween(s_exit, s_easeOut) : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear);
        _ = HideWhenDone(Value.AnimateTo(0, spec), generation);
    }

    private void Enter(ArcMotionLevel level)
    {
        _entering = level == ArcMotionLevel.Full;
        switch (level)
        {
            case ArcMotionLevel.Full:
                _ = Value.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                break;
            case ArcMotionLevel.Reduced:
                _ = Value.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                break;
            default:
                Value.Jump(1);
                break;
        }
    }

    private async Task HideWhenDone(Task<bool> run, int generation)
    {
        if (await run && generation == _generation && !IsActive)
        {
            IsVisible = false;
        }
    }

    private void OnTextChanged()
    {
        if (!IsActive || !IsVisible || Level == ArcMotionLevel.Off || Value.Value <= 0.001)
        {
            ShownText = Text;
            RefreshName();
            return;
        }
        // Arc removes the old pill (100ms out) and brings in a new one: here the same button swaps its label in between.
        var generation = ++_generation;
        _entering = false;
        var level = Level;
        var spec = level == ArcMotionLevel.Full ? MotionSpec.Tween(s_exit, s_easeOut) : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear);
        _ = SwapWhenOut(Value.AnimateTo(0, spec), generation);
    }

    private async Task SwapWhenOut(Task<bool> run, int generation)
    {
        if (!await run || generation != _generation || !IsActive)
        {
            return;
        }
        ShownText = Text;
        RefreshName();
        Enter(Level);
    }

    private void Apply(double value)
    {
        var p = Math.Clamp(value, 0, 1);
        Opacity = p;
        // Only the way in rises; the way out fades in place.
        _shift.Y = _entering ? Rise * (1 - p) : 0;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}
