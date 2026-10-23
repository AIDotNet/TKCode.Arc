// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): the pill, the input, the button and the links of newsletter-signup.module.css.
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The pill: one rounded field that holds the input and the button. Its border follows the state (hover, focus inside,
/// invalid, done). In a form narrower than 300 the pill loses its frame and the input and button stack, so the colors move
/// to the input (see <see cref="NewsletterEmailBox"/>). <see cref="Shake"/> swings it sideways for an error.
/// </summary>
[PseudoClasses(StackedPseudoClass, InvalidPseudoClass, DonePseudoClass)]
internal sealed class NewsletterPill : Border
{
    public const string StackedPseudoClass = ":stacked";
    public const string InvalidPseudoClass = ":invalid";
    public const string DonePseudoClass = ":done";

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<NewsletterPill, bool>(nameof(IsStacked));

    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<NewsletterPill, bool>(nameof(IsInvalid));

    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<NewsletterPill, bool>(nameof(IsDone));

    private static readonly Easing ShakeEase = new SplineEasing(0, 0, 0.58, 1);
    private static readonly double[] ShakeKeys = { 0, -6, 5, -3, 1, 0 };

    private readonly TranslateTransform _shake = new();
    private MotionValue<double>? _shakeProgress;

    public NewsletterPill()
    {
        RenderTransform = _shake;
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <summary>The horizontal offset of the shake right now (tests read it).</summary>
    public double ShakeOffset => _shake.X;

    /// <summary>True while the shake runs.</summary>
    public bool IsShaking => _shakeProgress?.IsAnimating == true;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStackedProperty)
        {
            PseudoClasses.Set(StackedPseudoClass, IsStacked);
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == IsDoneProperty)
        {
            PseudoClasses.Set(DonePseudoClass, IsDone);
        }
    }

    /// <summary>
    /// Swings the pill: x 0, −6, 5, −3, 1, 0 over 360 ms, each leg eased out. Only at full motion.
    /// </summary>
    /// <remarks>Code-driven: Avalonia style animations cannot target <c>RenderTransform</c> keyframes, and the legs share one clock.</remarks>
    public void Shake()
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        if (_shakeProgress is null)
        {
            _shakeProgress = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shakeProgress.Changed += p =>
            {
                var scaled = Math.Clamp(p, 0, 1) * (ShakeKeys.Length - 1);
                var leg = Math.Min((int)scaled, ShakeKeys.Length - 2);
                var t = ShakeEase.Ease(scaled - leg);
                _shake.X = ShakeKeys[leg] + (ShakeKeys[leg + 1] - ShakeKeys[leg]) * t;
            };
        }
        _shakeProgress.Jump(0);
        _ = _shakeProgress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(360), new LinearEasing()));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _shakeProgress?.Stop();
        _shake.X = 0;
    }
}

/// <summary>
/// The email field: borderless text on a transparent background with a muted placeholder. The pill around it carries the
/// frame; once the form stacks (<see cref="IsStacked"/>) the field draws its own 52 px pill with the border colors.
/// Enter and the input method hints are handled by the owner.
/// </summary>
[TemplatePart(TextPresenterPart, typeof(TextPresenter))]
[PseudoClasses(StackedPseudoClass, InvalidPseudoClass, DonePseudoClass)]
internal sealed class NewsletterEmailBox : AvaloniaTextBox
{
    public const string TextPresenterPart = "PART_TextPresenter";
    public const string StackedPseudoClass = ":stacked";
    public const string InvalidPseudoClass = ":invalid";
    public const string DonePseudoClass = ":done";

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<NewsletterEmailBox, bool>(nameof(IsStacked));

    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<NewsletterEmailBox, bool>(nameof(IsInvalid));

    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<NewsletterEmailBox, bool>(nameof(IsDone));

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <summary>The presenter, whose pre-edit text tells whether an input method is composing.</summary>
    public TextPresenter? Presenter { get; private set; }

    /// <summary>True while an input method is composing text (Enter belongs to it then).</summary>
    public bool IsComposing => !string.IsNullOrEmpty(Presenter?.PreeditText);

    protected override Type StyleKeyOverride => typeof(NewsletterEmailBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Presenter = e.NameScope.Find<TextPresenter>(TextPresenterPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStackedProperty)
        {
            PseudoClasses.Set(StackedPseudoClass, IsStacked);
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == IsDoneProperty)
        {
            PseudoClasses.Set(DonePseudoClass, IsDone);
        }
    }
}

/// <summary>Which of the button's four labels shows.</summary>
internal enum NewsletterButtonKey
{
    /// <summary>"Subscribe" and an arrow.</summary>
    Idle,

    /// <summary>"Try again" and an arrow (after a failure).</summary>
    Retry,

    /// <summary>A loading ring and "Subscribing".</summary>
    Sending,

    /// <summary>A check mark that draws itself and "Subscribed".</summary>
    Done
}

/// <summary>
/// A 14 px ring in the current text color: a 30% track and a quarter arc that turns once every 700 ms (1.6 s under reduced
/// motion). Turns only while attached, visible and in view; static when motion is off.
/// </summary>
internal sealed class NewsletterSpinner : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<NewsletterSpinner>();

    private readonly LiveLoop _loop;
    private double _turns;
    private Pen? _track;
    private Pen? _arc;
    private IBrush? _penBrush;
    private StreamGeometry? _arcGeometry;

    static NewsletterSpinner()
    {
        AffectsRender<NewsletterSpinner>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<NewsletterSpinner>(false);
        WidthProperty.OverrideDefaultValue<NewsletterSpinner>(14);
        HeightProperty.OverrideDefaultValue<NewsletterSpinner>(14);
    }

    public NewsletterSpinner()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        // A looping animation must pause when unseen: a Core LiveLoop, which also runs under reduced motion (slower).
        _loop = new LiveLoop(this, dt =>
        {
            var period = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full ? 0.7 : 1.6;
            _turns = (_turns + dt / period) % 1;
            InvalidateVisual();
            return true;
        })
        {
            MinimumLevel = ArcMotionLevel.Reduced
        };
        _loop.Start();
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>True while frames are being delivered (tests read it).</summary>
    public bool IsSpinning => _loop.IsRunning;

    /// <summary>The turn of the ring, 0 to 1.</summary>
    public double Turns => _turns;

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null)
        {
            return;
        }
        if (_track is null || !ReferenceEquals(_penBrush, brush))
        {
            var color = brush is ISolidColorBrush solid ? solid.Color : Colors.Black;
            var faint = new ImmutableSolidColorBrush(Color.FromArgb((byte)(color.A * 0.3), color.R, color.G, color.B));
            _track = new Pen(faint, 2);
            _arc = new Pen(brush, 2);
            _penBrush = brush;
        }
        var radius = (Math.Min(Bounds.Width, Bounds.Height) - 2) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(null, _track, center, radius, radius);
        _arcGeometry ??= BuildArc(radius);
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(_turns * 2 * Math.PI) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, _arc, _arcGeometry);
        }
    }

    private StreamGeometry BuildArc(double radius)
    {
        // The top quarter, from 10:30 to 1:30 o'clock: what CSS paints for border-top-color on a round box.
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var start = new Point(center.X - radius * Math.Sin(Math.PI / 4), center.Y - radius * Math.Cos(Math.PI / 4));
        var end = new Point(center.X + radius * Math.Sin(Math.PI / 4), center.Y - radius * Math.Cos(Math.PI / 4));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        return geometry;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _arcGeometry = null;
    }
}

/// <summary>
/// The submit button. Four labels share the button's one cell, unseen, so its width is the widest of them and never changes
/// with the state; the visible one swaps in place (<see cref="SiteSwap"/>). Idle and retry show the text and an arrow,
/// sending a ring, done a check mark that draws itself (340 ms, 120 ms after the swap starts). Pressing scales it to .97 on
/// the Snappy spring, except while sending or done.
/// </summary>
[TemplatePart(LabelsPart, typeof(Grid))]
[TemplatePart(SwapPart, typeof(SiteSwap))]
[PseudoClasses(IdlePseudoClass, RetryPseudoClass, SendingPseudoClass, DonePseudoClass)]
internal sealed class NewsletterSubmitButton : AvaloniaButton
{
    public const string LabelsPart = "PART_Labels";
    public const string SwapPart = "PART_Swap";
    public const string IdlePseudoClass = ":idle";
    public const string RetryPseudoClass = ":retry";
    public const string SendingPseudoClass = ":sending";
    public const string DonePseudoClass = ":done";

    public static readonly StyledProperty<NewsletterButtonKey> StateProperty =
        AvaloniaProperty.Register<NewsletterSubmitButton, NewsletterButtonKey>(nameof(State));

    public static readonly StyledProperty<string> IdleLabelProperty =
        AvaloniaProperty.Register<NewsletterSubmitButton, string>(nameof(IdleLabel), "Subscribe");

    public static readonly StyledProperty<string> RetryLabelProperty =
        AvaloniaProperty.Register<NewsletterSubmitButton, string>(nameof(RetryLabel), "Try again");

    public static readonly StyledProperty<string> SendingLabelProperty =
        AvaloniaProperty.Register<NewsletterSubmitButton, string>(nameof(SendingLabel), "Subscribing");

    public static readonly StyledProperty<string> DoneLabelProperty =
        AvaloniaProperty.Register<NewsletterSubmitButton, string>(nameof(DoneLabel), "Subscribed");

    private static readonly Geometry CheckGeometry = ArcIcons.Parse("M5 12.5l4.5 4.5L19 7.5");

    private Grid? _labels;
    private SiteSwap? _swap;
    private readonly List<Control> _sizers = new();
    private Control? _current;
    private NewsletterButtonKey? _shownKey;

    public NewsletterSubmitButton()
    {
        // The idle state is the default: the button starts with :idle.
        PseudoClasses.Set(IdlePseudoClass, true);
    }

    public NewsletterButtonKey State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public string IdleLabel { get => GetValue(IdleLabelProperty); set => SetValue(IdleLabelProperty, value); }

    public string RetryLabel { get => GetValue(RetryLabelProperty); set => SetValue(RetryLabelProperty, value); }

    public string SendingLabel { get => GetValue(SendingLabelProperty); set => SetValue(SendingLabelProperty, value); }

    public string DoneLabel { get => GetValue(DoneLabelProperty); set => SetValue(DoneLabelProperty, value); }

    protected override Type StyleKeyOverride => typeof(NewsletterSubmitButton);

    /// <summary>The label on screen (tests read it).</summary>
    public Control? CurrentLabel => _current;

    /// <summary>The swap that shows the label (tests read it).</summary>
    public SiteSwap? Swap => _swap;

    /// <summary>The unseen labels that size the button, in the order of <see cref="NewsletterButtonKey"/>.</summary>
    public IReadOnlyList<Control> Sizers => _sizers;

    /// <summary>The text of the current state's label.</summary>
    public string CurrentText => LabelFor(State);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _labels = e.NameScope.Find<Grid>(LabelsPart);
        _swap = e.NameScope.Find<SiteSwap>(SwapPart);
        _shownKey = null;
        _current = null;
        RebuildSizers();
        Show(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StateProperty)
        {
            PseudoClasses.Set(IdlePseudoClass, State == NewsletterButtonKey.Idle);
            PseudoClasses.Set(RetryPseudoClass, State == NewsletterButtonKey.Retry);
            PseudoClasses.Set(SendingPseudoClass, State == NewsletterButtonKey.Sending);
            PseudoClasses.Set(DonePseudoClass, State == NewsletterButtonKey.Done);
            // A done button leaves the tab order: the "different email" button takes the focus.
            Focusable = State != NewsletterButtonKey.Done;
            AutomationProperties.SetName(this, LabelFor(State));
            Show(animate: true);
        }
        else if (property == IdleLabelProperty || property == RetryLabelProperty || property == SendingLabelProperty || property == DoneLabelProperty)
        {
            RebuildSizers();
            _shownKey = null;
            Show(animate: false);
            AutomationProperties.SetName(this, LabelFor(State));
        }
    }

    private string LabelFor(NewsletterButtonKey key) => key switch
    {
        NewsletterButtonKey.Retry => RetryLabel,
        NewsletterButtonKey.Sending => SendingLabel,
        NewsletterButtonKey.Done => DoneLabel,
        _ => IdleLabel
    };

    private void RebuildSizers()
    {
        if (_labels is null)
        {
            return;
        }
        foreach (var sizer in _sizers)
        {
            _labels.Children.Remove(sizer);
        }
        _sizers.Clear();
        for (var i = 0; i < 4; i++)
        {
            var sizer = CreateLabel((NewsletterButtonKey)i, sizer: true);
            sizer.Opacity = 0;
            sizer.IsHitTestVisible = false;
            AutomationProperties.SetAccessibilityView(sizer, AccessibilityView.Raw);
            _sizers.Add(sizer);
            _labels.Children.Insert(i, sizer);
        }
    }

    private void Show(bool animate)
    {
        if (_swap is null || _shownKey == State)
        {
            return;
        }
        _shownKey = State;
        _current = CreateLabel(State, sizer: false);
        _ = animate;
        _swap.Content = _current;
    }

    private Control CreateLabel(NewsletterButtonKey key, bool sizer)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var text = new TextBlock { Text = LabelFor(key), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
        switch (key)
        {
            case NewsletterButtonKey.Idle:
            case NewsletterButtonKey.Retry:
                panel.Children.Add(text);
                panel.Children.Add(new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
                break;
            case NewsletterButtonKey.Sending:
                panel.Children.Add(sizer
                    ? new Border { Width = 14, Height = 14 }
                    : new NewsletterSpinner { VerticalAlignment = VerticalAlignment.Center });
                panel.Children.Add(text);
                break;
            default:
                if (sizer)
                {
                    panel.Children.Add(new Border { Width = 16, Height = 16 });
                }
                else
                {
                    var check = new StrokeDrawGlyph { Data = CheckGeometry, Size = 16, StrokeThickness = 2.4, VerticalAlignment = VerticalAlignment.Center, Progress = 0 };
                    // Draws itself 120 ms after the swap starts; under reduced motion and Off it is simply there.
                    check.AttachedToVisualTree += (_, _) =>
                    {
                        if (ArcMotion.IsFull(check))
                        {
                            check.Progress = 0;
                            _ = check.Play(TimeSpan.FromMilliseconds(340), ArcEasings.Enter, TimeSpan.FromMilliseconds(120));
                        }
                        else
                        {
                            check.Progress = 1;
                        }
                    };
                    panel.Children.Add(check);
                }
                panel.Children.Add(text);
                break;
        }
        return panel;
    }
}

/// <summary>
/// A text button for the message line: the foreground color with a 1 px underline 3 px below the text in the strong border
/// color, which turns to the text color on hover (160 ms). Raised as <c>Click</c>.
/// </summary>
[TemplatePart(TextPart, typeof(TextBlock))]
internal sealed class NewsletterLink : AvaloniaButton
{
    public const string TextPart = "PART_Text";

    private readonly SolidColorBrush _underline = new(Colors.Gray);
    private TextBlock? _text;
    private MotionValue<Color>? _underlineColor;
    private bool _hover;

    protected override Type StyleKeyOverride => typeof(NewsletterLink);

    /// <summary>The color of the underline right now (tests read it).</summary>
    public Color UnderlineColor => _underline.Color;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<TextBlock>(TextPart);
        if (_text is not null)
        {
            _text.TextDecorations = new TextDecorationCollection
            {
                new TextDecoration { Location = TextDecorationLocation.Underline, Stroke = _underline, StrokeThickness = 1, StrokeOffset = 3, StrokeThicknessUnit = TextDecorationUnit.Pixel, StrokeOffsetUnit = TextDecorationUnit.Pixel }
            };
        }
        ResolveUnderline();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResolveUnderline();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        _underlineColor?.Stop();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ResolveUnderline();

    private void ResolveUnderline()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        _underlineColor?.Dispose();
        _underlineColor = new MotionValue<Color>(this, Target(), MotionVectors.Color);
        _underlineColor.Changed += c => _underline.Color = c;
        _underline.Color = Target();
    }

    private Color Target() => ArcTokens.GetColor(this, _hover ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.BorderStrong);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Hover only for a mouse or a pen: Arc's (hover: hover) and (pointer: fine).
        if (e.Pointer.Type != PointerType.Touch)
        {
            SetHover(true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(false);
    }

    private void SetHover(bool hover)
    {
        if (_hover == hover)
        {
            return;
        }
        _hover = hover;
        var color = _underlineColor;
        if (color is not null && this.IsAttachedToVisualTree())
        {
            _ = color.AnimateTo(Target(), MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }
}
