using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

public sealed partial class DemoOverviewPage
{
    private readonly List<MetricTile> _tiles = new();

    /// <summary>
    /// The key metrics: four tiles in one card (two by two below 860), each a counter, its change against the previous
    /// range and a sparkline. The range in the hero drives them.
    /// </summary>
    private Control BuildMetrics()
    {
        var grid = new Grid();
        foreach (var metric in OverviewData.Metrics)
        {
            var tile = new MetricTile(metric);
            _tiles.Add(tile);
            grid.Children.Add(tile.Root);
        }
        var columns = 0;
        OnWidth(width =>
        {
            var next = width <= NarrowBelow ? 2 : 4;
            foreach (var tile in _tiles)
            {
                tile.SetCompact(width <= CompactBelow);
            }
            if (next == columns)
            {
                return;
            }
            columns = next;
            grid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)));
            grid.RowDefinitions = new RowDefinitions(columns == 4 ? "Auto" : "Auto,Auto");
            for (var i = 0; i < _tiles.Count; i++)
            {
                var root = _tiles[i].Root;
                Grid.SetColumn(root, i % columns);
                Grid.SetRow(root, i / columns);
                root.BorderThickness = new Thickness(i % columns == 0 ? 0 : 1, i / columns == 0 ? 0 : 1, 0, 0);
            }
        });
        var card = DemoUi.Card(grid, default);
        card.ClipToBounds = true;
        Avalonia.Automation.AutomationProperties.SetName(card, "Key metrics");
        return card;
    }

    private void ShowMetricsRange(OverviewRange range)
    {
        foreach (var tile in _tiles)
        {
            tile.Show(range);
        }
    }

    /// <summary>
    /// A key metric tile (no Arc equivalent): label, counter, a delta chip with the comparison, and a sparkline whose
    /// scrub readout shows above the line.
    /// </summary>
    private sealed class MetricTile
    {
        private readonly OverviewMetric _metric;
        private readonly AnimatedCounter _counter;
        private readonly ArcGlyph _arrow = new() { Size = 12, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _delta = DemoUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 12, FontWeight.Medium);
        private readonly OverviewTokenSlot _arrowColor;
        private readonly OverviewTokenSlot _deltaColor;
        private readonly ArcContentSwap _compare;
        private readonly Sparkline _sparkline;
        private string? _rangeId;

        public MetricTile(OverviewMetric metric)
        {
            _metric = metric;
            _arrowColor = new OverviewTokenSlot(_arrow, ArcGlyph.ForegroundProperty);
            _deltaColor = new OverviewTokenSlot(_delta, TextBlock.ForegroundProperty);
            var label = DemoUi.Line(metric.Label, ArcDesignTokenKind.TextSecondary);
            label.Margin = new Thickness(0, 0, 0, 6);
            _counter = new AnimatedCounter
            {
                Prefix = metric.Prefix.Length > 0 ? metric.Prefix : null,
                Suffix = metric.Suffix.Length > 0 ? metric.Suffix : null,
                Decimals = metric.Decimals,
                AnimateOnView = true,
                FontSize = 24,
                FontWeight = FontWeight.Medium,
                MinHeight = 30,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _delta.TextWrapping = TextWrapping.NoWrap;
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            chip.Children.Add(_arrow);
            chip.Children.Add(_delta);
            _compare = Rise(Swapped(string.Empty, ArcDesignTokenKind.TextMuted, 12));
            _compare.VerticalAlignment = VerticalAlignment.Center;
            var deltaRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
            deltaRow.Children.Add(chip);
            deltaRow.Children.Add(_compare);
            _sparkline = new Sparkline
            {
                DrawingWidth = 240,
                DrawingHeight = 28,
                // Its readout row stays empty at rest; pulling it up leaves the line 12 under the delta, as uiarc.dev's
                // absolutely placed readout does.
                Margin = new Thickness(0, -20, 0, 0),
                ValueFormatter = context => metric.Format(context.Value)
            };

            var body = new StackPanel();
            body.Children.Add(label);
            body.Children.Add(_counter);
            body.Children.Add(deltaRow);
            body.Children.Add(_sparkline);
            Root = new Border { Child = body, Padding = new Thickness(20, 16) };
            ArcTokens.Bind(Root, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        }

        public Border Root { get; }

        public void SetCompact(bool compact)
        {
            Root.Padding = compact ? new Thickness(16, 14) : new Thickness(20, 16);
            _compare.IsVisible = !compact;
        }

        public void Show(OverviewRange range)
        {
            if (_rangeId == range.Id)
            {
                return;
            }
            _rangeId = range.Id;
            var reading = _metric.Readings[range.Id];
            _counter.Value = reading.Value;
            _arrow.Data = reading.Rising ? ArcIcons.ArrowUpRight : s_arrowDownRight;
            _delta.Text = reading.Delta;
            var tone = reading.Good ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Danger;
            _arrowColor.Set(tone);
            _deltaColor.Set(tone);
            _compare.Content = Swapped(range.Short, ArcDesignTokenKind.TextMuted, 12);
            _sparkline.ItemsSource = reading.Series;
            _sparkline.PointLabels = range.PointLabels;
            var name = $"{_metric.Label}, {range.Span.ToLowerInvariant()}";
            Avalonia.Automation.AutomationProperties.SetName(_sparkline, name);
            Avalonia.Automation.AutomationProperties.SetName(Root,
                $"{_metric.Label}: {_metric.Format(reading.Value)}, {reading.Delta}{(reading.Good ? ", an improvement" : ", a decline")} {range.Compare}");
        }
    }

    private static readonly Geometry s_arrowDownRight = ArcIcons.Parse("M7 7l10 10 M17 7v10H7");

    #region Insight

    /// <summary>The growth MetricExplorer (uiarc.dev renders the block with its demo defaults: Parcelwise's growth metrics).</summary>
    private static Control BuildInsight()
    {
        var explorer = new MetricExplorer
        {
            Title = "Growth metrics",
            Caption = "Parcelwise · updated Sep 22, 2026 at 09:00 UTC",
            FlatText = "flat"
        };
        foreach (var metric in OverviewData.Growth())
        {
            explorer.Metrics.Add(metric);
        }
        return explorer;
    }

    #endregion
}
