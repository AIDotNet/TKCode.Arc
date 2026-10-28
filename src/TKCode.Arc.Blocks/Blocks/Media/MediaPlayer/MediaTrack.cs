using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One entry of a <see cref="MediaPlayer"/>'s playlist: what it is called, who made it, its cover and the audio to play.
/// </summary>
public class MediaTrack : AvaloniaObject
{
    /// <summary>The track's name: the player's heading while it is current, and its row in the playlist.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MediaTrack, string>(nameof(Title), string.Empty);

    /// <summary>Who made it; shown under the heading before the album.</summary>
    public static readonly StyledProperty<string?> ArtistProperty =
        AvaloniaProperty.Register<MediaTrack, string?>(nameof(Artist));

    /// <summary>The album or collection; shown under the heading after the artist.</summary>
    public static readonly StyledProperty<string?> AlbumProperty =
        AvaloniaProperty.Register<MediaTrack, string?>(nameof(Album));

    /// <summary>The cover art, drawn to fill the cover frame (cropped, not stretched).</summary>
    public static readonly StyledProperty<IImage?> CoverProperty =
        AvaloniaProperty.Register<MediaTrack, IImage?>(nameof(Cover));

    /// <summary>What the cover shows, for screen readers.</summary>
    public static readonly StyledProperty<string?> CoverDescriptionProperty =
        AvaloniaProperty.Register<MediaTrack, string?>(nameof(CoverDescription));

    /// <summary>The audio to play; a track without one can be selected but stays silent.</summary>
    public static readonly StyledProperty<AudioSource?> SourceProperty =
        AvaloniaProperty.Register<MediaTrack, AudioSource?>(nameof(Source));

    /// <summary>
    /// The track's length, shown in the playlist before it has been loaded; the player replaces it with the length the
    /// audio reports once the track is loaded.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<MediaTrack, TimeSpan>(nameof(Duration));

    /// <summary>Anything you want to keep with the track.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<MediaTrack, object?>(nameof(Tag));

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ArtistProperty"/>
    public string? Artist { get => GetValue(ArtistProperty); set => SetValue(ArtistProperty, value); }

    /// <inheritdoc cref="AlbumProperty"/>
    public string? Album { get => GetValue(AlbumProperty); set => SetValue(AlbumProperty, value); }

    /// <inheritdoc cref="CoverProperty"/>
    public IImage? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }

    /// <inheritdoc cref="CoverDescriptionProperty"/>
    public string? CoverDescription { get => GetValue(CoverDescriptionProperty); set => SetValue(CoverDescriptionProperty, value); }

    /// <inheritdoc cref="SourceProperty"/>
    public AudioSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>"Artist · Album", either part alone, or empty.</summary>
    public string Byline
    {
        get
        {
            var artist = Artist;
            var album = Album;
            if (string.IsNullOrWhiteSpace(artist))
            {
                return album ?? string.Empty;
            }
            return string.IsNullOrWhiteSpace(album) ? artist : $"{artist} · {album}";
        }
    }
}
