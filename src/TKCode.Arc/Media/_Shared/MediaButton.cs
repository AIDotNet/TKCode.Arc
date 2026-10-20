using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Colour role of a <see cref="MediaButton"/> (Arc card stack <c>data-tone</c>).</summary>
internal enum MediaButtonTone
{
    /// <summary>Bordered surface button.</summary>
    Neutral,

    /// <summary>The approving action: accent fill that follows the selected accent.</summary>
    Primary,

    /// <summary>Borderless secondary text that strengthens on hover.</summary>
    Quiet
}

/// <summary>Geometry of a <see cref="MediaButton"/>.</summary>
internal enum MediaButtonShape
{
    /// <summary>36px circle with an 18px glyph (carousel play and arrows).</summary>
    Round,

    /// <summary>44px tall control radius button with a 16px glyph and a label (card stack actions).</summary>
    Wide
}

/// <summary>
/// The small control buttons of the Media components (Carousel arrows and play, CardStack Pass / Undo / Keep).
/// <see cref="IsSoftDisabled"/> mirrors Arc's <c>aria-disabled</c>: the button keeps keyboard focus and stays in the
/// tab order but ignores activation and dims. With <see cref="IsLabelHidden"/> only the glyph shows while the label
/// still names the button for assistive technology.
/// </summary>
[PseudoClasses(SoftDisabledPseudoClass, IconOnlyPseudoClass, HasGlyphPseudoClass, HasLabelPseudoClass)]
internal sealed class MediaButton : AvaloniaButton
{
    public const string SoftDisabledPseudoClass = ":soft-disabled";
    public const string IconOnlyPseudoClass = ":icon-only";
    public const string HasGlyphPseudoClass = ":has-glyph";
    public const string HasLabelPseudoClass = ":has-label";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<MediaButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<MediaButtonTone> ToneProperty =
        AvaloniaProperty.Register<MediaButton, MediaButtonTone>(nameof(Tone));

    public static readonly StyledProperty<MediaButtonShape> ShapeProperty =
        AvaloniaProperty.Register<MediaButton, MediaButtonShape>(nameof(Shape), MediaButtonShape.Wide);

    public static readonly StyledProperty<bool> IsSoftDisabledProperty =
        AvaloniaProperty.Register<MediaButton, bool>(nameof(IsSoftDisabled));

    public static readonly StyledProperty<bool> IsLabelHiddenProperty =
        AvaloniaProperty.Register<MediaButton, bool>(nameof(IsLabelHidden));

    /// <summary>Lucide glyph shown before the label.</summary>
    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public MediaButtonTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public MediaButtonShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    /// <summary>Arc <c>aria-disabled</c>: dimmed and inactive, but focusable.</summary>
    public bool IsSoftDisabled
    {
        get => GetValue(IsSoftDisabledProperty);
        set => SetValue(IsSoftDisabledProperty, value);
    }

    /// <summary>Shows only the glyph; the label remains the accessible name.</summary>
    public bool IsLabelHidden
    {
        get => GetValue(IsLabelHiddenProperty);
        set => SetValue(IsLabelHiddenProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(MediaButton);

    protected override void OnClick()
    {
        // aria-disabled: the press is swallowed but focus stays on the button.
        if (IsSoftDisabled)
        {
            return;
        }
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSoftDisabledProperty)
        {
            PseudoClasses.Set(SoftDisabledPseudoClass, IsSoftDisabled);
        }
        else if (change.Property == IsLabelHiddenProperty || change.Property == ContentProperty)
        {
            PseudoClasses.Set(IconOnlyPseudoClass, IsLabelHidden);
            PseudoClasses.Set(HasLabelPseudoClass, Content is not null && !IsLabelHidden);
            if (Content is string label)
            {
                Avalonia.Automation.AutomationProperties.SetName(this, label);
            }
        }
        else if (change.Property == GlyphProperty)
        {
            PseudoClasses.Set(HasGlyphPseudoClass, Glyph is not null);
        }
    }
}
