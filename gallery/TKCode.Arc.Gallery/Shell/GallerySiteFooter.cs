using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The footer of the site's pages: the library's own <see cref="SiteFooter"/> with the products, the developer pages, the
/// community links and the sponsor. The desktop app leaves out the download.
/// </summary>
internal static class GallerySiteFooter
{
    public static SiteFooter Create(GalleryShell shell, Action? download = null)
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

        SiteFooterLink External(GalleryShellLangResourceKind label, Uri url)
        {
            var link = new SiteFooterLink { IsExternal = true };
            links[link] = (label, from => GallerySite.Open(from, url));
            return link;
        }

        var products = new SiteFooterColumn();
        products.Links.Add(Page(GalleryShellLangResourceKind.ProductArc, () => shell.RouteFor(GalleryRouteKind.Components)));
        var streamMark = new SiteFooterLink { Label = "StreamMark" };
        links[streamMark] = (null, _ => shell.Navigate(GalleryRoute.StreamMark));
        products.Links.Add(streamMark);
        products.Links.Add(Page(GalleryShellLangResourceKind.Category_Blocks, () => shell.RouteFor(GalleryRouteKind.Blocks)));
        products.Links.Add(Page(GalleryShellLangResourceKind.ProductSpecial, () => GalleryRoute.To(GalleryCatalog.Special[0].AllEntries.First())));
        products.Links.Add(Page(GalleryShellLangResourceKind.ProductMobile, () => GalleryRoute.To(GalleryCatalog.Mobile.Entries[0])));
        if (download is not null)
        {
            var link = new SiteFooterLink();
            links[link] = (GalleryShellLangResourceKind.NavDownload, _ => download());
            products.Links.Add(link);
        }

        var developers = new SiteFooterColumn();
        developers.Links.Add(Page(GalleryShellLangResourceKind.Category_Docs, () => shell.RouteFor(GalleryRouteKind.Docs)));
        developers.Links.Add(Page(GalleryShellLangResourceKind.ProductTemplates, () => GallerySite.Docs("DocsTemplates")));
        developers.Links.Add(Page(GalleryShellLangResourceKind.NavChangelog, () => GallerySite.Docs("DocsChangelog")));

        var community = new SiteFooterColumn();
        community.Links.Add(Site("GitHub", GalleryGitHub.RepositoryUrl));
        community.Links.Add(External(GalleryShellLangResourceKind.FooterStreamMarkRepo, GalleryStreamMarkView.RepositoryUrl));
        community.Links.Add(Page(GalleryShellLangResourceKind.NavFriends, () => GalleryRoute.Friends));

        var sponsor = new SiteFooterColumn();
        sponsor.Links.Add(Site("nextco.work", GalleryFriends.SponsorSite("footer")));

        var license = new SiteFooterLink();
        links[license] = (GalleryShellLangResourceKind.FooterLicense, from => GallerySite.Open(from, new Uri($"{GalleryGitHub.RepositoryUrl}/blob/main/LICENSE")));
        var version = new SiteFooterLink { Label = GalleryHost.Version };
        links[version] = (null, _ => shell.Navigate(GallerySite.Docs("DocsChangelog")));

        var github = new SiteFooterLink { Label = "GitHub", IsExternal = true };
        links[github] = (null, from => GallerySite.Open(from, GalleryGitHub.RepositoryUrl));

        var footer = new SiteFooter
        {
            Variant = SiteFooterVariant.Columns,
            BrandName = "TKCode",
            // The Gallery's own logo, not the arch of the block's default.
            BrandMark = new Image { Source = GalleryBrand.CreateLogo(), Width = 22, Height = 22 },
            IsStatusVisible = false,
            IsNewsletterVisible = false,
            BorderThickness = new Thickness(0)
        };
        footer.Columns.AddRange([products, developers, community, sponsor]);
        footer.LegalLinks.AddRange([license, version]);
        footer.SocialLinks.Add(github);

        void Refresh()
        {
            footer.Tagline = GalleryText.Get(GalleryShellLangResourceKind.FooterTagline);
            products.Title = GalleryText.Get(GalleryShellLangResourceKind.FooterProducts);
            developers.Title = GalleryText.Get(GalleryShellLangResourceKind.FooterDevelopers);
            community.Title = GalleryText.Get(GalleryShellLangResourceKind.FooterCommunity);
            sponsor.Title = GalleryText.Get(GalleryShellLangResourceKind.FooterSponsor);
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
