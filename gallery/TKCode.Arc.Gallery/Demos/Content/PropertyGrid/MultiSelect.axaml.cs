using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PropertyGridMultiSelectDemo : UserControl
{
    private readonly DemoEntity[] _drawing = PropertyGridDemoData.Drawing();

    public PropertyGridMultiSelectDemo()
    {
        InitializeComponent();
        // Like a CAD properties palette: all selected shapes, or only those of one kind.
        var kinds = new List<(string Text, DemoEntity[] Shapes)> { ($"All ({_drawing.Length})", _drawing) };
        foreach (var group in _drawing.GroupBy(shape => shape.ToString()))
        {
            kinds.Add(($"{group.Key} ({group.Count()})", group.ToArray()));
        }
        Kind.ItemsSource = kinds;
        Kind.TextSelector = item => (((string Text, DemoEntity[] Shapes))item!).Text;
        Kind.SelectionChanged += (_, _) =>
        {
            if (Kind.SelectedItem is (string, DemoEntity[] shapes))
            {
                Properties.SelectedObjects = shapes;
            }
        };
        Kind.SelectedIndex = 0;
    }
}
