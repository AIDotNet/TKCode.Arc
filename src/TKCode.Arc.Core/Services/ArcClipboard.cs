using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace TKCode.Arc.Primitives;

/// <summary>Clipboard helper for copy actions (CopyButton, CodeBlock, JsonViewer).</summary>
public static class ArcClipboard
{
    /// <summary>Copies <paramref name="text"/>; returns false when no clipboard is available or the platform refuses.</summary>
    public static async Task<bool> CopyTextAsync(Visual visual, string text)
    {
        var clipboard = TopLevel.GetTopLevel(visual)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }
        try
        {
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
