namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// MessagesPages.A.cs / MessagesPages.B.cs / MessagesPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterMessages()
    {
        RegisterMessagesA();
        RegisterMessagesB();
        RegisterMessagesC();
    }

    static partial void RegisterMessagesA();

    static partial void RegisterMessagesB();

    static partial void RegisterMessagesC();
}
