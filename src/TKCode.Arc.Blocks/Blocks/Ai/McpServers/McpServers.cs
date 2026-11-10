using Avalonia;
using TKCode.Arc.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A card to manage the tool servers (MCP) an assistant can call. Each server is a row with its mark, name, transport tag
/// (HTTP or Local), a live status line (connected with its tool count, connecting, couldn't connect, off) and a switch
/// that turns it on or off. Opening a row shows why it failed, with the log and Retry; its tools, each with a switch
/// (Enable all and Disable all for the lot, a "Changes data" tag on tools that write); its address or command, header or
/// variable keys and version; and Reconnect and Remove with a confirmation. Add server opens a form for a URL or a command
/// with its arguments and optional headers or environment variables; the new server joins the list and connects.
/// <para>
/// The servers are <see cref="McpServer"/> objects in <see cref="Servers"/> (four invented samples by default; give your
/// own list, an <c>ObservableCollection</c> works, and the block adds and removes on it). Connections go through
/// <see cref="ConnectAsync"/>; without it the block simulates one (<see cref="McpServersSamples.SimulateConnect"/>).
/// Every change is reported by an event, and read out by a polite live region.
/// </para>
/// <code language="xml">
/// &lt;arc:McpServers Servers="{Binding ToolServers}" ConnectAsync="{Binding Connect}" /&gt;
/// </code>
/// </summary>
[TemplatePart(AddPart, typeof(Button))]
[TemplatePart(FormHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(ListPart, typeof(Avalonia.Controls.StackPanel))]
[TemplatePart(EmptyPart, typeof(Avalonia.Controls.Control))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(CompactPseudoClass, EmptyPseudoClass, AddingPseudoClass)]
public partial class McpServers : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string SummaryPart = "PART_Summary";
    public const string AddPart = "PART_Add";
    public const string FormHostPart = "PART_FormHost";
    public const string ListPart = "PART_List";
    public const string EmptyPart = "PART_Empty";
    public const string EmptyTitlePart = "PART_EmptyTitle";
    public const string EmptyBodyPart = "PART_EmptyBody";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the block is at most <c>CompactWidth</c> (520) wide: paddings tighten.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the list has no servers.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while the Add server form is open.</summary>
    public const string AddingPseudoClass = ":adding";

    /// <summary>The English words (declared before the property that uses it as its default).</summary>
    internal static readonly McpServersLabels DefaultLabels = new();

    #region Properties

    /// <summary>The heading; null shows <see cref="McpServersLabels.Title"/>.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<McpServers, string?>(nameof(Title));

    /// <summary>The line under the heading; null shows <see cref="McpServersLabels.Subtitle"/>, an empty string hides it.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<McpServers, string?>(nameof(Subtitle));

    /// <summary>
    /// The servers, in display order. Default: a fresh copy of <see cref="McpServersSamples.Servers"/> per block. The block
    /// adds and removes servers on this list (when it is not read-only) and follows its changes when it raises them.
    /// </summary>
    public static readonly StyledProperty<IList<McpServer>?> ServersProperty =
        AvaloniaProperty.Register<McpServers, IList<McpServer>?>(nameof(Servers));

    /// <summary>The open server (two-way); one at a time, null when all are closed.</summary>
    public static readonly StyledProperty<McpServer?> ExpandedServerProperty =
        AvaloniaProperty.Register<McpServers, McpServer?>(nameof(ExpandedServer), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the Add server form is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsAddFormOpenProperty =
        AvaloniaProperty.Register<McpServers, bool>(nameof(IsAddFormOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Connects a server and lists its tools: awaited while the server shows Connecting, with a token cancelled when the
    /// server is turned off, removed, connected again or the block leaves the window. Return
    /// <see cref="McpConnectResult.Success"/> or <see cref="McpConnectResult.Failure"/>; throwing fails with the exception's
    /// message. Null waits <see cref="SimulatedConnectDuration"/> and uses <see cref="McpServersSamples.SimulateConnect"/>.
    /// </summary>
    public static readonly StyledProperty<Func<McpServer, CancellationToken, Task<McpConnectResult>>?> ConnectAsyncProperty =
        AvaloniaProperty.Register<McpServers, Func<McpServer, CancellationToken, Task<McpConnectResult>>?>(nameof(ConnectAsync));

    /// <summary>How long a simulated connection takes when <see cref="ConnectAsync"/> is null (1.2 s).</summary>
    public static readonly StyledProperty<TimeSpan> SimulatedConnectDurationProperty =
        AvaloniaProperty.Register<McpServers, TimeSpan>(nameof(SimulatedConnectDuration), McpServersSamples.SimulatedConnectTime);

    /// <summary>Whether the Add server button is offered (default true; a read-only list never offers it).</summary>
    public static readonly StyledProperty<bool> CanAddServersProperty =
        AvaloniaProperty.Register<McpServers, bool>(nameof(CanAddServers), true);

    /// <summary>Whether open servers offer Remove (default true; a read-only list never offers it).</summary>
    public static readonly StyledProperty<bool> CanRemoveServersProperty =
        AvaloniaProperty.Register<McpServers, bool>(nameof(CanRemoveServers), true);

    /// <summary>Every word the block shows or reads out (English by default).</summary>
    public static readonly StyledProperty<McpServersLabels> LabelsProperty =
        AvaloniaProperty.Register<McpServers, McpServersLabels>(nameof(Labels), DefaultLabels);

    /// <summary>How many servers are on and connected.</summary>
    public static readonly DirectProperty<McpServers, int> ConnectedCountProperty =
        AvaloniaProperty.RegisterDirect<McpServers, int>(nameof(ConnectedCount), o => o.ConnectedCount);

    /// <summary>How many tools are enabled on the connected servers: what the assistant can call now.</summary>
    public static readonly DirectProperty<McpServers, int> EnabledToolCountProperty =
        AvaloniaProperty.RegisterDirect<McpServers, int>(nameof(EnabledToolCount), o => o.EnabledToolCount);

    /// <summary>True while the block is at most 520 wide (the compact paddings).</summary>
    public static readonly DirectProperty<McpServers, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<McpServers, bool>(nameof(IsCompact), o => o.IsCompact);

    /// <summary>At or below this width the compact layout applies (set by the theme from the token: 520).</summary>
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<McpServers, double>(nameof(CompactWidth), 520);

    /// <summary>The padding of an open server's body (set by the theme; tighter when compact).</summary>
    internal static readonly StyledProperty<Thickness> BodyPaddingProperty =
        AvaloniaProperty.Register<McpServers, Thickness>(nameof(BodyPadding));

    /// <summary>The padding of the Add server form (set by the theme; tighter when compact).</summary>
    internal static readonly StyledProperty<Thickness> FormPaddingProperty =
        AvaloniaProperty.Register<McpServers, Thickness>(nameof(FormPadding));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="ServersProperty"/>
    public IList<McpServer>? Servers { get => GetValue(ServersProperty); set => SetValue(ServersProperty, value); }

    /// <inheritdoc cref="ExpandedServerProperty"/>
    public McpServer? ExpandedServer { get => GetValue(ExpandedServerProperty); set => SetValue(ExpandedServerProperty, value); }

    /// <inheritdoc cref="IsAddFormOpenProperty"/>
    public bool IsAddFormOpen { get => GetValue(IsAddFormOpenProperty); set => SetValue(IsAddFormOpenProperty, value); }

    /// <inheritdoc cref="ConnectAsyncProperty"/>
    public Func<McpServer, CancellationToken, Task<McpConnectResult>>? ConnectAsync
    {
        get => GetValue(ConnectAsyncProperty);
        set => SetValue(ConnectAsyncProperty, value);
    }

    /// <inheritdoc cref="SimulatedConnectDurationProperty"/>
    public TimeSpan SimulatedConnectDuration
    {
        get => GetValue(SimulatedConnectDurationProperty);
        set => SetValue(SimulatedConnectDurationProperty, value);
    }

    /// <inheritdoc cref="CanAddServersProperty"/>
    public bool CanAddServers { get => GetValue(CanAddServersProperty); set => SetValue(CanAddServersProperty, value); }

    /// <inheritdoc cref="CanRemoveServersProperty"/>
    public bool CanRemoveServers { get => GetValue(CanRemoveServersProperty); set => SetValue(CanRemoveServersProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public McpServersLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ConnectedCountProperty"/>
    public int ConnectedCount { get => _connectedCount; private set => SetAndRaise(ConnectedCountProperty, ref _connectedCount, value); }

    /// <inheritdoc cref="EnabledToolCountProperty"/>
    public int EnabledToolCount { get => _enabledToolCount; private set => SetAndRaise(EnabledToolCountProperty, ref _enabledToolCount, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal Thickness BodyPadding { get => GetValue(BodyPaddingProperty); set => SetValue(BodyPaddingProperty, value); }

    internal Thickness FormPadding { get => GetValue(FormPaddingProperty); set => SetValue(FormPaddingProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when a server joined the list from the form or <see cref="AddServer"/> (before it connects).</summary>
    public static readonly RoutedEvent<McpServersServerEventArgs> ServerAddedEvent =
        RoutedEvent.Register<McpServers, McpServersServerEventArgs>(nameof(ServerAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when a server left the list after its removal was confirmed, or by <see cref="RemoveServer"/>.</summary>
    public static readonly RoutedEvent<McpServersServerEventArgs> ServerRemovedEvent =
        RoutedEvent.Register<McpServers, McpServersServerEventArgs>(nameof(ServerRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when a server's switch turned it on or off.</summary>
    public static readonly RoutedEvent<McpServersServerToggledEventArgs> ServerToggledEvent =
        RoutedEvent.Register<McpServers, McpServersServerToggledEventArgs>(nameof(ServerToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when tools were turned on or off: one by its switch, or several by Enable all or Disable all.</summary>
    public static readonly RoutedEvent<McpServersToolToggledEventArgs> ToolToggledEvent =
        RoutedEvent.Register<McpServers, McpServersToolToggledEventArgs>(nameof(ToolToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when a server's <see cref="McpServer.Status"/> changed, by the block or by your code.</summary>
    public static readonly RoutedEvent<McpServersStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<McpServers, McpServersStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ServerAddedEvent"/>
    public event EventHandler<McpServersServerEventArgs>? ServerAdded
    {
        add => AddHandler(ServerAddedEvent, value);
        remove => RemoveHandler(ServerAddedEvent, value);
    }

    /// <inheritdoc cref="ServerRemovedEvent"/>
    public event EventHandler<McpServersServerEventArgs>? ServerRemoved
    {
        add => AddHandler(ServerRemovedEvent, value);
        remove => RemoveHandler(ServerRemovedEvent, value);
    }

    /// <inheritdoc cref="ServerToggledEvent"/>
    public event EventHandler<McpServersServerToggledEventArgs>? ServerToggled
    {
        add => AddHandler(ServerToggledEvent, value);
        remove => RemoveHandler(ServerToggledEvent, value);
    }

    /// <inheritdoc cref="ToolToggledEvent"/>
    public event EventHandler<McpServersToolToggledEventArgs>? ToolToggled
    {
        add => AddHandler(ToolToggledEvent, value);
        remove => RemoveHandler(ToolToggledEvent, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<McpServersStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    #endregion

    private int _connectedCount;
    private int _enabledToolCount;
    private bool _isCompact;

    /// <summary>Creates the block with its own copy of the sample servers.</summary>
    public McpServers()
    {
        SetCurrentValue(ServersProperty, McpServersSamples.Servers());
    }
}
