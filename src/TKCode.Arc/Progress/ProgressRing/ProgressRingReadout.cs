using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Centered percentage and completion checkmark inside a <see cref="ProgressRing"/>, drawn directly.
/// Displays tabular digits (cached <see cref="TextLayout"/> per integer 0–100) while running and transitions cleanly
/// to the completion check at 100%.
/// </summary>
internal sealed class ProgressRingReadout : Control
{
    public static readonly StyledProperty<double> CountProperty =
        AvaloniaProperty.Register<ProgressRingReadout, double>(nameof(Count));

    public static readonly StyledProperty<bool> IsCompleteProperty =
        AvaloniaProperty.Register<ProgressRingReadout, bool>(nameof(IsComplete));

    public static readonly StyledProperty<IBrush?> CheckBrushProperty =
        AvaloniaProperty.Register<ProgressRingReadout, IBrush?>(nameof(CheckBrush));

    public static readonly StyledProperty<double> CheckSizeProperty =
        AvaloniaProperty.Register<ProgressRingReadout, double>(nameof(CheckSize), 16);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ProgressRingReadout>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ProgressRingReadout>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ProgressRingReadout>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ProgressRingReadout>();

    public double Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public bool IsComplete { get => GetValue(IsCompleteProperty); set => SetValue(IsCompleteProperty, value); }
    public IBrush? CheckBrush { get => GetValue(CheckBrushProperty); set => SetValue(CheckBrushProperty, value); }
    public double CheckSize { get => GetValue(CheckSizeProperty); set => SetValue(CheckSizeProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private readonly TextLayout?[] _counts = new TextLayout?[101];
    private MotionValue<double>? _checkScale;
    private MotionValue<double>? _checkFade;
    private bool _checkFull = true;
    private IPen? _checkPen;
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    static ProgressRingReadout()
    {
        AffectsRender<ProgressRingReadout>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, ForegroundProperty, CheckBrushProperty, CheckSizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProgressRingReadout>(false);
    }

    /// <summary>The integer shown for the current count.</summary>
    internal int DisplayedCount => (int)Math.Round(Math.Clamp(Count, 0, 100), MidpointRounding.AwayFromZero);

    /// <summary>Current check visibility (0–1).</summary>
    internal double CheckOpacity => _checkFade?.Value ?? (IsComplete ? 1 : 0);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty)
        {
            var before = (int)Math.Round(Math.Clamp(change.GetOldValue<double>(), 0, 100), MidpointRounding.AwayFromZero);
            if (before != DisplayedCount)
            {
                InvalidateVisual();
            }
        }
        else if (change.Property == IsCompleteProperty)
        {
            RunCheck(IsComplete);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            ClearCounts();
        }
        else if (change.Property == CheckBrushProperty)
        {
            _checkPen = null;
        }
    }

    private void RunCheck(bool shown)
    {
        var target = shown ? 1.0 : 0.0;
        _checkScale ??= CreateValue(shown ? 0.6 : 1);
        _checkFade ??= CreateValue(1 - target);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _checkScale.Jump(1);
            _checkFade.Jump(target);
            return;
        }
        _checkFull = level == ArcMotionLevel.Full;
        if (!_checkFull)
        {
            _checkScale.Jump(1);
            _ = _checkFade.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, new LinearEasing()));
            return;
        }
        if (shown)
        {
            var delay = TimeSpan.FromSeconds(0.2);
            _checkScale.Jump(0.6);
            _ = _checkScale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, delay));
            _ = _checkFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
        }
        else
        {
            _ = _checkScale.AnimateTo(0.6, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            _ = _checkFade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private TextLayout CountText(int value)
    {
        return _counts[value] ??= new TextLayout(
            value.ToString(CultureInfo.InvariantCulture) + "%",
            new Typeface(FontFamily, FontStyle.Normal, FontWeight),
            FontSize,
            Foreground ?? Brushes.Gray,
            fontFeatures: s_tabular);
    }

    private void ClearCounts()
    {
        for (var i = 0; i < _counts.Length; i++)
        {
            _counts[i]?.Dispose();
            _counts[i] = null;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearCounts();
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var fade = Math.Clamp(CheckOpacity, 0, 1);
        var textOpacity = 1.0 - fade;
        if (textOpacity > 0.001)
        {
            var text = CountText(DisplayedCount);
            using (textOpacity < 1.0 ? context.PushOpacity(textOpacity) : default(DrawingContext.PushedState?))
            {
                text.Draw(context, new Point((width - text.WidthIncludingTrailingWhitespace) / 2.0, (height - text.Height) / 2.0));
            }
        }

        if (fade <= 0.001 || CheckBrush is not { } brush)
        {
            return;
        }
        _checkPen ??= new Pen(brush, 2.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var checkSize = Math.Max(8, CheckSize);
        var scale = _checkScale?.Value ?? 1.0;
        var center = new Point(width / 2.0, height / 2.0);
        var glyphScale = checkSize / 24.0 * scale;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(glyphScale, glyphScale) *
                     Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushOpacity(fade))
        {
            var effect = _checkFull ? ArcBlur.Get(2 * (1 - fade)) : null;
            var box = new Rect(center.X - checkSize / 2.0, center.Y - checkSize / 2.0, checkSize, checkSize);
            using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, box))
            using (context.PushTransform(matrix))
            {
                context.DrawGeometry(null, _checkPen, ArcIcons.Check);
            }
        }
    }
}
