// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): .textButton of signup-form.module.css.
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A text button inside a line of copy (Arc's <c>.textButton</c>): secondary text with an underline in the strong border
/// color 3px under the baseline (drawn here, so its color can move without a new text layout); a mouse or pen hover turns the text and the underline to the foreground (160 ms, standard).
/// <see cref="IsStrong"/> is the footer's "Sign in": foreground, medium weight, the underline hidden until hover. Keyboard
/// focus shows the 2px focus ring 2px outside, square (the browser outline of a text button).
/// </summary>
[TemplatePart(TextPart, typeof(TextBlock))]
[PseudoClasses(HoverPseudoClass, StrongPseudoClass)]
internal sealed class SignupFormLinkButton : AvaloniaButton
{
    public const string TextPart = "PART_Text";
    public const string HoverPseudoClass = ":hover";
    public const string StrongPseudoClass = ":strong";

    /// <summary>The footer's emphasized link: foreground, medium weight, no underline at rest.</summary>
    public static readonly StyledProperty<bool> IsStrongProperty =
        AvaloniaProperty.Register<SignupFormLinkButton, bool>(nameof(IsStrong));

    private readonly SolidColorBrush _underline = new(Colors.Transparent);
    private TextBlock? _text;
    private Pen? _pen;
    private MotionValue<Color>? _underlineColor;
    private bool _hover;

    protected override Type StyleKeyOverride => typeof(SignupFormLinkButton);

    /// <inheritdoc cref="IsStrongProperty"/>
    public bool IsStrong { get => GetValue(IsStrongProperty); set => SetValue(IsStrongProperty, value); }

    /// <summary>The underline's color right now (tests read it).</summary>
    internal Color UnderlineColor => _underline.Color;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<TextBlock>(TextPart);
        Resolve(animate: false);
    }

    /// <summary>Distance from the top of the button to its text's baseline.</summary>
    private double TextBaseline()
    {
        if (_text?.TextLayout is not { TextLines.Count: > 0 } layout)
        {
            return 0;
        }
        return _text.Bounds.Y + _text.Padding.Top + layout.TextLines[0].Baseline;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (_text?.TextLayout is { TextLines.Count: > 0 } layout)
        {
            // Inside a line of copy the button sits on the line's baseline (BaselineAlignment.Baseline), not its bottom.
            TextBlock.SetBaselineOffset(this, _text.Margin.Top + _text.Padding.Top + layout.TextLines[0].Baseline);
        }
        return size;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_text is null || _underline.Color.A == 0)
        {
            return;
        }
        // text-underline-offset: 3px from the alphabetic baseline, 1px thick, as wide as the text.
        _pen ??= new Pen(_underline, 1);
        var y = Math.Round(TextBaseline() + 3) + 0.5;
        context.DrawLine(_pen, new Point(_text.Bounds.X, y), new Point(_text.Bounds.Right, y));
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
        _underlineColor?.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStrongProperty)
        {
            PseudoClasses.Set(StrongPseudoClass, IsStrong);
            Resolve(animate: false);
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Resolve(animate: false);

    private Color Target()
    {
        if (_hover)
        {
            return ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        }
        return IsStrong ? Colors.Transparent : ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong);
    }

    private void Resolve(bool animate)
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        var target = Target();
        if (_underlineColor is null)
        {
            _underlineColor = new MotionValue<Color>(this, target, MotionVectors.Color);
            _underlineColor.Changed += c =>
            {
                _underline.Color = c;
                InvalidateVisual();
            };
        }
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            // Transparent → foreground crossfades the color with its alpha, as the browser interpolates it.
            var from = _underlineColor.Value;
            if (from.A == 0)
            {
                _underlineColor.Jump(Color.FromArgb(0, target.R, target.G, target.B));
            }
            else if (target.A == 0)
            {
                target = Color.FromArgb(0, from.R, from.G, from.B);
            }
            _ = _underlineColor.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            return;
        }
        _underlineColor.Jump(target);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Hover only for a mouse or a pen: Arc's (hover: hover) and (pointer: fine).
        if (e.Pointer.Type != PointerType.Touch)
        {
            SetHover(true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(false);
    }

    private void SetHover(bool hover)
    {
        if (_hover == hover)
        {
            return;
        }
        _hover = hover;
        PseudoClasses.Set(HoverPseudoClass, hover);
        Resolve(animate: true);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonAutomationPeer(this);
}
