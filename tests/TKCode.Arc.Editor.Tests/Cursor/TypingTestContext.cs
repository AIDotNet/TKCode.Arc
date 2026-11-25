// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts: the setup of suite 'Editor Controller' with
// setupOnEnterLanguage, setupIndentRulesLanguage, setupAutoClosingLanguage, setupAutoClosingLanguageTokenization,
// setAutoClosingLanguageEnabledSet, createTextModel, usingCursor, extractAutoClosingSpecialColumns and assertType;
// commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - there is no language service: a language is a configuration registered with the context's
//    LanguageConfigurationService (a model's codec knows its language);
//  - tokenization registries are per model here, so setupAutoClosingLanguageTokenization marks the auto-closing language
//    as tokenized and the models created for it afterwards get the tokenizer registered in theirs;
//  - withTestCodeEditor and usingCursor return an OperationsTestEditor (the test disposes it).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using Shouldly;

namespace TKCode.Arc.Editor.Tests;

/// <summary>The languages and helpers of upstream's 'Editor Controller' suite.</summary>
internal sealed class TypingTestContext : IDisposable
{
    public const string SurroundingLanguageId = "surroundingLanguage";
    public const string IndentRulesLanguageId = "indentRulesLanguage";
    public const string ElectricCharLanguageId = "electricCharLanguage";
    public const string AutoClosingLanguageId = "autoClosingLanguage";
    public const string EmptyClosingSurroundLanguageId = "emptyClosingSurroundLanguage";

    private readonly List<IDisposable> _disposables = new();
    private bool _autoClosingLanguageIsTokenized;

    public TypingTestContext()
    {
        Add(LanguageConfigurationService.Register(SurroundingLanguageId, new LanguageConfiguration
        {
            AutoClosingPairs = [new AutoClosingPairConditional("(", ")")],
        }));

        Add(LanguageConfigurationService.Register(EmptyClosingSurroundLanguageId, new LanguageConfiguration
        {
            SurroundingPairs = [new AutoClosingPair("<", "")],
        }));

        SetupIndentRulesLanguage(IndentRulesLanguageId, new IndentationRule(
            DecreaseIndentPattern: JsRegExp.Of(@"^\s*((?!\S.*\/[*]).*[*]\/\s*)?[})\]]|^\s*(case\b.*|default):\s*(\/\/.*|\/[*].*[*]\/\s*)?$"),
            IncreaseIndentPattern: JsRegExp.Of(@"^((?!\/\/).)*(\{[^}""'`]*|\([^)""']*|\[[^\]""']*|^\s*(\{\}|\(\)|\[\]|(case\b.*|default):))\s*(\/\/.*|\/[*].*[*]\/\s*)?$"))
        {
            IndentNextLinePattern = JsRegExp.Of(@"^\s*(for|while|if|else)\b(?!.*[;{}]\s*(\/\/.*|\/[*].*[*]\/\s*)?$)"),
            UnIndentedLinePattern = JsRegExp.Of(@"^(?!.*([;{}]|\S:)\s*(\/\/.*|\/[*].*[*]\/\s*)?$)(?!.*(\{[^}""']*|\([^)""']*|\[[^\]""']*|^\s*(\{\}|\(\)|\[\]|(case\b.*|default):))\s*(\/\/.*|\/[*].*[*]\/\s*)?$)(?!^\s*((?!\S.*\/[*]).*[*]\/\s*)?[})\]]|^\s*(case\b.*|default):\s*(\/\/.*|\/[*].*[*]\/\s*)?$)(?!^\s*(for|while|if|else)\b(?!.*[;{}]\s*(\/\/.*|\/[*].*[*]\/\s*)?$))"),
        });

        Add(LanguageConfigurationService.Register(ElectricCharLanguageId, new LanguageConfiguration
        {
            ElectricCharacterSupport = new ElectricCharacterSupportOptions { DocComment = new DocComment("/**") { Close = " */" } },
            Brackets = [("{", "}"), ("[", "]"), ("(", ")")],
        }));

        SetupAutoClosingLanguage();
    }

    public LanguageConfigurationService LanguageConfigurationService { get; } = new();

    public void Add(IDisposable disposable) => _disposables.Add(disposable);

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }
        LanguageConfigurationService.Dispose();
    }

    public string SetupOnEnterLanguage(IndentAction indentAction)
    {
        const string onEnterLanguageId = "onEnterMode";

        Add(LanguageConfigurationService.Register(onEnterLanguageId, new LanguageConfiguration
        {
            OnEnterRules = [new OnEnterRule(JsRegExp.Of(".*"), new EnterAction(indentAction))],
        }));
        return onEnterLanguageId;
    }

    public string SetupIndentRulesLanguage(string languageId, IndentationRule indentationRules)
    {
        Add(LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            IndentationRules = indentationRules,
        }));
        return languageId;
    }

    private void SetupAutoClosingLanguage()
    {
        Add(LanguageConfigurationService.Register(AutoClosingLanguageId, new LanguageConfiguration
        {
            Comments = new CommentRule { BlockComment = ("/*", "*/") },
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("{", "}"),
                new AutoClosingPairConditional("[", "]"),
                new AutoClosingPairConditional("(", ")"),
                new AutoClosingPairConditional("'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("\"", "\"") { NotIn = ["string"] },
                new AutoClosingPairConditional("`", "`") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("/**", " */") { NotIn = ["string"] },
                new AutoClosingPairConditional("begin", "end") { NotIn = ["string"] },
            ],
            ElectricCharacterSupport = new ElectricCharacterSupportOptions { DocComment = new DocComment("/**") { Close = " */" } },
        }));
    }

    /// <summary>Models of the auto-closing language created from now on get its tokenizer.</summary>
    public void SetupAutoClosingLanguageTokenization() => _autoClosingLanguageIsTokenized = true;

    public void SetAutoClosingLanguageEnabledSet(string chars)
    {
        Add(LanguageConfigurationService.Register(AutoClosingLanguageId, new LanguageConfiguration
        {
            AutoCloseBefore = chars,
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("{", "}"),
                new AutoClosingPairConditional("[", "]"),
                new AutoClosingPairConditional("(", ")"),
                new AutoClosingPairConditional("'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("\"", "\"") { NotIn = ["string"] },
                new AutoClosingPairConditional("`", "`") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("/**", " */") { NotIn = ["string"] },
            ],
        }));
    }

    /// <summary>Upstream's createTextModel + withTestCodeEditor (with focus).</summary>
    public OperationsTestEditor CreateEditor(
        string text,
        string? languageId = null,
        TextModelCreationOptions? modelOpts = null,
        EditorOptions? editorOpts = null)
    {
        var editor = OperationsTestEditor.Create(text, editorOpts, languageId, LanguageConfigurationService, modelOpts ?? TextModel.DefaultCreationOptions);
        if (_autoClosingLanguageIsTokenized && languageId == AutoClosingLanguageId)
        {
            AutoClosingLanguageTokenizer.Register(editor.Model, AutoClosingLanguageId);
        }
        return editor;
    }

    /// <summary>Upstream's usingCursor: the lines joined with \n.</summary>
    public OperationsTestEditor UsingCursor(
        string[] text,
        string? languageId = null,
        TextModelCreationOptions? modelOpts = null,
        EditorOptions? editorOpts = null) =>
        CreateEditor(string.Join('\n', text), languageId, modelOpts, editorOpts);

    /// <summary>Upstream's model options merged into the defaults (IRelaxedTextModelCreationOptions).</summary>
    public static TextModelCreationOptions ModelOpts(
        bool? insertSpaces = null,
        int? tabSize = null,
        int? indentSize = null,
        bool? trimAutoWhitespace = null,
        bool? detectIndentation = null)
    {
        var options = TextModel.DefaultCreationOptions;
        if (insertSpaces is { } i)
        {
            options = options with { InsertSpaces = i };
        }
        if (tabSize is { } t)
        {
            options = options with { TabSize = t };
        }
        if (indentSize is { } s)
        {
            options = options with { IndentSize = s };
        }
        if (trimAutoWhitespace is { } w)
        {
            options = options with { TrimAutoWhitespace = w };
        }
        if (detectIndentation is { } d)
        {
            options = options with { DetectIndentation = d };
        }
        return options;
    }

    public enum AutoClosingColumnType
    {
        Normal = 0,
        Special1 = 1,
        Special2 = 2,
    }

    public static AutoClosingColumnType[] ExtractAutoClosingSpecialColumns(int maxColumn, string annotatedLine)
    {
        var result = new AutoClosingColumnType[maxColumn + 1];
        for (var j = 1; j <= maxColumn; j++)
        {
            result[j] = AutoClosingColumnType.Normal;
        }
        var column = 1;
        for (var j = 0; j < annotatedLine.Length; j++)
        {
            if (annotatedLine[j] == '|')
            {
                result[column] = AutoClosingColumnType.Special1;
            }
            else if (annotatedLine[j] == '!')
            {
                result[column] = AutoClosingColumnType.Special2;
            }
            else
            {
                column++;
            }
        }
        return result;
    }

    /// <summary>Upstream's assertType: move, type <paramref name="chr"/> from the keyboard, check the line, undo.</summary>
    public static async Task AssertType(
        OperationsTestEditor editor,
        int lineNumber,
        int column,
        string chr,
        string expectedInsert,
        string message)
    {
        var model = editor.Model;
        var lineContent = model.GetLineContent(lineNumber);
        var expected = JsSubstr(lineContent, 0, column - 1) + expectedInsert + JsSubstr(lineContent, column - 1);
        editor.MoveTo(lineNumber, column);
        editor.Type(chr, OperationsTestEditor.Keyboard);
        model.GetLineContent(lineNumber).ShouldBe(expected, message);
        await model.Undo();
    }

    // String.prototype.substr
    private static string JsSubstr(string str, int start, int? length = null)
    {
        start = Math.Clamp(start, 0, str.Length);
        var len = Math.Clamp(length ?? str.Length - start, 0, str.Length - start);
        return str.Substring(start, len);
    }
}

/// <summary>The tokenizer of upstream's setupAutoClosingLanguageTokenization: strings, block and line comments, braces.</summary>
internal static class AutoClosingLanguageTokenizer
{
    public static void Register(TextModel model, string languageId)
    {
        var encodedLanguageId = (uint)model.Tokenization.LanguageIdCodec.EncodeLanguageId(languageId);
        TestTokenizationSupports.Register(model, new RecordStateTokenizationSupport<State>(
            new BaseState(null),
            (line, state) => Tokenize(line, state, encodedLanguageId)));
    }

    private abstract record State;

    private sealed record BaseState(State? Parent) : State;

    private sealed record StringState(char Char, State ParentState) : State;

    private sealed record BlockCommentState(State ParentState) : State;

    private static (uint[] Tokens, State EndState) Tokenize(string line, State state, uint encodedLanguageId)
    {
        var tokens = new List<(int Length, StandardTokenType Type)>();

        void GenerateToken(int length, StandardTokenType type, State? newState = null)
        {
            if (tokens.Count > 0 && tokens[^1].Type == type)
            {
                // grow last tokens
                tokens[^1] = (tokens[^1].Length + length, type);
            }
            else
            {
                tokens.Add((length, type));
            }
            line = line.Substring(Math.Min(length, line.Length));
            if (newState is not null)
            {
                state = newState;
            }
        }

        int MatchLength(Func<char, bool> isMatch)
        {
            var n = 0;
            while (n < line.Length && isMatch(line[n]))
            {
                n++;
            }
            return n;
        }

        while (line.Length > 0)
        {
            switch (state)
            {
                case BaseState baseState:
                {
                    var m1 = MatchLength(static c => c is not ('\'' or '"' or '`' or '{' or '}' or '/'));
                    if (m1 > 0)
                    {
                        GenerateToken(m1, StandardTokenType.Other);
                    }
                    else if (line[0] is '\'' or '"' or '`')
                    {
                        GenerateToken(1, StandardTokenType.String, new StringState(line[0], baseState));
                    }
                    else if (line[0] == '{')
                    {
                        GenerateToken(1, StandardTokenType.Other, new BaseState(baseState));
                    }
                    else if (line[0] == '}')
                    {
                        GenerateToken(1, StandardTokenType.Other, baseState.Parent ?? new BaseState(null));
                    }
                    else if (line.StartsWith("//", StringComparison.Ordinal))
                    {
                        GenerateToken(line.Length, StandardTokenType.Comment, baseState);
                    }
                    else if (line.StartsWith("/*", StringComparison.Ordinal))
                    {
                        GenerateToken(2, StandardTokenType.Comment, new BlockCommentState(baseState));
                    }
                    else
                    {
                        GenerateToken(1, StandardTokenType.Other, baseState);
                    }
                    break;
                }
                case StringState stringState:
                {
                    var m1 = MatchLength(static c => c is not ('\\' or '\'' or '"' or '`' or '$'));
                    if (m1 > 0)
                    {
                        GenerateToken(m1, StandardTokenType.String);
                    }
                    else if (line[0] == '\\')
                    {
                        GenerateToken(2, StandardTokenType.String);
                    }
                    else if (line[0] == stringState.Char)
                    {
                        GenerateToken(1, StandardTokenType.String, stringState.ParentState);
                    }
                    else if (line.StartsWith("${", StringComparison.Ordinal))
                    {
                        GenerateToken(2, StandardTokenType.Other, new BaseState(stringState));
                    }
                    else
                    {
                        GenerateToken(1, StandardTokenType.Other, stringState);
                    }
                    break;
                }
                case BlockCommentState blockCommentState:
                {
                    var m1 = MatchLength(static c => c != '*');
                    if (m1 > 0)
                    {
                        GenerateToken(m1, StandardTokenType.String);
                    }
                    else if (line.StartsWith("*/", StringComparison.Ordinal))
                    {
                        GenerateToken(2, StandardTokenType.Comment, blockCommentState.ParentState);
                    }
                    else
                    {
                        GenerateToken(1, StandardTokenType.Other, blockCommentState);
                    }
                    break;
                }
                default:
                    throw new InvalidOperationException("unknown state");
            }
        }

        var result = new uint[tokens.Count * 2];
        var startIndex = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            result[2 * i] = (uint)startIndex;
            result[(2 * i) + 1] =
                (encodedLanguageId << MetadataConsts.LanguageIdOffset)
                | ((uint)tokens[i].Type << MetadataConsts.TokenTypeOffset);
            startIndex += tokens[i].Length;
        }
        return (result, state);
    }
}
