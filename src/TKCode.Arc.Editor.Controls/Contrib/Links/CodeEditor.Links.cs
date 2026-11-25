// How the editor opens a link the user Ctrl+clicked (Cmd+click on macOS), or that the Open Link action opens: the
// LinkOpening event first, then the platform's launcher. Upstream opens links through the opener service
// (IOpenerService.open from src/vs/editor/contrib/links/browser/links.ts, commit b57d5c9e; MIT license); an
// application replaces that service, here it handles the event.

using Avalonia.Controls;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    /// <summary>
    /// Raised when the user opens a link in the text: Ctrl+click on it (Cmd+click on macOS), or the Open Link action.
    /// Set <see cref="CodeEditorLinkOpeningEventArgs.Handled"/> to open it yourself (or not at all); otherwise the
    /// editor asks the platform's launcher to open it.
    /// </summary>
    public event EventHandler<CodeEditorLinkOpeningEventArgs>? LinkOpening;

    /// <summary>Raises <see cref="LinkOpening"/>, then launches the link unless a handler handled it; true when it was opened or handled.</summary>
    internal async Task<bool> OpenLinkAsync(Uri? uri, string target, bool fromUserGesture)
    {
        var args = new CodeEditorLinkOpeningEventArgs(uri, target, fromUserGesture);
        LinkOpening?.Invoke(this, args);
        if (args.Handled)
        {
            return true;
        }

        if (uri is null || TopLevel.GetTopLevel(this)?.Launcher is not { } launcher)
        {
            return false;
        }

        return await launcher.LaunchUriAsync(uri);
    }
}

/// <summary>The data of <see cref="CodeEditor.LinkOpening"/>.</summary>
public sealed class CodeEditorLinkOpeningEventArgs : EventArgs
{
    internal CodeEditorLinkOpeningEventArgs(Uri? uri, string target, bool fromUserGesture)
    {
        Uri = uri;
        Target = target;
        FromUserGesture = fromUserGesture;
    }

    /// <summary>The link's target as a URI, or null when it is not a valid absolute URI (see <see cref="Target"/>).</summary>
    public Uri? Uri { get; }

    /// <summary>The link's target as written.</summary>
    public string Target { get; }

    /// <summary>Whether the user clicked the link (false for the Open Link action).</summary>
    public bool FromUserGesture { get; }

    /// <summary>Set to true when the handler opened the link (or decided not to), so that the editor does not launch it.</summary>
    public bool Handled { get; set; }
}
