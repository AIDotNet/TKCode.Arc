using System.ComponentModel;

namespace TKCode.Arc;

/// <summary>
/// What a <see cref="DataGridColumn.EditTemplate"/> edits: the value of one cell. The template's content gets this object as
/// its <c>DataContext</c>; bind your controls to <see cref="Value"/> and call <see cref="Commit"/> or <see cref="Cancel"/>
/// (Enter, Tab and Escape do the same when the focused control leaves them alone).
/// </summary>
public sealed class DataGridEditContext : INotifyPropertyChanged
{
    private readonly DataGrid _grid;
    private object? _value;
    private string? _errorMessage;

    internal DataGridEditContext(DataGrid grid, object row, DataGridColumn column, object? value, string text)
    {
        _grid = grid;
        Row = row;
        Column = column;
        _value = value;
        Text = text;
    }

    /// <summary>Raised when <see cref="Value"/> or <see cref="ErrorMessage"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The row object (a <see cref="DataGridRow"/> or one of your own classes).</summary>
    public object Row { get; }

    /// <summary>The column being edited.</summary>
    public DataGridColumn Column { get; }

    /// <summary>The cell's display text when editing began.</summary>
    public string Text { get; }

    /// <summary>
    /// The value to write: starts as the cell's current value (a string, a <see cref="decimal"/>, a <see cref="DateOnly"/>, a
    /// <see cref="bool"/> or null). Any .NET number or date type is accepted and converted to the column's type on commit.
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    /// <summary>The message the grid announces when the commit is refused because <see cref="Value"/> does not fit the column.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
        }
    }

    /// <summary>Writes <see cref="Value"/> and closes the editor, as Enter does without moving. Returns false when the commit was refused.</summary>
    public bool Commit() => _grid.CommitEdit();

    /// <summary>Closes the editor without writing.</summary>
    public void Cancel() => _grid.CancelEdit();
}
