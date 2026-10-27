using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>Where an image the editor is about to insert came from.</summary>
public enum RichTextImageOrigin
{
    /// <summary>The file picker opened by the slash menu or <see cref="RichTextEditor.PickImageAsync"/>.</summary>
    Picker,

    /// <summary>The clipboard: an image file, or the pixels of a screenshot or a copied picture.</summary>
    Paste,

    /// <summary>An image file dropped onto the editor.</summary>
    Drop
}

/// <summary>
/// Arguments of <see cref="RichTextEditor.ImageSourceRequested"/>: the editor holds a picture (a file, pasted pixels) and asks where
/// the document should keep it. Answer with <see cref="Source"/> (an address you already know) or <see cref="SourceTask"/> (an upload
/// in progress); do nothing and the editor embeds the picture as a <c>data:</c> address.
/// </summary>
public sealed class RichTextImageSourceRequestedEventArgs : EventArgs
{
    private readonly Func<Task<Stream>> _open;

    internal RichTextImageSourceRequestedEventArgs(RichTextImageOrigin origin, string? fileName, string contentType, Func<Task<Stream>> open)
    {
        Origin = origin;
        FileName = fileName;
        ContentType = contentType;
        _open = open;
        Alt = string.IsNullOrEmpty(fileName) ? null : Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>How the picture reached the editor.</summary>
    public RichTextImageOrigin Origin { get; }

    /// <summary>The file's name, or null for pasted pixels.</summary>
    public string? FileName { get; }

    /// <summary>The media type of the bytes <see cref="OpenReadAsync"/> returns, such as <c>image/png</c>.</summary>
    public string ContentType { get; }

    /// <summary>The description the image starts with (the file name without its extension); change it to set another.</summary>
    public string? Alt { get; set; }

    /// <summary>The address to keep in the document, when you know it right away (a path in your store, a CDN URL).</summary>
    public string? Source { get; set; }

    /// <summary>
    /// The address to keep, when it is not known yet: an upload. The editor waits for the task and inserts the image where the caret
    /// is then; a task that fails or returns null inserts nothing.
    /// </summary>
    public Task<string?>? SourceTask { get; set; }

    /// <summary>Set to true to drop the picture: nothing is inserted.</summary>
    public bool Cancel { get; set; }

    /// <summary>Opens the picture's bytes (each call returns a new stream).</summary>
    public Task<Stream> OpenReadAsync() => _open();

    /// <summary>Reads the picture's bytes.</summary>
    public async Task<byte[]> ReadAllBytesAsync()
    {
        await using var stream = await _open();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }

    internal bool IsAnswered => Cancel || Source is not null || SourceTask is not null;
}

/// <summary>
/// Arguments of <see cref="RichTextEditor.ImageLoading"/>: the editor needs the pixels of an address it shows. Answer with
/// <see cref="Bitmap"/> or <see cref="StreamTask"/> to load it yourself (an address that needs a token, a private store, your own scheme);
/// do nothing and the editor reads <c>data:</c>, http(s), <c>avares:</c> and local file addresses itself.
/// </summary>
public sealed class RichTextImageLoadingEventArgs : EventArgs
{
    internal RichTextImageLoadingEventArgs(string source)
    {
        Source = source;
    }

    /// <summary>The address stored in the document.</summary>
    public string Source { get; }

    /// <summary>The loaded picture, when you have it already.</summary>
    public Bitmap? Bitmap { get; set; }

    /// <summary>The picture's encoded bytes (PNG, JPEG, WebP...), when they are still on their way. A null result shows the failed placeholder.</summary>
    public Task<Stream?>? StreamTask { get; set; }

    internal bool IsAnswered => Bitmap is not null || StreamTask is not null;
}
