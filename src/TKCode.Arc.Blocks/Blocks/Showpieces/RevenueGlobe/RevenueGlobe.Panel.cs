using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

public partial class RevenueGlobe
{
    /// <summary>Rows in the top-cities list.</summary>
    internal const int TopCityRows = 5;

    private readonly List<RevenueGlobeRegionRow> _regionRowList = new();
    private readonly List<RevenueGlobeCityRow> _cityRowList = new();
    private readonly int[] _top = new int[TopCityRows];
    private int _topCount;
    private Func<double, string>? _currencyFormatter;
    private Func<double, string>? _countFormatter;
    private long _summaryKey = long.MinValue;
    private long _summaryTicks;
    private double _placedCenter = double.NaN;

    /// <summary>The cities in the top-cities list, best first (tests).</summary>
    internal ReadOnlySpan<int> TopCities => _top.AsSpan(0, _topCount);

    internal IReadOnlyList<RevenueGlobeRegionRow> RegionRowControls => _regionRowList;

    internal IReadOnlyList<RevenueGlobeCityRow> CityRowControls => _cityRowList;

    private string FormatCurrency(double value) => ShowpieceFormat.Currency(Math.Round(value), 0, CurrencySymbol, Culture);

    private void RefreshFormatters()
    {
        _currencyFormatter = FormatCurrency;
        _countFormatter = v => ShowpieceFormat.Grouped(v, 0, Culture);
        if (_revenueTicker is not null)
        {
            _revenueTicker.Formatter = _currencyFormatter;
        }
        if (_lastHourTicker is not null)
        {
            _lastHourTicker.Formatter = _currencyFormatter;
        }
        if (_paymentsTicker is not null)
        {
            _paymentsTicker.Formatter = _countFormatter;
        }
        foreach (var row in _regionRowList)
        {
            row.AmountFormatter = _currencyFormatter;
        }
        foreach (var row in _cityRowList)
        {
            row.AmountFormatter = _currencyFormatter;
        }
        _summaryKey = long.MinValue;
        UpdateFigures();
    }

    private void BuildRows()
    {
        _currencyFormatter ??= FormatCurrency;
        if (_regionRows is not null)
        {
            foreach (var row in _regionRowList)
            {
                row.Click -= OnRegionRowClick;
            }
            _regionRows.Children.Clear();
            _regionRowList.Clear();
            for (var i = 0; i < _regions.Length; i++)
            {
                var row = new RevenueGlobeRegionRow { Index = i, RegionName = _regions[i].Name, AmountFormatter = _currencyFormatter };
                row.Click += OnRegionRowClick;
                _regionRowList.Add(row);
                _regionRows.Children.Add(row);
            }
        }
        if (_cityRows is not null)
        {
            foreach (var row in _cityRowList)
            {
                row.Click -= OnCityRowClick;
                row.PointerEntered -= OnCityRowPointerEntered;
                row.PointerExited -= OnCityRowPointerExited;
            }
            _cityRows.Children.Clear();
            _cityRowList.Clear();
            var count = Math.Min(TopCityRows, _scene.Cities.Length);
            for (var i = 0; i < count; i++)
            {
                var row = new RevenueGlobeCityRow { AmountFormatter = _currencyFormatter };
                row.Click += OnCityRowClick;
                row.PointerEntered += OnCityRowPointerEntered;
                row.PointerExited += OnCityRowPointerExited;
                _cityRowList.Add(row);
                _cityRows.Children.Add(row);
            }
        }
        RefreshFormatters();
        UpdateRowSelection();
    }

    private void UpdateRowSelection()
    {
        var region = SelectedRegionIndex;
        foreach (var row in _regionRowList)
        {
            row.IsChecked = row.Index == region;
        }
        var city = SelectedCityIndex;
        foreach (var row in _cityRowList)
        {
            row.IsChecked = row.Index >= 0 && row.Index == city;
        }
    }

    /// <summary>
    /// Recomputes every total at the playhead (ledger minus the payments still flying) and hands the targets to the
    /// counting figures, the rows, the city dots and the card.
    /// </summary>
    private void UpdateFigures()
    {
        var time = _playback.Time;
        _revenue = Math.Max(0, _ledger.RevenueAt(time) - _pendingRevenue);
        _paymentCount = Math.Max(0, _ledger.CountAt(time) - _pendingCount);
        _lastHour = Math.Max(0, _ledger.RevenueBetween(time - 3600, time) - _pendingRevenue);
        var max = 0.0;
        var cities = _scene.Cities.Length;
        Array.Clear(_regionRevenue);
        for (var i = 0; i < cities && i < _cityRevenue.Length; i++)
        {
            var revenue = Math.Max(0, _ledger.CityRevenueAt(i, time) - _pendingCityRevenue[i]);
            _cityRevenue[i] = revenue;
            _cityCount[i] = Math.Max(0, _ledger.CityCountAt(i, time) - _pendingCityCount[i]);
            _scene.CityRevenue[i] = revenue;
            max = Math.Max(max, revenue);
            var region = _scene.CityRegion[i];
            if ((uint)region < (uint)_regionRevenue.Length)
            {
                _regionRevenue[region] += revenue;
            }
        }
        _scene.MaxCityRevenue = max;

        if (_revenueTicker is not null)
        {
            _revenueTicker.Value = _revenue;
        }
        if (_paymentsTicker is not null)
        {
            _paymentsTicker.Value = _paymentCount;
        }
        if (_lastHourTicker is not null)
        {
            _lastHourTicker.Value = _lastHour;
        }
        var labels = Labels;
        foreach (var row in _regionRowList)
        {
            var region = _regions[row.Index];
            var amount = _regionRevenue[row.Index];
            var share = region.Forecast > 0 ? amount / region.Forecast : 0;
            row.Amount = amount;
            row.Progress = Math.Clamp(share, 0, 1);
            row.ShareText = labels.OfForecast(Math.Round(share * 100));
        }
        UpdateTopCities();
        if (SelectedRegionIndex >= 0)
        {
            UpdateRegionLabels();
        }
        if (_tipCity >= 0)
        {
            FillTip();
        }
        _canvas?.InvalidateVisual();
        UpdateAccessibleText();
    }

    private void UpdateTopCities()
    {
        // Partial selection of the five biggest cities (the list is short; 40 cities × 5 slots).
        var count = 0;
        for (var i = 0; i < _cityRevenue.Length; i++)
        {
            var revenue = _cityRevenue[i];
            var position = count;
            while (position > 0 && _cityRevenue[_top[position - 1]] < revenue)
            {
                if (position < TopCityRows)
                {
                    _top[position] = _top[position - 1];
                }
                position--;
            }
            if (position < TopCityRows)
            {
                _top[position] = i;
                count = Math.Min(TopCityRows, count + 1);
            }
        }
        _topCount = count;
        var selected = SelectedCityIndex;
        for (var r = 0; r < _cityRowList.Count; r++)
        {
            var row = _cityRowList[r];
            if (r >= count)
            {
                row.Index = -1;
                row.IsVisible = false;
                continue;
            }
            var index = _top[r];
            row.IsVisible = true;
            if (row.Index != index)
            {
                row.Index = index;
                var city = _scene.Cities[index];
                row.CityName = city.Name;
                row.CountryCode = city.CountryCode;
                row.IsChecked = index == selected;
                AutomationProperties.SetName(row, city.Name);
            }
            row.Amount = _cityRevenue[index];
        }
    }

    /// <summary>
    /// The globe's description and the hidden city table for screen readers: refreshed when the minute shown changes,
    /// but while the playhead runs at most once a second of real time (a replay passes a minute every few frames).
    /// </summary>
    private void UpdateAccessibleText()
    {
        var key = (long)Math.Floor(_playback.Time / 60) * 7919 + _ledger.Version % 7919;
        if (key == _summaryKey)
        {
            return;
        }
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_summaryKey != long.MinValue && _playback.State == RevenueGlobePlaybackState.Replaying &&
            now - _summaryTicks < System.Diagnostics.Stopwatch.Frequency)
        {
            return;
        }
        _summaryKey = key;
        _summaryTicks = now;
        var labels = Labels;
        var time = (EffectiveDayStart + TimeSpan.FromSeconds(_playback.Time)).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var regions = new StringBuilder();
        for (var i = 0; i < _regions.Length; i++)
        {
            if (regions.Length > 0)
            {
                regions.Append(", ");
            }
            regions.Append(_regions[i].Name).Append(' ').Append(ShowpieceFormat.CompactCurrency(_regionRevenue[i], CurrencySymbol, Culture));
        }
        var leader = _topCount > 0 ? _scene.Cities[_top[0]].Name : string.Empty;
        var summary = labels.Summary(FormatCurrency(_revenue), ShowpieceFormat.Grouped(_paymentCount, 0, Culture), time, regions.ToString(), leader);
        if (_canvas is not null)
        {
            AutomationProperties.SetHelpText(_canvas, summary);
        }
        foreach (var row in _regionRowList)
        {
            var region = _regions[row.Index];
            var share = _revenue > 0 ? _regionRevenue[row.Index] / _revenue : 0;
            AutomationProperties.SetName(row, region.Name);
            AutomationProperties.SetHelpText(row, labels.RegionDetail(ShowpieceFormat.Percent(share, 0, false, Culture), FormatCurrency(region.Forecast)));
        }
        if (_table is null)
        {
            return;
        }
        var children = _table.Children;
        var cities = _scene.Cities;
        while (children.Count > cities.Length)
        {
            children.RemoveAt(children.Count - 1);
        }
        while (children.Count < cities.Length)
        {
            children.Add(new TextBlock());
        }
        for (var i = 0; i < cities.Length; i++)
        {
            var region = _scene.CityRegion[i];
            var regionName = (uint)region < (uint)_regions.Length ? _regions[region].Name : string.Empty;
            ((TextBlock)children[i]).Text = labels.CityTableRow(cities[i].Name, regionName, FormatCurrency(_cityRevenue[i]),
                ShowpieceFormat.Grouped(_cityCount[i], 0, Culture));
        }
    }

    // Camera ----------------------------------------------------------------------------------------------------------------

    private void ConfigureCamera()
    {
        if (_canvas is null)
        {
            return;
        }
        var hq = Headquarters?.Longitude ?? 0;
        var center = hq + DriftCenterOffset;
        _canvas.ConfigureCamera(center, DriftHalfRange, DriftSpeed, HomeTilt);
        if (center != _placedCenter && !_viewHeld)
        {
            // New headquarters (or first use): start in the middle of the drift range.
            _placedCenter = center;
            _canvas.PlaceHome();
        }
        _canvas.SetDriftEnabled(!_viewHeld);
    }
}
