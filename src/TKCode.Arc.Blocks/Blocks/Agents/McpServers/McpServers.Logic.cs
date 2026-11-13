using System.Collections.Specialized;
using Avalonia;
using TKCode.Arc.Primitives;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class McpServers
{
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private TextBlock? _summary;
    private Button? _add;
    private SpringSizeDecorator? _formHost;
    private McpServersAddForm? _form;
    private StackPanel? _list;
    private Control? _empty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyBody;
    private AiAnnouncer? _announcer;
    private INotifyCollectionChanged? _observedList;
    private readonly Dictionary<McpServer, McpServerRow> _rows = new();
    private readonly Dictionary<McpServer, McpServerStatus> _lastStatus = new();
    private readonly Dictionary<McpServer, CancellationTokenSource> _connections = new();
    private NavigationMethod _lastNavigation = NavigationMethod.Pointer;

    /// <summary>The waits of the simulated connection; tests replace it to step time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (time, token) => Task.Delay(time, token);

    /// <summary>The servers now in the list (empty without a list).</summary>
    internal IReadOnlyList<McpServer> CurrentServers => Servers is { } list ? list.ToArray() : [];

    /// <summary>Whether servers can be removed now.</summary>
    internal bool CanRemove => CanRemoveServers && Servers is { IsReadOnly: false };

    /// <summary>How the person moved last (keyboard draws focus rings, a pointer does not).</summary>
    internal NavigationMethod LastNavigation => _lastNavigation;

    /// <summary>The last polite announcement (tests and tools).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>The row of <paramref name="server"/>, or null.</summary>
    internal McpServerRow? RowOf(McpServer server) => _rows.GetValueOrDefault(server);

    /// <summary>The rows in display order.</summary>
    internal IReadOnlyList<McpServerRow> Rows => _list?.Children.OfType<McpServerRow>().ToArray() ?? [];

    /// <summary>The form (null until it first opens).</summary>
    internal McpServersAddForm? Form => _form;

    /// <summary>The Add server button.</summary>
    internal Button? AddButton => _add;

    /// <summary>The summary line.</summary>
    internal TextBlock? SummaryText => _summary;

    /// <summary>The empty state.</summary>
    internal Control? EmptyState => _empty;

    /// <summary>The form's host (its height opens and closes).</summary>
    internal SpringSizeDecorator? FormHost => _formHost;

    /// <summary>What a server shows: off while it is turned off, otherwise its <see cref="McpServer.Status"/>.</summary>
    internal McpServerStatus EffectiveStatus(McpServer server) => server.IsEnabled ? server.Status : McpServerStatus.Disabled;

    #region Public actions

    /// <summary>Opens the Add server form and focuses its name field.</summary>
    public void OpenAddForm() => SetCurrentValue(IsAddFormOpenProperty, true);

    /// <summary>Closes the Add server form and clears it.</summary>
    public void CloseAddForm() => CloseAddForm(returnFocus: false);

    /// <summary>
    /// Adds a server from <paramref name="draft"/> at the end of the list, raises <see cref="ServerAdded"/> and connects it.
    /// The draft is not checked: the form checks what people type. Returns the new server; on a read-only list, null.
    /// </summary>
    /// <param name="draft">What the server is called and how it is reached.</param>
    public McpServer? AddServer(McpServerDraft draft)
    {
        if (Servers is not { IsReadOnly: false } list)
        {
            return null;
        }
        var server = new McpServer
        {
            Id = McpServersDraftCheck.MakeId(draft.Name, list),
            Name = draft.Name,
            Transport = draft.Transport,
            Url = draft.Url,
            Command = draft.Command,
            Arguments = draft.Arguments,
            Variables = draft.Variables,
            IsEnabled = true,
            Status = McpServerStatus.Connecting,
        };
        list.Add(server);
        if (_observedList is null)
        {
            SyncRows();
        }
        RaiseEvent(new McpServersServerEventArgs(ServerAddedEvent, this, server));
        Announce(Labels.AddedAnnouncement(server.Name));
        _ = Connect(server);
        return server;
    }

    /// <summary>Removes <paramref name="server"/> from the list (cancelling its connection) and raises <see cref="ServerRemoved"/>.</summary>
    /// <param name="server">The server to remove.</param>
    /// <returns>Whether it was removed.</returns>
    public bool RemoveServer(McpServer server)
    {
        if (Servers is not { IsReadOnly: false } list)
        {
            return false;
        }
        var index = list.IndexOf(server);
        if (index < 0)
        {
            return false;
        }
        var hadFocus = RowOf(server)?.IsKeyboardFocusWithin == true;
        CancelConnection(server);
        list.RemoveAt(index);
        if (_observedList is null)
        {
            SyncRows();
        }
        RaiseEvent(new McpServersServerEventArgs(ServerRemovedEvent, this, server));
        Announce(Labels.RemovedAnnouncement(server.Name));
        if (hadFocus)
        {
            var rows = Rows;
            if (rows.Count > 0)
            {
                rows[Math.Min(index, rows.Count - 1)].FocusHeader(_lastNavigation);
            }
            else
            {
                _add?.Focus(_lastNavigation);
            }
        }
        return true;
    }

    /// <summary>
    /// Connects <paramref name="server"/> (again): Connecting, then Connected with the tools it lists (tools it listed before
    /// keep their switches) or Error with the message and log. A server that is off is not connected.
    /// </summary>
    /// <param name="server">The server to connect.</param>
    public async Task Connect(McpServer server)
    {
        if (!server.IsEnabled)
        {
            return;
        }
        CancelConnection(server);
        var cts = new CancellationTokenSource();
        _connections[server] = cts;
        server.Status = McpServerStatus.Connecting;
        Announce(Labels.ConnectingAnnouncement(server.Name));
        var hook = ConnectAsync;
        McpConnectResult result;
        try
        {
            if (hook is not null)
            {
                result = await hook(server, cts.Token);
            }
            else
            {
                await Delay(SimulatedConnectDuration, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                result = McpServersSamples.SimulateConnect(server);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            result = McpConnectResult.Failure(ex.Message);
        }
        if (cts.IsCancellationRequested || !_connections.TryGetValue(server, out var current) || current != cts)
        {
            return;
        }
        _connections.Remove(server);
        cts.Dispose();
        if (!server.IsEnabled)
        {
            return;
        }
        if (result.Succeeded)
        {
            MergeTools(server, result.Tools);
            if (result.Version is not null)
            {
                server.Version = result.Version;
            }
            server.ErrorMessage = null;
            server.ErrorLog = null;
            server.Status = McpServerStatus.Connected;
            Announce(Labels.ConnectedAnnouncement(server.Name, server.Tools.Count));
        }
        else
        {
            server.ErrorMessage = result.ErrorMessage;
            server.ErrorLog = result.ErrorLog;
            server.Status = McpServerStatus.Error;
            Announce(Labels.FailedAnnouncement(server.Name, result.ErrorMessage ?? string.Empty));
        }
    }

    #endregion

    #region Row actions

    internal void ToggleExpanded(McpServer server) =>
        SetCurrentValue(ExpandedServerProperty, ReferenceEquals(ExpandedServer, server) ? null : server);

    internal void SetServerEnabled(McpServer server, bool on)
    {
        if (server.IsEnabled == on)
        {
            return;
        }
        server.IsEnabled = on;
        RaiseEvent(new McpServersServerToggledEventArgs(ServerToggledEvent, this, server, on));
        if (on)
        {
            _ = Connect(server);
        }
        else
        {
            CancelConnection(server);
            server.Status = McpServerStatus.Disabled;
            Announce(Labels.ServerOffAnnouncement(server.Name));
        }
    }

    internal void SetToolEnabled(McpServer server, McpTool tool, bool on)
    {
        if (tool.IsEnabled == on)
        {
            return;
        }
        tool.IsEnabled = on;
        RaiseEvent(new McpServersToolToggledEventArgs(ToolToggledEvent, this, server, [tool], on));
        Announce(Labels.ToolAnnouncement(tool.Name, on));
    }

    internal void SetAllTools(McpServer server, bool on)
    {
        var changed = server.Tools.Where(t => t.IsEnabled != on).ToArray();
        if (changed.Length == 0)
        {
            return;
        }
        foreach (var tool in changed)
        {
            tool.IsEnabled = on;
        }
        RaiseEvent(new McpServersToolToggledEventArgs(ToolToggledEvent, this, server, changed, on));
        Announce(Labels.AllToolsAnnouncement(server.Name, server.Tools.Count, on));
    }

    /// <summary>The form handed a valid draft: add it, close the form and put the focus on the new server.</summary>
    internal McpServer? SubmitDraft(McpServerDraft draft)
    {
        var server = AddServer(draft);
        if (server is null)
        {
            return null;
        }
        var method = _lastNavigation;
        CloseAddForm(returnFocus: false);
        RowOf(server)?.FocusHeader(method);
        return server;
    }

    internal void CloseAddForm(bool returnFocus)
    {
        if (!IsAddFormOpen)
        {
            return;
        }
        if (returnFocus)
        {
            _add?.Focus(_lastNavigation);
        }
        SetCurrentValue(IsAddFormOpenProperty, false);
    }

    /// <summary>A row re-read its server: update the totals and raise <see cref="StatusChanged"/> when the status moved.</summary>
    internal void OnRowStatusSynced(McpServer server)
    {
        var status = server.Status;
        if (_lastStatus.TryGetValue(server, out var previous))
        {
            if (previous != status)
            {
                _lastStatus[server] = status;
                RaiseEvent(new McpServersStatusChangedEventArgs(StatusChangedEvent, this, server, previous, status));
            }
        }
        else
        {
            _lastStatus[server] = status;
        }
        UpdateTotals();
    }

    internal bool FocusNeighbour(McpServerRow row, int step)
    {
        var rows = Rows;
        var index = IndexOf(rows, row);
        if (index < 0)
        {
            return false;
        }
        var next = index + step;
        return next >= 0 && next < rows.Count && rows[next].FocusHeader(NavigationMethod.Directional);
    }

    internal bool FocusEdge(bool first)
    {
        var rows = Rows;
        return rows.Count > 0 && rows[first ? 0 : rows.Count - 1].FocusHeader(NavigationMethod.Directional);
    }

    private static int IndexOf(IReadOnlyList<McpServerRow> rows, McpServerRow row)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row))
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Template and list

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        if (_list is not null)
        {
            _list.Children.Clear();
        }
        if (_formHost is not null)
        {
            _formHost.Child = null;
        }
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        _summary = e.NameScope.Find<TextBlock>(SummaryPart);
        _add = e.NameScope.Find<Button>(AddPart);
        _formHost = e.NameScope.Find<SpringSizeDecorator>(FormHostPart);
        _list = e.NameScope.Find<StackPanel>(ListPart);
        _empty = e.NameScope.Find<Control>(EmptyPart);
        _emptyTitle = e.NameScope.Find<TextBlock>(EmptyTitlePart);
        _emptyBody = e.NameScope.Find<TextBlock>(EmptyBodyPart);
        _announcer = e.NameScope.Find<AiAnnouncer>(AnnouncerPart);
        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        if (_form is not null && _formHost is not null)
        {
            _formHost.Child = _form;
        }
        foreach (var row in _rows.Values)
        {
            row.Detach();
        }
        _rows.Clear();
        SyncRows();
        SyncText();
        SyncForm(focus: false);
        UpdatePseudoClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ServersProperty)
        {
            ObserveList(change.GetNewValue<IList<McpServer>?>());
            SyncRows();
        }
        else if (property == ExpandedServerProperty)
        {
            foreach (var row in _rows.Values)
            {
                row.SetExpanded(ReferenceEquals(row.Server, ExpandedServer));
            }
        }
        else if (property == IsAddFormOpenProperty)
        {
            SyncForm(focus: change.GetNewValue<bool>());
            UpdatePseudoClasses();
        }
        else if (property == LabelsProperty || property == TitleProperty || property == SubtitleProperty)
        {
            SyncText();
            if (property == LabelsProperty)
            {
                _form?.SyncLabels();
                foreach (var row in _rows.Values)
                {
                    row.SyncAll();
                }
            }
        }
        else if (property == CanAddServersProperty || property == CanRemoveServersProperty)
        {
            SyncText();
            foreach (var row in _rows.Values)
            {
                row.SyncAll();
            }
        }
        else if (property == BoundsProperty || property == CompactWidthProperty)
        {
            IsCompact = Bounds.Width > 0 && Bounds.Width <= CompactWidth;
            UpdatePseudoClasses();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var server in _connections.Keys.ToArray())
        {
            // A connection that was running when the block left cannot finish: it shows as failed to reconnect later.
            CancelConnection(server);
            if (server.Status == McpServerStatus.Connecting)
            {
                server.Status = McpServerStatus.Error;
            }
        }
    }

    static McpServers()
    {
        // Route the input modality before the parts handle it.
        KeyDownEvent.AddClassHandler<McpServers>((o, _) => o._lastNavigation = NavigationMethod.Tab, RoutingStrategies.Tunnel);
        PointerPressedEvent.AddClassHandler<McpServers>((o, _) => o._lastNavigation = NavigationMethod.Pointer, RoutingStrategies.Tunnel);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(IsAddFormOpenProperty, !IsAddFormOpen);
    }

    private void ObserveList(IList<McpServer>? list)
    {
        if (_observedList is not null)
        {
            _observedList.CollectionChanged -= OnListChanged;
        }
        _observedList = list as INotifyCollectionChanged;
        if (_observedList is not null)
        {
            _observedList.CollectionChanged += OnListChanged;
        }
    }

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncRows();

    /// <summary>Puts one row per server in list order, reusing the rows of servers that stay.</summary>
    private void SyncRows()
    {
        if (_observedList is null && Servers is INotifyCollectionChanged)
        {
            ObserveList(Servers);
        }
        var servers = CurrentServers;
        var wanted = new HashSet<McpServer>(servers);
        foreach (var gone in _rows.Keys.Where(s => !wanted.Contains(s)).ToArray())
        {
            _rows[gone].Detach();
            _rows.Remove(gone);
            _lastStatus.Remove(gone);
            CancelConnection(gone);
        }
        foreach (var gone in _lastStatus.Keys.Where(s => !wanted.Contains(s)).ToArray())
        {
            _lastStatus.Remove(gone);
        }
        if (ExpandedServer is { } expanded && !wanted.Contains(expanded))
        {
            SetCurrentValue(ExpandedServerProperty, null);
        }
        if (_list is null)
        {
            UpdateTotals();
            return;
        }
        _list.Children.Clear();
        foreach (var server in servers)
        {
            if (!_rows.TryGetValue(server, out var row))
            {
                row = new McpServerRow(this, server);
                _rows[server] = row;
            }
            row.SetExpanded(ReferenceEquals(server, ExpandedServer));
            _list.Children.Add(row);
        }
        UpdateTotals();
        UpdatePseudoClasses();
    }

    private void SyncText()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title ?? labels.Title;
        }
        if (_subtitle is not null)
        {
            var subtitle = Subtitle ?? labels.Subtitle;
            _subtitle.Text = subtitle;
            _subtitle.IsVisible = subtitle.Length > 0;
        }
        if (_add is not null)
        {
            _add.Content = labels.AddServer;
            AutomationProperties.SetName(_add, labels.AddServer);
            _add.IsVisible = CanAddServers && Servers is { IsReadOnly: false };
        }
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = labels.EmptyTitle;
        }
        if (_emptyBody is not null)
        {
            _emptyBody.Text = labels.EmptyBody;
        }
        UpdateTotals();
    }

    private void SyncForm(bool focus)
    {
        if (_formHost is null)
        {
            return;
        }
        if (IsAddFormOpen)
        {
            if (_form is null)
            {
                _form = new McpServersAddForm(this);
                _formHost.Child = _form;
            }
            _formHost.IsExpanded = true;
            if (focus)
            {
                var method = _lastNavigation;
                Dispatcher.UIThread.Post(() => _form?.FocusFirst(method), DispatcherPriority.Loaded);
            }
        }
        else
        {
            _formHost.IsExpanded = false;
            _form?.Reset();
        }
    }

    private void UpdateTotals()
    {
        var connected = 0;
        var tools = 0;
        var count = 0;
        foreach (var server in CurrentServers)
        {
            count++;
            if (EffectiveStatus(server) == McpServerStatus.Connected)
            {
                connected++;
                tools += server.EnabledToolCount;
            }
        }
        ConnectedCount = connected;
        EnabledToolCount = tools;
        if (_summary is not null)
        {
            _summary.Text = count > 0 ? Labels.Summary(connected, count, tools) : null;
            _summary.IsVisible = count > 0;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(CompactPseudoClass, IsCompact);
        PseudoClasses.Set(EmptyPseudoClass, CurrentServers.Count == 0);
        PseudoClasses.Set(AddingPseudoClass, IsAddFormOpen);
        _add?.SetValue(Button.VariantProperty, IsAddFormOpen ? ButtonVariant.Ghost : ButtonVariant.Secondary);
    }

    #endregion

    private void CancelConnection(McpServer server)
    {
        if (_connections.Remove(server, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void MergeTools(McpServer server, IReadOnlyList<McpTool> listed)
    {
        var previous = new Dictionary<string, McpTool>(StringComparer.Ordinal);
        foreach (var tool in server.Tools)
        {
            previous.TryAdd(tool.Name, tool);
        }
        var merged = new List<McpTool>(listed.Count);
        foreach (var tool in listed)
        {
            if (previous.TryGetValue(tool.Name, out var kept))
            {
                kept.Description = tool.Description;
                kept.ChangesData = tool.ChangesData;
                merged.Add(kept);
            }
            else
            {
                merged.Add(tool);
            }
        }
        if (merged.SequenceEqual(server.Tools))
        {
            return;
        }
        server.Tools.Clear();
        server.Tools.AddRange(merged);
    }

    private void Announce(string text) => _announcer?.Announce(text);
}
