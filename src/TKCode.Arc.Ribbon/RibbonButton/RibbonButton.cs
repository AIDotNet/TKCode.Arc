using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A command of a <see cref="RibbonGroup"/>: an icon and a label laid out by <see cref="RibbonItemSize"/>. Large stacks a 24px
/// icon over the label and takes the full height of the group; Medium puts a 16px icon beside the label; Small shows the icon
/// alone. <see cref="Size"/> is the size the button asks for and <see cref="MinSize"/> the smallest it accepts: when the
/// ribbon gets narrow its groups step down from Size toward MinSize (and finally collapse into a drop-down), and
/// <see cref="EffectiveSize"/> says what the button shows now.
/// <para>
/// The button runs <see cref="AvaloniaButton.Command"/> and raises <see cref="AvaloniaButton.Click"/> like any button, and the
/// ribbon reports it in <see cref="Ribbon.ItemInvoked"/>. The tooltip shows the label, <see cref="Shortcut"/> and
/// <see cref="Description"/> (set <c>arc:Tooltip.Tip</c> to replace it). A button with a <see cref="QuickAccessKey"/> can be
/// added to a <see cref="QuickAccessToolBar"/> from its context menu.
/// </para>
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(LabelPart, typeof(TextBlock))]
[PseudoClasses(LargePseudoClass, MediumPseudoClass, SmallPseudoClass, HasIconPseudoClass, DropDownPseudoClass,
    SplitPseudoClass, CheckedPseudoClass, OpenPseudoClass)]
public class RibbonButton : AvaloniaButton
{
    public const string FramePart = "PART_Frame";
    public const string LabelPart = "PART_Label";

    /// <summary><see cref="EffectiveSize"/> Large.</summary>
    public const string LargePseudoClass = ":large";

    /// <summary><see cref="EffectiveSize"/> Medium.</summary>
    public const string MediumPseudoClass = ":medium";

    /// <summary><see cref="EffectiveSize"/> Small.</summary>
    public const string SmallPseudoClass = ":small";

    /// <summary>The button has an <see cref="Icon"/>.</summary>
    public const string HasIconPseudoClass = ":has-icon";

    /// <summary>The button opens a menu (<see cref="RibbonDropDownButton"/>, <see cref="RibbonSplitButton"/>): it shows a chevron.</summary>
    public const string DropDownPseudoClass = ":dropdown";

    /// <summary>The button is a <see cref="RibbonSplitButton"/>.</summary>
    public const string SplitPseudoClass = ":split";

    /// <summary>A <see cref="RibbonToggleButton"/> that is on.</summary>
    public const string CheckedPseudoClass = ":checked";

    /// <summary>The button's menu is open.</summary>
    public const string OpenPseudoClass = ":open";

    #region 公共属性定义

    /// <summary>The command's name: shown beside or under the icon, and as the tooltip title. Also the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RibbonButton, string?>(nameof(Label));

    /// <summary>The icon: a 24x24 Lucide <see cref="Geometry"/> (such as <c>{x:Static arc:ArcIcons.Copy}</c>, drawn as a stroke glyph) or any control.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RibbonButton, object?>(nameof(Icon));

    /// <summary>The size the button asks for when there is room: Large (default), Medium or Small.</summary>
    public static readonly StyledProperty<RibbonItemSize> SizeProperty =
        AvaloniaProperty.Register<RibbonButton, RibbonItemSize>(nameof(Size), RibbonItemSize.Large);

    /// <summary>The smallest size the button takes when its group shrinks (default Small). A value above <see cref="Size"/> counts as Size.</summary>
    public static readonly StyledProperty<RibbonItemSize> MinSizeProperty =
        AvaloniaProperty.Register<RibbonButton, RibbonItemSize>(nameof(MinSize), RibbonItemSize.Small);

    /// <summary>The size the button shows now: <see cref="Size"/>, made smaller by its group when the ribbon is narrow.</summary>
    public static readonly DirectProperty<RibbonButton, RibbonItemSize> EffectiveSizeProperty =
        AvaloniaProperty.RegisterDirect<RibbonButton, RibbonItemSize>(nameof(EffectiveSize), o => o.EffectiveSize);

    /// <summary>A sentence for the tooltip that says what the command does.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RibbonButton, string?>(nameof(Description));

    /// <summary>The keyboard shortcut shown in the tooltip, such as <c>Ctrl+C</c> (display only; bind the key yourself).</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<RibbonButton, string?>(nameof(Shortcut));

    /// <summary>
    /// A key, unique in the ribbon, that lets a <see cref="QuickAccessToolBar"/> show this command (its
    /// <see cref="QuickAccessToolBar.Keys"/> list these keys). Null (default): the command cannot be added.
    /// </summary>
    public static readonly StyledProperty<string?> QuickAccessKeyProperty =
        AvaloniaProperty.Register<RibbonButton, string?>(nameof(QuickAccessKey));

    internal static readonly DirectProperty<RibbonButton, Geometry?> IconGeometryProperty =
        AvaloniaProperty.RegisterDirect<RibbonButton, Geometry?>(nameof(IconGeometry), o => o.IconGeometry);

    internal static readonly DirectProperty<RibbonButton, object?> IconContentProperty =
        AvaloniaProperty.RegisterDirect<RibbonButton, object?>(nameof(IconContent), o => o.IconContent);

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public RibbonItemSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="MinSizeProperty"/>
    public RibbonItemSize MinSize
    {
        get => GetValue(MinSizeProperty);
        set => SetValue(MinSizeProperty, value);
    }

    /// <inheritdoc cref="EffectiveSizeProperty"/>
    public RibbonItemSize EffectiveSize
    {
        get => _effectiveSize;
        private set => SetAndRaise(EffectiveSizeProperty, ref _effectiveSize, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut
    {
        get => GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    /// <inheritdoc cref="QuickAccessKeyProperty"/>
    public string? QuickAccessKey
    {
        get => GetValue(QuickAccessKeyProperty);
        set => SetValue(QuickAccessKeyProperty, value);
    }

    internal Geometry? IconGeometry
    {
        get => _iconGeometry;
        private set => SetAndRaise(IconGeometryProperty, ref _iconGeometry, value);
    }

    internal object? IconContent
    {
        get => _iconContent;
        private set => SetAndRaise(IconContentProperty, ref _iconContent, value);
    }

    #endregion

    private RibbonItemSize _effectiveSize = RibbonItemSize.Large;
    private RibbonItemSize? _cap;
    private Geometry? _iconGeometry;
    private object? _iconContent;
    private LazyFocusRing? _focusRing;
    private object? _ownTip;
    private bool _tipDirty = true;

    // Natural size of the button at each RibbonItemSize (indexed by the enum), measured by switching the size classes and
    // measuring with no constraint. A group lays its commands out from these numbers instead of trying sizes on the live tree.
    private readonly Size[] _naturalSizes = new Size[3];
    private bool _naturalSizesDirty = true;
    private bool _probing;
    private int _staleRequests;

    static RibbonButton()
    {
        FocusableProperty.OverrideDefaultValue<RibbonButton>(true);
        ClipToBoundsProperty.OverrideDefaultValue<RibbonButton>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<RibbonButton>(HorizontalAlignment.Left);
    }

    public RibbonButton()
    {
        UpdateEffectiveSize();
        UpdatePseudoClasses();
    }

    // The derived buttons share this theme; their differences are pseudo-classes.
    protected override Type StyleKeyOverride => typeof(RibbonButton);

    /// <summary>The ribbon this button belongs to (through its group, or through the quick access toolbar it sits on).</summary>
    internal Ribbon? OwnerRibbon =>
        this.FindLogicalAncestorOfType<Ribbon>() ?? this.FindLogicalAncestorOfType<QuickAccessToolBar>()?.Ribbon;

    /// <summary>Whether the button shows a chevron (drop-down and split buttons, and proxies of them).</summary>
    private protected virtual bool HasDropDown => false;

    /// <summary>
    /// Limits <see cref="EffectiveSize"/> to <paramref name="cap"/> (the group's current step, or Small on a quick access
    /// toolbar); null lets the button show <see cref="Size"/>.
    /// </summary>
    internal void SetSizeCap(RibbonItemSize? cap)
    {
        if (_cap == cap)
        {
            return;
        }
        _cap = cap;
        UpdateEffectiveSize();
    }

    /// <summary>The size the button would show under <paramref name="cap"/>.</summary>
    internal RibbonItemSize SizeUnder(RibbonItemSize? cap)
    {
        var size = Size;
        var min = MinSize > size ? size : MinSize;
        if (cap is { } c && c < size)
        {
            size = c < min ? min : c;
        }
        return size;
    }

    /// <summary>The button's natural size at <paramref name="size"/> (measured once, again after a change of its content).</summary>
    internal Size GetNaturalSize(RibbonItemSize size)
    {
        if (_naturalSizesDirty && TopLevel.GetTopLevel(this) is not null)
        {
            ProbeNaturalSizes();
        }
        return _naturalSizes[(int)size];
    }

    internal bool IsProbing => _probing;

    private void ProbeNaturalSizes()
    {
        var current = EffectiveSize;
        _probing = true;
        try
        {
            for (var i = 0; i < _naturalSizes.Length; i++)
            {
                ApplySizeClasses((RibbonItemSize)i);
                InvalidateMeasureDeep(this);
                Measure(Avalonia.Size.Infinity);
                _naturalSizes[i] = DesiredSize;
            }
        }
        finally
        {
            ApplySizeClasses(current);
            InvalidateMeasureDeep(this);
            _probing = false;
            _naturalSizesDirty = false;
        }
    }

    // A style change deep in the template only invalidates the element it touched; measuring the button with the same
    // constraint would return the cached size, so every element on the way is invalidated.
    private static void InvalidateMeasureDeep(Visual visual)
    {
        if (visual is Layoutable layoutable)
        {
            layoutable.InvalidateMeasure();
        }
        foreach (var child in visual.GetVisualChildren())
        {
            InvalidateMeasureDeep(child);
        }
    }

    private void UpdateEffectiveSize()
    {
        var size = SizeUnder(_cap);
        if (size == EffectiveSize && PseudoClasses.Contains(SizeClass(size)))
        {
            return;
        }
        EffectiveSize = size;
        if (!_probing)
        {
            ApplySizeClasses(size);
            // The size classes restyle parts deep in the template; measure them all again.
            InvalidateMeasureDeep(this);
        }
        _tipDirty = true;
    }

    private static string SizeClass(RibbonItemSize size) => size switch
    {
        RibbonItemSize.Large => LargePseudoClass,
        RibbonItemSize.Medium => MediumPseudoClass,
        _ => SmallPseudoClass
    };

    private void ApplySizeClasses(RibbonItemSize size)
    {
        PseudoClasses.Set(LargePseudoClass, size == RibbonItemSize.Large);
        PseudoClasses.Set(MediumPseudoClass, size == RibbonItemSize.Medium);
        PseudoClasses.Set(SmallPseudoClass, size == RibbonItemSize.Small);
    }

    private protected void UpdatePseudoClasses()
    {
        PseudoClasses.Set(HasIconPseudoClass, Icon is not null);
        PseudoClasses.Set(DropDownPseudoClass, HasDropDown);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty || change.Property == MinSizeProperty)
        {
            UpdateEffectiveSize();
            RequestGroupLayout();
        }
        else if (change.Property == IconProperty)
        {
            var icon = change.GetNewValue<object?>();
            IconGeometry = icon as Geometry;
            IconContent = icon is Geometry ? null : icon;
            UpdatePseudoClasses();
            MarkNaturalSizesDirty();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
            _tipDirty = true;
            MarkNaturalSizesDirty();
        }
        else if (change.Property == DescriptionProperty || change.Property == ShortcutProperty)
        {
            _tipDirty = true;
        }
        else if (change.Property == QuickAccessKeyProperty)
        {
            OwnerRibbon?.OnQuickAccessCatalogChanged();
        }
    }

    private void MarkNaturalSizesDirty()
    {
        if (_probing)
        {
            return;
        }
        _naturalSizesDirty = true;
        RequestGroupLayout();
    }

    /// <summary>Asks the group (and the row of groups) to lay out again after a change of this button.</summary>
    private void RequestGroupLayout()
    {
        for (Visual? visual = this.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Layoutable layoutable)
            {
                layoutable.InvalidateMeasure();
            }
            if (visual is RibbonGroupsPanel)
            {
                break;
            }
        }
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _naturalSizesDirty = true;
        _focusRing ??= new LazyFocusRing(this, RibbonButtonTokens.Key(RibbonButtonTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep10));
        _focusRing.Attach(AddRing, RemoveRing);
    }

    private void AddRing(Control ring)
    {
        LogicalChildren.Add(ring);
        VisualChildren.Add(ring);
        InvalidateMeasure();
    }

    private void RemoveRing(Control ring)
    {
        VisualChildren.Remove(ring);
        LogicalChildren.Remove(ring);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        _focusRing?.Ring?.Measure(availableSize);
        return size;
    }

    // Compared after MinWidth, margins and rounding (what the probe recorded), not in MeasureOverride.
    protected override Size MeasureCore(Size availableSize)
    {
        var size = base.MeasureCore(availableSize);
        if (!_probing && !_naturalSizesDirty && this.GetVisualParent() is RibbonGroupItemsPanel)
        {
            if (Math.Abs(_naturalSizes[(int)EffectiveSize].Width - size.Width) > 0.5)
            {
                // The content changed in a way the properties above do not see (a token, a font that finished loading):
                // measure the other sizes again and let the row of groups decide again, after this layout pass. A probe that
                // keeps disagreeing with the real measure must not loop, so this gives up after a few tries.
                if (_staleRequests++ < 3)
                {
                    _naturalSizesDirty = true;
                    Dispatcher.UIThread.Post(RequestGroupLayout, DispatcherPriority.Background);
                }
            }
            else
            {
                _staleRequests = 0;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _focusRing?.Ring?.Arrange(new Rect(size));
        return size;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
        EnsureToolTip();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        EnsureToolTip();
    }

    /// <summary>Builds the tooltip (label, shortcut, description) the first time it can show, and after a change of them.</summary>
    private void EnsureToolTip()
    {
        if (!_tipDirty)
        {
            return;
        }
        var current = Tooltip.GetTip(this);
        if (current is not null && !ReferenceEquals(current, _ownTip))
        {
            // The app set its own tooltip.
            _tipDirty = false;
            return;
        }
        _tipDirty = false;
        _ownTip = RibbonToolTip.Create(Label, Shortcut, Description, labelShown: EffectiveSize != RibbonItemSize.Small);
        Tooltip.SetTip(this, _ownTip);
    }

    protected override void OnClick() => InvokeCommand(null);

    /// <summary>Runs the command and raises <see cref="AvaloniaButton.Click"/>, then reports the command to the ribbon.</summary>
    private protected void InvokeCommand(MenuEntry? entry)
    {
        base.OnClick();
        ReportInvoked(entry);
    }

    /// <summary>Runs the command and raises <see cref="AvaloniaButton.Click"/> without reporting to the ribbon (the ribbon's own buttons).</summary>
    private protected void InvokeWithoutReport() => base.OnClick();

    /// <summary>Raises <see cref="Ribbon.ItemInvoked"/> for this button on its ribbon.</summary>
    private protected void ReportInvoked(MenuEntry? entry) => OwnerRibbon?.RaiseItemInvoked(this, entry);

    /// <summary>
    /// What a click on a quick access toolbar copy of this button does (<paramref name="anchor"/> is that copy): the same as
    /// a click here, with menus opening at the copy.
    /// </summary>
    internal virtual void InvokeFromQuickAccess(Control anchor) => OnClick();
}
