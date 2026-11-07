using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One voice bubble: a 44px capsule with a round play button, the waveform and the time. Incoming bubbles are muted with a
/// dark play button; your own are dark with a light one (<c>:own</c>). The background is drawn here, so during the send
/// morph <see cref="Frame"/> can stretch and move it (with the content riding at its left end) without a layout pass.
/// </summary>
[PseudoClasses(OwnPseudoClass)]
internal sealed class VoiceRecorderBubble : Control
{
    public const string OwnPseudoClass = ":own";

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<VoiceRecorderBubble, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<VoiceRecorderBubble, IBrush?>(nameof(Ink));

    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<VoiceRecorderBubble, Thickness>(nameof(Padding), new Thickness(6, 6, 14, 6));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<VoiceRecorderBubble, double>(nameof(Gap), 10);

    public static readonly StyledProperty<double> TimeOpacityProperty =
        AvaloniaProperty.Register<VoiceRecorderBubble, double>(nameof(TimeOpacity), 0.8);

    private Rect? _frame;

    static VoiceRecorderBubble()
    {
        AffectsRender<VoiceRecorderBubble>(BackgroundProperty);
        AffectsMeasure<VoiceRecorderBubble>(PaddingProperty, GapProperty);
    }

    public VoiceRecorderBubble(VoiceRecorderMessage message)
    {
        Message = message;
        PseudoClasses.Set(OwnPseudoClass, !message.IsIncoming);
        Play = new VoiceRecorderButton(message.IsIncoming ? VoiceRecorderButtonKind.Play : VoiceRecorderButtonKind.PlayOwn)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        Bars = new VoiceRecorderBars { Peaks = message.Peaks, Height = 22, VerticalAlignment = VerticalAlignment.Center };
        Time = new TextBlock
        {
            Text = VoiceRecorderClock.Format(message.Duration.TotalSeconds),
            FontFeatures = s_tabular,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetAccessibilityView(Time, Avalonia.Automation.AccessibilityView.Raw);
        ArcTokens.Bind(Time, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        VisualChildren.Add(Play);
        VisualChildren.Add(Bars);
        VisualChildren.Add(Time);
        LogicalChildren.Add(Play);
        LogicalChildren.Add(Bars);
        LogicalChildren.Add(Time);
    }

    /// <summary>Used by the XAML loader only.</summary>
    public VoiceRecorderBubble()
        : this(new VoiceRecorderMessage())
    {
    }

    protected override Type StyleKeyOverride => typeof(VoiceRecorderBubble);

    public VoiceRecorderMessage Message { get; }

    public VoiceRecorderButton Play { get; }

    public VoiceRecorderBars Bars { get; }

    public TextBlock Time { get; }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>The bubble's content color: the waveform and the time.</summary>
    public IBrush? Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double TimeOpacity { get => GetValue(TimeOpacityProperty); set => SetValue(TimeOpacityProperty, value); }

    /// <summary>The playback position in seconds.</summary>
    public double Position { get; set; }

    /// <summary>Whether this bubble plays.</summary>
    public bool IsPlaying { get; set; }

    /// <summary>The second the time shows (to update the text once a second only).</summary>
    internal long ShownSecond { get; set; } = -1;

    /// <summary>
    /// Where the background is drawn, relative to the bubble's own box, while it morphs in (the content rides at its left
    /// end, centred vertically); null draws it over the box.
    /// </summary>
    public Rect? Frame
    {
        get => _frame;
        set
        {
            _frame = value;
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InkProperty)
        {
            Bars.Foreground = Ink;
            Time.Foreground = Ink;
        }
        else if (change.Property == TimeOpacityProperty)
        {
            Time.Opacity = TimeOpacity;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var pad = Padding;
        Play.Measure(Size.Infinity);
        Time.Measure(Size.Infinity);
        var fixedWidth = pad.Left + Play.DesiredSize.Width + Gap * 2 + Time.DesiredSize.Width + pad.Right;
        var barsRoom = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - fixedWidth);
        Bars.Measure(new Size(barsRoom, Bars.Height));
        var height = double.IsNaN(Height) ? 44 : Height;
        return new Size(fixedWidth + Bars.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pad = Padding;
        var frame = _frame ?? new Rect(finalSize);
        var x = frame.X + pad.Left;
        var midY = frame.Y + frame.Height / 2;
        var play = Play.DesiredSize;
        Play.Arrange(new Rect(x, midY - play.Height / 2, play.Width, play.Height));
        x += play.Width + Gap;
        var time = Time.DesiredSize;
        var barsWidth = Math.Max(0, finalSize.Width - pad.Left - play.Width - Gap * 2 - time.Width - pad.Right);
        var barsHeight = double.IsNaN(Bars.Height) ? 22 : Bars.Height;
        Bars.Arrange(new Rect(x, midY - barsHeight / 2, barsWidth, barsHeight));
        x += barsWidth + Gap;
        Time.Arrange(new Rect(x, midY - time.Height / 2, time.Width, time.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Background;
        if (brush is null)
        {
            return;
        }
        var rect = _frame ?? new Rect(Bounds.Size);
        var radius = rect.Height / 2;
        context.DrawRectangle(brush, null, rect, radius, radius);
    }
}

/// <summary>A sender's round avatar: their picture, or their initials on the muted surface.</summary>
internal sealed class VoiceRecorderAvatar : Border
{
    public VoiceRecorderAvatar(VoiceRecorderMessage message, double size)
    {
        Width = size;
        Height = size;
        ClipToBounds = true;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
        if (message.Avatar is { } image)
        {
            Child = new Image { Source = image, Stretch = Stretch.UniformToFill };
        }
        else if (Initials(message.From) is { Length: > 0 } initials)
        {
            var text = new TextBlock
            {
                Text = initials,
                FontSize = Math.Round(size * 0.39),
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            Child = text;
        }
    }

    /// <summary>The first letters of the first two words, upper-cased.</summary>
    internal static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = char.ToUpperInvariant(parts[0][0]).ToString();
        return parts.Length > 1 ? first + char.ToUpperInvariant(parts[1][0]) : first;
    }
}

/// <summary>
/// The composer's bar while recording or reviewing: a capsule drawn here with its content laid out at the settled width
/// and pinned to the left, clipped to the capsule, so it can stretch out of the mic (and its rows crossfade) without the
/// content reflowing. <see cref="Recording"/> and <see cref="Review"/> are the two rows.
/// </summary>
internal sealed class VoiceRecorderCapsule : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<VoiceRecorderCapsule, IBrush?>(nameof(Background));

    private readonly RectangleGeometry _clip = new();

    static VoiceRecorderCapsule()
    {
        AffectsRender<VoiceRecorderCapsule>(BackgroundProperty);
    }

    public VoiceRecorderCapsule(Control recording, Control review)
    {
        Recording = recording;
        Review = review;
        VisualChildren.Add(recording);
        VisualChildren.Add(review);
        LogicalChildren.Add(recording);
        LogicalChildren.Add(review);
    }

    public Control Recording { get; }

    public Control Review { get; }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>The settled width the rows are laid out at.</summary>
    public double ContentWidth { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(ContentWidth, availableSize.Height);
        Recording.Measure(size);
        Review.Measure(size);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(0, 0, Math.Max(ContentWidth, 0), finalSize.Height);
        Recording.Arrange(rect);
        Review.Arrange(rect);
        var radius = finalSize.Height / 2;
        _clip.Rect = new Rect(finalSize);
        _clip.RadiusX = radius;
        _clip.RadiusY = radius;
        // Clip only while the bar is narrower than its content (a morph): a clipped visual can miss hit tests on its
        // children, and at rest the content fits inside the rounded ends anyway.
        Clip = finalSize.Width < ContentWidth - 0.5 ? _clip : null;
        InvalidateVisual();
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Background is { } brush)
        {
            var radius = Bounds.Height / 2;
            context.DrawRectangle(brush, null, new Rect(Bounds.Size), radius, radius);
        }
    }
}

/// <summary>
/// The composer row, laid out by hand: the hint and the mic at rest; the trash, the bar and stop (or send) while recording
/// or reviewing. <see cref="MicFrame"/> and <see cref="CapsuleFrame"/> override where the mic and the bar sit while the
/// layout morph carries one into the other.
/// </summary>
internal sealed class VoiceRecorderComposer : Panel
{
    public VoiceRecorderComposer(TextBlock hint, VoiceRecorderButton discard, VoiceRecorderCapsule capsule, VoiceRecorderButton stop,
        VoiceRecorderButton send, VoiceRecorderButton mic)
    {
        Hint = hint;
        Discard = discard;
        Capsule = capsule;
        Stop = stop;
        Send = send;
        Mic = mic;
        Children.Add(hint);
        Children.Add(discard);
        Children.Add(capsule);
        Children.Add(stop);
        Children.Add(send);
        Children.Add(mic);
    }

    public TextBlock Hint { get; }

    public VoiceRecorderButton Discard { get; }

    public VoiceRecorderCapsule Capsule { get; }

    public VoiceRecorderButton Stop { get; }

    public VoiceRecorderButton Send { get; }

    public VoiceRecorderButton Mic { get; }

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<VoiceRecorderComposer, double>(nameof(RowHeight), 48);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<VoiceRecorderComposer, double>(nameof(Gap), 8);

    public static readonly StyledProperty<double> DiscardSizeProperty =
        AvaloniaProperty.Register<VoiceRecorderComposer, double>(nameof(DiscardSize), 40);

    static VoiceRecorderComposer()
    {
        AffectsMeasure<VoiceRecorderComposer>(RowHeightProperty, GapProperty, DiscardSizeProperty);
    }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double DiscardSize { get => GetValue(DiscardSizeProperty); set => SetValue(DiscardSizeProperty, value); }

    /// <summary>Where the mic sits while it morphs; null at rest.</summary>
    public Rect? MicFrame { get; set; }

    /// <summary>Where the bar sits while it morphs; null at rest.</summary>
    public Rect? CapsuleFrame { get; set; }

    /// <summary>The mic's place at rest in a row <paramref name="width"/> wide.</summary>
    public Rect MicRect(double width) => new(Math.Max(0, width - RowHeight), 0, RowHeight, RowHeight);

    /// <summary>The bar's place at rest: between the trash and stop (the row minus 104px at Arc's sizes).</summary>
    public Rect CapsuleRect(double width)
    {
        var left = DiscardSize + Gap;
        return new Rect(left, 0, Math.Max(0, width - left - Gap - RowHeight), RowHeight);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        Hint.Measure(new Size(Math.Max(0, width - RowHeight - Gap), RowHeight));
        Discard.Measure(new Size(DiscardSize, DiscardSize));
        Stop.Measure(new Size(RowHeight, RowHeight));
        Send.Measure(new Size(RowHeight, RowHeight));
        Mic.Measure(new Size(RowHeight, RowHeight));
        Capsule.ContentWidth = CapsuleRect(width).Width;
        Capsule.Measure(new Size(Capsule.ContentWidth, RowHeight));
        return new Size(double.IsInfinity(availableSize.Width) ? width : 0, RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var hintHeight = Hint.DesiredSize.Height;
        Hint.Arrange(new Rect(0, (RowHeight - hintHeight) / 2, Math.Max(0, width - RowHeight - Gap), hintHeight));
        Discard.Arrange(new Rect(0, (RowHeight - DiscardSize) / 2, DiscardSize, DiscardSize));
        var capsule = CapsuleRect(width);
        if (Math.Abs(Capsule.ContentWidth - capsule.Width) > 0.01)
        {
            Capsule.ContentWidth = capsule.Width;
            Capsule.InvalidateMeasure();
        }
        Capsule.Arrange(CapsuleFrame ?? capsule);
        Stop.Arrange(MicRect(width));
        Send.Arrange(MicRect(width));
        Mic.Arrange(MicFrame ?? MicRect(width));
        return new Size(width, RowHeight);
    }
}

/// <summary>
/// A part that comes and goes with Arc's presence motion: in with a spring (scale from <see cref="FromScale"/> and opacity
/// on one curve), out over 120ms ease-out, then hidden. Reduced motion keeps the opacity only (the spring becomes a tween of
/// about the same length, since springs jump at that level); Off is instant.
/// </summary>
internal sealed class VoiceRecorderPresence
{
    private static readonly Avalonia.Animation.Easings.Easing s_easeOut = new Avalonia.Animation.Easings.CubicEaseOut();

    private readonly Visual _target;
    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _value;

    public VoiceRecorderPresence(Visual target, double fromScale, SpringSpec enter, TimeSpan reducedEnter)
    {
        _target = target;
        FromScale = fromScale;
        Enter = enter;
        ReducedEnter = reducedEnter;
        target.RenderTransform = _scale;
        target.RenderTransformOrigin = RelativePoint.Center;
    }

    public double FromScale { get; }

    public SpringSpec Enter { get; }

    public TimeSpan ReducedEnter { get; }

    /// <summary>Whether the part is wanted (it may still be leaving).</summary>
    public bool IsShown { get; private set; }

    /// <summary>The presence (0 gone, 1 in place; tests).</summary>
    public double Value => _value?.Value ?? (IsShown ? 1 : 0);

    /// <summary>Puts the part in place without motion.</summary>
    public void Set(bool shown)
    {
        IsShown = shown;
        Motion().Jump(shown ? 1 : 0);
        _target.IsVisible = shown;
    }

    public void Show()
    {
        if (IsShown)
        {
            return;
        }
        IsShown = true;
        _target.IsVisible = true;
        var motion = Motion();
        var level = ArcMotion.GetEffectiveLevel(_target);
        if (level == ArcMotionLevel.Off)
        {
            motion.Jump(1);
            return;
        }
        if (!motion.IsAnimating && motion.Value <= 0.001)
        {
            motion.Jump(0);
        }
        _ = level == ArcMotionLevel.Full
            ? motion.AnimateTo(1, MotionSpec.Spring(Enter))
            : motion.AnimateTo(1, MotionSpec.Tween(ReducedEnter, ArcEasings.Enter));
    }

    public async void Hide()
    {
        if (!IsShown)
        {
            return;
        }
        IsShown = false;
        var motion = Motion();
        if (ArcMotion.GetEffectiveLevel(_target) == ArcMotionLevel.Off)
        {
            motion.Jump(0);
            _target.IsVisible = false;
            return;
        }
        await motion.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), s_easeOut));
        if (!IsShown)
        {
            _target.IsVisible = false;
        }
    }

    private MotionValue<double> Motion()
    {
        if (_value is not null)
        {
            return _value;
        }
        _value = new MotionValue<double>(_target, IsShown ? 1 : 0, MotionVectors.Double);
        _value.Changed += Apply;
        return _value;
    }

    private void Apply(double value)
    {
        _target.Opacity = Math.Clamp(value, 0, 1);
        var scale = ArcMotion.IsFull(_target) ? FromScale + (1 - FromScale) * value : 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
    }
}

/// <summary>
/// A part that fades with a 2px blur: out over 120ms (exit curve), in over 240ms (standard curve) after 50ms (no delay
/// under reduced motion, as measured). The hint and the bar's two rows use it.
/// </summary>
internal sealed class VoiceRecorderBlurFade
{
    public static readonly TimeSpan Out = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan In = TimeSpan.FromMilliseconds(240);
    public static readonly TimeSpan InDelay = TimeSpan.FromMilliseconds(50);
    public const double Blur = 2;

    private readonly Visual _target;
    private MotionValue<double>? _value;

    public VoiceRecorderBlurFade(Visual target)
    {
        _target = target;
    }

    public bool IsShown { get; private set; } = true;

    public double Value => _value?.Value ?? (IsShown ? 1 : 0);

    public void Set(bool shown)
    {
        IsShown = shown;
        Motion().Jump(shown ? 1 : 0);
        _target.IsVisible = shown;
    }

    public void Show()
    {
        if (IsShown)
        {
            return;
        }
        IsShown = true;
        _target.IsVisible = true;
        var motion = Motion();
        var level = ArcMotion.GetEffectiveLevel(_target);
        if (level == ArcMotionLevel.Off)
        {
            motion.Jump(1);
            return;
        }
        _ = motion.AnimateTo(1, MotionSpec.Tween(In, ArcEasings.Standard, level == ArcMotionLevel.Full ? InDelay : TimeSpan.Zero));
    }

    public async void Hide()
    {
        if (!IsShown)
        {
            return;
        }
        IsShown = false;
        var motion = Motion();
        if (ArcMotion.GetEffectiveLevel(_target) == ArcMotionLevel.Off)
        {
            motion.Jump(0);
            _target.IsVisible = false;
            return;
        }
        await motion.AnimateTo(0, MotionSpec.Tween(Out, ArcEasings.Exit));
        if (!IsShown)
        {
            _target.IsVisible = false;
        }
    }

    private MotionValue<double> Motion()
    {
        if (_value is not null)
        {
            return _value;
        }
        _value = new MotionValue<double>(_target, IsShown ? 1 : 0, MotionVectors.Double);
        _value.Changed += value =>
        {
            _target.Opacity = Math.Clamp(value, 0, 1);
            if (_target is Control control)
            {
                control.Effect = ArcBlur.Get(Blur * (1 - Math.Clamp(value, 0, 1)));
            }
        };
        return _value;
    }
}
