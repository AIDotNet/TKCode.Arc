using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class RevenueGlobe
{
    private bool _selectionSync;
    private bool _viewHeld;
    private int _hoverCity = -1;
    private int _listHoverCity = -1;
    private int _tipCity = -1;
    private int[] _labeledCities = [];

    private int SelectedCityIndex => SelectedCity is { } city ? IndexOfCity(city) : -1;

    private int SelectedRegionIndex => SelectedRegion is { } region ? Array.IndexOf(_regions, region) : -1;

    private int IndexOfCity(RevenueCity city)
    {
        var index = Array.IndexOf(_scene.Cities, city);
        return index >= 0 ? index : CityIndexOf(city.Key);
    }

    private void OnSelectionPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (_selectionSync)
        {
            return;
        }
        var oldCity = change.Property == SelectedCityProperty ? change.OldValue as RevenueCity : SelectedCity;
        var oldRegion = change.Property == SelectedRegionProperty ? change.OldValue as RevenueRegion : SelectedRegion;
        _selectionSync = true;
        try
        {
            // A city and a region are never selected together: the newer one wins.
            if (change.Property == SelectedCityProperty && SelectedCity is not null && SelectedRegion is not null)
            {
                oldRegion = SelectedRegion;
                SetCurrentValue(SelectedRegionProperty, null);
            }
            else if (change.Property == SelectedRegionProperty && SelectedRegion is not null && SelectedCity is not null)
            {
                oldCity = SelectedCity;
                SetCurrentValue(SelectedCityProperty, null);
            }
        }
        finally
        {
            _selectionSync = false;
        }
        ApplySelectionToParts(animate: true);
        if (SelectedCity is { } city)
        {
            Announce(Labels.CameraOn(city.Name));
        }
        else if (SelectedRegion is { } region)
        {
            Announce(Labels.CameraOn(region.Name));
        }
        else if (oldCity is not null || oldRegion is not null)
        {
            Announce(Labels.SelectionCleared);
        }
        RaiseEvent(new RevenueGlobeSelectionChangedEventArgs(SelectionChangedEvent, oldCity, SelectedCity, oldRegion, SelectedRegion));
    }

    /// <summary>Points the camera, rings, rows and card at the current selection.</summary>
    private void ApplySelectionToParts(bool animate)
    {
        var city = SelectedCityIndex;
        var region = SelectedRegionIndex;
        PseudoClasses.Set(CitySelectedPseudoClass, city >= 0);
        PseudoClasses.Set(RegionSelectedPseudoClass, region >= 0);
        if (_canvas is not null)
        {
            _canvas.SelectedCity = city;
            _canvas.SelectedRegion = region;
            if (city >= 0)
            {
                _viewHeld = true;
                _canvas.FlyTo(_scene.CityPoints[city], CityZoom);
            }
            else if (region >= 0 && RegionCenter(region) is { } center)
            {
                _viewHeld = true;
                _canvas.FlyTo(center, RegionZoom);
            }
            else if (_viewHeld)
            {
                _viewHeld = false;
                _canvas.ReleaseView(drift: true);
            }
            _canvas.InvalidateVisual();
        }
        UpdateRowSelection();
        UpdateRegionLabels();
        UpdateTip();
    }

    /// <summary>
    /// Where the camera flies for a region: its own center when set, otherwise the revenue-weighted center of its
    /// cities on the sphere.
    /// </summary>
    internal ShowpieceVec3? RegionCenter(int region)
    {
        if ((uint)region >= (uint)_regions.Length)
        {
            return null;
        }
        var r = _regions[region];
        if (r.CenterLatitude is { } latitude && r.CenterLongitude is { } longitude)
        {
            return ShowpieceProjection.LatLonToUnit(latitude, longitude);
        }
        var points = new List<ShowpieceVec3>();
        var weights = new List<double>();
        for (var i = 0; i < _scene.Cities.Length; i++)
        {
            if (_scene.CityRegion[i] == region)
            {
                points.Add(_scene.CityPoints[i]);
                weights.Add(_cityRevenue.Length > i ? _cityRevenue[i] : 0);
            }
        }
        return RevenueGlobeMath.Centroid(points.ToArray(), weights.ToArray());
    }

    private void UpdateRegionLabels()
    {
        var region = SelectedRegionIndex;
        if (region < 0)
        {
            if (_labeledCities.Length > 0)
            {
                _labeledCities = [];
                if (_canvas is not null)
                {
                    _canvas.LabeledCities = _labeledCities;
                }
            }
            return;
        }
        // The region's three biggest cities by revenue at the playhead get a ring and a name.
        Span<int> top = stackalloc int[3];
        var count = 0;
        for (var i = 0; i < _scene.Cities.Length; i++)
        {
            if (_scene.CityRegion[i] != region)
            {
                continue;
            }
            var position = count;
            while (position > 0 && _cityRevenue[top[position - 1]] < _cityRevenue[i])
            {
                if (position < 3)
                {
                    top[position] = top[position - 1];
                }
                position--;
            }
            if (position < 3)
            {
                top[position] = i;
                count = Math.Min(3, count + 1);
            }
        }
        var same = _labeledCities.Length == count;
        for (var i = 0; same && i < count; i++)
        {
            same = _labeledCities[i] == top[i];
        }
        if (!same)
        {
            _labeledCities = top[..count].ToArray();
            if (_canvas is not null)
            {
                _canvas.LabeledCities = _labeledCities;
            }
        }
    }

    // Selection commands ---------------------------------------------------------------------------------------------------

    /// <summary>Selects <paramref name="city"/>: the camera flies there along the great circle and holds the view.</summary>
    public void FlyTo(RevenueCity city)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (ReferenceEquals(SelectedCity, city))
        {
            ApplySelectionToParts(animate: true);
            return;
        }
        SelectedCity = city;
    }

    /// <summary>Selects <paramref name="region"/>: the camera flies to its center and the other cities fade.</summary>
    public void FlyTo(RevenueRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (ReferenceEquals(SelectedRegion, region))
        {
            ApplySelectionToParts(animate: true);
            return;
        }
        SelectedRegion = region;
    }

    /// <summary>Clears the selection and brings the camera home (drift range center, home tilt, zoom 1).</summary>
    public void ResetView()
    {
        ClearSelection();
        _canvas?.OrbitCamera.AnimateTo(new ShowpieceOrbitPose
        {
            Yaw = _canvas.OrbitCamera.DriftCenter,
            Pitch = ShowpieceProjection.Radians(HomeTilt),
            Zoom = 1
        }, ShowpieceSprings.GlobeFly);
        _canvas?.RequestFrames();
    }

    /// <summary>Turns the globe by the given degrees (positive longitude turns the view east).</summary>
    public void SpinBy(double longitudeDegrees, double latitudeDegrees) => _canvas?.SpinBy(longitudeDegrees, latitudeDegrees);

    private void ClearSelection()
    {
        if (SelectedCity is not null)
        {
            SetCurrentValue(SelectedCityProperty, null);
        }
        if (SelectedRegion is not null)
        {
            SetCurrentValue(SelectedRegionProperty, null);
        }
    }

    private void InvokeCity(int index)
    {
        if ((uint)index >= (uint)_scene.Cities.Length)
        {
            return;
        }
        RaiseEvent(new RevenueGlobeCityEventArgs(CityInvokedEvent, _scene.Cities[index]));
    }

    // Canvas events ---------------------------------------------------------------------------------------------------------

    private void OnCanvasCityTapped(int index)
    {
        InvokeCity(index);
        var city = _scene.Cities[index];
        if (!ReferenceEquals(SelectedCity, city))
        {
            SetCurrentValue(SelectedCityProperty, city);
        }
    }

    private void OnCanvasBackgroundTapped() => ClearSelection();

    private void OnCanvasEscape() => ClearSelection();

    private void OnCanvasHoverChanged(int index)
    {
        _hoverCity = index;
        UpdateTip();
    }

    private void OnCanvasDragBegan()
    {
        _hoverCity = -1;
        UpdateTip();
    }

    private void OnCanvasViewChanged()
    {
        if (_canvas is null)
        {
            return;
        }
        var camera = _canvas.OrbitCamera;
        CenterLongitude = ShowpieceProjection.Degrees(ShowpieceProjection.WrapAngle(camera.Yaw));
        CenterLatitude = ShowpieceProjection.Degrees(camera.Pitch);
        Zoom = camera.Zoom;
        if (_tipCity >= 0)
        {
            PlaceTip();
        }
    }

    // Rows ------------------------------------------------------------------------------------------------------------------

    private void OnRegionRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not RevenueGlobeRegionRow row || (uint)row.Index >= (uint)_regions.Length)
        {
            return;
        }
        var region = _regions[row.Index];
        SetCurrentValue(SelectedRegionProperty, ReferenceEquals(SelectedRegion, region) ? null : region);
    }

    private void OnCityRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not RevenueGlobeCityRow row || (uint)row.Index >= (uint)_scene.Cities.Length)
        {
            return;
        }
        InvokeCity(row.Index);
        var city = _scene.Cities[row.Index];
        SetCurrentValue(SelectedCityProperty, ReferenceEquals(SelectedCity, city) ? null : city);
    }

    private void OnCityRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is RevenueGlobeCityRow row && e.Pointer.Type != PointerType.Touch)
        {
            SetListHover(row.Index);
        }
    }

    private void OnCityRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is RevenueGlobeCityRow row && row.Index == _listHoverCity)
        {
            SetListHover(-1);
        }
    }

    private void SetListHover(int index)
    {
        _listHoverCity = index;
        if (_canvas is not null)
        {
            _canvas.ListHoverCity = index;
        }
        UpdateTip();
    }

    // Hover card ------------------------------------------------------------------------------------------------------------

    /// <summary>The city the card shows: the hovered dot, else the hovered row, else the selected city.</summary>
    private int TipCityWanted => _hoverCity >= 0 ? _hoverCity : _listHoverCity >= 0 ? _listHoverCity : SelectedCityIndex;

    /// <summary>The city the card is open for (−1 when closed; tests).</summary>
    internal int TipCity => _tipCity;

    private void UpdateTip()
    {
        var city = TipCityWanted;
        if (_tip is null || city < 0)
        {
            CloseTip();
            return;
        }
        _tipCity = city;
        FillTip();
        PlaceTip();
    }

    private void CloseTip()
    {
        _tipCity = -1;
        if (_tip is not null)
        {
            _tip.IsOpen = false;
        }
    }

    private void PlaceTip()
    {
        if (_tip is null || _canvas is null || _tipCity < 0)
        {
            return;
        }
        if (!_canvas.TryProjectCity(_tipCity, out var point, out _))
        {
            // The city turned to the far side.
            _tip.IsOpen = false;
            return;
        }
        _tip.ShowAt(point);
    }

    private void FillTip()
    {
        var index = _tipCity;
        if ((uint)index >= (uint)_scene.Cities.Length)
        {
            return;
        }
        var city = _scene.Cities[index];
        var region = _scene.CityRegion[index];
        var regionName = (uint)region < (uint)_regions.Length ? _regions[region].Name : string.Empty;
        var local = (EffectiveDayStart + TimeSpan.FromSeconds(_playback.Time)).UtcDateTime.AddHours(city.UtcOffsetHours);
        _tipContent.CityName = city.Name;
        _tipContent.Subtitle = Labels.CityLine(regionName, local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        _tipContent.TodayText = FormatCurrency(_cityRevenue[index]);
        _tipContent.PaymentsText = ShowpieceFormat.Grouped(_cityCount[index], 0, Culture);
        _tipContent.ShareText = ShowpieceFormat.Percent(_revenue > 0 ? _cityRevenue[index] / _revenue : 0, 1, false, Culture);
    }
}
