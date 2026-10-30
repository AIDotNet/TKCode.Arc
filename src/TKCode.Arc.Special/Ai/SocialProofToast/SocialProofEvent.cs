using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Who a <see cref="SocialProofEvent"/> is about, which decides how its mark is drawn.</summary>
public enum SocialProofActor
{
    /// <summary>A person: a round avatar with their portrait, or their initials.</summary>
    Person,

    /// <summary>A company: a rounded square with its mark, or the first letter of its name.</summary>
    Company
}

/// <summary>Where a <see cref="SocialProofToast"/> sits.</summary>
public enum SocialProofToastPosition
{
    /// <summary>In the layout, filling its container's width (docs and previews). New toasts arrive at the bottom.</summary>
    Inline,

    /// <summary>Pinned to the window's bottom left corner, 24px from the edges.</summary>
    BottomLeft,

    /// <summary>Pinned to the window's bottom right corner, 24px from the edges.</summary>
    BottomRight,

    /// <summary>Pinned to the window's top left corner, 24px from the edges; the stack grows downward.</summary>
    TopLeft,

    /// <summary>Pinned to the window's top right corner, 24px from the edges; the stack grows downward.</summary>
    TopRight
}

/// <summary>
/// One entry in a <see cref="SocialProofToast"/> feed: a person or a company, what they did, and when. Feed it real,
/// recent events from your billing or analytics backend.
/// </summary>
public sealed class SocialProofEvent : AvaloniaObject
{
    /// <summary>Stable id, used to match a toast to its event in callbacks.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<SocialProofEvent, string>(nameof(Id), string.Empty);

    /// <summary>A first name or a company, shown in medium weight.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<SocialProofEvent, string>(nameof(Name), string.Empty);

    /// <summary>What they did, read after the name: "started a Pro trial".</summary>
    public static readonly StyledProperty<string> ActionProperty =
        AvaloniaProperty.Register<SocialProofEvent, string>(nameof(Action), string.Empty);

    /// <summary>Short context before the time, such as a city or a seat count.</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<SocialProofEvent, string?>(nameof(Meta));

    /// <summary>When it happened. Shown as relative time: Just now, 4 min ago, 2 h ago.</summary>
    public static readonly StyledProperty<DateTimeOffset?> TimeProperty =
        AvaloniaProperty.Register<SocialProofEvent, DateTimeOffset?>(nameof(Time));

    /// <summary>A time written out, shown as is (it takes the place of <see cref="Time"/>).</summary>
    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<SocialProofEvent, string?>(nameof(TimeText));

    /// <summary>A person (round avatar) or a company (rounded square mark). Person by default.</summary>
    public static readonly StyledProperty<SocialProofActor> ActorProperty =
        AvaloniaProperty.Register<SocialProofEvent, SocialProofActor>(nameof(Actor));

    /// <summary>A portrait for a person. Initials show while it loads or when it fails.</summary>
    public static readonly StyledProperty<ImageSource?> PortraitProperty =
        AvaloniaProperty.Register<SocialProofEvent, ImageSource?>(nameof(Portrait));

    /// <summary>A mark as a 24 × 24 stroked glyph, drawn at 18px in the text color so it follows the theme: a company's logo, or a person's own mark when there is no portrait. Without one, a company shows the first letter of its name.</summary>
    public static readonly StyledProperty<Geometry?> LogoProperty =
        AvaloniaProperty.Register<SocialProofEvent, Geometry?>(nameof(Logo));

    /// <summary>Shows a check badge with <see cref="VerifiedLabel"/>, or "Verified".</summary>
    public static readonly StyledProperty<bool> IsVerifiedProperty =
        AvaloniaProperty.Register<SocialProofEvent, bool>(nameof(IsVerified));

    /// <summary>The verified badge's label, such as "Verified purchase". Setting it also shows the badge.</summary>
    public static readonly StyledProperty<string?> VerifiedLabelProperty =
        AvaloniaProperty.Register<SocialProofEvent, string?>(nameof(VerifiedLabel));

    /// <summary>Creates an empty event (for XAML).</summary>
    public SocialProofEvent()
    {
    }

    /// <summary>Creates an event with its id, name and action.</summary>
    public SocialProofEvent(string id, string name, string action)
    {
        Id = id;
        Name = name;
        Action = action;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ActionProperty"/>
    public string Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="TimeProperty"/>
    public DateTimeOffset? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    /// <inheritdoc cref="TimeTextProperty"/>
    public string? TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }

    /// <inheritdoc cref="ActorProperty"/>
    public SocialProofActor Actor { get => GetValue(ActorProperty); set => SetValue(ActorProperty, value); }

    /// <inheritdoc cref="PortraitProperty"/>
    public ImageSource? Portrait { get => GetValue(PortraitProperty); set => SetValue(PortraitProperty, value); }

    /// <inheritdoc cref="LogoProperty"/>
    public Geometry? Logo { get => GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    /// <inheritdoc cref="IsVerifiedProperty"/>
    public bool IsVerified { get => GetValue(IsVerifiedProperty); set => SetValue(IsVerifiedProperty, value); }

    /// <inheritdoc cref="VerifiedLabelProperty"/>
    public string? VerifiedLabel { get => GetValue(VerifiedLabelProperty); set => SetValue(VerifiedLabelProperty, value); }

    /// <summary>Whether the badge shows.</summary>
    internal bool ShowsVerified => IsVerified || !string.IsNullOrEmpty(VerifiedLabel);

    /// <summary>The sentence the toast reads: the name and the action.</summary>
    internal string Sentence => string.IsNullOrEmpty(Action) ? Name : Name + " " + Action;
}

/// <summary>Arguments of <see cref="SocialProofToast.Shown"/>, <see cref="SocialProofToast.Dismissed"/> and <see cref="SocialProofToast.Selected"/>.</summary>
public class SocialProofToastEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SocialProofToastEventArgs(RoutedEvent routedEvent, SocialProofEvent socialProofEvent)
        : base(routedEvent)
    {
        Event = socialProofEvent;
    }

    /// <summary>The event the toast shows.</summary>
    public SocialProofEvent Event { get; }
}
