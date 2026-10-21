using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class BadgeChangingInPlaceDemo : UserControl
{
    private static readonly (string Text, BadgeTone Tone, Geometry Icon)[] States =
    [
        ("Queued", BadgeTone.Info, ArcIcons.Clock),
        ("Deploying", BadgeTone.Warning, ArcIcons.LoaderCircle),
        ("Live", BadgeTone.Success, ArcIcons.Check),
        ("Rolled back", BadgeTone.Danger, ArcIcons.RotateCcw)
    ];

    private int _state;
    private int _comments = 3;

    public BadgeChangingInPlaceDemo()
    {
        InitializeComponent();
    }

    private void OnNextStateClick(object? sender, RoutedEventArgs e)
    {
        var (text, tone, icon) = States[_state = (_state + 1) % States.Length];
        Deploy.Content = text;
        Deploy.Tone = tone;
        Deploy.Icon = new ArcGlyph { Data = icon, Size = 12 };
    }

    private void OnAddCommentClick(object? sender, RoutedEventArgs e)
    {
        Comments.Content = ++_comments;
    }
}
