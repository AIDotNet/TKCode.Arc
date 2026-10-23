// Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli): the flex and grid layouts of
// cta-section.module.css as small panels (Avalonia has no flex-wrap, container queries or text-wrap: balance).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// A text block with Arc's <c>text-wrap: balance</c> (<see cref="IsBalanced"/>: the narrowest width that keeps the same
/// number of lines), a width limit in characters (<see cref="MaxChars"/>, CSS <c>ch</c>: the advance of "0"), a line height
/// as a multiple of the font size (<see cref="Leading"/>) and a letter spacing in em (<see cref="Tracking"/>).
/// </summary>
internal sealed class CtaBalancedText : TextBlock
{
    public static readonly StyledProperty<bool> IsBalancedProperty =
        AvaloniaProperty.Register<CtaBalancedText, bool>(nameof(IsBalanced));

    public static readonly StyledProperty<double> MaxCharsProperty =
        AvaloniaProperty.Register<CtaBalancedText, double>(nameof(MaxChars));

    public static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<CtaBalancedText, double>(nameof(Leading));

    public static readonly StyledProperty<double> TrackingProperty =
        AvaloniaProperty.Register<CtaBalancedText, double>(nameof(Tracking));

    private double _chWidth;
    private double _chFontSize;
    private string? _chFamily;

    static CtaBalancedText()
    {
        AffectsMeasure<CtaBalancedText>(IsBalancedProperty, MaxCharsProperty, LeadingProperty, TrackingProperty);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>Wraps at the narrowest width that keeps the line count.</summary>
    public bool IsBalanced { get => GetValue(IsBalancedProperty); set => SetValue(IsBalancedProperty, value); }

    /// <summary>The widest the text gets, in characters of this font size (0 = no limit).</summary>
    public double MaxChars { get => GetValue(MaxCharsProperty); set => SetValue(MaxCharsProperty, value); }

    /// <summary>Line height as a multiple of the font size (0 = the font's own).</summary>
    public double Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    /// <summary>Letter spacing in em.</summary>
    public double Tracking { get => GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LeadingProperty || change.Property == FontSizeProperty)
        {
            LineHeight = Leading > 0 ? FontSize * Leading : double.NaN;
        }
        if (change.Property == TrackingProperty || change.Property == FontSizeProperty)
        {
            LetterSpacing = Tracking * FontSize;
        }
    }

    /// <summary>The advance of "0" (CSS <c>ch</c>), measured once per font and size.</summary>
    private double ChWidth()
    {
        var family = FontFamily?.Name;
        if (_chWidth <= 0 || _chFontSize != FontSize || _chFamily != family)
        {
            var layout = new TextLayout("0", new Typeface(FontFamily ?? FontFamily.Default, FontStyle, FontWeight), FontSize, null);
            _chWidth = layout.Width > 0 ? layout.Width : FontSize * 0.6;
            _chFontSize = FontSize;
            _chFamily = family;
        }
        return _chWidth;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var full = availableSize.Width;
        if (MaxChars > 0)
        {
            full = Math.Min(full, MaxChars * ChWidth());
        }
        var limited = availableSize.WithWidth(full);
        var size = base.MeasureOverride(limited);
        if (!IsBalanced || double.IsInfinity(full) || string.IsNullOrEmpty(Text))
        {
            return size;
        }
        var lines = TextLayout.TextLines.Count;
        if (lines < 2)
        {
            return size;
        }
        // The narrowest width that keeps the same number of lines (text-wrap: balance).
        var low = full / lines;
        var high = full;
        for (var i = 0; i < 7 && high - low > 1; i++)
        {
            var mid = (low + high) / 2;
            base.MeasureOverride(limited.WithWidth(mid));
            if (TextLayout.TextLines.Count > lines)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        // A pixel of slack so layout rounding never re-wraps the balanced lines.
        var width = Math.Min(full, Math.Ceiling(high) + 1);
        size = base.MeasureOverride(limited.WithWidth(width));
        while (TextLayout.TextLines.Count > lines && width < full)
        {
            width = Math.Min(full, width + 2);
            size = base.MeasureOverride(limited.WithWidth(width));
        }
        return size;
    }
}

/// <summary>
/// A wrapping row of items with equal gaps (CSS <c>flex-wrap: wrap; gap</c>), each line aligned left or centered and its
/// items centered vertically; <see cref="IsStacked"/> makes it a column of full-width items (the narrow actions).
/// </summary>
internal sealed class CtaFlowPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<CtaFlowPanel, double>(nameof(Spacing), 12);

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<CtaFlowPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<HorizontalAlignment> ContentAlignmentProperty =
        AvaloniaProperty.Register<CtaFlowPanel, HorizontalAlignment>(nameof(ContentAlignment), HorizontalAlignment.Left);

    private readonly List<(int Start, int End, double Width, double Height)> _lines = new();

    static CtaFlowPanel()
    {
        AffectsMeasure<CtaFlowPanel>(SpacingProperty, IsStackedProperty);
        AffectsArrange<CtaFlowPanel>(ContentAlignmentProperty);
    }

    /// <summary>Gap between items and between lines.</summary>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>One item per line, each as wide as the panel.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <summary>Where each line sits (Left or Center).</summary>
    public HorizontalAlignment ContentAlignment { get => GetValue(ContentAlignmentProperty); set => SetValue(ContentAlignmentProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        _lines.Clear();
        var gap = Spacing;
        var children = Children;
        var limit = availableSize.Width;
        if (IsStacked && !double.IsInfinity(limit))
        {
            var y = 0.0;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!child.IsVisible)
                {
                    continue;
                }
                child.Measure(new Size(limit, double.PositiveInfinity));
                y += (y > 0 ? gap : 0) + child.DesiredSize.Height;
                _lines.Add((i, i + 1, limit, child.DesiredSize.Height));
            }
            return new Size(limit, y);
        }
        double lineWidth = 0, lineHeight = 0, maxWidth = 0, total = 0;
        var start = 0;
        var any = false;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(limit, double.PositiveInfinity));
            var size = child.DesiredSize;
            if (any && lineWidth + gap + size.Width > limit)
            {
                _lines.Add((start, i, lineWidth, lineHeight));
                maxWidth = Math.Max(maxWidth, lineWidth);
                total += (total > 0 ? gap : 0) + lineHeight;
                start = i;
                lineWidth = size.Width;
                lineHeight = size.Height;
            }
            else
            {
                lineWidth += (any ? gap : 0) + size.Width;
                lineHeight = Math.Max(lineHeight, size.Height);
            }
            any = true;
        }
        if (any)
        {
            _lines.Add((start, children.Count, lineWidth, lineHeight));
            maxWidth = Math.Max(maxWidth, lineWidth);
            total += (total > 0 ? gap : 0) + lineHeight;
        }
        return new Size(maxWidth, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var gap = Spacing;
        var y = 0.0;
        foreach (var (start, end, width, height) in _lines)
        {
            var x = ContentAlignment == HorizontalAlignment.Center && !IsStacked ? Math.Max(0, (finalSize.Width - width) / 2) : 0;
            for (var i = start; i < end; i++)
            {
                var child = Children[i];
                if (!child.IsVisible)
                {
                    continue;
                }
                var size = child.DesiredSize;
                var w = IsStacked ? finalSize.Width : size.Width;
                child.Arrange(new Rect(x, y + (height - size.Height) / 2, w, size.Height));
                x += w + gap;
            }
            y += height + gap;
        }
        return finalSize;
    }
}

/// <summary>
/// The split layout: two equal columns with a gap and both columns centered vertically, or, when
/// <see cref="IsStacked"/>, one column with a smaller gap. The first child is the copy, the second the stage.
/// </summary>
internal sealed class CtaSplitPanel : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CtaSplitPanel, double>(nameof(Gap), 64);

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<CtaSplitPanel, bool>(nameof(IsStacked));

    static CtaSplitPanel()
    {
        AffectsMeasure<CtaSplitPanel>(GapProperty, IsStackedProperty);
    }

    /// <summary>The space between the columns (or rows when stacked).</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>One column.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        if (double.IsInfinity(width))
        {
            width = 0;
        }
        var column = IsStacked ? width : Math.Max(0, (width - Gap) / 2);
        double height = 0;
        var count = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(column, double.PositiveInfinity));
            height = IsStacked ? height + (count > 0 ? Gap : 0) + child.DesiredSize.Height : Math.Max(height, child.DesiredSize.Height);
            count++;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var column = IsStacked ? finalSize.Width : Math.Max(0, (finalSize.Width - Gap) / 2);
        var x = 0.0;
        var y = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var h = child.DesiredSize.Height;
            if (IsStacked)
            {
                child.Arrange(new Rect(0, y, column, h));
                y += h + Gap;
            }
            else
            {
                child.Arrange(new Rect(x, (finalSize.Height - h) / 2, column, h));
                x += column + Gap;
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The banner's content row: text on the left (it wraps) and the actions on the right, centered vertically; when
/// <see cref="IsStacked"/> the text sits above the actions, which are as wide as the banner.
/// </summary>
internal sealed class CtaBannerPanel : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CtaBannerPanel, double>(nameof(Gap), 16);

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<CtaBannerPanel, bool>(nameof(IsStacked));

    static CtaBannerPanel()
    {
        AffectsMeasure<CtaBannerPanel>(GapProperty, IsStackedProperty);
    }

    /// <summary>The space between the text and the actions.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>Text above actions.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var text = Children[0];
        var actions = Children[1];
        var width = availableSize.Width;
        if (IsStacked)
        {
            text.Measure(new Size(width, double.PositiveInfinity));
            actions.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, text.DesiredSize.Height + Gap + actions.DesiredSize.Height);
        }
        actions.Measure(new Size(width, double.PositiveInfinity));
        var room = Math.Max(0, width - actions.DesiredSize.Width - Gap);
        text.Measure(new Size(room, double.PositiveInfinity));
        return new Size(width, Math.Max(text.DesiredSize.Height, actions.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var text = Children[0];
        var actions = Children[1];
        if (IsStacked)
        {
            text.Arrange(new Rect(0, 0, finalSize.Width, text.DesiredSize.Height));
            actions.Arrange(new Rect(0, text.DesiredSize.Height + Gap, finalSize.Width, actions.DesiredSize.Height));
            return finalSize;
        }
        var aw = actions.DesiredSize.Width;
        actions.Arrange(new Rect(finalSize.Width - aw, (finalSize.Height - actions.DesiredSize.Height) / 2, aw, actions.DesiredSize.Height));
        var tw = Math.Max(0, finalSize.Width - aw - Gap);
        text.Arrange(new Rect(0, (finalSize.Height - text.DesiredSize.Height) / 2, tw, text.DesiredSize.Height));
        return finalSize;
    }
}
