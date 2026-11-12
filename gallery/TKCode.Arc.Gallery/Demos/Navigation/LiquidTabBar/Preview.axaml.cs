using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LiquidTabBarPreviewDemo : UserControl
{
    public LiquidTabBarPreviewDemo()
    {
        InitializeComponent();
        Section.Content = PanelText(Bar.SelectedValue);
        Bar.SelectionChanged += (_, _) => Section.Content = PanelText(Bar.SelectedValue);
        Mode.SelectionChanged += (_, _) => Bar.Labels = Mode.SelectedValue switch
        {
            "Always" => LiquidTabBarLabels.Always,
            "None" => LiquidTabBarLabels.None,
            _ => LiquidTabBarLabels.Active
        };
    }

    // The section text under the bar: one line of placeholder text per tab.
    private static string PanelText(object? section) => section switch
    {
        "likes" => "14 posts you liked",
        "alerts" => "3 alerts since yesterday",
        "account" => "Signed in on this device",
        _ => "New from people you follow"
    };
}
