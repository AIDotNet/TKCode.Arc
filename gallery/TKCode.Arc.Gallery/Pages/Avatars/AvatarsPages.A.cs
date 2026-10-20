namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterAvatarsA()
    {
        Register("AvatarGroup", AvatarGroupPage.Create);
        Register("Badge", BadgePage.Create);
    }
}
