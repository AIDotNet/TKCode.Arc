namespace TKCode.Arc;

/// <summary>
/// An inbox a <see cref="TicketRouter"/> plays on its own while the app sets no <see cref="TicketRouter.Tickets"/>: the
/// classifier model's name, the lanes, the stream of messages with the answer the simulated classifier gives for each
/// (<see cref="TicketMessage.Sample"/>) and the tickets already in the lanes. The router reads, labels and routes one
/// message at a time, sends the ones below the review threshold to the review lane, and loops when the stream runs out;
/// it pauses off screen and with <see cref="TicketRouter.IsPaused"/>. Nothing is sent. Set the data before handing the
/// simulation to the router; to play another inbox, give the router a new simulation.
/// </summary>
public sealed class TicketRouterSimulation
{
    /// <summary>The classifier model's name in the header and the classifying label (the router's own <see cref="TicketRouter.Model"/> wins).</summary>
    public string? Model { get; set; }

    /// <summary>The lanes left to right (the router's own <see cref="TicketRouter.Lanes"/> win); null or empty uses the default lanes.</summary>
    public IList<TicketLane>? Lanes { get; set; }

    /// <summary>The messages in arrival order, each with its <see cref="TicketMessage.Sample"/> answer; a message without one goes to review.</summary>
    public IList<TicketMessage>? Tickets { get; set; }

    /// <summary>Tickets already in the lanes when the inbox opens, newest first (the router's own <see cref="TicketRouter.InitialRouted"/> win).</summary>
    public IList<RoutedTicket>? Routed { get; set; }
}
