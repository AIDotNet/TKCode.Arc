// Ported from vscode (src/vs/editor/test/common/modes/supports/autoClosingPairsRules.ts, commit b57d5c9e), MIT license.

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Tests;

internal static class AutoClosingPairsRules
{
    public static readonly IReadOnlyList<IAutoClosingPairConditional> JavascriptAutoClosingPairsRules =
    [
        new AutoClosingPairConditional("{", "}"),
        new AutoClosingPairConditional("[", "]"),
        new AutoClosingPairConditional("(", ")"),
        new AutoClosingPairConditional("'", "'") { NotIn = ["string", "comment"] },
        new AutoClosingPairConditional("\"", "\"") { NotIn = ["string"] },
        new AutoClosingPairConditional("`", "`") { NotIn = ["string", "comment"] },
        new AutoClosingPairConditional("/**", " */") { NotIn = ["string"] },
    ];

    public static readonly IReadOnlyList<IAutoClosingPair> LatexAutoClosingPairsRules =
    [
        new AutoClosingPair("\\left(", "\\right)"),
        new AutoClosingPair("\\left[", "\\right]"),
        new AutoClosingPair("\\left\\{", "\\right\\}"),
        new AutoClosingPair("\\bigl(", "\\bigr)"),
        new AutoClosingPair("\\bigl[", "\\bigr]"),
        new AutoClosingPair("\\bigl\\{", "\\bigr\\}"),
        new AutoClosingPair("\\Bigl(", "\\Bigr)"),
        new AutoClosingPair("\\Bigl[", "\\Bigr]"),
        new AutoClosingPair("\\Bigl\\{", "\\Bigr\\}"),
        new AutoClosingPair("\\biggl(", "\\biggr)"),
        new AutoClosingPair("\\biggl[", "\\biggr]"),
        new AutoClosingPair("\\biggl\\{", "\\biggr\\}"),
        new AutoClosingPair("\\Biggl(", "\\Biggr)"),
        new AutoClosingPair("\\Biggl[", "\\Biggr]"),
        new AutoClosingPair("\\Biggl\\{", "\\Biggr\\}"),
        new AutoClosingPair("\\(", "\\)"),
        new AutoClosingPair("\\[", "\\]"),
        new AutoClosingPair("\\{", "\\}"),
        new AutoClosingPair("{", "}"),
        new AutoClosingPair("[", "]"),
        new AutoClosingPair("(", ")"),
        new AutoClosingPair("`", "'"),
    ];
}
