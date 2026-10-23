using TKCode.Arc.Gallery.Localization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The dismissible banner above the top bar that thanks the main sponsor: an <see cref="AnnouncementBar"/> with one message
/// and a link, in the language that is current. Closing it is remembered for a day (<c>DismissalKey</c>, <c>DismissalDuration</c>).
/// </summary>
public sealed class GallerySponsorBanner : AnnouncementBar
{
    private const string Id = "sponsor-nextco";

    public GallerySponsorBanner()
    {
        DismissalKey = Id;
        // Closing it hides it for a day; it comes back after that.
        DismissalDuration = TimeSpan.FromDays(1);
        IsDismissible = true;
        Label = "Sponsor";
        // The message is plain text, not a binding: it is set again when the language changes.
        GalleryText.Track(this, Refresh);
    }

    protected override Type StyleKeyOverride => typeof(AnnouncementBar);

    private void Refresh()
    {
        Messages.Clear();
        Messages.Add(new Announcement
        {
            Id = Id,
            Message = GalleryText.Get(GalleryShellLangResourceKind.SponsorBannerText),
            Action = new AnnouncementAction { Label = GalleryText.Get(GalleryShellLangResourceKind.SponsorBannerAction), Href = GalleryFriends.SponsorSite("banner") }
        });
    }
}
