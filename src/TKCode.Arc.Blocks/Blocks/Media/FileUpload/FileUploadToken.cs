// Ported from Arc's free file-upload block (MIT, Copyright (c) 2026 Elia Kuratli): file-upload.module.css.
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="FileUpload"/>. Colors, radii and type come from ArcDesign tokens in the theme; this token holds
/// the sizes and spacing of the dropzone, the rows and the progress bar.
/// </summary>
[ControlDesignToken]
internal sealed class FileUploadToken : AbstractControlDesignToken
{
    /// <summary>The dropzone is at least this tall (92).</summary>
    public double DropzoneMinHeight { get; set; }

    /// <summary>The dropzone's padding (16).</summary>
    public Thickness DropzoneInset { get; set; }

    /// <summary>The space between the dropzone's icon, copy and Browse (12).</summary>
    public double DropzoneSpacing { get; set; }

    /// <summary>The dash and the gap of the dropzone's 1px dashed edge (3).</summary>
    public double DashLength { get; set; }

    /// <summary>The dropzone's opacity while disabled (.52).</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>The space above the status line (8).</summary>
    public double StatusTopInset { get; set; }

    /// <summary>The list's top padding while it holds files (6).</summary>
    public double ListTopInset { get; set; }

    /// <summary>The space above each row, inside its clipped frame (6).</summary>
    public double RowSpacing { get; set; }

    /// <summary>A row's padding (12 10 10 10).</summary>
    public Thickness RowInset { get; set; }

    /// <summary>The space between a row's icon, copy, status and Remove (12).</summary>
    public double RowColumnSpacing { get; set; }

    /// <summary>The edge of the file icon's box and the upload icon's box (24).</summary>
    public double IconBoxEdge { get; set; }

    /// <summary>The edge of the status slot and of the Retry and Remove buttons (26).</summary>
    public double ButtonEdge { get; set; }

    /// <summary>The progress bar's height (3).</summary>
    public double BarHeight { get; set; }

    /// <summary>The progress bar's margin (0 5 0 1).</summary>
    public Thickness BarMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DropzoneMinHeight = 92;
        DropzoneInset = new Thickness(16);
        DropzoneSpacing = 12;
        DashLength = 3;
        DisabledOpacity = 0.52;
        StatusTopInset = 8;
        ListTopInset = 6;
        RowSpacing = 6;
        RowInset = new Thickness(12, 10, 10, 10);
        RowColumnSpacing = 12;
        IconBoxEdge = 24;
        ButtonEdge = 26;
        BarHeight = 3;
        BarMargin = new Thickness(0, 5, 0, 1);
    }
}
