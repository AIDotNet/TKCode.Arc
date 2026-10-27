using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A directory of the apps a workspace can connect (Arc Integrations, design reference): a heading and a search field, a
/// row of filters (All, Connected and one per category, each with its count), and one row per app with its mark, name,
/// category, description and a status pill. Connect runs <see cref="ConnectAction"/> (the pill spins, then turns into a
/// green "Connected" and a settings button slides in; a failure shows Retry). The name or the settings button opens a
/// drawer with the permissions, the sync frequency and a hold-to-disconnect button. Search and filters reflow the list:
/// leaving rows fade where they were, the others glide to their place and the list's height follows.
/// <code>
/// &lt;arc:Integrations&gt;
///     &lt;arc:Integrations.Categories&gt;
///         &lt;arc:IntegrationCategory Key="files" Label="Files" /&gt;
///     &lt;/arc:Integrations.Categories&gt;
///     &lt;arc:IntegrationItem Key="vaultline" Name="Vaultline" Category="files" Description="Mirror design files into boards." /&gt;
/// &lt;/arc:Integrations&gt;
/// </code>
/// </summary>
public partial class Integrations : TemplatedControl
{
    /// <summary>The <see cref="SelectedFilter"/> that shows every app.</summary>
    public const string AllFilter = IntegrationsQuery.All;

    /// <summary>The <see cref="SelectedFilter"/> that shows the connected apps.</summary>
    public const string ConnectedFilter = IntegrationsQuery.Connected;

    /// <summary>The English words used when no <see cref="Labels"/> are given (declared first: the property default uses it).</summary>
    internal static readonly IntegrationLabels DefaultLabels = new();

    #region Properties

    /// <summary>The heading; null shows <see cref="IntegrationLabels.Title"/>.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Integrations, string?>(nameof(Title));

    /// <summary>The line under the heading; null shows <see cref="IntegrationLabels.Subtitle"/>, an empty string hides it.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<Integrations, string?>(nameof(Subtitle));

    /// <summary>The apps, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Items"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Integrations, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The checked filter (two-way): <see cref="AllFilter"/>, <see cref="ConnectedFilter"/> or a category key.</summary>
    public static readonly StyledProperty<string> SelectedFilterProperty =
        AvaloniaProperty.Register<Integrations, string>(nameof(SelectedFilter), AllFilter, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search text (two-way); it matches names, category labels and descriptions, ignoring case.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<Integrations, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the Connected filter is offered (between All and the categories).</summary>
    public static readonly StyledProperty<bool> ShowConnectedFilterProperty =
        AvaloniaProperty.Register<Integrations, bool>(nameof(ShowConnectedFilter), true);

    /// <summary>Whether the search field is shown.</summary>
    public static readonly StyledProperty<bool> ShowSearchProperty =
        AvaloniaProperty.Register<Integrations, bool>(nameof(ShowSearch), true);

    /// <summary>
    /// Connects an app: awaited while its pill spins, with the request and a token that <see cref="CancelConnect"/> (or the
    /// block leaving the window) cancels. Return <see cref="IntegrationConnectResult.Connected"/> or
    /// <see cref="IntegrationConnectResult.Failed"/>; throwing fails too, a cancellation returns the app to idle. Null
    /// waits <see cref="SimulatedConnectDuration"/> and succeeds (a demo).
    /// </summary>
    public static readonly StyledProperty<Func<IntegrationConnectRequest, CancellationToken, Task<IntegrationConnectResult>>?> ConnectActionProperty =
        AvaloniaProperty.Register<Integrations, Func<IntegrationConnectRequest, CancellationToken, Task<IntegrationConnectResult>>?>(nameof(ConnectAction));

    /// <summary>
    /// Disconnects an app after the hold completed (or <see cref="DisconnectAsync"/>); throwing keeps it connected. Null
    /// disconnects at once.
    /// </summary>
    public static readonly StyledProperty<Func<IntegrationItem, CancellationToken, Task>?> DisconnectActionProperty =
        AvaloniaProperty.Register<Integrations, Func<IntegrationItem, CancellationToken, Task>?>(nameof(DisconnectAction));

    /// <summary>Run when a connect starts, with the <see cref="IntegrationItem"/> as the parameter; when it cannot execute the connect does not start.</summary>
    public static readonly StyledProperty<ICommand?> ConnectCommandProperty =
        AvaloniaProperty.Register<Integrations, ICommand?>(nameof(ConnectCommand));

    /// <summary>Run when an app was disconnected, with the <see cref="IntegrationItem"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> DisconnectCommandProperty =
        AvaloniaProperty.Register<Integrations, ICommand?>(nameof(DisconnectCommand));

    /// <summary>How long a connect takes when <see cref="ConnectAction"/> is null (1.4 s, as measured).</summary>
    public static readonly StyledProperty<TimeSpan> SimulatedConnectDurationProperty =
        AvaloniaProperty.Register<Integrations, TimeSpan>(nameof(SimulatedConnectDuration), DeveloperSprings.SimulatedConnect);

    /// <summary>How long the disconnect button reads "Unlinked" before the app returns to idle (660 ms, as measured).</summary>
    public static readonly StyledProperty<TimeSpan> DisconnectSettleDelayProperty =
        AvaloniaProperty.Register<Integrations, TimeSpan>(nameof(DisconnectSettleDelay), DeveloperSprings.DisconnectSettle);

    /// <summary>Turns a failed connect's exception into the drawer's line; null uses the exception's message.</summary>
    public static readonly StyledProperty<Func<Exception, string>?> ErrorFormatterProperty =
        AvaloniaProperty.Register<Integrations, Func<Exception, string>?>(nameof(ErrorFormatter));

    /// <summary>Every word it shows or announces (English by default).</summary>
    public static readonly StyledProperty<IntegrationLabels> LabelsProperty =
        AvaloniaProperty.Register<Integrations, IntegrationLabels>(nameof(Labels), DefaultLabels);

    /// <summary>Draws a bordered card with rounded corners (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<Integrations, bool>(nameof(IsFramed), true);

    /// <summary>How many apps are connected.</summary>
    public static readonly DirectProperty<Integrations, int> ConnectedCountProperty =
        AvaloniaProperty.RegisterDirect<Integrations, int>(nameof(ConnectedCount), o => o.ConnectedCount);

    /// <summary>How many apps the list shows (filter and search applied).</summary>
    public static readonly DirectProperty<Integrations, int> VisibleCountProperty =
        AvaloniaProperty.RegisterDirect<Integrations, int>(nameof(VisibleCount), o => o.VisibleCount);

    /// <summary>The app whose drawer is open; null while it is closed.</summary>
    public static readonly DirectProperty<Integrations, IntegrationItem?> OpenItemProperty =
        AvaloniaProperty.RegisterDirect<Integrations, IntegrationItem?>(nameof(OpenItem), o => o.OpenItem);

    /// <summary>True at 480 wide or less: rows stack their text, filters scroll, the settings button hides.</summary>
    public static readonly DirectProperty<Integrations, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<Integrations, bool>(nameof(IsCompact), o => o.IsCompact);

    /// <summary>Below this width the search moves under the heading (set by the theme from the token: 720).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<Integrations, double>(nameof(NarrowWidth), 720);

    /// <summary>At or below this width paddings tighten (520).</summary>
    internal static readonly StyledProperty<double> TightWidthProperty =
        AvaloniaProperty.Register<Integrations, double>(nameof(TightWidth), 520);

    /// <summary>At or below this width the compact layout applies (480).</summary>
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<Integrations, double>(nameof(CompactWidth), 480);

    #endregion

    #region Events

    /// <summary>Raised before a connect starts; setting <see cref="IntegrationConnectRequestedEventArgs.Cancel"/> keeps it from starting.</summary>
    public static readonly RoutedEvent<IntegrationConnectRequestedEventArgs> ConnectRequestedEvent =
        RoutedEvent.Register<Integrations, IntegrationConnectRequestedEventArgs>(nameof(ConnectRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when an app's status changes, by the block or by code.</summary>
    public static readonly RoutedEvent<IntegrationStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<Integrations, IntegrationStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the checked filter changes.</summary>
    public static readonly RoutedEvent<IntegrationFilterChangedEventArgs> FilterChangedEvent =
        RoutedEvent.Register<Integrations, IntegrationFilterChangedEventArgs>(nameof(FilterChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the search text changes.</summary>
    public static readonly RoutedEvent<IntegrationSearchChangedEventArgs> SearchChangedEvent =
        RoutedEvent.Register<Integrations, IntegrationSearchChangedEventArgs>(nameof(SearchChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person picks another sync frequency in the drawer.</summary>
    public static readonly RoutedEvent<IntegrationSyncFrequencyChangedEventArgs> SyncFrequencyChangedEvent =
        RoutedEvent.Register<Integrations, IntegrationSyncFrequencyChangedEventArgs>(nameof(SyncFrequencyChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when an app's drawer opens.</summary>
    public static readonly RoutedEvent<IntegrationEventArgs> ConfigurationOpenedEvent =
        RoutedEvent.Register<Integrations, IntegrationEventArgs>(nameof(ConfigurationOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when the drawer closes.</summary>
    public static readonly RoutedEvent<IntegrationEventArgs> ConfigurationClosedEvent =
        RoutedEvent.Register<Integrations, IntegrationEventArgs>(nameof(ConfigurationClosed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ConnectRequestedEvent"/>
    public event EventHandler<IntegrationConnectRequestedEventArgs>? ConnectRequested
    {
        add => AddHandler(ConnectRequestedEvent, value);
        remove => RemoveHandler(ConnectRequestedEvent, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<IntegrationStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    /// <inheritdoc cref="FilterChangedEvent"/>
    public event EventHandler<IntegrationFilterChangedEventArgs>? FilterChanged
    {
        add => AddHandler(FilterChangedEvent, value);
        remove => RemoveHandler(FilterChangedEvent, value);
    }

    /// <inheritdoc cref="SearchChangedEvent"/>
    public event EventHandler<IntegrationSearchChangedEventArgs>? SearchChanged
    {
        add => AddHandler(SearchChangedEvent, value);
        remove => RemoveHandler(SearchChangedEvent, value);
    }

    /// <inheritdoc cref="SyncFrequencyChangedEvent"/>
    public event EventHandler<IntegrationSyncFrequencyChangedEventArgs>? SyncFrequencyChanged
    {
        add => AddHandler(SyncFrequencyChangedEvent, value);
        remove => RemoveHandler(SyncFrequencyChangedEvent, value);
    }

    /// <inheritdoc cref="ConfigurationOpenedEvent"/>
    public event EventHandler<IntegrationEventArgs>? ConfigurationOpened
    {
        add => AddHandler(ConfigurationOpenedEvent, value);
        remove => RemoveHandler(ConfigurationOpenedEvent, value);
    }

    /// <inheritdoc cref="ConfigurationClosedEvent"/>
    public event EventHandler<IntegrationEventArgs>? ConfigurationClosed
    {
        add => AddHandler(ConfigurationClosedEvent, value);
        remove => RemoveHandler(ConfigurationClosedEvent, value);
    }

    #endregion

    private int _connectedCount;
    private int _visibleCount;
    private IntegrationItem? _openItem;
    private bool _isCompact;

    /// <summary>The apps, in display order, when no <see cref="ItemsSource"/> is set. Connecting does not reorder them.</summary>
    [Content]
    public AvaloniaList<IntegrationItem> Items { get; } = new();

    /// <summary>The categories, in filter order (after All and Connected).</summary>
    public AvaloniaList<IntegrationCategory> Categories { get; } = new();

    /// <summary>The choices of the drawer's sync frequency menu; empty hides the menu.</summary>
    public AvaloniaList<IntegrationSyncOption> SyncOptions { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="SelectedFilterProperty"/>
    public string SelectedFilter { get => GetValue(SelectedFilterProperty); set => SetValue(SelectedFilterProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="ShowConnectedFilterProperty"/>
    public bool ShowConnectedFilter { get => GetValue(ShowConnectedFilterProperty); set => SetValue(ShowConnectedFilterProperty, value); }

    /// <inheritdoc cref="ShowSearchProperty"/>
    public bool ShowSearch { get => GetValue(ShowSearchProperty); set => SetValue(ShowSearchProperty, value); }

    /// <inheritdoc cref="ConnectActionProperty"/>
    public Func<IntegrationConnectRequest, CancellationToken, Task<IntegrationConnectResult>>? ConnectAction
    {
        get => GetValue(ConnectActionProperty);
        set => SetValue(ConnectActionProperty, value);
    }

    /// <inheritdoc cref="DisconnectActionProperty"/>
    public Func<IntegrationItem, CancellationToken, Task>? DisconnectAction
    {
        get => GetValue(DisconnectActionProperty);
        set => SetValue(DisconnectActionProperty, value);
    }

    /// <inheritdoc cref="ConnectCommandProperty"/>
    public ICommand? ConnectCommand { get => GetValue(ConnectCommandProperty); set => SetValue(ConnectCommandProperty, value); }

    /// <inheritdoc cref="DisconnectCommandProperty"/>
    public ICommand? DisconnectCommand { get => GetValue(DisconnectCommandProperty); set => SetValue(DisconnectCommandProperty, value); }

    /// <inheritdoc cref="SimulatedConnectDurationProperty"/>
    public TimeSpan SimulatedConnectDuration
    {
        get => GetValue(SimulatedConnectDurationProperty);
        set => SetValue(SimulatedConnectDurationProperty, value);
    }

    /// <inheritdoc cref="DisconnectSettleDelayProperty"/>
    public TimeSpan DisconnectSettleDelay
    {
        get => GetValue(DisconnectSettleDelayProperty);
        set => SetValue(DisconnectSettleDelayProperty, value);
    }

    /// <inheritdoc cref="ErrorFormatterProperty"/>
    public Func<Exception, string>? ErrorFormatter { get => GetValue(ErrorFormatterProperty); set => SetValue(ErrorFormatterProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public IntegrationLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="ConnectedCountProperty"/>
    public int ConnectedCount { get => _connectedCount; private set => SetAndRaise(ConnectedCountProperty, ref _connectedCount, value); }

    /// <inheritdoc cref="VisibleCountProperty"/>
    public int VisibleCount { get => _visibleCount; private set => SetAndRaise(VisibleCountProperty, ref _visibleCount, value); }

    /// <inheritdoc cref="OpenItemProperty"/>
    public IntegrationItem? OpenItem { get => _openItem; private set => SetAndRaise(OpenItemProperty, ref _openItem, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double TightWidth { get => GetValue(TightWidthProperty); set => SetValue(TightWidthProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
}
