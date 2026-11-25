// Ported from vscode (src/vs/editor/contrib/linesOperations/test/browser/moveLinesCommand.test.ts, commit b57d5c9e),
// MIT license. Differences from upstream: the IndentRulesMode and EnterRulesMode classes are registration functions
// (languages are ids here; the language service is not needed).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.LinesOperations;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class MoveLinesCommandTests
{
    private static void TestMoveLinesDownCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, LanguageConfigurationService? languageConfigurationService = null) =>
        TestMoveLinesUpOrDownCommand(false, lines, selection, expectedLines, expectedSelection, languageConfigurationService);

    private static void TestMoveLinesUpCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, LanguageConfigurationService? languageConfigurationService = null) =>
        TestMoveLinesUpOrDownCommand(true, lines, selection, expectedLines, expectedSelection, languageConfigurationService);

    private static void TestMoveLinesDownWithIndentCommand(string? languageId, string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, LanguageConfigurationService? languageConfigurationService = null) =>
        TestMoveLinesUpOrDownWithIndentCommand(false, languageId, lines, selection, expectedLines, expectedSelection, languageConfigurationService);

    private static void TestMoveLinesUpWithIndentCommand(string? languageId, string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, LanguageConfigurationService? languageConfigurationService = null) =>
        TestMoveLinesUpOrDownWithIndentCommand(true, languageId, lines, selection, expectedLines, expectedSelection, languageConfigurationService);

    private static void TestMoveLinesUpOrDownCommand(bool up, string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, LanguageConfigurationService? languageConfigurationService)
    {
        using var ownService = new LanguageConfigurationService();
        var service = languageConfigurationService ?? ownService;
        TestCommand.Run(lines, null, selection, sel => new MoveLinesCommand(sel, !up, EditorAutoIndentStrategy.Advanced, service), expectedLines, expectedSelection);
    }

    private static void TestMoveLinesUpOrDownWithIndentCommand(bool up, string? languageId, string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, LanguageConfigurationService? languageConfigurationService)
    {
        using var ownService = new LanguageConfigurationService();
        var service = languageConfigurationService ?? ownService;
        TestCommand.Run(lines, languageId, selection, sel => new MoveLinesCommand(sel, !up, EditorAutoIndentStrategy.Full, service), expectedLines, expectedSelection);
    }

    private static readonly IndentationRule s_indentRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^\s*((?!\S.*\/[*]).*[*]\/\s*)?[})\]]|^\s*(case\b.*|default):\s*(\/\/.*|\/[*].*[*]\/\s*)?$"),
        IncreaseIndentPattern: JsRegExp.Of(@"(\{[^}""'`]*|\([^)""']*|\[[^\]""']*|^\s*(\{\}|\(\)|\[\]|(case\b.*|default):))\s*(\/\/.*|\/[*].*[*]\/\s*)?$"))
    {
        IndentNextLinePattern = JsRegExp.Of(@"^\s*(for|while|if|else)\b(?!.*[;{}]\s*(\/\/.*|\/[*].*[*]\/\s*)?$)"),
        UnIndentedLinePattern = JsRegExp.Of(@"^(?!.*([;{}]|\S:)\s*(\/\/.*|\/[*].*[*]\/\s*)?$)(?!.*(\{[^}""']*|\([^)""']*|\[[^\]""']*|^\s*(\{\}|\(\)|\[\]|(case\b.*|default):))\s*(\/\/.*|\/[*].*[*]\/\s*)?$)(?!^\s*((?!\S.*\/[*]).*[*]\/\s*)?[})\]]|^\s*(case\b.*|default):\s*(\/\/.*|\/[*].*[*]\/\s*)?$)(?!^\s*(for|while|if|else)\b(?!.*[;{}]\s*(\/\/.*|\/[*].*[*]\/\s*)?$))"),
    };

    // IndentRulesMode
    private static string RegisterIndentRulesMode(LanguageConfigurationService languageConfigurationService)
    {
        const string languageId = "moveLinesIndentMode";
        languageConfigurationService.Register(languageId, new LanguageConfiguration { IndentationRules = s_indentRules });
        return languageId;
    }

    // EnterRulesMode
    private static string RegisterEnterRulesMode(LanguageConfigurationService languageConfigurationService)
    {
        const string languageId = "moveLinesEnterMode";
        languageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            IndentationRules = new IndentationRule(JsRegExp.Of(@"^\s*\[$"), JsRegExp.Of(@"^\s*\]$")),
            Brackets = [("{", "}")],
        });
        return languageId;
    }

    // suite: Editor Contrib - Move Lines Command


    [Fact]
    public void MoveFirstUpLastDownDisabled()
    {
        TestMoveLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 1)
        );

        TestMoveLinesDownCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 1, 5, 1),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 1, 5, 1)
        );
    }


    [Fact]
    public void MoveFirstLineDown()
    {
        TestMoveLinesDownCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 4, 1, 1),
            [
                "second line",
                "first",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 2, 1)
        );
    }


    [Fact]
    public void Move2ndLineUp()
    {
        TestMoveLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 2, 1),
            [
                "second line",
                "first",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }


    [Fact]
    public void Issue1322aMove2ndLineUp()
    {
        TestMoveLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 12, 2, 12),
            [
                "second line",
                "first",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 12, 1, 12)
        );
    }


    [Fact]
    public void Issue1322bMoveLastLineUp()
    {
        TestMoveLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 6, 5, 6),
            [
                "first",
                "second line",
                "third line",
                "fifth",
                "fourth line"
            ],
            new TextSelection(4, 6, 4, 6)
        );
    }


    [Fact]
    public void Issue1322cMoveLastLineSelectedUp()
    {
        TestMoveLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 6, 5, 1),
            [
                "first",
                "second line",
                "third line",
                "fifth",
                "fourth line"
            ],
            new TextSelection(4, 6, 4, 1)
        );
    }


    [Fact]
    public void MoveLastLineUp()
    {
        TestMoveLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 1, 5, 1),
            [
                "first",
                "second line",
                "third line",
                "fifth",
                "fourth line"
            ],
            new TextSelection(4, 1, 4, 1)
        );
    }


    [Fact]
    public void Move4thLineDown()
    {
        TestMoveLinesDownCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(4, 1, 4, 1),
            [
                "first",
                "second line",
                "third line",
                "fifth",
                "fourth line"
            ],
            new TextSelection(5, 1, 5, 1)
        );
    }


    [Fact]
    public void MoveMultipleLinesDown()
    {
        TestMoveLinesDownCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(4, 4, 2, 2),
            [
                "first",
                "fifth",
                "second line",
                "third line",
                "fourth line"
            ],
            new TextSelection(5, 4, 3, 2)
        );
    }


    [Fact]
    public void InvisibleSelectionIsIgnored()
    {
        TestMoveLinesDownCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 1, 1),
            [
                "second line",
                "first",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 1, 2, 1)
        );
    }



    // suite: Editor contrib - Move Lines Command honors Indentation Rules



    // https://github.com/microsoft/vscode/issues/28552#issuecomment-307862797
    [Fact]
    public void FirstLineIndentationAdjustTo0()
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        var mode = RegisterIndentRulesMode(languageConfigurationService);

        TestMoveLinesUpWithIndentCommand(
            mode,
            [
                "class X {",
                "\tz = 2",
                "}"
            ],
            new TextSelection(2, 1, 2, 1),
            [
                "z = 2",
                "class X {",
                "}"
            ],
            new TextSelection(1, 1, 1, 1),
            languageConfigurationService
        );
    }


    // https://github.com/microsoft/vscode/issues/28552#issuecomment-307867717
    [Fact]
    public void MoveLinesAcrossBlock()
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        var mode = RegisterIndentRulesMode(languageConfigurationService);

        TestMoveLinesDownWithIndentCommand(
            mode,
            [
                "const value = 2;",
                "const standardLanguageDescriptions = [",
                "    {",
                "        diagnosticSource: 'js',",
                "    }",
                "];"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "const standardLanguageDescriptions = [",
                "    const value = 2;",
                "    {",
                "        diagnosticSource: 'js',",
                "    }",
                "];"
            ],
            new TextSelection(2, 5, 2, 5),
            languageConfigurationService
        );
    }



    [Fact]
    public void MoveLineShouldStillWorkAsBeforeIfThereIsNoIndentationRules()
    {
        TestMoveLinesUpWithIndentCommand(
            null,
            [
                "if (true) {",
                "    var task = new Task(() => {",
                "        var work = 1234;",
                "    });",
                "}"
            ],
            new TextSelection(3, 1, 3, 1),
            [
                "if (true) {",
                "        var work = 1234;",
                "    var task = new Task(() => {",
                "    });",
                "}"
            ],
            new TextSelection(2, 1, 2, 1)
        );
    }



    // suite: Editor - contrib - Move Lines Command honors onEnter Rules


    [Fact]
    public void Issue54829MoveBlockAcrossBlock()
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        var mode = RegisterEnterRulesMode(languageConfigurationService);

        TestMoveLinesDownWithIndentCommand(
            mode,

            [
                "if (true) {",
                "    if (false) {",
                "        if (1) {",
                "            console.log('b');",
                "        }",
                "        console.log('a');",
                "    }",
                "}"
            ],
            new TextSelection(3, 9, 5, 10),
            [
                "if (true) {",
                "    if (false) {",
                "        console.log('a');",
                "        if (1) {",
                "            console.log('b');",
                "        }",
                "    }",
                "}"
            ],
            new TextSelection(4, 9, 6, 10),
            languageConfigurationService
        );
    }
}
