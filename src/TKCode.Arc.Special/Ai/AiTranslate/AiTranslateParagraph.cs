using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The paragraph of an <see cref="AiTranslate"/> card, drawn as one cached <see cref="TextLayout"/>. It shows a stable text,
/// or a translation streaming in: the new text first, each chunk fading in on its own over 140ms, then what is left of the
/// old text dimmed to 45% (over 200ms), one old sentence retiring for each new sentence that starts. Swapping between
/// stable texts crossfades (the old text leaves in 100ms, the new one fades in over 160ms with a 2px rise), and the height
/// follows the text on a critically damped spring (visualDuration 0.24, no bounce) while a width change cuts straight to
/// the new height.
/// <para>
/// Code-driven: each chunk's fade starts when the chunk arrives and the height's target is the measured text, which a
/// ControlTheme transition cannot express. The fades run on a <see cref="LiveLoop"/> at full motion only; reduced motion and
/// Off show every end state at once (the spring then jumps too).
/// </para>
/// </summary>
internal sealed class AiTranslateParagraph : Control
{
    public const double ChunkFade = 0.14;
    public const double DimDuration = 0.2;
    public const double LeaveDuration = 0.1;
    public const double EnterDuration = 0.16;
    public const double Rise = 2;

    /// <summary>The height spring: visualDuration 0.24, no bounce.</summary>
    public static readonly SpringSpec HeightSpring = SpringSpec.FromVisual(0.24, 0);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<AiTranslateParagraph>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<AiTranslateParagraph>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<AiTranslateParagraph>();

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AiTranslateParagraph, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<double> DimOpacityProperty =
        AvaloniaProperty.Register<AiTranslateParagraph, double>(nameof(DimOpacity), 0.45);

    private const int AlphaSteps = 32;

    private readonly StringBuilder _new = new();
    private readonly List<(int Start, int Length, double Born)> _chunks = [];
    private readonly List<ValueSpan<TextRunProperties>> _spans = [];
    private readonly ImmutableSolidColorBrush?[] _brushes = new ImmutableSolidColorBrush?[AlphaSteps + 1];
    private List<string> _old = [];
    private string _stable = string.Empty;
    private string _composite = string.Empty;
    private int _oldStart = -1;
    private int _retired;
    private bool _streaming;
    private double _dimBorn = double.NegativeInfinity;
    private double _now;
    private LiveLoop? _loop;
    private TextLayout? _layout;
    private TextLayout? _leaving;
    private double _leaveBorn = double.NegativeInfinity;
    private double _enterBorn = double.NegativeInfinity;
    private double _layoutWidth = double.NaN;
    private bool _dirty = true;
    private bool _contentChanged;
    private MotionValue<double>? _height;
    private double _natural = double.NaN;
    private double _lastWidth = double.NaN;
    private Color _color = Colors.Black;
    private Typeface _typeface;

    static AiTranslateParagraph()
    {
        ClipToBoundsProperty.OverrideDefaultValue<AiTranslateParagraph>(true);
        AffectsMeasure<AiTranslateParagraph>(FontFamilyProperty, FontSizeProperty, LineHeightProperty);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public double DimOpacity { get => GetValue(DimOpacityProperty); set => SetValue(DimOpacityProperty, value); }

    /// <summary>The whole text on screen: the stable text, or the streamed text followed by what is left of the old one.</summary>
    public string DisplayedText => _composite;

    /// <summary>What has streamed in so far (empty when not streaming).</summary>
    public string StreamedText => _streaming ? _new.ToString() : string.Empty;

    /// <summary>The old sentences still on screen while a translation streams.</summary>
    public int RemainingOldSentences => _streaming ? Math.Max(0, _old.Count - _retired) : 0;

    public bool IsStreaming => _streaming;

    /// <summary>True while a crossfade, a chunk fade or the dimming runs.</summary>
    public bool IsFading => _loop?.IsActive == true;

    /// <summary>The opacity the old text is drawn at now (1 when nothing is dimmed).</summary>
    public double CurrentDim => _streaming && _oldStart >= 0 ? DimAt(_now) : 1;

    /// <summary>The height the paragraph is drawn at now.</summary>
    public double DisplayedHeight => _height?.Value ?? 0;

    /// <summary>The height of the text.</summary>
    public double NaturalHeight => double.IsNaN(_natural) ? 0 : _natural;

    private bool Full => IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    /// <summary>Shows a stable text; <paramref name="crossfade"/> swaps from the text on screen with one crossfade.</summary>
    public void Show(string text, bool crossfade)
    {
        var animate = crossfade && Full && _layout is not null && _composite.Length > 0 && text != _composite;
        _leaving?.Dispose();
        _leaving = null;
        if (animate)
        {
            _leaving = _layout;
            _layout = null;
            _leaveBorn = _now;
            _enterBorn = _now;
        }
        else
        {
            _enterBorn = double.NegativeInfinity;
        }
        _streaming = false;
        _new.Clear();
        _chunks.Clear();
        _old.Clear();
        _retired = 0;
        _stable = text;
        Recompose();
        if (animate)
        {
            Loop.Start();
        }
    }

    /// <summary>Starts streaming over <paramref name="from"/>: the text dims and waits for the first chunk.</summary>
    public void BeginStream(string from)
    {
        _leaving?.Dispose();
        _leaving = null;
        _enterBorn = double.NegativeInfinity;
        _streaming = true;
        _new.Clear();
        _chunks.Clear();
        _old = AiTranslateText.Sentences(from);
        _retired = 0;
        _stable = from;
        _dimBorn = _now;
        Recompose();
        if (Full)
        {
            Loop.Start();
        }
    }

    /// <summary>Appends one streamed chunk; a sentence that starts with it retires one dimmed old sentence.</summary>
    public void Append(string chunk)
    {
        if (!_streaming || chunk.Length == 0)
        {
            return;
        }
        if (Full)
        {
            _chunks.Add((_new.Length, chunk.Length, _now));
            Loop.Start();
        }
        _new.Append(chunk);
        _retired = Math.Min(_old.Count, AiTranslateText.StartedSentences(_new.ToString()));
        Recompose();
    }

    /// <summary>
    /// Ends the stream on its final text: what is left of the old text goes and the chunks still fading finish their fade
    /// (when the streamed text is the final text; otherwise it is shown at once).
    /// </summary>
    public void EndStream(string text)
    {
        var same = _streaming && _new.ToString() == text;
        _streaming = false;
        _old.Clear();
        _retired = 0;
        if (!same)
        {
            _chunks.Clear();
        }
        _new.Clear();
        _stable = text;
        _enterBorn = double.NegativeInfinity;
        Recompose();
    }

    private LiveLoop Loop => _loop ??= new LiveLoop(this, Step) { TracksViewport = false };

    /// <summary>Advances the fades by <paramref name="seconds"/> (the loop's frame; tests call it directly).</summary>
    internal bool Step(double seconds)
    {
        _now += seconds;
        var busy = false;
        if (_chunks.Count > 0)
        {
            _chunks.RemoveAll(c => _now - c.Born >= ChunkFade);
            busy = true;
        }
        if (_streaming && _oldStart >= 0 && _now - _dimBorn < DimDuration + seconds)
        {
            busy = true;
        }
        if (_leaving is not null)
        {
            if (_now - _leaveBorn >= LeaveDuration)
            {
                _leaving.Dispose();
                _leaving = null;
            }
            busy = true;
        }
        if (_now - _enterBorn < EnterDuration + seconds)
        {
            busy = true;
        }
        if (busy || _dirty)
        {
            if (!double.IsNaN(_layoutWidth))
            {
                Build(_layoutWidth);
            }
            InvalidateVisual();
        }
        return busy;
    }

    private double DimAt(double now)
    {
        if (!Full)
        {
            return DimOpacity;
        }
        var t = Math.Clamp((now - _dimBorn) / DimDuration, 0, 1);
        return 1 - (1 - DimOpacity) * ArcEasings.Standard.Ease(t);
    }

    private void Recompose()
    {
        if (_streaming)
        {
            var streamed = _new.ToString();
            var rest = string.Concat(_old.Skip(_retired));
            var separator = AiTranslateText.Separator(streamed, rest);
            _composite = streamed + separator + rest;
            _oldStart = rest.Length > 0 ? streamed.Length + separator.Length : -1;
        }
        else
        {
            _composite = _stable;
            _oldStart = -1;
        }
        AutomationProperties.SetName(this, _composite);
        _dirty = true;
        _contentChanged = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == DimOpacityProperty)
        {
            _dirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == LineHeightProperty)
        {
            _dirty = true;
        }
    }

    private ImmutableSolidColorBrush BrushAt(double opacity)
    {
        var step = (int)Math.Round(Math.Clamp(opacity, 0, 1) * AlphaSteps);
        return _brushes[step] ??= new ImmutableSolidColorBrush(_color, step / (double)AlphaSteps);
    }

    private void Build(double width)
    {
        var color = Foreground is ISolidColorBrush solid ? solid.Color : Colors.Black;
        if (color != _color)
        {
            _color = color;
            Array.Clear(_brushes);
        }
        _typeface = new Typeface(FontFamily);
        _spans.Clear();
        var full = Full;
        if (full)
        {
            foreach (var (start, length, born) in _chunks)
            {
                var alpha = Math.Clamp((_now - born) / ChunkFade, 0, 1);
                if (alpha < 1 && start + length <= _composite.Length)
                {
                    _spans.Add(new ValueSpan<TextRunProperties>(start, length,
                        new GenericTextRunProperties(_typeface, FontSize, null, BrushAt(alpha))));
                }
            }
        }
        if (_streaming && _oldStart >= 0)
        {
            _spans.Add(new ValueSpan<TextRunProperties>(_oldStart, _composite.Length - _oldStart,
                new GenericTextRunProperties(_typeface, FontSize, null, BrushAt(DimAt(_now)))));
        }
        _layout?.Dispose();
        _layout = new TextLayout(_composite, _typeface, FontSize, BrushAt(1), TextAlignment.Left, TextWrapping.Wrap,
            TextTrimming.None, null, FlowDirection, width, double.PositiveInfinity, LineHeight, 0, 0, null,
            _spans.Count > 0 ? [.. _spans] : null);
        _layoutWidth = width;
        _dirty = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        if (_layout is null || _dirty || !Same(width, _layoutWidth))
        {
            Build(width);
        }
        var natural = _layout!.Height;
        var passive = !double.IsNaN(_lastWidth) && !Same(width, _lastWidth);
        _lastWidth = width;
        if (_height is null)
        {
            _height = new MotionValue<double>(this, natural, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
            _natural = natural;
        }
        else if (passive)
        {
            // A window resize cuts straight to the new height.
            _height.Jump(natural);
            _natural = natural;
        }
        else if (Math.Abs(natural - _natural) > 0.25)
        {
            _natural = natural;
            if (_contentChanged && Full)
            {
                _ = _height.AnimateTo(natural, MotionSpec.Spring(HeightSpring));
            }
            else
            {
                _height.Jump(natural);
            }
        }
        _contentChanged = false;
        var measuredWidth = double.IsFinite(width) ? width : _layout.WidthIncludingTrailingWhitespace;
        return new Size(measuredWidth, Math.Max(0, _height.Value));
    }

    private static bool Same(double a, double b) =>
        (double.IsPositiveInfinity(a) && double.IsPositiveInfinity(b)) || Math.Abs(a - b) < 0.01;

    public override void Render(DrawingContext context)
    {
        if (_leaving is not null)
        {
            var left = Math.Clamp((_now - _leaveBorn) / LeaveDuration, 0, 1);
            using (context.PushOpacity(1 - ArcEasings.Standard.Ease(left)))
            {
                _leaving.Draw(context, default);
            }
        }
        if (_layout is null)
        {
            return;
        }
        var enter = Math.Clamp((_now - _enterBorn) / EnterDuration, 0, 1);
        if (enter >= 1)
        {
            _layout.Draw(context, default);
            return;
        }
        var eased = ArcEasings.Enter.Ease(enter);
        using (context.PushOpacity(eased))
        {
            _layout.Draw(context, new Point(0, Rise * (1 - eased)));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Away from a window the fades would only finish later: settle now.
        _chunks.Clear();
        _leaving?.Dispose();
        _leaving = null;
        _enterBorn = double.NegativeInfinity;
        _dimBorn = double.NegativeInfinity;
        _loop?.Stop();
        _dirty = true;
    }
}
