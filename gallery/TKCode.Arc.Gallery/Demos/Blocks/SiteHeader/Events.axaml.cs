using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SiteHeaderEventsDemo : UserControl
{
    private readonly ObservableCollection<string> _lines = new();
    private readonly SiteHeader _header = new() { Current = "product" };

    public SiteHeaderEventsDemo()
    {
        InitializeComponent();
        _header.ItemsSource = SiteHeaderDemoData.StudioItems();
        Log.ItemsSource = _lines;
        // Handled marks the destination as dealt with: nothing is launched.
        _header.Navigate += (_, e) =>
        {
            _lines.Insert(0, $"{e.Kind}: {e.Label}{(e.Section is null ? "" : $" (in {e.Section})")}");
            while (_lines.Count > 5)
            {
                _lines.RemoveAt(_lines.Count - 1);
            }
            e.Handled = true;
        };
        _header.PropertyChanged += (_, e) =>
        {
            if (e.Property == SiteHeader.CurrentProperty || e.Property == SiteHeader.OpenItemValueProperty
                || e.Property == SiteHeader.IsMenuOpenProperty || e.Property == SiteHeader.IsScrolledProperty)
            {
                ShowState();
            }
        };
        FrameHost.Child = SiteHeaderDemoData.CreateFrame(_header, 420, out _);
        ShowState();
    }

    private void ShowState()
    {
        State.Text = $"Current: {_header.Current ?? "none"}   Panel: {_header.OpenItemValue ?? "closed"}   " +
                     $"Menu: {(_header.IsMenuOpen ? "open" : "closed")}   Scrolled: {_header.IsScrolled}";
    }
}
