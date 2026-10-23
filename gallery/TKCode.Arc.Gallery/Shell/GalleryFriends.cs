using TKCode.Arc.Gallery.Localization;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>A site on the Friends page and the home page: a sponsor or a friend.</summary>
/// <param name="Name">The name shown on the card.</param>
/// <param name="Url">Where the card opens.</param>
/// <param name="Description">The shell text that describes it.</param>
/// <param name="Glyph">One to three letters drawn in the card's mark.</param>
/// <param name="Hue">The hue (0 to 360) of the mark's gradient.</param>
/// <param name="Logo">The name of the logo file, <c>friends/&lt;Logo&gt;.png</c> under the site's root (see <see cref="GalleryLogos"/>); null for none.</param>
public sealed record GalleryFriend(string Name, Uri Url, GalleryShellLangResourceKind Description, string Glyph, double Hue, string? Logo = null);

/// <summary>
/// The sites the Friends page lists. To add a sponsor or a friend, add a line to <see cref="Sponsors"/> or
/// <see cref="Friends"/> and a description to the shell text (<c>Localization/*.xlf</c>); an empty <see cref="Sponsors"/>
/// shows "Your logo here" slots instead.
/// </summary>
public static class GalleryFriends
{
    /// <summary>
    /// The main sponsor's site, which the banner and its card open. The address names where the visit came from
    /// (<c>utm_source=tkcode-arc</c>, which the sponsor's sign-up attribution recognises as TKCode.Arc).
    /// </summary>
    public static Uri SponsorSiteUrl { get; } = SponsorSite("card");

    /// <summary>The sponsor's site with the source of the visit; <paramref name="content"/> says which link on the Gallery was pressed.</summary>
    public static Uri SponsorSite(string content) =>
        new($"https://nextco.work/?utm_source=tkcode-arc&utm_medium=gallery&utm_campaign=sponsor&utm_content={content}");

    /// <summary>The address "Become a sponsor" opens.</summary>
    public static Uri SponsorUrl { get; } = new($"{GalleryGitHub.RepositoryUrl}/issues");

    /// <summary>Companies and people who fund the work.</summary>
    public static IReadOnlyList<GalleryFriend> Sponsors { get; } =
    [
        new("nextco.work", SponsorSiteUrl, GalleryShellLangResourceKind.FriendNextcoDesc, "nx", 280, "nextco")
    ];

    /// <summary>The projects and sites Arc builds on and recommends.</summary>
    public static IReadOnlyList<GalleryFriend> Friends { get; } =
    [
        new("Avalonia UI", new Uri("https://avaloniaui.net"), GalleryShellLangResourceKind.FriendAvaloniaDesc, "Av", 265, "avalonia"),
        new("AtomUI", new Uri("https://github.com/AtomUI/AtomUI"), GalleryShellLangResourceKind.FriendAtomUIDesc, "At", 200, "atomui"),
        new("Lucide", new Uri("https://lucide.dev"), GalleryShellLangResourceKind.FriendLucideDesc, "Lu", 25, "lucide"),
        new("AIDotNet", new Uri("https://github.com/AIDotNet"), GalleryShellLangResourceKind.FriendAiDotNetDesc, "AI", 150, "aidotnet")
    ];
}
