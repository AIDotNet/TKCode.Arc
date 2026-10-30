using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(HeaderPart, typeof(RolesPermissionsHeaderPanel))]
[TemplatePart(SubtitlePart, typeof(WrapPanel))]
[TemplatePart(MembersPart, typeof(AvatarGroup))]
[TemplatePart(ViewportPart, typeof(RolesPermissionsViewport))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(GridPart, typeof(RolesPermissionsGrid))]
[TemplatePart(LockedLegendPart, typeof(TextBlock))]
[TemplatePart(UnsavedLegendPart, typeof(TextBlock))]
[TemplatePart(BarSlotPart, typeof(Panel))]
[TemplatePart(BarPart, typeof(Border))]
[TemplatePart(BarTitlePart, typeof(ArcContentSwap))]
[TemplatePart(BarTextPart, typeof(ArcContentSwap))]
[TemplatePart(DiscardPart, typeof(Button))]
[TemplatePart(SavePart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[TemplatePart(AlertPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(FramedPseudoClass, NarrowPseudoClass, CompactPseudoClass, DirtyPseudoClass, SavingPseudoClass, SavedPseudoClass,
    FailedPseudoClass, ScrolledPseudoClass)]
public partial class RolesPermissions
{
    public const string HeaderPart = "PART_Header";
    public const string SubtitlePart = "PART_Subtitle";
    public const string MembersPart = "PART_Members";
    public const string ViewportPart = "PART_Viewport";
    public const string ScrollerPart = "PART_Scroller";
    public const string GridPart = "PART_Grid";
    public const string LockedLegendPart = "PART_LockedLegend";
    public const string UnsavedLegendPart = "PART_UnsavedLegend";
    public const string BarSlotPart = "PART_BarSlot";
    public const string BarPart = "PART_Bar";
    public const string BarTitlePart = "PART_BarTitle";
    public const string BarTextPart = "PART_BarText";
    public const string DiscardPart = "PART_Discard";
    public const string SavePart = "PART_Save";
    public const string AnnouncerPart = "PART_Announcer";
    public const string AlertPart = "PART_Alert";

    /// <summary>Set while <see cref="IsFramed"/> is true: the bordered card.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the content is narrower than 760: label column 200 at least, role columns 92.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the content is narrower than 540: label column 148, role columns 80, tighter insets, no group counts.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while there are unsaved changes.</summary>
    public const string DirtyPseudoClass = ":dirty";

    /// <summary>Set while the save handler runs.</summary>
    public const string SavingPseudoClass = ":saving";

    /// <summary>Set while the bar reads "saved".</summary>
    public const string SavedPseudoClass = ":saved";

    /// <summary>Set after a failed save until the next change or save.</summary>
    public const string FailedPseudoClass = ":failed";

    /// <summary>Set while the matrix is scrolled sideways (the sticky label column shows its edge).</summary>
    public const string ScrolledPseudoClass = ":scrolled";

    #region Token-driven internals

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<RolesPermissions, double>(nameof(NarrowWidth), 760);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<RolesPermissions, double>(nameof(CompactWidth), 540);

    internal static readonly StyledProperty<double> BarSlotHeightProperty =
        AvaloniaProperty.Register<RolesPermissions, double>(nameof(BarSlotHeight), 76);

    internal static readonly StyledProperty<TimeSpan> GroupRippleStepProperty =
        AvaloniaProperty.Register<RolesPermissions, TimeSpan>(nameof(GroupRippleStep), TimeSpan.FromMilliseconds(60));

    internal static readonly StyledProperty<TimeSpan> TableRippleStepProperty =
        AvaloniaProperty.Register<RolesPermissions, TimeSpan>(nameof(TableRippleStep), TimeSpan.FromMilliseconds(30));

    internal static readonly StyledProperty<TimeSpan> SavedHoldProperty =
        AvaloniaProperty.Register<RolesPermissions, TimeSpan>(nameof(SavedHold), TimeSpan.FromMilliseconds(1600));

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double BarSlotHeight { get => GetValue(BarSlotHeightProperty); set => SetValue(BarSlotHeightProperty, value); }

    internal TimeSpan GroupRippleStep { get => GetValue(GroupRippleStepProperty); set => SetValue(GroupRippleStepProperty, value); }

    internal TimeSpan TableRippleStep { get => GetValue(TableRippleStepProperty); set => SetValue(TableRippleStepProperty, value); }

    internal TimeSpan SavedHold { get => GetValue(SavedHoldProperty); set => SetValue(SavedHoldProperty, value); }

    #endregion

    private readonly RolesPermissionsModel _model = new();
    private readonly List<RolesPermissionsRole> _roles = new();
    private readonly List<RolesPermissionsGroup> _groups = new();
    private readonly List<string> _permissionKeys = new();
    private readonly HashSet<RolesPermissionsRole> _watchedRoles = new();
    private readonly HashSet<RolesPermissionsGroup> _watchedGroups = new();
    private INotifyCollectionChanged? _observedRoles;
    private INotifyCollectionChanged? _observedGroups;
    private INotifyCollectionChanged? _observedMembers;

    private RolesPermissionsHeaderPanel? _header;
    private WrapPanel? _subtitle;
    private AvatarGroup? _members;
    private RolesPermissionsViewport? _viewport;
    private ScrollViewer? _scroller;
    private RolesPermissionsGrid? _grid;
    private TextBlock? _lockedLegend;
    private TextBlock? _unsavedLegend;
    private Panel? _barSlot;
    private Border? _bar;
    private ArcContentSwap? _barTitle;
    private ArcContentSwap? _barText;
    private Button? _discard;
    private Button? _save;
    private ProjectsAnnouncer? _announcer;
    private ProjectsAnnouncer? _alert;

    private RollingDigits? _memberDigits;
    private RollingDigits? _roleDigits;
    private RollingDigits? _countDigits;
    private TextBlock? _pendingWord;
    private TextMorph? _changeWord;
    private string? _barTextValue;
    private BarTitleKind _barTitleKind = BarTitleKind.None;
    private object? _saveContentKind;

    private MotionValue<double>? _slotHeight;
    private MotionValue<double>? _barOpacity;
    private MotionValue<double>? _barShift;
    private readonly TranslateTransform _barTranslate = new();
    private bool _barOpen;

    private MenuSurface? _menu;
    private RolesPermissionsRoleHeader? _menuHeader;
    private Control? _menuFocusTarget;

    private CancellationTokenSource? _saveCancellation;
    private int _saveGeneration;
    private bool _dataAttached;
    private bool _syncingGrants;
    private bool _mutatingRoles;
    private bool _syncingExpanded;

    /// <summary>True while <see cref="Grants"/> holds a set the block wrote itself (not one the app gave).</summary>
    private bool _grantsEchoed;

    private enum BarTitleKind
    {
        None,
        Pending,
        Saved,
        Failed,
    }

    static RolesPermissions()
    {
        BlockFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<RolesPermissions>(false);
    }

    public RolesPermissions()
    {
        Roles.CollectionChanged += (_, _) =>
        {
            if (RolesSource is null && !_mutatingRoles)
            {
                RebuildData();
            }
        };
        Groups.CollectionChanged += (_, _) =>
        {
            if (GroupsSource is null)
            {
                RebuildData();
            }
        };
        Members.CollectionChanged += (_, _) => UpdateMembers();
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    /// <summary>The roles shown now (from <see cref="RolesSource"/> or <see cref="Roles"/>, plus unsaved copies), in column order.</summary>
    public IReadOnlyList<RolesPermissionsRole> EffectiveRoles => _roles;

    /// <summary>The groups shown now, in order.</summary>
    public IReadOnlyList<RolesPermissionsGroup> EffectiveGroups => _groups;

    internal RolesPermissionsGrid? Grid => _grid;

    internal RolesPermissionsModel Model => _model;

    internal ProjectsAnnouncer? Announcer => _announcer;

    internal ProjectsAnnouncer? Alert => _alert;

    internal Border? Bar => _bar;

    internal bool IsBarOpen => _barOpen;

    internal Button? SaveButton => _save;

    internal Button? DiscardButton => _discard;

    internal MenuSurface? Menu => _menu;

    internal string? BarText => _barTextValue;

    #region Public methods

    /// <summary>Whether <paramref name="roleKey"/> has <paramref name="permissionKey"/> now (saved or not).</summary>
    public bool IsGranted(string roleKey, string permissionKey)
    {
        var role = FindRole(roleKey);
        return role is not null && _model.IsGranted(role, permissionKey);
    }

    /// <summary>Whether the cell differs from its saved value.</summary>
    public bool IsUnsaved(string roleKey, string permissionKey)
    {
        var role = FindRole(roleKey);
        return role is not null && _model.IsUnsaved(role, permissionKey);
    }

    /// <summary>
    /// Sets one cell as if the person switched it (no effect on locked roles or while saving). Returns true when the
    /// value changed.
    /// </summary>
    public bool SetGrant(string roleKey, string permissionKey, bool isGranted)
    {
        var role = FindRole(roleKey);
        var permission = FindPermission(permissionKey);
        if (role is null || permission is null || role.IsLocked || SaveState == RolesPermissionsSaveState.Saving)
        {
            return false;
        }
        return SetCell(role, permission, isGranted, RolesPermissionsChangeSource.Code, announce: false);
    }

    /// <summary>Applies the group checkbox rule for one role (mixed or off → all on, on → all off). Returns the cells that changed.</summary>
    public int ToggleGroup(RolesPermissionsGroup group, string roleKey)
    {
        var role = FindRole(roleKey);
        if (role is null || !_groups.Contains(group))
        {
            return 0;
        }
        return ToggleGroupCore(group, role, RolesPermissionsChangeSource.Code, announce: false);
    }

    /// <summary>Copies a role into a new, unsaved role inserted on its right ("Manager (copy)"). Returns the copy, or null.</summary>
    public RolesPermissionsRole? DuplicateRole(string roleKey)
    {
        var role = FindRole(roleKey);
        return role is null ? null : DuplicateCore(role, focus: false);
    }

    /// <summary>Removes a role that was duplicated and not saved. Returns false for any other role.</summary>
    public bool DeleteRole(string roleKey)
    {
        var role = FindRole(roleKey);
        return role is not null && DeleteCore(role, isDiscard: false, focus: false);
    }

    /// <summary>Puts a role's cells back to their saved values (a new role's: to what it was copied with). Returns how many changed.</summary>
    public int RevertRole(string roleKey)
    {
        var role = FindRole(roleKey);
        return role is null ? 0 : RevertCore(role, focus: false);
    }

    /// <summary>Throws away every unsaved change (copies included), as the Discard button does. Returns how many went away.</summary>
    public int Discard() => DiscardCore(focus: false);

    /// <summary>
    /// Saves as the Save button does: raises <see cref="SaveStarting"/> and awaits <see cref="SaveHandler"/>. Returns true
    /// when the changes were saved; false when there was nothing to save, a save was already running, it was cancelled or failed.
    /// </summary>
    public Task<bool> SaveAsync() => SaveCore(fromButton: false);

    /// <summary>Cancels a running save (the handler's token); the changes stay unsaved.</summary>
    public void CancelSave() => _saveCancellation?.Cancel();

    /// <summary>Folds or unfolds a group (same as its disclosure button).</summary>
    public void SetGroupExpanded(RolesPermissionsGroup group, bool isExpanded) => group.IsExpanded = isExpanded;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        if (_discard is not null)
        {
            _discard.Click -= OnDiscardClick;
        }
        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }
        _header = e.NameScope.Find<RolesPermissionsHeaderPanel>(HeaderPart);
        _subtitle = e.NameScope.Find<WrapPanel>(SubtitlePart);
        _members = e.NameScope.Find<AvatarGroup>(MembersPart);
        _viewport = e.NameScope.Find<RolesPermissionsViewport>(ViewportPart);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _grid = e.NameScope.Find<RolesPermissionsGrid>(GridPart);
        _lockedLegend = e.NameScope.Find<TextBlock>(LockedLegendPart);
        _unsavedLegend = e.NameScope.Find<TextBlock>(UnsavedLegendPart);
        _barSlot = e.NameScope.Find<Panel>(BarSlotPart);
        _bar = e.NameScope.Find<Border>(BarPart);
        _barTitle = e.NameScope.Find<ArcContentSwap>(BarTitlePart);
        _barText = e.NameScope.Find<ArcContentSwap>(BarTextPart);
        _discard = e.NameScope.Find<Button>(DiscardPart);
        _save = e.NameScope.Find<Button>(SavePart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        _alert = e.NameScope.Find<ProjectsAnnouncer>(AlertPart);
        if (_grid is not null)
        {
            _grid.Owner = this;
            _grid.Scroller = _scroller;
        }
        if (_viewport is not null)
        {
            _viewport.Grid = _grid;
        }
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        if (_discard is not null)
        {
            _discard.Click += OnDiscardClick;
        }
        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }
        if (_bar is not null)
        {
            _bar.RenderTransform = _barTranslate;
            _bar.IsVisible = false;
            AutomationProperties.SetName(_bar, Labels.ReviewBarName);
        }
        if (_barSlot is not null)
        {
            _barSlot.Height = 0;
        }
        _barOpen = false;
        _barTitleKind = BarTitleKind.None;
        _saveContentKind = null;
        _countDigits = null;
        BuildSubtitle();
        UpdateLegends();
        UpdateMembers();
        RebuildData();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_dataAttached)
        {
            _dataAttached = true;
            RebuildData();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _saveCancellation?.Cancel();
        _menu?.Close(returnFocus: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RolesSourceProperty)
        {
            Observe(ref _observedRoles, change.OldValue, change.NewValue, OnRolesSourceChanged);
            RebuildData();
        }
        else if (change.Property == GroupsSourceProperty)
        {
            Observe(ref _observedGroups, change.OldValue, change.NewValue, OnGroupsSourceChanged);
            RebuildData();
        }
        else if (change.Property == MembersSourceProperty)
        {
            Observe(ref _observedMembers, change.OldValue, change.NewValue, OnMembersSourceChanged);
            UpdateMembers();
        }
        else if (change.Property == SavedGrantsProperty)
        {
            if (!_syncingGrants)
            {
                var saved = change.GetNewValue<IReadOnlySet<RolesPermissionsGrant>?>() ?? new HashSet<RolesPermissionsGrant>();
                if (Grants is null || (_grantsEchoed && _model.Current.SetEquals(_model.Saved)))
                {
                    _model.Reset(saved, null);
                }
                else
                {
                    _model.SetSaved(saved);
                }
                Refresh(null, animate: false);
            }
        }
        else if (change.Property == GrantsProperty)
        {
            if (!_syncingGrants)
            {
                _grantsEchoed = false;
                var current = change.GetNewValue<IReadOnlySet<RolesPermissionsGrant>?>();
                _model.SetCurrent(current ?? (IEnumerable<RolesPermissionsGrant>?)SavedGrants ?? Array.Empty<RolesPermissionsGrant>());
                Refresh(null, animate: true);
            }
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
            UpdateTier();
        }
        else if (change.Property == LabelsProperty || change.Property == WorkspaceNameProperty)
        {
            BuildSubtitle();
            UpdateLegends();
            if (change.Property == LabelsProperty)
            {
                RefreshHeaders(animate: false);
                foreach (var cell in _grid?.AllCells() ?? [])
                {
                    cell.UpdateAutomationName();
                }
                UpdateBar(animate: false);
            }
        }
        else if (change.Property == MemberCountProperty)
        {
            UpdateCounts();
        }
        else if (change.Property == BoundsProperty || change.Property == PaddingProperty || change.Property == NarrowWidthProperty ||
                 change.Property == CompactWidthProperty)
        {
            UpdateTier();
        }
    }

    private static void Observe(ref INotifyCollectionChanged? field, object? oldValue, object? newValue, NotifyCollectionChangedEventHandler handler)
    {
        if (field is not null)
        {
            field.CollectionChanged -= handler;
        }
        field = newValue as INotifyCollectionChanged;
        if (field is not null)
        {
            field.CollectionChanged += handler;
        }
    }

    private void OnRolesSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_mutatingRoles)
        {
            RebuildData();
        }
    }

    private void OnGroupsSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildData();

    private void OnMembersSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateMembers();

    /// <summary>Picks the width tier from the content width (the card, or the page minus the flat margin).</summary>
    private void UpdateTier()
    {
        var width = Bounds.Width - Padding.Left - Padding.Right - (IsFramed ? 2 : 0);
        if (width <= 0)
        {
            return;
        }
        var narrow = width < NarrowWidth;
        var compact = width < CompactWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow && !compact);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_grid is not null)
        {
            ((IPseudoClasses)_grid.Classes).Set(NarrowPseudoClass, narrow && !compact);
            ((IPseudoClasses)_grid.Classes).Set(CompactPseudoClass, compact);
        }
        if (_pendingWord is not null)
        {
            _pendingWord.IsVisible = !compact;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null || _grid is null)
        {
            return;
        }
        _grid.ScrollOffset = _scroller.Offset.X;
        PseudoClasses.Set(ScrolledPseudoClass, _scroller.Offset.X > 0.5);
    }

    #endregion

    #region Data

    private RolesPermissionsRole? FindRole(string key) => _roles.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));

    private RolesPermissionsItem? FindPermission(string key)
    {
        foreach (var group in _groups)
        {
            foreach (var permission in group.Permissions)
            {
                if (string.Equals(permission.Key, key, StringComparison.Ordinal))
                {
                    return permission;
                }
            }
        }
        return null;
    }

    private void RebuildData()
    {
        foreach (var role in _watchedRoles)
        {
            role.PropertyChanged -= OnRolePropertyChanged;
        }
        _watchedRoles.Clear();
        foreach (var group in _watchedGroups)
        {
            group.PropertyChanged -= OnGroupPropertyChanged;
            group.Permissions.CollectionChanged -= OnPermissionsChanged;
        }
        _watchedGroups.Clear();

        var newRoles = _roles.Where(r => r.IsNew).ToList();
        _roles.Clear();
        foreach (var item in RolesSource ?? Roles)
        {
            if (item is RolesPermissionsRole role && !_roles.Contains(role))
            {
                _roles.Add(role);
            }
        }
        // Copies the source cannot hold (a read-only list) stay until they are saved, discarded or removed.
        foreach (var copy in newRoles)
        {
            if (!_roles.Contains(copy) && _model.IsNew(copy))
            {
                _roles.Add(copy);
            }
        }
        _groups.Clear();
        foreach (var item in GroupsSource ?? Groups)
        {
            if (item is RolesPermissionsGroup group && !_groups.Contains(group))
            {
                _groups.Add(group);
            }
        }
        _permissionKeys.Clear();
        foreach (var group in _groups)
        {
            foreach (var permission in group.Permissions)
            {
                _permissionKeys.Add(permission.Key);
            }
        }
        foreach (var role in _roles)
        {
            role.PropertyChanged += OnRolePropertyChanged;
            _watchedRoles.Add(role);
        }
        foreach (var group in _groups)
        {
            group.PropertyChanged += OnGroupPropertyChanged;
            group.Permissions.CollectionChanged += OnPermissionsChanged;
            _watchedGroups.Add(group);
        }
        if (Grants is null && SavedGrants is not null && _model.Saved.Count == 0 && _model.Current.Count == 0)
        {
            _model.Reset(SavedGrants, null);
        }
        _grid?.Build(_roles, _groups);
        Refresh(null, animate: false);
        UpdateCounts();
    }

    private void OnPermissionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildData();

    private void OnRolePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not RolesPermissionsRole role)
        {
            return;
        }
        if (e.Property == RolesPermissionsRole.IsLockedProperty)
        {
            Refresh(null, animate: false);
        }
        else if (e.Property == RolesPermissionsRole.NameProperty || e.Property == RolesPermissionsRole.MemberCountProperty ||
                 e.Property == RolesPermissionsRole.IsNewProperty)
        {
            var header = _grid?.HeaderOf(role);
            header?.ResetText();
            header?.Refresh();
            foreach (var cell in _grid?.AllCells().Where(c => ReferenceEquals(c.Role, role)) ?? [])
            {
                cell.UpdateAutomationName();
            }
            UpdateBar(animate: true);
        }
    }

    private void OnGroupPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not RolesPermissionsGroup group)
        {
            return;
        }
        if (e.Property == RolesPermissionsGroup.IsExpandedProperty && !_syncingExpanded)
        {
            _grid?.SetExpanded(group, group.IsExpanded, animate: true);
        }
        else if (e.Property == RolesPermissionsGroup.NameProperty)
        {
            RebuildData();
        }
    }

    private void UpdateMembers()
    {
        if (_members is not null)
        {
            _members.ItemsSource = MembersSource ?? Members;
            _members.Label = Labels.MembersName;
        }
        UpdateCounts();
    }

    private int MembersCount()
    {
        if (MemberCount is { } count)
        {
            return count;
        }
        var n = 0;
        foreach (var _ in MembersSource ?? Members)
        {
            n++;
        }
        return n;
    }

    /// <summary>Builds the subtitle from its format: the workspace as text, the two counts as rolling digits.</summary>
    private void BuildSubtitle()
    {
        if (_subtitle is null)
        {
            return;
        }
        _subtitle.Children.Clear();
        _memberDigits = null;
        _roleDigits = null;
        var labels = Labels;
        var hasWorkspace = !string.IsNullOrEmpty(WorkspaceName);
        var format = hasWorkspace ? labels.SubtitleFormat : labels.SubtitleNoWorkspaceFormat;
        var memberSlot = hasWorkspace ? 1 : 0;
        var roleSlot = hasWorkspace ? 2 : 1;
        var literal = new System.Text.StringBuilder();
        void FlushLiteral()
        {
            if (literal.Length > 0)
            {
                var text = new TextBlock { Text = literal.ToString(), VerticalAlignment = VerticalAlignment.Center };
                text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable(DesignTokens.ArcDesignTokenKind.TextSecondary));
                _subtitle.Children.Add(text);
                literal.Clear();
            }
        }
        for (var i = 0; i < format.Length; i++)
        {
            var ch = format[i];
            var end = ch == '{' ? format.IndexOf('}', i) : -1;
            if (end > i && int.TryParse(format.AsSpan(i + 1, end - i - 1), out var slot))
            {
                if (hasWorkspace && slot == 0)
                {
                    literal.Append(WorkspaceName);
                }
                else if (slot == memberSlot || slot == roleSlot)
                {
                    FlushLiteral();
                    var digits = new RollingDigits { UseGrouping = true, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
                    digits.Bind(RollingDigits.ForegroundProperty, digits.GetResourceObservable(DesignTokens.ArcDesignTokenKind.Foreground));
                    _subtitle.Children.Add(digits);
                    if (slot == memberSlot)
                    {
                        _memberDigits = digits;
                    }
                    else
                    {
                        _roleDigits = digits;
                    }
                }
                i = end;
                continue;
            }
            literal.Append(ch);
        }
        FlushLiteral();
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        if (_memberDigits is not null)
        {
            _memberDigits.Value = MembersCount();
        }
        if (_roleDigits is not null)
        {
            _roleDigits.Value = _roles.Count;
        }
    }

    private void UpdateLegends()
    {
        if (_lockedLegend is not null)
        {
            _lockedLegend.Text = Labels.LockedLegend;
        }
        if (_unsavedLegend is not null)
        {
            _unsavedLegend.Text = Labels.UnsavedLegend;
        }
        if (_bar is not null)
        {
            AutomationProperties.SetName(_bar, Labels.ReviewBarName);
        }
        if (_discard is not null)
        {
            _discard.Content = Labels.Discard;
        }
    }

    #endregion

    #region Changes

    /// <summary>
    /// Pushes the model into every cell and header. <paramref name="delays"/> gives the ripple delay of the cells that
    /// changed (others change at once); without <paramref name="animate"/> everything jumps.
    /// </summary>
    private void Refresh(Func<RolesPermissionsCell, TimeSpan>? delays, bool animate)
    {
        if (_grid is not null)
        {
            foreach (var row in _grid.Rows)
            {
                if (row.Kind == RolesPermissionsRowKind.Header)
                {
                    continue;
                }
                IReadOnlyList<string>? groupKeys = row.Kind == RolesPermissionsRowKind.Group
                    ? row.Group!.Permissions.Select(p => p.Key).ToList()
                    : null;
                foreach (var cell in row.Cells)
                {
                    if (cell.IsHitTestVisible == false && _grid.ColumnOf(cell.Role) is null)
                    {
                        continue;
                    }
                    RolesPermissionsCheck state;
                    bool unsaved;
                    if (groupKeys is not null)
                    {
                        state = _model.GroupState(cell.Role, groupKeys);
                        unsaved = false;
                    }
                    else
                    {
                        state = _model.IsGranted(cell.Role, cell.Permission!.Key) ? RolesPermissionsCheck.On : RolesPermissionsCheck.Off;
                        unsaved = _model.IsUnsaved(cell.Role, cell.Permission.Key);
                    }
                    cell.Apply(state, unsaved, delays?.Invoke(cell) ?? TimeSpan.Zero, animate);
                }
            }
        }
        RefreshHeaders(animate);
        UpdatePending(animate);
    }

    private void RefreshHeaders(bool animate)
    {
        if (_grid is null)
        {
            return;
        }
        foreach (var column in _grid.Columns)
        {
            if (column.Removing)
            {
                continue;
            }
            column.Header.SetChanges(_model.RoleHasChanges(column.Role, _permissionKeys), animate);
        }
    }

    private void UpdatePending(bool animate)
    {
        PendingChangeCount = _model.PendingCount(_roles, _permissionKeys);
        HasUnsavedChanges = PendingChangeCount > 0;
        PseudoClasses.Set(DirtyPseudoClass, HasUnsavedChanges);
        PushGrants();
        UpdateBar(animate);
    }

    private void PushGrants()
    {
        var current = Grants;
        if (current is not null && current.SetEquals(_model.Current))
        {
            return;
        }
        if (current is null && SavedGrants is not null && _model.Current.SetEquals(SavedGrants))
        {
            return;
        }
        if (current is null && _model.Current.SetEquals(_model.Saved))
        {
            return;
        }
        _syncingGrants = true;
        try
        {
            SetCurrentValue(GrantsProperty, new HashSet<RolesPermissionsGrant>(_model.Current));
            _grantsEchoed = true;
        }
        finally
        {
            _syncingGrants = false;
        }
    }

    /// <summary>A person's (or code's) change came in: leave a finished "saved" or a failure behind.</summary>
    private void BeginEdit()
    {
        if (SaveState is RolesPermissionsSaveState.Saved or RolesPermissionsSaveState.Failed)
        {
            _saveGeneration++;
            SetSaveState(RolesPermissionsSaveState.Idle);
            ErrorText = null;
        }
    }

    private bool IsBusy()
    {
        if (SaveState != RolesPermissionsSaveState.Saving)
        {
            return false;
        }
        _announcer?.Announce(Labels.BusyMessage);
        return true;
    }

    internal void OnCellActivated(RolesPermissionsCell cell)
    {
        if (cell.IsLocked)
        {
            cell.Shake();
            _announcer?.Announce(ProjectsText.Format(Labels.LockedFormat, cell.Role.Name));
            return;
        }
        if (IsBusy())
        {
            return;
        }
        if (cell.IsGroupCell)
        {
            ToggleGroupCore(cell.Group, cell.Role, RolesPermissionsChangeSource.Group, announce: true);
        }
        else
        {
            var value = !_model.IsGranted(cell.Role, cell.Permission!.Key);
            SetCell(cell.Role, cell.Permission, value, RolesPermissionsChangeSource.Cell, announce: true);
        }
    }

    private bool SetCell(RolesPermissionsRole role, RolesPermissionsItem permission, bool value, RolesPermissionsChangeSource source, bool announce)
    {
        BeginEdit();
        if (!_model.Set(role, permission.Key, value))
        {
            return false;
        }
        Refresh(null, animate: true);
        if (announce)
        {
            _announcer?.Announce(ProjectsText.Format(value ? Labels.GrantedFormat : Labels.RevokedFormat, role.Name, permission.Name));
        }
        RaiseEvent(new RolesPermissionsGrantChangedEventArgs(GrantChangedEvent, this, role, permission, value,
            _model.IsUnsaved(role, permission.Key), source));
        return true;
    }

    private int ToggleGroupCore(RolesPermissionsGroup group, RolesPermissionsRole role, RolesPermissionsChangeSource source, bool announce)
    {
        if (role.IsLocked || SaveState == RolesPermissionsSaveState.Saving)
        {
            return 0;
        }
        BeginEdit();
        var keys = group.Permissions.Select(p => p.Key).ToList();
        var (changed, value) = _model.ToggleGroup(role, keys);
        if (changed.Count == 0)
        {
            return 0;
        }
        // Arc's ripple: each changed cell waits 60ms × (its place in the group + 1); unchanged cells and the group cell do not wait.
        var step = GroupRippleStep;
        var changedSet = new HashSet<string>(changed, StringComparer.Ordinal);
        Refresh(cell =>
        {
            if (ReferenceEquals(cell.Role, role) && cell.Permission is { } p && ReferenceEquals(cell.Group, group) && changedSet.Contains(p.Key))
            {
                return TimeSpan.FromTicks(step.Ticks * (group.Permissions.IndexOf(p) + 1));
            }
            return TimeSpan.Zero;
        }, animate: true);
        if (announce)
        {
            _announcer?.Announce(ProjectsText.Format(value ? Labels.GroupGrantedFormat : Labels.GroupRevokedFormat, role.Name, group.Name));
        }
        foreach (var key in changed)
        {
            var permission = group.Permissions.First(p => p.Key == key);
            RaiseEvent(new RolesPermissionsGrantChangedEventArgs(GrantChangedEvent, this, role, permission, value,
                _model.IsUnsaved(role, key), source));
        }
        RaiseEvent(new RolesPermissionsGroupToggledEventArgs(GroupToggledEvent, this, group, role, value, changed.Count));
        return changed.Count;
    }

    /// <summary>The table-wide ripple of Discard and Revert: 30ms × the permission's row number across every group.</summary>
    private Func<RolesPermissionsCell, TimeSpan> TableRipple(Func<RolesPermissionsCell, bool> applies)
    {
        var step = TableRippleStep;
        var rows = new Dictionary<RolesPermissionsItem, int>();
        var index = 0;
        foreach (var group in _groups)
        {
            foreach (var permission in group.Permissions)
            {
                rows[permission] = index++;
            }
        }
        return cell => cell.Permission is { } p && applies(cell) && rows.TryGetValue(p, out var i)
            ? TimeSpan.FromTicks(step.Ticks * i)
            : TimeSpan.Zero;
    }

    private RolesPermissionsRole? DuplicateCore(RolesPermissionsRole source, bool focus)
    {
        if (!CanDuplicateRoles || IsBusy())
        {
            return null;
        }
        BeginEdit();
        var labels = Labels;
        var names = new HashSet<string>(_roles.Select(r => r.Name), StringComparer.Ordinal);
        var keys = new HashSet<string>(_roles.Select(r => r.Key), StringComparer.Ordinal);
        var copy = new RolesPermissionsRole(RolesPermissionsModel.CopyKey(source.Key, keys),
            RolesPermissionsModel.CopyName(source.Name, labels.CopyNameFormat, labels.CopyNameNumberedFormat, names))
        {
            IsNew = true,
        };
        var index = _roles.IndexOf(source) + 1;
        _roles.Insert(index, copy);
        InsertIntoSource(source, copy);
        copy.PropertyChanged += OnRolePropertyChanged;
        _watchedRoles.Add(copy);
        _model.AddNewRole(source, copy, _permissionKeys);
        var column = _grid?.InsertColumn(index, copy, animate: true);
        Refresh(null, animate: true);
        UpdateCounts();
        _announcer?.Announce(ProjectsText.Format(labels.DuplicatedFormat, source.Name, copy.Name));
        RaiseEvent(new RolesPermissionsRoleDuplicatedEventArgs(RoleDuplicatedEvent, this, source, copy));
        if (column is not null && _grid is not null)
        {
            _grid.SetRoving(column.Header);
            _grid.FollowColumn(column);
            if (focus)
            {
                _menuFocusTarget = column.Header;
            }
        }
        return copy;
    }

    private void InsertIntoSource(RolesPermissionsRole source, RolesPermissionsRole copy)
    {
        IList? list = RolesSource is null ? Roles : RolesSource as IList;
        if (list is null || list.IsReadOnly || list.IsFixedSize)
        {
            return;
        }
        var at = list.IndexOf(source);
        _mutatingRoles = true;
        try
        {
            list.Insert(at < 0 ? list.Count : at + 1, copy);
        }
        finally
        {
            _mutatingRoles = false;
        }
    }

    private void RemoveFromSource(RolesPermissionsRole role)
    {
        IList? list = RolesSource is null ? Roles : RolesSource as IList;
        if (list is null || list.IsReadOnly || list.IsFixedSize || !list.Contains(role))
        {
            return;
        }
        _mutatingRoles = true;
        try
        {
            list.Remove(role);
        }
        finally
        {
            _mutatingRoles = false;
        }
    }

    private bool DeleteCore(RolesPermissionsRole role, bool isDiscard, bool focus)
    {
        if (!role.IsNew || !_roles.Contains(role) || (!isDiscard && IsBusy()))
        {
            return false;
        }
        if (!isDiscard)
        {
            BeginEdit();
        }
        var index = _roles.IndexOf(role);
        _roles.Remove(role);
        RemoveFromSource(role);
        role.PropertyChanged -= OnRolePropertyChanged;
        _watchedRoles.Remove(role);
        _model.RemoveRole(role.Key);
        role.IsNew = false;
        _grid?.RemoveColumn(role, animate: true);
        UpdateCounts();
        if (!isDiscard)
        {
            Refresh(null, animate: true);
            _announcer?.Announce(ProjectsText.Format(Labels.RemovedFormat, role.Name));
            var left = index > 0 ? _grid?.HeaderOf(_roles[index - 1]) : _grid?.Columns.FirstOrDefault(c => !c.Removing)?.Header;
            _grid?.SetRoving(left);
            if (focus)
            {
                _menuFocusTarget = left;
            }
        }
        RaiseEvent(new RolesPermissionsRoleDeletedEventArgs(RoleDeletedEvent, this, role, isDiscard));
        return true;
    }

    private int RevertCore(RolesPermissionsRole role, bool focus)
    {
        if (IsBusy())
        {
            return 0;
        }
        BeginEdit();
        var changed = _model.RevertRole(role, _permissionKeys);
        if (changed.Count == 0)
        {
            return 0;
        }
        var changedSet = new HashSet<string>(changed, StringComparer.Ordinal);
        Refresh(TableRipple(c => ReferenceEquals(c.Role, role) && changedSet.Contains(c.Permission!.Key)), animate: true);
        _announcer?.Announce(ProjectsText.Format(Labels.RevertedFormat, role.Name));
        RaiseRevertEvents(role, changed);
        RaiseEvent(new RolesPermissionsRevertedEventArgs(ChangesRevertedEvent, this, role, changed.Count));
        if (focus && _grid?.HeaderOf(role) is { } header)
        {
            _menuFocusTarget = header;
        }
        return changed.Count;
    }

    private void RaiseRevertEvents(RolesPermissionsRole role, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (FindPermission(key) is { } permission)
            {
                RaiseEvent(new RolesPermissionsGrantChangedEventArgs(GrantChangedEvent, this, role, permission,
                    _model.IsGranted(role, key), false, RolesPermissionsChangeSource.Discard));
            }
        }
    }

    private int DiscardCore(bool focus)
    {
        if (SaveState == RolesPermissionsSaveState.Saving)
        {
            return 0;
        }
        var count = PendingChangeCount;
        if (count == 0)
        {
            return 0;
        }
        BeginEdit();
        foreach (var copy in _roles.Where(r => r.IsNew).ToList())
        {
            DeleteCore(copy, isDiscard: true, focus: false);
        }
        var changedByRole = new List<(RolesPermissionsRole Role, List<string> Keys)>();
        foreach (var role in _roles)
        {
            var changed = _model.RevertRole(role, _permissionKeys);
            if (changed.Count > 0)
            {
                changedByRole.Add((role, changed));
            }
        }
        Refresh(TableRipple(_ => true), animate: true);
        _announcer?.Announce(ProjectsText.Format(Labels.DiscardedFormat, Labels.CountText(count)));
        foreach (var (role, keys) in changedByRole)
        {
            RaiseRevertEvents(role, keys);
        }
        RaiseEvent(new RolesPermissionsRevertedEventArgs(ChangesRevertedEvent, this, null, count));
        if (focus)
        {
            ReturnFocusToGrid();
        }
        return count;
    }

    /// <summary>Puts focus back on the grid's roving part (the last cell worked on), as Arc does after Save and Discard.</summary>
    private void ReturnFocusToGrid()
    {
        var target = _grid?.RovingElement;
        if (target is not null && target.IsEffectivelyVisible && target.Focusable)
        {
            // Save or Discard clicked with the mouse: the cell takes focus without the keyboard ring.
            target.Focus(BlockFocusModality.For(this));
        }
    }

    internal void ToggleGroupExpanded(RolesPermissionsGroup group)
    {
        var expanded = !group.IsExpanded;
        _syncingExpanded = true;
        try
        {
            group.SetCurrentValue(RolesPermissionsGroup.IsExpandedProperty, expanded);
        }
        finally
        {
            _syncingExpanded = false;
        }
        _grid?.SetExpanded(group, expanded, animate: true);
    }

    #endregion

    #region Role menu

    internal void ToggleRoleMenu(RolesPermissionsRoleHeader header, MenuOpenFocus focus)
    {
        if (!IsRoleMenuEnabled)
        {
            return;
        }
        var menu = EnsureMenu();
        if (menu.IsOpen && ReferenceEquals(_menuHeader, header))
        {
            menu.Close(returnFocus: true);
            return;
        }
        _menuHeader?.SetOpen(false);
        _menuHeader = header;
        _menuFocusTarget = null;
        BuildMenu(header.Role);
        menu.FocusReturnTarget = header;
        header.SetOpen(true);
        menu.Open(header, focus);
    }

    private MenuSurface EnsureMenu()
    {
        if (_menu is null)
        {
            _menu = new MenuSurface
            {
                Variant = MenuSurfaceVariant.Account,
                Side = MenuSide.Bottom,
                Alignment = MenuAlignment.Center,
                AnchorGap = 4,
                Width = 208,
            }.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusStep14);
            _menu.ItemClicked += OnMenuItemClicked;
            _menu.Closed += OnMenuClosed;
        }
        return _menu;
    }

    private void BuildMenu(RolesPermissionsRole role)
    {
        var menu = _menu!;
        var labels = Labels;
        menu.Items.Clear();
        var description = role.IsNew ? labels.NotSavedYet : role.Description ?? labels.People(role.MemberCount);
        menu.Header = new StackPanel
        {
            Spacing = 1,
            Margin = new Thickness(7, 7, 7, 8),
            Children =
            {
                new TextBlock { Text = role.Name, FontWeight = FontWeight.Medium, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = description, FontSize = 12, Classes = { "muted" }, TextTrimming = TextTrimming.CharacterEllipsis },
            },
        };
        if (menu.Header is StackPanel panel && panel.Children[1] is TextBlock muted)
        {
            muted.Bind(TextBlock.ForegroundProperty, muted.GetResourceObservable(DesignTokens.ArcDesignTokenKind.TextMuted));
        }
        if (CanDuplicateRoles)
        {
            menu.Items.Add(new MenuEntry { Header = labels.CopyRole, Icon = ArcIcons.Copy, Tag = MenuAction.Copy });
        }
        if (!role.IsLocked)
        {
            menu.Items.Add(new MenuEntry
            {
                Header = labels.UndoEdits,
                Icon = ArcIcons.RotateCcw,
                Tag = MenuAction.Undo,
                IsEnabled = _model.CountCellChanges(role, _permissionKeys) > 0 && SaveState != RolesPermissionsSaveState.Saving,
            });
        }
        if (role.IsNew)
        {
            menu.Items.Add(new MenuEntry
            {
                Header = labels.RemoveRole,
                Icon = ArcIcons.Trash2,
                Tag = MenuAction.Remove,
                IsDestructive = true,
                HasSeparatorBefore = true,
                IsEnabled = SaveState != RolesPermissionsSaveState.Saving,
            });
        }
    }

    private enum MenuAction
    {
        Copy,
        Undo,
        Remove,
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (_menuHeader is not { } header || e.Entry.Tag is not MenuAction action)
        {
            return;
        }
        var role = header.Role;
        switch (action)
        {
            case MenuAction.Copy:
                DuplicateCore(role, focus: true);
                break;
            case MenuAction.Undo:
                RevertCore(role, focus: true);
                break;
            case MenuAction.Remove:
                DeleteCore(role, isDiscard: false, focus: true);
                break;
        }
        if (_menuFocusTarget is not null && _menu is not null)
        {
            _menu.FocusReturnTarget = _menuFocusTarget;
        }
    }

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        _menuHeader?.SetOpen(false);
        _menuHeader = null;
        if (_menuFocusTarget is { } target)
        {
            _menuFocusTarget = null;
            // The menu hands focus back on close; the new column's header (or the neighbour of a removed one) takes it then,
            // with the keyboard ring only when a key made the pick.
            var method = BlockFocusModality.For(this);
            Dispatcher.UIThread.Post(() =>
            {
                if (target.IsEffectivelyVisible && target.Focusable)
                {
                    target.Focus(method);
                }
            }, DispatcherPriority.Background);
        }
    }

    #endregion
}
