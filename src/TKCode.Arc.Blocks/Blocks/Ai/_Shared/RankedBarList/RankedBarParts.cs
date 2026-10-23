using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The win-rate bar of a <see cref="RankedBarRow"/>, drawn directly: a pill-shaped track in <see cref="TrackBrush"/> at
/// 9% and, clipped to it, a fill from the left edge across <see cref="Ratio"/> of the width (Arc scales a full-width fill
/// from its left edge inside the rounded track). A new ratio only repaints.
/// </summary>
internal sealed class RankedBarBar : Control
{
    /// <summary>How much of <see cref="TrackBrush"/> the track shows (Arc: 9% of the foreground).</summary>
    public const double TrackOpacity = 0.09;

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<RankedBarBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<RankedBarBar, IBrush?>(nameof(FillBrush));

    private IBrush? _track;
    private double _ratio;

    static RankedBarBar()
    {
        AffectsRender<RankedBarBar>(FillBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<RankedBarBar>(false);
        FocusableProperty.OverrideDefaultValue<RankedBarBar>(false);
    }

    /// <summary>The color the track is a 9% tint of (Foreground).</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <summary>The fill (the row's series color).</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The share of the track the fill covers (0 to 1).</summary>
    public double Ratio
    {
        get => _ratio;
        set
        {
            var clamped = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
            if (clamped.Equals(_ratio))
            {
                return;
            }
            _ratio = clamped;
            InvalidateVisual();
        }
    }

    /// <summary>The track's own brush (the 9% tint), made once per <see cref="TrackBrush"/>.</summary>
    internal IBrush? Track => _track;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TrackBrushProperty)
        {
            _track = TrackBrush is ISolidColorBrush solid
                ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity * TrackOpacity)
                : null;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var track = new RoundedRect(bounds, bounds.Height / 2);
        if (_track is not null)
        {
            context.DrawRectangle(_track, null, track);
        }
        if (_ratio <= 0 || FillBrush is not { } fill)
        {
            return;
        }
        using (context.PushClip(track))
        {
            context.FillRectangle(fill, new Rect(0, 0, bounds.Width * _ratio, bounds.Height));
        }
    }
}

/// <summary>
/// The rank column of a <see cref="RankedBarList"/>: "1" to <see cref="Count"/>, one per row height, drawn directly. The
/// numbers never move with the rows (Arc's <c>aria-hidden</c> list beside the rows); screen readers get the rank from each
/// row's name instead, so the column is left out of the automation tree.
/// </summary>
internal sealed class RankedBarRanks : Control
{
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<RankedBarRanks, int>(nameof(Count));

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<RankedBarRanks, double>(nameof(RowHeight), 36);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<RankedBarRanks>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<RankedBarRanks>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<RankedBarRanks>();

    private readonly List<TextLayout> _layouts = new();

    static RankedBarRanks()
    {
        AffectsMeasure<RankedBarRanks>(CountProperty, RowHeightProperty);
        AffectsRender<RankedBarRanks>(CountProperty, RowHeightProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<RankedBarRanks>(false);
        FocusableProperty.OverrideDefaultValue<RankedBarRanks>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<RankedBarRanks>(AccessibilityView.Raw);
    }

    /// <summary>How many ranks show.</summary>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>The height of one rank (the rows' height).</summary>
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            ClearLayouts();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Math.Max(0, Count) * RowHeight);

    public override void Render(DrawingContext context)
    {
        var count = Count;
        if (count <= 0 || Foreground is not { } brush)
        {
            return;
        }
        var typeface = new Typeface(FontFamily);
        for (var i = 0; i < count; i++)
        {
            if (i >= _layouts.Count)
            {
                // Made once per rank and kept: ranks only change when the row count does.
                _layouts.Add(new TextLayout((i + 1).ToString(CultureInfo.InvariantCulture), typeface, FontSize, brush));
            }
            var layout = _layouts[i];
            layout.Draw(context, new Point(0, i * RowHeight + (RowHeight - layout.Height) / 2));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearLayouts();
    }

    private void ClearLayouts()
    {
        foreach (var layout in _layouts)
        {
            layout.Dispose();
        }
        _layouts.Clear();
    }

    /// <summary>The text of the rank drawn at <paramref name="index"/> (tests).</summary>
    internal static string RankText(int index) => (index + 1).ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Lays out the rows of a <see cref="RankedBarList"/>: every <see cref="RankedBarRow"/> at <see cref="RankedBarRow.Slot"/>
/// × <see cref="RowHeight"/>, full width. Only shown rows take room; rows that are leaving or waiting to enter keep their
/// place but add nothing to the height. A reorder changes slots and arranges once; the rows' render transforms carry the
/// motion (FLIP), so a replay never lays out per frame. Nothing is clipped here: the owner's height spring clips.
/// </summary>
internal sealed class RankedBarPanel : Panel
{
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<RankedBarPanel, double>(nameof(RowHeight), 36);

    static RankedBarPanel()
    {
        AffectsMeasure<RankedBarPanel>(RowHeightProperty);
    }

    /// <summary>The height of one row.</summary>
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rowSize = new Size(availableSize.Width, RowHeight);
        var shown = 0;
        var width = 0.0;
        foreach (var child in Children)
        {
            child.Measure(rowSize);
            width = Math.Max(width, child.DesiredSize.Width);
            if (child is RankedBarRow { IsShown: true })
            {
                shown++;
            }
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, shown * RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var height = RowHeight;
        foreach (var child in Children)
        {
            var slot = child is RankedBarRow row ? row.Slot : 0;
            child.Arrange(new Rect(0, slot * height, finalSize.Width, height));
        }
        return finalSize;
    }
}

/// <summary>Where a row of a <see cref="RankedBarList"/> is in its life.</summary>
internal enum RankedBarRowState
{
    /// <summary>Shown and counted (laid out, read by screen readers).</summary>
    Shown,

    /// <summary>Created during a replay's hold, invisible, waiting to fade in at its new rank.</summary>
    Entering,

    /// <summary>Dropped out of the list; fading out before it is removed.</summary>
    Leaving
}
