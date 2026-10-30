using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// One spec or price of <see cref="ModelCard"/> (a description list entry): a label and its value, plus an optional extra
/// under the value (the context gauge and page count). On wide cards it is a tile with a hairline outline; under the narrow
/// breakpoint it becomes a row with the label on the left and the value on the right, under a hairline.
/// </summary>
internal sealed class ModelCardSpecCell : Border
{
    private readonly Grid _grid;
    private readonly TextBlock _label;
    private readonly Control _value;
    private readonly Control? _extra;
    private readonly List<IDisposable> _look = [];

    public ModelCardSpecCell(Control value, Control? extra = null)
    {
        _value = value;
        _extra = extra;
        _label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }
            .Tok(TextBlock.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Tok(TextBlock.LineHeightProperty, ModelCardTokenKind.LabelLineHeight)
            .Arc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _grid = new Grid();
        _grid.Children.Add(_label);
        _grid.Children.Add(value);
        if (extra is not null)
        {
            _grid.Children.Add(extra);
        }
        Child = _grid;
        this.Arc(BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        SetStacked(false);
    }

    /// <summary>The label on screen.</summary>
    public string Label => _label.Text ?? string.Empty;

    /// <summary>True when the cell is a row (narrow cards).</summary>
    public bool IsStacked { get; private set; }

    public void SetLabel(string label)
    {
        _label.Text = label;
        AutomationProperties.SetName(_label, label);
    }

    /// <summary>A tile (wide) or a label and value row under a hairline (narrow).</summary>
    public void SetStacked(bool stacked)
    {
        IsStacked = stacked;
        foreach (var binding in _look)
        {
            binding.Dispose();
        }
        _look.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        if (stacked)
        {
            _look.Add(ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop));
            CornerRadius = default;
            Padding = default;
            _look.Add(Bind(MinHeightProperty, this.GetResourceObservable(ModelCardTokens.Key(ModelCardTokenKind.StackedRowHeight))));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Place(_label, 0, 0);
            Place(_value, 0, 1);
            _value.VerticalAlignment = VerticalAlignment.Center;
            _value.HorizontalAlignment = HorizontalAlignment.Right;
            _value.Margin = new Thickness(12, 0, 0, 0);
            if (_extra is not null)
            {
                Place(_extra, 1, 0);
                Grid.SetColumnSpan(_extra, 2);
                _extra.Margin = new Thickness(0, 0, 0, 8);
            }
        }
        else
        {
            _look.Add(ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness));
            _look.Add(ArcTokens.Bind(this, CornerRadiusProperty, ArcDesignTokenKind.RadiusStep12));
            _look.Add(Bind(PaddingProperty, this.GetResourceObservable(ModelCardTokens.Key(ModelCardTokenKind.CellPadding))));
            ClearValue(MinHeightProperty);
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Place(_label, 0, 0);
            Place(_value, 1, 0);
            _value.VerticalAlignment = VerticalAlignment.Top;
            _value.HorizontalAlignment = HorizontalAlignment.Stretch;
            _value.Margin = new Thickness(0, 2, 0, 0);
            if (_extra is not null)
            {
                Place(_extra, 2, 0);
                Grid.SetColumnSpan(_extra, 1);
                _extra.Margin = new Thickness(0, 8, 0, 0);
            }
        }
    }

    private static void Place(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
    }
}
