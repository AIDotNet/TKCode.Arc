namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterMessagesA()
    {
        Register("Alert", AlertPage.Create);
        Register("Toast", ToastPage.Create);
        Register("ToastStack", ToastStackPage.Create);
        Register("AnnouncementBar", AnnouncementBarPage.Create);
        Register("CookieConsent", CookieConsentPage.Create);
    }
}
