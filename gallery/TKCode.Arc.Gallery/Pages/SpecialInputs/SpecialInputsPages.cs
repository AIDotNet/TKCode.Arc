namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// SpecialInputsPages.A.cs / SpecialInputsPages.B.cs / SpecialInputsPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterSpecialInputs()
    {
        RegisterSpecialInputsA();
        RegisterSpecialInputsB();
        RegisterSpecialInputsC();
    }

    static partial void RegisterSpecialInputsA();

    static partial void RegisterSpecialInputsB();

    static partial void RegisterSpecialInputsC();
}
