using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>The controls of one request row of a <see cref="PromptCache"/>.</summary>
internal sealed class PromptCacheRowView
{
    public PromptCacheRowView(PromptCacheBar bar)
    {
        Bar = bar;
        // The title turns from muted to the foreground color when the request is sent: two layers, the top one's
        // opacity driven by the story clock (so it pauses with the story) instead of a theme transition.
        TitleMuted = new TextBlock { FontSize = 14, LineHeight = 19.6, FontWeight = FontWeight.Medium };
        Title = new TextBlock { FontSize = 14, LineHeight = 19.6, FontWeight = FontWeight.Medium, Opacity = 0 };
        Time = new TextBlock { FontSize = 14, LineHeight = 19.6, FontFeatures = PromptCacheText.Tabular, Opacity = 0 };
        Status = new PromptCacheStatus { HorizontalContentAlignment = HorizontalAlignment.Right };
        ArcTokens.Bind(TitleMuted, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(Title, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(Time, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        AutomationProperties.SetAccessibilityView(Time, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(Status, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(Title, AccessibilityView.Raw);
        for (var i = 0; i < Numbers.Length; i++)
        {
            Numbers[i] = new TextBlock { FontSize = 14, LineHeight = 19.6, FontFeatures = PromptCacheText.Tabular, Text = Dash };
            ArcTokens.Bind(Numbers[i], TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
            AutomationProperties.SetAccessibilityView(Numbers[i], AccessibilityView.Raw);
        }
    }

    /// <summary>The placeholder of a value not known yet.</summary>
    public const string Dash = "–";

    /// <summary>"Request 1" in the muted color (a queued row); its accessible name is the row's whole description.</summary>
    public TextBlock TitleMuted { get; }

    /// <summary>"Request 1" in the foreground color, shown once the request is sent.</summary>
    public TextBlock Title { get; }

    /// <summary>The simulated time it was sent ("0:38").</summary>
    public TextBlock Time { get; }

    public PromptCacheStatus Status { get; }

    public PromptCacheBar Bar { get; }

    /// <summary>Processed tokens, input cost and first token.</summary>
    public TextBlock[] Numbers { get; } = new TextBlock[3];

    /// <summary>Set when the row's texts changed and its numbers must be written again.</summary>
    public bool Dirty { get; set; } = true;

    public IEnumerable<Control> Controls()
    {
        yield return TitleMuted;
        yield return Title;
        yield return Time;
        yield return Status;
        yield return Bar;
        foreach (var n in Numbers)
        {
            yield return n;
        }
    }
}

/// <summary>
/// The requests of a <see cref="PromptCache"/> laid out as one grid: a header with the two fill keys and the three column
/// headings over a hairline, then a row per request with its title, time and status over its bar, and its three numbers.
/// The number columns are as wide as their widest possible value, so values line up across rows and do not jump while
/// they roll. Under 480px a row takes three lines (header, bar, numbers in three equal columns).
/// </summary>
internal sealed class PromptCacheTable : Control
{
    /// <summary>Defines the <see cref="Mode"/> property.</summary>
    public static readonly StyledProperty<PromptCacheWidthMode> ModeProperty =
        AvaloniaProperty.Register<PromptCacheTable, PromptCacheWidthMode>(nameof(Mode));

    /// <summary>Defines the <see cref="SeparatorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SeparatorBrushProperty =
        AvaloniaProperty.Register<PromptCacheTable, IBrush?>(nameof(SeparatorBrush));

    private const double ColumnGap = 12;
    private const double HeaderLine = 16.8;
    private const double HeaderPad = 8;
    private const double HeaderGap = 12;
    private const double RowGap = 16;
    private const double LineHeight = 20;
    private const double BarTop = 26;
    private const double KeySize = 10;
    private const double KeyGap = 6;
    private const double KeySpacing = 14;

    private readonly List<PromptCacheRowView> _rows = new();
    private readonly TextBlock[] _headings = new TextBlock[3];
    private readonly TextBlock[] _sizers = new TextBlock[3];
    private readonly TextBlock _keyProcessed;
    private readonly TextBlock _keyCached;
    private Rect _keySolid;
    private Rect _keyCachedRect;
    private double _separatorY;

    static PromptCacheTable()
    {
        AffectsMeasure<PromptCacheTable>(ModeProperty);
        AffectsRender<PromptCacheTable>(SeparatorBrushProperty);
    }

    public PromptCacheTable()
    {
        _keyProcessed = Small();
        _keyCached = Small();
        Add(_keyProcessed);
        Add(_keyCached);
        for (var i = 0; i < 3; i++)
        {
            _headings[i] = Small();
            Add(_headings[i]);
            _sizers[i] = new TextBlock { FontSize = 14, LineHeight = 19.6, FontFeatures = PromptCacheText.Tabular, Opacity = 0, IsHitTestVisible = false };
            AutomationProperties.SetAccessibilityView(_sizers[i], AccessibilityView.Raw);
            Add(_sizers[i]);
        }
        AutomationProperties.SetAccessibilityView(_keyProcessed, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_keyCached, AccessibilityView.Raw);
    }

    public PromptCacheWidthMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>The hairline under the header (BorderSubtle).</summary>
    public IBrush? SeparatorBrush { get => GetValue(SeparatorBrushProperty); set => SetValue(SeparatorBrushProperty, value); }

    /// <summary>The palette for the two fill keys.</summary>
    public PromptCachePalette? Palette { get; set; }

    public IReadOnlyList<PromptCacheRowView> Rows => _rows;

    private static TextBlock Small()
    {
        var text = new TextBlock { FontSize = 12, LineHeight = HeaderLine };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return text;
    }

    private void Add(Control control)
    {
        VisualChildren.Add(control);
        LogicalChildren.Add(control);
    }

    /// <summary>Sets the header texts.</summary>
    public void SetHeader(string processedKey, string cachedKey, string processed, string cost, string firstToken)
    {
        _keyProcessed.Text = processedKey;
        _keyCached.Text = cachedKey;
        _headings[0].Text = processed;
        _headings[1].Text = cost;
        _headings[2].Text = firstToken;
    }

    /// <summary>Sets the widest value each number column must hold.</summary>
    public void SetColumnSamples(string processed, string cost, string firstToken)
    {
        _sizers[0].Text = processed;
        _sizers[1].Text = cost;
        _sizers[2].Text = firstToken;
    }

    /// <summary>Replaces the rows (their bars come from the owner).</summary>
    public void SetRows(IReadOnlyList<PromptCacheRowView> rows)
    {
        foreach (var row in _rows)
        {
            foreach (var c in row.Controls())
            {
                VisualChildren.Remove(c);
                LogicalChildren.Remove(c);
            }
        }
        _rows.Clear();
        _rows.AddRange(rows);
        foreach (var row in _rows)
        {
            foreach (var c in row.Controls())
            {
                Add(c);
            }
        }
        InvalidateMeasure();
    }

    private double[] Columns(double width, out double first)
    {
        var columns = new double[3];
        for (var i = 0; i < 3; i++)
        {
            columns[i] = Math.Max(_headings[i].DesiredSize.Width, _sizers[i].DesiredSize.Width);
            foreach (var row in _rows)
            {
                columns[i] = Math.Max(columns[i], row.Numbers[i].DesiredSize.Width);
            }
            columns[i] = Math.Ceiling(columns[i]);
        }
        first = Math.Max(0, width - columns.Sum() - 3 * ColumnGap);
        return columns;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 478;
        foreach (var child in VisualChildren)
        {
            if (child is Control c)
            {
                c.Measure(Size.Infinity);
            }
        }
        double height;
        if (Mode == PromptCacheWidthMode.Compact)
        {
            height = HeaderLine + HeaderPad + HeaderLine + HeaderPad + 1 + HeaderGap;
            height += _rows.Count * (BarTop + 10 + 6.2 + 19.6) + Math.Max(0, _rows.Count - 1) * RowGap;
        }
        else
        {
            Columns(width, out var first);
            foreach (var row in _rows)
            {
                row.Bar.Measure(new Size(first, 10));
            }
            height = HeaderLine + HeaderPad + 1 + HeaderGap;
            height += _rows.Count * (BarTop + 10) + Math.Max(0, _rows.Count - 1) * RowGap;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        foreach (var s in _sizers)
        {
            s.Arrange(new Rect(0, 0, 0, 0));
        }
        // Header keys: a solid and a cached square, each with its label.
        var keyY = (HeaderLine - KeySize) / 2;
        _keySolid = new Rect(0, keyY, KeySize, KeySize);
        var x = KeySize + KeyGap;
        _keyProcessed.Arrange(new Rect(x, 0, _keyProcessed.DesiredSize.Width, HeaderLine));
        x += _keyProcessed.DesiredSize.Width + KeySpacing;
        _keyCachedRect = new Rect(x, keyY, KeySize, KeySize);
        x += KeySize + KeyGap;
        _keyCached.Arrange(new Rect(x, 0, _keyCached.DesiredSize.Width, HeaderLine));
        double y;
        if (Mode == PromptCacheWidthMode.Compact)
        {
            var third = width / 3;
            var headY = HeaderLine + HeaderPad;
            for (var i = 0; i < 3; i++)
            {
                _headings[i].Arrange(new Rect(third * i, headY, _headings[i].DesiredSize.Width, HeaderLine));
            }
            _separatorY = headY + HeaderLine + HeaderPad;
            y = _separatorY + 1 + HeaderGap;
            foreach (var row in _rows)
            {
                ArrangeTop(row, 0, y, width);
                row.Bar.Arrange(new Rect(0, y + BarTop, width, 10));
                var numbersY = y + BarTop + 10 + 6.2;
                for (var i = 0; i < 3; i++)
                {
                    var n = row.Numbers[i];
                    n.Arrange(new Rect(third * i, numbersY, n.DesiredSize.Width, 19.6));
                }
                y = numbersY + 19.6 + RowGap;
            }
        }
        else
        {
            var columns = Columns(width, out var first);
            var cx = first + ColumnGap;
            var lefts = new double[3];
            for (var i = 0; i < 3; i++)
            {
                lefts[i] = cx;
                var h = _headings[i];
                h.Arrange(new Rect(cx + columns[i] - h.DesiredSize.Width, 0, h.DesiredSize.Width, HeaderLine));
                cx += columns[i] + ColumnGap;
            }
            _separatorY = HeaderLine + HeaderPad;
            y = _separatorY + 1 + HeaderGap;
            foreach (var row in _rows)
            {
                ArrangeTop(row, 0, y, first);
                row.Bar.Arrange(new Rect(0, y + BarTop, first, 10));
                var rowHeight = BarTop + 10;
                for (var i = 0; i < 3; i++)
                {
                    var n = row.Numbers[i];
                    var nh = n.DesiredSize.Height;
                    n.Arrange(new Rect(lefts[i] + columns[i] - n.DesiredSize.Width, y + (rowHeight - nh) / 2, n.DesiredSize.Width, nh));
                }
                y += rowHeight + RowGap;
            }
        }
        return finalSize;
    }

    private static void ArrangeTop(PromptCacheRowView row, double x, double y, double width)
    {
        var title = row.TitleMuted;
        var time = row.Time;
        var status = row.Status;
        var sw = Math.Min(status.DesiredSize.Width, width);
        status.Arrange(new Rect(x + width - sw, y, sw, LineHeight));
        var tw = Math.Min(title.DesiredSize.Width, Math.Max(0, width - sw - 8));
        title.Arrange(new Rect(x, y, tw, LineHeight));
        row.Title.Arrange(new Rect(x, y, tw, LineHeight));
        var timeX = x + tw + 8;
        var timeW = Math.Min(time.DesiredSize.Width, Math.Max(0, width - sw - 8 - (timeX - x)));
        time.Arrange(new Rect(timeX, y, timeW, LineHeight));
    }

    public override void Render(DrawingContext context)
    {
        if (SeparatorBrush is { } line)
        {
            context.FillRectangle(line, new Rect(0, _separatorY, Bounds.Width, 1));
        }
        if (Palette is { } palette)
        {
            context.DrawRectangle(palette.KeySolid, null, _keySolid, 3, 3);
            context.DrawRectangle(palette.KeyCached, null, _keyCachedRect, 3, 3);
            context.DrawRectangle(null, palette.KeyOutline, _keyCachedRect.Deflate(0.5), 2.5, 2.5);
        }
    }
}
