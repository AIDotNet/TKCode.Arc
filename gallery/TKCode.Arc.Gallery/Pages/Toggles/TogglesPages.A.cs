namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterTogglesA()
    {
        Register("RadioCards", RadioCardsPage.Create);
        Register("BillingToggle", BillingTogglePage.Create);
        Register("Checkbox", CheckboxPage.Create);
        Register("RadioGroup", RadioGroupPage.Create);
        Register("SegmentedControl", SegmentedControlPage.Create);
    }
}
