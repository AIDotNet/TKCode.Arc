using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a member row lays out its columns, by the block's width.</summary>
internal enum TeamMembersRowMode
{
    /// <summary>Wider than 640: identity, last active (128), role (112), remove (32) on one line.</summary>
    Wide,

    /// <summary>640 or less: the last-active text moves under the email; role 104, remove 32.</summary>
    Narrow,

    /// <summary>420 or less: identity on top; last active, role (104) and remove (32) on a second line.</summary>
    TwoTier,
}

/// <summary>
/// Lays out a member row inside its padding: child 0 the avatar, 1 the identity, 2 the last-active text, 3 the role cell,
/// 4 the remove button, 5 the confirm face (which covers everything right of the avatar). Columns follow
/// <see cref="Mode"/>; hidden children keep their column so rows line up.
/// </summary>
internal sealed class TeamMembersMemberRowPanel : Panel
{
    public static readonly StyledProperty<TeamMembersRowMode> ModeProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, TeamMembersRowMode>(nameof(Mode));

    public static readonly StyledProperty<double> AvatarSizeProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(AvatarSize), 36);

    public static readonly StyledProperty<double> AvatarGapProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(AvatarGap), 12);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(ColumnGap), 12);

    public static readonly StyledProperty<double> ActiveWidthProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(ActiveWidth), 128);

    public static readonly StyledProperty<double> RoleWidthProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(RoleWidth), 112);

    public static readonly StyledProperty<double> NarrowRoleWidthProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(NarrowRoleWidth), 104);

    public static readonly StyledProperty<double> RemoveWidthProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(RemoveWidth), 32);

    /// <summary>The second tier's height in <see cref="TeamMembersRowMode.TwoTier"/> (32) and the gap between its cells (8).</summary>
    public static readonly StyledProperty<double> TierHeightProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(TierHeight), 32);

    public static readonly StyledProperty<double> TierGapProperty =
        AvaloniaProperty.Register<TeamMembersMemberRowPanel, double>(nameof(TierGap), 8);

    static TeamMembersMemberRowPanel()
    {
        AffectsMeasure<TeamMembersMemberRowPanel>(ModeProperty, AvatarSizeProperty, AvatarGapProperty, ColumnGapProperty,
            ActiveWidthProperty, RoleWidthProperty, NarrowRoleWidthProperty, RemoveWidthProperty, TierHeightProperty, TierGapProperty);
    }

    public TeamMembersRowMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    public double AvatarSize { get => GetValue(AvatarSizeProperty); set => SetValue(AvatarSizeProperty, value); }

    public double AvatarGap { get => GetValue(AvatarGapProperty); set => SetValue(AvatarGapProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double ActiveWidth { get => GetValue(ActiveWidthProperty); set => SetValue(ActiveWidthProperty, value); }

    public double RoleWidth { get => GetValue(RoleWidthProperty); set => SetValue(RoleWidthProperty, value); }

    public double NarrowRoleWidth { get => GetValue(NarrowRoleWidthProperty); set => SetValue(NarrowRoleWidthProperty, value); }

    public double RemoveWidth { get => GetValue(RemoveWidthProperty); set => SetValue(RemoveWidthProperty, value); }

    public double TierHeight { get => GetValue(TierHeightProperty); set => SetValue(TierHeightProperty, value); }

    public double TierGap { get => GetValue(TierGapProperty); set => SetValue(TierGapProperty, value); }

    private Control? Child(int index) => index < Children.Count ? Children[index] : null;

    private (double Identity, double Active, double Role, double Remove, double Gap) Columns(double viewWidth)
    {
        var gap = ColumnGap;
        switch (Mode)
        {
            case TeamMembersRowMode.Wide:
            {
                var identity = Math.Max(0, viewWidth - ActiveWidth - RoleWidth - RemoveWidth - 3 * gap);
                return (identity, ActiveWidth, RoleWidth, RemoveWidth, gap);
            }
            case TeamMembersRowMode.Narrow:
            {
                var identity = Math.Max(0, viewWidth - NarrowRoleWidth - RemoveWidth - 2 * gap);
                return (identity, 0, NarrowRoleWidth, RemoveWidth, gap);
            }
            default:
            {
                var tierGap = TierGap;
                var active = Math.Max(0, viewWidth - NarrowRoleWidth - RemoveWidth - 2 * tierGap);
                return (viewWidth, active, NarrowRoleWidth, RemoveWidth, tierGap);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var viewX = AvatarSize + AvatarGap;
        var viewWidth = Math.Max(0, width - viewX);
        var c = Columns(viewWidth);
        var inf = double.PositiveInfinity;
        Child(0)?.Measure(new Size(AvatarSize, AvatarSize));
        Child(1)?.Measure(new Size(c.Identity, inf));
        Child(2)?.Measure(new Size(Math.Max(0, c.Active), inf));
        Child(3)?.Measure(new Size(c.Role, inf));
        Child(4)?.Measure(new Size(c.Remove, inf));
        var identity = Child(1)?.DesiredSize.Height ?? 0;
        double height = Mode == TeamMembersRowMode.TwoTier
            ? Math.Max(AvatarSize, identity) + TierHeight
            : Math.Max(AvatarSize, identity);
        if (Child(5) is { } confirm)
        {
            confirm.Measure(new Size(viewWidth, inf));
            height = Math.Max(height, confirm.IsVisible ? confirm.DesiredSize.Height : 0);
        }
        for (var i = 6; i < Children.Count; i++)
        {
            Children[i].Measure(availableSize);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var viewX = AvatarSize + AvatarGap;
        var viewWidth = Math.Max(0, finalSize.Width - viewX);
        var c = Columns(viewWidth);
        var h = finalSize.Height;
        if (Mode == TeamMembersRowMode.TwoTier)
        {
            var identityHeight = Math.Max(AvatarSize, Child(1)?.DesiredSize.Height ?? 0);
            Child(0)?.Arrange(new Rect(0, (identityHeight - AvatarSize) / 2, AvatarSize, AvatarSize));
            Place(Child(1), viewX, 0, c.Identity, identityHeight);
            var y = identityHeight;
            var tier = TierHeight;
            Place(Child(2), viewX, y, c.Active, tier);
            Place(Child(3), viewX + c.Active + c.Gap, y, c.Role, tier);
            Place(Child(4), viewX + c.Active + c.Role + 2 * c.Gap, y, c.Remove, tier);
        }
        else
        {
            Child(0)?.Arrange(new Rect(0, (h - AvatarSize) / 2, AvatarSize, AvatarSize));
            var x = viewX;
            Place(Child(1), x, 0, c.Identity, h);
            x += c.Identity + c.Gap;
            if (Mode == TeamMembersRowMode.Wide)
            {
                Place(Child(2), x, 0, c.Active, h);
                x += c.Active + c.Gap;
            }
            else
            {
                Child(2)?.Arrange(new Rect(x, 0, 0, 0));
            }
            Place(Child(3), x, 0, c.Role, h);
            x += c.Role + c.Gap;
            Place(Child(4), x, 0, c.Remove, h);
        }
        if (Child(5) is { } confirm)
        {
            confirm.Arrange(new Rect(viewX, 0, viewWidth, h));
        }
        for (var i = 6; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(finalSize));
        }
        return finalSize;
    }

    /// <summary>Arranges a child in its cell, centred vertically by its own alignment (the cell is the full row height).</summary>
    private static void Place(Control? child, double x, double y, double width, double height)
    {
        if (child is null)
        {
            return;
        }
        var desired = child.DesiredSize;
        var childHeight = child.VerticalAlignment == VerticalAlignment.Stretch ? height : Math.Min(desired.Height, height);
        var top = child.VerticalAlignment switch
        {
            VerticalAlignment.Top => y,
            VerticalAlignment.Bottom => y + height - childHeight,
            VerticalAlignment.Stretch => y,
            _ => y + (height - childHeight) / 2
        };
        var childWidth = child.HorizontalAlignment == HorizontalAlignment.Stretch ? width : Math.Min(desired.Width, width);
        var left = child.HorizontalAlignment switch
        {
            HorizontalAlignment.Right => x + width - childWidth,
            HorizontalAlignment.Center => x + (width - childWidth) / 2,
            _ => x
        };
        child.Arrange(new Rect(left, top, Math.Max(0, childWidth), Math.Max(0, childHeight)));
    }
}

/// <summary>
/// Lays out a pending invite row inside its padding: child 0 the dashed avatar, 1 the identity (address and detail), 2 the
/// buttons. Inline, the buttons sit at the right; with <see cref="ActionsBelow"/> (480 or less) they go under the address,
/// shifted left by <see cref="BelowShift"/> so the button text lines up with the address.
/// </summary>
internal sealed class TeamMembersInviteRowPanel : Panel
{
    public static readonly StyledProperty<bool> ActionsBelowProperty =
        AvaloniaProperty.Register<TeamMembersInviteRowPanel, bool>(nameof(ActionsBelow));

    public static readonly StyledProperty<double> AvatarSizeProperty =
        AvaloniaProperty.Register<TeamMembersInviteRowPanel, double>(nameof(AvatarSize), 36);

    public static readonly StyledProperty<double> AvatarGapProperty =
        AvaloniaProperty.Register<TeamMembersInviteRowPanel, double>(nameof(AvatarGap), 12);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<TeamMembersInviteRowPanel, double>(nameof(ColumnGap), 12);

    public static readonly StyledProperty<double> BelowGapProperty =
        AvaloniaProperty.Register<TeamMembersInviteRowPanel, double>(nameof(BelowGap), 8);

    public static readonly StyledProperty<double> BelowShiftProperty =
        AvaloniaProperty.Register<TeamMembersInviteRowPanel, double>(nameof(BelowShift), 12);

    static TeamMembersInviteRowPanel()
    {
        AffectsMeasure<TeamMembersInviteRowPanel>(ActionsBelowProperty, AvatarSizeProperty, AvatarGapProperty, ColumnGapProperty,
            BelowGapProperty, BelowShiftProperty);
    }

    public bool ActionsBelow { get => GetValue(ActionsBelowProperty); set => SetValue(ActionsBelowProperty, value); }

    public double AvatarSize { get => GetValue(AvatarSizeProperty); set => SetValue(AvatarSizeProperty, value); }

    public double AvatarGap { get => GetValue(AvatarGapProperty); set => SetValue(AvatarGapProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double BelowGap { get => GetValue(BelowGapProperty); set => SetValue(BelowGapProperty, value); }

    public double BelowShift { get => GetValue(BelowShiftProperty); set => SetValue(BelowShiftProperty, value); }

    private Control? Child(int index) => index < Children.Count ? Children[index] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var viewX = AvatarSize + AvatarGap;
        var viewWidth = Math.Max(0, width - viewX);
        var inf = double.PositiveInfinity;
        Child(0)?.Measure(new Size(AvatarSize, AvatarSize));
        Child(2)?.Measure(new Size(inf, inf));
        var actions = Child(2)?.DesiredSize ?? default;
        double height;
        if (ActionsBelow)
        {
            Child(1)?.Measure(new Size(viewWidth, inf));
            var identity = Math.Max(AvatarSize, Child(1)?.DesiredSize.Height ?? 0);
            height = identity + BelowGap + actions.Height;
        }
        else
        {
            Child(1)?.Measure(new Size(Math.Max(0, viewWidth - actions.Width - ColumnGap), inf));
            height = Math.Max(AvatarSize, Math.Max(Child(1)?.DesiredSize.Height ?? 0, actions.Height));
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var viewX = AvatarSize + AvatarGap;
        var viewWidth = Math.Max(0, finalSize.Width - viewX);
        var actions = Child(2)?.DesiredSize ?? default;
        if (ActionsBelow)
        {
            var identity = Math.Max(AvatarSize, Child(1)?.DesiredSize.Height ?? 0);
            Child(0)?.Arrange(new Rect(0, (identity - AvatarSize) / 2, AvatarSize, AvatarSize));
            var idHeight = Child(1)?.DesiredSize.Height ?? 0;
            Child(1)?.Arrange(new Rect(viewX, (identity - idHeight) / 2, viewWidth, idHeight));
            Child(2)?.Arrange(new Rect(viewX - BelowShift, identity + BelowGap, actions.Width, actions.Height));
        }
        else
        {
            var h = finalSize.Height;
            Child(0)?.Arrange(new Rect(0, (h - AvatarSize) / 2, AvatarSize, AvatarSize));
            var identityWidth = Math.Max(0, viewWidth - actions.Width - ColumnGap);
            var idHeight = Child(1)?.DesiredSize.Height ?? 0;
            Child(1)?.Arrange(new Rect(viewX, (h - idHeight) / 2, identityWidth, idHeight));
            Child(2)?.Arrange(new Rect(finalSize.Width - actions.Width, (h - actions.Height) / 2, actions.Width, actions.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The header: the titles and the seat meter side by side (the meter at the right, <see cref="Gap"/> apart, centred); with
/// <see cref="IsStacked"/> (600 or less) the meter goes under the titles at full width.
/// </summary>
internal sealed class TeamMembersHeaderPanel : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<TeamMembersHeaderPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TeamMembersHeaderPanel, double>(nameof(Gap), 24);

    public static readonly StyledProperty<double> StackedGapProperty =
        AvaloniaProperty.Register<TeamMembersHeaderPanel, double>(nameof(StackedGap), 12);

    static TeamMembersHeaderPanel()
    {
        AffectsMeasure<TeamMembersHeaderPanel>(IsStackedProperty, GapProperty, StackedGapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double StackedGap { get => GetValue(StackedGapProperty); set => SetValue(StackedGapProperty, value); }

    private Control? Child(int index) => index < Children.Count ? Children[index] : null;

    private bool HasMeter => Child(1) is { IsVisible: true };

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var inf = double.PositiveInfinity;
        if (IsStacked || !HasMeter)
        {
            Child(0)?.Measure(new Size(width, inf));
            Child(1)?.Measure(new Size(width, inf));
            var titles = Child(0)?.DesiredSize.Height ?? 0;
            var meter = HasMeter ? Child(1)!.DesiredSize.Height : 0;
            return new Size(width, titles + (HasMeter ? StackedGap + meter : 0));
        }
        Child(1)!.Measure(new Size(width, inf));
        var meterSize = Child(1)!.DesiredSize;
        Child(0)?.Measure(new Size(Math.Max(0, width - meterSize.Width - Gap), inf));
        return new Size(width, Math.Max(Child(0)?.DesiredSize.Height ?? 0, meterSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (IsStacked || !HasMeter)
        {
            var titles = Child(0)?.DesiredSize.Height ?? 0;
            Child(0)?.Arrange(new Rect(0, 0, finalSize.Width, titles));
            if (HasMeter)
            {
                Child(1)!.Arrange(new Rect(0, titles + StackedGap, finalSize.Width, Child(1)!.DesiredSize.Height));
            }
            else
            {
                Child(1)?.Arrange(default);
            }
            return finalSize;
        }
        var meterSize = Child(1)!.DesiredSize;
        var titleSize = Child(0)?.DesiredSize ?? default;
        Child(0)?.Arrange(new Rect(0, (finalSize.Height - titleSize.Height) / 2, Math.Max(0, finalSize.Width - meterSize.Width - Gap), titleSize.Height));
        Child(1)!.Arrange(new Rect(finalSize.Width - meterSize.Width, (finalSize.Height - meterSize.Height) / 2, meterSize.Width, meterSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The invite row: child 0 the email field, 1 the role picker, 2 the send button, <see cref="Gap"/> apart on one line
/// (top-aligned, the field takes the rest); with <see cref="IsStacked"/> the field takes the first line and the picker
/// (left) and the button (right) the second.
/// </summary>
internal sealed class TeamMembersInviteFormPanel : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<TeamMembersInviteFormPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TeamMembersInviteFormPanel, double>(nameof(Gap), 8);

    static TeamMembersInviteFormPanel()
    {
        AffectsMeasure<TeamMembersInviteFormPanel>(IsStackedProperty, GapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private Control? Child(int index) => index < Children.Count ? Children[index] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var inf = double.PositiveInfinity;
        Child(1)?.Measure(new Size(inf, inf));
        Child(2)?.Measure(new Size(inf, inf));
        var picker = Child(1)?.DesiredSize ?? default;
        var send = Child(2)?.DesiredSize ?? default;
        if (IsStacked)
        {
            Child(0)?.Measure(new Size(width, inf));
            var field = Child(0)?.DesiredSize.Height ?? 0;
            return new Size(width, field + Gap + Math.Max(picker.Height, send.Height));
        }
        Child(0)?.Measure(new Size(Math.Max(0, width - picker.Width - send.Width - 2 * Gap), inf));
        return new Size(width, Math.Max(Child(0)?.DesiredSize.Height ?? 0, Math.Max(picker.Height, send.Height)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var picker = Child(1)?.DesiredSize ?? default;
        var send = Child(2)?.DesiredSize ?? default;
        var field = Child(0)?.DesiredSize.Height ?? 0;
        if (IsStacked)
        {
            Child(0)?.Arrange(new Rect(0, 0, finalSize.Width, field));
            var y = field + Gap;
            Child(1)?.Arrange(new Rect(0, y, picker.Width, picker.Height));
            Child(2)?.Arrange(new Rect(finalSize.Width - send.Width, y, send.Width, send.Height));
            return finalSize;
        }
        var fieldWidth = Math.Max(0, finalSize.Width - picker.Width - send.Width - 2 * Gap);
        Child(0)?.Arrange(new Rect(0, 0, fieldWidth, field));
        Child(1)?.Arrange(new Rect(fieldWidth + Gap, 0, picker.Width, picker.Height));
        Child(2)?.Arrange(new Rect(finalSize.Width - send.Width, 0, send.Width, send.Height));
        return finalSize;
    }
}

/// <summary>
/// A section head: child 0 the heading (title and count), 1 the search (optional) at <see cref="SearchWidth"/> on the right;
/// with <see cref="IsStacked"/> the search takes its own full-width line, <see cref="RowGap"/> under the heading.
/// </summary>
internal sealed class TeamMembersSectionHeadPanel : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<TeamMembersSectionHeadPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> SearchWidthProperty =
        AvaloniaProperty.Register<TeamMembersSectionHeadPanel, double>(nameof(SearchWidth), 240);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<TeamMembersSectionHeadPanel, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<TeamMembersSectionHeadPanel, double>(nameof(ColumnGap), 16);

    static TeamMembersSectionHeadPanel()
    {
        AffectsMeasure<TeamMembersSectionHeadPanel>(IsStackedProperty, SearchWidthProperty, RowGapProperty, ColumnGapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double SearchWidth { get => GetValue(SearchWidthProperty); set => SetValue(SearchWidthProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    private Control? Child(int index) => index < Children.Count ? Children[index] : null;

    private bool HasSearch => Child(1) is { IsVisible: true };

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var inf = double.PositiveInfinity;
        if (!HasSearch)
        {
            Child(0)?.Measure(new Size(width, inf));
            Child(1)?.Measure(default);
            return new Size(width, Child(0)?.DesiredSize.Height ?? 0);
        }
        if (IsStacked)
        {
            Child(0)?.Measure(new Size(width, inf));
            Child(1)!.Measure(new Size(width, inf));
            return new Size(width, (Child(0)?.DesiredSize.Height ?? 0) + RowGap + Child(1)!.DesiredSize.Height);
        }
        var search = Math.Min(SearchWidth, width);
        Child(1)!.Measure(new Size(search, inf));
        Child(0)?.Measure(new Size(Math.Max(0, width - search - ColumnGap), inf));
        return new Size(width, Math.Max(Child(0)?.DesiredSize.Height ?? 0, Child(1)!.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var heading = Child(0)?.DesiredSize ?? default;
        if (!HasSearch)
        {
            Child(0)?.Arrange(new Rect(0, (finalSize.Height - heading.Height) / 2, finalSize.Width, heading.Height));
            Child(1)?.Arrange(default);
            return finalSize;
        }
        var search = Child(1)!.DesiredSize;
        if (IsStacked)
        {
            Child(0)?.Arrange(new Rect(0, 0, finalSize.Width, heading.Height));
            Child(1)!.Arrange(new Rect(0, heading.Height + RowGap, finalSize.Width, search.Height));
            return finalSize;
        }
        var searchWidth = Math.Min(SearchWidth, finalSize.Width);
        Child(0)?.Arrange(new Rect(0, (finalSize.Height - heading.Height) / 2, Math.Max(0, finalSize.Width - searchWidth - ColumnGap), heading.Height));
        Child(1)!.Arrange(new Rect(finalSize.Width - searchWidth, (finalSize.Height - search.Height) / 2, searchWidth, search.Height));
        return finalSize;
    }
}

/// <summary>
/// Holds one list row and makes it enter, leave and collapse with Arc's layout animation: the height follows
/// <c>reveal × the row's height</c> on the Smooth spring (code-driven because the row's own height is only known after
/// layout and an exit must report when it finished), with an opacity tween on top. Clips only while it is not fully open.
/// </summary>
internal sealed class TeamMembersRowSlot : Decorator
{
    private readonly MotionValue<double> _reveal;
    private readonly MotionValue<double> _fade;
    private readonly RectangleGeometry _clip = new();
    private double _childHeight;

    public TeamMembersRowSlot()
    {
        _reveal = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _reveal.Changed += _ => InvalidateMeasure();
        _fade.Changed += v => Opacity = v;
    }

    /// <summary>The fraction of the row's height shown (0 collapsed, 1 open).</summary>
    public double Reveal => _reveal.Value;

    /// <summary>True while the row is leaving (it is removed when the exit ends).</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>True while the row is collapsed by the search (or collapsing).</summary>
    public bool IsCollapsed { get; private set; }

    /// <summary>The key of the data item shown.</summary>
    public string? Key { get; set; }

    /// <summary>True while the height is moving.</summary>
    public bool IsMoving => _reveal.IsAnimating;

    /// <summary>Starts collapsed and invisible (call before the slot is first measured), then opens.</summary>
    public void Enter(bool animate, TimeSpan fade)
    {
        if (!animate || !ArcMotion.IsFull(this))
        {
            _reveal.Jump(1);
            _fade.Jump(1);
            return;
        }
        _reveal.Jump(0);
        _ = _reveal.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        if (fade > TimeSpan.Zero)
        {
            _fade.Jump(0);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(fade, ArcEasings.Standard));
        }
        else
        {
            _fade.Jump(1);
        }
    }

    /// <summary>Fades out (120ms) while the height closes; completes when the height reached zero.</summary>
    public async Task<bool> ExitAsync(bool animate)
    {
        IsLeaving = true;
        IsHitTestVisible = false;
        if (!animate || !ArcMotion.IsFull(this) || !IsAttached)
        {
            _fade.Jump(0);
            _reveal.Jump(0);
            return true;
        }
        _ = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        return await _reveal.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>Collapses (search filtered the row out) or opens it again (with a 200ms fade in).</summary>
    public void SetCollapsed(bool collapsed, bool animate)
    {
        if (collapsed == IsCollapsed)
        {
            return;
        }
        IsCollapsed = collapsed;
        IsHitTestVisible = !collapsed;
        var full = animate && ArcMotion.IsFull(this) && IsAttached;
        if (collapsed)
        {
            if (full)
            {
                _ = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
                _ = _reveal.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _fade.Jump(0);
                _reveal.Jump(0);
            }
            return;
        }
        if (full)
        {
            _ = _fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard));
            _ = _reveal.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _fade.Jump(1);
            _reveal.Jump(1);
        }
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        _childHeight = child.DesiredSize.Height;
        var reveal = Math.Clamp(_reveal.Value, 0, 1);
        var height = reveal >= 0.999 ? _childHeight : Math.Round(_childHeight * reveal, 2);
        return new Size(child.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_childHeight, finalSize.Height)));
        if (_reveal.Value >= 0.999)
        {
            Clip = null;
        }
        else
        {
            // Wider than the slot: a focus ring may stick out sideways; only the bottom edge cuts.
            _clip.Rect = new Rect(-8, 0, finalSize.Width + 16, finalSize.Height);
            Clip = _clip;
            InvalidateVisual();
        }
        return finalSize;
    }
}

/// <summary>
/// A pending invite's avatar: a dashed circle (1px BorderStrong) with the initials in TextMuted. After a send it pops in
/// (opacity 0 → 1, scale 0.6 → 1 on a slightly underdamped spring, after a delay); drawn in Render.
/// </summary>
internal sealed class TeamMembersDashedAvatar : Control
{
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<TeamMembersDashedAvatar, string?>(nameof(Initials));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<TeamMembersDashedAvatar, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<TeamMembersDashedAvatar, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TeamMembersDashedAvatar, double>(nameof(FontSize), 12);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<TeamMembersDashedAvatar, FontFamily>(nameof(FontFamily), FontFamily.Default);

    /// <summary>The pop's spring: ω 12, ζ 0.8 (measured; slightly underdamped).</summary>
    internal static readonly SpringSpec PopSpring = SpringSpec.FromPhysical(144, 19.2);

    private readonly MotionValue<double> _pop;
    private readonly ScaleTransform _scale = new();
    private FormattedText? _text;
    private Pen? _pen;

    static TeamMembersDashedAvatar()
    {
        AffectsRender<TeamMembersDashedAvatar>(InitialsProperty, StrokeProperty, ForegroundProperty, FontSizeProperty, FontFamilyProperty);
    }

    public TeamMembersDashedAvatar()
    {
        _pop = new MotionValue<double>(this, 1, MotionVectors.Double);
        _pop.Changed += Apply;
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = _scale;
    }

    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The pop's progress (0 hidden, 1 shown).</summary>
    public double PopProgress => _pop.Value;

    /// <summary>Hides the avatar now and pops it in after <paramref name="delay"/> (Full motion only; otherwise it shows at once).</summary>
    public void PopIn(TimeSpan delay)
    {
        if (!ArcMotion.IsFull(this))
        {
            _pop.Jump(1);
            return;
        }
        _pop.Jump(0);
        _ = _pop.AnimateTo(1, MotionSpec.Spring(PopSpring, delay));
    }

    private void Apply(double value)
    {
        Opacity = Math.Clamp(value, 0, 1);
        var scale = 0.6 + 0.4 * value;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InitialsProperty || change.Property == ForegroundProperty || change.Property == FontSizeProperty ||
            change.Property == FontFamilyProperty)
        {
            _text = null;
        }
        else if (change.Property == StrokeProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(36, 36);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var d = Math.Min(bounds.Width, bounds.Height);
        var rect = new Rect((bounds.Width - d) / 2 + 0.5, (bounds.Height - d) / 2 + 0.5, d - 1, d - 1);
        _pen ??= new Pen(Stroke ?? Brushes.Gray, 1, new DashStyle([3, 3], 0));
        context.DrawEllipse(Brushes.Transparent, _pen, rect);
        if (string.IsNullOrEmpty(Initials))
        {
            return;
        }
        _text ??= new FormattedText(Initials, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium), FontSize, Foreground ?? Brushes.Gray);
        context.DrawText(_text, new Point((bounds.Width - _text.Width) / 2, (bounds.Height - _text.Height) / 2));
    }
}
