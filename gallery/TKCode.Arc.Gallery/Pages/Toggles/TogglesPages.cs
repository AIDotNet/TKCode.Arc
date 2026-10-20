namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// TogglesPages.A.cs / TogglesPages.B.cs / TogglesPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterToggles()
    {
        Register("Switch", SwitchPage.Create);
        RegisterTogglesA();
        RegisterTogglesB();
        RegisterTogglesC();
    }

    static partial void RegisterTogglesA();

    static partial void RegisterTogglesB();

    static partial void RegisterTogglesC();
}
