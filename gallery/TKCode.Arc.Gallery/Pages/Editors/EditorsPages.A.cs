namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterEditorsA()
    {
        Register("RichTextEditor", RichTextEditorPage.Create);
        Register("SignaturePad", SignaturePadPage.Create);
        Register("FileDropzone", FileDropzonePage.Create);
    }
}
