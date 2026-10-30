using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// One benchmark row of <see cref="ModelCard"/>: a color dot, the model's name, a bar and the score. The row is focusable
/// and named by its full readout (score, rank and gap to the leader); the bar itself is hidden from assistive technology.
/// Hovering or focusing tints it while the other rows fade to half opacity (160ms, in the theme). Under the tiny breakpoint
/// the bar moves under the name.
/// </summary>
[PseudoClasses(HighlightedPseudoClass, DimmedPseudoClass, EmptyPseudoClass)]
internal sealed class ModelCardBenchmarkRow : ContentControl
{
    internal const string HighlightedPseudoClass = ":highlighted";
    internal const string DimmedPseudoClass = ":dimmed";
    internal const string EmptyPseudoClass = ":empty";

    private readonly Grid _grid;
    private readonly Ellipse _dot;
    private readonly ModelCardText _name;
    private readonly IDisposable[] _fill = new IDisposable[2];
    private bool _stacked;

    public ModelCardBenchmarkRow()
    {
        _dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        _name = new ModelCardText { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) }
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.HeadingFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.HeadingLineHeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        Bar = new ModelCardBar { VerticalAlignment = VerticalAlignment.Center }
            .Tok(ModelCardBar.BarHeightProperty, ModelCardTokenKind.BarHeight)
            .Arc(ModelCardBar.TrackBrushProperty, ArcDesignTokenKind.SurfaceMuted);
        AutomationProperties.SetAccessibilityView(Bar, AccessibilityView.Raw);
        Score = new ModelCardNumber { VerticalAlignment = VerticalAlignment.Center, Alignment = HorizontalAlignment.Right }
            .Tok(ModelCardNumber.FontSizeProperty, ModelCardTokenKind.HeadingFontSize)
            .Arc(ModelCardNumber.ForegroundProperty, ArcDesignTokenKind.Foreground)
            .Arc(ModelCardNumber.PlaceholderBrushProperty, ArcDesignTokenKind.TextMuted)
            .Tok(WidthProperty, ModelCardTokenKind.BenchmarkScoreWidth);
        Score.FontWeight = FontWeight.Medium;
        AutomationProperties.SetAccessibilityView(Score, AccessibilityView.Raw);
        _grid = new Grid();
        _grid.Children.Add(_dot);
        _grid.Children.Add(_name);
        _grid.Children.Add(Bar);
        _grid.Children.Add(Score);
        Content = _grid;
        Focusable = true;
        SetStacked(false);
        SetOwn(false);
    }

    /// <summary>The bar.</summary>
    public ModelCardBar Bar { get; }

    /// <summary>The score.</summary>
    public ModelCardNumber Score { get; }

    /// <summary>The model's name on screen.</summary>
    public string ModelName => _name.Text;

    /// <summary>The readout screen readers hear and the tooltip shows.</summary>
    public string Readout { get; private set; } = string.Empty;

    /// <summary>True when the bar sits under the name.</summary>
    public bool IsStacked => _stacked;

    /// <summary>The row of the card's own model draws its dot and bar in the accent; rivals use muted ink.</summary>
    public void SetOwn(bool own)
    {
        foreach (var binding in _fill)
        {
            binding?.Dispose();
        }
        var kind = own ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.TextMuted;
        _fill[0] = ArcTokens.Bind(Bar, ModelCardBar.FillBrushProperty, kind);
        _fill[1] = ArcTokens.Bind(_dot, Shape.FillProperty, kind);
    }

    /// <summary>Shows a model's name and readout (the score, bar and color are set by the card).</summary>
    public void SetModel(string name, string readout, bool animate)
    {
        _name.Show(name, animate);
        Readout = readout;
        AutomationProperties.SetName(this, readout);
        Tooltip.SetTip(this, readout.Length > 0 ? readout : null);
    }

    /// <summary>Lays the row out on one line, or with the bar under the name.</summary>
    public void SetStacked(bool stacked)
    {
        _stacked = stacked;
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        if (stacked)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Place(_dot, 0, 0, 1);
            Place(_name, 0, 1, 1);
            Place(Score, 0, 2, 1);
            Place(Bar, 1, 0, 3);
            Bar.Margin = new Thickness(0, 6, 0, 0);
        }
        else
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Place(_dot, 0, 0, 1);
            Place(_name, 0, 1, 1);
            Place(Bar, 0, 2, 1);
            Place(Score, 0, 3, 1);
            Bar.Margin = default;
        }
        _name.Tok(WidthProperty, ModelCardTokenKind.BenchmarkNameWidth);
        if (stacked)
        {
            _name.ClearValue(WidthProperty);
        }
        this.Tok(HeightProperty, stacked ? ModelCardTokenKind.StackedBenchmarkRowHeight : ModelCardTokenKind.BenchmarkRowHeight);
    }

    private static void Place(Control control, int row, int column, int span)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
    }

    /// <summary>Sets the hover and dim states.</summary>
    public void SetEmphasis(bool highlighted, bool dimmed)
    {
        PseudoClasses.Set(HighlightedPseudoClass, highlighted);
        PseudoClasses.Set(DimmedPseudoClass, dimmed);
    }

    /// <summary>Marks a reserved row that has no model (fewer than two rivals): it stays in place, empty.</summary>
    public void SetEmpty(bool empty)
    {
        PseudoClasses.Set(EmptyPseudoClass, empty);
        Focusable = !empty;
        IsHitTestVisible = !empty;
        _grid.IsVisible = !empty;
    }
}

/// <summary>
/// One row of the compare table of <see cref="ModelCard"/>: a row header and the two models' values. The better value is
/// set in medium weight with a check, the text "better" for screen readers and, on wide cards, the margin it wins by;
/// benchmark rows also draw mini bars on wide cards. Narrower, the label sits above its two values.
/// </summary>
internal sealed class ModelCardCompareRow : Border
{
    private readonly Grid _grid;
    private readonly TextBlock _label;
    private readonly ModelCardCompareCell[] _cells = new ModelCardCompareCell[2];
    private bool _stacked;

    public ModelCardCompareRow(ModelCardCompareKind kind, bool hasBars)
    {
        Kind = kind;
        HasBars = hasBars;
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Tok(TextBlock.LineHeightProperty, ModelCardTokenKind.LabelLineHeight)
            .Arc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _cells[0] = new ModelCardCompareCell(hasBars);
        _cells[1] = new ModelCardCompareCell(hasBars);
        _grid = new Grid();
        _grid.Children.Add(_label);
        _grid.Children.Add(_cells[0]);
        _grid.Children.Add(_cells[1]);
        Child = _grid;
        this.Arc(BorderBrushProperty, ArcDesignTokenKind.BorderSubtle).Arc(BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        SetStacked(false);
    }

    /// <summary>How the row decides the better value.</summary>
    public ModelCardCompareKind Kind { get; }

    /// <summary>Whether the cells draw mini bars (benchmark rows).</summary>
    public bool HasBars { get; }

    /// <summary>The row header.</summary>
    public string Label => _label.Text ?? string.Empty;

    /// <summary>The cell of the card's model (0) or the rival (1).</summary>
    public ModelCardCompareCell Cell(int index) => _cells[index];

    public void SetLabel(string label)
    {
        _label.Text = label;
        AutomationProperties.SetName(_label, label);
    }

    /// <summary>Puts the label above the values (narrow cards) or beside them, and shows or hides margins and bars.</summary>
    public void SetStacked(bool stacked)
    {
        _stacked = stacked;
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        if (stacked)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Place(_label, 0, 0, 2);
            Place(_cells[0], 1, 0, 1);
            Place(_cells[1], 1, 1, 1);
            _label.VerticalAlignment = VerticalAlignment.Bottom;
            this.Tok(HeightProperty, ModelCardTokenKind.StackedCompareRowHeight);
        }
        else
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Place(_label, 0, 0, 1);
            Place(_cells[0], 0, 1, 1);
            Place(_cells[1], 0, 2, 1);
            _label.VerticalAlignment = VerticalAlignment.Center;
            this.Tok(HeightProperty, ModelCardTokenKind.CompareRowHeight);
        }
        _label.Tok(WidthProperty, ModelCardTokenKind.CompareLabelWidth);
        if (stacked)
        {
            _label.ClearValue(WidthProperty);
        }
        _cells[0].SetWide(!stacked);
        _cells[1].SetWide(!stacked);
    }

    private static void Place(Control control, int row, int column, int span)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
    }
}

/// <summary>A value cell of the compare table: an optional mini bar, the value, and the check with its margin when it is the better one.</summary>
internal sealed class ModelCardCompareCell : StackPanel
{
    private readonly ArcGlyph _check;
    private readonly TextBlock _margin;
    private readonly bool _hasBars;
    private bool _wide = true;

    public ModelCardCompareCell(bool hasBars)
    {
        _hasBars = hasBars;
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        VerticalAlignment = VerticalAlignment.Center;
        Bar = new ModelCardBar { Width = 36, VerticalAlignment = VerticalAlignment.Center, IsVisible = hasBars }
            .Tok(WidthProperty, ModelCardTokenKind.MiniBarWidth)
            .Arc(ModelCardBar.TrackBrushProperty, ArcDesignTokenKind.SurfaceMuted)
            .Arc(ModelCardBar.FillBrushProperty, ArcDesignTokenKind.TextMuted);
        Bar.BarHeight = 4;
        AutomationProperties.SetAccessibilityView(Bar, AccessibilityView.Raw);
        Number = new ModelCardNumber { VerticalAlignment = VerticalAlignment.Center }
            .Tok(ModelCardNumber.FontSizeProperty, ModelCardTokenKind.HeadingFontSize)
            .Arc(ModelCardNumber.ForegroundProperty, ArcDesignTokenKind.Foreground)
            .Arc(ModelCardNumber.PlaceholderBrushProperty, ArcDesignTokenKind.TextMuted);
        Text = new ModelCardText { VerticalAlignment = VerticalAlignment.Center, IsVisible = false }
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.HeadingFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.HeadingLineHeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _check = new ArcGlyph { Data = ArcIcons.Check, Size = 14, VerticalAlignment = VerticalAlignment.Center, IsVisible = false }
            .Arc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        _margin = new TextBlock { VerticalAlignment = VerticalAlignment.Center, IsVisible = false }
            .Tok(TextBlock.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Arc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        Children.Add(Bar);
        Children.Add(Number);
        Children.Add(Text);
        Children.Add(_check);
        Children.Add(_margin);
    }

    /// <summary>The mini bar (benchmark rows on wide cards).</summary>
    public ModelCardBar Bar { get; }

    /// <summary>A numeric value.</summary>
    public ModelCardNumber Number { get; }

    /// <summary>A text value (the knowledge cutoff).</summary>
    public ModelCardText Text { get; }

    /// <summary>True when this value is the better one.</summary>
    public bool IsBetter { get; private set; }

    /// <summary>The margin it wins by, or empty.</summary>
    public string MarginText => _margin.Text ?? string.Empty;

    /// <summary>Whether the margin is shown (wide cards).</summary>
    public bool ShowsMargin => _margin.IsVisible;

    /// <summary>Uses the text value instead of the number.</summary>
    public void UseText()
    {
        Number.IsVisible = false;
        Text.IsVisible = true;
    }

    /// <summary>Marks the better value: medium weight, a check, "better" for screen readers and the margin on wide cards.</summary>
    public void SetVerdict(bool better, string margin, string spoken, string betterWord)
    {
        IsBetter = better;
        var weight = better ? FontWeight.SemiBold : FontWeight.Normal;
        Number.FontWeight = weight;
        Text.FontWeight = weight;
        _check.IsVisible = better;
        _margin.Text = margin;
        _margin.IsVisible = better && _wide && margin.Length > 0;
        AutomationProperties.SetName(this, better ? spoken + ", " + betterWord : spoken);
    }

    /// <summary>Shows or hides the margin and mini bar with the card's width.</summary>
    public void SetWide(bool wide)
    {
        _wide = wide;
        Bar.IsVisible = _hasBars && wide;
        _margin.IsVisible = IsBetter && wide && (_margin.Text?.Length ?? 0) > 0;
    }
}

/// <summary>A gray block of the loading skeleton.</summary>
internal sealed class ModelCardSkeletonBlock : Border
{
    public ModelCardSkeletonBlock(double height, double widthFraction = 1)
    {
        Height = height;
        WidthFraction = widthFraction;
        HorizontalAlignment = HorizontalAlignment.Left;
        this.Arc(BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
    }

    /// <summary>The share of the available width the block takes.</summary>
    public double WidthFraction { get; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width * WidthFraction : size.Width;
        return new Size(width, Height);
    }
}
