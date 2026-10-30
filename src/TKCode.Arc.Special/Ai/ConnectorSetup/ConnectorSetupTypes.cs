using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Where one app of a <see cref="ConnectorSetup"/> is: resting, in one of the connect steps, connected or failed.</summary>
public enum ConnectorPhase
{
    /// <summary>Not connected: the row shows the app's description and Connect.</summary>
    Idle,

    /// <summary>The first step: the app's sign-in page is opening (the OAuth redirect or popup).</summary>
    Redirecting,

    /// <summary>The person is approving the access in the app.</summary>
    Authorizing,

    /// <summary>Access was granted and the first sync runs; the row counts what it has read.</summary>
    Syncing,

    /// <summary>Connected: the row shows the account and Connected, which opens the granted scopes.</summary>
    Connected,

    /// <summary>The connect failed: an alert, the reason and Retry.</summary>
    Failed,

    /// <summary>Access is being revoked after the inline confirmation.</summary>
    Disconnecting
}

/// <summary>The built-in marks of <see cref="ConnectorMark"/>: neutral Lucide glyphs for common kinds of apps (no brand logos).</summary>
public enum ConnectorMarkKind
{
    /// <summary>No built-in glyph: the mark shows <see cref="ConnectorMark.Glyph"/>, or the first letter of the name.</summary>
    None,

    /// <summary>Team chat (a speech bubble).</summary>
    Chat,

    /// <summary>A code host (a branch).</summary>
    Code,

    /// <summary>Documents and wikis (a page).</summary>
    Docs,

    /// <summary>A file drive (a folder).</summary>
    Drive,

    /// <summary>An issue tracker (a dotted circle).</summary>
    Issues,

    /// <summary>A calendar.</summary>
    Calendar,

    /// <summary>Email (an envelope).</summary>
    Mail,

    /// <summary>A CRM (people).</summary>
    Crm,

    /// <summary>A database.</summary>
    Database,

    /// <summary>Analytics (a bar chart).</summary>
    Analytics
}

/// <summary>
/// One app a <see cref="ConnectorSetup"/> lists. Write the scopes as plain-language capabilities ("Post replies as your assistant"),
/// not raw OAuth scope strings.
/// </summary>
public sealed class ConnectorApp
{
    /// <summary>Creates an empty app (set its properties).</summary>
    public ConnectorApp()
    {
    }

    /// <summary>Creates an app with its id, name, description and built-in mark.</summary>
    public ConnectorApp(string id, string name, string description, ConnectorMarkKind kind = ConnectorMarkKind.None)
    {
        Id = id;
        Name = name;
        Description = description;
        Kind = kind;
    }

    /// <summary>The app's stable id; <see cref="ConnectorSetup.ConnectedIds"/> holds these.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The app's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One line on what the app holds, shown while it is not connected.</summary>
    public string? Description { get; set; }

    /// <summary>The built-in mark (a neutral glyph for the kind of app).</summary>
    public ConnectorMarkKind Kind { get; set; }

    /// <summary>Your own mark: a 24 × 24 stroked glyph that takes the place of the built-in one.</summary>
    public Geometry? Glyph { get; set; }

    /// <summary>The host the sign-in opens on ("Opening chat.example.com"); the name when not set.</summary>
    public string? Host { get; set; }

    /// <summary>The account or workspace of a connection made before (a connect may return a new one).</summary>
    public string? Account { get; set; }

    /// <summary>What the assistant may do in the app, in plain language (a connect may return its own list).</summary>
    public IList<string> Scopes { get; set; } = new List<string>();

    /// <summary>How many items the first sync reads, when known (it reserves the count's width while it rolls).</summary>
    public long? SyncCount { get; set; }

    /// <summary>What the sync counts, plural ("messages", "files").</summary>
    public string? SyncNoun { get; set; }

    /// <summary>More words the search matches.</summary>
    public IList<string> Keywords { get; set; } = new List<string>();
}

/// <summary>What a finished connect reports back: the account, the granted scopes and how many items the first sync read.</summary>
public sealed class ConnectorConnectResult
{
    /// <summary>The account or workspace that granted access.</summary>
    public string? Account { get; init; }

    /// <summary>The granted scopes in plain language; the app's own list when null.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>How many items the first sync read.</summary>
    public long? Synced { get; init; }
}

/// <summary>
/// What a connect handler gets besides the app: a token that is cancelled when the person presses Cancel or the card
/// leaves the window, and the report calls that move the row through its steps. The report calls may come from any
/// thread.
/// </summary>
public sealed class ConnectorConnectContext
{
    private readonly Action<ConnectorPhase, long?, long?> _report;

    internal ConnectorConnectContext(CancellationToken cancellationToken, Action<ConnectorPhase, long?, long?> report)
    {
        CancellationToken = cancellationToken;
        _report = report;
    }

    /// <summary>Cancelled on Cancel or when the card is removed; pass it to your OAuth and sync calls.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>The sign-in page is open and waits for the person to approve.</summary>
    public void ReportAuthorizing() => Post(ConnectorPhase.Authorizing, null, null);

    /// <summary>Access was granted and the first sync runs: <paramref name="synced"/> items so far, of <paramref name="total"/> when known.</summary>
    public void ReportSyncing(long synced, long? total = null) => Post(ConnectorPhase.Syncing, Math.Max(0, synced), total);

    private void Post(ConnectorPhase phase, long? synced, long? total)
    {
        if (CancellationToken.IsCancellationRequested)
        {
            return;
        }
        if (Dispatcher.UIThread.CheckAccess())
        {
            _report(phase, synced, total);
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!CancellationToken.IsCancellationRequested)
                {
                    _report(phase, synced, total);
                }
            });
        }
    }
}

/// <summary>
/// Runs your OAuth flow for <paramref name="app"/>: report the steps through <paramref name="context"/>, throw an
/// exception with a short human message ("Needs admin approval") to show it with Retry, and return the result to finish.
/// </summary>
public delegate Task<ConnectorConnectResult?> ConnectorSetupConnectHandler(ConnectorApp app, ConnectorConnectContext context);

/// <summary>Revokes the assistant's access to <paramref name="app"/> after the person confirmed.</summary>
public delegate Task ConnectorSetupDisconnectHandler(ConnectorApp app, CancellationToken cancellationToken);

/// <summary>Arguments of <see cref="ConnectorSetup.AppDisconnected"/>.</summary>
public class ConnectorAppEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ConnectorAppEventArgs(RoutedEvent routedEvent, ConnectorApp app) : base(routedEvent)
    {
        App = app;
    }

    /// <summary>The app.</summary>
    public ConnectorApp App { get; }
}

/// <summary>Arguments of <see cref="ConnectorSetup.AppConnected"/>.</summary>
public sealed class ConnectorConnectedEventArgs : ConnectorAppEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ConnectorConnectedEventArgs(RoutedEvent routedEvent, ConnectorApp app, ConnectorConnectResult result) : base(routedEvent, app)
    {
        Result = result;
    }

    /// <summary>The account, scopes and sync count of the connection.</summary>
    public ConnectorConnectResult Result { get; }
}

/// <summary>Arguments of <see cref="ConnectorSetup.ConnectedIdsChanged"/>.</summary>
public sealed class ConnectorConnectedIdsChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ConnectorConnectedIdsChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> ids) : base(routedEvent)
    {
        Ids = ids;
    }

    /// <summary>The ids connected now, in list order.</summary>
    public IReadOnlyList<string> Ids { get; }
}
