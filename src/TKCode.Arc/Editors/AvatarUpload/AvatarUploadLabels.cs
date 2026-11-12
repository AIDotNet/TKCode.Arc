namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AvatarUpload"/> shows or says, in English by default. Replace the whole object to translate or
/// reword it; the texts that include a number or a name are format functions.
/// </summary>
public sealed class AvatarUploadLabels
{
    /// <summary>The English defaults.</summary>
    public static AvatarUploadLabels Default { get; } = new();

    /// <summary>The pick button with no picture.</summary>
    public string Upload { get; init; } = "Upload photo";

    /// <summary>The pick button with a picture.</summary>
    public string Change { get; init; } = "Change";

    /// <summary>The button that removes the picture.</summary>
    public string Remove { get; init; } = "Remove";

    /// <summary>The editor's confirm button.</summary>
    public string Save { get; init; } = "Save";

    /// <summary>The editor's cancel button, and the button that gives up a failed upload.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The button that tries a failed upload again.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button that stops an upload.</summary>
    public string CancelUpload { get; init; } = "Cancel upload";

    /// <summary>What the field says while a file is dragged over it.</summary>
    public string DropHint { get; init; } = "Drop to upload";

    /// <summary>The upload status, from the percentage: "Uploading 42%".</summary>
    public Func<int, string> Uploading { get; init; } = percent => "Uploading " + percent + "%";

    /// <summary>What the field says when an upload fails without a reason.</summary>
    public string UploadFailed { get; init; } = "Upload failed";

    /// <summary>What the field says after a successful upload.</summary>
    public string Uploaded { get; init; } = "Photo updated";

    /// <summary>What the field says after the picture was removed.</summary>
    public string Removed { get; init; } = "Photo removed";

    /// <summary>A refused file of the wrong type, from its name.</summary>
    public Func<string, string> WrongType { get; init; } = name => name + " is not a PNG, JPG, WebP or GIF image";

    /// <summary>A refused file of the wrong type while <see cref="AvatarUpload.Accept"/> is set, from its name and the accepted types in words ("PNG or JPEG").</summary>
    public Func<string, string, string> WrongTypeOf { get; init; } = (name, types) =>
        name + " is not " + (types.Length > 0 && "AEIOU".Contains(types[0]) ? "an " : "a ") + types + " image";

    /// <summary>A refused file that is too large, from its name and the limit ("10 MB").</summary>
    public Func<string, string, string> TooLarge { get; init; } = (name, limit) => name + " is larger than " + limit;

    /// <summary>A file that could not be read as an image, from its name.</summary>
    public Func<string, string> Unreadable { get; init; } = name => name + " could not be read";

    /// <summary>The name of the picture's circle.</summary>
    public string Picture { get; init; } = "Profile photo";

    /// <summary>The name of the editor's frame.</summary>
    public string CropArea { get; init; } = "Photo position";

    /// <summary>The help text of the editor's frame.</summary>
    public string CropHelp { get; init; } = "Drag or use the arrow keys to move the photo, plus and minus to zoom, Enter to save, Escape to cancel";

    /// <summary>The zoom slider and the two zoom buttons.</summary>
    public string Zoom { get; init; } = "Zoom";

    public string ZoomIn { get; init; } = "Zoom in";

    public string ZoomOut { get; init; } = "Zoom out";
}
