using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Text settings shared by the editor and the painted amount, so both shape every character identically.</summary>
internal static class MoneyInputText
{
    /// <summary><c>font-variant-numeric: tabular-nums; font-kerning: none</c>.</summary>
    public static readonly FontFeatureCollection Features = new() { FontFeature.Parse("-kern"), FontFeature.Parse("+tnum") };

    public static Typeface TypefaceOf(Control control)
        => new(TextElement.GetFontFamily(control), TextElement.GetFontStyle(control), TextElement.GetFontWeight(control));

    public static TextLayout Layout(string text, Typeface typeface, double fontSize, IBrush? brush, double lineHeight, double letterSpacing)
        => new(text, typeface, fontSize, brush, lineHeight: lineHeight, letterSpacing: letterSpacing, fontFeatures: Features);
}

/// <summary>
/// The amount area (Arc <c>.amountWrap</c> + <c>.group</c>): clips its content and scales the group down to fit the room
/// instead of scrolling (left-center origin, smooth spring; the first fit jumps), and carries the sideways kick a refused
/// change gives the amount. Both run on <see cref="MotionValue{T}"/>: the fit retargets continuously while columns
/// spring and the kick starts with an initial velocity, which declarative transitions cannot express.
/// </summary>
internal sealed class MoneyInputStage : Decorator
{
    /// <summary>Arc <c>KICK = physical(.42, .3)</c>.</summary>
    internal static readonly SpringSpec KickSpring = SpringSpec.FromVisual(0.42, 0.3);

    private readonly MatrixTransform _transform = new();
    private MotionValue<double>? _scale;
    private MotionValue<double>? _kick;
    private bool _fitted;

    public MoneyInputStage()
    {
        ClipToBounds = true;
    }

    /// <summary>Current fit scale (1 while the amount fits).</summary>
    internal double Scale => _scale?.Value ?? 1;

    /// <summary>Scale the fit is heading to.</summary>
    internal double TargetScale => _scale?.Target ?? 1;

    /// <summary>Current kick offset in pixels.</summary>
    internal double KickOffset => _kick?.Value ?? 0;

    /// <summary>True while a kick spring is running.</summary>
    internal bool IsKicking => _kick?.IsAnimating ?? false;

    /// <summary>Width of the group before scaling.</summary>
    internal double NaturalWidth { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty)
        {
            if (change.OldValue is Control old)
            {
                old.RenderTransform = null;
            }
            if (change.NewValue is Control child)
            {
                child.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
                child.RenderTransform = _transform;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _fitted = false;
    }

    /// <summary>A refused change kicks the amount toward the press and springs it home (Arc: velocity toward × 160).</summary>
    public void Kick(int toward)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _kick ??= CreateMotion(0);
        _ = _kick.AnimateTo(0, MotionSpec.Spring(KickSpring), toward * 160.0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        // The group keeps its natural width; the stage takes whatever room the shell gives it (flex: 1, min-width: 0).
        child.Measure(Size.Infinity);
        return new Size(0, Math.Min(child.DesiredSize.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var natural = child.DesiredSize;
            child.Arrange(new Rect(0, (finalSize.Height - natural.Height) / 2, natural.Width, natural.Height));
            NaturalWidth = natural.Width;
            Fit(natural.Width, finalSize.Width);
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // The whole area answers clicks (Arc: clicking the amount focuses the editor with the caret at the end).
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    private void Fit(double natural, double room)
    {
        var next = natural > 0 && room > 0 ? Math.Min(1, room / natural) : 1;
        _scale ??= CreateMotion(1);
        if (!_fitted || !ArcMotion.IsFull(this))
        {
            _fitted = true;
            if (Math.Abs(_scale.Value - next) > 1e-6 || _scale.IsAnimating)
            {
                _scale.Jump(next);
            }
            return;
        }
        if (Math.Abs(_scale.Target - next) > 1e-4)
        {
            _ = _scale.AnimateTo(next, MotionSpec.Spring(ArcSpring.Smooth));
        }
    }

    private MotionValue<double> CreateMotion(double initial)
    {
        var motion = new MotionValue<double>(this, initial, MotionVectors.Double);
        motion.Changed += _ => UpdateTransform();
        return motion;
    }

    private void UpdateTransform()
    {
        var scale = _scale?.Value ?? 1;
        var x = _kick?.Value ?? 0;
        _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, 0);
    }
}

/// <summary>
/// Arc <c>.number</c>: the painted amount with the editor's text presenter laid over it at the same origin. The presenter is
/// measured unconstrained so a column spring never rebuilds its text layout.
/// </summary>
internal sealed class MoneyInputNumber : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(Size.Infinity);
            if (i == 0)
            {
                width = child.DesiredSize.Width;
            }
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, child.DesiredSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>How a change of the painted amount moves (Arc <c>Glyphs</c> variants).</summary>
internal enum MoneyInputAmountMotion
{
    /// <summary>Typing: cut straight to the result so the caret never lags.</summary>
    Cut,

    /// <summary>Chips, keys and programmatic changes: columns open and close on springs and changed glyphs roll.</summary>
    Roll
}

/// <summary>
/// The painted amount (Arc <c>.mirror</c>): one column per character keyed by place value, ghost fraction digits and the
/// ghost "0" of an empty amount. A new column opens its width on the morph spring while its opacity fades in on fast; a
/// leaving column closes on smooth with an instant fade; a changed glyph rolls in from 0.55em in the direction of change
/// out of a soft blur while the old one leaves the other way. Columns are feathered at the top and bottom edge while a
/// glyph rolls. Reduced motion: no roll and no width spring, glyphs fade in on instant. Drawn in one control from cached
/// text layouts; every animated value is a <see cref="MotionValue{T}"/> because the roll direction and the per-column
/// presence (AnimatePresence) are decided at run time.
/// </summary>
internal sealed class MoneyInputAmount : Control
{
    private const double Rise = 0.55;
    private const double Feather = 0.18;
    private const double BlurSoft = 4;
    private const double BlurSubtle = 2;

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MoneyInputAmount>();

    /// <summary>Ghost digits: Arc <c>.column[data-ghost]</c>.</summary>
    public static readonly StyledProperty<IBrush?> GhostBrushProperty =
        AvaloniaProperty.Register<MoneyInputAmount, IBrush?>(nameof(GhostBrush));

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<MoneyInputAmount, double>(nameof(LineHeight), 43.2);

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? GhostBrush { get => GetValue(GhostBrushProperty); set => SetValue(GhostBrushProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    private readonly List<Column> _columns = new();
    private readonly Dictionary<char, GlyphLayouts> _glyphs = new();
    private readonly Action<double> _measure;
    private readonly Action<double> _visual;
    private IBrush? _featherMask;
    private double _featherHeight = -1;
    private bool _hasState;

    static MoneyInputAmount()
    {
        AffectsRender<MoneyInputAmount>(ForegroundProperty, GhostBrushProperty);
        AffectsMeasure<MoneyInputAmount>(LineHeightProperty);
    }

    public MoneyInputAmount()
    {
        _measure = _ => InvalidateMeasure();
        _visual = _ => InvalidateVisual();
        IsHitTestVisible = true;
    }

    /// <summary>The characters currently shown (leaving columns excluded), ghosts included.</summary>
    internal string ShownText
    {
        get
        {
            var chars = new char[_columns.Count];
            var n = 0;
            foreach (var column in _columns)
            {
                if (!column.Leaving)
                {
                    chars[n++] = column.Char;
                }
            }
            return new string(chars, 0, n);
        }
    }

    /// <summary>Number of columns still painted, leaving ones included.</summary>
    internal int ColumnCount => _columns.Count;

    /// <summary>Ghost flags of the shown columns, in order.</summary>
    internal IEnumerable<bool> GhostFlags => _columns.Where(c => !c.Leaving).Select(c => c.Ghost);

    /// <summary>True while any glyph rolls or any column opens or closes.</summary>
    internal bool IsRolling => _columns.Any(c => c.Leaving || c.Width.IsAnimating || c.Current.IsMoving || c.Exiting is { Count: > 0 });

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextElement.FontFamilyProperty || change.Property == TextElement.FontSizeProperty ||
            change.Property == TextElement.FontWeightProperty || change.Property == TextElement.FontStyleProperty ||
            change.Property == TextElement.LetterSpacingProperty || change.Property == LineHeightProperty ||
            change.Property == ForegroundProperty || change.Property == GhostBrushProperty)
        {
            _glyphs.Clear();
            _featherHeight = -1;
            foreach (var column in _columns)
            {
                column.TargetWidth = column.Leaving ? 0 : Advance(column.Char);
                if (!column.Width.IsAnimating)
                {
                    column.Width.Jump(column.TargetWidth);
                }
            }
            InvalidateMeasure();
        }
    }

    /// <summary>Applies the next set of columns (Arc re-render of <c>Glyphs</c>).</summary>
    public void Update(IReadOnlyList<MoneyInputPart> parts, int direction, MoneyInputAmountMotion motion)
    {
        var first = !_hasState;
        _hasState = true;
        var full = ArcMotion.IsFull(this);
        // Typing and reduced motion both cut the columns; only reduced motion fades a changed glyph in.
        var cut = first || motion == MoneyInputAmountMotion.Cut || !full;
        var fade = !first && motion != MoneyInputAmountMotion.Cut && !full;
        foreach (var column in _columns)
        {
            column.Seen = false;
        }
        foreach (var part in parts)
        {
            var column = Find(part.Rank);
            if (column is null)
            {
                column = new Column(this, part.Rank, part.Char, part.Ghost, Advance(part.Char));
                Insert(column);
                if (!cut)
                {
                    column.Width.Jump(0);
                    column.Opacity.Jump(0);
                    _ = column.Width.AnimateTo(column.TargetWidth, MotionSpec.Spring(ArcSpring.Morph));
                    _ = column.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                }
                column.Seen = true;
                continue;
            }
            column.Seen = true;
            var target = Advance(part.Char);
            if (column.Leaving)
            {
                column.Leaving = false;
                column.TargetWidth = target;
                if (cut)
                {
                    column.Width.Jump(target);
                    column.Opacity.Jump(1);
                }
                else
                {
                    _ = column.Width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
                    _ = column.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                }
            }
            if (column.Char != part.Char)
            {
                SwapGlyph(column, part.Char, direction, motion == MoneyInputAmountMotion.Cut || first, fade);
                column.TargetWidth = target;
                if (column.Width.IsAnimating)
                {
                    _ = column.Width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
                }
                else
                {
                    column.Width.Jump(target);
                }
            }
            if (column.Ghost != part.Ghost)
            {
                column.Ghost = part.Ghost;
                // A CSS color transition (standard) that also runs while typing; reduced motion removes it.
                if (first || !full)
                {
                    column.GhostMix.Jump(part.Ghost ? 1 : 0);
                }
                else
                {
                    _ = column.GhostMix.AnimateTo(part.Ghost ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
                }
            }
        }
        for (var i = _columns.Count - 1; i >= 0; i--)
        {
            var column = _columns[i];
            if (column.Seen || column.Leaving)
            {
                continue;
            }
            if (cut)
            {
                column.Dispose();
                _columns.RemoveAt(i);
                continue;
            }
            column.Leaving = true;
            column.TargetWidth = 0;
            _ = column.Opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            RemoveWhenClosed(column, column.Width.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)));
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void SwapGlyph(Column column, char next, int direction, bool cutGlyph, bool fade)
    {
        column.Char = next;
        if (cutGlyph || fade)
        {
            column.ClearExiting();
            column.Current.Reset(next);
            if (fade)
            {
                column.Current.Opacity.Jump(0);
                _ = column.Current.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            return;
        }
        // The old glyph leaves the other way in the instant duration; the new one rises out of a soft blur.
        var leaving = column.Current;
        column.Exiting ??= new List<Glyph>(2);
        column.Exiting.Add(leaving);
        leaving.BeginExit(-direction);
        RemoveWhenExited(column, leaving, leaving.Progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)));
        var entering = new Glyph(this, next);
        column.Current = entering;
        entering.Y.Jump(direction * Rise);
        entering.Opacity.Jump(0);
        entering.Blur.Jump(BlurSoft);
        _ = entering.Y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
        _ = entering.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        _ = entering.Blur.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Enter));
    }

    private async void RemoveWhenClosed(Column column, Task<bool> closing)
    {
        if (await closing && column.Leaving && _columns.Remove(column))
        {
            column.Dispose();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private async void RemoveWhenExited(Column column, Glyph glyph, Task<bool> exiting)
    {
        await exiting;
        if (column.Exiting is { } list && list.Remove(glyph))
        {
            glyph.Dispose();
            InvalidateVisual();
        }
    }

    private Column? Find(int rank)
    {
        foreach (var column in _columns)
        {
            if (column.Rank == rank)
            {
                return column;
            }
        }
        return null;
    }

    private void Insert(Column column)
    {
        var index = 0;
        while (index < _columns.Count && _columns[index].Rank < column.Rank)
        {
            index++;
        }
        _columns.Insert(index, column);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        foreach (var column in _columns)
        {
            width += Math.Max(0, column.Width.Value);
        }
        return new Size(width, LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        if (_columns.Count == 0)
        {
            return;
        }
        var em = TextElement.GetFontSize(this);
        var lineHeight = LineHeight;
        var feather = Feather * em;
        var band = new Rect(-em, -feather, Bounds.Width + 2 * em, lineHeight + 2 * feather);
        var rolling = false;
        foreach (var column in _columns)
        {
            if (column.Current.IsMoving || column.Exiting is { Count: > 0 })
            {
                rolling = true;
                break;
            }
        }
        // Each column clips its rolling glyph a feather beyond the line box and fades it at the edges (Arc .column mask).
        DrawingContext.PushedState clip = default;
        DrawingContext.PushedState mask = default;
        if (rolling)
        {
            clip = context.PushClip(band);
            mask = context.PushOpacityMask(FeatherMask(lineHeight, feather), band);
        }
        var x = 0.0;
        foreach (var column in _columns)
        {
            var width = Math.Max(0, column.Width.Value);
            var opacity = Math.Clamp(column.Opacity.Value, 0, 1);
            if (opacity > 0.001)
            {
                var ghost = Math.Clamp(column.GhostMix.Value, 0, 1);
                if (column.Exiting is { } exiting)
                {
                    foreach (var glyph in exiting)
                    {
                        var p = Math.Clamp(glyph.Progress.Value, 0, 1);
                        var y = glyph.ExitFromY + (glyph.ExitDirection * Rise - glyph.ExitFromY) * p;
                        DrawGlyph(context, glyph.Char, x, width, y * em, opacity * glyph.ExitFromOpacity * (1 - p),
                            glyph.ExitFromBlur + (BlurSubtle - glyph.ExitFromBlur) * p, ghost);
                    }
                }
                var current = column.Current;
                DrawGlyph(context, current.Char, x, width, current.Y.Value * em, opacity * Math.Clamp(current.Opacity.Value, 0, 1),
                    current.Blur.Value, ghost);
            }
            x += width;
        }
        if (rolling)
        {
            mask.Dispose();
            clip.Dispose();
        }
    }

    private void DrawGlyph(DrawingContext context, char c, double x, double width, double y, double opacity, double blur, double ghost)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        var glyph = Layouts(c);
        var origin = new Point(x + (width - glyph.Width) / 2, y);
        var effect = ArcBlur.Get(blur);
        DrawingContext.PushedState blurred = default;
        if (effect is not null)
        {
            blurred = context.PushEffect(effect, new Rect(origin.X - 8, origin.Y - 8, glyph.Width + 16, LineHeight + 16));
        }
        if (ghost < 0.999)
        {
            DrawLayout(context, glyph.Normal, origin, opacity * (1 - ghost));
        }
        if (ghost > 0.001)
        {
            DrawLayout(context, glyph.Ghost, origin, opacity * ghost);
        }
        if (effect is not null)
        {
            blurred.Dispose();
        }
    }

    private static void DrawLayout(DrawingContext context, TextLayout layout, Point origin, double opacity)
    {
        if (opacity >= 0.999)
        {
            layout.Draw(context, origin);
            return;
        }
        using (context.PushOpacity(opacity))
        {
            layout.Draw(context, origin);
        }
    }

    private IBrush FeatherMask(double lineHeight, double feather)
    {
        var height = lineHeight + 2 * feather;
        if (_featherMask is null || Math.Abs(_featherHeight - height) > 0.01)
        {
            _featherHeight = height;
            var edge = Math.Clamp(feather * 1.4 / height, 0, 0.5);
            _featherMask = new Avalonia.Media.Immutable.ImmutableLinearGradientBrush(
                new[]
                {
                    new Avalonia.Media.Immutable.ImmutableGradientStop(0, Colors.Transparent),
                    new Avalonia.Media.Immutable.ImmutableGradientStop(edge, Colors.Black),
                    new Avalonia.Media.Immutable.ImmutableGradientStop(1 - edge, Colors.Black),
                    new Avalonia.Media.Immutable.ImmutableGradientStop(1, Colors.Transparent)
                },
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        }
        return _featherMask;
    }

    /// <summary>The advance of <paramref name="c"/> in the amount's font (letter spacing included).</summary>
    internal double Advance(char c) => Layouts(c).Width;

    private GlyphLayouts Layouts(char c)
    {
        if (_glyphs.TryGetValue(c, out var cached))
        {
            return cached;
        }
        var typeface = MoneyInputText.TypefaceOf(this);
        var size = TextElement.GetFontSize(this);
        var spacing = TextElement.GetLetterSpacing(this);
        var text = c.ToString();
        var normal = MoneyInputText.Layout(text, typeface, size, Foreground, LineHeight, spacing);
        var ghost = MoneyInputText.Layout(text, typeface, size, GhostBrush, LineHeight, spacing);
        var layouts = new GlyphLayouts(normal, ghost, normal.WidthIncludingTrailingWhitespace);
        _glyphs[c] = layouts;
        return layouts;
    }

    private sealed record GlyphLayouts(TextLayout Normal, TextLayout Ghost, double Width);

    private sealed class Column : IDisposable
    {
        public Column(MoneyInputAmount owner, int rank, char c, bool ghost, double width)
        {
            Rank = rank;
            Char = c;
            Ghost = ghost;
            TargetWidth = width;
            Width = new MotionValue<double>(owner, width, MotionVectors.Double);
            Width.Changed += owner._measure;
            Opacity = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Opacity.Changed += owner._visual;
            GhostMix = new MotionValue<double>(owner, ghost ? 1 : 0, MotionVectors.Double);
            GhostMix.Changed += owner._visual;
            Current = new Glyph(owner, c);
        }

        public int Rank { get; }
        public char Char { get; set; }
        public bool Ghost { get; set; }
        public bool Leaving { get; set; }
        public bool Seen { get; set; }
        public double TargetWidth { get; set; }
        public MotionValue<double> Width { get; }
        public MotionValue<double> Opacity { get; }
        public MotionValue<double> GhostMix { get; }
        public Glyph Current { get; set; }
        public List<Glyph>? Exiting { get; set; }

        public void ClearExiting()
        {
            if (Exiting is null)
            {
                return;
            }
            foreach (var glyph in Exiting)
            {
                glyph.Dispose();
            }
            Exiting.Clear();
        }

        public void Dispose()
        {
            Width.Dispose();
            Opacity.Dispose();
            GhostMix.Dispose();
            Current.Dispose();
            ClearExiting();
        }
    }

    private sealed class Glyph : IDisposable
    {
        public Glyph(MoneyInputAmount owner, char c)
        {
            Char = c;
            Y = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Opacity = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Blur = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Progress = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Y.Changed += owner._visual;
            Opacity.Changed += owner._visual;
            Blur.Changed += owner._visual;
            Progress.Changed += owner._visual;
        }

        public char Char { get; private set; }

        /// <summary>Vertical offset in em.</summary>
        public MotionValue<double> Y { get; }
        public MotionValue<double> Opacity { get; }
        public MotionValue<double> Blur { get; }

        /// <summary>Exit progress (0 → 1).</summary>
        public MotionValue<double> Progress { get; }

        public int ExitDirection { get; private set; }

        /// <summary>Where the exit starts (a glyph can leave mid-roll).</summary>
        public double ExitFromY { get; private set; }

        public double ExitFromOpacity { get; private set; } = 1;

        public double ExitFromBlur { get; private set; }

        public bool IsMoving => Y.IsAnimating || Math.Abs(Y.Value) > 1e-4;

        public void Reset(char c)
        {
            Char = c;
            Y.Jump(0);
            Opacity.Jump(1);
            Blur.Jump(0);
        }

        public void BeginExit(int direction)
        {
            ExitDirection = direction;
            ExitFromY = Y.Value;
            ExitFromOpacity = Math.Clamp(Opacity.Value, 0, 1);
            ExitFromBlur = Math.Max(0, Blur.Value);
            Y.Stop();
            Opacity.Stop();
            Blur.Stop();
            Progress.Jump(0);
        }

        public void Dispose()
        {
            Y.Dispose();
            Opacity.Dispose();
            Blur.Dispose();
            Progress.Dispose();
        }
    }
}
