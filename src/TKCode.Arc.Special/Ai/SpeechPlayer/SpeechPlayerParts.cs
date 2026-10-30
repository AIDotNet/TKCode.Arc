using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A square icon button of the speech player's dock (Stop reading, Good and Bad response): 28px (36px on touch), quiet at
/// rest, SurfaceMuted under the pointer, a 16px Lucide glyph, a keyboard ring. <see cref="IsActive"/> marks a chosen
/// feedback button (full color).
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(ActivePseudoClass)]
internal sealed class SpeechPlayerIconButton : AvaloniaButton
{
    public const string ActivePseudoClass = ":active";

    /// <summary>The glyph (24 × 24 Lucide geometry).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SpeechPlayerIconButton, Geometry?>(nameof(Icon));

    /// <summary>Whether the button is the chosen one of a pair (feedback).</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SpeechPlayerIconButton, bool>(nameof(IsActive));

    protected override Type StyleKeyOverride => typeof(SpeechPlayerIconButton);

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(ActivePseudoClass, IsActive);
        }
    }
}

/// <summary>A text button of the dock (Read aloud, the speed and the voice): quiet pill, SurfaceMuted under the pointer.</summary>
internal sealed class SpeechPlayerTextButton : AvaloniaButton
{
    /// <summary>A leading 16px glyph (Read aloud), or null.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SpeechPlayerTextButton, Geometry?>(nameof(Icon));

    protected override Type StyleKeyOverride => typeof(SpeechPlayerTextButton);

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>
/// The play button: a filled circle whose glyph follows the state (play, pause, play again, retry, a spinner while
/// preparing). The glyphs swap in place with a bounce-free scale and fade (theme transitions on each layer).
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(PlayingPseudoClass, FinishedPseudoClass, FailedPseudoClass, PreparingPseudoClass)]
internal sealed class SpeechPlayerPlayButton : AvaloniaButton
{
    public const string PlayingPseudoClass = ":playing";
    public const string FinishedPseudoClass = ":finished";
    public const string FailedPseudoClass = ":failed";
    public const string PreparingPseudoClass = ":preparing";

    protected override Type StyleKeyOverride => typeof(SpeechPlayerPlayButton);

    /// <summary>The state the glyph shows.</summary>
    internal SpeechPlayerState Shows { get; private set; } = SpeechPlayerState.Paused;

    internal void Show(SpeechPlayerState state)
    {
        Shows = state;
        PseudoClasses.Set(PlayingPseudoClass, state == SpeechPlayerState.Playing);
        PseudoClasses.Set(FinishedPseudoClass, state == SpeechPlayerState.Finished);
        PseudoClasses.Set(FailedPseudoClass, state == SpeechPlayerState.Failed);
        PseudoClasses.Set(PreparingPseudoClass, state == SpeechPlayerState.Preparing);
    }
}

/// <summary>
/// One option's label in the speed or voice button. Every option is laid out in the same cell and only the chosen one is
/// shown, so the button is as wide as its widest option and never resizes; labels crossfade (theme transitions).
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(ShownPseudoClass)]
internal sealed class SpeechPlayerChoice : TextBlock
{
    public const string ShownPseudoClass = ":shown";

    protected override Type StyleKeyOverride => typeof(SpeechPlayerChoice);

    /// <summary>Whether this option is the one shown.</summary>
    public bool IsShown
    {
        get => PseudoClasses.Contains(ShownPseudoClass);
        set => PseudoClasses.Set(ShownPseudoClass, value);
    }
}

/// <summary>
/// The thin scrubbable progress bar. The fill and thumb are drawn from <see cref="Fraction"/>, which the player writes on
/// every frame (a redraw, never a layout). <see cref="Emphasis"/> (0 at rest, 1 on hover, focus or drag; a theme
/// transition) thickens the track and scales the thumb in. Arrow keys move 2 seconds, Page Up / Page Down 10, Home / End
/// jump to the ends, Space and K play or pause. A drag moves playback live and reports its end once.
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(DraggingPseudoClass)]
internal sealed class SpeechPlayerProgress : Control
{
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>How much of the speech has played (0..1).</summary>
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, double>(nameof(Fraction));

    /// <summary>0 at rest, 1 while hovered, focused or dragged.</summary>
    public static readonly StyledProperty<double> EmphasisProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, double>(nameof(Emphasis));

    /// <summary>The track's color.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, IBrush?>(nameof(TrackBrush));

    /// <summary>The played part's color.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, IBrush?>(nameof(FillBrush));

    /// <summary>The thumb's color.</summary>
    public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, IBrush?>(nameof(ThumbBrush));

    /// <summary>The focus ring's color.</summary>
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, IBrush?>(nameof(FocusBrush));

    /// <summary>The track at rest.</summary>
    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, double>(nameof(TrackThickness), 3);

    /// <summary>The track while emphasized.</summary>
    public static readonly StyledProperty<double> TrackActiveThicknessProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, double>(nameof(TrackActiveThickness), 5);

    /// <summary>The thumb's diameter at full emphasis.</summary>
    public static readonly StyledProperty<double> ThumbSizeProperty =
        AvaloniaProperty.Register<SpeechPlayerProgress, double>(nameof(ThumbSize), 12);

    private bool _dragging;

    static SpeechPlayerProgress()
    {
        AffectsRender<SpeechPlayerProgress>(FractionProperty, EmphasisProperty, TrackBrushProperty, FillBrushProperty,
            ThumbBrushProperty, FocusBrushProperty, TrackThicknessProperty, TrackActiveThicknessProperty, ThumbSizeProperty);
        FocusableProperty.OverrideDefaultValue<SpeechPlayerProgress>(true);
    }

    /// <inheritdoc cref="FractionProperty"/>
    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }

    /// <inheritdoc cref="EmphasisProperty"/>
    public double Emphasis { get => GetValue(EmphasisProperty); set => SetValue(EmphasisProperty, value); }

    /// <inheritdoc cref="TrackBrushProperty"/>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <inheritdoc cref="FillBrushProperty"/>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <inheritdoc cref="ThumbBrushProperty"/>
    public IBrush? ThumbBrush { get => GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }

    /// <inheritdoc cref="FocusBrushProperty"/>
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    /// <inheritdoc cref="TrackThicknessProperty"/>
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }

    /// <inheritdoc cref="TrackActiveThicknessProperty"/>
    public double TrackActiveThickness { get => GetValue(TrackActiveThicknessProperty); set => SetValue(TrackActiveThicknessProperty, value); }

    /// <inheritdoc cref="ThumbSizeProperty"/>
    public double ThumbSize { get => GetValue(ThumbSizeProperty); set => SetValue(ThumbSizeProperty, value); }

    /// <summary>The position and length in seconds, for the slider's value (screen readers).</summary>
    internal double Seconds { get; set; }

    internal double TotalSeconds { get; set; }

    /// <summary>Moves playback by a number of seconds (keys).</summary>
    public Action<double>? SeekBy { get; set; }

    /// <summary>Moves playback to a fraction (Home, End, drags, screen readers); the flag is true while a drag continues.</summary>
    public Action<double, bool>? SeekTo { get; set; }

    /// <summary>Plays or pauses (Space, K).</summary>
    public Action? Toggle { get; set; }

    /// <summary>Whether a drag is in progress.</summary>
    internal bool IsDragging => _dragging;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                SeekBy?.Invoke(-2);
                break;
            case Key.Right:
            case Key.Up:
                SeekBy?.Invoke(2);
                break;
            case Key.PageDown:
                SeekBy?.Invoke(-10);
                break;
            case Key.PageUp:
                SeekBy?.Invoke(10);
                break;
            case Key.Home:
                SeekTo?.Invoke(0, false);
                break;
            case Key.End:
                SeekTo?.Invoke(1, false);
                break;
            case Key.Space:
            case Key.K:
                Toggle?.Invoke();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragging = true;
        PseudoClasses.Set(DraggingPseudoClass, true);
        e.Pointer.Capture(this);
        e.Handled = true;
        SeekTo?.Invoke(FractionAt(e.GetPosition(this).X), true);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            SeekTo?.Invoke(FractionAt(e.GetPosition(this).X), true);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            EndDrag(FractionAt(e.GetPosition(this).X));
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            EndDrag(Fraction);
        }
    }

    private void EndDrag(double fraction)
    {
        _dragging = false;
        PseudoClasses.Set(DraggingPseudoClass, false);
        SeekTo?.Invoke(fraction, false);
    }

    /// <summary>The fraction under <paramref name="x"/>, measured along the track (the thumb's half width is the inset).</summary>
    internal double FractionAt(double x)
    {
        var inset = ThumbSize / 2;
        var width = Bounds.Width - 2 * inset;
        return width <= 0 ? 0 : Math.Clamp((x - inset) / width, 0, 1);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole height takes the pointer, not only the thin track.
        context.FillRectangle(Brushes.Transparent, bounds);
        var emphasis = Math.Clamp(Emphasis, 0, 1);
        var inset = ThumbSize / 2;
        var width = Math.Max(0, bounds.Width - 2 * inset);
        var thickness = TrackThickness + (TrackActiveThickness - TrackThickness) * emphasis;
        var top = (bounds.Height - thickness) / 2;
        var track = new Rect(inset, top, width, thickness);
        var radius = thickness / 2;
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, new RoundedRect(track, radius));
        }
        var fraction = Math.Clamp(double.IsFinite(Fraction) ? Fraction : 0, 0, 1);
        var x = inset + width * fraction;
        if (FillBrush is { } fill && fraction > 0)
        {
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(inset, top, Math.Max(thickness, x - inset), thickness), radius));
        }
        var thumb = ThumbSize / 2 * emphasis;
        if (ThumbBrush is { } thumbBrush && thumb > 0.25)
        {
            context.DrawEllipse(thumbBrush, null, new Point(x, bounds.Height / 2), thumb, thumb);
        }
        if (FocusBrush is { } focus && Classes.Contains(":focus-visible"))
        {
            var ring = new Rect(-2, bounds.Height / 2 - ThumbSize / 2 - 4, bounds.Width + 4, ThumbSize + 8);
            context.DrawRectangle(null, new Pen(focus, 2), new RoundedRect(ring, ring.Height / 2));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProgressPeer(this);

    private sealed class ProgressPeer(SpeechPlayerProgress owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        private SpeechPlayerProgress Bar => (SpeechPlayerProgress)Owner;

        public bool IsReadOnly => false;

        public double Minimum => 0;

        public double Maximum => Bar.TotalSeconds;

        public double Value => Bar.Seconds;

        public double SmallChange => 2;

        public double LargeChange => 10;

        public void SetValue(double value)
        {
            var total = Bar.TotalSeconds;
            Bar.SeekTo?.Invoke(total <= 0 ? 0 : Math.Clamp(value / total, 0, 1), false);
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetHelpTextCore() => AutomationProperties.GetHelpText(Owner);
    }
}

/// <summary>The action row: Read aloud, then the app's actions or the default Copy, Good response and Bad response.</summary>
[Avalonia.Controls.Metadata.TemplatePart(ReadAloudPart, typeof(SpeechPlayerTextButton))]
[Avalonia.Controls.Metadata.TemplatePart(ActionsPart, typeof(ContentPresenter))]
[Avalonia.Controls.Metadata.TemplatePart(DefaultsPart, typeof(Panel))]
[Avalonia.Controls.Metadata.TemplatePart(CopyPart, typeof(CopyButton))]
[Avalonia.Controls.Metadata.TemplatePart(GoodPart, typeof(SpeechPlayerIconButton))]
[Avalonia.Controls.Metadata.TemplatePart(BadPart, typeof(SpeechPlayerIconButton))]
[Avalonia.Controls.Metadata.PseudoClasses(TouchPseudoClass)]
internal sealed class SpeechPlayerActionRow : TemplatedControl
{
    public const string TouchPseudoClass = ":touch";

    internal void SetTouch(bool touch) => PseudoClasses.Set(TouchPseudoClass, touch);

    public const string ReadAloudPart = "PART_ReadAloud";
    public const string ActionsPart = "PART_Actions";
    public const string DefaultsPart = "PART_Defaults";
    public const string CopyPart = "PART_Copy";
    public const string GoodPart = "PART_Good";
    public const string BadPart = "PART_Bad";

    public SpeechPlayerTextButton? ReadAloud { get; private set; }

    public ContentPresenter? Actions { get; private set; }

    public Panel? Defaults { get; private set; }

    public CopyButton? Copy { get; private set; }

    public SpeechPlayerIconButton? Good { get; private set; }

    public SpeechPlayerIconButton? Bad { get; private set; }

    /// <summary>Raised after the template is applied, so the player can wire the parts.</summary>
    public event Action? PartsReady;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ReadAloud = e.NameScope.Find<SpeechPlayerTextButton>(ReadAloudPart);
        Actions = e.NameScope.Find<ContentPresenter>(ActionsPart);
        Defaults = e.NameScope.Find<Panel>(DefaultsPart);
        Copy = e.NameScope.Find<CopyButton>(CopyPart);
        Good = e.NameScope.Find<SpeechPlayerIconButton>(GoodPart);
        Bad = e.NameScope.Find<SpeechPlayerIconButton>(BadPart);
        PartsReady?.Invoke();
    }
}

/// <summary>
/// The compact player: play, the elapsed time, the progress bar, the total time, speed, voice and Stop reading. While
/// preparing or after a failure a message takes the place of the times and the bar. Below the narrow width the total
/// time steps aside; on touch the row is 36px.
/// </summary>
[Avalonia.Controls.Metadata.TemplatePart(PlayPart, typeof(SpeechPlayerPlayButton))]
[Avalonia.Controls.Metadata.TemplatePart(ElapsedPart, typeof(TextBlock))]
[Avalonia.Controls.Metadata.TemplatePart(ProgressPart, typeof(SpeechPlayerProgress))]
[Avalonia.Controls.Metadata.TemplatePart(TotalPart, typeof(TextBlock))]
[Avalonia.Controls.Metadata.TemplatePart(PreparingPart, typeof(ToolCallText))]
[Avalonia.Controls.Metadata.TemplatePart(ErrorPart, typeof(TextBlock))]
[Avalonia.Controls.Metadata.TemplatePart(SpeedPart, typeof(SpeechPlayerTextButton))]
[Avalonia.Controls.Metadata.TemplatePart(SpeedLabelsPart, typeof(Panel))]
[Avalonia.Controls.Metadata.TemplatePart(VoicePart, typeof(SpeechPlayerTextButton))]
[Avalonia.Controls.Metadata.TemplatePart(VoiceLabelsPart, typeof(Panel))]
[Avalonia.Controls.Metadata.TemplatePart(StopPart, typeof(SpeechPlayerIconButton))]
[Avalonia.Controls.Metadata.PseudoClasses(PreparingPseudoClass, FailedPseudoClass, NarrowPseudoClass, TouchPseudoClass)]
internal sealed class SpeechPlayerControls : TemplatedControl
{
    public const string PlayPart = "PART_Play";
    public const string ElapsedPart = "PART_Elapsed";
    public const string ProgressPart = "PART_Progress";
    public const string TotalPart = "PART_Total";
    public const string PreparingPart = "PART_Preparing";
    public const string ErrorPart = "PART_Error";
    public const string SpeedPart = "PART_Speed";
    public const string SpeedLabelsPart = "PART_SpeedLabels";
    public const string VoicePart = "PART_Voice";
    public const string VoiceLabelsPart = "PART_VoiceLabels";
    public const string StopPart = "PART_Stop";

    public const string PreparingPseudoClass = ":preparing";
    public const string FailedPseudoClass = ":failed";
    public const string NarrowPseudoClass = ":narrow";
    public const string TouchPseudoClass = ":touch";

    /// <summary>Below this width of the whole player the total time steps aside (theme: the token's NarrowWidth).</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<SpeechPlayerControls, double>(nameof(NarrowWidth), 440);

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    public SpeechPlayerPlayButton? Play { get; private set; }

    public TextBlock? Elapsed { get; private set; }

    public SpeechPlayerProgress? Progress { get; private set; }

    public TextBlock? Total { get; private set; }

    public ToolCallText? Preparing { get; private set; }

    public TextBlock? Error { get; private set; }

    public SpeechPlayerTextButton? Speed { get; private set; }

    public Panel? SpeedLabels { get; private set; }

    public SpeechPlayerTextButton? Voice { get; private set; }

    public Panel? VoiceLabels { get; private set; }

    public SpeechPlayerIconButton? Stop { get; private set; }

    /// <summary>Raised after the template is applied, so the player can wire the parts.</summary>
    public event Action? PartsReady;

    internal void SetFlags(bool preparing, bool failed, bool narrow, bool touch)
    {
        PseudoClasses.Set(PreparingPseudoClass, preparing);
        PseudoClasses.Set(FailedPseudoClass, failed);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(TouchPseudoClass, touch);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Play = e.NameScope.Find<SpeechPlayerPlayButton>(PlayPart);
        Elapsed = e.NameScope.Find<TextBlock>(ElapsedPart);
        Progress = e.NameScope.Find<SpeechPlayerProgress>(ProgressPart);
        Total = e.NameScope.Find<TextBlock>(TotalPart);
        Preparing = e.NameScope.Find<ToolCallText>(PreparingPart);
        Error = e.NameScope.Find<TextBlock>(ErrorPart);
        Speed = e.NameScope.Find<SpeechPlayerTextButton>(SpeedPart);
        SpeedLabels = e.NameScope.Find<Panel>(SpeedLabelsPart);
        Voice = e.NameScope.Find<SpeechPlayerTextButton>(VoicePart);
        VoiceLabels = e.NameScope.Find<Panel>(VoiceLabelsPart);
        Stop = e.NameScope.Find<SpeechPlayerIconButton>(StopPart);
        PartsReady?.Invoke();
    }
}
