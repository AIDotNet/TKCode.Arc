using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The looks of a <see cref="GlidingPillBar"/>.</summary>
internal enum GlidingPillBarLook
{
    /// <summary>A foreground highlight with background text (blog filters).</summary>
    Solid,

    /// <summary>A muted highlight (comparison picker, page numbers).</summary>
    Quiet,

    /// <summary>A raised surface highlight in a muted tray (changelog months).</summary>
    Raised
}

/// <summary>
/// A row of toggle buttons with one highlight gliding between them on Arc's morph spring: the filters, pickers, page numbers
/// and month nav of the Content and Pricing blocks.
/// </summary>
internal sealed class GlidingPillBar : Control
{
    private readonly GlidingIndicator _indicator = new() { IsHitTestVisible = false };
    private readonly List<GlidingPillButton> _buttons = new();
    private int _selectedIndex = -1;
    private bool _placed;

    public static readonly StyledProperty<GlidingPillBarLook> LookProperty =
        AvaloniaProperty.Register<GlidingPillBar, GlidingPillBarLook>(nameof(Look));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<GlidingPillBar, double>(nameof(Spacing), 2);

    public static readonly StyledProperty<IBrush?> IndicatorFillProperty =
        AvaloniaProperty.Register<GlidingPillBar, IBrush?>(nameof(IndicatorFill));

    public static readonly StyledProperty<IBrush?> IndicatorStrokeProperty =
        AvaloniaProperty.Register<GlidingPillBar, IBrush?>(nameof(IndicatorStroke));

    public static readonly StyledProperty<BoxShadows> IndicatorShadowProperty =
        AvaloniaProperty.Register<GlidingPillBar, BoxShadows>(nameof(IndicatorShadow));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<GlidingPillBar, CornerRadius>(nameof(CornerRadius));

    static GlidingPillBar()
    {
        AffectsMeasure<GlidingPillBar>(SpacingProperty);
    }

    public GlidingPillBar()
    {
        _indicator.Spring = ArcSpring.Morph;
        PseudoClasses.Set(":solid-bar", true);
        VisualChildren.Add(_indicator);
    }

    public GlidingPillBarLook Look { get => GetValue(LookProperty); set => SetValue(LookProperty, value); }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public IBrush? IndicatorFill { get => GetValue(IndicatorFillProperty); set => SetValue(IndicatorFillProperty, value); }

    public IBrush? IndicatorStroke { get => GetValue(IndicatorStrokeProperty); set => SetValue(IndicatorStrokeProperty, value); }

    public BoxShadows IndicatorShadow { get => GetValue(IndicatorShadowProperty); set => SetValue(IndicatorShadowProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The buttons, in order.</summary>
    public IReadOnlyList<GlidingPillButton> Buttons => _buttons;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value)
            {
                return;
            }
            _selectedIndex = value;
            ApplySelection();
            InvalidateArrange();
        }
    }

    /// <summary>The highlight, for tests.</summary>
    public GlidingIndicator Indicator => _indicator;

    /// <summary>A button was pressed; the owner decides what is selected.</summary>
    public event Action<int>? ItemPressed;

    /// <summary>Replaces the buttons: <paramref name="labels"/> with screen reader names from <paramref name="name"/>.</summary>
    public void SetItems(IReadOnlyList<string> labels, Func<int, string>? name = null)
    {
        foreach (var button in _buttons)
        {
            button.Pressed -= OnButtonPressed;
            VisualChildren.Remove(button);
            LogicalChildren.Remove(button);
        }
        _buttons.Clear();
        for (var i = 0; i < labels.Count; i++)
        {
            var button = new GlidingPillButton { Content = labels[i], Index = i };
            button.SetLook(Look);
            AutomationProperties.SetName(button, name?.Invoke(i) ?? labels[i]);
            button.Pressed += OnButtonPressed;
            _buttons.Add(button);
            VisualChildren.Add(button);
            LogicalChildren.Add(button);
        }
        ApplySelection();
        InvalidateMeasure();
    }

    private void OnButtonPressed(int index) => ItemPressed?.Invoke(index);

    private void ApplySelection()
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsChecked = i == _selectedIndex;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LookProperty)
        {
            PseudoClasses.Set(":solid-bar", Look == GlidingPillBarLook.Solid);
            foreach (var button in _buttons)
            {
                button.SetLook(Look);
            }
        }
        else if (change.Property == IndicatorFillProperty)
        {
            _indicator.Fill = IndicatorFill;
        }
        else if (change.Property == IndicatorStrokeProperty)
        {
            _indicator.Stroke = IndicatorStroke;
            _indicator.StrokeThickness = IndicatorStroke is null ? 0 : 1;
        }
        else if (change.Property == IndicatorShadowProperty)
        {
            _indicator.BoxShadow = IndicatorShadow;
        }
        else if (change.Property == CornerRadiusProperty)
        {
            _indicator.CornerRadius = CornerRadius;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        var width = 0.0;
        var height = 0.0;
        foreach (var button in _buttons)
        {
            button.Measure(infinite);
            width += button.DesiredSize.Width;
            height = Math.Max(height, button.DesiredSize.Height);
        }
        if (_buttons.Count > 1)
        {
            width += Spacing * (_buttons.Count - 1);
        }
        _indicator.Measure(infinite);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var selected = default(Rect);
        for (var i = 0; i < _buttons.Count; i++)
        {
            var rect = new Rect(x, 0, _buttons[i].DesiredSize.Width, finalSize.Height);
            _buttons[i].Arrange(rect);
            if (i == _selectedIndex)
            {
                selected = rect;
            }
            x += rect.Width + Spacing;
        }
        _indicator.Arrange(new Rect(finalSize));
        if (_selectedIndex >= 0 && _selectedIndex < _buttons.Count)
        {
            _indicator.MoveTo(selected, animate: _placed);
            _placed = true;
        }
        else
        {
            _indicator.Hide(animate: false);
            _placed = false;
        }
        return finalSize;
    }
}

/// <summary>One button of a <see cref="GlidingPillBar"/>. Its state is its owner's: pressing it only reports the press.</summary>
[PseudoClasses(":solid", ":quiet", ":raised")]
internal sealed class GlidingPillButton : ToggleButton
{
    private LazyFocusRing? _focusRing;

    public int Index { get; init; }

    public event Action<int>? Pressed;

    protected override Type StyleKeyOverride => typeof(GlidingPillButton);

    public void SetLook(GlidingPillBarLook look)
    {
        PseudoClasses.Set(":solid", look == GlidingPillBarLook.Solid);
        PseudoClasses.Set(":quiet", look == GlidingPillBarLook.Quiet);
        PseudoClasses.Set(":raised", look == GlidingPillBarLook.Raised);
    }

    protected override void Toggle() => Pressed?.Invoke(Index);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing = BlockFocus.AttachRing(this, e.NameScope.Find<Panel>(BlockFocus.RootPart), ArcDesignTokenKind.RadiusPill);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>The keyboard focus ring of the Content and Pricing blocks' small buttons: 2 outside, made on first use.</summary>
internal static class BlockFocus
{
    public const string RootPart = "PART_Root";

    public static LazyFocusRing? AttachRing(Control owner, Panel? host, ArcDesignTokenKind radius)
    {
        if (host is null)
        {
            return null;
        }
        var ring = new LazyFocusRing(owner, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness), ArcDesignTokens.Key(radius));
        ring.Attach(part =>
        {
            part.Margin = new Thickness(-2);
            host.Children.Add(part);
        }, part => host.Children.Remove(part));
        return ring;
    }
}

/// <summary>A plain button with the lazy focus ring; hover reports mouse and pen only through <c>:fine-hover</c>.</summary>
[PseudoClasses(FineHoverPseudoClass)]
internal class BlockButton : Avalonia.Controls.Button
{
    public const string FineHoverPseudoClass = ":fine-hover";

    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => GetType();

    /// <summary>The ring's corner radius.</summary>
    protected virtual ArcDesignTokenKind RingRadius => ArcDesignTokenKind.RadiusPill;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing = BlockFocus.AttachRing(this, e.NameScope.Find<Panel>(BlockFocus.RootPart), RingRadius);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            PseudoClasses.Set(FineHoverPseudoClass, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(FineHoverPseudoClass, false);
    }
}
