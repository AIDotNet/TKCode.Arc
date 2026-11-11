using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using System.Collections.Specialized;

namespace TKCode.Arc;

/// <summary>
/// The items panel of <see cref="SettingsGroup"/>: rows in lines of equal-width columns, each row spanning
/// <see cref="SettingsGroup.ColumnSpanProperty"/> columns, every row of a line as tall as the tallest. With one column it
/// measures and arranges like a vertical <see cref="StackPanel"/>. The settings (columns, spacing) are read from the group
/// that hosts the panel; without one it is a single column. Rows in the first line are told so, to drop their hairline.
/// </summary>
internal sealed class SettingsGroupPanel : Panel
{
    private readonly List<Slot> _slots = new();
    private readonly List<double> _lineHeights = new();
    private SettingsGroup? _owner;

    static SettingsGroupPanel()
    {
        AffectsParentMeasure<SettingsGroupPanel>(SettingsGroup.ColumnSpanProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = this.FindAncestorOfType<SettingsGroup>();
        InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner = null;
    }

    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        if (e.OldItems is null)
        {
            return;
        }
        foreach (var item in e.OldItems)
        {
            if (item is Control control && RowOf(control) is { } row)
            {
                row.SetFirstLine(false);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // A generated row sits in a ContentPresenter that builds it only in its own measure. Build it now so the span and
        // first-line checks below see the row; otherwise the first ItemsSource row keeps its hairline (issue #106).
        foreach (var child in Children)
        {
            if (child is ContentPresenter { Child: null, Content: not null } presenter)
            {
                presenter.UpdateChild();
            }
        }
        var width = availableSize.Width;
        var columns = ResolveColumns(width);
        _owner?.SetActualColumns(columns);
        var columnSpacing = columns > 1 ? _owner?.ColumnSpacing ?? 0 : 0;
        Place(columns);
        // Before the rows are measured, so a hairline that comes or goes is part of this pass.
        foreach (var slot in _slots)
        {
            if (RowOf(slot.Child) is { } row)
            {
                row.SetFirstLine(slot.Line == 0);
            }
        }

        double desiredWidth;
        if (double.IsInfinity(width))
        {
            // No width to share: each row takes what it needs, and a column is as wide as its widest single-span row.
            var unit = 0.0;
            var widest = 0.0;
            foreach (var slot in _slots)
            {
                slot.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var w = slot.Child.DesiredSize.Width;
                widest = Math.Max(widest, w);
                if (slot.Span == 1)
                {
                    unit = Math.Max(unit, w);
                }
            }
            desiredWidth = columns == 1 ? widest : Math.Max(widest, unit * columns + columnSpacing * (columns - 1));
        }
        else
        {
            var column = ColumnWidth(width, columns, columnSpacing);
            var widest = 0.0;
            foreach (var slot in _slots)
            {
                slot.Child.Measure(new Size(SpanWidth(column, slot.Span, columnSpacing), double.PositiveInfinity));
                widest = Math.Max(widest, slot.Child.DesiredSize.Width);
            }
            // One column reports its widest row, like a vertical StackPanel; more columns fill the width they share.
            desiredWidth = columns == 1 ? widest : width;
        }

        return new Size(desiredWidth, MeasureLines());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ResolveColumns(finalSize.Width);
        var columnSpacing = columns > 1 ? _owner?.ColumnSpacing ?? 0 : 0;
        var rowSpacing = _owner?.RowSpacing ?? 0;
        Place(columns);
        MeasureLines();
        var column = ColumnWidth(finalSize.Width, columns, columnSpacing);
        var y = 0.0;
        var line = 0;
        foreach (var slot in _slots)
        {
            while (line < slot.Line)
            {
                y += _lineHeights[line] + rowSpacing;
                line++;
            }
            var x = slot.Column * (column + columnSpacing);
            // One column arranges like a vertical StackPanel: never narrower than the row asked for.
            var w = columns == 1
                ? Math.Max(finalSize.Width, slot.Child.DesiredSize.Width)
                : SpanWidth(column, slot.Span, columnSpacing);
            slot.Child.Arrange(new Rect(x, y, w, _lineHeights[slot.Line]));
        }
        return finalSize;
    }

    /// <summary>The number of columns at <paramref name="width"/>: <see cref="SettingsGroup.Columns"/>, reduced to what fits at <see cref="SettingsGroup.MinColumnWidth"/>.</summary>
    private int ResolveColumns(double width)
    {
        if (_owner is null)
        {
            return 1;
        }
        var columns = _owner.Columns;
        var min = _owner.MinColumnWidth;
        if (min <= 0 || double.IsInfinity(width) || double.IsNaN(width))
        {
            return Math.Max(1, columns);
        }
        var spacing = _owner.ColumnSpacing;
        var fit = Math.Max(1, (int)Math.Min(int.MaxValue, Math.Floor((width + spacing) / (min + spacing))));
        if (columns == 0)
        {
            var rows = 0;
            foreach (var child in Children)
            {
                if (child.IsVisible)
                {
                    rows++;
                }
            }
            return Math.Max(1, Math.Min(fit, rows));
        }
        return Math.Min(columns, fit);
    }

    /// <summary>Assigns each visible child a line, a first column and a span; a row that does not fit starts the next line.</summary>
    private void Place(int columns)
    {
        _slots.Clear();
        var line = 0;
        var column = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var span = SpanOf(child, columns);
            if (column > 0 && column + span > columns)
            {
                line++;
                column = 0;
            }
            _slots.Add(new Slot(child, line, column, span));
            column += span;
            if (column >= columns)
            {
                line++;
                column = 0;
            }
        }
    }

    /// <summary>Fills <see cref="_lineHeights"/> from the measured rows and returns the total height with the row spacing.</summary>
    private double MeasureLines()
    {
        _lineHeights.Clear();
        foreach (var slot in _slots)
        {
            while (_lineHeights.Count <= slot.Line)
            {
                _lineHeights.Add(0);
            }
            _lineHeights[slot.Line] = Math.Max(_lineHeights[slot.Line], slot.Child.DesiredSize.Height);
        }
        var total = 0.0;
        foreach (var height in _lineHeights)
        {
            total += height;
        }
        if (_lineHeights.Count > 1)
        {
            total += (_owner?.RowSpacing ?? 0) * (_lineHeights.Count - 1);
        }
        return total;
    }

    private static int SpanOf(Control child, int columns)
    {
        var span = SettingsGroup.GetColumnSpan(child);
        if (span == 1 && child is ContentPresenter { Child: { } inner })
        {
            span = SettingsGroup.GetColumnSpan(inner);
        }
        return span <= 0 || span > columns ? columns : span;
    }

    private static double ColumnWidth(double width, int columns, double spacing) =>
        Math.Max(0, (width - spacing * (columns - 1)) / columns);

    private static double SpanWidth(double column, int span, double spacing) => column * span + spacing * (span - 1);

    private static SettingsRow? RowOf(Control child) =>
        child as SettingsRow ?? (child as ContentPresenter)?.Child as SettingsRow;

    private readonly record struct Slot(Control Child, int Line, int Column, int Span);
}
