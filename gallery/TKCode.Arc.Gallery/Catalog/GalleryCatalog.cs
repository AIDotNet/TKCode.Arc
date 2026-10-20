namespace TKCode.Arc.Gallery.Catalog;

public sealed record GalleryEntry(string ClassName, string Title, string Slug, int Phase, string Description)
{
    public string ArcUrl => $"https://uiarc.dev/components/{Slug}";
}

public sealed record GalleryCategory(string Code, string Title, string ChineseTitle, GalleryEntry[] Entries);

public static partial class GalleryCatalog
{
    public static int Count => Categories.Sum(c => c.Entries.Length);
}
