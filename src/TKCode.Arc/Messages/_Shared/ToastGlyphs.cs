using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Glyphs and stroke widths taken from Arc's toast sources.</summary>
internal static class ToastGlyphs
{
    /// <summary>Toast's own check mark (not Lucide's): <c>M4 12.5l5 5L20 6.5</c>, drawn at stroke 2.25.</summary>
    public static Geometry Check { get; } = ArcIcons.Parse("M4 12.5l5 5L20 6.5");

    /// <summary>Toast stack icons are 18px Lucide glyphs at stroke 1.75; loading shows a ring instead.</summary>
    public static Geometry? For(ToastKind kind) => kind switch
    {
        ToastKind.Success => MessagesIcons.CircleCheck,
        ToastKind.Info => ArcIcons.Info,
        ToastKind.Warning => ArcIcons.TriangleAlert,
        ToastKind.Error => MessagesIcons.CircleX,
        _ => null
    };

    /// <summary>The visually hidden prefix read before each toast.</summary>
    public static string LabelFor(ToastKind kind) => kind switch
    {
        ToastKind.Success => "Success",
        ToastKind.Info => "Info",
        ToastKind.Warning => "Warning",
        ToastKind.Error => "Error",
        _ => "In progress"
    };
}
