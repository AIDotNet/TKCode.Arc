using Avalonia;
using Avalonia.Animation.Easings;
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
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>Motion numbers of <see cref="TagInput"/> taken from <c>tag-input.tsx</c> and <c>motionTokens</c>.</summary>
internal static class TagInputMotion
{
    /// <summary>motion's default tween curve (<c>easeInOut</c>), used where Arc gives a duration without an ease.</summary>
    public static readonly Easing DefaultEase = new SplineEasing(0.42, 0, 0.58, 1);

    public static readonly Easing Linear = new LinearEasing();

    /// <summary><c>motionTokens.blur.soft</c> / <c>.subtle</c>.</summary>
    public const double SoftBlur = 4;
    public const double SubtleBlur = 2;

    /// <summary>Tags and the pick ring enter from and leave to 0.9.</summary>
    public const double PresenceScale = 0.9;

    /// <summary>Duplicate pulse: <c>scale: [1, 1.06, 1]</c> over standard + instant on the in-out ease.</summary>
    public const double PulsePeak = 1.06;
    public static readonly TimeSpan PulseDuration = ArcMotionPresets.Standard + ArcMotionPresets.Instant;

    /// <summary>The placeholder rises <c>0.3em</c>.</summary>
    public const double PlaceholderRise = 0.3;

    /// <summary>Refused add: Arc's ScrubInput shake, <c>physical(.3, .42)</c> released with -260 px/s.</summary>
    public static readonly SpringSpec Shake = SpringSpec.FromVisual(0.3, 0.42);
    public const double ShakeVelocity = -260;

    /// <summary>Keyframes <c>[1, 1.06, 1]</c>: each half eases in-out (motion applies one ease per segment).</summary>
    public static double PulseScaleAt(double progress)
    {
        var p = Math.Clamp(progress, 0, 1);
        return p < 0.5
            ? 1 + (PulsePeak - 1) * ArcEasings.InOut.Ease(p * 2)
            : PulsePeak - (PulsePeak - 1) * ArcEasings.InOut.Ease((p - 0.5) * 2);
    }

    public static bool IsBlurVisible(double radius) => radius >= 0.05;
}

/// <summary>
/// The pill behind a tag (<c>SurfaceMuted</c> fill, 1px <c>Border</c>). It is a sibling of the tag's label and button so
/// the pick ring can ride between them, exactly like Arc's ring sits above the fills but below the labels.
/// </summary>
internal sealed class TagInputChipFill : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<TagInputChipFill, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<TagInputChipFill, IBrush?>(nameof(BorderBrush));

    private Pen? _pen;
    private IBrush? _penBrush;

    static TagInputChipFill()
    {
        AffectsRender<TagInputChipFill>(BackgroundProperty, BorderBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TagInputChipFill>(false);
        FocusableProperty.OverrideDefaultValue<TagInputChipFill>(false);
    }

    public TagInputChipFill()
    {
        ScaleTransform = new ScaleTransform(1, 1);
        ShiftTransform = new TranslateTransform();
        RenderTransform = new TransformGroup { Children = { ScaleTransform, ShiftTransform } };
        ZIndex = 0;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    internal ScaleTransform ScaleTransform { get; }

    internal TranslateTransform ShiftTransform { get; }

    internal BlurEffect? Blur { get; set; }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 1 || rect.Height <= 1)
        {
            return;
        }
        var border = BorderBrush;
        if (border is not null && !ReferenceEquals(border, _penBrush))
        {
            _pen = new Pen(border, 1);
            _penBrush = border;
        }
        var inner = rect.Deflate(0.5);
        var radius = inner.Height / 2;
        context.DrawRectangle(Background, border is null ? null : _pen, inner, radius, radius);
    }
}

/// <summary>
/// The pick ring: a 1px <c>Accent</c> pill on <c>AccentSubtle</c> sized to the picked tag's resting box. It glides between
/// picks on the morph spring (opacity fast), first appears in place from 0.9 on snappy, and fades with the tag it leaves.
/// Drawn in <see cref="Render"/> so the glide never relayouts.
/// </summary>
internal sealed class TagInputPickRing : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<TagInputPickRing, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<TagInputPickRing, IBrush?>(nameof(BorderBrush));

    private MotionValue<Rect>? _box;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _scale;
    private Pen? _pen;
    private IBrush? _penBrush;

    static TagInputPickRing()
    {
        AffectsRender<TagInputPickRing>(BackgroundProperty, BorderBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TagInputPickRing>(false);
        FocusableProperty.OverrideDefaultValue<TagInputPickRing>(false);
    }

    public TagInputPickRing()
    {
        ZIndex = 1;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>The box the ring rests on, or null while no tag is picked.</summary>
    internal Rect? At { get; private set; }

    internal Rect Box => _box?.Value ?? default;

    internal double RingOpacity => _opacity?.Value ?? 0;

    internal double RingScale => _scale?.Value ?? 1;

    private void Ensure()
    {
        if (_box is not null)
        {
            return;
        }
        _box = new MotionValue<Rect>(this, default, MotionVectors.Rect);
        _box.Changed += _ => InvalidateVisual();
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity.Changed += _ => InvalidateVisual();
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _scale.Changed += _ => InvalidateVisual();
    }

    /// <summary>
    /// Moves the ring onto <paramref name="box"/> (Arc's layout effect on every pick). With <paramref name="follow"/> a
    /// ring that is already shown moves at once (its tag moved because the column was resized).
    /// </summary>
    public void ShowAt(Rect box, ArcMotionLevel level, bool follow = false)
    {
        if (At is { } at && at == box)
        {
            return;
        }
        Ensure();
        if (follow && At is not null)
        {
            _box!.Jump(box);
            At = box;
            return;
        }
        var reduced = level != ArcMotionLevel.Full;
        // A ring that is still fading from the last pick glides on to the next one, so quick Backspaces read as one motion.
        if (!reduced && (At is not null || _opacity!.Value > 0.02))
        {
            _ = _box!.AnimateTo(box, MotionSpec.Spring(ArcSpring.Morph));
            _ = _scale!.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, TagInputMotion.DefaultEase));
        }
        else
        {
            _box!.Jump(box);
            _opacity!.Jump(0);
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(reduced ? ArcMotionPresets.Instant : ArcMotionPresets.Fast, TagInputMotion.DefaultEase));
            _scale!.Jump(reduced ? 1 : TagInputMotion.PresenceScale);
            _ = _scale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        }
        At = box;
    }

    /// <summary>Fades the ring out where it is (the curve of a removed tag, so a picked tag and its ring leave as one).</summary>
    public void Clear(ArcMotionLevel level)
    {
        if (At is null)
        {
            return;
        }
        At = null;
        Ensure();
        if (level != ArcMotionLevel.Full)
        {
            _opacity!.Jump(0);
            return;
        }
        var leave = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        _ = _opacity!.AnimateTo(0, leave);
        _ = _scale!.AnimateTo(TagInputMotion.PresenceScale, leave);
    }

    public override void Render(DrawingContext context)
    {
        var opacity = RingOpacity;
        var box = Box;
        if (opacity <= 0.001 || box.Width <= 1 || box.Height <= 1)
        {
            return;
        }
        var border = BorderBrush;
        if (border is not null && !ReferenceEquals(border, _penBrush))
        {
            _pen = new Pen(border, 1);
            _penBrush = border;
        }
        var scale = RingScale;
        var center = box.Center;
        var transform = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                        Matrix.CreateTranslation(center.X, center.Y);
        var inner = box.Deflate(0.5);
        var radius = inner.Height / 2;
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        using (context.PushTransform(transform))
        {
            context.DrawRectangle(Background, border is null ? null : _pen, inner, radius, radius);
        }
    }
}

/// <summary>
/// One tag's label and remove button (the fill is <see cref="Fill"/>, a sibling below the pick ring). The label is
/// drawn from a cached <see cref="TextLayout"/>; the only child is the remove button. Enter, exit, the duplicate pulse
/// and the FLIP glide are driven by <see cref="MotionValue{T}"/> because they are one-shot, interruptible motions
/// started from code (AnimatePresence and layout animation), which a ControlTheme transition cannot express.
/// </summary>
internal sealed class TagInputChip : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<TagInputChip>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<TagInputChip>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<TagInputChip>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<TagInputChip>();

    /// <summary>Arc <c>padding: 0 4px 0 9px</c> inside the 1px border.</summary>
    public static readonly StyledProperty<Thickness> InsetProperty =
        AvaloniaProperty.Register<TagInputChip, Thickness>(nameof(Inset), new Thickness(9, 0, 4, 0));

    /// <summary>Arc <c>gap: 3px</c>.</summary>
    public static readonly StyledProperty<double> LabelGapProperty =
        AvaloniaProperty.Register<TagInputChip, double>(nameof(LabelGap), 3);

    private const double BorderWidth = 1;

    private readonly TagInputPanel _panel;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _shift = new();
    private TextLayout? _layout;
    private double _layoutWidth = double.NaN;
    private MotionValue<Vector>? _flip;
    private MotionValue<double>? _grow;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _blur;
    private MotionValue<double>? _pulse;
    private BlurEffect? _blurEffect;
    private double _presenceScale = 1;
    private double _pulseScale = 1;
    private bool _pressed;

    static TagInputChip()
    {
        AffectsMeasure<TagInputChip>(InsetProperty, LabelGapProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<TagInputChip>(ForegroundProperty);
        FocusableProperty.OverrideDefaultValue<TagInputChip>(false);
    }

    public TagInputChip(TagInputPanel panel, string text)
    {
        _panel = panel;
        TagText = text;
        Fill = new TagInputChipFill();
        RemoveButton = new TagInputRemoveButton();
        RemoveButton.Click += OnRemoveClick;
        VisualChildren.Add(RemoveButton);
        LogicalChildren.Add(RemoveButton);
        RenderTransform = new TransformGroup { Children = { _scale, _shift } };
        ZIndex = 2;
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public Thickness Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    public double LabelGap { get => GetValue(LabelGapProperty); set => SetValue(LabelGapProperty, value); }

    /// <summary>The tag this chip shows.</summary>
    public string TagText { get; }

    public TagInputChipFill Fill { get; }

    public TagInputRemoveButton RemoveButton { get; }

    /// <summary>True from the moment the tag is removed until its exit finishes (it no longer holds a slot).</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>Resting layout position (no FLIP offset), for the next FLIP and the pick ring.</summary>
    public Point? LastPosition { get; set; }

    public Rect LastRect { get; set; }

    public bool IsPicked
    {
        get => RemoveButton.IsPicked;
        set => RemoveButton.IsPicked = value;
    }

    internal double PresenceScale => _presenceScale;

    internal double PulseScale => _pulseScale;

    internal Vector FlipOffset => _flip?.Value ?? default;

    internal double BlurRadius => _blur?.Value ?? 0;

    internal bool IsPulsing => _pulse?.IsAnimating == true;

    // ---------------------------------------------------------------- layout and drawing

    private TextLayout EnsureLayout(double maxWidth)
    {
        if (_layout is null || !SameWidth(_layoutWidth, maxWidth))
        {
            _layout?.Dispose();
            var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
            _layout = new TextLayout(TagText, typeface, FontSize, Foreground, TextAlignment.Left, TextWrapping.NoWrap,
                TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity,
                double.NaN, 0, 1);
            _layoutWidth = maxWidth;
        }
        return _layout;
    }

    private static bool SameWidth(double a, double b) => a.Equals(b) || Math.Abs(a - b) < 0.01;

    private void ResetLayout()
    {
        _layout?.Dispose();
        _layout = null;
        _layoutWidth = double.NaN;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty ||
            change.Property == FontSizeProperty || change.Property == FontWeightProperty)
        {
            ResetLayout();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        RemoveButton.Measure(Size.Infinity);
        var button = RemoveButton.DesiredSize;
        var inset = Inset;
        var chrome = 2 * BorderWidth + inset.Left + inset.Right + LabelGap + button.Width;
        var maxLabel = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width - chrome) : double.PositiveInfinity;
        var layout = EnsureLayout(maxLabel);
        var height = Math.Max(layout.Height, button.Height) + 2 * BorderWidth + inset.Top + inset.Bottom;
        return new Size(chrome + layout.WidthIncludingTrailingWhitespace, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var button = RemoveButton.DesiredSize;
        RemoveButton.Arrange(new Rect(finalSize.Width - BorderWidth - Inset.Right - button.Width,
            (finalSize.Height - button.Height) / 2, button.Width, button.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // The whole pill takes the press (Arc: the tag's own click), not only the glyphs.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (_layout is not { } layout)
        {
            return;
        }
        layout.Draw(context, new Point(BorderWidth + Inset.Left, (Bounds.Height - layout.Height) / 2));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Settle transient motion so a re-attached tag rests in place at its normal size.
        _flip?.Jump(default);
        _pulse?.Jump(1);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TagInputChipAutomationPeer(this);

    // ---------------------------------------------------------------- input

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!IsLeaving)
        {
            _panel.OnRemoveRequested(this);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || IsLeaving || IsInRemoveButton(e.Source))
        {
            return;
        }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // A press on a tag never takes focus from the field (Arc prevents the mousedown); the release picks it.
        e.Handled = true;
        _pressed = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        e.Pointer.Capture(null);
        e.Handled = true;
        if (inside && !IsLeaving)
        {
            _panel.OnChipClicked(this);
        }
        else
        {
            _panel.OnChipPressCancelled();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressed)
        {
            _pressed = false;
            _panel.OnChipPressCancelled();
        }
    }

    internal bool IsInRemoveButton(object? source) =>
        source is Visual visual && (ReferenceEquals(visual, RemoveButton) || RemoveButton.IsVisualAncestorOf(visual));

    // ---------------------------------------------------------------- motion

    private void EnsureMotion()
    {
        if (_grow is not null)
        {
            return;
        }
        _grow = new MotionValue<double>(this, 1, MotionVectors.Double);
        _grow.Changed += v =>
        {
            _presenceScale = v;
            ApplyScale();
        };
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += ApplyOpacity;
        _blur = new MotionValue<double>(this, 0, MotionVectors.Double);
        _blur.Changed += ApplyBlur;
    }

    /// <summary>
    /// Enters where the tag was typed: opacity fast + enter ease, scale 0.9 on the morph spring, a soft blur clearing on
    /// standard + enter ease. Reduced: a fade on instant. Off: at once.
    /// </summary>
    public void Appear(ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        EnsureMotion();
        _fade!.Jump(0);
        if (level == ArcMotionLevel.Reduced)
        {
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, TagInputMotion.DefaultEase));
            return;
        }
        _grow!.Jump(TagInputMotion.PresenceScale);
        _blur!.Jump(TagInputMotion.SoftBlur);
        _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        _ = _grow.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
        _ = _blur.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    /// <summary>
    /// Leaves without holding a slot (popLayout): opacity 0, scale 0.9 and a subtle blur on instant + standard ease.
    /// Reduced and Off: gone at once (Arc's reduced exit has no duration).
    /// </summary>
    public async void Leave(ArcMotionLevel level, Action done)
    {
        IsLeaving = true;
        IsHitTestVisible = false;
        RemoveButton.IsTabStop = false;
        RemoveButton.Focusable = false;
        IsPicked = false;
        if (level != ArcMotionLevel.Full || !IsLoaded)
        {
            done();
            return;
        }
        EnsureMotion();
        var leave = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        _ = _grow!.AnimateTo(TagInputMotion.PresenceScale, leave);
        _ = _blur!.AnimateTo(TagInputMotion.SubtleBlur, leave);
        try
        {
            await _fade!.AnimateTo(0, leave);
        }
        finally
        {
            done();
        }
    }

    /// <summary>A duplicate answers with one pulse (1 → 1.06 → 1, 360ms in-out). Skipped below full motion.</summary>
    public void Pulse()
    {
        if (!IsLoaded || !ArcMotion.IsFull(_panel))
        {
            return;
        }
        if (_pulse is null)
        {
            _pulse = new MotionValue<double>(this, 0, MotionVectors.Double);
            _pulse.Changed += p =>
            {
                _pulseScale = TagInputMotion.PulseScaleAt(p);
                ApplyScale();
            };
        }
        _pulse.Jump(0);
        _ = _pulse.AnimateTo(1, MotionSpec.Tween(TagInputMotion.PulseDuration, TagInputMotion.Linear));
    }

    /// <summary>FLIP: the slot moved by <paramref name="delta"/>; the tag starts where it was and glides home on morph.</summary>
    public void FlipBy(Vector delta)
    {
        if (_flip is null)
        {
            _flip = new MotionValue<Vector>(this, default, MotionVectors.Vector);
            _flip.Changed += ApplyShift;
        }
        var velocity = _flip.Velocity;
        _flip.Jump(_flip.Value + delta);
        _ = _flip.AnimateTo(default, MotionSpec.Spring(ArcSpring.Morph), velocity);
    }

    public void ResetFlip() => _flip?.Jump(default);

    private void ApplyScale()
    {
        var s = _presenceScale * _pulseScale;
        _scale.ScaleX = s;
        _scale.ScaleY = s;
        Fill.ScaleTransform.ScaleX = s;
        Fill.ScaleTransform.ScaleY = s;
    }

    private void ApplyShift(Vector offset)
    {
        _shift.X = offset.X;
        _shift.Y = offset.Y;
        Fill.ShiftTransform.X = offset.X;
        Fill.ShiftTransform.Y = offset.Y;
    }

    private void ApplyOpacity(double value)
    {
        var o = Math.Clamp(value, 0, 1);
        Opacity = o;
        Fill.Opacity = o;
    }

    private void ApplyBlur(double radius)
    {
        if (!TagInputMotion.IsBlurVisible(radius))
        {
            Effect = null;
            Fill.Effect = null;
            return;
        }
        _blurEffect ??= new BlurEffect();
        _blurEffect.Radius = radius;
        Effect = _blurEffect;
        Fill.Blur ??= new BlurEffect();
        Fill.Blur.Radius = radius;
        Fill.Effect = Fill.Blur;
    }

    /// <summary>The tag reads as plain text; its remove button is the only control inside.</summary>
    private sealed class TagInputChipAutomationPeer : ControlAutomationPeer
    {
        public TagInputChipAutomationPeer(TagInputChip owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetNameCore() => ((TagInputChip)Owner).TagText;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The 22px round remove button of a tag: Lucide X 14 in <c>TextSecondary</c>; a mouse hover (never touch) fills it with
/// <c>Surface</c> and darkens the glyph; focus-visible adds an inset 1.5px <c>FocusRing</c>; the picked tag's glyph is
/// <c>Foreground</c>. The press is code-driven so it can be quick (instant) and release on Arc's CSS spring: a
/// ControlTheme transition has one curve for both directions.
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(HoveredPseudoClass, PickedPseudoClass)]
internal sealed class TagInputRemoveButton : AvaloniaButton
{
    public const string HoveredPseudoClass = ":hovered";
    public const string PickedPseudoClass = ":picked";

    public static readonly StyledProperty<double> PressScaleProperty =
        AvaloniaProperty.Register<TagInputRemoveButton, double>(nameof(PressScale), 0.96);

    private readonly ScaleTransform _press = new(1, 1);
    private MotionValue<double>? _scale;
    private bool _picked;

    public TagInputRemoveButton()
    {
        RenderTransform = _press;
    }

    public double PressScale { get => GetValue(PressScaleProperty); set => SetValue(PressScaleProperty, value); }

    public bool IsPicked
    {
        get => _picked;
        set
        {
            _picked = value;
            PseudoClasses.Set(PickedPseudoClass, value);
        }
    }

    internal double CurrentScale => _press.ScaleX;

    protected override Type StyleKeyOverride => typeof(TagInputRemoveButton);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            PseudoClasses.Set(HoveredPseudoClass, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoveredPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPressedProperty)
        {
            UpdatePress();
        }
    }

    private void UpdatePress()
    {
        // Reduced motion: no transform at all (Arc: `.tag button:active { transform: none }`).
        if (!ArcMotion.IsFull(this))
        {
            _scale?.Jump(1);
            _press.ScaleX = 1;
            _press.ScaleY = 1;
            return;
        }
        if (_scale is null)
        {
            _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
            _scale.Changed += s =>
            {
                _press.ScaleX = s;
                _press.ScaleY = s;
            };
        }
        _ = IsPressed
            ? _scale.AnimateTo(PressScale, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard))
            : _scale.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
    }
}

/// <summary>
/// The field's own text editor: transparent, no chrome or focus adorner, no visible placeholder (the overlay draws it).
/// Keys, the comma and pastes go to the owning <see cref="TagInput"/> first.
/// </summary>
internal sealed class TagInputEditor : AvaloniaTextBox
{
    private bool _bypassPaste;

    public TagInputEditor()
    {
        Shift = new TranslateTransform();
        RenderTransform = Shift;
        ZIndex = 2;
        AddHandler(PastingFromClipboardEvent, OnPastingFromClipboard);
    }

    internal TagInput? Owner { get; set; }

    internal TranslateTransform Shift { get; }

    protected override Type StyleKeyOverride => typeof(TagInputEditor);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Owner is { } owner && owner.HandleEditorKey(e))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (Owner is { } owner && !string.IsNullOrEmpty(e.Text) && owner.HandleEditorTextInput(e.Text))
        {
            e.Handled = true;
            return;
        }
        base.OnTextInput(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnEditorLostFocus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Owner?.OnDraftChanged();
        }
    }

    /// <summary>
    /// A paste with commas, newlines or tabs becomes several tags. The native paste would drop everything after the
    /// first line, so the clipboard text is read here first; a paste without separators goes on as a native paste.
    /// </summary>
    private async void OnPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        if (_bypassPaste)
        {
            _bypassPaste = false;
            return;
        }
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (Owner is null || clipboard is null)
        {
            return;
        }
        e.Handled = true;
        string? text = null;
        try
        {
            text = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard);
        }
        catch (Exception)
        {
            text = null;
        }
        if (string.IsNullOrEmpty(text) || Owner is null)
        {
            return;
        }
        if (Owner.HandlePaste(text))
        {
            return;
        }
        _bypassPaste = true;
        Paste();
    }
}
