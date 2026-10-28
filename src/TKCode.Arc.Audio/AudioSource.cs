namespace TKCode.Arc;

/// <summary>Audio to load: an address, a local file, or encoded bytes in memory (WAV, MP3, FLAC; what plays depends on the backend).</summary>
public abstract class AudioSource
{
    private protected AudioSource()
    {
    }

    /// <summary>Audio at an http(s) or file address.</summary>
    public static AudioSource FromUri(Uri uri) => new UriAudioSource(uri ?? throw new ArgumentNullException(nameof(uri)));

    /// <summary>Audio in a local file.</summary>
    public static AudioSource FromFile(string path) => new UriAudioSource(new Uri(Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)))));

    /// <summary>Encoded audio in memory, such as a WAV file's bytes, with its media type (<c>audio/wav</c>, <c>audio/mpeg</c>).</summary>
    public static AudioSource FromBytes(ReadOnlyMemory<byte> data, string mediaType = "audio/wav") => new BytesAudioSource(data, mediaType);
}

/// <summary>An <see cref="AudioSource"/> at an address.</summary>
public sealed class UriAudioSource : AudioSource
{
    internal UriAudioSource(Uri uri)
    {
        Uri = uri;
    }

    /// <summary>The audio's address.</summary>
    public Uri Uri { get; }
}

/// <summary>An <see cref="AudioSource"/> held in memory.</summary>
public sealed class BytesAudioSource : AudioSource
{
    internal BytesAudioSource(ReadOnlyMemory<byte> data, string mediaType)
    {
        Data = data;
        MediaType = mediaType;
    }

    /// <summary>The encoded audio.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>The media type of <see cref="Data"/>.</summary>
    public string MediaType { get; }
}
