using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using TKCode.Arc.Primitives;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A person's profile page as one block: a wide cover with the avatar riding its bottom edge, the name, handle and
/// headline, Share and Edit profile; an about column (bio, location, website, joined date and up to three stats) beside
/// two tabs, Activity (grouped rows) and Projects (cards with stars). Edit profile slides a sheet in from the right inside
/// the block; saving writes the new values back and makes each changed field glow once.
/// <para>
/// The block is <see cref="PageHeight"/> tall (760, or less when less space is available) and scrolls inside; the sheet
/// always covers the visible part. Its layout follows its own width: the cover shrinks below 1000, the columns stack
/// below 760, the header tightens below 640, the cards go to one column below 560 and the sheet's paired fields stack
/// below 420.
/// </para>
/// </summary>
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(CoverPart, typeof(Border))]
[TemplatePart(CoverImagePart, typeof(ArcImage))]
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(SharePart, typeof(ProfilePageButton))]
[TemplatePart(ShareStackPart, typeof(ProfilePageSwapStack))]
[TemplatePart(EditPart, typeof(ProfilePageButton))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(HandlePart, typeof(TextBlock))]
[TemplatePart(HeadlinePart, typeof(TextBlock))]
[TemplatePart(BioPart, typeof(TextBlock))]
[TemplatePart(LocationPart, typeof(TextBlock))]
[TemplatePart(WebsitePart, typeof(ProfilePageButton))]
[TemplatePart(JoinedPart, typeof(TextBlock))]
[TemplatePart(StatsPart, typeof(ProfilePageColumns))]
[TemplatePart(BodyPart, typeof(ProfilePageBodyPanel))]
[TemplatePart(TabsPart, typeof(ProfilePageTabStrip))]
[TemplatePart(PanelSizePart, typeof(SpringSizeDecorator))]
[TemplatePart(PanelSwapPart, typeof(ProfilePagePanelSwap))]
[TemplatePart(ScrimPart, typeof(Border))]
[TemplatePart(SheetPart, typeof(ProfilePageSheet))]
[PseudoClasses(CoverMediumPseudoClass, StackedPseudoClass, CompactPseudoClass, SingleColumnPseudoClass, EditingPseudoClass, FramedPseudoClass)]
public class ProfilePage : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";
    public const string CoverPart = "PART_Cover";
    public const string CoverImagePart = "PART_CoverImage";
    public const string AvatarPart = "PART_Avatar";
    public const string SharePart = "PART_Share";
    public const string ShareStackPart = "PART_ShareStack";
    public const string EditPart = "PART_Edit";
    public const string NamePart = "PART_Name";
    public const string NameGlowPart = "PART_NameGlow";
    public const string HandlePart = "PART_Handle";
    public const string HeadlineDotPart = "PART_HeadlineDot";
    public const string HeadlinePart = "PART_Headline";
    public const string HeadlineGlowPart = "PART_HeadlineGlow";
    public const string BioPart = "PART_Bio";
    public const string BioGlowPart = "PART_BioGlow";
    public const string LocationPart = "PART_Location";
    public const string LocationGlowPart = "PART_LocationGlow";
    public const string WebsitePart = "PART_Website";
    public const string WebsiteGlowPart = "PART_WebsiteGlow";
    public const string JoinedRowPart = "PART_JoinedRow";
    public const string JoinedPart = "PART_Joined";
    public const string StatsPart = "PART_Stats";
    public const string AboutPart = "PART_About";
    public const string MainPart = "PART_Main";
    public const string BodyPart = "PART_Body";
    public const string TabsPart = "PART_Tabs";
    public const string PanelSizePart = "PART_PanelSize";
    public const string PanelSwapPart = "PART_PanelSwap";
    public const string ScrimPart = "PART_Scrim";
    public const string SheetPart = "PART_Sheet";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the page is narrower than 1000: the cover is 200 high.</summary>
    public const string CoverMediumPseudoClass = ":cover-medium";

    /// <summary>Set while the page is narrower than 760: the about block sits above the tabs.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the page is narrower than 640: a 128 cover, an 84 avatar, a 22px name and tighter padding.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the page is narrower than 560: the project cards are in one column.</summary>
    public const string SingleColumnPseudoClass = ":single-column";

    /// <summary>Set while the edit sheet is open.</summary>
    public const string EditingPseudoClass = ":editing";

    /// <summary>Set while <see cref="IsFramed"/>.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Below this width the cover is 200 high instead of 240.</summary>
    public const double CoverBreakpoint = 1000;

    /// <summary>Below this width the about block and the tabs stack.</summary>
    public const double StackBreakpoint = 760;

    /// <summary>Below this width the header and the body tighten.</summary>
    public const double CompactBreakpoint = 640;

    /// <summary>Below this width the project cards are in one column.</summary>
    public const double SingleColumnBreakpoint = 560;

    /// <summary>Below this width the sheet spans the page and its paired fields stack.</summary>
    public const double SheetBreakpoint = 420;

    #region Properties

    /// <summary>The person shown. A successful save replaces it with a new object holding the trimmed values.</summary>
    public static readonly StyledProperty<ProfileDetails?> ProfileProperty =
        AvaloniaProperty.Register<ProfilePage, ProfileDetails?>(nameof(Profile), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The numbers under the bio, when no <see cref="StatsSource"/> is set (up to three are shown).</summary>
    public static readonly StyledProperty<AvaloniaList<ProfileStat>> StatsProperty =
        AvaloniaProperty.Register<ProfilePage, AvaloniaList<ProfileStat>>(nameof(Stats));

    /// <summary>A collection of <see cref="ProfileStat"/> you own; wins over <see cref="Stats"/> and is followed when it notifies.</summary>
    public static readonly StyledProperty<IEnumerable?> StatsSourceProperty =
        AvaloniaProperty.Register<ProfilePage, IEnumerable?>(nameof(StatsSource));

    /// <summary>The Activity rows, when no <see cref="ActivitySource"/> is set.</summary>
    public static readonly StyledProperty<AvaloniaList<ProfileActivity>> ActivityProperty =
        AvaloniaProperty.Register<ProfilePage, AvaloniaList<ProfileActivity>>(nameof(Activity));

    /// <summary>A collection of <see cref="ProfileActivity"/> you own; wins over <see cref="Activity"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ActivitySourceProperty =
        AvaloniaProperty.Register<ProfilePage, IEnumerable?>(nameof(ActivitySource));

    /// <summary>The project cards, when no <see cref="ProjectsSource"/> is set.</summary>
    public static readonly StyledProperty<AvaloniaList<ProfileProject>> ProjectsProperty =
        AvaloniaProperty.Register<ProfilePage, AvaloniaList<ProfileProject>>(nameof(Projects));

    /// <summary>A collection of <see cref="ProfileProject"/> you own; wins over <see cref="Projects"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ProjectsSourceProperty =
        AvaloniaProperty.Register<ProfilePage, IEnumerable?>(nameof(ProjectsSource));

    /// <summary>The tab shown (two-way; default Activity). Bind it to control the tabs from outside.</summary>
    public static readonly StyledProperty<ProfilePageTab> TabProperty =
        AvaloniaProperty.Register<ProfilePage, ProfilePageTab>(nameof(Tab), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether Edit profile is offered (default true). False makes the page read-only, as for someone else's profile.</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<ProfilePage, bool>(nameof(IsEditable), true);

    /// <summary>Whether the edit sheet is open (two-way). Setting it opens or closes the sheet.</summary>
    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<ProfilePage, bool>(nameof(IsEditing), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The link Share copies; null copies "https://example.com/@{handle}".</summary>
    public static readonly StyledProperty<string?> ProfileUrlProperty =
        AvaloniaProperty.Register<ProfilePage, string?>(nameof(ProfileUrl));

    /// <summary>
    /// Saves the edited profile (it receives the trimmed draft). Throw to keep the sheet open: the exception's message
    /// is shown above the sheet's buttons. Null simulates a save that succeeds after <see cref="SimulatedSaveDelay"/>.
    /// </summary>
    public static readonly StyledProperty<Func<ProfileDetails, Task>?> SaveHandlerProperty =
        AvaloniaProperty.Register<ProfilePage, Func<ProfileDetails, Task>?>(nameof(SaveHandler));

    /// <summary>How long the simulated save takes when no <see cref="SaveHandler"/> is set (default 700ms).</summary>
    public static readonly StyledProperty<TimeSpan> SimulatedSaveDelayProperty =
        AvaloniaProperty.Register<ProfilePage, TimeSpan>(nameof(SimulatedSaveDelay), TimeSpan.FromMilliseconds(700));

    /// <summary>
    /// Copies the link (returns whether it worked; throwing counts as a failure). Null uses the system clipboard.
    /// </summary>
    public static readonly StyledProperty<Func<string, Task<bool>>?> CopyHandlerProperty =
        AvaloniaProperty.Register<ProfilePage, Func<string, Task<bool>>?>(nameof(CopyHandler));

    /// <summary>How long Share shows Copied or Failed before it reads Share again (default 1800ms).</summary>
    public static readonly StyledProperty<TimeSpan> ShareFeedbackDurationProperty =
        AvaloniaProperty.Register<ProfilePage, TimeSpan>(nameof(ShareFeedbackDuration), TimeSpan.FromMilliseconds(1800));

    /// <summary>The most characters the bio field takes (default 200); the counter shows what is left.</summary>
    public static readonly StyledProperty<int> BioMaxLengthProperty =
        AvaloniaProperty.Register<ProfilePage, int>(nameof(BioMaxLength), 200);

    /// <summary>The block's height when there is room (default 760); with less space it takes what it gets and scrolls inside.</summary>
    public static readonly StyledProperty<double> PageHeightProperty =
        AvaloniaProperty.Register<ProfilePage, double>(nameof(PageHeight), 760);

    /// <summary>Whether the block has a 1px border and rounded corners (default true); false for a full-window page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<ProfilePage, bool>(nameof(IsFramed), true);

    /// <summary>Every word the block shows or announces. Replace the whole object to translate it.</summary>
    public static readonly StyledProperty<ProfilePageLabels> LabelsProperty =
        AvaloniaProperty.Register<ProfilePage, ProfilePageLabels>(nameof(Labels), new ProfilePageLabels());

    /// <summary>Whether a save is running.</summary>
    public static readonly DirectProperty<ProfilePage, bool> IsSavingProperty =
        AvaloniaProperty.RegisterDirect<ProfilePage, bool>(nameof(IsSaving), o => o.IsSaving);

    /// <summary>Raised when <see cref="Tab"/> changes (by a tab, the arrow keys or a binding).</summary>
    public static readonly RoutedEvent<ProfilePageTabChangedEventArgs> TabChangedEvent =
        RoutedEvent.Register<ProfilePage, ProfilePageTabChangedEventArgs>(nameof(TabChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a project's star is pressed, after its count and state were updated.</summary>
    public static readonly RoutedEvent<ProfileStarChangedEventArgs> StarChangedEvent =
        RoutedEvent.Register<ProfilePage, ProfileStarChangedEventArgs>(nameof(StarChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after a successful save, once <see cref="Profile"/> holds the new values.</summary>
    public static readonly RoutedEvent<ProfileSavedEventArgs> SavedEvent =
        RoutedEvent.Register<ProfilePage, ProfileSavedEventArgs>(nameof(Saved), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SaveHandler"/> threw.</summary>
    public static readonly RoutedEvent<ProfileSaveFailedEventArgs> SaveFailedEvent =
        RoutedEvent.Register<ProfilePage, ProfileSaveFailedEventArgs>(nameof(SaveFailed), RoutingStrategies.Bubble);

    /// <summary>Raised after Share tried to copy the link.</summary>
    public static readonly RoutedEvent<ProfileSharedEventArgs> SharedEvent =
        RoutedEvent.Register<ProfilePage, ProfileSharedEventArgs>(nameof(Shared), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ProfileProperty"/>
    public ProfileDetails? Profile { get => GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }

    /// <inheritdoc cref="StatsProperty"/>
    public AvaloniaList<ProfileStat> Stats { get => GetValue(StatsProperty); set => SetValue(StatsProperty, value); }

    /// <inheritdoc cref="StatsSourceProperty"/>
    public IEnumerable? StatsSource { get => GetValue(StatsSourceProperty); set => SetValue(StatsSourceProperty, value); }

    /// <inheritdoc cref="ActivityProperty"/>
    public AvaloniaList<ProfileActivity> Activity { get => GetValue(ActivityProperty); set => SetValue(ActivityProperty, value); }

    /// <inheritdoc cref="ActivitySourceProperty"/>
    public IEnumerable? ActivitySource { get => GetValue(ActivitySourceProperty); set => SetValue(ActivitySourceProperty, value); }

    /// <inheritdoc cref="ProjectsProperty"/>
    public AvaloniaList<ProfileProject> Projects { get => GetValue(ProjectsProperty); set => SetValue(ProjectsProperty, value); }

    /// <inheritdoc cref="ProjectsSourceProperty"/>
    public IEnumerable? ProjectsSource { get => GetValue(ProjectsSourceProperty); set => SetValue(ProjectsSourceProperty, value); }

    /// <inheritdoc cref="TabProperty"/>
    public ProfilePageTab Tab { get => GetValue(TabProperty); set => SetValue(TabProperty, value); }

    /// <inheritdoc cref="IsEditableProperty"/>
    public bool IsEditable { get => GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <inheritdoc cref="IsEditingProperty"/>
    public bool IsEditing { get => GetValue(IsEditingProperty); set => SetValue(IsEditingProperty, value); }

    /// <inheritdoc cref="ProfileUrlProperty"/>
    public string? ProfileUrl { get => GetValue(ProfileUrlProperty); set => SetValue(ProfileUrlProperty, value); }

    /// <inheritdoc cref="SaveHandlerProperty"/>
    public Func<ProfileDetails, Task>? SaveHandler { get => GetValue(SaveHandlerProperty); set => SetValue(SaveHandlerProperty, value); }

    /// <inheritdoc cref="SimulatedSaveDelayProperty"/>
    public TimeSpan SimulatedSaveDelay { get => GetValue(SimulatedSaveDelayProperty); set => SetValue(SimulatedSaveDelayProperty, value); }

    /// <inheritdoc cref="CopyHandlerProperty"/>
    public Func<string, Task<bool>>? CopyHandler { get => GetValue(CopyHandlerProperty); set => SetValue(CopyHandlerProperty, value); }

    /// <inheritdoc cref="ShareFeedbackDurationProperty"/>
    public TimeSpan ShareFeedbackDuration { get => GetValue(ShareFeedbackDurationProperty); set => SetValue(ShareFeedbackDurationProperty, value); }

    /// <inheritdoc cref="BioMaxLengthProperty"/>
    public int BioMaxLength { get => GetValue(BioMaxLengthProperty); set => SetValue(BioMaxLengthProperty, value); }

    /// <inheritdoc cref="PageHeightProperty"/>
    public double PageHeight { get => GetValue(PageHeightProperty); set => SetValue(PageHeightProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ProfilePageLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    private bool _isSaving;

    /// <inheritdoc cref="IsSavingProperty"/>
    public bool IsSaving
    {
        get => _isSaving;
        private set => SetAndRaise(IsSavingProperty, ref _isSaving, value);
    }

    /// <inheritdoc cref="TabChangedEvent"/>
    public event EventHandler<ProfilePageTabChangedEventArgs>? TabChanged
    {
        add => AddHandler(TabChangedEvent, value);
        remove => RemoveHandler(TabChangedEvent, value);
    }

    /// <inheritdoc cref="StarChangedEvent"/>
    public event EventHandler<ProfileStarChangedEventArgs>? StarChanged
    {
        add => AddHandler(StarChangedEvent, value);
        remove => RemoveHandler(StarChangedEvent, value);
    }

    /// <inheritdoc cref="SavedEvent"/>
    public event EventHandler<ProfileSavedEventArgs>? Saved
    {
        add => AddHandler(SavedEvent, value);
        remove => RemoveHandler(SavedEvent, value);
    }

    /// <inheritdoc cref="SaveFailedEvent"/>
    public event EventHandler<ProfileSaveFailedEventArgs>? SaveFailed
    {
        add => AddHandler(SaveFailedEvent, value);
        remove => RemoveHandler(SaveFailedEvent, value);
    }

    /// <inheritdoc cref="SharedEvent"/>
    public event EventHandler<ProfileSharedEventArgs>? Shared
    {
        add => AddHandler(SharedEvent, value);
        remove => RemoveHandler(SharedEvent, value);
    }

    #endregion

    private static readonly TimeSpan ReducedSheetIn = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ReducedSheetOut = TimeSpan.FromMilliseconds(150);
    private static readonly Avalonia.Animation.Easings.Easing EaseOut = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);

    /// <summary>Arc's sheet exit: a fast critically damped spring (fitted ω ≈ 39, about 250ms).</summary>
    internal static readonly SpringSpec SheetExitSpring = SpringSpec.FromPhysical(1521, 78);

    private readonly ProfilePageActivityView _activityView = new();
    private readonly ProfilePageProjectsView _projectsView = new();
    private readonly ProfilePageTabButton _activityTab = new(ProfilePageTab.Activity);
    private readonly ProfilePageTabButton _projectsTab = new(ProfilePageTab.Projects);
    private readonly List<AvaloniaObject> _watchedItems = new();
    private INotifyCollectionChanged? _observedStats, _observedActivity, _observedProjects;
    private ProfileDetails? _watchedProfile;
    private bool _attached;

    private ScrollViewer? _scroll;
    private Border? _cover;
    private ArcImage? _coverImage;
    private Avatar? _avatar;
    private ProfilePageButton? _share;
    private ProfilePageSwapStack? _shareStack;
    private TextBlock? _shareLabel, _copiedLabel, _failedLabel;
    private ProfilePageButton? _edit;
    private TextBlock? _name, _handle, _headline, _headlineDot, _bio, _location, _joined;
    private ProfilePageGlow? _nameGlow, _headlineGlow, _bioGlow, _locationGlow, _websiteGlow;
    private ProfilePageButton? _website;
    private Control? _joinedRow, _about, _main;
    private ProfilePageColumns? _stats;
    private ProfilePageBodyPanel? _body;
    private ProfilePageTabStrip? _tabs;
    private SpringSizeDecorator? _panelSize;
    private ProfilePagePanelSwap? _panelSwap;
    private Border? _scrim;
    private ProfilePageSheet? _sheet;
    private ProjectsAnnouncer? _announcer;

    private DispatcherTimer? _shareTimer;
    private MotionValue<double>? _sheetSlide, _sheetFade;
    private readonly TranslateTransform _sheetTranslate = new();
    private int _sheetVersion;
    private bool _sheetShown;
    private int _saveVersion;

    public ProfilePage()
    {
        SetCurrentValue(StatsProperty, new AvaloniaList<ProfileStat>());
        SetCurrentValue(ActivityProperty, new AvaloniaList<ProfileActivity>());
        SetCurrentValue(ProjectsProperty, new AvaloniaList<ProfileProject>());
        PseudoClasses.Set(FramedPseudoClass, true);
        _activityTab.Command = new ProfilePageTabCommand(this, ProfilePageTab.Activity);
        _projectsTab.Command = new ProfilePageTabCommand(this, ProfilePageTab.Projects);
        _activityTab.AddHandler(KeyDownEvent, OnTabKeyDown, RoutingStrategies.Tunnel);
        _projectsTab.AddHandler(KeyDownEvent, OnTabKeyDown, RoutingStrategies.Tunnel);
        _activityTab.IsSelected = true;
        // Where the focus lands after the sheet opens or closes depends on how the person got there: keyboard focus
        // shows the ring, pointer focus does not.
        AddHandler(KeyDownEvent, (_, _) => _keyboardInput = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardInput = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private bool _keyboardInput = true;

    private NavigationMethod FocusMethod => _keyboardInput ? NavigationMethod.Tab : NavigationMethod.Pointer;

    // Accessors for tests and the snapshot tool --------------------------------------------------------------------------

    internal ProfilePageTabButton ActivityTabButton => _activityTab;
    internal ProfilePageTabButton ProjectsTabButton => _projectsTab;
    internal ProfilePageActivityView ActivityView => _activityView;
    internal ProfilePageProjectsView ProjectsView => _projectsView;
    internal ProfilePageSheet? Sheet => _sheet;
    internal ProfilePageButton? ShareButton => _share;
    internal ProfilePageSwapStack? ShareStack => _shareStack;
    internal ProfilePageButton? EditButton => _edit;
    internal ProfilePageButton? WebsiteButton => _website;
    internal Border? Scrim => _scrim;
    internal Border? Cover => _cover;
    internal Avatar? AvatarView => _avatar;
    internal TextBlock? NameText => _name;
    internal TextBlock? HeadlineText => _headline;
    internal TextBlock? BioText => _bio;
    internal TextBlock? LocationText => _location;
    internal ProfilePageColumns? StatsPanel => _stats;
    internal ProfilePageBodyPanel? Body => _body;
    internal ProfilePageTabStrip? TabStrip => _tabs;
    internal ProfilePagePanelSwap? PanelSwap => _panelSwap;
    internal ScrollViewer? Scroller => _scroll;
    internal ProjectsAnnouncer? Announcer => _announcer;
    internal double SheetOffset => _sheetSlide?.Value ?? 0;
    internal double SheetOpacity => _sheet?.Opacity ?? 0;
    internal bool IsSheetShown => _sheetShown;

    internal ProfilePageGlow? GlowOf(ProfileFields field) => field switch
    {
        ProfileFields.Name => _nameGlow,
        ProfileFields.Headline => _headlineGlow,
        ProfileFields.Location => _locationGlow,
        ProfileFields.Website => _websiteGlow,
        ProfileFields.Bio => _bioGlow,
        _ => null,
    };

    // Template ---------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _scroll = scope.Find<ScrollViewer>(ScrollPart);
        _cover = scope.Find<Border>(CoverPart);
        _coverImage = scope.Find<ArcImage>(CoverImagePart);
        _avatar = scope.Find<Avatar>(AvatarPart);
        _share = scope.Find<ProfilePageButton>(SharePart);
        _shareStack = scope.Find<ProfilePageSwapStack>(ShareStackPart);
        _edit = scope.Find<ProfilePageButton>(EditPart);
        _name = scope.Find<TextBlock>(NamePart);
        _nameGlow = scope.Find<ProfilePageGlow>(NameGlowPart);
        _handle = scope.Find<TextBlock>(HandlePart);
        _headlineDot = scope.Find<TextBlock>(HeadlineDotPart);
        _headline = scope.Find<TextBlock>(HeadlinePart);
        _headlineGlow = scope.Find<ProfilePageGlow>(HeadlineGlowPart);
        _bio = scope.Find<TextBlock>(BioPart);
        _bioGlow = scope.Find<ProfilePageGlow>(BioGlowPart);
        _location = scope.Find<TextBlock>(LocationPart);
        _locationGlow = scope.Find<ProfilePageGlow>(LocationGlowPart);
        _website = scope.Find<ProfilePageButton>(WebsitePart);
        _websiteGlow = scope.Find<ProfilePageGlow>(WebsiteGlowPart);
        _joinedRow = scope.Find<Control>(JoinedRowPart);
        _joined = scope.Find<TextBlock>(JoinedPart);
        _stats = scope.Find<ProfilePageColumns>(StatsPart);
        _about = scope.Find<Control>(AboutPart);
        _main = scope.Find<Control>(MainPart);
        _body = scope.Find<ProfilePageBodyPanel>(BodyPart);
        _tabs = scope.Find<ProfilePageTabStrip>(TabsPart);
        _panelSize = scope.Find<SpringSizeDecorator>(PanelSizePart);
        _panelSwap = scope.Find<ProfilePagePanelSwap>(PanelSwapPart);
        _scrim = scope.Find<Border>(ScrimPart);
        _sheet = scope.Find<ProfilePageSheet>(SheetPart);
        _announcer = scope.Find<ProjectsAnnouncer>(AnnouncerPart);

        if (_shareStack is not null && _shareStack.Children.Count == 0)
        {
            _shareStack.AddLayer(ProfilePageSwapStack.GlyphLabel(ArcIcons.Share2, Labels.Share, out var share));
            _shareStack.AddLayer(ProfilePageSwapStack.GlyphLabel(ArcIcons.Check, Labels.Copied, out var copied));
            _shareStack.AddLayer(ProfilePageSwapStack.GlyphLabel(ArcIcons.X, Labels.CopyFailed, out var failed));
            _shareLabel = share;
            _copiedLabel = copied;
            _failedLabel = failed;
        }
        if (_share is not null)
        {
            _share.Click += OnShareClick;
            AutomationProperties.SetLiveSetting(_share, AutomationLiveSetting.Polite);
        }
        if (_edit is not null)
        {
            _edit.Click += OnEditClick;
        }
        if (_website is not null)
        {
            _website.Click += OnWebsiteClick;
        }
        if (_tabs is not null)
        {
            foreach (var tab in new[] { _activityTab, _projectsTab })
            {
                if (tab.Parent is Panel old && !ReferenceEquals(old, _tabs))
                {
                    old.Children.Remove(tab);
                }
                if (!_tabs.Children.Contains(tab))
                {
                    _tabs.AddTab(tab);
                }
            }
        }
        if (_panelSwap is not null)
        {
            _panelSwap.Show(Tab == ProfilePageTab.Activity ? _activityView : _projectsView, animate: false);
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        if (_sheet is not null)
        {
            _sheet.SubmitRequested += OnSheetSubmit;
            _sheet.DismissRequested += OnSheetDismiss;
            _sheet.RenderTransform = _sheetTranslate;
            _sheet.IsVisible = _sheetShown;
        }
        ApplyLabels();
        RefreshProfile();
        RebuildStats();
        RebuildActivity();
        RebuildProjects();
        UpdateBreakpoints(Bounds.Width);
    }

    private void Unhook()
    {
        if (_share is not null)
        {
            _share.Click -= OnShareClick;
        }
        if (_edit is not null)
        {
            _edit.Click -= OnEditClick;
        }
        if (_website is not null)
        {
            _website.Click -= OnWebsiteClick;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
        if (_sheet is not null)
        {
            _sheet.SubmitRequested -= OnSheetSubmit;
            _sheet.DismissRequested -= OnSheetDismiss;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        WatchProfile();
        ObserveSources();
        RebuildStats();
        RebuildActivity();
        RebuildProjects();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _shareTimer?.Stop();
        WatchProfile();
        ObserveSources();
        UnwatchItems();
        _projectsView.Detach();
    }

    // Size -------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsFinite(availableSize.Height) ? Math.Min(PageHeight, availableSize.Height) : PageHeight;
        var desired = base.MeasureOverride(availableSize.WithHeight(height));
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width;
        return new Size(width, height);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateBreakpoints(e.NewSize.Width);
        }
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        PseudoClasses.Set(CoverMediumPseudoClass, width < CoverBreakpoint);
        PseudoClasses.Set(StackedPseudoClass, width < StackBreakpoint);
        PseudoClasses.Set(CompactPseudoClass, width < CompactBreakpoint);
        PseudoClasses.Set(SingleColumnPseudoClass, width < SingleColumnBreakpoint);
        if (_body is not null)
        {
            _body.IsStacked = width < StackBreakpoint;
        }
        _projectsView.Columns = width < SingleColumnBreakpoint ? 1 : 2;
        _sheet?.UpdateWidth(width, 420);
    }

    // Properties -------------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ProfileProperty)
        {
            WatchProfile();
            RefreshProfile();
        }
        else if (property == StatsSourceProperty || property == ActivitySourceProperty || property == ProjectsSourceProperty)
        {
            ObserveSources();
            if (property == StatsSourceProperty)
            {
                RebuildStats();
            }
            else if (property == ActivitySourceProperty)
            {
                RebuildActivity();
            }
            else
            {
                RebuildProjects();
            }
        }
        else if (property == StatsProperty || property == ActivityProperty || property == ProjectsProperty)
        {
            if (change.OldValue is AvaloniaList<ProfileStat> oldStats)
            {
                oldStats.CollectionChanged -= OnStatsListChanged;
            }
            if (change.OldValue is AvaloniaList<ProfileActivity> oldActivity)
            {
                oldActivity.CollectionChanged -= OnActivityListChanged;
            }
            if (change.OldValue is AvaloniaList<ProfileProject> oldProjects)
            {
                oldProjects.CollectionChanged -= OnProjectsListChanged;
            }
            if (change.NewValue is AvaloniaList<ProfileStat> s)
            {
                s.CollectionChanged += OnStatsListChanged;
            }
            if (change.NewValue is AvaloniaList<ProfileActivity> a)
            {
                a.CollectionChanged += OnActivityListChanged;
            }
            if (change.NewValue is AvaloniaList<ProfileProject> p)
            {
                p.CollectionChanged += OnProjectsListChanged;
            }
            RebuildStats();
            RebuildActivity();
            RebuildProjects();
        }
        else if (property == TabProperty)
        {
            OnTabChanged(change.GetOldValue<ProfilePageTab>(), change.GetNewValue<ProfilePageTab>());
        }
        else if (property == IsEditingProperty)
        {
            if (change.GetNewValue<bool>())
            {
                ShowSheet();
            }
            else
            {
                HideSheet(restoreFocus: true);
            }
        }
        else if (property == IsEditableProperty)
        {
            if (!IsEditable && IsEditing)
            {
                SetCurrentValue(IsEditingProperty, false);
            }
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
            RefreshProfile();
            RebuildActivity();
            _projectsView.RefreshLabels();
            _sheet?.SetLabels(Labels);
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
    }

    private void OnStatsListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (StatsSource is null)
        {
            RebuildStats();
        }
    }

    private void OnActivityListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ActivitySource is null)
        {
            RebuildActivity();
        }
    }

    private void OnProjectsListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ProjectsSource is null)
        {
            RebuildProjects();
        }
    }

    private void ObserveSources()
    {
        Observe(ref _observedStats, _attached ? StatsSource as INotifyCollectionChanged : null, OnStatsSourceChanged);
        Observe(ref _observedActivity, _attached ? ActivitySource as INotifyCollectionChanged : null, OnActivitySourceChanged);
        Observe(ref _observedProjects, _attached ? ProjectsSource as INotifyCollectionChanged : null, OnProjectsSourceChanged);
    }

    private static void Observe(ref INotifyCollectionChanged? observed, INotifyCollectionChanged? next, NotifyCollectionChangedEventHandler handler)
    {
        if (ReferenceEquals(observed, next))
        {
            return;
        }
        if (observed is not null)
        {
            observed.CollectionChanged -= handler;
        }
        observed = next;
        if (observed is not null)
        {
            observed.CollectionChanged += handler;
        }
    }

    private void OnStatsSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildStats();

    private void OnActivitySourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildActivity();

    private void OnProjectsSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildProjects();

    /// <summary>The stats shown now (from <see cref="StatsSource"/> or <see cref="Stats"/>), at most three.</summary>
    public IReadOnlyList<ProfileStat> CurrentStats => (StatsSource ?? (IEnumerable?)Stats ?? Array.Empty<object>()).OfType<ProfileStat>().Take(3).ToList();

    /// <summary>The activity rows shown now.</summary>
    public IReadOnlyList<ProfileActivity> CurrentActivity => (ActivitySource ?? (IEnumerable?)Activity ?? Array.Empty<object>()).OfType<ProfileActivity>().ToList();

    /// <summary>The projects shown now.</summary>
    public IReadOnlyList<ProfileProject> CurrentProjects => (ProjectsSource ?? (IEnumerable?)Projects ?? Array.Empty<object>()).OfType<ProfileProject>().ToList();

    private void WatchProfile()
    {
        var next = _attached ? Profile : null;
        if (ReferenceEquals(next, _watchedProfile))
        {
            return;
        }
        if (_watchedProfile is not null)
        {
            _watchedProfile.PropertyChanged -= OnProfileChanged;
        }
        _watchedProfile = next;
        if (_watchedProfile is not null)
        {
            _watchedProfile.PropertyChanged += OnProfileChanged;
        }
    }

    private void OnProfileChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => RefreshProfile();

    private void UnwatchItems()
    {
        foreach (var item in _watchedItems)
        {
            item.PropertyChanged -= OnItemChanged;
        }
        _watchedItems.Clear();
    }

    private void WatchItems()
    {
        UnwatchItems();
        if (!_attached)
        {
            return;
        }
        foreach (var item in CurrentStats.Cast<AvaloniaObject>().Concat(CurrentActivity))
        {
            item.PropertyChanged += OnItemChanged;
            _watchedItems.Add(item);
        }
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is ProfileStat)
        {
            RebuildStats();
        }
        else if (sender is ProfileActivity)
        {
            RebuildActivity();
        }
    }

    // Content ----------------------------------------------------------------------------------------------------------

    private void ApplyLabels()
    {
        var labels = Labels;
        if (_shareLabel is not null)
        {
            _shareLabel.Text = labels.Share;
        }
        if (_copiedLabel is not null)
        {
            _copiedLabel.Text = labels.Copied;
        }
        if (_failedLabel is not null)
        {
            _failedLabel.Text = labels.CopyFailed;
        }
        if (_share is not null)
        {
            AutomationProperties.SetName(_share, (_shareStack?.Shown ?? 0) switch
            {
                1 => labels.Copied,
                2 => labels.CopyFailed,
                _ => labels.Share,
            });
        }
        if (_edit is not null)
        {
            _edit.Content = labels.EditProfile;
            AutomationProperties.SetName(_edit, labels.EditProfile);
        }
        _activityTab.Label = labels.ActivityTab;
        _projectsTab.Label = labels.ProjectsTab;
        AutomationProperties.SetName(_activityTab, labels.ActivityTab);
        AutomationProperties.SetName(_projectsTab, labels.ProjectsTab);
        if (_tabs is not null)
        {
            AutomationProperties.SetName(_tabs, labels.TabListName);
        }
        if (_about is not null)
        {
            AutomationProperties.SetName(_about, labels.AboutName);
        }
        if (_main is not null)
        {
            AutomationProperties.SetName(_main, labels.ContentName);
        }
    }

    private void RefreshProfile()
    {
        var p = Profile ?? s_empty;
        if (_name is not null)
        {
            _name.Text = p.Name;
        }
        if (_avatar is not null)
        {
            _avatar.DisplayName = p.Name;
            _avatar.Source = p.Avatar;
            AutomationProperties.SetName(_avatar, Labels.AvatarName(p.Name));
        }
        if (_handle is not null)
        {
            _handle.Text = string.IsNullOrEmpty(p.Handle) ? string.Empty : "@" + p.Handle;
            _handle.IsVisible = !string.IsNullOrEmpty(p.Handle);
        }
        var hasHeadline = !string.IsNullOrWhiteSpace(p.Headline);
        if (_headline is not null)
        {
            _headline.Text = p.Headline;
        }
        if (_headlineGlow is not null)
        {
            _headlineGlow.IsVisible = hasHeadline;
        }
        if (_headlineDot is not null)
        {
            _headlineDot.IsVisible = hasHeadline && !string.IsNullOrEmpty(p.Handle);
        }
        if (_bio is not null)
        {
            // The bio is one paragraph: line breaks read as spaces.
            _bio.Text = (p.Bio ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ');
        }
        if (_bioGlow is not null)
        {
            _bioGlow.IsVisible = !string.IsNullOrWhiteSpace(p.Bio);
        }
        if (_location is not null)
        {
            _location.Text = p.Location;
        }
        if (_locationGlow is not null)
        {
            _locationGlow.IsVisible = !string.IsNullOrWhiteSpace(p.Location);
        }
        if (_website is not null)
        {
            _website.Content = p.Website;
            AutomationProperties.SetName(_website, p.Website);
        }
        if (_websiteGlow is not null)
        {
            _websiteGlow.IsVisible = !string.IsNullOrWhiteSpace(p.Website);
        }
        if (_joined is not null)
        {
            _joined.Text = string.IsNullOrWhiteSpace(p.JoinedText) ? string.Empty : Labels.Joined(p.JoinedText);
        }
        if (_joinedRow is not null)
        {
            _joinedRow.IsVisible = !string.IsNullOrWhiteSpace(p.JoinedText);
        }
        if (_cover is not null)
        {
            if (p.CoverBrush is { } brush)
            {
                _cover.Background = brush;
            }
            else
            {
                _cover.ClearValue(Border.BackgroundProperty);
            }
        }
        if (_coverImage is not null)
        {
            _coverImage.Source = p.Cover;
            _coverImage.IsVisible = p.Cover is not null;
        }
        _activityView.UpdateName(p.FirstName);
    }

    private static readonly ProfileDetails s_empty = new();

    private void RebuildStats()
    {
        if (_stats is null)
        {
            return;
        }
        _stats.Children.Clear();
        var stats = CurrentStats;
        foreach (var stat in stats)
        {
            var value = new TextBlock
            {
                Text = stat.Value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture),
                FontSize = 18,
                LineHeight = 25.2,
                LetterSpacing = -0.18,
                FontWeight = FontWeight.Medium,
                FontFeatures = s_tabular,
            };
            var label = new TextBlock { Text = stat.Label, FontSize = 12, LineHeight = 16.8 };
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
            var item = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Bottom };
            item.Children.Add(value);
            item.Children.Add(label);
            AutomationProperties.SetName(item, $"{value.Text} {stat.Label}");
            _stats.Children.Add(item);
        }
        if (_stats.Parent is Control frame)
        {
            frame.IsVisible = stats.Count > 0;
        }
        WatchItems();
    }

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private void RebuildActivity()
    {
        var items = CurrentActivity;
        _activityView.Build(items, (Profile ?? s_empty).FirstName);
        _activityTab.Count = items.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        WatchItems();
    }

    private void RebuildProjects()
    {
        var projects = CurrentProjects;
        if (_attached)
        {
            _projectsView.Build(projects, this);
        }
        _projectsTab.Count = projects.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }

    // Tabs -------------------------------------------------------------------------------------------------------------

    private sealed class ProfilePageTabCommand : System.Windows.Input.ICommand
    {
        private readonly ProfilePage _owner;
        private readonly ProfilePageTab _tab;

        public ProfilePageTabCommand(ProfilePage owner, ProfilePageTab tab)
        {
            _owner = owner;
            _tab = tab;
        }

#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _owner.SetCurrentValue(TabProperty, _tab);
    }

    private void OnTabChanged(ProfilePageTab oldTab, ProfilePageTab newTab)
    {
        _activityTab.IsSelected = newTab == ProfilePageTab.Activity;
        _projectsTab.IsSelected = newTab == ProfilePageTab.Projects;
        _tabs?.ArmGlide();
        _panelSwap?.Show(newTab == ProfilePageTab.Activity ? _activityView : _projectsView, animate: true);
        RaiseEvent(new ProfilePageTabChangedEventArgs(TabChangedEvent, oldTab, newTab));
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right) || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        // Two tabs: either arrow moves to the other one, wrapping; the selection follows the focus.
        e.Handled = true;
        var next = Tab == ProfilePageTab.Activity ? ProfilePageTab.Projects : ProfilePageTab.Activity;
        SetCurrentValue(TabProperty, next);
        (next == ProfilePageTab.Activity ? _activityTab : _projectsTab).Focus(NavigationMethod.Directional);
    }

    // Stars ------------------------------------------------------------------------------------------------------------

    internal void ToggleStar(ProfileProject project, ProfilePageStarButton? button)
    {
        var starred = !project.IsStarred;
        var count = Math.Max(0, project.StarCount + (starred ? 1 : -1));
        project.SetCurrentValue(ProfileProject.IsStarredProperty, starred);
        project.SetCurrentValue(ProfileProject.StarCountProperty, count);
        if (starred)
        {
            button?.Pop();
        }
        RaiseEvent(new ProfileStarChangedEventArgs(StarChangedEvent, project, starred, count));
    }

    // Share ------------------------------------------------------------------------------------------------------------

    /// <summary>The link Share copies now.</summary>
    public string EffectiveProfileUrl =>
        !string.IsNullOrWhiteSpace(ProfileUrl) ? ProfileUrl! : $"https://example.com/@{(Profile ?? s_empty).Handle}";

    /// <summary>Copies the profile link as Share does and shows Copied or Failed on the button. Returns whether it worked.</summary>
    public async Task<bool> ShareAsync()
    {
        var url = EffectiveProfileUrl;
        bool ok;
        try
        {
            ok = CopyHandler is { } copy ? await copy(url) : await ArcClipboard.CopyTextAsync(this, url);
        }
        catch (Exception)
        {
            ok = false;
        }
        if (_shareStack is not null)
        {
            _shareStack.Shown = ok ? 1 : 2;
        }
        ApplyLabels();
        _announcer?.Announce(ok ? Labels.CopiedAnnouncement(url) : Labels.CopyFailedAnnouncement);
        RestartShareTimer();
        RaiseEvent(new ProfileSharedEventArgs(SharedEvent, url, ok));
        return ok;
    }

    private void RestartShareTimer()
    {
        _shareTimer ??= new DispatcherTimer();
        _shareTimer.Stop();
        _shareTimer.Interval = ShareFeedbackDuration;
        _shareTimer.Tick -= OnShareTimer;
        _shareTimer.Tick += OnShareTimer;
        _shareTimer.Start();
    }

    private void OnShareTimer(object? sender, EventArgs e)
    {
        _shareTimer?.Stop();
        if (_shareStack is not null)
        {
            _shareStack.Shown = 0;
        }
        ApplyLabels();
    }

    private void OnShareClick(object? sender, RoutedEventArgs e) => _ = ShareAsync();

    private async void OnWebsiteClick(object? sender, RoutedEventArgs e)
    {
        var site = (Profile ?? s_empty).Website.Trim();
        if (site.Length == 0)
        {
            return;
        }
        var text = site.StartsWith("http://", StringComparison.Ordinal) || site.StartsWith("https://", StringComparison.Ordinal)
            ? site
            : "https://" + site;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            try
            {
                await launcher.LaunchUriAsync(uri);
            }
            catch (Exception)
            {
                // The platform could not open it; nothing to undo.
            }
        }
    }

    // Edit sheet -------------------------------------------------------------------------------------------------------

    /// <summary>Opens the edit sheet with a fresh draft of <see cref="Profile"/> (when <see cref="IsEditable"/>).</summary>
    public void OpenEditor()
    {
        if (IsEditable)
        {
            SetCurrentValue(IsEditingProperty, true);
        }
    }

    /// <summary>Closes the edit sheet and drops the draft; the focus returns to Edit profile.</summary>
    public void CloseEditor() => SetCurrentValue(IsEditingProperty, false);

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        OpenEditor();
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        CloseEditor();
    }

    private void OnSheetDismiss(object? sender, EventArgs e) => CloseEditor();

    private void ShowSheet()
    {
        if (!IsEditable)
        {
            SetCurrentValue(IsEditingProperty, false);
            return;
        }
        if (_sheet is null || _scrim is null)
        {
            return;
        }
        PseudoClasses.Set(EditingPseudoClass, true);
        _sheet.Load(Profile ?? s_empty, Labels, BioMaxLength);
        _sheet.UpdateWidth(Bounds.Width, 420);
        EnsureSheetMotion();
        var version = ++_sheetVersion;
        var wasShown = _sheetShown;
        _sheetShown = true;
        _sheet.IsVisible = true;
        _sheet.IsHitTestVisible = true;
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var width = SheetTravel();
        if (!wasShown)
        {
            _sheetSlide!.Jump(level == ArcMotionLevel.Full ? width : 0);
            _sheetFade!.Jump(level == ArcMotionLevel.Reduced ? 0 : 1);
        }
        // Arc: the sheet slides in from 100% on the Smooth spring (fitted ω 13.0) while the scrim fades in over 240ms
        // (the theme, on :editing); reduced motion fades the sheet in over 160ms instead of sliding it.
        if (level == ArcMotionLevel.Full)
        {
            _sheetFade!.Jump(1);
            _ = _sheetSlide!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _sheetSlide!.Jump(0);
            _ = _sheetFade!.AnimateTo(1, MotionSpec.Tween(ReducedSheetIn, EaseOut));
        }
        else
        {
            _sheetSlide!.Jump(0);
            _sheetFade!.Jump(1);
        }
        var method = FocusMethod;
        if (_sheet.NameBox?.Focus(method) != true)
        {
            // The sheet was hidden until now: its fields can take the focus after the next layout.
            Dispatcher.UIThread.Post(() =>
            {
                if (version == _sheetVersion && _sheetShown)
                {
                    _sheet?.FocusFirst(method);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private double SheetTravel()
    {
        var w = _sheet?.Bounds.Width ?? 0;
        if (!(w > 0))
        {
            w = Math.Min(420, Bounds.Width > 0 ? Bounds.Width : 420);
        }
        return w;
    }

    private void HideSheet(bool restoreFocus)
    {
        if (!_sheetShown || _sheet is null || _scrim is null)
        {
            PseudoClasses.Set(EditingPseudoClass, false);
            return;
        }
        PseudoClasses.Set(EditingPseudoClass, false);
        var version = ++_sheetVersion;
        _sheet.IsHitTestVisible = false;
        if (restoreFocus && (_sheet.IsKeyboardFocusWithin || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is null))
        {
            _edit?.Focus(FocusMethod);
        }
        EnsureSheetMotion();
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        Task sheetDone;
        if (level == ArcMotionLevel.Full)
        {
            sheetDone = _sheetSlide!.AnimateTo(SheetTravel(), MotionSpec.Spring(SheetExitSpring));
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            sheetDone = _sheetFade!.AnimateTo(0, MotionSpec.Tween(ReducedSheetOut, EaseOut));
        }
        else
        {
            _sheetFade!.Jump(0);
            sheetDone = Task.CompletedTask;
        }
        _ = FinishHideAsync(sheetDone, version);
    }

    private async Task FinishHideAsync(Task done, int version)
    {
        if (!done.IsCompleted)
        {
            await done;
        }
        if (version != _sheetVersion || IsEditing)
        {
            return;
        }
        _sheetShown = false;
        if (_sheet is not null)
        {
            _sheet.IsVisible = false;
        }
    }

    private void EnsureSheetMotion()
    {
        if (_sheetSlide is not null)
        {
            return;
        }
        // Code-driven: the sheet mounts only while open, enters on Smooth and leaves on a faster critically damped spring
        // (no preset), and its travel is its run-time width.
        _sheetSlide = new MotionValue<double>(this, 0, MotionVectors.Double);
        _sheetSlide.Changed += v => _sheetTranslate.X = v;
        _sheetFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _sheetFade.Changed += v =>
        {
            if (_sheet is not null)
            {
                _sheet.Opacity = v;
            }
        };
    }

    private async void OnSheetSubmit(object? sender, EventArgs e)
    {
        if (_sheet is null || IsSaving || !_sheet.IsDirty)
        {
            return;
        }
        if (!_sheet.Submit())
        {
            return;
        }
        var draft = _sheet.BuildDraft();
        var previous = Profile ?? s_empty;
        var version = ++_saveVersion;
        IsSaving = true;
        _sheet.IsSaving = true;
        _sheet.ShowAlert(null);
        try
        {
            if (SaveHandler is { } save)
            {
                await save(draft);
            }
            else
            {
                await Task.Delay(SimulatedSaveDelay);
            }
        }
        catch (Exception ex)
        {
            if (version != _saveVersion)
            {
                return;
            }
            IsSaving = false;
            var message = string.IsNullOrWhiteSpace(ex.Message) ? Labels.SaveFailed : ex.Message;
            if (_sheet is not null)
            {
                _sheet.IsSaving = false;
                if (IsEditing)
                {
                    _sheet.ShowAlert(message);
                }
            }
            if (_announcer is not null)
            {
                _announcer.IsAssertive = true;
                _announcer.Announce(message);
                _announcer.IsAssertive = false;
            }
            RaiseEvent(new ProfileSaveFailedEventArgs(SaveFailedEvent, draft, ex));
            return;
        }
        if (version != _saveVersion)
        {
            return;
        }
        IsSaving = false;
        if (_sheet is not null)
        {
            _sheet.IsSaving = false;
        }
        var changed = ProfilePageValidation.Changed(previous, draft);
        SetCurrentValue(ProfileProperty, draft);
        CloseEditor();
        foreach (var field in new[] { ProfileFields.Name, ProfileFields.Headline, ProfileFields.Location, ProfileFields.Website, ProfileFields.Bio })
        {
            if (changed.HasFlag(field) && GlowOf(field) is { IsVisible: true } glow)
            {
                glow.Flash();
            }
        }
        _announcer?.Announce(Labels.SavedAnnouncement);
        RaiseEvent(new ProfileSavedEventArgs(SavedEvent, previous, draft, changed));
    }
}
