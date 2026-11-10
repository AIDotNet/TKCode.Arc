using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// An agent's mark: a rounded tile tinted from <see cref="TintBrush"/> (the theme maps the tint to a series or accent
/// token through pseudo-classes, so it follows the theme and the accent) with the agent's glyph, or its initial, in
/// the full tint. Decorative: hidden from assistive technology, the card or heading names the agent.
/// </summary>
internal sealed class AgentGalleryMark : Border
{
    /// <summary>The color the tile is tinted from (set by the theme from <see cref="Tint"/>).</summary>
    public static readonly StyledProperty<IBrush?> TintBrushProperty =
        AvaloniaProperty.Register<AgentGalleryMark, IBrush?>(nameof(TintBrush));

    /// <summary>Share of the tint in the tile's background.</summary>
    public static readonly StyledProperty<double> TintAlphaProperty =
        AvaloniaProperty.Register<AgentGalleryMark, double>(nameof(TintAlpha), 0.14);

    private readonly ArcGlyph _glyph = new() { StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _initial = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
    private AgentGalleryTint _tint;
    private bool _hasTint;

    public AgentGalleryMark()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        IsHitTestVisible = false;
        Child = _glyph;
        SetTint(AgentGalleryTint.Accent);
    }

    public IBrush? TintBrush { get => GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }

    public double TintAlpha { get => GetValue(TintAlphaProperty); set => SetValue(TintAlphaProperty, value); }

    /// <summary>The glyph shown (null shows <see cref="SetAgent"/>'s initial).</summary>
    internal Geometry? Glyph => _glyph.Data;

    /// <summary>The tint as a pseudo-class (<c>:accent</c>, <c>:series1</c> … <c>:neutral</c>).</summary>
    internal AgentGalleryTint Tint => _tint;

    /// <summary>Shows <paramref name="agent"/>'s glyph (or initial) in its tint.</summary>
    public void SetAgent(AgentGalleryItem agent)
    {
        SetTint(agent.Tint);
        if (agent.Glyph is { } glyph)
        {
            _glyph.Data = glyph;
            Child = _glyph;
        }
        else
        {
            _glyph.Data = null;
            _initial.Text = agent.Name.Length > 0 ? agent.Name[..1].ToUpperInvariant() : "?";
            Child = _initial;
        }
    }

    private void SetTint(AgentGalleryTint tint)
    {
        if (_hasTint && tint == _tint)
        {
            return;
        }
        _hasTint = true;
        _tint = tint;
        PseudoClasses.Set(":accent", tint == AgentGalleryTint.Accent);
        PseudoClasses.Set(":series1", tint == AgentGalleryTint.Series1);
        PseudoClasses.Set(":series2", tint == AgentGalleryTint.Series2);
        PseudoClasses.Set(":series3", tint == AgentGalleryTint.Series3);
        PseudoClasses.Set(":series4", tint == AgentGalleryTint.Series4);
        PseudoClasses.Set(":neutral", tint == AgentGalleryTint.Neutral);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TintBrushProperty || change.Property == TintAlphaProperty)
        {
            var color = (TintBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
            Background = new ImmutableSolidColorBrush(color, Math.Clamp(TintAlpha, 0, 1));
            _glyph.Foreground = TintBrush;
            _initial.Foreground = TintBrush;
        }
        else if (change.Property == BoundsProperty)
        {
            var side = Math.Min(Bounds.Width, Bounds.Height);
            if (side > 0)
            {
                _glyph.Size = Math.Round(side * 0.5);
                _initial.FontSize = Math.Round(side * 0.42);
            }
        }
    }
}

/// <summary>A capability tag on a card: a small pill of muted text.</summary>
internal sealed class AgentGalleryTag : Border
{
    private readonly TextBlock _text = new() { TextTrimming = TextTrimming.CharacterEllipsis };

    public AgentGalleryTag()
    {
        Child = _text;
        Padding = new Thickness(8, 2);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(_text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        _text.LineHeight = 18;
        Bind(BackgroundProperty, this.GetResourceObservable(AgentGalleryTokens.Key(AgentGalleryTokenKind.TagBackground), ToBrush));
    }

    private static object? ToBrush(object? value) => value is Color color ? new ImmutableSolidColorBrush(color) : value;

    /// <summary>The tag's text.</summary>
    public string? Text
    {
        get => _text.Text;
        set => _text.Text = value;
    }
}

/// <summary>
/// The card grid: as many equal columns as fit at <see cref="MinItemWidth"/> (at most <see cref="MaxColumns"/>), the
/// cards of a row as tall as its tallest, <see cref="Gap"/> across and down. Hidden children (the pool's spare cards)
/// take no slot. <see cref="Columns"/> is what the keyboard moves Up and Down by.
/// </summary>
internal sealed class AgentGalleryGrid : Panel
{
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<AgentGalleryGrid, double>(nameof(MinItemWidth), 248);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AgentGalleryGrid, double>(nameof(Gap), 12);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<AgentGalleryGrid, int>(nameof(MaxColumns), 4);

    private readonly List<double> _rowHeights = new();

    static AgentGalleryGrid()
    {
        AffectsMeasure<AgentGalleryGrid>(MinItemWidthProperty, GapProperty, MaxColumnsProperty);
    }

    public double MinItemWidth { get => GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    /// <summary>The number of columns of the last measure (at least 1).</summary>
    public int Columns { get; private set; } = 1;

    /// <summary>The column count for <paramref name="width"/>.</summary>
    internal static int ColumnsFor(double width, double minItem, double gap, int maxColumns)
    {
        var max = Math.Max(1, maxColumns);
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return max;
        }
        var fit = (int)Math.Floor((width + gap) / (Math.Max(1, minItem) + gap));
        return Math.Clamp(fit, 1, max);
    }

    private double ItemWidth(double width, int columns)
        => double.IsInfinity(width) ? MinItemWidth : Math.Max(0, (width - (columns - 1) * Gap) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = ColumnsFor(availableSize.Width, MinItemWidth, Gap, MaxColumns);
        Columns = columns;
        var itemWidth = ItemWidth(availableSize.Width, columns);
        _rowHeights.Clear();
        var index = 0;
        var rowHeight = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            index++;
            if (index % columns == 0)
            {
                _rowHeights.Add(rowHeight);
                rowHeight = 0;
            }
        }
        if (index % columns != 0)
        {
            _rowHeights.Add(rowHeight);
        }
        var height = _rowHeights.Sum() + Math.Max(0, _rowHeights.Count - 1) * Gap;
        var width = double.IsInfinity(availableSize.Width) ? columns * itemWidth + (columns - 1) * Gap : availableSize.Width;
        return new Size(index == 0 ? 0 : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width, MinItemWidth, Gap, MaxColumns);
        Columns = columns;
        var itemWidth = ItemWidth(finalSize.Width, columns);
        var index = 0;
        var y = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var row = index / columns;
            var column = index % columns;
            if (column == 0 && row > 0)
            {
                y += (row - 1 < _rowHeights.Count ? _rowHeights[row - 1] : 0) + Gap;
            }
            var height = row < _rowHeights.Count ? _rowHeights[row] : child.DesiredSize.Height;
            child.Arrange(new Rect(column * (itemWidth + Gap), y, itemWidth, height));
            index++;
        }
        return finalSize;
    }
}
