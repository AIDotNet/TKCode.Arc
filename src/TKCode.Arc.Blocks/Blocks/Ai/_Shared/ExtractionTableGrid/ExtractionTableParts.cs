using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// The columns of one line of an <see cref="ExtractionTableGrid"/> (the header or a row): each child takes its column's
/// width from <see cref="Widths"/> (NaN = an equal share of what the fixed columns leave), children in column order. Every
/// line of a table gets the same array, so the columns line up without a shared size scope; the line asks for the fixed
/// widths only (the table's minimum width covers the rest).
/// </summary>
internal sealed class ExtractionTableRowPanel : Panel
{
    private double[] _widths = [];

    /// <summary>The column widths; NaN shares the rest equally.</summary>
    public double[] Widths
    {
        get => _widths;
        set
        {
            _widths = value;
            InvalidateMeasure();
        }
    }

    /// <summary>The widths of the columns for a line <paramref name="width"/> wide.</summary>
    public static double[] Resolve(double[] widths, double width)
    {
        var fixedSum = 0.0;
        var stars = 0;
        foreach (var w in widths)
        {
            if (double.IsNaN(w))
            {
                stars++;
            }
            else
            {
                fixedSum += w;
            }
        }
        var share = stars == 0 ? 0 : Math.Max(0, width - fixedSum) / stars;
        var resolved = new double[widths.Length];
        for (var i = 0; i < widths.Length; i++)
        {
            resolved[i] = double.IsNaN(widths[i]) ? share : widths[i];
        }
        return resolved;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var widths = Resolve(_widths, double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width);
        var height = 0.0;
        var fixedSum = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var w = i < widths.Length ? widths[i] : 0;
            Children[i].Measure(new Size(w, availableSize.Height));
            height = Math.Max(height, Children[i].DesiredSize.Height);
            if (i < _widths.Length && !double.IsNaN(_widths[i]))
            {
                fixedSum += _widths[i];
            }
        }
        return new Size(fixedSum, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = Resolve(_widths, finalSize.Width);
        var x = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var w = i < widths.Length ? widths[i] : 0;
            Children[i].Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w;
        }
        return finalSize;
    }
}

/// <summary>
/// A data row of an <see cref="ExtractionTableGrid"/>: 32 high, with a 1px BorderSubtle line above from the second row on
/// (<c>:separated</c>, 33 high). A lit row (its path linked) takes its group color at 12%; the theme fades fills in 160ms.
/// </summary>
[PseudoClasses(SeparatedPseudoClass)]
internal sealed class ExtractionTableRow : Border
{
    public const string SeparatedPseudoClass = ":separated";

    public ExtractionTableRow()
    {
        Child = Panel;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.DataItem);
    }

    protected override Type StyleKeyOverride => typeof(ExtractionTableRow);

    /// <summary>The cells' panel.</summary>
    public ExtractionTableRowPanel Panel { get; } = new();

    /// <summary>The cells, in column order.</summary>
    public List<ExtractionTableCell> Cells { get; } = new();

    /// <summary>The row's values (the owner's list).</summary>
    public IList<ExtractionValue>? Values { get; set; }

    /// <summary>Shows the separator above the row (every row but the first).</summary>
    public void SetSeparated(bool separated) => PseudoClasses.Set(SeparatedPseudoClass, separated);
}

/// <summary>
/// One cell of an <see cref="ExtractionTableGrid"/>: padding 2 around a 28px <see cref="ExtractionInput"/> (a lit cell takes
/// its group color at 12%). A value that needs a check carries the model's note as its tooltip and in the box's help text
/// (a table has no reason line).
/// </summary>
internal sealed class ExtractionTableCell : Border
{
    private ExtractionValue? _observed;

    public ExtractionTableCell(ExtractionColumn column, bool numeric)
    {
        Column = column;
        Input = new ExtractionInput { IsCell = true, IsNumeric = numeric };
        Child = Input;
    }

    protected override Type StyleKeyOverride => typeof(ExtractionTableCell);

    /// <summary>The column.</summary>
    public ExtractionColumn Column { get; }

    /// <summary>The value box.</summary>
    public ExtractionInput Input { get; }

    /// <summary>The value (the owner's).</summary>
    public ExtractionValue? Value
    {
        get => Input.Value;
        set
        {
            Input.Value = value;
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Observe(value);
            }
            UpdateNote();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Value);
        UpdateNote();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    private void Observe(ExtractionValue? value)
    {
        if (ReferenceEquals(_observed, value))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnValuePropertyChanged;
        }
        _observed = value;
        if (_observed is not null)
        {
            _observed.PropertyChanged += OnValuePropertyChanged;
        }
    }

    private void OnValuePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ExtractionValue.NeedsCheckProperty || e.Property == ExtractionValue.NoteProperty)
        {
            UpdateNote();
        }
    }

    /// <summary>The note of a value that needs a check: the tooltip and the help text.</summary>
    private void UpdateNote()
    {
        var note = Value is { NeedsCheck: true, Note: { Length: > 0 } text } ? text : null;
        Tooltip.SetTip(this, note);
        Input.Description = note;
    }
}

/// <summary>A column header of an <see cref="ExtractionTableGrid"/>: 12 TextMuted, padding 0 9, numbers right-aligned.</summary>
internal sealed class ExtractionTableHeaderCell : TextBlock
{
    public ExtractionTableHeaderCell(string text, bool numeric)
    {
        Text = text;
        TextAlignment = numeric ? Avalonia.Media.TextAlignment.End : Avalonia.Media.TextAlignment.Start;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.HeaderItem);
    }

    protected override Type StyleKeyOverride => typeof(ExtractionTableHeaderCell);
}
