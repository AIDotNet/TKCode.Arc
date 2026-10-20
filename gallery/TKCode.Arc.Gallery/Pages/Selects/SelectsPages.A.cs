namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterSelectsA()
    {
        Register("Select", SelectPage.Create);
        Register("Combobox", ComboboxPage.Create);
        Register("MultiSelect", MultiSelectPage.Create);
        Register("MorphSelect", MorphSelectPage.Create);
        Register("ChipGroup", ChipGroupPage.Create);
    }
}
