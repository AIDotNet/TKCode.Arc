using Avalonia;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>What a <see cref="VideoTextTrack"/> holds.</summary>
public enum VideoTextTrackKind
{
    /// <summary>Captions: dialogue plus sound effects and other audio information, for viewers who cannot hear it.</summary>
    Captions,

    /// <summary>Subtitles: a translation of the dialogue.</summary>
    Subtitles,

    /// <summary>Chapter titles; split the seek bar into segments and are not drawn over the picture.</summary>
    Chapters,

    /// <summary>Text descriptions of the picture, read by screen readers; not drawn.</summary>
    Descriptions,
}

/// <summary>Where a cue's text lines up within its box (WebVTT <c>align</c>).</summary>
public enum VideoCueAlign
{
    /// <summary>At the start of the line (left in left-to-right text).</summary>
    Start,

    /// <summary>Centered (the default).</summary>
    Center,

    /// <summary>At the end of the line.</summary>
    End,

    /// <summary>Left.</summary>
    Left,

    /// <summary>Right.</summary>
    Right,
}

/// <summary>One styled run of a cue's text.</summary>
/// <param name="Text">The run's text; line breaks are <c>\n</c>.</param>
/// <param name="IsBold">Inside <c>&lt;b&gt;</c>.</param>
/// <param name="IsItalic">Inside <c>&lt;i&gt;</c>.</param>
/// <param name="IsUnderline">Inside <c>&lt;u&gt;</c>.</param>
/// <param name="Voice">The speaker from an enclosing <c>&lt;v Speaker&gt;</c>, or null.</param>
public sealed record VideoCueRun(string Text, bool IsBold = false, bool IsItalic = false, bool IsUnderline = false, string? Voice = null);

/// <summary>One timed piece of text from a WebVTT or SRT file.</summary>
public sealed record VideoCue
{
    /// <summary>The cue identifier, or null.</summary>
    public string? Id { get; init; }

    /// <summary>When the cue appears.</summary>
    public required TimeSpan Start { get; init; }

    /// <summary>When the cue disappears (exclusive).</summary>
    public required TimeSpan End { get; init; }

    /// <summary>The plain text with markup removed; line breaks are <c>\n</c>.</summary>
    public required string Text { get; init; }

    /// <summary>The styled runs; one plain run when the cue has no markup.</summary>
    public IReadOnlyList<VideoCueRun> Runs { get; init; } = [];

    /// <summary>WebVTT <c>line</c>: a line number (negative counts from the bottom) or, when <see cref="LineIsPercent"/>, a percentage of the picture height; null for auto (stacked from the bottom).</summary>
    public double? Line { get; init; }

    /// <summary>Whether <see cref="Line"/> is a percentage.</summary>
    public bool LineIsPercent { get; init; }

    /// <summary>WebVTT <c>position</c> in percent of the picture width; null for auto.</summary>
    public double? Position { get; init; }

    /// <summary>WebVTT <c>size</c>: the box width in percent of the picture width (default 100).</summary>
    public double Size { get; init; } = 100;

    /// <summary>WebVTT <c>align</c>.</summary>
    public VideoCueAlign Align { get; init; } = VideoCueAlign.Center;

    /// <summary>Whether the cue is shown at <paramref name="time"/>.</summary>
    public bool IsActiveAt(TimeSpan time) => time >= Start && time < End;
}

/// <summary>
/// A captions, subtitles, chapters or descriptions track for <see cref="VideoPlayer"/>: WebVTT or SRT text from
/// <see cref="Source"/> (avares, file, http(s)) or <see cref="Text"/>.
/// </summary>
public sealed class VideoTextTrack : AvaloniaObject
{
    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<VideoTextTrackKind> KindProperty =
        AvaloniaProperty.Register<VideoTextTrack, VideoTextTrackKind>(nameof(Kind), VideoTextTrackKind.Captions);

    /// <summary>Defines the <see cref="Language"/> property.</summary>
    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<VideoTextTrack, string?>(nameof(Language));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VideoTextTrack, string?>(nameof(Label));

    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<Uri?> SourceProperty =
        AvaloniaProperty.Register<VideoTextTrack, Uri?>(nameof(Source));

    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<VideoTextTrack, string?>(nameof(Text));

    /// <summary>Defines the <see cref="IsDefault"/> property.</summary>
    public static readonly StyledProperty<bool> IsDefaultProperty =
        AvaloniaProperty.Register<VideoTextTrack, bool>(nameof(IsDefault));

    private IReadOnlyList<VideoCue> _cues = [];
    private IReadOnlyList<string> _warnings = [];
    private Task? _loading;

    /// <summary>What the track holds.</summary>
    public VideoTextTrackKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The BCP 47 language tag, such as <c>zh-CN</c>.</summary>
    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <summary>The name shown in the captions menu; defaults to the language's native name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Where the WebVTT or SRT file is (avares, file, http, https).</summary>
    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The WebVTT or SRT text itself; used instead of <see cref="Source"/> when set.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Whether the player selects this track when it loads (the first default captions or subtitles track wins).</summary>
    public bool IsDefault
    {
        get => GetValue(IsDefaultProperty);
        set => SetValue(IsDefaultProperty, value);
    }

    /// <summary>The cues, in start order; empty until <see cref="LoadAsync"/> completes.</summary>
    public IReadOnlyList<VideoCue> Cues => _cues;

    /// <summary>Problems found while parsing (bad cues are skipped), with line numbers.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Raised on the UI thread when <see cref="Cues"/> changes.</summary>
    public event EventHandler? CuesChanged;

    /// <summary>Reads and parses the track once (again after <see cref="Source"/> or <see cref="Text"/> changes). Never throws for bad content; a read failure leaves the cues empty and adds a warning.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default) => _loading ??= LoadCoreAsync(cancellationToken);

    /// <summary>Sets the cues directly (for tracks built in code).</summary>
    public void SetCues(IEnumerable<VideoCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        _cues = cues.OrderBy(static c => c.Start).ToArray();
        _warnings = [];
        _loading = Task.CompletedTask;
        CuesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == TextProperty)
        {
            _loading = null;
        }
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        string? text = Text;
        var warnings = new List<string>();
        if (text is null && Source is { } source)
        {
            try
            {
                text = await ReadAsync(source, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException or ArgumentException)
            {
                warnings.Add($"Could not read {source}: {exception.Message}");
            }
        }
        var result = text is null ? new VideoCaptionParseResult([], []) : VideoCaptionParser.Parse(text);
        warnings.AddRange(result.Warnings);
        _cues = result.Cues;
        _warnings = warnings;
        CuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<string> ReadAsync(Uri source, CancellationToken cancellationToken)
    {
        byte[] bytes;
        switch (source.Scheme)
        {
            case "avares":
            {
                await using var stream = AssetLoader.Open(source);
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                bytes = buffer.ToArray();
                break;
            }
            case "http" or "https":
            {
                using var client = new HttpClient();
                bytes = await client.GetByteArrayAsync(source, cancellationToken).ConfigureAwait(false);
                break;
            }
            default:
                bytes = await File.ReadAllBytesAsync(source.IsAbsoluteUri ? source.LocalPath : source.OriginalString, cancellationToken).ConfigureAwait(false);
                break;
        }
        return VideoCaptionParser.Decode(bytes);
    }
}
