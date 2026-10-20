namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// TextFieldsPages.A.cs / TextFieldsPages.B.cs / TextFieldsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterTextFields()
    {
        RegisterTextFieldsA();
        RegisterTextFieldsB();
        RegisterTextFieldsC();
    }

    static partial void RegisterTextFieldsA();

    static partial void RegisterTextFieldsB();

    static partial void RegisterTextFieldsC();
}
