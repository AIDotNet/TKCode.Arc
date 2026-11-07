using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs the control center draws that are not in the Core icon subset.</summary>
internal static class ControlCenterIcons
{
    /// <summary>Lucide "headphones": the Focus tile while no session runs.</summary>
    public static Geometry Headphones => ArcIcons.Parse(
        "M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3");

    /// <summary>Lucide "users-round".</summary>
    public static Geometry UsersRound => ArcIcons.Parse(
        "M18 21a8 8 0 0 0-16 0 M5 8a5 5 0 1 0 10 0a5 5 0 1 0 -10 0z M22 20c0-3.37-2-6.5-4-8a5 5 0 0 0-.45-8.3");

    /// <summary>Lucide "sunset".</summary>
    public static Geometry Sunset => ArcIcons.Parse(
        "M12 10V2 M4.93 10.93l1.41 1.41 M2 18h2 M20 18h2 M19.07 10.93l-1.41 1.41 M22 22H2 M16 6l-4 4-4-4 M16 18a4 4 0 0 0-8 0");

    /// <summary>Lucide "presentation": the Presenting tile.</summary>
    public static Geometry Presentation => ArcIcons.Parse("M2 3h20 M21 3v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V3 M7 21l5-5 5 5");

    /// <summary>Lucide "volume-x": volume 0.</summary>
    public static Geometry VolumeX => ArcIcons.Parse(
        "M11 4.702a.7.7 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.7.7 0 0 0 11 19.298z M16.5 14.5l5-5 M16.5 9.5l5 5");

    /// <summary>Lucide "volume-1": volume 1 to 49.</summary>
    public static Geometry Volume1 => ArcIcons.Parse(
        "M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z M16 9a5 5 0 0 1 0 6");

    /// <summary>The volume glyph for a volume: muted at 0, one wave below 50, two waves from 50.</summary>
    public static Geometry Volume(int value) => value <= 0 ? VolumeX : value < 50 ? Volume1 : ArcIcons.Volume2;
}

/// <summary>
/// Invented sample content for <see cref="ControlCenter"/> demos and tests: three focus modes, three channels and three
/// people, none of them real.
/// </summary>
public static class ControlCenterSamples
{
    /// <summary>The sample panel name ("Studio controls").</summary>
    public const string PanelName = "Studio controls";

    /// <summary>The volume the sample panel starts at (64).</summary>
    public const int Volume = 64;

    /// <summary>Lucide "headphones", for your own focus modes.</summary>
    public static Geometry HeadphonesIcon => ControlCenterIcons.Headphones;

    /// <summary>Lucide "users-round", for your own focus modes.</summary>
    public static Geometry UsersRoundIcon => ControlCenterIcons.UsersRound;

    /// <summary>Lucide "sunset", for your own focus modes.</summary>
    public static Geometry SunsetIcon => ControlCenterIcons.Sunset;

    /// <summary>Three focus modes: Drafting (50 min), Reviewing (30 min) and Recharge (90 min).</summary>
    public static IReadOnlyList<ControlCenterFocusMode> FocusModes() =>
    [
        new ControlCenterFocusMode
        {
            Id = "drafting", Label = "Drafting", Icon = ControlCenterIcons.Headphones,
            Description = "Long stretches for writing and sketching", DefaultMinutes = 50
        },
        new ControlCenterFocusMode
        {
            Id = "review", Label = "Reviewing", Icon = ControlCenterIcons.UsersRound,
            Description = "Short block to read and comment on work", DefaultMinutes = 30
        },
        new ControlCenterFocusMode
        {
            Id = "recharge", Label = "Recharge", Icon = ControlCenterIcons.Sunset,
            Description = "Step away from the screen and stretch", DefaultMinutes = 90
        }
    ];

    /// <summary>Three channels: Comments and Calendar nudges on, Shared folders off.</summary>
    public static IReadOnlyList<ControlCenterChannel> Channels() =>
    [
        new ControlCenterChannel { Id = "comments", Label = "Comments", Description = "Notes left on your files", IsOn = true },
        new ControlCenterChannel { Id = "calendar", Label = "Calendar nudges", Description = "Ten minutes before an event", IsOn = true },
        new ControlCenterChannel { Id = "uploads", Label = "Shared folders", Description = "New uploads from clients", IsOn = false }
    ];

    /// <summary>Three people with initials avatars: Mara Lind, Oskar Vey and Ines Paz.</summary>
    public static IReadOnlyList<ControlCenterPerson> People() =>
    [
        new ControlCenterPerson { Id = "mara", Name = "Mara Lind" },
        new ControlCenterPerson { Id = "oskar", Name = "Oskar Vey" },
        new ControlCenterPerson { Id = "ines", Name = "Ines Paz" }
    ];

    /// <summary>Fills <paramref name="center"/> with the sample modes, channels and people, the sample name and volume 64.</summary>
    public static void Apply(ControlCenter center)
    {
        center.FocusModes.Clear();
        center.FocusModes.AddRange(FocusModes());
        center.Channels.Clear();
        center.Channels.AddRange(Channels());
        center.People.Clear();
        center.People.AddRange(People());
        center.Labels = new ControlCenterLabels { PanelName = PanelName };
        center.Volume = Volume;
    }
}
