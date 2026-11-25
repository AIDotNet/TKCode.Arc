// Ported from vscode (src/vs/editor/test/common/model/editableTextModelAuto.test.ts, commit b57d5c9e), MIT license.
// The four generated cases are kept. Upstream's random generator (TestModel, generateEdits) only runs when its
// GENERATE_TESTS constant is set, which it is not; it searches for a failing case without end and is not a test, so it
// is left out.

using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.EditableTextModelTestUtils;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Upstream suite 'EditorModel Auto Tests'.</summary>
public sealed class EditorModelAutoTests
{
    private static SingleEditOperation EditOp(int startLineNumber, int startColumn, int endLineNumber, int endColumn, string[] text) =>
        new(new TextRange(startLineNumber, startColumn, endLineNumber, endColumn), string.Join('\n', text), false);

    [Fact]
    public void Auto1()
    {
        TestApplyEditsWithSyncedModels(
            [
                "ioe",
                "",
                "yjct",
                "",
                "",
            ],
            [
                EditOp(1, 2, 1, 2, ["b", "r", "fq"]),
                EditOp(1, 4, 2, 1, ["", ""]),
            ],
            [
                "ib",
                "r",
                "fqoe",
                "",
                "yjct",
                "",
                "",
            ]);
    }

    [Fact]
    public void Auto2()
    {
        TestApplyEditsWithSyncedModels(
            [
                "f",
                "littnhskrq",
                "utxvsizqnk",
                "lslqz",
                "jxn",
                "gmm",
            ],
            [
                EditOp(1, 2, 1, 2, ["", "o"]),
                EditOp(2, 4, 2, 4, ["zaq", "avb"]),
                EditOp(2, 5, 6, 2, ["jlr", "zl", "j"]),
            ],
            [
                "f",
                "o",
                "litzaq",
                "avbtjlr",
                "zl",
                "jmm",
            ]);
    }

    [Fact]
    public void Auto3()
    {
        TestApplyEditsWithSyncedModels(
            [
                "ofw",
                "qsxmziuvzw",
                "rp",
                "qsnymek",
                "elth",
                "wmgzbwudxz",
                "iwsdkndh",
                "bujlbwb",
                "asuouxfv",
                "xuccnb",
            ],
            [
                EditOp(4, 3, 4, 3, [""]),
            ],
            [
                "ofw",
                "qsxmziuvzw",
                "rp",
                "qsnymek",
                "elth",
                "wmgzbwudxz",
                "iwsdkndh",
                "bujlbwb",
                "asuouxfv",
                "xuccnb",
            ]);
    }

    [Fact]
    public void Auto4()
    {
        TestApplyEditsWithSyncedModels(
            [
                "fefymj",
                "qum",
                "vmiwxxaiqq",
                "dz",
                "lnqdgorosf",
            ],
            [
                EditOp(1, 3, 1, 5, ["hp"]),
                EditOp(1, 7, 2, 1, ["kcg", "", "mpx"]),
                EditOp(2, 2, 2, 2, ["", "aw", ""]),
                EditOp(2, 2, 2, 2, ["vqr", "mo"]),
                EditOp(4, 2, 5, 3, ["xyc"]),
            ],
            [
                "fehpmjkcg",
                "",
                "mpxq",
                "aw",
                "vqr",
                "moum",
                "vmiwxxaiqq",
                "dxycqdgorosf",
            ]);
    }
}
