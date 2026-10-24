using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A countdown in <c>m:ss</c> whose changed characters roll, one column each: while the time runs down the new digit
/// drops in from 0.7em above and the old one falls 0.7em below (both on Arc's snappy spring, fading over 160 ms and
/// blurring 2px); when the time goes up (a restart) they move the other way. Columns are as wide as the widest digit, so
/// the text does not jitter. Reduced motion and Off change the characters at once.
/// </summary>
/// <remarks>
/// Code-driven because each column keeps its leaving glyph on screen, out of layout, while the new glyph arrives on a
/// spring; a ControlTheme transition cannot hold two values of one property.
/// </remarks>
internal sealed class LoginImmersiveCountdown : Panel
{
    /// <summary>The seconds shown.</summary>
    public static readonly StyledProperty<int> SecondsProperty =
        AvaloniaProperty.Register<LoginImmersiveCountdown, int>(nameof(Seconds));

    /// <summary>How far a glyph travels, in em (0.7).</summary>
    public const double TravelEm = 0.7;

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly SpringSpec s_snappy = SpringSpec.FromVisual(0.26, 0);

    private sealed class Glyph
    {
        public required TextBlock Text { get; init; }
        public required TranslateTransform Translate { get; init; }
        public required int Column { get; init; }
        public MotionValue<double>? Y { get; set; }
        public MotionValue<double>? Fade { get; set; }
        public bool IsLeaving { get; set; }
    }

    private readonly List<Glyph> _glyphs = new();
    private string _text = string.Empty;
    private double _digitWidth;

    static LoginImmersiveCountdown()
    {
        AffectsMeasure<LoginImmersiveCountdown>(SecondsProperty);
    }

    public LoginImmersiveCountdown()
    {
        ClipToBounds = false;
        IsHitTestVisible = false;
    }

    /// <inheritdoc cref="SecondsProperty"/>
    public int Seconds { get => GetValue(SecondsProperty); set => SetValue(SecondsProperty, value); }

    /// <summary>The text shown (tests read it).</summary>
    public string Text => _text;

    /// <summary>Glyphs still rolling away (tests read it).</summary>
    internal int LeavingCount => _glyphs.Count(g => g.IsLeaving);

    /// <summary>Formats seconds as <c>m:ss</c> (minutes without padding, seconds with two digits).</summary>
    public static string Format(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SecondsProperty)
        {
            var direction = change.GetNewValue<int>() < change.GetOldValue<int>() ? 1 : -1;
            Update(Format(Seconds), direction);
        }
        else if (change.Property == TextBlock.FontSizeProperty || change.Property == TextBlock.FontFamilyProperty)
        {
            _digitWidth = 0;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_glyphs.Count == 0)
        {
            Update(Format(Seconds), 1);
        }
    }

    private void Update(string text, int direction)
    {
        var previous = _text;
        _text = text;
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = previous.Length > 0 && IsLoaded && IsEffectivelyVisible && level == ArcMotionLevel.Full;
        if (!animate)
        {
            foreach (var glyph in _glyphs.ToArray())
            {
                Remove(glyph);
            }
            for (var i = 0; i < text.Length; i++)
            {
                Add(text[i], i);
            }
            InvalidateMeasure();
            return;
        }
        // Columns are keyed by their index from the left, like the web original; columns past the new length go at once.
        foreach (var glyph in _glyphs.ToArray())
        {
            if (glyph.Column >= text.Length)
            {
                Remove(glyph);
            }
            else if (!glyph.IsLeaving && (glyph.Column >= previous.Length || previous[glyph.Column] != text[glyph.Column]))
            {
                Leave(glyph, direction);
            }
        }
        for (var i = 0; i < text.Length; i++)
        {
            if (i < previous.Length && previous[i] == text[i])
            {
                continue;
            }
            Enter(Add(text[i], i), direction);
        }
        InvalidateMeasure();
    }

    private Glyph Add(char character, int column)
    {
        var translate = new TranslateTransform();
        var text = new TextBlock { Text = character.ToString(), RenderTransform = translate };
        var glyph = new Glyph { Text = text, Translate = translate, Column = column };
        _glyphs.Add(glyph);
        Children.Add(text);
        return glyph;
    }

    private void Remove(Glyph glyph)
    {
        glyph.Y?.Dispose();
        glyph.Fade?.Dispose();
        _glyphs.Remove(glyph);
        Children.Remove(glyph.Text);
    }

    private double Travel => TravelEm * (double.IsNaN(FontSize) || FontSize <= 0 ? 14 : FontSize);

    private double FontSize => TextElementFontSize();

    private double TextElementFontSize() => GetValue(TextBlock.FontSizeProperty);

    /// <summary>Gives a glyph its two motion values once (position, and fade with blur).</summary>
    private void EnsureMotion(Glyph glyph)
    {
        if (glyph.Y is not null && glyph.Fade is not null)
        {
            return;
        }
        var text = glyph.Text;
        glyph.Y = new MotionValue<double>(this, glyph.Translate.Y, MotionVectors.Double);
        glyph.Y.Changed += y => glyph.Translate.Y = y;
        glyph.Fade = new MotionValue<double>(this, text.Opacity, MotionVectors.Double);
        glyph.Fade.Changed += f =>
        {
            text.Opacity = f;
            text.Effect = ArcBlur.Get(2 * (1 - f));
        };
    }

    private void Enter(Glyph glyph, int direction)
    {
        glyph.Translate.Y = -Travel * direction;
        glyph.Text.Opacity = 0;
        glyph.Text.Effect = ArcBlur.Get(2);
        EnsureMotion(glyph);
        _ = glyph.Y!.AnimateTo(0, MotionSpec.Spring(s_snappy));
        _ = glyph.Fade!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, s_easeOut));
    }

    private async void Leave(Glyph glyph, int direction)
    {
        glyph.IsLeaving = true;
        EnsureMotion(glyph);
        _ = glyph.Fade!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, s_easeOut));
        await glyph.Y!.AnimateTo(Travel * direction, MotionSpec.Spring(s_snappy));
        if (_glyphs.Contains(glyph))
        {
            Remove(glyph);
        }
    }

    private double DigitWidth()
    {
        if (_digitWidth > 0)
        {
            return _digitWidth;
        }
        var probe = new TextBlock
        {
            FontFamily = GetValue(TextBlock.FontFamilyProperty),
            FontSize = TextElementFontSize(),
            FontWeight = GetValue(TextBlock.FontWeightProperty)
        };
        var widest = 0.0;
        for (var d = 0; d <= 9; d++)
        {
            probe.Text = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            probe.Measure(Size.Infinity);
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }
        _digitWidth = widest;
        return widest;
    }

    private double ColumnWidth(char character, TextBlock text) => char.IsDigit(character) ? DigitWidth() : text.DesiredSize.Width;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        foreach (var glyph in _glyphs)
        {
            glyph.Text.Measure(Size.Infinity);
            height = Math.Max(height, glyph.Text.DesiredSize.Height);
        }
        var width = 0.0;
        for (var i = 0; i < _text.Length; i++)
        {
            var current = _glyphs.FirstOrDefault(g => !g.IsLeaving && g.Column == i);
            width += current is null ? DigitWidth() : ColumnWidth(_text[i], current.Text);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var lefts = new double[_text.Length + 1];
        var widths = new double[_text.Length + 1];
        for (var i = 0; i < _text.Length; i++)
        {
            var current = _glyphs.FirstOrDefault(g => !g.IsLeaving && g.Column == i);
            var width = current is null ? DigitWidth() : ColumnWidth(_text[i], current.Text);
            lefts[i] = x;
            widths[i] = width;
            x += width;
        }
        foreach (var glyph in _glyphs)
        {
            var column = Math.Clamp(glyph.Column, 0, Math.Max(0, _text.Length - 1));
            var size = glyph.Text.DesiredSize;
            var left = _text.Length == 0 ? 0 : lefts[column] + (widths[column] - size.Width) / 2;
            glyph.Text.Arrange(new Rect(left, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}
