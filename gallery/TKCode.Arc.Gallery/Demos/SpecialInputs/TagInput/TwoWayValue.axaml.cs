using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TagInputTwoWayValueDemo : UserControl
{
    public TagInputTwoWayValueDemo()
    {
        InitializeComponent();
        Labels.ValueChanged += OnValueChanged;
    }

    private void OnValueChanged(object? sender, TagInputValueChangedEventArgs e)
    {
        ValueText.Text = "Value: " + (e.NewValue.Count == 0 ? "(empty)" : string.Join(", ", e.NewValue));
        var parts = new List<string>(2);
        if (e.Added.Count > 0)
        {
            parts.Add("added " + string.Join(", ", e.Added));
        }
        if (e.Removed.Count > 0)
        {
            parts.Add("removed " + string.Join(", ", e.Removed));
        }
        LastEvent.Text = "Last event: " + string.Join("; ", parts);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Labels.Value = ["bug", "ui"];
    }
}
