using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>One-shot timers of a <see cref="SmartFormFill"/>: the simulated read and save, the fill stagger and the autoplay loop.</summary>
internal interface ISmartFormFillTimers
{
    /// <summary>Runs <paramref name="callback"/> once after <paramref name="delay"/>; disposing the result cancels it.</summary>
    IDisposable Start(TimeSpan delay, Action callback);
}

/// <summary>Wall-clock timers on the UI thread (a zero delay runs on the next dispatcher pass).</summary>
internal sealed class SmartFormFillDispatcherTimers : ISmartFormFillTimers
{
    public static readonly SmartFormFillDispatcherTimers Instance = new();

    public IDisposable Start(TimeSpan delay, Action callback) => new Shot(delay, callback);

    private sealed class Shot : IDisposable
    {
        private DispatcherTimer? _timer;
        private Action? _callback;

        public Shot(TimeSpan delay, Action callback)
        {
            _callback = callback;
            if (delay <= TimeSpan.Zero)
            {
                Dispatcher.UIThread.Post(Fire, DispatcherPriority.Background);
                return;
            }
            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay };
            _timer.Tick += (_, _) => Fire();
            _timer.Start();
        }

        private void Fire()
        {
            _timer?.Stop();
            _timer = null;
            var callback = _callback;
            _callback = null;
            callback?.Invoke();
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
            _callback = null;
        }
    }
}

/// <summary>
/// The form grid of a <see cref="SmartFormFill"/>: fields in order, two half fields to a row and full fields on a row of
/// their own; a half field followed by a full one keeps its half. <see cref="IsSingleColumn"/> (under 440px) puts every
/// field on its own row. Rows take the height of their tallest field, so a note under one field never overlaps the next row.
/// </summary>
internal sealed class SmartFormFillFormPanel : Panel
{
    public static readonly StyledProperty<bool> IsSingleColumnProperty =
        AvaloniaProperty.Register<SmartFormFillFormPanel, bool>(nameof(IsSingleColumn));

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<SmartFormFillFormPanel, double>(nameof(ColumnGap), 12);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<SmartFormFillFormPanel, double>(nameof(RowGap), 14);

    static SmartFormFillFormPanel()
    {
        AffectsMeasure<SmartFormFillFormPanel>(IsSingleColumnProperty, ColumnGapProperty, RowGapProperty);
    }

    public bool IsSingleColumn { get => GetValue(IsSingleColumnProperty); set => SetValue(IsSingleColumnProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>The rows: each a list of (child, column, columns spanned), two columns wide.</summary>
    private List<List<(Control Child, int Column, int Span)>> Rows()
    {
        var rows = new List<List<(Control, int, int)>>();
        List<(Control, int, int)>? open = null;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var full = IsSingleColumn || (child as SmartFormFillFieldView)?.Field?.Span == SmartFormFillSpan.Full;
            if (full)
            {
                rows.Add([(child, 0, 2)]);
                open = null;
            }
            else if (open is not null)
            {
                open.Add((child, 1, 1));
                open = null;
            }
            else
            {
                open = [(child, 0, 1)];
                rows.Add(open);
            }
        }
        return rows;
    }

    private double ColumnWidth(double width) => Math.Max(0, (width - ColumnGap) / 2);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        var height = 0.0;
        var rows = Rows();
        foreach (var row in rows)
        {
            var rowHeight = 0.0;
            foreach (var (child, _, span) in row)
            {
                // Whole pixels: the grid can arrange this panel a fraction narrower than it measured it (layout rounding),
                // and a field measured wider than its slot would be clipped at its right edge.
                child.Measure(new Size(Math.Floor(span == 2 ? width : ColumnWidth(width)), double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }
            height += rowHeight;
        }
        if (rows.Count > 1)
        {
            height += RowGap * (rows.Count - 1);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var column = ColumnWidth(width);
        var y = 0.0;
        foreach (var row in Rows())
        {
            var rowHeight = row.Max(c => c.Child.DesiredSize.Height);
            foreach (var (child, index, span) in row)
            {
                var x = index == 0 ? 0 : column + ColumnGap;
                var slot = span == 2 ? width : column;
                child.Arrange(new Rect(x, y, slot, rowHeight));
            }
            y += rowHeight + RowGap;
        }
        return finalSize;
    }
}

/// <summary>
/// Hosts the paste area and the pasted text view in the well. It asks for its least height only, never its content's, and
/// gives its children the whole height it gets, so the well beside the form follows the form's height and the text area
/// (whose frame has a fixed height) is sized to the host instead of the other way round.
/// </summary>
internal sealed class SmartFormFillWellHost : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsInfinity(availableSize.Height) ? MinHeight : Math.Max(MinHeight, availableSize.Height);
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, Math.Max(0, height)));
        }
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is Textarea area && Math.Abs((double.IsNaN(area.FrameHeight) ? 0 : area.FrameHeight) - finalSize.Height) > 0.5)
            {
                // The text area's frame takes the host's height; the host's own height never depends on it.
                var height = Math.Max(0, finalSize.Height);
                Dispatcher.UIThread.Post(() => area.FrameHeight = height, DispatcherPriority.Render);
            }
            child.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }
}

/// <summary>Builds the company logo slot: the app's own mark, else a monogram in a tinted rounded square, else a building.</summary>
internal static class SmartFormFillLogo
{
    /// <summary>Lucide "building-2".</summary>
    public static Geometry Building => ArcIcons.Parse(
        "M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Z M6 12H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2 M18 9h2a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-2 " +
        "M10 6h4 M10 10h4 M10 14h4 M10 18h4");

    /// <summary>The key a logo is cached under: empty for the building, else the first letter.</summary>
    public static string KeyOf(string? company)
    {
        var name = company?.Trim();
        return string.IsNullOrEmpty(name) ? string.Empty : char.ToUpperInvariant(name[0]).ToString();
    }

    public static Control Create(string? company, Func<string, Control?>? resolve, double size)
    {
        var name = company?.Trim() ?? string.Empty;
        if (name.Length > 0 && resolve?.Invoke(name) is { } own)
        {
            own.Width = size;
            own.Height = size;
            return own;
        }
        if (name.Length == 0)
        {
            var glyph = new ArcGlyph { Data = Building, Size = size - 4, Width = size, Height = size };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            return glyph;
        }
        var letter = new TextBlock
        {
            Text = KeyOf(name),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ArcTokens.Bind(letter, TextBlock.ForegroundProperty, ArcDesignTokenKind.AccentStrong);
        ArcTokens.Bind(letter, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        var square = new Border { Width = size, Height = size, Child = letter }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(square, Border.BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
        return square;
    }
}
