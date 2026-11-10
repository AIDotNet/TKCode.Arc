using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>What an <see cref="AvatarUpload"/> is doing.</summary>
public enum AvatarUploadState
{
    /// <summary>No picture: the field invites a drop or a pick.</summary>
    Empty,

    /// <summary>A picture is being framed in the circle.</summary>
    Editing,

    /// <summary>The framed picture is being uploaded.</summary>
    Uploading,

    /// <summary>The field shows its picture.</summary>
    Done,

    /// <summary>The upload failed; it can be retried or cancelled.</summary>
    Failed
}

/// <summary>Why an <see cref="AvatarUpload"/> refused a file.</summary>
public enum AvatarUploadRejection
{
    /// <summary>The file is not a PNG, JPEG, WebP, GIF or BMP image.</summary>
    Type,

    /// <summary>The file is larger than <see cref="AvatarUpload.MaxFileSize"/>.</summary>
    Size,

    /// <summary>The file could not be read as an image.</summary>
    Unreadable
}

/// <summary>A framed picture: the square image to upload and the part of the original it was cut from.</summary>
public sealed class AvatarUploadRequest
{
    public AvatarUploadRequest(Bitmap image, PixelRect sourceRect, string? fileName)
    {
        Image = image;
        SourceRect = sourceRect;
        FileName = fileName;
    }

    /// <summary>The square picture, <see cref="AvatarUpload.OutputSize"/> pixels on each side. Show it in a circle.</summary>
    public Bitmap Image { get; }

    /// <summary>The square of the original picture, in its pixels, that the image was cut from.</summary>
    public PixelRect SourceRect { get; }

    /// <summary>The name of the file the picture came from, when it came from a file.</summary>
    public string? FileName { get; }
}

/// <summary>Arguments of <see cref="AvatarUpload.Cropped"/>.</summary>
public class AvatarUploadCroppedEventArgs : RoutedEventArgs
{
    public AvatarUploadCroppedEventArgs(RoutedEvent routedEvent, AvatarUploadRequest request)
        : base(routedEvent)
    {
        Request = request;
    }

    /// <summary>The framed picture.</summary>
    public AvatarUploadRequest Request { get; }
}

/// <summary>Arguments of <see cref="AvatarUpload.FileRejected"/>.</summary>
public class AvatarUploadRejectedEventArgs : RoutedEventArgs
{
    public AvatarUploadRejectedEventArgs(RoutedEvent routedEvent, string fileName, AvatarUploadRejection reason)
        : base(routedEvent)
    {
        FileName = fileName;
        Reason = reason;
    }

    /// <summary>The refused file's name.</summary>
    public string FileName { get; }

    /// <summary>Why it was refused.</summary>
    public AvatarUploadRejection Reason { get; }
}
