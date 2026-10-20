using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The counted percentage of a <see cref="Progress"/> plus its completion check, drawn directly. The count is
/// right-aligned in a box as wide as "100%" (so the check never shifts as the number gains a digit) and follows the
/// fill spring frame by frame. At 100% the check settles in after 0.24s: scale 0.6 → 1 on a snappy spring with a
/// subtle blur clearing (P26); reduced motion fades it.
/// </summary>
internal sealed class ProgressReadout : Control
{
    public static readonly StyledProperty<double> CountProperty =
        AvaloniaProperty.Register<ProgressReadout, double>(nameof(Count));

    public static readonly StyledProperty<bool> IsCompleteProperty =
        AvaloniaProperty.Register<ProgressReadout, bool>(nameof(IsComplete));

    public static readonly StyledProperty<IBrush?> CheckBrushProperty =
        AvaloniaProperty.Register<ProgressReadout, IBrush?>(nameof(CheckBrush));

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ProgressReadout, double>(nameof(LineHeight), 19.6);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ProgressReadout>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ProgressReadout>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ProgressReadout>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ProgressReadout>();

    public double Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public bool IsComplete { get => GetValue(IsCompleteProperty); set => SetValue(IsCompleteProperty, value); }
    public IBrush? CheckBrush { get => GetValue(CheckBrushProperty); set => SetValue(CheckBrushProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private const double CheckSize = 14;
    private const double Gap = 4;

    private readonly FormattedText?[] _counts = new FormattedText?[101];
    private double _countBoxWidth;
    private MotionValue<double>? _checkScale;
    private MotionValue<double>? _checkFade;
    private bool _checkFull = true;
    private IPen? _checkPen;
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    static ProgressReadout()
    {
        AffectsMeasure<ProgressReadout>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        AffectsRender<ProgressReadout>(ForegroundProperty, CheckBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProgressReadout>(false);
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
            Array.Clear(_counts);
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
            var delay = TimeSpan.FromSeconds(0.24);
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

    private FormattedText Format(string text) =>
        Configure(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Gray));

    private static FormattedText Configure(FormattedText text)
    {
        text.SetFontFeatures(s_tabular);
        return text;
    }

    private FormattedText CountText(int value)
    {
        return _counts[value] ??= Format(value.ToString(CultureInfo.InvariantCulture) + "%");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _countBoxWidth = Math.Ceiling(CountText(100).WidthIncludingTrailingWhitespace);
        return new Size(CheckSize + Gap + _countBoxWidth, Math.Max(LineHeight, CountText(0).Height));
    }

    public override void Render(DrawingContext context)
    {
        var height = Bounds.Height;
        var countBoxX = Bounds.Width - _countBoxWidth;
        var text = CountText(DisplayedCount);
        context.DrawText(text, new Point(Bounds.Width - text.WidthIncludingTrailingWhitespace, (height - text.Height) / 2));

        var fade = Math.Clamp(CheckOpacity, 0, 1);
        if (fade <= 0.001 || CheckBrush is not { } brush)
        {
            return;
        }
        _checkPen ??= new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var scale = _checkScale?.Value ?? 1;
        var center = new Point(countBoxX - Gap - CheckSize / 2, height / 2);
        var glyphScale = CheckSize / 24.0 * scale;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(glyphScale, glyphScale) *
                     Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushOpacity(fade))
        {
            var effect = _checkFull ? ProgressBlur.Get(2 * (1 - fade)) : null;
            var box = new Rect(center.X - CheckSize / 2, center.Y - CheckSize / 2, CheckSize, CheckSize);
            using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, box))
            using (context.PushTransform(matrix))
            {
                context.DrawGeometry(null, _checkPen, ArcIcons.Check);
            }
        }
    }
}
