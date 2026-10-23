using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// An organization map you can zoom into without losing your place: teams hold project cards, projects hold task blocks,
/// tasks open into their details and checklist. Each zoom level shows different information (team leads and counts, then
/// project titles and health, then task titles and due dates, then the task's summary, assignee and checklist), not the
/// same picture larger. Scroll or pinch to zoom about the pointer, drag to pan, click anything to fly into it; the
/// breadcrumbs, the details panel and the screen-reader text follow whatever is under the center of the view. A minimap,
/// zoom buttons and a guided tour sit on the map; ticking a checklist item updates the task and every total above it.
/// </summary>
/// <remarks>
/// <para>
/// Data: <see cref="Organization"/> holds <see cref="SemanticZoomTeam"/>s with <see cref="SemanticZoomProject"/>s with
/// <see cref="SemanticZoomTask"/>s; the layout, the totals and every label are derived from it.
/// </para>
/// <para>
/// Keys on the map: arrows move a cursor between the items in view (the current item's children and siblings), Enter or
/// Space zooms into the cursor's item or ticks the checklist item under it, Escape clears the cursor and then zooms out
/// one level, Backspace zooms out one level, + and − zoom by 1.5, 0 or Home show the whole organization.
/// </para>
/// </remarks>
[TemplatePart(TitlePart, typeof(ContentPresenter))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(BodyPart, typeof(SemanticZoomBodyPanel))]
[TemplatePart(StagePart, typeof(SemanticZoomStage))]
[TemplatePart(CrumbsPart, typeof(SemanticZoomCrumbs))]
[TemplatePart(TourPart, typeof(SemanticZoomTourButton))]
[TemplatePart(CardPart, typeof(ShowpieceHoverCard))]
[TemplatePart(CardContentPart, typeof(SemanticZoomCard))]
[TemplatePart(MinimapFramePart, typeof(Border))]
[TemplatePart(MinimapPart, typeof(SemanticZoomMinimap))]
[TemplatePart(HintPart, typeof(TextBlock))]
[TemplatePart(ZoomPart, typeof(ShowpieceZoomControls))]
[TemplatePart(LivePart, typeof(ShowpieceLiveRegion))]
[TemplatePart(TablePart, typeof(SemanticZoomHiddenTable))]
[TemplatePart(PanelPart, typeof(SemanticZoomPanel))]
[PseudoClasses(FitPseudoClass, TouringPseudoClass, SidePanelPseudoClass, CompactPseudoClass, InteractedPseudoClass)]
public partial class SemanticZoom : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string BodyPart = "PART_Body";
    public const string StagePart = "PART_Stage";
    public const string CrumbsPart = "PART_Crumbs";
    public const string TourPart = "PART_Tour";
    public const string CardPart = "PART_Card";
    public const string CardContentPart = "PART_CardContent";
    public const string MinimapFramePart = "PART_MinimapFrame";
    public const string MinimapPart = "PART_Minimap";
    public const string HintPart = "PART_Hint";
    public const string ZoomPart = "PART_Zoom";
    public const string LivePart = "PART_Live";
    public const string TablePart = "PART_Table";
    public const string PanelPart = "PART_Panel";

    /// <summary>Set while the whole organization is in view (zoom 1; the fit button is disabled).</summary>
    public const string FitPseudoClass = ":fit";

    /// <summary>Set while the tour plays.</summary>
    public const string TouringPseudoClass = ":touring";

    /// <summary>Set while the details panel sits beside the map.</summary>
    public const string SidePanelPseudoClass = ":side-panel";

    /// <summary>Set in containers narrower than 600 px.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set after the first interaction with the map (the hint fades away for good).</summary>
    public const string InteractedPseudoClass = ":interacted";

    #region Properties

    /// <summary>The organization to map: its teams, their projects and tasks.</summary>
    public static readonly StyledProperty<SemanticZoomOrganization?> OrganizationProperty =
        AvaloniaProperty.Register<SemanticZoom, SemanticZoomOrganization?>(nameof(Organization));

    /// <summary>The heading above the map; hidden when null.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<SemanticZoom, object?>(nameof(Header));

    /// <summary>The line under the heading; when null it is built from <see cref="SubtitleFormat"/>; an empty string hides it.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<SemanticZoom, string?>(nameof(Subtitle));

    /// <summary>The automatic subtitle: <c>{0}</c> teams, <c>{1}</c> projects, <c>{2}</c> tasks.</summary>
    public static readonly StyledProperty<string> SubtitleFormatProperty =
        AvaloniaProperty.Register<SemanticZoom, string>(nameof(SubtitleFormat), "{0} teams, {1} projects, {2} tasks. Zoom in to see more.");

    /// <summary>
    /// The node in view (two-way): the organization, a team, a project or a task. It follows the camera (whatever is under
    /// the center at the current zoom); setting it flies there.
    /// </summary>
    public static readonly StyledProperty<SemanticZoomNode?> ScopeProperty =
        AvaloniaProperty.Register<SemanticZoom, SemanticZoomNode?>(nameof(Scope), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The chain from the organization down to <see cref="Scope"/> (what the breadcrumbs show).</summary>
    public static readonly DirectProperty<SemanticZoom, IReadOnlyList<SemanticZoomNode>> ScopePathProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoom, IReadOnlyList<SemanticZoomNode>>(nameof(ScopePath), o => o.ScopePath);

    /// <summary>The zoom relative to the whole organization in view (1 = fit).</summary>
    public static readonly DirectProperty<SemanticZoom, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoom, double>(nameof(Zoom), o => o.Zoom);

    /// <summary>The zoom as the readout shows it: one decimal under 10 ("1.6×"), whole numbers above ("31×").</summary>
    public static readonly DirectProperty<SemanticZoom, string> ZoomTextProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoom, string>(nameof(ZoomText), o => o.ZoomText);

    /// <summary>The largest zoom (default 53, deep enough to read a task's checklist); the smallest is always 1.</summary>
    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<SemanticZoom, double>(nameof(MaxZoom), 53.04);

    /// <summary>The node under the keyboard cursor (null when there is none, or when the cursor is on a checklist item).</summary>
    public static readonly DirectProperty<SemanticZoom, SemanticZoomNode?> CursorNodeProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoom, SemanticZoomNode?>(nameof(CursorNode), o => o.CursorNode);

    /// <summary>Whether the tour is playing (two-way). Any manual interaction with the map stops it.</summary>
    public static readonly StyledProperty<bool> IsTourPlayingProperty =
        AvaloniaProperty.Register<SemanticZoom, bool>(nameof(IsTourPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The tour's stops in order; the organization as a stop means the whole map. When null the tour visits a team, one of
    /// its projects, two tasks, a second team and one of its projects, then returns to the whole map.
    /// </summary>
    public static readonly StyledProperty<AvaloniaList<SemanticZoomNode>?> TourStopsProperty =
        AvaloniaProperty.Register<SemanticZoom, AvaloniaList<SemanticZoomNode>?>(nameof(TourStops));

    /// <summary>How long the tour waits at each stop after arriving (default 1.5 s; 2.4 s at reduced motion).</summary>
    public static readonly StyledProperty<TimeSpan> TourDwellProperty =
        AvaloniaProperty.Register<SemanticZoom, TimeSpan>(nameof(TourDwell), TimeSpan.FromSeconds(1.5));

    /// <summary>Where the details panel goes (default <see cref="SemanticZoomPanelPlacement.Auto"/>).</summary>
    public static readonly StyledProperty<SemanticZoomPanelPlacement> DetailPanelPlacementProperty =
        AvaloniaProperty.Register<SemanticZoom, SemanticZoomPanelPlacement>(nameof(DetailPanelPlacement));

    /// <summary>
    /// The map's height in px; when null it is 48% of the control's width between 400 and 580 (420 in containers narrower
    /// than 600 px).
    /// </summary>
    public static readonly StyledProperty<double?> StageHeightProperty =
        AvaloniaProperty.Register<SemanticZoom, double?>(nameof(StageHeight));

    /// <summary>Shows the breadcrumbs in the map's top-left corner. Default true.</summary>
    public static readonly StyledProperty<bool> IsBreadcrumbVisibleProperty =
        AvaloniaProperty.Register<SemanticZoom, bool>(nameof(IsBreadcrumbVisible), true);

    /// <summary>Shows the tour button in the map's top-right corner. Default true.</summary>
    public static readonly StyledProperty<bool> IsTourButtonVisibleProperty =
        AvaloniaProperty.Register<SemanticZoom, bool>(nameof(IsTourButtonVisible), true);

    /// <summary>Shows the minimap (bottom left) once the view leaves the whole organization. Default true.</summary>
    public static readonly StyledProperty<bool> IsMinimapVisibleProperty =
        AvaloniaProperty.Register<SemanticZoom, bool>(nameof(IsMinimapVisible), true);

    /// <summary>Shows the zoom buttons and readout (bottom right). Default true.</summary>
    public static readonly StyledProperty<bool> IsZoomControlsVisibleProperty =
        AvaloniaProperty.Register<SemanticZoom, bool>(nameof(IsZoomControlsVisible), true);

    /// <summary>Shows the gesture hint until the first interaction. Default true.</summary>
    public static readonly StyledProperty<bool> IsHintVisibleProperty =
        AvaloniaProperty.Register<SemanticZoom, bool>(nameof(IsHintVisible), true);

    /// <summary>Every label, button name and announcement (English defaults); assign a new instance to change wording.</summary>
    public static readonly StyledProperty<SemanticZoomTexts> TextsProperty =
        AvaloniaProperty.Register<SemanticZoom, SemanticZoomTexts>(nameof(Texts), SemanticZoomTexts.Default);

    /// <summary>Formats dates and percentages; null uses <see cref="SemanticZoomFormatter.Default"/> ("Sep 23", "59%").</summary>
    public static readonly StyledProperty<ISemanticZoomFormatter?> FormatterProperty =
        AvaloniaProperty.Register<SemanticZoom, ISemanticZoomFormatter?>(nameof(Formatter));

    private IReadOnlyList<SemanticZoomNode> _scopePath = Array.Empty<SemanticZoomNode>();
    private double _zoom = 1;
    private string _zoomText = ShowpieceFormat.ZoomReadout(1);
    private SemanticZoomNode? _cursorNode;

    /// <inheritdoc cref="OrganizationProperty"/>
    [Content]
    public SemanticZoomOrganization? Organization { get => GetValue(OrganizationProperty); set => SetValue(OrganizationProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="SubtitleFormatProperty"/>
    public string SubtitleFormat { get => GetValue(SubtitleFormatProperty); set => SetValue(SubtitleFormatProperty, value); }

    /// <inheritdoc cref="ScopeProperty"/>
    public SemanticZoomNode? Scope { get => GetValue(ScopeProperty); set => SetValue(ScopeProperty, value); }

    /// <inheritdoc cref="ScopePathProperty"/>
    public IReadOnlyList<SemanticZoomNode> ScopePath { get => _scopePath; private set => SetAndRaise(ScopePathProperty, ref _scopePath, value); }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom { get => _zoom; private set => SetAndRaise(ZoomProperty, ref _zoom, value); }

    /// <inheritdoc cref="ZoomTextProperty"/>
    public string ZoomText { get => _zoomText; private set => SetAndRaise(ZoomTextProperty, ref _zoomText, value); }

    /// <inheritdoc cref="MaxZoomProperty"/>
    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <inheritdoc cref="CursorNodeProperty"/>
    public SemanticZoomNode? CursorNode { get => _cursorNode; private set => SetAndRaise(CursorNodeProperty, ref _cursorNode, value); }

    /// <inheritdoc cref="IsTourPlayingProperty"/>
    public bool IsTourPlaying { get => GetValue(IsTourPlayingProperty); set => SetValue(IsTourPlayingProperty, value); }

    /// <inheritdoc cref="TourStopsProperty"/>
    public AvaloniaList<SemanticZoomNode>? TourStops { get => GetValue(TourStopsProperty); set => SetValue(TourStopsProperty, value); }

    /// <inheritdoc cref="TourDwellProperty"/>
    public TimeSpan TourDwell { get => GetValue(TourDwellProperty); set => SetValue(TourDwellProperty, value); }

    /// <inheritdoc cref="DetailPanelPlacementProperty"/>
    public SemanticZoomPanelPlacement DetailPanelPlacement { get => GetValue(DetailPanelPlacementProperty); set => SetValue(DetailPanelPlacementProperty, value); }

    /// <inheritdoc cref="StageHeightProperty"/>
    public double? StageHeight { get => GetValue(StageHeightProperty); set => SetValue(StageHeightProperty, value); }

    /// <inheritdoc cref="IsBreadcrumbVisibleProperty"/>
    public bool IsBreadcrumbVisible { get => GetValue(IsBreadcrumbVisibleProperty); set => SetValue(IsBreadcrumbVisibleProperty, value); }

    /// <inheritdoc cref="IsTourButtonVisibleProperty"/>
    public bool IsTourButtonVisible { get => GetValue(IsTourButtonVisibleProperty); set => SetValue(IsTourButtonVisibleProperty, value); }

    /// <inheritdoc cref="IsMinimapVisibleProperty"/>
    public bool IsMinimapVisible { get => GetValue(IsMinimapVisibleProperty); set => SetValue(IsMinimapVisibleProperty, value); }

    /// <inheritdoc cref="IsZoomControlsVisibleProperty"/>
    public bool IsZoomControlsVisible { get => GetValue(IsZoomControlsVisibleProperty); set => SetValue(IsZoomControlsVisibleProperty, value); }

    /// <inheritdoc cref="IsHintVisibleProperty"/>
    public bool IsHintVisible { get => GetValue(IsHintVisibleProperty); set => SetValue(IsHintVisibleProperty, value); }

    /// <inheritdoc cref="TextsProperty"/>
    public SemanticZoomTexts Texts { get => GetValue(TextsProperty); set => SetValue(TextsProperty, value); }

    /// <inheritdoc cref="FormatterProperty"/>
    public ISemanticZoomFormatter? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when the node in view changes (camera flights, pans and zooms included).</summary>
    public static readonly RoutedEvent<SemanticZoomScopeChangedEventArgs> ScopeChangedEvent =
        RoutedEvent.Register<SemanticZoom, SemanticZoomScopeChangedEventArgs>(nameof(ScopeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a node is clicked or chosen with Enter (the camera then flies into it).</summary>
    public static readonly RoutedEvent<SemanticZoomNodeEventArgs> NodeInvokedEvent =
        RoutedEvent.Register<SemanticZoom, SemanticZoomNodeEventArgs>(nameof(NodeInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a checklist item is ticked or unticked on the map.</summary>
    public static readonly RoutedEvent<SemanticZoomChecklistEventArgs> ChecklistItemToggledEvent =
        RoutedEvent.Register<SemanticZoom, SemanticZoomChecklistEventArgs>(nameof(ChecklistItemToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when the tour starts.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TourStartedEvent =
        RoutedEvent.Register<SemanticZoom, RoutedEventArgs>(nameof(TourStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when the tour stops, at its end or interrupted.</summary>
    public static readonly RoutedEvent<SemanticZoomTourStoppedEventArgs> TourStoppedEvent =
        RoutedEvent.Register<SemanticZoom, SemanticZoomTourStoppedEventArgs>(nameof(TourStopped), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ScopeChangedEvent"/>
    public event EventHandler<SemanticZoomScopeChangedEventArgs>? ScopeChanged
    {
        add => AddHandler(ScopeChangedEvent, value);
        remove => RemoveHandler(ScopeChangedEvent, value);
    }

    /// <inheritdoc cref="NodeInvokedEvent"/>
    public event EventHandler<SemanticZoomNodeEventArgs>? NodeInvoked
    {
        add => AddHandler(NodeInvokedEvent, value);
        remove => RemoveHandler(NodeInvokedEvent, value);
    }

    /// <inheritdoc cref="ChecklistItemToggledEvent"/>
    public event EventHandler<SemanticZoomChecklistEventArgs>? ChecklistItemToggled
    {
        add => AddHandler(ChecklistItemToggledEvent, value);
        remove => RemoveHandler(ChecklistItemToggledEvent, value);
    }

    /// <inheritdoc cref="TourStartedEvent"/>
    public event EventHandler<RoutedEventArgs>? TourStarted
    {
        add => AddHandler(TourStartedEvent, value);
        remove => RemoveHandler(TourStartedEvent, value);
    }

    /// <inheritdoc cref="TourStoppedEvent"/>
    public event EventHandler<SemanticZoomTourStoppedEventArgs>? TourStopped
    {
        add => AddHandler(TourStoppedEvent, value);
        remove => RemoveHandler(TourStoppedEvent, value);
    }

    #endregion

    #region Commands

    /// <summary>Flies to the node given as the command parameter (the organization shows everything).</summary>
    public ICommand FlyToCommand { get; }

    /// <summary>Shows the whole organization.</summary>
    public ICommand ResetViewCommand { get; }

    /// <summary>Starts the tour, or stops it while it plays.</summary>
    public ICommand ToggleTourCommand { get; }

    #endregion
}
