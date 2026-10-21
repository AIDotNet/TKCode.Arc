// Ported from Arc's free notification-center block (MIT, Copyright (c) 2026 Elia Kuratli).
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The buttons of a <see cref="NotificationCenter"/>, one look per Arc class.</summary>
internal enum NotificationCenterButtonKind
{
    /// <summary><c>.trigger</c>: the 42px bell.</summary>
    Trigger,

    /// <summary><c>.close</c>: the 30px close button of the header.</summary>
    Close,

    /// <summary><c>.viewSwitch button</c>: All / Unread.</summary>
    View,

    /// <summary><c>.markAll</c>: the quiet "Mark all read" text button.</summary>
    MarkAll,

    /// <summary><c>.itemToggle</c>: the title and preview of a row (expands it).</summary>
    Row,

    /// <summary><c>.itemActions button</c>: the outlined Mark read / Dismiss buttons.</summary>
    Action,

    /// <summary><c>.empty button</c>: the underlined "View all updates" link.</summary>
    Link,

    /// <summary><c>.footer button</c>: "Clear read".</summary>
    Footer
}

/// <summary>
/// A button of <see cref="NotificationCenter"/>: optional Lucide glyph plus content, styled by <see cref="Kind"/>.
/// <see cref="IsOn"/> is the open bell, the chosen view (<c>aria-pressed</c>) or the expanded row (<c>aria-expanded</c>).
/// Hover styles use <c>:hover</c>, set for mouse and pen only (Arc's <c>(hover: hover) and (pointer: fine)</c>).
/// </summary>
internal sealed class NotificationCenterButton : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<NotificationCenterButtonKind> KindProperty =
        AvaloniaProperty.Register<NotificationCenterButton, NotificationCenterButtonKind>(nameof(Kind));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<NotificationCenterButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<NotificationCenterButton, double>(nameof(GlyphSize), 14);

    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<NotificationCenterButton, bool>(nameof(IsOn));

    private bool _keyboardActivation;

    public NotificationCenterButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    /// <summary>True while a Click raised by Enter or Space is being handled (focus moves then show the ring).</summary>
    public bool IsKeyboardActivation => _keyboardActivation;

    protected override Type StyleKeyOverride => typeof(NotificationCenterButton);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _keyboardActivation = true;
        try
        {
            base.OnKeyDown(e);
        }
        finally
        {
            _keyboardActivation = false;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        _keyboardActivation = true;
        try
        {
            base.OnKeyUp(e);
        }
        finally
        {
            _keyboardActivation = false;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty && ControlAutomationPeer.FromElement(this) is ButtonPeer peer)
        {
            peer.RaiseStateChanged(change.GetNewValue<bool>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonPeer(this);

    /// <summary>A button; the view buttons are toggles, the bell and the rows expand and collapse.</summary>
    private sealed class ButtonPeer(NotificationCenterButton owner) : ButtonAutomationPeer(owner), IToggleProvider, IExpandCollapseProvider
    {
        private NotificationCenterButton Button => (NotificationCenterButton)Owner;

        public ToggleState ToggleState => Button.IsOn ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public ExpandCollapseState ExpandCollapseState => Button.IsOn ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Button.IsOn)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Button.IsOn)
            {
                Invoke();
            }
        }

        public void RaiseStateChanged(bool on)
        {
            if (Button.Kind == NotificationCenterButtonKind.View)
            {
                RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                    on ? ToggleState.Off : ToggleState.On, on ? ToggleState.On : ToggleState.Off);
            }
            else if (Button.Kind is NotificationCenterButtonKind.Row or NotificationCenterButtonKind.Trigger)
            {
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    on ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                    on ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
            }
        }

        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(IToggleProvider))
            {
                return Button.Kind == NotificationCenterButtonKind.View ? this : null;
            }
            if (providerType == typeof(IExpandCollapseProvider))
            {
                return Button.Kind is NotificationCenterButtonKind.Row or NotificationCenterButtonKind.Trigger ? this : null;
            }
            return base.GetProviderCore(providerType);
        }
    }
}

/// <summary>
/// AnimatePresence for one part of the panel (a row, the details, the empty state, the footer, "Mark all read").
/// The part opens its height from zero on the smooth spring and fades in, and leaves the same way, each with its own
/// delay, the way Arc's motion variants time them; while it collapses the content keeps its height and is clipped.
/// Code-driven because the ControlTheme cannot express it: the height and the opacity run on different clocks
/// (spring and tween) with per-row delays chosen at exit time (bulk cascade), the part must stay in the tree until
/// its exit finishes, and an exit reverses from where it is when the part comes back.
/// Reduced motion shows parts at once and fades them out in 120ms (Arc's <c>instant</c> transition and
/// <c>fadeOut</c>); Off applies every change at once.
/// </summary>
internal sealed class NotificationCenterReveal : Decorator
{
    private readonly TranslateTransform _translate = new();
    private MotionValue<double>? _height;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _offset;
    private bool _shown = true;
    private int _generation;

    /// <summary>The height follows the presence (false for parts that only fade, such as "Mark all read").</summary>
    public bool CollapsesHeight { get; set; } = true;

    /// <summary>Blur at the hidden end in px (Arc: 2px on "Mark all read").</summary>
    public double BlurRadius { get; set; }

    private double _placeholderHeight;

    /// <summary>
    /// Height the part takes while it has no content: the panel keeps a cheap host for every notification and gives it
    /// the height of a row until the row is created (rows are created only near the viewport).
    /// </summary>
    public double PlaceholderHeight
    {
        get => _placeholderHeight;
        set
        {
            if (Math.Abs(_placeholderHeight - value) < 0.01)
            {
                return;
            }
            _placeholderHeight = value;
            if (Child is null)
            {
                InvalidateMeasure();
            }
        }
    }

    /// <summary>Shown, or showing (false while leaving and after it left).</summary>
    public bool IsShown => _shown;

    /// <summary>True between <see cref="Hide"/> and the end of the exit.</summary>
    public bool IsLeaving => !_shown && Child is { IsVisible: true };

    /// <summary>The exit finished and the content was hidden.</summary>
    public event EventHandler? Hidden;

    /// <summary>Fraction of the content height shown (0–1).</summary>
    internal double HeightFraction => _height?.Value ?? (_shown ? 1 : 0);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty && change.NewValue is Control child)
        {
            child.RenderTransform = _translate;
            child.IsVisible = _shown;
        }
    }

    private void Ensure()
    {
        if (_height is not null)
        {
            return;
        }
        _height = new MotionValue<double>(this, _shown ? 1 : 0, MotionVectors.Double);
        _height.Changed += _ => InvalidateMeasure();
        _opacity = new MotionValue<double>(this, _shown ? 1 : 0, MotionVectors.Double);
        _opacity.Changed += ApplyOpacity;
        _offset = new MotionValue<double>(this, 0, MotionVectors.Double);
        _offset.Changed += y => _translate.Y = y;
    }

    private void ApplyOpacity(double value)
    {
        var opacity = Math.Clamp(value, 0, 1);
        Opacity = opacity;
        Effect = BlurRadius > 0 ? ArcBlur.Get(BlurRadius * (1 - opacity)) : null;
    }

    // Attached is enough: a part mounted into a shown panel animates on its first frame (its parent is on screen).
    private ArcMotionLevel LevelFor(bool animate) =>
        animate && TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    /// <summary>
    /// Shows the content. From hidden it starts collapsed (when <paramref name="growHeight"/>), transparent (when
    /// <paramref name="fadeIn"/>) and <paramref name="rise"/> px low; a part that is leaving turns back from where it is.
    /// </summary>
    public void Show(bool animate, bool growHeight = true, TimeSpan heightDelay = default, bool fadeIn = true,
                     TimeSpan fadeDelay = default, double rise = 0, TimeSpan riseDelay = default)
    {
        Ensure();
        var fresh = !_shown && !IsLeaving;
        if (_shown && !fresh && _height!.Target >= 1 && _opacity!.Target >= 1)
        {
            return;
        }
        _generation++;
        _shown = true;
        IsHitTestVisible = true;
        if (Child is { } child)
        {
            child.IsVisible = true;
        }
        var level = LevelFor(animate);
        if (level != ArcMotionLevel.Full)
        {
            // Reduced: Arc enters with a zero-length transition. Off: at once.
            _height!.Jump(1);
            _opacity!.Jump(1);
            _offset!.Jump(0);
            return;
        }
        if (fresh)
        {
            _height!.Jump(CollapsesHeight && growHeight ? 0 : 1);
            _opacity!.Jump(fadeIn ? 0 : 1);
            _offset!.Jump(rise);
        }
        _ = _height!.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth, heightDelay));
        _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, fadeDelay));
        _ = _offset!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, riseDelay));
    }

    /// <summary>Plays the exit after <paramref name="delay"/> and hides the content when it finishes.</summary>
    public void Hide(bool animate, TimeSpan delay = default)
    {
        if (!_shown)
        {
            return;
        }
        Ensure();
        _shown = false;
        var generation = ++_generation;
        IsHitTestVisible = false;
        var level = LevelFor(animate);
        if (level == ArcMotionLevel.Off || Child is not { IsVisible: true })
        {
            Finish();
            return;
        }
        Task<bool> done;
        if (level == ArcMotionLevel.Reduced)
        {
            // Arc's fadeOut: opacity only, 120ms, no delay; the height snaps when the part leaves.
            done = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            var fade = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, delay));
            done = CollapsesHeight ? Both(fade, _height!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, delay))) : fade;
        }
        if (done.IsCompleted)
        {
            Finish();
            return;
        }
        _ = FinishWhenDone(done, generation);
    }

    /// <summary>Jumps to shown or hidden without motion (the panel opening: Arc's <c>initial={false}</c>).</summary>
    public void Set(bool shown)
    {
        Ensure();
        _generation++;
        _shown = shown;
        IsHitTestVisible = shown;
        _height!.Jump(shown ? 1 : 0);
        _opacity!.Jump(shown ? 1 : 0);
        _offset!.Jump(0);
        if (Child is { } child)
        {
            child.IsVisible = shown;
        }
    }

    private static async Task<bool> Both(Task<bool> first, Task<bool> second) => await first & await second;

    private async Task FinishWhenDone(Task<bool> task, int generation)
    {
        var reached = await task;
        if (reached && generation == _generation && !_shown)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _height?.Jump(0);
        if (Child is { } child)
        {
            child.IsVisible = false;
        }
        InvalidateMeasure();
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            // An empty host keeps the height of the row it stands for (and collapses with it).
            return _placeholderHeight <= 0
                ? default
                : new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                    Math.Min(_placeholderHeight * Math.Clamp(HeightFraction, 0, 1), availableSize.Height));
        }
        child.Measure(CollapsesHeight ? availableSize.WithHeight(double.PositiveInfinity) : availableSize);
        var desired = child.DesiredSize;
        if (!CollapsesHeight)
        {
            return desired;
        }
        var height = desired.Height * Math.Clamp(HeightFraction, 0, 1);
        return new Size(desired.Width, Math.Min(height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            // The content keeps its own height while the frame opens or closes around it (Arc's overflow: hidden).
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
        }
        return finalSize;
    }
}

/// <summary>
/// The 6px unread dot after a row title. It pops in from 0.3 on the snappy spring and leaves fast at 0.3, after the
/// bulk cascade delay when every update is marked at once. Drawn in <see cref="Render"/>: no layout while it moves.
/// </summary>
internal sealed class NotificationCenterDot : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<NotificationCenterDot, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<NotificationCenterDot, double>(nameof(Diameter), 6);

    private MotionValue<double>? _progress;
    private bool _present = true;
    private bool _fadeOnly;
    private int _generation;

    static NotificationCenterDot()
    {
        AffectsRender<NotificationCenterDot>(FillProperty);
        AffectsMeasure<NotificationCenterDot>(DiameterProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<NotificationCenterDot>(false);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    public bool IsPresent => _present;

    private MotionValue<double> Progress
    {
        get
        {
            if (_progress is null)
            {
                _progress = new MotionValue<double>(this, _present ? 1 : 0, MotionVectors.Double);
                _progress.Changed += _ => InvalidateVisual();
            }
            return _progress;
        }
    }

    // Attached is enough: a part mounted into a shown panel animates on its first frame (its parent is on screen).
    private ArcMotionLevel LevelFor(bool animate) =>
        animate && TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    /// <summary>Shows or hides the dot; <paramref name="exitDelay"/> holds a leaving dot (bulk cascade).</summary>
    public void SetPresent(bool present, bool animate, TimeSpan exitDelay = default)
    {
        if (present == _present)
        {
            return;
        }
        _present = present;
        var generation = ++_generation;
        var level = LevelFor(animate);
        _fadeOnly = level == ArcMotionLevel.Reduced;
        if (present)
        {
            IsVisible = true;
            if (level == ArcMotionLevel.Full)
            {
                _ = Progress.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else
            {
                Progress.Jump(1);
            }
            return;
        }
        if (level == ArcMotionLevel.Off)
        {
            Progress.Jump(0);
            IsVisible = false;
            return;
        }
        var spec = level == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, exitDelay);
        _ = HideWhenDone(Progress.AnimateTo(0, spec), generation);
    }

    private async Task HideWhenDone(Task<bool> task, int generation)
    {
        var reached = await task;
        if (reached && generation == _generation && !_present)
        {
            IsVisible = false;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Diameter, Diameter);

    public override void Render(DrawingContext context)
    {
        var p = _progress?.Value ?? (_present ? 1 : 0);
        var opacity = Math.Clamp(p, 0, 1);
        if (Fill is not { } fill || opacity <= 0.001)
        {
            return;
        }
        var scale = _fadeOnly ? 1 : 0.3 + 0.7 * p;
        var radius = Diameter / 2 * Math.Max(scale, 0);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        using (context.PushOpacity(opacity))
        {
            context.DrawEllipse(fill, null, center, radius, radius);
        }
    }
}

/// <summary>
/// A row title followed by its unread dot (Arc's <c>.itemTitle</c> flex row): the title takes its natural width, cut
/// with an ellipsis when the line is too narrow, and the dot sits right after it.
/// </summary>
internal sealed class NotificationCenterTitleLine : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<NotificationCenterTitleLine, double>(nameof(Spacing), 6);

    static NotificationCenterTitleLine()
    {
        AffectsMeasure<NotificationCenterTitleLine>(SpacingProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    private (Control? Title, Control? Dot) Parts => (Children.Count > 0 ? Children[0] : null, Children.Count > 1 ? Children[1] : null);

    protected override Size MeasureOverride(Size availableSize)
    {
        var (title, dot) = Parts;
        var dotWidth = 0.0;
        var height = 0.0;
        if (dot is { IsVisible: true })
        {
            dot.Measure(availableSize);
            dotWidth = dot.DesiredSize.Width + Spacing;
            height = dot.DesiredSize.Height;
        }
        var titleWidth = 0.0;
        if (title is not null)
        {
            title.Measure(availableSize.WithWidth(Math.Max(0, availableSize.Width - dotWidth)));
            titleWidth = title.DesiredSize.Width;
            height = Math.Max(height, title.DesiredSize.Height);
        }
        return new Size(titleWidth + dotWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (title, dot) = Parts;
        var x = 0.0;
        if (title is not null)
        {
            var width = Math.Min(title.DesiredSize.Width, finalSize.Width);
            title.Arrange(new Rect(0, (finalSize.Height - title.DesiredSize.Height) / 2, width, title.DesiredSize.Height));
            x = width + Spacing;
        }
        if (dot is { IsVisible: true })
        {
            var size = dot.DesiredSize;
            dot.Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The details of an open row: the longer text and the "Mark read" / "Dismiss" buttons. They are a control of their own so
/// that a row only pays for them the first time it opens (the two buttons are most of what a row costs to build).
/// </summary>
[TemplatePart(ReadTogglePart, typeof(NotificationCenterButton))]
[TemplatePart(ReadGlyphPart, typeof(ArcContentSwap))]
[TemplatePart(ReadLabelPart, typeof(ArcContentSwap))]
[TemplatePart(DismissPart, typeof(NotificationCenterButton))]
[PseudoClasses(NarrowPseudoClass)]
internal sealed class NotificationCenterDetails : Avalonia.Controls.Primitives.TemplatedControl
{
    public const string ReadTogglePart = "PART_ReadToggle";
    public const string ReadGlyphPart = "PART_ReadGlyph";
    public const string ReadLabelPart = "PART_ReadLabel";
    public const string DismissPart = "PART_Dismiss";
    public const string NarrowPseudoClass = ":narrow";

    public static readonly StyledProperty<string?> DetailTextProperty =
        AvaloniaProperty.Register<NotificationCenterDetails, string?>(nameof(DetailText));

    public static readonly StyledProperty<string?> DismissTextProperty =
        AvaloniaProperty.Register<NotificationCenterDetails, string?>(nameof(DismissText));

    static NotificationCenterDetails()
    {
        FocusableProperty.OverrideDefaultValue<NotificationCenterDetails>(false);
    }

    public string? DetailText { get => GetValue(DetailTextProperty); set => SetValue(DetailTextProperty, value); }

    public string? DismissText { get => GetValue(DismissTextProperty); set => SetValue(DismissTextProperty, value); }

    public NotificationCenterButton? ReadToggle { get; private set; }

    public ArcContentSwap? ReadGlyph { get; private set; }

    public ArcContentSwap? ReadLabel { get; private set; }

    public NotificationCenterButton? Dismiss { get; private set; }

    public void SetTexts(string? detail, string? dismiss)
    {
        DetailText = detail;
        DismissText = dismiss;
    }

    public void SetNarrow(bool narrow) => PseudoClasses.Set(NarrowPseudoClass, narrow);

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ReadToggle = e.NameScope.Find<NotificationCenterButton>(ReadTogglePart);
        ReadGlyph = e.NameScope.Find<ArcContentSwap>(ReadGlyphPart);
        ReadLabel = e.NameScope.Find<ArcContentSwap>(ReadLabelPart);
        Dismiss = e.NameScope.Find<NotificationCenterButton>(DismissPart);
    }
}
