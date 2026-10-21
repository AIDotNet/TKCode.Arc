using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SidebarDataBindingDemo : UserControl
{
    private readonly List<string> _lines = new();

    public SidebarDataBindingDemo()
    {
        InitializeComponent();
        // ItemInvoked fires for every choice, also when the page was already selected (SelectedKey would not change).
        Nav.ItemInvoked += (_, e) =>
        {
            _lines.Insert(0, $"{DateTime.Now:HH:mm:ss}  {e.Item.Label} ({e.Item.Key})");
            if (_lines.Count > 4)
            {
                _lines.RemoveAt(_lines.Count - 1);
            }
            Log.Text = string.Join(Environment.NewLine, _lines);
        };
    }
}
