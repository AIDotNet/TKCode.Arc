using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The panel the toasts stack in. Every toast sits in the same 64px slot at the anchored edge (the bottom, or the top for
/// the top corners); where it shows is a render transform the <see cref="SocialProofToast"/> springs, so moving never
/// lays anything out. It reserves the height of the fanned stack so hovering never pushes the page around.
/// </summary>
internal sealed class SocialProofToastStack : Panel
{
    public static readonly StyledProperty<double> CardHeightProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(CardHeight), 64);

    public static readonly StyledProperty<double> FanGapProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(FanGap), 8);

    public static readonly StyledProperty<double> StackStepProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(StackStep), 10);

    public static readonly StyledProperty<double> StackScaleStepProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(StackScaleStep), 0.05);

    public static readonly StyledProperty<double> EnterOffsetProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(EnterOffset), 16);

    public static readonly StyledProperty<double> ExitScaleProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(ExitScale), 0.96);

    public static readonly StyledProperty<double> CornerWidthProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(CornerWidth), 360);

    public static readonly StyledProperty<double> CornerInsetProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(CornerInset), 24);

    public static readonly StyledProperty<double> CompactInsetProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(CompactInset), 16);

    public static readonly StyledProperty<double> CompactBreakpointProperty =
        AvaloniaProperty.Register<SocialProofToastStack, double>(nameof(CompactBreakpoint), 480);

    public static readonly StyledProperty<int> SlotsProperty =
        AvaloniaProperty.Register<SocialProofToastStack, int>(nameof(Slots), 3);

    public static readonly StyledProperty<bool> IsTopAnchoredProperty =
        AvaloniaProperty.Register<SocialProofToastStack, bool>(nameof(IsTopAnchored));

    static SocialProofToastStack()
    {
        AffectsMeasure<SocialProofToastStack>(CardHeightProperty, FanGapProperty, SlotsProperty, CornerWidthProperty);
        AffectsArrange<SocialProofToastStack>(IsTopAnchoredProperty);
    }

    public double CardHeight { get => GetValue(CardHeightProperty); set => SetValue(CardHeightProperty, value); }

    public double FanGap { get => GetValue(FanGapProperty); set => SetValue(FanGapProperty, value); }

    public double StackStep { get => GetValue(StackStepProperty); set => SetValue(StackStepProperty, value); }

    public double StackScaleStep { get => GetValue(StackScaleStepProperty); set => SetValue(StackScaleStepProperty, value); }

    public double EnterOffset { get => GetValue(EnterOffsetProperty); set => SetValue(EnterOffsetProperty, value); }

    public double ExitScale { get => GetValue(ExitScaleProperty); set => SetValue(ExitScaleProperty, value); }

    public double CornerWidth { get => GetValue(CornerWidthProperty); set => SetValue(CornerWidthProperty, value); }

    public double CornerInset { get => GetValue(CornerInsetProperty); set => SetValue(CornerInsetProperty, value); }

    public double CompactInset { get => GetValue(CompactInsetProperty); set => SetValue(CompactInsetProperty, value); }

    public double CompactBreakpoint { get => GetValue(CompactBreakpointProperty); set => SetValue(CompactBreakpointProperty, value); }

    /// <summary>How many toasts can fan out (the toast's MaxVisible): the reserved height.</summary>
    public int Slots { get => GetValue(SlotsProperty); set => SetValue(SlotsProperty, value); }

    /// <summary>Toasts sit at the top and the stack grows downward (the top corners).</summary>
    public bool IsTopAnchored { get => GetValue(IsTopAnchoredProperty); set => SetValue(IsTopAnchoredProperty, value); }

    /// <summary>The fanned stack's height: every slot and the gaps between them.</summary>
    public double ReservedHeight => CardHeight + (CardHeight + FanGap) * Math.Max(0, Slots - 1);

    protected override Type StyleKeyOverride => typeof(SocialProofToastStack);

    protected override Size MeasureOverride(Size availableSize)
    {
        // The stack fills its container's width; it never follows its content.
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : CornerWidth;
        var slot = new Size(width, CardHeight);
        foreach (var child in Children)
        {
            child.Measure(child is SocialProofToastCard ? slot : new Size(1, 1));
        }
        return new Size(width, ReservedHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = IsTopAnchored ? 0 : Math.Max(0, finalSize.Height - CardHeight);
        foreach (var child in Children)
        {
            child.Arrange(child is SocialProofToastCard ? new Rect(0, top, finalSize.Width, CardHeight) : new Rect(0, 0, 1, 1));
        }
        return finalSize;
    }
}

/// <summary>
/// Holds the stack in a window corner: a pass-through panel the size of the overlay layer (it has no background, so the
/// page under it stays clickable), with the stack 24px from the corner, or across the width with 16px gutters on a
/// narrow window.
/// </summary>
internal sealed class SocialProofToastCorner : Panel
{
    private readonly SocialProofToastStack _stack;
    private Control? _layer;
    private SocialProofToastPosition _position;

    public SocialProofToastCorner(SocialProofToastStack stack)
    {
        _stack = stack;
        IsHitTestVisible = true;
    }

    public void Mount(Panel layer, SocialProofToastPosition position)
    {
        _position = position;
        if (!ReferenceEquals(_layer, layer))
        {
            Unmount();
            _layer = layer;
            layer.SizeChanged += OnLayerSizeChanged;
            layer.Children.Add(this);
        }
        if (_stack.Parent is Panel parent && !ReferenceEquals(parent, this))
        {
            parent.Children.Remove(_stack);
        }
        if (_stack.Parent is null)
        {
            Children.Add(_stack);
        }
        Place();
    }

    public void Unmount()
    {
        if (_layer is Panel layer)
        {
            layer.SizeChanged -= OnLayerSizeChanged;
            layer.Children.Remove(this);
        }
        _layer = null;
        Children.Remove(_stack);
    }

    private void OnLayerSizeChanged(object? sender, SizeChangedEventArgs e) => Place();

    private void Place()
    {
        if (_layer is null)
        {
            return;
        }
        var size = _layer.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            size = TopLevel.GetTopLevel(_layer)?.ClientSize ?? size;
        }
        Width = size.Width;
        Height = size.Height;
        var top = _position is SocialProofToastPosition.TopLeft or SocialProofToastPosition.TopRight;
        var left = _position is SocialProofToastPosition.TopLeft or SocialProofToastPosition.BottomLeft;
        _stack.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        if (size.Width < _stack.CompactBreakpoint)
        {
            _stack.HorizontalAlignment = HorizontalAlignment.Stretch;
            _stack.Width = double.NaN;
            _stack.Margin = new Thickness(_stack.CompactInset);
        }
        else
        {
            _stack.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            _stack.Width = _stack.CornerWidth;
            _stack.Margin = new Thickness(_stack.CornerInset);
        }
    }
}

/// <summary>
/// A toast's second line: the meta and time, then the verified badge right after them (its first child is the meta, its
/// second the badge). The badge keeps its width and the meta is truncated first, so the line never wraps, the badge never
/// hides and no gap opens between them on a wide toast.
/// </summary>
internal sealed class SocialProofToastMetaRow : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<SocialProofToastMetaRow, double>(nameof(Spacing), 8);

    static SocialProofToastMetaRow()
    {
        AffectsMeasure<SocialProofToastMetaRow>(SpacingProperty);
    }

    /// <summary>The space between the meta and the badge when both show.</summary>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    private Control? Meta => Children.Count > 0 && Children[0].IsVisible ? Children[0] : null;

    private Control? Badge => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    private double Gap => Meta is not null && Badge is not null ? Spacing : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var meta = Meta;
        var badge = Badge;
        var gap = Gap;
        var badgeWidth = 0.0;
        var height = 0.0;
        if (badge is not null)
        {
            badge.Measure(new Size(Math.Max(0, width - gap), availableSize.Height));
            badgeWidth = badge.DesiredSize.Width;
            height = badge.DesiredSize.Height;
        }
        var metaWidth = 0.0;
        if (meta is not null)
        {
            meta.Measure(new Size(Math.Max(0, width - gap - badgeWidth), availableSize.Height));
            metaWidth = meta.DesiredSize.Width;
            height = Math.Max(height, meta.DesiredSize.Height);
        }
        var desired = metaWidth + gap + badgeWidth;
        return new Size(double.IsFinite(width) ? Math.Min(desired, width) : desired, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var meta = Meta;
        var badge = Badge;
        var gap = Gap;
        var badgeWidth = badge is null ? 0 : Math.Min(badge.DesiredSize.Width, Math.Max(0, finalSize.Width - gap));
        var metaWidth = meta is null ? 0 : Math.Min(meta.DesiredSize.Width, Math.Max(0, finalSize.Width - gap - badgeWidth));
        meta?.Arrange(new Rect(0, 0, metaWidth, finalSize.Height));
        badge?.Arrange(new Rect(metaWidth + gap, 0, badgeWidth, finalSize.Height));
        return finalSize;
    }
}

/// <summary>
/// One toast: the mark (a round avatar for a person, a rounded square for a company), the sentence on one line, the meta,
/// time and verified badge on a second, and a dismiss button. It is a button so a selectable stack can open pricing or a
/// customer story from it; otherwise it takes no focus and only its dismiss button does. Its place in the stack is
/// sprung from code (see <see cref="Place"/>).
/// </summary>
[TemplatePart(ContentPart, typeof(Control))]
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(MonogramPart, typeof(Border))]
[TemplatePart(InitialPart, typeof(TextBlock))]
[TemplatePart(LogoPart, typeof(ArcGlyph))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(TextBlock))]
[TemplatePart(BadgePart, typeof(Control))]
[TemplatePart(BadgeTextPart, typeof(TextBlock))]
[TemplatePart(DismissPart, typeof(SocialProofToastDismiss))]
[PseudoClasses(CoveredPseudoClass, SelectablePseudoClass, TouchPseudoClass, CompanyPseudoClass)]
internal sealed class SocialProofToastCard : AvaloniaButton
{
    internal const string ContentPart = "PART_Content";
    internal const string AvatarPart = "PART_Avatar";
    internal const string MonogramPart = "PART_Monogram";
    internal const string InitialPart = "PART_Initial";
    internal const string LogoPart = "PART_Logo";
    internal const string TitlePart = "PART_Title";
    internal const string MetaPart = "PART_Meta";
    internal const string BadgePart = "PART_Badge";
    internal const string BadgeTextPart = "PART_BadgeText";
    internal const string DismissPart = "PART_Dismiss";

    internal const string CoveredPseudoClass = ":covered";
    internal const string SelectablePseudoClass = ":selectable";
    internal const string TouchPseudoClass = ":touch";
    internal const string CompanyPseudoClass = ":company";

    /// <summary>The color of the name (the rest of the sentence uses the text's own, secondary color).</summary>
    public static readonly StyledProperty<IBrush?> NameBrushProperty =
        AvaloniaProperty.Register<SocialProofToastCard, IBrush?>(nameof(NameBrush));

    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _scale = new();
    private readonly Run _nameRun = new() { FontWeight = FontWeight.Medium };
    private readonly Run _actionRun = new();
    private MotionValue<double>? _y;
    private MotionValue<double>? _s;
    private MotionValue<double>? _o;
    private Avatar? _avatar;
    private Border? _monogram;
    private TextBlock? _initial;
    private ArcGlyph? _logo;
    private TextBlock? _title;
    private TextBlock? _meta;
    private Control? _badge;
    private TextBlock? _badgeText;
    private SocialProofToastDismiss? _dismiss;
    private SocialProofEvent? _event;
    private SocialProofToastLabels _labels = SocialProofToastLabels.Default;
    private DateTimeOffset _now;

    public SocialProofToastCard()
    {
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        _nameRun.Bind(TextElement.ForegroundProperty, this.GetObservable(NameBrushProperty));
    }

    protected override Type StyleKeyOverride => typeof(SocialProofToastCard);

    public IBrush? NameBrush { get => GetValue(NameBrushProperty); set => SetValue(NameBrushProperty, value); }

    /// <summary>The event on the toast.</summary>
    public SocialProofEvent? Event => _event;

    /// <summary>The toast is leaving (dismissed or pushed past the limit): inert and hidden from assistive tech.</summary>
    public bool IsExiting { get; private set; }

    /// <summary>Raised when the dismiss button is pressed.</summary>
    public event EventHandler? DismissRequested;

    internal SocialProofToastDismiss? DismissButton => _dismiss;

    internal TextBlock? TitleBlock => _title;

    internal TextBlock? MetaBlock => _meta;

    internal Control? Badge => _badge;

    internal Avatar? AvatarView => _avatar;

    internal Border? MonogramView => _monogram;

    /// <summary>The current position, scale and opacity (what the springs have reached).</summary>
    internal double OffsetY => _translate.Y;

    internal double CurrentScale => _scale.ScaleX;

    internal double TargetY => _y?.Target ?? _translate.Y;

    internal double TargetScale => _s?.Target ?? _scale.ScaleX;

    internal double TargetOpacity => _o?.Target ?? Opacity;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_dismiss is not null)
        {
            _dismiss.Click -= OnDismissClick;
        }
        if (_title is not null)
        {
            _title.Inlines?.Clear();
        }
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _monogram = e.NameScope.Find<Border>(MonogramPart);
        _initial = e.NameScope.Find<TextBlock>(InitialPart);
        _logo = e.NameScope.Find<ArcGlyph>(LogoPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _meta = e.NameScope.Find<TextBlock>(MetaPart);
        _badge = e.NameScope.Find<Control>(BadgePart);
        _badgeText = e.NameScope.Find<TextBlock>(BadgeTextPart);
        _dismiss = e.NameScope.Find<SocialProofToastDismiss>(DismissPart);
        if (_dismiss is not null)
        {
            _dismiss.Click += OnDismissClick;
        }
        if (_title is not null)
        {
            _title.Inlines ??= [];
            _title.Inlines.Add(_nameRun);
            _title.Inlines.Add(_actionRun);
        }
        Refresh();
    }

    private void OnDismissClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        e.Handled = true;
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Puts an event on the toast (a pooled toast is reused with a new one).</summary>
    public void Show(SocialProofEvent e, SocialProofToastLabels labels, DateTimeOffset now, bool selectable)
    {
        _event = e;
        _labels = labels;
        _now = now;
        IsExiting = false;
        IsEnabled = true;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Default);
        SetSelectable(selectable);
        Refresh();
    }

    /// <summary>Re-reads the relative time (the stack calls this as time passes).</summary>
    public void UpdateTime(DateTimeOffset now)
    {
        _now = now;
        Refresh();
    }

    public void SetLabels(SocialProofToastLabels labels)
    {
        _labels = labels;
        Refresh();
    }

    public void SetSelectable(bool selectable)
    {
        Focusable = selectable;
        IsTabStop = selectable;
        Cursor = selectable ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) : null;
        PseudoClasses.Set(SelectablePseudoClass, selectable);
    }

    public void SetCovered(bool covered) => PseudoClasses.Set(CoveredPseudoClass, covered);

    public void SetTouch(bool touch) => PseudoClasses.Set(TouchPseudoClass, touch);

    /// <summary>Makes the toast inert as it leaves: no input, no focus, nothing read out.</summary>
    public void MarkExiting()
    {
        IsExiting = true;
        IsEnabled = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    private void Refresh()
    {
        if (_event is not { } e)
        {
            return;
        }
        // The slot: a person's portrait (or initials) in a round Avatar; a mark when there is no portrait (a company's
        // always wins), round for a person and a rounded square for a company; otherwise a company's first letter.
        var company = e.Actor == SocialProofActor.Company;
        var logo = e.Logo is not null && (company || e.Portrait is null);
        var avatar = !company && !logo;
        PseudoClasses.Set(CompanyPseudoClass, company);
        if (_avatar is not null)
        {
            _avatar.IsVisible = avatar;
            _avatar.DisplayName = e.Name;
            _avatar.Source = avatar ? e.Portrait : null;
        }
        if (_monogram is not null)
        {
            _monogram.IsVisible = !avatar;
        }
        if (_logo is not null)
        {
            _logo.Data = e.Logo;
            _logo.IsVisible = logo;
        }
        if (_initial is not null)
        {
            _initial.Text = SocialProofToastFormat.Monogram(e.Name);
            _initial.IsVisible = !avatar && !logo;
        }
        _nameRun.Text = e.Name;
        _actionRun.Text = string.IsNullOrEmpty(e.Action) ? string.Empty : " " + e.Action;
        if (_meta is not null)
        {
            var meta = SocialProofToastFormat.Meta(e, _now, _labels);
            if (_meta.Text != meta)
            {
                _meta.Text = meta;
            }
            _meta.IsVisible = meta.Length > 0;
        }
        var verified = SocialProofToastFormat.Verified(e, _labels);
        if (_badge is not null)
        {
            _badge.IsVisible = verified is not null;
        }
        if (_badgeText is not null)
        {
            _badgeText.Text = verified;
        }
        AutomationProperties.SetName(this, SocialProofToastFormat.Accessible(e, _now, _labels));
        if (_dismiss is not null)
        {
            AutomationProperties.SetName(_dismiss, _labels.Dismiss(e.Sentence));
        }
    }

    // ---- Motion ----------------------------------------------------------------------------------------------------
    // Code-driven: each toast's offset and scale depend on its index in a list that changes every few seconds and on the
    // hover state, and a spring interrupted mid-move must keep its velocity. A ControlTheme cannot express either.

    private void EnsureMotion()
    {
        if (_y is not null)
        {
            return;
        }
        _y = new MotionValue<double>(this, 0, MotionVectors.Double);
        _y.Changed += v => _translate.Y = v;
        _s = new MotionValue<double>(this, 1, MotionVectors.Double);
        _s.Changed += v =>
        {
            _scale.ScaleX = v;
            _scale.ScaleY = v;
        };
        _o = new MotionValue<double>(this, 1, MotionVectors.Double);
        _o.Changed += v => Opacity = v;
    }

    /// <summary>Jumps to a pose: no motion.</summary>
    public void Jump(double y, double scale, double opacity)
    {
        EnsureMotion();
        _y!.Jump(y);
        _s!.Jump(scale);
        _o!.Jump(opacity);
        _translate.Y = y;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Opacity = opacity;
    }

    /// <summary>Springs the offset and scale (the springs jump under reduced motion and off).</summary>
    public void Place(double y, double scale, SpringSpec spring)
    {
        EnsureMotion();
        if (_y!.IsAnimating ? _y.Target != y : _y.Value != y)
        {
            _ = _y.AnimateTo(y, MotionSpec.Spring(spring));
        }
        if (_s!.IsAnimating ? _s.Target != scale : _s.Value != scale)
        {
            _ = _s.AnimateTo(scale, MotionSpec.Spring(spring));
        }
    }

    /// <summary>Tweens the scale (a dismissed toast shrinking as it fades).</summary>
    public Task<bool> ShrinkTo(double scale, TimeSpan duration, Avalonia.Animation.Easings.Easing easing)
    {
        EnsureMotion();
        return _s!.AnimateTo(scale, MotionSpec.Tween(duration, easing));
    }

    /// <summary>Tweens the opacity (fades run under reduced motion too).</summary>
    public Task<bool> FadeTo(double opacity, TimeSpan duration, Avalonia.Animation.Easings.Easing easing)
    {
        EnsureMotion();
        if (!_o!.IsAnimating && _o.Value == opacity)
        {
            return Task.FromResult(true);
        }
        return _o.AnimateTo(opacity, MotionSpec.Tween(duration, easing));
    }
}

/// <summary>A toast's dismiss button: 24px, an X, a quiet fill under the pointer and a keyboard focus ring.</summary>
internal sealed class SocialProofToastDismiss : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(SocialProofToastDismiss);

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonAutomationPeer(this);
}
