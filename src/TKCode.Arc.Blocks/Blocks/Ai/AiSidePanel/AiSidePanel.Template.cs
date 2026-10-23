using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

[TemplatePart(DockPart, typeof(AiPanelDock))]
[TemplatePart(PagePart, typeof(AiPanelPageView))]
[TemplatePart(MarkPart, typeof(ModelMark))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(ModelPart, typeof(TextBlock))]
[TemplatePart(ClosePart, typeof(AiPanelIconButton))]
[TemplatePart(ChipsPart, typeof(Control))]
[TemplatePart(PagePillPart, typeof(AiPanelContextPill))]
[TemplatePart(SelectionPillPart, typeof(AiPanelSelectionPill))]
[TemplatePart(ScrollerPart, typeof(AiFollowScroller))]
[TemplatePart(ThreadPart, typeof(Control))]
[TemplatePart(TurnsPart, typeof(StackPanel))]
[TemplatePart(EmptyPart, typeof(Control))]
[TemplatePart(EmptyTitlePart, typeof(TextBlock))]
[TemplatePart(EmptyTextPart, typeof(TextBlock))]
[TemplatePart(EmptySuggestionsPart, typeof(AiPanelSuggestionRow))]
[TemplatePart(UndoPillPart, typeof(AiUndoPill))]
[TemplatePart(BoxPart, typeof(AiPanelBox))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(SheetPseudoClass, OpenPseudoClass, BusyPseudoClass, EmptyPseudoClass, SelectionPseudoClass)]
public partial class AiSidePanel
{
    public const string DockPart = "PART_Dock";
    public const string PagePart = "PART_Page";
    public const string MarkPart = "PART_Mark";
    public const string NamePart = "PART_Name";
    public const string ModelPart = "PART_Model";
    public const string ClosePart = "PART_Close";
    public const string ChipsPart = "PART_Chips";
    public const string PagePillPart = "PART_PagePill";
    public const string SelectionPillPart = "PART_SelectionPill";
    public const string ScrollerPart = "PART_Scroller";
    public const string ThreadPart = "PART_Thread";
    public const string TurnsPart = "PART_Turns";
    public const string EmptyPart = "PART_Empty";
    public const string EmptyTitlePart = "PART_EmptyTitle";
    public const string EmptyTextPart = "PART_EmptyText";
    public const string EmptySuggestionsPart = "PART_EmptySuggestions";
    public const string UndoPillPart = "PART_UndoPill";
    public const string BoxPart = "PART_Box";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the assistant is a bottom sheet (<see cref="IsSheetMode"/>).</summary>
    public const string SheetPseudoClass = ":sheet";

    /// <summary>Set while the assistant is open.</summary>
    public const string OpenPseudoClass = ":open";

    /// <summary>Set while <see cref="IsBusy"/>.</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>Set while the thread has no turns: the empty state and its suggestions show.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while a selection is the context of the next question.</summary>
    public const string SelectionPseudoClass = ":selection";

    #region Token-driven internals

    internal static readonly StyledProperty<TimeSpan> ReadingMinimumProperty =
        AvaloniaProperty.Register<AiSidePanel, TimeSpan>(nameof(ReadingMinimum), TimeSpan.FromMilliseconds(600));

    /// <summary>The shortest "Reading the page" (the token's 0.6s).</summary>
    internal TimeSpan ReadingMinimum { get => GetValue(ReadingMinimumProperty); set => SetValue(ReadingMinimumProperty, value); }

    #endregion

    private AiPanelDock? _dock;
    private AiPanelPageView? _pageView;
    private ModelMark? _mark;
    private TextBlock? _name;
    private TextBlock? _model;
    private AiPanelIconButton? _close;
    private Control? _chips;
    private AiPanelContextPill? _pagePill;
    private AiPanelSelectionPill? _selectionPill;
    private AiFollowScroller? _scroller;
    private Control? _threadHost;
    private StackPanel? _turnsPanel;
    private Control? _empty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyText;
    private AiPanelSuggestionRow? _emptySuggestions;
    private AiUndoPill? _undoPill;
    private AiPanelBox? _box;
    private AiAnnouncer? _announcer;
    private AiFadePresence? _selectionFade;
    private AiFadePresence? _emptyFade;
    private AiFadePresence? _threadFade;
    private bool _keyboardMode;

    static AiSidePanel()
    {
        FocusableProperty.OverrideDefaultValue<AiSidePanel>(false);
    }

    /// <summary>Creates a side panel with no page.</summary>
    public AiSidePanel()
    {
        _loop = new Motion.LiveLoop(this, Step)
        {
            // The answer keeps its cadence at every motion level (only the fades go) and while the assistant is closed.
            MinimumLevel = Motion.ArcMotionLevel.Off,
            TracksViewport = false
        };
        // Where the last input came from decides whether a focus move shows the keyboard ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardMode = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(AiPanelPageView.SelectionRequestedEvent, OnSelectionRequested);
        AddHandler(AiPanelDock.FocusPanelRequestedEvent, OnFocusPanelRequested);
        AddHandler(AiPanelSuggestionRow.InvokedEvent, OnSuggestionInvoked);
        AddHandler(AiPanelChangeCard.ApplyRequestedEvent, OnApplyRequested);
        AddHandler(AiPanelChangeCard.DiscardRequestedEvent, OnDiscardRequested);
        AddHandler(AiPanelErrorCard.RetryRequestedEvent, OnRetryRequested);
        AddHandler(AiPanelBox.SendRequestedEvent, OnBoxSendRequested);
        AddHandler(AiPanelBox.StopRequestedEvent, OnBoxStopRequested);
        Suggestions.CollectionChanged += OnSuggestionsChanged;
        Turns.CollectionChanged += OnTurnsChanged;
    }

    #region Test surface

    /// <summary>The live region's last announcement (tests).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    internal AiPanelDock? Dock => _dock;
    internal AiPanelPageView? PageView => _pageView;
    internal AiPanelIconButton? CloseButton => _close;
    internal AiPanelContextPill? PagePill => _pagePill;
    internal AiPanelSelectionPill? SelectionPill => _selectionPill;
    internal AiFollowScroller? Scroller => _scroller;
    internal StackPanel? TurnsPanel => _turnsPanel;
    internal Control? EmptyState => _empty;
    internal AiPanelSuggestionRow? EmptySuggestions => _emptySuggestions;
    internal AiUndoPill? UndoPill => _undoPill;
    internal AiPanelBox? Box => _box;
    internal TextBlock? NameText => _name;
    internal TextBlock? ModelText => _model;

    /// <summary>Whether the selection pill is shown (or fading in).</summary>
    internal bool IsSelectionPillShown => _selectionFade?.IsShown == true;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _dock = scope.Find<AiPanelDock>(DockPart);
        _pageView = scope.Find<AiPanelPageView>(PagePart);
        _mark = scope.Find<ModelMark>(MarkPart);
        _name = scope.Find<TextBlock>(NamePart);
        _model = scope.Find<TextBlock>(ModelPart);
        _close = scope.Find<AiPanelIconButton>(ClosePart);
        _chips = scope.Find<Control>(ChipsPart);
        _pagePill = scope.Find<AiPanelContextPill>(PagePillPart);
        _selectionPill = scope.Find<AiPanelSelectionPill>(SelectionPillPart);
        _scroller = scope.Find<AiFollowScroller>(ScrollerPart);
        _threadHost = scope.Find<Control>(ThreadPart);
        _turnsPanel = scope.Find<StackPanel>(TurnsPart);
        _empty = scope.Find<Control>(EmptyPart);
        _emptyTitle = scope.Find<TextBlock>(EmptyTitlePart);
        _emptyText = scope.Find<TextBlock>(EmptyTextPart);
        _emptySuggestions = scope.Find<AiPanelSuggestionRow>(EmptySuggestionsPart);
        _undoPill = scope.Find<AiUndoPill>(UndoPillPart);
        _box = scope.Find<AiPanelBox>(BoxPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        HookParts();

        SyncPageView();
        ApplyLabels();
        ApplyIdentity();
        SyncSheetMode();
        PseudoClasses.Set(OpenPseudoClass, IsOpen);
        ShowSelection(Selection, animate: false);
        RebuildThread();
        RefreshUndoPill();
        UpdateBusy();
    }

    private void HookParts()
    {
        if (_dock is not null)
        {
            _dock.PropertyChanged += OnDockPropertyChanged;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_selectionPill is not null)
        {
            _selectionPill.RemoveRequested += OnSelectionRemoveRequested;
            _selectionFade = new AiFadePresence(_selectionPill, Selection is not null);
        }
        if (_undoPill is not null)
        {
            _undoPill.UndoRequested += OnUndoRequested;
        }
        if (_empty is not null)
        {
            _emptyFade = new AiFadePresence(_empty, Turns.Count == 0);
        }
        if (_threadHost is not null)
        {
            _threadHost.IsVisible = true;
            _threadFade = new AiFadePresence(_threadHost, Turns.Count > 0);
        }
    }

    private void UnhookParts()
    {
        if (_dock is not null)
        {
            _dock.PropertyChanged -= OnDockPropertyChanged;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_selectionPill is not null)
        {
            _selectionPill.RemoveRequested -= OnSelectionRemoveRequested;
        }
        if (_undoPill is not null)
        {
            _undoPill.UndoRequested -= OnUndoRequested;
        }
        _selectionFade?.Dispose();
        _selectionFade = null;
        _emptyFade?.Dispose();
        _emptyFade = null;
        _threadFade?.Dispose();
        _threadFade = null;
        ClearThreadViews();
    }

    /// <summary>Says <paramref name="text"/> through the polite live region.</summary>
    private void Announce(string? text) => _announcer?.Announce(text);

    /// <summary>How a focus move should look: with the ring after a key, without after a pointer.</summary>
    private NavigationMethod FocusMethod => _keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer;

    #endregion

    #region Words

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/> and the assistant's name.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        var name = AssistantName;
        AutomationProperties.SetName(this, labels.Panel(name));
        if (_dock is not null)
        {
            _dock.ReopenText = labels.Reopen(name);
            _dock.PanelName = labels.Panel(name);
        }
        if (_pageView is not null)
        {
            _pageView.AskButtonText = labels.AskAboutSelection;
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Close);
            Tooltip.SetTip(_close, labels.CloseTip);
        }
        if (_chips is not null)
        {
            AutomationProperties.SetName(_chips, labels.Context);
        }
        if (_pagePill is not null)
        {
            _pagePill.Text = labels.ThisPage;
        }
        if (_turnsPanel is not null)
        {
            AutomationProperties.SetName(_turnsPanel, labels.Conversation);
        }
        if (_scroller is not null)
        {
            _scroller.JumpButtonName = labels.ScrollToLatest;
        }
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = labels.EmptyTitle(Page?.Title ?? string.Empty);
        }
        if (_emptyText is not null)
        {
            _emptyText.Text = labels.EmptyDescription(name);
        }
        if (_emptySuggestions is not null)
        {
            AutomationProperties.SetName(_emptySuggestions, labels.Suggestions);
        }
        if (_suggestionRow is not null)
        {
            AutomationProperties.SetName(_suggestionRow, labels.Suggestions);
        }
        if (_undoPill is not null)
        {
            _undoPill.UndoText = labels.Undo;
        }
        if (_box is not null)
        {
            _box.InputName = labels.Message;
            _box.SendName = labels.Send;
            _box.StopName = labels.Stop;
        }
        ShowSelection(Selection, animate: false);
        RefreshPlaceholder();
        RefreshUndoPill();
        foreach (var view in _views.Values)
        {
            view.ApplyLabels(labels, name);
        }
    }

    /// <summary>
    /// The page view is the content of the dock's presenter, out of reach of template bindings: its page, column width and
    /// mark are set here.
    /// </summary>
    private void SyncPageView()
    {
        if (_pageView is null)
        {
            return;
        }
        _pageView.Page = Page;
        _pageView.MaxColumnWidth = PageMaxWidth;
        _pageView.AssistantMark = AssistantMark;
    }

    /// <summary>The header's identity: the mark (the spark when none is given), the name and the optional model line.</summary>
    private void ApplyIdentity()
    {
        if (_mark is not null)
        {
            _mark.Mark = AiAssistantSpark.Resolve(AssistantMark);
        }
        if (_name is not null)
        {
            _name.Text = AssistantName;
        }
        if (_model is not null)
        {
            _model.Text = ModelName;
            _model.IsVisible = !string.IsNullOrEmpty(ModelName);
        }
    }

    private void RefreshPlaceholder()
    {
        if (_box is null)
        {
            return;
        }
        _box.Placeholder = Selection is { } selection && !string.IsNullOrEmpty(selection.Label)
            ? Labels.PlaceholderForSelection(selection.Label.ToLowerInvariant())
            : Labels.Placeholder;
    }

    #endregion

    #region Our properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PageProperty)
        {
            SyncPageView();
            OnPageChanged(change.GetOldValue<AiPanelPage?>(), change.GetNewValue<AiPanelPage?>());
        }
        else if (property == PageMaxWidthProperty)
        {
            SyncPageView();
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
        }
        else if (property == AssistantNameProperty)
        {
            ApplyLabels();
            ApplyIdentity();
        }
        else if (property == ModelNameProperty || property == AssistantMarkProperty)
        {
            ApplyIdentity();
            SyncPageView();
        }
        else if (property == IsOpenProperty)
        {
            OnIsOpenChanged(change.GetNewValue<bool>());
        }
        else if (property == ResponderProperty)
        {
            UpdateBusy();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A panel that leaves the window stops its answer (Arc's unmount aborts the request).
        CancelRun(announce: false);
        UnwatchPage();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchPage();
    }

    #endregion

    #region Page

    private AiPanelPage? _watchedPage;

    /// <summary>A new page object: the thread, the undo and the selection start over (spec 2.2).</summary>
    private void OnPageChanged(AiPanelPage? oldPage, AiPanelPage? newPage)
    {
        _ = oldPage;
        UnwatchPage();
        WatchPage();
        CancelRun(announce: false);
        Turns.Clear();
        _used.Clear();
        SetLastApplied(null);
        if (Selection is not null)
        {
            SetSelection(null, animate: false);
        }
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = Labels.EmptyTitle(newPage?.Title ?? string.Empty);
        }
        RefreshSuggestions();
    }

    private void WatchPage()
    {
        var page = TopLevel.GetTopLevel(this) is not null ? Page : null;
        if (ReferenceEquals(page, _watchedPage))
        {
            return;
        }
        UnwatchPage();
        _watchedPage = page;
        if (page is not null)
        {
            page.PropertyChanged += OnPagePropertyChanged;
        }
    }

    private void UnwatchPage()
    {
        if (_watchedPage is not null)
        {
            _watchedPage.PropertyChanged -= OnPagePropertyChanged;
            _watchedPage = null;
        }
    }

    private void OnPagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiPanelPage.TitleProperty && _emptyTitle is not null)
        {
            _emptyTitle.Text = Labels.EmptyTitle(Page?.Title ?? string.Empty);
        }
    }

    #endregion

    #region Open, close, sheet

    /// <summary>Opens the assistant (docked, or the sheet pulled up) and puts the focus in its box.</summary>
    public void Open()
    {
        SetCurrentValue(IsOpenProperty, true);
        FocusBoxSoon();
    }

    /// <summary>Closes the assistant; the focus moves to the reopen pill if it was in the assistant.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    private void OnIsOpenChanged(bool open)
    {
        PseudoClasses.Set(OpenPseudoClass, open);
        Announce(Labels.AnnounceOpen(open, AssistantName));
    }

    private void OnDockPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiPanelDock.IsSheetModeProperty)
        {
            SyncSheetMode();
        }
    }

    /// <summary>The sheet follows the dock: our read-only property, <c>:sheet</c>, the page's tighter padding.</summary>
    private void SyncSheetMode()
    {
        var sheet = _dock?.IsSheetMode ?? false;
        IsSheetMode = sheet;
        PseudoClasses.Set(SheetPseudoClass, sheet);
        if (_pageView is not null)
        {
            _pageView.IsCompact = sheet;
        }
        if (_close is not null)
        {
            _close.Glyph = sheet ? ArcIcons.X : ArcIcons.PanelRightClose;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        // The dock closes and moves the focus to its reopen pill.
        _close?.RaiseEvent(new RoutedEventArgs(AiPanelDock.CloseRequestedEvent, _close));
    }

    /// <summary>The reopen pill opened the assistant: the box takes the focus (spec 6.3).</summary>
    private void OnFocusPanelRequested(object? sender, RoutedEventArgs e)
    {
        if (_box is not null && _box.FocusInput(FocusMethod))
        {
            e.Handled = true;
        }
    }

    /// <summary>Focuses the box once the opened assistant is laid out (a panel out of sight has no templates yet).</summary>
    private void FocusBoxSoon()
    {
        if (_box is null || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        UpdateLayout();
        _box.FocusInput(FocusMethod);
    }

    #endregion

    #region Selection

    private void OnSelectionRequested(object? sender, AiPanelSelectionEventArgs e)
    {
        if (e.Source is not AiPanelPageView)
        {
            return;
        }
        e.Handled = true;
        SetSelection(e.Selection, animate: true);
        RaiseEvent(new AiPanelSelectionEventArgs(SelectionAskedEvent, this, e.Selection));
        Announce(Labels.AnnounceSelection(e.Selection.Label));
        if (!IsOpen)
        {
            // Opening also pulls a closed sheet up.
            SetCurrentValue(IsOpenProperty, true);
        }
        FocusBoxSoon();
    }

    /// <summary>Removes the selection pill and the block's marker; the next question is about the whole page.</summary>
    public void ClearSelection()
    {
        if (Selection is null)
        {
            return;
        }
        SetSelection(null, animate: true);
        Announce(Labels.AnnounceSelectionRemoved);
    }

    private void OnSelectionRemoveRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ClearSelection();
        _box?.FocusInput(FocusMethod);
    }

    /// <summary>One selection at most: a new one replaces the old (its pill fades out while the new one fades in).</summary>
    private void SetSelection(AiPanelSelection? selection, bool animate)
    {
        Selection = selection;
        PseudoClasses.Set(SelectionPseudoClass, selection is not null);
        if (_pageView is not null)
        {
            _pageView.MarkedBlockId = selection?.BlockId;
        }
        ShowSelection(selection, animate);
        RefreshPlaceholder();
    }

    private void ShowSelection(AiPanelSelection? selection, bool animate)
    {
        if (_selectionPill is null)
        {
            return;
        }
        if (selection is not null)
        {
            _selectionPill.Text = Labels.SelectionPill(selection.Label);
            _selectionPill.FullText = selection.Text;
            _selectionPill.RemoveName = Labels.RemoveSelection(selection.Label);
        }
        if (_selectionFade is null)
        {
            return;
        }
        if (!animate)
        {
            // Replace the presence so the pill shows or hides at once.
            _selectionFade.Dispose();
            _selectionFade = new AiFadePresence(_selectionPill, selection is not null);
            return;
        }
        _selectionFade.SetShown(selection is not null);
    }

    #endregion

    #region Box

    private void OnBoxSendRequested(object? sender, AiPanelSendEventArgs e)
    {
        e.Handled = true;
        if (IsBusy || Responder is null)
        {
            return;
        }
        _box?.SetCurrentValue(AiPanelBox.TextProperty, string.Empty);
        _ = AskAsync(e.Text);
    }

    private void OnBoxStopRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Stop();
    }

    #endregion
}
