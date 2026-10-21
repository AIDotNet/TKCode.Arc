using System.Runtime.InteropServices.JavaScript;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>Keeps the GitHub star count in local storage for an hour (the <c>arc-stars</c> module of main.js).</summary>
internal sealed partial class BrowserStarCache : IGalleryStarCache
{
    public int? Read() => Load() is var stars && stars >= 0 ? stars : null;

    public void Write(int stars) => Save(stars);

    [JSImport("read", "arc-stars")]
    private static partial int Load();

    [JSImport("write", "arc-stars")]
    private static partial void Save(int stars);
}
