using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

public partial class UndoToastPreviewDemo : UserControl
{
    private static readonly string[] s_threads = ["Venue options for the spring offsite", "Invoice 2291 from Harbor Paper Co.", "Design review notes"];

    public UndoToastPreviewDemo()
    {
        InitializeComponent();
        foreach (var thread in s_threads)
        {
            List.Children.Add(Row(thread));
        }
    }

    private Control Row(string title)
    {
        var archive = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = "Archive" };
        var row = new Border
        {
            Padding = new Avalonia.Thickness(14, 8),
            Child = new DockPanel
            {
                Children =
                {
                    archive,
                    new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }
                }
            }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        DockPanel.SetDock(archive, Dock.Right);
        ArcTokens.Bind(row, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        archive.Click += async (_, _) =>
        {
            var index = List.Children.IndexOf(row);
            List.Children.Remove(row);
            // The toast answers true when the person undid the action.
            if (await Toast.ShowAsync("Conversation archived"))
            {
                List.Children.Insert(Math.Min(index, List.Children.Count), row);
            }
        };
        return row;
    }
}
