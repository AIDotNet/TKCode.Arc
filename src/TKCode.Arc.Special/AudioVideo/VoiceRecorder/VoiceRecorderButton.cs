using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What a recorder button is: it picks the size, colors and glyph in the theme.</summary>
internal enum VoiceRecorderButtonKind
{
    /// <summary>The 48px mic (foreground fill).</summary>
    Mic,

    /// <summary>The 40px trash (quiet; rose under the pointer).</summary>
    Discard,

    /// <summary>The 48px stop (accent fill, filled square).</summary>
    Stop,

    /// <summary>The 48px send (accent fill, arrow up).</summary>
    Send,

    /// <summary>The 32px play of the review and of incoming bubbles (foreground fill).</summary>
    Play,

    /// <summary>The 32px play of your own bubbles (surface fill on the dark bubble).</summary>
    PlayOwn,
}

/// <summary>
/// A round button of the recorder. The mic's glyph sits a fixed inset from the left edge (centred while the button is
/// square), so while the mic stretches into the bar or back, the glyph rides at the left end as in Arc. Round play buttons shrink
/// to 92% while pressed. Pointer focus shows no ring; keyboard focus a 2px ring 2px outside.
/// </summary>
[PseudoClasses(MicPseudoClass, DiscardPseudoClass, StopPseudoClass, SendPseudoClass, PlayPseudoClass, PlayOwnPseudoClass, PlayingPseudoClass)]
internal sealed class VoiceRecorderButton : AvaloniaButton
{
    public const string MicPseudoClass = ":mic";
    public const string DiscardPseudoClass = ":discard";
    public const string StopPseudoClass = ":stop";
    public const string SendPseudoClass = ":send";
    public const string PlayPseudoClass = ":play";
    public const string PlayOwnPseudoClass = ":play-own";
    public const string PlayingPseudoClass = ":playing";

    /// <summary>Whether a play button shows pause.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VoiceRecorderButton, bool>(nameof(IsPlaying));

    private readonly VoiceRecorderButtonKind _kind;

    public VoiceRecorderButton(VoiceRecorderButtonKind kind)
    {
        _kind = kind;
        PseudoClasses.Set(MicPseudoClass, kind == VoiceRecorderButtonKind.Mic);
        PseudoClasses.Set(DiscardPseudoClass, kind == VoiceRecorderButtonKind.Discard);
        PseudoClasses.Set(StopPseudoClass, kind == VoiceRecorderButtonKind.Stop);
        PseudoClasses.Set(SendPseudoClass, kind == VoiceRecorderButtonKind.Send);
        PseudoClasses.Set(PlayPseudoClass, kind is VoiceRecorderButtonKind.Play or VoiceRecorderButtonKind.PlayOwn);
        PseudoClasses.Set(PlayOwnPseudoClass, kind == VoiceRecorderButtonKind.PlayOwn);
        ClickMode = ClickMode.Release;
    }

    /// <summary>Used by the XAML loader only.</summary>
    public VoiceRecorderButton()
        : this(VoiceRecorderButtonKind.Play)
    {
    }

    protected override Type StyleKeyOverride => typeof(VoiceRecorderButton);

    public VoiceRecorderButtonKind Kind => _kind;

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <summary>The last press came from a pointer (the recorder moves focus without a ring then).</summary>
    internal bool PressedByPointer { get; private set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        PressedByPointer = true;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        PressedByPointer = false;
        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
        {
            PseudoClasses.Set(PlayingPseudoClass, IsPlaying);
        }
    }
}

/// <summary>
/// A 24 × 24 Lucide glyph, stroked and optionally filled (the recorder's play, pause and stop are solid).
/// </summary>
internal sealed class VoiceRecorderGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<VoiceRecorderGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<VoiceRecorderGlyph, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<VoiceRecorderGlyph, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<VoiceRecorderGlyph, bool>(nameof(IsFilled));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<VoiceRecorderGlyph, IBrush?>(nameof(Foreground));

    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    static VoiceRecorderGlyph()
    {
        AffectsMeasure<VoiceRecorderGlyph>(SizeProperty);
        AffectsRender<VoiceRecorderGlyph>(DataProperty, StrokeThicknessProperty, IsFilledProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<VoiceRecorderGlyph>(false);
    }

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var data = Data;
        var brush = Foreground;
        if (data is null || brush is null)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || Math.Abs(_penThickness - StrokeThickness) > double.Epsilon)
        {
            _pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = StrokeThickness;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(IsFilled ? brush : null, _pen, data);
        }
    }
}
