using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

public partial class CustomerGalaxy
{
    /// <summary>A list row's action for screen readers (default "Fly to this cluster").</summary>
    public static readonly StyledProperty<string> FlyToClusterTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(FlyToClusterText), "Fly to this cluster");

    /// <summary>A list row's action in the overview; <c>{0}</c> is the health grouping in lower case ("Group by {0} and fly to this cluster").</summary>
    public static readonly StyledProperty<string> GroupAndFlyFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(GroupAndFlyFormat), "Group by {0} and fly to this cluster");

    /// <inheritdoc cref="FlyToClusterTextProperty"/>
    public string FlyToClusterText { get => GetValue(FlyToClusterTextProperty); set => SetValue(FlyToClusterTextProperty, value); }

    /// <inheritdoc cref="GroupAndFlyFormatProperty"/>
    public string GroupAndFlyFormat { get => GetValue(GroupAndFlyFormatProperty); set => SetValue(GroupAndFlyFormatProperty, value); }

    private ArcContentSwap? _scope;
    private ShowpieceNumber? _mrrNumber;
    private ShowpieceNumber? _accountsNumber;
    private ShowpieceNumber? _growthNumber;
    private ShowpieceNumber? _riskNumber;
    private Border? _growthDot;
    private Border? _riskDot;
    private TextBlock? _growthLabel;
    private TextBlock? _riskLabel;
    private TextBlock? _listTitle;
    private GalaxySortButton? _sortMrr;
    private GalaxySortButton? _sortAccounts;
    private GalaxyRowsPanel? _rows;
    private readonly List<GalaxyGroupRow> _rowControls = new();
    private readonly List<GalaxyClusterLabel> _clusterLabels = new();

    /// <summary>The groups of the totals list: the clusters, or the health states in the overview.</summary>
    private GalaxyGroupSet ListSet => _layout ?? _model.HealthSet;

    private bool ListIsHealth => _layout is null || string.Equals(_layout.Key, GalaxyGrouping.HealthKey, StringComparison.OrdinalIgnoreCase);

    // Colors -----------------------------------------------------------------------------------------------------------

    /// <summary>A state's color: its fixed <see cref="GalaxyHealthState.Color"/>, else its tone's ArcDesign token now.</summary>
    internal Color ResolveColor(GalaxyHealthState state)
    {
        if (state.Color is { } color)
        {
            return color;
        }
        var kind = state.Tone switch
        {
            GalaxyTone.Success => ArcDesignTokenKind.Success,
            GalaxyTone.Warning => ArcDesignTokenKind.Warning,
            GalaxyTone.Danger => ArcDesignTokenKind.Danger,
            GalaxyTone.Accent => ArcDesignTokenKind.Accent,
            GalaxyTone.Series1 => ArcDesignTokenKind.Series1,
            GalaxyTone.Series2 => ArcDesignTokenKind.Series2,
            GalaxyTone.Series3 => ArcDesignTokenKind.Series3,
            GalaxyTone.Series4 => ArcDesignTokenKind.Series4,
            _ => ArcDesignTokenKind.TextMuted
        };
        return ArcTokens.GetColor(this, kind);
    }

    private IBrush BrushFor(GalaxyHealthState state, double opacity) => GalaxyBrushes.Of(ResolveColor(state), opacity);

    private void RefreshColors()
    {
        if (_stage is null)
        {
            return;
        }
        _stage.InvalidateColors();
        RefreshLegend();
        RefreshTotals();
        UpdateList(animate: false);
        UpdateCard();
    }

    // Texts ------------------------------------------------------------------------------------------------------------

    private void ApplyTexts()
    {
        if (_titleText is not null)
        {
            _titleText.Text = Title;
            _titleText.IsVisible = !string.IsNullOrEmpty(Title);
        }
        RefreshSubtitle();
        if (_tabs is not null)
        {
            AutomationProperties.SetName(_tabs, GroupByText);
        }
        if (_zoomBar is not null)
        {
            _zoomBar.ZoomInText = ZoomInText;
            _zoomBar.ZoomOutText = ZoomOutText;
            _zoomBar.FitText = FitText;
        }
        if (_sortMrr is not null)
        {
            _sortMrr.Content = MrrText;
        }
        if (_sortAccounts is not null)
        {
            _sortAccounts.Content = AccountsText;
        }
        if (_sortMrr?.Parent is Control group)
        {
            AutomationProperties.SetName(group, SortByText);
        }
        if (_stage is not null)
        {
            AutomationProperties.SetHelpText(_stage, KeyboardHelpText);
        }
        RefreshSortButtons();
    }

    private void RefreshSubtitle()
    {
        if (_subtitleText is null)
        {
            return;
        }
        var text = Subtitle ?? string.Format(Culture, SubtitleFormat, ShowpieceFormat.Grouped(_model.Count, 0, Culture));
        _subtitleText.Text = text;
        _subtitleText.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void RefreshSortButtons()
    {
        if (_sortMrr is not null)
        {
            _sortMrr.IsChecked = GroupSort == GalaxyGroupSort.Mrr;
        }
        if (_sortAccounts is not null)
        {
            _sortAccounts.IsChecked = GroupSort == GalaxyGroupSort.Accounts;
        }
    }

    // Tabs and legend --------------------------------------------------------------------------------------------------

    private void RefreshTabs(bool animate)
    {
        if (_tabs is null)
        {
            return;
        }
        var headers = new List<string>(Groupings.Count + 1) { AllText };
        foreach (var grouping in Groupings)
        {
            headers.Add(grouping.DisplayHeader);
        }
        var selected = 0;
        if (_layoutKey is not null)
        {
            selected = -1;
            for (var i = 0; i < Groupings.Count; i++)
            {
                if (string.Equals(Groupings[i].Key, _layoutKey, StringComparison.OrdinalIgnoreCase))
                {
                    selected = i + 1;
                    break;
                }
            }
        }
        _tabs.SetTabs(headers, selected, animate);
    }

    private void RefreshLegend()
    {
        if (_legend is null)
        {
            return;
        }
        var states = _model.States.Length > 0 ? _model.States : HealthStates.ToArray();
        var children = _legend.Children;
        var fits = children.Count == states.Length + 2 && children[^1] is GalaxySizeLegend && children[^2] is GalaxyLegendDivider;
        for (var i = 0; fits && i < states.Length; i++)
        {
            fits = children[i] is GalaxyLegendItem;
        }
        if (!fits)
        {
            children.Clear();
            foreach (var _ in states)
            {
                children.Add(new GalaxyLegendItem());
            }
            var divider = new GalaxyLegendDivider { Width = 1 };
            ArcTokens.Bind(divider, Border.BackgroundProperty, ArcDesignTokenKind.Border);
            children.Add(divider);
            children.Add(new GalaxySizeLegend());
        }
        for (var i = 0; i < states.Length; i++)
        {
            var item = (GalaxyLegendItem)children[i];
            item.Text = states[i].DisplayLabel;
            item.DotBrush = BrushFor(states[i], states[i].DotOpacity >= 0.9 ? 1 : states[i].DotOpacity);
        }
        var size = (GalaxySizeLegend)children[^1];
        size.Text = SizeLegendText;
        size.DotBrush = GalaxyBrushes.Of(ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted), 0.72);
    }

    // Labels -----------------------------------------------------------------------------------------------------------

    private Size[] LabelSizesFor(GalaxyGroupSet set)
    {
        var family = FontFamily;
        var name = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
        var detail = new Typeface(family);
        var sizes = new Size[set.Count];
        for (var g = 0; g < set.Count; g++)
        {
            var group = set.Groups[g];
            var title = new FormattedText(group.Label, Culture, FlowDirection.LeftToRight, name, 14, null).WidthIncludingTrailingWhitespace;
            var line = new FormattedText(FormatMrr(group.Mrr) + LabelDetail(group), Culture, FlowDirection.LeftToRight, detail, 12, null)
                .WidthIncludingTrailingWhitespace;
            sizes[g] = new Size(Math.Ceiling(Math.Max(title, line)) + 2, GalaxyLayout.LabelHeight);
        }
        return sizes;
    }

    private string LabelDetail(GalaxyGroupSummary group) => " · " + AccountCountText(group.AccountCount);

    private void RebuildLabels(bool animateIn)
    {
        if (_labels is null)
        {
            return;
        }
        _labels.Children.Clear();
        _clusterLabels.Clear();
        if (_layout is not { } layout)
        {
            return;
        }
        for (var g = 0; g < layout.Count; g++)
        {
            var group = layout.Groups[g];
            var label = new GalaxyClusterLabel
            {
                Text = group.Label,
                MrrFormatter = _mrrFormat,
                Mrr = group.Mrr,
                Detail = LabelDetail(group)
            };
            _clusterLabels.Add(label);
            _labels.Children.Add(label);
            if (animateIn)
            {
                label.Enter(TimeSpan.FromMilliseconds(120));
            }
        }
        RefreshLabelDimming();
        PositionLabels();
    }

    private void PositionLabels()
    {
        if (_stage is null || _layout is not { HasGeometry: true } layout || _clusterLabels.Count != layout.Count)
        {
            return;
        }
        var camera = _stage.View;
        var k = camera.Scale;
        for (var g = 0; g < layout.Count; g++)
        {
            var c = camera.WorldToScreen(layout.Centers[g]);
            _clusterLabels[g].Place(new Point(c.X, c.Y - layout.Radii[g] * k - GalaxyLayout.LabelGap));
        }
    }

    private void RefreshLabelDimming()
    {
        var keep = _focusIndex >= 0 ? _focusIndex : _layout is not null ? _hoverRow : -1;
        for (var g = 0; g < _clusterLabels.Count; g++)
        {
            _clusterLabels[g].IsDimmed = keep >= 0 && g != keep;
        }
    }

    // Totals -----------------------------------------------------------------------------------------------------------

    private void RefreshTotals()
    {
        var focused = _focusIndex >= 0 && _layout is { } layout ? layout.Groups[_focusIndex] : null;
        var empty = _model.Count == 0;
        if (_scope is not null)
        {
            var text = focused?.Label ?? AllAccountsText;
            if (!Equals(_scope.Content, text))
            {
                _scope.Content = text;
            }
        }
        var growthState = Array.FindIndex(_model.States, s => s.IsGrowth);
        var riskState = Array.FindIndex(_model.States, s => s.IsRisk);
        SetNumber(_mrrNumber, empty ? double.NaN : focused?.Mrr ?? _model.TotalMrr, money: true);
        SetNumber(_accountsNumber, empty ? double.NaN : focused?.AccountCount ?? _model.Count, money: false);
        SetNumber(_growthNumber, empty ? double.NaN : focused?.GrowthMrr ?? _model.GrowthMrr, money: true);
        SetNumber(_riskNumber, empty ? double.NaN : focused?.AtRiskMrr ?? _model.AtRiskMrr, money: true);
        if (_growthLabel is not null)
        {
            _growthLabel.Text = growthState >= 0 ? _model.States[growthState].DisplayLabel : null;
        }
        if (_riskLabel is not null)
        {
            _riskLabel.Text = riskState >= 0 ? _model.States[riskState].DisplayLabel : null;
        }
        if (_growthDot is not null && growthState >= 0)
        {
            _growthDot.Background = BrushFor(_model.States[growthState], 1);
        }
        if (_riskDot is not null && riskState >= 0)
        {
            _riskDot.Background = BrushFor(_model.States[riskState], 1);
        }
        if (_growthNumber?.Parent is Control growthCell)
        {
            growthCell.IsVisible = growthState >= 0;
        }
        if (_riskNumber?.Parent is Control riskCell)
        {
            riskCell.IsVisible = riskState >= 0;
        }
    }

    private void SetNumber(ShowpieceNumber? number, double value, bool money)
    {
        if (number is null)
        {
            return;
        }
        if (money)
        {
            number.Formatter = _mrrFormat;
        }
        else
        {
            number.Formatter = null;
            number.Format = ShowpieceNumberFormat.Number;
            number.CultureName = string.IsNullOrEmpty(CultureName) ? "en-US" : CultureName;
        }
        number.Value = value;
    }

    // List -------------------------------------------------------------------------------------------------------------

    private void RebuildList()
    {
        if (_rows is null)
        {
            return;
        }
        foreach (var row in _rowControls)
        {
            row.Click -= OnRowClick;
            row.PointerEntered -= OnRowEntered;
            row.PointerExited -= OnRowExited;
        }
        _rowControls.Clear();
        _rows.Children.Clear();
        _rows.Reset();
        var set = ListSet;
        for (var g = 0; g < set.Count; g++)
        {
            var row = new GalaxyGroupRow { Key = set.Groups[g].Key, Label = set.Groups[g].Label };
            row.Click += OnRowClick;
            row.PointerEntered += OnRowEntered;
            row.PointerExited += OnRowExited;
            _rowControls.Add(row);
            _rows.Children.Add(row);
        }
        UpdateList(animate: false);
    }

    private void UpdateList(bool animate)
    {
        if (_rows is null || _stage is null)
        {
            return;
        }
        var set = ListSet;
        if (_listTitle is not null)
        {
            _listTitle.Text = ListTitle();
        }
        if (_rowControls.Count != set.Count)
        {
            return;
        }
        var byAccounts = GroupSort == GalaxyGroupSort.Accounts;
        double max = 0;
        foreach (var group in set.Groups)
        {
            max = Math.Max(max, byAccounts ? group.AccountCount : group.Mrr);
        }
        var states = _model.States;
        var healthList = ListIsHealth;
        for (var g = 0; g < set.Count; g++)
        {
            var group = set.Groups[g];
            var row = _rowControls[g];
            row.Label = group.Label;
            if (byAccounts)
            {
                row.SetNumber(group.AccountCount, ShowpieceNumberFormat.Number, null);
            }
            else
            {
                row.SetNumber(group.Mrr, ShowpieceNumberFormat.Number, _mrrFormat);
            }
            var share = healthList ? string.Empty : " · " + string.Format(Culture, AtRiskShareFormat, ShowpieceFormat.Percent(group.AtRiskShare, 0, false, Culture));
            row.Caption = (byAccounts ? FormatMrr(group.Mrr) : AccountCountText(group.AccountCount)) + share;
            row.IsChecked = _layout is not null && g == _focusIndex;
            var fraction = max > 0 ? (byAccounts ? group.AccountCount : group.Mrr) / max : 0;
            double[] weights;
            IBrush?[] brushes;
            if (healthList)
            {
                var state = StateOfGroup(group);
                weights = new[] { 1.0 };
                brushes = new IBrush?[] { state is null ? null : BrushFor(state, state.BarOpacity) };
            }
            else
            {
                weights = new double[states.Length];
                brushes = new IBrush?[states.Length];
                for (var s = 0; s < states.Length; s++)
                {
                    weights[s] = group.HealthMrr[s];
                    brushes[s] = BrushFor(states[s], states[s].BarOpacity);
                }
            }
            row.SetBar(fraction, weights, brushes, animate);
            var action = _layout is null
                ? string.Format(Culture, GroupAndFlyFormat, HealthGroupingName().ToLower(Culture))
                : FlyToClusterText;
            var risk = healthList ? string.Empty : ", " + string.Format(Culture, AtRiskShareFormat, ShowpieceFormat.Percent(group.AtRiskShare, 0, false, Culture));
            AutomationProperties.SetName(row, $"{group.Label}, {FormatMrr(group.Mrr)} {MrrText}, {AccountCountText(group.AccountCount)}{risk}");
            AutomationProperties.SetHelpText(row, action);
        }
        var order = Enumerable.Range(0, set.Count)
                              .OrderByDescending(g => byAccounts ? set.Groups[g].AccountCount : set.Groups[g].Mrr)
                              .ThenBy(g => g)
                              .ToArray();
        _rows.SetOrder(order, animate);
    }

    private GalaxyHealthState? StateOfGroup(GalaxyGroupSummary group)
    {
        foreach (var state in _model.States)
        {
            if (string.Equals(state.Key, group.Key, StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }
        return null;
    }

    private string ListTitle()
    {
        // The overview lists the health states: it borrows the health grouping's title when there is one.
        var grouping = _layoutKey is not null ? FindGrouping(_layoutKey) : Groupings.FirstOrDefault(g => g.IsHealth);
        if (grouping is not null)
        {
            return grouping.ListTitle ?? string.Format(Culture, ListTitleFormat, grouping.DisplayHeader.ToLower(Culture));
        }
        return string.Format(Culture, ListTitleFormat, HealthGroupingText.ToLower(Culture));
    }

    /// <summary>The name of the health grouping: its tab's header when it has one, otherwise <see cref="HealthGroupingText"/>.</summary>
    private string HealthGroupingName() =>
        Groupings.FirstOrDefault(g => g.IsHealth) is { } health && !string.IsNullOrEmpty(health.Header) ? health.Header! : HealthGroupingText;

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is GalaxyGroupRow row)
        {
            FlyToGroup(row.Key);
        }
    }

    private void OnRowEntered(object? sender, PointerEventArgs e)
    {
        if (sender is GalaxyGroupRow row && e.Pointer.Type != PointerType.Touch)
        {
            SetRowHover(ListSet.IndexOf(row.Key));
        }
    }

    private void OnRowExited(object? sender, PointerEventArgs e) => SetRowHover(-1);

    /// <summary>Hovering a row (nothing focused) dims every other cluster and its label; the numbers stay.</summary>
    private void SetRowHover(int group)
    {
        if (_hoverRow == group)
        {
            return;
        }
        _hoverRow = group;
        if (_focusIndex < 0)
        {
            _stage?.Dim(group);
        }
        RefreshLabelDimming();
        RefreshStates();
    }

    // States -----------------------------------------------------------------------------------------------------------

    private void RefreshStates()
    {
        PseudoClasses.Set(GroupedPseudoClass, _layout is not null);
        PseudoClasses.Set(ClusterFocusedPseudoClass, _focusIndex >= 0);
        PseudoClasses.Set(RowHoverPseudoClass, _hoverRow >= 0 && _focusIndex < 0);
        PseudoClasses.Set(EmptyPseudoClass, _model.Count == 0);
        if (_showAllHost is not null)
        {
            _showAllHost.IsPresent = _focusIndex >= 0;
        }
        RefreshSubtitle();
    }

    private void RefreshSummary()
    {
        var set = ListSet;
        Summary = new GalaxySummary(_model.TotalMrr, _model.Count, _model.GrowthMrr, _model.AtRiskMrr, _model.HealthSet.Groups, set.Groups);
        if (_table is not null)
        {
            var rows = new List<string>(set.Count);
            foreach (var group in set.Groups)
            {
                var parts = new List<string>(_model.States.Length);
                for (var s = 0; s < _model.States.Length; s++)
                {
                    parts.Add(_model.States[s].DisplayLabel + " " + ShowpieceFormat.Grouped(group.HealthCounts[s], 0, Culture));
                }
                rows.Add($"{group.Label}: {AccountCountText(group.AccountCount)}, {FormatMrr(group.Mrr)} {MrrText}; {string.Join(", ", parts)}");
            }
            _table.SetRows(ListTitle(), rows);
        }
        if (_stage is not null)
        {
            AutomationProperties.SetName(_stage, StageSummaryText());
        }
    }
}
