using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A number that counts to its new value: <see cref="Value"/> springs on <see cref="Spring"/> (smooth, no overshoot) and
/// every frame draws <see cref="Formatter"/> of the current value in tabular figures. The size is measured from the target's
/// text only, so a count never relayouts the row it sits in. A change while counting continues from the shown value with its
/// velocity. Off and Reduced motion show the new value at once.
/// Code-driven because the text is a function of an animated number, which a ControlTheme transition cannot express.
/// </summary>
internal sealed class FinanceCountingText : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<FinanceCountingText, double>(nameof(Value));

    public static readonly StyledProperty<Func<double, string>?> FormatterProperty =
        AvaloniaProperty.Register<FinanceCountingText, Func<double, string>?>(nameof(Formatter));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<FinanceCountingText>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<FinanceCountingText>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<FinanceCountingText>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<FinanceCountingText>();

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<FinanceCountingText, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<ArcSpring> SpringProperty =
        AvaloniaProperty.Register<FinanceCountingText, ArcSpring>(nameof(Spring), ArcSpring.Smooth);

    private readonly FinanceText _text = new();
    private MotionValue<double>? _shown;
    private bool _hasValue;

    static FinanceCountingText()
    {
        AffectsMeasure<FinanceCountingText>(FormatterProperty, FontSizeProperty, FontWeightProperty, FontFamilyProperty, LineHeightProperty);
        AffectsRender<FinanceCountingText>(ForegroundProperty);
        HorizontalAlignmentProperty.OverrideDefaultValue<FinanceCountingText>(HorizontalAlignment.Right);
        VerticalAlignmentProperty.OverrideDefaultValue<FinanceCountingText>(VerticalAlignment.Center);
        IsHitTestVisibleProperty.OverrideDefaultValue<FinanceCountingText>(false);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Func<double, string>? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public ArcSpring Spring { get => GetValue(SpringProperty); set => SetValue(SpringProperty, value); }

    /// <summary>The value drawn right now (the target once the count settles).</summary>
    public double ShownValue => _shown?.Value ?? Value;

    /// <summary>The text drawn right now.</summary>
    public string ShownText => Format(ShownValue);

    /// <summary>Shows <see cref="Value"/> at once (the first value, or a data change that should not count).</summary>
    public void Settle()
    {
        _shown?.Jump(Value);
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            _shown ??= CreateShown();
            if (!_hasValue || !this.IsAttachedToVisualTree())
            {
                _hasValue = true;
                _shown.Jump(Value);
            }
            else
            {
                _ = _shown.AnimateTo(Value, MotionSpec.Spring(Spring));
            }
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == FontFamilyProperty)
        {
            _text.Clear();
            _text.FontFamily = FontFamily;
        }
    }

    private MotionValue<double> CreateShown()
    {
        var shown = new MotionValue<double>(this, Value, MotionVectors.Double);
        shown.Changed += _ => InvalidateVisual();
        return shown;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Settle();
        _text.Clear();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _text.FontFamily = FontFamily;
        var layout = _text.Get(Format(Value), FontSize, FontWeight, Brushes.Black);
        var height = double.IsNaN(LineHeight) ? layout.Height : LineHeight;
        return new Size(Math.Ceiling(layout.WidthIncludingTrailingWhitespace), height);
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground ?? Brushes.Black;
        var layout = _text.Get(ShownText, FontSize, FontWeight, brush);
        var width = layout.WidthIncludingTrailingWhitespace;
        var x = HorizontalAlignment == HorizontalAlignment.Left ? 0 : Bounds.Width - width;
        var y = (Bounds.Height - layout.Height) / 2;
        layout.Draw(context, new Point(x, y));
        _text.Sweep();
    }

    private string Format(double value) => Formatter?.Invoke(value) ?? FinanceFormat.Count(value);
}
