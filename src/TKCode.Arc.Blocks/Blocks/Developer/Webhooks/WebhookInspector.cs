using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The indented, colored JSON of a payload (Arc Webhooks, design reference): 12/20 mono, line numbers 40 wide and
/// right-aligned with 16 after them (TextMuted, not selectable), keys Foreground, strings TextSecondary, numbers and
/// literals Accent, punctuation TextMuted; padding 12 16 12 0; lines never wrap (the panel scrolls). Text that is not
/// JSON is shown as it is. Built once per delivery: nothing animates inside.
/// </summary>
internal sealed class WebhookPayloadView : Grid
{
    public WebhookPayloadView(string? payload)
    {
        ColumnDefinitions = new ColumnDefinitions("56,*");
        Margin = new Thickness(0, 12, 16, 12);
        var lines = WebhookJson.Format(payload);
        Text = lines is null ? payload ?? string.Empty : WebhookJson.ToText(lines);
        var numbers = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, mono: true, lineHeight: 20);
        numbers.TextAlignment = TextAlignment.Right;
        numbers.Margin = new Thickness(0, 0, 16, 0);
        var code = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, mono: true, lineHeight: 20);
        code.TextWrapping = TextWrapping.NoWrap;
        var count = lines?.Count ?? Math.Max(1, Text.Split('\n').Length);
        numbers.Text = string.Join("\n", Enumerable.Range(1, count));
        if (lines is null)
        {
            code.Text = Text;
        }
        else
        {
            var inlines = new InlineCollection();
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    inlines.Add(new LineBreak());
                }
                foreach (var span in lines[i])
                {
                    var run = new Run(span.Text);
                    var color = span.Kind switch
                    {
                        WebhookJsonKind.Key => ArcDesignTokenKind.Foreground,
                        WebhookJsonKind.String => ArcDesignTokenKind.TextSecondary,
                        WebhookJsonKind.Number or WebhookJsonKind.Literal => ArcDesignTokenKind.Accent,
                        _ => ArcDesignTokenKind.TextMuted,
                    };
                    ArcTokens.Bind(run, TextElement.ForegroundProperty, color);
                    inlines.Add(run);
                }
            }
            code.Inlines = inlines;
        }
        LineCount = count;
        Children.Add(numbers);
        SetColumn(code, 1);
        Children.Add(code);
    }

    /// <summary>The indented text (what Copy copies).</summary>
    public string Text { get; }

    /// <summary>How many lines are shown.</summary>
    public int LineCount { get; }
}

/// <summary>
/// The headers of a request as name / value rows (padding 12 16, 8 between rows): the name 12/20 TextMuted in a 144
/// column, the value 12/20 mono Foreground breaking anywhere, 12 between; at ≤ 420 the name sits above the value.
/// </summary>
internal sealed class WebhookHeadersView : StackPanel
{
    private readonly double _stackedWidth;
    private readonly List<(Grid Row, TextBlock Name, TextBlock Value)> _rows = new();
    private bool? _stacked;

    public WebhookHeadersView(IEnumerable<WebhookHeader> headers, double stackedWidth)
    {
        _stackedWidth = stackedWidth;
        Spacing = 8;
        Margin = new Thickness(16, 12);
        foreach (var header in headers)
        {
            var name = WebhookUi.Text(header.Name, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 20);
            var value = WebhookUi.Text(header.Value, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, mono: true, lineHeight: 20);
            value.TextWrapping = TextWrapping.Wrap;
            var row = new Grid { ColumnSpacing = 12 };
            row.Children.Add(name);
            row.Children.Add(value);
            _rows.Add((row, name, value));
            Children.Add(row);
        }
        Text = WebhookMath.HeadersText(headers);
        Apply(false);
    }

    /// <summary>"Name: value" lines (what Copy copies).</summary>
    public string Text { get; }

    /// <summary>Whether names sit above values.</summary>
    public bool IsStacked => _stacked == true;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            Apply(availableSize.Width + Margin.Left + Margin.Right <= _stackedWidth);
        }
        return base.MeasureOverride(availableSize);
    }

    private void Apply(bool stacked)
    {
        if (_stacked == stacked)
        {
            return;
        }
        _stacked = stacked;
        foreach (var (row, name, value) in _rows)
        {
            if (stacked)
            {
                row.ColumnDefinitions = new ColumnDefinitions("*");
                row.RowDefinitions = new RowDefinitions("Auto,Auto");
                SetGridCell(value, 1, 0);
            }
            else
            {
                row.ColumnDefinitions = new ColumnDefinitions("144,*");
                row.RowDefinitions = new RowDefinitions("Auto");
                SetGridCell(value, 0, 1);
            }
        }
    }

    private static void SetGridCell(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
    }
}

/// <summary>
/// The payload / headers panel's frame: 1px Border, radius 10, the row-hover fill, and a 2px focus ring of radius 14 when
/// the scrolling area inside gets keyboard focus (Arc: the tabpanel is a tab stop and scrolls with the arrow keys).
/// </summary>
internal sealed class WebhookPanelFrame : ContentControl
{
    /// <summary>Whether the focus ring shows.</summary>
    public static readonly StyledProperty<bool> ShowsRingProperty =
        AvaloniaProperty.Register<WebhookPanelFrame, bool>(nameof(ShowsRing));

    /// <summary>The panel's fill (the row plate's color).</summary>
    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<WebhookPanelFrame, IBrush?>(nameof(HoverBrush));

    public bool ShowsRing { get => GetValue(ShowsRingProperty); set => SetValue(ShowsRingProperty, value); }

    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(WebhookPanelFrame);
}

/// <summary>
/// The delivery inspector (Arc Webhooks, design reference; padding 16 20 20, 12 between parts): the event (mono 14 500)
/// and "id · try N" (mono 12 TextMuted), both blur-swapped, with Copy at the right; the response (label, status badge,
/// "status in duration", body in mono 12/18 breaking anywhere) blur-swapped as one block that leaves the layout while it
/// fades, so nothing under it moves (Arc pushes the tabs down ≈22px for 160ms); the Payload / Headers tabs; and the
/// panel (fills the height beside the deliveries, 256 high when stacked) whose content blur-swaps. With no delivery it
/// shows one muted line instead.
/// </summary>
internal sealed class WebhookInspector : Border
{
    private readonly Webhooks _owner;
    private readonly Grid _grid;
    private readonly ArcContentSwap _title;
    private readonly ArcContentSwap _subtitle;
    private readonly CopyButton _copy;
    private readonly ArcContentSwap _response;
    private readonly SegmentedControl _tabs;
    private readonly WebhookPanelFrame _frame;
    private readonly ScrollArea _scroll;
    private readonly ArcContentSwap _panel;
    private readonly TextBlock _placeholder;
    private WebhookDelivery? _delivery;
    private WebhookPayloadView? _payload;
    private WebhookHeadersView? _headers;
    private bool _syncingTab;

    public WebhookInspector(Webhooks owner)
    {
        _owner = owner;
        var labels = owner.Labels;
        _title = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, HorizontalContentAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(_title, TextElement.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(_title, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_title, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _title.SetValue(TextElement.FontWeightProperty, FontWeight.Medium);
        _subtitle = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, HorizontalContentAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(_subtitle, TextElement.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(_subtitle, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_subtitle, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var heading = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(_title);
        heading.Children.Add(_subtitle);
        _copy = new CopyButton
        {
            Label = labels.Copy,
            CopiedLabel = labels.Copied,
            ResetAfter = DeveloperSprings.CopyReset,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _copy.PropertyChanged += OnCopyChanged;
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        head.Children.Add(heading);
        Grid.SetColumn(_copy, 1);
        head.Children.Add(_copy);

        _response = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top,
        };

        _tabs = new SegmentedControl
        {
            ItemsSource = new[]
            {
                new SegmentedOption(WebhookInspectorTab.Payload, labels.Payload),
                new SegmentedOption(WebhookInspectorTab.Headers, labels.Headers),
            },
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(_tabs, labels.TabsName);
        _tabs.SelectionChanged += OnTabChanged;

        _panel = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        _scroll = new ScrollArea
        {
            Content = _panel,
            Focusable = true,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        _scroll.GotFocus += (_, e) => _frame!.ShowsRing = e.NavigationMethod != NavigationMethod.Pointer;
        _scroll.LostFocus += (_, _) => _frame!.ShowsRing = false;
        _frame = new WebhookPanelFrame { Content = _scroll };
        _frame[!WebhookPanelFrame.HoverBrushProperty] = owner[!Webhooks.PlateBrushProperty];
        _frame.MinHeight = owner.MinPanelHeight;

        _grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), RowSpacing = 12 };
        _grid.Children.Add(head);
        Grid.SetRow(_response, 1);
        _grid.Children.Add(_response);
        Grid.SetRow(_tabs, 2);
        _grid.Children.Add(_tabs);
        Grid.SetRow(_frame, 3);
        _grid.Children.Add(_frame);

        _placeholder = WebhookUi.Text(labels.InspectorPlaceholder, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextMuted, lineHeight: 19.6);
        _placeholder.TextWrapping = TextWrapping.Wrap;
        _placeholder.Margin = new Thickness(0, 8);

        var host = new Panel();
        host.Children.Add(_grid);
        host.Children.Add(_placeholder);
        Child = host;
        Padding = new Thickness(owner.Gutter, 16, owner.Gutter, 20);
        Show(null, false);
    }

    /// <summary>The delivery shown.</summary>
    public WebhookDelivery? Delivery => _delivery;

    /// <summary>The Copy button (tests).</summary>
    public CopyButton CopyButton => _copy;

    /// <summary>The tabs.</summary>
    public SegmentedControl Tabs => _tabs;

    /// <summary>The scrolling panel (a tab stop).</summary>
    public ScrollArea Panel => _scroll;

    /// <summary>The panel's frame.</summary>
    public WebhookPanelFrame Frame => _frame;

    /// <summary>The response block swap (tests: its old content leaves the layout).</summary>
    public ArcContentSwap ResponseSwap => _response;

    /// <summary>The payload view of the shown delivery.</summary>
    public WebhookPayloadView? PayloadView => _payload;

    /// <summary>The headers view of the shown delivery.</summary>
    public WebhookHeadersView? HeadersView => _headers;

    /// <summary>What Copy copies now.</summary>
    public string CopyText => _owner.InspectorTab == WebhookInspectorTab.Headers ? _headers?.Text ?? string.Empty : _payload?.Text ?? string.Empty;

    /// <summary>Panel height cap (stacked: 256; beside the deliveries: none, it fills).</summary>
    public void ApplyLayout(bool stacked, double gutter)
    {
        Padding = new Thickness(gutter, 16, gutter, 20);
        _frame.Height = stacked ? _owner.StackedPanelHeight : double.NaN;
    }

    /// <summary>Shows <paramref name="delivery"/>; the parts blur-swap when <paramref name="animate"/>.</summary>
    public void Show(WebhookDelivery? delivery, bool animate)
    {
        var labels = _owner.Labels;
        _delivery = delivery;
        _grid.IsVisible = delivery is not null;
        _placeholder.IsVisible = delivery is null;
        if (delivery is null)
        {
            _payload = null;
            _headers = null;
            _copy.Value = null;
            return;
        }
        SetMode(animate);
        _title.Content = delivery.EventType;
        _subtitle.Content = labels.InspectorSubtitle(delivery.EventId ?? string.Empty, delivery.Attempt);
        _response.Content = BuildResponse(delivery);
        _payload = new WebhookPayloadView(delivery.Payload);
        _headers = new WebhookHeadersView(delivery.Headers, _owner.HeadersStackedWidth);
        SyncTab(animate);
    }

    /// <summary>Follows <see cref="Webhooks.InspectorTab"/>.</summary>
    public void SyncTab(bool animate)
    {
        _syncingTab = true;
        _tabs.SelectedIndex = _owner.InspectorTab == WebhookInspectorTab.Headers ? 1 : 0;
        _syncingTab = false;
        _panel.Mode = animate ? ContentSwapMode.BlurSwap : ContentSwapMode.None;
        var next = _owner.InspectorTab == WebhookInspectorTab.Headers ? (Control?)_headers : _payload;
        if (!ReferenceEquals(_panel.Content, next))
        {
            // Payload and headers share one scroll area; each starts at its first line, not where the other was left.
            _scroll.Offset = default;
        }
        _panel.Content = next;
        _copy.Value = CopyText;
        AutomationProperties.SetName(_scroll, _owner.InspectorTab == WebhookInspectorTab.Headers ? _owner.Labels.Headers : _owner.Labels.Payload);
    }

    private void SetMode(bool animate)
    {
        var mode = animate ? ContentSwapMode.BlurSwap : ContentSwapMode.None;
        _title.Mode = mode;
        _subtitle.Mode = mode;
        _response.Mode = mode;
    }

    private Control BuildResponse(WebhookDelivery delivery)
    {
        var labels = _owner.Labels;
        var status = WebhookMath.StatusTextOf(delivery, labels.NoResponse);
        var badge = new Badge
        {
            Size = BadgeSize.Small,
            Tone = delivery.IsFailure ? BadgeTone.Danger : BadgeTone.Success,
            Content = WebhookMath.CodeText(delivery.StatusCode),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var line = WebhookUi.Text(labels.ResponseLine(status, WebhookMath.FormatDuration(delivery.Duration)),
            ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, lineHeight: 16.8);
        line.VerticalAlignment = VerticalAlignment.Center;
        var label = WebhookUi.Text(labels.Response, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        label.VerticalAlignment = VerticalAlignment.Center;
        var block = new StackPanel { Spacing = 6 };
        block.Children.Add(WebhookUi.Row(8, label, badge, line));
        if (!string.IsNullOrEmpty(delivery.ResponseBody))
        {
            var body = WebhookUi.Text(delivery.ResponseBody, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, mono: true, lineHeight: 18);
            body.TextWrapping = TextWrapping.Wrap;
            block.Children.Add(body);
        }
        return block;
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingTab || _tabs.SelectedIndex < 0)
        {
            return;
        }
        _owner.SetCurrentValue(Webhooks.InspectorTabProperty,
            _tabs.SelectedIndex == 1 ? WebhookInspectorTab.Headers : WebhookInspectorTab.Payload);
    }

    private void OnCopyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CopyButton.StateProperty && _copy.State == CopyState.Copied)
        {
            _owner.OnInspectorCopied(_owner.InspectorTab == WebhookInspectorTab.Headers ? WebhookCopiedContent.Headers : WebhookCopiedContent.Payload, CopyText);
        }
    }
}

/// <summary>
/// Lays the deliveries and the inspector side by side (1fr 1fr; the inspector as tall as the deliveries, or as its own
/// content needs) or stacked (at ≤ 880). The deliveries column carries the dividing line: right edge side by side, bottom
/// edge stacked.
/// </summary>
internal sealed class WebhookSplitPanel : Panel
{
    /// <summary>Whether the two parts stack.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<WebhookSplitPanel, bool>(nameof(IsStacked));

    static WebhookSplitPanel()
    {
        AffectsMeasure<WebhookSplitPanel>(IsStackedProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    private double _height;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }
        var left = Children[0];
        var right = Children[1];
        var width = availableSize.Width;
        if (IsStacked || double.IsInfinity(width))
        {
            left.Measure(new Size(width, double.PositiveInfinity));
            right.Measure(new Size(width, double.PositiveInfinity));
            _height = left.DesiredSize.Height + right.DesiredSize.Height;
            return new Size(Math.Max(left.DesiredSize.Width, right.DesiredSize.Width), _height);
        }
        var half = width / 2;
        left.Measure(new Size(half, double.PositiveInfinity));
        var leftHeight = left.DesiredSize.Height;
        right.Measure(new Size(half, leftHeight));
        _height = Math.Max(leftHeight, right.DesiredSize.Height);
        return new Size(width, _height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }
        var left = Children[0];
        var right = Children[1];
        if (IsStacked)
        {
            var top = left.DesiredSize.Height;
            left.Arrange(new Rect(0, 0, finalSize.Width, top));
            right.Arrange(new Rect(0, top, finalSize.Width, Math.Max(0, finalSize.Height - top)));
            return finalSize;
        }
        var half = finalSize.Width / 2;
        left.Arrange(new Rect(0, 0, half, finalSize.Height));
        right.Arrange(new Rect(half, 0, finalSize.Width - half, finalSize.Height));
        return finalSize;
    }
}

/// <summary>
/// One endpoint's lower half: "Recent deliveries" with its list (rows grow in with a flash; a gliding plate with a 1px
/// Border ring marks the inspected one) or the empty state with "Send test event", and the inspector. The block puts a
/// new one in a crossfade whenever the endpoint changes, so the old and new halves overlap without pushing anything.
/// </summary>
internal sealed class WebhookSplitView : Border
{
    private readonly Webhooks _owner;
    private readonly WebhookSplitPanel _split;
    private readonly Border _left;
    private readonly DeveloperListPanel _list;
    private readonly GlidingIndicator _highlight;
    private readonly PresenceHost _emptyHost;
    private readonly Button _sendTest;
    private readonly WebhookInspector _inspector;
    private readonly TextBlock _emptyHint;
    private bool? _twoLine;
    private WebhookDelivery? _selected;
    private DateTime _glideUntil;
    private bool _sendingTest;

    public WebhookSplitView(Webhooks owner, WebhookEndpoint endpoint)
    {
        _owner = owner;
        Endpoint = endpoint;
        var labels = owner.Labels;
        var heading = WebhookUi.Text(labels.RecentDeliveries, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, FontWeight.Medium, lineHeight: 13.2);
        heading.Margin = new Thickness(owner.Gutter, 0, owner.Gutter, 6);
        _highlight = new GlidingIndicator
        {
            StrokeThickness = 1,
            IsHitTestVisible = false,
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        _highlight[!GlidingIndicator.FillProperty] = owner[!Webhooks.PlateBrushProperty];
        ArcTokens.Bind(_highlight, GlidingIndicator.StrokeProperty, ArcDesignTokenKind.Border);
        _list = new DeveloperListPanel
        {
            Motion = DeveloperListMotion.Insert,
            FlashInset = owner.RowPlateInset,
            FlashCornerRadius = new CornerRadius(10),
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<WebhookDelivery>((d, _) => CreateRow(owner, endpoint, d)),
            Items = endpoint.Deliveries,
            VerticalAlignment = VerticalAlignment.Top,
        };
        AutomationProperties.SetName(_list, labels.DeliveriesListName(endpoint.DisplayName));
        var listHost = new Panel();
        listHost.Children.Add(_highlight);
        listHost.Children.Add(_list);
        _list.LayoutUpdated += (_, _) => FollowLayout();

        var emptyTitle = WebhookUi.Text(labels.EmptyTitle, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 19.6);
        _emptyHint = WebhookUi.Text(labels.EmptyHint(endpoint.Host), ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        _emptyHint.TextWrapping = TextWrapping.Wrap;
        _emptyHint.MaxWidth = 360;
        _emptyHint.HorizontalAlignment = HorizontalAlignment.Left;
        _emptyHint.Margin = new Thickness(0, 4, 0, 0);
        _sendTest = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
        };
        _sendTest.Click += OnSendTestClick;
        var empty = new StackPanel { Margin = new Thickness(owner.Gutter, 8, owner.Gutter, 0) };
        empty.Children.Add(emptyTitle);
        empty.Children.Add(_emptyHint);
        empty.Children.Add(_sendTest);
        _emptyHost = new PresenceHost { Child = empty, Mode = PresenceMode.Fade, Collapse = PresenceCollapse.Height, IsPresent = endpoint.Deliveries.Count == 0 };

        var column = new StackPanel();
        column.Children.Add(heading);
        column.Children.Add(listHost);
        column.Children.Add(_emptyHost);
        _left = new Border { Padding = new Thickness(0, 12), Child = column };
        ArcTokens.Bind(_left, Border.BorderBrushProperty, ArcDesignTokenKind.Border);

        _inspector = new WebhookInspector(owner);
        _split = new WebhookSplitPanel();
        _split.Children.Add(_left);
        _split.Children.Add(_inspector);
        Child = _split;
        RefreshSendTest();
        endpoint.Deliveries.CollectionChanged += OnDeliveriesChanged;
        ApplyLayout(owner.IsStacked, owner.Gutter);
    }

    /// <summary>The endpoint.</summary>
    public WebhookEndpoint Endpoint { get; }

    private static WebhookDeliveryRow CreateRow(Webhooks owner, WebhookEndpoint endpoint, WebhookDelivery delivery)
    {
        var row = new WebhookDeliveryRow(owner, endpoint, delivery);
        row[!WebhookRowFrame.HoverBrushProperty] = owner[!Webhooks.PlateBrushProperty];
        return row;
    }

    /// <summary>The delivery list.</summary>
    public DeveloperListPanel List => _list;

    /// <summary>The inspected delivery's plate.</summary>
    public GlidingIndicator Highlight => _highlight;

    /// <summary>The inspector.</summary>
    public WebhookInspector Inspector => _inspector;

    /// <summary>The empty state (present while there is no delivery).</summary>
    public PresenceHost EmptyState => _emptyHost;

    /// <summary>The empty state's button.</summary>
    public Button SendTestButton => _sendTest;

    /// <summary>The row of a delivery, if it is shown.</summary>
    public WebhookDeliveryRow? RowFor(WebhookDelivery delivery) => _list.ContainerFor(delivery) as WebhookDeliveryRow;

    /// <summary>Side by side or stacked; gutters.</summary>
    public void ApplyLayout(bool stacked, double gutter)
    {
        _split.IsStacked = stacked;
        _left.BorderThickness = stacked ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 0);
        _inspector.ApplyLayout(stacked, gutter);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            var listWidth = _split.IsStacked ? availableSize.Width : availableSize.Width / 2;
            SetTwoLine(listWidth <= _owner.DeliveryTwoLineWidth);
        }
        return base.MeasureOverride(availableSize);
    }

    private void SetTwoLine(bool twoLine)
    {
        if (_twoLine == twoLine)
        {
            return;
        }
        _twoLine = twoLine;
        foreach (var item in _list.LiveItems)
        {
            if (_list.ContainerFor(item) is WebhookDeliveryRow row)
            {
                row.ApplyLayout(twoLine);
            }
        }
        _list.ItemPrepared -= OnItemPrepared;
        _list.ItemPrepared += OnItemPrepared;
    }

    private void OnItemPrepared(object? sender, DeveloperListItemEventArgs e)
    {
        if (e.Visual is WebhookDeliveryRow row)
        {
            row.ApplyLayout(_twoLine == true);
            row.IsSelected = ReferenceEquals(row.Delivery, _selected);
        }
    }

    /// <summary>Marks <paramref name="delivery"/> as inspected: the plate glides to it and the inspector follows.</summary>
    public void Select(WebhookDelivery? delivery, bool animate)
    {
        var changed = !ReferenceEquals(_selected, delivery);
        _selected = delivery;
        foreach (var item in _list.LiveItems)
        {
            if (_list.ContainerFor(item) is WebhookDeliveryRow row)
            {
                row.IsSelected = ReferenceEquals(item, delivery);
            }
        }
        var glide = animate && changed && ArcMotion.IsFull(this);
        if (glide)
        {
            _glideUntil = DateTime.UtcNow.AddMilliseconds(700);
        }
        PlaceHighlight(glide);
        if (changed || _inspector.Delivery is null && delivery is not null)
        {
            _inspector.Show(delivery, animate && _inspector.Delivery is not null);
        }
    }

    /// <summary>Re-reads the tab.</summary>
    public void SyncTab(bool animate) => _inspector.SyncTab(animate);

    private void FollowLayout() => PlaceHighlight(DateTime.UtcNow < _glideUntil);

    private void PlaceHighlight(bool glide)
    {
        if (_selected is null || _list.ContainerFor(_selected) is not { } row || _list.StateOf(_selected) is not { } state || state.IsLeaving)
        {
            _highlight.Hide(animate: false);
            return;
        }
        var inset = _owner.RowPlateInset;
        var rect = new Rect(row.Bounds.X + inset.Left, row.Bounds.Y + state.Offset + inset.Top,
            Math.Max(0, row.Bounds.Width - inset.Left - inset.Right), Math.Max(0, row.Bounds.Height - inset.Top - inset.Bottom));
        if (rect == _highlight.CurrentRect && _highlight.CurrentOpacity >= 1)
        {
            return;
        }
        if (glide && _highlight.CurrentOpacity > 0)
        {
            _highlight.MoveTo(rect, DeveloperSprings.Smooth);
        }
        else
        {
            _highlight.MoveTo(rect, animate: false);
        }
    }

    private void OnDeliveriesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _emptyHost.IsPresent = Endpoint.Deliveries.Count == 0;
    }

    /// <summary>Busy or idle "Send test event".</summary>
    public void SetSendingTest(bool sending)
    {
        _sendingTest = sending;
        RefreshSendTest();
    }

    private void RefreshSendTest()
    {
        var labels = _owner.Labels;
        _sendTest.Content = _sendingTest
            ? WebhookUi.ButtonLabel(WebhookUi.Spinner(), labels.SendingTest)
            : (object)labels.SendTest;
        AutomationProperties.SetName(_sendTest, _sendingTest ? labels.SendingTest : labels.SendTest);
    }

    private void OnSendTestClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_sendingTest)
        {
            _ = _owner.SendTestFromViewAsync(Endpoint);
        }
    }

    /// <summary>Stops following the endpoint once the view is gone (the crossfade removed it).</summary>
    public void Release()
    {
        Endpoint.Deliveries.CollectionChanged -= OnDeliveriesChanged;
        _list.Items = null;
    }
}
