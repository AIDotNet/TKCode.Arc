using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One person currently viewing, as a <see cref="PresenceStack"/> shows them: a name (initials stand in without a
/// picture), an optional picture, the color of their ring (their cursor color, say) and whether they are idle.
/// </summary>
public class PresenceStackViewer : AvaloniaObject
{
    /// <summary>The person's name: the tooltip, the list and the accessible name; its initials stand in without a picture.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<PresenceStackViewer, string?>(nameof(Name));

    /// <summary>The person's picture (any source an Arc <c>Avatar</c> takes); null shows initials.</summary>
    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<PresenceStackViewer, ImageSource?>(nameof(Source));

    /// <summary>The ring color; null takes a chart series color picked from the name, so a person keeps one color.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<PresenceStackViewer, IBrush?>(nameof(Brush));

    /// <summary>True while the person has the page open but is away: the avatar fades and the ring turns gray.</summary>
    public static readonly StyledProperty<bool> IsIdleProperty =
        AvaloniaProperty.Register<PresenceStackViewer, bool>(nameof(IsIdle));

    /// <summary>A short note under the name in the tooltip and the list ("Editing slide 3", "On mobile").</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<PresenceStackViewer, string?>(nameof(Detail));

    /// <summary>Creates an empty viewer (set <see cref="Name"/>).</summary>
    public PresenceStackViewer()
    {
    }

    /// <summary>Creates a viewer with a name and, optionally, a ring color.</summary>
    public PresenceStackViewer(string name, IBrush? brush = null, string? detail = null)
    {
        Name = name;
        Brush = brush;
        Detail = detail;
    }

    /// <summary>The person's name: the tooltip, the list and the accessible name; its initials stand in without a picture.</summary>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <summary>The person's picture (any source an Arc <c>Avatar</c> takes); null shows initials.</summary>
    public ImageSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>The ring color; null takes a chart series color picked from the name, so a person keeps one color.</summary>
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    /// <summary>True while the person has the page open but is away: the avatar fades and the ring turns gray.</summary>
    public bool IsIdle { get => GetValue(IsIdleProperty); set => SetValue(IsIdleProperty, value); }

    /// <summary>A short note under the name in the tooltip and the list ("Editing slide 3", "On mobile").</summary>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
}
