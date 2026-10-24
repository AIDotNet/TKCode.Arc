// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the strength meter of signup-form.tsx.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The four strength bars beside the password feedback (Arc's <c>.meter</c>): pill-shaped bars, the first
/// <see cref="Level"/> of them on. On bars take the level's tone (1 danger, 2 warning, 3 accent, 4 success), off bars the
/// border color; each bar crossfades its color over 160 ms on the standard curve (CSS <c>transition: background-color</c>),
/// at once under reduced motion and Off. Decorative: the words beside it carry the strength.
/// </summary>
internal sealed class SignupFormMeter : Control
{
    /// <summary>The number of bars on, 0 to 4.</summary>
    public static readonly StyledProperty<int> LevelProperty =
        AvaloniaProperty.Register<SignupFormMeter, int>(nameof(Level), coerce: (_, v) => Math.Clamp(v, 0, BarCount));

    /// <summary>Width of one bar.</summary>
    public static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<SignupFormMeter, double>(nameof(BarWidth), 20);

    /// <summary>Height of one bar.</summary>
    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<SignupFormMeter, double>(nameof(BarHeight), 3);

    /// <summary>Gap between bars.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<SignupFormMeter, double>(nameof(Gap), 3);

    internal const int BarCount = 4;

    private readonly SolidColorBrush[] _brushes = new SolidColorBrush[BarCount];
    private readonly MotionValue<Color>?[] _colors = new MotionValue<Color>?[BarCount];

    static SignupFormMeter()
    {
        AffectsMeasure<SignupFormMeter>(BarWidthProperty, BarHeightProperty, GapProperty);
        AffectsRender<SignupFormMeter>(LevelProperty);
    }

    public SignupFormMeter()
    {
        for (var i = 0; i < BarCount; i++)
        {
            _brushes[i] = new SolidColorBrush(Colors.Transparent);
        }
        IsHitTestVisible = false;
    }

    /// <inheritdoc cref="LevelProperty"/>
    public int Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    /// <inheritdoc cref="BarWidthProperty"/>
    public double BarWidth { get => GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }

    /// <inheritdoc cref="BarHeightProperty"/>
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>The color bar <paramref name="index"/> shows right now (tests read it).</summary>
    internal Color BarColor(int index) => _brushes[index].Color;

    /// <summary>The token a bar settles on for a level.</summary>
    internal static ArcDesignTokenKind ToneOf(int bar, int level)
    {
        if (bar >= level)
        {
            return ArcDesignTokenKind.Border;
        }
        return level switch
        {
            1 => ArcDesignTokenKind.Danger,
            2 => ArcDesignTokenKind.Warning,
            4 => ArcDesignTokenKind.Success,
            _ => ArcDesignTokenKind.Accent
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        Resolve(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        foreach (var color in _colors)
        {
            color?.Stop();
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Resolve(animate: false);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LevelProperty)
        {
            Resolve(animate: true);
        }
    }

    private void Resolve(bool animate)
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        var level = Level;
        var full = animate && IsLoaded && ArcMotion.IsFull(this);
        for (var i = 0; i < BarCount; i++)
        {
            var target = ArcTokens.GetColor(this, ToneOf(i, level));
            var index = i;
            if (_colors[i] is null)
            {
                var value = new MotionValue<Color>(this, target, MotionVectors.Color);
                value.Changed += c =>
                {
                    _brushes[index].Color = c;
                    InvalidateVisual();
                };
                _colors[i] = value;
                _brushes[i].Color = target;
                continue;
            }
            if (full)
            {
                _ = _colors[i]!.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _colors[i]!.Jump(target);
                _brushes[i].Color = target;
            }
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(BarWidth * BarCount + Gap * (BarCount - 1), BarHeight);

    public override void Render(DrawingContext context)
    {
        var height = BarHeight;
        var y = (Bounds.Height - height) / 2;
        var radius = height / 2;
        for (var i = 0; i < BarCount; i++)
        {
            var rect = new Rect(i * (BarWidth + Gap), y, BarWidth, height);
            context.DrawRectangle(_brushes[i], null, rect, radius, radius);
        }
    }
}
