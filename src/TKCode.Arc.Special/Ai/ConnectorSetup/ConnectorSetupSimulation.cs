namespace TKCode.Arc;

/// <summary>
/// The apps a <see cref="ConnectorSetup"/> lists and plays on its own while the app sets no
/// <see cref="ConnectorSetup.Connectors"/>: the assistant's name, the apps, the ones already connected and a scripted run
/// of connects (one of them can fail first, then succeed on retry). The run starts once the card is on screen and stops
/// for good at the first pointer or key press inside it; each connect is simulated (sign-in, approval, the first sync
/// counting up) and nothing is sent. Set the data before handing the simulation to the card; to show other apps, give the
/// card a new simulation.
/// </summary>
public sealed class ConnectorSetupSimulation
{
    /// <summary>The assistant's name in the texts (the card's own <see cref="ConnectorSetup.AssistantName"/> wins).</summary>
    public string? AssistantName { get; set; }

    /// <summary>The apps, in order.</summary>
    public IReadOnlyList<ConnectorApp>? Apps { get; set; }

    /// <summary>The ids connected when the card first shows (the card's own <see cref="ConnectorSetup.ConnectedIds"/> win).</summary>
    public IReadOnlyList<string>? Connected { get; set; }

    /// <summary>The scripted run, in order: each step waits its pause, then connects its app if it is idle or failed.</summary>
    public IList<ConnectorSetupSimulationStep>? Steps { get; set; }
}

/// <summary>One step of the scripted run of a <see cref="ConnectorSetupSimulation"/>.</summary>
public sealed class ConnectorSetupSimulationStep
{
    /// <summary>Creates an empty step.</summary>
    public ConnectorSetupSimulationStep()
    {
    }

    /// <summary>Creates a step.</summary>
    /// <param name="appId">The <see cref="ConnectorApp.Id"/> it connects.</param>
    /// <param name="pause">The wait before it.</param>
    /// <param name="failure">The reason this attempt fails after approval, or null to succeed.</param>
    public ConnectorSetupSimulationStep(string appId, TimeSpan pause, string? failure = null)
    {
        AppId = appId;
        Pause = pause;
        Failure = failure;
    }

    /// <summary>The <see cref="ConnectorApp.Id"/> it connects; a step whose app is connecting or connected is skipped.</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>The wait before the step.</summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(1200);

    /// <summary>The reason this attempt fails after approval ("Needs admin approval"), or null to succeed.</summary>
    public string? Failure { get; set; }
}

/// <summary>The simulated OAuth flow a <see cref="ConnectorSetup"/> without a connect handler plays, and its pacing.</summary>
internal static class ConnectorSetupSimulatedFlow
{
    /// <summary>The steps of the simulated flow.</summary>
    public static readonly TimeSpan RedirectTime = TimeSpan.FromMilliseconds(700);

    public static readonly TimeSpan AuthorizeTime = TimeSpan.FromMilliseconds(900);

    public static readonly TimeSpan SyncTick = TimeSpan.FromMilliseconds(40);

    public const int SyncTicks = 36;

    public static readonly TimeSpan DisconnectTime = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Stands in for an OAuth flow when there is no connect handler: the sign-in opens, the person approves, then the first
    /// sync counts up to the app's <see cref="ConnectorApp.SyncCount"/> (400 when unknown). With <paramref name="failure"/>
    /// it fails after approval.
    /// </summary>
    public static async Task<ConnectorConnectResult?> ConnectAsync(ConnectorApp app, ConnectorConnectContext context,
        Func<TimeSpan, CancellationToken, Task> delay, string? failure)
    {
        var token = context.CancellationToken;
        await delay(RedirectTime, token);
        token.ThrowIfCancellationRequested();
        context.ReportAuthorizing();
        await delay(AuthorizeTime, token);
        token.ThrowIfCancellationRequested();
        if (failure is not null)
        {
            throw new InvalidOperationException(failure);
        }
        var total = app.SyncCount is { } count && count > 0 ? count : 400;
        for (var i = 0; i < SyncTicks; i++)
        {
            context.ReportSyncing(total * i / SyncTicks, total);
            await delay(SyncTick, token);
            token.ThrowIfCancellationRequested();
        }
        context.ReportSyncing(total, total);
        return new ConnectorConnectResult { Account = app.Account, Scopes = [.. app.Scopes], Synced = total };
    }
}
