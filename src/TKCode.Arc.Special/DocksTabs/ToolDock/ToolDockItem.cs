using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One entry of a <see cref="ToolDock"/>: a tool or mode with a glyph, a name, an optional key hint and an optional
/// badge. With members in <see cref="Items"/> the entry is a group: its slot shows the member it holds (the selected one,
/// else the one chosen last, else the first) and choosing it opens a tray of the members above the dock.
/// </summary>
/// <remarks>
/// Members are <see cref="ToolDockItem"/>s too; they only use <see cref="Value"/>, <see cref="Label"/>,
/// <see cref="Glyph"/> and <see cref="Shortcut"/>. A disabled entry cannot be chosen and the arrow keys skip it.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(TintPart, typeof(Border))]
[TemplatePart(FacePart, typeof(Border))]
[TemplatePart(ContentPart, typeof(Panel))]
[TemplatePart(GlyphPart, typeof(ToolDockGlyph))]
[PseudoClasses(SelectedPseudoClass, GroupPseudoClass, OpenPseudoClass, HoverPseudoClass, PressedPseudoClass, DraggingPseudoClass)]
public class ToolDockItem : TemplatedControl
{
    internal const string RootPart = "PART_Root";
    internal const string TintPart = "PART_Tint";
    internal const string FacePart = "PART_Face";
    internal const string ContentPart = "PART_Content";
    internal const string GlyphPart = "PART_Glyph";
    internal const string SelectedPseudoClass = ":selected";
    internal const string GroupPseudoClass = ":group";
    internal const string OpenPseudoClass = ":open";
    internal const string HoverPseudoClass = ":hover-fine";
    internal const string PressedPseudoClass = ":pressed";
    internal const string DraggingPseudoClass = ":dragging";

    /// <summary>Press: the slot dips to 92% in 90 ms (CSS <c>ease-out</c>), Arc Dock (design reference).</summary>
    internal const double PressScale = 0.92;

    /// <summary>Drag: the lifted slot grows to 112%.</summary>
    internal const double LiftScale = 1.12;

    private static readonly TimeSpan s_pressDuration = TimeSpan.FromMilliseconds(90);
    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>Release: Arc's 580 ms CSS spring on the dock, a critically damped spring (fitted ω0 13.79).</summary>
    private static readonly SpringSpec s_release = SpringSpec.FromVisual(0.38, 0);

    /// <summary>The selection tint gliding between slots (fitted ω0 15.4, ζ 0.9).</summary>
    internal static readonly SpringSpec TintSpring = SpringSpec.FromVisual(0.34, 0.1);

    /// <summary>What the dock reports when this entry or member is chosen (<see cref="ToolDock.SelectedValue"/>).</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<ToolDockItem, object?>(nameof(Value));

    /// <summary>The name shown in the floating label and read by assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ToolDockItem, string?>(nameof(Label));

    /// <summary>A 24 × 24 stroked glyph. A group's slot shows the glyph of the member it holds.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ToolDockItem, Geometry?>(nameof(Glyph));

    /// <summary>A key hint such as "V", shown in the floating label and added to the accessible name. Display only: the page handles the key.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<ToolDockItem, string?>(nameof(Shortcut));

    /// <summary>A count shown on the slot's corner; its digits roll when it changes and it leaves at 0.</summary>
    public static readonly StyledProperty<int> BadgeProperty =
        AvaloniaProperty.Register<ToolDockItem, int>(nameof(Badge), coerce: static (_, value) => Math.Max(0, value));

    /// <summary>Whether this entry holds the dock's value (for a group: whether one of its members does).</summary>
    public static readonly DirectProperty<ToolDockItem, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<ToolDockItem, bool>(nameof(IsSelected), o => o.IsSelected);

    /// <summary>Whether this group's tray is open.</summary>
    public static readonly DirectProperty<ToolDockItem, bool> IsTrayOpenProperty =
        AvaloniaProperty.RegisterDirect<ToolDockItem, bool>(nameof(IsTrayOpen), o => o.IsTrayOpen);

    /// <summary>For a group, the member its slot shows: the selected member, else the one chosen last, else the first.</summary>
    public static readonly DirectProperty<ToolDockItem, ToolDockItem?> ShownItemProperty =
        AvaloniaProperty.RegisterDirect<ToolDockItem, ToolDockItem?>(nameof(ShownItem), o => o.ShownItem);

    private bool _isSelected;
    private bool _isTrayOpen;
    private ToolDockItem? _shownItem;
    private ToolDockItem? _lastChosen;
    private ToolDockItem? _group;
    private Panel? _root;
    private Border? _tint;
    private Border? _face;
    private Panel? _content;
    private ToolDockGlyph? _glyph;
    private ToolDockBadge? _badge;
    private LazyFocusRing? _focusRing;
    private MotionValue<double>? _press;
    private ScaleTransform? _pressTransform;
    private MotionValue<double>? _lift;
    private MotionValue<double>? _offset;
    private MatrixTransform? _slotTransform;
    private MotionValue<double>? _tintOffset;
    private TranslateTransform? _tintTransform;
    private IPointer? _pressPointer;
    private bool _dragged;

    static ToolDockItem()
    {
        FocusableProperty.OverrideDefaultValue<ToolDockItem>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ToolDockItem>(false);
    }

    /// <summary>Creates an entry.</summary>
    public ToolDockItem()
    {
        Items = new AvaloniaList<ToolDockItem>();
        Items.CollectionChanged += OnItemsChanged;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <inheritdoc cref="BadgeProperty"/>
    public int Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>The members of a group, in order; empty for a plain entry. In XAML, write them as children.</summary>
    [Content]
    public AvaloniaList<ToolDockItem> Items { get; }

    /// <summary>Whether this entry is a group (it has members).</summary>
    public bool IsGroup => Items.Count > 0;

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => _isSelected; private set => SetAndRaise(IsSelectedProperty, ref _isSelected, value); }

    /// <inheritdoc cref="IsTrayOpenProperty"/>
    public bool IsTrayOpen { get => _isTrayOpen; private set => SetAndRaise(IsTrayOpenProperty, ref _isTrayOpen, value); }

    /// <inheritdoc cref="ShownItemProperty"/>
    public ToolDockItem? ShownItem { get => _shownItem; private set => SetAndRaise(ShownItemProperty, ref _shownItem, value); }

    /// <summary>The dock this entry is a slot of (null for members and detached entries).</summary>
    internal ToolDock? Owner => ItemsControl.ItemsControlFromItemContainer(this) as ToolDock;

    /// <summary>The group this entry is a member of.</summary>
    internal ToolDockItem? Group => _group;

    /// <summary>The badge part once a count was shown.</summary>
    internal ToolDockBadge? BadgePart => _badge;

    /// <summary>The glyph part (null before the template is applied).</summary>
    internal ToolDockGlyph? GlyphView => _glyph;

    /// <summary>The slot's current horizontal displacement (drag and reflow), in px.</summary>
    internal double SlotOffset => _offset?.Value ?? 0;

    /// <summary>The slot's current lift scale (1 at rest).</summary>
    internal double SlotLift => _lift?.Value ?? 1;

    /// <summary>The press dip's current scale (1 at rest).</summary>
    internal double PressDip => _press?.Value ?? 1;

    /// <summary>The selection tint's current displacement from this slot, in px.</summary>
    internal double TintOffset => _tintOffset?.Value ?? 0;

    /// <summary>Whether a pointer is pressed on the slot (a drag may start from it).</summary>
    internal bool IsPressed => _pressPointer is not null;

    /// <summary>The name shown in the floating label: the label, or the value's text.</summary>
    internal string DisplayName => Label is { Length: > 0 } label
        ? label
        : Convert.ToString(Value, CultureInfo.CurrentCulture) ?? string.Empty;

    /// <summary>The glyph the slot shows: a group shows its member's.</summary>
    internal Geometry? DisplayGlyph => IsGroup ? (ShownItem?.Glyph ?? Glyph) : Glyph;

    /// <summary>The floating label's text: "Name", or "Group: Member" for a group.</summary>
    internal string LabelText => IsGroup && ShownItem is { } shown ? $"{DisplayName}: {shown.DisplayName}" : DisplayName;

    /// <summary>The key hint in the floating label (a group shows its member's).</summary>
    internal string? LabelShortcut => IsGroup ? ShownItem?.Shortcut : Shortcut;

    /// <summary>
    /// The accessible name, for example "Pen, P" or "Comment, C, 2"; a group joins its label and visible member
    /// ("Shapes: Circle, O") and appends ", selected" while one of its members is the value.
    /// </summary>
    internal string AccessibleName
    {
        get
        {
            var name = new StringBuilder(LabelText);
            if (LabelShortcut is { Length: > 0 } shortcut)
            {
                name.Append(", ").Append(shortcut);
            }
            if (Badge > 0 && _group is null)
            {
                name.Append(", ").Append(Badge.ToString(CultureInfo.CurrentCulture));
            }
            if (IsGroup && IsSelected)
            {
                name.Append(", selected");
            }
            return name.ToString();
        }
    }

    /// <summary>Whether a press on this entry may start a drag (it is a slot and the dock reorders).</summary>
    internal bool IsDragged => _dragged;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Panel>(RootPart);
        _tint = e.NameScope.Find<Border>(TintPart);
        _face = e.NameScope.Find<Border>(FacePart);
        _content = e.NameScope.Find<Panel>(ContentPart);
        _glyph = e.NameScope.Find<ToolDockGlyph>(GlyphPart);
        _glyph?.Show(DisplayGlyph, animate: false);
        if (_pressTransform is not null && _face is not null)
        {
            _face.RenderTransform = _pressTransform;
        }
        if (_tintTransform is not null && _tint is not null)
        {
            _tint.RenderTransform = _tintTransform;
        }
        if (_badge is not null)
        {
            (_badge.Parent as Panel)?.Children.Remove(_badge);
            _content?.Children.Add(_badge);
        }
        else
        {
            UpdateBadge(animate: false);
        }
        _focusRing ??= new LazyFocusRing(this, ToolDockTokens.Key(ToolDockTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep18));
        _focusRing.Attach(_root);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty)
        {
            if (!IsGroup)
            {
                _glyph?.Show(DisplayGlyph, animate: false);
            }
            _group?.OnMemberChanged(this);
        }
        else if (change.Property == BadgeProperty)
        {
            UpdateBadge(animate: true);
        }
        else if (change.Property == ValueProperty)
        {
            if (_group is { } group)
            {
                group.OnMemberChanged(this);
                group.Owner?.OnItemsStateChanged();
            }
            else
            {
                Owner?.OnItemsStateChanged();
            }
        }
        else if (change.Property == LabelProperty || change.Property == ShortcutProperty)
        {
            _group?.OnMemberChanged(this);
            Owner?.OnItemTextChanged(this);
        }
        else if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
        }
        else if (change.Property == IsTrayOpenProperty)
        {
            PseudoClasses.Set(OpenPseudoClass, IsTrayOpen);
        }
        else if (change.Property == IsEnabledProperty && !IsEffectivelyEnabled)
        {
            EndPress(animate: false);
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ToolDockItem member in e.OldItems)
            {
                if (ReferenceEquals(member._group, this))
                {
                    member._group = null;
                    LogicalChildren.Remove(member);
                }
            }
        }
        if (e.NewItems is not null)
        {
            foreach (ToolDockItem member in e.NewItems)
            {
                member._group = this;
                LogicalChildren.Add(member);
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var child in LogicalChildren.OfType<ToolDockItem>().ToArray())
            {
                if (!Items.Contains(child))
                {
                    child._group = null;
                    LogicalChildren.Remove(child);
                }
            }
        }
        if (_lastChosen is not null && !Items.Contains(_lastChosen))
        {
            _lastChosen = null;
        }
        PseudoClasses.Set(GroupPseudoClass, IsGroup);
        Owner?.OnItemsStateChanged();
        UpdateShown(animate: false);
    }

    private void OnMemberChanged(ToolDockItem member)
    {
        if (ReferenceEquals(member, ShownItem))
        {
            _glyph?.Show(DisplayGlyph, animate: false);
            Owner?.OnItemTextChanged(this);
        }
        Owner?.OnMemberChanged(this, member);
    }

    /// <summary>Updates the selection state from the dock's value (a group also picks the member it shows).</summary>
    internal void Sync(object? value, bool animate)
    {
        if (IsGroup)
        {
            var holder = FindMember(value);
            if (holder is not null)
            {
                _lastChosen = holder;
            }
            IsSelected = holder is not null;
            UpdateShown(animate);
        }
        else
        {
            IsSelected = value is not null && Equals(Value, value);
        }
    }

    /// <summary>The member whose value is <paramref name="value"/>, or null.</summary>
    internal ToolDockItem? FindMember(object? value)
    {
        if (value is null)
        {
            return null;
        }
        foreach (var member in Items)
        {
            if (Equals(member.Value, value))
            {
                return member;
            }
        }
        return null;
    }

    /// <summary>Remembers the member chosen last (shown when the group does not hold the value).</summary>
    internal void Remember(ToolDockItem member) => _lastChosen = member;

    private void UpdateShown(bool animate)
    {
        var shown = IsGroup ? (_lastChosen is not null && Items.Contains(_lastChosen) ? _lastChosen : Items[0]) : null;
        if (ReferenceEquals(shown, ShownItem))
        {
            return;
        }
        ShownItem = shown;
        _glyph?.Show(DisplayGlyph, animate);
        Owner?.OnItemTextChanged(this);
    }

    internal void SetTrayOpen(bool open) => IsTrayOpen = open;

    // ---- Pointer -------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        PseudoClasses.Set(HoverPseudoClass, true);
        Owner?.OnHover(this, true);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
        Owner?.OnHover(this, false);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEffectivelyEnabled || Owner is null)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressPointer = e.Pointer;
        _dragged = false;
        PseudoClasses.Set(PressedPseudoClass, true);
        AnimatePress(PressScale);
        if (e.Pointer.Type != PointerType.Touch)
        {
            // A mouse press focuses the slot without a ring, as a button would; touch leaves the focus alone so a swipe
            // across the dock can still scroll the page.
            Focus(NavigationMethod.Pointer);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!ReferenceEquals(e.Pointer, _pressPointer))
        {
            return;
        }
        var dragged = _dragged;
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        EndPress(animate: true);
        if (!dragged && inside && IsEffectivelyEnabled)
        {
            e.Handled = true;
            Owner?.OnItemClicked(this, e.Pointer.Type);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!ReferenceEquals(e.Pointer, _pressPointer) || _dragged)
        {
            return;
        }
        // A drag takes the capture over to this slot (from the part under the press); the press then ends with the drop.
        if (e.Pointer.Captured is Visual captured && (ReferenceEquals(captured, this) || this.IsVisualAncestorOf(captured)))
        {
            return;
        }
        EndPress(animate: true);
    }

    /// <summary>A drag took over the press: the release will not choose the slot, and the slot lifts.</summary>
    internal void BeginDrag()
    {
        _dragged = true;
        PseudoClasses.Set(DraggingPseudoClass, true);
        ZIndex = 1;
        AnimateLift(LiftScale);
    }

    /// <summary>The drag ended: the slot shrinks back and settles into its place, then drops back to its layer.</summary>
    internal async void EndDrag()
    {
        PseudoClasses.Set(DraggingPseudoClass, false);
        EndPress(animate: true);
        _dragged = false;
        AnimateLift(1);
        await AnimateOffset(0, ArcMotionPresets.Smooth);
        if (!_dragged)
        {
            ZIndex = 0;
        }
    }

    internal void EndPress(bool animate)
    {
        _pressPointer = null;
        PseudoClasses.Set(PressedPseudoClass, false);
        if (animate)
        {
            AnimatePress(1);
        }
        else
        {
            _press?.Jump(1);
        }
    }

    // ---- Motion --------------------------------------------------------------------------------------------------

    private void AnimatePress(double scale)
    {
        if (_press is null)
        {
            if (Math.Abs(scale - 1) < 1e-9)
            {
                return;
            }
            _pressTransform = new ScaleTransform();
            if (_face is not null)
            {
                _face.RenderTransform = _pressTransform;
            }
            _press = new MotionValue<double>(this, 1, MotionVectors.Double);
            _press.Changed += value =>
            {
                _pressTransform!.ScaleX = value;
                _pressTransform.ScaleY = value;
            };
        }
        // Press and release have different curves (90 ms ease-out down, a 580 ms spring back up) and a release must start
        // from wherever the dip is, so the dip runs in code; a ControlTheme transition has one curve for both directions.
        if (!ArcMotion.IsFull(this))
        {
            _press.Jump(1);
            return;
        }
        if (scale < 1)
        {
            _press.AnimateTo(scale, MotionSpec.Tween(s_pressDuration, EaseOut));
        }
        else
        {
            _press.AnimateTo(scale, MotionSpec.Spring(s_release));
        }
    }

    private void EnsureSlotMotion()
    {
        if (_lift is not null)
        {
            return;
        }
        _slotTransform = new MatrixTransform();
        RenderTransform = _slotTransform;
        _lift = new MotionValue<double>(this, 1, MotionVectors.Double);
        _offset = new MotionValue<double>(this, 0, MotionVectors.Double);
        _lift.Changed += _ => ApplySlotTransform();
        _offset.Changed += _ => ApplySlotTransform();
    }

    private void ApplySlotTransform()
    {
        var scale = _lift!.Value;
        _slotTransform!.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(_offset!.Value, 0);
    }

    private void AnimateLift(double scale)
    {
        EnsureSlotMotion();
        if (!ArcMotion.IsFull(this))
        {
            _lift!.Jump(1);
            return;
        }
        _lift!.AnimateTo(scale, MotionSpec.Spring(ArcMotionPresets.Smooth));
    }

    /// <summary>Moves the slot by <paramref name="offset"/> at once (it follows the pointer while dragged).</summary>
    internal void JumpOffset(double offset)
    {
        EnsureSlotMotion();
        _offset!.Jump(offset);
    }

    /// <summary>Springs the slot's displacement to <paramref name="target"/> (reflow after a move, a drop).</summary>
    internal Task<bool> AnimateOffset(double target, SpringSpec spring)
    {
        EnsureSlotMotion();
        if (!ArcMotion.IsFull(this))
        {
            _offset!.Jump(target);
            return Task.FromResult(true);
        }
        return _offset!.AnimateTo(target, MotionSpec.Spring(spring));
    }

    /// <summary>
    /// Starts the selection tint <paramref name="from"/> px away from this slot (where the previous slot's tint was) and
    /// springs it home, so the tint reads as one shape that slides; the slots are the same size, so it never stretches.
    /// </summary>
    internal void GlideTintFrom(double from, double velocity)
    {
        if (_tintOffset is null)
        {
            if (Math.Abs(from) < 0.5)
            {
                return;
            }
            _tintTransform = new TranslateTransform();
            if (_tint is not null)
            {
                _tint.RenderTransform = _tintTransform;
            }
            _tintOffset = new MotionValue<double>(this, 0, MotionVectors.Double);
            _tintOffset.Changed += value => _tintTransform!.X = value;
        }
        _tintOffset.Jump(from);
        if (Math.Abs(from) < 0.5)
        {
            _tintOffset.Jump(0);
            return;
        }
        _tintOffset.AnimateTo(0, MotionSpec.Spring(TintSpring), velocity);
    }

    /// <summary>The tint's current displacement velocity, in px/s (handed over to the next slot on a quick change).</summary>
    internal double TintVelocity => _tintOffset is { IsAnimating: true } tint ? tint.Velocity : 0;

    private void UpdateBadge(bool animate)
    {
        if (_group is not null)
        {
            return;
        }
        var count = Badge;
        if (_badge is null)
        {
            if (count <= 0 || _content is null)
            {
                return;
            }
            _badge = new ToolDockBadge { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            _content.Children.Add(_badge);
            animate = false;
        }
        _badge.SetCount(count, animate && IsAttachedToVisualTree());
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    // ---- Focus ---------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
        Owner?.OnItemFocused(this, e.NavigationMethod);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        Owner?.OnFocusLeaving();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolDockItemAutomationPeer(this);

    /// <summary>A button: plain entries report whether they are pressed (selected), groups whether their tray is open.</summary>
    private sealed class ToolDockItemAutomationPeer(ToolDockItem owner)
        : ControlAutomationPeer(owner), IToggleProvider, IExpandCollapseProvider, IInvokeProvider
    {
        private ToolDockItem Item => (ToolDockItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore()
            => AutomationProperties.GetName(Item) is { Length: > 0 } name ? name : Item.AccessibleName;

        public ToggleState ToggleState => Item.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public ExpandCollapseState ExpandCollapseState => Item.IsTrayOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Item.IsTrayOpen)
            {
                Item.Owner?.OpenTray(Item, focusMember: false);
            }
        }

        public void Collapse()
        {
            if (Item.IsTrayOpen)
            {
                Item.Owner?.CloseTray();
            }
        }

        public void Invoke() => Item.Owner?.Choose(Item, keyboard: false);

        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(IToggleProvider))
            {
                return Item.IsGroup ? null : this;
            }
            if (providerType == typeof(IExpandCollapseProvider))
            {
                return Item.IsGroup ? this : null;
            }
            return base.GetProviderCore(providerType);
        }
    }
}
