using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Docs;

/// <summary>
/// A file of a demo as its Code tab shows it. Its text is not part of the gallery assembly: it is read from
/// <see cref="GalleryTexts"/> when a Code tab or "Copy page" needs it (every demo's source was several megabytes that a
/// visitor who looks at a few examples never read).
/// </summary>
/// <param name="Path">Relative to the gallery project, such as <c>Demos/Buttons/Button/Variants.axaml</c>.</param>
/// <param name="Language">The highlighting language: <c>axaml</c> or <c>cs</c>.</param>
public sealed record GalleryDemoFile(string Path, string Language)
{
    /// <summary>
    /// Where <see cref="GalleryTexts"/> has the file: its path plus <c>.txt</c>, so the build does not compile it as XAML
    /// or C# a second time and a server sends it as text.
    /// </summary>
    public string ContentPath => Path + ".txt";

    /// <summary>
    /// The file's text for callers that cannot wait (tests, tools); empty when it cannot be read. It fails when
    /// <see cref="GalleryTexts.Source"/> fetches the text: a view uses <see cref="ReadAsync"/>.
    /// </summary>
    public string Source => Normalize(GalleryTexts.Read(ContentPath)) ?? string.Empty;

    /// <summary>
    /// The file's text, or null when it cannot be read: at once from the gallery's resources, later when the WebAssembly
    /// host fetches it.
    /// </summary>
    public ValueTask<string?> ReadAsync()
    {
        var pending = GalleryTexts.Source.ReadAsync(ContentPath);
        return pending.IsCompletedSuccessfully ? new ValueTask<string?>(Normalize(pending.Result)) : Await(pending);

        static async ValueTask<string?> Await(ValueTask<string?> pending) => Normalize(await pending);
    }

    // As the file is written, with LF line endings and no blank lines after it.
    private static string? Normalize(string? text) => text?.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
}

/// <summary>
/// A live example: <c>Demos/&lt;Category&gt;/&lt;Component&gt;/&lt;Name&gt;.axaml</c> and its code-behind, if it has one.
/// </summary>
/// <param name="Create">Creates the demo's view (no reflection: the generator calls its constructor).</param>
/// <param name="Files">The .axaml file, then the code-behind.</param>
public sealed record GalleryDemo(string Component, string Name, Func<Control> Create, GalleryDemoFile[] Files)
{
    /// <summary><c>Component.Name</c>, the name a page gives in <c>&lt;Demo name="..." /&gt;</c>.</summary>
    public string Id => $"{Component}.{Name}";
}

/// <summary>The demos the generator collected from the gallery's <c>Demos/</c> folder.</summary>
public static class GalleryDemos
{
    private static readonly Dictionary<string, GalleryDemo> s_demos =
        GalleryDemoData.Demos.ToDictionary(d => d.Id, StringComparer.Ordinal);

    private static readonly HashSet<string> s_components =
        GalleryDemoData.Demos.Select(d => d.Component).ToHashSet(StringComparer.Ordinal);

    /// <summary>Every demo, ordered by id.</summary>
    public static IReadOnlyList<GalleryDemo> All => GalleryDemoData.Demos;

    /// <summary>Whether <paramref name="component"/> has at least one demo.</summary>
    public static bool Has(string component) => s_components.Contains(component);

    /// <summary>The demo <c>Component.Name</c>, or null.</summary>
    public static GalleryDemo? Get(string id) => s_demos.GetValueOrDefault(id);
}
