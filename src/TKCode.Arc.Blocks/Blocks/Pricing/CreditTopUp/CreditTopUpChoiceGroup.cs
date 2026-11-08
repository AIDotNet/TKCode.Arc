using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A choice of <see cref="CreditTopUp"/> (the packs, the auto top-up thresholds): a radio group whose selection is a
/// single shape gliding between the options on the Quick spring (Arc Credit top-up, design reference: ω ≈ 20.5–22,
/// ζ ≈ 1). When the selection appears from none or goes away (the amount moved off every pack) the shape fades in place
/// instead of travelling. Arrows (wrapping), Home and End move and select; only the selected option (or the first when
/// none is) is a tab stop. Code-driven because the shape's target is the measured option box.
/// </summary>
internal sealed class CreditTopUpChoiceGroup : Panel
{
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, int>(nameof(SelectedIndex), -1);

    /// <summary>When true the options share the width equally (the packs); otherwise they keep their own width.</summary>
    public static readonly StyledProperty<bool> IsUniformProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, bool>(nameof(IsUniform));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, double>(nameof(Gap));

    public static readonly StyledProperty<IBrush?> IndicatorFillProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, IBrush?>(nameof(IndicatorFill));

    /// <summary>Alpha applied to <see cref="IndicatorFill"/>'s color (the pack ring is the accent at 6%).</summary>
    public static readonly StyledProperty<double> IndicatorFillAlphaProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, double>(nameof(IndicatorFillAlpha), 1);

    public static readonly StyledProperty<IBrush?> IndicatorStrokeProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, IBrush?>(nameof(IndicatorStroke));

    public static readonly StyledProperty<double> IndicatorThicknessProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, double>(nameof(IndicatorThickness));

    public static readonly StyledProperty<CornerRadius> IndicatorRadiusProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, CornerRadius>(nameof(IndicatorRadius));

    /// <summary>How far the indicator box reaches past the option box (negative pulls it in).</summary>
    public static readonly StyledProperty<double> IndicatorInflateProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, double>(nameof(IndicatorInflate));

    /// <summary>Whether the indicator draws above the options (the pack ring) or below them (the threshold pill).</summary>
    public static readonly StyledProperty<bool> IsIndicatorOnTopProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceGroup, bool>(nameof(IsIndicatorOnTop));

    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public bool IsUniform { get => GetValue(IsUniformProperty); set => SetValue(IsUniformProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public IBrush? IndicatorFill { get => GetValue(IndicatorFillProperty); set => SetValue(IndicatorFillProperty, value); }
    public double IndicatorFillAlpha { get => GetValue(IndicatorFillAlphaProperty); set => SetValue(IndicatorFillAlphaProperty, value); }
    public IBrush? IndicatorStroke { get => GetValue(IndicatorStrokeProperty); set => SetValue(IndicatorStrokeProperty, value); }
    public double IndicatorThickness { get => GetValue(IndicatorThicknessProperty); set => SetValue(IndicatorThicknessProperty, value); }
    public CornerRadius IndicatorRadius { get => GetValue(IndicatorRadiusProperty); set => SetValue(IndicatorRadiusProperty, value); }
    public double IndicatorInflate { get => GetValue(IndicatorInflateProperty); set => SetValue(IndicatorInflateProperty, value); }
    public bool IsIndicatorOnTop { get => GetValue(IsIndicatorOnTopProperty); set => SetValue(IsIndicatorOnTopProperty, value); }

    /// <summary>The spring of a glide between two options.</summary>
    internal static readonly SpringSpec GlideSpring = ArcMotionPresets.Quick;

    /// <summary>An option was picked by click or key.</summary>
    public event Action<int>? Picked;

    private readonly GlidingIndicator _indicator = new() { FadeDuration = TimeSpan.FromMilliseconds(170) };
    private readonly List<CreditTopUpChoiceButton> _options = new();
    private int _shown = -1;
    private Rect _target;

    public CreditTopUpChoiceGroup()
    {
        Children.Add(_indicator);
    }

    static CreditTopUpChoiceGroup()
    {
        AffectsMeasure<CreditTopUpChoiceGroup>(IsUniformProperty, GapProperty);
    }

    /// <summary>The options, in order.</summary>
    internal IReadOnlyList<CreditTopUpChoiceButton> Options => _options;

    /// <summary>The selection shape (tests: where it is and how visible).</summary>
    internal GlidingIndicator Indicator => _indicator;

    /// <summary>Replaces the options: one button per content, with its accessible name and style class.</summary>
    public void SetOptions(IReadOnlyList<object> contents, IReadOnlyList<string> names, string styleClass)
    {
        var same = contents.Count == _options.Count;
        if (!same)
        {
            foreach (var option in _options)
            {
                option.Click -= OnOptionClick;
                Children.Remove(option);
            }
            _options.Clear();
            for (var i = 0; i < contents.Count; i++)
            {
                var option = new CreditTopUpChoiceButton { Index = i };
                option.Classes.Add(styleClass);
                option.Click += OnOptionClick;
                _options.Add(option);
                Children.Insert(IsIndicatorOnTop ? Children.Count - 1 : Children.Count, option);
            }
            _shown = -1;
        }
        for (var i = 0; i < contents.Count; i++)
        {
            _options[i].Content = contents[i];
            AutomationProperties.SetName(_options[i], names[i]);
        }
        UpdateSelection(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            UpdateSelection(true);
        }
        else if (change.Property == IsIndicatorOnTopProperty)
        {
            Children.Remove(_indicator);
            Children.Insert(IsIndicatorOnTop ? Children.Count : 0, _indicator);
        }
        else if (change.Property == IndicatorFillProperty || change.Property == IndicatorFillAlphaProperty)
        {
            _indicator.Fill = IndicatorFill is ISolidColorBrush solid && IndicatorFillAlpha < 1
                ? new SolidColorBrush(solid.Color, solid.Opacity * IndicatorFillAlpha)
                : IndicatorFill;
        }
        else if (change.Property == IndicatorStrokeProperty)
        {
            _indicator.Stroke = IndicatorStroke;
        }
        else if (change.Property == IndicatorThicknessProperty)
        {
            _indicator.StrokeThickness = IndicatorThickness;
        }
        else if (change.Property == IndicatorRadiusProperty)
        {
            _indicator.CornerRadius = IndicatorRadius;
        }
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CreditTopUpChoiceButton option)
        {
            Picked?.Invoke(option.Index);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _options.Count == 0)
        {
            return;
        }
        var current = SelectedIndex >= 0 ? SelectedIndex : FocusedIndex();
        int next;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                next = current < 0 ? 0 : CreditTopUpMath.Wrap(current, 1, _options.Count);
                break;
            case Key.Left:
            case Key.Up:
                next = current < 0 ? _options.Count - 1 : CreditTopUpMath.Wrap(current, -1, _options.Count);
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = _options.Count - 1;
                break;
            default:
                return;
        }
        e.Handled = true;
        Picked?.Invoke(next);
        _options[next].Focus(NavigationMethod.Directional);
    }

    private int FocusedIndex()
    {
        for (var i = 0; i < _options.Count; i++)
        {
            if (_options[i].IsFocused)
            {
                return i;
            }
        }
        return -1;
    }

    private void UpdateSelection(bool animate)
    {
        var index = SelectedIndex;
        for (var i = 0; i < _options.Count; i++)
        {
            _options[i].IsSelected = i == index;
            // Roving tab stop: the selected option, or the first when none is selected.
            KeyboardNavigation.SetIsTabStop(_options[i], index >= 0 && index < _options.Count ? i == index : i == 0);
        }
        PlaceIndicator(animate);
    }

    private void PlaceIndicator(bool animate)
    {
        var index = SelectedIndex;
        if (index < 0 || index >= _options.Count)
        {
            _shown = -1;
            _target = default;
            _indicator.Hide(animate);
            return;
        }
        var box = _options[index].Bounds;
        if (box.Width <= 0)
        {
            return;
        }
        var target = box.Inflate(IndicatorInflate);
        if (!animate && _shown == index && target == _target)
        {
            return;
        }
        // Between two options the shape glides; from none it lands in place and fades in (GlidingIndicator does both).
        _indicator.MoveTo(target, GlideSpring, animate);
        _shown = index;
        _target = target;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = _options.Count;
        if (count == 0)
        {
            return default;
        }
        var gaps = Gap * (count - 1);
        var height = 0.0;
        var width = 0.0;
        if (IsUniform)
        {
            var each = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, (availableSize.Width - gaps) / count);
            foreach (var option in _options)
            {
                option.Measure(new Size(each, availableSize.Height));
                height = Math.Max(height, option.DesiredSize.Height);
                width = Math.Max(width, option.DesiredSize.Width);
            }
            width = double.IsInfinity(availableSize.Width) ? width * count + gaps : availableSize.Width;
        }
        else
        {
            foreach (var option in _options)
            {
                option.Measure(Size.Infinity);
                width += option.DesiredSize.Width;
                height = Math.Max(height, option.DesiredSize.Height);
            }
            width += gaps;
        }
        _indicator.Measure(availableSize);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = _options.Count;
        var x = 0.0;
        var each = count > 0 ? Math.Max(0, (finalSize.Width - Gap * (count - 1)) / count) : 0;
        foreach (var option in _options)
        {
            var w = IsUniform ? each : option.DesiredSize.Width;
            option.Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w + Gap;
        }
        _indicator.Arrange(new Rect(finalSize));
        // A resize moves the shape without animation.
        PlaceIndicator(false);
        return finalSize;
    }
}

/// <summary>An option of <see cref="CreditTopUpChoiceGroup"/>: a button that reads as a radio button.</summary>
internal sealed class CreditTopUpChoiceButton : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<CreditTopUpChoiceButton, bool>(nameof(IsSelected));

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public int Index { get; init; }

    protected override Type StyleKeyOverride => typeof(CreditTopUpChoiceButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Hover styles are for a precise pointer only.
        PseudoClasses.Set(":hover", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":hover", false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RadioPeer(this);

    private sealed class RadioPeer(CreditTopUpChoiceButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override string? GetHelpTextCore() => ((CreditTopUpChoiceButton)Owner).IsSelected ? "selected" : null;
    }
}
