namespace TKCode.Arc.Gallery.Pages;

// Pages of the Mobile section (the components of TKCode.Arc.Mobile). Every page is a docs page with live demos
// (Docs/Mobile/<Component>/<lang>.md, Demos/Mobile/<Component>/<Name>.axaml), which GalleryPages.Create shows as a
// GalleryPageView; the demos run inside a PhoneFrame (GalleryDemoView). The hook is here so a Mobile component that needs
// a hand-built page can register one (GalleryPages.Register) like the other categories.
public static partial class GalleryPages
{
    static partial void RegisterMobile()
    {
    }
}
