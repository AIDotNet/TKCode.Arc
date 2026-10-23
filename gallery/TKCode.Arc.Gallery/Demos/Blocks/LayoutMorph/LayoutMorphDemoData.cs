using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The LayoutMorph demos' collection: the "Harbor Lane spring edit" of 14 desk pieces by 10 designers (all invented),
/// in the order covers, designers and scenes interleave, with pictures drawn in code from a seed (abstract portraits and
/// object scenes, never photos). Everything is deterministic: the same key always gives the same aspect ratio and picture.
/// </summary>
public static class LayoutMorphDemoData
{
    /// <summary>The collection's name in the heading and the panel.</summary>
    public const string CollectionName = "Harbor Lane spring edit";

    public const string ShortName = "Spring edit";

    public const string Title = "One shelf, six ways to look";

    public const string Lede = "The Harbor Lane spring edit: 14 desk pieces from 10 designers. Choose a view and every card glides to its new spot.";

    private sealed record Designer(string Key, string Name, string Role, string City, int Pieces, int Orders, double Rating, int Since, string About);

    private sealed record Piece(string Key, string Name, int Price, string Designer, string Material, string Size, int Stock,
        string Ships, double Rating, bool IsScene, string About);

    private static readonly Designer[] s_designers =
    [
        new("priya-anand", "Priya Anand", "Product designer", "Pune", 1, 1240, 4.8, 2017,
            "Designs desk accessories that fold flat and ship without foam."),
        new("tomasz-wrona", "Tomasz Wrona", "Joiner", "Gdansk", 2, 2960, 4.9, 2012,
            "Builds furniture from birch and maple in a two-person workshop by the harbour."),
        new("leila-haddad", "Leila Haddad", "Textile artist", "Tunis", 1, 860, 4.7, 2019,
            "Felts and weaves wool from small farms into covers, sleeves and soft cases."),
        new("noah-fischer", "Noah Fischer", "Lighting designer", "Basel", 1, 540, 4.6, 2021,
            "Makes lamps that light the page and leave the rest of the room dim."),
        new("yuki-aoyama", "Yuki Aoyama", "Metalsmith", "Osaka", 2, 1780, 4.9, 2014,
            "Folds, files and patinates steel and brass into small things for the desk."),
        new("gabriel-costa", "Gabriel Costa", "Leatherworker", "Porto", 1, 1120, 4.7, 2016,
            "Cuts and stitches vegetable-tanned leather by hand, one hide at a time."),
        new("ingrid-solberg", "Ingrid Solberg", "Printmaker", "Bergen", 1, 690, 4.8, 2020,
            "Prints bands of northern light in small, signed editions."),
        new("kwame-boateng", "Kwame Boateng", "Furniture designer", "Accra", 2, 2310, 4.8, 2013,
            "Designs chairs and stands that bend and fold instead of being screwed together."),
        new("camille-laurent", "Camille Laurent", "Glass artist", "Lyon", 2, 3060, 4.9, 2015,
            "Blows small batches of glass in soft greens and smoke for the kitchen and the desk."),
        new("rohan-mehta", "Rohan Mehta", "Bookbinder", "Jaipur", 1, 1450, 4.5, 2018,
            "Sews and binds notebooks with cloth covers that open flat on any page.")
    ];

    // Covers (no picture) first, then the five scenes.
    private static readonly Piece[] s_pieces =
    [
        new("slate-desk-pad", "Slate & leather desk pad", 64, "Gabriel Costa", "Vegetable-tanned leather", "60 × 35 cm", 14, "2 days", 4.7, false,
            "A wide leather pad over a slate felt back. It warms with use and keeps the desk quiet."),
        new("cork-riser", "Cork monitor riser", 88, "Priya Anand", "Pressed cork", "54 × 22 cm", 23, "3 days", 4.6, false,
            "Lifts a screen to eye level and folds flat for the bag. The cork grips without feet."),
        new("ink-pen-tray", "Ink pen tray", 36, "Yuki Aoyama", "Blackened steel", "24 × 8 cm", 31, "2 days", 4.8, false,
            "A shallow tray for pens and a clip or two, folded from one sheet and blackened by hand."),
        new("cloth-notebook", "Cloth-bound notebook", 26, "Rohan Mehta", "Linen cloth, dot grid", "A5, 192 pages", 58, "1 day", 4.5, false,
            "Sewn signatures lie flat on the desk; linen over board outside, cream dot grid inside."),
        new("felt-sleeve", "Felt laptop sleeve", 72, "Leila Haddad", "Wool felt", "Fits 14 in", 19, "3 days", 4.7, false,
            "Thick wool felt with a leather tab, sized for a fourteen-inch laptop and its charger."),
        new("maple-card-holder", "Maple card holder", 28, "Tomasz Wrona", "Hard maple", "9 × 6 cm", 42, "2 days", 4.6, false,
            "A small block of oiled maple that keeps forty cards upright."),
        new("brass-page-weight", "Brass page weight", 54, "Yuki Aoyama", "Solid brass", "16 cm", 12, "4 days", 4.9, false,
            "A heavy brass bar that keeps a book open at the page and darkens with handling."),
        new("sage-planter", "Sage glass planter", 46, "Camille Laurent", "Mouth-blown glass", "12 cm tall", 9, "5 days", 4.8, false,
            "A small pot of sage-green glass for a desk plant; every one comes out a little different."),
        new("walnut-book-stand", "Walnut book stand", 98, "Kwame Boateng", "Black walnut", "30 × 22 cm", 7, "4 days", 4.7, false,
            "Holds a cookbook or a sketchbook open at a gentle angle and folds flat behind the screen."),
        new("halo-floor-lamp", "Halo floor lamp", 320, "Noah Fischer", "Powder-coated steel", "150 cm tall", 6, "6 days", 4.6, true,
            "A thin steel stem and a shallow shade that throws a soft ring of light over a reading chair."),
        new("bentwood-chair", "Bentwood task chair", 540, "Kwame Boateng", "Steam-bent ash", "46 cm seat", 4, "10 days", 4.8, true,
            "A light, steam-bent chair whose curved back follows the shoulders through a long day."),
        new("pour-over-set", "Glass pour-over set", 96, "Camille Laurent", "Borosilicate glass", "600 ml", 15, "3 days", 4.7, true,
            "A cone, a carafe and two small cups in heat-proof glass for a slow cup between meetings."),
        new("birch-standing-desk", "Birch standing desk", 1180, "Tomasz Wrona", "Birch plywood", "140 × 70 cm", 3, "14 days", 4.6, true,
            "A birch top on two quiet motors that rise from sitting to standing in twelve seconds."),
        new("dawn-print", "Dawn print 40 × 50", 84, "Ingrid Solberg", "Archival pigment print", "40 × 50 cm", 26, "2 days", 4.8, true,
            "Three bands of early colour over a pale sun, printed on cotton rag and signed on the back.")
    ];

    // t = cover piece, m = designer, p = scene piece (structure only).
    private const string Order = "tmpmtmtpmtmpmtmtpmtmtpmt";

    private static Dictionary<string, IImage>? s_images;

    /// <summary>All 24 cards: 14 pieces and 10 designers, interleaved.</summary>
    public static List<LayoutMorphItem> CreateItems()
    {
        var covers = s_pieces.Where(p => !p.IsScene).GetEnumerator();
        var scenes = s_pieces.Where(p => p.IsScene).GetEnumerator();
        var designers = ((IEnumerable<Designer>)s_designers).GetEnumerator();
        var items = new List<LayoutMorphItem>(24);
        foreach (var kind in Order)
        {
            switch (kind)
            {
                case 't':
                    covers.MoveNext();
                    items.Add(CreatePiece(covers.Current));
                    break;
                case 'p':
                    scenes.MoveNext();
                    items.Add(CreatePiece(scenes.Current));
                    break;
                default:
                    designers.MoveNext();
                    items.Add(CreateDesigner(designers.Current));
                    break;
            }
        }
        return items;
    }

    /// <summary>The ten designers only (a team page).</summary>
    public static List<LayoutMorphItem> CreateDesigners() => s_designers.Select(CreateDesigner).ToList();

    /// <summary>The fourteen pieces only (a catalogue).</summary>
    public static List<LayoutMorphItem> CreatePieces() => s_pieces.Select(CreatePiece).ToList();

    /// <summary>Products / Designers / Avg rating for the panel while no card is described.</summary>
    public static List<LayoutMorphFact> CreateTotals(IReadOnlyList<LayoutMorphItem> items)
    {
        var pieces = items.Count(i => i.Tag is Piece);
        var designers = items.Count(i => i.Tag is Designer);
        var ratings = items.Select(i => i.Tag switch
        {
            Piece p => p.Rating,
            Designer d => d.Rating,
            _ => double.NaN
        }).Where(r => !double.IsNaN(r)).ToList();
        var average = ratings.Count == 0 ? 0 : ratings.Average();
        return
        [
            new LayoutMorphFact("Products", pieces.ToString(CultureInfo.InvariantCulture)),
            new LayoutMorphFact("Designers", designers.ToString(CultureInfo.InvariantCulture)),
            new LayoutMorphFact("Avg rating", average.ToString("0.0", CultureInfo.InvariantCulture))
        ];
    }

    private static LayoutMorphItem CreatePiece(Piece piece)
    {
        var item = new LayoutMorphItem
        {
            Key = piece.Key,
            Title = piece.Name,
            Subtitle = "By " + piece.Designer,
            Meta = piece.Designer + " · " + piece.Material,
            Value = Money(piece.Price),
            Description = piece.About,
            Image = piece.IsScene ? Image(piece.Key) : null,
            AspectRatio = Aspect(piece.Key, piece.IsScene ? 0.80 : 0.75, piece.IsScene ? 1.50 : 1.40),
            OpenVariant = piece.IsScene ? LayoutMorphOpenVariant.Hero : LayoutMorphOpenVariant.Profile,
            ActionText = "Add to bag",
            ActiveActionText = "Added to bag",
            Tag = piece
        };
        item.Facts.Add(new LayoutMorphFact("Price", Money(piece.Price)));
        item.Facts.Add(new LayoutMorphFact("Size", piece.Size));
        item.Facts.Add(new LayoutMorphFact("In stock", piece.Stock.ToString(CultureInfo.InvariantCulture)));
        item.Facts.Add(new LayoutMorphFact("Ships in", piece.Ships));
        item.Stats.Add(new LayoutMorphFact("Price", Money(piece.Price)));
        item.Stats.Add(new LayoutMorphFact("In stock", piece.Stock.ToString(CultureInfo.InvariantCulture)));
        item.Stats.Add(new LayoutMorphFact("Rating", piece.Rating.ToString("0.0", CultureInfo.InvariantCulture)));
        return item;
    }

    private static LayoutMorphItem CreateDesigner(Designer designer)
    {
        var pieces = designer.Pieces == 1 ? "1 piece" : designer.Pieces + " pieces";
        var item = new LayoutMorphItem
        {
            Key = designer.Key,
            Title = designer.Name,
            Subtitle = designer.Role + ", " + designer.City,
            Meta = designer.Role + " in " + designer.City,
            Value = pieces,
            Description = designer.About,
            Image = Image(designer.Key),
            AspectRatio = Aspect(designer.Key, 1.00, 1.50),
            OpenVariant = LayoutMorphOpenVariant.Profile,
            ActionText = "Follow",
            ActiveActionText = "Following",
            AutomationName = designer.Name + ", " + designer.Role + " in " + designer.City,
            Tag = designer
        };
        var orders = designer.Orders.ToString("#,0", CultureInfo.InvariantCulture);
        var rating = designer.Rating.ToString("0.0", CultureInfo.InvariantCulture);
        item.Facts.Add(new LayoutMorphFact("Pieces here", designer.Pieces.ToString(CultureInfo.InvariantCulture)));
        item.Facts.Add(new LayoutMorphFact("Orders", orders));
        item.Facts.Add(new LayoutMorphFact("Rating", rating));
        item.Facts.Add(new LayoutMorphFact("Since", designer.Since.ToString(CultureInfo.InvariantCulture)));
        item.Stats.Add(new LayoutMorphFact("Pieces", designer.Pieces.ToString(CultureInfo.InvariantCulture)));
        item.Stats.Add(new LayoutMorphFact("Orders", orders));
        item.Stats.Add(new LayoutMorphFact("Rating", rating));
        return item;
    }

    private static string Money(int price) => "$" + price.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>A seeded masonry ratio in [min, max], in steps of 0.05.</summary>
    public static double Aspect(string key, double min, double max)
    {
        var random = new Mulberry32(Fnv1A(key));
        var value = min + random.Next() * (max - min);
        return Math.Round(value / 0.05) * 0.05;
    }

    // Pictures ----------------------------------------------------------------------------------------------------------

    private static IImage? Image(string key)
    {
        s_images ??= new Dictionary<string, IImage>();
        if (s_images.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var image = key switch
        {
            "halo-floor-lamp" => DrawScene(key, SceneSubject.Lamp),
            "bentwood-chair" => DrawScene(key, SceneSubject.Chair),
            "pour-over-set" => DrawScene(key, SceneSubject.PourOver),
            "birch-standing-desk" => DrawScene(key, SceneSubject.Desk),
            "dawn-print" => DrawScene(key, SceneSubject.Print),
            _ => DrawPortrait(key)
        };
        if (image is not null)
        {
            s_images[key] = image;
        }
        return image;
    }

    private const int PictureWidth = 640;
    private const int PictureHeight = 800;

    // Hue pairs (OKLCH degrees): sand, dusty blue, sage, clay, lilac, teal, ochre, rose, indigo, moss.
    private static readonly (double From, double To)[] s_huePairs =
    [
        (32, 48), (210, 225), (150, 165), (18, 30), (285, 300), (190, 200), (60, 75), (340, 350), (235, 250), (110, 125)
    ];

    private enum SceneSubject
    {
        Lamp,
        Chair,
        PourOver,
        Desk,
        Print
    }

    private static Bitmap? s_grain;

    /// <summary>An abstract portrait: a gradient ground, shoulders in a complementary hue, a neutral head, a dark cap of hair.</summary>
    private static IImage? DrawPortrait(string key)
    {
        var random = new Mulberry32(Fnv1A(key));
        var (h1, h2) = s_huePairs[(int)(random.Next() * s_huePairs.Length) % s_huePairs.Length];
        var chroma = 0.05 + random.Next() * 0.04;
        var angle = (random.Next() * 30 - 15) * Math.PI / 180;
        var bitmap = new RenderTargetBitmap(new PixelSize(PictureWidth, PictureHeight), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            const double w = PictureWidth;
            const double h = PictureHeight;
            var dx = Math.Sin(angle) * 0.5;
            var ground = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5 - dx, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5 + dx, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.ToColor(0.78, chroma, h1), 0),
                    new GradientStop(Oklch.ToColor(0.62, chroma, h2), 1)
                }
            };
            context.DrawRectangle(ground, null, new Rect(0, 0, w, h));

            // Shoulders: a rounded trapezoid over the bottom 38%, in the complementary hue.
            var shoulderHue = (h1 + 180 + random.Next() * 20) % 360;
            var shoulderColor = Oklch.ToColor(0.45 + random.Next() * 0.1, 0.06 + random.Next() * 0.04, shoulderHue);
            var top = h * 0.62 + random.Next() * 20;
            var shoulders = new StreamGeometry();
            using (var g = shoulders.Open())
            {
                var inset = w * (0.17 + random.Next() * 0.05);
                g.BeginFigure(new Point(-20, h + 10), true);
                g.LineTo(new Point(inset - 40, top + 70));
                g.QuadraticBezierTo(new Point(inset - 10, top), new Point(inset + 70, top - 4));
                g.LineTo(new Point(w - inset - 70, top - 4));
                g.QuadraticBezierTo(new Point(w - inset + 10, top), new Point(w - inset + 40, top + 70));
                g.LineTo(new Point(w + 20, h + 10));
                g.EndFigure(true);
            }
            context.DrawGeometry(new SolidColorBrush(shoulderColor), null, shoulders);

            // Neck.
            var neckColor = Oklch.ToColor(0.64, 0.02, 60);
            context.DrawRectangle(new SolidColorBrush(neckColor), null, new RoundedRect(new Rect(w * 0.43, h * 0.45, w * 0.14, top - h * 0.45 + 30), 30));

            // Head: a neutral warm-grey disc with a soft radial shade, 34% of the width, at (50%, 38%).
            var radius = w * 0.17;
            var center = new Point(w * (0.5 + (random.Next() - 0.5) * 0.04), h * 0.38);
            var head = new RadialGradientBrush
            {
                Center = new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.ToColor(0.74, 0.02, 60), 0),
                    new GradientStop(Oklch.ToColor(0.62, 0.03, 50), 1)
                }
            };
            // Jaw shadow under the head.
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)), null, new Point(center.X, center.Y + radius * 0.95), radius * 0.62, radius * 0.22);
            context.DrawEllipse(head, null, center, radius, radius * 1.08);

            // Hair: a dark cap over the top of the head, ±38° plus a little.
            var hairColor = Oklch.ToColor(0.22 + random.Next() * 0.08, 0.02, h2);
            var cap = new StreamGeometry();
            using (var g = cap.Open())
            {
                var spread = (38 + random.Next() * 30) * Math.PI / 180;
                var outer = radius * 1.14;
                var start = new Point(center.X - Math.Sin(spread + 0.6) * outer, center.Y - Math.Cos(spread + 0.6) * outer * 1.05);
                var end = new Point(center.X + Math.Sin(spread + 0.6) * outer, center.Y - Math.Cos(spread + 0.6) * outer * 1.05);
                g.BeginFigure(start, true);
                g.ArcTo(end, new Size(outer, outer * 1.08), 0, true, SweepDirection.Clockwise);
                g.QuadraticBezierTo(new Point(center.X, center.Y - radius * 0.55), start);
                g.EndFigure(true);
            }
            context.DrawGeometry(new SolidColorBrush(hairColor), null, cap);

            // A thin rim light on the lit side.
            var rim = new StreamGeometry();
            using (var g = rim.Open())
            {
                g.BeginFigure(new Point(center.X - radius * 0.98, center.Y + radius * 0.15), false);
                g.ArcTo(new Point(center.X - radius * 0.35, center.Y - radius * 1.0), new Size(radius, radius * 1.08), 0, false, SweepDirection.Clockwise);
                g.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 2), rim);

            DrawGrain(context, w, h);
        }
        return bitmap;
    }

    /// <summary>A scene: a sky gradient, a soft light, a dark table band, one object in silhouette, grain and a vignette.</summary>
    private static IImage? DrawScene(string key, SceneSubject subject)
    {
        var random = new Mulberry32(Fnv1A(key));
        var (h1, h2) = s_huePairs[(int)(random.Next() * s_huePairs.Length) % s_huePairs.Length];
        var chroma = 0.05 + random.Next() * 0.04;
        var bitmap = new RenderTargetBitmap(new PixelSize(PictureWidth, PictureHeight), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            const double w = PictureWidth;
            const double h = PictureHeight;
            context.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.ToColor(0.80, chroma * 0.8, h1), 0),
                    new GradientStop(Oklch.ToColor(0.64, chroma, h2), 1)
                }
            }, null, new Rect(0, 0, w, h));

            // The main light: a large soft disc.
            var light = new Point(w * (0.25 + random.Next() * 0.5), h * (0.18 + random.Next() * 0.2));
            context.DrawEllipse(new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(230, 255, 250, 238), 0),
                    new GradientStop(Color.FromArgb(0, 255, 250, 238), 1)
                }
            }, null, light, w * 0.55, w * 0.55);

            // The table: a dark band over the bottom 28%.
            var tableTop = h * 0.72;
            var table = Oklch.ToColor(0.22, 0.02, h2);
            context.DrawRectangle(new SolidColorBrush(table), null, new Rect(0, tableTop, w, h - tableTop));
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), null, new Rect(0, tableTop, w, 3));

            var ink = new SolidColorBrush(Oklch.ToColor(0.26, 0.015, h2));
            var paper = new SolidColorBrush(Oklch.ToColor(0.95, 0.01, h1));
            switch (subject)
            {
                case SceneSubject.Lamp:
                    DrawLamp(context, w, tableTop, ink);
                    break;
                case SceneSubject.Chair:
                    DrawChair(context, w, tableTop, ink);
                    break;
                case SceneSubject.PourOver:
                    DrawPourOver(context, w, tableTop, ink);
                    break;
                case SceneSubject.Desk:
                    DrawDesk(context, w, tableTop, ink);
                    break;
                default:
                    DrawPrint(context, w, h, tableTop, paper, h1, h2, chroma);
                    break;
            }
            DrawGrain(context, w, h);

            // A 6% vignette.
            context.DrawRectangle(new RadialGradientBrush
            {
                RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55),
                    new GradientStop(Color.FromArgb(40, 0, 0, 0), 1)
                }
            }, null, new Rect(0, 0, w, h));
        }
        return bitmap;
    }

    private static void DrawLamp(DrawingContext context, double w, double floor, IBrush ink)
    {
        var x = w * 0.52;
        context.DrawRectangle(ink, null, new Rect(x - 5, floor - 470, 10, 470));
        context.DrawEllipse(ink, null, new Point(x, floor - 4), 70, 12);
        var shade = new StreamGeometry();
        using (var g = shade.Open())
        {
            g.BeginFigure(new Point(x - 120, floor - 470), true);
            g.ArcTo(new Point(x + 120, floor - 470), new Size(120, 95), 0, false, SweepDirection.Clockwise);
            g.EndFigure(true);
        }
        context.DrawGeometry(ink, null, shade);
        // The bright inside of the shade.
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 255, 244, 214)), null, new Point(x, floor - 470), 112, 14);
        context.DrawEllipse(new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(120, 255, 240, 200), 0),
                new GradientStop(Color.FromArgb(0, 255, 240, 200), 1)
            }
        }, null, new Point(x, floor - 380), 220, 200);
    }

    private static void DrawChair(DrawingContext context, double w, double floor, IBrush ink)
    {
        var x = w * 0.5;
        var seatY = floor - 190;
        var pen = new Pen(ink, 10, lineCap: PenLineCap.Round);
        context.DrawLine(pen, new Point(x - 110, seatY + 10), new Point(x - 130, floor + 40));
        context.DrawLine(pen, new Point(x + 110, seatY + 10), new Point(x + 130, floor + 40));
        context.DrawLine(pen, new Point(x - 70, seatY + 14), new Point(x - 80, floor + 70));
        context.DrawLine(pen, new Point(x + 70, seatY + 14), new Point(x + 80, floor + 70));
        context.DrawRectangle(ink, null, new RoundedRect(new Rect(x - 135, seatY - 16, 270, 36), 16));
        var back = new StreamGeometry();
        using (var g = back.Open())
        {
            g.BeginFigure(new Point(x - 125, seatY - 10), false);
            g.CubicBezierTo(new Point(x - 150, seatY - 230), new Point(x + 150, seatY - 230), new Point(x + 125, seatY - 10));
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(ink, 14, lineCap: PenLineCap.Round), back);
        context.DrawLine(new Pen(ink, 8, lineCap: PenLineCap.Round), new Point(x, seatY - 178), new Point(x, seatY - 14));
    }

    private static void DrawPourOver(DrawingContext context, double w, double floor, IBrush ink)
    {
        var glass = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
        var edge = new Pen(ink, 4);
        var x = w * 0.45;
        // Carafe.
        context.DrawRectangle(glass, edge, new RoundedRect(new Rect(x - 75, floor - 230, 150, 230), 28));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(170, 92, 58, 36)), null, new RoundedRect(new Rect(x - 71, floor - 110, 142, 108), 24));
        // Cone dripper.
        var cone = new StreamGeometry();
        using (var g = cone.Open())
        {
            g.BeginFigure(new Point(x - 105, floor - 360), true);
            g.LineTo(new Point(x + 105, floor - 360));
            g.LineTo(new Point(x + 22, floor - 236));
            g.LineTo(new Point(x - 22, floor - 236));
            g.EndFigure(true);
        }
        context.DrawGeometry(glass, edge, cone);
        // Two cups.
        context.DrawRectangle(glass, edge, new RoundedRect(new Rect(w * 0.70, floor - 86, 82, 86), 18));
        context.DrawRectangle(glass, edge, new RoundedRect(new Rect(w * 0.70 + 96, floor - 74, 70, 74), 16));
    }

    private static void DrawDesk(DrawingContext context, double w, double floor, IBrush ink)
    {
        var top = floor - 330;
        context.DrawRectangle(ink, null, new RoundedRect(new Rect(w * 0.14, top, w * 0.72, 30), 6));
        context.DrawRectangle(ink, null, new Rect(w * 0.24, top + 30, 26, floor - top - 30 + 40));
        context.DrawRectangle(ink, null, new Rect(w * 0.76 - 26, top + 30, 26, floor - top - 30 + 40));
        context.DrawRectangle(ink, null, new RoundedRect(new Rect(w * 0.18, floor + 34, w * 0.18, 16), 6));
        context.DrawRectangle(ink, null, new RoundedRect(new Rect(w * 0.64, floor + 34, w * 0.18, 16), 6));
        // A screen and a cup on the desk.
        context.DrawRectangle(ink, null, new RoundedRect(new Rect(w * 0.40, top - 170, w * 0.26, 150), 8));
        context.DrawRectangle(ink, null, new Rect(w * 0.52, top - 22, 12, 22));
        context.DrawRectangle(ink, null, new RoundedRect(new Rect(w * 0.22, top - 48, 38, 48), 8));
    }

    private static void DrawPrint(DrawingContext context, double w, double h, double floor, IBrush paper, double h1, double h2, double chroma)
    {
        var frame = new Rect(w * 0.16, h * 0.12, w * 0.68, floor - h * 0.12 - 40);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), null, frame.Translate(new Vector(8, 12)));
        context.DrawRectangle(paper, null, frame);
        var art = frame.Deflate(34);
        using (context.PushClip(art))
        {
            context.DrawRectangle(new SolidColorBrush(Oklch.ToColor(0.86, chroma, h1)), null, art);
            context.DrawEllipse(new SolidColorBrush(Oklch.ToColor(0.93, 0.06, 80)), null, new Point(art.Center.X, art.Y + art.Height * 0.42), art.Width * 0.2, art.Width * 0.2);
            for (var band = 0; band < 3; band++)
            {
                var y = art.Y + art.Height * (0.55 + band * 0.15);
                var wave = new StreamGeometry();
                using (var g = wave.Open())
                {
                    g.BeginFigure(new Point(art.X - 10, y), true);
                    for (var k = 0; k < 4; k++)
                    {
                        var x0 = art.X + art.Width * k / 4;
                        var x1 = art.X + art.Width * (k + 1) / 4;
                        g.QuadraticBezierTo(new Point((x0 + x1) / 2, y + (k % 2 == 0 ? -18 : 18)), new Point(x1 + (k == 3 ? 10 : 0), y));
                    }
                    g.LineTo(new Point(art.Right + 10, art.Bottom + 10));
                    g.LineTo(new Point(art.X - 10, art.Bottom + 10));
                    g.EndFigure(true);
                }
                context.DrawGeometry(new SolidColorBrush(Oklch.ToColor(0.72 - band * 0.12, chroma + 0.02, h2 + band * 8)), null, wave);
            }
        }
    }

    /// <summary>±3% luminance grain from a seeded 128px tile.</summary>
    private static void DrawGrain(DrawingContext context, double w, double h)
    {
        s_grain ??= CreateGrain();
        var brush = new ImageBrush(s_grain)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            DestinationRect = new RelativeRect(0, 0, 128, 128, RelativeUnit.Absolute)
        };
        context.DrawRectangle(brush, null, new Rect(0, 0, w, h));
    }

    private static Bitmap CreateGrain()
    {
        const int size = 128;
        var random = new Mulberry32(20261022);
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < size * size; i++)
        {
            var light = random.Next() < 0.5;
            var a = (byte)(random.Next() * 16);
            var v = light ? a : (byte)0;
            pixels[i * 4] = v;
            pixels[i * 4 + 1] = v;
            pixels[i * 4 + 2] = v;
            pixels[i * 4 + 3] = a;
        }
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
        {
            for (var row = 0; row < size; row++)
            {
                Marshal.Copy(pixels, row * size * 4, buffer.Address + row * buffer.RowBytes, size * 4);
            }
        }
        return bitmap;
    }

    // Seeded randomness --------------------------------------------------------------------------------------------------

    /// <summary>FNV-1a 32-bit hash of a key.</summary>
    public static uint Fnv1A(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }

    /// <summary>mulberry32: a tiny deterministic generator (same seed, same sequence on every platform).</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;

        public double Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return ((t ^ (t >> 14)) >> 0) / 4294967296.0;
            }
        }
    }
}
