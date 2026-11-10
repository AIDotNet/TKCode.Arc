using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BackToTopPreviewDemo : UserControl
{
    private static readonly string[] s_routes = ["GET /api/orders", "POST /api/cart", "GET /api/products", "PUT /api/profile", "GET /api/search"];

    public BackToTopPreviewDemo()
    {
        InitializeComponent();
        var entries = new List<string>();
        for (var i = 0; i < 80; i++)
        {
            var seconds = 12 * 3600 + 4 * 60 + i * 7;
            var status = i % 11 == 5 ? 500 : i % 7 == 3 ? 404 : 200;
            entries.Add($"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}  {s_routes[i % s_routes.Length],-18} {status}  {18 + i * 37 % 140} ms");
        }
        Entries.ItemsSource = entries;
        // Start part-way down the log so the button is already showing.
        Log.Loaded += (_, _) => Log.Offset = new Vector(0, 640);
        Top.ScrolledToTop += (_, _) => Status.Text = "Back at the top. Scroll down again to bring the button back.";
    }
}
