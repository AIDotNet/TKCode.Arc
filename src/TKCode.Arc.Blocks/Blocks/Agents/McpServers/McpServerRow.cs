using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One server of <see cref="McpServers"/>: a header button (mark, name, transport tag, live status line, chevron) with
/// the server's switch beside it, and a body that opens under it on a height spring. The body is built the first time
/// the row opens: the error box with the log and Retry, the tools with their switches and Enable all, the details
/// (address or command, header or variable keys, version), and Reconnect and Remove with an inline confirmation.
/// The row follows its <see cref="McpServer"/> and its tools; every action goes to the owner, which changes the data
/// and raises the events.
/// </summary>
[PseudoClasses(OpenPseudoClass)]
internal sealed class McpServerRow : Border
{
    public const string OpenPseudoClass = ":open";

    private readonly McpServers _owner;
    private readonly McpServerRowHeader _header;
    private readonly Switch _switch;
    private readonly TextBlock _mark;
    private readonly TextBlock _name;
    private readonly Border _tag;
    private readonly McpServerStatusDot _dot;
    private readonly ArcContentSwap _status;
    private readonly TextBlock _endpoint;
    private readonly SpringSizeDecorator _bodyHost;
    private readonly Dictionary<McpTool, McpServerToolRow> _toolRows = new();
    private string? _statusText;
    private bool _syncing;
    private bool _isExpanded;
    private bool _isLogOpen;
    private bool _isConfirming;

    // The body, built on the first open.
    private StackPanel? _body;
    private Border? _errorBox;
    private TextBlock? _errorMessage;
    private Button? _retry;
    private Button? _logToggle;
    private ScrollViewer? _logHost;
    private SelectableTextBlock? _log;
    private TextBlock? _disabledNote;
    private TextBlock? _toolsCount;
    private Button? _toggleAll;
    private StackPanel? _toolList;
    private TextBlock? _noTools;
    private StackPanel? _fetching;
    private Grid? _details;
    private WrapPanel? _actions;
    private Button? _reconnect;
    private Button? _remove;
    private Border? _confirm;
    private TextBlock? _question;
    private Button? _keep;
    private Button? _confirmRemove;

    public McpServerRow(McpServers owner, McpServer server)
    {
        _owner = owner;
        Server = server;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };

        // ---- Header ----
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var markBox = new Border
        {
            VerticalAlignment = VerticalAlignment.Center,
            Child = _mark = McpServersUi.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, medium: true),
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        _mark.HorizontalAlignment = HorizontalAlignment.Center;
        markBox.Bind(WidthProperty, markBox.GetResourceObservable(McpServersTokens.Key(McpServersTokenKind.MarkSize)));
        markBox.Bind(HeightProperty, markBox.GetResourceObservable(McpServersTokens.Key(McpServersTokenKind.MarkSize)));
        ArcTokens.Bind(markBox, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(markBox, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(markBox, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        content.Children.Add(markBox);

        var lines = new StackPanel { Spacing = 3, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lines, 1);
        var top = new ArcShrinkRow { Spacing = 8 };
        _name = McpServersUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, medium: true, lineHeight: 19.6);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        ArcShrinkRow.SetIsShrinkable(_name, true);
        top.Children.Add(_name);
        _tag = McpServersUi.Tag(string.Empty);
        top.Children.Add(_tag);
        lines.Children.Add(top);

        var statusLine = new ArcShrinkRow { Spacing = 6 };
        _dot = new McpServerStatusDot { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-5, 0, -3, 0) };
        BindToken(_dot, McpServerStatusDot.DotSizeProperty, McpServersTokenKind.StatusDotSize);
        BindToken(_dot, McpServerStatusDot.HaloSpreadProperty, McpServersTokenKind.StatusHaloSpread);
        BindToken(_dot, McpServerStatusDot.ConnectedPulseProperty, McpServersTokenKind.ConnectedPulse);
        BindToken(_dot, McpServerStatusDot.ConnectingPulseProperty, McpServersTokenKind.ConnectingPulse);
        ArcTokens.Bind(_dot, McpServerStatusDot.ConnectedBrushProperty, ArcDesignTokenKind.Success);
        ArcTokens.Bind(_dot, McpServerStatusDot.ConnectingBrushProperty, ArcDesignTokenKind.Accent);
        ArcTokens.Bind(_dot, McpServerStatusDot.ErrorBrushProperty, ArcDesignTokenKind.Danger);
        ArcTokens.Bind(_dot, McpServerStatusDot.OffBrushProperty, ArcDesignTokenKind.TextMuted);
        statusLine.Children.Add(_dot);
        _status = new ArcContentSwap { Mode = ContentSwapMode.Label, VerticalAlignment = VerticalAlignment.Center };
        statusLine.Children.Add(_status);
        statusLine.Children.Add(McpServersUi.Text("·", ArcDesignTokenKind.TextMuted));
        _endpoint = McpServersUi.Text(null, ArcDesignTokenKind.TextMuted, mono: true);
        _endpoint.TextTrimming = TextTrimming.CharacterEllipsis;
        ArcShrinkRow.SetIsShrinkable(_endpoint, true);
        statusLine.Children.Add(_endpoint);
        lines.Children.Add(statusLine);
        content.Children.Add(lines);

        _header = new McpServerRowHeader { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch };
        _header.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.ToggleExpanded(Server);
        };
        _header.KeyDown += OnHeaderKeyDown;
        grid.Children.Add(_header);

        _switch = new Switch { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
        Grid.SetColumn(_switch, 1);
        _switch.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _owner.SetServerEnabled(Server, _switch.IsChecked == true);
            }
        };
        grid.Children.Add(_switch);

        // ---- Body (filled on the first open) ----
        _bodyHost = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, IsExpanded = false, Spring = ArcSpring.Smooth };
        Grid.SetRow(_bodyHost, 1);
        Grid.SetColumnSpan(_bodyHost, 2);
        grid.Children.Add(_bodyHost);

        Child = grid;
        AddHandler(KeyDownEvent, OnRowKeyDown, RoutingStrategies.Bubble);
        Server.Tools.CollectionChanged += OnToolsChanged;
        Server.PropertyChanged += OnServerChanged;
        foreach (var tool in Server.Tools)
        {
            tool.PropertyChanged += OnToolChanged;
        }
        SyncAll();
    }

    public McpServer Server { get; }

    public McpServerRowHeader Header => _header;

    public Switch ServerSwitch => _switch;

    public ArcContentSwap StatusSwap => _status;

    public McpServerStatusDot Dot => _dot;

    public string? StatusText => _statusText;

    public string? EndpointText => _endpoint.Text;

    public string? MarkText => _mark.Text;

    public bool IsExpanded => _isExpanded;

    public bool IsBodyBuilt => _body is not null;

    public bool IsLogOpen => _isLogOpen;

    public bool IsConfirmingRemove => _isConfirming;

    public Border? ErrorBox => _errorBox;

    public string? ErrorMessageText => _errorMessage?.Text;

    public Button? RetryButton => _retry;

    public Button? LogToggle => _logToggle;

    public SelectableTextBlock? LogText => _log;

    public TextBlock? DisabledNote => _disabledNote;

    public TextBlock? ToolsCount => _toolsCount;

    public Button? ToggleAllButton => _toggleAll;

    public TextBlock? NoToolsText => _noTools;

    public StackPanel? FetchingLine => _fetching;

    public Grid? Details => _details;

    public Button? ReconnectButton => _reconnect;

    public Button? RemoveButton => _remove;

    public Border? ConfirmBox => _confirm;

    public Button? KeepButton => _keep;

    public Button? ConfirmRemoveButton => _confirmRemove;

    public IReadOnlyList<McpServerToolRow> ToolRows => _toolList?.Children.OfType<McpServerToolRow>().ToArray() ?? [];

    /// <summary>Stops following the server (the row left the list).</summary>
    public void Detach()
    {
        Server.Tools.CollectionChanged -= OnToolsChanged;
        Server.PropertyChanged -= OnServerChanged;
        foreach (var tool in _toolRows.Keys.Concat(Server.Tools).Distinct())
        {
            tool.PropertyChanged -= OnToolChanged;
        }
    }

    /// <summary>Opens or closes the body; the first open builds it.</summary>
    public void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded)
        {
            return;
        }
        _isExpanded = expanded;
        if (expanded)
        {
            EnsureBody();
            SyncBody();
        }
        else
        {
            _isConfirming = false;
            if (_body is not null)
            {
                SyncConfirm();
            }
        }
        _header.IsExpanded = expanded;
        _bodyHost.IsExpanded = expanded;
        PseudoClasses.Set(OpenPseudoClass, expanded);
    }

    /// <summary>Re-reads the words (the owner's labels changed).</summary>
    public void SyncAll()
    {
        _syncing = true;
        try
        {
            var labels = _owner.Labels;
            _mark.Text = McpServersDraftCheck.Initials(Server.Name);
            _name.Text = Server.Name;
            ((TextBlock)_tag.Child!).Text = Server.Transport == McpTransport.Http ? labels.HttpTag : labels.CommandTag;
            _endpoint.Text = Server.Endpoint;
            _switch.IsChecked = Server.IsEnabled;
            AutomationProperties.SetName(_switch, labels.ServerSwitchName(Server.Name));
        }
        finally
        {
            _syncing = false;
        }
        SyncStatus();
        if (_body is not null)
        {
            SyncBody();
        }
    }

    /// <summary>Focuses the header (keyboard navigation and focus after an action).</summary>
    public bool FocusHeader(NavigationMethod method = NavigationMethod.Tab) => _header.Focus(method);

    private void SyncStatus()
    {
        var labels = _owner.Labels;
        var status = _owner.EffectiveStatus(Server);
        _dot.Status = status;
        var text = status switch
        {
            McpServerStatus.Connected => $"{labels.Connected} · {labels.ToolCount(Server.EnabledToolCount, Server.Tools.Count)}",
            McpServerStatus.Connecting => labels.Connecting,
            McpServerStatus.Error => labels.Failed,
            _ => labels.Off,
        };
        if (text != _statusText)
        {
            _statusText = text;
            var block = McpServersUi.Text(text, status == McpServerStatus.Error ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextSecondary);
            block.TextTrimming = TextTrimming.CharacterEllipsis;
            _status.Content = block;
        }
        AutomationProperties.SetName(_header, Server.Name);
        AutomationProperties.SetHelpText(_header, $"{text}, {Server.Endpoint}");
        _owner.OnRowStatusSynced(Server);
    }

    #region Body

    private void EnsureBody()
    {
        if (_body is not null)
        {
            return;
        }
        var labels = _owner.Labels;
        _body = new StackPanel { Spacing = 14 };
        _body.Bind(MarginProperty, _owner.GetObservable(McpServers.BodyPaddingProperty));

        // Error
        _errorMessage = McpServersUi.Text(null, ArcDesignTokenKind.Foreground, lineHeight: 18);
        _errorMessage.TextWrapping = TextWrapping.Wrap;
        _retry = McpServersUi.Button(ButtonVariant.Secondary, labels.Retry);
        _retry.Click += (_, e) =>
        {
            e.Handled = true;
            _ = _owner.Connect(Server);
        };
        _logToggle = McpServersUi.Button(ButtonVariant.Ghost, labels.ShowLog);
        _logToggle.Click += (_, e) =>
        {
            e.Handled = true;
            _isLogOpen = !_isLogOpen;
            SyncError();
        };
        _log = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 18, Margin = new Thickness(12, 10) };
        ArcTokens.Bind(_log, SelectableTextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(_log, SelectableTextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_log, SelectableTextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _logHost = new ScrollViewer { Content = _log, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _logHost.Bind(MaxHeightProperty, _logHost.GetResourceObservable(McpServersTokens.Key(McpServersTokenKind.LogMaxHeight)));
        var logBox = new Border { Child = _logHost, Margin = new Thickness(0, 4, 0, 0) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        ArcTokens.Bind(logBox, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(logBox, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(logBox, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        AutomationProperties.SetName(_log, labels.ShowLog);
        var errorText = new StackPanel { Spacing = 4 };
        errorText.Children.Add(McpServersUi.Text(labels.ErrorTitle, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, medium: true, lineHeight: 19.6));
        errorText.Children.Add(_errorMessage);
        var errorActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        errorActions.Children.Add(_retry);
        errorActions.Children.Add(_logToggle);
        errorText.Children.Add(errorActions);
        errorText.Children.Add(logBox);
        var errorGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
        var alert = McpServersUi.Glyph(ArcIcons.CircleAlert, 16, ArcDesignTokenKind.Danger);
        alert.VerticalAlignment = VerticalAlignment.Top;
        alert.Margin = new Thickness(0, 2, 0, 0);
        errorGrid.Children.Add(alert);
        Grid.SetColumn(errorText, 1);
        errorGrid.Children.Add(errorText);
        _errorBox = new Border { Padding = new Thickness(12), Child = errorGrid }.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        ArcTokens.Bind(_errorBox, BackgroundProperty, ArcDesignTokenKind.DangerSubtle);
        AutomationProperties.SetLiveSetting(_errorBox, AutomationLiveSetting.Off);
        _body.Children.Add(_errorBox);

        // Off
        _disabledNote = McpServersUi.Text(labels.DisabledNote, ArcDesignTokenKind.TextSecondary);
        _disabledNote.TextWrapping = TextWrapping.Wrap;
        _body.Children.Add(_disabledNote);

        // Tools
        var tools = new StackPanel();
        var toolsHead = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 2) };
        var heading = McpServersUi.Text(labels.ToolsHeading, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, medium: true, lineHeight: 19.6);
        AutomationProperties.SetHeadingLevel(heading, 4);
        toolsHead.Children.Add(heading);
        _toolsCount = McpServersUi.Text(null, ArcDesignTokenKind.TextMuted);
        _toolsCount.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(_toolsCount, 1);
        toolsHead.Children.Add(_toolsCount);
        _toggleAll = McpServersUi.Button(ButtonVariant.Ghost, labels.DisableAll);
        _toggleAll.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.SetAllTools(Server, Server.EnabledToolCount < Server.Tools.Count);
        };
        Grid.SetColumn(_toggleAll, 2);
        toolsHead.Children.Add(_toggleAll);
        tools.Children.Add(toolsHead);
        _toolList = new StackPanel();
        AutomationProperties.SetName(_toolList, labels.ToolsHeading);
        tools.Children.Add(_toolList);
        _noTools = McpServersUi.Text(labels.NoTools, ArcDesignTokenKind.TextMuted);
        _noTools.Margin = new Thickness(0, 8, 0, 0);
        tools.Children.Add(_noTools);
        _fetching = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        _fetching.Children.Add(new ArcSpinner { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center });
        _fetching.Children.Add(McpServersUi.Text(labels.FetchingTools, ArcDesignTokenKind.TextMuted));
        tools.Children.Add(_fetching);
        _body.Children.Add(tools);

        // Details
        _details = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16, RowSpacing = 4 };
        _body.Children.Add(_details);

        // Actions and the confirmation that replaces them
        _reconnect = McpServersUi.Button(ButtonVariant.Secondary, labels.Reconnect);
        _reconnect.Click += (_, e) =>
        {
            e.Handled = true;
            _ = _owner.Connect(Server);
        };
        _remove = McpServersUi.Button(ButtonVariant.Ghost, labels.Remove);
        _remove.Click += (_, e) =>
        {
            e.Handled = true;
            _isConfirming = true;
            SyncConfirm();
            _keep?.Focus(_owner.LastNavigation);
        };
        _actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        _actions.Children.Add(_reconnect);
        _actions.Children.Add(_remove);
        _body.Children.Add(_actions);

        _question = McpServersUi.Text(null, ArcDesignTokenKind.Foreground, lineHeight: 18);
        _question.TextWrapping = TextWrapping.Wrap;
        _keep = McpServersUi.Button(ButtonVariant.Secondary, labels.Keep);
        _keep.Click += (_, e) =>
        {
            e.Handled = true;
            CancelConfirm();
        };
        _confirmRemove = McpServersUi.Button(ButtonVariant.Danger, labels.RemoveConfirm);
        _confirmRemove.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.RemoveServer(Server);
        };
        var confirmPanel = new StackPanel { Spacing = 10 };
        confirmPanel.Children.Add(_question);
        var confirmButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        confirmButtons.Children.Add(_keep);
        confirmButtons.Children.Add(_confirmRemove);
        confirmPanel.Children.Add(confirmButtons);
        _confirm = new Border { Padding = new Thickness(12), Child = confirmPanel }.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        ArcTokens.Bind(_confirm, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(_confirm, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_confirm, BackgroundProperty, ArcDesignTokenKind.Surface);
        _body.Children.Add(_confirm);

        _bodyHost.Child = _body;
        RebuildTools();
    }

    private void SyncBody()
    {
        if (_body is null)
        {
            return;
        }
        var labels = _owner.Labels;
        var status = _owner.EffectiveStatus(Server);
        SyncError();
        _disabledNote!.Text = labels.DisabledNote;
        _disabledNote.IsVisible = status == McpServerStatus.Disabled;

        var total = Server.Tools.Count;
        var enabled = Server.EnabledToolCount;
        _toolsCount!.Text = total > 0 ? labels.ToolsOn(enabled, total) : null;
        McpServersUi.SetText(_toggleAll!, enabled < total ? labels.EnableAll : labels.DisableAll);
        _toggleAll!.IsVisible = total > 1;
        _fetching!.IsVisible = status == McpServerStatus.Connecting && total == 0;
        _noTools!.IsVisible = total == 0 && status == McpServerStatus.Connected;
        foreach (var row in _toolRows.Values)
        {
            row.Sync(labels);
        }

        SyncDetails(labels);
        McpServersUi.SetText(_reconnect!, labels.Reconnect);
        McpServersUi.SetText(_remove!, labels.Remove);
        _reconnect!.IsVisible = status == McpServerStatus.Connected;
        _reconnect.IsEnabled = status != McpServerStatus.Connecting;
        _remove!.IsVisible = _owner.CanRemove;
        SyncConfirm();
    }

    private void SyncError()
    {
        if (_errorBox is null)
        {
            return;
        }
        var labels = _owner.Labels;
        var failed = _owner.EffectiveStatus(Server) == McpServerStatus.Error;
        _errorBox.IsVisible = failed;
        _errorMessage!.Text = Server.ErrorMessage;
        var hasLog = !string.IsNullOrWhiteSpace(Server.ErrorLog);
        _logToggle!.IsVisible = hasLog;
        McpServersUi.SetText(_logToggle, _isLogOpen ? labels.HideLog : labels.ShowLog);
        McpServersUi.SetText(_retry!, labels.Retry);
        _log!.Text = Server.ErrorLog;
        ((Control)_logHost!.Parent!).IsVisible = hasLog && _isLogOpen;
    }

    private void SyncDetails(McpServersLabels labels)
    {
        var details = _details!;
        details.Children.Clear();
        details.RowDefinitions.Clear();
        var http = Server.Transport == McpTransport.Http;
        AddDetail(http ? labels.AddressLabel : labels.CommandLineLabel, Server.Endpoint, mono: true);
        if (Server.Variables is { Count: > 0 } variables)
        {
            AddDetail(http ? labels.HeadersDetail : labels.EnvironmentDetail, string.Join(", ", variables.Select(v => v.Key)), mono: true);
        }
        if (!string.IsNullOrWhiteSpace(Server.Version))
        {
            AddDetail(labels.VersionLabel, Server.Version!, mono: false);
        }

        void AddDetail(string label, string value, bool mono)
        {
            var row = details.RowDefinitions.Count;
            details.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var key = McpServersUi.Text(label, ArcDesignTokenKind.TextMuted);
            key.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(key, row);
            details.Children.Add(key);
            var text = McpServersUi.Text(value, ArcDesignTokenKind.TextSecondary, mono: mono);
            text.TextWrapping = TextWrapping.Wrap;
            text.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 1);
            details.Children.Add(text);
        }
    }

    private void SyncConfirm()
    {
        if (_confirm is null)
        {
            return;
        }
        var labels = _owner.Labels;
        _confirm.IsVisible = _isConfirming;
        _actions!.IsVisible = !_isConfirming;
        _question!.Text = labels.RemoveQuestion(Server.Name, Server.Tools.Count);
        McpServersUi.SetText(_keep!, labels.Keep);
        McpServersUi.SetText(_confirmRemove!, labels.RemoveConfirm);
    }

    private void CancelConfirm()
    {
        if (!_isConfirming)
        {
            return;
        }
        _isConfirming = false;
        SyncConfirm();
        _remove?.Focus(_owner.LastNavigation);
    }

    private void RebuildTools()
    {
        if (_toolList is null)
        {
            return;
        }
        var labels = _owner.Labels;
        var wanted = new HashSet<McpTool>(Server.Tools);
        foreach (var gone in _toolRows.Keys.Where(t => !wanted.Contains(t)).ToArray())
        {
            _toolRows.Remove(gone);
        }
        _toolList.Children.Clear();
        var first = true;
        foreach (var tool in Server.Tools)
        {
            if (!_toolRows.TryGetValue(tool, out var row))
            {
                row = new McpServerToolRow(tool, (t, on) => _owner.SetToolEnabled(Server, t, on), labels);
                _toolRows[tool] = row;
            }
            row.ShowsDivider = !first;
            first = false;
            _toolList.Children.Add(row);
        }
    }

    #endregion

    #region Following the data

    private void OnServerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == McpServer.StatusProperty || e.Property == McpServer.IsEnabledProperty)
        {
            _syncing = true;
            try
            {
                _switch.IsChecked = Server.IsEnabled;
            }
            finally
            {
                _syncing = false;
            }
            SyncStatus();
            SyncBody();
        }
        else if (e.Property == McpServer.ErrorMessageProperty || e.Property == McpServer.ErrorLogProperty)
        {
            SyncError();
        }
        else
        {
            SyncAll();
        }
    }

    private void OnToolsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (McpTool tool in e.OldItems)
            {
                tool.PropertyChanged -= OnToolChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (McpTool tool in e.NewItems)
            {
                tool.PropertyChanged += OnToolChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var tool in _toolRows.Keys)
            {
                tool.PropertyChanged -= OnToolChanged;
            }
            foreach (var tool in Server.Tools)
            {
                tool.PropertyChanged -= OnToolChanged;
                tool.PropertyChanged += OnToolChanged;
            }
        }
        RebuildTools();
        SyncStatus();
        SyncBody();
    }

    private void OnToolChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is McpTool tool && _toolRows.TryGetValue(tool, out var row))
        {
            row.Sync(_owner.Labels);
        }
        if (e.Property == McpTool.IsEnabledProperty)
        {
            SyncStatus();
            SyncBody();
        }
    }

    #endregion

    #region Keyboard

    private void OnHeaderKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = _owner.FocusNeighbour(this, +1);
                break;
            case Key.Up:
                e.Handled = _owner.FocusNeighbour(this, -1);
                break;
            case Key.Home:
                e.Handled = _owner.FocusEdge(first: true);
                break;
            case Key.End:
                e.Handled = _owner.FocusEdge(first: false);
                break;
        }
    }

    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled || !_isExpanded)
        {
            return;
        }
        if (_isConfirming)
        {
            CancelConfirm();
        }
        else
        {
            _owner.ToggleExpanded(Server);
            FocusHeader();
        }
        e.Handled = true;
    }

    #endregion

    private void BindToken(AvaloniaObject target, AvaloniaProperty property, McpServersTokenKind kind) =>
        target.Bind(property, this.GetResourceObservable(McpServersTokens.Key(kind)));
}
