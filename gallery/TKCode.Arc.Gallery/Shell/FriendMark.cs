using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The picture of a site: a soft gradient of the site's hue, with its logo in a white rounded tile when the logo source
/// has one, and with its letters until then (and for good when there is none).
/// </summary>
public sealed class FriendMark : Border
{
    private readonly TextBlock _letters;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly Border _tile;
    private readonly string? _logo;
    private Bitmap? _bitmap;
    private int _loadVersion;

    /// <param name="friend">The site.</param>
    /// <param name="tile">The size of the logo's tile.</param>
    /// <param name="minHeight">The height the mark keeps at least.</param>
    public FriendMark(GalleryFriend friend, double tile, double minHeight)
    {
        _logo = friend.Logo;
        MinHeight = minHeight;
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Hsl(friend.Hue, 0.7, 0.62), 0),
                new GradientStop(Hsl(friend.Hue + 40, 0.65, 0.42), 1)
            }
        };
        _letters = new TextBlock
        {
            Text = friend.Glyph,
            FontSize = tile * 0.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.HighQuality);
        _tile = new Border
        {
            Width = tile,
            Height = tile,
            CornerRadius = new CornerRadius(tile * 0.24),
            ClipToBounds = true,
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 14, Color = Color.FromArgb(0x40, 0, 0, 0) }),
            IsVisible = false,
            Child = _image
        };
        Child = new Grid { Children = { _letters, _tile } };
    }

    /// <summary>Whether the logo is shown.</summary>
    public bool IsLogoShown => _tile.IsVisible;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_logo is not null)
        {
            _ = ShowAsync(_logo, ++_loadVersion);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The decoded logo is released with the page; one still being fetched is dropped when it arrives.
        _loadVersion++;
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _tile.IsVisible = false;
        _letters.IsVisible = true;
    }

    private async Task ShowAsync(string name, int version)
    {
        Stream? stream;
        try
        {
            stream = await GalleryLogos.Source.OpenAsync(name);
        }
        catch (Exception e) when (e is IOException or HttpRequestException or InvalidOperationException)
        {
            // A logo that cannot be fetched is a missing logo; the letters stay.
            stream = null;
        }
        if (version != _loadVersion || stream is null)
        {
            stream?.Dispose();
            return;
        }
        using (stream)
        {
            _bitmap = new Bitmap(stream);
        }
        _image.Source = _bitmap;
        _tile.IsVisible = true;
        _letters.IsVisible = false;
    }

    private static Color Hsl(double hue, double saturation, double lightness)
    {
        hue = (hue % 360 + 360) % 360;
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - c / 2;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
