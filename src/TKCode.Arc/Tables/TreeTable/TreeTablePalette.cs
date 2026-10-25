using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Resolved brushes, pens and fonts shared by the self-drawn parts of one <see cref="TreeTable"/>.</summary>
internal sealed class TreeTablePalette
{
    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    public int Version { get; private set; }

    public IBrush? Foreground { get; private set; }
    public IBrush? TextSecondary { get; private set; }
    public IBrush? TextMuted { get; private set; }
    public IBrush? Divider { get; private set; }
    public IBrush? Skeleton { get; private set; }
    public IBrush? Danger { get; private set; }
    public IPen GuidePen { get; private set; } = new Pen(new ImmutableSolidColorBrush(Colors.Transparent), 1);
    public IPen GuideActivePen { get; private set; } = new Pen(new ImmutableSolidColorBrush(Colors.Transparent), 1);
    public IPen? FocusRing { get; private set; }
    public Typeface Regular { get; private set; }
    public double SmallFontSize { get; private set; } = 12;

    public void Update(IBrush? foreground, IBrush? textSecondary, IBrush? textMuted, IBrush? divider, IBrush? skeleton,
                       IBrush? danger, IBrush? guide, IBrush? guideActive, IBrush? focusRing, FontFamily fontFamily,
                       double smallFontSize)
    {
        Foreground = foreground;
        TextSecondary = textSecondary;
        TextMuted = textMuted;
        Divider = divider;
        Skeleton = skeleton;
        Danger = danger;
        GuidePen = new Pen(guide ?? Brushes.Transparent, 1);
        GuideActivePen = new Pen(guideActive ?? Brushes.Transparent, 1);
        FocusRing = focusRing is null ? null : new Pen(focusRing, 2);
        Regular = new Typeface(fontFamily);
        SmallFontSize = smallFontSize;
        Version++;
    }
}
