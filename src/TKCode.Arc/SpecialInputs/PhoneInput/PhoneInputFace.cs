using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The flag and calling code of the country trigger. A new country rolls in the direction of the list (a country further
/// down rises from below): the incoming layer travels <c>dir × 0.6em</c> on the glide spring while it fades in (0.2s) and
/// unblurs from 4px (0.22s); the old layer lifts <c>−dir × 0.5em</c> and fades out through a 2px blur in 0.12s. Reduced
/// motion keeps a short fade; Off swaps at once. Layers are arranged over the whole slot and never take part in layout,
/// so only the incoming country sizes the trigger.
/// </summary>
internal sealed class PhoneInputFace : Control
{
    /// <summary>Glide: 0.28s, bounce 0.08 (Arc's <c>physical(.28, .08)</c>).</summary>
    internal static readonly SpringSpec Glide = SpringSpec.FromVisual(0.28, 0.08);

    /// <summary>Flags use the platform emoji font first, then the body font (letters on platforms without flag glyphs).</summary>
    internal static readonly FontFamily FlagFontFamily =
        new("Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji, fonts:Inter#Inter, $Default");

    internal static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    internal const double FlagSlot = 20;
    internal const double FlagFontSize = 17;
    internal const double Gap = 6;

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<PhoneInputFace>();

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private FaceLayer? _current;
    private readonly List<FaceLayer> _leaving = new();

    static PhoneInputFace()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<PhoneInputFace>(false);
        AffectsMeasure<PhoneInputFace>(TextElement.FontFamilyProperty, TextElement.FontSizeProperty, TextElement.FontWeightProperty);
    }

    /// <summary>The country shown (the incoming layer).</summary>
    public PhoneCountry? Country => _current?.Country;

    /// <summary>Width of the current flag and code.</summary>
    public double ContentWidth => _current?.ContentWidth ?? 0;

    /// <summary>
    /// Shows <paramref name="country"/>; <paramref name="direction"/> is 1 when it sits further down the list than the
    /// previous one, −1 when higher.
    /// </summary>
    public void Show(PhoneCountry country, int direction, bool animate)
    {
        if (_current is not null && ReferenceEquals(_current.Country, country))
        {
            return;
        }
        var layer = new FaceLayer(this, country);
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
        Enter(layer, level == ArcMotionLevel.Full, dir);
        Exit(outgoing, level == ArcMotionLevel.Full, dir);
    }

    private double Em => TextElement.GetFontSize(this);

    private void Enter(FaceLayer layer, bool full, int dir)
    {
        layer.Opacity = 0;
        var progress = layer.CreateValue(0);
        if (!full)
        {
            progress.Changed += p => layer.Opacity = Math.Clamp(p, 0, 1);
            _ = progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        var startY = dir * 0.6 * Em;
        layer.SetOffset(startY);
        layer.SetBlur(4);
        var y = layer.CreateValue(startY);
        y.Changed += layer.SetOffset;
        _ = y.AnimateTo(0, MotionSpec.Spring(Glide));
        // Opacity (0.2s) and blur (0.22s) share the enter ease; one tween drives both on the longer clock.
        progress.Changed += p =>
        {
            var t = Math.Clamp(p, 0, 1);
            layer.Opacity = ArcEasings.Enter.Ease(Math.Min(1, t * 1.1));
            layer.SetBlur(4 * (1 - ArcEasings.Enter.Ease(t)));
        };
        _ = progress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(220), new Avalonia.Animation.Easings.LinearEasing()));
    }

    private void Exit(FaceLayer layer, bool full, int dir)
    {
        layer.StopAll();
        _leaving.Add(layer);
        var startOpacity = layer.Opacity;
        var startY = layer.OffsetY;
        var endY = -dir * 0.5 * Em;
        var startBlur = layer.Blur;
        var progress = layer.CreateValue(0);
        progress.Changed += p =>
        {
            layer.Opacity = startOpacity * (1 - p);
            if (full)
            {
                layer.SetOffset(startY + (endY - startY) * p);
                layer.SetBlur(startBlur + (2 - startBlur) * p);
            }
        };
        var spec = full
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(80), ArcEasings.Standard);
        progress.AnimateTo(1, spec).ContinueWith(_ => Dispatcher.Post(() => Remove(layer)), TaskScheduler.Default);
    }

    private void Remove(FaceLayer layer)
    {
        layer.StopAll();
        _leaving.Remove(layer);
        if (!ReferenceEquals(layer, _current))
        {
            VisualChildren.Remove(layer);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in VisualChildren)
        {
            if (child is FaceLayer layer)
            {
                layer.Measure(Size.Infinity);
            }
        }
        var height = Math.Max(FlagFontSize, _current?.DesiredSize.Height ?? 0);
        return new Size(_current?.ContentWidth ?? 0, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in VisualChildren)
        {
            if (child is FaceLayer layer)
            {
                layer.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, layer.ContentWidth), finalSize.Height));
            }
        }
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == TextElement.FontFamilyProperty ||
            change.Property == TextElement.FontSizeProperty || change.Property == TextElement.FontWeightProperty)
        {
            foreach (var child in VisualChildren)
            {
                if (child is FaceLayer layer)
                {
                    layer.Invalidate();
                }
            }
        }
    }

    /// <summary>One flag + calling code copy. It moves through its render transform and blur only.</summary>
    private sealed class FaceLayer : Control
    {
        private readonly PhoneInputFace _owner;
        private readonly TranslateTransform _shift = new();
        private readonly List<MotionValue<double>> _values = new();
        private BlurEffect? _blur;
        private TextLayout? _flag;
        private TextLayout? _dial;

        public FaceLayer(PhoneInputFace owner, PhoneCountry country)
        {
            _owner = owner;
            Country = country;
            RenderTransform = _shift;
            IsHitTestVisible = false;
        }

        public PhoneCountry Country { get; }

        public double OffsetY => _shift.Y;

        public double Blur => _blur?.Radius ?? 0;

        public double ContentWidth
        {
            get
            {
                Ensure();
                return Math.Ceiling(FlagSlot + Gap + (_dial?.WidthIncludingTrailingWhitespace ?? 0));
            }
        }

        public MotionValue<double> CreateValue(double initial)
        {
            var value = new MotionValue<double>(this, initial, MotionVectors.Double);
            _values.Add(value);
            return value;
        }

        public void StopAll()
        {
            foreach (var value in _values)
            {
                value.Stop();
            }
            _values.Clear();
        }

        public void SetOffset(double y) => _shift.Y = y;

        public void SetBlur(double radius)
        {
            if (radius < 0.05)
            {
                _blur = null;
                Effect = null;
                return;
            }
            _blur ??= new BlurEffect();
            _blur.Radius = radius;
            Effect = _blur;
        }

        public void Invalidate()
        {
            _flag?.Dispose();
            _dial?.Dispose();
            _flag = null;
            _dial = null;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void Ensure()
        {
            var foreground = _owner.Foreground ?? Brushes.Black;
            if (_flag is null)
            {
                _flag = new TextLayout(Country.Flag, new Typeface(FlagFontFamily), FlagFontSize, foreground, lineHeight: FlagFontSize);
            }
            if (_dial is null)
            {
                var family = TextElement.GetFontFamily(_owner);
                var weight = TextElement.GetFontWeight(_owner);
                var size = TextElement.GetFontSize(_owner);
                _dial = new TextLayout("+" + Country.Dial, new Typeface(family, FontStyle.Normal, weight), size, foreground,
                    fontFeatures: TabularNumbers);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Ensure();
            return new Size(ContentWidth, Math.Max(_flag!.Height, _dial!.Height));
        }

        public override void Render(DrawingContext context)
        {
            Ensure();
            var height = Bounds.Height;
            var flag = _flag!;
            var flagX = (FlagSlot - flag.WidthIncludingTrailingWhitespace) / 2;
            flag.Draw(context, new Point(flagX, (height - flag.Height) / 2));
            var dial = _dial!;
            dial.Draw(context, new Point(FlagSlot + Gap, (height - dial.Height) / 2));
        }
    }
}
