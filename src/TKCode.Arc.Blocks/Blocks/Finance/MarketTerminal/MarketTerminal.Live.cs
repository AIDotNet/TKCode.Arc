using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

// The live half of the block: the simulation and its timer, the header, the toolbar, the chart and the watchlist.
public partial class MarketTerminal
{
    private MarketSimulation? _simulation;
    private DispatcherTimer? _timer;
    private readonly List<MarketWatchRow> _rows = new();
    private readonly Dictionary<string, double> _shownPrices = new(StringComparer.Ordinal);

    private TextBlock? _tickerText, _nameText, _exchangeText, _changeText, _todayText;
    private TextBlock? _openLabel, _openValue, _highLabel, _highValue, _lowLabel, _lowValue, _volumeLabel, _volumeValue;
    private AnimatedCounter? _price;
    private ArcGlyph? _changeGlyph;
    private Control? _changeLine;
    private MarketLiveButton? _live;
    private SegmentedControl? _ranges;
    private MarketAverageToggle? _ma20, _ma50;
    private MarketIconButton? _zoomIn, _zoomOut, _reset;
    private TextBlock? _watchTitle, _watchCount;
    private GlidingIndicator? _watchSelection;
    private StackPanel? _watchRows;
    private bool _syncing;

    private static readonly Avalonia.Media.Geometry s_arrowDownRight = ArcIcons.Parse("m7 7 10 10 M17 7v10H7");

    /// <summary>The live price of <paramref name="ticker"/> (0 when it is not in the watchlist).</summary>
    public double PriceOf(string ticker) => _simulation?.Price(ticker) ?? 0;

    /// <summary>Moves <paramref name="ticker"/> to <paramref name="price"/>, as a tick would (feed real prices with it).</summary>
    public void SetPrice(string ticker, double price)
    {
        if (_simulation is null || !_simulation.Contains(ticker))
        {
            return;
        }
        _simulation.SetPrice(ticker, Math.Round(price, 2));
        AfterTick();
    }

    /// <summary>Advances the simulation by one tick now (whether or not it is live).</summary>
    public void Tick()
    {
        if (_simulation is null)
        {
            return;
        }
        _simulation.Tick();
        AfterTick();
    }

    private void HookParts(INameScope scope)
    {
        _tickerText = scope.Find<TextBlock>("PART_TickerText");
        _nameText = scope.Find<TextBlock>("PART_NameText");
        _exchangeText = scope.Find<TextBlock>("PART_ExchangeText");
        _price = scope.Find<AnimatedCounter>("PART_Price");
        _changeGlyph = scope.Find<ArcGlyph>("PART_ChangeGlyph");
        _changeLine = scope.Find<Control>("PART_ChangeLine");
        _changeText = scope.Find<TextBlock>("PART_ChangeText");
        _todayText = scope.Find<TextBlock>("PART_TodayText");
        _openLabel = scope.Find<TextBlock>("PART_OpenLabel");
        _openValue = scope.Find<TextBlock>("PART_OpenValue");
        _highLabel = scope.Find<TextBlock>("PART_HighLabel");
        _highValue = scope.Find<TextBlock>("PART_HighValue");
        _lowLabel = scope.Find<TextBlock>("PART_LowLabel");
        _lowValue = scope.Find<TextBlock>("PART_LowValue");
        _volumeLabel = scope.Find<TextBlock>("PART_VolumeLabel");
        _volumeValue = scope.Find<TextBlock>("PART_VolumeValue");
        _live = scope.Find<MarketLiveButton>("PART_Live");
        _ranges = scope.Find<SegmentedControl>("PART_Ranges");
        _ma20 = scope.Find<MarketAverageToggle>("PART_Ma20");
        _ma50 = scope.Find<MarketAverageToggle>("PART_Ma50");
        _zoomIn = scope.Find<MarketIconButton>("PART_ZoomIn");
        _zoomOut = scope.Find<MarketIconButton>("PART_ZoomOut");
        _reset = scope.Find<MarketIconButton>("PART_Reset");
        _watchTitle = scope.Find<TextBlock>("PART_WatchTitle");
        _watchCount = scope.Find<TextBlock>("PART_WatchCount");
        _watchSelection = scope.Find<GlidingIndicator>("PART_WatchSelection");
        _watchRows = scope.Find<StackPanel>("PART_WatchRows");

        if (_live is not null)
        {
            _live.Click += OnLiveClick;
        }
        if (_ranges is not null)
        {
            _ranges.ItemsSource = Enum.GetValues<MarketRange>().Select(r => new SegmentedOption(r, Labels.RangeLabel(r))).ToList();
            _ranges.SelectionChanged += OnRangesSelectionChanged;
        }
        if (_ma20 is not null)
        {
            _ma20.IsCheckedChanged += OnAverageToggled;
        }
        if (_ma50 is not null)
        {
            _ma50.IsCheckedChanged += OnAverageToggled;
        }
        if (_zoomIn is not null)
        {
            _zoomIn.Click += OnZoomInClick;
        }
        if (_zoomOut is not null)
        {
            _zoomOut.Click += OnZoomOutClick;
        }
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }
        if (_watchRows is not null)
        {
            _watchRows.PropertyChanged += OnWatchRowsPropertyChanged;
            _watchRows.AddHandler(KeyDownEvent, OnWatchKeyDown, RoutingStrategies.Bubble);
        }
        if (_chart is not null)
        {
            _chart.Announce = text => _announcer?.Announce(text);
        }
        HookTicket(scope);
        ApplyLabels();
        SyncToolbar();
    }

    private void UnhookParts()
    {
        if (_live is not null)
        {
            _live.Click -= OnLiveClick;
        }
        if (_ranges is not null)
        {
            _ranges.SelectionChanged -= OnRangesSelectionChanged;
        }
        if (_ma20 is not null)
        {
            _ma20.IsCheckedChanged -= OnAverageToggled;
        }
        if (_ma50 is not null)
        {
            _ma50.IsCheckedChanged -= OnAverageToggled;
        }
        if (_zoomIn is not null)
        {
            _zoomIn.Click -= OnZoomInClick;
        }
        if (_zoomOut is not null)
        {
            _zoomOut.Click -= OnZoomOutClick;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        if (_watchRows is not null)
        {
            _watchRows.PropertyChanged -= OnWatchRowsPropertyChanged;
            _watchRows.RemoveHandler(KeyDownEvent, OnWatchKeyDown);
            _watchRows.Children.Clear();
        }
        _rows.Clear();
        UnhookTicket();
    }

    private void Rebuild()
    {
        var session = DateOnly.FromDateTime(SessionDate ?? DateTime.Today);
        _simulation = Symbols.Count > 0 ? new MarketSimulation(Symbols, session) : null;
        _shownPrices.Clear();
        if (_simulation is not null)
        {
            foreach (var ticker in _simulation.Tickers)
            {
                _shownPrices[ticker] = _simulation.Price(ticker);
            }
        }
        RebuildWatchlist();
        RebuildOrders();
        ShowSeries(animate: false);
        UpdateAll();
        UpdateLiveState();
    }

    private void UpdateAll()
    {
        UpdateHeader();
        UpdateWatchlist(flash: false);
        UpdateTicket();
        UpdateChartName();
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        SetText(_openLabel, labels.Open);
        SetText(_highLabel, labels.High);
        SetText(_lowLabel, labels.Low);
        SetText(_volumeLabel, labels.Volume);
        SetText(_todayText, labels.Today);
        SetText(_watchTitle, labels.Watchlist);
        SetText(_watchCount, labels.SymbolCount(Symbols.Count));
        if (_ma20 is not null)
        {
            _ma20.Content = labels.Ma20;
        }
        if (_ma50 is not null)
        {
            _ma50.Content = labels.Ma50;
        }
        SetName(_zoomIn, labels.ZoomIn);
        SetName(_zoomOut, labels.ZoomOut);
        SetName(_reset, labels.ResetView);
        SetName(_ranges, labels.RangeGroup);
        if (_ranges is not null)
        {
            _syncing = true;
            _ranges.ItemsSource = Enum.GetValues<MarketRange>().Select(r => new SegmentedOption(r, labels.RangeLabel(r))).ToList();
            _ranges.SelectedValue = Range;
            _syncing = false;
        }
        if (_chart is not null)
        {
            _chart.Labels = labels;
            AutomationProperties.SetHelpText(_chart, labels.ChartHelp);
        }
        UpdateLiveButton();
        ApplyTicketLabels();
    }

    private static void SetText(TextBlock? text, string? value)
    {
        if (text is not null)
        {
            text.Text = value;
        }
    }

    private static void SetName(Control? control, string name)
    {
        if (control is not null)
        {
            AutomationProperties.SetName(control, name);
        }
    }

    private void OnTickerChanged(string? old)
    {
        var current = EffectiveSymbol?.Ticker;
        ShowSeries(animate: true);
        UpdateHeader();
        UpdateWatchlist(flash: false);
        ResetTicketForTicker();
        UpdateChartName();
        if (old != current)
        {
            RaiseEvent(new MarketTickerChangedEventArgs(TickerChangedEvent, old, current));
        }
    }

    private void ShowSeries(bool animate)
    {
        var symbol = EffectiveSymbol;
        if (_chart is null || _simulation is null || symbol is null || !_simulation.Contains(symbol.Ticker))
        {
            return;
        }
        _chart.SetSeries(_simulation.Series(symbol.Ticker, Range), symbol.PreviousClose, animate);
        UpdateChartName();
    }

    private void UpdateChartName()
    {
        var symbol = EffectiveSymbol;
        if (_chart is null || symbol is null)
        {
            return;
        }
        var labels = Labels;
        AutomationProperties.SetName(_chart, labels.ChartName(symbol.Ticker, labels.RangeName(Range), labels.Interval(Range)));
    }

    private void SyncToolbar()
    {
        _syncing = true;
        try
        {
            if (_ranges is not null)
            {
                _ranges.SelectedValue = Range;
            }
            if (_ma20 is not null)
            {
                _ma20.IsChecked = ShowMa20;
            }
            if (_ma50 is not null)
            {
                _ma50.IsChecked = ShowMa50;
            }
            if (_chart is not null)
            {
                _chart.ShowMa20 = ShowMa20;
                _chart.ShowMa50 = ShowMa50;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnRangesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _ranges?.SelectedValue is MarketRange range)
        {
            SetCurrentValue(RangeProperty, range);
        }
    }

    private void OnAverageToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        if (ReferenceEquals(sender, _ma20))
        {
            SetCurrentValue(ShowMa20Property, _ma20!.IsChecked == true);
        }
        else if (ReferenceEquals(sender, _ma50))
        {
            SetCurrentValue(ShowMa50Property, _ma50!.IsChecked == true);
        }
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => _chart?.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => _chart?.ZoomOut();

    private void OnResetClick(object? sender, RoutedEventArgs e) => _chart?.ResetView();

    private void OnLiveClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsLiveProperty, !IsLive);

    private void UpdateLiveButton()
    {
        if (_live is null)
        {
            return;
        }
        _live.IsLive = IsLive;
        _live.Content = Labels.Live;
        AutomationProperties.SetName(_live, IsLive ? Labels.PauseLive : Labels.ResumeLive);
    }

    private void UpdateLiveState()
    {
        UpdateLiveButton();
        var run = IsLive && _simulation is not null && this.IsAttachedToVisualTree();
        if (!run)
        {
            _timer?.Stop();
            return;
        }
        if (_timer is null)
        {
            _timer = new DispatcherTimer();
            _timer.Tick += (_, _) => Tick();
        }
        var interval = TickInterval > TimeSpan.Zero ? TickInterval : TimeSpan.FromMilliseconds(1200);
        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }
        _timer.Start();
    }

    private void AfterTick()
    {
        UpdateHeader();
        UpdateWatchlist(flash: true);
        _chart?.OnTick();
        FillWorkingOrders();
        UpdateTicket();
    }

    private void UpdateHeader()
    {
        var symbol = EffectiveSymbol;
        if (symbol is null || _simulation is null)
        {
            return;
        }
        var ticker = symbol.Ticker;
        SetText(_tickerText, ticker);
        SetText(_nameText, symbol.Name);
        SetText(_exchangeText, $"{symbol.Exchange}, {symbol.Currency}");
        var price = _simulation.Price(ticker);
        if (_price is not null)
        {
            _price.Value = price;
        }
        var change = price - symbol.PreviousClose;
        var share = symbol.PreviousClose > 0 ? change / symbol.PreviousClose : 0;
        SetText(_changeText, $"{FinanceFormat.SignedPrice(change)} ({FinanceFormat.SignedPercent(share, 2)})");
        if (_changeGlyph is not null)
        {
            _changeGlyph.Data = change >= 0 ? ArcIcons.ArrowUpRight : s_arrowDownRight;
        }
        _changeLine?.Classes.Set("down", change < 0);
        var today = _simulation.Today(ticker);
        SetText(_openValue, FinanceFormat.Price(today.Open));
        SetText(_highValue, FinanceFormat.Price(today.High));
        SetText(_lowValue, FinanceFormat.Price(today.Low));
        SetText(_volumeValue, FinanceFormat.Volume(today.Volume));
    }

    private void RebuildWatchlist()
    {
        if (_watchRows is null)
        {
            return;
        }
        _watchRows.Children.Clear();
        _rows.Clear();
        SetText(_watchCount, Labels.SymbolCount(Symbols.Count));
        if (_simulation is null)
        {
            return;
        }
        foreach (var symbol in Symbols)
        {
            if (!_simulation.Contains(symbol.Ticker) || _rows.Any(r => r.Ticker == symbol.Ticker))
            {
                continue;
            }
            var row = new MarketWatchRow { Ticker = symbol.Ticker, SymbolName = symbol.Name };
            row.SetSeries(_simulation.Intraday(symbol.Ticker), symbol.PreviousClose);
            var ticker = symbol.Ticker;
            row.Click += (_, _) => SetCurrentValue(TickerProperty, ticker);
            _rows.Add(row);
            _watchRows.Children.Add(row);
        }
        UpdateWatchlist(flash: false);
    }

    private void UpdateWatchlist(bool flash)
    {
        if (_simulation is null)
        {
            return;
        }
        var current = EffectiveSymbol?.Ticker;
        foreach (var row in _rows)
        {
            var symbol = _simulation.Symbol(row.Ticker!);
            if (symbol is null)
            {
                continue;
            }
            var price = _simulation.Price(symbol.Ticker);
            var change = symbol.PreviousClose > 0 ? (price - symbol.PreviousClose) / symbol.PreviousClose : 0;
            row.PriceText = FinanceFormat.Price(price);
            row.ChangeText = FinanceFormat.SignedPercent(change, 2);
            row.IsUp = change >= 0;
            row.IsCurrent = symbol.Ticker == current;
            AutomationProperties.SetName(row, Labels.RowName(symbol.Ticker, symbol.Name, FinanceFormat.Price(price), change));
            row.InvalidateSparkline();
            if (_shownPrices.TryGetValue(symbol.Ticker, out var shown) && Math.Abs(shown - price) > 1e-9 && flash)
            {
                row.Flash(price > shown);
            }
            _shownPrices[symbol.Ticker] = price;
        }
        Dispatcher.UIThread.Post(() => MoveSelection(animate: true), DispatcherPriority.Loaded);
    }

    private void OnWatchRowsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            MoveSelection(animate: false);
        }
    }

    /// <summary>Glides the selected row's fill to the charted symbol's row.</summary>
    private void MoveSelection(bool animate)
    {
        if (_watchSelection is null)
        {
            return;
        }
        var row = _rows.FirstOrDefault(r => r.IsCurrent);
        if (row is null || row.Bounds.Height <= 0)
        {
            _watchSelection.Hide();
            return;
        }
        _watchSelection.MoveTo(row.Bounds, animate && _watchSelection.HasTarget);
    }

    private void OnWatchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || _rows.Count == 0)
        {
            return;
        }
        var index = _rows.FindIndex(r => r.IsFocused);
        if (index < 0)
        {
            return;
        }
        var target = e.Key switch
        {
            Key.Down => Math.Min(index + 1, _rows.Count - 1),
            Key.Up => Math.Max(index - 1, 0),
            Key.Home => 0,
            Key.End => _rows.Count - 1,
            _ => -1,
        };
        if (target < 0)
        {
            return;
        }
        _rows[target].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    /// <summary>The watchlist rows (tests).</summary>
    internal IReadOnlyList<MarketWatchRow> WatchRows => _rows;

    internal MarketChart? Chart => _chart;

    internal MarketSimulation? Simulation => _simulation;

    internal FinanceAnnouncer? Announcer => _announcer;

    private void OnOrdersChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnOrderListChanged(e);
}
