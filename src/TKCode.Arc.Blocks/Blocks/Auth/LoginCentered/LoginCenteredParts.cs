// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): the small controls of login-centered.tsx / .module.css.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The quiet pill buttons of the block (themed in LoginCenteredTheme.axaml by class): <c>text</c> for "Use a passkey
/// instead" and "Resend code" (secondary text, 36 high, hover and press fill the pill), <c>switch</c> for the mode switch
/// in the top bar and <c>legal</c> for the footer links (32 high). <see cref="AuthInertButton.IsInert"/> is Arc's
/// <c>aria-disabled</c>: the button keeps its focus and its place in the tab order but shows the muted, regular-weight look
/// and does nothing.
/// </summary>
internal sealed class LoginCenteredPillButton : AuthInertButton
{
    protected override Type StyleKeyOverride => typeof(LoginCenteredPillButton);
}

/// <summary>
/// "Change" after the address in the code view: medium text in the foreground color with a 1px underline 3px under the
/// baseline in the strong border color, which takes the text color on hover. It sits in the sentence as an inline
/// element, so the sentence wraps around it.
/// </summary>
internal sealed class LoginCenteredInlineButton : AvaloniaButton
{
    private readonly TextBlock _text = new();
    private IBrush? _underline;

    public LoginCenteredInlineButton()
    {
        Content = _text;
        _text.TextWrapping = TextWrapping.NoWrap;
        _text.LineHeight = 19.6;
    }

    protected override Type StyleKeyOverride => typeof(LoginCenteredInlineButton);

    /// <summary>The visible text.</summary>
    public string? Text
    {
        get => _text.Text;
        set => _text.Text = value;
    }

    /// <summary>The underline's brush: the strong border color at rest, the text color on hover (set by the theme).</summary>
    public static readonly StyledProperty<IBrush?> UnderlineBrushProperty =
        AvaloniaProperty.Register<LoginCenteredInlineButton, IBrush?>(nameof(UnderlineBrush));

    public IBrush? UnderlineBrush { get => GetValue(UnderlineBrushProperty); set => SetValue(UnderlineBrushProperty, value); }

    /// <summary>The text block inside (its baseline places the button in the sentence).</summary>
    internal TextBlock TextBlock => _text;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UnderlineBrushProperty && !ReferenceEquals(_underline, UnderlineBrush))
        {
            _underline = UnderlineBrush;
            // A new collection makes the text block draw the new color (a decoration's own brush change is not observed).
            _text.TextDecorations = _underline is null
                ? null
                : new TextDecorationCollection
                {
                    new TextDecoration
                    {
                        Location = TextDecorationLocation.Baseline,
                        Stroke = _underline,
                        StrokeThickness = 1,
                        StrokeThicknessUnit = TextDecorationUnit.Pixel,
                        StrokeOffset = 3,
                        StrokeOffsetUnit = TextDecorationUnit.Pixel
                    }
                };
        }
    }
}

/// <summary>The product mark in the top bar: Arc's 20 × 20 arch with a dot, stroked 2px in the accent.</summary>
internal sealed class LoginCenteredBrandMark : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LoginCenteredBrandMark>();

    private static readonly Geometry s_arch = StreamGeometry.Parse("M3 15a7 7 0 0 1 14 0");
    private Pen? _pen;
    private IBrush? _penBrush;

    static LoginCenteredBrandMark()
    {
        AffectsRender<LoginCenteredBrandMark>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginCenteredBrandMark>(false);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(20, 20);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        if (!ReferenceEquals(brush, _penBrush))
        {
            _penBrush = brush;
            _pen = new Pen(brush, 2, lineCap: PenLineCap.Round);
        }
        var scale = Bounds.Width / 20;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(null, _pen, s_arch);
            context.DrawEllipse(brush, null, new Point(10, 15), 1.75, 1.75);
        }
    }
}
