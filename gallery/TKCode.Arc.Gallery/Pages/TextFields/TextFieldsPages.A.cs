namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterTextFieldsA()
    {
        Register("Input", InputPage.Create);
        Register("Textarea", TextareaPage.Create);
        Register("PasswordField", PasswordFieldPage.Create);
        Register("PasswordStrength", PasswordStrengthPage.Create);
        Register("SearchField", SearchFieldPage.Create);
        Register("ExpandingSearch", ExpandingSearchPage.Create);
        Register("InlineEdit", InlineEditPage.Create);
    }
}
