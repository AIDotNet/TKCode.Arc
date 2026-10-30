using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One counting value of <see cref="GenerationStats"/>: Core <see cref="RollingDigits"/> (tabular digits, each digit in a
/// cell as wide as "0") that glides toward each new value over about 100ms (linear, so chunks of tokens read as one smooth
/// count), and a dash while there is no value. A drop (a new request) or a change of unit cuts straight to the new value;
/// a live clock (time to first token while waiting) follows the frame. The width it takes only grows during a run, so the
/// text beside it never moves back and forth; a drop or a reset frees it. Reduced motion and Off: values show at once.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because each glide starts from the value shown now toward a target that keeps
/// moving with every chunk, and drops must jump; a ControlTheme transition cannot express that.
/// </para>
/// </summary>
internal sealed class GenerationStatsNumber : Control
{
    /// <summary>The glide toward each new value.</summary>
    internal static readonly TimeSpan GlideDuration = TimeSpan.FromMilliseconds(100);

    /// <summary>The font of the digits and the dash.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<GenerationStatsNumber>();

    /// <summary>The size of the digits and the dash.</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<GenerationStatsNumber>();

    /// <summary>The weight of the digits and the dash.</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<GenerationStatsNumber>();

    /// <summary>The color of the digits and the dash.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<GenerationStatsNumber>();

    private readonly RollingDigits _digits;
    private readonly TextBlock _placeholder;
    private MotionValue<double>? _glide;
    private GenerationNumber? _target;
    private double _reserved;

    public GenerationStatsNumber()
    {
        _digits = new RollingDigits { IsInstant = true, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
        _placeholder = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        VisualChildren.Add(_digits);
        VisualChildren.Add(_placeholder);
        LogicalChildren.Add(_digits);
        LogicalChildren.Add(_placeholder);
        IsHitTestVisible = false;
    }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The value it is heading to, or null while it shows the dash.</summary>
    internal GenerationNumber? Target => _target;

    /// <summary>The text drawn now (the dash, or the digits where the glide has got to).</summary>
    internal string DisplayText => _target is null ? _placeholder.Text ?? string.Empty : _digits.DisplayText;

    /// <summary>True while the value glides.</summary>
    internal bool IsGliding => _glide?.IsAnimating == true;

    /// <summary>
    /// Shows <paramref name="number"/>, or <paramref name="placeholder"/> when it is null. <paramref name="live"/> jumps
    /// (a clock that moves every frame); <paramref name="instant"/> jumps once (the first state, a variant switch).
    /// </summary>
    public void Show(GenerationNumber? number, string placeholder, bool live = false, bool instant = false)
    {
        if (number is not { } next)
        {
            _placeholder.Text = placeholder;
            if (_target is not null)
            {
                _target = null;
                _glide?.Stop();
                _digits.IsVisible = false;
                _placeholder.IsVisible = true;
                _reserved = 0;
                InvalidateMeasure();
            }
            return;
        }
        var previous = _target;
        _target = next;
        _digits.IsVisible = true;
        _placeholder.IsVisible = false;
        if (previous is { } before && before == next)
        {
            return;
        }
        var shown = _glide?.Value ?? _digits.Value;
        var sameFormat = previous is { } p && p.Decimals == next.Decimals && p.Prefix == next.Prefix && p.Suffix == next.Suffix;
        if (!sameFormat)
        {
            _digits.Decimals = next.Decimals;
            _digits.Prefix = next.Prefix;
            _digits.Suffix = next.Suffix;
        }
        var drop = previous is not null && sameFormat && next.Value < shown;
        if (drop)
        {
            _reserved = 0;
        }
        var glides = !live && !instant && sameFormat && !drop && IsLoaded && ArcMotion.IsFull(this);
        var glide = Glide;
        if (glides)
        {
            _ = glide.AnimateTo(next.Value, MotionSpec.Tween(GlideDuration, s_linear));
        }
        else
        {
            glide.Jump(next.Value);
            Apply(next.Value);
        }
    }

    /// <summary>Frees the width this value has reserved (a new run).</summary>
    public void Release()
    {
        _reserved = 0;
        InvalidateMeasure();
    }

    private static readonly Easing s_linear = new LinearEasing();

    private MotionValue<double> Glide
    {
        get
        {
            if (_glide is null)
            {
                _glide = new MotionValue<double>(this, _target?.Value ?? 0, MotionVectors.Double);
                _glide.Changed += Apply;
            }
            return _glide;
        }
    }

    private void Apply(double value)
    {
        if (_target is not { } target)
        {
            return;
        }
        var rounded = Math.Round(value, target.Decimals, MidpointRounding.AwayFromZero);
        if (_digits.Value != rounded)
        {
            _digits.Value = rounded;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _digits.Measure(availableSize);
        _placeholder.Measure(availableSize);
        var child = _target is null ? _placeholder.DesiredSize : _digits.DesiredSize;
        var height = Math.Max(_digits.DesiredSize.Height, _placeholder.DesiredSize.Height);
        _reserved = Math.Max(_reserved, child.Width);
        return new Size(_reserved, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _digits.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, _digits.DesiredSize.Width), finalSize.Height));
        _placeholder.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, _placeholder.DesiredSize.Width), finalSize.Height));
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_target is { } target)
        {
            _glide?.Jump(target.Value);
            Apply(target.Value);
        }
    }
}
