namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterSpecialInputsA()
    {
        Register("NumberField", NumberFieldPage.Create);
        Register("MoneyInput", MoneyInputPage.Create);
        Register("PhoneInput", PhoneInputPage.Create);
        Register("ScrubInput", ScrubInputPage.Create);
        Register("TagInput", TagInputPage.Create);
        Register("MentionInput", MentionInputPage.Create);
        Register("ShortcutRecorder", ShortcutRecorderPage.Create);
    }
}
