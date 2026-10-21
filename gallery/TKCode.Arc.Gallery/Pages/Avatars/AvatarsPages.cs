namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// AvatarsPages.A.cs / AvatarsPages.B.cs / AvatarsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterAvatars()
    {
        RegisterAvatarsA();
        RegisterAvatarsB();
        RegisterAvatarsC();
    }

    static partial void RegisterAvatarsA();

    static partial void RegisterAvatarsB();

    static partial void RegisterAvatarsC();
}
