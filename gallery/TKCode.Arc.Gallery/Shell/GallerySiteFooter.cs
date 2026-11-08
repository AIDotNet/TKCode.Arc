using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>The footer of the site's pages: the library's own <see cref="SiteFooter"/> in its one-row layout.</summary>
internal static class GallerySiteFooter
{
    public static SiteFooter Create(GalleryShell shell)
    {
        var links = new Dictionary<SiteFooterLink, (GalleryShellLangResourceKind? Label, Action<Control> Open)>();

        SiteFooterLink Page(GalleryShellLangResourceKind label, Func<GalleryRoute> route)
        {
            var link = new SiteFooterLink();
            links[link] = (label, _ => shell.Navigate(route()));
            return link;
        }

        SiteFooterLink Site(string name, Uri url)
        {
            var link = new SiteFooterLink { Label = name, IsExternal = true };
            links[link] = (null, from => GallerySite.Open(from, url));
            return link;
        }

        var row = new AvaloniaList<SiteFooterLink>
        {
            Page(GalleryShellLangResourceKind.Components, () => GalleryRoute.ComponentsIndex),
            Page(GalleryShellLangResourceKind.NavProjects, () => GalleryRoute.Projects),
            Page(GalleryShellLangResourceKind.Category_Docs, () => shell.RouteFor(GalleryRouteKind.Docs)),
            Page(GalleryShellLangResourceKind.NavChangelog, () => GallerySite.Docs("DocsChangelog")),
            Page(GalleryShellLangResourceKind.NavFriends, () => GalleryRoute.Friends),
            Site("nextco.work", GalleryFriends.SponsorSite("footer"))
        };
        var github = Site("GitHub", GalleryGitHub.RepositoryUrl);

        var footer = new SiteFooter
        {
            Variant = SiteFooterVariant.Minimal,
            BrandName = "TKCode",
            // The Gallery's own logo, not the arch of the block's default.
            BrandMark = new Image { Source = GalleryBrand.CreateLogo(), Width = 22, Height = 22 },
            IsStatusVisible = false,
            BorderThickness = new Thickness(0),
            Links = row
        };
        footer.SocialLinks.Add(github);

        void Refresh()
        {
            footer.Tagline = GalleryText.Get(GalleryShellLangResourceKind.FooterTagline);
            foreach (var (link, (label, _)) in links)
            {
                if (label is { } kind)
                {
                    link.Label = GalleryText.Get(kind);
                }
            }
        }
        Refresh();
        // The text is plain strings, not bindings: it is set again when the language changes.
        GalleryText.Track(footer, Refresh);

        footer.Navigate += (_, e) =>
        {
            e.Handled = true;
            if (e.Link is { } link && links.TryGetValue(link, out var target))
            {
                target.Open(footer);
            }
            else if (e.Kind == SiteFooterNavigationKind.Brand)
            {
                shell.Navigate(GalleryRoute.Home);
            }
        };
        return footer;
    }
}
