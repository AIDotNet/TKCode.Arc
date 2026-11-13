using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>The printed sheet of the preview.</summary>
internal sealed class InvoiceStudioPaper : Border
{
    protected override Type StyleKeyOverride => typeof(InvoiceStudioPaper);
}

/// <summary>Text on the sheet (classes muted, strong, heading, total, number).</summary>
internal sealed class InvoiceStudioInk : TextBlock
{
    protected override Type StyleKeyOverride => typeof(InvoiceStudioInk);
}

/// <summary>A horizontal rule on the sheet.</summary>
internal sealed class InvoiceStudioRule : Border
{
    protected override Type StyleKeyOverride => typeof(InvoiceStudioRule);
}

/// <summary>One editable line of the form; it lays its fields out in one row, or in two when the block is narrow.</summary>
internal sealed class InvoiceStudioLineRow : Border
{
    private readonly Grid _grid = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly Control[] _cells;

    public InvoiceStudioLineRow(InvoiceLineItem line, Control description, Control quantity, Control price, Control tax, Control amount, Control remove)
    {
        Line = line;
        Description = description;
        Quantity = quantity;
        Price = price;
        Tax = tax;
        AmountText = amount;
        Remove = remove;
        _cells = [description, quantity, price, tax, amount, remove];
        foreach (var cell in _cells)
        {
            _grid.Children.Add(cell);
        }
        Child = _grid;
        Arrange(narrow: false);
    }

    protected override Type StyleKeyOverride => typeof(InvoiceStudioLineRow);

    public InvoiceLineItem Line { get; }
    public Control Description { get; }
    public Control Quantity { get; }
    public Control Price { get; }
    public Control Tax { get; }
    public Control AmountText { get; }
    public Control Remove { get; }

    /// <summary>The columns of a wide row (description, qty, price, tax, amount, remove); the header uses the same.</summary>
    public const string WideColumns = "*,76,112,104,92,36";

    /// <summary>Wide: one row. Narrow: the description on its own row, the numbers under it.</summary>
    public void Arrange(bool narrow)
    {
        _grid.ColumnDefinitions = new ColumnDefinitions(narrow ? "76,*,104,Auto,36" : WideColumns);
        _grid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
        if (narrow)
        {
            Place(Description, 0, 0, 5);
            Place(Quantity, 1, 0);
            Place(Price, 1, 1);
            Place(Tax, 1, 2);
            Place(AmountText, 1, 3);
            Place(Remove, 1, 4);
        }
        else
        {
            for (var i = 0; i < _cells.Length; i++)
            {
                Place(_cells[i], 0, i);
            }
        }
    }

    private static void Place(Control control, int row, int column, int span = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
    }
}
