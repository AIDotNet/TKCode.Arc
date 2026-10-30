using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One block of an <see cref="AgentCursor"/> document drawn from a cached <see cref="TextLayout"/>: the text, the agent's
/// selection and change tints under it (Core <see cref="TextRangeDecoration"/>s, rounded, repeated on every wrapped line),
/// the wavy underline that flags a typo, a whole-text fade (the old text of a rewrite leaving in 110ms, an inserted block's
/// text arriving) and per-chunk fades for streamed words (each chunk fades in over 140ms, no blur, no slide: the shared
/// <see cref="AiTextFades"/>, which fades the glyphs in place without laying the text out again). It also answers where
/// its characters and lines are, so cursors and selections are measured from live line boxes.
/// <para>
/// Drawn in code because the chunk fades are per character range of one paragraph and the tints follow the text's wrapped
/// lines, which neither a ControlTheme transition nor a text block's runs can express. A streamed word lays out only this
/// block again; the frames of its fade do not.
/// </para>
/// </summary>
internal sealed class AgentCursorTextView : Control
{
    /// <summary>The block's text.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AgentCursorTextView, string>(nameof(Text), string.Empty);

    /// <summary>The font family (inherited).</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AgentCursorTextView>();

    /// <summary>The font size (inherited).</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AgentCursorTextView>();

    /// <summary>The font weight (inherited).</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<AgentCursorTextView>();

    /// <summary>The text color.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<AgentCursorTextView, IBrush?>(nameof(Foreground));

    /// <summary>The height of each line.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AgentCursorTextView, double>(nameof(LineHeight), double.NaN);

    /// <summary>The wavy underline's color.</summary>
    public static readonly StyledProperty<IBrush?> FlagBrushProperty =
        AvaloniaProperty.Register<AgentCursorTextView, IBrush?>(nameof(FlagBrush));

    /// <summary>How strong the selection fill is (0 to 1 of the agent's color).</summary>
    public static readonly StyledProperty<double> SelectionOpacityProperty =
        AvaloniaProperty.Register<AgentCursorTextView, double>(nameof(SelectionOpacity), 0.22);

    /// <summary>How strong the change tint is (0 to 1 of the agent's color).</summary>
    public static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<AgentCursorTextView, double>(nameof(TintOpacity), 0.12);

    /// <summary>The tints' and selection's corner radius (its top-left value is used).</summary>
    public static readonly StyledProperty<CornerRadius> TintCornerRadiusProperty =
        AvaloniaProperty.Register<AgentCursorTextView, CornerRadius>(nameof(TintCornerRadius), new CornerRadius(4));

    /// <summary>How far the tints and selection reach past the glyphs.</summary>
    public static readonly StyledProperty<Thickness> TintOutsetProperty =
        AvaloniaProperty.Register<AgentCursorTextView, Thickness>(nameof(TintOutset), new Thickness(2, 1));

    /// <summary>The wavy underline's stroke, height and wavelength.</summary>
    public static readonly StyledProperty<double> FlagStrokeWidthProperty =
        AvaloniaProperty.Register<AgentCursorTextView, double>(nameof(FlagStrokeWidth), 1.25);

    /// <inheritdoc cref="FlagStrokeWidthProperty"/>
    public static readonly StyledProperty<double> FlagAmplitudeProperty =
        AvaloniaProperty.Register<AgentCursorTextView, double>(nameof(FlagAmplitude), 1.5);

    /// <inheritdoc cref="FlagStrokeWidthProperty"/>
    public static readonly StyledProperty<double> FlagWavelengthProperty =
        AvaloniaProperty.Register<AgentCursorTextView, double>(nameof(FlagWavelength), 4);

    private static readonly TimeSpan ChunkFade = TimeSpan.FromMilliseconds(140);

    private readonly TextRangeDecoration _selection = new() { Extent = TextRangeExtent.Line };
    private readonly List<TextRangeDecoration> _tints = [];
    private readonly List<TextRangeDecoration> _drawn = [];
    private readonly AiTextFades _fades;
    private TextLayout? _layout;
    private double _layoutWidth = double.NaN;
    private bool _layoutDirty = true;
    private Color _accent = Colors.Gray;
    private double _tintStrength = 1;
    private (int Start, int Length) _flag;
    private MotionValue<double>? _textFade;
    private MotionValue<double>? _tintFade;
    private MotionValue<double>? _flagFade;
    private double _textOpacity = 1;
    private double _flagOpacity;
    private StreamGeometry? _wave;
    private Pen? _wavePen;

    static AgentCursorTextView()
    {
        AffectsMeasure<AgentCursorTextView>(TextProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        AffectsRender<AgentCursorTextView>(ForegroundProperty, FlagBrushProperty, SelectionOpacityProperty, TintOpacityProperty,
            TintCornerRadiusProperty, TintOutsetProperty);
        ClipToBoundsProperty.OverrideDefaultValue<AgentCursorTextView>(false);
    }

    public AgentCursorTextView()
    {
        _fades = new AiTextFades(this, InvalidateVisual) { Duration = ChunkFade };
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="FlagBrushProperty"/>
    public IBrush? FlagBrush { get => GetValue(FlagBrushProperty); set => SetValue(FlagBrushProperty, value); }

    /// <inheritdoc cref="SelectionOpacityProperty"/>
    public double SelectionOpacity { get => GetValue(SelectionOpacityProperty); set => SetValue(SelectionOpacityProperty, value); }

    /// <inheritdoc cref="TintOpacityProperty"/>
    public double TintOpacity { get => GetValue(TintOpacityProperty); set => SetValue(TintOpacityProperty, value); }

    /// <inheritdoc cref="TintCornerRadiusProperty"/>
    public CornerRadius TintCornerRadius { get => GetValue(TintCornerRadiusProperty); set => SetValue(TintCornerRadiusProperty, value); }

    /// <inheritdoc cref="TintOutsetProperty"/>
    public Thickness TintOutset { get => GetValue(TintOutsetProperty); set => SetValue(TintOutsetProperty, value); }

    /// <inheritdoc cref="FlagStrokeWidthProperty"/>
    public double FlagStrokeWidth { get => GetValue(FlagStrokeWidthProperty); set => SetValue(FlagStrokeWidthProperty, value); }

    /// <inheritdoc cref="FlagStrokeWidthProperty"/>
    public double FlagAmplitude { get => GetValue(FlagAmplitudeProperty); set => SetValue(FlagAmplitudeProperty, value); }

    /// <inheritdoc cref="FlagStrokeWidthProperty"/>
    public double FlagWavelength { get => GetValue(FlagWavelengthProperty); set => SetValue(FlagWavelengthProperty, value); }

    /// <summary>The selected range (tests), or (0, 0).</summary>
    internal (int Start, int Length) SelectionRange => (_selection.Start, _selection.Length);

    /// <summary>The tinted ranges (tests).</summary>
    internal IReadOnlyList<(int Start, int Length)> TintRanges => _tints.Select(t => (t.Start, t.Length)).ToList();

    /// <summary>The flagged range (tests), or (0, 0).</summary>
    internal (int Start, int Length) FlagRange => _flag;

    /// <summary>The whole text's opacity right now.</summary>
    internal double TextOpacity => _textOpacity;

    /// <summary>How many chunks are still fading in.</summary>
    internal int FadingChunks => _fades.Count;

    /// <summary>The agent's color: the selection and tints are drawn in it.</summary>
    public void SetAccent(Color color)
    {
        if (_accent == color)
        {
            return;
        }
        _accent = color;
        InvalidateVisual();
    }

    /// <summary>Selects <paramref name="length"/> characters from <paramref name="start"/> (0 clears).</summary>
    public void SetSelection(int start, int length)
    {
        length = Math.Max(0, length);
        if (_selection.Start == start && _selection.Length == length)
        {
            return;
        }
        _selection.Start = start;
        _selection.Length = length;
        InvalidateVisual();
    }

    /// <summary>Tints the whole text as changed (a rewrite while it streams, an inserted block).</summary>
    public void TintAll()
    {
        if (_tints.Count == 1 && _tints[0].Start == 0 && _tints[0].Length >= Text.Length)
        {
            return;
        }
        _tints.Clear();
        _tints.Add(new TextRangeDecoration(0, int.MaxValue / 2));
        RestoreTintStrength();
        InvalidateVisual();
    }

    /// <summary>
    /// Replaces <paramref name="length"/> characters at <paramref name="start"/> with <paramref name="replacement"/>: tints the
    /// new word, fades it in as a chunk and moves the tints after it.
    /// </summary>
    public void Replace(int start, int length, string replacement)
    {
        var text = Text;
        if (start < 0 || start + length > text.Length)
        {
            return;
        }
        var delta = replacement.Length - length;
        foreach (var tint in _tints)
        {
            if (tint.Start >= start + length)
            {
                tint.Start += delta;
            }
            else if (tint.Start + tint.Length > start)
            {
                tint.Length = Math.Max(0, tint.Length + delta);
            }
        }
        Text = text[..start] + replacement + text[(start + length)..];
        var covered = _tints.Any(t => t.Start <= start && t.Start + t.Length >= start + replacement.Length);
        if (!covered)
        {
            _tints.Add(new TextRangeDecoration(start, replacement.Length));
        }
        RestoreTintStrength();
        AddChunk(start, replacement.Length);
    }

    /// <summary>Removes the tints: fading over 200ms (accepting) or at once (undoing, resetting).</summary>
    public void ClearTints(bool fade)
    {
        if (_tints.Count == 0)
        {
            return;
        }
        if (!fade || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _tints.Clear();
            _tintStrength = 1;
            _tintFade?.Jump(1);
            InvalidateVisual();
            return;
        }
        EnsureTintFade();
        _ = FadeTintsOut();
    }

    private async Task FadeTintsOut()
    {
        var done = await _tintFade!.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard));
        if (done)
        {
            _tints.Clear();
            _tintStrength = 1;
            _tintFade.Jump(1);
            InvalidateVisual();
        }
    }

    private void RestoreTintStrength()
    {
        if (_tintFade is { } fade)
        {
            fade.Jump(1);
        }
        _tintStrength = 1;
    }

    private void EnsureTintFade()
    {
        if (_tintFade is null)
        {
            _tintFade = new MotionValue<double>(this, _tintStrength, MotionVectors.Double);
            _tintFade.Changed += v =>
            {
                _tintStrength = v;
                InvalidateVisual();
            };
        }
    }

    /// <summary>Flags <paramref name="length"/> characters from <paramref name="start"/> with the wavy underline, fading it in.</summary>
    public void SetFlag(int start, int length)
    {
        _flag = (start, length);
        _wave = null;
        if (_flagFade is null)
        {
            _flagFade = new MotionValue<double>(this, 0, MotionVectors.Double);
            _flagFade.Changed += v =>
            {
                _flagOpacity = v;
                InvalidateVisual();
            };
        }
        _flagFade.Jump(0);
        _ = _flagFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    /// <summary>Removes the wavy underline.</summary>
    public void ClearFlag()
    {
        _flag = default;
        _wave = null;
        _flagFade?.Jump(0);
        _flagOpacity = 0;
        InvalidateVisual();
    }

    /// <summary>Fades the whole text to <paramref name="opacity"/> over <paramref name="duration"/> (at once below full motion).</summary>
    public void FadeText(double opacity, TimeSpan duration)
    {
        if (_textFade is null)
        {
            _textFade = new MotionValue<double>(this, _textOpacity, MotionVectors.Double);
            _textFade.Changed += v =>
            {
                _textOpacity = v;
                InvalidateVisual();
            };
        }
        if (!ArcMotion.IsFull(this))
        {
            _textFade.Jump(opacity);
            return;
        }
        _ = _textFade.AnimateTo(opacity, MotionSpec.Tween(duration, ArcEasings.Standard));
    }

    /// <summary>Sets the whole text's opacity at once.</summary>
    public void JumpTextOpacity(double opacity)
    {
        _textFade?.Jump(opacity);
        _textOpacity = opacity;
        InvalidateVisual();
    }

    /// <summary>Fades <paramref name="length"/> new characters from <paramref name="start"/> in over 140ms (full motion only).</summary>
    public void AddChunk(int start, int length)
    {
        if (length > 0 && _fades.Add(start, start + length))
        {
            InvalidateVisual();
        }
    }

    /// <summary>Ends every fade at once (stop, reset).</summary>
    public void FinishFades()
    {
        _fades.Clear();
        _textFade?.Jump(1);
        _textOpacity = 1;
        InvalidateVisual();
    }

    /// <summary>How many visual lines the text wraps to at its current width.</summary>
    public int LineCount => EnsureLayout(CurrentWidth()).TextLines.Count;

    /// <summary>The end of visual line <paramref name="line"/> (before its trailing spaces).</summary>
    public int LineEnd(int line)
    {
        var layout = EnsureLayout(CurrentWidth());
        var lines = layout.TextLines;
        if (lines.Count == 0)
        {
            return 0;
        }
        var l = lines[Math.Clamp(line, 0, lines.Count - 1)];
        var end = l.FirstTextSourceIndex + l.Length;
        var text = Text;
        end = Math.Clamp(end, 0, text.Length);
        while (end > l.FirstTextSourceIndex && end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }
        return end;
    }

    /// <summary>Where the caret before character <paramref name="index"/> is: its left edge, a little below its line's middle.</summary>
    public Point CaretPoint(int index)
    {
        var layout = EnsureLayout(CurrentWidth());
        var rect = layout.HitTestTextPosition(Math.Clamp(index, 0, Text.Length));
        return new Point(rect.X, rect.Y + rect.Height * 0.55);
    }

    /// <summary>The character index nearest to <paramref name="point"/> (local coordinates).</summary>
    public int IndexAt(Point point)
    {
        var layout = EnsureLayout(CurrentWidth());
        return Math.Clamp(layout.HitTestPoint(point).TextPosition, 0, Text.Length);
    }

    private double CurrentWidth() => Bounds.Width > 0 ? Bounds.Width : double.IsNaN(_layoutWidth) ? double.PositiveInfinity : _layoutWidth;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty
            || change.Property == FontWeightProperty || change.Property == LineHeightProperty || change.Property == ForegroundProperty)
        {
            _layoutDirty = true;
            _wave = null;
            if (change.Property == TextProperty)
            {
                AutomationPropertiesName();
            }
        }
        else if (change.Property == FlagBrushProperty || change.Property == FlagStrokeWidthProperty)
        {
            _wavePen = null;
        }
    }

    private void AutomationPropertiesName() => Avalonia.Automation.AutomationProperties.SetName(this, Text);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;
        var layout = EnsureLayout(width);
        var height = layout.Height;
        if (Text.Length == 0)
        {
            height = double.IsNaN(LineHeight) ? FontSize * 1.4 : LineHeight;
        }
        return new Size(double.IsInfinity(width) ? layout.WidthIncludingTrailingWhitespace : width, height);
    }

    private TextLayout EnsureLayout(double width)
    {
        if (_layout is not null && !_layoutDirty && (Math.Abs(_layoutWidth - width) < 0.01 || double.IsInfinity(_layoutWidth) && double.IsInfinity(width)))
        {
            return _layout;
        }
        _layout?.Dispose();
        _layoutWidth = width;
        _layoutDirty = false;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        _layout = new TextLayout(Text, typeface, FontSize, Foreground, TextAlignment.Left, TextWrapping.Wrap, null, null,
            FlowDirection.LeftToRight, width, double.PositiveInfinity, LineHeight, 0, 0, null, null);
        _fades.ForgetLines();
        return _layout;
    }

    public override void Render(DrawingContext context)
    {
        var layout = EnsureLayout(Bounds.Width);
        _drawn.Clear();
        if (_tints.Count > 0 && _tintStrength > 0)
        {
            var tint = new ImmutableSolidColorBrush(_accent, TintOpacity);
            var radius = TintCornerRadius.TopLeft;
            foreach (var t in _tints)
            {
                t.Background = tint;
                t.CornerRadius = radius;
                t.Outset = TintOutset;
                t.Extent = TextRangeExtent.Text;
                t.Opacity = _tintStrength;
                _drawn.Add(t);
            }
        }
        if (_selection.Length > 0)
        {
            _selection.Background = new ImmutableSolidColorBrush(_accent, SelectionOpacity);
            _selection.CornerRadius = TintCornerRadius.TopLeft;
            _selection.Outset = new Thickness(TintOutset.Left, 0);
            _drawn.Add(_selection);
        }
        if (_drawn.Count > 0)
        {
            TextRangeRenderer.DrawBackgrounds(context, layout, _drawn, default);
        }
        if (_textOpacity > 0)
        {
            using (_textOpacity < 1 ? context.PushOpacity(_textOpacity) : default)
            {
                _fades.Draw(context, layout, default, int.MaxValue);
            }
        }
        DrawFlag(context, layout);
    }

    private void DrawFlag(DrawingContext context, TextLayout layout)
    {
        if (_flag.Length <= 0 || _flagOpacity <= 0 || FlagBrush is not { } brush)
        {
            return;
        }
        if (_wave is null)
        {
            _wave = new StreamGeometry();
            using var g = _wave.Open();
            var amplitude = FlagAmplitude;
            var half = Math.Max(1, FlagWavelength / 2);
            foreach (var rect in TextRangeRenderer.GetRects(layout, _flag.Start, _flag.Length))
            {
                var y = rect.Bottom + amplitude;
                var x = rect.Left;
                g.BeginFigure(new Point(x, y), false);
                var up = true;
                while (x < rect.Right)
                {
                    var next = Math.Min(rect.Right, x + half);
                    g.QuadraticBezierTo(new Point((x + next) / 2, y + (up ? -amplitude * 2 : amplitude * 2)), new Point(next, y));
                    up = !up;
                    x = next;
                }
                g.EndFigure(false);
            }
        }
        _wavePen ??= new Pen(brush, FlagStrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (_flagOpacity < 1 ? context.PushOpacity(_flagOpacity) : default)
        {
            context.DrawGeometry(null, _wavePen, _wave);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _layout?.Dispose();
        _layout = null;
        _layoutDirty = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AgentCursorTextViewPeer(this);

    private sealed class AgentCursorTextViewPeer(AgentCursorTextView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((AgentCursorTextView)Owner).Text;

        protected override bool IsControlElementCore() => true;
    }
}
