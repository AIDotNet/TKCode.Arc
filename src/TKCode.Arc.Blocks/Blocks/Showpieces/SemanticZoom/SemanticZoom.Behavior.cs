using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class SemanticZoom : ISemanticZoomStageHost
{
    /// <summary>Inside widths above this put <see cref="SemanticZoomPanelPlacement.Auto"/>'s panel beside the map.</summary>
    internal const double SideBreakpoint = 900;

    /// <summary>Controls narrower than this are compact (smaller safe area, icon-only tour button, small minimap).</summary>
    internal const double CompactBreakpoint = 600;

    private ContentPresenter? _title;
    private TextBlock? _subtitle;
    private SemanticZoomBodyPanel? _body;
    private SemanticZoomStage? _stage;
    private SemanticZoomCrumbs? _crumbs;
    private SemanticZoomTourButton? _tourButton;
    private ShowpieceHoverCard? _card;
    private SemanticZoomCard? _cardContent;
    private Border? _minimapFrame;
    private SemanticZoomMinimap? _minimap;
    private TextBlock? _hint;
    private ShowpieceZoomControls? _zoomBar;
    private ShowpieceLiveRegion? _live;
    private SemanticZoomHiddenTable? _table;
    private SemanticZoomPanel? _panel;

    private SemanticZoomOrganization? _org;
    // True while in a visual tree (the organization's events are watched only then).
    private bool _watching;
    // The organization's revision when the map was detached: a different one on return means the map is stale.
    private int? _revisionAtDetach;
    private SemanticZoomLayout _layout = SemanticZoomLayout.Empty;
    private SemanticZoomLabels _labels = SemanticZoomLabels.Empty;
    private readonly SemanticZoomTour _tour = new();
    private readonly LiveLoop _tourLoop;
    private List<SemanticZoomNode> _tourStops = new();
    private int[] _candidates = Array.Empty<int>();
    private Point[] _candidateCenters = Array.Empty<Point>();
    private bool _dirty = true;
    private bool _structurePosted;
    private bool _syncingScope;
    private bool _syncingTour;
    private bool _touring;
    private SemanticZoomNode? _pendingScope;
    private bool _userDriven;
    private bool _announcePending;
    private bool _cardFromCursor;
    private bool _compact;
    private bool _side;
    private int _readoutKey = int.MinValue;
    private SemanticZoomScope _scope = SemanticZoomScope.Organization;

    static SemanticZoom()
    {
        FocusableProperty.OverrideDefaultValue<SemanticZoom>(false);
        AffectsMeasure<SemanticZoom>(DetailPanelPlacementProperty, StageHeightProperty);
    }

    public SemanticZoom()
    {
        SetCurrentValue(TextsProperty, new SemanticZoomTexts());
        FlyToCommand = new SemanticZoomCommand(p => FlyTo(p as SemanticZoomNode ?? Organization), p => p is SemanticZoomNode);
        ResetViewCommand = new SemanticZoomCommand(_ => ResetView(), _ => true);
        ToggleTourCommand = new SemanticZoomCommand(_ => ToggleTour(), _ => true);
        // The tour's clock: a frame loop that only counts time (it draws nothing), so a dwell costs no redraws. It runs
        // at every motion level because the tour still plays at reduced motion (as jumps).
        _tourLoop = new LiveLoop(this, OnTourFrame) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <summary>The stage (tests and the snapshot harness).</summary>
    internal SemanticZoomStage? Stage => _stage;

    /// <summary>The laid-out map (tests).</summary>
    internal SemanticZoomLayout MapLayout => _layout;

    /// <summary>The map's labels (tests).</summary>
    internal SemanticZoomLabels Labels => _labels;

    /// <summary>The hover card (tests).</summary>
    internal ShowpieceHoverCard? Card => _card;

    /// <summary>The hover card's content (tests).</summary>
    internal SemanticZoomCard? CardContent => _cardContent;

    /// <summary>The breadcrumbs (tests).</summary>
    internal SemanticZoomCrumbs? Crumbs => _crumbs;

    /// <summary>The details panel (tests).</summary>
    internal SemanticZoomPanel? DetailPanel => _panel;

    /// <summary>The minimap (tests).</summary>
    internal SemanticZoomMinimap? Minimap => _minimap;

    /// <summary>The hidden project table (tests).</summary>
    internal SemanticZoomHiddenTable? Table => _table;

    /// <summary>The live region's last announcement (tests).</summary>
    internal string? LastAnnouncement => _live?.LastAnnouncement;

    /// <summary>The tour clock (tests).</summary>
    internal SemanticZoomTour Tour => _tour;

    /// <summary>The checklist item under the keyboard cursor, or null.</summary>
    internal SemanticZoomChecklistItem? CursorItem =>
        _stage is { CursorItem: >= 0 } stage && _scope.Task >= 0 && _layout.Boxes[_scope.Task].Node is SemanticZoomTask task &&
        stage.CursorItem < task.Checklist.Count
            ? task.Checklist[stage.CursorItem]
            : null;

    private SemanticZoomTexts TextsOrDefault => Texts ?? SemanticZoomTexts.Default;

    private ISemanticZoomFormatter FormatterOrDefault => Formatter ?? SemanticZoomFormatter.Default;

    private bool Animates => ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    // Template ---------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stage is not null)
        {
            _stage.Host = null;
        }
        if (_crumbs is not null)
        {
            _crumbs.Chosen -= OnCrumbChosen;
        }
        if (_tourButton is not null)
        {
            _tourButton.Click -= OnTourClick;
        }
        if (_minimap is not null)
        {
            _minimap.Host = null;
        }
        var scope = e.NameScope;
        _title = scope.Find<ContentPresenter>(TitlePart);
        _subtitle = scope.Find<TextBlock>(SubtitlePart);
        _body = scope.Find<SemanticZoomBodyPanel>(BodyPart);
        _stage = scope.Find<SemanticZoomStage>(StagePart);
        _crumbs = scope.Find<SemanticZoomCrumbs>(CrumbsPart);
        _tourButton = scope.Find<SemanticZoomTourButton>(TourPart);
        _card = scope.Find<ShowpieceHoverCard>(CardPart);
        _cardContent = scope.Find<SemanticZoomCard>(CardContentPart);
        _minimapFrame = scope.Find<Border>(MinimapFramePart);
        _minimap = scope.Find<SemanticZoomMinimap>(MinimapPart);
        _hint = scope.Find<TextBlock>(HintPart);
        _zoomBar = scope.Find<ShowpieceZoomControls>(ZoomPart);
        _live = scope.Find<ShowpieceLiveRegion>(LivePart);
        _table = scope.Find<SemanticZoomHiddenTable>(TablePart);
        _panel = scope.Find<SemanticZoomPanel>(PanelPart);
        if (_stage is not null)
        {
            _stage.Host = this;
            _stage.MaxZoom = MaxZoom;
        }
        if (_crumbs is not null)
        {
            _crumbs.Chosen += OnCrumbChosen;
        }
        if (_tourButton is not null)
        {
            _tourButton.Click += OnTourClick;
        }
        if (_minimap is not null)
        {
            _minimap.Host = this;
        }
        if (_card is not null)
        {
            _card.Placement = ShowpieceCardPlacement.SemanticZoom;
        }
        if (_zoomBar is not null)
        {
            _zoomBar.Stage = _stage;
        }
        ApplyTexts();
        _dirty = true;
        Rebuild(keepView: false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_dirty && _stage is not null)
        {
            Rebuild(keepView: true);
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : Bounds.Width;
        ApplyWidth(width);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Applies the width rules: compact under 600 px, the panel beside the map above 900 px inside.</summary>
    private void ApplyWidth(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        var compact = width < CompactBreakpoint;
        var inner = Math.Max(0, width - BorderThickness.Left - BorderThickness.Right);
        var placement = DetailPanelPlacement;
        var side = placement == SemanticZoomPanelPlacement.Side ||
                   placement == SemanticZoomPanelPlacement.Auto && inner > SideBreakpoint;
        if (placement == SemanticZoomPanelPlacement.None)
        {
            side = false;
        }
        _compact = compact;
        _side = side;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(SidePanelPseudoClass, side);
        if (_body is not null)
        {
            _body.IsSide = side;
            _body.ShowsPanel = placement != SemanticZoomPanelPlacement.None;
            _body.StageHeight = StageHeight ?? (compact ? SemanticZoomBodyPanel.CompactStageHeight : SemanticZoomBodyPanel.StageHeightFor(inner));
        }
        if (_stage is not null)
        {
            _stage.IsCompact = compact;
        }
        if (_zoomBar is not null)
        {
            _zoomBar.IsCompact = compact;
        }
        if (_tourButton is not null)
        {
            _tourButton.IsCompact = compact;
        }
        if (_minimap is not null)
        {
            _minimap.IsCompact = compact;
        }
        if (_panel is not null)
        {
            _panel.Layout = side || compact ? SemanticZoomPanelLayout.Stack : SemanticZoomPanelLayout.Grid;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrganizationProperty)
        {
            AttachOrganization(change.GetNewValue<SemanticZoomOrganization?>());
        }
        else if (change.Property == ScopeProperty)
        {
            if (!_syncingScope)
            {
                var node = change.GetNewValue<SemanticZoomNode?>() ?? Organization;
                if (_stage is null || _dirty)
                {
                    // Not laid out yet (a Scope set in XAML): go there without a flight once the map exists.
                    _pendingScope = node;
                }
                else
                {
                    FlyTo(node);
                }
            }
        }
        else if (change.Property == MaxZoomProperty)
        {
            if (_stage is not null)
            {
                _stage.MaxZoom = MaxZoom;
            }
        }
        else if (change.Property == IsTourPlayingProperty)
        {
            if (!_syncingTour)
            {
                if (change.GetNewValue<bool>())
                {
                    StartTour();
                }
                else
                {
                    StopTour();
                }
            }
        }
        else if (change.Property == TextsProperty || change.Property == FormatterProperty)
        {
            ApplyTexts();
            Relabel();
        }
        else if (change.Property == SubtitleProperty || change.Property == SubtitleFormatProperty)
        {
            UpdateSubtitle();
        }
        else if (change.Property == HeaderProperty)
        {
            if (_title is not null)
            {
                _title.IsVisible = change.NewValue is not null;
            }
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            _tour.IsReduced = !Animates;
        }
    }

    // Data -------------------------------------------------------------------------------------------------------------

    private void AttachOrganization(SemanticZoomOrganization? organization)
    {
        WatchOrganization(false);
        _org = organization;
        WatchOrganization(_watching);
        _revisionAtDetach = null;
        if (IsTourPlaying)
        {
            StopTour();
        }
        _dirty = true;
        Rebuild(keepView: false);
    }

    /// <summary>
    /// The organization usually outlives the map (a view model's, or shared sample data): the map listens to it only while
    /// it is in a visual tree, so a map that was taken out can be collected.
    /// </summary>
    private void WatchOrganization(bool watch)
    {
        if (_org is null)
        {
            return;
        }
        _org.StructureChanged -= OnStructureChanged;
        _org.ValuesChanged -= OnValuesChanged;
        if (watch)
        {
            _org.StructureChanged += OnStructureChanged;
            _org.ValuesChanged += OnValuesChanged;
        }
    }

    private void OnStructureChanged()
    {
        _dirty = true;
        if (_structurePosted)
        {
            return;
        }
        _structurePosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _structurePosted = false;
            if (_dirty)
            {
                Rebuild(keepView: true);
            }
        }, DispatcherPriority.Normal);
        InvalidateMeasure();
    }

    private void OnValuesChanged(SemanticZoomNode node)
    {
        if (_dirty)
        {
            return;
        }
        // Rebuild the labels of the node and everything above it (their totals changed with it).
        var texts = TextsOrDefault;
        var formatter = FormatterOrDefault;
        for (var n = node; n is not null; n = n.Parent)
        {
            var box = _layout.IndexOf(n);
            if (box >= 0)
            {
                _labels.Update(_layout, box, texts, formatter);
            }
        }
        _stage?.InvalidateVisual();
        _minimap?.InvalidateVisual();
        UpdateTable();
        UpdateSubtitle();
        RefreshCard();
    }

    /// <summary>
    /// Lays the map out again from <see cref="Organization"/> (after changing names or values the map cannot observe, such
    /// as a person's name) and rebuilds every label. The view is kept.
    /// </summary>
    public void Refresh()
    {
        _dirty = true;
        Rebuild(keepView: true);
    }

    private void Rebuild(bool keepView)
    {
        if (_stage is null)
        {
            return;
        }
        _dirty = false;
        _layout = SemanticZoomLayout.Build(_org);
        _labels = SemanticZoomLabels.Build(_layout, TextsOrDefault, FormatterOrDefault);
        _candidates = new int[Math.Max(16, _layout.Boxes.Length)];
        _candidateCenters = new Point[_candidates.Length + 16];
        _stage.Labels = _labels;
        _stage.Load(_layout, keepView);
        _minimap?.InvalidateVisual();
        UpdateSubtitle();
        UpdateTable();
        if (_panel is not null)
        {
            _panel.Texts = TextsOrDefault;
            _panel.Formatter = FormatterOrDefault;
        }
        HideCard();
        CursorNode = null;
        OnStageScopeChanged(_stage.CurrentScope);
        OnStageCamera();
        _announcePending = false;
        if (_pendingScope is { } pending)
        {
            _pendingScope = null;
            FlyTo(pending, animate: false);
        }
    }

    private void Relabel()
    {
        if (_stage is null || _dirty)
        {
            return;
        }
        _labels = SemanticZoomLabels.Build(_layout, TextsOrDefault, FormatterOrDefault);
        _stage.Labels = _labels;
        _minimap?.InvalidateVisual();
        UpdateTable();
        UpdateSubtitle();
        if (_panel is not null)
        {
            _panel.Texts = TextsOrDefault;
            _panel.Formatter = FormatterOrDefault;
        }
        _crumbs?.SetPath(ScopePath);
        RefreshCard();
    }

    private void ApplyTexts()
    {
        var texts = TextsOrDefault;
        if (_zoomBar is not null)
        {
            _zoomBar.ZoomInText = texts.ZoomIn;
            _zoomBar.ZoomOutText = texts.ZoomOut;
            _zoomBar.FitText = SemanticZoomTexts.Format(texts.FitFormat, Organization?.Title ?? string.Empty);
        }
        if (_tourButton is not null)
        {
            _tourButton.StartText = texts.StartTour;
            _tourButton.StopText = texts.StopTour;
        }
        if (_hint is not null)
        {
            _hint.Text = texts.PointerHint;
        }
        if (_crumbs is not null)
        {
            AutomationProperties.SetName(_crumbs, texts.BreadcrumbLabel);
        }
        if (_minimap is not null)
        {
            AutomationProperties.SetName(_minimap, texts.MinimapLabel);
        }
        if (_stage is not null)
        {
            _stage.RoleDescription = texts.StageRole;
            AutomationProperties.SetHelpText(_stage, texts.StageHelp);
        }
        if (_title is not null)
        {
            _title.IsVisible = Header is not null;
        }
    }

    private void UpdateSubtitle()
    {
        if (_subtitle is null)
        {
            return;
        }
        var text = Subtitle ?? (_org is null
            ? string.Empty
            : SemanticZoomTexts.Format(SubtitleFormat, _org.Teams.Count, _org.ProjectCount, _org.TaskCount));
        _subtitle.Text = text;
        _subtitle.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void UpdateTable()
    {
        if (_table is null)
        {
            return;
        }
        var texts = TextsOrDefault;
        var formatter = FormatterOrDefault;
        var rows = new List<string>(_layout.ProjectCount);
        for (var i = _layout.FirstProject; i < _layout.FirstTask; i++)
        {
            var project = (SemanticZoomProject)_layout.Boxes[i].Node;
            rows.Add(SemanticZoomTexts.Format(texts.TableRowFormat, project.Title, project.Team?.Title ?? string.Empty,
                texts.HealthName(project.Health), formatter.FormatPercent(project.Progress), project.OpenTasks, project.BlockedTasks,
                project.Due is { } due ? formatter.FormatDate(due) : texts.NoneText));
        }
        _table.SetRows(SemanticZoomTexts.Format(texts.TableCaptionFormat, _org?.Title ?? string.Empty), rows);
    }

    // Stage host ------------------------------------------------------------------------------------------------------

    void ISemanticZoomStageHost.OnStageScopeChanged(SemanticZoomScope scope) => OnStageScopeChanged(scope);

    private void OnStageScopeChanged(SemanticZoomScope scope)
    {
        var old = Scope;
        _scope = scope;
        var node = scope.Deepest >= 0 ? _layout.Boxes[scope.Deepest].Node : _org;
        var path = node?.GetPath() ?? (IReadOnlyList<SemanticZoomNode>)Array.Empty<SemanticZoomNode>();
        _syncingScope = true;
        try
        {
            SetCurrentValue(ScopeProperty, node);
        }
        finally
        {
            _syncingScope = false;
        }
        ScopePath = path;
        _crumbs?.SetPath(path);
        if (_panel is not null)
        {
            _panel.Scope = node;
        }
        _minimap?.InvalidateVisual();
        if (CursorNode is not null && _stage is not null && _stage.Scene.Cursor < 0)
        {
            CursorNode = null;
        }
        if (!ReferenceEquals(old, node))
        {
            if (_cardFromCursor)
            {
                HideCard();
            }
            _announcePending = true;
            RaiseEvent(new SemanticZoomScopeChangedEventArgs(ScopeChangedEvent, old, node, path, _userDriven));
        }
    }

    void ISemanticZoomStageHost.OnStageCamera() => OnStageCamera();

    private void OnStageCamera()
    {
        if (_stage is null)
        {
            return;
        }
        var camera = _stage.View;
        var zoom = camera.Zoom;
        Zoom = zoom;
        var key = zoom < 9.95 ? (int)Math.Round(zoom * 10) : 1000 + (int)Math.Round(zoom);
        if (key != _readoutKey)
        {
            _readoutKey = key;
            ZoomText = ShowpieceFormat.ZoomReadout(zoom);
        }
        var fit = zoom < 1.0005 && Math.Abs(camera.TargetZoom - 1) < 1e-3;
        PseudoClasses.Set(FitPseudoClass, fit);
        if (_zoomBar is not null)
        {
            _zoomBar.Zoom = zoom;
            _zoomBar.IsAtFit = fit;
        }
        _minimap?.InvalidateVisual();
        if (_cardFromCursor)
        {
            PlaceCursorCard();
        }
        if (!camera.IsMoving)
        {
            _userDriven = false;
            if (_announcePending)
            {
                _announcePending = false;
                Announce(SemanticZoomTexts.Format(TextsOrDefault.AnnounceScopeFormat, Describe(Scope)));
            }
        }
    }

    void ISemanticZoomStageHost.OnStageHover(Point? position, object? target)
    {
        if (position is not { } p || target is not SemanticZoomNode node)
        {
            if (!_cardFromCursor)
            {
                HideCard();
            }
            return;
        }
        _cardFromCursor = false;
        ShowCard(node, p);
    }

    void ISemanticZoomStageHost.OnStageTap(object? target)
    {
        MarkInteracted();
        switch (target)
        {
            case SemanticZoomNode node:
                StopTourByUser();
                Invoke(node);
                break;
            case SemanticZoomChecklistItem item:
                StopTourByUser();
                Toggle(item);
                break;
        }
    }

    void ISemanticZoomStageHost.OnStageInput()
    {
        MarkInteracted();
        StopTourByUser();
        _userDriven = true;
    }

    void ISemanticZoomStageHost.OnStageReset()
    {
        MarkInteracted();
        StopTourByUser();
        _userDriven = true;
        ResetView();
    }

    bool ISemanticZoomStageHost.OnStageKey(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.Left:
                MarkInteracted();
                return MoveCursor(SemanticZoomDirection.Left);
            case Key.Right:
                MarkInteracted();
                return MoveCursor(SemanticZoomDirection.Right);
            case Key.Up:
                MarkInteracted();
                return MoveCursor(SemanticZoomDirection.Up);
            case Key.Down:
                MarkInteracted();
                return MoveCursor(SemanticZoomDirection.Down);
            case Key.Enter:
            case Key.Space:
                MarkInteracted();
                return ActivateCursor();
            case Key.Escape:
                if (IsTourPlaying)
                {
                    StopTourByUser();
                    return true;
                }
                if (CursorNode is not null || CursorItem is not null)
                {
                    ClearCursor();
                    return true;
                }
                return FlyOutByUser();
            case Key.Back:
                MarkInteracted();
                StopTourByUser();
                return FlyOutByUser();
        }
        return false;
    }

    string? ISemanticZoomStageHost.StageSummary => StageSummary();

    private string StageSummary()
    {
        var texts = TextsOrDefault;
        var org = _org;
        if (org is null)
        {
            return texts.StageRole;
        }
        return SemanticZoomTexts.Format(texts.StageLabelFormat, org.Title, org.Teams.Count, org.ProjectCount, org.TaskCount,
            Scope?.Title ?? org.Title);
    }

    // Public operations ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Flies the camera so <paramref name="node"/> fills the view (the organization shows everything). Without
    /// <paramref name="animate"/>, or at reduced motion, the view jumps there.
    /// </summary>
    public void FlyTo(SemanticZoomNode? node, bool animate = true)
    {
        if (_stage is null)
        {
            return;
        }
        if (node is null || node is SemanticZoomOrganization)
        {
            _stage.FitView(animate);
            return;
        }
        var box = _layout.IndexOf(node);
        if (box >= 0)
        {
            _stage.FlyToBox(box, animate);
        }
    }

    /// <summary>Zooms out one level: from a task to its project, a project to its team, a team to the whole organization.</summary>
    public void FlyOut()
    {
        if (_stage is null)
        {
            return;
        }
        var scope = _stage.CurrentScope;
        if (scope.Level == SemanticZoomLevel.Organization)
        {
            if (!_stage.View.IsAtFit)
            {
                _stage.FitView();
            }
            return;
        }
        var parent = scope.Parent;
        if (parent.Level == SemanticZoomLevel.Organization)
        {
            _stage.FitView();
        }
        else
        {
            _stage.FlyToBox(parent.Deepest);
        }
    }

    /// <summary>Shows the whole organization (animated at full motion unless <paramref name="animate"/> is false).</summary>
    public void ResetView(bool animate = true) => _stage?.FitView(animate);

    /// <summary>Zooms by <paramref name="factor"/> about <paramref name="anchor"/> (map px; default the center of the view).</summary>
    public void ZoomBy(double factor, Point? anchor = null) => _stage?.ZoomAbout(factor, anchor);

    /// <summary>Starts the tour from its first stop.</summary>
    public void StartTour()
    {
        if (_stage is null || _org is null)
        {
            SyncTourPlaying(false);
            return;
        }
        _tourStops = TourStops is { Count: > 0 } stops ? stops.ToList() : SemanticZoomTour.DefaultStops(_org);
        _tour.DwellSeconds = Math.Max(0, TourDwell.TotalSeconds);
        _tour.IsReduced = !Animates;
        ClearCursor();
        HideCard();
        if (_tour.Start(_tourStops.Count) != SemanticZoomTourAction.GoToStop)
        {
            SyncTourPlaying(false);
            return;
        }
        _touring = true;
        SyncTourPlaying(true);
        PseudoClasses.Set(TouringPseudoClass, true);
        MarkInteracted();
        RaiseEvent(new RoutedEventArgs(TourStartedEvent));
        Announce(TextsOrDefault.AnnounceTourStarted);
        GoToStop(0);
        _tourLoop.Start();
    }

    /// <summary>Stops the tour; the camera stays where it is.</summary>
    public void StopTour() => EndTour(completed: false, stopCamera: true);

    /// <summary>Starts the tour, or stops it while it plays.</summary>
    public void ToggleTour()
    {
        if (_touring)
        {
            StopTour();
        }
        else
        {
            StartTour();
        }
    }

    /// <summary>
    /// Moves the keyboard cursor to the nearest item in <paramref name="direction"/> among the current item's children and
    /// siblings (in a task: its checklist items). The first move picks the first child. Returns false when nothing lies
    /// that way.
    /// </summary>
    public bool MoveCursor(SemanticZoomDirection direction)
    {
        if (_stage is null || _layout.IsEmpty)
        {
            return false;
        }
        var scene = _stage.Scene;
        var count = scene.CandidateCount(_candidates);
        count = Math.Min(count, _candidates.Length);
        var items = _stage.ChecklistRowsShown();
        var total = items + count;
        if (total == 0)
        {
            return false;
        }
        var camera = _stage.View;
        // Checklist rows first (in a task they are the children), then the node candidates.
        for (var i = 0; i < items; i++)
        {
            _candidateCenters[i] = camera.ScreenToWorld(_stage.ChecklistItemRect(i).Center);
        }
        for (var i = 0; i < count; i++)
        {
            _candidateCenters[items + i] = _layout.Boxes[_candidates[i]].Rect.Center;
        }
        var current = -1;
        if (_stage.CursorItem >= 0 && _stage.CursorItem < items)
        {
            current = _stage.CursorItem;
        }
        else if (scene.Cursor >= 0)
        {
            var at = Array.IndexOf(_candidates, scene.Cursor, 0, count);
            current = at >= 0 ? items + at : -1;
        }
        int next;
        if (current < 0)
        {
            next = 0;
        }
        else
        {
            next = SemanticZoomScene.Nearest(_candidateCenters.AsSpan(0, total), _candidateCenters[current], direction, current);
            if (next < 0)
            {
                return true;
            }
        }
        if (next < items)
        {
            _stage.SetCursor(-1, next);
            CursorNode = null;
            HideCard();
            var item = CursorItem;
            if (item is not null)
            {
                var texts = TextsOrDefault;
                Announce(SemanticZoomTexts.Format(texts.AnnounceItemFormat, item.Text, item.IsDone ? texts.TickedText : texts.NotTickedText));
            }
        }
        else
        {
            var box = _candidates[next - items];
            _stage.SetCursor(box);
            var node = _layout.Boxes[box].Node;
            CursorNode = node;
            _cardFromCursor = true;
            PlaceCursorCard();
            Announce(SemanticZoomTexts.Format(TextsOrDefault.AnnounceCursorFormat, Describe(node)));
        }
        return true;
    }

    private bool ActivateCursor()
    {
        if (CursorItem is { } item)
        {
            StopTourByUser();
            Toggle(item);
            return true;
        }
        if (CursorNode is { } node)
        {
            StopTourByUser();
            ClearCursor();
            Invoke(node);
            return true;
        }
        return false;
    }

    private void ClearCursor()
    {
        _stage?.SetCursor(-1);
        CursorNode = null;
        if (_cardFromCursor)
        {
            HideCard();
        }
    }

    private bool FlyOutByUser()
    {
        if (_stage is null)
        {
            return false;
        }
        var scope = _stage.CurrentScope;
        if (scope.Level == SemanticZoomLevel.Organization && _stage.View.IsAtFit)
        {
            return false;
        }
        _userDriven = true;
        MarkInteracted();
        FlyOut();
        return true;
    }

    private void Invoke(SemanticZoomNode node)
    {
        var args = new SemanticZoomNodeEventArgs(NodeInvokedEvent, node);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        _userDriven = true;
        HideCard();
        FlyTo(node);
    }

    private void Toggle(SemanticZoomChecklistItem item)
    {
        var task = item.Task;
        item.IsDone = !item.IsDone;
        if (task is null)
        {
            return;
        }
        RaiseEvent(new SemanticZoomChecklistEventArgs(ChecklistItemToggledEvent, task, item, item.IsDone));
        var texts = TextsOrDefault;
        Announce(item.IsDone
            ? SemanticZoomTexts.Format(texts.AnnounceTickedFormat, item.Text, task.Title, task.ChecklistDone, task.Checklist.Count)
            : SemanticZoomTexts.Format(texts.AnnounceUntickedFormat, item.Text, task.Title, task.ChecklistDone, task.Checklist.Count,
                texts.StatusName(task.Status).ToLowerInvariant()));
    }

    private void MarkInteracted() => PseudoClasses.Set(InteractedPseudoClass, true);

    // Tour -------------------------------------------------------------------------------------------------------------

    private void OnTourClick(object? sender, RoutedEventArgs e)
    {
        MarkInteracted();
        ToggleTour();
    }

    private bool OnTourFrame(double dt)
    {
        AdvanceTour(dt);
        return _tour.IsPlaying;
    }

    /// <summary>Advances the tour clock (the frame loop; tests call it directly).</summary>
    internal void AdvanceTour(double dt)
    {
        if (!_tour.IsPlaying || _stage is null)
        {
            return;
        }
        var action = _tour.Advance(dt, _stage.View.IsMoving);
        if (action == SemanticZoomTourAction.GoToStop)
        {
            GoToStop(_tour.StopIndex);
        }
        else if (action == SemanticZoomTourAction.Finished)
        {
            EndTour(completed: true, stopCamera: false);
        }
    }

    private void GoToStop(int index)
    {
        if (index < 0 || index >= _tourStops.Count)
        {
            return;
        }
        _userDriven = false;
        FlyTo(_tourStops[index]);
    }

    private void StopTourByUser()
    {
        if (_touring)
        {
            // Manual input takes the camera over: the tour stops, the input keeps the camera's motion.
            EndTour(completed: false, stopCamera: false);
        }
    }

    private void EndTour(bool completed, bool stopCamera)
    {
        var wasPlaying = _touring;
        _touring = false;
        _tour.Stop();
        _tourLoop.Stop();
        SyncTourPlaying(false);
        PseudoClasses.Set(TouringPseudoClass, false);
        if (!wasPlaying)
        {
            return;
        }
        if (stopCamera && _stage is not null && _stage.View.IsFlying)
        {
            _stage.View.Stop();
        }
        RaiseEvent(new SemanticZoomTourStoppedEventArgs(TourStoppedEvent, completed));
        Announce(TextsOrDefault.AnnounceTourStopped);
    }

    private void SyncTourPlaying(bool playing)
    {
        _syncingTour = true;
        try
        {
            SetCurrentValue(IsTourPlayingProperty, playing);
            if (_tourButton is not null)
            {
                _tourButton.IsChecked = playing;
            }
        }
        finally
        {
            _syncingTour = false;
        }
    }

    // Breadcrumbs and minimap -------------------------------------------------------------------------------------------

    private void OnCrumbChosen(object? sender, SemanticZoomNode node)
    {
        MarkInteracted();
        StopTourByUser();
        _userDriven = true;
        FlyTo(node);
    }

    /// <summary>The minimap was pressed or dragged at a world point.</summary>
    internal void OnMinimapCenter(Point world)
    {
        MarkInteracted();
        StopTourByUser();
        _userDriven = true;
        _stage?.CenterOn(world);
    }

    // Hover card -------------------------------------------------------------------------------------------------------

    private void ShowCard(SemanticZoomNode node, Point anchor)
    {
        if (_card is null || _cardContent is null)
        {
            return;
        }
        _cardContent.Show(node, TextsOrDefault, FormatterOrDefault);
        _card.ShowAt(anchor);
    }

    private void PlaceCursorCard()
    {
        if (_stage is null || CursorNode is not { } node)
        {
            return;
        }
        var box = _layout.IndexOf(node);
        if (box < 0)
        {
            return;
        }
        var rect = _stage.BoxOnScreen(box);
        // Beside the item's top-right corner, its top level with the item's (it flips at the stage edges like a pointer card).
        ShowCard(node, new Point(rect.Right - 6, rect.Top - 18));
    }

    private void HideCard()
    {
        _cardFromCursor = false;
        if (_card is not null)
        {
            _card.IsOpen = false;
        }
    }

    private void RefreshCard()
    {
        if (_card is { IsOpen: true } && _cardContent?.Node is { } node)
        {
            _cardContent.Show(node, TextsOrDefault, FormatterOrDefault);
        }
    }

    // Announcements ------------------------------------------------------------------------------------------------------

    private void Announce(string text) => _live?.Announce(text);

    /// <summary>The spoken summary of a node (live region and cursor).</summary>
    internal string Describe(SemanticZoomNode? node)
    {
        var texts = TextsOrDefault;
        var formatter = FormatterOrDefault;
        switch (node)
        {
            case SemanticZoomOrganization org:
                return SemanticZoomTexts.Format(texts.AnnounceOrganizationFormat, org.Title, org.Teams.Count, org.ProjectCount,
                    org.OpenTasks, formatter.FormatPercent(org.Progress));
            case SemanticZoomTeam team:
                return SemanticZoomTexts.Format(texts.AnnounceTeamFormat, team.Title, team.ProjectCount, team.OpenTasks,
                    formatter.FormatPercent(team.Progress));
            case SemanticZoomProject project:
                return SemanticZoomTexts.Format(texts.AnnounceProjectFormat, project.Title,
                    texts.HealthName(project.Health).ToLowerInvariant(), formatter.FormatPercent(project.Progress), project.OpenTasks,
                    project.Due is { } due ? formatter.FormatDate(due) : texts.NoneText);
            case SemanticZoomTask task:
                return SemanticZoomTexts.Format(texts.AnnounceTaskFormat, task.Title, texts.StatusName(task.Status).ToLowerInvariant(),
                    task.Assignee?.Name ?? texts.NobodyText, task.Due is { } taskDue ? formatter.FormatDate(taskDue) : texts.NoneText,
                    task.ChecklistDone, task.Checklist.Count);
            default:
                return string.Empty;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchOrganization(true);
        if (_revisionAtDetach is { } seen && _org is not null && _org.Revision != seen)
        {
            // The organization changed while the map was away: catch up (labels and layout).
            OnStructureChanged();
        }
        _revisionAtDetach = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchOrganization(false);
        _revisionAtDetach = _org?.Revision;
        if (_touring)
        {
            EndTour(completed: false, stopCamera: false);
        }
    }
}

/// <summary>A small command over delegates (the control's commands).</summary>
internal sealed class SemanticZoomCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool> _canExecute;

    public SemanticZoomCommand(Action<object?> execute, Func<object?, bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add
        {
        }
        remove
        {
        }
    }

    public bool CanExecute(object? parameter) => _canExecute(parameter);

    public void Execute(object? parameter)
    {
        if (_canExecute(parameter))
        {
            _execute(parameter);
        }
    }
}
