// Ported from vscode (src/vs/editor/test/common/model/textModelWithTokens.test.ts: the suite
// 'TextModel.getLineIndentGuide', and model.line.test.ts: the suite 'ModelLine - getIndentLevel'; commit b57d5c9e), MIT
// license.
// Differences: model.guides is model.GetGuides(service); each expected row is a tuple.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelGetLineIndentGuideTests
{
    private static void AssertIndentGuides((int Indent, int Start, int End, int ActiveIndent, string Text)[] lines, int indentSize)
    {
        const string languageId = "testLang";
        using var service = new LanguageConfigurationService();
        var text = string.Join('\n', lines.Select(l => l.Text));
        using var model = TestTextModel.Create(text, languageId);
        model.UpdateOptions(new TextModelUpdateOptions { IndentSize = indentSize });
        var guides = model.GetGuides(service);

        var actualIndents = guides.GetLinesIndentGuides(1, model.GetLineCount());

        var actual = new (int, int, int, int, string)[model.GetLineCount()];
        for (var line = 1; line <= model.GetLineCount(); line++)
        {
            var activeIndentGuide = guides.GetActiveIndentGuide(line, 1, model.GetLineCount());
            actual[line - 1] = (actualIndents[line - 1], activeIndentGuide.StartLineNumber, activeIndentGuide.EndLineNumber, activeIndentGuide.Indent, model.GetLineContent(line));
        }

        actual.ShouldBe(lines.Select(l => (l.Indent, l.Start, l.End, l.ActiveIndent, l.Text)).ToArray());
    }

    [Fact]
    public void GetLineIndentGuide_one_level_2() =>
        AssertIndentGuides(
        [
            (0, 2, 4, 1, "A"),
            (1, 2, 4, 1, "  A"),
            (1, 2, 4, 1, "  A"),
            (1, 2, 4, 1, "  A"),
        ], 2);

    [Fact]
    public void GetLineIndentGuide_two_levels() =>
        AssertIndentGuides(
        [
            (0, 2, 5, 1, "A"),
            (1, 2, 5, 1, "  A"),
            (1, 4, 5, 2, "  A"),
            (2, 4, 5, 2, "    A"),
            (2, 4, 5, 2, "    A"),
        ], 2);

    [Fact]
    public void GetLineIndentGuide_three_levels() =>
        AssertIndentGuides(
        [
            (0, 2, 4, 1, "A"),
            (1, 3, 4, 2, "  A"),
            (2, 4, 4, 3, "    A"),
            (3, 4, 4, 3, "      A"),
            (0, 5, 5, 0, "A"),
        ], 2);

    [Fact]
    public void GetLineIndentGuide_decreasing_indent() =>
        AssertIndentGuides(
        [
            (2, 1, 1, 2, "    A"),
            (1, 1, 1, 2, "  A"),
            (0, 1, 2, 1, "A"),
        ], 2);

    [Fact]
    public void GetLineIndentGuide_Java() =>
        AssertIndentGuides(
        [
            (0, 2, 9, 1, "class A {"),
            (1, 3, 4, 2, "  void foo() {"),
            (2, 3, 4, 2, "    console.log(1);"),
            (2, 3, 4, 2, "    console.log(2);"),
            (1, 3, 4, 2, "  }"),
            (1, 2, 9, 1, ""),
            (1, 8, 8, 2, "  void bar() {"),
            (2, 8, 8, 2, "    console.log(3);"),
            (1, 8, 8, 2, "  }"),
            (0, 2, 9, 1, "}"),
            (0, 12, 12, 1, "interface B {"),
            (1, 12, 12, 1, "  void bar();"),
            (0, 12, 12, 1, "}"),
        ], 2);

    [Fact]
    public void GetLineIndentGuide_Javadoc() =>
        AssertIndentGuides(
        [
            (0, 2, 3, 1, "/**"),
            (1, 2, 3, 1, " * Comment"),
            (1, 2, 3, 1, " */"),
            (0, 5, 6, 1, "class A {"),
            (1, 5, 6, 1, "  void foo() {"),
            (1, 5, 6, 1, "  }"),
            (0, 5, 6, 1, "}"),
        ], 2);

    [Fact]
    public void GetLineIndentGuide_Whitespace() =>
        AssertIndentGuides(
        [
            (0, 2, 7, 1, "class A {"),
            (1, 2, 7, 1, ""),
            (1, 4, 5, 2, "  void foo() {"),
            (2, 4, 5, 2, "    "),
            (2, 4, 5, 2, "    return 1;"),
            (1, 4, 5, 2, "  }"),
            (1, 2, 7, 1, "      "),
            (0, 2, 7, 1, "}"),
        ], 2);

    [Fact]
    public void GetLineIndentGuide_Tabs() =>
        AssertIndentGuides(
        [
            (0, 2, 7, 1, "class A {"),
            (1, 2, 7, 1, "\t\t"),
            (1, 4, 5, 2, "\tvoid foo() {"),
            (2, 4, 5, 2, "\t \t//hello"),
            (2, 4, 5, 2, "\t    return 2;"),
            (1, 4, 5, 2, "  \t}"),
            (1, 2, 7, 1, "      "),
            (0, 2, 7, 1, "}"),
        ], 4);

    [Fact]
    public void GetLineIndentGuide_checker_ts() =>
        AssertIndentGuides(
        [
            (0, 1, 1, 0, "/// <reference path=\"binder.ts\"/>"),
            (0, 2, 2, 0, ""),
            (0, 3, 3, 0, "/* @internal */"),
            (0, 5, 16, 1, "namespace ts {"),
            (1, 5, 16, 1, "    let nextSymbolId = 1;"),
            (1, 5, 16, 1, "    let nextNodeId = 1;"),
            (1, 5, 16, 1, "    let nextMergeId = 1;"),
            (1, 5, 16, 1, "    let nextFlowId = 1;"),
            (1, 5, 16, 1, ""),
            (1, 11, 15, 2, "    export function getNodeId(node: Node): number {"),
            (2, 12, 13, 3, "        if (!node.id) {"),
            (3, 12, 13, 3, "            node.id = nextNodeId;"),
            (3, 12, 13, 3, "            nextNodeId++;"),
            (2, 12, 13, 3, "        }"),
            (2, 11, 15, 2, "        return node.id;"),
            (1, 11, 15, 2, "    }"),
            (0, 5, 16, 1, "}"),
        ], 4);

    [Fact]
    public void Issue_8425_Missing_indentation_lines_for_first_level_indentation() =>
        AssertIndentGuides(
        [
            (1, 2, 3, 2, "\tindent1"),
            (2, 2, 3, 2, "\t\tindent2"),
            (2, 2, 3, 2, "\t\tindent2"),
            (1, 2, 3, 2, "\tindent1"),
        ], 4);

    [Fact]
    public void Issue_8952_Indentation_guide_lines_going_through_text_on_yml_file() =>
        AssertIndentGuides(
        [
            (0, 2, 5, 1, "properties:"),
            (1, 3, 5, 2, "    emailAddress:"),
            (2, 3, 5, 2, "        - bla"),
            (2, 5, 5, 3, "        - length:"),
            (3, 5, 5, 3, "            max: 255"),
            (0, 6, 6, 0, "getters:"),
        ], 4);

    [Fact]
    public void Issue_11892_Indent_guides_look_funny() =>
        AssertIndentGuides(
        [
            (0, 2, 7, 1, "function test(base) {"),
            (1, 3, 6, 2, "\tswitch (base) {"),
            (2, 4, 4, 3, "\t\tcase 1:"),
            (3, 4, 4, 3, "\t\t\treturn 1;"),
            (2, 6, 6, 3, "\t\tcase 2:"),
            (3, 6, 6, 3, "\t\t\treturn 2;"),
            (1, 2, 7, 1, "\t}"),
            (0, 2, 7, 1, "}"),
        ], 4);

    [Fact]
    public void Issue_12398_Problem_in_indent_guidelines() =>
        AssertIndentGuides(
        [
            (2, 2, 2, 3, "\t\t.bla"),
            (3, 2, 2, 3, "\t\t\tlabel(for)"),
            (0, 3, 3, 0, "include script"),
        ], 4);

    [Fact]
    public void Tweaks_no_active() =>
        AssertIndentGuides(
        [
            (0, 1, 1, 0, "A"),
            (0, 2, 2, 0, "A"),
        ], 2);

    [Fact]
    public void Tweaks_inside_scope() =>
        AssertIndentGuides(
        [
            (0, 2, 2, 1, "A"),
            (1, 2, 2, 1, "  A"),
        ], 2);

    [Fact]
    public void Tweaks_scope_start() =>
        AssertIndentGuides(
        [
            (0, 2, 2, 1, "A"),
            (1, 2, 2, 1, "  A"),
            (0, 2, 2, 1, "A"),
        ], 2);

    [Fact]
    public void Tweaks_empty_line() =>
        AssertIndentGuides(
        [
            (0, 2, 4, 1, "A"),
            (1, 2, 4, 1, "  A"),
            (1, 2, 4, 1, ""),
            (1, 2, 4, 1, "  A"),
            (0, 2, 4, 1, "A"),
        ], 2);

    [Fact]
    public void Issue_49173()
    {
        using var service = new LanguageConfigurationService();
        using var model = TestTextModel.Create(string.Join('\n',
            "class A {",
            "\tpublic m1(): void {",
            "\t}",
            "\tpublic m2(): void {",
            "\t}",
            "\tpublic m3(): void {",
            "\t}",
            "\tpublic m4(): void {",
            "\t}",
            "\tpublic m5(): void {",
            "\t}",
            "}"));

        var actual = model.GetGuides(service).GetActiveIndentGuide(2, 4, 9);
        actual.ShouldBe(new ActiveIndentGuideInfo(2, 9, 1));
    }
}

public sealed class ModelLineGetIndentLevelTests
{
    [Theory]
    [InlineData("", -1)]
    [InlineData(" ", -1)]
    [InlineData("   \t", -1)]
    [InlineData("Hello", 0)]
    [InlineData(" Hello", 1)]
    [InlineData("   Hello", 3)]
    [InlineData("\tHello", 4)]
    [InlineData(" \tHello", 4)]
    [InlineData("  \tHello", 4)]
    [InlineData("   \tHello", 4)]
    [InlineData("    \tHello", 8)]
    [InlineData("     \tHello", 8)]
    [InlineData("\t Hello", 5)]
    [InlineData("\t \tHello", 8)]
    public void GetIndentLevel(string text, int expected) => TextModelUtils.ComputeIndentLevel(text, 4).ShouldBe(expected);
}
