using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A count that rolls in a reserved width (the header's "6 steps" and "3 of 7"): tabular digits, each in a cell as wide as
/// the widest digit. A new value at full motion rolls in (the old number rises 0.4em and fades in 100ms, the new one
/// rises from 0.4em below in 210ms on the standard curve while fading in over 160ms; a smaller value rolls the other
/// way) and the slot's width follows in 240ms. Reduced motion and motion off change it in place. Code-driven: two layers
/// of drawn digits on their own clocks, which ControlTheme transitions cannot express.
/// </summary>
internal sealed class AgentPlanCount : Control
{
    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<AgentPlanCount, int>(nameof(Value));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<AgentPlanCount>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AgentPlanCount>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AgentPlanCount>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<AgentPlanCount>();

    /// <summary>Defines the <see cref="LineHeight"/> property.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AgentPlanCount, double>(nameof(LineHeight), double.NaN);

    private readonly TextLayout?[] _digits = new TextLayout?[10];
    private double _cell;
    private double _height;
    private string _current = "0";
    private string? _previous;
    private int _direction = 1;
    private MotionValue<double>? _roll;
    private MotionValue<double>? _width;
    private bool _dirty = true;
    private static readonly Avalonia.Animation.Easings.LinearEasing s_linear = new();

    static AgentPlanCount()
    {
        AffectsMeasure<AgentPlanCount>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        AffectsRender<AgentPlanCount>(ForegroundProperty);
    }

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>The line height (NaN: the font's), so the digits sit on the same baseline as the words beside them.</summary>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>The text shown (tests).</summary>
    public string Text => _current;

    /// <summary>True while a roll runs.</summary>
    public bool IsRolling => _roll?.IsAnimating == true;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var old = change.GetOldValue<int>();
            var value = change.GetNewValue<int>();
            var text = value.ToString(CultureInfo.CurrentCulture);
            if (text == _current)
            {
                return;
            }
            var animate = IsLoaded && AgentPlanMotion.IsFull(this);
            _previous = animate ? _current : null;
            _current = text;
            _direction = value >= old ? 1 : -1;
            _roll ??= CreateRoll();
            if (animate)
            {
                _roll.Jump(0);
                _ = _roll.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(210), s_linear));
            }
            else
            {
                _roll.Jump(1);
            }
            UpdateWidth(animate);
            InvalidateVisual();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty ||
                 change.Property == LineHeightProperty)
        {
            _dirty = true;
            InvalidateMeasure();
        }
    }

    private MotionValue<double> CreateRoll()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private void UpdateWidth(bool animate)
    {
        if (_cell <= 0)
        {
            return;
        }
        var target = _cell * _current.Length;
        _width ??= CreateWidth(target);
        if (animate)
        {
            _ = _width.AnimateTo(target, MotionSpec.Tween(AgentPlanMotion.CountWidth, ArcEasings.Standard));
        }
        else
        {
            _width.Jump(target);
        }
    }

    private MotionValue<double> CreateWidth(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private void EnsureDigits()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        var brush = Foreground ?? Brushes.Black;
        _cell = 0;
        for (var d = 0; d < 10; d++)
        {
            _digits[d]?.Dispose();
            var layout = new TextLayout(((char)('0' + d)).ToString(), typeface, FontSize, brush, lineHeight: LineHeight);
            _digits[d] = layout;
            _cell = Math.Max(_cell, layout.WidthIncludingTrailingWhitespace);
            _height = layout.Height;
        }
        _width?.Jump(_cell * _current.Length);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureDigits();
        _width ??= CreateWidth(_cell * _current.Length);
        return new Size(Math.Max(0, _width.Value), _height);
    }

    public override void Render(DrawingContext context)
    {
        EnsureDigits();
        var p = Math.Clamp(_roll?.Value ?? 1, 0, 1);
        var em = FontSize;
        if (_previous is { } previous && p < 1)
        {
            // The old number leaves in the first half of the new one's travel (100 of 210ms).
            var q = Math.Clamp(p / (100.0 / 210.0), 0, 1);
            var opacity = 1 - q;
            if (opacity > 0.01)
            {
                using (context.PushOpacity(opacity))
                {
                    DrawNumber(context, previous, -_direction * 0.4 * em * ArcEasings.Standard.Ease(q));
                }
            }
            // The new one fades in over 160ms on the enter curve while it rises for 210ms.
            var fade = ArcEasings.Enter.Ease(Math.Clamp(p * 210.0 / 160.0, 0, 1));
            using (context.PushOpacity(fade))
            {
                DrawNumber(context, _current, _direction * 0.4 * em * (1 - ArcEasings.Standard.Ease(p)));
            }
            return;
        }
        DrawNumber(context, _current, 0);
    }

    private void DrawNumber(DrawingContext context, string text, double dy)
    {
        var width = Bounds.Width;
        var start = (width - _cell * text.Length) / 2;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is < '0' or > '9')
            {
                continue;
            }
            var layout = _digits[c - '0'];
            if (layout is null)
            {
                continue;
            }
            var x = start + i * _cell + (_cell - layout.WidthIncludingTrailingWhitespace) / 2;
            layout.Draw(context, new Point(x, dy));
        }
    }
}
