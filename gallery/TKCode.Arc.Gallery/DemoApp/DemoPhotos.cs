namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The portraits of the demo's people, from the placeholder service the other Gallery demos use. Each name keeps its
/// own photo; without a network, or for a name that is not a teammate, the avatar shows initials.
/// </summary>
internal static class DemoPhotos
{
    private static readonly Dictionary<string, int> s_photos = new(StringComparer.Ordinal)
    {
        ["Emma Collins"] = 47, ["Marcus Johnson"] = 12, ["Ava Mitchell"] = 45, ["Jasmine Brooks"] = 32, ["Sofia Ramirez"] = 49,
        ["Ryan Sullivan"] = 60, ["Olivia Bennett"] = 44, ["Chloe Nguyen"] = 9, ["Hannah Walsh"] = 20, ["Daniel Kim"] = 15
    };

    public static ImageSource? For(string? name) =>
        name is not null && s_photos.TryGetValue(name, out var index) && ImageSource.TryParse($"https://i.pravatar.cc/160?img={index}", out var source)
            ? source
            : null;
}
