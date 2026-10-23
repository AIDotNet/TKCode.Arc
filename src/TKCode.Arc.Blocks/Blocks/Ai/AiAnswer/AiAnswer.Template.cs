using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

[TemplatePart(TopBarPart, typeof(Border))]
[TemplatePart(NewThreadPart, typeof(AiAnswerActionButton))]
[TemplatePart(TitlePart, typeof(ArcContentSwap))]
[TemplatePart(SharePart, typeof(AiAnswerPillButton))]
[TemplatePart(ShareLabelPart, typeof(ArcContentSwap))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(ColumnPart, typeof(Border))]
[TemplatePart(ArticlePart, typeof(Control))]
[TemplatePart(QuestionPart, typeof(ArcContentSwap))]
[TemplatePart(StepsPart, typeof(Control))]
[TemplatePart(ReasoningPart, typeof(AiReasoningDisclosure))]
[TemplatePart(SourcesPart, typeof(AiSourcesRow))]
[TemplatePart(StagePart, typeof(AiAnswerStage))]
[TemplatePart(DocumentPart, typeof(AiAnswerDocument))]
[TemplatePart(NotePart, typeof(AiStatusNote))]
[TemplatePart(FooterPart, typeof(Control))]
[TemplatePart(ActionsPart, typeof(AiAnswerActions))]
[TemplatePart(ModelLinePart, typeof(AiModelLine))]
[TemplatePart(RelatedPart, typeof(AiRelatedQuestions))]
[TemplatePart(EmptyPart, typeof(Control))]
[TemplatePart(EmptyTitlePart, typeof(TextBlock))]
[TemplatePart(EmptyDescriptionPart, typeof(TextBlock))]
[TemplatePart(SuggestionsPart, typeof(AiRelatedQuestions))]
[TemplatePart(DockPart, typeof(Border))]
[TemplatePart(ComposerPart, typeof(AiComposer))]
[TemplatePart(StatusRegionPart, typeof(AiAnnouncer))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(EmptyPseudoClass, BusyPseudoClass)]
public partial class AiAnswer
{
    public const string TopBarPart = "PART_TopBar";
    public const string NewThreadPart = "PART_NewThread";
    public const string TitlePart = "PART_Title";
    public const string SharePart = "PART_Share";
    public const string ShareLabelPart = "PART_ShareLabel";
    public const string ScrollerPart = "PART_Scroller";
    public const string ColumnPart = "PART_Column";
    public const string ArticlePart = "PART_Article";
    public const string QuestionPart = "PART_Question";
    public const string StepsPart = "PART_Steps";
    public const string ReasoningPart = "PART_Reasoning";
    public const string SourcesPart = "PART_Sources";
    public const string StagePart = "PART_Stage";
    public const string DocumentPart = "PART_Document";
    public const string NotePart = "PART_Note";
    public const string FooterPart = "PART_Footer";
    public const string ActionsPart = "PART_Actions";
    public const string ModelLinePart = "PART_ModelLine";
    public const string RelatedPart = "PART_Related";
    public const string EmptyPart = "PART_Empty";
    public const string EmptyTitlePart = "PART_EmptyTitle";
    public const string EmptyDescriptionPart = "PART_EmptyDescription";
    public const string SuggestionsPart = "PART_Suggestions";
    public const string DockPart = "PART_Dock";
    public const string ComposerPart = "PART_Composer";
    public const string StatusRegionPart = "PART_Status";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the empty page shows (no question).</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while <see cref="IsBusy"/>.</summary>
    public const string BusyPseudoClass = ":busy";

    #region Token-driven internals

    internal static readonly StyledProperty<double> ColumnMaxWidthProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(ColumnMaxWidth), 768);

    internal static readonly StyledProperty<double> SideRatioProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(SideRatio), 0.05);

    internal static readonly StyledProperty<double> SideMinProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(SideMin), 16);

    internal static readonly StyledProperty<double> SideMaxProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(SideMax), 32);

    internal static readonly StyledProperty<double> TopRatioProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(TopRatio), 0.06);

    internal static readonly StyledProperty<double> TopMinProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(TopMin), 28);

    internal static readonly StyledProperty<double> ColumnBottomPaddingProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(ColumnBottomPadding), 48);

    internal static readonly StyledProperty<double> EmptyTopRatioProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(EmptyTopRatio), 0.08);

    internal static readonly StyledProperty<double> QuestionFontBaseProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(QuestionFontBase), 17.6);

    internal static readonly StyledProperty<double> QuestionFontRatioProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(QuestionFontRatio), 0.016);

    internal static readonly StyledProperty<double> QuestionFontMinProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(QuestionFontMin), 22);

    internal static readonly StyledProperty<double> QuestionFontMaxProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(QuestionFontMax), 28);

    internal static readonly StyledProperty<double> QuestionLeadingProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(QuestionLeading), 1.1);

    internal static readonly StyledProperty<double> QuestionTrackingProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(QuestionTracking), -0.02);

    internal static readonly StyledProperty<double> DockTopPaddingProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(DockTopPadding), 6);

    internal static readonly StyledProperty<double> DockBottomPaddingProperty =
        AvaloniaProperty.Register<AiAnswer, double>(nameof(DockBottomPadding), 14);

    internal static readonly StyledProperty<Thickness> ComposerPaddingProperty =
        AvaloniaProperty.Register<AiAnswer, Thickness>(nameof(ComposerPadding), new Thickness(14, 7, 7, 7));

    internal static readonly StyledProperty<CornerRadius> ComposerCornerRadiusProperty =
        AvaloniaProperty.Register<AiAnswer, CornerRadius>(nameof(ComposerCornerRadius), new CornerRadius(26));

    internal static readonly StyledProperty<TimeSpan> CopiedDurationProperty =
        AvaloniaProperty.Register<AiAnswer, TimeSpan>(nameof(CopiedDuration), TimeSpan.FromSeconds(1.6));

    /// <summary>The subtle border (theme: BorderSubtle): the hairline of light themes.</summary>
    internal static readonly StyledProperty<IBrush?> SubtleBorderBrushProperty =
        AvaloniaProperty.Register<AiAnswer, IBrush?>(nameof(SubtleBorderBrush));

    /// <summary>The hairline under the top bar: BorderSubtle in light themes, Border in dark ones (Arc maps it to both).</summary>
    internal static readonly StyledProperty<IBrush?> HairlineBrushProperty =
        AvaloniaProperty.Register<AiAnswer, IBrush?>(nameof(HairlineBrush));

    internal double ColumnMaxWidth { get => GetValue(ColumnMaxWidthProperty); set => SetValue(ColumnMaxWidthProperty, value); }
    internal double SideRatio { get => GetValue(SideRatioProperty); set => SetValue(SideRatioProperty, value); }
    internal double SideMin { get => GetValue(SideMinProperty); set => SetValue(SideMinProperty, value); }
    internal double SideMax { get => GetValue(SideMaxProperty); set => SetValue(SideMaxProperty, value); }
    internal double TopRatio { get => GetValue(TopRatioProperty); set => SetValue(TopRatioProperty, value); }
    internal double TopMin { get => GetValue(TopMinProperty); set => SetValue(TopMinProperty, value); }
    internal double ColumnBottomPadding { get => GetValue(ColumnBottomPaddingProperty); set => SetValue(ColumnBottomPaddingProperty, value); }
    internal double EmptyTopRatio { get => GetValue(EmptyTopRatioProperty); set => SetValue(EmptyTopRatioProperty, value); }
    internal double QuestionFontBase { get => GetValue(QuestionFontBaseProperty); set => SetValue(QuestionFontBaseProperty, value); }
    internal double QuestionFontRatio { get => GetValue(QuestionFontRatioProperty); set => SetValue(QuestionFontRatioProperty, value); }
    internal double QuestionFontMin { get => GetValue(QuestionFontMinProperty); set => SetValue(QuestionFontMinProperty, value); }
    internal double QuestionFontMax { get => GetValue(QuestionFontMaxProperty); set => SetValue(QuestionFontMaxProperty, value); }
    internal double QuestionLeading { get => GetValue(QuestionLeadingProperty); set => SetValue(QuestionLeadingProperty, value); }
    internal double QuestionTracking { get => GetValue(QuestionTrackingProperty); set => SetValue(QuestionTrackingProperty, value); }
    internal double DockTopPadding { get => GetValue(DockTopPaddingProperty); set => SetValue(DockTopPaddingProperty, value); }
    internal double DockBottomPadding { get => GetValue(DockBottomPaddingProperty); set => SetValue(DockBottomPaddingProperty, value); }
    internal Thickness ComposerPadding { get => GetValue(ComposerPaddingProperty); set => SetValue(ComposerPaddingProperty, value); }
    internal CornerRadius ComposerCornerRadius { get => GetValue(ComposerCornerRadiusProperty); set => SetValue(ComposerCornerRadiusProperty, value); }
    internal TimeSpan CopiedDuration { get => GetValue(CopiedDurationProperty); set => SetValue(CopiedDurationProperty, value); }
    internal IBrush? SubtleBorderBrush { get => GetValue(SubtleBorderBrushProperty); set => SetValue(SubtleBorderBrushProperty, value); }
    internal IBrush? HairlineBrush { get => GetValue(HairlineBrushProperty); set => SetValue(HairlineBrushProperty, value); }

    #endregion

    #region Seams (tests)

    /// <summary>The clock of the measured thinking and answer times (tests give their own).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>Writes copied text to the clipboard (tests replace it).</summary>
    internal Func<string, Task> CopyTextAsync { get; set; }

    /// <summary>Opens a source nobody handled (tests replace it); the default uses the top level's launcher.</summary>
    internal Action<Uri> OpenUri { get; set; }

    #endregion

    private Border? _topBar;
    private AiAnswerActionButton? _newThread;
    private ArcContentSwap? _title;
    private AiAnswerPillButton? _share;
    private ArcContentSwap? _shareLabel;
    private TextBlock? _shareWidthIdle;
    private TextBlock? _shareWidthCopied;
    private TextBlock? _shareWidthFailed;
    private ScrollViewer? _scroller;
    private Border? _column;
    private Control? _article;
    private ArcContentSwap? _question;
    private Control? _steps;
    private AiReasoningDisclosure? _reasoning;
    private AiSourcesRow? _sources;
    private AiAnswerStage? _stage;
    private AiAnswerDocument? _document;
    private AiStatusNote? _note;
    private Control? _footer;
    private AiAnswerActions? _actions;
    private AiModelLine? _modelLine;
    private AiRelatedQuestions? _related;
    private Control? _empty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyDescription;
    private AiRelatedQuestions? _suggestions;
    private Border? _dock;
    private AiComposer? _composer;
    private AiAnnouncer? _statusRegion;
    private AiAnnouncer? _announcer;
    private AiFadePresence? _relatedFade;
    private double _questionFontSize = 28;
    private bool _keyboardMode;
    private bool _syncingComposer;
    private bool _syncingPanels;

    static AiAnswer()
    {
        FocusableProperty.OverrideDefaultValue<AiAnswer>(false);
    }

    /// <summary>Creates an empty answer page.</summary>
    public AiAnswer()
    {
        CopyTextAsync = text => ArcClipboard.CopyTextAsync(this, text);
        OpenUri = uri =>
        {
            if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                _ = launcher.LaunchUriAsync(uri);
            }
        };
        // Where the last input came from decides whether a focus move shows the keyboard ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardMode = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        Sources.CollectionChanged += OnStaticCollectionChanged;
        FollowUps.CollectionChanged += OnStaticCollectionChanged;
        Models.CollectionChanged += OnModelsChanged;
        Providers.CollectionChanged += OnModelsChanged;
        FeedbackReasons.CollectionChanged += OnFeedbackReasonsChanged;
        Versions.CollectionChanged += OnVersionsChanged;
        AddHandler(AiAnswerDocument.CitationActivatedEvent, OnCitationActivated);
        AddHandler(AiAnswerDocument.CitationInvokedEvent, OnCitationInvoked);
        AddHandler(AiAnswerDocument.RevealCompletedEvent, OnRevealCompleted);
        AddHandler(AiAnswerDocument.CodeCopiedEvent, OnCodeCopied);
        AddHandler(AiSourcesRow.CardActivatedEvent, OnCardActivated);
        AddHandler(AiSourcesRow.CardInvokedEvent, OnCardInvoked);
        AddHandler(AiAnswerActions.CopyRequestedEvent, OnCopyRequested);
        AddHandler(AiAnswerActions.LinkRequestedEvent, OnLinkRequested);
        AddHandler(AiAnswerActions.RegenerateChosenEvent, OnRegenerateChosen);
        AddHandler(AiAnswerActions.FeedbackChangedEvent, OnActionsFeedbackChanged);
        AddHandler(AiAnswerActions.VersionChangedEvent, OnActionsVersionChanged);
        AddHandler(AiStatusNote.ActionRequestedEvent, OnNoteActionRequested);
        AddHandler(AiRelatedQuestions.QuestionInvokedEvent, OnQuestionInvoked);
    }

    #region Test surface

    /// <summary>The last thing either live region said (tests).</summary>
    internal string? LastAnnouncement { get; private set; }

    /// <summary>The status region's last announcement (tests).</summary>
    internal string? LastStatusAnnouncement => _statusRegion?.Announcement;

    /// <summary>The action region's last announcement (tests).</summary>
    internal string? LastActionAnnouncement => _announcer?.Announcement;

    internal Border? TopBar => _topBar;
    internal AiAnswerActionButton? NewThreadButton => _newThread;
    internal ArcContentSwap? TitleSwap => _title;
    internal AiAnswerPillButton? ShareButton => _share;
    internal ScrollViewer? Scroller => _scroller;
    internal Border? Column => _column;
    internal Control? Article => _article;
    internal Control? Steps => _steps;
    internal AiReasoningDisclosure? ReasoningRow => _reasoning;
    internal AiSourcesRow? SourcesRow => _sources;
    internal AiAnswerStage? Stage => _stage;
    internal AiAnswerDocument? Document => _document;
    internal AiStatusNote? Note => _note;
    internal Control? Footer => _footer;
    internal AiAnswerActions? Actions => _actions;
    internal AiModelLine? ModelLineView => _modelLine;
    internal AiRelatedQuestions? Related => _related;
    internal Control? EmptyPage => _empty;
    internal TextBlock? EmptyTitleText => _emptyTitle;
    internal AiRelatedQuestions? SuggestionList => _suggestions;
    internal Border? Dock => _dock;
    internal AiComposer? Composer => _composer;

    /// <summary>The question heading on screen now.</summary>
    internal AiAnswerHeading? QuestionHeading => _question?.Content as AiAnswerHeading;

    /// <summary>The title text on screen now.</summary>
    internal string? TitleText => (_title?.Content as TextBlock)?.Text;

    /// <summary>The share pill's label on screen now.</summary>
    internal string? ShareText => (_shareLabel?.Content as TextBlock)?.Text;

    /// <summary>The column's side padding now.</summary>
    internal double SidePadding { get; private set; }

    /// <summary>The question's font size now.</summary>
    internal double QuestionFontSize => _questionFontSize;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _topBar = scope.Find<Border>(TopBarPart);
        _newThread = scope.Find<AiAnswerActionButton>(NewThreadPart);
        _title = scope.Find<ArcContentSwap>(TitlePart);
        _share = scope.Find<AiAnswerPillButton>(SharePart);
        _shareLabel = scope.Find<ArcContentSwap>(ShareLabelPart);
        _shareWidthIdle = scope.Find<TextBlock>("PART_ShareWidthIdle");
        _shareWidthCopied = scope.Find<TextBlock>("PART_ShareWidthCopied");
        _shareWidthFailed = scope.Find<TextBlock>("PART_ShareWidthFailed");
        _scroller = scope.Find<ScrollViewer>(ScrollerPart);
        _column = scope.Find<Border>(ColumnPart);
        _article = scope.Find<Control>(ArticlePart);
        _question = scope.Find<ArcContentSwap>(QuestionPart);
        _steps = scope.Find<Control>(StepsPart);
        _reasoning = scope.Find<AiReasoningDisclosure>(ReasoningPart);
        _sources = scope.Find<AiSourcesRow>(SourcesPart);
        _stage = scope.Find<AiAnswerStage>(StagePart);
        _document = scope.Find<AiAnswerDocument>(DocumentPart);
        _note = scope.Find<AiStatusNote>(NotePart);
        _footer = scope.Find<Control>(FooterPart);
        _actions = scope.Find<AiAnswerActions>(ActionsPart);
        _modelLine = scope.Find<AiModelLine>(ModelLinePart);
        _related = scope.Find<AiRelatedQuestions>(RelatedPart);
        _empty = scope.Find<Control>(EmptyPart);
        _emptyTitle = scope.Find<TextBlock>(EmptyTitlePart);
        _emptyDescription = scope.Find<TextBlock>(EmptyDescriptionPart);
        _suggestions = scope.Find<AiRelatedQuestions>(SuggestionsPart);
        _dock = scope.Find<Border>(DockPart);
        _composer = scope.Find<AiComposer>(ComposerPart);
        _statusRegion = scope.Find<AiAnnouncer>(StatusRegionPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        HookParts();

        _shownQuestion = null;
        _shownTitle = null;
        _shownVersion = null;
        ApplyLabels();
        ApplyComposerSettings();
        ApplyMetrics(Bounds.Width);
        UpdateHairline();
        UpdateShare();
        UpdateModels();
        UpdateFeedbackReasons();
        if (_suggestions is not null)
        {
            _suggestions.Questions = Suggestions;
        }
        RefreshStatic();
        ShowPage(PageChange.None);
    }

    private void HookParts()
    {
        if (_newThread is not null)
        {
            _newThread.Click += OnNewThreadClick;
        }
        if (_share is not null)
        {
            _share.Click += OnShareClick;
        }
        if (_reasoning is not null)
        {
            _reasoning.PropertyChanged += OnReasoningPropertyChanged;
        }
        if (_sources is not null)
        {
            _sources.PropertyChanged += OnSourcesRowPropertyChanged;
        }
        if (_composer is not null)
        {
            _composer.Sent += OnComposerSent;
            _composer.StopRequested += OnComposerStopRequested;
            _composer.PropertyChanged += OnComposerPropertyChanged;
        }
        if (_related is not null)
        {
            _relatedFade = new AiFadePresence(_related, shown: false);
        }
    }

    private void UnhookParts()
    {
        if (_newThread is not null)
        {
            _newThread.Click -= OnNewThreadClick;
        }
        if (_share is not null)
        {
            _share.Click -= OnShareClick;
        }
        if (_reasoning is not null)
        {
            _reasoning.PropertyChanged -= OnReasoningPropertyChanged;
        }
        if (_sources is not null)
        {
            _sources.PropertyChanged -= OnSourcesRowPropertyChanged;
            _sources.Sources = null;
        }
        if (_composer is not null)
        {
            _composer.Sent -= OnComposerSent;
            _composer.StopRequested -= OnComposerStopRequested;
            _composer.PropertyChanged -= OnComposerPropertyChanged;
        }
        _relatedFade?.Dispose();
        _relatedFade = null;
        HookShownVersion(null);
    }

    /// <summary>Says <paramref name="text"/> in the status region (Thinking, Writing the answer, Answer ready ...).</summary>
    private void AnnounceStatus(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        _statusRegion?.Announce(text);
        LastAnnouncement = text;
    }

    /// <summary>Says what an action did (copied, rated, another version) in the second region.</summary>
    private void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        _announcer?.Announce(text);
        LastAnnouncement = text;
    }

    #endregion

    #region Words

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/>.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.Answer);
        if (_newThread is not null)
        {
            AutomationProperties.SetName(_newThread, labels.NewThread);
            Tooltip.SetTip(_newThread, labels.NewThread);
        }
        if (_share is not null)
        {
            AutomationProperties.SetName(_share, labels.ShareThread);
            Tooltip.SetTip(_share, labels.ShareThread);
        }
        if (_shareWidthIdle is not null)
        {
            _shareWidthIdle.Text = labels.Share;
        }
        if (_shareWidthCopied is not null)
        {
            _shareWidthCopied.Text = labels.Copied;
        }
        if (_shareWidthFailed is not null)
        {
            _shareWidthFailed.Text = labels.CopyFailed;
        }
        UpdateShareLabel(instant: true);
        if (_reasoning is not null)
        {
            _reasoning.ThinkingText = labels.Thinking;
        }
        if (_sources is not null)
        {
            _sources.CardName = labels.SourceCard;
            _sources.ListName = labels.Sources;
        }
        if (_document is not null)
        {
            _document.CitationNameFormat = labels.SourceCard;
            _document.TableName = labels.Table;
            _document.CodeName = labels.Code;
            _document.CopyCodeLabel = labels.CopyCode;
            _document.CopiedCodeLabel = labels.Copied;
            _document.CopyFailedCodeLabel = labels.CopyFailed;
        }
        if (_actions is not null)
        {
            _actions.ToolbarName = labels.AnswerActions;
            _actions.CopyText = labels.CopyAnswer;
            _actions.CopiedText = labels.Copied;
            _actions.RegenerateText = labels.Regenerate;
            _actions.GoodText = labels.GoodAnswer;
            _actions.BadText = labels.BadAnswer;
            _actions.RemoveFeedbackText = labels.RemoveFeedback;
            _actions.LinkText = labels.CopyLink;
            _actions.LinkCopiedText = labels.Copied;
            _actions.TryAgainText = labels.TryAgain;
            _actions.MoreConciseText = labels.MoreConcise;
            _actions.MoreDetailedText = labels.MoreDetailed;
            _actions.SwitchModelText = labels.SwitchModel;
            _actions.AnswerAgainWithText = labels.AnswerAgainWith;
            _actions.WhatWentWrongText = labels.WhatWentWrong;
            _actions.ThanksTitle = labels.ThanksTitle;
            _actions.ThanksDescription = labels.ThanksDescription;
            _actions.ThanksDescriptionFor = labels.ThanksNoted;
            _actions.PreviousVersionText = labels.PreviousVersion;
            _actions.NextVersionText = labels.NextVersion;
            _actions.VersionName = labels.Version;
            _actions.VersionCountText = labels.VersionCount;
        }
        if (_related is not null)
        {
            _related.Heading = labels.Related;
        }
        if (_suggestions is not null)
        {
            _suggestions.Heading = null;
            _suggestions.ListName = labels.Suggestions;
        }
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = labels.EmptyTitle;
        }
        if (_emptyDescription is not null)
        {
            _emptyDescription.Text = labels.EmptyDescription;
        }
        if (_composer is not null)
        {
            _composer.AccessibleName = labels.Message;
            _composer.Labels = ComposerLabels(labels);
        }
        UpdateFeedbackReasons();
        UpdatePlaceholder();
        UpdateTitle();
        if (_shownVersion is not null)
        {
            ShowVersionParts(_shownVersion);
        }
    }

    /// <summary>
    /// The follow-up box's words: the answer's own for what it shows (placeholder, name, send, stop); its own "sent" and
    /// "stopped" announcements are silenced (the answer announces its status instead).
    /// </summary>
    private static AiComposerLabels ComposerLabels(AiAnswerLabels labels) => new()
    {
        Placeholder = labels.FollowUpPlaceholder,
        Message = labels.Message,
        Send = labels.Send,
        Stop = labels.Stop,
        SendEmpty = labels.Send,
        AnnounceStopped = string.Empty,
        AnnounceSent = string.Empty
    };

    /// <summary>The box says "Ask anything" on the empty page and "Ask a follow-up" under an answer.</summary>
    private void UpdatePlaceholder()
    {
        if (_composer is not null)
        {
            _composer.Placeholder = Status == AiAnswerStatus.Empty ? Labels.AskPlaceholder : Labels.FollowUpPlaceholder;
        }
    }

    private void ApplyComposerSettings()
    {
        if (_composer is null)
        {
            return;
        }
        // The box's own inline look sets these in a state style: local values here (the answer's box is 26 round, 18
        // from the text on the left counting the editor's own 4).
        _composer.CornerRadius = ComposerCornerRadius;
        _composer.Padding = ComposerPadding;
        _composer.MaxLines = Math.Max(1, MaxComposerLines);
        _composer.IsGenerating = IsBusy;
        _syncingComposer = true;
        try
        {
            _composer.Text = ComposerText ?? string.Empty;
        }
        finally
        {
            _syncingComposer = false;
        }
    }

    #endregion

    #region Size

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            ApplyMetrics(e.NewSize.Width);
        }
    }

    /// <summary>
    /// The column follows the block's own width (Arc's container query units): side padding 5% between 16 and 32, top
    /// padding 6% but at least 28, the question at 1.1rem + 1.6% between 22 and 28; at most 768 wide with its paddings and
    /// centered. The empty page adds 8% above its heading; the dock follows the column; the narrow sources row bleeds into
    /// the side padding.
    /// </summary>
    private void ApplyMetrics(double width)
    {
        var known = width > 0;
        var side = Math.Clamp(known ? width * SideRatio : SideMax, SideMin, SideMax);
        var top = Math.Max(TopMin, known ? width * TopRatio : TopMin);
        var font = Math.Clamp(known ? QuestionFontBase + width * QuestionFontRatio : QuestionFontMax, QuestionFontMin, QuestionFontMax);
        SidePadding = side;
        if (_column is not null)
        {
            _column.Padding = new Thickness(side, top, side, ColumnBottomPadding);
            _column.MaxWidth = ColumnMaxWidth;
        }
        if (_dock is not null)
        {
            _dock.Padding = new Thickness(side, DockTopPadding, side, DockBottomPadding);
            _dock.MaxWidth = ColumnMaxWidth;
        }
        if (_empty is not null)
        {
            _empty.Margin = new Thickness(0, known ? width * EmptyTopRatio : 0, 0, 0);
        }
        if (_sources is not null)
        {
            _sources.Bleed = side;
        }
        if (Math.Abs(font - _questionFontSize) > 0.01 || _emptyTitle?.FontSize != font)
        {
            _questionFontSize = font;
            if (QuestionHeading is { } heading)
            {
                ApplyHeadingFont(heading);
            }
            if (_emptyTitle is not null)
            {
                ApplyHeadingFont(_emptyTitle);
            }
        }
    }

    /// <summary>The question's size, line height (1.1) and tracking (−0.02em) at the current width.</summary>
    private void ApplyHeadingFont(TextBlock heading)
    {
        heading.FontSize = _questionFontSize;
        heading.LineHeight = Math.Round(_questionFontSize * QuestionLeading, 2);
        heading.LetterSpacing = _questionFontSize * QuestionTracking;
    }

    /// <summary>The top bar's hairline: BorderSubtle on a light page, Border on a dark one.</summary>
    private void UpdateHairline()
    {
        var dark = Background is ISolidColorBrush background && Oklch.FromColor(background.Color).L < 0.5;
        HairlineBrush = dark ? BorderBrush : SubtleBorderBrush;
    }

    #endregion

    #region Lifetime

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // An answer that leaves the window stops (Arc's unmount aborts the stream); what was shown stays.
        if (_run is { } run)
        {
            StopRun(run, announce: false);
        }
        _shareCancel?.Cancel();
        _shareTimer?.Stop();
        if (_shareState != ShareState.Idle)
        {
            _shareState = ShareState.Idle;
            UpdateShareLabel(instant: true);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // A question set before the answer had a window (or a responder) is asked now.
        Dispatcher.UIThread.Post(AskPendingQuestion, DispatcherPriority.Loaded);
    }

    #endregion
}
