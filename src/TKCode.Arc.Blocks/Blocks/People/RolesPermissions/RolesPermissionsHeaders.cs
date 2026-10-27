using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>One cached line or paragraph for a self-drawn part: rebuilt only when the text, font, brush or room changes.</summary>
internal sealed class RolesPermissionsText
{
    private string? _text;
    private Typeface _typeface;
    private double _size;
    private IBrush? _brush;
    private double _maxWidth = double.NaN;
    private bool _wrap;
    private double _lineHeight;
    private TextLayout? _layout;

    public TextLayout? Get(string? text, Typeface typeface, double size, IBrush brush, double maxWidth, bool wrap, double lineHeight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        maxWidth = Math.Max(1, maxWidth);
        if (_layout is not null && string.Equals(_text, text, StringComparison.Ordinal) && _typeface == typeface && _size == size &&
            ReferenceEquals(_brush, brush) && Same(_maxWidth, maxWidth) && _wrap == wrap && _lineHeight.Equals(lineHeight))
        {
            return _layout;
        }
        _layout?.Dispose();
        _text = text;
        _typeface = typeface;
        _size = size;
        _brush = brush;
        _maxWidth = maxWidth;
        _wrap = wrap;
        _lineHeight = lineHeight;
        _layout = new TextLayout(text, typeface, size, brush, TextAlignment.Left,
            wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis, null,
            FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, lineHeight, 0, wrap ? 0 : 1);
        return _layout;
    }

    public void Reset()
    {
        _layout?.Dispose();
        _layout = null;
        _text = null;
    }

    private static bool Same(double a, double b) =>
        double.IsPositiveInfinity(a) && double.IsPositiveInfinity(b) || Math.Abs(a - b) < 0.01;
}

/// <summary>Draws 24 × 24 Lucide icons at a size with a stroke that stays 1.75px on screen.</summary>
internal static class RolesPermissionsIcons
{
    public static void Draw(DrawingContext context, Geometry icon, IPen pen, Point center, double size, double angle = 0)
    {
        var scale = size / 24;
        var transform = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale);
        if (angle != 0)
        {
            transform *= Matrix.CreateRotation(angle * Math.PI / 180);
        }
        transform *= Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(transform))
        {
            context.DrawGeometry(null, pen, icon);
        }
    }

    /// <summary>A round-capped pen whose stroke is 1.75px after the icon is scaled to <paramref name="size"/>.</summary>
    public static ImmutablePen Pen(IImmutableSolidColorBrush brush, double size)
        => new(brush, 1.75 * 24 / size, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
}

/// <summary>
/// A role's column header: a 44-high button (6 narrower than the column, radius 10) with the name (500, ellipsis) between
/// a lock (locked roles) and a chevron that turns over while the menu is open, and a second line with the member count or
/// "New role", led by the unsaved dot when the role has changes. Opens the role's menu.
/// <para>
/// Self-drawn for the same reason as the cells (one light control per column that the grid re-arranges per frame while a
/// column springs). The hover tint (120ms), the chevron (580ms CSS spring) and the dot (quick spring) run in code because
/// they belong to the drawing.
/// </para>
/// </summary>
internal sealed class RolesPermissionsRoleHeader : RolesPermissionsPart
{
    private readonly RolesPermissionsText _name = new();
    private readonly RolesPermissionsText _line = new();
    private readonly SolidColorBrush _hoverBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _dotBrush = new(Colors.Orange);
    private MotionValue<double>? _hover;
    private MotionValue<double>? _chevron;
    private MotionValue<double>? _dot;
    private ImmutablePen? _iconPen;
    private int _iconPenVersion = -1;
    private bool _pointerOver;
    private bool _isOpen;
    private bool _hasChanges;
    private bool _spaceDown;

    public RolesPermissionsRoleHeader()
    {
        Focusable = true;
        IsTabStop = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public RolesPermissionsRole Role { get; init; } = null!;

    public bool IsOpen => _isOpen;

    public bool HasChanges => _hasChanges;

    internal string? SecondLine { get; private set; }

    public void SetOpen(bool open)
    {
        if (_isOpen == open)
        {
            return;
        }
        _isOpen = open;
        Ensure();
        Run(_chevron!, open ? 180 : 0, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        RunHover();
        Refresh();
    }

    public void SetChanges(bool changes, bool animate)
    {
        Ensure();
        if (_hasChanges != changes || !animate)
        {
            _hasChanges = changes;
            var target = changes ? 1.0 : 0.0;
            if (animate && Animates)
            {
                _dot!.AnimateTo(target, MotionSpec.Spring(ArcSpring.Quick));
            }
            else
            {
                _dot!.Jump(target);
            }
        }
        Refresh();
    }

    /// <summary>Re-reads the role's name, count and state into the drawing and the screen reader name.</summary>
    public void Refresh()
    {
        if (Grid?.Owner is not { } owner)
        {
            return;
        }
        var labels = owner.Labels;
        SecondLine = Role.IsNew ? labels.NewRole : labels.People(Role.MemberCount);
        var name = Role.Name + ", " + SecondLine;
        if (Role.IsLocked)
        {
            name += labels.FixedSuffix;
        }
        if (_hasChanges)
        {
            name += labels.ChangedSuffix;
        }
        AutomationProperties.SetName(this, name);
        InvalidateVisual();
    }

    private void Ensure()
    {
        if (_hover is not null)
        {
            return;
        }
        _hover = new MotionValue<double>(this, 0, MotionVectors.Double);
        _hover.Changed += _ => InvalidateVisual();
        _chevron = new MotionValue<double>(this, 0, MotionVectors.Double);
        _chevron.Changed += _ => InvalidateVisual();
        _dot = new MotionValue<double>(this, 0, MotionVectors.Double);
        _dot.Changed += _ => InvalidateVisual();
    }

    private void Run(MotionValue<double> value, double target, MotionSpec spec)
    {
        if (Animates)
        {
            value.AnimateTo(target, spec);
        }
        else
        {
            value.Jump(target);
        }
    }

    private void RunHover()
    {
        Ensure();
        // Arc: the header's background turns to the muted surface in 120ms on hover (and stays while its menu is open).
        Run(_hover!, _pointerOver || _isOpen ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 94 : availableSize.Width;
        var height = Grid?.RoleButtonHeight ?? 44;
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (Grid is null || Bounds.Width < 2)
        {
            return;
        }
        var paint = Grid.Paint;
        var radius = Grid.RoleButtonRadius;
        var hover = _hover?.Value ?? 0;
        if (hover > 0.001)
        {
            _hoverBrush.Color = RolesPermissionsPaint.Fade(paint.SurfaceMuted, hover);
            context.DrawRectangle(_hoverBrush, null, bounds, radius, radius);
        }
        if (_iconPenVersion != paint.Version)
        {
            _iconPen = RolesPermissionsIcons.Pen(paint.TextMutedBrush, 12);
            _iconPenVersion = paint.Version;
        }
        var typeface = new Typeface(Grid.FontFamily, FontStyle.Normal, FontWeight.Medium);
        var regular = new Typeface(Grid.FontFamily, FontStyle.Normal, FontWeight.Normal);
        // The name leaves room for the chevron on its right (and the same on its left so it stays centred); below 540
        // the chevron is gone and the name may use more of the button.
        var chevron = Grid.IsChevronVisible;
        var inner = Math.Max(1, bounds.Width - 8 - (chevron ? 36 : 16));
        var name = _name.Get(Role.Name, typeface, 13, paint.ForegroundBrush, inner, false, 18.2);
        var line = _line.Get(SecondLine, regular, 12, paint.TextMutedBrush, Math.Max(1, bounds.Width - 8), false, 16.8);
        var top = (bounds.Height - (18.2 + 1 + 16.8)) / 2;
        if (name is not null)
        {
            var nameWidth = Math.Min(inner, name.WidthIncludingTrailingWhitespace);
            var x = (bounds.Width - nameWidth) / 2;
            name.Draw(context, new Point(x, top));
            var mid = top + 18.2 / 2;
            if (Role.IsLocked)
            {
                RolesPermissionsIcons.Draw(context, ArcIcons.Lock, _iconPen!, new Point(x - 16 + 6, mid), 12);
            }
            if (chevron)
            {
                RolesPermissionsIcons.Draw(context, ArcIcons.ChevronDown, _iconPen!, new Point(x + nameWidth + 4 + 6, mid), 12, _chevron?.Value ?? 0);
            }
        }
        if (line is not null)
        {
            var lineWidth = line.WidthIncludingTrailingWhitespace;
            var x = (bounds.Width - lineWidth) / 2;
            var y = top + 18.2 + 1;
            line.Draw(context, new Point(x, y));
            var dot = _dot?.Value ?? 0;
            if (dot > 0.001)
            {
                var size = Grid.DotSize * (0.3 + 0.7 * dot);
                _dotBrush.Color = RolesPermissionsPaint.Fade(paint.Warning, dot);
                context.DrawEllipse(_dotBrush, null, new Point(x - 10 + Grid.DotSize / 2, y + 16.8 / 2), size / 2, size / 2);
            }
        }
        if (IsFocusVisible)
        {
            DrawFocusRing(context, bounds, radius);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _pointerOver = true;
            RunHover();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        RunHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        Grid?.Owner?.ToggleRoleMenu(this, MenuOpenFocus.Surface);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter)
        {
            e.Handled = true;
            Grid?.Owner?.ToggleRoleMenu(this, MenuOpenFocus.First);
            return;
        }
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Space)
        {
            e.Handled = true;
            _spaceDown = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space && _spaceDown)
        {
            _spaceDown = false;
            e.Handled = true;
            Grid?.Owner?.ToggleRoleMenu(this, MenuOpenFocus.First);
            return;
        }
        base.OnKeyUp(e);
    }

    internal void ResetText()
    {
        _name.Reset();
        _line.Reset();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);

    /// <summary>A column header button with a menu (aria-haspopup=menu, aria-expanded).</summary>
    private sealed class HeaderPeer(RolesPermissionsRoleHeader owner) : ControlAutomationPeer(owner), IExpandCollapseProvider, IInvokeProvider
    {
        private RolesPermissionsRoleHeader Header => (RolesPermissionsRoleHeader)Owner;

        public ExpandCollapseState ExpandCollapseState => Header.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand()
        {
            if (!Header.IsOpen)
            {
                Header.Grid?.Owner?.ToggleRoleMenu(Header, MenuOpenFocus.First);
            }
        }

        public void Collapse()
        {
            if (Header.IsOpen)
            {
                Header.Grid?.Owner?.ToggleRoleMenu(Header, MenuOpenFocus.First);
            }
        }

        public void Invoke() => Header.Grid?.Owner?.ToggleRoleMenu(Header, MenuOpenFocus.First);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// A group's disclosure button: chevron (turned −90° while folded), the group's name (500) and its permission count
/// (hidden below 540px), 28 high with radius 8. Toggles <see cref="RolesPermissionsGroup.IsExpanded"/>.
/// </summary>
internal sealed class RolesPermissionsGroupButton : RolesPermissionsPart
{
    private readonly RolesPermissionsText _name = new();
    private readonly RolesPermissionsText _count = new();
    private readonly SolidColorBrush _hoverBrush = new(Colors.Transparent);
    private MotionValue<double>? _hover;
    private MotionValue<double>? _angle;
    private ImmutablePen? _iconPen;
    private int _iconPenVersion = -1;
    private bool _spaceDown;

    public RolesPermissionsGroupButton()
    {
        Focusable = true;
        IsTabStop = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public RolesPermissionsGroup Group { get; init; } = null!;

    public int PermissionCount { get; set; }

    public bool IsExpanded { get; private set; } = true;

    internal double ChevronAngle => _angle?.Value ?? (IsExpanded ? 0 : -90);

    public void SetExpanded(bool expanded, bool animate)
    {
        Ensure();
        IsExpanded = expanded;
        var target = expanded ? 0.0 : -90.0;
        if (animate && Animates)
        {
            // Arc's chevron turns on a slightly bouncy spring (fitted ω≈16, ζ≈.85): the morph preset.
            _angle!.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
        }
        else
        {
            _angle!.Jump(target);
        }
        Refresh();
    }

    public void Refresh()
    {
        if (Grid?.Owner is not { } owner)
        {
            return;
        }
        AutomationProperties.SetName(this, ProjectsText.Format(owner.Labels.GroupButtonFormat, Group.Name, PermissionCount));
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void Ensure()
    {
        if (_hover is not null)
        {
            return;
        }
        _hover = new MotionValue<double>(this, 0, MotionVectors.Double);
        _hover.Changed += _ => InvalidateVisual();
        _angle = new MotionValue<double>(this, 0, MotionVectors.Double);
        _angle.Changed += _ => InvalidateVisual();
    }

    private Typeface NameFace => new(Grid!.FontFamily, FontStyle.Normal, FontWeight.Medium);

    private Typeface CountFace => new(Grid!.FontFamily, FontStyle.Normal, FontWeight.Normal);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Grid is null)
        {
            return new Size(80, 28);
        }
        var paint = Grid.Paint;
        var name = _name.Get(Group.Name, NameFace, 13, paint.ForegroundBrush, double.PositiveInfinity, false, 16.9);
        var width = 6 + 16 + 8 + (name?.WidthIncludingTrailingWhitespace ?? 0) + 8;
        if (Grid.IsCountVisible)
        {
            var count = _count.Get(PermissionCount.ToString(System.Globalization.CultureInfo.InvariantCulture), CountFace, 13,
                paint.TextMutedBrush, double.PositiveInfinity, false, 16.9);
            width += 8 + (count?.WidthIncludingTrailingWhitespace ?? 0);
        }
        var max = double.IsInfinity(availableSize.Width) ? width : availableSize.Width;
        return new Size(Math.Min(width, max), Grid.GroupButtonHeight);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (Grid is null)
        {
            return;
        }
        var paint = Grid.Paint;
        var hover = _hover?.Value ?? 0;
        if (hover > 0.001)
        {
            _hoverBrush.Color = RolesPermissionsPaint.Fade(paint.SurfaceMuted, hover);
            context.DrawRectangle(_hoverBrush, null, bounds, Grid.GroupButtonRadius, Grid.GroupButtonRadius);
        }
        if (_iconPenVersion != paint.Version)
        {
            _iconPen = RolesPermissionsIcons.Pen(paint.TextMutedBrush, 16);
            _iconPenVersion = paint.Version;
        }
        var mid = bounds.Height / 2;
        RolesPermissionsIcons.Draw(context, ArcIcons.ChevronDown, _iconPen!, new Point(6 + 8, mid), 16, ChevronAngle);
        var x = 6 + 16 + 8.0;
        var room = Math.Max(1, bounds.Width - x - 8);
        var name = _name.Get(Group.Name, NameFace, 13, paint.ForegroundBrush, double.PositiveInfinity, false, 16.9);
        if (name is not null)
        {
            using (context.PushClip(new Rect(x, 0, room, bounds.Height)))
            {
                name.Draw(context, new Point(x, mid - 16.9 / 2));
            }
            x += name.WidthIncludingTrailingWhitespace + 8;
        }
        if (Grid.IsCountVisible)
        {
            var count = _count.Get(PermissionCount.ToString(System.Globalization.CultureInfo.InvariantCulture), CountFace, 13,
                paint.TextMutedBrush, double.PositiveInfinity, false, 16.9);
            count?.Draw(context, new Point(x, mid - 16.9 / 2));
        }
        if (IsFocusVisible)
        {
            DrawFocusRing(context, bounds, Grid.GroupButtonRadius);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            Ensure();
            Hover(1);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Ensure();
        Hover(0);
    }

    private void Hover(double target)
    {
        if (Animates)
        {
            _hover!.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            _hover!.Jump(target);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        Grid?.Owner?.ToggleGroupExpanded(Group);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter)
        {
            e.Handled = true;
            Grid?.Owner?.ToggleGroupExpanded(Group);
            return;
        }
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Space)
        {
            e.Handled = true;
            _spaceDown = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space && _spaceDown)
        {
            _spaceDown = false;
            e.Handled = true;
            Grid?.Owner?.ToggleGroupExpanded(Group);
            return;
        }
        base.OnKeyUp(e);
    }

    internal void ResetText()
    {
        _name.Reset();
        _count.Reset();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer(RolesPermissionsGroupButton owner) : ControlAutomationPeer(owner), IExpandCollapseProvider
    {
        private RolesPermissionsGroupButton Button => (RolesPermissionsGroupButton)Owner;

        public ExpandCollapseState ExpandCollapseState => Button.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Button.IsExpanded)
            {
                Button.Grid?.Owner?.ToggleGroupExpanded(Button.Group);
            }
        }

        public void Collapse()
        {
            if (Button.IsExpanded)
            {
                Button.Grid?.Owner?.ToggleGroupExpanded(Button.Group);
            }
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The sticky label column: the "Permission" caption and every permission name (wrapping when the column is narrow),
/// over an opaque surface with the rows' borders and hover tint, so role columns scrolled under it disappear. The grid
/// places it at the scroll offset; while scrolled, a 1px border marks its right edge (160ms).
/// </summary>
internal sealed class RolesPermissionsLabelLayer : Control
{
    private readonly SolidColorBrush _hoverBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _dividerBrush = new(Colors.Transparent);
    private readonly RolesPermissionsText _caption = new();
    private MotionValue<double>? _divider;

    public RolesPermissionsGrid? Grid { get; set; }

    public void SetScrolled(bool scrolled)
    {
        _divider ??= CreateDivider();
        var target = scrolled ? 1.0 : 0.0;
        if (ArcMotion.IsFull(this) && (TopLevel.GetTopLevel(this) is not null))
        {
            _divider.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _divider.Jump(target);
        }
    }

    private MotionValue<double> CreateDivider()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public void ResetText() => _caption.Reset();

    public override void Render(DrawingContext context)
    {
        var grid = Grid;
        if (grid is null)
        {
            return;
        }
        var paint = grid.Paint;
        var width = Bounds.Width;
        context.FillRectangle(paint.SurfaceBrush, new Rect(Bounds.Size));
        var regular = new Typeface(grid.FontFamily, FontStyle.Normal, FontWeight.Normal);
        var caption = _caption.Get(grid.Owner?.Labels.PermissionHeader, regular, 12, paint.TextMutedBrush,
            Math.Max(1, width - grid.LabelInset - grid.LabelEndInset), false, 15.6);
        caption?.Draw(context, new Point(grid.LabelInset, grid.HeaderRowHeight - 12 - 15.6));

        foreach (var row in grid.Rows)
        {
            if (row.Kind == RolesPermissionsRowKind.Header || !row.IsPlaced)
            {
                continue;
            }
            var rect = new Rect(0, row.Y, width, row.Height);
            using (row.Clip is { } clip ? context.PushClip(new Rect(0, clip.Top, width, clip.Height)) : default(DrawingContext.PushedState?))
            {
                using (row.Opacity < 1 ? context.PushOpacity(row.Opacity) : default(DrawingContext.PushedState?))
                {
                    var hover = row.HoverValue;
                    if (hover > 0.001)
                    {
                        _hoverBrush.Color = RolesPermissionsPaint.Fade(paint.RowHoverFill, hover);
                        context.FillRectangle(_hoverBrush, rect);
                    }
                    var pen = row.Kind == RolesPermissionsRowKind.Group ? paint.BorderPen : paint.BorderSubtlePen;
                    context.DrawLine(pen, new Point(0, row.Y + 0.5), new Point(width, row.Y + 0.5));
                    if (row.Kind == RolesPermissionsRowKind.Permission && row.Label is { } label)
                    {
                        var content = row.Height - 1;
                        label.Draw(context, new Point(grid.PermissionInset, row.Y + 1 + (content - label.Height) / 2));
                    }
                }
            }
        }

        var divider = _divider?.Value ?? 0;
        if (divider > 0.001)
        {
            _dividerBrush.Color = RolesPermissionsPaint.Fade(paint.Border, divider);
            context.FillRectangle(_dividerBrush, new Rect(width - 1, 0, 1, Bounds.Height));
        }
    }
}
