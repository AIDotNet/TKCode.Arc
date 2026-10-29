using System.Runtime.CompilerServices;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The block's own buttons (text, icon, role, outline and send looks are classes in the theme).</summary>
internal sealed class InvitePeopleButton : AvaloniaButton
{
    public const string OpenPseudoClass = ":open";
    public const string ReadyPseudoClass = ":ready";

    protected override Type StyleKeyOverride => typeof(InvitePeopleButton);

    /// <summary>Sets one of the button's state pseudo-classes (<see cref="OpenPseudoClass"/>, <see cref="ReadyPseudoClass"/>).</summary>
    public void SetState(string pseudoClass, bool on) => PseudoClasses.Set(pseudoClass, on);
}

/// <summary>The field's text box: no frame (the field draws one), a placeholder, single line.</summary>
internal sealed class InvitePeopleInput : TextBox
{
    public const string HasTextPseudoClass = ":has-text";

    protected override Type StyleKeyOverride => typeof(InvitePeopleInput);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            PseudoClasses.Set(HasTextPseudoClass, !string.IsNullOrEmpty(Text));
        }
    }
}

/// <summary>A line of the role menu (Arc's <c>menuitemradio</c>).</summary>
internal sealed class InvitePeopleMenuItem : AvaloniaButton
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<InvitePeopleMenuItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<InvitePeopleMenuItem, string?>(nameof(Description));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<InvitePeopleMenuItem, bool>(nameof(IsChecked));

    public InvitePeopleMenuItem(InvitePeopleRole role)
    {
        Role = role;
        Title = role.Name;
        Description = role.Description;
        AutomationProperties.SetName(this, role.Name);
    }

    public InvitePeopleRole Role { get; }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    protected override Type StyleKeyOverride => typeof(InvitePeopleMenuItem);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(":checked", IsChecked);
        }
    }
}

/// <summary>
/// The header: the titles and the team avatars side by side (16 apart), or the avatars on a second line 12 below,
/// aligned left, when the titles' natural width and the avatars do not fit (CSS flex-wrap).
/// </summary>
internal sealed class InvitePeopleHeaderPanel : Panel
{
    private const double ColumnGap = 16;
    private const double RowGap = 12;
    private bool _wrapped;

    /// <summary>True when the avatars sit under the titles.</summary>
    public bool IsWrapped => _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }
        var titles = Children[0];
        var stack = Children[1];
        stack.Measure(Size.Infinity);
        var stackSize = stack.IsVisible ? stack.DesiredSize : default;
        titles.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var natural = titles.DesiredSize.Width;
        var width = double.IsInfinity(availableSize.Width) ? natural + ColumnGap + stackSize.Width : availableSize.Width;
        _wrapped = stackSize.Width > 0 && natural + ColumnGap + stackSize.Width > width;
        if (_wrapped)
        {
            titles.Measure(new Size(width, availableSize.Height));
            return new Size(width, titles.DesiredSize.Height + RowGap + stackSize.Height);
        }
        var gap = stackSize.Width > 0 ? ColumnGap : 0;
        titles.Measure(new Size(Math.Max(0, width - gap - stackSize.Width), availableSize.Height));
        return new Size(width, Math.Max(titles.DesiredSize.Height, stackSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }
        var titles = Children[0];
        var stack = Children[1];
        var stackSize = stack.IsVisible ? stack.DesiredSize : default;
        if (_wrapped)
        {
            titles.Arrange(new Rect(0, 0, finalSize.Width, titles.DesiredSize.Height));
            stack.Arrange(new Rect(0, titles.DesiredSize.Height + RowGap, stackSize.Width, stackSize.Height));
            return finalSize;
        }
        var gap = stackSize.Width > 0 ? ColumnGap : 0;
        var titlesWidth = Math.Max(0, finalSize.Width - gap - stackSize.Width);
        titles.Arrange(new Rect(0, (finalSize.Height - titles.DesiredSize.Height) / 2, titlesWidth, titles.DesiredSize.Height));
        stack.Arrange(new Rect(finalSize.Width - stackSize.Width, (finalSize.Height - stackSize.Height) / 2, stackSize.Width, stackSize.Height));
        return finalSize;
    }
}

/// <summary>Portraits: Arc's <see cref="Avatar"/> at any diameter, initials (weight 500, TextSecondary) on the muted surface.</summary>
internal static class InvitePeopleAvatars
{
    public static Avatar Create(double diameter, InvitePeoplePerson? person)
    {
        var avatar = new Avatar
        {
            Width = diameter,
            Height = diameter,
            FontSize = Math.Max(10, Math.Round(diameter * 0.42)),
            FontWeight = FontWeight.Medium,
            BorderThickness = default,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
            IsHitTestVisible = false
        };
        ArcTokens.Bind(avatar, Avatar.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetAccessibilityView(avatar, AccessibilityView.Raw);
        Update(avatar, person);
        return avatar;
    }

    public static void Update(Avatar avatar, InvitePeoplePerson? person)
    {
        var name = person?.Name;
        // An address alone shows its first letter (Arc: "u0@x.io" → "U").
        avatar.DisplayName = !string.IsNullOrWhiteSpace(name) ? name : FirstLetter(person?.Email);
        avatar.Source = person?.Avatar;
    }

    private static string? FirstLetter(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        var trimmed = email.Trim();
        return trimmed.Length > 0 ? trimmed[..1].ToUpperInvariant() : null;
    }
}

/// <summary>
/// The team's overlapping avatars in the header: 28 wide, 8 overlap, each cut out by a 2px surface ring; at most
/// <see cref="MaxVisible"/>, then a "+N" pill. A person added at the front pushes the others right (FLIP on the layout
/// spring); the avatar pushed past the limit shrinks and fades before it leaves.
/// </summary>
internal sealed class InvitePeopleTeamStack : Panel
{
    public const double Diameter = 28;
    public const double Overlap = 8;
    public const double Ring = 2;
    public const int MaxVisible = 5;

    private readonly ProjectsFlip _flip;
    private readonly Dictionary<InvitePeoplePerson, Border> _rings = new();
    private readonly List<Control> _order = new();
    private Border? _more;
    private TextBlock? _moreText;
    private int _moreCount;

    public InvitePeopleTeamStack()
    {
        _flip = new ProjectsFlip(this);
        ClipToBounds = false;
    }

    /// <summary>The avatar of <paramref name="person"/> if it is shown.</summary>
    public Avatar? AvatarOf(InvitePeoplePerson person) =>
        _rings.TryGetValue(person, out var ring) && ring.Child is Avatar avatar ? avatar : null;

    /// <summary>Shows <paramref name="people"/> (front first). <paramref name="animate"/> springs the change.</summary>
    public void SetPeople(IReadOnlyList<InvitePeoplePerson> people, bool animate, Func<int, string> moreText)
    {
        animate &= ArcMotion.IsFull(this);
        if (animate)
        {
            _flip.Arm(MotionSpec.Spring(InvitePeopleMotion.Layout));
        }
        var visible = people.Take(MaxVisible).ToList();
        foreach (var (person, ring) in _rings.ToList())
        {
            if (visible.Contains(person))
            {
                continue;
            }
            _rings.Remove(person);
            Leave(ring, animate);
        }
        _order.Clear();
        foreach (var person in visible)
        {
            if (!_rings.TryGetValue(person, out var ring))
            {
                ring = CreateRing(person);
                _rings[person] = ring;
                Children.Add(ring);
            }
            else if (ring.Child is Avatar avatar)
            {
                InvitePeopleAvatars.Update(avatar, person);
            }
            _order.Add(ring);
        }
        _moreCount = Math.Max(0, people.Count - visible.Count);
        if (_moreCount > 0)
        {
            if (_more is null)
            {
                _moreText = new TextBlock
                {
                    FontWeight = FontWeight.Medium,
                    FontFeatures = FontFeatureCollection.Parse("tnum"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                ArcTokens.Bind(_moreText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                ArcTokens.Bind(_moreText, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
                var pill = new Border { Height = Diameter, Padding = new Thickness(7, 0), Child = _moreText }
                    .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
                ArcTokens.Bind(pill, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
                _more = new Border { Padding = new Thickness(Ring), Child = pill }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
                ArcTokens.Bind(_more, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
                Children.Add(_more);
                if (animate)
                {
                    var motion = _flip.Get(_more);
                    motion.Opacity.Jump(0);
                    _ = motion.Opacity.AnimateTo(1, InvitePeopleMotion.Fade());
                }
            }
            _moreText!.Text = "+" + _moreCount;
            AutomationProperties.SetName(_more, moreText(_moreCount));
            _order.Add(_more);
        }
        else if (_more is not null)
        {
            var more = _more;
            _more = null;
            _moreText = null;
            Leave(more, animate);
        }
        InvalidateMeasure();
    }

    private Border CreateRing(InvitePeoplePerson person)
    {
        var ring = new Border
        {
            Width = Diameter + 2 * Ring,
            Height = Diameter + 2 * Ring,
            Padding = new Thickness(Ring),
            Child = InvitePeopleAvatars.Create(Diameter, person)
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(ring, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        AutomationProperties.SetAccessibilityView(ring, AccessibilityView.Raw);
        return ring;
    }

    private async void Leave(Control child, bool animate)
    {
        var motion = _flip.Get(child);
        motion.IsLeaving = true;
        if (animate)
        {
            _ = motion.Scale.AnimateTo(0.9, MotionSpec.Tween(TimeSpan.FromMilliseconds(320), ArcEasings.Exit));
            await motion.Opacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(320), ArcEasings.Exit));
        }
        _flip.Forget(child);
        Children.Remove(child);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        if (_order.Count == 0)
        {
            return default;
        }
        var step = Diameter - Overlap;
        var width = 0.0;
        for (var i = 0; i < _order.Count; i++)
        {
            var w = _order[i].DesiredSize.Width - 2 * Ring;
            width = Math.Max(width, i * step + w);
        }
        return new Size(width, Diameter);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var step = Diameter - Overlap;
        for (var i = 0; i < _order.Count; i++)
        {
            var child = _order[i];
            var rect = new Rect(i * step - Ring, -Ring, child.DesiredSize.Width, child.DesiredSize.Height);
            _flip.Place(child, rect);
            child.Arrange(rect);
        }
        foreach (var child in Children)
        {
            if (!_order.Contains(child) && _flip.TryGet(child, out var motion) && motion.Layout is { } last)
            {
                child.Arrange(last);
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The seat meter: a 6px pill track with three cumulative segments drawn back to front (new, pending, members). The
/// members and pending segments carry a 2px surface seam at their right edge. Widths follow the smooth spring (code
/// driven: three values from one data change, measured against the control's width); the new segment's color crosses
/// to the danger color in 240ms when the seats run over.
/// </summary>
internal sealed class InvitePeopleSeatBar : Control
{
    private const double Seam = 2;
    private readonly MotionValue<double> _members;
    private readonly MotionValue<double> _pending;
    private readonly MotionValue<double> _new;
    private readonly MotionValue<double> _danger;
    private ImmutableSolidColorBrush? _track;
    private ImmutableSolidColorBrush? _surface;
    private ImmutableSolidColorBrush? _membersBrush;
    private ImmutableSolidColorBrush? _pendingBrush;
    private Color _accent;
    private Color _dangerColor;
    private ImmutableSolidColorBrush? _newBrush;
    private bool _placed;

    public InvitePeopleSeatBar()
    {
        _members = new MotionValue<double>(this, 0, MotionVectors.Double);
        _pending = new MotionValue<double>(this, 0, MotionVectors.Double);
        _new = new MotionValue<double>(this, 0, MotionVectors.Double);
        _danger = new MotionValue<double>(this, 0, MotionVectors.Double);
        _members.Changed += _ => InvalidateVisual();
        _pending.Changed += _ => InvalidateVisual();
        _new.Changed += _ => InvalidateVisual();
        _danger.Changed += _ => InvalidateVisual();
        IsHitTestVisible = false;
        ActualThemeVariantChanged += (_, _) => ClearBrushes();
    }

    /// <summary>The segment fractions being drawn (tests read them).</summary>
    public (double Members, double Pending, double New) Drawn => (_members.Value, _pending.Value, _new.Value);

    /// <summary>The target fractions.</summary>
    public (double Members, double Pending, double New) Target => (_members.Target, _pending.Target, _new.Target);

    public void SetSeats(InvitePeopleSeats seats)
    {
        var animate = _placed && IsAttachedToVisualTree();
        Go(_members, seats.MembersFraction, animate);
        Go(_pending, seats.PendingFraction, animate);
        Go(_new, seats.NewFraction, animate);
        var danger = seats.IsOver ? 1 : 0;
        if (animate)
        {
            _ = _danger.AnimateTo(danger, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        else
        {
            _danger.Jump(danger);
        }
        _placed = true;
    }

    private bool IsAttachedToVisualTree() => VisualRoot is not null;

    private static void Go(MotionValue<double> value, double target, bool animate)
    {
        if (animate)
        {
            _ = value.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            value.Jump(target);
        }
    }

    private void ClearBrushes()
    {
        _track = null;
        _newBrush = null;
        InvalidateVisual();
    }

    private void EnsureBrushes()
    {
        if (_track is not null)
        {
            return;
        }
        _track = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted));
        _surface = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Surface));
        _membersBrush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted));
        _pendingBrush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong));
        _accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        _dangerColor = ArcTokens.GetColor(this, ArcDesignTokenKind.Danger);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        EnsureBrushes();
        var radius = size.Height / 2;
        context.DrawRectangle(_track, null, new RoundedRect(new Rect(size), radius));
        using (context.PushClip(new RoundedRect(new Rect(size), radius)))
        {
            var w = size.Width;
            var newWidth = _new.Value * w;
            var pendingWidth = _pending.Value * w;
            var membersWidth = _members.Value * w;
            if (newWidth > 0.5)
            {
                var color = Lerp(_accent, _dangerColor, Math.Clamp(_danger.Value, 0, 1));
                if (_newBrush is null || _newBrush.Color != color)
                {
                    _newBrush = new ImmutableSolidColorBrush(color);
                }
                context.DrawRectangle(_newBrush, null, new RoundedRect(new Rect(0, 0, newWidth, size.Height), radius));
            }
            if (pendingWidth > 0.5)
            {
                if (newWidth > pendingWidth + 0.5)
                {
                    context.DrawRectangle(_surface, null, new RoundedRect(new Rect(Seam, 0, pendingWidth, size.Height), radius));
                }
                context.DrawRectangle(_pendingBrush, null, new RoundedRect(new Rect(0, 0, pendingWidth, size.Height), radius));
            }
            if (membersWidth > 0.5)
            {
                if (Math.Max(newWidth, pendingWidth) > membersWidth + 0.5)
                {
                    context.DrawRectangle(_surface, null, new RoundedRect(new Rect(Seam, 0, membersWidth, size.Height), radius));
                }
                context.DrawRectangle(_membersBrush, null, new RoundedRect(new Rect(0, 0, membersWidth, size.Height), radius));
            }
        }
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
}

/// <summary>
/// The chips and the text box in one wrapping line set: chips 32 high, 6 apart in both directions, the text box last,
/// at least 143 wide and filling the rest of its line (it moves to a new line when less is left). After
/// <see cref="ArmLayout"/> every child that moved springs from where it was drawn (Arc's layout animation, k 140 c 22);
/// chips that are leaving keep their last place outside the flow while they fade.
/// </summary>
internal sealed class InvitePeopleChipPanel : Panel
{
    public const double ChipHeight = 32;
    public const double Gap = 6;
    public const double InputMinWidth = 143;

    private readonly ProjectsFlip _flip;
    private readonly List<Rect> _rects = new();

    public InvitePeopleChipPanel()
    {
        _flip = new ProjectsFlip(this);
        ClipToBounds = false;
    }

    /// <summary>The motion state of a child (opacity fades for chips that enter or leave).</summary>
    public ProjectsItemMotion MotionOf(Control child) => _flip.Get(child);

    /// <summary>Springs the next layout's moves.</summary>
    public void ArmLayout()
    {
        if (ArcMotion.IsFull(this))
        {
            _flip.Arm(MotionSpec.Spring(InvitePeopleMotion.Layout));
        }
    }

    /// <summary>Fades <paramref name="chip"/> out where it is and removes it.</summary>
    public async void Remove(Control chip)
    {
        var motion = _flip.Get(chip);
        if (motion.IsLeaving)
        {
            return;
        }
        motion.IsLeaving = true;
        InvalidateMeasure();
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && chip.IsEffectivelyVisible)
        {
            if (ArcMotion.IsFull(this))
            {
                _ = motion.Scale.AnimateTo(0.96, InvitePeopleMotion.Fade());
            }
            await motion.Opacity.AnimateTo(0, InvitePeopleMotion.Fade());
        }
        _flip.Forget(chip);
        Children.Remove(chip);
    }

    /// <summary>Fades a new chip in (160ms; Off shows it at once).</summary>
    public void Enter(Control chip)
    {
        var motion = _flip.Get(chip);
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            return;
        }
        motion.Opacity.Jump(0);
        _ = motion.Opacity.AnimateTo(1, InvitePeopleMotion.Fade());
    }

    /// <summary>Drops every child at once (reset).</summary>
    public void Settle()
    {
        foreach (var child in Children.ToList())
        {
            if (_flip.TryGet(child, out var motion) && motion.IsLeaving)
            {
                _flip.Forget(child);
                Children.Remove(child);
            }
        }
        _flip.Settle();
    }

    private bool IsLeaving(Control child) => _flip.TryGet(child, out var motion) && motion.IsLeaving;

    private Size Flow(double width)
    {
        _rects.Clear();
        double x = 0, y = 0;
        var rows = 1;
        foreach (var child in Children)
        {
            if (IsLeaving(child) || !child.IsVisible)
            {
                _rects.Add(default);
                continue;
            }
            if (child is InvitePeopleInput)
            {
                if (x > 0 && x + InputMinWidth > width)
                {
                    x = 0;
                    y += ChipHeight + Gap;
                    rows++;
                }
                _rects.Add(new Rect(x, y, Math.Max(InputMinWidth, width - x), ChipHeight));
                x = width;
                continue;
            }
            var w = Math.Min(child.DesiredSize.Width, width);
            if (x > 0 && x + w > width)
            {
                x = 0;
                y += ChipHeight + Gap;
                rows++;
            }
            _rects.Add(new Rect(x, y, w, ChipHeight));
            x += w + Gap;
        }
        return new Size(width, rows * ChipHeight + (rows - 1) * Gap);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, ChipHeight));
        }
        return Flow(width);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Flow(finalSize.Width);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (IsLeaving(child))
            {
                if (_flip.TryGet(child, out var motion) && motion.Layout is { } last)
                {
                    child.Arrange(last);
                }
                continue;
            }
            var rect = _rects[i];
            _flip.Place(child, rect);
            child.Arrange(rect);
        }
        return finalSize;
    }
}

/// <summary>
/// One chip: an outer holder that the panel moves and fades, around the face (fill, outline, content) that shakes.
/// </summary>
internal sealed class InvitePeopleChip : Decorator
{
    public InvitePeopleChip(InvitePeopleEntry entry)
    {
        Entry = entry;
        Face = new InvitePeopleChipFace { Height = InvitePeopleChipPanel.ChipHeight }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        Child = Face;
    }

    public InvitePeopleEntry Entry { get; }

    public InvitePeopleChipFace Face { get; }

    /// <summary>The bindings of the face's fill and outline (replaced when the chip is marked).</summary>
    public List<IDisposable> LookBindings { get; } = new();

    public Avatar? Avatar { get; set; }

    public InvitePeopleButton? RoleButton { get; set; }

    public ArcContentSwap? RoleText { get; set; }

    public InvitePeopleButton? RemoveButton { get; set; }

    public InvitePeopleButton? EditButton { get; set; }

    public bool IsArmed { get; set; }
}

/// <summary>The visible pill of a chip (its own type so the theme can cross-fade its fill and outline in 160ms).</summary>
internal sealed class InvitePeopleChipFace : Border
{
    protected override Type StyleKeyOverride => typeof(InvitePeopleChipFace);
}

/// <summary>A row of the suggestion list (not focusable: the text box keeps the focus, Arc's <c>aria-activedescendant</c>).</summary>
internal sealed class InvitePeopleSuggestionRow : Border
{
    public InvitePeopleSuggestionRow(InvitePeopleSuggestion suggestion, int index)
    {
        Suggestion = suggestion;
        Index = index;
    }

    public InvitePeopleSuggestion Suggestion { get; }

    public int Index { get; }

    public Control? Avatar { get; set; }

    public TextBlock? Note { get; set; }
}

/// <summary>A row of the pending list.</summary>
internal sealed class InvitePeoplePendingRow : Border
{
    public InvitePeoplePendingRow(InvitePeoplePendingInvite invite)
    {
        Invite = invite;
    }

    public InvitePeoplePendingInvite Invite { get; }

    public Grid Layout { get; set; } = null!;

    public StackPanel Copy { get; set; } = null!;

    public Avatar Avatar { get; set; } = null!;

    public TextBlock NameText { get; set; } = null!;

    public TextBlock Detail { get; set; } = null!;

    public ArcContentSwap Status { get; set; } = null!;

    public InvitePeopleButton Resend { get; set; } = null!;

    public ArcContentSwap ResendText { get; set; } = null!;

    public InvitePeopleButton Revoke { get; set; } = null!;

    public SpringSizeDecorator Slot { get; set; } = null!;

    public bool IsResent { get; set; }

    public bool IsLeaving { get; set; }

    public bool IsNarrow { get; private set; }

    /// <summary>Wide: one 52px line (avatar, name, status, actions). Narrow: the status moves under the name.</summary>
    public void SetNarrow(bool narrow)
    {
        if (IsNarrow == narrow && Status.Parent is not null)
        {
            return;
        }
        IsNarrow = narrow;
        if (Status.Parent is Panel parent)
        {
            parent.Children.Remove(Status);
        }
        if (narrow)
        {
            Status.Margin = new Thickness(0, 2, 0, 0);
            Copy.Children.Add(Status);
        }
        else
        {
            Status.Margin = default;
            Grid.SetColumn(Status, 2);
            Layout.Children.Add(Status);
        }
    }
}

/// <summary>
/// Avatar flights (Arc's shared <c>layoutId</c>: suggestion → chip → pending row → team). The source is gone by the time
/// the target exists, so its box is snapshotted first; a ghost portrait then flies from that box to the target's live box
/// (read every frame, so the target may still be moving) on one spring (k 120, c 22), while the target itself stays
/// hidden. Code-driven because the boxes come from different containers (and the popup layer) at run time.
/// </summary>
internal sealed class InvitePeopleFlightLayer : Canvas
{
    private sealed class Flight
    {
        public required Avatar Ghost { get; init; }
        public required Control Target { get; init; }
        public required Rect From { get; init; }
        public required MotionValue<double> Progress { get; init; }
        public MatrixTransform Transform { get; } = new();
        public IDisposable? Hidden { get; set; }
        public Rect LastTarget { get; set; }
    }

    private readonly List<Flight> _flights = new();

    public InvitePeopleFlightLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>Flights in the air (tests read it).</summary>
    public int Count => _flights.Count;

    /// <summary>The box of <paramref name="visual"/> in this layer's coordinates, or null when it is not in the same tree.</summary>
    public Rect? BoxOf(Visual? visual)
    {
        if (visual is null || visual.Bounds.Width <= 0 || TopLevel.GetTopLevel(visual) is null || TopLevel.GetTopLevel(this) is null)
        {
            return null;
        }
        var origin = visual.TranslatePoint(default, this);
        var corner = visual.TranslatePoint(new Point(visual.Bounds.Width, visual.Bounds.Height), this);
        if (origin is null || corner is null)
        {
            return null;
        }
        return new Rect(origin.Value, corner.Value);
    }

    /// <summary>
    /// Flies a portrait of <paramref name="person"/> from <paramref name="from"/> to <paramref name="target"/>. The target
    /// is hidden until it lands; at reduced or no motion nothing flies and the target shows at once.
    /// </summary>
    public void Fly(Rect? from, Control target, InvitePeoplePerson? person)
    {
        if (from is not { } source || !ArcMotion.IsFull(this) || source.Width <= 0)
        {
            return;
        }
        var hidden = target.SetValue(OpacityProperty, 0d, BindingPriority.Animation);
        void Start()
        {
            if (TopLevel.GetTopLevel(this) is null)
            {
                hidden?.Dispose();
                return;
            }
            var size = target.Bounds.Size.Width > 0 ? target.Bounds.Size : source.Size;
            var ghost = InvitePeopleAvatars.Create(size.Width, person);
            ghost.Height = size.Height;
            var flight = new Flight
            {
                Ghost = ghost,
                Target = target,
                From = source,
                Progress = new MotionValue<double>(this, 0, MotionVectors.Double),
                Hidden = hidden,
                LastTarget = BoxOf(target) ?? source
            };
            ghost.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            ghost.RenderTransform = flight.Transform;
            Children.Add(ghost);
            _flights.Add(flight);
            Place(flight, 0);
            flight.Progress.Changed += p => Place(flight, p);
            _ = Run(flight);
        }
        if (target.Bounds.Width > 0 && TopLevel.GetTopLevel(target) is not null)
        {
            Start();
            return;
        }
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (target.Bounds.Width <= 0 && TopLevel.GetTopLevel(target) is not null)
            {
                return;
            }
            target.LayoutUpdated -= handler;
            Start();
        };
        target.LayoutUpdated += handler;
    }

    private async Task Run(Flight flight)
    {
        await flight.Progress.AnimateTo(1, MotionSpec.Spring(InvitePeopleMotion.Flight));
        Land(flight);
    }

    private void Place(Flight flight, double progress)
    {
        var target = BoxOf(flight.Target) ?? flight.LastTarget;
        flight.LastTarget = target;
        var from = flight.From;
        var x = from.X + (target.X - from.X) * progress;
        var y = from.Y + (target.Y - from.Y) * progress;
        var w = from.Width + (target.Width - from.Width) * progress;
        var size = flight.Ghost.Width > 0 ? flight.Ghost.Width : 1;
        var scale = w / size;
        flight.Transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y);
    }

    private void Land(Flight flight)
    {
        if (!_flights.Remove(flight))
        {
            return;
        }
        flight.Hidden?.Dispose();
        flight.Progress.Dispose();
        Children.Remove(flight.Ghost);
    }

    /// <summary>Ends every flight at once (reset, detach).</summary>
    public void LandAll()
    {
        foreach (var flight in _flights.ToList())
        {
            flight.Progress.Stop();
            Land(flight);
        }
    }
}

/// <summary>
/// The refusal shake: x through [0, 6, −6, 4, −4, 2, 0] in equal steps over 420ms, smoothed between keys. A new chip that
/// cannot be invited shakes the other way first. Code-driven: a one-off key sequence started by a click, which a
/// ControlTheme cannot express. Only at full motion.
/// </summary>
internal static class InvitePeopleShake
{
    private static readonly double[] s_keys = [0, 6, -6, 4, -4, 2, 0];
    private static readonly ConditionalWeakTable<Control, MotionValue<double>> s_running = new();
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(420);

    public static void Run(Control target, double direction = 1)
    {
        if (!ArcMotion.IsFull(target))
        {
            return;
        }
        if (target.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            target.RenderTransform = translate;
        }
        if (s_running.TryGetValue(target, out var previous))
        {
            previous.Stop();
            previous.Dispose();
            s_running.Remove(target);
        }
        var progress = new MotionValue<double>(target, 0, MotionVectors.Double);
        progress.Changed += p => translate.X = direction * Sample(p);
        s_running.Add(target, progress);
        _ = Play(target, progress, translate);
    }

    private static async Task Play(Control target, MotionValue<double> progress, TranslateTransform translate)
    {
        await progress.AnimateTo(1, MotionSpec.Tween(Duration, new LinearEasing()));
        translate.X = 0;
        if (s_running.TryGetValue(target, out var current) && ReferenceEquals(current, progress))
        {
            s_running.Remove(target);
            progress.Dispose();
        }
    }

    /// <summary>The offset at progress <paramref name="p"/> (0..1).</summary>
    public static double Sample(double p)
    {
        p = Math.Clamp(p, 0, 1);
        var segments = s_keys.Length - 1;
        var position = p * segments;
        var index = Math.Min((int)position, segments - 1);
        var u = position - index;
        var eased = u * u * (3 - 2 * u);
        return s_keys[index] + (s_keys[index + 1] - s_keys[index]) * eased;
    }
}

/// <summary>Springs and tweens measured on the invite form (Arc Invite people, design reference; fitted from frame captures).</summary>
internal static class InvitePeopleMotion
{
    /// <summary>Chips, the text box and team avatars moving to new places (k 140, c 22).</summary>
    public static readonly SpringSpec Layout = SpringSpec.FromPhysical(140, 22);

    /// <summary>Avatar flights between the suggestion, the chip, the pending row and the team (k 120, c 22).</summary>
    public static readonly SpringSpec Flight = SpringSpec.FromPhysical(120, 22);

    /// <summary>The suggestion highlight gliding between rows (k 150, c 24).</summary>
    public static readonly SpringSpec Highlight = SpringSpec.FromPhysical(150, 24);

    /// <summary>A chip or the error panel fading in or out (160ms, Arc's standard curve).</summary>
    public static MotionSpec Fade() => MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);

    /// <summary>A pending row fading as it collapses (130ms).</summary>
    public static MotionSpec RowFade() => MotionSpec.Tween(TimeSpan.FromMilliseconds(130), ArcEasings.Standard);
}

/// <summary>A small helper for code-built text in the block's type ramp.</summary>
internal static class InvitePeopleText
{
    public static TextBlock Create(double size, ArcDesignTokenKind color, FontWeight weight = FontWeight.Normal, double lineHeight = double.NaN)
    {
        var text = new TextBlock
        {
            FontSize = size,
            FontWeight = weight,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!double.IsNaN(lineHeight))
        {
            text.LineHeight = lineHeight;
        }
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, color);
        return text;
    }

    public static ArcGlyph Glyph(Geometry data, double size, double stroke = 1.75) => new()
    {
        Data = data,
        Size = size,
        StrokeThickness = stroke,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static Ellipse Dot(double size) => new() { Width = size, Height = size };
}
