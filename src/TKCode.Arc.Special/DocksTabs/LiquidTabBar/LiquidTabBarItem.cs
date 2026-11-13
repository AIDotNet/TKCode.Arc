using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaSelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;

namespace TKCode.Arc;

/// <summary>
/// One tab of a <see cref="LiquidTabBar"/>: a glyph, a label shown next to it when the tab is open, and an optional badge
/// count. A tab is 44px wide with its glyph alone and opens to fit its label; a tab without a glyph is a text tab that
/// always shows its label. A disabled tab is skipped by the arrow keys and cannot be selected.
/// </summary>
/// <remarks>
/// The tab draws itself (glyph, label, badge); the bar's lens draws the same tab again, inverted, wherever it covers it.
/// With <see cref="ItemsControl.ItemsSource"/>, set the tab properties with a style on <c>LiquidTabBarItem</c> bound to
/// the data item, or let the label fall back to the item's text.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, HoverPseudoClass)]
public class LiquidTabBarItem : ContentControl, ISelectable
{
    internal const string RootPart = "PART_Root";
    internal const string SelectedPseudoClass = ":selected";
    internal const string PressedPseudoClass = ":pressed";
    internal const string HoverPseudoClass = ":hover";

    /// <summary>Whether this tab is the selected one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaSelectingItemsControl.IsSelectedProperty.AddOwner<LiquidTabBarItem>();

    /// <summary>The value the bar reports as its <c>SelectedValue</c> while this tab is selected.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<LiquidTabBarItem, object?>(nameof(Value));

    /// <summary>
    /// The tab's name: shown next to the glyph while the tab is open and read by screen readers. When null, the text of
    /// <see cref="ContentControl.Content"/> or of <see cref="Value"/> is used.
    /// </summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<LiquidTabBarItem, string?>(nameof(Label));

    /// <summary>A 24 × 24 stroked glyph (for example <c>ArcIcons.House</c>). Null makes a text tab that always shows its label.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<LiquidTabBarItem, Geometry?>(nameof(Glyph));

    /// <summary>The glyph drawn inside the lens, filled in; null fills in <see cref="Glyph"/>.</summary>
    public static readonly StyledProperty<Geometry?> ActiveGlyphProperty =
        AvaloniaProperty.Register<LiquidTabBarItem, Geometry?>(nameof(ActiveGlyph));

    /// <summary>A count shown on the glyph's corner; its digits roll when it changes. 0 hides the badge.</summary>
    public static readonly StyledProperty<int> BadgeProperty =
        AvaloniaProperty.Register<LiquidTabBarItem, int>(nameof(Badge), coerce: (_, v) => Math.Max(0, v));

    /// <summary>What screen readers hear for the badge, after the label (for example "3 unread"); null reads the number.</summary>
    public static readonly StyledProperty<string?> BadgeLabelProperty =
        AvaloniaProperty.Register<LiquidTabBarItem, string?>(nameof(BadgeLabel));

    private LazyFocusRing? _focusRing;
    private MotionValue<double>? _open;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _roll;
    private MotionValue<double>? _presence;
    private double _openness;
    private double _labelFade = 1;
    private double _badgePresence;
    private readonly LiquidTabBarBadgeRoll _badge = new();
    private bool _pressedInside;

    // Label text, measured once per text or font change; drawn with a cached layout per brush.
    private string _labelText = string.Empty;
    private double _labelWidth;
    private bool _labelDirty = true;
    private TextLayout? _labelLayout;
    private IBrush? _labelBrush;
    private TextLayout? _lensLabelLayout;
    private IBrush? _lensLabelBrush;

    private Pen? _glyphPen;
    private IBrush? _glyphPenBrush;

    static LiquidTabBarItem()
    {
        SelectableMixin.Attach<LiquidTabBarItem>(IsSelectedProperty);
        PressedMixin.Attach<LiquidTabBarItem>();
        FocusableProperty.OverrideDefaultValue<LiquidTabBarItem>(true);
        AffectsMeasure<LiquidTabBarItem>(LabelProperty, GlyphProperty, BadgeProperty);
        AffectsRender<LiquidTabBarItem>(ForegroundProperty, ActiveGlyphProperty);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="ActiveGlyphProperty"/>
    public Geometry? ActiveGlyph { get => GetValue(ActiveGlyphProperty); set => SetValue(ActiveGlyphProperty, value); }

    /// <inheritdoc cref="BadgeProperty"/>
    public int Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <inheritdoc cref="BadgeLabelProperty"/>
    public string? BadgeLabel { get => GetValue(BadgeLabelProperty); set => SetValue(BadgeLabelProperty, value); }

    private LiquidTabBar? Owner => ItemsControl.ItemsControlFromItemContainer(this) as LiquidTabBar;

    /// <summary>The label text in use (see <see cref="Label"/>).</summary>
    internal string LabelText
    {
        get
        {
            EnsureLabel();
            return _labelText;
        }
    }

    /// <summary>How open the tab is now, 0 (glyph only) to 1 (label shown).</summary>
    internal double Openness => _openness;

    /// <summary>How visible the label is now (both layers use it).</summary>
    internal double LabelOpacity => IsTextTab ? 1 : LiquidTabBarMetrics.LabelOpacity(_openness, _labelFade);

    /// <summary>Whether the badge is drawn now (shown, or still going away).</summary>
    internal bool HasBadge => _badgePresence > 0.001 && _badge.Count > 0;

    /// <summary>A tab without a glyph: its label always shows and it never opens or closes.</summary>
    internal bool IsTextTab => Glyph is null;

    /// <summary>Width with the glyph alone.</summary>
    internal double ClosedWidth => IsTextTab ? OpenWidth : LiquidTabBarMetrics.SlotWidth;

    /// <summary>Width with the label shown.</summary>
    internal double OpenWidth
    {
        get
        {
            EnsureLabel();
            var label = Math.Round(_labelWidth);
            if (IsTextTab)
            {
                var badge = Badge > 0 ? BadgeWidth + LiquidTabBarMetrics.BadgeLabelShift : 0;
                return LiquidTabBarMetrics.TextPadding * 2 + label + badge;
            }
            return LabelX + label + LiquidTabBarMetrics.LabelTrailing;
        }
    }

    /// <summary>Width now, between <see cref="ClosedWidth"/> and <see cref="OpenWidth"/> by <see cref="Openness"/>.</summary>
    internal double CurrentWidth => ClosedWidth + (OpenWidth - ClosedWidth) * Math.Clamp(_openness, 0, 1.2);

    /// <summary>Width once the tab reaches <paramref name="openness"/>.</summary>
    internal double WidthAt(double openness) => ClosedWidth + (OpenWidth - ClosedWidth) * openness;

    /// <summary>Where the label starts, from the tab's left edge.</summary>
    private double LabelX => IsTextTab
        ? LiquidTabBarMetrics.TextPadding
        : LiquidTabBarMetrics.LabelOffset + (Badge > 0 ? LiquidTabBarMetrics.BadgeLabelShift : 0);

    private double BadgeWidth => Math.Max(LiquidTabBarMetrics.BadgeMinWidth,
        _badge.Count * (Owner?.BaseDigits.DigitWidth ?? 7) + 2 * LiquidTabBarMetrics.BadgePadding);

    /// <summary>The accessible name: the label, then the badge (its <see cref="BadgeLabel"/> or the number).</summary>
    internal string AccessibleName
    {
        get
        {
            var label = LabelText;
            var badge = Badge;
            if (badge <= 0)
            {
                return label;
            }
            var spoken = BadgeLabel is { Length: > 0 } text ? text : badge.ToString(System.Globalization.CultureInfo.CurrentCulture);
            return label.Length == 0 ? spoken : label + ", " + spoken;
        }
    }

    /// <summary>
    /// Moves the tab toward <paramref name="openness"/> (1 open, 0 closed) on the slide spring, keeping its speed, or
    /// jumps there. A label that should no longer show fades out at once; one that should show is allowed to again.
    /// </summary>
    internal void SetOpenTarget(double openness, bool labelShown, bool animate)
    {
        if (IsTextTab)
        {
            openness = 1;
            labelShown = true;
        }
        if (!animate)
        {
            _open?.Jump(openness);
            _openness = openness;
            _fade?.Jump(labelShown ? 1 : 0);
            _labelFade = labelShown ? 1 : 0;
            Owner?.OnItemGeometryChanged();
            InvalidateVisual();
            return;
        }
        _open ??= CreateValue(_openness, v => _openness = v, geometry: true);
        if (Math.Abs(_open.Target - openness) > 0.0001 || Math.Abs(_open.Value - openness) > 0.0001)
        {
            _ = _open.AnimateTo(openness, LiquidTabBarMetrics.Slide);
        }
        if (labelShown)
        {
            _fade?.Jump(1);
            _labelFade = 1;
        }
        else if (_labelFade > 0)
        {
            _fade ??= CreateValue(_labelFade, v => _labelFade = v, geometry: false);
            if (ArcMotion.IsFull(this))
            {
                _ = _fade.AnimateTo(0, LiquidTabBarMetrics.LabelFadeOut);
            }
            else
            {
                _fade.Jump(0);
            }
        }
    }

    private MotionValue<double> CreateValue(double initial, Action<double> write, bool geometry)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v =>
        {
            write(v);
            if (geometry)
            {
                Owner?.OnItemGeometryChanged();
            }
            else
            {
                Owner?.OnItemLookChanged();
            }
            InvalidateVisual();
        };
        return value;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == ContentProperty || change.Property == ValueProperty
            || change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == FontWeightProperty)
        {
            _labelDirty = true;
            InvalidateMeasure();
            Owner?.OnItemGeometryChanged();
        }
        else if (change.Property == BadgeProperty)
        {
            OnBadgeChanged(change.GetNewValue<int>());
        }
        else if (change.Property == GlyphProperty)
        {
            Owner?.OnItemsChanged();
        }
        else if (change.Property == ActiveGlyphProperty)
        {
            Owner?.OnItemLookChanged();
        }
    }

    private void OnBadgeChanged(int badge)
    {
        var animate = ArcMotion.IsFull(this) && VisualRoot is not null;
        if (badge > 0)
        {
            var wasShown = _badge.Count > 0 && _badgePresence > 0.001;
            var rolls = _badge.SetValue(badge, animate && wasShown);
            if (rolls)
            {
                _roll ??= CreateRoll();
                _roll.Jump(0);
                _ = _roll.AnimateTo(1, LiquidTabBarMetrics.BadgeRoll);
            }
            else
            {
                _roll?.Jump(1);
            }
            SetPresence(1, animate);
        }
        else
        {
            SetPresence(0, animate);
        }
        InvalidateMeasure();
        Owner?.OnItemGeometryChanged();
        InvalidateVisual();
    }

    private MotionValue<double> CreateRoll()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            _badge.Progress = v;
            Owner?.OnItemLookChanged();
            InvalidateVisual();
        };
        return value;
    }

    private void SetPresence(double target, bool animate)
    {
        if (!animate)
        {
            _presence?.Jump(target);
            _badgePresence = target;
            return;
        }
        _presence ??= CreateValue(_badgePresence, v => _badgePresence = v, geometry: false);
        _ = _presence.AnimateTo(target, LiquidTabBarMetrics.BadgePop);
    }

    private void EnsureLabel()
    {
        if (!_labelDirty)
        {
            return;
        }
        _labelDirty = false;
        _labelText = Label ?? Content switch
        {
            string s => s,
            Control => null,
            { } other => other.ToString(),
            null => null
        } ?? Value?.ToString() ?? string.Empty;
        DisposeLabelLayouts();
        if (_labelText.Length == 0)
        {
            _labelWidth = 0;
            return;
        }
        using var probe = CreateLabelLayout(Brushes.Black);
        _labelWidth = probe.WidthIncludingTrailingWhitespace;
    }

    private TextLayout CreateLabelLayout(IBrush brush)
        => new(_labelText, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, brush,
            letterSpacing: -0.01 * FontSize);

    private void DisposeLabelLayouts()
    {
        _labelLayout?.Dispose();
        _labelLayout = null;
        _labelBrush = null;
        _lensLabelLayout?.Dispose();
        _lensLabelLayout = null;
        _lensLabelBrush = null;
    }

    private TextLayout? LabelLayout(IBrush? brush, ref TextLayout? cache, ref IBrush? cacheBrush)
    {
        EnsureLabel();
        if (brush is null || _labelText.Length == 0)
        {
            return null;
        }
        if (cache is null || !ReferenceEquals(cacheBrush, brush))
        {
            cache?.Dispose();
            cache = CreateLabelLayout(brush);
            cacheBrush = brush;
        }
        return cache;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(new Size(OpenWidth, LiquidTabBarMetrics.ItemHeight));
        return new Size(OpenWidth, LiquidTabBarMetrics.ItemHeight);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, LiquidTabBarTokens.Key(LiquidTabBarTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        var height = Bounds.Height;
        var foreground = Foreground;
        // The whole tab takes the pointer (a press captures the tab itself, so a lost capture reaches it).
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Glyph is { } glyph && foreground is not null)
        {
            if (_glyphPen is null || !ReferenceEquals(_glyphPenBrush, foreground))
            {
                _glyphPen = new Pen(foreground, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                _glyphPenBrush = foreground;
            }
            LiquidTabBarPainter.DrawGlyph(context, glyph, GlyphOrigin(height), null, _glyphPen);
        }
        var labelsShown = IsTextTab || owner?.Labels == LiquidTabBarLabels.Always;
        if (labelsShown)
        {
            DrawLabel(context, LabelLayout(foreground, ref _labelLayout, ref _labelBrush), height);
        }
        if (owner is not null && HasBadge)
        {
            var digits = owner.BaseDigits;
            LiquidTabBarPainter.DrawBadge(context, BadgeRect(height, digits.DigitWidth), _badgePresence, owner.Background,
                owner.LensBackground, digits, _badge);
        }
    }

    /// <summary>Draws this tab inverted, as the lens shows it: the glyph filled in, the label and the badge in the lens colors.</summary>
    internal void RenderInverted(DrawingContext context, LiquidTabBar owner, IPen? glyphPen)
    {
        var height = Bounds.Height;
        if ((ActiveGlyph ?? Glyph) is { } glyph)
        {
            LiquidTabBarPainter.DrawGlyph(context, glyph, GlyphOrigin(height), owner.LensForeground, glyphPen);
        }
        DrawLabel(context, LabelLayout(owner.LensForeground, ref _lensLabelLayout, ref _lensLabelBrush), height);
        if (HasBadge)
        {
            var digits = owner.LensDigits;
            LiquidTabBarPainter.DrawBadge(context, BadgeRect(height, digits.DigitWidth), _badgePresence, owner.LensBackground,
                owner.LensForeground, digits, _badge);
        }
    }

    private static Point GlyphOrigin(double height)
        => new(LiquidTabBarMetrics.GlyphInset, (height - LiquidTabBarMetrics.GlyphSize) / 2);

    private Rect BadgeRect(double height, double digitWidth)
    {
        if (IsTextTab)
        {
            var x = LiquidTabBarMetrics.TextPadding + Math.Round(_labelWidth) + LiquidTabBarMetrics.BadgeLabelShift;
            return LiquidTabBarPainter.BadgeRect(new Point(x, (height - LiquidTabBarMetrics.BadgeHeight) / 2), _badge.Count, digitWidth);
        }
        var glyph = GlyphOrigin(height);
        return LiquidTabBarPainter.BadgeRect(
            new Point(glyph.X + LiquidTabBarMetrics.BadgeOffsetX, glyph.Y + LiquidTabBarMetrics.BadgeOffsetY), _badge.Count, digitWidth);
    }

    private void DrawLabel(DrawingContext context, TextLayout? layout, double height)
    {
        var opacity = LabelOpacity;
        if (layout is null || opacity <= 0.001)
        {
            return;
        }
        var slide = IsTextTab || !ArcMotion.IsFull(this) ? 0 : -LiquidTabBarMetrics.LabelSlide * (1 - Math.Clamp(_openness, 0, 1));
        var origin = new Point(LabelX + slide, (height - layout.Height) / 2);
        using (context.PushClip(new Rect(0, 0, Bounds.Width, height)))
        using (context.PushOpacity(opacity))
        {
            layout.Draw(context, origin);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Hover follows the mouse only: a touch never leaves a tab looking hovered.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type == PointerType.Mouse);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        _pressedInside = e.Pointer.Type != PointerType.Mouse || point.Properties.IsLeftButtonPressed;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressedInside;
        _pressedInside = false;
        // A tap or click selects on release, inside the tab; a drag of the lens handles its own release.
        if (!pressed || e.Handled || !new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            return;
        }
        e.Handled = true;
        Owner?.OnItemClicked(this);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressedInside = false;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A detached tab lands where it was going; the next attach starts from there.
        if (_open is not null)
        {
            _open.Jump(_open.Target);
        }
        if (_fade is not null)
        {
            _fade.Jump(_fade.Target);
        }
        if (_presence is not null)
        {
            _presence.Jump(_presence.Target);
        }
        _roll?.Jump(1);
        DisposeLabelLayouts();
        _glyphPen = null;
        _glyphPenBrush = null;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LiquidTabBarItemAutomationPeer(this);

    private sealed class LiquidTabBarItemAutomationPeer : ListItemAutomationPeer
    {
        public LiquidTabBarItemAutomationPeer(LiquidTabBarItem owner) : base(owner)
        {
        }

        private new LiquidTabBarItem Owner => (LiquidTabBarItem)base.Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override string? GetNameCore()
            => AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : Owner.AccessibleName;
    }
}
