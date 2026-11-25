// Not a port. Upstream tests autoIndent.ts and enterAction.ts only through the editor (viewModel.type in
// src/vs/editor/contrib/indentation/test/browser/indentation.test.ts, commit b57d5c9e, MIT license), which is not
// ported yet. These call the functions directly on the models and at the positions of some of those tests, and check
// the values the editor builds its result from (the indent converter is the cursor's: ShiftCommand.ShiftIndent and
// UnshiftIndent with the model's options).

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class AutoIndentTests
{
    private const string LanguageId = Language.TypeScript;

    private sealed class IndentConverter : IIndentConverter
    {
        private readonly TextModelResolvedOptions _options;

        public IndentConverter(TextModel model)
        {
            _options = model.GetOptions();
        }

        public string ShiftIndent(string indentation) =>
            ShiftCommand.ShiftIndent(indentation, indentation.Length + 1, _options.TabSize, _options.IndentSize, _options.InsertSpaces);

        public string UnshiftIndent(string indentation) =>
            ShiftCommand.UnshiftIndent(indentation, indentation.Length + 1, _options.TabSize, _options.IndentSize, _options.InsertSpaces);
    }

    private static void WithModel(string[] lines, Action<TextModel, LanguageConfigurationService> callback)
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        using var registration = IndentationTestUtils.RegisterLanguageConfiguration(languageConfigurationService, LanguageId);
        using var model = TestTextModel.Create(string.Join('\n', lines), LanguageId);
        callback(model, languageConfigurationService);
    }

    [Fact]
    public void Issue_208215_Indent_After_Arrow_Function()
    {
        WithModel(["const add1 = (n) =>"], (model, service) =>
        {
            var range = new TextRange(1, 20, 1, 20);
            EnterActions.GetEnterAction(EditorAutoIndentStrategy.Full, model, range, service).ShouldBeNull();
            var r = AutoIndent.GetIndentForEnter(EditorAutoIndentStrategy.Full, model, range, new IndentConverter(model), service);
            r.ShouldBe(new IndentForEnterResult("", "    "));
        });
    }

    [Fact]
    public void Issue_208215_Indent_After_Arrow_Function_2()
    {
        WithModel(
            [
                "const array = [1, 2, 3, 4, 5];",
                "array.map(",
                "    v =>",
            ],
            (model, service) =>
            {
                var r = AutoIndent.GetIndentForEnter(EditorAutoIndentStrategy.Full, model, new TextRange(3, 9, 3, 9), new IndentConverter(model), service);
                r.ShouldBe(new IndentForEnterResult("    ", "        "));
            });
    }

    [Fact]
    public void Issue_115304_Indent_Block_Comment_OnEnter()
    {
        WithModel(
            [
                "/** */",
                "function f() {}",
            ],
            (model, service) =>
            {
                var action = EnterActions.GetEnterAction(EditorAutoIndentStrategy.Advanced, model, new TextRange(1, 4, 1, 4), service);
                action.ShouldBe(new CompleteEnterAction(IndentAction.IndentOutdent, " * ", 0, ""));
            });
    }

    [Fact]
    public void Enter_Action_Removes_Text_From_The_Indentation()
    {
        // The javascript rule for ` */|` (removeText 1).
        WithModel(
            [
                "    /**",
                "     */",
            ],
            (model, service) =>
            {
                var action = EnterActions.GetEnterAction(EditorAutoIndentStrategy.Advanced, model, new TextRange(2, 8, 2, 8), service);
                action.ShouldBe(new CompleteEnterAction(IndentAction.None, "", 1, "    "));
            });
    }

    [Fact]
    public void Issue_208232_Incorrect_Indentation_Inside_Of_Comments()
    {
        WithModel(
            [
                "/**",
                "indentation done for {",
                "*/",
            ],
            (model, service) =>
            {
                IndentationTestUtils.RegisterTokenizationSupport(
                    model,
                    [
                        [new(0, StandardTokenType.Comment)],
                        [new(0, StandardTokenType.Comment)],
                        [new(0, StandardTokenType.Comment)],
                    ],
                    LanguageId);
                var range = new TextRange(2, 23, 2, 23);
                EnterActions.GetEnterAction(EditorAutoIndentStrategy.Full, model, range, service).ShouldBeNull();
                var r = AutoIndent.GetIndentForEnter(EditorAutoIndentStrategy.Full, model, range, new IndentConverter(model), service);
                r.ShouldBe(new IndentForEnterResult("", ""));
            });
    }

    [Fact]
    public void Issue_209802_Allman_Style_Braces_In_JavaScript()
    {
        WithModel(["if (/*condition*/)"], (model, service) =>
        {
            var r = AutoIndent.GetIndentForEnter(EditorAutoIndentStrategy.Full, model, new TextRange(1, 19, 1, 19), new IndentConverter(model), service);
            r.ShouldBe(new IndentForEnterResult("", "    "));
        });

        WithModel(
            [
                "if (/*condition*/)",
                "    ",
            ],
            (model, service) =>
            {
                var cursorConfig = new CursorConfiguration(LanguageId, model.GetOptions(), new EditorOptions(), service);
                var indentation = AutoIndent.GetIndentActionForType(cursorConfig, model, new TextRange(2, 5, 2, 5), "{", new IndentConverter(model), service);
                indentation.ShouldBe("");
            });
    }

    [Fact]
    public void Get_Good_Indent_For_Line()
    {
        WithModel(
            [
                "function f() {",
                "return 1;",
                "}",
            ],
            (model, service) =>
            {
                var virtualModel = new TextModelVirtualModel(model);
                var converter = new IndentConverter(model);
                AutoIndent.GetGoodIndentForLine(EditorAutoIndentStrategy.Full, virtualModel, LanguageId, 1, converter, service).ShouldBe("");
                AutoIndent.GetGoodIndentForLine(EditorAutoIndentStrategy.Full, virtualModel, LanguageId, 2, converter, service).ShouldBe("    ");
                AutoIndent.GetGoodIndentForLine(EditorAutoIndentStrategy.Full, virtualModel, LanguageId, 3, converter, service).ShouldBe("");
                AutoIndent.GetGoodIndentForLine(EditorAutoIndentStrategy.Advanced, virtualModel, LanguageId, 2, converter, service).ShouldBeNull();
            });
    }

    [Fact]
    public void Get_Inherit_Indent_For_Line_After_Indent_Next_Line()
    {
        WithModel(
            [
                "function f() {",
                "    if (a)",
                "        b();",
                "",
            ],
            (model, service) =>
            {
                // Line 3 only is indented by `if (a)`: line 4 goes back to the indentation of line 2.
                var r = AutoIndent.GetInheritIndentForLine(EditorAutoIndentStrategy.Full, model, 4, true, service);
                r.ShouldBe(new InheritIndentResult("    ", null, 2));
            });
    }

    [Fact]
    public void Get_Indent_Metadata()
    {
        WithModel(
            [
                "function f() {",
                "}",
            ],
            (model, service) =>
            {
                AutoIndent.GetIndentMetadata(model, 1, service).ShouldBe(IndentConsts.IncreaseMask);
                AutoIndent.GetIndentMetadata(model, 2, service).ShouldBe(IndentConsts.DecreaseMask);
                AutoIndent.GetIndentMetadata(model, 3, service).ShouldBeNull();
            });
    }
}
