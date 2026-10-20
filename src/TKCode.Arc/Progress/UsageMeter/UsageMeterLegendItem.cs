using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaToggleButton = Avalonia.Controls.Primitives.ToggleButton;

namespace TKCode.Arc;

/// <summary>
/// One legend entry of a <see cref="UsageMeter"/>: a toggle button (pressed = pinned) with a swatch, the category
/// label and its rolling value. Hover (fine pointers) and focus preview the category; a press pins it.
/// </summary>
[TemplatePart(KeyPart, typeof(ArcContentSwap))]
[TemplatePart(LabelPart, typeof(ProgressSwapText))]
[TemplatePart(ValuePart, typeof(RollingDigits))]
[PseudoClasses(DimmedPseudoClass)]
internal sealed class UsageMeterLegendItem : AvaloniaToggleButton
{
    public const string KeyPart = "PART_Key";
    public const string LabelPart = "PART_Label";
    public const string ValuePart = "PART_Value";
    public const string DimmedPseudoClass = ":dimmed";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<UsageMeterLegendItem, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<UsageMeterLegendItem, double>(nameof(Value));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<UsageMeterLegendItem, int>(nameof(Decimals), 1);

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<UsageMeterLegendItem, string?>(nameof(Unit));

    public static readonly StyledProperty<object?> KeyContentProperty =
        AvaloniaProperty.Register<UsageMeterLegendItem, object?>(nameof(KeyContent));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public object? KeyContent { get => GetValue(KeyContentProperty); set => SetValue(KeyContentProperty, value); }

    private readonly UsageMeter _owner;

    public UsageMeterLegendItem(UsageMeter owner, string key)
    {
        _owner = owner;
        Key = key;
    }

    public string Key { get; set; }

    public bool IsDimmed
    {
        get => PseudoClasses.Contains(DimmedPseudoClass);
        set => PseudoClasses.Set(DimmedPseudoClass, value);
    }

    protected override Type StyleKeyOverride => typeof(UsageMeterLegendItem);

    protected override void OnClick()
    {
        // The meter owns the pinned state (one category at a time); a press toggles it there.
        _owner.TogglePin(Key);
        var args = new RoutedEventArgs(ClickEvent);
        RaiseEvent(args);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner.SetFocused(this);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _owner.SetFocused(null);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _owner.SetHovered(Key);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _owner.SetHovered(null);
        }
    }
}

/// <summary>The legend grid: as many 108px-minimum columns as fit, sharing the width (CSS auto-fit), 2px apart.</summary>
internal sealed class UsageMeterLegendPanel : Panel
{
    internal const double MinColumn = 108;
    internal const double Gap = 2;

    internal static int ColumnsFor(double width, int count) =>
        Math.Max(1, Math.Min(Math.Max(count, 1), (int)Math.Floor((width + Gap) / (MinColumn + Gap))));

    private int _columns = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return default;
        }
        var width = double.IsInfinity(availableSize.Width) ? count * (MinColumn + Gap) - Gap : availableSize.Width;
        _columns = ColumnsFor(width, count);
        var column = (width - (_columns - 1) * Gap) / _columns;
        var height = 0.0;
        for (var row = 0; row * _columns < count; row++)
        {
            var tallest = 0.0;
            for (var c = 0; c < _columns && row * _columns + c < count; c++)
            {
                var child = Children[row * _columns + c];
                child.Measure(new Size(column, double.PositiveInfinity));
                tallest = Math.Max(tallest, child.DesiredSize.Height);
            }
            height += tallest + (row > 0 ? Gap : 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        var column = (finalSize.Width - (_columns - 1) * Gap) / _columns;
        var y = 0.0;
        for (var row = 0; row * _columns < count; row++)
        {
            var tallest = 0.0;
            for (var c = 0; c < _columns && row * _columns + c < count; c++)
            {
                tallest = Math.Max(tallest, Children[row * _columns + c].DesiredSize.Height);
            }
            for (var c = 0; c < _columns && row * _columns + c < count; c++)
            {
                Children[row * _columns + c].Arrange(new Rect(c * (column + Gap), y, column, tallest));
            }
            y += tallest + Gap;
        }
        return finalSize;
    }
}
