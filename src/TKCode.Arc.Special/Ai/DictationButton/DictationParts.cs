using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The mic's slot, which springs into the recording pill. Its children are the faces (the mic, the recording pill, Try
/// again); one is active at a time. The slot's width springs to the active face's width (critically damped, 0.24s visual
/// duration) while it stays anchored on the right, so the mic grows leftward and its spot becomes Insert. Faces crossfade
/// (out 100ms, in 160ms); a face that is not active takes no pointer or Tab, and hides once faded. A rounded fill behind
/// the faces fades in for the pill and Try again.
/// <para>
/// Code-driven: the width target is the measured width of whichever face is active (Try again fits its label), and the
/// crossfade uses different durations in and out, neither of which a ControlTheme transition can express.
/// </para>
/// </summary>
internal sealed class DictationPill : Panel
{
    /// <summary>The fill behind the pill and Try again.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<DictationPill, IBrush?>(nameof(Fill));

    internal static readonly SpringSpec WidthSpring = SpringSpec.FromVisual(0.24, 0);
    internal static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(100);
    internal static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(160);

    private readonly Border _fill = new() { IsHitTestVisible = false, Opacity = 0 };
    private readonly Dictionary<Control, MotionValue<double>> _fades = new();
    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _width;
    private MotionValue<double>? _fillOpacity;
    private Control? _active;
    private double _target = -1;
    private bool _filled;
    private bool _instantNext = true;

    public DictationPill()
    {
        _fill.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        Children.Add(_fill);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>The face on show.</summary>
    internal Control? ActiveFace => _active;

    /// <summary>The width the slot is springing to.</summary>
    internal double TargetWidth => _target;

    /// <summary>The slot's current width.</summary>
    internal double CurrentWidth => _width?.Value ?? 0;

    /// <summary>True while the width springs.</summary>
    internal bool IsMoving => _width?.IsAnimating == true;

    /// <summary>The fill's current opacity.</summary>
    internal double FillOpacity => _fill.Opacity;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FillProperty)
        {
            _fill.Background = Fill;
        }
    }

    /// <summary>Shows <paramref name="face"/> (one of the children), with the fill behind it or not.</summary>
    internal void Show(Control face, bool filled, bool instant)
    {
        instant |= _instantNext || !IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off;
        _instantNext = false;
        if (_active == face && _filled == filled)
        {
            return;
        }
        _active = face;
        foreach (var child in Children)
        {
            if (child == _fill)
            {
                continue;
            }
            var active = child == face;
            child.IsHitTestVisible = active;
            KeyboardNavigation.SetTabNavigation(child, active ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
            KeyboardNavigation.SetIsTabStop(child, active);
            if (active)
            {
                child.IsVisible = true;
                Fade(child, 1, instant, FadeIn);
            }
            else if (child.IsVisible)
            {
                Fade(child, 0, instant, FadeOut);
            }
        }
        if (_filled != filled || instant)
        {
            _filled = filled;
            _fillOpacity ??= CreateFillOpacity();
            if (instant)
            {
                _fillOpacity.Jump(filled ? 1 : 0);
            }
            else
            {
                _ = _fillOpacity.AnimateTo(filled ? 1 : 0, MotionSpec.Tween(filled ? FadeIn : FadeOut, ArcEasings.Standard));
            }
        }
        if (instant)
        {
            _target = -1;
            _jumpNext = true;
        }
        InvalidateMeasure();
    }

    private bool _jumpNext;

    private MotionValue<double> CreateFillOpacity()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v => _fill.Opacity = v;
        return value;
    }

    private void Fade(Control child, double to, bool instant, TimeSpan duration)
    {
        if (!_fades.TryGetValue(child, out var fade))
        {
            fade = new MotionValue<double>(this, child.Opacity, MotionVectors.Double);
            var target = child;
            fade.Changed += v => target.Opacity = v;
            _fades[child] = fade;
        }
        if (instant)
        {
            fade.Jump(to);
            child.Opacity = to;
            if (to <= 0)
            {
                child.IsVisible = false;
            }
            return;
        }
        _ = FadeAsync(child, fade, to, duration);
    }

    private async Task FadeAsync(Control child, MotionValue<double> fade, double to, TimeSpan duration)
    {
        var done = await fade.AnimateTo(to, MotionSpec.Tween(duration, ArcEasings.Standard));
        if (done && to <= 0 && child != _active)
        {
            child.IsVisible = false;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        var childSize = new Size(double.PositiveInfinity, availableSize.Height);
        foreach (var child in Children)
        {
            if (child == _fill)
            {
                continue;
            }
            child.Measure(childSize);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var target = _active?.DesiredSize.Width ?? 0;
        _width ??= CreateWidth(target);
        if (Math.Abs(target - _target) > 0.01)
        {
            var first = _target < 0;
            _target = target;
            if (first || _jumpNext || !IsLoaded || !ArcMotion.IsFull(this))
            {
                _width.Jump(target);
            }
            else
            {
                _ = SpringAsync(target);
            }
            _jumpNext = false;
        }
        _fill.Measure(new Size(_width.Value, height));
        return new Size(Math.Max(0, _width.Value), height);
    }

    private async Task SpringAsync(double target)
    {
        await _width!.AnimateTo(target, MotionSpec.Spring(WidthSpring));
        InvalidateArrange();
    }

    private MotionValue<double> CreateWidth(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        _fill.Arrange(new Rect(0, 0, width, finalSize.Height));
        foreach (var child in Children)
        {
            if (child == _fill)
            {
                continue;
            }
            var w = child.DesiredSize.Width;
            child.Arrange(new Rect(width - w, 0, w, finalSize.Height));
        }
        if (IsMoving)
        {
            // While it springs the faces are clipped to the growing pill; at rest focus rings may spill out.
            _clip.Rect = new Rect(0, 0, width, finalSize.Height);
            _clip.RadiusX = finalSize.Height / 2;
            _clip.RadiusY = finalSize.Height / 2;
            Clip = _clip;
            InvalidateVisual();
        }
        else if (Clip is not null)
        {
            Clip = null;
            InvalidateVisual();
        }
        return finalSize;
    }
}

/// <summary>
/// A round icon button of the dictation parts: the 36px mic and send, the pill's 28px cancel and Insert, and Try again with
/// its label. <see cref="IsBusy"/> crossfades the icon into a spinner. Its accessible name is its
/// <c>AutomationProperties.Name</c>; the glyph is not read.
/// </summary>
[PseudoClasses(HoverPseudoClass, BusyPseudoClass, LabelledPseudoClass)]
internal sealed class DictationIconButton : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";
    public const string BusyPseudoClass = ":busy";
    public const string LabelledPseudoClass = ":labelled";

    /// <summary>A 24 × 24 stroked glyph.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DictationIconButton, Geometry?>(nameof(Icon));

    /// <summary>The glyph's size.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<DictationIconButton, double>(nameof(IconSize), 18);

    /// <summary>Text after the icon (Try again); null for an icon-only button.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DictationIconButton, string?>(nameof(Text));

    /// <summary>Shows a spinner in place of the icon.</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<DictationIconButton, bool>(nameof(IsBusy));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    protected override Type StyleKeyOverride => typeof(DictationIconButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusyProperty)
        {
            PseudoClasses.Set(BusyPseudoClass, IsBusy);
        }
        else if (change.Property == TextProperty)
        {
            PseudoClasses.Set(LabelledPseudoClass, !string.IsNullOrEmpty(Text));
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(DictationIconButton owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>The model label of the composer's toolbar: a small mark and the name.</summary>
internal sealed class DictationModelLabel : StackPanel
{
    public DictationModelLabel(DictationModel model)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        VerticalAlignment = VerticalAlignment.Center;
        Model = model;
        var glyph = new ArcGlyph { Data = model.Mark ?? ArcIcons.Sparkles, Size = 16, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Accent);
        var name = new TextBlock
        {
            Text = model.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ArcTokens.Bind(name, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        Children.Add(glyph);
        Children.Add(name);
    }

    public DictationModel Model { get; }
}
