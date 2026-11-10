using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One person in a <see cref="PresenceStack"/>: an Arc <see cref="Avatar"/> inside a 2px ring in the person's color and
/// a 1px surface edge that separates it from its neighbour. It is focusable (the stack is one tab stop; the arrow keys
/// move between people) and its tooltip and accessible name carry the name, the detail and "idle". The stack moves it
/// (slide, grow in, shrink out) through <see cref="Visual.RenderTransform"/> and <see cref="Visual.Opacity"/>; a join also
/// sends one ring out from it.
/// </summary>
internal sealed class PresenceStackSlot : Control
{
    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<PresenceStackSlot, IBrush?>(nameof(SurfaceBrush));

    internal static readonly StyledProperty<IBrush?> IdleBrushProperty =
        AvaloniaProperty.Register<PresenceStackSlot, IBrush?>(nameof(IdleBrush));

    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<PresenceStackSlot, IBrush?>(nameof(FocusBrush));

    internal static readonly StyledProperty<IBrush?> Series1Property =
        AvaloniaProperty.Register<PresenceStackSlot, IBrush?>(nameof(Series1));

    internal static readonly StyledProperty<IBrush?> Series2Property =
        AvaloniaProperty.Register<PresenceStackSlot, IBrush?>(nameof(Series2));

    internal static readonly StyledProperty<IBrush?> Series3Property =
        AvaloniaProperty.Register<PresenceStackSlot, IBrush?>(nameof(Series3));

    internal static readonly StyledProperty<IBrush?> Series4Property =
        AvaloniaProperty.Register<PresenceStackSlot, IBrush?>(nameof(Series4));

    internal static readonly StyledProperty<double> IdleOpacityProperty =
        AvaloniaProperty.Register<PresenceStackSlot, double>(nameof(IdleOpacity), 0.45);

    /// <summary>The ring (2) and the surface edge outside it (1).</summary>
    public const double RingWidth = 2;

    public const double EdgeWidth = 1;

    private readonly Avatar _avatar = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly MatrixTransform _transform = new();
    private PresenceStackViewer? _viewer;
    private IPen? _ringPen;
    private IBrush? _ringPenBrush;
    private IPen? _focusPen;
    private IBrush? _focusPenBrush;
    private ImmutablePen? _pulsePen;
    private Color _pulseColor;
    private bool _watching;
    private bool _attached;

    static PresenceStackSlot()
    {
        FocusableProperty.OverrideDefaultValue<PresenceStackSlot>(true);
        FocusAdornerProperty.OverrideDefaultValue<PresenceStackSlot>(null);
        AffectsRender<PresenceStackSlot>(SurfaceBrushProperty, IdleBrushProperty, FocusBrushProperty, Series1Property,
            Series2Property, Series3Property, Series4Property);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<PresenceStackSlot>(AutomationControlType.ListItem);
    }

    public PresenceStackSlot()
    {
        AutomationProperties.SetAccessibilityView(_avatar, AccessibilityView.Raw);
        _avatar.IsHitTestVisible = false;
        VisualChildren.Add(_avatar);
        LogicalChildren.Add(_avatar);
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.TopLeft;
        ClipToBounds = false;
    }

    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    public IBrush? IdleBrush { get => GetValue(IdleBrushProperty); set => SetValue(IdleBrushProperty, value); }

    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public IBrush? Series1 { get => GetValue(Series1Property); set => SetValue(Series1Property, value); }

    public IBrush? Series2 { get => GetValue(Series2Property); set => SetValue(Series2Property, value); }

    public IBrush? Series3 { get => GetValue(Series3Property); set => SetValue(Series3Property, value); }

    public IBrush? Series4 { get => GetValue(Series4Property); set => SetValue(Series4Property, value); }

    /// <summary>The avatar's opacity while the person is idle.</summary>
    public double IdleOpacity { get => GetValue(IdleOpacityProperty); set => SetValue(IdleOpacityProperty, value); }

    /// <summary>The person shown.</summary>
    public PresenceStackViewer? Viewer => _viewer;

    /// <summary>The avatar inside (tests).</summary>
    public Avatar Avatar => _avatar;

    /// <summary>True once the stack let the person go; the slot is removed after its exit.</summary>
    public bool IsLeaving { get; set; }

    /// <summary>Where the slot is drawn now (x) and where it is heading, in the stack's coordinates.</summary>
    public MotionValue<double>? X { get; set; }

    /// <summary>How present the slot is: 0 gone, 1 fully in. Scales (full motion) and fades it.</summary>
    public MotionValue<double>? Presence { get; set; }

    /// <summary>The join ring, 0 to 1 while it travels out; null when there is none.</summary>
    public MotionValue<double>? Pulse { get; set; }

    /// <summary>The x the stack placed it at (arrange position).</summary>
    public double LayoutX { get; set; }

    /// <summary>The index of the series color the ring takes when the viewer has no brush.</summary>
    public int SeriesIndex { get; private set; }

    public void SetViewer(PresenceStackViewer viewer, AvatarSize size)
    {
        if (!ReferenceEquals(_viewer, viewer))
        {
            Unwatch();
            _viewer = viewer;
            if (_attached)
            {
                Watch();
            }
        }
        _avatar.Size = size;
        Update();
    }

    public void SetSize(AvatarSize size)
    {
        _avatar.Size = size;
        InvalidateMeasure();
    }

    /// <summary>Applies the motion values: slide from the layout position, scale around the center, fade.</summary>
    public void ApplyMotion(bool scales)
    {
        var x = X?.Value ?? LayoutX;
        var presence = Math.Clamp(Presence?.Value ?? 1, 0, 1.2);
        var scale = scales ? 0.55 + 0.45 * presence : 1;
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        _transform.Matrix = new Matrix(scale, 0, 0, scale, cx - cx * scale + (x - LayoutX), cy - cy * scale);
        Opacity = Math.Clamp(presence, 0, 1);
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Watch();
        Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Unwatch();
    }

    private void Watch()
    {
        if (_viewer is not null && !_watching)
        {
            _viewer.PropertyChanged += OnViewerChanged;
            _watching = true;
        }
    }

    private void Unwatch()
    {
        if (_viewer is not null && _watching)
        {
            _viewer.PropertyChanged -= OnViewerChanged;
        }
        _watching = false;
    }

    private void OnViewerChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Update();

    private void Update()
    {
        var viewer = _viewer;
        if (viewer is null)
        {
            return;
        }
        _avatar.DisplayName = viewer.Name;
        _avatar.Source = viewer.Source;
        _avatar.Opacity = viewer.IsIdle ? IdleOpacity : 1;
        SeriesIndex = PresenceStack.SeriesIndexOf(viewer.Name);
        var name = PresenceStack.Describe(viewer);
        AutomationProperties.SetName(this, name);
        Tooltip.SetTip(this, name);
        InvalidateVisual();
    }

    /// <summary>The ring's brush: the person's, or their series color; gray while idle.</summary>
    public IBrush? RingBrush
    {
        get
        {
            if (_viewer is { IsIdle: true })
            {
                return IdleBrush;
            }
            return _viewer?.Brush ?? (SeriesIndex % 4) switch
            {
                0 => Series1,
                1 => Series2,
                2 => Series3,
                _ => Series4
            };
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _avatar.Measure(Size.Infinity);
        var d = _avatar.DesiredSize.Width;
        var s = d + 2 * (RingWidth + EdgeWidth);
        return new Size(s, s);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var d = _avatar.DesiredSize;
        _avatar.Arrange(new Rect((finalSize.Width - d.Width) / 2, (finalSize.Height - d.Height) / 2, d.Width, d.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var center = new Point(size.Width / 2, size.Height / 2);
        var outer = Math.Min(size.Width, size.Height) / 2;
        if (SurfaceBrush is { } surface)
        {
            context.DrawEllipse(surface, null, center, outer, outer);
        }
        if (RingBrush is { } ring)
        {
            if (!ReferenceEquals(ring, _ringPenBrush))
            {
                _ringPenBrush = ring;
                _ringPen = new ImmutablePen(ring.ToImmutable(), RingWidth);
            }
            var r = outer - EdgeWidth - RingWidth / 2;
            context.DrawEllipse(null, _ringPen, center, r, r);
            if (Pulse is { } pulse && pulse.Value is > 0 and < 1 && ring is ISolidColorBrush solid)
            {
                // The join ring: it travels out to 1.7 times the radius and fades as it goes.
                var color = solid.Color;
                if (_pulsePen is null || color != _pulseColor)
                {
                    _pulseColor = color;
                    _pulsePen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.5);
                }
                using (context.PushOpacity((1 - pulse.Value) * 0.8))
                {
                    var pr = outer * (1 + 0.7 * pulse.Value);
                    context.DrawEllipse(null, _pulsePen, center, pr, pr);
                }
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusBrush is { } focus)
        {
            if (!ReferenceEquals(focus, _focusPenBrush))
            {
                _focusPenBrush = focus;
                _focusPen = new ImmutablePen(focus.ToImmutable(), 2);
            }
            context.DrawEllipse(null, _focusPen, center, outer + 2, outer + 2);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}

/// <summary>
/// The "+N" chip after the visible people: a round button whose count rolls the way it moved. A click, Enter or Space
/// opens the list of everyone viewing. It moves with the stack like a slot.
/// </summary>
internal sealed class PresenceStackOverflow : AvaloniaButton
{
    private readonly RollingDigits _digits = new() { Prefix = "+", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly MatrixTransform _transform = new();
    private bool _shown;

    public PresenceStackOverflow()
    {
        Content = _digits;
        AutomationProperties.SetAccessibilityView(_digits, AccessibilityView.Raw);
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.TopLeft;
        ClipToBounds = false;
    }

    protected override Type StyleKeyOverride => typeof(PresenceStackOverflow);

    /// <summary>The hidden people the chip counts.</summary>
    public int Count { get; private set; }

    /// <summary>The count the digits show (tests).</summary>
    public double ShownCount => _digits.Value;

    public MotionValue<double>? X { get; set; }

    public MotionValue<double>? Presence { get; set; }

    public double LayoutX { get; set; }

    public bool IsLeaving { get; set; }

    public void SetCount(int count, string name, bool animate)
    {
        Count = count;
        if (count <= 0)
        {
            return;
        }
        _digits.IsInstant = !_shown || !animate;
        _digits.Value = count;
        _shown = true;
        AutomationProperties.SetName(this, name);
        Tooltip.SetTip(this, name);
    }

    public void ApplyMotion(bool scales)
    {
        var x = X?.Value ?? LayoutX;
        var presence = Math.Clamp(Presence?.Value ?? 1, 0, 1.2);
        var scale = scales ? 0.55 + 0.45 * presence : 1;
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        _transform.Matrix = new Matrix(scale, 0, 0, scale, cx - cx * scale + (x - LayoutX), cy - cy * scale);
        Opacity = Math.Clamp(presence, 0, 1);
    }

    /// <summary>The digits (tests).</summary>
    internal RollingDigits Digits => _digits;
}

/// <summary>A row of the full list: the avatar, the name, and the detail or "Idle" under it.</summary>
internal sealed class PresenceStackListRow : Control
{
    private readonly Avatar _avatar = new() { Size = AvatarSize.Small, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _detail = new() { TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 };
    private readonly StackPanel _text = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ArcShrinkRow _row = new() { Spacing = 10 };

    public PresenceStackListRow(PresenceStackViewer viewer, string idleText)
    {
        _avatar.DisplayName = viewer.Name;
        _avatar.Source = viewer.Source;
        _avatar.Opacity = viewer.IsIdle ? 0.5 : 1;
        AutomationProperties.SetAccessibilityView(_avatar, AccessibilityView.Raw);
        _name.Text = viewer.Name;
        var detail = viewer.IsIdle ? (viewer.Detail is { Length: > 0 } d ? idleText + " · " + d : idleText) : viewer.Detail;
        _detail.Text = detail;
        _detail.IsVisible = !string.IsNullOrEmpty(detail);
        ArcTokens.Bind(_name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_detail, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _text.Children.Add(_name);
        _text.Children.Add(_detail);
        ArcShrinkRow.SetIsShrinkable(_text, true);
        _row.Children.Add(_avatar);
        _row.Children.Add(_text);
        VisualChildren.Add(_row);
        LogicalChildren.Add(_row);
        AutomationProperties.SetName(this, PresenceStack.Describe(viewer));
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _row.Measure(availableSize);
        return _row.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _row.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
