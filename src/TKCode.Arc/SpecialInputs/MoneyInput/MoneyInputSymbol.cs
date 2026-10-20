using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The currency symbol slot (Arc <c>SymbolSlot</c>): the symbol swaps with a short roll (enters from 0.4em out of a soft
/// blur on snappy, leaves to -0.3em on instant) while the slot springs to the new symbol's width on morph; the first
/// measure and reduced motion jump. As a prefix it keeps 0.06em before the digits, as a suffix 0.18em after them.
/// Drawn from cached layouts; the slot width is a <see cref="MotionValue{T}"/> because it follows a measured text
/// width (ResizeObserver in Arc) that styles cannot target.
/// </summary>
internal sealed class MoneyInputSymbol : Control
{
    private const double EnterRise = 0.4;
    private const double ExitRise = -0.3;
    private const double BlurSoft = 4;

    public static readonly StyledProperty<string?> SymbolProperty =
        AvaloniaProperty.Register<MoneyInputSymbol, string?>(nameof(Symbol));

    /// <summary>Suffix position (after the amount): margin-left .18em instead of margin-right .06em.</summary>
    public static readonly StyledProperty<bool> IsSuffixProperty =
        AvaloniaProperty.Register<MoneyInputSymbol, bool>(nameof(IsSuffix));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MoneyInputSymbol>();

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<MoneyInputSymbol, double>(nameof(LineHeight), 43.2);

    public string? Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }

    public bool IsSuffix { get => GetValue(IsSuffixProperty); set => SetValue(IsSuffixProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    private readonly MotionValue<double> _width;
    private readonly MotionValue<double> _enterY;
    private readonly MotionValue<double> _enterOpacity;
    private readonly MotionValue<double> _enterBlur;
    private readonly MotionValue<double> _exit;
    private TextLayout? _current;
    private TextLayout? _leaving;
    private bool _leavingRolls;
    private bool _measured;
    private bool _measuring;

    static MoneyInputSymbol()
    {
        AffectsMeasure<MoneyInputSymbol>(IsSuffixProperty, LineHeightProperty);
        AffectsRender<MoneyInputSymbol>(ForegroundProperty);
    }

    public MoneyInputSymbol()
    {
        Action<double> visual = _ => InvalidateVisual();
        _width = new MotionValue<double>(this, 0, MotionVectors.Double);
        _width.Changed += _ =>
        {
            if (!_measuring)
            {
                InvalidateMeasure();
            }
        };
        _enterY = new MotionValue<double>(this, 0, MotionVectors.Double);
        _enterOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _enterBlur = new MotionValue<double>(this, 0, MotionVectors.Double);
        _exit = new MotionValue<double>(this, 1, MotionVectors.Double);
        _enterY.Changed += visual;
        _enterOpacity.Changed += visual;
        _enterBlur.Changed += visual;
        _exit.Changed += visual;
    }

    /// <summary>The slot width (animated).</summary>
    internal double SlotWidth => _width.Value;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _measured = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SymbolProperty)
        {
            Swap(change.GetOldValue<string?>());
        }
        else if (change.Property == TextElement.FontFamilyProperty || change.Property == TextElement.FontSizeProperty ||
                 change.Property == TextElement.FontWeightProperty || change.Property == TextElement.FontStyleProperty ||
                 change.Property == TextElement.LetterSpacingProperty || change.Property == ForegroundProperty ||
                 change.Property == LineHeightProperty)
        {
            _current = null;
            _leaving = null;
            _measured = false;
            InvalidateMeasure();
        }
    }

    private void Swap(string? previous)
    {
        var old = _current ?? (string.IsNullOrEmpty(previous) ? null : Shape(previous));
        _current = null;
        if (!_measured || old is null || !IsEffectivelyVisible)
        {
            _leaving = null;
            InvalidateMeasure();
            return;
        }
        var full = ArcMotion.IsFull(this);
        _leaving = old;
        _leavingRolls = full;
        _exit.Jump(0);
        _ = _exit.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        if (full)
        {
            _enterY.Jump(EnterRise);
            _enterOpacity.Jump(0);
            _enterBlur.Jump(BlurSoft);
            _ = _enterY.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _enterOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            _ = _enterBlur.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        }
        else
        {
            _enterY.Jump(0);
            _enterBlur.Jump(0);
            _enterOpacity.Jump(0);
            _ = _enterOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        InvalidateMeasure();
    }

    private TextLayout Shape(string text)
        => MoneyInputText.Layout(text, MoneyInputText.TypefaceOf(this), TextElement.GetFontSize(this), Foreground, LineHeight,
            TextElement.GetLetterSpacing(this));

    private TextLayout? Current()
    {
        if (_current is null && !string.IsNullOrEmpty(Symbol))
        {
            _current = Shape(Symbol);
        }
        return _current;
    }

    private double Gap => TextElement.GetFontSize(this) * (IsSuffix ? 0.18 : 0.06);

    protected override Size MeasureOverride(Size availableSize)
    {
        var target = Current()?.WidthIncludingTrailingWhitespace ?? 0;
        _measuring = true;
        try
        {
            if (!_measured || !ArcMotion.IsFull(this))
            {
                _measured = true;
                if (Math.Abs(_width.Value - target) > 1e-6 || _width.IsAnimating)
                {
                    _width.Jump(target);
                }
            }
            else if (Math.Abs(_width.Target - target) > 1e-6)
            {
                _ = _width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
            }
        }
        finally
        {
            _measuring = false;
        }
        return new Size(Math.Max(0, _width.Value) + Gap, LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        var left = IsSuffix ? Gap : 0;
        var em = TextElement.GetFontSize(this);
        if (_leaving is { } leaving)
        {
            var p = Math.Clamp(_exit.Value, 0, 1);
            if (p < 1)
            {
                using (context.PushOpacity(1 - p))
                {
                    leaving.Draw(context, new Point(left, _leavingRolls ? ExitRise * p * em : 0));
                }
            }
        }
        if (Current() is not { } current)
        {
            return;
        }
        var opacity = Math.Clamp(_enterOpacity.Value, 0, 1);
        var effect = ArcBlur.Get(_enterBlur.Value);
        DrawingContext.PushedState blurred = default;
        if (effect is not null)
        {
            blurred = context.PushEffect(effect, new Rect(left - 8, -8, current.WidthIncludingTrailingWhitespace + 16, LineHeight + 16));
        }
        var origin = new Point(left, _enterY.Value * em);
        if (opacity >= 0.999)
        {
            current.Draw(context, origin);
        }
        else
        {
            using (context.PushOpacity(opacity))
            {
                current.Draw(context, origin);
            }
        }
        if (effect is not null)
        {
            blurred.Dispose();
        }
    }
}
