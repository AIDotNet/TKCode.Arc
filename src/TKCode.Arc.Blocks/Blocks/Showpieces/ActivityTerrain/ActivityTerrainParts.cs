using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How the summary's three blocks are laid out.</summary>
internal enum ActivityTerrainStatsLayout
{
    /// <summary>Stacked in the column beside the plot (containers of 880 and wider).</summary>
    Column,

    /// <summary>Three equal columns under the plot (560 to 879).</summary>
    Row,

    /// <summary>The total and the busiest day side by side, the peak hours under them (below 560).</summary>
    Narrow
}

/// <summary>
/// The body: the plot column and the summary, side by side when wide (the summary <see cref="StatsWidth"/> wide,
/// <see cref="Gap"/> apart, stretched to the body's height so its divider runs the full length), else stacked.
/// </summary>
internal sealed class ActivityTerrainBodyPanel : Panel
{
    public static readonly StyledProperty<bool> IsWideProperty =
        AvaloniaProperty.Register<ActivityTerrainBodyPanel, bool>(nameof(IsWide));

    public static readonly StyledProperty<double> StatsWidthProperty =
        AvaloniaProperty.Register<ActivityTerrainBodyPanel, double>(nameof(StatsWidth), 224);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ActivityTerrainBodyPanel, double>(nameof(Gap), 20);

    static ActivityTerrainBodyPanel()
    {
        AffectsMeasure<ActivityTerrainBodyPanel>(IsWideProperty, StatsWidthProperty, GapProperty);
    }

    public bool IsWide { get => GetValue(IsWideProperty); set => SetValue(IsWideProperty, value); }

    public double StatsWidth { get => GetValue(StatsWidthProperty); set => SetValue(StatsWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private Control? Main => Children.Count > 0 ? Children[0] : null;

    private Control? Side => Children.Count > 1 ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        if (IsWide && double.IsFinite(width))
        {
            var mainWidth = Math.Max(0, width - StatsWidth - Gap);
            Main?.Measure(new Size(mainWidth, double.PositiveInfinity));
            Side?.Measure(new Size(StatsWidth, double.PositiveInfinity));
            var height = Math.Max(Main?.DesiredSize.Height ?? 0, Side?.DesiredSize.Height ?? 0);
            return new Size(width, height);
        }
        Main?.Measure(new Size(width, double.PositiveInfinity));
        Side?.Measure(new Size(width, double.PositiveInfinity));
        var stacked = (Main?.DesiredSize.Height ?? 0) + (Side is { IsVisible: true } ? Gap + Side.DesiredSize.Height : 0);
        var desiredWidth = Math.Max(Main?.DesiredSize.Width ?? 0, Side?.DesiredSize.Width ?? 0);
        return new Size(double.IsFinite(width) ? width : desiredWidth, stacked);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (IsWide)
        {
            var mainWidth = Math.Max(0, finalSize.Width - StatsWidth - Gap);
            Main?.Arrange(new Rect(0, 0, mainWidth, Main.DesiredSize.Height));
            Side?.Arrange(new Rect(mainWidth + Gap, 0, StatsWidth, finalSize.Height));
            return finalSize;
        }
        var mainHeight = Main?.DesiredSize.Height ?? 0;
        Main?.Arrange(new Rect(0, 0, finalSize.Width, mainHeight));
        Side?.Arrange(new Rect(0, mainHeight + Gap, finalSize.Width, Side.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>Lays out the summary's three blocks (total, peak hours, busiest day) per <see cref="Layout"/>.</summary>
internal sealed class ActivityTerrainStatsPanel : Panel
{
    public static readonly StyledProperty<ActivityTerrainStatsLayout> LayoutProperty =
        AvaloniaProperty.Register<ActivityTerrainStatsPanel, ActivityTerrainStatsLayout>(nameof(Layout), ActivityTerrainStatsLayout.Row);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ActivityTerrainStatsPanel, double>(nameof(ColumnGap), 20);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ActivityTerrainStatsPanel, double>(nameof(RowGap), 16);

    static ActivityTerrainStatsPanel()
    {
        AffectsMeasure<ActivityTerrainStatsPanel>(LayoutProperty, ColumnGapProperty, RowGapProperty);
    }

    public ActivityTerrainStatsLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private Control? Block(int index) => Children.Count > index ? Children[index] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        var total = Block(0);
        var peaks = Block(1);
        var busiest = Block(2);
        switch (Layout)
        {
            case ActivityTerrainStatsLayout.Column:
            {
                double height = 0;
                foreach (var block in Children)
                {
                    block.Measure(new Size(width, double.PositiveInfinity));
                    height += block.DesiredSize.Height;
                }
                return new Size(width, height);
            }
            case ActivityTerrainStatsLayout.Narrow:
            {
                var column = Math.Max(0, (width - ColumnGap) / 2);
                total?.Measure(new Size(column, double.PositiveInfinity));
                busiest?.Measure(new Size(column, double.PositiveInfinity));
                peaks?.Measure(new Size(width, double.PositiveInfinity));
                var first = Math.Max(total?.DesiredSize.Height ?? 0, busiest?.DesiredSize.Height ?? 0);
                return new Size(width, first + RowGap + (peaks?.DesiredSize.Height ?? 0));
            }
            default:
            {
                var column = Math.Max(0, (width - 2 * ColumnGap) / 3);
                double height = 0;
                foreach (var block in Children)
                {
                    block.Measure(new Size(column, double.PositiveInfinity));
                    height = Math.Max(height, block.DesiredSize.Height);
                }
                return new Size(width, height);
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var total = Block(0);
        var peaks = Block(1);
        var busiest = Block(2);
        switch (Layout)
        {
            case ActivityTerrainStatsLayout.Column:
            {
                double y = 0;
                foreach (var block in Children)
                {
                    block.Arrange(new Rect(0, y, width, block.DesiredSize.Height));
                    y += block.DesiredSize.Height;
                }
                break;
            }
            case ActivityTerrainStatsLayout.Narrow:
            {
                var column = Math.Max(0, (width - ColumnGap) / 2);
                var first = Math.Max(total?.DesiredSize.Height ?? 0, busiest?.DesiredSize.Height ?? 0);
                total?.Arrange(new Rect(0, 0, column, first));
                busiest?.Arrange(new Rect(column + ColumnGap, 0, column, first));
                peaks?.Arrange(new Rect(0, first + RowGap, width, peaks.DesiredSize.Height));
                break;
            }
            default:
            {
                var column = Math.Max(0, (width - 2 * ColumnGap) / 3);
                total?.Arrange(new Rect(0, 0, column, finalSize.Height));
                peaks?.Arrange(new Rect(column + ColumnGap, 0, column, finalSize.Height));
                busiest?.Arrange(new Rect(2 * (column + ColumnGap), 0, column, finalSize.Height));
                break;
            }
        }
        return finalSize;
    }
}

/// <summary>
/// One row of the peak hours: the day and hour on the left, the value (rolling digits) on the right. A toggle in look
/// and for screen readers (pressed while the camera is on that hour); the terrain decides the pressed state, so clicks
/// do not toggle it here.
/// </summary>
internal sealed class ActivityTerrainPeakButton : ToggleButton
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ActivityTerrainPeakButton, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ActivityTerrainPeakButton, double>(nameof(Value));

    public static readonly StyledProperty<Func<double, string>?> FormatterProperty =
        AvaloniaProperty.Register<ActivityTerrainPeakButton, Func<double, string>?>(nameof(Formatter));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public Func<double, string>? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    /// <summary>The hour the row stands for.</summary>
    internal ActivityTerrainCell Cell { get; set; }

    protected override Type StyleKeyOverride => typeof(ActivityTerrainPeakButton);

    protected override void Toggle()
    {
    }
}

/// <summary>The "Reset view" button in the plot's corner: 32px high, radius 10, a rotate-ccw glyph and a label.</summary>
internal sealed class ActivityTerrainResetButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ActivityTerrainResetButton);
}

/// <summary>
/// A line of text that wraps by words and rolls its numbers: the busiest day's "{0} {1}, {2} in working hours", where
/// {0} is a count, {1} the unit and {2} a percentage (both numbers roll on <see cref="ShowpieceNumber"/>).
/// </summary>
internal sealed class ActivityTerrainCaption : WrapPanel
{
    private readonly ShowpieceNumber _count = new() { Format = ShowpieceNumberFormat.Number };
    private readonly ShowpieceNumber _share = new() { Format = ShowpieceNumberFormat.Percent, Decimals = 1 };
    private string? _format;
    private string? _unit;

    public ActivityTerrainCaption()
    {
        ItemSpacing = 3.2;
        LineSpacing = 0;
        Orientation = Orientation.Horizontal;
    }

    internal ShowpieceNumber Count => _count;

    internal ShowpieceNumber Share => _share;

    /// <summary>The whole caption as plain text (screen readers).</summary>
    internal string Text { get; private set; } = string.Empty;

    /// <summary>Sets the words around the numbers (only rebuilt when the format or the unit change) and the numbers.</summary>
    public void Update(string format, string unit, double count, double share, Func<double, string>? countFormatter)
    {
        _count.Formatter = countFormatter;
        _count.Value = count;
        _share.Value = share;
        if (format != _format || unit != _unit)
        {
            _format = format;
            _unit = unit;
            Rebuild(format, unit);
        }
        Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, format,
            countFormatter?.Invoke(count) ?? ShowpieceFormat.Grouped(count), unit, ShowpieceFormat.Percent(share, 1));
        Avalonia.Automation.AutomationProperties.SetName(this, Text);
    }

    private void Rebuild(string format, string unit)
    {
        // The numbers keep their wheels across rebuilds: take them out of whatever group held them.
        foreach (var number in new[] { _count, _share })
        {
            if (number.Parent is Panel parent)
            {
                parent.Children.Remove(number);
            }
        }
        Children.Clear();
        foreach (var word in format.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith("{0}", StringComparison.Ordinal) || word.StartsWith("{2}", StringComparison.Ordinal))
            {
                var number = word[1] == '0' ? _count : _share;
                var rest = word[3..];
                if (rest.Length == 0)
                {
                    Children.Add(number);
                }
                else
                {
                    var group = new StackPanel { Orientation = Orientation.Horizontal };
                    group.Children.Add(number);
                    group.Children.Add(Word(rest.Replace("{1}", unit, StringComparison.Ordinal)));
                    Children.Add(group);
                }
                continue;
            }
            Children.Add(Word(word.Replace("{1}", unit, StringComparison.Ordinal)));
        }
    }

    private static TextBlock Word(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
}
