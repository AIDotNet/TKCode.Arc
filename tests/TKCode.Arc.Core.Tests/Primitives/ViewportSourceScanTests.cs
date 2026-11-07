using System.Text.RegularExpressions;
using Shouldly;

namespace TKCode.Arc.Core.Tests.Primitives;

/// <summary>
/// Controls that size or shape themselves by "the window" read <c>ArcViewport</c> (the nearest <c>ArcViewportHost</c>,
/// otherwise the window), never <c>TopLevel.ClientSize</c> directly, so they behave the same inside a narrow-viewport
/// preview as on a phone (design/engineering/gallery-mobile-preview.md, section 4.2).
/// </summary>
public partial class ViewportSourceScanTests
{
    // Places that mean the window itself, not the viewport the content sits in.
    private static readonly string[] s_allowed =
    [
        // ArcViewport's own fallback, and the popup positioner's base rectangle before it is narrowed to the viewport.
        "TKCode.Arc.Core/Primitives/Viewport/ArcViewport.Host.cs",
        "TKCode.Arc.Core/Primitives/Popups/ArcPopup.cs",
        // A modal on the window's own layer when the layer has no size yet (a host's layer is handled before).
        "TKCode.Arc.Core/Primitives/Modal/ModalPresence.cs",
        // A floating Dock window is a window: its own client size.
        "TKCode.Arc.Dock/DockFloating/DockFloatingWindow.cs",
        // Dragging a card near the window's edge scrolls: the edge that matters is the window's.
        "TKCode.Arc.Blocks/Blocks/Projects/KanbanBoard/KanbanBoard.Drag.cs",
        // Fallbacks after ArcViewport (only reached when the control is not in a window).
        "TKCode.Arc/Messages/ToastStack/ToastHost.cs"
    ];

    [GeneratedRegex(@"\bClientSize\b(?!Property\b)|ClientSizeProperty")]
    private static partial Regex ClientSizePattern();

    // A read that falls back to the window after ArcViewport ("ArcViewport.GetSize(this) ?? topLevel.ClientSize") is fine.
    [GeneratedRegex(@"ArcViewport\.\w+\([^;]*\)\s*\?\?[^;]*ClientSize|ArcViewport\.GetBounds\([^;]*\)\s*\?\?\s*new Rect\([^;]*ClientSize")]
    private static partial Regex FallbackPattern();

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TKCode.Arc.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new InvalidOperationException("TKCode.Arc.slnx was not found above the test folder");
    }

    [Fact]
    public void Controls_Read_The_Viewport_Not_The_Window_Client_Size()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal) ||
                relative.Contains("GeneratedFiles/", StringComparison.Ordinal) || s_allowed.Contains(relative))
            {
                continue;
            }
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal) || !ClientSizePattern().IsMatch(line) || FallbackPattern().IsMatch(line))
                {
                    continue;
                }
                offenders.Add($"{relative}:{i + 1}: {code}");
            }
        }
        offenders.ShouldBeEmpty("Read the viewport with ArcViewport.GetSize / GetBounds / Observe (TKCode.Arc.Primitives) instead of " +
                                "TopLevel.ClientSize, or add the file to the allowed list with the reason it means the window itself.");
    }
}
