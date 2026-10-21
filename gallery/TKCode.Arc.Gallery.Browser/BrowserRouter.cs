using System;
using System.Runtime.InteropServices.JavaScript;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// Keeps the open page in the browser's address bar: opening a page pushes its address (<c>/components/button</c>) and
/// title onto the history, and Back, Forward and an address typed into the bar open the page they name. The functions
/// behind it are the <c>arc-nav</c> module of main.js. Addresses are relative to the document's base (<c>&lt;base&gt;</c>
/// in index.html), so the site can be served from a sub-path.
/// </summary>
internal static partial class BrowserRouter
{
    private static bool s_fromBrowser;

    /// <summary>The page the address names; the home page when it names none.</summary>
    public static GalleryRoute InitialRoute() => GalleryRoute.Parse(CurrentPath()) ?? GalleryRoute.Home;

    public static void Attach(GalleryShell shell)
    {
        // The address the page opened on is kept when it named a page and replaced by the home page's when it did not.
        Replace(shell.Route.Path, shell.Route.Title);
        shell.RouteChanged += route =>
        {
            if (s_fromBrowser)
            {
                SetTitle(route.Title);
            }
            else
            {
                Push(route.Path, route.Title);
            }
        };
        Listen(path =>
        {
            // Back and Forward already moved the history; opening the page must not push another entry.
            s_fromBrowser = true;
            try
            {
                shell.Navigate(GalleryRoute.Parse(path) ?? GalleryRoute.Home);
            }
            finally
            {
                s_fromBrowser = false;
            }
        });
    }

    [JSImport("path", "arc-nav")]
    private static partial string CurrentPath();

    [JSImport("push", "arc-nav")]
    private static partial void Push(string path, string title);

    [JSImport("replace", "arc-nav")]
    private static partial void Replace(string path, string title);

    [JSImport("title", "arc-nav")]
    private static partial void SetTitle(string title);

    [JSImport("listen", "arc-nav")]
    private static partial void Listen([JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onPop);
}
