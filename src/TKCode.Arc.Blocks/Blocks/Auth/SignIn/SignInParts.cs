// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli): the layout and motion pieces of sign-in.tsx
// and sign-in.module.css that Avalonia has no direct equivalent for (AnimatePresence popLayout, the height glide, the
// rolling seconds, flex-wrap, text-wrap: balance / pretty, text-underline-offset).
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The motion constants of Arc's sign-in block.</summary>
internal static class SignInMotion
{
    /// <summary>motion's ease for a tween that names none: easeOut, cubic-bezier(0, 0, .58, 1) (measured on the preview).</summary>
    public static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>Arc's <c>spring.smooth</c> {.4, 0}.</summary>
    public static readonly SpringSpec Smooth = SpringSpec.FromVisual(0.4, 0);

    /// <summary>Arc's <c>spring.snappy</c> {.26, 0}.</summary>
    public static readonly SpringSpec Snappy = SpringSpec.FromVisual(0.26, 0);

    /// <summary>Arc's <c>spring.morph</c> {.42, 0}.</summary>
    public static readonly SpringSpec Morph = SpringSpec.FromVisual(0.42, 0);

    public static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    public static MotionSpec Tween(double milliseconds, Easing easing, double delay = 0) =>
        MotionSpec.Tween(Ms(milliseconds), easing, Ms(delay));

    public static MotionSpec Spring(SpringSpec spring, double delay = 0) => MotionSpec.Spring(spring, Ms(delay));

    /// <summary>motion's <c>circOut</c>.</summary>
    public static double CircOut(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return Math.Sqrt(1 - (1 - p) * (1 - p));
    }
}

/// <summary>
/// The animated values of one element (Arc's <c>motion.div</c>): a translation, a uniform scale about the element's
/// <see cref="Visual.RenderTransformOrigin"/>, an opacity and a blur. Each is a <see cref="MotionValue{T}"/> created on
/// first use, written straight to the element (one shared matrix transform, cached blur effects).
/// </summary>
internal sealed class SignInLayer
{
    private readonly Control _target;
    private readonly MatrixTransform _transform = new();
    private MotionValue<double>? _x;
    private MotionValue<double>? _y;
    private MotionValue<double>? _scale;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _blur;

    public SignInLayer(Control target)
    {
        _target = target;
        _target.RenderTransform = _transform;
    }

    public Control Target => _target;

    public MotionValue<double> X => _x ??= Create(0, ApplyTransform);

    public MotionValue<double> Y => _y ??= Create(0, ApplyTransform);

    public MotionValue<double> Scale => _scale ??= Create(1, ApplyTransform);

    public MotionValue<double> Opacity => _opacity ??= Create(1, v => _target.Opacity = Math.Clamp(v, 0, 1));

    public MotionValue<double> Blur => _blur ??= Create(0, v => _target.Effect = ArcBlur.Get(Math.Max(0, v)));

    /// <summary>The translation right now.</summary>
    public Vector Offset => new(_x?.Value ?? 0, _y?.Value ?? 0);

    /// <summary>Puts every value where an element at rest has it.</summary>
    public void Rest()
    {
        _x?.Jump(0);
        _y?.Jump(0);
        _scale?.Jump(1);
        _opacity?.Jump(1);
        _blur?.Jump(0);
    }

    private MotionValue<double> Create(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(_target, initial, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    private void ApplyTransform(double _)
    {
        var s = _scale?.Value ?? 1;
        _transform.Matrix = Matrix.CreateScale(s, s) * Matrix.CreateTranslation(_x?.Value ?? 0, _y?.Value ?? 0);
    }
}

/// <summary>
/// The card's viewport (Arc's <c>useStepHeight</c>): normally as tall as its content; while the step changes its height
/// follows the new content on the smooth spring from the height it had, then returns to the content's height, so field
/// messages that open later move nothing but themselves. Always clips (Arc: <c>overflow: hidden</c>; the steps carry
/// their own padding, so focus rings stay inside). Code-driven because the target is a measured height.
/// </summary>
internal sealed class SignInViewport : Decorator
{
    private MotionValue<double>? _height;
    private bool _gliding;
    private bool _needsTarget;
    private int _run;

    public SignInViewport()
    {
        ClipToBounds = true;
    }

    /// <summary>True while the height follows a step change.</summary>
    public bool IsGliding => _gliding;

    /// <summary>The height the viewport has right now while gliding.</summary>
    public double GlideHeight => _height?.Value ?? Bounds.Height;

    /// <summary>
    /// Starts the glide from the current height (Arc: only under full motion and when both heights are known); call it
    /// before the new step is laid out.
    /// </summary>
    public void BeginGlide()
    {
        var from = _gliding && _height is not null ? _height.Value : Bounds.Height;
        if (from <= 0 || !IsLoaded || !ArcMotion.IsFull(this))
        {
            StopGlide();
            return;
        }
        if (_height is null)
        {
            _height = new MotionValue<double>(this, from, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
        }
        if (!_gliding)
        {
            _height.Jump(from);
        }
        _gliding = true;
        _needsTarget = true;
        InvalidateMeasure();
    }

    /// <summary>Back to the content's height at once.</summary>
    public void StopGlide()
    {
        _run++;
        _needsTarget = false;
        if (_gliding)
        {
            _gliding = false;
            _height?.Stop();
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var desired = child.DesiredSize;
        if (!_gliding || _height is null)
        {
            return desired;
        }
        var target = desired.Height;
        if (target <= 0)
        {
            StopGlide();
            return desired;
        }
        // A size change while gliding retargets the spring (Arc's ResizeObserver calls glide again); the value keeps its velocity.
        if (_needsTarget || Math.Abs(_height.Target - target) > 0.01)
        {
            _needsTarget = false;
            Glide(target);
        }
        return desired.WithHeight(Math.Max(0, _height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, Child.DesiredSize.Height)));
        return finalSize;
    }

    private async void Glide(double target)
    {
        var run = ++_run;
        var done = await _height!.AnimateTo(target, SignInMotion.Spring(SignInMotion.Smooth));
        if (done && run == _run)
        {
            _gliding = false;
            InvalidateMeasure();
        }
    }
}

/// <summary>
/// Holds the card's steps (Arc's <c>AnimatePresence mode="popLayout"</c>): the current step sizes the panel; a leaving step
/// is taken out of the flow at once and stays where it was, at the width it had, until it is removed.
/// </summary>
internal sealed class SignInStepHost : Panel
{
    private readonly Dictionary<Control, double> _exiting = new();

    /// <summary>The step that sizes the panel.</summary>
    public Control? Current { get; private set; }

    /// <summary>Steps on their way out.</summary>
    public IReadOnlyCollection<Control> Exiting => _exiting.Keys;

    /// <summary>Adds <paramref name="step"/> on top and makes it the current step.</summary>
    public void Show(Control step)
    {
        Children.Add(step);
        Current = step;
    }

    /// <summary>Takes <paramref name="step"/> out of the flow; it keeps its width and position and no longer takes input.</summary>
    public void BeginExit(Control step)
    {
        _exiting[step] = step.Bounds.Width > 0 ? step.Bounds.Width : double.NaN;
        step.IsHitTestVisible = false;
        if (ReferenceEquals(Current, step))
        {
            Current = null;
        }
        InvalidateMeasure();
    }

    /// <summary>Removes a step for good.</summary>
    public void Remove(Control step)
    {
        _exiting.Remove(step);
        Children.Remove(step);
        if (ReferenceEquals(Current, step))
        {
            Current = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = default(Size);
        foreach (var child in Children)
        {
            if (_exiting.TryGetValue(child, out var width))
            {
                child.Measure(new Size(double.IsNaN(width) ? availableSize.Width : width, double.PositiveInfinity));
                continue;
            }
            child.Measure(availableSize.WithHeight(double.PositiveInfinity));
            if (ReferenceEquals(child, Current))
            {
                desired = child.DesiredSize;
            }
        }
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (_exiting.TryGetValue(child, out var width))
            {
                child.Arrange(new Rect(0, 0, double.IsNaN(width) ? finalSize.Width : width, child.DesiredSize.Height));
                continue;
            }
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// A line that swaps its content in place (Arc's footer status and resend label: <c>AnimatePresence mode="popLayout"</c>):
/// the new content rises 6px out of a 4px blur on the enter curve (240ms); the old one leaves the flow at once and lifts
/// 4px into a 2px blur while it fades (160ms). Reduced: the new content fades in over 120ms and the old one goes at once.
/// The first content shows without motion.
/// </summary>
internal sealed class SignInSwap : Control
{
    private readonly List<(Control Content, SignInLayer Layer, Size Size)> _exiting = new();
    private Control? _current;
    private SignInLayer? _currentLayer;
    private object? _key;
    private bool _hasKey;

    public SignInSwap()
    {
        ClipToBounds = false;
    }

    /// <summary>The content showing now.</summary>
    public Control? Current => _current;

    /// <summary>Contents still leaving.</summary>
    public int ExitingCount => _exiting.Count;

    /// <summary>The key of the content showing now.</summary>
    public object? Key => _key;

    /// <summary>Shows <paramref name="content"/> under <paramref name="key"/>; the same key keeps the current content.</summary>
    public void Show(object? key, Func<Control> content)
    {
        if (_hasKey && Equals(key, _key))
        {
            return;
        }
        var first = !_hasKey;
        _hasKey = true;
        _key = key;
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = !first && IsLoaded && level != ArcMotionLevel.Off;
        if (_current is not null && _currentLayer is not null)
        {
            var old = _current;
            var oldLayer = _currentLayer;
            var size = old.Bounds.Size;
            _current = null;
            _currentLayer = null;
            if (animate && level == ArcMotionLevel.Full)
            {
                _exiting.Add((old, oldLayer, size));
                Leave(old, oldLayer);
            }
            else
            {
                RemoveChild(old);
            }
        }
        var next = content();
        _current = next;
        _currentLayer = new SignInLayer(next);
        LogicalChildren.Add(next);
        VisualChildren.Add(next);
        if (animate)
        {
            if (level == ArcMotionLevel.Full)
            {
                _currentLayer.Y.Jump(6);
                _currentLayer.Opacity.Jump(0);
                _currentLayer.Blur.Jump(4);
                _ = _currentLayer.Y.AnimateTo(0, SignInMotion.Tween(240, ArcEasings.Enter));
                _ = _currentLayer.Opacity.AnimateTo(1, SignInMotion.Tween(240, ArcEasings.Enter));
                _ = _currentLayer.Blur.AnimateTo(0, SignInMotion.Tween(240, ArcEasings.Enter));
            }
            else
            {
                _currentLayer.Opacity.Jump(0);
                _ = _currentLayer.Opacity.AnimateTo(1, SignInMotion.Tween(120, SignInMotion.EaseOut));
            }
        }
        InvalidateMeasure();
    }

    private async void Leave(Control old, SignInLayer layer)
    {
        _ = layer.Y.AnimateTo(-4, SignInMotion.Tween(160, SignInMotion.EaseOut));
        _ = layer.Blur.AnimateTo(2, SignInMotion.Tween(160, SignInMotion.EaseOut));
        await layer.Opacity.AnimateTo(0, SignInMotion.Tween(160, SignInMotion.EaseOut));
        _exiting.RemoveAll(e => ReferenceEquals(e.Content, old));
        RemoveChild(old);
    }

    private void RemoveChild(Control child)
    {
        VisualChildren.Remove(child);
        LogicalChildren.Remove(child);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var (content, _, size) in _exiting)
        {
            content.Measure(size.Width > 0 ? new Size(size.Width, double.PositiveInfinity) : availableSize);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Measure(availableSize);
        return _current.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (content, _, size) in _exiting)
        {
            content.Arrange(new Rect(0, 0, size.Width > 0 ? size.Width : content.DesiredSize.Width, content.DesiredSize.Height));
        }
        _current?.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, 0), _current.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The seconds before a new code can be sent ("0:29"), one column per character with tabular digits. A character that
/// changes rolls: the new one drops in from 0.7em above while the old one drops 0.7em below (snappy spring), each
/// fading and blurring 2px over 160ms; a timer that restarts rolls the other way (Arc's <c>RollingTime</c>). Reduced and
/// off swap at once.
/// </summary>
internal sealed class SignInRollingTime : Control
{
    private sealed class Glyph(TextBlock block, SignInLayer layer)
    {
        public TextBlock Block { get; } = block;

        public SignInLayer Layer { get; } = layer;

        public bool IsLeaving { get; set; }
    }

    private readonly List<List<Glyph>> _columns = new();
    private string _text = string.Empty;
    private int _seconds = -1;
    private double _digitWidth;
    private double _digitFontSize;

    public SignInRollingTime()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        IsHitTestVisible = false;
    }

    /// <summary>The text showing ("0:29").</summary>
    public string Text => _text;

    /// <summary>The seconds showing.</summary>
    public int Seconds => _seconds;

    /// <summary>Glyphs still leaving (tests).</summary>
    public int LeavingCount => _columns.Sum(c => c.Count(g => g.IsLeaving));

    /// <summary>Arc's text for a number of seconds: minutes, a colon and two-digit seconds.</summary>
    public static string Format(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>Shows <paramref name="seconds"/>; the first value and a value set while detached show without motion.</summary>
    public void SetSeconds(int seconds)
    {
        if (seconds == _seconds)
        {
            return;
        }
        var direction = _seconds < 0 || seconds < _seconds ? 1 : -1;
        var animate = _seconds >= 0 && IsLoaded && ArcMotion.IsFull(this);
        _seconds = seconds;
        var text = Format(seconds);
        _text = text;
        // Columns beyond the new text go at once (their span unmounts), new columns appear without motion.
        while (_columns.Count > text.Length)
        {
            foreach (var glyph in _columns[^1])
            {
                RemoveBlock(glyph.Block);
            }
            _columns.RemoveAt(_columns.Count - 1);
        }
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i].ToString();
            if (i >= _columns.Count)
            {
                _columns.Add([AddGlyph(character)]);
                continue;
            }
            var column = _columns[i];
            var current = column.LastOrDefault(g => !g.IsLeaving);
            if (current is not null && current.Block.Text == character)
            {
                continue;
            }
            if (!animate)
            {
                foreach (var glyph in column)
                {
                    RemoveBlock(glyph.Block);
                }
                column.Clear();
                column.Add(AddGlyph(character));
                continue;
            }
            var em = FontSizeNow() * 0.7;
            if (current is not null)
            {
                Leave(column, current, em * direction);
            }
            var entering = AddGlyph(character);
            column.Add(entering);
            entering.Layer.Y.Jump(-em * direction);
            entering.Layer.Opacity.Jump(0);
            entering.Layer.Blur.Jump(2);
            _ = entering.Layer.Y.AnimateTo(0, SignInMotion.Spring(SignInMotion.Snappy));
            _ = entering.Layer.Opacity.AnimateTo(1, SignInMotion.Tween(160, SignInMotion.EaseOut));
            _ = entering.Layer.Blur.AnimateTo(0, SignInMotion.Tween(160, SignInMotion.EaseOut));
        }
        InvalidateMeasure();
    }

    private async void Leave(List<Glyph> column, Glyph glyph, double to)
    {
        glyph.IsLeaving = true;
        _ = glyph.Layer.Opacity.AnimateTo(0, SignInMotion.Tween(160, SignInMotion.EaseOut));
        _ = glyph.Layer.Blur.AnimateTo(2, SignInMotion.Tween(160, SignInMotion.EaseOut));
        await glyph.Layer.Y.AnimateTo(to, SignInMotion.Spring(SignInMotion.Snappy));
        if (column.Remove(glyph))
        {
            RemoveBlock(glyph.Block);
        }
    }

    private Glyph AddGlyph(string character)
    {
        var block = new TextBlock { Text = character };
        LogicalChildren.Add(block);
        VisualChildren.Add(block);
        return new Glyph(block, new SignInLayer(block));
    }

    private void RemoveBlock(TextBlock block)
    {
        VisualChildren.Remove(block);
        LogicalChildren.Remove(block);
        InvalidateMeasure();
    }

    private double FontSizeNow() => GetValue(TextElement.FontSizeProperty);

    /// <summary>The widest digit (CSS <c>tabular-nums</c> gives every digit that advance).</summary>
    private double DigitWidth()
    {
        var size = FontSizeNow();
        if (_digitWidth <= 0 || Math.Abs(_digitFontSize - size) > 0.01)
        {
            var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty), GetValue(TextElement.FontStyleProperty),
                GetValue(TextElement.FontWeightProperty));
            var widest = 0.0;
            for (var d = 0; d <= 9; d++)
            {
                using var layout = new TextLayout(d.ToString(), typeface, size, null);
                widest = Math.Max(widest, layout.WidthIncludingTrailingWhitespace);
            }
            _digitWidth = widest;
            _digitFontSize = size;
        }
        return _digitWidth;
    }

    private double ColumnWidth(int index, List<Glyph> column)
    {
        if (index < _text.Length && char.IsDigit(_text[index]))
        {
            return DigitWidth();
        }
        var width = 0.0;
        foreach (var glyph in column)
        {
            width = Math.Max(width, glyph.Block.DesiredSize.Width);
        }
        return width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        for (var i = 0; i < _columns.Count; i++)
        {
            foreach (var glyph in _columns[i])
            {
                glyph.Block.Measure(Size.Infinity);
                height = Math.Max(height, glyph.Block.DesiredSize.Height);
            }
            width += ColumnWidth(i, _columns[i]);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        for (var i = 0; i < _columns.Count; i++)
        {
            var columnWidth = ColumnWidth(i, _columns[i]);
            foreach (var glyph in _columns[i])
            {
                var size = glyph.Block.DesiredSize;
                glyph.Block.Arrange(new Rect(x + (columnWidth - size.Width) / 2, 0, size.Width, size.Height));
            }
            x += columnWidth;
        }
        return finalSize;
    }
}

/// <summary>
/// A round avatar (Arc's Avatar inside the sign-in card): a photo cropped to the circle (object-fit cover) inside a 1px
/// border, or the initials of <see cref="DisplayName"/> on the muted surface.
/// </summary>
internal sealed class SignInAvatar : Control
{
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<SignInAvatar, string?>(nameof(DisplayName));

    public static readonly StyledProperty<IImage?> PhotoProperty =
        AvaloniaProperty.Register<SignInAvatar, IImage?>(nameof(Photo));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SignInAvatar, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<SignInAvatar, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<SignInAvatar>();

    public static readonly StyledProperty<double> InitialsFontSizeProperty =
        AvaloniaProperty.Register<SignInAvatar, double>(nameof(InitialsFontSize), 12);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<SignInAvatar>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<SignInAvatar>();

    private TextLayout? _initials;
    private Pen? _pen;
    private IBrush? _penBrush;
    private readonly EllipseGeometry _clip = new();

    static SignInAvatar()
    {
        AffectsRender<SignInAvatar>(DisplayNameProperty, PhotoProperty, FillProperty, StrokeProperty, ForegroundProperty,
            InitialsFontSizeProperty, FontFamilyProperty, FontWeightProperty);
    }

    public SignInAvatar()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    public IImage? Photo { get => GetValue(PhotoProperty); set => SetValue(PhotoProperty, value); }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double InitialsFontSize { get => GetValue(InitialsFontSizeProperty); set => SetValue(InitialsFontSizeProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>The initials drawn without a photo ("EC").</summary>
    public string Initials => Avatar.ComputeInitials(DisplayName);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayNameProperty || change.Property == ForegroundProperty || change.Property == InitialsFontSizeProperty ||
            change.Property == FontFamilyProperty || change.Property == FontWeightProperty)
        {
            _initials?.Dispose();
            _initials = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2;
        if (Fill is { } fill)
        {
            context.DrawEllipse(fill, null, center, radius, radius);
        }
        if (Photo is { } photo && photo.Size.Width > 0 && photo.Size.Height > 0)
        {
            // Inside the 1px border, like Arc's absolutely placed img.
            var inner = Math.Max(0, radius - 1);
            _clip.Center = center;
            _clip.RadiusX = inner;
            _clip.RadiusY = inner;
            using (context.PushGeometryClip(_clip))
            {
                var dest = new Rect(center.X - inner, center.Y - inner, inner * 2, inner * 2);
                context.DrawImage(photo, CoverSource(photo, dest.Size), dest);
            }
        }
        else if (!string.IsNullOrEmpty(DisplayName) && Foreground is { } foreground)
        {
            _initials ??= new TextLayout(Initials, new Typeface(FontFamily, FontStyle.Normal, FontWeight), InitialsFontSize, foreground);
            _initials.Draw(context, new Point(center.X - _initials.WidthIncludingTrailingWhitespace / 2, center.Y - _initials.Height / 2));
        }
        if (Stroke is { } stroke)
        {
            if (_pen is null || !ReferenceEquals(_penBrush, stroke))
            {
                _pen = new Pen(stroke, 1);
                _penBrush = stroke;
            }
            context.DrawEllipse(null, _pen, center, radius - 0.5, radius - 0.5);
        }
    }

    /// <summary>The part of <paramref name="image"/> that covers <paramref name="target"/> (object-fit cover), in pixels.</summary>
    internal static Rect CoverSource(IImage image, Size target)
    {
        var source = image is Avalonia.Media.Imaging.Bitmap bitmap
            ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height)
            : image.Size;
        if (target.Width <= 0 || target.Height <= 0)
        {
            return new Rect(source);
        }
        var scale = Math.Max(target.Width / source.Width, target.Height / source.Height);
        var w = target.Width / scale;
        var h = target.Height / scale;
        return new Rect((source.Width - w) / 2, (source.Height - h) / 2, w, h);
    }
}

/// <summary>
/// Text with Arc's wrapping: <see cref="IsBalanced"/> is <c>text-wrap: balance</c> (the narrowest width that keeps the line
/// count, for headings); <see cref="IsPretty"/> is <c>text-wrap: pretty</c> (no single word alone on the last line).
/// </summary>
internal sealed class SignInText : TextBlock
{
    public static readonly StyledProperty<bool> IsBalancedProperty =
        AvaloniaProperty.Register<SignInText, bool>(nameof(IsBalanced));

    public static readonly StyledProperty<bool> IsPrettyProperty =
        AvaloniaProperty.Register<SignInText, bool>(nameof(IsPretty));

    static SignInText()
    {
        AffectsMeasure<SignInText>(IsBalancedProperty, IsPrettyProperty);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public bool IsBalanced { get => GetValue(IsBalancedProperty); set => SetValue(IsBalancedProperty, value); }

    public bool IsPretty { get => GetValue(IsPrettyProperty); set => SetValue(IsPrettyProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        var full = availableSize.Width;
        if (double.IsInfinity(full) || string.IsNullOrEmpty(Text) || (!IsBalanced && !IsPretty))
        {
            return size;
        }
        var lines = TextLayout.TextLines.Count;
        if (lines < 2)
        {
            return size;
        }
        if (IsBalanced)
        {
            var low = full / lines;
            var high = full;
            for (var i = 0; i < 8 && high - low > 1; i++)
            {
                var mid = (low + high) / 2;
                base.MeasureOverride(availableSize.WithWidth(mid));
                if (TextLayout.TextLines.Count > lines)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }
            // A pixel of slack so layout rounding never re-wraps the balanced lines.
            var width = Math.Min(full, Math.Ceiling(high) + 1);
            size = base.MeasureOverride(availableSize.WithWidth(width));
            while (TextLayout.TextLines.Count > lines && width < full)
            {
                width = Math.Min(full, width + 2);
                size = base.MeasureOverride(availableSize.WithWidth(width));
            }
            return size;
        }
        if (!LastLineIsOneWord())
        {
            return size;
        }
        // Pretty: give up a little width until a second word joins the last line, as long as the line count holds.
        for (var width = full - 4; width > full * 0.7; width -= 4)
        {
            var trial = base.MeasureOverride(availableSize.WithWidth(width));
            if (TextLayout.TextLines.Count != lines)
            {
                break;
            }
            if (!LastLineIsOneWord())
            {
                return trial.WithWidth(Math.Min(full, Math.Ceiling(trial.Width) + 1));
            }
        }
        return base.MeasureOverride(availableSize);
    }

    private bool LastLineIsOneWord()
    {
        var text = Text ?? string.Empty;
        var last = TextLayout.TextLines[^1];
        var start = Math.Clamp(last.FirstTextSourceIndex, 0, text.Length);
        var length = Math.Clamp(last.Length, 0, text.Length - start);
        return !text.Substring(start, length).Trim().Contains(' ');
    }
}

/// <summary>
/// An underline for a text block, drawn <see cref="UnderlineOffset"/> below each line's baseline in its own brush (CSS
/// <c>text-decoration-color</c> and <c>text-underline-offset</c>, which Avalonia's decorations cannot express). It lies
/// over <see cref="Target"/> (same box) and draws nothing else.
/// </summary>
internal sealed class SignInUnderline : Control
{
    public static readonly StyledProperty<IBrush?> UnderlineBrushProperty =
        AvaloniaProperty.Register<SignInUnderline, IBrush?>(nameof(UnderlineBrush));

    public static readonly StyledProperty<double> UnderlineOffsetProperty =
        AvaloniaProperty.Register<SignInUnderline, double>(nameof(UnderlineOffset), 3);

    private TextBlock? _target;

    static SignInUnderline()
    {
        AffectsRender<SignInUnderline>(UnderlineBrushProperty, UnderlineOffsetProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SignInUnderline>(false);
    }

    public IBrush? UnderlineBrush { get => GetValue(UnderlineBrushProperty); set => SetValue(UnderlineBrushProperty, value); }

    public double UnderlineOffset { get => GetValue(UnderlineOffsetProperty); set => SetValue(UnderlineOffsetProperty, value); }

    /// <summary>The text block whose lines are underlined.</summary>
    public TextBlock? Target
    {
        get => _target;
        set
        {
            if (_target is not null)
            {
                _target.PropertyChanged -= OnTargetChanged;
            }
            _target = value;
            if (_target is not null)
            {
                _target.PropertyChanged += OnTargetChanged;
            }
            InvalidateVisual();
        }
    }

    private void OnTargetChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == TextBlock.TextProperty)
        {
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    public override void Render(DrawingContext context)
    {
        if (UnderlineBrush is not { } brush || _target is not { } target || string.IsNullOrEmpty(target.Text))
        {
            return;
        }
        var y = target.Padding.Top;
        foreach (var line in target.TextLayout.TextLines)
        {
            var top = Math.Round(y + line.Baseline + UnderlineOffset);
            context.FillRectangle(brush, new Rect(target.Padding.Left + line.Start, top, line.Width, 1));
            y += line.Height;
        }
    }
}

/// <summary>Children side by side in equal columns <see cref="Gap"/> apart (Arc's SSO grid: <c>repeat(n, minmax(0, 1fr))</c>).</summary>
internal sealed class SignInProviderRow : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<SignInProviderRow, double>(nameof(Gap), 8);

    static SignInProviderRow()
    {
        AffectsMeasure<SignInProviderRow>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private double ColumnWidth(double width)
    {
        var count = Children.Count;
        return count == 0 || double.IsInfinity(width) ? double.PositiveInfinity : Math.Max(0, (width - Gap * (count - 1)) / count);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var column = ColumnWidth(availableSize.Width);
        var height = 0.0;
        var natural = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(column, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            natural = Math.Max(natural, child.DesiredSize.Width);
        }
        var width = double.IsInfinity(availableSize.Width) ? natural * Children.Count + Gap * Math.Max(0, Children.Count - 1) : availableSize.Width;
        return new Size(Children.Count == 0 ? 0 : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var column = ColumnWidth(finalSize.Width);
        var x = 0.0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, column, finalSize.Height));
            x += column + Gap;
        }
        return finalSize;
    }
}

/// <summary>
/// Arc's footer row (<c>display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center</c>): the
/// status (first child, <c>flex: 1 1 12rem</c>) and the sign-up line (second child, no wrap). When both do not fit
/// side by side the status takes its own line above; extra height is shared between the lines (align-content: normal).
/// </summary>
internal sealed class SignInFooterPanel : Panel
{
    /// <summary>The status's flex basis (12rem).</summary>
    public const double StatusBasis = 192;

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<SignInFooterPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<SignInFooterPanel, double>(nameof(RowGap), 4);

    static SignInFooterPanel()
    {
        AffectsMeasure<SignInFooterPanel>(ColumnGapProperty, RowGapProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>True when the status sits on its own line.</summary>
    public bool IsWrapped { get; private set; }

    private Control? Status => Children.Count > 0 ? Children[0] : null;

    private Control? Switch => Children.Count > 1 ? Children[1] : null;

    private static bool Shows(Control? child) => child is { IsVisible: true };

    protected override Size MeasureOverride(Size availableSize)
    {
        var status = Status;
        var line = Switch;
        line?.Measure(Size.Infinity);
        var switchWidth = Shows(line) ? line!.DesiredSize.Width : 0;
        var width = availableSize.Width;
        IsWrapped = Shows(status) && Shows(line) && !double.IsInfinity(width) && StatusBasis + ColumnGap + switchWidth > width + 0.01;
        var statusWidth = !Shows(status) ? 0 : IsWrapped || !Shows(line) ? width : Math.Max(0, width - ColumnGap - switchWidth);
        status?.Measure(new Size(double.IsInfinity(statusWidth) ? StatusBasis : statusWidth, double.PositiveInfinity));
        var statusHeight = Shows(status) ? status!.DesiredSize.Height : 0;
        var switchHeight = Shows(line) ? line!.DesiredSize.Height : 0;
        var height = IsWrapped ? statusHeight + RowGap + switchHeight : Math.Max(statusHeight, switchHeight);
        var desiredWidth = double.IsInfinity(width) ? StatusBasis + ColumnGap + switchWidth : width;
        return new Size(desiredWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var status = Status;
        var line = Switch;
        var statusSize = Shows(status) ? status!.DesiredSize : default;
        var switchSize = Shows(line) ? line!.DesiredSize : default;
        if (IsWrapped)
        {
            // Two lines: extra height is shared equally, each item centred in its line.
            var extra = Math.Max(0, finalSize.Height - (statusSize.Height + RowGap + switchSize.Height)) / 2;
            var first = statusSize.Height + extra;
            status?.Arrange(new Rect(0, (first - statusSize.Height) / 2, finalSize.Width, statusSize.Height));
            var secondTop = first + RowGap;
            line?.Arrange(new Rect(0, secondTop + extra / 2, switchSize.Width, switchSize.Height));
            return finalSize;
        }
        var statusWidth = Shows(line) ? Math.Max(0, finalSize.Width - ColumnGap - switchSize.Width) : finalSize.Width;
        status?.Arrange(new Rect(0, (finalSize.Height - statusSize.Height) / 2, statusWidth, statusSize.Height));
        line?.Arrange(new Rect(finalSize.Width - switchSize.Width, (finalSize.Height - switchSize.Height) / 2, switchSize.Width, switchSize.Height));
        return finalSize;
    }
}
