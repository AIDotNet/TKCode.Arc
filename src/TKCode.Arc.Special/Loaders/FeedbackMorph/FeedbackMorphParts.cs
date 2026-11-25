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
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// Shared behavior of the morph's buttons: a <c>:hover</c> pseudo-class for mouse and pen only (a touch shows no hover),
/// and a keyboard focus ring built on first use in the template's <c>PART_Root</c> panel.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal abstract class FeedbackMorphButtonBase : AvaloniaButton
{
    internal const string HoverPseudoClass = ":hover";
    internal const string RootPart = "PART_Root";

    private LazyFocusRing? _focusRing;

    static FeedbackMorphButtonBase()
    {
        ClipToBoundsProperty.OverrideDefaultValue<FeedbackMorphButtonBase>(false);
    }

    /// <summary>The resource key of the ring's corner radius.</summary>
    protected virtual object FocusRingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing = new LazyFocusRing(this, FeedbackMorphTokens.Key(FeedbackMorphTokenKind.FocusRingMargin), FocusRingRadiusKey);
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
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

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// The button face: a speech-bubble glyph and the label on the surface's fill. It opens the panel; while the panel shows
/// it is out of the tab order and hidden from assistive technology.
/// </summary>
internal sealed class FeedbackMorphTrigger : FeedbackMorphButtonBase
{
    // Lucide "message-square-text" (ISC), as Core's glyph set does not carry it.
    public static readonly Geometry BubbleGlyph = ArcIcons.Parse(
        "M22 17a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 21.286V5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2z M7 11h10 M7 15h6 M7 7h8");

    /// <summary>The label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FeedbackMorphTrigger, string?>(nameof(Label));

    private readonly FeedbackMorph _owner;

    public FeedbackMorphTrigger(FeedbackMorph owner)
    {
        _owner = owner;
    }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _owner.SetTriggerPressed(IsPressed);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _owner.SetTriggerPressed(false);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _owner.SetTriggerPressed(false);
    }
}

/// <summary>The round 28 px close button of the panel's title row.</summary>
internal sealed class FeedbackMorphIconButton : FeedbackMorphButtonBase
{
    /// <summary>The 24 × 24 stroked glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<FeedbackMorphIconButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>
/// The send button: "Send" with an up arrow, and while the note is sent a spinner with "Sending" (the label leaves up and
/// the busy text comes from below). It is as wide as the wider of the two, so the swap never changes its size. With
/// nothing to send it fades to 0.45 but still takes clicks (the panel then nudges the faces and shows a hint).
/// </summary>
[PseudoClasses(BusyPseudoClass, EmptyPseudoClass)]
internal sealed class FeedbackMorphSend : FeedbackMorphButtonBase
{
    internal const string BusyPseudoClass = ":busy";
    internal const string EmptyPseudoClass = ":empty";

    /// <summary>The idle text.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FeedbackMorphSend, string?>(nameof(Label));

    /// <summary>The text while the note is sent.</summary>
    public static readonly StyledProperty<string?> BusyLabelProperty =
        AvaloniaProperty.Register<FeedbackMorphSend, string?>(nameof(BusyLabel));

    /// <summary>Whether the note is being sent.</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<FeedbackMorphSend, bool>(nameof(IsBusy));

    /// <summary>Whether there is nothing to send yet (no face and an empty note).</summary>
    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<FeedbackMorphSend, bool>(nameof(IsEmpty));

    public FeedbackMorphSend()
    {
        // Arc shows Cmd+Enter and Ctrl+Enter as the button's shortcut (from the note).
        AutomationProperties.SetAcceleratorKey(this, "Control+Enter");
    }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? BusyLabel { get => GetValue(BusyLabelProperty); set => SetValue(BusyLabelProperty, value); }

    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    public bool IsEmpty { get => GetValue(IsEmptyProperty); set => SetValue(IsEmptyProperty, value); }

    protected override object FocusRingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep8);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusyProperty)
        {
            var busy = change.GetNewValue<bool>();
            PseudoClasses.Set(BusyPseudoClass, busy);
            AutomationProperties.SetName(this, busy ? BusyLabel : Label);
        }
        else if (change.Property == IsEmptyProperty)
        {
            PseudoClasses.Set(EmptyPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == LabelProperty || change.Property == BusyLabelProperty)
        {
            AutomationProperties.SetName(this, IsBusy ? BusyLabel : Label);
        }
    }
}

/// <summary>Stacks its children on one another and takes the size of the largest (the send button's two labels).</summary>
internal sealed class FeedbackMorphOverlay : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            child.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>The note: a borderless multi-line text box on the field's fill (the field draws the edge and the prompt).</summary>
internal sealed class FeedbackMorphNote : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(FeedbackMorphNote);
}

/// <summary>
/// The thank-you pill: a check in a soft success disc whose ring and tick draw themselves, then the thank-you words rising
/// one by one. It takes focus when it appears (so the keyboard is not lost) but is not in the tab order.
/// </summary>
[TemplatePart(RingPart, typeof(StrokeDrawGlyph))]
[TemplatePart(CheckPart, typeof(StrokeDrawGlyph))]
[TemplatePart(WordsPart, typeof(StackPanel))]
internal sealed class FeedbackMorphSentFace : TemplatedControl, IFeedbackMorphFace
{
    internal const string RingPart = "PART_Ring";
    internal const string CheckPart = "PART_Check";
    internal const string WordsPart = "PART_Words";

    /// <summary>The thank-you words.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FeedbackMorphSentFace, string?>(nameof(Text));

    // 24-unit drawings of the success mark: the ring starts at the top and runs clockwise; the tick runs left to right.
    public static readonly Geometry RingGlyph = Geometry.Parse("M12 2 A10 10 0 1 1 11.99 2");
    public static readonly Geometry CheckGlyph = Geometry.Parse("M7.9 12.4 L10.7 15.1 L16.2 9.4");

    // Arc Feedback morph (design reference): the ring draws 130 ms into the pill over 280 ms, the tick 350 ms in over
    // 190 ms; the words start 315 ms in, 40 ms apart, each fading over 170 ms while rising 7 px on the shape's spring.
    private static readonly TimeSpan RingDelay = TimeSpan.FromMilliseconds(130);
    private static readonly TimeSpan RingDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CheckDuration = TimeSpan.FromMilliseconds(190);
    private static readonly TimeSpan WordsDelay = TimeSpan.FromMilliseconds(315);
    private static readonly TimeSpan WordStagger = TimeSpan.FromMilliseconds(40);
    private const double WordRise = 7;

    private StrokeDrawGlyph? _ring;
    private StrokeDrawGlyph? _check;
    private StackPanel? _words;
    private readonly List<WordMotion> _wordMotions = new();
    private bool? _pendingEntrance;

    public FeedbackMorphSentFace()
    {
        Focusable = true;
        IsTabStop = false;
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The words (tests).</summary>
    internal IReadOnlyList<TextBlock> Words => _words?.Children.OfType<TextBlock>().ToList() ?? (IReadOnlyList<TextBlock>)[];

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring = e.NameScope.Find<StrokeDrawGlyph>(RingPart);
        _check = e.NameScope.Find<StrokeDrawGlyph>(CheckPart);
        _words = e.NameScope.Find<StackPanel>(WordsPart);
        BuildWords();
        if (_pendingEntrance is { } animate)
        {
            _pendingEntrance = null;
            OnEntering(animate);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
            BuildWords();
        }
    }

    private void BuildWords()
    {
        if (_words is null)
        {
            return;
        }
        foreach (var motion in _wordMotions)
        {
            motion.Dispose();
        }
        _wordMotions.Clear();
        _words.Children.Clear();
        foreach (var word in (Text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var block = new TextBlock { Text = word, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _words.Children.Add(block);
            _wordMotions.Add(new WordMotion(this, block));
        }
    }

    public void OnEntering(bool animate)
    {
        if (_words is null)
        {
            // Shown before its template was applied (first send): play once the parts exist.
            _pendingEntrance = animate;
            return;
        }
        if (!animate)
        {
            _ring?.Progress = 1;
            _check?.Progress = 1;
            foreach (var motion in _wordMotions)
            {
                motion.Rest();
            }
            return;
        }
        if (_ring is not null)
        {
            _ring.Progress = 0;
            _ = _ring.Play(RingDuration, ArcEasings.Standard, RingDelay);
        }
        if (_check is not null)
        {
            _check.Progress = 0;
            _ = _check.Play(CheckDuration, ArcEasings.Standard, CheckDelay);
        }
        for (var i = 0; i < _wordMotions.Count; i++)
        {
            _wordMotions[i].Play(WordsDelay + WordStagger * i, WordRise);
        }
    }

    /// <summary>One word's fade, rise and blur.</summary>
    private sealed class WordMotion : IDisposable
    {
        private readonly TranslateTransform _translate = new();
        private readonly MotionValue<double> _progress;
        private readonly MotionValue<double> _rise;

        public WordMotion(Visual owner, TextBlock block)
        {
            block.RenderTransform = _translate;
            _progress = new MotionValue<double>(owner, 1, MotionVectors.Double);
            _progress.Changed += p =>
            {
                block.Opacity = p;
                block.Effect = ArcBlur.Get(2 * (1 - p));
            };
            _rise = new MotionValue<double>(owner, 0, MotionVectors.Double);
            _rise.Changed += y => _translate.Y = y;
        }

        public void Play(TimeSpan delay, double rise)
        {
            _progress.Jump(0);
            _rise.Jump(rise);
            _ = _progress.AnimateTo(1, MotionSpec.Tween(FeedbackMorphSurface.Fade, ArcEasings.Standard, delay));
            _ = _rise.AnimateTo(0, MotionSpec.Spring(FeedbackMorphSurface.ShapeSpring, delay));
        }

        public void Rest()
        {
            _progress.Jump(1);
            _rise.Jump(0);
        }

        public void Dispose()
        {
            _progress.Dispose();
            _rise.Dispose();
        }
    }
}
