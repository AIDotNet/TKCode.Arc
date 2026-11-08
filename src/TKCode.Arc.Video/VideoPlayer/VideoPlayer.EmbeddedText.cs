namespace TKCode.Arc;

public partial class VideoPlayer
{
    // Text tracks stored in the media (IVideoEmbeddedTextTracks): while the media is loaded they are VideoTextTracks with
    // IsEmbedded in TextTracks, after the app's own tracks, so the captions menu, the C key and the caption layer treat them
    // like any other track. Selecting one tells the backend to deliver its cues; they are added to the track as they come.
    private IVideoEmbeddedTextTracks? _embeddedSource;
    private readonly List<VideoTextTrack> _embeddedTracks = [];

    private void AttachEmbeddedText(IVideoPlayer player)
    {
        if (player is not IVideoEmbeddedTextTracks source)
        {
            return;
        }
        _embeddedSource = source;
        source.EmbeddedTextTracksChanged += OnEmbeddedTextTracksChanged;
        source.EmbeddedCue += OnEmbeddedCue;
        OnEmbeddedTextTracksChanged(source, EventArgs.Empty);
    }

    private void DetachEmbeddedText()
    {
        if (_embeddedSource is { } source)
        {
            source.EmbeddedTextTracksChanged -= OnEmbeddedTextTracksChanged;
            source.EmbeddedCue -= OnEmbeddedCue;
            _embeddedSource = null;
        }
        RemoveEmbeddedTracks();
    }

    private void RemoveEmbeddedTracks()
    {
        if (_embeddedTracks.Count == 0)
        {
            return;
        }
        var tracks = _embeddedTracks.ToArray();
        _embeddedTracks.Clear();
        foreach (var track in tracks)
        {
            _textTracks.Remove(track);
        }
    }

    private void OnEmbeddedTextTracksChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _embeddedSource) || _embeddedSource is not { } source)
        {
            return;
        }
        RemoveEmbeddedTracks();
        foreach (var info in source.EmbeddedTextTracks)
        {
            var track = new VideoTextTrack(info.Index)
            {
                Kind = info.Kind,
                Language = string.IsNullOrEmpty(info.Language) ? null : info.Language,
                Label = string.IsNullOrEmpty(info.Label) ? null : info.Label,
            };
            _embeddedTracks.Add(track);
            _textTracks.Add(track);
        }
    }

    private void OnEmbeddedCue(object? sender, VideoEmbeddedCueEventArgs e)
    {
        if (!ReferenceEquals(sender, _embeddedSource))
        {
            return;
        }
        foreach (var track in _embeddedTracks)
        {
            if (track.EmbeddedIndex == e.Track)
            {
                track.AddEmbeddedCue(e.Start, e.End, e.Text);
                return;
            }
        }
    }

    /// <summary>Tells the backend which embedded track to deliver: the selected one when it is embedded, else none.</summary>
    private void SelectEmbeddedText(VideoTextTrack? selected)
    {
        if (_embeddedSource is not { } source)
        {
            return;
        }
        var index = selected is { IsEmbedded: true } && _embeddedTracks.Contains(selected) ? selected.EmbeddedIndex : -1;
        source.SelectEmbeddedTextTrack(index);
    }
}
