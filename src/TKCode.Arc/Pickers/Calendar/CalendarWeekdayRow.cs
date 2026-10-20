using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Seven weekday initials aligned with the day columns (rendered, no child controls).</summary>
internal sealed class CalendarWeekdayRow : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<CalendarWeekdayRow>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<CalendarWeekdayRow>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<CalendarWeekdayRow>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<CalendarWeekdayRow>();

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    private readonly string[] _names = new string[7];
    private FormattedText?[] _texts = new FormattedText?[7];

    static CalendarWeekdayRow()
    {
        AffectsRender<CalendarWeekdayRow>(ForegroundProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty);
    }

    public void Update(CultureInfo culture, DayOfWeek firstDay)
    {
        var names = culture.DateTimeFormat.ShortestDayNames;
        var changed = false;
        for (var i = 0; i < 7; i++)
        {
            var name = names[((int)firstDay + i) % 7];
            if (name.Length > 2)
            {
                name = name[..2];
            }
            if (_names[i] != name)
            {
                _names[i] = name;
                changed = true;
            }
        }
        if (changed)
        {
            _texts = new FormattedText?[7];
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == FontWeightProperty ||
            change.Property == ForegroundProperty)
        {
            _texts = new FormattedText?[7];
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Calendar.DefaultWidth : availableSize.Width;
        return new Size(width, 24);
    }

    public override void Render(DrawingContext context)
    {
        var cell = CalendarMonthPane.CellSize(Bounds.Width);
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        for (var i = 0; i < 7; i++)
        {
            if (_names[i] is null)
            {
                continue;
            }
            var text = _texts[i] ??= new FormattedText(_names[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
            var x = i * (cell + CalendarMonthPane.Gap) + (cell - text.Width) / 2;
            context.DrawText(text, new Point(x, (Bounds.Height - text.Height) / 2));
        }
    }
}
