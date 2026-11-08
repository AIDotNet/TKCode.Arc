using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FluidTabControlDynamicItemsDemo : UserControl
{
    private readonly ObservableCollection<string> _releases = new() { "Version 3.2", "Version 3.1", "Version 3.0" };
    private int _next = 3;

    public FluidTabControlDynamicItemsDemo()
    {
        InitializeComponent();
        Board.ItemsSource = _releases;
        Add.Click += (_, _) =>
        {
            _next++;
            _releases.Insert(0, $"Version 3.{_next}");
            Board.SelectedIndex = 0;
        };
        Remove.Click += (_, _) =>
        {
            if (Board.SelectedIndex >= 0)
            {
                _releases.RemoveAt(Board.SelectedIndex);
            }
        };
        MoveUp.Click += (_, _) =>
        {
            var index = Board.SelectedIndex;
            if (index > 0)
            {
                _releases.Move(index, index - 1);
                Board.SelectedIndex = index - 1;
            }
        };
    }
}
