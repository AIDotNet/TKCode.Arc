// Ported from vscode (src/vs/base/common/strings.ts: GraphemeBreakType, commit b57d5c9e), MIT license.
// Differences from upstream: Regional_Indicator and Extended_Pictographic are RegionalIndicator and ExtendedPictographic.

namespace TKCode.Arc.Editor;

/// <summary>The Unicode Grapheme_Cluster_Break property of a code point (UAX #29).</summary>
internal enum GraphemeBreakType
{
    Other = 0,
    Prepend = 1,
    CR = 2,
    LF = 3,
    Control = 4,
    Extend = 5,
    RegionalIndicator = 6,
    SpacingMark = 7,
    L = 8,
    V = 9,
    T = 10,
    LV = 11,
    LVT = 12,
    ZWJ = 13,
    ExtendedPictographic = 14,
}
