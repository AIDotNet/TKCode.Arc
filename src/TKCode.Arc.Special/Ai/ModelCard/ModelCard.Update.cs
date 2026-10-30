using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// How ModelCard brings its parts in line with its state, its width and the person's pointer and keys.
public partial class ModelCard
{
    /// <summary>Card widths where the layout changes (the card's own width, like a container query).</summary>
    internal const double NarrowWidth = 480;

    internal const double CompactWidth = 420;

    internal const double TinyWidth = 360;

    internal const double CompareWideWidth = 520;

    /// <summary>The panel crossfade: the old panel fades out in 100ms while the new one fades in over 160ms.</summary>
    internal static readonly TimeSpan PanelFadeOut = TimeSpan.FromMilliseconds(100);

    internal static readonly TimeSpan PanelFadeIn = TimeSpan.FromMilliseconds(160);

    /// <summary>The first reveal of the benchmark bars: one after another, 40ms apart.</summary>
    internal static readonly TimeSpan BarStagger = TimeSpan.FromMilliseconds(40);

    /// <summary>One period of the skeleton's pulse.</summary>
    private const double PulsePeriod = 1.6;

    private bool _syncing;
    private int? _hoverRow;
    private int? _focusRow;
    private (bool Narrow, bool Compact, bool Tiny, bool CompareWide)? _breakpoints;
    private ViewportVisibility? _viewport;
    private LiveLoop? _pulse;
    private double _pulseTime;
    private readonly string[] _readouts = new string[BenchmarkRows];

    /// <summary>The breakpoints in effect (tests).</summary>
    internal bool IsNarrow => _breakpoints?.Narrow == true;

    internal bool IsCompact => _breakpoints?.Compact == true;

    internal bool IsTiny => _breakpoints?.Tiny == true;

    internal bool IsCompareWide => _breakpoints?.CompareWide == true;

    /// <summary>The benchmark row under the pointer or keyboard focus, or null.</summary>
    internal int? ActiveRow => _hoverRow ?? _focusRow;

    /// <summary>The panel shown, for tests.</summary>
    internal ModelCardPanelKind? ShownPanel => _shownPanel;

    internal Control? PanelOf(ModelCardPanelKind kind) => _panels.GetValueOrDefault(kind);

    internal IReadOnlyList<ModelCardBenchmarkRow> BenchmarkRowParts => _rows;

    internal IReadOnlyList<ModelCardCompareRow> CompareRowParts => _compareRows;

    internal IReadOnlyList<ModelCardChip> Chips => _chipList;

    internal IReadOnlyList<ModelCardSpecCell> SpecCells => _specCells;

    internal IReadOnlyList<ModelCardSpecCell> PriceCells => _priceCells;

    internal Switch? CompareSwitch => _compareSwitch;

    internal Slider? VolumeSlider => _slider;

    internal SegmentedControl? Switcher => _switcher;

    internal Button? RivalButton => _rivalButton;

    internal MenuSurface? RivalMenu => _rivalMenu;

    internal CopyButton? CopyIdButton => _copy;

    internal Button? SelectButton => _select;

    internal Button? RetryButton => _retry;

    internal ModelCardNumber? ContextValue => _context;

    internal ModelCardNumber? PagesValue => _pages;

    internal ModelCardBar? Gauge => _gauge;

    internal ModelCardNumber? MaxOutputValue => _maxOutput;

    internal ModelCardText? CutoffText => _cutoff;

    internal ModelCardNumber? InputPrice => _input;

    internal ModelCardNumber? OutputPrice => _output;

    internal ModelCardNumber? CachedPrice => _cached;

    internal ModelCardNumber? CostValue => _cost;

    internal ModelCardText? NameText => _name;

    internal ModelCardText? ProviderText => _provider;

    internal ModelCardText? SummaryText => _summary;

    internal ModelCardText? ReadoutText => _readout;

    internal ModelCardText? CaptionText => _caption;

    internal ModelCardText? TallyText => _tally;

    internal TextBlock? FootnoteText => _footnote;

    internal ModelCardText? IdText => _id;

    internal TextBlock? StatusText => _status;

    internal TextBlock? ErrorMessage => _errorText;

    internal StackPanel? BenchmarkSection => _benchSection;

    internal Grid? Stack => _stack;

    /// <summary>The words that do not depend on the model.</summary>
    private void RefreshWords()
    {
        if (_root is null)
        {
            return;
        }
        var labels = Labels;
        if (_compareSwitch is not null)
        {
            _compareSwitch.Content = labels.Compare;
            AutomationProperties.SetName(_compareSwitch, labels.Compare);
        }
        _specCells[0].SetLabel(labels.ContextWindow);
        _specCells[1].SetLabel(labels.MaxOutput);
        _specCells[2].SetLabel(labels.KnowledgeCutoff);
        _priceCells[0].SetLabel(labels.Input);
        _priceCells[1].SetLabel(labels.Output);
        _priceCells[2].SetLabel(labels.CachedInput);
        _pricesHeading!.Text = labels.PricesHeading;
        _estimateHeading!.Text = labels.EstimateHeading;
        _slider!.Label = labels.MonthlyVolume;
        _benchHeading!.Text = labels.BenchmarksHeading;
        AutomationProperties.SetName(_switcher!, labels.BenchmarkSwitcher);
        _compareWithLabel!.Text = labels.CompareWith;
        _copy!.Label = labels.CopyId;
        _copy.CopiedLabel = labels.Copied;
        AutomationProperties.SetName(_copy, labels.CopyId);
        _retry!.Content = labels.Retry;
        _compareRowsKey = null;
    }

    /// <summary>Brings every part in line with the state; values that changed crossfade or roll when <paramref name="animate"/>.</summary>
    private void Refresh(bool animate)
    {
        if (_root is null)
        {
            return;
        }
        var anim = animate && IsLoaded && ArcMotion.IsFull(this);
        var model = ShownModel;
        var labels = Labels;
        _syncing = true;
        try
        {
            AutomationProperties.SetName(this, Model is null ? labels.NoModel : model.Name);
            RefreshHeader(model, labels, anim);
            RefreshDetails(model, labels, anim);
            RefreshCompare(model, labels, anim);
            RefreshStates(labels);
            RefreshFooter(model, labels, anim);
            ShowPanel(ActivePanel, animate);
        }
        finally
        {
            _syncing = false;
        }
        UpdatePulse();
        WatchViewport();
    }

    private void RefreshHeader(ModelCardModel model, ModelCardLabels labels, bool anim)
    {
        var markKey = model.Id + "\u0001" + model.Name + "\u0001" + model.MarkColor + "\u0001" + model.Mark?.GetHashCode();
        if (_markSwap is not null && markKey != _shownMarkKey)
        {
            _markSwap.Mode = anim && _shownMarkKey is not null ? ContentSwapMode.Crossfade : ContentSwapMode.None;
            _shownMarkKey = markKey;
            _markSwap.Content = new ModelCardMark(model);
        }
        _name!.Show(Model is null ? labels.NoModel : model.Name, anim);
        _provider!.Show(model.Provider ?? string.Empty, anim);
        if (_compareSwitch is not null)
        {
            _compareSwitch.IsVisible = ShownRivals.Count > 0;
            _compareSwitch.IsEnabled = !IsLoading && string.IsNullOrEmpty(ErrorText);
            _compareSwitch.IsChecked = View == ModelCardView.Compare;
        }
    }

    private bool PoolHasCached() =>
        ShownModel.Pricing?.CachedInput is not null || ShownRivals.Any(r => r.Pricing?.CachedInput is not null);

    private void RefreshDetails(ModelCardModel model, ModelCardLabels labels, bool anim)
    {
        _summary!.Show(model.Summary ?? string.Empty, anim);
        foreach (var chip in _chipList)
        {
            var supported = (model.Modalities & chip.Modality) != 0;
            chip.SetSupported(supported, ModalityLabel(chip.Modality, labels), supported ? labels.Supported : labels.NotSupported);
        }

        var notListed = labels.NotListed;
        _context!.Show(model.ContextWindow is { } window ? ModelCardFormat.Tokens(window) : null, notListed, anim);
        var widest = Math.Max(model.ContextWindow ?? 0, ShownRivals.Select(r => r.ContextWindow ?? 0).DefaultIfEmpty(0).Max());
        _gauge!.SetFraction(widest > 0 ? (model.ContextWindow ?? 0) / (double)widest : 0, anim);
        if (_gauge.IsArmed)
        {
            _gauge.Reveal(TimeSpan.Zero, animate: false);
        }
        _pages!.Show(model.ContextWindow is { } tokens
            ? new ModelCardNumberValue(ModelCardFormat.Pages(tokens), 0, "≈ ", " " + labels.Pages)
            : null, string.Empty, anim);
        _maxOutput!.Show(model.MaxOutput is { } output ? ModelCardFormat.Tokens(output) : null, notListed, anim);
        _cutoff!.Show(ModelCardFormat.Month(model.KnowledgeCutoff, notListed), anim);
        SetSpecCellNames();

        var pricing = model.Pricing;
        _input!.Show(pricing is null ? null : ModelCardFormat.Price(pricing.Input, Currency), notListed, anim);
        _output!.Show(pricing is null ? null : ModelCardFormat.Price(pricing.Output, Currency), notListed, anim);
        _cached!.Show(pricing?.CachedInput is { } cached ? ModelCardFormat.Price(cached, Currency) : null, notListed, anim);
        var priceCount = PoolHasCached() ? 3 : 2;
        if (_priceGrid!.Tag is not int shownCount || shownCount != priceCount)
        {
            _priceGrid.Tag = priceCount;
            LayoutCells(_priceGrid, _priceCells, priceCount, IsNarrow);
        }

        var index = ModelCardFormat.SnapIndex(MonthlyTokens);
        if (_slider!.Value != index)
        {
            _slider.Value = index;
        }
        var cost = ModelCardFormat.MonthlyCost(model, MonthlyTokens, InputShare);
        _cost!.Show(cost is { } c ? ModelCardFormat.Cost(c, Currency, labels.PerMonth) : null, notListed, anim);
        AutomationProperties.SetName(_cost, labels.EstimateName(_cost.Text, ModelCardFormat.TokensText(MonthlyTokens)));
        _mix!.Text = labels.InputMix(InputShare);

        RefreshBenchmarks(model, labels, anim);
    }

    private void SetSpecCellNames()
    {
        foreach (var cell in _specCells.Concat(_priceCells))
        {
            AutomationProperties.SetName(cell, cell.Label);
        }
    }

    private static string ModalityLabel(ModelCardModalities modality, ModelCardLabels labels) => modality switch
    {
        ModelCardModalities.Text => labels.Text,
        ModelCardModalities.Image => labels.Image,
        ModelCardModalities.Pdf => labels.Pdf,
        ModelCardModalities.Audio => labels.Audio,
        ModelCardModalities.Video => labels.Video,
        ModelCardModalities.Tools => labels.Tools,
        _ => labels.Reasoning
    };

    private void RefreshBenchmarks(ModelCardModel model, ModelCardLabels labels, bool anim)
    {
        var offered = OfferedBenchmarks;
        var benchmark = CurrentBenchmark;
        _benchSection!.IsVisible = benchmark is not null;
        var key = string.Join("\u0001", offered.Select(b => b.Id + "\u0002" + (b.ShortName ?? b.Name)));
        if (key != _switcherKey)
        {
            _switcherKey = key;
            _switcher!.ItemsSource = offered.Select(b => new SegmentedOption(b.Id, b.ShortName ?? b.Name)).ToList();
        }
        if (benchmark is null)
        {
            return;
        }
        if (!Equals(_switcher!.SelectedValue, benchmark.Id))
        {
            _switcher.SelectedValue = benchmark.Id;
        }
        var caption = benchmark.Name;
        if (!string.IsNullOrWhiteSpace(benchmark.Description))
        {
            caption += ", " + benchmark.Description;
        }
        if (IsSample)
        {
            caption += ". " + labels.SampleScores;
        }
        _caption!.Show(caption, anim);

        var rivals = ShownRivals;
        var entries = new ModelCardModel?[BenchmarkRows];
        entries[0] = model;
        for (var i = 1; i < BenchmarkRows; i++)
        {
            entries[i] = i - 1 < rivals.Count ? rivals[i - 1] : null;
        }
        var field = new List<(ModelCardModel Model, double Score)>();
        foreach (var candidate in rivals.Prepend(model))
        {
            if (candidate.ScoreOf(benchmark.Id) is { } s)
            {
                field.Add((candidate, s));
            }
        }
        field.Sort((a, b) => b.Score.CompareTo(a.Score));
        var scores = field.Select(f => f.Score).ToList();
        for (var i = 0; i < BenchmarkRows; i++)
        {
            var row = _rows[i];
            row.SetOwn(i == 0);
            if (entries[i] is not { } entry)
            {
                row.SetEmpty(true);
                _readouts[i] = string.Empty;
                continue;
            }
            row.SetEmpty(false);
            var score = entry.ScoreOf(benchmark.Id);
            row.Score.Show(score is { } value ? ModelCardFormat.Score(value, benchmark) : null, labels.None, anim);
            row.Bar.SetFraction(score is { } v && benchmark.Max > 0 ? v / benchmark.Max : 0, anim);
            var readout = ReadoutOf(entry, score, benchmark, field, scores, labels);
            _readouts[i] = readout;
            row.SetModel(entry.Name, readout, anim);
        }
        UpdateEmphasis();
    }

    /// <summary>A row's readout: the score, the rank among every model with a score, and the gap to the leader (or the lead).</summary>
    private static string ReadoutOf(ModelCardModel entry, double? score, ModelCardBenchmark benchmark,
        List<(ModelCardModel Model, double Score)> field, List<double> scores, ModelCardLabels labels)
    {
        if (score is not { } value)
        {
            return labels.NoScore(entry.Name);
        }
        var rank = ModelCardFormat.Rank(value, scores);
        var gap = string.Empty;
        if (rank == 1)
        {
            var runnerUp = field.FirstOrDefault(f => !ReferenceEquals(f.Model, entry) && f.Score <= value);
            if (runnerUp.Model is not null && value - runnerUp.Score > 1e-9)
            {
                gap = labels.Leads(ModelCardFormat.Points(value - runnerUp.Score), runnerUp.Model.Name);
            }
        }
        else
        {
            var leader = field[0];
            gap = labels.Behind(ModelCardFormat.Points(leader.Score - value), leader.Model.Name);
        }
        return labels.Readout(entry.Name, ModelCardFormat.Score(value, benchmark).Text, labels.Rank(rank, scores.Count), gap);
    }

    private void RefreshStates(ModelCardLabels labels)
    {
        _errorText!.Text = ErrorText ?? string.Empty;
        _retry!.IsVisible = CanRetry;
        AutomationProperties.SetName(_errorText, ErrorText ?? string.Empty);
        if (_stack is not null)
        {
            AutomationProperties.SetHelpText(_stack, IsLoading ? labels.Loading : string.Empty);
        }
    }

    private void RefreshFooter(ModelCardModel model, ModelCardLabels labels, bool anim)
    {
        _id!.Show(model.Id, anim);
        _copy!.Value = model.Id;
        _copy.IsEnabled = model.Id.Length > 0;
        _select!.IsVisible = CanSelect;
        _select.IsEnabled = Model is not null;
        _selectCheck!.IsVisible = IsSelected;
        var text = IsSelected ? labels.Selected : SelectLabel ?? labels.UseModel;
        _selectText!.Text = text;
        AutomationProperties.SetName(_select, text);
        _footerButtons!.IsVisible = true;
    }

    /// <summary>Lays out spec or price cells as tiles side by side, or as rows (narrow).</summary>
    private static void LayoutCells(Grid grid, List<ModelCardSpecCell> cells, int count, bool stacked)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            cell.IsVisible = i < count;
            cell.SetStacked(stacked);
            if (stacked)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(cell, i);
                Grid.SetColumn(cell, 0);
            }
            else
            {
                if (i < count)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                }
                Grid.SetRow(cell, 0);
                Grid.SetColumn(cell, Math.Min(i, count - 1));
            }
        }
    }

    /// <summary>Applies the layout of the card's width (the card reads its own width, like a container query).</summary>
    private void ApplyBreakpoints(double width, bool force)
    {
        if (_root is null)
        {
            return;
        }
        if (!(width > 0))
        {
            width = 640;
        }
        var next = (width < NarrowWidth, width < CompactWidth, width < TinyWidth, width >= CompareWideWidth);
        if (!force && _breakpoints == next)
        {
            return;
        }
        _breakpoints = next;
        var (narrow, compact, tiny, compareWide) = next;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        _summary!.Lines = compact ? 3 : 2;
        _readout!.Lines = narrow ? 3 : 2;
        LayoutCells(_specGrid!, _specCells, 3, narrow);
        var priceCount = PoolHasCached() ? 3 : 2;
        _priceGrid!.Tag = priceCount;
        LayoutCells(_priceGrid, _priceCells, priceCount, narrow);

        // The estimate drops under the slider on narrow cards.
        var grid = _estimateGrid!;
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        if (narrow)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(_slider!, 0);
            Grid.SetColumn(_cost!, 0);
            Grid.SetRow(_cost!, 1);
            _cost!.Alignment = HorizontalAlignment.Left;
            _cost.HorizontalAlignment = HorizontalAlignment.Left;
            _cost.Margin = new Thickness(0, 4, 0, 0);
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetRow(_cost!, 0);
            Grid.SetColumn(_cost!, 1);
            _cost!.Alignment = HorizontalAlignment.Right;
            _cost.HorizontalAlignment = HorizontalAlignment.Stretch;
            _cost.Margin = new Thickness(16, 0, 0, 8);
        }

        // The benchmark switcher drops under its heading on narrow cards.
        var header = _benchHeader!;
        header.ColumnDefinitions.Clear();
        header.RowDefinitions.Clear();
        if (narrow)
        {
            header.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            header.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(_switcher!, 0);
            Grid.SetRow(_switcher!, 1);
            _switcher!.HorizontalAlignment = HorizontalAlignment.Left;
            _switcher.Margin = new Thickness(0, 8, 0, 0);
        }
        else
        {
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetRow(_switcher!, 0);
            Grid.SetColumn(_switcher!, 1);
            _switcher!.HorizontalAlignment = HorizontalAlignment.Right;
            _switcher.Margin = new Thickness(12, 0, 0, 0);
        }

        foreach (var row in _rows)
        {
            row.SetStacked(tiny);
        }
        LayoutCompare(compareWide);

        // The footer puts the buttons under the model id on narrow cards.
        var footer = _footer!;
        footer.ColumnDefinitions.Clear();
        footer.RowDefinitions.Clear();
        if (narrow)
        {
            footer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            footer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(_footerButtons!, 0);
            Grid.SetRow(_footerButtons!, 1);
            _footerButtons!.HorizontalAlignment = HorizontalAlignment.Left;
            _footerButtons.Margin = default;
        }
        else
        {
            footer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            footer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetRow(_footerButtons!, 0);
            Grid.SetColumn(_footerButtons!, 1);
            _footerButtons!.HorizontalAlignment = HorizontalAlignment.Right;
            _footerButtons.Margin = new Thickness(12, 0, 0, 0);
        }
    }

    /// <summary>Shows one panel of the stacked cell: the old one fades out in 100ms while the new one fades in over 160ms.</summary>
    /// <remarks>
    /// Code-driven (<see cref="MotionValue{T}"/>) because the two directions have different durations and a switch can
    /// interrupt another mid-fade; the panels stay mounted in one grid cell so the card keeps the tallest panel's height,
    /// which <see cref="ArcContentSwap"/> (it lets the outgoing content leave the layout) cannot guarantee.
    /// </remarks>
    private void ShowPanel(ModelCardPanelKind kind, bool animate)
    {
        if (_shownPanel == kind)
        {
            return;
        }
        var first = _shownPanel is null;
        _shownPanel = kind;
        var fades = animate && !first && IsLoaded && ArcMotion.IsFull(this);
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var refocus = false;
        foreach (var (panelKind, panel) in _panels)
        {
            var active = panelKind == kind;
            panel.IsHitTestVisible = active;
            KeyboardNavigation.SetTabNavigation(panel, active ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
            AutomationProperties.SetAccessibilityView(panel, active ? AccessibilityView.Default : AccessibilityView.Raw);
            if (!active && focused is not null && panel.IsVisualAncestorOf(focused))
            {
                refocus = true;
            }
            if (!_fades.TryGetValue(panelKind, out var fade))
            {
                fade = new MotionValue<double>(panel, panel.Opacity, MotionVectors.Double);
                var target = panel;
                fade.Changed += value => target.Opacity = value;
                _fades[panelKind] = fade;
            }
            var goal = active ? 1.0 : 0.0;
            if (fades)
            {
                _ = fade.AnimateTo(goal, MotionSpec.Tween(active ? PanelFadeIn : PanelFadeOut, ArcEasings.Standard));
            }
            else
            {
                fade.Jump(goal);
                panel.Opacity = goal;
            }
        }
        if (refocus && _compareSwitch is { IsVisible: true, IsEnabled: true })
        {
            _compareSwitch.Focus(SpecialFocusModality.For(this));
        }
    }

    private void SetHover(int index)
    {
        _hoverRow = index;
        UpdateEmphasis();
    }

    private void ClearHover(int index)
    {
        if (_hoverRow == index)
        {
            _hoverRow = null;
            UpdateEmphasis();
        }
    }

    private void SetFocusRow(int index)
    {
        _focusRow = index;
        UpdateEmphasis();
    }

    private void ClearFocusRow(int index)
    {
        if (_focusRow == index)
        {
            _focusRow = null;
            UpdateEmphasis();
        }
    }

    /// <summary>Tints the hovered or focused row, fades the others to half and crossfades the readout to that row.</summary>
    private void UpdateEmphasis()
    {
        if (_readout is null)
        {
            return;
        }
        var active = ActiveRow;
        for (var i = 0; i < BenchmarkRows; i++)
        {
            _rows[i].SetEmphasis(active == i, active is not null && active != i);
        }
        var text = active is { } a && _readouts[a] is { Length: > 0 } readout ? readout : _readouts[0] ?? string.Empty;
        _readout.Show(text, animate: true);
    }

    private void OnCompareToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _compareSwitch is null)
        {
            return;
        }
        var view = _compareSwitch.IsChecked == true ? ModelCardView.Compare : ModelCardView.Details;
        if (view == View)
        {
            return;
        }
        SetCurrentValue(ViewProperty, view);
        RaiseEvent(new ModelCardViewChangedEventArgs(ViewChangedEvent, view));
    }

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        var stops = ModelCardFormat.VolumeStops;
        var tokens = stops[Math.Clamp((int)Math.Round(e.NewValue), 0, stops.Length - 1)];
        if (tokens == MonthlyTokens)
        {
            return;
        }
        SetCurrentValue(MonthlyTokensProperty, tokens);
        RaiseEvent(new ModelCardMonthlyTokensEventArgs(MonthlyTokensChangedEvent, tokens));
    }

    private void OnSwitcherChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _switcher?.SelectedValue is not string id || id == CurrentBenchmark?.Id)
        {
            return;
        }
        SetCurrentValue(BenchmarkProperty, id);
        RaiseEvent(new ModelCardIdEventArgs(BenchmarkChangedEvent, id));
    }

    private void OnRivalPicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is not string id || id == CurrentRival?.Id)
        {
            return;
        }
        SetCurrentValue(CompareWithProperty, id);
        RaiseEvent(new ModelCardIdEventArgs(CompareWithChangedEvent, id));
    }

    /// <summary>Says what changed in the polite status region.</summary>
    private void Announce(AvaloniaProperty property)
    {
        if (_status is null)
        {
            return;
        }
        var labels = Labels;
        string? message = null;
        if (IsLoading)
        {
            message = labels.Loading;
        }
        else if (!string.IsNullOrEmpty(ErrorText))
        {
            message = null;
        }
        else if (property == BenchmarkProperty)
        {
            message = CurrentBenchmark is { } benchmark ? labels.ShowingBenchmark(benchmark.Name) : null;
        }
        else if (View == ModelCardView.Compare && CurrentRival is { } rival)
        {
            message = labels.Comparing(ShownModel.Name, rival.Name);
        }
        else if (property == ViewProperty)
        {
            message = labels.ShowingDetails(ShownModel.Name);
        }
        if (message is not null)
        {
            _status.Text = message;
            AutomationProperties.SetName(_status, message);
        }
    }

    /// <summary>
    /// The benchmark bars grow from zero once, the first time the card is mostly in view, 40ms apart. Without Full motion
    /// they are drawn at once.
    /// </summary>
    private void WatchViewport()
    {
        if (_root is null || !_rows[0].Bar.IsArmed)
        {
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            RevealBars(animate: false);
            return;
        }
        if (_viewport is null && TopLevel.GetTopLevel(this) is not null)
        {
            _viewport = new ViewportVisibility(this, 0.5, once: true);
            _viewport.VisibilityChanged += (_, visible) =>
            {
                if (visible)
                {
                    RevealBars(animate: true);
                }
            };
            if (_viewport.IsVisible)
            {
                RevealBars(animate: true);
            }
        }
    }

    private void RevealBars(bool animate)
    {
        for (var i = 0; i < BenchmarkRows; i++)
        {
            _rows[i].Bar.Reveal(TimeSpan.FromTicks(BarStagger.Ticks * i), animate);
        }
    }

    /// <summary>The skeleton pulses while loading (Full motion only; reduced motion holds it still).</summary>
    private void UpdatePulse()
    {
        if (_skeleton is null)
        {
            return;
        }
        if (IsLoading && TopLevel.GetTopLevel(this) is not null)
        {
            _pulse ??= new LiveLoop(_skeleton, dt =>
            {
                _pulseTime += dt;
                var low = this.TryFindResource(ModelCardTokens.Key(ModelCardTokenKind.SkeletonPulseOpacity), out var v) && v is double d ? d : 0.5;
                var wave = 0.5 + 0.5 * Math.Cos(_pulseTime / PulsePeriod * Math.PI * 2);
                _skeleton.Opacity = low + (1 - low) * wave;
                return true;
            });
            _pulse.Start();
        }
        else
        {
            _pulse?.Stop();
            _pulseTime = 0;
            _skeleton.Opacity = 1;
        }
    }
}
