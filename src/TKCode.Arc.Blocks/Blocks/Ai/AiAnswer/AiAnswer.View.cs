using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class AiAnswer
{
    /// <summary>Why the page is shown again: it decides the motion.</summary>
    private enum PageChange
    {
        /// <summary>Nothing moves (a new template, words, settings).</summary>
        None,

        /// <summary>A new question replaced the page: the title and the question crossfade, the rest is replaced at once, the page scrolls to the top.</summary>
        Question,

        /// <summary>A new version is being written: the page scrolls to the top, the old answer fades out while the area's height springs.</summary>
        Regenerate,

        /// <summary>Another version is shown: the answers crossfade while the area's height springs.</summary>
        Version,

        /// <summary>The finished answer of <see cref="Answer"/> changed: shown again at once.</summary>
        Static
    }

    private AiAnswerVersion? _shownVersion;
    private AiAnswerVersion? _hookedVersion;
    private string? _shownQuestion;
    private string? _shownTitle;
    private bool _settingIndex;
    private bool _changingVersions;

    /// <summary>The time a crossfaded answer takes to fade out (ai.md 4.2, document mode: 160ms).</summary>
    internal static readonly TimeSpan AnswerExitDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>The time the next answer takes to fade in (240ms).</summary>
    internal static readonly TimeSpan AnswerEnterDuration = TimeSpan.FromMilliseconds(240);

    /// <summary>The version on screen (the one <see cref="CurrentVersionIndex"/> points at), or null.</summary>
    internal AiAnswerVersion? CurrentVersion =>
        Versions.Count == 0 ? null : Versions[Math.Clamp(CurrentVersionIndex, 0, Versions.Count - 1)];

    /// <summary>The model of new answers: <see cref="Model"/>, else the first of <see cref="Models"/>.</summary>
    private AiModel? EffectiveModel => Model ?? (Models.Count > 0 ? Models[0] : null);

    /// <summary>No question, no answer, nothing running: the empty page.</summary>
    private bool IsEmptyPage => _run is null && Versions.Count == 0 && string.IsNullOrWhiteSpace(Question);

    #region Our properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == QuestionProperty)
        {
            if (!_settingQuestion)
            {
                OnQuestionSet(change.GetOldValue<string?>(), change.GetNewValue<string?>());
            }
        }
        else if (property == TitleProperty)
        {
            UpdateTitle();
        }
        else if (property == AnswerProperty)
        {
            _answerStamp++;
            _staticSuppressed = false;
            if (!string.IsNullOrEmpty(change.GetNewValue<string?>()) && !_pendingAsked)
            {
                // A finished answer heads the page: a question set before it is not asked of the responder.
                _questionPending = false;
            }
            OnStaticChanged();
        }
        else if (property == ReasoningProperty || property == UsageProperty)
        {
            OnStaticChanged();
        }
        else if (property == ModelProperty)
        {
            OnStaticChanged();
            UpdateModels();
        }
        else if (property == ResponderProperty)
        {
            if (_run is null && _questionPending)
            {
                AskPendingQuestion();
            }
            ShowNote(CurrentVersion, running: false);
            UpdateDock();
        }
        else if (property == ShareLinkProviderProperty)
        {
            UpdateShare();
        }
        else if (property == ComposerTextProperty)
        {
            if (_composer is not null && !_syncingComposer && _composer.Text != (change.GetNewValue<string?>() ?? string.Empty))
            {
                _syncingComposer = true;
                try
                {
                    _composer.Text = change.GetNewValue<string?>() ?? string.Empty;
                }
                finally
                {
                    _syncingComposer = false;
                }
            }
        }
        else if (property == MaxComposerLinesProperty)
        {
            if (_composer is not null)
            {
                _composer.MaxLines = Math.Max(1, change.GetNewValue<int>());
            }
        }
        else if (property == IsReasoningExpandedProperty)
        {
            SyncPanels();
        }
        else if (property == IsSourcesExpandedProperty)
        {
            SyncPanels();
        }
        else if (property == RevealIntervalProperty || property == WordsPerStepProperty)
        {
            ApplyTiming();
        }
        else if (property == CurrentVersionIndexProperty)
        {
            if (!_settingIndex)
            {
                OnIndexSet(change.GetNewValue<int>());
            }
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
        }
        else if (property == BackgroundProperty || property == BorderBrushProperty || property == SubtleBorderBrushProperty)
        {
            UpdateHairline();
        }
        else if (property == ComposerPaddingProperty || property == ComposerCornerRadiusProperty)
        {
            ApplyComposerSettings();
        }
        else if (property == ColumnMaxWidthProperty || property == SideRatioProperty || property == SideMinProperty ||
                 property == SideMaxProperty || property == TopRatioProperty || property == TopMinProperty ||
                 property == ColumnBottomPaddingProperty || property == EmptyTopRatioProperty || property == QuestionFontBaseProperty ||
                 property == QuestionFontRatioProperty || property == QuestionFontMinProperty || property == QuestionFontMaxProperty ||
                 property == QuestionLeadingProperty || property == QuestionTrackingProperty || property == DockTopPaddingProperty ||
                 property == DockBottomPaddingProperty)
        {
            _questionFontSize = -1;
            ApplyMetrics(Bounds.Width);
        }
    }

    /// <summary>The reasoning and sources panels follow <see cref="IsReasoningExpanded"/> and <see cref="IsSourcesExpanded"/>.</summary>
    private void SyncPanels()
    {
        _syncingPanels = true;
        try
        {
            if (_reasoning is not null)
            {
                _reasoning.IsExpanded = IsReasoningExpanded;
            }
            if (_sources is not null)
            {
                _sources.IsExpanded = IsSourcesExpanded;
            }
        }
        finally
        {
            _syncingPanels = false;
        }
    }

    private void OnReasoningPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiReasoningDisclosure.IsExpandedProperty && !_syncingPanels)
        {
            SetCurrentValue(IsReasoningExpandedProperty, e.GetNewValue<bool>());
        }
    }

    private void OnSourcesRowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiSourcesRow.IsExpandedProperty && !_syncingPanels)
        {
            SetCurrentValue(IsSourcesExpandedProperty, e.GetNewValue<bool>());
        }
    }

    private void OnComposerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiComposer.TextProperty && !_syncingComposer)
        {
            _syncingComposer = true;
            try
            {
                SetCurrentValue(ComposerTextProperty, e.GetNewValue<string?>() ?? string.Empty);
            }
            finally
            {
                _syncingComposer = false;
            }
        }
    }

    /// <summary>The reveal clock of the answer (and the reasoning) follows <see cref="RevealInterval"/> and <see cref="WordsPerStep"/>.</summary>
    private void ApplyTiming()
    {
        if (_document is not null)
        {
            _document.RevealInterval = RevealInterval;
            _document.WordsPerStep = Math.Max(1, WordsPerStep);
        }
    }

    #endregion

    #region Versions

    /// <summary>
    /// Code moved <see cref="CurrentVersionIndex"/>: out-of-range values are clamped; while a version is being written the
    /// index stays on it (the text being written has nowhere else to go).
    /// </summary>
    private void OnIndexSet(int index)
    {
        var target = Versions.Count == 0 ? 0 : Math.Clamp(index, 0, Versions.Count - 1);
        if (_run is { } run && Versions.IndexOf(run.Version) is var running and >= 0)
        {
            target = running;
        }
        if (target != index)
        {
            SetIndex(target);
        }
        if (!ReferenceEquals(CurrentVersion, _shownVersion))
        {
            ShowPage(PageChange.Version);
        }
    }

    private void SetIndex(int index)
    {
        _settingIndex = true;
        try
        {
            SetCurrentValue(CurrentVersionIndexProperty, index);
        }
        finally
        {
            _settingIndex = false;
        }
    }

    /// <summary>Shows the version at <paramref name="index"/>; the pager's moves are announced ("Showing version 2 of 3").</summary>
    public void ShowVersion(int index) => ShowVersionAt(index, announce: false);

    private void ShowVersionAt(int index, bool announce)
    {
        if (Versions.Count == 0 || _run is not null)
        {
            return;
        }
        var target = Math.Clamp(index, 0, Versions.Count - 1);
        SetIndex(target);
        if (!ReferenceEquals(CurrentVersion, _shownVersion))
        {
            ShowPage(PageChange.Version);
        }
        if (announce)
        {
            Announce(Labels.AnnounceVersion(target + 1, Versions.Count));
        }
    }

    private void OnVersionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_changingVersions)
        {
            return;
        }
        // Code changed the list: keep the index in range and show what it points at.
        var clamped = Versions.Count == 0 ? 0 : Math.Clamp(CurrentVersionIndex, 0, Versions.Count - 1);
        if (clamped != CurrentVersionIndex)
        {
            SetIndex(clamped);
        }
        ShowPage(PageChange.None);
    }

    /// <summary>Empties <see cref="Versions"/> without reacting to it as a change made by code.</summary>
    private void ClearVersions()
    {
        _changingVersions = true;
        try
        {
            Versions.Clear();
            _staticVersion = null;
        }
        finally
        {
            _changingVersions = false;
        }
        SetIndex(0);
    }

    private void AddVersion(AiAnswerVersion version)
    {
        _changingVersions = true;
        try
        {
            Versions.Add(version);
        }
        finally
        {
            _changingVersions = false;
        }
    }

    #endregion

    #region The page

    /// <summary>
    /// Shows the state of the page: the empty page or the question with its shown version, the top bar, the box. The
    /// motion depends on <paramref name="change"/>; everything else follows at once.
    /// </summary>
    private void ShowPage(PageChange change)
    {
        var empty = IsEmptyPage;
        if (_article is not null)
        {
            _article.IsVisible = !empty;
        }
        if (_empty is not null)
        {
            _empty.IsVisible = empty;
        }
        if (_newThread is not null)
        {
            _newThread.IsEnabled = !empty;
        }
        UpdateDock();
        UpdateQuestionHeading();
        // Arc's section is labelled by its heading: the question, or the empty page's heading.
        if ((empty ? _emptyTitle : QuestionHeading) is { } label)
        {
            AutomationProperties.SetLabeledBy(this, label);
        }
        else
        {
            ClearValue(AutomationProperties.LabeledByProperty);
        }
        if (change is PageChange.Question or PageChange.Regenerate && _scroller is not null)
        {
            // Arc: a new question or a new version starts at the top of the page, at once.
            _scroller.Offset = new Vector(_scroller.Offset.X, 0);
        }
        var version = CurrentVersion;
        if (change is PageChange.Regenerate or PageChange.Version && _stage is not null && !ReferenceEquals(version, _shownVersion))
        {
            _stage.Crossfade(AnswerExitDuration, AnswerEnterDuration);
            _stage.Arm();
        }
        ShowVersionParts(version, reload: change is PageChange.Static or PageChange.None || !ReferenceEquals(version, _shownVersion));
        UpdateShare();
        UpdateTitle();
        UpdatePlaceholder();
    }

    /// <summary>The follow-up box needs a responder: a finished answer shown without one has no box (Arc's static answer).</summary>
    private void UpdateDock()
    {
        if (_dock is not null)
        {
            _dock.IsVisible = Responder is not null;
        }
    }

    /// <summary>The parts of one version: text, reasoning, sources, note, footer, related questions; then the status.</summary>
    private void ShowVersionParts(AiAnswerVersion? version, bool reload = false)
    {
        HookShownVersion(version);
        var running = _run is { } run && ReferenceEquals(run.Version, version);
        // A version being written is never shown again from the start while it stays on screen (its reveal would restart).
        if (_document is not null && (!ReferenceEquals(version, _shownVersion) || (reload && !running)))
        {
            _document.LitSourceNumber = 0;
            _document.Sources = version?.Sources;
            if (version is null)
            {
                _document.Reset();
            }
            else if (running)
            {
                // A version being written shows what arrived so far, paced from here on.
                _document.Reset();
                if (_run!.Raw.Length > 0)
                {
                    _document.Push(_run.Raw.ToString());
                }
            }
            else
            {
                _document.SetMarkdown(version.Markdown);
            }
        }
        if (_sources is not null)
        {
            _sources.ActiveNumber = 0;
        }
        _shownVersion = version;
        UpdateSteps(version, running);
        ShowNote(version, running);
        UpdateFooter(version, running);
        UpdateRelated(version, running);
        UpdateModels();
        UpdateStatusFromState();
    }

    /// <summary>The reasoning row (while it thinks, or with reasoning or a thinking time) and the sources row (with sources).</summary>
    private void UpdateSteps(AiAnswerVersion? version, bool running)
    {
        var run = running ? _run : null;
        var thinking = run is { Thought: null };
        var thought = run is not null ? run.Thought : version?.Usage?.ThoughtTime;
        var reasoning = run is not null ? _reasoningShown.ToString() : version?.Reasoning;
        var showsReasoning = thinking || thought is not null || !string.IsNullOrWhiteSpace(reasoning);
        if (_reasoning is not null)
        {
            _reasoning.ThinkingText = Labels.Thinking;
            _reasoning.ThoughtText = thought is { } time ? Labels.ThoughtFor(Seconds(time)) : run is null ? Labels.ReasoningSummary : null;
            _reasoning.Reasoning = reasoning;
            _reasoning.IsThinking = thinking;
            _reasoning.IsVisible = version is not null && showsReasoning;
        }
        var count = version?.Sources.Count ?? 0;
        if (_sources is not null)
        {
            if (!ReferenceEquals(_sources.Sources, version?.Sources))
            {
                _sources.Sources = version?.Sources;
            }
            _sources.LabelText = Labels.SourceCount(count);
            _sources.IsVisible = count > 0;
        }
        if (_steps is not null)
        {
            _steps.IsVisible = version is not null && (showsReasoning || count > 0);
        }
        SyncPanels();
    }

    /// <summary>Whole seconds of a thinking time, at least 1 ("Thought for 1s" rather than 0s).</summary>
    internal static int Seconds(TimeSpan time) => Math.Max(1, (int)Math.Round(time.TotalSeconds, MidpointRounding.AwayFromZero));

    /// <summary>The note under a stopped or failed version, with Try again when there is a responder to ask.</summary>
    private void ShowNote(AiAnswerVersion? version, bool running)
    {
        if (_note is null)
        {
            return;
        }
        var labels = Labels;
        if (version is null || running || version.Outcome == AiAnswerOutcome.Complete)
        {
            _note.IsActive = false;
            _note.IsVisible = false;
            return;
        }
        if (version.Outcome == AiAnswerOutcome.Stopped)
        {
            _note.Tone = AiStatusNoteTone.Stopped;
            _note.Text = string.IsNullOrEmpty(version.Markdown) ? labels.StoppedBeforeAnswer : labels.StoppedNote;
        }
        else
        {
            _note.Tone = AiStatusNoteTone.Error;
            _note.Text = labels.FailedNote(version.ErrorMessage);
        }
        _note.Title = null;
        _note.ActionText = Responder is not null && !string.IsNullOrWhiteSpace(Question) ? labels.Retry : null;
        if (!_note.IsVisible)
        {
            _note.IsVisible = true;
        }
        _note.IsActive = true;
    }

    /// <summary>The footer: the action bar (hidden, its height kept, while the version is written) and the model line.</summary>
    private void UpdateFooter(AiAnswerVersion? version, bool running)
    {
        if (_footer is not null)
        {
            _footer.IsVisible = version is not null;
        }
        if (_actions is not null)
        {
            _actions.IsBusy = running;
            _actions.Feedback = version?.Feedback ?? AiAnswerFeedbackValue.None;
            _actions.VersionCount = Versions.Count;
            _actions.VersionIndex = Math.Clamp(CurrentVersionIndex, 0, Math.Max(0, Versions.Count - 1));
            _actions.HasShare = ShareLinkProvider is not null;
        }
        if (_modelLine is not null)
        {
            var usage = version?.Usage;
            _modelLine.Model = version?.Model ?? EffectiveModel;
            _modelLine.Providers = Providers;
            _modelLine.TokensText = usage?.Tokens is { } tokens ? Labels.Tokens(tokens) : null;
            _modelLine.DurationText = usage?.Duration is { } duration ? Labels.Seconds(Math.Round(duration.TotalSeconds, 1)) : null;
            _modelLine.IsStopped = version is not null && version.Outcome != AiAnswerOutcome.Complete;
        }
    }

    /// <summary>The related questions of the shown version fade in once it is written (and out while a new one is).</summary>
    private void UpdateRelated(AiAnswerVersion? version, bool running)
    {
        if (_related is null)
        {
            return;
        }
        if (!ReferenceEquals(_related.Questions, version?.FollowUps) && version is not null)
        {
            _related.Questions = version.FollowUps;
        }
        _relatedFade?.SetShown(version is not null && !running && version.FollowUps.Count > 0);
    }

    /// <summary>The models of "Switch model" leave out the shown version's model; the marks look up the providers.</summary>
    private void UpdateModels()
    {
        var current = CurrentVersion?.Model ?? EffectiveModel;
        if (_actions is not null)
        {
            var others = new List<AiModel>(Models.Count);
            foreach (var model in Models)
            {
                if (model is not null && !SameModel(model, current))
                {
                    others.Add(model);
                }
            }
            _actions.Models = others;
            _actions.Providers = Providers;
        }
        if (_modelLine is not null)
        {
            _modelLine.Providers = Providers;
        }
    }

    private static bool SameModel(AiModel a, AiModel? b) =>
        b is not null && (ReferenceEquals(a, b) || (!string.IsNullOrEmpty(a.Id) && a.Id == b.Id));

    private void OnModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateModels();
        if (_shownVersion is { } version)
        {
            UpdateFooter(version, _run is { } run && ReferenceEquals(run.Version, version));
        }
        OnStaticChanged();
    }

    private void UpdateFeedbackReasons()
    {
        if (_actions is not null)
        {
            _actions.FeedbackReasons = FeedbackReasons.Count > 0 ? FeedbackReasons : Labels.FeedbackReasons;
        }
    }

    private void OnFeedbackReasonsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateFeedbackReasons();

    /// <summary>Follows the shown version's sources (they stream in) and follow-up questions (the end of the stream brings them).</summary>
    private void HookShownVersion(AiAnswerVersion? version)
    {
        if (ReferenceEquals(version, _hookedVersion))
        {
            return;
        }
        if (_hookedVersion is not null)
        {
            _hookedVersion.Sources.CollectionChanged -= OnShownSourcesChanged;
            _hookedVersion.FollowUps.CollectionChanged -= OnShownFollowUpsChanged;
        }
        _hookedVersion = version;
        if (version is not null)
        {
            version.Sources.CollectionChanged += OnShownSourcesChanged;
            version.FollowUps.CollectionChanged += OnShownFollowUpsChanged;
        }
    }

    private void OnShownSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_shownVersion is { } version)
        {
            UpdateSteps(version, _run is { } run && ReferenceEquals(run.Version, version));
        }
    }

    private void OnShownFollowUpsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_shownVersion is { } version)
        {
            UpdateRelated(version, _run is { } run && ReferenceEquals(run.Version, version));
        }
    }

    /// <summary>
    /// <see cref="Status"/>: Thinking or Writing while a version is written, Empty on the empty page, otherwise how the shown
    /// version ended (each version keeps its own: paging to a stopped one shows Stopped).
    /// </summary>
    private void UpdateStatusFromState()
    {
        if (_run is { } run)
        {
            SetStatus(run.Thought is null ? AiAnswerStatus.Thinking : AiAnswerStatus.Writing);
            ErrorMessage = null;
            return;
        }
        if (IsEmptyPage)
        {
            SetStatus(AiAnswerStatus.Empty);
            ErrorMessage = null;
            return;
        }
        var version = CurrentVersion;
        SetStatus(version?.Outcome switch
        {
            AiAnswerOutcome.Stopped => AiAnswerStatus.Stopped,
            AiAnswerOutcome.Failed => AiAnswerStatus.Failed,
            _ => AiAnswerStatus.Ready
        });
        ErrorMessage = version?.Outcome == AiAnswerOutcome.Failed ? version.ErrorMessage : null;
    }

    private void SetStatus(AiAnswerStatus status)
    {
        var old = Status;
        if (old == status)
        {
            return;
        }
        Status = status;
        var busy = status is AiAnswerStatus.Thinking or AiAnswerStatus.Writing;
        IsBusy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        PseudoClasses.Set(EmptyPseudoClass, status == AiAnswerStatus.Empty);
        // Avalonia has no aria-busy: the block's item status says what it is doing while busy.
        AutomationProperties.SetItemStatus(this, status switch
        {
            AiAnswerStatus.Thinking => Labels.AnnounceThinking,
            AiAnswerStatus.Writing => Labels.AnnounceWriting,
            _ => null
        });
        if (_composer is not null)
        {
            _composer.IsGenerating = busy;
        }
        if (_share is not null)
        {
            _share.IsEnabled = status != AiAnswerStatus.Empty;
        }
        UpdatePlaceholder();
        UpdateTitle();
        RaiseEvent(new AiAnswerStatusEventArgs(StatusChangedEvent, this, old, status));
    }

    #endregion

    #region Title and question

    /// <summary>The top bar's title: <see cref="Title"/>, else the question; "New thread" on the empty page. A change crossfades.</summary>
    private void UpdateTitle()
    {
        var text = IsEmptyPage ? Labels.NewThread : !string.IsNullOrWhiteSpace(Title) ? Title! : Question ?? Labels.NewThread;
        if (_title is null || text == _shownTitle)
        {
            return;
        }
        _shownTitle = text;
        _title.Content = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Medium,
            LineHeight = 19.6,
            LetterSpacing = -0.14,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>The question heading: a new one per question, so the old one can crossfade out while the new one fades in.</summary>
    private void UpdateQuestionHeading()
    {
        var text = Question ?? string.Empty;
        if (_question is null || text == _shownQuestion)
        {
            return;
        }
        _shownQuestion = text;
        var heading = new AiAnswerHeading { Text = text };
        ApplyHeadingFont(heading);
        _question.Content = heading;
    }

    /// <summary>Moves the focus to the question heading once it is on screen (after a related question or a suggestion).</summary>
    private void FocusQuestion()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (QuestionHeading is { IsEffectivelyVisible: true } heading)
            {
                heading.Focus(NavigationMethod.Unspecified);
            }
        }, DispatcherPriority.Loaded);
    }

    #endregion

    #region Share pill

    private enum ShareState
    {
        Idle,
        Copied,
        Failed
    }

    private ShareState _shareState;
    private CancellationTokenSource? _shareCancel;
    private DispatcherTimer? _shareTimer;
    private DateTimeOffset _shareUntil;

    /// <summary>Share shows only with a link provider, and is disabled on the empty page.</summary>
    private void UpdateShare()
    {
        if (_share is not null)
        {
            _share.IsVisible = ShareLinkProvider is not null;
            _share.IsEnabled = !IsEmptyPage;
        }
        if (_actions is not null)
        {
            _actions.HasShare = ShareLinkProvider is not null;
        }
    }

    /// <summary>"Share", "Copied" or "Failed" (the pill keeps the width of the longest); a change swaps like Arc's labels.</summary>
    private void UpdateShareLabel(bool instant)
    {
        if (_share is not null)
        {
            _share.Glyph = _shareState == ShareState.Copied ? ArcIcons.Check : ArcIcons.Link2;
        }
        if (_shareLabel is null)
        {
            return;
        }
        var text = _shareState switch
        {
            ShareState.Copied => Labels.Copied,
            ShareState.Failed => Labels.CopyFailed,
            _ => Labels.Share
        };
        if (_shareLabel.Content is TextBlock shown && shown.Text == text)
        {
            return;
        }
        var label = new TextBlock { Text = text, LineHeight = 19.6, VerticalAlignment = VerticalAlignment.Center };
        if (instant)
        {
            var mode = _shareLabel.Mode;
            _shareLabel.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
            _shareLabel.Content = label;
            _shareLabel.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
        }
        else
        {
            _shareLabel.Content = label;
        }
    }

    private void SetShareState(ShareState state)
    {
        _shareState = state;
        UpdateShareLabel(instant: false);
        _shareTimer?.Stop();
        if (state == ShareState.Idle)
        {
            return;
        }
        _shareUntil = Time.GetUtcNow() + CopiedDuration;
        if (_shareTimer is null)
        {
            _shareTimer = new DispatcherTimer();
            _shareTimer.Tick += (_, _) => TickShare();
        }
        _shareTimer.Interval = CopiedDuration > TimeSpan.Zero ? CopiedDuration : TimeSpan.FromMilliseconds(1);
        _shareTimer.Start();
    }

    /// <summary>Turns the share pill back to "Share" once its time is up (the timer calls it; tests call it after moving the time).</summary>
    internal void TickShare()
    {
        if (_shareState == ShareState.Idle)
        {
            _shareTimer?.Stop();
            return;
        }
        var now = Time.GetUtcNow();
        if (now >= _shareUntil)
        {
            _shareTimer?.Stop();
            _shareState = ShareState.Idle;
            UpdateShareLabel(instant: false);
        }
        else if (_shareTimer is not null)
        {
            _shareTimer.Interval = _shareUntil - now;
        }
    }

    #endregion
}
