using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(HostPart, typeof(Panel))]
[PseudoClasses(FramedPseudoClass, WidePseudoClass, CompactPseudoClass, BusyPseudoClass)]
public partial class WebhookTester
{
    internal const string HostPart = "PART_Host";
    internal const string FramedPseudoClass = ":framed";
    internal const string WidePseudoClass = ":wide";
    internal const string CompactPseudoClass = ":compact";
    internal const string BusyPseudoClass = ":busy";

    private enum ResponseTab
    {
        Body,
        Headers,
        Request
    }

    private static readonly WebhookTestEvent[] s_sampleEvents =
    [
        new("order.paid", """{"id":"evt_1QhT8sKZ3v2Lw9Xc","type":"order.paid","created":1793111528,"livemode":false,"data":{"object":{"id":"ord_1QhT8rKZ3v2Lw9Xc","customer":"cus_RkN4b7TzqP1m","amount_paid":4900,"currency":"usd","status":"paid","receipt_url":null}}}"""),
        new("customer.created", """{"id":"evt_1QhT9aKZ3v2Lw9Xc","type":"customer.created","created":1793111602,"livemode":false,"data":{"object":{"id":"cus_RkN4b7TzqP1m","email":"ada@northwind.dev","name":"Ada Lovelace","plan":"team"}}}"""),
        new("subscription.updated", """{"id":"evt_1QhTBcKZ3v2Lw9Xc","type":"subscription.updated","created":1793111690,"livemode":false,"data":{"object":{"id":"sub_1QhTBbKZ3v2Lw9Xc","customer":"cus_RkN4b7TzqP1m","status":"active","quantity":12},"previous":{"quantity":10}}}"""),
        new("checkout.completed", """{"id":"evt_1QhTDeKZ3v2Lw9Xc","type":"checkout.completed","created":1793111774,"livemode":false,"data":{"object":{"id":"chk_1QhTDdKZ3v2Lw9Xc","amount_total":14700,"currency":"usd","payment_status":"paid"}}}"""),
        new("payment.failed", """{"id":"evt_1QhTFgKZ3v2Lw9Xc","type":"payment.failed","created":1793111851,"livemode":false,"data":{"object":{"id":"pay_1QhTFfKZ3v2Lw9Xc","amount":4900,"currency":"usd","failure_code":"card_declined"}}}"""),
    ];

    private bool _built;
    private bool _wide;
    private bool _compact;
    private bool _syncing;
    private bool _endpointTouched;
    private ResponseTab _tab;
    private Panel? _hostPanel;

    private Border? _markHost;
    private readonly List<WebhookTesterBox> _sectionBoxes = new();
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private WebhookTesterBox? _badge;
    private TextBlock? _badgeText;
    private Grid? _body;
    private Grid? _requestColumn;
    private Grid? _responseColumn;
    private TextBlock? _endpointLabel;
    private Input? _endpointInput;
    private TextBlock? _eventLabel;
    private Select? _eventSelect;
    private TextBlock? _secretLabel;
    private TextBlock? _secretText;
    private WebhookTesterPick? _reveal;
    private TextBlock? _revealText;
    private ArcGlyph? _revealGlyph;
    private CopyButton? _copySecret;
    private TextBlock? _payloadLabel;
    private WebhookTesterEditor? _editor;
    private ArcGlyph? _validityGlyph;
    private TextBlock? _validity;
    private Button? _format;
    private StackPanel? _hint;
    private TextBlock? _hintTail;
    private Button? _send;
    private readonly WebhookTesterBox[] _stepCircles = new WebhookTesterBox[3];
    private readonly TextBlock[] _stepLabels = new TextBlock[3];
    private readonly TextBlock[] _stepDetails = new TextBlock[3];
    private readonly WebhookTesterConnector[] _connectors = new WebhookTesterConnector[2];
    private StackPanel? _tabList;
    private readonly WebhookTesterPick[] _tabs = new WebhookTesterPick[3];
    private WebhookTesterEditor? _panel;
    private CopyButton? _copyPanel;
    private TextBlock? _recentTitle;
    private WebhookTesterBox? _failedBadge;
    private TextBlock? _failedText;
    private StackPanel? _rows;
    private TextBlock? _noRows;
    private ScrollViewer? _rowsScroll;
    private DeveloperAnnouncer? _announcer;

    #region Parts, for tests and demos

    internal Input? EndpointInput => _endpointInput;

    internal Select? EventSelect => _eventSelect;

    internal WebhookTesterEditor? Editor => _editor;

    internal WebhookTesterEditor? ResponsePanel => _panel;

    internal Button? SendButton => _send;

    internal Button? FormatButton => _format;

    internal WebhookTesterPick? RevealToggle => _reveal;

    internal IReadOnlyList<WebhookTesterPick> Tabs => _tabs;

    internal StackPanel? DeliveryRows => _rows;

    internal TextBlock? SecretText => _secretText;

    internal TextBlock? ValidityText => _validity;

    internal TextBlock? StepLabel(int index) => _stepLabels[index];

    internal TextBlock? StepDetail(int index) => _stepDetails[index];

    internal WebhookTesterBox? StepCircle(int index) => _stepCircles[index];

    internal DeveloperAnnouncer? Announcer => _announcer;

    internal bool IsSplit => _wide;

    /// <summary>The recent list's retry button of <paramref name="delivery"/>, or null.</summary>
    internal Button? RetryButtonOf(WebhookTestDelivery delivery) =>
        _rows?.Children.OfType<Grid>().FirstOrDefault(g => ReferenceEquals(g.Tag, delivery))?.Children.OfType<Button>().FirstOrDefault();

    #endregion

    static WebhookTester()
    {
        AffectsMeasure<WebhookTester>(CardMaxWidthProperty);
        FocusableProperty.OverrideDefaultValue<WebhookTester>(false);
    }

    public WebhookTester()
    {
        _events.CollectionChanged += OnEventsChanged;
        _deliveries.CollectionChanged += OnDeliveriesChanged;
        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
        AddHandler(PointerPressedEvent, OnInteracted, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnInteractedKey, RoutingStrategies.Bubble, handledEventsToo: true);
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    private IReadOnlyList<WebhookTestEvent> EffectiveEvents => _events.Count > 0 ? _events : s_sampleEvents;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var host = e.NameScope.Find<Panel>(HostPart);
        if (host is null || ReferenceEquals(host, _hostPanel))
        {
            return;
        }
        _hostPanel = host;
        if (!_built)
        {
            Build();
            _built = true;
        }
        host.Children.Clear();
        host.Children.Add(_root!);
        SyncAll();
    }

    private Control? _root;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        if (!_built || _syncing)
        {
            return;
        }
        if (change.Property == EndpointProperty)
        {
            if (_endpointInput is not null && _endpointInput.Text != Endpoint)
            {
                _syncing = true;
                _endpointInput.Text = Endpoint;
                _syncing = false;
            }
            ValidateEndpoint(showError: _endpointTouched);
        }
        else if (change.Property == SelectedEventProperty)
        {
            OnSelectedEventChanged();
        }
        else if (change.Property == PayloadProperty)
        {
            if (_editor is not null && _editor.Text != Payload)
            {
                _syncing = true;
                _editor.Text = Payload ?? string.Empty;
                _syncing = false;
            }
            UpdateValidity();
        }
        else if (change.Property == SecretProperty)
        {
            SyncSecret();
        }
        else if (change.Property == SourceProperty || change.Property == LabelsProperty)
        {
            SyncHeader();
            if (change.Property == LabelsProperty)
            {
                SyncLabels();
            }
        }
        else if (change.Property == SelectedDeliveryProperty)
        {
            if (change.NewValue is WebhookTestDelivery delivery)
            {
                ShowDelivery(delivery);
            }
            SyncRowSelection();
        }
    }

    #region Build

    private void Build()
    {
        var labels = Labels;

        // Header: mark, title, subtitle and the test-mode badge.
        _markHost = new Border { VerticalAlignment = VerticalAlignment.Center };
        _title = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeBase, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 20);
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        AutomationProperties.SetHeadingLevel(_title, 2);
        _subtitle = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);
        _subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        var heading = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(WebhookTesterUi.Row(8, _markHost, _title));
        heading.Children.Add(_subtitle);
        _badgeText = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Warning, FontWeight.Medium, lineHeight: 16);
        _badge = new WebhookTesterBox { Tone = WebhookTesterTone.Badge, Content = _badgeText, VerticalAlignment = VerticalAlignment.Center };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        header.Children.Add(heading);
        Grid.SetColumn(_badge, 1);
        header.Children.Add(_badge);

        // Request column.
        _endpointLabel = WebhookTesterUi.FieldLabel(labels.Endpoint);
        var post = WebhookTesterUi.Text("POST", ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, FontWeight.SemiBold, mono: true);
        _endpointInput = new Input { Prefix = post, Size = ControlSize.Small };
        AutomationProperties.SetName(_endpointInput, labels.Endpoint);
        _endpointInput.TextChanged += OnEndpointTextChanged;

        _eventLabel = WebhookTesterUi.FieldLabel(labels.Event);
        _eventSelect = new Select
        {
            TextSelector = item => (item as WebhookTestEvent)?.Type,
        };
        AutomationProperties.SetName(_eventSelect, labels.Event);
        _eventSelect.SelectionChanged += OnEventPicked;

        _secretLabel = WebhookTesterUi.FieldLabel(labels.Secret);
        _secretText = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, mono: true, lineHeight: 19.6);
        _secretText.TextTrimming = TextTrimming.CharacterEllipsis;
        _secretText.VerticalAlignment = VerticalAlignment.Center;
        _revealGlyph = WebhookTesterUi.Glyph(ArcIcons.Eye, 14, ArcDesignTokenKind.TextSecondary);
        _revealText = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, FontWeight.Medium);
        _reveal = new WebhookTesterPick
        {
            TogglesItself = true,
            Content = WebhookTesterUi.Row(6, _revealGlyph, _revealText),
            Classes = { "reveal" },
        };
        _reveal.IsCheckedChanged += (_, _) => SyncSecret();
        _copySecret = new CopyButton { IsIconOnly = true };
        var secretRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6 };
        secretRow.Children.Add(_secretText);
        Grid.SetColumn(_reveal, 1);
        secretRow.Children.Add(_reveal);
        Grid.SetColumn(_copySecret, 2);
        secretRow.Children.Add(_copySecret);
        var secretBox = new WebhookTesterBox { Tone = WebhookTesterTone.Secret, Content = secretRow };

        _payloadLabel = WebhookTesterUi.FieldLabel(string.Empty);
        _editor = new WebhookTesterEditor { MinHeight = 200, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap };
        _editor.TextChanged += OnPayloadTextChanged;

        _validityGlyph = WebhookTesterUi.Glyph(ArcIcons.CircleCheck, 14, ArcDesignTokenKind.Success);
        _validity = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16);
        _validity.TextTrimming = TextTrimming.CharacterEllipsis;
        _validity.VerticalAlignment = VerticalAlignment.Center;
        _format = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        _format.Click += (_, _) => FormatPayload();
        var validityRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        validityRow.Children.Add(WebhookTesterUi.Row(6, _validityGlyph, _validity));
        Grid.SetColumn(_format, 1);
        validityRow.Children.Add(_format);

        _hintTail = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
        _hintTail.VerticalAlignment = VerticalAlignment.Center;
        _hint = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Classes = { "hint" } };
        _send = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Right };
        _send.Click += (_, _) => _ = SendAsync();
        var sendRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Margin = new Thickness(0, 14, 0, 0) };
        sendRow.Children.Add(_hint);
        Grid.SetColumn(_send, 1);
        sendRow.Children.Add(_send);

        _requestColumn = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto"), MinWidth = 0 };
        AddCell(_requestColumn, Field(_endpointLabel, _endpointInput), 0);
        AddCell(_requestColumn, Field(_eventLabel, _eventSelect), 1);
        AddCell(_requestColumn, Field(_secretLabel, secretBox), 2);
        AddCell(_requestColumn, _payloadLabel, 3);
        AddCell(_requestColumn, _editor, 4);
        AddCell(_requestColumn, validityRow, 5);
        AddCell(_requestColumn, sendRow, 6);

        // Response column: timeline, tabs, panel.
        var timeline = BuildTimeline(labels);
        _tabList = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        AutomationProperties.SetControlTypeOverride(_tabList, AutomationControlType.Tab);
        for (var i = 0; i < _tabs.Length; i++)
        {
            var index = i;
            var tab = new WebhookTesterPick { Classes = { "tab" } };
            AutomationProperties.SetControlTypeOverride(tab, AutomationControlType.TabItem);
            tab.Click += (_, _) => SelectTab((ResponseTab)index, focus: false);
            tab.KeyDown += (_, e) => OnTabKeyDown(index, e);
            _tabs[i] = tab;
            _tabList.Children.Add(tab);
        }
        _copyPanel = new CopyButton { IsIconOnly = true, HorizontalAlignment = HorizontalAlignment.Right };
        var tabRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Margin = new Thickness(0, 16, 0, 8) };
        tabRow.Children.Add(_tabList);
        Grid.SetColumn(_copyPanel, 1);
        tabRow.Children.Add(_copyPanel);
        _panel = new WebhookTesterEditor
        {
            IsReadOnly = true,
            ShowsGutter = false,
            IsTinted = true,
            TextWrapping = TextWrapping.NoWrap,
            MinHeight = 120,
            MaxHeight = 260,
            Classes = { "panel" },
        };
        _responseColumn = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), MinWidth = 0 };
        AddCell(_responseColumn, timeline, 0);
        AddCell(_responseColumn, tabRow, 1);
        AddCell(_responseColumn, _panel, 2);

        _body = new Grid { ColumnSpacing = 20, RowSpacing = 20, Classes = { "body" } };
        _body.Children.Add(_requestColumn);
        _body.Children.Add(_responseColumn);

        // Recent deliveries.
        _recentTitle = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 19.6);
        AutomationProperties.SetHeadingLevel(_recentTitle, 3);
        _failedText = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Danger, FontWeight.Medium, lineHeight: 16);
        _failedBadge = new WebhookTesterBox { Tone = WebhookTesterTone.FailedBadge, Content = _failedText, VerticalAlignment = VerticalAlignment.Center };
        var recentHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        recentHeader.Children.Add(_recentTitle);
        recentHeader.Children.Add(_failedBadge);
        _rows = new StackPanel { Spacing = 0 };
        AutomationProperties.SetControlTypeOverride(_rows, AutomationControlType.List);
        _noRows = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextMuted, lineHeight: 19.6);
        _noRows.Margin = new Thickness(8, 8);
        var rowsHost = new StackPanel();
        rowsHost.Children.Add(_rows);
        rowsHost.Children.Add(_noRows);
        _rowsScroll = new ScrollViewer
        {
            Content = rowsHost,
            MaxHeight = 240,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var recent = new StackPanel();
        recent.Children.Add(recentHeader);
        recent.Children.Add(_rowsScroll);

        _announcer = new DeveloperAnnouncer();

        var content = new StackPanel();
        var bodyHost = new WebhookTesterBox { Tone = WebhookTesterTone.Body, Content = _body };
        var recentHost = new WebhookTesterBox { Tone = WebhookTesterTone.Recent, Content = recent };
        var headerHost = new WebhookTesterBox { Tone = WebhookTesterTone.Header, Content = header };
        _sectionBoxes.Add(headerHost);
        _sectionBoxes.Add(bodyHost);
        _sectionBoxes.Add(recentHost);
        content.Children.Add(headerHost);
        content.Children.Add(bodyHost);
        content.Children.Add(recentHost);
        content.Children.Add(_announcer);
        _root = content;
    }

    private static StackPanel Field(TextBlock label, Control control)
    {
        var field = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        field.Children.Add(label);
        field.Children.Add(control);
        return field;
    }

    private static void AddCell(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private Control BuildTimeline(WebhookTesterLabels labels)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto"), Classes = { "timeline" } };
        for (var i = 0; i < 3; i++)
        {
            var circle = new WebhookTesterBox { Tone = WebhookTesterTone.StepPending, Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center };
            var label = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 18);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            var detail = WebhookTesterUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, mono: true, lineHeight: 14);
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(label);
            text.Children.Add(detail);
            var step = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            step.Children.Add(circle);
            step.Children.Add(text);
            Grid.SetColumn(step, i * 2);
            grid.Children.Add(step);
            _stepCircles[i] = circle;
            _stepLabels[i] = label;
            _stepDetails[i] = detail;
            if (i < 2)
            {
                var connector = new WebhookTesterConnector { Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center, Classes = { "connector" } };
                ArcTokens.Bind(connector, WebhookTesterConnector.TrackBrushProperty, ArcDesignTokenKind.Border);
                ArcTokens.Bind(connector, WebhookTesterConnector.FillBrushProperty, ArcDesignTokenKind.Accent);
                ArcTokens.Bind(connector, WebhookTesterConnector.DotBrushProperty, ArcDesignTokenKind.Accent);
                Grid.SetColumn(connector, i * 2 + 1);
                grid.Children.Add(connector);
                _connectors[i] = connector;
            }
        }
        AutomationProperties.SetName(grid, labels.Title);
        return grid;
    }

    #endregion

    #region Sync

    private void SyncAll()
    {
        _syncing = true;
        SyncHeader();
        SyncLabels();
        _eventSelect!.ItemsSource = EffectiveEvents.ToList();
        var chosen = SelectedEvent is { } e && EffectiveEvents.Contains(e) ? e : EffectiveEvents.FirstOrDefault();
        _eventSelect.SelectedItem = chosen;
        _endpointInput!.Text = Endpoint;
        if (chosen is not null && string.IsNullOrEmpty(Payload))
        {
            _editor!.Text = WebhookTesterJson.Format(chosen.Payload);
            SetCurrentValue(PayloadProperty, _editor.Text);
        }
        else
        {
            _editor!.Text = Payload ?? string.Empty;
        }
        if (SelectedEvent is null && chosen is not null)
        {
            SetCurrentValue(SelectedEventProperty, chosen);
        }
        _syncing = false;
        SyncSecret();
        UpdateValidity();
        SyncPayloadLabel();
        if (SelectedDelivery is null && _deliveries.Count > 0)
        {
            SetCurrentValue(SelectedDeliveryProperty, _deliveries[0]);
        }
        RebuildRows();
        if (SelectedDelivery is { } delivery)
        {
            ShowDelivery(delivery);
        }
        else
        {
            SetPhase(WebhookTestPhase.Idle);
            RefreshResponse(null);
        }
        ApplyWidth(Bounds.Width);
    }

    private void SyncHeader()
    {
        var source = Source ?? new WebhookTesterSource();
        _title!.Text = Labels.Title;
        _subtitle!.Text = Labels.Subtitle(source.Name);
        var mark = source.MarkFactory?.Invoke() ?? WebhookTesterUi.DefaultMark(source.Name);
        _markHost!.Child = mark;
        _badgeText!.Text = Labels.TestMode;
    }

    private void SyncLabels()
    {
        var labels = Labels;
        _endpointLabel!.Text = labels.Endpoint;
        AutomationProperties.SetName(_endpointInput!, labels.Endpoint);
        _eventLabel!.Text = labels.Event;
        AutomationProperties.SetName(_eventSelect!, labels.Event);
        _secretLabel!.Text = labels.Secret;
        AutomationProperties.SetName(_reveal!, labels.RevealName);
        AutomationProperties.SetName(_copySecret!, labels.CopySecret);
        _format!.Content = labels.Format;
        AutomationProperties.SetName(_format, labels.Format);
        _hintTail!.Text = labels.SendHintTail;
        _hint!.Children.Clear();
        var modifier = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS()
            ? ("⌘", labels.SendHintCommand)
            : (labels.SendHintControl, labels.SendHintControl);
        _hint.Children.Add(WebhookTesterUi.KeyCap(modifier.Item1, modifier.Item2));
        _hint.Children.Add(WebhookTesterUi.KeyCap("↵", labels.SendHintReturn));
        _hint.Children.Add(_hintTail);
        _tabs[0].Content = labels.TabBody;
        _tabs[1].Content = labels.TabHeaders;
        _tabs[2].Content = labels.TabRequest;
        for (var i = 0; i < _tabs.Length; i++)
        {
            AutomationProperties.SetName(_tabs[i], _tabs[i].Content as string);
        }
        AutomationProperties.SetName(_tabList!, labels.TabsName);
        AutomationProperties.SetName(_copyPanel!, labels.CopyPanel);
        AutomationProperties.SetName(_panel!, labels.TabsName);
        _recentTitle!.Text = labels.Recent;
        AutomationProperties.SetName(_rows!, labels.DeliveriesName);
        _noRows!.Text = labels.NoDeliveries;
        SyncSendButton();
        SyncTabs();
    }

    private void SyncSecret()
    {
        if (_secretText is null)
        {
            return;
        }
        var labels = Labels;
        var revealed = _reveal!.IsChecked == true;
        var secret = Secret ?? string.Empty;
        if (revealed)
        {
            _secretText.Text = secret;
            AutomationProperties.SetName(_secretText, labels.Secret);
        }
        else
        {
            var cut = secret.IndexOf('_');
            var prefix = cut >= 0 ? secret[..(cut + 1)] : string.Empty;
            _secretText.Text = prefix + new string('•', 16);
            // The value stays out of the accessibility tree until it is revealed.
            AutomationProperties.SetName(_secretText, labels.SecretMasked);
        }
        _revealText!.Text = revealed ? labels.Shown : labels.Hidden;
        _revealGlyph!.Data = revealed ? ArcIcons.EyeOff : ArcIcons.Eye;
        _copySecret!.Value = secret;
    }

    private void SyncPayloadLabel()
    {
        var type = SelectedEvent?.Type ?? EffectiveEvents.FirstOrDefault()?.Type ?? string.Empty;
        var text = Labels.Payload(type);
        _payloadLabel!.Text = text;
        AutomationProperties.SetName(_editor!, text);
    }

    private void SyncSendButton()
    {
        if (_send is null)
        {
            return;
        }
        var busy = _phase is WebhookTestPhase.Signing or WebhookTestPhase.Sending;
        _send.IsLoading = busy;
        _send.Content = busy ? Labels.Sending : Labels.Send;
        AutomationProperties.SetName(_send, _send.Content as string);
        PseudoClasses.Set(BusyPseudoClass, busy);
    }

    private void ApplyWidth(double width)
    {
        if (!_built || width <= 0)
        {
            return;
        }
        // One pixel of hysteresis at each threshold so that a width on the line does not flip back and forth.
        var wide = _wide ? width >= SplitWidth - 1 : width >= SplitWidth;
        var compact = _compact ? width < 421 : width < 420;
        if (wide != _wide || _body!.ColumnDefinitions.Count == 0)
        {
            _wide = wide;
            _body!.ColumnDefinitions = new ColumnDefinitions(wide ? "*,*" : "*");
            _body.RowDefinitions = new RowDefinitions(wide ? "*" : "Auto,Auto");
            Grid.SetColumn(_requestColumn!, 0);
            Grid.SetRow(_requestColumn!, 0);
            Grid.SetColumn(_responseColumn!, wide ? 1 : 0);
            Grid.SetRow(_responseColumn!, wide ? 0 : 1);
            _panel!.MaxHeight = wide ? double.PositiveInfinity : 260;
            PseudoClasses.Set(WidePseudoClass, wide);
        }
        if (compact != _compact)
        {
            _compact = compact;
            PseudoClasses.Set(CompactPseudoClass, compact);
            foreach (var box in _sectionBoxes)
            {
                box.IsCompact = compact;
            }
            RebuildRows();
        }
    }

    #endregion

    #region Fields

    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_built)
        {
            return;
        }
        _syncing = true;
        _eventSelect!.ItemsSource = EffectiveEvents.ToList();
        var current = SelectedEvent;
        _eventSelect.SelectedItem = current is not null && EffectiveEvents.Contains(current) ? current : EffectiveEvents.FirstOrDefault();
        _syncing = false;
        if (!ReferenceEquals(_eventSelect.SelectedItem, current))
        {
            SetCurrentValue(SelectedEventProperty, _eventSelect.SelectedItem as WebhookTestEvent);
        }
    }

    private void OnEventPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _eventSelect?.SelectedItem is not WebhookTestEvent picked)
        {
            return;
        }
        SetCurrentValue(SelectedEventProperty, picked);
    }

    private void OnSelectedEventChanged()
    {
        var picked = SelectedEvent ?? EffectiveEvents.FirstOrDefault();
        if (picked is null)
        {
            return;
        }
        _syncing = true;
        if (!ReferenceEquals(_eventSelect!.SelectedItem, picked))
        {
            _eventSelect.SelectedItem = picked;
        }
        _syncing = false;
        // Choosing an event loads its payload: valid JSON pretty printed, anything else kept as is.
        var text = WebhookTesterJson.Format(picked.Payload);
        _editor!.Text = text;
        SetCurrentValue(PayloadProperty, text);
        SyncPayloadLabel();
        UpdateValidity();
    }

    private void OnEndpointTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        _endpointTouched = true;
        _syncing = true;
        SetCurrentValue(EndpointProperty, _endpointInput!.Text);
        _syncing = false;
        ValidateEndpoint(showError: true);
    }

    private bool ValidateEndpoint(bool showError)
    {
        var ok = WebhookTesterJson.IsAllowedEndpoint(Endpoint);
        if (_endpointInput is not null)
        {
            _endpointInput.ErrorMessage = showError && !ok ? Labels.EndpointInvalid : null;
        }
        return ok;
    }

    private void OnPayloadTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        SetCurrentValue(PayloadProperty, _editor!.Text);
        _syncing = false;
        UpdateValidity();
    }

    private void UpdateValidity()
    {
        if (_validity is null)
        {
            return;
        }
        var labels = Labels;
        var check = WebhookTesterJson.Check(_editor!.Text);
        if (check.IsValid)
        {
            _validity.Text = labels.ValidJson(check.Bytes);
            _validityGlyph!.Data = ArcIcons.CircleCheck;
            ArcTokens.Bind(_validityGlyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
            _editor.Classes.Set("invalid", false);
        }
        else
        {
            _validity.Text = check.IsEmpty ? labels.EmptyJson : labels.InvalidJson(check.Line, check.Message ?? string.Empty);
            _validityGlyph!.Data = ArcIcons.CircleAlert;
            ArcTokens.Bind(_validityGlyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
            _editor.Classes.Set("invalid", true);
        }
        // Arc's aria-invalid: the editor's automation help names the problem, the footer says it in words.
        AutomationProperties.SetHelpText(_editor, check.IsValid ? null : _validity.Text);
        _format!.IsEnabled = check.IsValid;
    }

    private void FormatPayload()
    {
        var formatted = WebhookTesterJson.Format(_editor!.Text);
        if (formatted != _editor.Text)
        {
            _editor.Text = formatted;
        }
        _editor.Focus();
    }

    #endregion

    #region Tabs

    private void SelectTab(ResponseTab tab, bool focus)
    {
        _tab = tab;
        SyncTabs();
        RefreshResponse(_shown);
        if (focus)
        {
            _tabs[(int)tab].Focus(NavigationMethod.Directional);
        }
    }

    private void SyncTabs()
    {
        for (var i = 0; i < _tabs.Length; i++)
        {
            var selected = i == (int)_tab;
            _tabs[i].IsChecked = selected;
            // Roving tab index: only the selected tab is a stop of Tab.
            _tabs[i].IsTabStop = selected;
        }
    }

    private void OnTabKeyDown(int index, KeyEventArgs e)
    {
        var next = e.Key switch
        {
            Key.Right => (index + 1) % _tabs.Length,
            Key.Left => (index + _tabs.Length - 1) % _tabs.Length,
            Key.Home => 0,
            Key.End => _tabs.Length - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        SelectTab((ResponseTab)next, focus: true);
    }

    #endregion

    #region Keyboard

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl or Cmd with Enter sends from anywhere in the block; plain Enter in the endpoint field does too.
        var mod = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var inEndpoint = e.Source is Visual source && _endpointInput is not null && source.GetSelfAndVisualAncestors().Contains(_endpointInput);
        if (e.Key == Key.Enter && (mod || (inEndpoint && e.KeyModifiers == KeyModifiers.None)))
        {
            e.Handled = true;
            _ = SendAsync();
        }
    }

    #endregion

    #region Rows

    private static string TimeText(DateTimeOffset time) => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private string OutcomeText(WebhookTestResponse response)
    {
        if (response.TimedOut)
        {
            return Labels.ChipTimeout;
        }
        return response.Status == 0 ? Labels.ChipFailed : response.Status.ToString(CultureInfo.InvariantCulture);
    }

    private void OnDeliveriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_built)
        {
            RebuildRows();
        }
    }

    private void RebuildRows()
    {
        if (_rows is null)
        {
            return;
        }
        var labels = Labels;
        _rows.Children.Clear();
        foreach (var delivery in _deliveries)
        {
            _rows.Children.Add(BuildRow(delivery, labels));
        }
        _noRows!.IsVisible = _deliveries.Count == 0;
        var failed = _deliveries.Count(d => d.IsFailed);
        _failedBadge!.IsVisible = failed > 0;
        _failedText!.Text = labels.FailedCount(failed);
        SyncRowSelection();
    }

    private Control BuildRow(WebhookTestDelivery delivery, WebhookTesterLabels labels)
    {
        var response = delivery.Response;
        var tone = response.IsSuccess ? WebhookTesterChipTone.Success : WebhookTesterChipTone.Danger;
        var chip = WebhookTesterUi.Chip(OutcomeText(response), tone);
        var name = WebhookTesterUi.Text(delivery.Event, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, lineHeight: 19.6);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.VerticalAlignment = VerticalAlignment.Center;
        var time = WebhookTesterUi.Text(TimeText(delivery.Time), ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, mono: true);
        time.VerticalAlignment = VerticalAlignment.Center;
        time.IsVisible = !_compact;
        var inner = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,96"), ColumnSpacing = 12 };
        inner.Children.Add(chip);
        Grid.SetColumn(name, 1);
        inner.Children.Add(name);
        Grid.SetColumn(time, 2);
        inner.Children.Add(time);

        var pick = new WebhookTesterPick { Content = inner, Classes = { "row" }, Tag = delivery };
        AutomationProperties.SetControlTypeOverride(pick, AutomationControlType.ListItem);
        AutomationProperties.SetName(pick, labels.DeliveryName(delivery.Event, OutcomeText(response), TimeText(delivery.Time)));
        pick.Click += (_, _) => SelectDelivery(delivery);

        var row = new Grid { Tag = delivery };
        row.Children.Add(pick);
        Control action;
        if (delivery.IsFailed)
        {
            var retrying = _retrying.Contains(delivery.Id);
            var retry = new Button
            {
                Variant = ButtonVariant.Secondary,
                Size = ControlSize.Small,
                IsLoading = retrying,
                Content = retrying ? labels.Retrying : labels.Retry,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Classes = { "retry" },
            };
            AutomationProperties.SetName(retry, $"{labels.Retry} {delivery.Event}");
            retry.Click += (_, _) => _ = RetryAsync(delivery);
            action = retry;
        }
        else
        {
            var duration = WebhookTesterUi.Text(labels.StepDuration(response.DurationMs), ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, mono: true);
            duration.HorizontalAlignment = HorizontalAlignment.Right;
            duration.VerticalAlignment = VerticalAlignment.Center;
            duration.Margin = new Thickness(0, 0, 16, 0);
            duration.IsHitTestVisible = false;
            action = duration;
        }
        row.Children.Add(action);
        return row;
    }

    private void SelectDelivery(WebhookTestDelivery delivery) => SetCurrentValue(SelectedDeliveryProperty, delivery);

    private void SyncRowSelection()
    {
        if (_rows is null)
        {
            return;
        }
        foreach (var row in _rows.Children.OfType<Grid>())
        {
            if (row.Children.OfType<WebhookTesterPick>().FirstOrDefault() is { } pick)
            {
                pick.IsChecked = ReferenceEquals(row.Tag, SelectedDelivery);
            }
        }
    }

    #endregion
}
