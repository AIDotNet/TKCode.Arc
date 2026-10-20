using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The shell layer behind a <see cref="ScrubInput"/>'s row (Arc <c>.shell</c> + <c>.fill</c>): a rounded
/// <c>SurfaceMuted</c> rectangle with a 1px edge and the track wash scaled from the left by the value fraction. Drawn
/// in <see cref="Render"/> so the strain stretch (width + |strain|, anchored on the far side) and the fill glide only
/// repaint; nothing relayouts per frame.
/// </summary>
internal sealed class ScrubInputShell : Control
{
    public static readonly StyledProperty<IBrush?> ShellBackgroundProperty =
        AvaloniaProperty.Register<ScrubInputShell, IBrush?>(nameof(ShellBackground));

    public static readonly StyledProperty<IBrush?> ShellBorderBrushProperty =
        AvaloniaProperty.Register<ScrubInputShell, IBrush?>(nameof(ShellBorderBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ScrubInputShell, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ScrubInputShell, CornerRadius>(nameof(CornerRadius));

    private IPen? _pen;
    private double _fraction;
    private double _strain;
    private bool _isTrackVisible;

    static ScrubInputShell()
    {
        AffectsRender<ScrubInputShell>(ShellBackgroundProperty, ShellBorderBrushProperty, TrackBrushProperty, CornerRadiusProperty);
    }

    public IBrush? ShellBackground { get => GetValue(ShellBackgroundProperty); set => SetValue(ShellBackgroundProperty, value); }

    public IBrush? ShellBorderBrush { get => GetValue(ShellBorderBrushProperty); set => SetValue(ShellBorderBrushProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Track fill fraction 0..1 (the wash's <c>scaleX</c>).</summary>
    public double Fraction
    {
        get => _fraction;
        set
        {
            if (_fraction != value)
            {
                _fraction = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Strain past a limit in px: positive past the maximum (grows right), negative past the minimum (grows left).</summary>
    public double Strain
    {
        get => _strain;
        set
        {
            if (_strain != value)
            {
                _strain = value;
                InvalidateVisual();
            }
        }
    }

    public bool IsTrackVisible
    {
        get => _isTrackVisible;
        set
        {
            if (_isTrackVisible != value)
            {
                _isTrackVisible = value;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShellBorderBrushProperty)
        {
            _pen = ShellBorderBrush is { } brush ? new Pen(brush, 1) : null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        // scaleX(1 + |strain| / width) with the origin on the far side equals width + |strain| anchored there.
        var stretch = Math.Abs(_strain);
        var outer = new Rect(_strain >= 0 ? 0 : -stretch, 0, width + stretch, height);
        var radius = Math.Min(CornerRadius.TopLeft, height / 2);
        context.DrawRectangle(ShellBackground, null, new RoundedRect(outer, radius));
        if (_isTrackVisible && _fraction > 0 && TrackBrush is { } track)
        {
            var inner = outer.Deflate(1);
            using (context.PushClip(new RoundedRect(inner, Math.Max(0, radius - 1))))
            {
                context.FillRectangle(track, new Rect(inner.X, inner.Y, inner.Width * Math.Min(1, _fraction), inner.Height));
            }
        }
        if (_pen is not null)
        {
            context.DrawRectangle(null, _pen, new RoundedRect(outer.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }
}

/// <summary>
/// The number editor of a <see cref="ScrubInput"/>: an Avalonia <see cref="AvaloniaTextBox"/> without a frame
/// (Arc <c>.input</c>). Arrow keys, Enter and Escape go to the owner before the text box sees them.
/// </summary>
internal sealed class ScrubInputEditor : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(ScrubInputEditor);

    internal ScrubInput? Owner => TemplatedParent as ScrubInput;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Owner?.HandleEditorKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        base.OnKeyDown(e);
    }

    // The peer is chosen once (Avalonia caches it): with finite limits it also reports the range (aria-valuemin/max).
    protected override AutomationPeer OnCreateAutomationPeer() =>
        Owner is { HasFiniteRange: true } ? new ScrubInputEditorRangeAutomationPeer(this) : new ScrubInputEditorAutomationPeer(this);
}

/// <summary>The editor as Arc's <c>role="spinbutton"</c>: name from the label, value text as help text, invalid as status.</summary>
internal class ScrubInputEditorAutomationPeer : TextBoxAutomationPeer
{
    public ScrubInputEditorAutomationPeer(ScrubInputEditor owner) : base(owner)
    {
    }

    protected ScrubInput? Input => ((ScrubInputEditor)Owner).Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

    protected override string? GetNameCore()
    {
        var own = AutomationProperties.GetName(Owner);
        if (!string.IsNullOrEmpty(own))
        {
            return own;
        }
        return Input?.AutomationName ?? base.GetNameCore();
    }

    protected override string? GetHelpTextCore()
    {
        var input = Input;
        if (input is null)
        {
            return base.GetHelpTextCore();
        }
        var help = AutomationProperties.GetHelpText(input);
        return string.IsNullOrEmpty(help) ? input.ValueText : help;
    }

    protected override string? GetItemStatusCore() => Input is { IsInvalid: true } ? "Invalid entry" : base.GetItemStatusCore();
}

/// <summary>Adds the finite range (aria-valuenow / valuemin / valuemax) to the spinbutton.</summary>
internal sealed class ScrubInputEditorRangeAutomationPeer : ScrubInputEditorAutomationPeer, IRangeValueProvider
{
    public ScrubInputEditorRangeAutomationPeer(ScrubInputEditor owner) : base(owner)
    {
    }

    bool IRangeValueProvider.IsReadOnly => Input is not { IsEffectivelyEnabled: true };

    public double Minimum => Input?.Minimum ?? 0;

    public double Maximum => Input?.Maximum ?? 0;

    double IRangeValueProvider.Value => Input?.Value ?? 0;

    public double SmallChange => Input?.Step ?? 1;

    public double LargeChange => Input?.EffectiveCoarseStep ?? 10;

    void IRangeValueProvider.SetValue(double value) => Input?.SetValueFromAutomation(value);
}

/// <summary>
/// The unit after the number (Arc <c>.unit</c>). With more than one unit it is a button that cycles them; with one it
/// is plain text hidden from assistive technology. The text swaps with <see cref="ScrubInputUnitSwap"/>.
/// </summary>
internal sealed class ScrubInputUnitButton : AvaloniaButton
{
    public const string CyclePseudoClass = ":cycle";
    public const string HoverPseudoClass = ":hover-fine";

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ScrubInputUnitButton, string?>(nameof(Unit));

    public static readonly StyledProperty<bool> IsCyclingProperty =
        AvaloniaProperty.Register<ScrubInputUnitButton, bool>(nameof(IsCycling));

    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    public bool IsCycling { get => GetValue(IsCyclingProperty); set => SetValue(IsCyclingProperty, value); }

    protected override Type StyleKeyOverride => typeof(ScrubInputUnitButton);

    public ScrubInputUnitButton()
    {
        ApplyCycling();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCyclingProperty)
        {
            ApplyCycling();
        }
    }

    private void ApplyCycling()
    {
        var cycling = IsCycling;
        PseudoClasses.Set(CyclePseudoClass, cycling);
        SetCurrentValue(FocusableProperty, cycling);
        SetCurrentValue(IsHitTestVisibleProperty, cycling);
        AutomationProperties.SetAccessibilityView(this, cycling ? AccessibilityView.Default : AccessibilityView.Raw);
        if (!cycling)
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    // Hover is for fine pointers only (@media (hover: hover) and (pointer: fine)).
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, IsCycling && e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// Swaps the unit text like Arc's <c>AnimatePresence mode="popLayout"</c>: the new unit rises from 6px out of a 2px blur
/// on the snappy spring while the old one leaves to -6px with a 2px blur over the instant duration; the outgoing text is
/// out of layout. Reduced motion fades both over the instant duration. Only animates when <see cref="IsAnimated"/>
/// (the cycling button); the first text never animates.
/// </summary>
internal sealed class ScrubInputUnitSwap : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ScrubInputUnitSwap, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<ScrubInputUnitSwap, bool>(nameof(IsAnimated));

    private static readonly Avalonia.Animation.Easings.Easing s_easeInOut = new Avalonia.Animation.Easings.SplineEasing(0.42, 0, 0.58, 1);

    private readonly TextBlock[] _layers = new TextBlock[2];
    private readonly TranslateTransform[] _offsets = new TranslateTransform[2];
    private readonly BlurEffect[] _blurs = new BlurEffect[2];
    private int _current;
    private bool _exiting;
    private MotionValue<double>? _enter;
    private MotionValue<double>? _exit;
    private bool _enterFull;
    private bool _exitFull;

    public ScrubInputUnitSwap()
    {
        for (var i = 0; i < 2; i++)
        {
            _offsets[i] = new TranslateTransform();
            _blurs[i] = new BlurEffect { Radius = 0 };
            _layers[i] = new TextBlock
            {
                RenderTransform = _offsets[i],
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = i == 0
            };
            LogicalChildren.Add(_layers[i]);
            VisualChildren.Add(_layers[i]);
        }
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsAnimated { get => GetValue(IsAnimatedProperty); set => SetValue(IsAnimatedProperty, value); }

    /// <summary>The text block showing the current unit (tests).</summary>
    internal TextBlock CurrentLayer => _layers[_current];

    /// <summary>True while the previous unit is still leaving.</summary>
    internal bool IsExiting => _exiting;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Swap(change.GetNewValue<string?>());
        }
    }

    private void Swap(string? text)
    {
        var current = _layers[_current];
        if (!IsAnimated || !this.IsAttachedToVisualTree() || string.IsNullOrEmpty(current.Text) || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _enter?.Stop();
            _exit?.Stop();
            current.Text = text;
            Settle(_current, true);
            var other = 1 - _current;
            _layers[other].IsVisible = false;
            _exiting = false;
            InvalidateMeasure();
            return;
        }
        var full = ArcMotion.IsFull(this);
        var leaving = _current;
        _current = 1 - _current;
        var entering = _layers[_current];
        entering.Text = text;
        entering.IsVisible = true;
        InvalidateMeasure();

        _exiting = true;
        _exitFull = full;
        _exit ??= CreateValue(OnExitChanged);
        _exit.Jump(0);
        _ = RunExit(leaving);

        _enterFull = full;
        _enter ??= CreateValue(OnEnterChanged);
        _enter.Jump(0);
        _ = _enter.AnimateTo(1, full ? MotionSpec.Spring(ArcSpring.Snappy) : MotionSpec.Tween(ArcMotionPresets.Instant, s_easeInOut));
    }

    private MotionValue<double> CreateValue(Action<double> changed)
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    private async Task RunExit(int leaving)
    {
        var reached = await _exit!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_easeInOut));
        if (reached && _current != leaving)
        {
            _layers[leaving].IsVisible = false;
            Settle(leaving, false);
            _exiting = false;
            InvalidateArrange();
        }
    }

    private void OnEnterChanged(double p)
    {
        var layer = _current;
        _layers[layer].Opacity = Math.Clamp(p, 0, 1);
        Apply(layer, _enterFull ? 6 * (1 - p) : 0, _enterFull ? 2 * Math.Max(0, 1 - p) : 0);
    }

    private void OnExitChanged(double p)
    {
        if (!_exiting)
        {
            return;
        }
        var layer = 1 - _current;
        _layers[layer].Opacity = Math.Clamp(1 - p, 0, 1);
        Apply(layer, _exitFull ? -6 * p : 0, _exitFull ? 2 * p : 0);
    }

    private void Apply(int layer, double y, double blur)
    {
        _offsets[layer].Y = y;
        if (blur > 0.01)
        {
            _blurs[layer].Radius = blur;
            if (_layers[layer].Effect is null)
            {
                _layers[layer].Effect = _blurs[layer];
            }
        }
        else if (_layers[layer].Effect is not null)
        {
            _layers[layer].Effect = null;
        }
    }

    private void Settle(int layer, bool visible)
    {
        _layers[layer].Opacity = visible ? 1 : 0;
        Apply(layer, 0, 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = default(Size);
        for (var i = 0; i < 2; i++)
        {
            _layers[i].Measure(availableSize);
            if (i == _current)
            {
                desired = _layers[i].DesiredSize;
            }
        }
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The outgoing unit is popped out of layout: it keeps its own width, centered on the slot.
        for (var i = 0; i < 2; i++)
        {
            var size = _layers[i].DesiredSize;
            var x = (finalSize.Width - size.Width) / 2;
            var y = (finalSize.Height - size.Height) / 2;
            _layers[i].Arrange(new Rect(x, y, size.Width, size.Height));
        }
        return finalSize;
    }
}
