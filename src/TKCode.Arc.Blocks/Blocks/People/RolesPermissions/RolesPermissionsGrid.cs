using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

internal enum RolesPermissionsRowKind
{
    Header,
    Group,
    Permission,
}

/// <summary>One row of the matrix and where the last layout put it.</summary>
internal sealed class RolesPermissionsRow
{
    public RolesPermissionsRowKind Kind { get; init; }

    public RolesPermissionsGroup? Group { get; init; }

    public RolesPermissionsItem? Permission { get; init; }

    public int GroupIndex { get; init; }

    /// <summary>The permission's place in its group (0-based); −1 for header and group rows.</summary>
    public int IndexInGroup { get; init; } = -1;

    /// <summary>The permission's place among every permission of the matrix (0-based); −1 for header and group rows.</summary>
    public int GlobalIndex { get; init; } = -1;

    public RolesPermissionsGroupButton? Button { get; set; }

    public List<RolesPermissionsCell> Cells { get; } = new();

    public RolesPermissionsText LabelText { get; } = new();

    public TextLayout? Label { get; set; }

    public double Y { get; set; }

    public double Height { get; set; }

    /// <summary>The part of the row a folding group still shows (grid coordinates); null when all of it shows.</summary>
    public Rect? Clip { get; set; }

    public double Opacity { get; set; } = 1;

    public bool IsPlaced { get; set; }

    public MotionValue<double>? Hover { get; set; }

    public double HoverValue => Hover?.Value ?? 0;
}

/// <summary>One role column: its header, its width factor (0 → 1 while it springs in) and its fade.</summary>
internal sealed class RolesPermissionsColumn
{
    public RolesPermissionsRole Role { get; init; } = null!;

    public RolesPermissionsRoleHeader Header { get; init; } = null!;

    public MotionValue<double> Size { get; init; } = null!;

    public MotionValue<double> Fade { get; init; } = null!;

    public bool Removing { get; set; }

    public double X { get; set; }

    public double Width { get; set; }
}

/// <summary>A group's fold: the body height factor (0 → 1) and the body fade.</summary>
internal sealed class RolesPermissionsFold
{
    public RolesPermissionsGroup Group { get; init; } = null!;

    public RolesPermissionsRow Row { get; init; } = null!;

    public List<RolesPermissionsRow> Rows { get; } = new();

    public MotionValue<double> Expand { get; init; } = null!;

    public MotionValue<double> Fade { get; init; } = null!;

    public bool Expanded { get; set; } = true;
}

/// <summary>
/// The matrix itself: a header row of role buttons, then per group a row with a disclosure button and group checkboxes,
/// followed by its permission rows. Every interactive part is a light self-drawn control; this panel places them, draws
/// the row borders, the hovered row tint and the gliding column band underneath, keeps the label column sticky at the
/// horizontal scroll offset, and runs the grid's keyboard model (one tab stop, arrows that keep the column and snap to
/// the nearest one, Home/End, Ctrl+Home/End, folded rows skipped).
/// <para>
/// Column widths and group heights are factors animated in code (a slot's width springs 0 → 1 when a role is duplicated,
/// a group body's height springs when it folds): one measure of this panel per frame re-places the parts without
/// re-measuring any of them, which is what keeps a hundred cells cheap while a column springs.
/// </para>
/// </summary>
internal sealed class RolesPermissionsGrid : Control
{
    #region Theme-bound properties

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<RolesPermissionsGrid>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<RolesPermissionsGrid>();
    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(MutedBrush));
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BorderBrush));
    public static readonly StyledProperty<IBrush?> SubtleBorderBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(SubtleBorderBrush));
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(SurfaceBrush));
    public static readonly StyledProperty<IBrush?> SurfaceMutedBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(SurfaceMutedBrush));
    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(WarningBrush));
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(FocusRingBrush));
    public static readonly StyledProperty<IBrush?> BoxOffBorderBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BoxOffBorderBrush));
    public static readonly StyledProperty<IBrush?> BoxHoverFillBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BoxHoverFillBrush));
    public static readonly StyledProperty<IBrush?> BoxHoverBorderBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BoxHoverBorderBrush));
    public static readonly StyledProperty<IBrush?> BoxOnFillBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BoxOnFillBrush));
    public static readonly StyledProperty<IBrush?> BoxOnGlyphBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BoxOnGlyphBrush));
    public static readonly StyledProperty<IBrush?> BoxMixedFillBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BoxMixedFillBrush));
    public static readonly StyledProperty<IBrush?> BoxLockedFillBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BoxLockedFillBrush));
    public static readonly StyledProperty<IBrush?> BandFillBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(BandFillBrush));
    public static readonly StyledProperty<IBrush?> RowHoverFillBrushProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, IBrush?>(nameof(RowHoverFillBrush));

    public static readonly StyledProperty<double> LabelMinWidthProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(LabelMinWidth), 248);
    public static readonly StyledProperty<double> RoleWidthProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(RoleWidth), 100);
    public static readonly StyledProperty<double> LabelInsetProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(LabelInset), 20);
    public static readonly StyledProperty<double> PermissionInsetProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(PermissionInset), 44);
    public static readonly StyledProperty<double> PermissionFontSizeProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(PermissionFontSize), 13);
    public static readonly StyledProperty<double> LabelEndInsetProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(LabelEndInset), 12);
    public static readonly StyledProperty<double> PermissionLineHeightProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(PermissionLineHeight), 16.9);
    public static readonly StyledProperty<double> PermissionVerticalInsetProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(PermissionVerticalInset), 8);
    public static readonly StyledProperty<double> HeaderRowHeightProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(HeaderRowHeight), 56);
    public static readonly StyledProperty<double> GroupRowHeightProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(GroupRowHeight), 45);
    public static readonly StyledProperty<double> PermissionRowHeightProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(PermissionRowHeight), 40);
    public static readonly StyledProperty<double> RoleButtonInsetProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(RoleButtonInset), 6);
    public static readonly StyledProperty<double> RoleButtonHeightProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(RoleButtonHeight), 44);
    public static readonly StyledProperty<double> RoleButtonRadiusProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(RoleButtonRadius), 10);
    public static readonly StyledProperty<double> GroupButtonHeightProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(GroupButtonHeight), 28);
    public static readonly StyledProperty<double> GroupButtonRadiusProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(GroupButtonRadius), 8);
    public static readonly StyledProperty<double> CellWidthProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(CellWidth), 40);
    public static readonly StyledProperty<double> CellHeightProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(CellHeight), 32);
    public static readonly StyledProperty<double> CellRadiusProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(CellRadius), 8);
    public static readonly StyledProperty<double> BoxSizeProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(BoxSize), 16);
    public static readonly StyledProperty<double> BoxRadiusProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(BoxRadius), 5);
    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(DotSize), 5);
    public static readonly StyledProperty<double> BandInsetProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(BandInset), 4);
    public static readonly StyledProperty<double> BandRadiusProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, double>(nameof(BandRadius), 10);

    public static readonly StyledProperty<bool> IsCountVisibleProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, bool>(nameof(IsCountVisible), true);

    public static readonly StyledProperty<bool> IsChevronVisibleProperty =
        AvaloniaProperty.Register<RolesPermissionsGrid, bool>(nameof(IsChevronVisible), true);

    public FontFamily FontFamily => GetValue(FontFamilyProperty);
    public IBrush? Foreground => GetValue(ForegroundProperty);
    public IBrush? MutedBrush => GetValue(MutedBrushProperty);
    public IBrush? BorderBrush => GetValue(BorderBrushProperty);
    public IBrush? SubtleBorderBrush => GetValue(SubtleBorderBrushProperty);
    public IBrush? SurfaceBrush => GetValue(SurfaceBrushProperty);
    public IBrush? SurfaceMutedBrush => GetValue(SurfaceMutedBrushProperty);
    public IBrush? WarningBrush => GetValue(WarningBrushProperty);
    public IBrush? FocusRingBrush => GetValue(FocusRingBrushProperty);
    public IBrush? BoxOffBorderBrush => GetValue(BoxOffBorderBrushProperty);
    public IBrush? BoxHoverFillBrush => GetValue(BoxHoverFillBrushProperty);
    public IBrush? BoxHoverBorderBrush => GetValue(BoxHoverBorderBrushProperty);
    public IBrush? BoxOnFillBrush => GetValue(BoxOnFillBrushProperty);
    public IBrush? BoxOnGlyphBrush => GetValue(BoxOnGlyphBrushProperty);
    public IBrush? BoxMixedFillBrush => GetValue(BoxMixedFillBrushProperty);
    public IBrush? BoxLockedFillBrush => GetValue(BoxLockedFillBrushProperty);
    public IBrush? BandFillBrush => GetValue(BandFillBrushProperty);
    public IBrush? RowHoverFillBrush => GetValue(RowHoverFillBrushProperty);
    public double LabelMinWidth => GetValue(LabelMinWidthProperty);
    public double RoleWidth => GetValue(RoleWidthProperty);
    public double LabelInset => GetValue(LabelInsetProperty);
    public double PermissionInset => GetValue(PermissionInsetProperty);
    public double PermissionFontSize => GetValue(PermissionFontSizeProperty);
    public double LabelEndInset => GetValue(LabelEndInsetProperty);
    public double PermissionLineHeight => GetValue(PermissionLineHeightProperty);
    public double PermissionVerticalInset => GetValue(PermissionVerticalInsetProperty);
    public double HeaderRowHeight => GetValue(HeaderRowHeightProperty);
    public double GroupRowHeight => GetValue(GroupRowHeightProperty);
    public double PermissionRowHeight => GetValue(PermissionRowHeightProperty);
    public double RoleButtonInset => GetValue(RoleButtonInsetProperty);
    public double RoleButtonHeight => GetValue(RoleButtonHeightProperty);
    public double RoleButtonRadius => GetValue(RoleButtonRadiusProperty);
    public double GroupButtonHeight => GetValue(GroupButtonHeightProperty);
    public double GroupButtonRadius => GetValue(GroupButtonRadiusProperty);
    public double CellWidth => GetValue(CellWidthProperty);
    public double CellHeight => GetValue(CellHeightProperty);
    public double CellRadius => GetValue(CellRadiusProperty);
    public double BoxSize => GetValue(BoxSizeProperty);
    public double BoxRadius => GetValue(BoxRadiusProperty);
    public double DotSize => GetValue(DotSizeProperty);
    public double BandInset => GetValue(BandInsetProperty);
    public double BandRadius => GetValue(BandRadiusProperty);
    public bool IsCountVisible => GetValue(IsCountVisibleProperty);
    public bool IsChevronVisible => GetValue(IsChevronVisibleProperty);

    #endregion

    private readonly List<RolesPermissionsColumn> _columns = new();
    private readonly List<RolesPermissionsRow> _rows = new();
    private readonly List<RolesPermissionsFold> _folds = new();
    private readonly RolesPermissionsLabelLayer _labels;
    private readonly SolidColorBrush _bandBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _rowBrush = new(Colors.Transparent);
    private MotionValue<double>? _bandX;
    private MotionValue<double>? _bandFade;
    private MotionValue<double>? _scroll;
    private RolesPermissionsColumn? _bandColumn;
    private double _bandWidth;
    private RolesPermissionsColumn? _hoverColumn;
    private RolesPermissionsRow? _hoverRow;
    private RolesPermissionsColumn? _focusColumn;
    private RolesPermissionsRow? _focusRow;
    private Control? _roving;
    private double _labelWidth;
    private double _contentWidth;
    private double _contentHeight;
    private double _viewportWidth;
    private double _scrollOffset;
    private bool _settingScroll;

    static RolesPermissionsGrid()
    {
        AffectsMeasure<RolesPermissionsGrid>(LabelMinWidthProperty, RoleWidthProperty, LabelInsetProperty, PermissionInsetProperty,
            PermissionFontSizeProperty, HeaderRowHeightProperty, GroupRowHeightProperty, PermissionRowHeightProperty,
            IsCountVisibleProperty, IsChevronVisibleProperty, FontFamilyProperty);
        FocusableProperty.OverrideDefaultValue<RolesPermissionsGrid>(false);
        ClipToBoundsProperty.OverrideDefaultValue<RolesPermissionsGrid>(false);
    }

    public RolesPermissionsGrid()
    {
        _labels = new RolesPermissionsLabelLayer { Grid = this, ZIndex = 1 };
        VisualChildren.Add(_labels);
        LogicalChildren.Add(_labels);
        AddHandler(PointerMovedEvent, OnAnyPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnPartGotFocus, RoutingStrategies.Bubble);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    public RolesPermissions? Owner { get; set; }

    public RolesPermissionsPaint Paint { get; } = new();

    public IReadOnlyList<RolesPermissionsRow> Rows => _rows;

    public IReadOnlyList<RolesPermissionsColumn> Columns => _columns;

    public IReadOnlyList<RolesPermissionsFold> Folds => _folds;

    internal RolesPermissionsLabelLayer LabelLayer => _labels;

    internal double LabelWidth => _labelWidth;

    internal double ContentWidth => _contentWidth;

    internal RolesPermissionsColumn? BandColumn => _bandColumn;

    internal double BandOpacity => _bandFade?.Value ?? 0;

    internal double BandX => _bandX?.Value ?? 0;

    internal Control? RovingElement => _roving;

    /// <summary>The width the matrix has to fill (the scroller's viewport): the label column takes what the roles leave.</summary>
    public double ViewportWidth
    {
        get => _viewportWidth;
        set
        {
            if (Math.Abs(_viewportWidth - value) > 0.01)
            {
                _viewportWidth = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The horizontal scroll offset; the label column and the group buttons stay put at it.</summary>
    public double ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            if (Math.Abs(_scrollOffset - value) > 0.01)
            {
                _scrollOffset = value;
                _labels.SetScrolled(value > 0.5);
                InvalidateArrange();
                UpdatePointerTargets(null);
            }
        }
    }

    /// <summary>The scroller the panel sits in (set by the block) to bring focused columns into view.</summary>
    public ScrollViewer? Scroller { get; set; }

    #region Building

    /// <summary>Rebuilds every row and column (no animation).</summary>
    public void Build(IReadOnlyList<RolesPermissionsRole> roles, IReadOnlyList<RolesPermissionsGroup> groups)
    {
        foreach (var child in VisualChildren.ToList())
        {
            if (!ReferenceEquals(child, _labels))
            {
                VisualChildren.Remove(child);
            }
        }
        foreach (var child in LogicalChildren.ToList())
        {
            if (!ReferenceEquals(child, _labels))
            {
                LogicalChildren.Remove(child);
            }
        }
        _columns.Clear();
        _rows.Clear();
        _folds.Clear();
        _hoverColumn = null;
        _hoverRow = null;
        _focusColumn = null;
        _focusRow = null;
        _bandColumn = null;
        _bandFade?.Jump(0);
        _roving = null;

        _rows.Add(new RolesPermissionsRow { Kind = RolesPermissionsRowKind.Header });
        var global = 0;
        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var row = new RolesPermissionsRow { Kind = RolesPermissionsRowKind.Group, Group = group, GroupIndex = g };
            var button = new RolesPermissionsGroupButton { Group = group, Grid = this, ZIndex = 2, PermissionCount = group.Permissions.Count };
            row.Button = button;
            AddPart(button);
            _rows.Add(row);
            var fold = new RolesPermissionsFold
            {
                Group = group,
                Row = row,
                Expand = new MotionValue<double>(this, group.IsExpanded ? 1 : 0, MotionVectors.Double),
                Fade = new MotionValue<double>(this, group.IsExpanded ? 1 : 0, MotionVectors.Double),
                Expanded = group.IsExpanded,
            };
            fold.Expand.Changed += _ => InvalidateMeasure();
            fold.Fade.Changed += _ => InvalidateArrange();
            _folds.Add(fold);
            for (var i = 0; i < group.Permissions.Count; i++)
            {
                var permissionRow = new RolesPermissionsRow
                {
                    Kind = RolesPermissionsRowKind.Permission,
                    Group = group,
                    Permission = group.Permissions[i],
                    GroupIndex = g,
                    IndexInGroup = i,
                    GlobalIndex = global++,
                };
                fold.Rows.Add(permissionRow);
                _rows.Add(permissionRow);
            }
            button.SetExpanded(group.IsExpanded, false);
            button.Refresh();
        }
        for (var c = 0; c < roles.Count; c++)
        {
            InsertColumn(c, roles[c], false);
        }
        foreach (var fold in _folds)
        {
            SetBodyVisible(fold, fold.Expanded);
        }
        _roving = _columns.Count > 0 ? _columns[0].Header : _rows.Count > 1 ? _rows[1].Button : null;
        UpdateTabStops();
        InvalidateMeasure();
    }

    /// <summary>Adds a role column at <paramref name="index"/>; animated, its width springs in from 0 and it fades in.</summary>
    public RolesPermissionsColumn InsertColumn(int index, RolesPermissionsRole role, bool animate)
    {
        var header = new RolesPermissionsRoleHeader { Role = role, Grid = this };
        var column = new RolesPermissionsColumn
        {
            Role = role,
            Header = header,
            Size = new MotionValue<double>(this, animate ? 0 : 1, MotionVectors.Double),
            Fade = new MotionValue<double>(this, animate ? 0 : 1, MotionVectors.Double),
        };
        column.Size.Changed += _ => InvalidateMeasure();
        column.Fade.Changed += _ => InvalidateArrange();
        index = Math.Clamp(index, 0, _columns.Count);
        _columns.Insert(index, column);
        AddPart(header);
        header.Refresh();
        foreach (var row in _rows)
        {
            if (row.Kind == RolesPermissionsRowKind.Header)
            {
                continue;
            }
            var cell = new RolesPermissionsCell { Role = role, Group = row.Group!, Permission = row.Permission, Grid = this };
            row.Cells.Insert(index, cell);
            AddPart(cell);
            var fold = _folds[row.GroupIndex];
            if (row.Kind == RolesPermissionsRowKind.Permission && !fold.Expanded && fold.Expand.Value <= 0.001)
            {
                cell.IsVisible = false;
            }
        }
        if (animate)
        {
            // Arc: the new slot's width springs 0 → full on the smooth spring while it fades in over 220ms (enter curve).
            column.Size.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
            if (ArcMotion.IsFull(this))
            {
                column.Fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(220), ArcEasings.Enter));
            }
            else
            {
                column.Fade.Jump(1);
            }
        }
        UpdateTabStops();
        InvalidateMeasure();
        return column;
    }

    /// <summary>Takes a column out: it fades in 120ms while its width springs to 0, then its parts leave.</summary>
    public void RemoveColumn(RolesPermissionsRole role, bool animate)
    {
        var column = _columns.FirstOrDefault(c => ReferenceEquals(c.Role, role) && !c.Removing);
        if (column is null)
        {
            return;
        }
        column.Removing = true;
        var index = _columns.IndexOf(column);
        foreach (var part in PartsOf(index))
        {
            part.IsHitTestVisible = false;
            part.Focusable = false;
        }
        if (ReferenceEquals(_hoverColumn, column))
        {
            _hoverColumn = null;
        }
        if (ReferenceEquals(_focusColumn, column))
        {
            _focusColumn = null;
        }
        UpdateBand();
        if (animate && (TopLevel.GetTopLevel(this) is not null) && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            if (ArcMotion.IsFull(this))
            {
                column.Fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            else
            {
                column.Fade.Jump(0);
            }
            _ = column.Size.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(
                _ => Avalonia.Threading.Dispatcher.UIThread.Post(() => Detach(column)),
                TaskScheduler.Default);
            if (column.Size.IsAnimating)
            {
                return;
            }
        }
        Detach(column);
    }

    private void Detach(RolesPermissionsColumn column)
    {
        var index = _columns.IndexOf(column);
        if (index < 0)
        {
            return;
        }
        foreach (var part in PartsOf(index).ToList())
        {
            RemovePart(part);
        }
        foreach (var row in _rows)
        {
            if (row.Cells.Count > index)
            {
                row.Cells.RemoveAt(index);
            }
        }
        _columns.RemoveAt(index);
        if (ReferenceEquals(_bandColumn, column))
        {
            _bandColumn = null;
        }
        UpdateTabStops();
        InvalidateMeasure();
    }

    private IEnumerable<RolesPermissionsPart> PartsOf(int columnIndex)
    {
        yield return _columns[columnIndex].Header;
        foreach (var row in _rows)
        {
            if (row.Cells.Count > columnIndex)
            {
                yield return row.Cells[columnIndex];
            }
        }
    }

    private void AddPart(RolesPermissionsPart part)
    {
        part.Grid = this;
        VisualChildren.Add(part);
        LogicalChildren.Add(part);
    }

    private void RemovePart(RolesPermissionsPart part)
    {
        if (ReferenceEquals(_roving, part))
        {
            _roving = null;
        }
        VisualChildren.Remove(part);
        LogicalChildren.Remove(part);
    }

    public RolesPermissionsColumn? ColumnOf(RolesPermissionsRole role) => _columns.FirstOrDefault(c => ReferenceEquals(c.Role, role) && !c.Removing);

    public RolesPermissionsRoleHeader? HeaderOf(RolesPermissionsRole role) => ColumnOf(role)?.Header;

    public RolesPermissionsCell? CellAt(RolesPermissionsRole role, RolesPermissionsItem permission)
    {
        var index = _columns.FindIndex(c => ReferenceEquals(c.Role, role) && !c.Removing);
        if (index < 0)
        {
            return null;
        }
        var row = _rows.FirstOrDefault(r => ReferenceEquals(r.Permission, permission));
        return row is not null && row.Cells.Count > index ? row.Cells[index] : null;
    }

    public RolesPermissionsCell? GroupCellAt(RolesPermissionsRole role, RolesPermissionsGroup group)
    {
        var index = _columns.FindIndex(c => ReferenceEquals(c.Role, role) && !c.Removing);
        if (index < 0)
        {
            return null;
        }
        var row = _rows.FirstOrDefault(r => r.Kind == RolesPermissionsRowKind.Group && ReferenceEquals(r.Group, group));
        return row is not null && row.Cells.Count > index ? row.Cells[index] : null;
    }

    public RolesPermissionsGroupButton? ButtonOf(RolesPermissionsGroup group)
        => _rows.FirstOrDefault(r => r.Kind == RolesPermissionsRowKind.Group && ReferenceEquals(r.Group, group))?.Button;

    public IEnumerable<RolesPermissionsCell> AllCells()
    {
        foreach (var row in _rows)
        {
            foreach (var cell in row.Cells)
            {
                yield return cell;
            }
        }
    }

    #endregion

    #region Folding

    /// <summary>Folds or unfolds a group: the body's height springs (smooth) while it fades (120ms out, 240ms in).</summary>
    public void SetExpanded(RolesPermissionsGroup group, bool expanded, bool animate)
    {
        var fold = _folds.FirstOrDefault(f => ReferenceEquals(f.Group, group));
        if (fold is null || fold.Expanded == expanded)
        {
            return;
        }
        fold.Expanded = expanded;
        fold.Row.Button?.SetExpanded(expanded, animate);
        var full = animate && ArcMotion.IsFull(this) && (TopLevel.GetTopLevel(this) is not null);
        if (expanded)
        {
            SetBodyVisible(fold, true);
        }
        else if (fold.Rows.Any(r => r.Cells.Any(c => c.IsFocused)))
        {
            fold.Row.Button?.Focus(NavigationMethod.Directional);
        }
        if (full)
        {
            _ = fold.Expand.AnimateTo(expanded ? 1 : 0, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(
                t => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!fold.Expanded && fold.Expand.Value <= 0.001)
                    {
                        SetBodyVisible(fold, false);
                    }
                }), TaskScheduler.Default);
            fold.Fade.AnimateTo(expanded ? 1 : 0, expanded
                ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
                : MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
        }
        else
        {
            fold.Expand.Jump(expanded ? 1 : 0);
            fold.Fade.Jump(expanded ? 1 : 0);
            if (!expanded)
            {
                SetBodyVisible(fold, false);
            }
        }
        if (!expanded && ReferenceEquals(_focusRow?.Group, group) && _focusRow?.Kind == RolesPermissionsRowKind.Permission)
        {
            _focusRow = null;
        }
        UpdateTabStops();
        InvalidateMeasure();
    }

    private static void SetBodyVisible(RolesPermissionsFold fold, bool visible)
    {
        foreach (var row in fold.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsVisible = visible;
            }
        }
    }

    #endregion

    #region Layout

    private double ColumnSlot(RolesPermissionsColumn column) => RoleWidth * Math.Max(0, column.Size.Value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = _viewportWidth > 0 ? _viewportWidth : double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var roles = 0.0;
        foreach (var column in _columns)
        {
            roles += ColumnSlot(column);
        }
        _labelWidth = Math.Max(LabelMinWidth, viewport - roles);
        _contentWidth = _labelWidth + roles;

        var font = FontFamily;
        var regular = new Typeface(font, FontStyle.Normal, FontWeight.Normal);
        var textWidth = Math.Max(1, _labelWidth - PermissionInset - LabelEndInset);
        var lineHeight = PermissionFontSize < 13 ? PermissionLineHeight * PermissionFontSize / 13 : PermissionLineHeight;
        var y = HeaderRowHeight;
        foreach (var fold in _folds)
        {
            fold.Row.Height = GroupRowHeight;
            y += GroupRowHeight;
            var body = 0.0;
            foreach (var row in fold.Rows)
            {
                row.Label = row.LabelText.Get(row.Permission!.Name, regular, PermissionFontSize, Paint.ForegroundBrush, textWidth, true, lineHeight);
                var text = row.Label?.Height ?? lineHeight;
                row.Height = Math.Max(PermissionRowHeight, Math.Ceiling(text + 2 * PermissionVerticalInset) + 1);
                body += row.Height;
            }
            y += body * Math.Clamp(fold.Expand.Value, 0, 1);
        }
        _contentHeight = y;

        var cellSize = new Size(CellWidth, CellHeight);
        var headerSize = new Size(Math.Max(0, RoleWidth - RoleButtonInset), RoleButtonHeight);
        foreach (var column in _columns)
        {
            column.Header.Measure(headerSize);
        }
        foreach (var row in _rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.Measure(cellSize);
            }
            row.Button?.Measure(new Size(Math.Max(0, _labelWidth - LabelInset + 6 - LabelEndInset), GroupButtonHeight));
        }
        _labels.Measure(new Size(_labelWidth, _contentHeight));
        return new Size(_contentWidth, _contentHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = _labelWidth;
        foreach (var column in _columns)
        {
            column.X = x;
            column.Width = ColumnSlot(column);
            x += column.Width;
        }
        var header = _rows[0];
        header.Y = 0;
        header.Height = HeaderRowHeight;
        header.IsPlaced = true;
        var headerWidth = Math.Max(0, RoleWidth - RoleButtonInset);
        foreach (var column in _columns)
        {
            var left = column.X + (column.Width - headerWidth) / 2;
            column.Header.Arrange(new Rect(left, (HeaderRowHeight - RoleButtonHeight) / 2, headerWidth, RoleButtonHeight));
            column.Header.Opacity = Math.Clamp(column.Fade.Value, 0, 1);
        }

        var y = HeaderRowHeight;
        foreach (var fold in _folds)
        {
            var groupRow = fold.Row;
            groupRow.Y = y;
            groupRow.IsPlaced = true;
            groupRow.Clip = null;
            groupRow.Opacity = 1;
            PlaceCells(groupRow, null, 1);
            if (groupRow.Button is { } button)
            {
                var top = y + 1 + (groupRow.Height - 1 - GroupButtonHeight) / 2;
                button.Arrange(new Rect(_scrollOffset + LabelInset - 6, top, button.DesiredSize.Width, GroupButtonHeight));
            }
            y += groupRow.Height;

            var expand = Math.Clamp(fold.Expand.Value, 0, 1);
            var bodyTop = y;
            var offset = 0.0;
            foreach (var row in fold.Rows)
            {
                offset += row.Height;
            }
            var visible = offset * expand;
            var moving = expand < 0.999;
            var bodyClip = new Rect(0, bodyTop, Math.Max(_contentWidth, finalSize.Width), visible);
            var opacity = Math.Clamp(fold.Fade.Value, 0, 1);
            var rowY = bodyTop;
            foreach (var row in fold.Rows)
            {
                row.Y = rowY;
                row.IsPlaced = expand > 0.001;
                row.Clip = moving ? bodyClip : null;
                row.Opacity = opacity;
                PlaceCells(row, moving ? bodyClip : null, opacity);
                rowY += row.Height;
            }
            y = bodyTop + visible;
        }
        _labels.Arrange(new Rect(_scrollOffset, 0, _labelWidth, _contentHeight));
        SyncBandToLayout();
        ScheduleFollow();
        return new Size(Math.Max(finalSize.Width, _contentWidth), _contentHeight);
    }

    private void PlaceCells(RolesPermissionsRow row, Rect? clip, double opacity)
    {
        var top = row.Y + 1 + (row.Height - 1 - CellHeight) / 2;
        for (var c = 0; c < row.Cells.Count && c < _columns.Count; c++)
        {
            var column = _columns[c];
            var cell = row.Cells[c];
            var rect = new Rect(column.X + (column.Width - CellWidth) / 2, top, CellWidth, CellHeight);
            cell.Arrange(rect);
            cell.Opacity = Math.Clamp(column.Fade.Value, 0, 1) * opacity;
            if (clip is { } r)
            {
                var local = new Rect(-8, r.Top - rect.Y, rect.Width + 16, Math.Max(0, r.Height));
                cell.SetClip(local.Height <= 0 ? new Rect(0, 0, 0, 0) : local);
            }
            else
            {
                cell.SetClip(null);
            }
        }
    }

    #endregion

    #region Drawing

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.PropertyType == typeof(IBrush) && change.Property != FontFamilyProperty)
        {
            RefreshPaint();
        }
        else if (change.Property == IsChevronVisibleProperty || change.Property == IsCountVisibleProperty)
        {
            foreach (var column in _columns)
            {
                column.Header.InvalidateVisual();
            }
            foreach (var row in _rows)
            {
                row.Button?.InvalidateMeasure();
                row.Button?.InvalidateVisual();
            }
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !IsKeyboardFocusWithin)
        {
            _focusRow = null;
            _focusColumn = null;
            UpdateRowHover();
            UpdateBand();
        }
    }

    private void RefreshPaint()
    {
        Paint.Refresh(this);
        foreach (var row in _rows)
        {
            row.LabelText.Reset();
            row.Button?.ResetText();
            foreach (var cell in row.Cells)
            {
                cell.RefreshPaint();
            }
        }
        foreach (var column in _columns)
        {
            column.Header.ResetText();
            column.Header.InvalidateVisual();
        }
        _labels.ResetText();
        _labels.InvalidateVisual();
        InvalidateMeasure();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var width = Math.Max(Bounds.Width, _contentWidth);
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, _contentHeight));
        foreach (var row in _rows)
        {
            if (row.Kind == RolesPermissionsRowKind.Header || !row.IsPlaced)
            {
                continue;
            }
            using (row.Clip is { } clip ? context.PushClip(clip) : default(DrawingContext.PushedState?))
            {
                using (row.Opacity < 1 ? context.PushOpacity(row.Opacity) : default(DrawingContext.PushedState?))
                {
                    var hover = row.HoverValue;
                    if (hover > 0.001)
                    {
                        _rowBrush.Color = RolesPermissionsPaint.Fade(Paint.RowHoverFill, hover);
                        context.FillRectangle(_rowBrush, new Rect(0, row.Y, width, row.Height));
                    }
                    var pen = row.Kind == RolesPermissionsRowKind.Group ? Paint.BorderPen : Paint.BorderSubtlePen;
                    context.DrawLine(pen, new Point(0, row.Y + 0.5), new Point(width, row.Y + 0.5));
                }
            }
        }
        var band = _bandFade?.Value ?? 0;
        if (band > 0.001 && _bandWidth > 0)
        {
            _bandBrush.Color = RolesPermissionsPaint.Fade(Paint.BandFill, band);
            var rect = new Rect(_bandX?.Value ?? 0, BandInset, _bandWidth, Math.Max(0, _contentHeight - 2 * BandInset));
            context.DrawRectangle(_bandBrush, null, rect, BandRadius, BandRadius);
        }
    }

    #endregion

    #region Band and row highlight

    private void OnAnyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        UpdatePointerTargets(e.GetPosition(this));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverColumn = null;
        _hoverRow = null;
        _lastPointer = null;
        UpdateRowHover();
        UpdateBand();
    }

    private Point? _lastPointer;

    private void UpdatePointerTargets(Point? position)
    {
        position ??= _lastPointer;
        if (position is not { } p)
        {
            return;
        }
        _lastPointer = p;
        RolesPermissionsColumn? column = null;
        if (p.X >= _scrollOffset + _labelWidth)
        {
            foreach (var c in _columns)
            {
                if (!c.Removing && p.X >= c.X && p.X < c.X + c.Width)
                {
                    column = c;
                    break;
                }
            }
        }
        RolesPermissionsRow? row = null;
        foreach (var r in _rows)
        {
            if (r.Kind != RolesPermissionsRowKind.Header && r.IsPlaced && p.Y >= r.Y && p.Y < r.Y + r.Height &&
                (r.Clip is not { } clip || (p.Y >= clip.Top && p.Y < clip.Bottom)))
            {
                row = r;
                break;
            }
        }
        if (!ReferenceEquals(row, _hoverRow))
        {
            _hoverRow = row;
            UpdateRowHover();
        }
        if (!ReferenceEquals(column, _hoverColumn))
        {
            _hoverColumn = column;
            UpdateBand();
        }
    }

    private void UpdateRowHover()
    {
        foreach (var row in _rows)
        {
            if (row.Kind == RolesPermissionsRowKind.Header)
            {
                continue;
            }
            var target = ReferenceEquals(row, _hoverRow) || ReferenceEquals(row, _focusRow) ? 1.0 : 0.0;
            if (row.Hover is null)
            {
                if (target == 0)
                {
                    continue;
                }
                row.Hover = new MotionValue<double>(this, 0, MotionVectors.Double);
                row.Hover.Changed += _ =>
                {
                    InvalidateVisual();
                    _labels.InvalidateVisual();
                };
            }
            if (Math.Abs(row.Hover.Target - target) < 0.001)
            {
                continue;
            }
            if (ArcMotion.IsFull(this))
            {
                // Arc: the row's background (label cell included) changes in 120ms on the standard curve.
                row.Hover.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            else
            {
                row.Hover.Jump(target);
            }
        }
    }

    /// <summary>
    /// The band follows the pointer's column; with the pointer outside the role columns it follows the focused part's
    /// column (Arc did not show which wins when both exist: the pointer wins here, as the more direct signal).
    /// </summary>
    private void UpdateBand()
    {
        var target = _hoverColumn ?? _focusColumn;
        if (target is { Removing: true })
        {
            target = null;
        }
        _bandX ??= CreateBandValue();
        _bandFade ??= CreateBandValue();
        var full = ArcMotion.IsFull(this) && (TopLevel.GetTopLevel(this) is not null);
        if (target is null)
        {
            if (_bandColumn is not null)
            {
                _bandColumn = null;
                Fade(0, full);
            }
            return;
        }
        if (ReferenceEquals(target, _bandColumn))
        {
            return;
        }
        var appearing = _bandColumn is null || (_bandFade.Value < 0.01 && !_bandFade.IsAnimating);
        _bandColumn = target;
        _bandWidth = target.Width;
        if (appearing || !full)
        {
            _bandX.Jump(target.X);
        }
        else
        {
            // Arc: the band slides between columns on the smooth spring.
            _bandX.AnimateTo(target.X, MotionSpec.Spring(ArcSpring.Smooth));
        }
        Fade(1, full);
        InvalidateVisual();
    }

    private void Fade(double target, bool animate)
    {
        if (animate)
        {
            _bandFade!.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _bandFade!.Jump(target);
        }
    }

    private MotionValue<double> CreateBandValue()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private void SyncBandToLayout()
    {
        if (_bandColumn is not { } column || _bandX is null)
        {
            return;
        }
        _bandWidth = column.Width;
        if (Math.Abs(_bandX.Target - column.X) < 0.01)
        {
            return;
        }
        if (_bandX.IsAnimating)
        {
            _bandX.AnimateTo(column.X, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _bandX.Jump(column.X);
        }
    }

    #endregion

    #region Focus and keyboard

    private void OnPartGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is not RolesPermissionsPart part || !ReferenceEquals(part.Grid, this))
        {
            return;
        }
        _roving = part;
        UpdateTabStops();
        var (row, column) = Locate(part);
        _focusRow = row is { Kind: not RolesPermissionsRowKind.Header } ? row : null;
        _focusColumn = column;
        UpdateRowHover();
        UpdateBand();
        if (column is not null)
        {
            EnsureColumnVisible(column, false);
        }
    }

    private (RolesPermissionsRow? Row, RolesPermissionsColumn? Column) Locate(Control part)
    {
        for (var c = 0; c < _columns.Count; c++)
        {
            if (ReferenceEquals(_columns[c].Header, part))
            {
                return (_rows[0], _columns[c]);
            }
        }
        foreach (var row in _rows)
        {
            if (ReferenceEquals(row.Button, part))
            {
                return (row, null);
            }
            var index = row.Cells.IndexOf(part as RolesPermissionsCell ?? null!);
            if (index >= 0)
            {
                return (row, index < _columns.Count ? _columns[index] : null);
            }
        }
        return (null, null);
    }

    /// <summary>Only the roving element is a tab stop (Arc's single tab stop in the grid).</summary>
    public void UpdateTabStops()
    {
        if (_roving is null || !_roving.IsEffectivelyVisible || !VisualChildren.Contains(_roving) || !_roving.Focusable)
        {
            _roving = _columns.FirstOrDefault(c => !c.Removing)?.Header ?? (Control?)_rows.FirstOrDefault(r => r.Button is not null)?.Button;
        }
        foreach (var child in VisualChildren)
        {
            if (child is RolesPermissionsPart part)
            {
                part.IsTabStop = ReferenceEquals(part, _roving);
            }
        }
    }

    /// <summary>Moves the roving tab stop to <paramref name="part"/> without focusing it (focus returns there on Tab).</summary>
    public void SetRoving(Control? part)
    {
        if (part is null)
        {
            return;
        }
        _roving = part;
        UpdateTabStops();
    }

    private List<RolesPermissionsRow> VisibleRows()
    {
        var list = new List<RolesPermissionsRow>();
        foreach (var row in _rows)
        {
            if (row.Kind == RolesPermissionsRowKind.Permission && !_folds[row.GroupIndex].Expanded)
            {
                continue;
            }
            list.Add(row);
        }
        return list;
    }

    /// <summary>The focusable parts of a row with their column number (0 = the group button, 1.. = role columns).</summary>
    private List<(Control Part, int Column)> ItemsOf(RolesPermissionsRow row)
    {
        var items = new List<(Control, int)>();
        if (row.Button is not null)
        {
            items.Add((row.Button, 0));
        }
        var n = 0;
        for (var c = 0; c < _columns.Count; c++)
        {
            if (_columns[c].Removing)
            {
                continue;
            }
            n++;
            if (row.Kind == RolesPermissionsRowKind.Header)
            {
                items.Add((_columns[c].Header, n));
            }
            else if (c < row.Cells.Count)
            {
                items.Add((row.Cells[c], n));
            }
        }
        return items;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not RolesPermissionsPart part || !ReferenceEquals(part.Grid, this))
        {
            return;
        }
        var ctrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        if ((e.KeyModifiers & ~KeyModifiers.Control) != KeyModifiers.None)
        {
            return;
        }
        var rows = VisibleRows();
        var (row, _) = Locate(part);
        if (row is null)
        {
            return;
        }
        var rowIndex = rows.IndexOf(row);
        var items = ItemsOf(row);
        var position = items.FindIndex(i => ReferenceEquals(i.Part, part));
        if (rowIndex < 0 || position < 0)
        {
            return;
        }
        var column = items[position].Column;
        Control? target = null;
        switch (e.Key)
        {
            case Key.Left when !ctrl:
                target = position > 0 ? items[position - 1].Part : null;
                break;
            case Key.Right when !ctrl:
                target = position < items.Count - 1 ? items[position + 1].Part : null;
                break;
            case Key.Up when !ctrl:
                target = rowIndex > 0 ? Nearest(ItemsOf(rows[rowIndex - 1]), column) : null;
                break;
            case Key.Down when !ctrl:
                target = rowIndex < rows.Count - 1 ? Nearest(ItemsOf(rows[rowIndex + 1]), column) : null;
                break;
            case Key.Home:
                target = ctrl ? ItemsOf(rows[0]).Select(i => i.Part).FirstOrDefault() : items[0].Part;
                break;
            case Key.End:
                target = ctrl ? ItemsOf(rows[^1]).Select(i => i.Part).LastOrDefault() : items[^1].Part;
                break;
            default:
                return;
        }
        e.Handled = true;
        if (target is not null && !ReferenceEquals(target, part))
        {
            target.Focus(NavigationMethod.Directional);
        }
    }

    /// <summary>The part in the same column, or the nearest column when the row has none there (the group button's column 0 → 1).</summary>
    private static Control? Nearest(List<(Control Part, int Column)> items, int column)
    {
        Control? best = null;
        var distance = int.MaxValue;
        foreach (var (part, c) in items)
        {
            var d = Math.Abs(c - column);
            if (d < distance)
            {
                distance = d;
                best = part;
            }
        }
        return best;
    }

    /// <summary>Scrolls horizontally so the column is not under the sticky labels or past the right edge.</summary>
    public void EnsureColumnVisible(RolesPermissionsColumn column, bool smooth)
    {
        if (Scroller is not { } scroller)
        {
            return;
        }
        var viewport = scroller.Viewport.Width;
        if (viewport <= 0)
        {
            return;
        }
        var left = column.X - _labelWidth;
        var right = _labelWidth + (column.X - _labelWidth) + RoleWidth - viewport;
        var current = scroller.Offset.X;
        var target = current;
        if (column.X - current < _labelWidth)
        {
            target = left;
        }
        else if (column.X + RoleWidth - current > viewport)
        {
            target = right;
        }
        var max = Math.Max(0, _labelWidth + _columns.Where(c => !c.Removing).Count() * RoleWidth - viewport);
        target = Math.Clamp(target, 0, max);
        if (Math.Abs(target - current) < 0.5)
        {
            return;
        }
        if (smooth && ArcMotion.IsFull(this))
        {
            if (_scroll is null)
            {
                _scroll = new MotionValue<double>(this, current, MotionVectors.Double);
                _scroll.Changed += v =>
                {
                    if (Scroller is { } s)
                    {
                        _settingScroll = true;
                        s.Offset = new Vector(v, s.Offset.Y);
                        _settingScroll = false;
                    }
                };
            }
            _scroll.Jump(current);
            // The browser's smooth scroll of the new column into view, about 130ms.
            _scroll.AnimateTo(target, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Standard));
        }
        else
        {
            scroller.Offset = new Vector(target, scroller.Offset.Y);
        }
    }

    internal bool IsSettingScroll => _settingScroll;

    private RolesPermissionsColumn? _follow;
    private bool _followPosted;

    /// <summary>
    /// Keeps a springing column in view while it grows (Arc scrolls a duplicated column into view on narrow screens):
    /// after each layout the scroller is moved so the column's right edge shows, until its width settles.
    /// </summary>
    public void FollowColumn(RolesPermissionsColumn column)
    {
        _follow = column;
        InvalidateArrange();
    }

    private void ScheduleFollow()
    {
        if (_follow is null || _followPosted)
        {
            return;
        }
        _followPosted = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _followPosted = false;
            if (_follow is not { } column || Scroller is not { } scroller)
            {
                return;
            }
            var viewport = scroller.Viewport.Width;
            var max = Math.Max(0, scroller.Extent.Width - viewport);
            var needed = column.X + column.Width - viewport;
            if (needed > scroller.Offset.X + 0.5)
            {
                _settingScroll = true;
                scroller.Offset = new Vector(Math.Min(max, needed), scroller.Offset.Y);
                _settingScroll = false;
            }
            if (!column.Size.IsAnimating || column.Removing)
            {
                _follow = null;
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new GridPeer(this);

    private sealed class GridPeer(RolesPermissionsGrid owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}
