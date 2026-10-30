using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The Lucide glyph the agent status needs that Core <see cref="ArcIcons"/> does not ship (24×24, stroked). Path data is
/// lucide-static 1.59.0 as converted by <c>tools/lucide/generate_lucide.py</c> (the same strings as
/// <c>TKCode.Arc.Icons.Lucide</c>, which a control package must not depend on), parsed once through
/// <see cref="ArcIcons.Parse"/>. The check and cross drawn inside the orb are our own short strokes (see
/// <see cref="AgentStatusFace"/>), not icons.
/// </summary>
internal static class AgentStatusIcons
{
    // Lucide bot, ISC.
    private const string BotData =
        "M12 8V4H8 M6 8h12a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-8a2 2 0 0 1 2 -2z M2 14h2 M20 14h2 M15 13v2 M9 13v2";

    /// <summary>A robot head: the default agent mark.</summary>
    public static Geometry Bot => ArcIcons.Parse(BotData);
}
