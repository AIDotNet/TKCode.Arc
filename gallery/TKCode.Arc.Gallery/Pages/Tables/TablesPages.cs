namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// TablesPages.A.cs / TablesPages.B.cs / TablesPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterTables()
    {
        RegisterTablesA();
        RegisterTablesB();
        RegisterTablesC();
    }

    static partial void RegisterTablesA();

    static partial void RegisterTablesB();

    static partial void RegisterTablesC();
}
