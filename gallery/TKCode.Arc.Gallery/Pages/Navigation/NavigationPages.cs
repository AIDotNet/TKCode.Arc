namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// NavigationPages.A.cs / NavigationPages.B.cs / NavigationPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterNavigation()
    {
        RegisterNavigationA();
        RegisterNavigationB();
        RegisterNavigationC();
    }

    static partial void RegisterNavigationA();

    static partial void RegisterNavigationB();

    static partial void RegisterNavigationC();
}
