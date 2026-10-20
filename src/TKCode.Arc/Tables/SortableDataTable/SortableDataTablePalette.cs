using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Resolved colors, pens and fonts shared by the self-drawn parts of one <see cref="SortableDataTable"/>.</summary>
internal sealed class SortableDataTablePalette
{
    public int Version { get; private set; }

    public Color Surface { get; private set; }
    public Color ColumnTint { get; private set; }
    public Color Foreground { get; private set; }
    public Color TextSecondary { get; private set; }
    public Color TextMuted { get; private set; }
    public Color BorderStrong { get; private set; }
    public Color ControlOn { get; private set; }

    /// <summary>Row fills for base Surface (index 0) and the sorted column tint (index 1).</summary>
    public Color[] Hover { get; } = new Color[2];
    public Color[] Selected { get; } = new Color[2];
    public Color[] SelectedHover { get; } = new Color[2];

    public IBrush SurfaceBrush { get; private set; } = Brushes.Transparent;
    public IBrush ColumnTintBrush { get; private set; } = Brushes.Transparent;
    public IBrush ForegroundBrush { get; private set; } = Brushes.Black;
    public IBrush TextSecondaryBrush { get; private set; } = Brushes.Gray;
    public IBrush TextMutedBrush { get; private set; } = Brushes.Gray;
    public IBrush ControlOnBrush { get; private set; } = Brushes.Black;
    public IPen DividerPen { get; private set; } = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Transparent));
    public IPen RulePen { get; private set; } = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Transparent));
    public IPen GlyphPen { get; private set; } = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White));
    public IPen? FocusPen { get; private set; }

    public FontFamily FontFamily { get; private set; } = FontFamily.Default;
    public double FontSize { get; private set; } = 14;
    public double SmallFontSize { get; private set; } = 12;
    public Typeface Regular { get; private set; }
    public Typeface Medium { get; private set; }

    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    public void Update(Color surface, Color columnTint, Color foreground, Color textSecondary, Color textMuted,
                       Color border, Color borderSubtle, Color borderStrong, Color accent, Color controlOn,
                       Color controlGlyph, Color? focusRing, FontFamily fontFamily, double fontSize, double smallFontSize,
                       FontWeight medium)
    {
        Surface = surface;
        ColumnTint = columnTint;
        Foreground = foreground;
        TextSecondary = textSecondary;
        TextMuted = textMuted;
        BorderStrong = borderStrong;
        ControlOn = controlOn;
        var bases = new[] { surface, columnTint };
        for (var i = 0; i < 2; i++)
        {
            Hover[i] = Oklch.MixOklab(bases[i], foreground, 0.03);
            Selected[i] = Oklch.MixOklab(bases[i], accent, 0.08);
            SelectedHover[i] = Oklch.MixOklab(bases[i], accent, 0.11);
        }
        SurfaceBrush = new ImmutableSolidColorBrush(surface);
        ColumnTintBrush = new ImmutableSolidColorBrush(columnTint);
        ForegroundBrush = new ImmutableSolidColorBrush(foreground);
        TextSecondaryBrush = new ImmutableSolidColorBrush(textSecondary);
        TextMutedBrush = new ImmutableSolidColorBrush(textMuted);
        ControlOnBrush = new ImmutableSolidColorBrush(controlOn);
        DividerPen = new ImmutablePen(new ImmutableSolidColorBrush(borderSubtle), 1);
        RulePen = new ImmutablePen(new ImmutableSolidColorBrush(border), 1);
        GlyphPen = new ImmutablePen(new ImmutableSolidColorBrush(controlGlyph), 1.75, lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round);
        FocusPen = focusRing is { } ring ? new ImmutablePen(new ImmutableSolidColorBrush(ring), 2) : null;
        FontFamily = fontFamily;
        FontSize = fontSize;
        SmallFontSize = smallFontSize;
        Regular = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal);
        Medium = new Typeface(fontFamily, FontStyle.Normal, medium);
        Version++;
    }

    /// <summary>Row fill for a base (0 surface, 1 column tint) at the given hover and selection progress.</summary>
    public Color RowFill(int baseIndex, double hover, double selected)
    {
        var bases = baseIndex == 0 ? Surface : ColumnTint;
        var idle = TablesBDrawing.Lerp(bases, Hover[baseIndex], hover);
        if (selected <= 0.001)
        {
            return idle;
        }
        var chosen = TablesBDrawing.Lerp(Selected[baseIndex], SelectedHover[baseIndex], hover);
        return TablesBDrawing.Lerp(idle, chosen, selected);
    }
}
