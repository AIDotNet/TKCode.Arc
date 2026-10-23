using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The review area under a model picker: the selected model's description, four facts (context window, input and
/// output price per million tokens, effort) and a short-lived confirmation line after a change. Set
/// <see cref="Model"/> and <see cref="Effort"/>; call <see cref="ShowNotice"/> when the person changes either.
/// <code>
/// &lt;arc:ModelSummary Model="{Binding SelectedModel}" Effort="{Binding Effort}"
///                   Note="Prices in USD per 1M tokens · demo values" /&gt;
/// </code>
/// On a change the description crossfades (old out in 120ms, new in over 160ms) and only the facts whose text changed
/// crossfade (100 / 160ms); the description slot simply grows or shrinks when the line count changes. At 400px or
/// narrower (<see cref="IsCompact"/>) the facts fold into two rows of two. The change line fades in, stays for
/// <see cref="NoticeDuration"/> and fades out; a new one replaces it with a crossfade. Its slot always keeps its
/// height, so nothing below moves. Reduced motion crossfades in 120ms; with motion off everything changes at once.
/// The summary is a named region for screen readers and each fact reads as one phrase ("Context: 800K tokens"); it
/// does not announce changes itself: the owner does that in its own live region.
/// </summary>
[TemplatePart(DescriptionPart, typeof(ArcContentSwap))]
[TemplatePart(FactsPart, typeof(Panel))]
[TemplatePart(ContextPart, typeof(Control))]
[TemplatePart(InputPart, typeof(Control))]
[TemplatePart(OutputPart, typeof(Control))]
[TemplatePart(EffortPart, typeof(Control))]
[TemplatePart(NoticePart, typeof(ArcContentSwap))]
[PseudoClasses(CompactPseudoClass, HasNoticePseudoClass)]
public class ModelSummary : TemplatedControl
{
    public const string DescriptionPart = "PART_Description";
    public const string FactsPart = "PART_Facts";
    public const string ContextPart = "PART_Context";
    public const string InputPart = "PART_Input";
    public const string OutputPart = "PART_Output";
    public const string EffortPart = "PART_Effort";
    public const string NoticePart = "PART_Notice";

    public const string CompactPseudoClass = ":compact";
    public const string HasNoticePseudoClass = ":has-notice";

    #region Properties

    /// <summary>The model to describe; null shows dashes in the facts and no description.</summary>
    public static readonly StyledProperty<AiModel?> ModelProperty =
        AvaloniaProperty.Register<ModelSummary, AiModel?>(nameof(Model));

    /// <summary>The effort the model runs at; shown in the Effort fact (a model without effort levels shows <see cref="ModelSummaryLabels.NotAdjustable"/>).</summary>
    public static readonly StyledProperty<ModelEffort?> EffortProperty =
        AvaloniaProperty.Register<ModelSummary, ModelEffort?>(nameof(Effort));

    /// <summary>Small print under the facts, such as "Prices in USD per 1M tokens · demo values"; null or empty shows none.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<ModelSummary, string?>(nameof(Note));

    /// <summary>How long a confirmation from <see cref="ShowNotice"/> stays before it fades out (2.6s). Zero or less shows none.</summary>
    public static readonly StyledProperty<TimeSpan> NoticeDurationProperty =
        AvaloniaProperty.Register<ModelSummary, TimeSpan>(nameof(NoticeDuration), TimeSpan.FromSeconds(2.6));

    /// <summary>The currency symbol in front of the prices ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ModelSummary, string>(nameof(CurrencySymbol), "$");

    /// <summary>Every word the summary shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<ModelSummaryLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelSummary, ModelSummaryLabels>(nameof(Labels), new ModelSummaryLabels());

    /// <summary>True when the summary is at most 400px wide and the facts sit in two rows of two.</summary>
    public static readonly DirectProperty<ModelSummary, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<ModelSummary, bool>(nameof(IsCompact), o => o.IsCompact);

    /// <summary>The confirmation text currently shown; null when there is none, from the moment it starts to fade out.</summary>
    public static readonly DirectProperty<ModelSummary, string?> NoticeProperty =
        AvaloniaProperty.RegisterDirect<ModelSummary, string?>(nameof(Notice), o => o.Notice);

    /// <summary>Width at or below which the facts fold into two rows (token, 400).</summary>
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ModelSummary, double>(nameof(CompactWidth), 400);

    /// <inheritdoc cref="ModelProperty"/>
    public AiModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="EffortProperty"/>
    public ModelEffort? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="NoticeDurationProperty"/>
    public TimeSpan NoticeDuration { get => GetValue(NoticeDurationProperty); set => SetValue(NoticeDurationProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelSummaryLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => _isCompact;
        private set => SetAndRaise(IsCompactProperty, ref _isCompact, value);
    }

    /// <inheritdoc cref="NoticeProperty"/>
    public string? Notice
    {
        get => _notice;
        private set => SetAndRaise(NoticeProperty, ref _notice, value);
    }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    private static readonly IDataTemplate DescriptionTemplate =
        new FuncDataTemplate<string>(static (text, _) => new ModelSummaryText { Text = text });

    private static readonly IDataTemplate NoticeTemplate =
        new FuncDataTemplate<ModelSummaryNoticeContent>(static (content, _) => new ModelSummaryNoticeLine { Text = content?.Text });

    private ArcContentSwap? _description;
    private ModelSummaryFacts? _facts;
    private ModelSummaryFact? _context;
    private ModelSummaryFact? _input;
    private ModelSummaryFact? _output;
    private ModelSummaryFact? _effort;
    private ArcContentSwap? _noticeSwap;
    private AiModel? _observedModel;
    private IDisposable? _noticeTimer;
    private int _noticeGeneration;
    private bool _isCompact;
    private string? _notice;
    private bool _attached;

    static ModelSummary()
    {
        FocusableProperty.OverrideDefaultValue<ModelSummary>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<ModelSummary>(AutomationLandmarkType.Region);
    }

    /// <summary>
    /// Starts the timer that ends a confirmation: (duration, callback) → a handle that cancels it. Tests replace it
    /// with a manual clock; the default is a one-shot <see cref="DispatcherTimer"/>.
    /// </summary>
    internal Func<TimeSpan, Action, IDisposable> NoticeTimerFactory { get; set; } =
        static (duration, elapsed) => DispatcherTimer.RunOnce(elapsed, duration);

    /// <summary>True while a confirmation timer is running.</summary>
    internal bool IsNoticeTimerRunning => _noticeTimer is not null;

    internal ArcContentSwap? DescriptionSwap => _description;

    internal ArcContentSwap? NoticeSwap => _noticeSwap;

    internal ModelSummaryFacts? FactsPanel => _facts;

    /// <summary>The four fact cells in reading order (Context, Input, Output, Effort); empty before the template is applied.</summary>
    internal IReadOnlyList<ModelSummaryFact> Facts =>
        _context is null || _input is null || _output is null || _effort is null ? [] : [_context, _input, _output, _effort];

    #region Public actions

    /// <summary>
    /// Shows <paramref name="text"/> with a green check under the facts for <see cref="NoticeDuration"/>, then fades it
    /// out. A confirmation already showing is replaced with a crossfade. Null or empty text clears the line.
    /// </summary>
    /// <param name="text">The confirmation, such as "Aurora Prime 3 → Cobalt Atlas 2".</param>
    public void ShowNotice(string text)
    {
        StopNoticeTimer();
        if (string.IsNullOrEmpty(text) || NoticeDuration <= TimeSpan.Zero)
        {
            ClearNoticeCore();
            return;
        }
        Notice = text;
        PseudoClasses.Set(HasNoticePseudoClass, true);
        if (_noticeSwap is not null)
        {
            _noticeSwap.Content = new ModelSummaryNoticeContent(text);
        }
        if (_attached)
        {
            StartNoticeTimer();
        }
    }

    /// <summary>Fades out the confirmation now (if one is shown).</summary>
    public void ClearNotice()
    {
        StopNoticeTimer();
        ClearNoticeCore();
    }

    #endregion

    #region Notice

    private void StartNoticeTimer()
    {
        var generation = ++_noticeGeneration;
        var duration = NoticeDuration;
        // DispatcherTimer cannot wait longer than int.MaxValue ms (about 24 days): such a confirmation simply stays.
        if (duration.TotalMilliseconds >= int.MaxValue)
        {
            return;
        }
        _noticeTimer = NoticeTimerFactory(duration, () => ExpireNotice(generation));
    }

    private void StopNoticeTimer()
    {
        _noticeGeneration++;
        _noticeTimer?.Dispose();
        _noticeTimer = null;
    }

    private void ExpireNotice(int generation)
    {
        if (generation != _noticeGeneration)
        {
            return;
        }
        _noticeTimer?.Dispose();
        _noticeTimer = null;
        ClearNoticeCore();
    }

    private void ClearNoticeCore()
    {
        Notice = null;
        PseudoClasses.Set(HasNoticePseudoClass, false);
        if (_noticeSwap is not null)
        {
            _noticeSwap.Content = null;
        }
    }

    #endregion

    #region Template and content

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _description = e.NameScope.Find<ArcContentSwap>(DescriptionPart);
        _facts = e.NameScope.Find<ModelSummaryFacts>(FactsPart);
        _context = e.NameScope.Find<ModelSummaryFact>(ContextPart);
        _input = e.NameScope.Find<ModelSummaryFact>(InputPart);
        _output = e.NameScope.Find<ModelSummaryFact>(OutputPart);
        _effort = e.NameScope.Find<ModelSummaryFact>(EffortPart);
        _noticeSwap = e.NameScope.Find<ArcContentSwap>(NoticePart);
        if (_description is not null)
        {
            _description.ContentTemplate = DescriptionTemplate;
        }
        if (_facts is not null)
        {
            _facts.IsCompact = IsCompact;
        }
        if (_noticeSwap is not null)
        {
            _noticeSwap.ContentTemplate = NoticeTemplate;
            _noticeSwap.Content = _notice is null ? null : new ModelSummaryNoticeContent(_notice);
        }
        Refresh();
    }

    /// <summary>
    /// Shows the current model, effort and labels. A part whose text is unchanged keeps its content, so only the
    /// description and the facts that really changed crossfade.
    /// </summary>
    private void Refresh()
    {
        var labels = Labels;
        var model = Model;
        if (_description is not null)
        {
            var description = string.IsNullOrWhiteSpace(model?.Description) ? null : model!.Description;
            if (description is null)
            {
                // A model without a description folds the slot away at once.
                _description.IsVisible = false;
                _description.Content = null;
            }
            else
            {
                _description.IsVisible = true;
                _description.Content = description;
            }
        }
        var context = model?.ContextTokens is { } tokens ? labels.ContextValue(AiModelFormat.Tokens(tokens)) : null;
        var input = model?.InputPrice is { } inputPrice ? AiModelFormat.Price(inputPrice, CurrencySymbol) : null;
        var output = model?.OutputPrice is { } outputPrice ? AiModelFormat.Price(outputPrice, CurrencySymbol) : null;
        string? effort = null;
        if (model is not null)
        {
            if (!model.SupportsEffort)
            {
                effort = labels.NotAdjustable;
            }
            else if (Effort is { } level)
            {
                effort = labels.EffortName(level);
            }
        }
        SetFact(_context, labels.Context, context, labels);
        SetFact(_input, labels.Input, input, labels);
        SetFact(_output, labels.Output, output, labels);
        SetFact(_effort, labels.Effort, effort, labels);
    }

    private static void SetFact(ModelSummaryFact? fact, string label, string? value, ModelSummaryLabels labels)
    {
        if (fact is null)
        {
            return;
        }
        fact.Label = label;
        fact.Value = value ?? labels.Missing;
        AutomationProperties.SetName(fact, labels.Fact(label, value ?? labels.MissingSpoken));
    }

    /// <summary>The facts fold into two rows once the summary is laid out at the compact width or narrower.</summary>
    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width <= CompactWidth + 0.01;
        if (compact == _isCompact)
        {
            return;
        }
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_facts is not null)
        {
            _facts.IsCompact = compact;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateCompact(e.NewSize.Width);
        }
    }

    #endregion

    #region Data and lifetime

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty)
        {
            Observe();
            Refresh();
        }
        else if (change.Property == EffortProperty || change.Property == CurrencySymbolProperty || change.Property == LabelsProperty)
        {
            Refresh();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
        else if (change.Property == NoticeDurationProperty && change.GetNewValue<TimeSpan>() <= TimeSpan.Zero)
        {
            ClearNotice();
        }
    }

    /// <summary>Follows the model's own properties (a new price or description) while the summary is in a window.</summary>
    private void Observe()
    {
        var target = _attached ? Model : null;
        if (ReferenceEquals(target, _observedModel))
        {
            return;
        }
        if (_observedModel is not null)
        {
            _observedModel.PropertyChanged -= OnModelPropertyChanged;
        }
        _observedModel = target;
        if (target is not null)
        {
            target.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        var property = e.Property;
        if (property == AiModel.DescriptionProperty || property == AiModel.ContextTokensProperty ||
            property == AiModel.InputPriceProperty || property == AiModel.OutputPriceProperty ||
            property == AiModel.EffortLevelsProperty)
        {
            Refresh();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Observe();
        Refresh();
        // A confirmation shown while the summary was out of the window starts its time now.
        if (_notice is not null && _noticeTimer is null)
        {
            StartNoticeTimer();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Observe();
        // The timer stops with the summary; a confirmation would be stale when it comes back, so it goes too.
        ClearNotice();
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ModelSummaryAutomationPeer(this);

    /// <summary>A named region ("Selected model") holding the description, the facts and the change line.</summary>
    private sealed class ModelSummaryAutomationPeer(ModelSummary owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((ModelSummary)Owner).Labels.Region;
    }
}
