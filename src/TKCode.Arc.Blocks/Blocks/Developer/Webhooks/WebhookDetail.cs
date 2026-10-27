using Avalonia.Controls.Documents;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The selected endpoint's detail (Arc Webhooks, design reference): its name (display font 14/15.4 500) and a line of
/// who added it and when (20px generated avatar), the secret hint and the version, separated by 3px dots, both
/// blur-swapped as blocks when the endpoint changes; then the chart's title ("310 events ■ 3 failed", or the hovered
/// bucket's reading, swapped at once) and the traffic strip.
/// </summary>
internal sealed class WebhookDetail : StackPanel
{
    private readonly Webhooks _owner;
    private readonly Border _head;
    private readonly Border _chartBlock;
    private readonly ArcContentSwap _name;
    private readonly ArcContentSwap _meta;
    private readonly ArcContentSwap _title;
    private readonly WebhookVolumeStrip _strip;
    private WebhookEndpoint? _endpoint;
    private bool _watching;

    public WebhookDetail(Webhooks owner)
    {
        _owner = owner;
        _name = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, HorizontalContentAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(_name, TextElement.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_name, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_name, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _name.SetValue(TextElement.FontWeightProperty, FontWeight.Medium);
        AutomationProperties.SetHeadingLevel(_name, 3);
        _meta = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        var headStack = new StackPanel();
        headStack.Children.Add(_name);
        headStack.Children.Add(_meta);
        _head = new Border { Child = headStack };

        _title = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
        _strip = new WebhookVolumeStrip();
        _strip.HoverChanged += (_, _) => RefreshTitle(animate: false);
        var chart = new StackPanel();
        chart.Children.Add(_title);
        chart.Children.Add(_strip);
        _chartBlock = new Border { Child = chart };
        Children.Add(_head);
        Children.Add(_chartBlock);
        ApplyLayout(owner.Gutter);
    }

    /// <summary>The traffic strip.</summary>
    public WebhookVolumeStrip Strip => _strip;

    /// <summary>The chart title's swap (tests read its content).</summary>
    public ArcContentSwap ChartTitle => _title;

    /// <summary>The name's swap.</summary>
    public ArcContentSwap NameSwap => _name;

    /// <summary>The chart title's text right now (tests).</summary>
    public string ChartTitleText { get; private set; } = string.Empty;

    public void ApplyLayout(double gutter)
    {
        _head.Padding = new Thickness(gutter, 16, gutter, 0);
        _chartBlock.Padding = new Thickness(gutter, 16, gutter, 0);
        _strip.ChartHeight = _owner.ChartHeight;
        _strip.BarGap = _owner.BarGap;
    }

    /// <summary>Shows <paramref name="endpoint"/>; texts blur-swap and bars spring when <paramref name="animate"/>.</summary>
    public void Show(WebhookEndpoint? endpoint, bool animate)
    {
        if (!ReferenceEquals(_endpoint, endpoint))
        {
            Unwatch();
            _endpoint = endpoint;
            Watch();
        }
        IsVisible = endpoint is not null;
        if (endpoint is null)
        {
            return;
        }
        var mode = animate ? ContentSwapMode.BlurSwap : ContentSwapMode.None;
        _name.Mode = mode;
        _meta.Mode = mode;
        _name.Content = endpoint.DisplayName;
        _meta.Content = BuildMeta(endpoint);
        _strip.Hover(-1);
        Refresh(animate);
    }

    /// <summary>Reads the chart again (a retry, a test, a pushed delivery): bars spring, the title swaps.</summary>
    public void Refresh(bool animate)
    {
        if (_endpoint is not { } endpoint)
        {
            return;
        }
        var labels = _owner.Labels;
        _strip.SetData(endpoint.Volume, animate);
        var axis = new List<(int, string)>();
        for (var i = 0; i < endpoint.Volume.Count && i < 48; i += 12)
        {
            axis.Add((i, WebhookMath.FormatClock(endpoint.Volume[i].Start)));
        }
        if (endpoint.Volume.Count > 0)
        {
            _strip.SetAxis(axis, labels.AxisNow);
        }
        var (total, failed) = WebhookMath.Sum(endpoint.Volume);
        var busiest = WebhookMath.Busiest(endpoint.Volume);
        var busiestText = busiest >= 0
            ? labels.BucketRange(WebhookMath.FormatClock(endpoint.Volume[busiest].Start), WebhookMath.FormatClock(endpoint.Volume[busiest].End))
            : string.Empty;
        AutomationProperties.SetName(_strip, labels.ChartSummary(total, failed, busiestText, busiest >= 0 ? endpoint.Volume[busiest].Total : 0));
        RefreshTitle(animate);
    }

    private void RefreshTitle(bool animate)
    {
        if (_endpoint is not { } endpoint)
        {
            return;
        }
        var labels = _owner.Labels;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var hovered = _strip.HoveredIndex;
        string text;
        if (hovered >= 0 && hovered < endpoint.Volume.Count)
        {
            var bucket = endpoint.Volume[hovered];
            var range = labels.BucketRange(WebhookMath.FormatClock(bucket.Start), WebhookMath.FormatClock(bucket.End));
            var rangeText = WebhookUi.Text(range, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 16.8);
            row.Children.Add(rangeText);
            row.Children.Add(WebhookUi.Text(labels.ChartEvents(bucket.Total), ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8));
            text = range + " " + labels.ChartEvents(bucket.Total);
            if (bucket.Failed > 0)
            {
                row.Children.Add(Legend(labels.ChartFailed(bucket.Failed)));
                text += " " + labels.ChartFailed(bucket.Failed);
            }
            _title.Mode = ContentSwapMode.None;
        }
        else
        {
            var (total, failed) = WebhookMath.Sum(endpoint.Volume);
            if (total == 0)
            {
                text = labels.NoTrafficYet;
                row.Children.Add(WebhookUi.Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8));
            }
            else
            {
                text = labels.ChartEvents(total);
                row.Children.Add(WebhookUi.Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8));
                if (failed > 0)
                {
                    row.Children.Add(Legend(labels.ChartFailed(failed)));
                    text += " " + labels.ChartFailed(failed);
                }
                else
                {
                    row.Children.Add(WebhookUi.Text(labels.NoFailures, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
                    text += " " + labels.NoFailures;
                }
            }
            _title.Mode = animate ? ContentSwapMode.BlurSwap : ContentSwapMode.None;
        }
        if (text == ChartTitleText && _title.Content is not null)
        {
            return;
        }
        ChartTitleText = text;
        _title.Content = row;
    }

    private static Control Legend(string text)
    {
        var square = new Border { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center }.BindCornerRadius(ArcDesignTokenKind.RadiusStep3);
        ArcTokens.Bind(square, Border.BackgroundProperty, ArcDesignTokenKind.Danger);
        var label = WebhookUi.Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8);
        return WebhookUi.Row(6, square, label);
    }

    private Control BuildMeta(WebhookEndpoint endpoint)
    {
        var labels = _owner.Labels;
        var items = new List<Control>();
        string? date = endpoint.AddedOn is { } on ? on.ToString(labels.DateFormat, CultureInfo.CurrentCulture) : null;
        if (!string.IsNullOrWhiteSpace(endpoint.AddedBy))
        {
            var avatar = new Avatar
            {
                DisplayName = endpoint.AddedBy,
                Size = AvatarSize.Small,
                Width = 20,
                Height = 20,
                FontSize = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var who = WebhookUi.Text(date is null ? endpoint.AddedBy : labels.AddedBy(endpoint.AddedBy!, date),
                ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
            who.VerticalAlignment = VerticalAlignment.Center;
            items.Add(WebhookUi.Row(8, avatar, who));
        }
        else if (date is not null)
        {
            items.Add(WebhookUi.Text(labels.AddedOn(date), ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
        }
        if (!string.IsNullOrWhiteSpace(endpoint.SecretHint))
        {
            items.Add(WebhookUi.Text(endpoint.SecretHint, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, mono: true, lineHeight: 16.8));
        }
        if (!string.IsNullOrWhiteSpace(endpoint.ApiVersion))
        {
            items.Add(WebhookUi.Text(endpoint.ApiVersion, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
        }
        // Arc: every item carries its dot and the line is shifted 11px left and clipped, so no line (wrapped or not)
        // starts with a dot.
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-11, 0, 0, 0) };
        foreach (var item in items)
        {
            item.VerticalAlignment = VerticalAlignment.Center;
            var piece = WebhookUi.Row(8, WebhookUi.Dot(), item);
            piece.Margin = new Thickness(0, 2, 8, 2);
            wrap.Children.Add(piece);
        }
        return new Border { Child = wrap, ClipToBounds = true };
    }

    private void Watch()
    {
        if (_endpoint is null || _watching)
        {
            return;
        }
        _watching = true;
        _endpoint.Volume.CollectionChanged += OnVolumeChanged;
        _endpoint.StatsChanged += OnStatsChanged;
        foreach (var bucket in _endpoint.Volume)
        {
            bucket.PropertyChanged += OnBucketChanged;
        }
    }

    private void Unwatch()
    {
        if (_endpoint is null || !_watching)
        {
            return;
        }
        _watching = false;
        _endpoint.Volume.CollectionChanged -= OnVolumeChanged;
        _endpoint.StatsChanged -= OnStatsChanged;
        foreach (var bucket in _endpoint.Volume)
        {
            bucket.PropertyChanged -= OnBucketChanged;
        }
    }

    private void OnVolumeChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Unwatch();
        Watch();
        Refresh(animate: true);
    }

    private void OnBucketChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => _owner.QueueDetailRefresh();

    private void OnStatsChanged(object? sender, EventArgs e)
    {
        if (_endpoint is not null)
        {
            _name.Content = _endpoint.DisplayName;
        }
    }

    /// <summary>Stops following the endpoint (the block is going away).</summary>
    public void Release()
    {
        Unwatch();
        _endpoint = null;
    }
}
