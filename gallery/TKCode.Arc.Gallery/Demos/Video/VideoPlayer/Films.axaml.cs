using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Switches the player between two animated trailers.</summary>
public partial class VideoPlayerFilmsDemo : UserControl
{
    public VideoPlayerFilmsDemo() => InitializeComponent();

    private void OnFilmChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control also reports its first selection while the XAML loads, before the named controls are set.
        if (Player is null || Credit is null)
        {
            return;
        }
        var bunny = Film.SelectedValue as string == "bunny";
        // The player keeps playing across a new source; here the new film starts from its poster.
        Player.Pause();
        Player.Title = bunny ? "Big Buck Bunny" : "Sintel";
        Player.Poster = bunny ? VideoDemoMedia.BigBuckBunnyPoster : VideoDemoMedia.SintelPoster;
        Player.Source = bunny ? VideoDemoMedia.BigBuckBunny : VideoDemoMedia.Sintel;
        Credit.Text = bunny
            ? "Big Buck Bunny trailer © Blender Foundation, peach.blender.org, CC BY 3.0"
            : "Sintel trailer © Blender Foundation, durian.blender.org, CC BY 3.0";
    }
}
