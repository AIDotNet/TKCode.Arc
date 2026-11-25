// Ported from vscode (src/vs/editor/contrib/linesOperations/test/browser/linesOperations.test.ts, commit b57d5c9e), MIT
// license: the tests from 'toggle case' on (the rest is in LinesOperationsTests.cs).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.LinesOperations;
using LinesOperationsImpl = TKCode.Arc.Editor.Contrib.LinesOperations.LinesOperations;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;
using static TKCode.Arc.Editor.Tests.Contrib.EditingTestEditor;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed partial class LinesOperationsTests
{
    [Fact]
    public void ToggleCase()
    {
        WithTestCodeEditor(
            [
                "hello world",
                "öçşğü",
                "parseHTMLString",
                "getElementById",
                "insertHTML",
                "PascalCase",
                "CSSSelectorsList",
                "iD",
                "tEST",
                "öçşÖÇŞğüĞÜ",
                "audioConverter.convertM4AToMP3();",
                "snake_case",
                "Capital_Snake_Case",
                "function helloWorld() {\nreturn someGlobalObject.printHelloWorld(\"en\", \"utf-8\");\n}\nhelloWorld();",
                "\"JavaScript\"",
                "parseHTML4String",
                "_accessor: ServicesAccessor"
            ], null, editor => {
                var model = editor.GetModel()!;
                var uppercaseAction = new UpperCaseAction();
                var lowercaseAction = new LowerCaseAction();
                var titlecaseAction = new TitleCaseAction();
                var snakecaseAction = new SnakeCaseAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 12));
                ExecuteAction(uppercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "HELLO WORLD");
                AssertSelection(editor, new TextSelection(1, 1, 1, 12));

                editor.SetSelection(new TextSelection(1, 1, 1, 12));
                ExecuteAction(lowercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "hello world");
                AssertSelection(editor, new TextSelection(1, 1, 1, 12));

                editor.SetSelection(new TextSelection(1, 3, 1, 3));
                ExecuteAction(uppercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "HELLO world");
                AssertSelection(editor, new TextSelection(1, 3, 1, 3));

                editor.SetSelection(new TextSelection(1, 4, 1, 4));
                ExecuteAction(lowercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "hello world");
                AssertSelection(editor, new TextSelection(1, 4, 1, 4));

                editor.SetSelection(new TextSelection(1, 1, 1, 12));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "Hello World");
                AssertSelection(editor, new TextSelection(1, 1, 1, 12));

                editor.SetSelection(new TextSelection(2, 1, 2, 6));
                ExecuteAction(uppercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "ÖÇŞĞÜ");
                AssertSelection(editor, new TextSelection(2, 1, 2, 6));

                editor.SetSelection(new TextSelection(2, 1, 2, 6));
                ExecuteAction(lowercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "öçşğü");
                AssertSelection(editor, new TextSelection(2, 1, 2, 6));

                editor.SetSelection(new TextSelection(2, 1, 2, 6));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "Öçşğü");
                AssertSelection(editor, new TextSelection(2, 1, 2, 6));

                editor.SetSelection(new TextSelection(3, 1, 3, 16));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(3), "parse_html_string");
                AssertSelection(editor, new TextSelection(3, 1, 3, 18));

                editor.SetSelection(new TextSelection(4, 1, 4, 15));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(4), "get_element_by_id");
                AssertSelection(editor, new TextSelection(4, 1, 4, 18));

                editor.SetSelection(new TextSelection(5, 1, 5, 11));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(5), "insert_html");
                AssertSelection(editor, new TextSelection(5, 1, 5, 12));

                editor.SetSelection(new TextSelection(6, 1, 6, 11));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(6), "pascal_case");
                AssertSelection(editor, new TextSelection(6, 1, 6, 12));

                editor.SetSelection(new TextSelection(7, 1, 7, 17));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(7), "css_selectors_list");
                AssertSelection(editor, new TextSelection(7, 1, 7, 19));

                editor.SetSelection(new TextSelection(8, 1, 8, 3));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(8), "i_d");
                AssertSelection(editor, new TextSelection(8, 1, 8, 4));

                editor.SetSelection(new TextSelection(9, 1, 9, 5));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(9), "t_est");
                AssertSelection(editor, new TextSelection(9, 1, 9, 6));

                editor.SetSelection(new TextSelection(10, 1, 10, 11));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(10), "öçş_öç_şğü_ğü");
                AssertSelection(editor, new TextSelection(10, 1, 10, 14));

                editor.SetSelection(new TextSelection(11, 1, 11, 34));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(11), "audio_converter.convert_m4a_to_mp3();");
                AssertSelection(editor, new TextSelection(11, 1, 11, 38));

                editor.SetSelection(new TextSelection(12, 1, 12, 11));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(12), "snake_case");
                AssertSelection(editor, new TextSelection(12, 1, 12, 11));

                editor.SetSelection(new TextSelection(13, 1, 13, 19));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(13), "capital_snake_case");
                AssertSelection(editor, new TextSelection(13, 1, 13, 19));

                editor.SetSelection(new TextSelection(14, 1, 17, 14));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetValueInRange(new TextSelection(14, 1, 17, 15)), "function hello_world() {\nreturn some_global_object.print_hello_world(\"en\", \"utf-8\");\n}\nhello_world();");
                AssertSelection(editor, new TextSelection(14, 1, 17, 15));

                editor.SetSelection(new TextSelection(18, 1, 18, 13));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(18), "\"java_script\"");
                AssertSelection(editor, new TextSelection(18, 1, 18, 14));

                editor.SetSelection(new TextSelection(19, 1, 19, 17));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(19), "parse_html4_string");
                AssertSelection(editor, new TextSelection(19, 1, 19, 19));

                editor.SetSelection(new TextSelection(20, 1, 20, 28));
                ExecuteAction(snakecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(20), "_accessor: services_accessor");
                AssertSelection(editor, new TextSelection(20, 1, 20, 29));
            }
        );

        WithTestCodeEditor(
            [
                "foO baR BaZ",
                "foO'baR'BaZ",
                "foO[baR]BaZ",
                "foO`baR~BaZ",
                "foO^baR%BaZ",
                "foO$baR!BaZ",
                "'physician's assistant'"
            ], null, editor => {
                var model = editor.GetModel()!;
                var titlecaseAction = new TitleCaseAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 12));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "Foo Bar Baz");

                editor.SetSelection(new TextSelection(2, 1, 2, 12));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "Foo'bar'baz");

                editor.SetSelection(new TextSelection(3, 1, 3, 12));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(3), "Foo[Bar]Baz");

                editor.SetSelection(new TextSelection(4, 1, 4, 12));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(4), "Foo`Bar~Baz");

                editor.SetSelection(new TextSelection(5, 1, 5, 12));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(5), "Foo^Bar%Baz");

                editor.SetSelection(new TextSelection(6, 1, 6, 12));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(6), "Foo$Bar!Baz");

                editor.SetSelection(new TextSelection(7, 1, 7, 23));
                ExecuteAction(titlecaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(7), "'Physician's Assistant'");
            }
        );

        WithTestCodeEditor(
            [
                "camel from words",
                "from_snake_case",
                "from-kebab-case",
                "alreadyCamel",
                "ReTain_some_CAPitalization",
                "my_var.test_function()",
                "öçş_öç_şğü_ğü",
                "XMLHttpRequest",
                "\tfunction hello_world() {",
                "\t\treturn some_global_object;",
                "\t}",
            ], null, editor => {
                var model = editor.GetModel()!;
                var camelcaseAction = new CamelCaseAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 18));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "camelFromWords");

                editor.SetSelection(new TextSelection(2, 1, 2, 15));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "fromSnakeCase");

                editor.SetSelection(new TextSelection(3, 1, 3, 15));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(3), "fromKebabCase");

                editor.SetSelection(new TextSelection(4, 1, 4, 12));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(4), "alreadyCamel");

                editor.SetSelection(new TextSelection(5, 1, 5, 26));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(5), "reTainSomeCAPitalization");

                editor.SetSelection(new TextSelection(6, 1, 6, 23));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(6), "myVar.testFunction()");

                editor.SetSelection(new TextSelection(7, 1, 7, 14));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(7), "öçşÖçŞğüĞü");

                editor.SetSelection(new TextSelection(8, 1, 8, 14));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(8), "XMLHttpRequest");

                editor.SetSelection(new TextSelection(9, 1, 11, 2));
                ExecuteAction(camelcaseAction, editor);
                AssertDeepStrictEqual(model.GetValueInRange(new TextSelection(9, 1, 11, 3)), "\tfunction helloWorld() {\n\t\treturn someGlobalObject;\n\t}");
            }
        );

        WithTestCodeEditor(
            [
                "",
                "   "
            ], null, editor => {
                var model = editor.GetModel()!;
                var uppercaseAction = new UpperCaseAction();
                var lowercaseAction = new LowerCaseAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 1));
                ExecuteAction(uppercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "");
                AssertSelection(editor, new TextSelection(1, 1, 1, 1));

                editor.SetSelection(new TextSelection(1, 1, 1, 1));
                ExecuteAction(lowercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "");
                AssertSelection(editor, new TextSelection(1, 1, 1, 1));

                editor.SetSelection(new TextSelection(2, 2, 2, 2));
                ExecuteAction(uppercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "   ");
                AssertSelection(editor, new TextSelection(2, 2, 2, 2));

                editor.SetSelection(new TextSelection(2, 2, 2, 2));
                ExecuteAction(lowercaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "   ");
                AssertSelection(editor, new TextSelection(2, 2, 2, 2));
            }
        );

        WithTestCodeEditor(
            [
                "hello world",
                "öçşğü",
                "parseHTMLString",
                "getElementById",
                "PascalCase",
                "öçşÖÇŞğüĞÜ",
                "audioConverter.convertM4AToMP3();",
                "Capital_Snake_Case",
                "parseHTML4String",
                "_accessor: ServicesAccessor",
                "Kebab-Case",
            ], null, editor => {
                var model = editor.GetModel()!;
                var kebabCaseAction = new KebabCaseAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 12));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "hello world");
                AssertSelection(editor, new TextSelection(1, 1, 1, 12));

                editor.SetSelection(new TextSelection(2, 1, 2, 6));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "öçşğü");
                AssertSelection(editor, new TextSelection(2, 1, 2, 6));

                editor.SetSelection(new TextSelection(3, 1, 3, 16));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(3), "parse-html-string");
                AssertSelection(editor, new TextSelection(3, 1, 3, 18));

                editor.SetSelection(new TextSelection(4, 1, 4, 15));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(4), "get-element-by-id");
                AssertSelection(editor, new TextSelection(4, 1, 4, 18));

                editor.SetSelection(new TextSelection(5, 1, 5, 11));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(5), "pascal-case");
                AssertSelection(editor, new TextSelection(5, 1, 5, 12));

                editor.SetSelection(new TextSelection(6, 1, 6, 11));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(6), "öçş-öç-şğü-ğü");
                AssertSelection(editor, new TextSelection(6, 1, 6, 14));

                editor.SetSelection(new TextSelection(7, 1, 7, 34));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(7), "audio-converter.convert-m4a-to-mp3();");
                AssertSelection(editor, new TextSelection(7, 1, 7, 38));

                editor.SetSelection(new TextSelection(8, 1, 8, 19));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(8), "capital-snake-case");
                AssertSelection(editor, new TextSelection(8, 1, 8, 19));

                editor.SetSelection(new TextSelection(9, 1, 9, 17));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(9), "parse-html4-string");
                AssertSelection(editor, new TextSelection(9, 1, 9, 19));

                editor.SetSelection(new TextSelection(10, 1, 10, 28));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(10), "_accessor: services-accessor");
                AssertSelection(editor, new TextSelection(10, 1, 10, 29));

                editor.SetSelection(new TextSelection(11, 1, 11, 11));
                ExecuteAction(kebabCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(11), "kebab-case");
                AssertSelection(editor, new TextSelection(11, 1, 11, 11));
            }
        );

        WithTestCodeEditor(
            [
                "hello world",
                "öçşğü",
                "parseHTMLString",
                "getElementById",
                "PascalCase",
                "öçşÖÇŞğüĞÜ",
                "audioConverter.convertM4AToMP3();",
                "Capital_Snake_Case",
                "parseHTML4String",
                "Kebab-Case",
                "FOO_BAR",
                "FOO BAR A",
                "xML_HTTP-reQUEsT",
                "ÉCOLE",
                "ΩMEGA_CASE",
                "ДОМ_ТЕСТ",
            ], null, editor => {
                var model = editor.GetModel()!;
                var pascalCaseAction = new PascalCaseAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 12));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "HelloWorld");
                AssertSelection(editor, new TextSelection(1, 1, 1, 11));

                editor.SetSelection(new TextSelection(2, 1, 2, 6));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "Öçşğü");
                AssertSelection(editor, new TextSelection(2, 1, 2, 6));

                editor.SetSelection(new TextSelection(3, 1, 3, 16));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(3), "ParseHTMLString");
                AssertSelection(editor, new TextSelection(3, 1, 3, 16));

                editor.SetSelection(new TextSelection(4, 1, 4, 15));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(4), "GetElementById");
                AssertSelection(editor, new TextSelection(4, 1, 4, 15));

                editor.SetSelection(new TextSelection(5, 1, 5, 11));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(5), "PascalCase");
                AssertSelection(editor, new TextSelection(5, 1, 5, 11));

                editor.SetSelection(new TextSelection(6, 1, 6, 11));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(6), "ÖçşÖÇŞğüĞÜ");
                AssertSelection(editor, new TextSelection(6, 1, 6, 11));

                editor.SetSelection(new TextSelection(7, 1, 7, 34));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(7), "AudioConverter.ConvertM4AToMP3();");
                AssertSelection(editor, new TextSelection(7, 1, 7, 34));

                editor.SetSelection(new TextSelection(8, 1, 8, 19));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(8), "CapitalSnakeCase");
                AssertSelection(editor, new TextSelection(8, 1, 8, 17));

                editor.SetSelection(new TextSelection(9, 1, 9, 17));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(9), "ParseHTML4String");
                AssertSelection(editor, new TextSelection(9, 1, 9, 17));

                editor.SetSelection(new TextSelection(10, 1, 10, 11));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(10), "KebabCase");
                AssertSelection(editor, new TextSelection(10, 1, 10, 10));

                editor.SetSelection(new TextSelection(9, 1, 10, 11));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetValueInRange(new TextSelection(9, 1, 10, 11)), "ParseHTML4String\nKebabCase");
                AssertSelection(editor, new TextSelection(9, 1, 10, 10));

                editor.SetSelection(new TextSelection(11, 1, 11, 8));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(11), "FooBar");
                AssertSelection(editor, new TextSelection(11, 1, 11, 7));

                editor.SetSelection(new TextSelection(12, 1, 12, 10));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(12), "FooBarA");
                AssertSelection(editor, new TextSelection(12, 1, 12, 8));

                editor.SetSelection(new TextSelection(13, 1, 13, 17));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(13), "XmlHttpReQUEsT");
                AssertSelection(editor, new TextSelection(13, 1, 13, 15));

                editor.SetSelection(new TextSelection(14, 1, 14, 6));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(14), "École");
                AssertSelection(editor, new TextSelection(14, 1, 14, 6));

                editor.SetSelection(new TextSelection(15, 1, 15, 11));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(15), "ΩmegaCase");
                AssertSelection(editor, new TextSelection(15, 1, 15, 10));

                editor.SetSelection(new TextSelection(16, 1, 16, 9));
                ExecuteAction(pascalCaseAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(16), "ДомТест");
                AssertSelection(editor, new TextSelection(16, 1, 16, 8));

            }
        );
    }

    // suite: DeleteAllRightAction
    [Fact]
    public void ShouldBeNoopOnEmpty()
    {
            WithTestCodeEditor([""], null, editor => {
                var model = editor.GetModel()!;
                var action = new DeleteAllRightAction();

                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), [""]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 1, 1, 1)]);

                editor.SetSelection(new TextSelection(1, 1, 1, 1));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), [""]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 1, 1, 1)]);

                editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(1, 1, 1, 1), new TextSelection(1, 1, 1, 1)]);
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), [""]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 1, 1, 1)]);
            });
    }


    [Fact]
    public void ShouldDeleteSelectedRange()
    {
            WithTestCodeEditor([
                "hello",
                "world"
            ], null, editor => {
                var model = editor.GetModel()!;
                var action = new DeleteAllRightAction();

                editor.SetSelection(new TextSelection(1, 2, 1, 5));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["ho", "world"]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 2, 1, 2)]);

                editor.SetSelection(new TextSelection(1, 1, 2, 4));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["ld"]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 1, 1, 1)]);

                editor.SetSelection(new TextSelection(1, 1, 1, 3));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), [""]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 1, 1, 1)]);
            });
    }


    [Fact]
    public void ShouldDeleteToTheRightOfTheCursor()
    {
            WithTestCodeEditor([
                "hello",
                "world"
            ], null, editor => {
                var model = editor.GetModel()!;
                var action = new DeleteAllRightAction();

                editor.SetSelection(new TextSelection(1, 3, 1, 3));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["he", "world"]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 3, 1, 3)]);

                editor.SetSelection(new TextSelection(2, 1, 2, 1));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["he", ""]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(2, 1, 2, 1)]);
            });
    }


    [Fact]
    public void ShouldJoinTwoLinesIfAtTheEndOfTheLine()
    {
            WithTestCodeEditor([
                "hello",
                "world"
            ], null, editor => {
                var model = editor.GetModel()!;
                var action = new DeleteAllRightAction();

                editor.SetSelection(new TextSelection(1, 6, 1, 6));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["helloworld"]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 6, 1, 6)]);

                editor.SetSelection(new TextSelection(1, 6, 1, 6));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["hello"]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 6, 1, 6)]);

                editor.SetSelection(new TextSelection(1, 6, 1, 6));
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["hello"]);
                AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 6, 1, 6)]);
            });
    }


    [Fact]
    public void ShouldWorkWithMultipleCursors()
    {
            WithTestCodeEditor([
                "hello",
                "there",
                "world"
            ], null, editor => {
                var model = editor.GetModel()!;
                var action = new DeleteAllRightAction();

                editor.SetSelections([
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(1, 6, 1, 6),
                    new TextSelection(3, 4, 3, 4),
                ]);
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["hethere", "wor"]);
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(2, 4, 2, 4)
                ]);

                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["he", "wor"]);
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(2, 4, 2, 4)
                ]);

                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["hewor"]);
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(1, 6, 1, 6)
                ]);

                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["he"]);
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3)
                ]);

                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["he"]);
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3)
                ]);
            });
    }


    [Fact]
    public void ShouldWorkWithUndoRedo()
    {
            WithTestCodeEditor([
                "hello",
                "there",
                "world"
            ], null, editor => {
                var model = editor.GetModel()!;
                var action = new DeleteAllRightAction();

                editor.SetSelections([
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(1, 6, 1, 6),
                    new TextSelection(3, 4, 3, 4),
                ]);
                ExecuteAction(action, editor);
                AssertDeepStrictEqual(model.GetLinesContent(), ["hethere", "wor"]);
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(2, 4, 2, 4)
                ]);

                editor.Undo();
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(1, 6, 1, 6),
                    new TextSelection(3, 4, 3, 4)
                ]);
                editor.Redo();
                AssertDeepStrictEqual(editor.GetSelections(), [
                    new TextSelection(1, 3, 1, 3),
                    new TextSelection(2, 4, 2, 4)
                ]);
            });
    }


    [Fact]
    public void InsertLineBeforeActionTest()
    {
        void TestInsertLineBefore(int lineNumber, int column, Action<TextModel, TestViewModel> callback)
        {
            string[] TEXT = [
                "First line",
                "Second line",
                "Third line"
            ];
            WithTestCodeEditor(TEXT, null, (editor, viewModel) => {
                editor.SetPosition(new TextPosition(lineNumber, column));
                var insertLineBeforeAction = new InsertLineBeforeAction();

                ExecuteAction(insertLineBeforeAction, editor);
                callback(editor.GetModel()!, viewModel);
            });
        }

        TestInsertLineBefore(1, 3, (model, viewModel) => {
            AssertDeepStrictEqual(viewModel.GetSelection(), new TextSelection(1, 1, 1, 1));
            AssertDeepStrictEqual(model.GetLineContent(1), "");
            AssertDeepStrictEqual(model.GetLineContent(2), "First line");
            AssertDeepStrictEqual(model.GetLineContent(3), "Second line");
            AssertDeepStrictEqual(model.GetLineContent(4), "Third line");
        });

        TestInsertLineBefore(2, 3, (model, viewModel) => {
            AssertDeepStrictEqual(viewModel.GetSelection(), new TextSelection(2, 1, 2, 1));
            AssertDeepStrictEqual(model.GetLineContent(1), "First line");
            AssertDeepStrictEqual(model.GetLineContent(2), "");
            AssertDeepStrictEqual(model.GetLineContent(3), "Second line");
            AssertDeepStrictEqual(model.GetLineContent(4), "Third line");
        });

        TestInsertLineBefore(3, 3, (model, viewModel) => {
            AssertDeepStrictEqual(viewModel.GetSelection(), new TextSelection(3, 1, 3, 1));
            AssertDeepStrictEqual(model.GetLineContent(1), "First line");
            AssertDeepStrictEqual(model.GetLineContent(2), "Second line");
            AssertDeepStrictEqual(model.GetLineContent(3), "");
            AssertDeepStrictEqual(model.GetLineContent(4), "Third line");
        });
    }


    [Fact]
    public void InsertLineAfterActionTest()
    {
        void TestInsertLineAfter(int lineNumber, int column, Action<TextModel, TestViewModel> callback)
        {
            string[] TEXT = [
                "First line",
                "Second line",
                "Third line"
            ];
            WithTestCodeEditor(TEXT, null, (editor, viewModel) => {
                editor.SetPosition(new TextPosition(lineNumber, column));
                var insertLineAfterAction = new InsertLineAfterAction();

                ExecuteAction(insertLineAfterAction, editor);
                callback(editor.GetModel()!, viewModel);
            });
        }

        TestInsertLineAfter(1, 3, (model, viewModel) => {
            AssertDeepStrictEqual(viewModel.GetSelection(), new TextSelection(2, 1, 2, 1));
            AssertDeepStrictEqual(model.GetLineContent(1), "First line");
            AssertDeepStrictEqual(model.GetLineContent(2), "");
            AssertDeepStrictEqual(model.GetLineContent(3), "Second line");
            AssertDeepStrictEqual(model.GetLineContent(4), "Third line");
        });

        TestInsertLineAfter(2, 3, (model, viewModel) => {
            AssertDeepStrictEqual(viewModel.GetSelection(), new TextSelection(3, 1, 3, 1));
            AssertDeepStrictEqual(model.GetLineContent(1), "First line");
            AssertDeepStrictEqual(model.GetLineContent(2), "Second line");
            AssertDeepStrictEqual(model.GetLineContent(3), "");
            AssertDeepStrictEqual(model.GetLineContent(4), "Third line");
        });

        TestInsertLineAfter(3, 3, (model, viewModel) => {
            AssertDeepStrictEqual(viewModel.GetSelection(), new TextSelection(4, 1, 4, 1));
            AssertDeepStrictEqual(model.GetLineContent(1), "First line");
            AssertDeepStrictEqual(model.GetLineContent(2), "Second line");
            AssertDeepStrictEqual(model.GetLineContent(3), "Third line");
            AssertDeepStrictEqual(model.GetLineContent(4), "");
        });
    }


    [Fact]
    public void Bug18276EditorIndentationBrokenWhenSelectionIsEmpty()
    {

        var model = TestTextModel.Create("function baz() {", null, TextModel.DefaultCreationOptions with { InsertSpaces = false });

        WithTestCodeEditor(model, null, editor => {
            var indentLinesAction = new IndentLinesAction();
            editor.SetPosition(new TextPosition(1, 2));

            ExecuteAction(indentLinesAction, editor);
            AssertDeepStrictEqual(model.GetLineContent(1), "\tfunction baz() {");
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 3, 1, 3));

            CoreEditingCommands.Tab.RunEditorCommand(editor, null);
            AssertDeepStrictEqual(model.GetLineContent(1), "\tf\tunction baz() {");
        });

        model.Dispose();
    }


    [Fact]
    public void Issue80736IndentingWhileTheCursorIsAtTheStartOfALineOfTextCausesTheAddedSpacesOrTabToBeSelected()
    {
        var model = TestTextModel.Create("Some text", null, TextModel.DefaultCreationOptions with { InsertSpaces = false });

        WithTestCodeEditor(model, null, editor => {
            var indentLinesAction = new IndentLinesAction();
            editor.SetPosition(new TextPosition(1, 1));

            ExecuteAction(indentLinesAction, editor);
            AssertDeepStrictEqual(model.GetLineContent(1), "\tSome text");
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 2, 1, 2));
        });

        model.Dispose();
    }


    [Fact]
    public void IndentingOnEmptyLineShouldMoveCursor()
    {
        var model = TestTextModel.Create("");

        WithTestCodeEditor(model, new TestCodeEditorOptions { EditorOptions = EditorOptions.Default with { UseTabStops = false } }, editor => {
            var indentLinesAction = new IndentLinesAction();
            editor.SetPosition(new TextPosition(1, 1));

            ExecuteAction(indentLinesAction, editor);
            AssertDeepStrictEqual(model.GetLineContent(1), "    ");
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 5, 1, 5));
        });

        model.Dispose();
    }


    [Fact]
    public void Issue62112DeleteLineDoesNotWorkProperlyWhenMultipleCursorsAreOnLine()
    {
        string[] TEXT = [
            "a",
            "foo boo",
            "too",
            "c",
        ];
        WithTestCodeEditor(TEXT, null, editor => {
            editor.SetSelections([
                new TextSelection(2, 4, 2, 4),
                new TextSelection(2, 8, 2, 8),
                new TextSelection(3, 4, 3, 4),
            ]);
            var deleteLinesAction = new DeleteLinesAction();
            ExecuteAction(deleteLinesAction, editor);

            AssertDeepStrictEqual(editor.GetValue(), "a\nc");
        });
    }


    private static void TestDeleteLinesCommand(string[] initialText, TextSelection _initialSelections, string[] resultingText, TextSelection _resultingSelections) =>
        TestDeleteLinesCommand(initialText, [_initialSelections], resultingText, [_resultingSelections]);

    private static void TestDeleteLinesCommand(string[] initialText, TextSelection[] initialSelections, string[] resultingText, TextSelection[] resultingSelections)
    {
        WithTestCodeEditor(initialText, null, editor => {
            editor.SetSelections(initialSelections);
            var deleteLinesAction = new DeleteLinesAction();
            ExecuteAction(deleteLinesAction, editor);

            AssertDeepStrictEqual(editor.GetValue(), string.Join("\n", resultingText));
            AssertDeepStrictEqual(editor.GetSelections(), resultingSelections);
        });
    }

    [Fact]
    public void EmptySelectionInMiddleOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 3, 2, 3),
            [
                "first",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 3, 2, 3)
        );
    }


    [Fact]
    public void EmptySelectionAtTopOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5),
            [
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void EmptySelectionAtEndOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 2, 5, 2),
            [
                "first",
                "second line",
                "third line",
                "fourth line"
            ],
            new TextSelection(4, 2, 4, 2)
        );
    }


    [Fact]
    public void WithSelectionInMiddleOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 3, 2, 2),
            [
                "first",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 2, 2, 2)
        );
    }


    [Fact]
    public void WithSelectionAtTopOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 4, 1, 5),
            [
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void WithSelectionAtEndOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 1, 5, 2),
            [
                "first",
                "second line",
                "third line",
                "fourth line"
            ],
            new TextSelection(4, 2, 4, 2)
        );
    }


    [Fact]
    public void WithFullLineSelectionInMiddleOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(4, 1, 2, 1),
            [
                "first",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 2, 1)
        );
    }


    [Fact]
    public void WithFullLineSelectionAtTopOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 1, 5),
            [
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void WithFullLineSelectionAtEndOfLines()
    {
        TestDeleteLinesCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(4, 1, 5, 2),
            [
                "first",
                "second line",
                "third line"
            ],
            new TextSelection(3, 2, 3, 2)
        );
    }


    [Fact]
    public void Multicursor1()
    {
        TestDeleteLinesCommand(
            [
                "class P {",
                "",
                "    getA() {",
                "        if (true) {",
                "            return \"a\";",
                "        }",
                "    }",
                "",
                "    getB() {",
                "        if (true) {",
                "            return \"b\";",
                "        }",
                "    }",
                "",
                "    getC() {",
                "        if (true) {",
                "            return \"c\";",
                "        }",
                "    }",
                "}",
            ],
            [
                new TextSelection(4, 1, 5, 1),
                new TextSelection(10, 1, 11, 1),
                new TextSelection(16, 1, 17, 1),
            ],
            [
                "class P {",
                "",
                "    getA() {",
                "            return \"a\";",
                "        }",
                "    }",
                "",
                "    getB() {",
                "            return \"b\";",
                "        }",
                "    }",
                "",
                "    getC() {",
                "            return \"c\";",
                "        }",
                "    }",
                "}",
            ],
            [
                new TextSelection(4, 1, 4, 1),
                new TextSelection(9, 1, 9, 1),
                new TextSelection(14, 1, 14, 1),
            ]
        );
    }

}

