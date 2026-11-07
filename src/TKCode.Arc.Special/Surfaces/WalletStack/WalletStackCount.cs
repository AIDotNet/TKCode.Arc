using System.Globalization;
using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The header count "1 of 4" (Arc Wallet stack, design reference): when the front card changes, the old number rises
/// 6px and fades while the new one comes up from 6px below (the other way round when going back), on Smooth over 700ms.
/// It is hidden from screen readers; the live region speaks the change.
/// </summary>
/// <remarks>Code-driven: the two numbers are drawn stacked and cross over on one progress value.</remarks>
internal sealed class WalletStackCount : Avalonia.Controls.Control
{
    public const double Roll = 6;

    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<WalletStackCount, IBrush?>(nameof(Ink));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<WalletStackCount, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<WalletStackCount, double>(nameof(FontSize), 14);

    private static readonly Avalonia.Animation.Easings.Easing s_ease = new ArcSpringEasing(ArcMotionPresets.Smooth);

    private readonly MotionValue<double> _progress;
    private string _before = string.Empty;
    private string _after = string.Empty;
    private string _current = string.Empty;
    private string? _previous;
    private int _direction = 1;
    private int _value;
    private FormattedText? _beforeText, _afterText, _currentText, _previousText;

    static WalletStackCount()
    {
        AffectsMeasure<WalletStackCount>(FontFamilyProperty, FontSizeProperty);
        AffectsRender<WalletStackCount>(InkProperty);
    }

    public WalletStackCount()
    {
        IsHitTestVisible = false;
        Focusable = false;
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
        _progress = new MotionValue<double>(this, 1, MotionVectors.Double);
        _progress.Changed += _ => InvalidateVisual();
    }

    public IBrush? Ink
    {
        get => GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The text as read ("2 of 4"; tests read it).</summary>
    public string Text => _before + _current + _after;

    /// <summary>The roll's progress (1 at rest).</summary>
    public double Progress => _progress.Value;

    /// <summary>Shows <paramref name="number"/>; rolls when <paramref name="animate"/> and the number changed.</summary>
    public void Set(string before, int value, string number, string after, bool animate)
    {
        var changed = number != _current;
        if (changed && animate && _current.Length > 0)
        {
            _direction = value >= _value ? 1 : -1;
            _previous = _current;
            _previousText = _currentText;
            _progress.Jump(0);
            _ = _progress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(700), s_ease));
        }
        else if (!animate)
        {
            _previous = null;
            _progress.Jump(1);
        }
        if (before != _before || after != _after || changed)
        {
            _before = before;
            _after = after;
            _current = number;
            _value = value;
            _beforeText = _afterText = _currentText = null;
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private FormattedText Format(string text) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily), FontSize, Ink ?? Brushes.Gray) { };

    private void EnsureText()
    {
        _beforeText ??= Format(_before);
        _afterText ??= Format(_after);
        _currentText ??= Format(_current);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InkProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            _beforeText = _afterText = _currentText = _previousText = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureText();
        return new Size(_beforeText!.WidthIncludingTrailingWhitespace + _currentText!.WidthIncludingTrailingWhitespace +
                        _afterText!.WidthIncludingTrailingWhitespace, Math.Ceiling(FontSize * 1.4));
    }

    public override void Render(DrawingContext context)
    {
        EnsureText();
        var lineHeight = FontSize * 1.4;
        var top = (Bounds.Height - lineHeight) / 2 + (lineHeight - _currentText!.Height) / 2;
        var x = Bounds.Width - _afterText!.WidthIncludingTrailingWhitespace;
        context.DrawText(_afterText, new Point(x, top));
        x -= _currentText.WidthIncludingTrailingWhitespace;
        var p = Math.Clamp(_progress.Value, 0, 1);
        var travel = ArcMotion.IsFull(this) ? Roll : 0;
        if (_previous is not null && p < 1)
        {
            _previousText ??= Format(_previous);
            using (context.PushOpacity(1 - p))
            {
                var px = x + _currentText.WidthIncludingTrailingWhitespace - _previousText.WidthIncludingTrailingWhitespace;
                context.DrawText(_previousText, new Point(px, top - travel * p * _direction));
            }
            using (context.PushOpacity(p))
            {
                context.DrawText(_currentText, new Point(x, top + travel * (1 - p) * _direction));
            }
        }
        else
        {
            context.DrawText(_currentText, new Point(x, top));
        }
        x -= _beforeText!.WidthIncludingTrailingWhitespace;
        context.DrawText(_beforeText, new Point(x, top));
    }
}
