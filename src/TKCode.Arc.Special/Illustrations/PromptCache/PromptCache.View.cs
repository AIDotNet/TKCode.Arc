using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

// The view of a PromptCache: template parts, the per-frame update from the story state, the breakpoints and the palette.
public partial class PromptCache
{
    private const double WideWidth = 660;
    private const double CompactWidth = 480;
    private const double TinyWidth = 260;

    private static readonly ArcDesignTokenKind[] s_series =
        [ArcDesignTokenKind.Series1, ArcDesignTokenKind.Series2, ArcDesignTokenKind.Series3, ArcDesignTokenKind.Series4];

    private TextBlock? _titleText;
    private ArcGlyph? _modelMark;
    private TextBlock? _modelName;
    private Control? _modelDot;
    private TextBlock? _ttlText;
    private Panel? _legend;
    private PromptCacheSplit? _split;
    private PromptCacheTable? _table;
    private Control? _cachePanel;
    private ArcRing? _ring;
    private TextBlock? _cacheName;
    private PromptCacheStatus? _cacheStatus;
    private PromptCacheBar? _cacheBar;
    private TextBlock? _cachedTokens;
    private PromptCachePrices? _prices;
    private ArcContentSwap? _captionSwap;
    private PromptCacheFooter? _footer;
    private TextBlock? _costValue;
    private ArcContentSwap? _costLabel;
    private TextBlock? _costDetail;
    private TextBlock? _speedValue;
    private TextBlock? _speedLabel;
    private TextBlock? _speedDetail;
    private ToggleButton? _toggle;
    private TextBlock? _toggleText;
    private PromptCacheFlight? _flight;
    private AvaloniaButton? _playPart;
    private AvaloniaButton? _restartPart;

    private PromptCacheRowView[] _views = [];
    private PromptCacheSegment[]? _viewSegments;
    private int _viewRows = -1;
    private PromptCachePalette? _palette;
    private bool _paletteDirty = true;
    private bool _syncingToggle;
    private string? _costLabelText;
    private int _shownRemaining = -1;
    private PromptCacheWidthMode _mode = PromptCacheWidthMode.Wide;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        var ns = e.NameScope;
        // These run before the base's own click handlers (subscribed in base.OnApplyTemplate), so a pause is known to
        // be the reader's.
        if (_playPart is not null)
        {
            _playPart.Click -= OnPlayPreClick;
            _playPart.Click -= OnPlayPostClick;
        }
        if (_restartPart is not null)
        {
            _restartPart.Click -= OnRestartPostClick;
        }
        _playPart = ns.Find<AvaloniaButton>(PlayButtonPart);
        _restartPart = ns.Find<AvaloniaButton>(RestartButtonPart);
        if (_playPart is not null)
        {
            _playPart.Click += OnPlayPreClick;
        }
        base.OnApplyTemplate(e);
        if (_playPart is not null)
        {
            _playPart.Click += OnPlayPostClick;
        }
        if (_restartPart is not null)
        {
            _restartPart.Click += OnRestartPostClick;
        }
        if (_toggle is not null)
        {
            _toggle.IsCheckedChanged -= OnToggleChanged;
        }
        _titleText = ns.Find<TextBlock>("PART_Title");
        _modelMark = ns.Find<ArcGlyph>("PART_ModelMark");
        _modelName = ns.Find<TextBlock>("PART_ModelName");
        _modelDot = ns.Find<Control>("PART_Dot");
        _ttlText = ns.Find<TextBlock>("PART_Ttl");
        _legend = ns.Find<Panel>("PART_Legend");
        _split = ns.Find<PromptCacheSplit>("PART_Split");
        _table = ns.Find<PromptCacheTable>(TablePart);
        _cachePanel = ns.Find<Control>("PART_CachePanel");
        _ring = ns.Find<ArcRing>("PART_Ring");
        _cacheName = ns.Find<TextBlock>("PART_CacheName");
        _cacheStatus = ns.Find<PromptCacheStatus>("PART_CacheStatus");
        _cacheBar = ns.Find<PromptCacheBar>(CacheBarPart);
        _cachedTokens = ns.Find<TextBlock>("PART_CachedTokens");
        _prices = ns.Find<PromptCachePrices>("PART_Prices");
        _captionSwap = ns.Find<ArcContentSwap>("PART_Caption");
        _footer = ns.Find<PromptCacheFooter>("PART_Footer");
        _costValue = ns.Find<TextBlock>("PART_CostValue");
        _costLabel = ns.Find<ArcContentSwap>("PART_CostLabel");
        _costDetail = ns.Find<TextBlock>("PART_CostDetail");
        _speedValue = ns.Find<TextBlock>("PART_SpeedValue");
        _speedLabel = ns.Find<TextBlock>("PART_SpeedLabel");
        _speedDetail = ns.Find<TextBlock>("PART_SpeedDetail");
        _toggle = ns.Find<ToggleButton>(TogglePart);
        _toggleText = ns.Find<TextBlock>("PART_ToggleText");
        _flight = ns.Find<PromptCacheFlight>("PART_Flight");
        if (_toggle is not null)
        {
            _toggle.IsCheckedChanged += OnToggleChanged;
        }
        if (_prices is not null && _prices.Children.Count == 0)
        {
            for (var i = 0; i < 7; i++)
            {
                var text = new TextBlock { FontSize = 12, LineHeight = 16.8 };
                var value = i % 2 == 1 && i < 6;
                if (value)
                {
                    text.FontFeatures = PromptCacheText.Tabular;
                }
                ArcTokens.Bind(text, TextBlock.ForegroundProperty,
                    i == 6 ? ArcDesignTokenKind.TextMuted : value ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
                _prices.Children.Add(text);
            }
        }
        _viewRows = -1;
        _viewSegments = null;
        _costLabelText = null;
        _shownRemaining = -1;
        _paletteDirty = true;
        if (_captionSwap is not null)
        {
            _captionSwap.Content = null;
            _captionShown = null;
        }
        RefreshAllViews();
        UpdateBreakpoints(Bounds.Width);
    }

    private void OnPlayPreClick(object? sender, RoutedEventArgs e) => _userToggledPlay = true;

    private void OnPlayPostClick(object? sender, RoutedEventArgs e) => _userToggledPlay = false;

    private void OnRestartPostClick(object? sender, RoutedEventArgs e) => Announce(Labels.Restarted);

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingToggle || _toggle is null)
        {
            return;
        }
        SetCurrentValue(IsChangeArmedProperty, _toggle.IsChecked == true);
    }

    private void SyncToggle()
    {
        if (_toggle is null)
        {
            return;
        }
        _syncingToggle = true;
        try
        {
            _toggle.IsChecked = IsChangeArmed;
        }
        finally
        {
            _syncingToggle = false;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        // Breakpoints follow the card's content width (the card's 24px padding and 1px border on each side).
        var content = width - 50;
        var mode = content < CompactWidth ? PromptCacheWidthMode.Compact : content < WideWidth ? PromptCacheWidthMode.Narrow : PromptCacheWidthMode.Wide;
        _mode = mode;
        PseudoClasses.Set(NarrowPseudoClass, mode != PromptCacheWidthMode.Wide);
        PseudoClasses.Set(CompactPseudoClass, mode == PromptCacheWidthMode.Compact);
        PseudoClasses.Set(TinyPseudoClass, content < TinyWidth);
        if (_split is not null)
        {
            _split.IsStacked = mode != PromptCacheWidthMode.Wide;
        }
        if (_table is not null)
        {
            _table.Mode = mode;
        }
        if (_prices is not null)
        {
            _prices.Mode = mode;
        }
        if (_footer is not null)
        {
            _footer.Mode = mode;
        }
    }

    // ---- palette ----------------------------------------------------------------------------------------------------

    private void InvalidatePalette()
    {
        _paletteDirty = true;
        if (_table is not null)
        {
            EnsurePalette();
        }
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        var surfaceBrush = GetValue(SurfaceInkProperty);
        if (surfaceBrush is null)
        {
            return;
        }
        _paletteDirty = false;
        var series = new[]
        {
            ColorOf(GetValue(Series1InkProperty), Colors.SteelBlue),
            ColorOf(GetValue(Series2InkProperty), Colors.MediumPurple),
            ColorOf(GetValue(Series3InkProperty), Colors.Orange),
            ColorOf(GetValue(Series4InkProperty), Colors.Teal)
        };
        var colors = new Color[_segments.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = _segments[i].Color is ISolidColorBrush own
                ? own.Color
                : series[PromptCachePalette.SeriesOrder[i % PromptCachePalette.SeriesOrder.Length]];
        }
        _palette = new PromptCachePalette(
            ColorOf(surfaceBrush, Colors.White),
            ColorOf(GetValue(TrackInkProperty), Colors.WhiteSmoke),
            ColorOf(GetValue(MessageInkProperty), Colors.DimGray),
            ColorOf(GetValue(WarningInkProperty), Colors.DarkOrange),
            ColorOf(GetValue(KeyInkProperty), Colors.Gray),
            colors);
        foreach (var view in _views)
        {
            view.Bar.Palette = _palette;
            view.Bar.Refresh();
        }
        if (_cacheBar is not null)
        {
            _cacheBar.Palette = _palette;
            _cacheBar.Refresh();
        }
        if (_table is not null)
        {
            _table.Palette = _palette;
            _table.InvalidateVisual();
        }
        if (_flight is not null)
        {
            _flight.Palette = _palette;
            _flight.InvalidateVisual();
        }
    }

    // ---- static parts -----------------------------------------------------------------------------------------------

    private void EnsureViews()
    {
        if (_table is null)
        {
            return;
        }
        if (_viewRows == _rows.Length && ReferenceEquals(_viewSegments, _segments))
        {
            return;
        }
        _viewRows = _rows.Length;
        _viewSegments = _segments;
        _views = new PromptCacheRowView[_rows.Length];
        for (var i = 0; i < _views.Length; i++)
        {
            var bar = new PromptCacheBar();
            bar.SetShape(_grows, _messageGrow);
            bar.Palette = _palette;
            _views[i] = new PromptCacheRowView(bar);
        }
        _table.SetRows(_views);
        AutomationProperties.SetName(_table, Labels.RequestsName);
        _cacheBar?.SetShape(_grows, 0);
        _paletteDirty = true;
        RebuildLegend();
    }

    private void RefreshAllViews()
    {
        if (_table is null)
        {
            return;
        }
        EnsureViews();
        EnsurePalette();
        RefreshStaticTexts();
        for (var i = 0; i < _rows.Length; i++)
        {
            RefreshRowText(i);
            RefreshRowStatus(i);
        }
        RefreshCacheStatus(force: true);
        RefreshStatLabels();
        ShowCaption();
        _statsShown = null;
        UpdateFrame(Now, force: true);
    }

    private void RefreshStaticTexts()
    {
        var labels = Labels;
        var title = Title ?? string.Empty;
        AutomationProperties.SetName(this, labels.CardName(title));
        if (_titleText is not null)
        {
            _titleText.Text = title;
        }
        var model = Model;
        if (_modelName is not null)
        {
            _modelName.Text = model?.Name;
            _modelName.IsVisible = model is not null;
        }
        if (_modelMark is not null)
        {
            _modelMark.IsVisible = model is not null;
            _modelMark.Data = model?.Mark ?? ArcIcons.Sparkles;
            if (model?.MarkBrush is { } brush)
            {
                _modelMark.Foreground = brush;
            }
            else
            {
                ArcTokens.Bind(_modelMark, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Series1);
            }
        }
        if (_modelDot is not null)
        {
            // Opacity, not visibility: the compact layout hides the dot through the theme.
            _modelDot.Opacity = model is null ? 0 : 1;
        }
        if (_ttlText is not null)
        {
            _ttlText.Text = labels.TtlNote(_ttl);
        }
        _table?.SetHeader(labels.ProcessedKey, labels.CachedKey, labels.ProcessedColumn, labels.CostColumn, labels.FirstTokenColumn);
        UpdateColumnSamples();
        if (_cacheName is not null)
        {
            _cacheName.Text = labels.CacheName;
        }
        if (_cachePanel is not null)
        {
            AutomationProperties.SetName(_cachePanel, labels.CacheName);
        }
        if (_legend is not null)
        {
            AutomationProperties.SetName(_legend, labels.LegendName);
        }
        if (_prices is { Children.Count: 7 } prices)
        {
            ((TextBlock)prices.Children[0]).Text = labels.InputPrice;
            ((TextBlock)prices.Children[1]).Text = PromptCacheMath.FormatPrice(_pricing.InputPrice);
            ((TextBlock)prices.Children[2]).Text = labels.WritePrice;
            ((TextBlock)prices.Children[3]).Text = PromptCacheMath.FormatPrice(_pricing.CacheWritePrice);
            ((TextBlock)prices.Children[4]).Text = labels.ReadPrice;
            ((TextBlock)prices.Children[5]).Text = PromptCacheMath.FormatPrice(_pricing.CacheReadPrice);
            ((TextBlock)prices.Children[6]).Text = labels.PriceUnit;
        }
        if (_speedLabel is not null)
        {
            _speedLabel.Text = labels.FasterFirstToken;
        }
        if (_toggle is not null)
        {
            var visible = _changeIndex >= 0;
            _toggle.IsVisible = visible;
            var text = visible ? labels.ChangeToggle(SegmentLabel(_changeIndex)) : string.Empty;
            if (_toggleText is not null)
            {
                _toggleText.Text = text;
            }
            AutomationProperties.SetName(_toggle, text);
            SyncToggle();
        }
    }

    // The number columns are as wide as the widest value any request of the round could show.
    private void UpdateColumnSamples()
    {
        if (_table is null)
        {
            return;
        }
        string processed = "0", cost = PromptCacheMath.FormatCost(0), first = PromptCacheMath.FormatSeconds(0);
        foreach (var request in _requests)
        {
            foreach (var kind in new[] { PromptCacheRequestKind.Write, PromptCacheRequestKind.Hit })
            {
                var r = PromptCacheMath.Compute(_tokens, request.Tokens, kind, null, _pricing, _firstToken);
                processed = Longer(processed, PromptCacheMath.FormatTokens(r.ProcessedTokens));
                cost = Longer(cost, PromptCacheMath.FormatCost(r.Cost));
                first = Longer(first, PromptCacheMath.FormatSeconds(r.FirstToken));
            }
        }
        _table.SetColumnSamples(Digits(processed), Digits(cost), Digits(first));
    }

    private static string Longer(string a, string b) => b.Length > a.Length ? b : a;

    // With tabular figures every digit is as wide as "0"; widen a sample to its widest form.
    private static string Digits(string text) => new(text.Select(c => char.IsDigit(c) ? '0' : c).ToArray());

    private void RebuildLegend()
    {
        if (_legend is null)
        {
            return;
        }
        _legend.Children.Clear();
        for (var i = 0; i < _segments.Length; i++)
        {
            var segment = _segments[i];
            var swatch = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
            if (segment.Color is { } own)
            {
                swatch.Background = own;
            }
            else
            {
                ArcTokens.Bind(swatch, Border.BackgroundProperty, s_series[PromptCachePalette.SeriesOrder[i % PromptCachePalette.SeriesOrder.Length]]);
            }
            var label = new TextBlock { Text = segment.Label, FontSize = 12, LineHeight = 16.8, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            var count = new TextBlock
            {
                Text = PromptCacheMath.FormatTokens(segment.Tokens),
                FontSize = 12,
                LineHeight = 16.8,
                FontFeatures = PromptCacheText.Tabular,
                VerticalAlignment = VerticalAlignment.Center
            };
            ArcTokens.Bind(count, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            _legend.Children.Add(LegendItem(swatch, label, count));
        }
        var messageSwatch = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
        messageSwatch.Bind(Border.BackgroundProperty, this.GetObservable(MessageInkProperty));
        var messageLabel = new TextBlock { Text = Labels.MessageLabel, FontSize = 12, LineHeight = 16.8, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(messageLabel, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _legend.Children.Add(LegendItem(messageSwatch, messageLabel, null));
    }

    private static Control LegendItem(Control swatch, TextBlock label, TextBlock? count)
    {
        var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 16, 6) };
        AutomationProperties.SetAccessibilityView(swatch, AccessibilityView.Raw);
        item.Children.Add(swatch);
        item.Children.Add(label);
        if (count is not null)
        {
            item.Children.Add(count);
        }
        return item;
    }

    // ---- dynamic texts ----------------------------------------------------------------------------------------------

    private string? _captionShown;

    private void SetCaption(string text)
    {
        _caption = text;
        ShowCaption();
    }

    private void ShowCaption()
    {
        if (_captionSwap is null || _caption == _captionShown)
        {
            return;
        }
        _captionShown = _caption;
        var block = new TextBlock { Text = _caption, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 19.6 };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _captionSwap.Content = block;
    }

    private readonly (string? Text, Geometry? Icon, ArcDesignTokenKind Tone, bool Busy)[] _rowStatus = new (string?, Geometry?, ArcDesignTokenKind, bool)[MaxRequests];

    private void SetRowStatus(int row, string? text, Geometry? icon, ArcDesignTokenKind tone, bool busy)
    {
        if (row < _rowStatus.Length)
        {
            _rowStatus[row] = (text, icon, tone, busy);
        }
        RefreshRowStatus(row);
    }

    private void RefreshRowStatus(int row)
    {
        if (row >= _views.Length || row >= _rowStatus.Length)
        {
            return;
        }
        var r = _rows[row];
        var status = _views[row].Status;
        var (text, icon, tone, busy) = _rowStatus[row];
        if (!r.Started || text is null)
        {
            status.Hide();
            return;
        }
        status.Show(text, icon, tone, busy);
    }

    private void RefreshRowText(int row)
    {
        if (row >= _views.Length)
        {
            return;
        }
        var labels = Labels;
        var r = _rows[row];
        var view = _views[row];
        var title = labels.RequestTitle(row + 1);
        view.TitleMuted.Text = title;
        view.Title.Text = title;
        view.Time.Text = r.Result is { } res && r.Started ? PromptCacheMath.FormatClock(res.Time) : string.Empty;
        if (!r.Started)
        {
            _rowStatus[row] = default;
        }
        string sentence;
        if (r.Done && r.Result is { } result)
        {
            var segment = result.BreakIndex is { } k ? SegmentLabel(k) : null;
            sentence = labels.RowDone(new PromptCacheRowText(row + 1, PromptCacheMath.FormatClock(result.Time), result.Message,
                labels.Outcome(result.Kind, segment), PromptCacheMath.FormatTokens(result.CachedTokens),
                PromptCacheMath.FormatTokens(result.ProcessedTokens), PromptCacheMath.FormatCost(result.Cost),
                PromptCacheMath.FormatSeconds(result.FirstToken)));
        }
        else
        {
            sentence = labels.RowPending(row + 1);
        }
        AutomationProperties.SetName(view.TitleMuted, sentence);
        RefreshRowStatus(row);
        view.Dirty = true;
    }

    private void RefreshCacheStatus(bool force)
    {
        if (_cacheStatus is null)
        {
            return;
        }
        var labels = Labels;
        switch (CacheState)
        {
            case PromptCacheState.Empty:
                _cacheStatus.Show(labels.Empty, ExplainerIcons.CircleDashed, ArcDesignTokenKind.TextMuted, busy: false);
                break;
            case PromptCacheState.Writing:
                _cacheStatus.Show(labels.Writing, null, ArcDesignTokenKind.TextSecondary, busy: true);
                break;
            case PromptCacheState.Reading:
                _cacheStatus.Show(labels.Reading, null, ArcDesignTokenKind.TextSecondary, busy: true);
                break;
            case PromptCacheState.Expired:
                _cacheStatus.Show(labels.Expired, ExplainerIcons.TimerOff, ArcDesignTokenKind.Warning, busy: false);
                break;
            default:
                var remaining = PromptCacheMath.RemainingSeconds(SimAt(Now) - _lastUse, _ttl);
                if (force || remaining != _shownRemaining)
                {
                    _shownRemaining = remaining;
                    _cacheStatus.Show(labels.ExpiresIn(PromptCacheMath.FormatClock(remaining)), null, ArcDesignTokenKind.TextSecondary, busy: false);
                }
                return;
        }
        _shownRemaining = -1;
    }

    private void RefreshStatLabels()
    {
        var labels = Labels;
        var text = _higher ? labels.HigherCost : labels.LowerCost;
        if (_costLabel is not null && text != _costLabelText)
        {
            _costLabelText = text;
            var block = new TextBlock { Text = text, FontSize = 14, LineHeight = 19.6 };
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            _costLabel.Content = block;
        }
        _statsShown = null;
    }

    // ---- frames -----------------------------------------------------------------------------------------------------

    private protected override void OnTimeChanged() => UpdateFrame(Now, force: false);

    private string? _statsShown;

    private bool _statsMoving;

    private double _shownTokens = double.NaN;

    private void UpdateFrame(double now, bool force)
    {
        if (_building)
        {
            return;
        }
        var dt = Math.Max(0, now - _lastFrame);
        _lastFrame = now;
        var sim = SimAt(now);
        CheckExpiry(sim);
        // The ring follows its target with a short critically damped lag on the story clock (it pauses with it).
        var target = _warm ? PromptCacheMath.Drained(sim - _lastUse, _ttl) : 1;
        _ringDisplay = _full ? PromptCacheMath.Follow(_ringDisplay, target, dt) : target;
        if (Math.Abs(_ringDisplay - target) < 0.0005)
        {
            _ringDisplay = target;
        }
        var low = _warm && CacheState == PromptCacheState.Warm && PromptCacheMath.IsLow(sim - _lastUse, _ttl);
        PseudoClasses.Set(LowPseudoClass, low);
        if (_table is null)
        {
            return;
        }
        EnsurePalette();
        if (_ring is not null)
        {
            _ring.Value = 1 - _ringDisplay;
        }
        if (CacheState == PromptCacheState.Warm)
        {
            RefreshCacheStatus(force: false);
        }
        for (var i = 0; i < _rows.Length && i < _views.Length; i++)
        {
            UpdateRow(i, now, force);
        }
        UpdatePanelBar(now);
        UpdateFlight(now);
        var tokens = _panelTokens.Value(now);
        if (_cachedTokens is not null && (force || !tokens.Equals(_shownTokens)))
        {
            _shownTokens = tokens;
            SetText(_cachedTokens, Labels.TokensCached(PromptCacheMath.FormatTokens(tokens)));
        }
        UpdateStats(now, force);
    }

    private void UpdateRow(int i, double now, bool force)
    {
        var r = _rows[i];
        var view = _views[i];
        if (r.PendingReset && (!_full || now >= r.FadeFrom + PromptCacheTimeline.RowFadeSeconds))
        {
            ResetRow(r);
            RefreshRowText(i);
        }
        var fade = r.PendingReset ? 1 - Math.Clamp((now - r.FadeFrom) / PromptCacheTimeline.RowFadeSeconds, 0, 1) : 1;
        var plan = r.Plan;
        var started = r.Started && plan is not null;
        var titleIn = !started ? 0 : _full ? Math.Clamp((now - plan!.Start) / 0.24, 0, 1) : 1;
        var timeIn = !started ? 0 : _full ? Math.Clamp((now - plan!.Start) / PromptCacheTimeline.CachedFadeSeconds, 0, 1) : 1;
        view.Title.Opacity = titleIn * fade;
        view.Time.Opacity = timeIn * fade;
        view.Status.Opacity = fade;
        // Rolling numbers are written while they move and once more when they stop.
        var moving = r.Processed.IsMoving(now) || r.Cost.IsMoving(now) || r.FirstToken.IsMoving(now);
        if (force || moving || view.Dirty)
        {
            view.Dirty = moving;
            SetText(view.Numbers[0], r.Done ? PromptCacheMath.FormatTokens(r.Processed.Value(now)) : PromptCacheRowView.Dash);
            SetText(view.Numbers[1], r.Done ? PromptCacheMath.FormatCost(r.Cost.Value(now)) : PromptCacheRowView.Dash);
            SetText(view.Numbers[2], r.Done ? PromptCacheMath.FormatSeconds(r.FirstToken.Value(now)) : PromptCacheRowView.Dash);
        }
        foreach (var n in view.Numbers)
        {
            n.Opacity = r.Done ? fade : 1;
        }
        FillBar(view.Bar, started ? plan : null, now, fade);
    }

    private static void SetText(TextBlock block, string text)
    {
        if (block.Text != text)
        {
            block.Text = text;
        }
    }

    private void FillBar(PromptCacheBar bar, PromptCacheRequestPlan? plan, double now, double fade)
    {
        if (plan is null)
        {
            bar.Clear();
            bar.Update();
            return;
        }
        var n = bar.Processed.Length;
        for (var i = 0; i < n; i++)
        {
            if (i < plan.CachedTo)
            {
                bar.Cached[i] = now < plan.Start ? 0 : _full ? ArcEasings.Standard.Ease(Math.Clamp((now - plan.Start) / PromptCacheTimeline.CachedFadeSeconds, 0, 1)) : 1;
                bar.Processed[i] = 0;
            }
            else
            {
                var start = i < plan.SweepStarts.Length ? plan.SweepStarts[i] : double.NaN;
                var duration = i < plan.SweepDurations.Length ? plan.SweepDurations[i] : 0;
                bar.Processed[i] = double.IsNaN(start) || now < start ? 0 : duration <= 0 ? 1 : Math.Clamp((now - start) / duration, 0, 1);
                bar.Cached[i] = 0;
            }
        }
        bar.Message = now < plan.MessageStart ? 0 : plan.MessageDuration <= 0 ? 1 : Math.Clamp((now - plan.MessageStart) / plan.MessageDuration, 0, 1);
        bar.Band = BandAt(plan, now);
        bar.BandTo = plan.CachedTo;
        if (plan.Kind == PromptCacheRequestKind.Partial && plan.Break is { } k)
        {
            bar.MarkIndex = k;
            bar.Mark = now < plan.MarkStart ? 0 : _full ? ArcEasings.Standard.Ease(Math.Clamp((now - plan.MarkStart) / PromptCacheTimeline.MarkSeconds, 0, 1)) : 1;
        }
        else
        {
            bar.Mark = double.NaN;
        }
        bar.ContentOpacity = fade;
        bar.Update();
    }

    private double BandAt(PromptCacheRequestPlan plan, double now)
    {
        if (!_full || plan.CachedTo <= 0 || double.IsNaN(plan.BandStart))
        {
            return double.NaN;
        }
        var p = (now - plan.BandStart) / PromptCacheTimeline.BandSeconds;
        if (p < 0 || p >= 1)
        {
            return double.NaN;
        }
        var w = PromptCacheTimeline.BandWidth;
        return -w + (1 + w) * ArcEasings.InOut.Ease(p);
    }

    private void UpdatePanelBar(double now)
    {
        if (_cacheBar is null || _cacheBar.Cached.Length != _panelOpen.Length)
        {
            return;
        }
        for (var i = 0; i < _panelOpen.Length; i++)
        {
            double o;
            if (_panelOpen[i])
            {
                o = 1;
            }
            else if (!double.IsNaN(_panelCloseAt[i]))
            {
                o = now < _panelCloseAt[i] ? 1 : 1 - ArcEasings.Standard.Ease(Math.Clamp((now - _panelCloseAt[i]) / PromptCacheTimeline.CloseSeconds, 0, 1));
            }
            else
            {
                o = 0;
            }
            _cacheBar.Cached[i] = o;
            _cacheBar.Processed[i] = 0;
        }
        _cacheBar.Message = 0;
        if (_panelBand is { } band)
        {
            _cacheBar.Band = BandAt(band, now);
            _cacheBar.BandTo = _panelOpen.Length;
        }
        else
        {
            _cacheBar.Band = double.NaN;
        }
        _cacheBar.Mark = double.NaN;
        _cacheBar.Update();
    }

    private void UpdateFlight(double now)
    {
        if (_flight is null)
        {
            return;
        }
        if (_flightRow >= 0 && _flightRow < _views.Length && !double.IsNaN(_flightStart) && _full)
        {
            var p = Math.Clamp((now - _flightStart) / PromptCacheTimeline.FlightSeconds, 0, 1);
            _flight.From = _views[_flightRow].Bar;
            _flight.To = _cacheBar;
            _flight.FirstSegment = _flightFrom;
            _flight.Palette = _palette;
            _flight.Progress = ArcEasings.Standard.Ease(p);
            _flight.InvalidateVisual();
        }
        else if (!double.IsNaN(_flight.Progress))
        {
            _flight.Progress = double.NaN;
            _flight.InvalidateVisual();
        }
    }

    private void UpdateStats(double now, bool force)
    {
        // Rolling statistics are written while they move and once more when they stop.
        var moving = _percent.IsMoving(now) || _costSum.IsMoving(now) || _speedup.IsMoving(now) || _avgFirst.IsMoving(now);
        if (!force && !moving && !_statsMoving && _statsShown is not null)
        {
            return;
        }
        _statsMoving = moving;
        var labels = Labels;
        var cost = _hasCost ? PromptCacheMath.FormatPercent(_percent.Value(now)) : PromptCacheRowView.Dash;
        var costDetail = _hasCost
            ? labels.CostDetail(PromptCacheMath.FormatCost(_costSum.Value(now)), PromptCacheMath.FormatCost(_uncachedSum.Value(now)))
            : labels.NoRequests;
        var speed = _hasHits ? PromptCacheMath.FormatSpeedup(_speedup.Value(now)) : PromptCacheRowView.Dash;
        var speedDetail = _hasHits
            ? labels.SpeedDetail(PromptCacheMath.FormatSeconds(_avgFirst.Value(now)), PromptCacheMath.FormatSeconds(_avgUncached.Value(now)))
            : labels.NoHits;
        if (_costValue is not null)
        {
            SetText(_costValue, cost);
        }
        if (_costDetail is not null)
        {
            SetText(_costDetail, costDetail);
        }
        if (_speedValue is not null)
        {
            SetText(_speedValue, speed);
        }
        if (_speedDetail is not null)
        {
            SetText(_speedDetail, speedDetail);
        }
        _statsShown = cost;
    }
}
