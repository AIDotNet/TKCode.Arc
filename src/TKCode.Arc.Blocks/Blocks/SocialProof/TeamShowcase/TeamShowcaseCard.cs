using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One person of a <see cref="TeamShowcase"/>: a square portrait with the bio panel over its lower part, then the name and
/// the role. The bio shows while a mouse or pen is over the card, while it has keyboard focus and while it is pinned
/// (a tap, a click, Enter or Space toggles the pin). The grid moves cards with <see cref="ShiftFrom"/>,
/// <see cref="Enter"/> and <see cref="Leave"/>; all three only write the render transform and the opacity.
/// </summary>
[PseudoClasses(RevealedPseudoClass, PinnedPseudoClass)]
[TemplatePart(RingHostPart, typeof(Panel))]
internal sealed class TeamShowcaseCard : TemplatedControl
{
    public const string RevealedPseudoClass = ":revealed";
    public const string PinnedPseudoClass = ":pinned";
    public const string RingHostPart = "PART_RingHost";

    public static readonly StyledProperty<string?> MemberNameProperty =
        AvaloniaProperty.Register<TeamShowcaseCard, string?>(nameof(MemberName));

    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<TeamShowcaseCard, string?>(nameof(Role));

    public static readonly StyledProperty<string?> BioProperty =
        AvaloniaProperty.Register<TeamShowcaseCard, string?>(nameof(Bio));

    public static readonly StyledProperty<string?> LocationProperty =
        AvaloniaProperty.Register<TeamShowcaseCard, string?>(nameof(Location));

    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<TeamShowcaseCard, int>(nameof(Seed));

    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<TeamShowcaseCard, IImage?>(nameof(Image));

    private readonly TranslateTransform _translate = new();
    private MotionValue<Vector>? _shift;
    private MotionValue<double>? _presence;
    private LazyFocusRing? _ring;
    private bool _fineHover;
    private bool _pinned;
    private int _leaveVersion;

    static TeamShowcaseCard()
    {
        FocusableProperty.OverrideDefaultValue<TeamShowcaseCard>(true);
        ClipToBoundsProperty.OverrideDefaultValue<TeamShowcaseCard>(false);
    }

    public TeamShowcaseCard(TeamShowcaseMember member)
    {
        Member = member;
        MemberName = member.Name;
        Role = member.Role;
        Bio = member.Bio;
        Location = member.Location;
        Image = member.Image;
        Seed = member.Seed ?? TeamShowcasePortrait.SeedOf(member.Name);
        RenderTransform = _translate;
        AutomationProperties.SetName(this, member.Name);
        AutomationProperties.SetHelpText(this, string.Join(". ",
            new[] { member.Role, member.Department, member.Bio, member.Location }.Where(s => !string.IsNullOrWhiteSpace(s))));
    }

    /// <summary>The person this card shows.</summary>
    public TeamShowcaseMember Member { get; }

    public string? MemberName { get => GetValue(MemberNameProperty); set => SetValue(MemberNameProperty, value); }

    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    public string? Bio { get => GetValue(BioProperty); set => SetValue(BioProperty, value); }

    public string? Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <summary>True while the bio panel shows.</summary>
    public bool IsRevealed => PseudoClasses.Contains(RevealedPseudoClass);

    /// <summary>True when a tap, a click or a key pinned the bio open.</summary>
    public bool IsPinned => _pinned;

    /// <summary>True between <see cref="Leave"/> and the end of its fade (the grid keeps the card in its old slot meanwhile).</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>The slot the grid gave the card last time, in the grid's coordinates; null before the first arrange.</summary>
    public Rect? Slot { get; set; }

    /// <summary>Raised when the card is pressed (click, tap, Enter or Space).</summary>
    public event EventHandler? Invoked;

    /// <summary>Pins or unpins the bio.</summary>
    public void SetPinned(bool pinned)
    {
        _pinned = pinned;
        PseudoClasses.Set(PinnedPseudoClass, pinned);
        UpdateRevealed();
    }

    /// <summary>Slides the card from <paramref name="offset"/> (old slot minus new slot) to its new slot.</summary>
    public void ShiftFrom(Vector offset)
    {
        if (!ArcMotion.IsFull(this) || offset == default)
        {
            return;
        }
        _shift ??= CreateShift();
        _shift.Jump(_shift.Value + offset);
        _ = _shift.AnimateTo(default, MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>Fades the card in (with a short rise under full motion) after the filter brought it back.</summary>
    public void Enter(TimeSpan delay)
    {
        _leaveVersion++;
        IsLeaving = false;
        IsVisible = true;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || TopLevel.GetTopLevel(this) is null)
        {
            _presence?.Jump(1);
            ApplyPresence(1);
            return;
        }
        _presence ??= CreatePresence();
        _presence.Jump(0);
        ApplyPresence(0);
        _ = _presence.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
    }

    /// <summary>Fades the card out and hides it; the grid keeps it in place until then.</summary>
    public async void Leave()
    {
        SetPinned(false);
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || TopLevel.GetTopLevel(this) is null)
        {
            IsVisible = false;
            return;
        }
        var version = ++_leaveVersion;
        IsLeaving = true;
        _presence ??= CreatePresence();
        var done = await _presence.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Exit));
        if (done && version == _leaveVersion)
        {
            IsLeaving = false;
            IsVisible = false;
            (Parent as Avalonia.Layout.Layoutable)?.InvalidateArrange();
        }
    }

    /// <summary>Ends every movement at once (the grid re-laid out without animation).</summary>
    public void Settle()
    {
        _shift?.Jump(default);
        ApplyShift(default);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring?.Attach(null, null);
        if (e.NameScope.Find<Panel>(RingHostPart) is { } host)
        {
            _ring = new LazyFocusRing(this, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness),
                ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep24));
            _ring.Attach(ring =>
            {
                ring.Margin = new Thickness(-4);
                host.Children.Add(ring);
            }, ring => host.Children.Remove(ring));
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _ring?.Update();
        UpdateRevealed();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _ring?.Update();
        UpdateRevealed();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _fineHover = true;
            UpdateRevealed();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _fineHover = false;
        UpdateRevealed();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Activate();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Enter or Key.Space)
        {
            Activate();
            e.Handled = true;
        }
        else if (!e.Handled && e.Key == Key.Escape && _pinned)
        {
            SetPinned(false);
            e.Handled = true;
        }
    }

    private void Activate()
    {
        SetPinned(!_pinned);
        Invoked?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateRevealed()
    {
        var focused = IsFocused && Classes.Contains(":focus-visible");
        var revealed = !string.IsNullOrWhiteSpace(Bio) && (_pinned || _fineHover || focused);
        PseudoClasses.Set(RevealedPseudoClass, revealed);
    }

    private MotionValue<Vector> CreateShift()
    {
        var value = new MotionValue<Vector>(this, default, MotionVectors.Vector);
        value.Changed += ApplyShift;
        return value;
    }

    private MotionValue<double> CreatePresence()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += ApplyPresence;
        return value;
    }

    private void ApplyShift(Vector offset)
    {
        var rise = _presence is { } p && ArcMotion.IsFull(this) ? (1 - p.Value) * 10 : 0;
        _translate.X = offset.X;
        _translate.Y = offset.Y + rise;
    }

    private void ApplyPresence(double value)
    {
        Opacity = Math.Clamp(value, 0, 1);
        ApplyShift(_shift?.Value ?? default);
    }
}

/// <summary>A decorator whose height follows its width (the portrait is square at every column width).</summary>
internal sealed class TeamShowcaseSquare : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var edge = double.IsInfinity(availableSize.Width) ? 220 : availableSize.Width;
        Child?.Measure(new Size(edge, edge));
        return new Size(edge, edge);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
