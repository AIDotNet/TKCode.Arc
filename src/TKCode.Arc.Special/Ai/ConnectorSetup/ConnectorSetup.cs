using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A "Connect your tools" card for an AI assistant (Arc Connector setup, design reference). Each app connects inline: its
/// row opens the sign-in, waits for the person to approve and runs a first sync with a progress hairline and a rolling
/// count, then lists the scopes it was granted behind a Connected button and offers Disconnect with an inline
/// confirmation. A search field filters the list by name, description, account and keywords.
/// <para>
/// The OAuth flow is the app's: set <see cref="ConnectHandler"/> to run it, report the steps through the
/// <see cref="ConnectorConnectContext"/> and return the account, scopes and sync count (throw to show the reason with
/// Retry); set <see cref="DisconnectHandler"/> to revoke. Without handlers the flow is simulated and nothing is sent. With
/// no <see cref="Connectors"/> the card lists its <see cref="Simulation"/>'s apps and, once on screen, plays its scripted
/// run; with neither it lists no apps.
/// </para>
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(SearchPart, typeof(SearchField))]
[TemplatePart(ListPart, typeof(StackPanel))]
[TemplatePart(EmptyPart, typeof(TextBlock))]
[TemplatePart(FooterPart, typeof(Control))]
[TemplatePart(FooterContentPart, typeof(ContentPresenter))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
public partial class ConnectorSetup : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string SearchPart = "PART_Search";
    internal const string ListPart = "PART_List";
    internal const string EmptyPart = "PART_Empty";
    internal const string FooterPart = "PART_Footer";
    internal const string FooterContentPart = "PART_FooterContent";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>The apps to list. Null lists the <see cref="Simulation"/>'s apps, or none without one.</summary>
    public static readonly StyledProperty<IReadOnlyList<ConnectorApp>?> ConnectorsProperty =
        AvaloniaProperty.Register<ConnectorSetup, IReadOnlyList<ConnectorApp>?>(nameof(Connectors));

    /// <summary>
    /// The ids of the connected apps (two-way). The card writes the new list after each connect or disconnect finishes.
    /// Null at first connects nothing, or the simulation's connected apps while it plays.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ConnectedIdsProperty =
        AvaloniaProperty.Register<ConnectorSetup, IReadOnlyList<string>?>(nameof(ConnectedIds), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Runs your OAuth flow for an app. Null simulates it (nothing is sent).</summary>
    public static readonly StyledProperty<ConnectorSetupConnectHandler?> ConnectHandlerProperty =
        AvaloniaProperty.Register<ConnectorSetup, ConnectorSetupConnectHandler?>(nameof(ConnectHandler));

    /// <summary>Revokes access after the inline confirmation. Null simulates it.</summary>
    public static readonly StyledProperty<ConnectorSetupDisconnectHandler?> DisconnectHandlerProperty =
        AvaloniaProperty.Register<ConnectorSetup, ConnectorSetupDisconnectHandler?>(nameof(DisconnectHandler));

    /// <summary>
    /// The assistant's name, used in the description, the scopes heading, the confirmation and the footer. Null uses the
    /// simulation's while it plays; with no name the texts say "your assistant".
    /// </summary>
    public static readonly StyledProperty<string?> AssistantNameProperty =
        AvaloniaProperty.Register<ConnectorSetup, string?>(nameof(AssistantName));

    /// <summary>The card's heading; "Connect your tools" when null.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ConnectorSetup, string?>(nameof(Title));

    /// <summary>The line under the heading; built from the assistant's name when null.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ConnectorSetup, string?>(nameof(Description));

    /// <summary>Shows the search field.</summary>
    public static readonly StyledProperty<bool> IsSearchableProperty =
        AvaloniaProperty.Register<ConnectorSetup, bool>(nameof(IsSearchable), true);

    /// <summary>The search field's placeholder; "Search apps" when null.</summary>
    public static readonly StyledProperty<string?> SearchPlaceholderProperty =
        AvaloniaProperty.Register<ConnectorSetup, string?>(nameof(SearchPlaceholder));

    /// <summary>The search text (two-way); it matches name, description, account and keywords.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<ConnectorSetup, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Plays the <see cref="Simulation"/>'s scripted run once the card is on screen. It stops at the first pointer or key
    /// press. Null plays it while the simulation's apps show and there is no connect handler.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<ConnectorSetup, bool?>(nameof(AutoPlay));

    /// <summary>The footer under the list (text or any content); a short privacy note when null.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ConnectorSetup, object?>(nameof(Footer));

    /// <summary>Shows the footer.</summary>
    public static readonly StyledProperty<bool> IsFooterVisibleProperty =
        AvaloniaProperty.Register<ConnectorSetup, bool>(nameof(IsFooterVisible), true);

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<ConnectorSetupLabels> LabelsProperty =
        AvaloniaProperty.Register<ConnectorSetup, ConnectorSetupLabels>(nameof(Labels), ConnectorSetupLabels.Default);

    /// <summary>Raised once a connection completes, with the account, scopes and sync count. Bubbles.</summary>
    public static readonly RoutedEvent<ConnectorConnectedEventArgs> AppConnectedEvent =
        RoutedEvent.Register<ConnectorSetup, ConnectorConnectedEventArgs>(nameof(AppConnected), RoutingStrategies.Bubble);

    /// <summary>Raised once access to an app was revoked. Bubbles.</summary>
    public static readonly RoutedEvent<ConnectorAppEventArgs> AppDisconnectedEvent =
        RoutedEvent.Register<ConnectorSetup, ConnectorAppEventArgs>(nameof(AppDisconnected), RoutingStrategies.Bubble);

    /// <summary>Raised with the new list of connected ids after a connect or disconnect finishes. Bubbles.</summary>
    public static readonly RoutedEvent<ConnectorConnectedIdsChangedEventArgs> ConnectedIdsChangedEvent =
        RoutedEvent.Register<ConnectorSetup, ConnectorConnectedIdsChangedEventArgs>(nameof(ConnectedIdsChanged), RoutingStrategies.Bubble);

    private readonly List<ConnectorSetupRow> _rows = [];
    private TextBlock? _title;
    private TextBlock? _description;
    private SearchField? _search;
    private StackPanel? _list;
    private TextBlock? _empty;
    private Control? _footer;
    private ContentPresenter? _footerContent;
    private ExplainerAnnouncer? _announcer;
    private bool _writingIds;

    static ConnectorSetup()
    {
        FocusableProperty.OverrideDefaultValue<ConnectorSetup>(false);
    }

    /// <summary>Creates the card.</summary>
    public ConnectorSetup()
    {
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
    }

    /// <inheritdoc cref="ConnectorsProperty"/>
    public IReadOnlyList<ConnectorApp>? Connectors { get => GetValue(ConnectorsProperty); set => SetValue(ConnectorsProperty, value); }

    /// <inheritdoc cref="ConnectedIdsProperty"/>
    public IReadOnlyList<string>? ConnectedIds { get => GetValue(ConnectedIdsProperty); set => SetValue(ConnectedIdsProperty, value); }

    /// <inheritdoc cref="ConnectHandlerProperty"/>
    public ConnectorSetupConnectHandler? ConnectHandler { get => GetValue(ConnectHandlerProperty); set => SetValue(ConnectHandlerProperty, value); }

    /// <inheritdoc cref="DisconnectHandlerProperty"/>
    public ConnectorSetupDisconnectHandler? DisconnectHandler { get => GetValue(DisconnectHandlerProperty); set => SetValue(DisconnectHandlerProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string? AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <summary>
    /// The apps the card lists and plays on its own while <see cref="Connectors"/> is null: the assistant's name, the apps,
    /// the ones already connected and the scripted run, played exactly like your own apps would connect (simulated without
    /// a connect handler). Null lists no apps instead.
    /// </summary>
    public static readonly StyledProperty<ConnectorSetupSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ConnectorSetup, ConnectorSetupSimulation?>(nameof(Simulation));

    /// <inheritdoc cref="SimulationProperty"/>
    public ConnectorSetupSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsSearchableProperty"/>
    public bool IsSearchable { get => GetValue(IsSearchableProperty); set => SetValue(IsSearchableProperty, value); }

    /// <inheritdoc cref="SearchPlaceholderProperty"/>
    public string? SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <inheritdoc cref="IsFooterVisibleProperty"/>
    public bool IsFooterVisible { get => GetValue(IsFooterVisibleProperty); set => SetValue(IsFooterVisibleProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ConnectorSetupLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="AppConnectedEvent"/>
    public event EventHandler<ConnectorConnectedEventArgs>? AppConnected
    {
        add => AddHandler(AppConnectedEvent, value);
        remove => RemoveHandler(AppConnectedEvent, value);
    }

    /// <inheritdoc cref="AppDisconnectedEvent"/>
    public event EventHandler<ConnectorAppEventArgs>? AppDisconnected
    {
        add => AddHandler(AppDisconnectedEvent, value);
        remove => RemoveHandler(AppDisconnectedEvent, value);
    }

    /// <inheritdoc cref="ConnectedIdsChangedEvent"/>
    public event EventHandler<ConnectorConnectedIdsChangedEventArgs>? ConnectedIdsChanged
    {
        add => AddHandler(ConnectedIdsChangedEvent, value);
        remove => RemoveHandler(ConnectedIdsChangedEvent, value);
    }

    /// <summary>Whether the card lists the simulation's apps (the app has set no connectors).</summary>
    internal bool ShowsSimulation => Connectors is null && Simulation is not null;

    /// <summary>The assistant's name in the texts: <see cref="AssistantName"/>, else the simulation's while it plays.</summary>
    internal string EffectiveAssistantName => AssistantName ?? (ShowsSimulation ? Simulation!.AssistantName : null) ?? string.Empty;

    /// <summary>Whether connects are simulated (no connect handler).</summary>
    internal bool IsSimulated => ConnectHandler is null;

    /// <summary>The apps listed now: yours, else the simulation's, else none.</summary>
    internal IReadOnlyList<ConnectorApp> EffectiveConnectors =>
        Connectors ?? Simulation?.Apps ?? [];

    /// <summary>Whether the scripted run plays.</summary>
    internal bool EffectiveAutoPlay => ShowsSimulation && (AutoPlay ?? ConnectHandler is null);

    internal IReadOnlyList<ConnectorSetupRow> Rows => _rows;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal SearchField? SearchBox => _search;

    internal TextBlock? EmptyText => _empty;

    internal ContentPresenter? FooterContent => _footerContent;

    /// <summary>Where an app is now, or <see cref="ConnectorPhase.Idle"/> for an unknown id.</summary>
    public ConnectorPhase GetPhase(string id) => Find(id)?.Phase ?? ConnectorPhase.Idle;

    internal ConnectorSetupRow? Find(string id) => _rows.FirstOrDefault(r => r.App.Id == id);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_search is not null)
        {
            _search.PropertyChanged -= OnSearchPropertyChanged;
        }
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _search = e.NameScope.Find<SearchField>(SearchPart);
        _list = e.NameScope.Find<StackPanel>(ListPart);
        _empty = e.NameScope.Find<TextBlock>(EmptyPart);
        _footer = e.NameScope.Find<Control>(FooterPart);
        _footerContent = e.NameScope.Find<ContentPresenter>(FooterContentPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_search is not null)
        {
            _search.Text = SearchText ?? string.Empty;
            _search.PropertyChanged += OnSearchPropertyChanged;
        }
        if (_list is not null)
        {
            AutomationProperties.SetControlTypeOverride(_list, AutomationControlType.List);
            _list.Children.Clear();
            foreach (var row in _rows)
            {
                _list.Children.Add(row);
            }
        }
        if (_rows.Count == 0)
        {
            BuildRows();
        }
        RefreshHeader();
        ApplyFilter();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ConnectorsProperty)
        {
            BuildRows();
            ApplyFilter();
        }
        else if (change.Property == SimulationProperty)
        {
            // Another simulation (or none): its scripted run stops, and an unset Connectors lists the new apps.
            HaltScript();
            _played = false;
            if (Connectors is null)
            {
                BuildRows();
                ApplyFilter();
            }
            RefreshHeader();
            UpdateAutoPlay();
        }
        else if (change.Property == ConnectedIdsProperty)
        {
            if (!_writingIds)
            {
                SyncConnected();
            }
        }
        else if (change.Property == LabelsProperty || change.Property == AssistantNameProperty)
        {
            RefreshHeader();
            foreach (var row in _rows)
            {
                row.Refresh();
            }
            ApplyFilter();
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == SearchPlaceholderProperty
                 || change.Property == IsSearchableProperty || change.Property == FooterProperty || change.Property == IsFooterVisibleProperty)
        {
            RefreshHeader();
        }
        else if (change.Property == SearchTextProperty)
        {
            if (_search is not null && (_search.Text ?? string.Empty) != (SearchText ?? string.Empty))
            {
                _search.Text = SearchText ?? string.Empty;
            }
            ApplyFilter();
        }
        else if (change.Property == AutoPlayProperty || change.Property == ConnectHandlerProperty)
        {
            UpdateAutoPlay();
        }
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && _search is not null)
        {
            var text = _search.Text ?? string.Empty;
            if (text != (SearchText ?? string.Empty))
            {
                SetCurrentValue(SearchTextProperty, text);
            }
        }
    }

    private void RefreshHeader()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title ?? labels.Title;
            AutomationProperties.SetName(this, _title.Text);
        }
        if (_description is not null)
        {
            _description.Text = Description ?? labels.Description(EffectiveAssistantName);
        }
        if (_search is not null)
        {
            var placeholder = SearchPlaceholder ?? labels.SearchPlaceholder;
            _search.Placeholder = placeholder;
            _search.Label = placeholder;
            _search.IsVisible = IsSearchable;
        }
        if (_footer is not null)
        {
            _footer.IsVisible = IsFooterVisible;
        }
        if (_footerContent is not null)
        {
            _footerContent.Content = Footer ?? labels.Footer(EffectiveAssistantName);
        }
    }

    // Rows ----------------------------------------------------------------------------------------------------------------

    private void BuildRows()
    {
        foreach (var row in _rows)
        {
            CancelRow(row);
        }
        _rows.Clear();
        _list?.Children.Clear();
        var connected = InitialConnected();
        foreach (var app in EffectiveConnectors)
        {
            var row = new ConnectorSetupRow(this, app);
            if (connected.Contains(app.Id))
            {
                row.SetConnected(app.Account, [.. app.Scopes], app.SyncCount);
            }
            _rows.Add(row);
            _list?.Children.Add(row);
        }
    }

    private HashSet<string> InitialConnected()
    {
        if (ConnectedIds is { } ids)
        {
            return [.. ids];
        }
        return ShowsSimulation && Simulation!.Connected is { } connected ? [.. connected] : [];
    }

    /// <summary>The app set <see cref="ConnectedIds"/>: rows that are not busy follow it.</summary>
    private void SyncConnected()
    {
        var ids = InitialConnected();
        foreach (var row in _rows)
        {
            if (row.IsBusy || row.Phase == ConnectorPhase.Disconnecting)
            {
                continue;
            }
            var connected = ids.Contains(row.App.Id);
            if (connected && row.Phase != ConnectorPhase.Connected)
            {
                row.SetConnected(row.App.Account, [.. row.App.Scopes], row.App.SyncCount);
            }
            else if (!connected && row.Phase == ConnectorPhase.Connected)
            {
                row.SetIdle();
            }
        }
    }

    /// <summary>The connected ids in list order, written back after a connect or disconnect finished.</summary>
    internal IReadOnlyList<string> CurrentConnectedIds()
    {
        var ids = _rows.Where(r => r.Phase is ConnectorPhase.Connected or ConnectorPhase.Disconnecting).Select(r => r.App.Id).ToList();
        // Ids of apps that are not listed (filtered out by the app) are kept.
        if (ConnectedIds is { } previous)
        {
            foreach (var id in previous)
            {
                if (Find(id) is null && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }
        return ids;
    }

    private void WriteConnectedIds()
    {
        var ids = CurrentConnectedIds();
        _writingIds = true;
        try
        {
            SetCurrentValue(ConnectedIdsProperty, ids);
        }
        finally
        {
            _writingIds = false;
        }
        RaiseEvent(new ConnectorConnectedIdsChangedEventArgs(ConnectedIdsChangedEvent, ids));
    }

    // Search --------------------------------------------------------------------------------------------------------------

    /// <summary>Whether <paramref name="row"/> matches <paramref name="query"/> (name, description, account, keywords).</summary>
    internal static bool Matches(ConnectorSetupRow row, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }
        var q = query.Trim();
        bool Has(string? text) => !string.IsNullOrEmpty(text) && text.Contains(q, StringComparison.OrdinalIgnoreCase);
        var app = row.App;
        return Has(app.Name) || Has(app.Description) || Has(row.Account ?? app.Account) || app.Keywords.Any(Has);
    }

    private void ApplyFilter()
    {
        var query = SearchText ?? string.Empty;
        var visible = 0;
        foreach (var row in _rows)
        {
            row.IsVisible = Matches(row, query);
            if (row.IsVisible)
            {
                visible++;
            }
        }
        var position = 0;
        foreach (var row in _rows)
        {
            if (!row.IsVisible)
            {
                continue;
            }
            position++;
            AutomationProperties.SetPositionInSet(row, position);
            AutomationProperties.SetSizeOfSet(row, visible);
            row.Classes.Set("first", position == 1);
        }
        if (_empty is not null)
        {
            _empty.IsVisible = visible == 0 && _rows.Count > 0;
            _empty.Text = Labels.NoMatches(query.Trim());
        }
    }

    // Announcements -------------------------------------------------------------------------------------------------------

    /// <summary>Says <paramref name="text"/> in the polite live region, noting a simulated run.</summary>
    internal void Announce(string text, bool simulated)
    {
        _announcer?.Announce(simulated ? text + " " + Labels.AnnounceSimulated : text);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAutoPlay();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        HaltScript();
        _viewport?.Dispose();
        _viewport = null;
        ListenForInteraction(false);
        // Unmounting aborts every running flow (Arc: the signal aborts on unmount).
        foreach (var row in _rows)
        {
            if (row.IsBusy)
            {
                CancelRow(row);
                row.SetIdle();
            }
            else if (row.Phase == ConnectorPhase.Disconnecting)
            {
                CancelRow(row);
                row.SetConnected(row.Account, row.Scopes, row.Total);
            }
        }
    }
}
