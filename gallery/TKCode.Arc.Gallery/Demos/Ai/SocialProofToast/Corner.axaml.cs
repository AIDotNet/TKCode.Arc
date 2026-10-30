using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The stack inline or pinned to a window corner.</summary>
public partial class SocialProofToastCornerDemo : UserControl
{
    private readonly bool _ready;

    public SocialProofToastCornerDemo()
    {
        InitializeComponent();
        Placement.SelectionChanged += OnPlacementChanged;
        _ready = true;
    }

    private void OnPlacementChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SegmentedControl reports its first selection while the XAML loads.
        if (!_ready)
        {
            return;
        }
        Stack.Position = (Placement.SelectedValue as string) switch
        {
            "BottomLeft" => SocialProofToastPosition.BottomLeft,
            "BottomRight" => SocialProofToastPosition.BottomRight,
            "TopRight" => SocialProofToastPosition.TopRight,
            _ => SocialProofToastPosition.Inline,
        };
    }
}
