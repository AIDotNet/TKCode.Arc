using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Lucide transport glyphs drawn the way the player draws them: filled with the text color and stroked at 1.75 with
/// round caps and joins (24 × 24 viewBox scaled to <see cref="Size"/>). <see cref="OpticalShift"/> nudges the play
/// triangle right (6% of the size) so it looks centred in its circle.
/// </summary>
internal sealed class NowPlayingGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<NowPlayingGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<NowPlayingGlyph, double>(nameof(Size), 18);

    public static readonly StyledProperty<double> OpticalShiftProperty =
        AvaloniaProperty.Register<NowPlayingGlyph, double>(nameof(OpticalShift));

    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<NowPlayingGlyph, bool>(nameof(IsFilled), true);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<NowPlayingGlyph>();

    private Pen? _pen;

    static NowPlayingGlyph()
    {
        AffectsRender<NowPlayingGlyph>(DataProperty, ForegroundProperty, OpticalShiftProperty, IsFilledProperty);
        AffectsMeasure<NowPlayingGlyph>(SizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<NowPlayingGlyph>(false);
    }

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double OpticalShift { get => GetValue(OpticalShiftProperty); set => SetValue(OpticalShiftProperty, value); }

    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush)
        {
            return;
        }
        _pen ??= new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var size = Size;
        var scale = size / 24;
        var x = (Bounds.Width - size) / 2 + OpticalShift * size;
        var y = (Bounds.Height - size) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(IsFilled ? brush : null, _pen, data);
        }
    }
}

/// <summary>
/// The play / pause icon of the play button: two stacked glyphs that trade places (as measured) — the leaving one fades,
/// blurs to 2px and shrinks to 0.6 over 120ms ease-out, the arriving one grows from 0.6 out of a 2px blur on a firm
/// spring (k 400, c 40). Reduced motion crossfades.
/// <para>Why code: the arriving glyph rides a retargetable spring (quick play/pause presses reverse it with velocity).</para>
/// </summary>
internal sealed class NowPlayingPlayGlyph : Control
{
    private readonly Layer _play;
    private readonly Layer _pause;
    private bool _playing;
    private double _size = 18;

    public NowPlayingPlayGlyph()
    {
        _play = new Layer(this, new NowPlayingGlyph { Data = ArcIcons.Play, OpticalShift = 0.06 }, 1);
        _pause = new Layer(this, new NowPlayingGlyph { Data = ArcIcons.Pause }, 0);
        IsHitTestVisible = false;
    }

    /// <summary>True while the pause glyph shows.</summary>
    public bool IsPlaying => _playing;

    internal NowPlayingGlyph PlayLayer => _play.Glyph;

    internal NowPlayingGlyph PauseLayer => _pause.Glyph;

    /// <summary>The glyph size (18 in the mini bar, 24 in the full player); changes at once.</summary>
    public double GlyphSize
    {
        get => _size;
        set
        {
            _size = value;
            _play.Glyph.Size = value;
            _pause.Glyph.Size = value;
            InvalidateMeasure();
        }
    }

    public void Show(bool playing, bool animate)
    {
        if (playing == _playing)
        {
            return;
        }
        _playing = playing;
        var incoming = playing ? _pause : _play;
        var outgoing = playing ? _play : _pause;
        if (!animate)
        {
            incoming.Jump(1);
            outgoing.Jump(0);
            return;
        }
        var full = ArcMotion.IsFull(this);
        incoming.Travel = full;
        outgoing.Travel = full;
        _ = outgoing.Value.AnimateTo(0, MotionSpec.Tween(NowPlayingMotion.GlyphOut, NowPlayingMotion.EaseOut));
        _ = incoming.Value.AnimateTo(1, full
            ? MotionSpec.Spring(NowPlayingMotion.Firm)
            : MotionSpec.Tween(NowPlayingMotion.Fade, NowPlayingMotion.EaseOut));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _play.Glyph.Measure(availableSize);
        _pause.Glyph.Measure(availableSize);
        return new Size(_size, _size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _play.Glyph.Arrange(rect);
        _pause.Glyph.Arrange(rect);
        return finalSize;
    }

    private sealed class Layer
    {
        private readonly MatrixTransform _transform = new();

        public Layer(NowPlayingPlayGlyph owner, NowPlayingGlyph glyph, double initial)
        {
            Glyph = glyph;
            glyph.RenderTransform = _transform;
            glyph.RenderTransformOrigin = RelativePoint.Center;
            owner.VisualChildren.Add(glyph);
            owner.LogicalChildren.Add(glyph);
            Value = new MotionValue<double>(owner, initial, MotionVectors.Double);
            Value.Changed += Apply;
            Apply(initial);
        }

        public NowPlayingGlyph Glyph { get; }

        public MotionValue<double> Value { get; }

        public bool Travel { get; set; }

        public void Jump(double v) => Value.Jump(v);

        private void Apply(double v)
        {
            var o = Math.Clamp(v, 0, 1);
            Glyph.Opacity = o;
            Glyph.IsVisible = o > 0.001;
            if (Travel && o < 0.999)
            {
                var s = 0.6 + 0.4 * v;
                _transform.Matrix = Matrix.CreateScale(s, s);
                Glyph.Effect = ArcBlur.Get(2 * (1 - o));
            }
            else
            {
                _transform.Matrix = Matrix.Identity;
                Glyph.Effect = null;
            }
        }
    }
}

/// <summary>
/// A button of the player, styled by class: <c>main</c> (the mini bar's text area: no fill, a square keyboard ring),
/// <c>play</c> (a muted 40 circle in the mini bar, a 52 text-colored circle in the full player: <c>:full</c>, 240ms),
/// <c>skip</c> (transparent, muted on hover) and <c>collapse</c> (34, dark glass on the artwork). Remembers whether the
/// last press came from the keyboard so focus moves show the ring only then. <see cref="ExpandState"/> makes it report
/// expanded / collapsed to assistive tech (aria-expanded).
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(FullPseudoClass)]
internal sealed class NowPlayingButton : Button
{
    public const string FullPseudoClass = ":full";

    public bool PressedByKeyboard { get; private set; }

    /// <summary>Reported expand state (null: this button does not expand anything).</summary>
    public bool? ExpandState { get; set; }

    public bool IsFull
    {
        get => PseudoClasses.Contains(FullPseudoClass);
        set => PseudoClasses.Set(FullPseudoClass, value);
    }

    protected override Type StyleKeyOverride => typeof(NowPlayingButton);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        PressedByKeyboard = false;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            PressedByKeyboard = true;
        }
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        ExpandState is null ? base.OnCreateAutomationPeer() : new ExpandPeer(this);

    private sealed class ExpandPeer(NowPlayingButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState =>
            owner.ExpandState == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        // Both press the button (the button decides which way the player goes), as ButtonAutomationPeer.Invoke does.
        public void Expand() => Invoke();

        public void Collapse() => Invoke();
    }
}

/// <summary>
/// The appearance of a part that only one layout has (the mini text, levels and progress line; the full player's collapse
/// button, info, waveform and previous button), as measured: leaving fades out in 100ms (exit curve) blurring to 2px;
/// arriving fades in over 240ms (enter curve) after a stagger delay, rising from a small offset out of a 2px blur.
/// Reduced motion: 120ms out, 240ms in with no delay, offset or blur. A hidden part takes no input and no focus.
/// </summary>
internal sealed class NowPlayingPresence
{
    private readonly Control _target;
    private readonly double _offset;
    private readonly TimeSpan _delay;
    private readonly MotionValue<double> _value;
    private readonly TranslateTransform? _translate;
    private bool _present = true;
    private bool _travel;
    private bool _entering;

    public NowPlayingPresence(Control target, double enterOffset, TimeSpan enterDelay)
    {
        _target = target;
        _offset = enterOffset;
        _delay = enterDelay;
        if (enterOffset != 0)
        {
            _translate = new TranslateTransform();
            target.RenderTransform = _translate;
        }
        _value = new MotionValue<double>(target, 1, MotionVectors.Double);
        _value.Changed += Apply;
    }

    public bool IsPresent => _present;

    public double Value => _value.Value;

    public bool IsAnimating => _value.IsAnimating;

    public void Show(bool animate)
    {
        _present = true;
        _target.IsVisible = true;
        _target.IsHitTestVisible = true;
        SetTabStop(true);
        _entering = true;
        if (!animate)
        {
            _value.Jump(1);
            return;
        }
        _travel = ArcMotion.IsFull(_target);
        var delay = _travel ? _delay : TimeSpan.Zero;
        if (_value.Value <= 0.001)
        {
            Apply(0);
        }
        _ = _value.AnimateTo(1, MotionSpec.Tween(NowPlayingMotion.Enter, ArcEasings.Enter, delay));
    }

    public void Hide(bool animate)
    {
        _present = false;
        _target.IsHitTestVisible = false;
        SetTabStop(false);
        _entering = false;
        if (!animate)
        {
            _value.Jump(0);
            return;
        }
        _travel = ArcMotion.IsFull(_target);
        _ = _value.AnimateTo(0, MotionSpec.Tween(_travel ? NowPlayingMotion.Exit : NowPlayingMotion.ReducedExit, ArcEasings.Exit));
    }

    private void SetTabStop(bool on)
    {
        if (_target is InputElement input && _target.Focusable)
        {
            KeyboardNavigation.SetIsTabStop(input, on);
        }
        foreach (var child in NowPlayingTree.FocusableDescendants(_target))
        {
            KeyboardNavigation.SetIsTabStop(child, on);
        }
    }

    private void Apply(double v)
    {
        var o = Math.Clamp(v, 0, 1);
        _target.Opacity = o;
        if (!_present && o <= 0.001)
        {
            _target.IsVisible = false;
        }
        var moving = _value.IsAnimating && _travel;
        if (_translate is not null)
        {
            _translate.Y = moving && _entering ? _offset * (1 - o) : 0;
        }
        _target.Effect = moving && o < 0.999 ? ArcBlur.Get(2 * (1 - o)) : null;
    }
}

/// <summary>Logical-tree helpers for <see cref="NowPlayingPresence"/>.</summary>
internal static class NowPlayingTree
{
    public static IEnumerable<InputElement> FocusableDescendants(Control root)
    {
        foreach (var child in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root))
        {
            if (child is InputElement input && input.Focusable)
            {
                yield return input;
            }
        }
    }
}

/// <summary>
/// The title and artist lines, with the swap of a track change (as measured, the same in both directions): the old lines
/// fade and rise 6px in 120ms ease-out; the new ones fade in over 240ms (enter curve) rising from 6px below out of a 2px
/// blur. Reduced motion crossfades (Arc still moves the old text and blurs the new; a pure fade is calmer and simpler).
/// </summary>
internal sealed class NowPlayingTrackText : Panel
{
    private readonly Face _a;
    private readonly Face _b;
    private Face _current;

    public NowPlayingTrackText(Func<TextBlock> title, Func<TextBlock> artist)
    {
        _a = new Face(this, title(), artist());
        _b = new Face(this, title(), artist());
        _current = _a;
        _b.Value.Jump(0);
        IsHitTestVisible = false;
    }

    public string Title => _current.Title.Text ?? string.Empty;

    public string Artist => _current.Artist.Text ?? string.Empty;

    internal TextBlock TitleBlock => _current.Title;

    internal TextBlock ArtistBlock => _current.Artist;

    public void Show(string title, string artist, bool animate)
    {
        if (!animate)
        {
            _current.Set(title, artist);
            (_current == _a ? _b : _a).Value.Jump(0);
            _current.Value.Jump(1);
            return;
        }
        var outgoing = _current;
        var incoming = _current == _a ? _b : _a;
        _current = incoming;
        incoming.Set(title, artist);
        var full = ArcMotion.IsFull(this);
        outgoing.Entering = false;
        incoming.Entering = true;
        outgoing.Travel = full;
        incoming.Travel = full;
        incoming.Value.Jump(0);
        _ = outgoing.Value.AnimateTo(0, MotionSpec.Tween(NowPlayingMotion.GlyphOut, NowPlayingMotion.EaseOut));
        _ = incoming.Value.AnimateTo(1, MotionSpec.Tween(NowPlayingMotion.Enter, ArcEasings.Enter));
    }

    private sealed class Face
    {
        private readonly StackPanel _panel;
        private readonly TranslateTransform _translate = new();

        public Face(NowPlayingTrackText owner, TextBlock title, TextBlock artist)
        {
            Title = title;
            Artist = artist;
            _panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, artist }, RenderTransform = _translate };
            owner.Children.Add(_panel);
            Value = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Value.Changed += Apply;
        }

        public TextBlock Title { get; }

        public TextBlock Artist { get; }

        public MotionValue<double> Value { get; }

        public bool Entering { get; set; }

        public bool Travel { get; set; }

        public void Set(string title, string artist)
        {
            Title.Text = title;
            Artist.Text = artist;
        }

        private void Apply(double v)
        {
            var o = Math.Clamp(v, 0, 1);
            _panel.Opacity = o;
            _panel.IsVisible = o > 0.001;
            var moving = Travel && Value.IsAnimating;
            _translate.Y = !moving ? 0 : Entering ? 6 * (1 - o) : -6 * (1 - o);
            _panel.Effect = moving && Entering && o < 0.999 ? ArcBlur.Get(2 * (1 - o)) : null;
        }
    }
}

/// <summary>
/// The mini bar's progress line: a 2px text-colored bar from the left to the played fraction (radius 1). It sits on the
/// bottom edge of the inner box, so the shell clips it at rest (as measured in Arc) and it only shows during the morph.
/// </summary>
internal sealed class NowPlayingProgressLine : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<NowPlayingProgressLine>();

    private double _fraction;
    private double _drawnFraction;

    static NowPlayingProgressLine()
    {
        AffectsRender<NowPlayingProgressLine>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<NowPlayingProgressLine>(false);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double Fraction
    {
        get => _fraction;
        set
        {
            var v = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
            if (v != _fraction)
            {
                _fraction = v;
                // Repaint once the line has grown a quarter of a device pixel (see NowPlayingRepaint).
                if (NowPlayingRepaint.Moved(this, _drawnFraction, v))
                {
                    InvalidateVisual();
                }
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        _drawnFraction = _fraction;
        if (Foreground is not { } brush || _fraction <= 0)
        {
            return;
        }
        var h = Bounds.Height;
        context.DrawRectangle(brush, null, new RoundedRect(new Rect(0, 0, Bounds.Width * _fraction, h), h / 2));
    }
}

/// <summary>The shell's surface: raised fill, 1px border, floating shadow; its radius follows the morph (22 to 30).</summary>
internal sealed class NowPlayingSurface : Border
{
    public NowPlayingSurface()
    {
        IsHitTestVisible = false;
    }
}

/// <summary>Decides when a moving playhead needs a new frame.</summary>
internal static class NowPlayingRepaint
{
    /// <summary>The smallest change of the cut, in device pixels, worth a repaint.</summary>
    public const double Threshold = 0.25;

    /// <summary>
    /// Whether the cut at <paramref name="next"/> differs visibly from the one drawn at <paramref name="drawn"/> across
    /// <paramref name="visual"/>'s width: by a quarter of a device pixel, or by reaching either end exactly.
    /// </summary>
    public static bool Moved(Visual visual, double drawn, double next)
    {
        if (next <= 0 || next >= 1 || drawn <= 0)
        {
            return next != drawn;
        }
        var scale = TopLevel.GetTopLevel(visual)?.RenderScaling ?? 1;
        var width = visual.Bounds.Width * scale;
        return width <= 0 || Math.Abs(next - drawn) * width >= Threshold;
    }
}
