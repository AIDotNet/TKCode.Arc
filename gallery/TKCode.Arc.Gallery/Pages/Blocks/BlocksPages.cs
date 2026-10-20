namespace TKCode.Arc.Gallery.Pages;

// Pages of the Blocks category (one hook per block group under Blocks/, implemented in BlocksPages.A.cs /
// BlocksPages.B.cs as blocks land; unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterBlocks()
    {
        RegisterBlocksA();
    }

    static partial void RegisterBlocksA();
}
