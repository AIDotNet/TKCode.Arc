using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A dashboard card in which an assistant explains why a metric moved: the metric and its change on top, the chart it
/// talks about with the changed stretch in a band, a short summary, the likely causes ranked by how much of the change
/// they explain, and one suggested next step with a button. Selecting a cause moves the chart's band to that cause's
/// stretch. While <see cref="IsAnalyzing"/> is true the text gives way to a shimmering line and placeholder lines.
/// <code>
/// &lt;arc:AiInsightCard Title="Weekly active users" Value="18,420" Change="-12.4"
///                    Summary="Active users fell 12.4% over the last five days..." /&gt;
/// </code>
/// With no data set the card shows its invented sample (<see cref="AiInsightCardSamples"/>).
/// </summary>
[TemplatePart(ChartPart, typeof(AiInsightCardChart))]
[TemplatePart(CausesPart, typeof(Panel))]
[TemplatePart(HelpfulPart, typeof(Button))]
[TemplatePart(NotHelpfulPart, typeof(Button))]
[TemplatePart(ActionPart, typeof(Button))]
[TemplatePart(ReadoutPart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass, AnalyzingPseudoClass, GoodPseudoClass, BadPseudoClass, HasCausesPseudoClass,
    HasNextStepPseudoClass, HelpfulPseudoClass, NotHelpfulPseudoClass)]
public class AiInsightCard : TemplatedControl
{
    public const string ChartPart = "PART_Chart";
    public const string CausesPart = "PART_Causes";
    public const string HelpfulPart = "PART_Helpful";
    public const string NotHelpfulPart = "PART_NotHelpful";
    public const string ActionPart = "PART_Action";
    public const string ReadoutPart = "PART_Readout";
    public const string AnnouncerPart = "PART_Announcer";

    public const string CompactPseudoClass = ":compact";
    public const string AnalyzingPseudoClass = ":analyzing";
    public const string GoodPseudoClass = ":good";
    public const string BadPseudoClass = ":bad";
    public const string HasCausesPseudoClass = ":has-causes";
    public const string HasNextStepPseudoClass = ":has-next-step";
    public const string HelpfulPseudoClass = ":helpful";
    public const string NotHelpfulPseudoClass = ":not-helpful";

    #region Properties

    /// <summary>The metric's name ("Weekly active users").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiInsightCard, string?>(nameof(Title), AiInsightCardSamples.Title);

    /// <summary>The metric's current value, formatted ("18,420").</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<AiInsightCard, string?>(nameof(Value), AiInsightCardSamples.Value);

    /// <summary>The change in percent (−12.4 for a 12.4% drop); null hides the change pill.</summary>
    public static readonly StyledProperty<double?> ChangeProperty =
        AvaloniaProperty.Register<AiInsightCard, double?>(nameof(Change), AiInsightCardSamples.Change);

    /// <summary>Whether a rise is good news (true: rises are green, drops red); false flips it, for costs or error rates.</summary>
    public static readonly StyledProperty<bool> IsIncreaseGoodProperty =
        AvaloniaProperty.Register<AiInsightCard, bool>(nameof(IsIncreaseGood), true);

    /// <summary>What the change is measured against ("vs. the 14 days before"); null leaves it out.</summary>
    public static readonly StyledProperty<string?> PeriodTextProperty =
        AvaloniaProperty.Register<AiInsightCard, string?>(nameof(PeriodText), AiInsightCardSamples.Period);

    /// <summary>The metric's values, oldest first, drawn as the line; fewer than two hides the chart.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> PointsProperty =
        AvaloniaProperty.Register<AiInsightCard, IReadOnlyList<double>?>(nameof(Points), AiInsightCardSamples.Points());

    /// <summary>One label per point ("Nov 4"), used by the readout and the chart's description; null numbers the points.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PointLabelsProperty =
        AvaloniaProperty.Register<AiInsightCard, IReadOnlyList<string>?>(nameof(PointLabels), AiInsightCardSamples.PointLabels());

    /// <summary>The same metric over the period before, drawn as a dashed line; null leaves it out.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ComparisonPointsProperty =
        AvaloniaProperty.Register<AiInsightCard, IReadOnlyList<double>?>(nameof(ComparisonPoints), AiInsightCardSamples.ComparisonPoints());

    /// <summary>Formats a point's value in the readout; null uses the number with group separators and no decimals.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<AiInsightCard, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>The stretch the insight is about, shown as a band while no cause with its own stretch is selected; null shows none.</summary>
    public static readonly StyledProperty<AiInsightRange?> FocusRangeProperty =
        AvaloniaProperty.Register<AiInsightCard, AiInsightRange?>(nameof(FocusRange), AiInsightCardSamples.Focus());

    /// <summary>What moved, in two or three sentences.</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<AiInsightCard, string?>(nameof(Summary), AiInsightCardSamples.Summary);

    /// <summary>The likely causes, most important first; null or empty hides the section.</summary>
    public static readonly StyledProperty<IReadOnlyList<AiInsightCause>?> CausesProperty =
        AvaloniaProperty.Register<AiInsightCard, IReadOnlyList<AiInsightCause>?>(nameof(Causes), AiInsightCardSamples.Causes());

    /// <summary>The selected cause (two-way); null when none is. It must be one of <see cref="Causes"/>.</summary>
    public static readonly StyledProperty<AiInsightCause?> SelectedCauseProperty =
        AvaloniaProperty.Register<AiInsightCard, AiInsightCause?>(nameof(SelectedCause), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The suggested next step; null or empty hides the suggestion box.</summary>
    public static readonly StyledProperty<string?> NextStepProperty =
        AvaloniaProperty.Register<AiInsightCard, string?>(nameof(NextStep), AiInsightCardSamples.NextStep);

    /// <summary>Run by the suggestion's button, with the <see cref="AiInsightActionEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<AiInsightCard, ICommand?>(nameof(ActionCommand));

    /// <summary>Whether the suggestion box shows its button.</summary>
    public static readonly StyledProperty<bool> IsActionVisibleProperty =
        AvaloniaProperty.Register<AiInsightCard, bool>(nameof(IsActionVisible), true);

    /// <summary>Small print at the bottom (the data used, when it was written); null leaves it out.</summary>
    public static readonly StyledProperty<string?> FootnoteProperty =
        AvaloniaProperty.Register<AiInsightCard, string?>(nameof(Footnote), AiInsightCardSamples.Footnote);

    /// <summary>True while the insight is being written: the text gives way to a shimmering line and placeholders.</summary>
    public static readonly StyledProperty<bool> IsAnalyzingProperty =
        AvaloniaProperty.Register<AiInsightCard, bool>(nameof(IsAnalyzing));

    /// <summary>What the person said about the insight (two-way); pressing the same thumb again takes it back.</summary>
    public static readonly StyledProperty<AiInsightFeedback> FeedbackProperty =
        AvaloniaProperty.Register<AiInsightCard, AiInsightFeedback>(nameof(Feedback), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the thumbs up and down buttons show.</summary>
    public static readonly StyledProperty<bool> IsFeedbackVisibleProperty =
        AvaloniaProperty.Register<AiInsightCard, bool>(nameof(IsFeedbackVisible), true);

    /// <summary>Every word the card shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AiInsightCardLabels> LabelsProperty =
        AvaloniaProperty.Register<AiInsightCard, AiInsightCardLabels>(nameof(Labels), new AiInsightCardLabels());

    /// <summary>The readout over the chart: the picked point, or the last point when none is picked.</summary>
    public static readonly DirectProperty<AiInsightCard, string?> ReadoutProperty =
        AvaloniaProperty.RegisterDirect<AiInsightCard, string?>(nameof(Readout), o => o.Readout);

    /// <summary>True when the card is at most <c>CompactWidth</c> (420) wide.</summary>
    public static readonly DirectProperty<AiInsightCard, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AiInsightCard, bool>(nameof(IsCompact), o => o.IsCompact);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AiInsightCard, double>(nameof(CompactWidth), 420);

    internal static readonly DirectProperty<AiInsightCard, string?> ChangeTextProperty =
        AvaloniaProperty.RegisterDirect<AiInsightCard, string?>(nameof(ChangeText), o => o.ChangeText);

    internal static readonly DirectProperty<AiInsightCard, Geometry?> ChangeGlyphProperty =
        AvaloniaProperty.RegisterDirect<AiInsightCard, Geometry?>(nameof(ChangeGlyph), o => o.ChangeGlyph);

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="ChangeProperty"/>
    public double? Change { get => GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }

    /// <inheritdoc cref="IsIncreaseGoodProperty"/>
    public bool IsIncreaseGood { get => GetValue(IsIncreaseGoodProperty); set => SetValue(IsIncreaseGoodProperty, value); }

    /// <inheritdoc cref="PeriodTextProperty"/>
    public string? PeriodText { get => GetValue(PeriodTextProperty); set => SetValue(PeriodTextProperty, value); }

    /// <inheritdoc cref="PointsProperty"/>
    public IReadOnlyList<double>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }

    /// <inheritdoc cref="PointLabelsProperty"/>
    public IReadOnlyList<string>? PointLabels { get => GetValue(PointLabelsProperty); set => SetValue(PointLabelsProperty, value); }

    /// <inheritdoc cref="ComparisonPointsProperty"/>
    public IReadOnlyList<double>? ComparisonPoints { get => GetValue(ComparisonPointsProperty); set => SetValue(ComparisonPointsProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="FocusRangeProperty"/>
    public AiInsightRange? FocusRange { get => GetValue(FocusRangeProperty); set => SetValue(FocusRangeProperty, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <inheritdoc cref="CausesProperty"/>
    public IReadOnlyList<AiInsightCause>? Causes { get => GetValue(CausesProperty); set => SetValue(CausesProperty, value); }

    /// <inheritdoc cref="SelectedCauseProperty"/>
    public AiInsightCause? SelectedCause { get => GetValue(SelectedCauseProperty); set => SetValue(SelectedCauseProperty, value); }

    /// <inheritdoc cref="NextStepProperty"/>
    public string? NextStep { get => GetValue(NextStepProperty); set => SetValue(NextStepProperty, value); }

    /// <inheritdoc cref="ActionCommandProperty"/>
    public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }

    /// <inheritdoc cref="IsActionVisibleProperty"/>
    public bool IsActionVisible { get => GetValue(IsActionVisibleProperty); set => SetValue(IsActionVisibleProperty, value); }

    /// <inheritdoc cref="FootnoteProperty"/>
    public string? Footnote { get => GetValue(FootnoteProperty); set => SetValue(FootnoteProperty, value); }

    /// <inheritdoc cref="IsAnalyzingProperty"/>
    public bool IsAnalyzing { get => GetValue(IsAnalyzingProperty); set => SetValue(IsAnalyzingProperty, value); }

    /// <inheritdoc cref="FeedbackProperty"/>
    public AiInsightFeedback Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <inheritdoc cref="IsFeedbackVisibleProperty"/>
    public bool IsFeedbackVisible { get => GetValue(IsFeedbackVisibleProperty); set => SetValue(IsFeedbackVisibleProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiInsightCardLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ReadoutProperty"/>
    public string? Readout { get => _readout; private set => SetAndRaise(ReadoutProperty, ref _readout, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal string? ChangeText { get => _changeText; private set => SetAndRaise(ChangeTextProperty, ref _changeText, value); }

    internal Geometry? ChangeGlyph { get => _changeGlyph; private set => SetAndRaise(ChangeGlyphProperty, ref _changeGlyph, value); }

    #endregion

    #region Events

    /// <summary>Raised when the suggestion's button is pressed (before <see cref="ActionCommand"/> runs).</summary>
    public static readonly RoutedEvent<AiInsightActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<AiInsightCard, AiInsightActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Feedback"/> changes.</summary>
    public static readonly RoutedEvent<AiInsightFeedbackEventArgs> FeedbackChangedEvent =
        RoutedEvent.Register<AiInsightCard, AiInsightFeedbackEventArgs>(nameof(FeedbackChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedCause"/> changes.</summary>
    public static readonly RoutedEvent<AiInsightCauseEventArgs> SelectedCauseChangedEvent =
        RoutedEvent.Register<AiInsightCard, AiInsightCauseEventArgs>(nameof(SelectedCauseChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the suggestion's button is pressed (before <see cref="ActionCommand"/> runs).</summary>
    public event EventHandler<AiInsightActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <summary>Raised when <see cref="Feedback"/> changes.</summary>
    public event EventHandler<AiInsightFeedbackEventArgs>? FeedbackChanged
    {
        add => AddHandler(FeedbackChangedEvent, value);
        remove => RemoveHandler(FeedbackChangedEvent, value);
    }

    /// <summary>Raised when <see cref="SelectedCause"/> changes.</summary>
    public event EventHandler<AiInsightCauseEventArgs>? SelectedCauseChanged
    {
        add => AddHandler(SelectedCauseChangedEvent, value);
        remove => RemoveHandler(SelectedCauseChangedEvent, value);
    }

    #endregion

    private AiInsightCardChart? _chart;
    private Panel? _causes;
    private Button? _helpful;
    private Button? _notHelpful;
    private Button? _action;
    private AiAnnouncer? _announcer;
    private TextBlock? _eyebrowText;
    private TextBlock? _summaryHeading;
    private TextBlock? _causesHeading;
    private TextBlock? _nextStepHeading;
    private TextShimmer? _analyzingText;
    private readonly List<AiInsightCardCauseRow> _rows = new();
    private string? _readout;
    private bool _isCompact;
    private string? _changeText;
    private Geometry? _changeGlyph;
    private bool _syncingRows;

    static AiInsightCard()
    {
        FocusableProperty.OverrideDefaultValue<AiInsightCard>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AiInsightCard>(AutomationLandmarkType.Region);
    }

    /// <summary>The cause rows in order; empty before the template is applied.</summary>
    internal IReadOnlyList<AiInsightCardCauseRow> CauseRows => _rows;

    internal AiInsightCardChart? Chart => _chart;

    internal string? Announcement => _announcer?.Announcement;

    #region Public actions

    /// <summary>Presses the suggestion's button: raises <see cref="ActionInvoked"/> and runs <see cref="ActionCommand"/>.</summary>
    public void InvokeAction()
    {
        var args = new AiInsightActionEventArgs(ActionInvokedEvent, NextStep, SelectedCause);
        RaiseEvent(args);
        if (ActionCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    /// <summary>Picks a point of the chart for the readout (−1 goes back to the last point).</summary>
    /// <param name="index">The point's index.</param>
    public void ShowPoint(int index) => _chart?.SetHover(index);

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_chart is not null)
        {
            _chart.HoverChanged -= OnHoverChanged;
        }
        Unhook(_helpful, OnHelpfulClick);
        Unhook(_notHelpful, OnNotHelpfulClick);
        Unhook(_action, OnActionClick);

        _chart = e.NameScope.Find<AiInsightCardChart>(ChartPart);
        _causes = e.NameScope.Find<Panel>(CausesPart);
        _helpful = e.NameScope.Find<Button>(HelpfulPart);
        _notHelpful = e.NameScope.Find<Button>(NotHelpfulPart);
        _action = e.NameScope.Find<Button>(ActionPart);
        _announcer = e.NameScope.Find<AiAnnouncer>(AnnouncerPart);
        _eyebrowText = e.NameScope.Find<TextBlock>("PART_EyebrowText");
        _summaryHeading = e.NameScope.Find<TextBlock>("PART_SummaryHeading");
        _causesHeading = e.NameScope.Find<TextBlock>("PART_CausesHeading");
        _nextStepHeading = e.NameScope.Find<TextBlock>("PART_NextStepHeading");
        _analyzingText = e.NameScope.Find<TextShimmer>("PART_AnalyzingText");

        if (_chart is not null)
        {
            _chart.HoverChanged += OnHoverChanged;
        }
        Hook(_helpful, OnHelpfulClick);
        Hook(_notHelpful, OnNotHelpfulClick);
        Hook(_action, OnActionClick);

        UpdateChange();
        UpdateChartData();
        BuildRows();
        UpdateBand(false);
        UpdateLabels();
        UpdateFeedback();
        UpdateReadout();
        PseudoClasses.Set(AnalyzingPseudoClass, IsAnalyzing);
        PseudoClasses.Set(HasNextStepPseudoClass, !string.IsNullOrWhiteSpace(NextStep));
    }

    private static void Hook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unhook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ChangeProperty || property == IsIncreaseGoodProperty)
        {
            UpdateChange();
        }
        else if (property == PointsProperty || property == ComparisonPointsProperty)
        {
            UpdateChartData();
            UpdateBand(false);
            UpdateReadout();
        }
        else if (property == PointLabelsProperty || property == ValueFormatterProperty)
        {
            UpdateReadout();
            UpdateLabels();
        }
        else if (property == CausesProperty)
        {
            if (SelectedCause is { } selected && Causes?.Contains(selected) != true)
            {
                SetCurrentValue(SelectedCauseProperty, null);
            }
            BuildRows();
        }
        else if (property == SelectedCauseProperty)
        {
            SyncRows();
            UpdateBand(true);
            RaiseEvent(new AiInsightCauseEventArgs(SelectedCauseChangedEvent, SelectedCause));
        }
        else if (property == FocusRangeProperty)
        {
            UpdateBand(true);
        }
        else if (property == NextStepProperty)
        {
            PseudoClasses.Set(HasNextStepPseudoClass, !string.IsNullOrWhiteSpace(NextStep));
        }
        else if (property == IsAnalyzingProperty)
        {
            PseudoClasses.Set(AnalyzingPseudoClass, IsAnalyzing);
            if (_analyzingText is not null)
            {
                _analyzingText.IsActive = IsAnalyzing;
            }
            if (IsAnalyzing)
            {
                _announcer?.Announce(Labels.Analyzing);
            }
            else if (!string.IsNullOrWhiteSpace(Summary))
            {
                _announcer?.Announce(Summary);
            }
        }
        else if (property == FeedbackProperty)
        {
            UpdateFeedback();
            RaiseEvent(new AiInsightFeedbackEventArgs(FeedbackChangedEvent,
                change.GetOldValue<AiInsightFeedback>(), change.GetNewValue<AiInsightFeedback>()));
        }
        else if (property == LabelsProperty)
        {
            UpdateChange();
            BuildRows();
            UpdateLabels();
            UpdateFeedback();
            UpdateReadout();
        }
        else if (property == BoundsProperty || property == CompactWidthProperty)
        {
            IsCompact = Bounds.Width > 0 && Bounds.Width <= CompactWidth;
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
    }

    private void UpdateChange()
    {
        var change = Change;
        if (change is not { } percent || !double.IsFinite(percent))
        {
            ChangeText = null;
            ChangeGlyph = null;
            PseudoClasses.Set(GoodPseudoClass, false);
            PseudoClasses.Set(BadPseudoClass, false);
            return;
        }
        ChangeText = Labels.Change(percent);
        ChangeGlyph = percent < 0 ? ArcIcons.TrendingDown : ArcIcons.TrendingUp;
        var good = percent == 0 || (percent > 0) == IsIncreaseGood;
        PseudoClasses.Set(GoodPseudoClass, good);
        PseudoClasses.Set(BadPseudoClass, !good);
    }

    private void UpdateChartData() => _chart?.SetData(Points, ComparisonPoints);

    private void UpdateBand(bool animate)
    {
        if (_chart is null)
        {
            return;
        }
        var range = SelectedCause?.Range ?? FocusRange;
        _chart.SetBand(range, animate);
    }

    private void BuildRows()
    {
        if (_causes is null)
        {
            return;
        }
        foreach (var row in _rows)
        {
            row.IsCheckedChanged -= OnRowChecked;
            row.KeyDown -= OnRowKeyDown;
        }
        _causes.Children.Clear();
        _rows.Clear();
        var labels = Labels;
        if (Causes is { } causes)
        {
            foreach (var cause in causes)
            {
                var share = labels.Share(cause.Share);
                var row = new AiInsightCardCauseRow(cause)
                {
                    Title = cause.Title,
                    Detail = cause.Detail,
                    Share = cause.Share,
                    ShareText = share,
                    IsChecked = ReferenceEquals(cause, SelectedCause),
                };
                AutomationProperties.SetName(row, labels.CauseName(cause.Title, share));
                if (!string.IsNullOrWhiteSpace(cause.Detail))
                {
                    AutomationProperties.SetHelpText(row, cause.Detail);
                }
                row.IsCheckedChanged += OnRowChecked;
                row.KeyDown += OnRowKeyDown;
                _rows.Add(row);
                _causes.Children.Add(row);
            }
        }
        AutomationProperties.SetName(_causes, labels.CausesList);
        PseudoClasses.Set(HasCausesPseudoClass, _rows.Count > 0);
    }

    private void SyncRows()
    {
        _syncingRows = true;
        try
        {
            foreach (var row in _rows)
            {
                row.IsChecked = ReferenceEquals(row.Cause, SelectedCause);
            }
        }
        finally
        {
            _syncingRows = false;
        }
    }

    private void OnRowChecked(object? sender, RoutedEventArgs e)
    {
        if (_syncingRows || sender is not AiInsightCardCauseRow row)
        {
            return;
        }
        if (row.IsChecked == true)
        {
            SetCurrentValue(SelectedCauseProperty, row.Cause);
        }
        else if (ReferenceEquals(SelectedCause, row.Cause))
        {
            SetCurrentValue(SelectedCauseProperty, null);
        }
    }

    // Up and Down move between the causes like a list; Tab still walks through them.
    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not AiInsightCardCauseRow row || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var index = _rows.IndexOf(row);
        var next = e.Key switch
        {
            Key.Down => Math.Min(_rows.Count - 1, index + 1),
            Key.Up => Math.Max(0, index - 1),
            Key.Home => 0,
            Key.End => _rows.Count - 1,
            _ => -1,
        };
        if (next >= 0)
        {
            _rows[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    private void OnHelpfulClick(object? sender, RoutedEventArgs e) => Answer(AiInsightFeedback.Helpful);

    private void OnNotHelpfulClick(object? sender, RoutedEventArgs e) => Answer(AiInsightFeedback.NotHelpful);

    private void Answer(AiInsightFeedback feedback)
    {
        var next = Feedback == feedback ? AiInsightFeedback.None : feedback;
        SetCurrentValue(FeedbackProperty, next);
        if (next != AiInsightFeedback.None)
        {
            _announcer?.Announce(Labels.FeedbackThanks);
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e) => InvokeAction();

    private void UpdateFeedback()
    {
        PseudoClasses.Set(HelpfulPseudoClass, Feedback == AiInsightFeedback.Helpful);
        PseudoClasses.Set(NotHelpfulPseudoClass, Feedback == AiInsightFeedback.NotHelpful);
        if (_helpful is not null)
        {
            AutomationProperties.SetName(_helpful, Labels.Helpful);
            AutomationProperties.SetItemStatus(_helpful, Feedback == AiInsightFeedback.Helpful ? "selected" : null);
        }
        if (_notHelpful is not null)
        {
            AutomationProperties.SetName(_notHelpful, Labels.NotHelpful);
            AutomationProperties.SetItemStatus(_notHelpful, Feedback == AiInsightFeedback.NotHelpful ? "selected" : null);
        }
    }

    private void UpdateLabels()
    {
        var labels = Labels;
        SetText(_eyebrowText, labels.Eyebrow);
        SetText(_summaryHeading, labels.SummaryHeading);
        SetText(_causesHeading, labels.CausesHeading);
        SetText(_nextStepHeading, labels.NextStepHeading);
        if (_analyzingText is not null)
        {
            _analyzingText.Text = labels.Analyzing;
            _analyzingText.IsActive = IsAnalyzing;
        }
        if (_action is not null)
        {
            _action.Content = labels.Action;
        }
        if (_chart is null)
        {
            return;
        }
        AutomationProperties.SetName(_chart, Title is { Length: > 0 } title ? $"{labels.Chart}: {title}" : labels.Chart);
        var points = Points;
        if (points is { Count: > 1 })
        {
            AutomationProperties.SetHelpText(_chart,
                labels.ChartSummary(LabelOf(0), Format(points[0]), LabelOf(points.Count - 1), Format(points[^1])));
        }
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    private void OnHoverChanged(int index)
    {
        UpdateReadout();
        // Pointer hovering is silent; points reached with the arrow keys are read out.
        if (_chart is { IsKeyboardFocusWithin: true } && index >= 0 && Readout is { } readout)
        {
            AutomationProperties.SetHelpText(_chart, readout);
            _announcer?.Announce(readout);
        }
    }

    private void UpdateReadout()
    {
        var points = Points;
        if (points is not { Count: > 0 })
        {
            Readout = null;
            return;
        }
        var index = _chart is { HoverIndex: >= 0 } chart ? chart.HoverIndex : points.Count - 1;
        index = Math.Min(index, points.Count - 1);
        var label = LabelOf(index);
        var value = Format(points[index]);
        Readout = ComparisonPoints is { } before && index < before.Count
            ? Labels.PointWithComparison(label, value, Format(before[index]))
            : Labels.Point(label, value);
    }

    private string LabelOf(int index)
        => PointLabels is { } labels && index < labels.Count ? labels[index] : (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);

    private string Format(double value)
        => ValueFormatter?.Invoke(value) ?? value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);

    protected override AutomationPeer OnCreateAutomationPeer() => new AiInsightCardPeer(this);

    private sealed class AiInsightCardPeer(AiInsightCard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : ((AiInsightCard)Owner).Title;
    }
}
