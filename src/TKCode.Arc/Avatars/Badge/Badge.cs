using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace TKCode.Arc;

/// <summary>Color treatment of a <see cref="Badge"/>.</summary>
public enum BadgeTone
{
    Neutral,
    Success,

    /// <summary>Uses the accent color.</summary>
    Info,
    Warning,
    Danger
}

/// <summary>Size of a <see cref="Badge"/>: Medium is 26px tall, Small 22px.</summary>
public enum BadgeSize
{
    Small,
    Medium
}

/// <summary>
/// A small pill for status, category or metadata (Arc Badge): a tone, an optional leading icon and a short label.
/// It is a standalone inline label, not a count overlay. When a string or number label changes while the badge stays
/// mounted, the new text rises out of a soft blur, the old one lifts away, and the pill width springs to fit (P2, P5);
/// a new icon pops in from 0.6. Other content is shown as is. Reduced motion crossfades and the width jumps.
/// </summary>
[PseudoClasses(TextPseudoClass, IconPseudoClass)]
public class Badge : ContentControl
{
    public const string TextPseudoClass = ":text";
    public const string IconPseudoClass = ":icon";

    /// <summary>Color treatment (Arc <c>tone</c>, neutral by default).</summary>
    public static readonly StyledProperty<BadgeTone> ToneProperty =
        AvaloniaProperty.Register<Badge, BadgeTone>(nameof(Tone));

    /// <summary>Medium (26px, default) or Small (22px).</summary>
    public static readonly StyledProperty<BadgeSize> SizeProperty =
        AvaloniaProperty.Register<Badge, BadgeSize>(nameof(Size), BadgeSize.Medium);

    /// <summary>Leading icon (for example an <see cref="ArcGlyph"/>); it takes the tone color and is hidden from assistive technology.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<Badge, object?>(nameof(Icon));

    public BadgeTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public BadgeSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    static Badge()
    {
        FocusableProperty.OverrideDefaultValue<Badge>(false);
    }

    public Badge()
    {
        UpdateContentState();
    }

    /// <summary>True for content Arc animates in place: strings and numbers.</summary>
    internal static bool IsTextual(object? content) => content is string or sbyte or byte or short or ushort or int or uint
        or long or ulong or float or double or decimal;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == IconProperty)
        {
            UpdateContentState();
        }
    }

    private void UpdateContentState()
    {
        PseudoClasses.Set(TextPseudoClass, IsTextual(Content));
        PseudoClasses.Set(IconPseudoClass, Icon is not null);
        if (IsTextual(Content))
        {
            Avalonia.Automation.AutomationProperties.SetName(this, Convert.ToString(Content, System.Globalization.CultureInfo.CurrentCulture));
        }
    }
}
