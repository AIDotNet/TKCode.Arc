using System.Collections.ObjectModel;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The pictures and the invented names of the Photos demos (pictures from <see cref="PhotosDemoArt"/>).</summary>
public static class PhotosDemoData
{
    private static readonly string[] s_mugViews =
    [
        "Front, handle on the right",
        "Handle side, with the maker's mark",
        "From above, filled",
        "Close-up of the speckled glaze",
        "On the table, steaming"
    ];

    /// <summary>The five views of the demo mug in one glaze.</summary>
    public static IReadOnlyList<ProductGalleryImage> Mug(int glaze) =>
        Enumerable.Range(0, PhotosDemoArt.MugViews).Select(view => new ProductGalleryImage
        {
            Source = ImageSource.FromBitmap(PhotosDemoArt.Mug(view, glaze)),
            Description = $"{PhotosDemoArt.GlazeName(glaze)} glaze. {s_mugViews[view]}"
        }).ToArray();

    /// <summary>The mug in the oat glaze.</summary>
    public static IReadOnlyList<ProductGalleryImage> OatMug => s_oatMug ??= Mug(0);

    private static IReadOnlyList<ProductGalleryImage>? s_oatMug;

    private static IReadOnlyList<LightboxGalleryImage>? s_landscapes;
    private static IReadOnlyList<CoverFlowItem>? s_albumItems;
    private static IReadOnlyList<CoverFlowItem>? s_bookItems;

    private static readonly string[] s_captions =
    [
        "First light on the ridge, from the hut at 2,100 m",
        "Boats coming home as the harbour lights come on",
        "Pine valley after the rain",
        "Wind lines on the dunes at noon",
        "Clear night over the pass",
        "Wildflowers along the old cart road",
        "Fresh snow on the three sisters",
        "The lake before the wind gets up",
        "Late sun in the red canyon",
        "Green lights over the fjord",
        "Shallow water at the lagoon",
        "Morning fog in the low hills"
    ];

    private static readonly (int Width, int Height)[] s_shapes =
    [
        (900, 600), (600, 800), (900, 600), (1000, 560), (700, 700), (900, 600),
        (600, 900), (900, 600), (800, 600), (1000, 500), (900, 675), (640, 800)
    ];

    /// <summary>The twelve landscapes for the lightbox, with captions and their proportions.</summary>
    public static IReadOnlyList<LightboxGalleryImage> Landscapes => s_landscapes ??=
        Enumerable.Range(0, PhotosDemoArt.SceneCount).Select(i =>
        {
            var (w, h) = s_shapes[i];
            return new LightboxGalleryImage
            {
                Source = ImageSource.FromBitmap(PhotosDemoArt.Scene(i, w, h)),
                Caption = s_captions[i],
                AspectRatio = (double)w / h
            };
        }).ToArray();

    /// <summary>One photo for the grid demos: landscape <paramref name="index"/> in one of the shapes.</summary>
    public static PhotoGridPhoto GridPhoto(int index)
    {
        var scene = index % PhotosDemoArt.SceneCount;
        var (w, h) = s_shapes[(index * 5 + 3) % s_shapes.Length];
        return new PhotoGridPhoto
        {
            Source = ImageSource.FromBitmap(PhotosDemoArt.Scene(scene, w / 2, h / 2)),
            AspectRatio = (double)w / h,
            Description = s_captions[scene]
        };
    }

    /// <summary>Eighteen photos of mixed shapes.</summary>
    public static IReadOnlyList<PhotoGridPhoto> GridPhotos => s_gridPhotos ??= Enumerable.Range(0, 18).Select(GridPhoto).ToArray();

    private static IReadOnlyList<PhotoGridPhoto>? s_gridPhotos;

    /// <summary>A collection the reflow demo adds to and takes from.</summary>
    public static ObservableCollection<PhotoGridPhoto> NewAlbum() => new(Enumerable.Range(0, 10).Select(GridPhoto));

    private static readonly (string Title, string Artist)[] s_albums =
    [
        ("Paper Lanterns", "Mira Vale"),
        ("Copper Hours", "The Lindens"),
        ("North of Nowhere", "Oskar Fen"),
        ("Static Bloom", "Juno & the Tidewater"),
        ("Glass Garden", "Ayla Brook"),
        ("Slow Orbit", "Kites Over Harlow"),
        ("Velvet Signal", "Nadia Quill"),
        ("Salt and Ember", "The Moss Choir"),
        ("Quiet Machines", "Rook Avenue"),
        ("Night Ferry", "Ilse Marrow"),
        ("Field Notes", "Tomas Brae"),
        ("Second Summer", "Pale Harbour")
    ];

    /// <summary>Twelve albums with invented titles and artists.</summary>
    public static IReadOnlyList<CoverFlowItem> Albums => s_albumItems ??=
        s_albums.Select((a, i) => new CoverFlowItem
        {
            Source = ImageSource.FromBitmap(PhotosDemoArt.Album(i, a.Title)),
            Title = a.Title,
            Subtitle = a.Artist
        }).ToArray();

    private static readonly (string Title, string Author)[] s_books =
    [
        ("The Lighthouse Keeper's Almanac", "R. Okonkwo"),
        ("Small Rivers", "Helena Strand"),
        ("A Field Guide to Clouds", "Ivo Marsh"),
        ("The Cartographer's Daughter", "Lin Ashby"),
        ("Bread and Weather", "Pia Kettering"),
        ("Winter Orchard", "Samir Doyle"),
        ("Notes from the Night Train", "Elsa Wren"),
        ("How Bridges Stand", "D. Halloran")
    ];

    /// <summary>Eight books with invented titles and authors.</summary>
    public static IReadOnlyList<CoverFlowItem> Books => s_bookItems ??=
        s_books.Select((b, i) => new CoverFlowItem
        {
            Source = ImageSource.FromBitmap(PhotosDemoArt.Book(i + 3, b.Title)),
            Title = b.Title,
            Subtitle = b.Author
        }).ToArray();
}
