using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RichTextEditorImagesAndTablesDemo : UserControl
{
    private const string Picture = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAPAAAABgCAMAAAAU7Q/jAAAAMFBMVEVabuZidN9qethygNJ6hsuCjMSKkr6SmLeanrCipKqqqqOysJy6tpbCvI/KwojSyILM3E3EAAAB7UlEQVR42uXZW27CQBBE0Ul4BGiD97/bBIHkqaHGCgIBvl07OH991aXYfbl9263c1nYbt63bj93ObW93cIu/ZfNGNm+8zLv5DG885F0tzxs872HWG//igryxCO/ued54m3f7Hm9k88YzveuP9Eb0wCm88Tnn1Wu8sUTv/gFvZDifHTiNN7J5g59HuoHgPdzhHfh5pN4hQS6IdyjJvB4M9lowK4/U68BLP59nvQaMywXx3oJ5uSDeFvxN9zZg5vlcexWcwCtgaC4MPXAKbwXGfI9mvRMYm0cdMPl8duA03is4j/cCRueR7lgA3rjDewbD80i9x8LPBfF2wFyvB7NyQbwWTPY68NLP51mvAeNyQbw3YGAuiLcF470NmHk+114FJ/AKGJoLxx44hbcC5/BOYGwedcDgXLDgNN4rGJ0LBpzIeyr8XBDvGZzKeyr8XBBvB8z1ejArF8RrwWSvAy/9fJ71GjAuF8R7AwbmgnhbMN7bgJG5IF4FJ/AKGJoLpx44hbcCY75Hs94JjM2jDhicCxacxnsF5/FewOg80o2FnwviPYNTecfCzwXxdsBcrwezckG8Fkz2OjDufK69Bsz23oCBuSDeFoz3NmDm+Vx7FZzAK2BoLow9cApvBcZ8j2a9ExibRx0wOBcsOI13/AXt4J7cAlwLVwAAAABJRU5ErkJggg==";

    public RichTextEditorImagesAndTablesDemo()
    {
        InitializeComponent();
        Editor.Html = $"<h2>Quarterly report</h2><p>Images and tables live in their own blocks.</p><img src=\"{Picture}\" alt=\"A gradient\"><table><colgroup><col width=\"200\"><col width=\"100\"></colgroup><tr><th>Region</th><th data-align=\"end\">Revenue</th></tr><tr><td>North</td><td data-align=\"end\">1,240</td></tr><tr><td>South</td><td data-align=\"end\">980</td></tr><tr><td colspan=\"2\" data-align=\"center\" data-bg=\"gray\">Figures in thousands of euros</td></tr></table><p>Right-click a cell to add or delete rows and columns, color cells or toggle the header row and column; drag across cells to select, copy or merge them, drag a column border to resize it, or drag the grips at the top and left of the table to move columns and rows.</p>";
    }

    // The host decides where a picked, pasted or dropped picture is kept. Here it is "uploaded" to a made-up store;
    // a real app would send the bytes (ReadAllBytesAsync or OpenReadAsync) to its server and return the final address.
    private void OnImageSourceRequested(object? sender, RichTextImageSourceRequestedEventArgs e)
    {
        Log.Text = $"{e.Origin}: {e.FileName ?? "pasted image"} ({e.ContentType}) is stored by the host.";
        e.SourceTask = StoreAsync(e);
    }

    private static async Task<string?> StoreAsync(RichTextImageSourceRequestedEventArgs e)
    {
        var bytes = await e.ReadAllBytesAsync();
        return "data:" + e.ContentType + ";base64," + Convert.ToBase64String(bytes);
    }
}
