using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SheetStackEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public SheetStackEventsDemo()
    {
        InitializeComponent();
    }

    private void OnStackChanged(object? sender, SheetStackChangedEventArgs e)
    {
        var stack = e.NewStack.Count == 0 ? "(closed)" : string.Join(" › ", e.NewStack);
        _lines.Insert(0, $"{e.Reason}: {stack}");
        if (_lines.Count > 8)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }

    // Pushing several levels at once: set the whole stack.
    private void OnPushChart(object? sender, RoutedEventArgs e) => Stack.Stack = ["filters", "frame", "chart"];

    private void OnPopToFilters(object? sender, RoutedEventArgs e) => Stack.PopTo("filters");

    private void OnClose(object? sender, RoutedEventArgs e) => Stack.Close();
}
