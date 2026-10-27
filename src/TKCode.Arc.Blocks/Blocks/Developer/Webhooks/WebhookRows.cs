using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An endpoint row (Arc Webhooks, design reference): the whole row is one button (padding 10 20) over four columns
/// 12fr / 13fr / 72 / 104 with 20 between: name and URL (host darker than the path, mono), up to three event chips and
/// "+N", the rate (slot swap) over "success", and the health badge. Narrow (≤ 760): name and URL at the left, rate over
/// badge at the right, no chips. Rows after the first have a 1px BorderSubtle line on top.
/// </summary>
internal sealed class WebhookEndpointRow : WebhookRowFrame
{
    private readonly Webhooks _owner;
    private readonly WebhookRowButton _button;
    private readonly Grid _grid;
    private readonly TextBlock _name;
    private readonly TextBlock _url;
    private readonly Run _host = new();
    private readonly Run _rest = new();
    private readonly WrapPanel _chips;
    private readonly StackPanel _rateCell;
    private readonly WebhookSlotText _rate;
    private readonly TextBlock _rateCaption;
    private readonly Badge _badge;
    private readonly StackPanel _right;
    private bool _narrow;
    private bool _attached;

    public WebhookEndpointRow(Webhooks owner, WebhookEndpoint endpoint)
    {
        _owner = owner;
        Endpoint = endpoint;
        _name = WebhookUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 19.6);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _url = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, mono: true, lineHeight: 16.8);
        _url.TextTrimming = TextTrimming.CharacterEllipsis;
        ArcTokens.Bind(_host, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _url.Inlines = new InlineCollection { _host, _rest };
        var identity = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(_name);
        identity.Children.Add(_url);

        _chips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        _rate = new WebhookSlotText { FontWeight = FontWeight.Medium, LineHeight = 18.2, HorizontalAlignment = HorizontalAlignment.Right };
        ArcTokens.Bind(_rate, WebhookSlotText.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_rate, WebhookSlotText.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_rate, WebhookSlotText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _rateCaption = WebhookUi.Text(owner.Labels.RateCaption, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        _rateCaption.HorizontalAlignment = HorizontalAlignment.Right;
        _rateCell = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        _rateCell.Children.Add(_rate);
        _rateCell.Children.Add(_rateCaption);

        _badge = new Badge { Size = BadgeSize.Small, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        _right = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

        _grid = new Grid { ColumnSpacing = owner.Metrics.RowColumnGap };
        _button = new WebhookRowButton
        {
            Item = endpoint,
            Content = _grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _button.Click += OnClick;
        _button.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        Content = _button;
        _identity = identity;
        ApplyLayout(owner.IsNarrow, owner.Gutter);
        Refresh();
    }

    private readonly StackPanel _identity;

    /// <summary>The endpoint.</summary>
    public WebhookEndpoint Endpoint { get; }

    /// <summary>The row's button (focus target).</summary>
    public WebhookRowButton Button => _button;

    /// <summary>The rate text (tests).</summary>
    public WebhookSlotText Rate => _rate;

    /// <summary>The badge (tests).</summary>
    public Badge HealthBadge => _badge;

    /// <summary>The chips panel (tests).</summary>
    public WrapPanel Chips => _chips;

    /// <summary>Wide: four columns; narrow (≤ 760): identity and a rate-over-badge stack, no chips.</summary>
    public void ApplyLayout(bool narrow, double gutter)
    {
        _button.Padding = new Thickness(gutter, 10);
        if (_narrow == narrow && _grid.Children.Count > 0)
        {
            return;
        }
        _narrow = narrow;
        _grid.Children.Clear();
        _right.Children.Clear();
        _rateCell.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        if (narrow)
        {
            _grid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
            _rateCell.Children.Add(_rate);
            _right.Children.Add(_rateCell);
            _right.Children.Add(_badge);
            Grid.SetColumn(_identity, 0);
            Grid.SetColumn(_right, 1);
            _grid.Children.Add(_identity);
            _grid.Children.Add(_right);
        }
        else
        {
            var m = _owner.Metrics;
            _grid.ColumnDefinitions = new ColumnDefinitions
            {
                new(12, GridUnitType.Star),
                new(13, GridUnitType.Star),
                new(m.RateColumnWidth, GridUnitType.Pixel),
                new(m.StateColumnWidth, GridUnitType.Pixel),
            };
            _rateCell.Children.Add(_rate);
            _rateCell.Children.Add(_rateCaption);
            Grid.SetColumn(_identity, 0);
            Grid.SetColumn(_chips, 1);
            Grid.SetColumn(_rateCell, 2);
            Grid.SetColumn(_badge, 3);
            _grid.Children.Add(_identity);
            _grid.Children.Add(_chips);
            _grid.Children.Add(_rateCell);
            _grid.Children.Add(_badge);
        }
    }

    /// <summary>Reads the endpoint again (name, URL, chips, rate, badge, accessible name).</summary>
    public void Refresh()
    {
        var labels = _owner.Labels;
        var endpoint = Endpoint;
        _name.Text = endpoint.DisplayName;
        var (host, rest) = WebhookMath.SplitUrl(endpoint.Url);
        _host.Text = host;
        _rest.Text = rest;
        _rateCaption.Text = labels.RateCaption;

        _chips.Children.Clear();
        var (shown, more) = WebhookMath.Chips(endpoint.Events, _owner.Metrics.MaxChips);
        foreach (var name in shown)
        {
            _chips.Children.Add(Chip(name));
        }
        if (more > 0)
        {
            _chips.Children.Add(Chip("+" + more));
        }

        var rateText = WebhookMath.FormatRate(endpoint.DeliveredRate);
        _rate.Text = rateText;
        var (tone, icon, text) = endpoint.Health switch
        {
            WebhookHealth.Failing => (BadgeTone.Warning, ArcIcons.TriangleAlert, labels.FailingBadge(endpoint.FailingCount)),
            WebhookHealth.Healthy => (BadgeTone.Success, ArcIcons.Check, labels.HealthyBadge),
            _ => (BadgeTone.Neutral, (Geometry?)null, labels.NoTrafficBadge),
        };
        _badge.Tone = tone;
        _badge.Icon = icon is null ? null : new ArcGlyph { Data = icon, Size = 12, StrokeThickness = 1.75 };
        _badge.Content = text;
        AutomationProperties.SetName(_button, labels.EndpointRowName(endpoint.DisplayName, endpoint.Host, rateText, text,
            string.Join(", ", endpoint.Events)));
    }

    private static Control Chip(string text)
    {
        var label = WebhookUi.Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, mono: true, lineHeight: 16.8);
        label.VerticalAlignment = VerticalAlignment.Center;
        var chip = new Border
        {
            Height = 22,
            Padding = new Thickness(8, 0),
            Margin = new Thickness(0, 2, 4, 2),
            Child = label,
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(chip, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(chip, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(chip, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        return chip;
    }

    private void OnClick(object? sender, RoutedEventArgs e) => _owner.SelectEndpointFromRow(Endpoint, WebhookChangeSource.Pointer);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_owner.MoveEndpointFocus(Endpoint, e.Key))
        {
            e.Handled = true;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_attached)
        {
            _attached = true;
            Endpoint.StatsChanged += OnStatsChanged;
            Endpoint.Events.CollectionChanged += OnEventsChanged;
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_attached)
        {
            _attached = false;
            Endpoint.StatsChanged -= OnStatsChanged;
            Endpoint.Events.CollectionChanged -= OnEventsChanged;
        }
    }

    private void OnStatsChanged(object? sender, EventArgs e) => Refresh();

    private void OnEventsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Refresh();
}

/// <summary>
/// A delivery row (Arc Webhooks, design reference): 40 high; the button (padding 6 4 6 20) holds the status badge (44),
/// the event in mono with "Try N" / "Test", the duration and the time (52 each, 12 between); the action column (100,
/// 16 at the right) holds Retry, "✓ Resolved" or "Retried". Two-line rows (the list ≤ 460 wide) put the duration and
/// time under the event.
/// </summary>
internal sealed class WebhookDeliveryRow : WebhookRowFrame
{
    private readonly Webhooks _owner;
    private readonly WebhookEndpoint _endpoint;
    private readonly WebhookRowButton _button;
    private readonly Grid _content;
    private readonly Badge _code;
    private readonly WebhookEventText _event;
    private readonly TextBlock _suffix;
    private readonly StackPanel _eventCell;
    private readonly TextBlock _duration;
    private readonly TextBlock _time;
    private readonly StackPanel _meta;
    private readonly ArcContentSwap _action;
    private readonly Button _retry;
    private Control? _resolved;
    private Control? _retried;
    private bool _twoLine;
    private bool _attached;
    private bool _built;

    public WebhookDeliveryRow(Webhooks owner, WebhookEndpoint endpoint, WebhookDelivery delivery)
    {
        _owner = owner;
        _endpoint = endpoint;
        Delivery = delivery;
        _code = new Badge { Size = BadgeSize.Small, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        _event = new WebhookEventText { VerticalAlignment = VerticalAlignment.Center };
        _suffix = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        _suffix.VerticalAlignment = VerticalAlignment.Center;
        _eventCell = WebhookUi.Row(8, _event, _suffix);
        _eventCell.VerticalAlignment = VerticalAlignment.Center;
        _eventCell.ClipToBounds = true;
        _duration = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8);
        _time = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        _meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _content = new Grid { ColumnSpacing = 12 };
        _button = new WebhookRowButton
        {
            Item = delivery,
            Content = _content,
            Padding = new Thickness(20, 6, 4, 6),
            RingMargin = new Thickness(4, -2, -2, -2),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _button.Click += (_, _) => _owner.SelectDeliveryFromRow(Delivery, WebhookChangeSource.Pointer);
        _button.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        _retry = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, IsPressScaleEnabled = true };
        _retry.Click += OnRetryClick;
        _action = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 16, 0),
        };
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{owner.Metrics.ActionColumnWidth}") };
        root.Children.Add(_button);
        Grid.SetColumn(_action, 1);
        root.Children.Add(_action);
        Content = root;
        PlateInset = owner.Metrics.RowPlateInset;
        ApplyLayout(false);
        Refresh();
    }

    /// <summary>The delivery.</summary>
    public WebhookDelivery Delivery { get; }

    /// <summary>The row's button (focus target).</summary>
    public WebhookRowButton Button => _button;

    /// <summary>The retry button (present while the delivery is an open failure).</summary>
    public Button RetryButton => _retry;

    /// <summary>The action column (tests).</summary>
    public ArcContentSwap Action => _action;

    /// <summary>Whether the row shows two lines.</summary>
    public bool IsTwoLine => _twoLine;

    /// <summary>One line (event, duration and time side by side) or two (duration and time under the event).</summary>
    public void ApplyLayout(bool twoLine)
    {
        if (_built && _twoLine == twoLine)
        {
            return;
        }
        _built = true;
        _twoLine = twoLine;
        _content.Children.Clear();
        _meta.Children.Clear();
        _content.RowDefinitions.Clear();
        if (twoLine)
        {
            Height = double.NaN;
            MinHeight = _owner.Metrics.DeliveryRowHeight;
            _content.ColumnDefinitions = new ColumnDefinitions("44,*");
            _content.RowDefinitions = new RowDefinitions("Auto,Auto");
            _duration.HorizontalAlignment = HorizontalAlignment.Left;
            _time.HorizontalAlignment = HorizontalAlignment.Left;
            _meta.Children.Add(_duration);
            _meta.Children.Add(_time);
            Grid.SetRowSpan(_code, 2);
            Grid.SetColumn(_eventCell, 1);
            Grid.SetColumn(_meta, 1);
            Grid.SetRow(_meta, 1);
            _content.Children.Add(_code);
            _content.Children.Add(_eventCell);
            _content.Children.Add(_meta);
        }
        else
        {
            Height = _owner.Metrics.DeliveryRowHeight;
            _content.ColumnDefinitions = new ColumnDefinitions("44,*,52,52");
            _duration.HorizontalAlignment = HorizontalAlignment.Right;
            _time.HorizontalAlignment = HorizontalAlignment.Right;
            _duration.VerticalAlignment = VerticalAlignment.Center;
            _time.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRowSpan(_code, 1);
            Grid.SetRow(_meta, 0);
            Grid.SetColumn(_eventCell, 1);
            Grid.SetColumn(_duration, 2);
            Grid.SetColumn(_time, 3);
            _content.Children.Add(_code);
            _content.Children.Add(_eventCell);
            _content.Children.Add(_duration);
            _content.Children.Add(_time);
        }
    }

    /// <summary>Reads the delivery again.</summary>
    public void Refresh()
    {
        var labels = _owner.Labels;
        var d = Delivery;
        _code.Tone = d.IsFailure ? BadgeTone.Danger : BadgeTone.Success;
        _code.Content = WebhookMath.CodeText(d.StatusCode);
        _event.Text = d.EventType;
        _event.IsDimmed = d.IsResolved;
        _suffix.Text = d.IsTest ? labels.TestLabel : d.Attempt > 1 ? labels.TryLabel(d.Attempt) : null;
        _suffix.IsVisible = !string.IsNullOrEmpty(_suffix.Text);
        var duration = WebhookMath.FormatDuration(d.Duration);
        var time = WebhookMath.FormatTime(d.SentAt);
        _duration.Text = duration;
        _time.Text = time;
        var status = WebhookMath.StatusTextOf(d, labels.NoResponse);
        var name = labels.DeliveryRowName(WebhookMath.CodeText(d.StatusCode), status, d.EventType, duration, time);
        AutomationProperties.SetName(_button, d.IsResolved ? name + labels.ResolvedSuffix : name);
        RefreshAction();
    }

    /// <summary>Retry while open (busy while it waits), "✓ Resolved" once a retry went through, "Retried" when taken over.</summary>
    public void RefreshAction()
    {
        var labels = _owner.Labels;
        var d = Delivery;
        if (d.IsOpenFailure && _owner.ShowsRetry)
        {
            var busy = _owner.IsRetrying(d);
            _retry.Content = busy
                ? WebhookUi.ButtonLabel(WebhookUi.Spinner(), labels.Retrying)
                : WebhookUi.ButtonLabel(WebhookUi.Glyph(ArcIcons.RotateCw, 16, ArcDesignTokenKind.Foreground), labels.Retry);
            AutomationProperties.SetName(_retry, busy ? labels.RetryingName(d.EventType) : labels.RetryName(d.EventType, WebhookMath.FormatTime(d.SentAt)));
            _action.Content = _retry;
        }
        else if (d.IsResolved)
        {
            _resolved ??= WebhookUi.Row(6,
                WebhookUi.Glyph(ArcIcons.Check, 14, ArcDesignTokenKind.Success),
                WebhookUi.Text(labels.Resolved, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
            _action.Content = _resolved;
        }
        else if (d.IsFailure && d.RetriedBy is not null)
        {
            _retried ??= WebhookUi.Text(labels.Retried, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
            _action.Content = _retried;
        }
        else
        {
            _action.Content = null;
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!_owner.IsRetrying(Delivery))
        {
            _ = _owner.RetryFromRowAsync(_endpoint, Delivery);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_owner.MoveDeliveryFocus(_endpoint, Delivery, e.Key))
        {
            e.Handled = true;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_attached)
        {
            _attached = true;
            Delivery.PropertyChanged += OnDeliveryChanged;
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_attached)
        {
            _attached = false;
            Delivery.PropertyChanged -= OnDeliveryChanged;
        }
    }

    private void OnDeliveryChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();
}
