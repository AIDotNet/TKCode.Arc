using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A model details card for a model picker, docs or a marketplace that keeps one size in every state (Arc Model card,
/// design reference). The header shows the model's mark, name and provider with a Compare switch. The details view has a
/// two line summary, all seven modality chips (unsupported ones dashed and muted), the context window with a gauge that
/// reads as pages, max output and knowledge cutoff, input, output and cached prices per million tokens, a monthly volume
/// slider that rolls the estimated cost live, and benchmark bars against two rivals where hovering or focusing a row shows
/// the exact score, its rank and the gap to the leader. The footer shows the API model id with Copy model id. Compare
/// crossfades to a side by side table against any rival, with a check and the margin on the better value and a tally of
/// who leads.
/// <para>
/// Details, compare, loading and error are stacked in one grid cell, so switching models, views or benchmarks never moves
/// the card: every price, limit and score rolls in place in tabular digits and every text value crossfades. Without a
/// <see cref="Model"/> the card keeps its frame, says no model is selected and reads every value as not listed.
/// </para>
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(NarrowPseudoClass, ComparePseudoClass, LoadingPseudoClass, ErrorPseudoClass)]
public partial class ModelCard : TemplatedControl
{
    internal const string RootPart = "PART_Root";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string ComparePseudoClass = ":compare";
    internal const string LoadingPseudoClass = ":loading";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>The model the card describes. Null shows the empty card: no model selected, every value not listed.</summary>
    public static readonly StyledProperty<ModelCardModel?> ModelProperty =
        AvaloniaProperty.Register<ModelCard, ModelCardModel?>(nameof(Model));

    /// <summary>
    /// Other models. The first two appear under the model in the benchmark rows, ranks count every rival with a score, and
    /// every rival can be picked in the compare view. The model itself is filtered out, so one shared pool works for every
    /// model. Null or an empty list hides the Compare switch and the rival rows.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<ModelCardModel>?> RivalsProperty =
        AvaloniaProperty.Register<ModelCard, IReadOnlyList<ModelCardModel>?>(nameof(Rivals));

    /// <summary>
    /// Benchmarks in order. Only those the model has a score for are offered in the switcher; with none the benchmark
    /// section hides. Null shows no benchmarks.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<ModelCardBenchmark>?> BenchmarksProperty =
        AvaloniaProperty.Register<ModelCard, IReadOnlyList<ModelCardBenchmark>?>(nameof(Benchmarks));

    /// <summary>The view on screen (two-way): the details or the compare table.</summary>
    public static readonly StyledProperty<ModelCardView> ViewProperty =
        AvaloniaProperty.Register<ModelCard, ModelCardView>(nameof(View), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the rival in the compare view (two-way). Null or an unknown id uses the first rival.</summary>
    public static readonly StyledProperty<string?> CompareWithProperty =
        AvaloniaProperty.Register<ModelCard, string?>(nameof(CompareWith), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the benchmark the bars show (two-way). Null or one the model has no score for uses the first scored benchmark.</summary>
    public static readonly StyledProperty<string?> BenchmarkProperty =
        AvaloniaProperty.Register<ModelCard, string?>(nameof(Benchmark), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The monthly token volume of the cost estimate (two-way). It snaps to the nearest stop: 1M, 5M, 10M, 25M, 50M, 100M,
    /// 250M, 500M or 1B.
    /// </summary>
    public static readonly StyledProperty<long> MonthlyTokensProperty =
        AvaloniaProperty.Register<ModelCard, long>(nameof(MonthlyTokens), 100_000_000, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => ModelCardFormat.VolumeStops[ModelCardFormat.SnapIndex(value)]);

    /// <summary>Share of the monthly volume billed at the input price; the rest is billed at the output price.</summary>
    public static readonly StyledProperty<double> InputShareProperty =
        AvaloniaProperty.Register<ModelCard, double>(nameof(InputShare), 0.75);

    /// <summary>The ISO 4217 currency of the prices ("USD" writes "$").</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<ModelCard, string>(nameof(Currency), "USD");

    /// <summary>Labels prices and scores as samples in the benchmark caption and the compare footnote. Turn it on while your figures are placeholders.</summary>
    public static readonly StyledProperty<bool> IsSampleProperty =
        AvaloniaProperty.Register<ModelCard, bool>(nameof(IsSample));

    /// <summary>
    /// The footnote under the compare table: say where the prices and scores come from and when they were checked. Null
    /// shows the sample footnote while <see cref="IsSample"/> is on, and nothing otherwise.
    /// </summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<ModelCard, string?>(nameof(Note));

    /// <summary>Shows a skeleton of the card and disables Compare; the status region says the details are loading.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ModelCard, bool>(nameof(IsLoading));

    /// <summary>Replaces the body with this error message (announced as an alert). The header still shows the model.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<ModelCard, string?>(nameof(ErrorText));

    /// <summary>Adds a Retry button to the error state; it raises <see cref="RetryRequested"/>.</summary>
    public static readonly StyledProperty<bool> CanRetryProperty =
        AvaloniaProperty.Register<ModelCard, bool>(nameof(CanRetry));

    /// <summary>Adds a primary Use model button to the footer, for model pickers; it raises <see cref="SelectRequested"/>.</summary>
    public static readonly StyledProperty<bool> CanSelectProperty =
        AvaloniaProperty.Register<ModelCard, bool>(nameof(CanSelect));

    /// <summary>Shows the select button as Selected, with a check.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ModelCard, bool>(nameof(IsSelected));

    /// <summary>The select button's label; null uses <see cref="ModelCardLabels.UseModel"/>.</summary>
    public static readonly StyledProperty<string?> SelectLabelProperty =
        AvaloniaProperty.Register<ModelCard, string?>(nameof(SelectLabel));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<ModelCardLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelCard, ModelCardLabels>(nameof(Labels), ModelCardLabels.Default);

    /// <summary>Raised when the person flips the Compare switch. Bubbles.</summary>
    public static readonly RoutedEvent<ModelCardViewChangedEventArgs> ViewChangedEvent =
        RoutedEvent.Register<ModelCard, ModelCardViewChangedEventArgs>(nameof(ViewChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person picks a rival from the compare menu. Bubbles.</summary>
    public static readonly RoutedEvent<ModelCardIdEventArgs> CompareWithChangedEvent =
        RoutedEvent.Register<ModelCard, ModelCardIdEventArgs>(nameof(CompareWithChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person picks a benchmark in the switcher. Bubbles.</summary>
    public static readonly RoutedEvent<ModelCardIdEventArgs> BenchmarkChangedEvent =
        RoutedEvent.Register<ModelCard, ModelCardIdEventArgs>(nameof(BenchmarkChanged), RoutingStrategies.Bubble);

    /// <summary>Raised with the new volume as the person moves the volume slider. Bubbles.</summary>
    public static readonly RoutedEvent<ModelCardMonthlyTokensEventArgs> MonthlyTokensChangedEvent =
        RoutedEvent.Register<ModelCard, ModelCardMonthlyTokensEventArgs>(nameof(MonthlyTokensChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after Copy model id has written the id to the clipboard. Bubbles.</summary>
    public static readonly RoutedEvent<ModelCardIdEventArgs> IdCopiedEvent =
        RoutedEvent.Register<ModelCard, ModelCardIdEventArgs>(nameof(IdCopied), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Use model. Bubbles.</summary>
    public static readonly RoutedEvent<ModelCardModelEventArgs> SelectRequestedEvent =
        RoutedEvent.Register<ModelCard, ModelCardModelEventArgs>(nameof(SelectRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Retry in the error state. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ModelCard, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ModelProperty"/>
    public ModelCardModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="RivalsProperty"/>
    public IReadOnlyList<ModelCardModel>? Rivals { get => GetValue(RivalsProperty); set => SetValue(RivalsProperty, value); }

    /// <inheritdoc cref="BenchmarksProperty"/>
    public IReadOnlyList<ModelCardBenchmark>? Benchmarks { get => GetValue(BenchmarksProperty); set => SetValue(BenchmarksProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public ModelCardView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <inheritdoc cref="CompareWithProperty"/>
    public string? CompareWith { get => GetValue(CompareWithProperty); set => SetValue(CompareWithProperty, value); }

    /// <inheritdoc cref="BenchmarkProperty"/>
    public string? Benchmark { get => GetValue(BenchmarkProperty); set => SetValue(BenchmarkProperty, value); }

    /// <inheritdoc cref="MonthlyTokensProperty"/>
    public long MonthlyTokens { get => GetValue(MonthlyTokensProperty); set => SetValue(MonthlyTokensProperty, value); }

    /// <inheritdoc cref="InputShareProperty"/>
    public double InputShare { get => GetValue(InputShareProperty); set => SetValue(InputShareProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="IsSampleProperty"/>
    public bool IsSample { get => GetValue(IsSampleProperty); set => SetValue(IsSampleProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="CanRetryProperty"/>
    public bool CanRetry { get => GetValue(CanRetryProperty); set => SetValue(CanRetryProperty, value); }

    /// <inheritdoc cref="CanSelectProperty"/>
    public bool CanSelect { get => GetValue(CanSelectProperty); set => SetValue(CanSelectProperty, value); }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <inheritdoc cref="SelectLabelProperty"/>
    public string? SelectLabel { get => GetValue(SelectLabelProperty); set => SetValue(SelectLabelProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelCardLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ViewChangedEvent"/>
    public event EventHandler<ModelCardViewChangedEventArgs>? ViewChanged
    {
        add => AddHandler(ViewChangedEvent, value);
        remove => RemoveHandler(ViewChangedEvent, value);
    }

    /// <inheritdoc cref="CompareWithChangedEvent"/>
    public event EventHandler<ModelCardIdEventArgs>? CompareWithChanged
    {
        add => AddHandler(CompareWithChangedEvent, value);
        remove => RemoveHandler(CompareWithChangedEvent, value);
    }

    /// <inheritdoc cref="BenchmarkChangedEvent"/>
    public event EventHandler<ModelCardIdEventArgs>? BenchmarkChanged
    {
        add => AddHandler(BenchmarkChangedEvent, value);
        remove => RemoveHandler(BenchmarkChangedEvent, value);
    }

    /// <inheritdoc cref="MonthlyTokensChangedEvent"/>
    public event EventHandler<ModelCardMonthlyTokensEventArgs>? MonthlyTokensChanged
    {
        add => AddHandler(MonthlyTokensChangedEvent, value);
        remove => RemoveHandler(MonthlyTokensChangedEvent, value);
    }

    /// <inheritdoc cref="IdCopiedEvent"/>
    public event EventHandler<ModelCardIdEventArgs>? IdCopied
    {
        add => AddHandler(IdCopiedEvent, value);
        remove => RemoveHandler(IdCopiedEvent, value);
    }

    /// <inheritdoc cref="SelectRequestedEvent"/>
    public event EventHandler<ModelCardModelEventArgs>? SelectRequested
    {
        add => AddHandler(SelectRequestedEvent, value);
        remove => RemoveHandler(SelectRequestedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>The empty model the card shows without a <see cref="Model"/>.</summary>
    private static readonly ModelCardModel s_noModel = new();

    /// <summary>The model on screen: <see cref="Model"/>, or an empty one.</summary>
    internal ModelCardModel ShownModel => Model ?? s_noModel;

    /// <summary>The rivals on screen: <see cref="Rivals"/> without the model itself.</summary>
    internal IReadOnlyList<ModelCardModel> ShownRivals
    {
        get
        {
            var own = ShownModel.Id;
            var list = new List<ModelCardModel>();
            foreach (var rival in Rivals ?? [])
            {
                if (rival is not null && rival.Id != own)
                {
                    list.Add(rival);
                }
            }
            return list;
        }
    }

    /// <summary>All benchmarks: <see cref="Benchmarks"/>, or none.</summary>
    internal IReadOnlyList<ModelCardBenchmark> AllBenchmarks => Benchmarks ?? [];

    /// <summary>The benchmarks the switcher offers: those the model has a score for.</summary>
    internal IReadOnlyList<ModelCardBenchmark> OfferedBenchmarks =>
        AllBenchmarks.Where(b => ShownModel.ScoreOf(b.Id) is not null).ToList();

    /// <summary>The benchmark the bars show, or null when the model has no scores.</summary>
    internal ModelCardBenchmark? CurrentBenchmark
    {
        get
        {
            var offered = OfferedBenchmarks;
            return offered.FirstOrDefault(b => b.Id == Benchmark) ?? offered.FirstOrDefault();
        }
    }

    /// <summary>The rival of the compare view, or null when there are none.</summary>
    internal ModelCardModel? CurrentRival
    {
        get
        {
            var rivals = ShownRivals;
            return rivals.FirstOrDefault(r => r.Id == CompareWith) ?? rivals.FirstOrDefault();
        }
    }

    /// <summary>Which panel is on screen.</summary>
    internal ModelCardPanelKind ActivePanel =>
        IsLoading ? ModelCardPanelKind.Loading
        : !string.IsNullOrEmpty(ErrorText) ? ModelCardPanelKind.Error
        : View == ModelCardView.Compare && CurrentRival is not null ? ModelCardPanelKind.Compare
        : ModelCardPanelKind.Details;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        SpecialFocusModality.Watch();
        Teardown();
        var root = e.NameScope.Find<Panel>(RootPart);
        if (root is not null)
        {
            Build(root);
            ApplyBreakpoints(Bounds.Width, force: true);
            Refresh(animate: false);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewProperty || change.Property == IsLoadingProperty || change.Property == ErrorTextProperty)
        {
            PseudoClasses.Set(ComparePseudoClass, View == ModelCardView.Compare);
            PseudoClasses.Set(LoadingPseudoClass, IsLoading);
            PseudoClasses.Set(ErrorPseudoClass, !IsLoading && !string.IsNullOrEmpty(ErrorText));
            Refresh(animate: true);
            Announce(change.Property);
        }
        else if (change.Property == CompareWithProperty || change.Property == BenchmarkProperty)
        {
            Refresh(animate: true);
            Announce(change.Property);
        }
        else if (change.Property == ModelProperty || change.Property == RivalsProperty || change.Property == BenchmarksProperty
                 || change.Property == MonthlyTokensProperty || change.Property == InputShareProperty
                 || change.Property == CurrencyProperty || change.Property == IsSampleProperty || change.Property == NoteProperty
                 || change.Property == CanRetryProperty || change.Property == CanSelectProperty
                 || change.Property == IsSelectedProperty || change.Property == SelectLabelProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == LabelsProperty)
        {
            RefreshWords();
            Refresh(animate: false);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyBreakpoints(e.NewSize.Width, force: false);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchViewport();
        UpdatePulse();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        _pulse?.Stop();
    }
}

/// <summary>The four panels stacked in the body of a <see cref="ModelCard"/>.</summary>
internal enum ModelCardPanelKind
{
    Details,
    Compare,
    Loading,
    Error
}
