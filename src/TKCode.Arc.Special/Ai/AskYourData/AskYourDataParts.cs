using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The Generated SQL row of an <see cref="AskYourData"/>: the whole row is one button that opens or closes the SQL, filled
/// with SurfaceMuted under the pointer, with a keyboard ring inside its edge. Screen readers get its expanded state.
/// </summary>
internal sealed class AskYourDataSqlToggle : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(AskYourDataSqlToggle);
}

/// <summary>
/// The answer as a table: a header row (the category column, a column per series and a Total column when the series add
/// up), then a row per category with right-aligned tabular numbers over hairlines. Wide tables scroll inside their own
/// well. Rows are built when the result changes, never per frame.
/// </summary>
[TemplatePart(GridPart, typeof(Grid))]
internal sealed class AskYourDataTable : TemplatedControl
{
    internal const string GridPart = "PART_Grid";

    /// <summary>The result shown.</summary>
    public static readonly StyledProperty<AskYourDataResult?> ResultProperty =
        AvaloniaProperty.Register<AskYourDataTable, AskYourDataResult?>(nameof(Result));

    /// <summary>The words of the headers.</summary>
    public static readonly StyledProperty<AskYourDataLabels> LabelsProperty =
        AvaloniaProperty.Register<AskYourDataTable, AskYourDataLabels>(nameof(Labels), AskYourDataLabels.Default);

    private Grid? _grid;

    /// <inheritdoc cref="ResultProperty"/>
    public AskYourDataResult? Result { get => GetValue(ResultProperty); set => SetValue(ResultProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AskYourDataLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The cells' grid (tests).</summary>
    internal Grid? CellGrid => _grid;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grid = e.NameScope.Find<Grid>(GridPart);
        Build();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ResultProperty || change.Property == LabelsProperty)
        {
            Build();
        }
    }

    private void Build()
    {
        if (_grid is not { } grid)
        {
            return;
        }
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        if (Result is not { HasData: true } result)
        {
            return;
        }
        var format = result.FormatValue ?? AskYourDataFormat.Value;
        var totals = result.Stacked && result.Series.Count > 1;
        var columns = 1 + result.Series.Count + (totals ? 1 : 0);
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)) { MinWidth = 72 });
        for (var c = 1; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { MinWidth = 64 });
        }
        var rows = result.Categories.Count + 1;
        for (var r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(30)));
        }
        AddCell(grid, 0, 0, result.CategoryLabel ?? Labels.Category, header: true, number: false);
        for (var s = 0; s < result.Series.Count; s++)
        {
            AddCell(grid, 0, 1 + s, result.Series[s].Label, header: true, number: true);
        }
        if (totals)
        {
            AddCell(grid, 0, columns - 1, Labels.Total, header: true, number: true);
        }
        for (var i = 0; i < result.Categories.Count; i++)
        {
            var row = i + 1;
            AddCell(grid, row, 0, result.Categories[i], header: false, number: false, rowHeader: true);
            for (var s = 0; s < result.Series.Count; s++)
            {
                AddCell(grid, row, 1 + s, format(AskYourDataFormat.ValueAt(result.Series[s], i)), header: false, number: true);
            }
            if (totals)
            {
                AddCell(grid, row, columns - 1, format(AskYourDataFormat.TotalAt(result, i)), header: false, number: true, strong: true);
            }
        }
        for (var r = 0; r < rows; r++)
        {
            var line = new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false,
            };
            ArcTokens.Bind(line, Border.BackgroundProperty, r == 0 ? ArcDesignTokenKind.Border : ArcDesignTokenKind.BorderSubtle);
            Grid.SetRow(line, r);
            Grid.SetColumnSpan(line, columns);
            grid.Children.Add(line);
        }
    }

    private static void AddCell(Grid grid, int row, int column, string text, bool header, bool number, bool rowHeader = false, bool strong = false)
    {
        var cell = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = number ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(column == 0 ? 2 : 12, 0, 2, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFeatures = number ? new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } } : null,
        };
        if (header || strong)
        {
            ArcTokens.Bind(cell, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        ArcTokens.Bind(cell, TextBlock.ForegroundProperty,
            header ? ArcDesignTokenKind.TextMuted : rowHeader ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
        AutomationProperties.SetName(cell, text);
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}

/// <summary>
/// The placeholder of the stage while the SQL is written and the query runs: a row of faint bars that breathe (opacity
/// 0.45 to 1 and back every 1.6s on a sine). Driven by a Core <see cref="LiveLoop"/>, so it pauses off screen, when hidden
/// and with reduced motion (then it rests at full strength).
/// </summary>
internal sealed class AskYourDataSkeleton : Control
{
    /// <summary>The bars' fill (theme: SurfaceMuted).</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AskYourDataSkeleton, IBrush?>(nameof(Fill));

    /// <summary>One breath.</summary>
    internal const double BreathSeconds = 1.6;

    private static readonly double[] s_heights = [0.42, 0.55, 0.5, 0.64, 0.7, 0.6, 0.74, 0.8, 0.72, 0.86, 0.9, 0.96];

    private readonly LiveLoop _loop;
    private double _phase;

    static AskYourDataSkeleton()
    {
        AffectsRender<AskYourDataSkeleton>(FillProperty);
    }

    public AskYourDataSkeleton()
    {
        _loop = new LiveLoop(this, dt =>
        {
            _phase = (_phase + dt / BreathSeconds) % 1;
            InvalidateVisual();
            return true;
        });
        _loop.RunningChanged += _ => InvalidateVisual();
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>Whether it is breathing now.</summary>
    internal bool IsBreathing => _loop.IsRunning;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loop.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (Fill is not { } fill || Bounds.Width <= 0)
        {
            return;
        }
        var opacity = _loop.IsRunning ? 0.725 + 0.275 * Math.Cos(_phase * 2 * Math.PI) : 1;
        using (context.PushOpacity(opacity))
        {
            var n = s_heights.Length;
            var band = Bounds.Width / n;
            var width = Math.Min(band * 0.6, 40);
            var bottom = Bounds.Height - 22;
            for (var i = 0; i < n; i++)
            {
                var h = (bottom - 8) * s_heights[i];
                context.DrawRectangle(fill, null, new Rect(band * (i + 0.5) - width / 2, bottom - h, width, h), 3, 3);
            }
        }
    }
}
