using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The value shown in a select trigger (P3): a later option rises from below while the old one lifts away,
/// both through a short blur; an earlier option travels the other way. A value picked from a list can instead fly
/// in from where its option was (P8). Every layer is arranged over the full slot and left aligned, so the leaving
/// copy never shifts sideways, and the label ellipsizes instead of widening the trigger.
/// </summary>
internal sealed class SelectValueRoll : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<SelectValueRoll>();

    public static readonly StyledProperty<IBrush?> PlaceholderForegroundProperty =
        AvaloniaProperty.Register<SelectValueRoll, IBrush?>(nameof(PlaceholderForeground));

    public static readonly StyledProperty<IBrush?> IconForegroundProperty =
        AvaloniaProperty.Register<SelectValueRoll, IBrush?>(nameof(IconForeground));

    public static readonly StyledProperty<FontWeight> PlaceholderFontWeightProperty =
        AvaloniaProperty.Register<SelectValueRoll, FontWeight>(nameof(PlaceholderFontWeight), FontWeight.Normal);

    /// <summary>Travel of the incoming value in em (Select 0.35, MorphSelect 0.5, MultiSelect count 0.5).</summary>
    public static readonly StyledProperty<double> EnterTravelProperty =
        AvaloniaProperty.Register<SelectValueRoll, double>(nameof(EnterTravel), 0.35);

    public static readonly StyledProperty<double> ExitTravelProperty =
        AvaloniaProperty.Register<SelectValueRoll, double>(nameof(ExitTravel), 0.3);

    public static readonly StyledProperty<double> EnterBlurProperty =
        AvaloniaProperty.Register<SelectValueRoll, double>(nameof(EnterBlur), 4);

    public static readonly StyledProperty<double> ExitBlurProperty =
        AvaloniaProperty.Register<SelectValueRoll, double>(nameof(ExitBlur), 2);

    public static readonly StyledProperty<TimeSpan> ExitDurationProperty =
        AvaloniaProperty.Register<SelectValueRoll, TimeSpan>(nameof(ExitDuration), ArcMotionPresets.Fast);

    /// <summary>MorphSelect: the vertical travel runs on the glide spring (0.28s, bounce 0.08) instead of a tween.</summary>
    public static readonly StyledProperty<bool> SpringTravelProperty =
        AvaloniaProperty.Register<SelectValueRoll, bool>(nameof(SpringTravel));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<SelectValueRoll, double>(nameof(IconSize), 20);

    public static readonly StyledProperty<double> IconSpacingProperty =
        AvaloniaProperty.Register<SelectValueRoll, double>(nameof(IconSpacing), 8);

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? PlaceholderForeground { get => GetValue(PlaceholderForegroundProperty); set => SetValue(PlaceholderForegroundProperty, value); }
    public IBrush? IconForeground { get => GetValue(IconForegroundProperty); set => SetValue(IconForegroundProperty, value); }
    public FontWeight PlaceholderFontWeight { get => GetValue(PlaceholderFontWeightProperty); set => SetValue(PlaceholderFontWeightProperty, value); }
    public double EnterTravel { get => GetValue(EnterTravelProperty); set => SetValue(EnterTravelProperty, value); }
    public double ExitTravel { get => GetValue(ExitTravelProperty); set => SetValue(ExitTravelProperty, value); }
    public double EnterBlur { get => GetValue(EnterBlurProperty); set => SetValue(EnterBlurProperty, value); }
    public double ExitBlur { get => GetValue(ExitBlurProperty); set => SetValue(ExitBlurProperty, value); }
    public TimeSpan ExitDuration { get => GetValue(ExitDurationProperty); set => SetValue(ExitDurationProperty, value); }
    public bool SpringTravel { get => GetValue(SpringTravelProperty); set => SetValue(SpringTravelProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public double IconSpacing { get => GetValue(IconSpacingProperty); set => SetValue(IconSpacingProperty, value); }

    internal static readonly SpringSpec Glide = SpringSpec.FromVisual(0.28, 0.08);
    internal static readonly SpringSpec Fly = SpringSpec.FromVisual(0.3, 0.06);

    private RollLayer? _current;
    private readonly List<RollLayer> _leaving = new();

    static SelectValueRoll()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<SelectValueRoll>(false);
    }

    /// <summary>The text currently shown (the incoming layer).</summary>
    public string Text => _current?.Text ?? string.Empty;

    public bool IsPlaceholder => _current?.IsPlaceholder ?? true;

    /// <summary>Width the current value needs (icon and label), for content-sized triggers.</summary>
    public double ContentWidth => _current?.ContentWidth ?? 0;

    /// <summary>
    /// Shows <paramref name="text"/>. <paramref name="direction"/> is 1 when the new value is later in the list,
    /// -1 when earlier. <paramref name="fly"/> is where the label starts relative to its resting place.
    /// </summary>
    public void Show(string text, object? icon, bool placeholder, int direction = 1, Vector? fly = null, bool animate = true)
    {
        if (_current is not null && _current.Text == text && ReferenceEquals(_current.Icon, icon) && _current.IsPlaceholder == placeholder)
        {
            return;
        }
        var layer = new RollLayer(this, text, icon, placeholder);
        VisualChildren.Add(layer);
        var outgoing = _current;
        _current = layer;
        InvalidateMeasure();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (outgoing is null || !animate || !IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            if (outgoing is not null)
            {
                Remove(outgoing);
            }
            foreach (var stale in _leaving.ToArray())
            {
                Remove(stale);
            }
            return;
        }
        var dir = direction >= 0 ? 1 : -1;
        var full = level == ArcMotionLevel.Full;
        Enter(layer, full, dir, full ? fly : null);
        Exit(outgoing, full, dir, fly is not null && full);
    }

    private void Enter(RollLayer layer, bool full, int dir, Vector? fly)
    {
        var em = TextElement.GetFontSize(this);
        if (!full)
        {
            layer.Opacity = 0;
            var fade = layer.CreateProgress();
            fade.Changed += p => layer.Opacity = p;
            _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        if (fly is { } from)
        {
            // P8: the label lifts out of its option and settles here; no blur, it is the same text moving.
            layer.Offset = from;
            var travel = layer.CreateOffset(from);
            travel.Changed += v => layer.Offset = v;
            _ = travel.AnimateTo(default, MotionSpec.Spring(Fly));
            return;
        }
        var startY = dir * EnterTravel * em;
        var blur = EnterBlur;
        layer.Opacity = 0;
        layer.Offset = new Vector(0, startY);
        layer.SetBlur(blur);
        var progress = layer.CreateProgress();
        if (SpringTravel)
        {
            var y = layer.CreateOffset(new Vector(0, startY));
            y.Changed += v => layer.Offset = v;
            _ = y.AnimateTo(default, MotionSpec.Spring(Glide));
            progress.Changed += p =>
            {
                layer.Opacity = p;
                layer.SetBlur(blur * (1 - p));
            };
            _ = progress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Enter));
            return;
        }
        progress.Changed += p =>
        {
            layer.Opacity = p;
            layer.Offset = new Vector(0, startY * (1 - p));
            layer.SetBlur(blur * (1 - p));
        };
        _ = progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    private void Exit(RollLayer layer, bool full, int dir, bool flying)
    {
        layer.StopAll();
        _leaving.Add(layer);
        var em = TextElement.GetFontSize(this);
        var startOpacity = layer.Opacity;
        var startY = layer.Offset.Y;
        var endY = flying ? -0.3 * em : -dir * ExitTravel * em;
        var blur = ExitBlur;
        var progress = layer.CreateProgress();
        progress.Changed += p =>
        {
            layer.Opacity = startOpacity * (1 - p);
            if (full)
            {
                layer.Offset = new Vector(layer.Offset.X, startY + (endY - startY) * p);
                layer.SetBlur(blur * p);
            }
        };
        var spec = full
            ? MotionSpec.Tween(flying ? TimeSpan.FromMilliseconds(120) : ExitDuration, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        progress.AnimateTo(1, spec).ContinueWith(_ => Dispatcher.Post(() => Remove(layer)),
            TaskScheduler.Default);
    }

    private void Remove(RollLayer layer)
    {
        layer.StopAll();
        _leaving.Remove(layer);
        if (!ReferenceEquals(layer, _current))
        {
            VisualChildren.Remove(layer);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == PlaceholderForegroundProperty ||
            change.Property == TextElement.FontFamilyProperty || change.Property == TextElement.FontSizeProperty ||
            change.Property == TextElement.FontWeightProperty || change.Property == PlaceholderFontWeightProperty ||
            change.Property == IconForegroundProperty)
        {
            foreach (var child in VisualChildren)
            {
                (child as RollLayer)?.Invalidate();
            }
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        var width = 0.0;
        foreach (var visual in VisualChildren)
        {
            if (visual is not Layoutable child)
            {
                continue;
            }
            child.Measure(availableSize);
            height = Math.Max(height, child.DesiredSize.Height);
            if (ReferenceEquals(child, _current))
            {
                width = child.DesiredSize.Width;
            }
        }
        if (_current is null)
        {
            height = TextElement.GetFontSize(this) * 1.4;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var visual in VisualChildren)
        {
            (visual as Layoutable)?.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _leaving.ToArray())
        {
            Remove(layer);
        }
        if (_current is not null)
        {
            _current.StopAll();
            _current.Opacity = 1;
            _current.Offset = default;
            _current.SetBlur(0);
        }
    }

    /// <summary>One copy of the value: optional icon plus an ellipsizing label, drawn directly.</summary>
    private sealed class RollLayer : Control
    {
        private readonly SelectValueRoll _owner;
        private readonly TranslateTransform _translate = new();
        private TextLayout? _layout;
        // The unconstrained layout: it measures the layer and, when the text fits, is also the arranged one.
        private TextLayout? _natural;
        private double _layoutWidth = -1;
        private BlurEffect? _blur;
        private MotionValue<double>? _progress;
        private MotionValue<Vector>? _offset;
        private Pen? _iconPen;

        public RollLayer(SelectValueRoll owner, string text, object? icon, bool placeholder)
        {
            _owner = owner;
            Text = text;
            Icon = icon;
            IsPlaceholder = placeholder;
            RenderTransform = _translate;
            IsHitTestVisible = false;
        }

        public string Text { get; }
        public object? Icon { get; }
        public bool IsPlaceholder { get; }

        public double ContentWidth { get; private set; }

        public Vector Offset
        {
            get => new(_translate.X, _translate.Y);
            set
            {
                _translate.X = value.X;
                _translate.Y = value.Y;
            }
        }

        public MotionValue<double> CreateProgress()
        {
            _progress?.Dispose();
            _progress = new MotionValue<double>(this, 0, MotionVectors.Double);
            return _progress;
        }

        public MotionValue<Vector> CreateOffset(Vector start)
        {
            _offset?.Dispose();
            _offset = new MotionValue<Vector>(this, start, MotionVectors.Vector);
            return _offset;
        }

        public void StopAll()
        {
            _progress?.Stop();
            _offset?.Stop();
        }

        public void SetBlur(double radius)
        {
            if (radius < 0.05)
            {
                if (_blur is not null)
                {
                    Effect = null;
                    _blur = null;
                }
                return;
            }
            if (_blur is null)
            {
                _blur = new BlurEffect { Radius = radius };
                Effect = _blur;
            }
            else
            {
                _blur.Radius = radius;
            }
        }

        public void Invalidate()
        {
            _layout = null;
            _natural = null;
            _iconPen = null;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private bool HasIcon => Icon is Geometry or IImage;

        private double IconBox => HasIcon ? _owner.IconSize + _owner.IconSpacing : 0;

        private TextLayout Build(double maxWidth)
        {
            var weight = IsPlaceholder ? _owner.PlaceholderFontWeight : TextElement.GetFontWeight(_owner);
            var brush = IsPlaceholder ? _owner.PlaceholderForeground ?? _owner.Foreground : _owner.Foreground;
            return new TextLayout(Text, new Typeface(TextElement.GetFontFamily(_owner), FontStyle.Normal, weight),
                TextElement.GetFontSize(_owner), brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis,
                null, FlowDirection.LeftToRight, Math.Max(0, maxWidth), double.PositiveInfinity, double.NaN, 0, 1);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var natural = _natural ??= Build(double.PositiveInfinity);
            ContentWidth = Math.Ceiling(natural.WidthIncludingTrailingWhitespace + IconBox);
            var height = Math.Max(natural.Height, HasIcon ? _owner.IconSize : 0);
            return new Size(Math.Min(ContentWidth, availableSize.Width), height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var textWidth = Math.Max(0, finalSize.Width - IconBox);
            if (_layout is null || Math.Abs(textWidth - _layoutWidth) > 0.5)
            {
                _layout = _natural is { } natural && natural.WidthIncludingTrailingWhitespace <= textWidth
                    ? natural
                    : Build(textWidth);
                _layoutWidth = textWidth;
            }
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            var layout = _layout;
            if (layout is null)
            {
                return;
            }
            var height = Bounds.Height;
            var x = 0.0;
            if (HasIcon)
            {
                var size = _owner.IconSize;
                var box = new Rect(0, (height - size) / 2, size, size);
                if (Icon is Geometry glyph && (_owner.IconForeground ?? _owner.Foreground) is { } brush)
                {
                    _iconPen ??= new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                    var scale = 16 / 24.0;
                    using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X + 2, box.Y + 2)))
                    {
                        context.DrawGeometry(null, _iconPen, glyph);
                    }
                }
                else if (Icon is IImage image)
                {
                    using (context.PushClip(new RoundedRect(box, size / 2)))
                    {
                        context.DrawImage(image, box);
                    }
                }
                x = IconBox;
            }
            layout.Draw(context, new Point(x, (height - layout.Height) / 2));
        }
    }
}
