// Ported from vscode (src/vs/editor/standalone/test/browser/monarch.test.ts, commit b57d5c9e), MIT license.
// Differences: the definitions are written with MonarchBuilder (regular expressions as their JavaScript source); the
// tokenizers get a TokenizationRegistry instance and a LanguageService instead of the global registry and the
// configuration service (maxTokenizationLineLength is a constructor argument).

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Standalone.Monarch;
using Xunit;
using static TKCode.Arc.Editor.Standalone.Monarch.MonarchBuilder;

namespace TKCode.Arc.Editor.Tests;

public sealed class MonarchTests
{
    private readonly TokenizationRegistry<ITokenizationSupport> _registry = new();
    private readonly LanguageService _languageService = new();

    private MonarchTokenizer CreateMonarchTokenizer(string languageId, MonarchLanguage language, int maxTokenizationLineLength = MonarchTokenizer.DefaultMaxTokenizationLineLength) =>
        new(_languageService, null!, _registry, languageId, MonarchCompile.Compile(languageId, language), maxTokenizationLineLength);

    private static List<IReadOnlyList<Token>> GetTokens(MonarchTokenizer tokenizer, string[] lines)
    {
        var actualTokens = new List<IReadOnlyList<Token>>();
        var state = tokenizer.GetInitialState();
        foreach (var line in lines)
        {
            var result = tokenizer.Tokenize(line, true, state);
            actualTokens.Add(result.Tokens);
            state = result.EndState;
        }

        return actualTokens;
    }

    private static void AssertTokens(List<IReadOnlyList<Token>> actual, Token[][] expected)
    {
        actual.Count.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            actual[i].ShouldBe(expected[i], $"line {i + 1}");
        }
    }

    private static MonarchStates States(params (string Name, MonarchLanguageRule[] Rules)[] states)
    {
        var result = new MonarchStates();
        foreach (var (name, rules) in states)
        {
            result[name] = rules;
        }

        return result;
    }

    private const string SqlQueryStart = "(SELECT|INSERT|UPDATE|DELETE|CREATE|REPLACE|ALTER|WITH)";

    private void RegisterSql()
    {
        _languageService.RegisterLanguage(new LanguageExtensionPoint("sql"));
        _registry.Register("sql", CreateMonarchTokenizer("sql", new MonarchLanguage
        {
            Tokenizer = States(("root", [R(".", "token")])),
        }));
    }

    private static readonly string[] SqlLines =
    [
        "mysql_query(\"\"\"SELECT * FROM table_name WHERE ds = '<DATEID>'\"\"\")",
        "mysql_query(\"\"\"",
        "SELECT *",
        "FROM table_name",
        "WHERE ds = '<DATEID>'",
        "\"\"\")",
    ];

    private static readonly Token[][] SqlExpected =
    [
        [
            new(0, "source.test1", "test1"),
            new(12, "string.quote.test1", "test1"),
            new(15, "token.sql", "sql"),
            new(61, "string.quote.test1", "test1"),
            new(64, "source.test1", "test1"),
        ],
        [
            new(0, "source.test1", "test1"),
            new(12, "string.quote.test1", "test1"),
        ],
        [new(0, "token.sql", "sql")],
        [new(0, "token.sql", "sql")],
        [new(0, "token.sql", "sql")],
        [
            new(0, "string.quote.test1", "test1"),
            new(3, "source.test1", "test1"),
        ],
    ];

    [Fact]
    public void Ensure_rematch_and_nextEmbedded_can_be_used_together_in_Monarch_grammar()
    {
        RegisterSql();
        var tokenizer = CreateMonarchTokenizer("test1", new MonarchLanguage
        {
            Tokenizer = States(
                ("root",
                [
                    R("(\"\"\")" + SqlQueryStart, Groups(Token("string.quote"), Token("@rematch", next: "@endStringWithSQL", nextEmbedded: "sql"))),
                    R("(\"\"\")$", Groups(Token("string.quote", next: "@maybeStringIsSQL"))),
                ]),
                ("maybeStringIsSQL",
                [
                    R("(.*)", Cases(
                        (SqlQueryStart + "\\b.*", Token("@rematch", next: "@endStringWithSQL", nextEmbedded: "sql")),
                        ("@default", Token("@rematch", switchTo: "@endDblDocString")))),
                ]),
                ("endDblDocString",
                [
                    R("[^']+", "string"),
                    R("\\\\'", "string"),
                    R("'''", "string", "@popall"),
                    R("'", "string"),
                ]),
                ("endStringWithSQL", [R("\"\"\"", Token("string.quote", next: "@popall", nextEmbedded: "@pop"))])),
        });

        AssertTokens(GetTokens(tokenizer, SqlLines), SqlExpected);
    }

    [Fact]
    public void Test_nextEmbedded_pop_in_cases_statement()
    {
        RegisterSql();
        var tokenizer = CreateMonarchTokenizer("test1", new MonarchLanguage
        {
            Tokenizer = States(
                ("root",
                [
                    R("(\"\"\")" + SqlQueryStart, Groups(Token("string.quote"), Token("@rematch", next: "@endStringWithSQL", nextEmbedded: "sql"))),
                    R("(\"\"\")$", Groups(Token("string.quote", next: "@maybeStringIsSQL"))),
                ]),
                ("maybeStringIsSQL",
                [
                    R("(.*)", Cases(
                        (SqlQueryStart + "\\b.*", Token("@rematch", next: "@endStringWithSQL", nextEmbedded: "sql")),
                        ("@default", Token("@rematch", switchTo: "@endDblDocString")))),
                ]),
                ("endDblDocString",
                [
                    R("[^']+", "string"),
                    R("\\\\'", "string"),
                    R("'''", "string", "@popall"),
                    R("'", "string"),
                ]),
                ("endStringWithSQL",
                [
                    R("\"\"\"", Cases(
                        ("\"\"\"", Cases(
                            ("", Token("string.quote", next: "@popall", nextEmbedded: "@pop")))),
                        ("@default", "")))
                ])),
        });

        AssertTokens(GetTokens(tokenizer, SqlLines), SqlExpected);
    }

    [Fact]
    public void Monaco_editor_1235_Empty_Line_Handling()
    {
        var tokenizer = CreateMonarchTokenizer("test", new MonarchLanguage
        {
            Tokenizer = States(
                ("root", [Include("@comments")]),
                ("comments",
                [
                    R("\\/\\/$", "comment"), // empty single-line comment
                    R("\\/\\/", "comment", "@comment_cpp"),
                ]),
                ("comment_cpp",
                [
                    R("(?:[^\\\\]|(?:\\\\.))+$", "comment", "@pop"),
                    R(".+$", "comment"),
                    R("$", "comment", "@pop"),
                    // No possible rule to detect an empty line and @pop?
                ])),
        });

        string[] lines =
        [
            "// This comment \\",
            "   continues on the following line",
            "",
            "// This comment does NOT continue \\\\",
            "   because the escape char was itself escaped",
            "",
            "// This comment DOES continue because \\\\\\",
            "   the 1st '\\' escapes the 2nd; the 3rd escapes EOL",
            "",
            "// This comment continues to the following line \\",
            "",
            "But the line was empty. This line should not be commented.",
        ];

        AssertTokens(GetTokens(tokenizer, lines),
        [
            [new(0, "comment.test", "test")],
            [new(0, "comment.test", "test")],
            [],
            [new(0, "comment.test", "test")],
            [new(0, "source.test", "test")],
            [],
            [new(0, "comment.test", "test")],
            [new(0, "comment.test", "test")],
            [],
            [new(0, "comment.test", "test")],
            [],
            [new(0, "source.test", "test")],
        ]);
    }

    [Fact]
    public void Monaco_editor_2265_Exit_a_state_at_end_of_line()
    {
        var tokenizer = CreateMonarchTokenizer("test", new MonarchLanguage
        {
            IncludeLF = true,
            Tokenizer = States(
                ("root",
                [
                    R("^\\*", "", "@inner"),
                    R("\\:\\*", "", "@inner"),
                    R("[^*:]+", "string"),
                    R("[*:]", "string"),
                ]),
                ("inner",
                [
                    R("\\n", "", "@pop"),
                    R("\\d+", "number"),
                    R("[^\\d]+", ""),
                ])),
        });

        string[] lines =
        [
            "PRINT 10 * 20",
            "*FX200, 3",
            "PRINT 2*3:*FX200, 3",
        ];

        AssertTokens(GetTokens(tokenizer, lines),
        [
            [new(0, "string.test", "test")],
            [
                new(0, string.Empty, "test"),
                new(3, "number.test", "test"),
                new(6, string.Empty, "test"),
                new(8, "number.test", "test"),
            ],
            [
                new(0, "string.test", "test"),
                new(9, string.Empty, "test"),
                new(13, "number.test", "test"),
                new(16, string.Empty, "test"),
                new(18, "number.test", "test"),
            ],
        ]);
    }

    [Fact]
    public void Issue_115662_monarchCompile_function_need_an_extra_option_which_can_control_replacement()
    {
        var attributes = new MonarchAttributes
        {
            ["uselessReplaceKey1"] = "@uselessReplaceKey2",
            ["uselessReplaceKey2"] = "@uselessReplaceKey3",
            ["uselessReplaceKey3"] = "@uselessReplaceKey4",
            ["uselessReplaceKey4"] = "@uselessReplaceKey5",
            ["uselessReplaceKey5"] = "@ham",
        };
        var tokenizer1 = CreateMonarchTokenizer("test", new MonarchLanguage
        {
            IgnoreCase = false,
            Attributes = attributes,
            // /@\w+/.test('@ham') is true, so upstream's regex is new RegExp('^@uselessReplaceKey1$')
            Tokenizer = States(("root", [Expanded("^@uselessReplaceKey1$", Token("ham"))])),
        });

        var tokenizer2 = CreateMonarchTokenizer("test", new MonarchLanguage
        {
            IgnoreCase = false,
            Tokenizer = States(("root", [Expanded("@@ham", Token("ham"))])),
        });

        string[] lines = ["@ham"];

        AssertTokens(GetTokens(tokenizer1, lines), [[new(0, "ham.test", "test")]]);
        AssertTokens(GetTokens(tokenizer2, lines), [[new(0, "ham.test", "test")]]);
    }

    [Fact]
    public void Monaco_editor_2424_Allow_to_target_at_at()
    {
        var tokenizer = CreateMonarchTokenizer("test", new MonarchLanguage
        {
            IgnoreCase = false,
            Tokenizer = States(("root", [Expanded("@@@@", Token("ham"))])),
        });

        AssertTokens(GetTokens(tokenizer, ["@@"]), [[new(0, "ham.test", "test")]]);
    }

    [Fact]
    public void Monaco_editor_3025_Check_maxTokenizationLineLength_before_tokenizing()
    {
        // Set maxTokenizationLineLength to 4 so that "ham" works but "hamham" would fail
        var tokenizer = CreateMonarchTokenizer("test", new MonarchLanguage
        {
            Tokenizer = States(("root", [Expanded("ham", Token("ham"))])),
        }, maxTokenizationLineLength: 4);

        string[] lines =
        [
            "ham", // length 3, should be tokenized
            "hamham", // length 6, should NOT be tokenized
        ];

        AssertTokens(GetTokens(tokenizer, lines),
        [
            [new(0, "ham.test", "test")],
            [new(0, string.Empty, "test")],
        ]);
    }

    private MonarchTokenizer CreateRawStringTokenizer() => CreateMonarchTokenizer("test", new MonarchLanguage
    {
        IgnoreCase = false,
        Attributes = new MonarchAttributes { ["encoding"] = MonarchValue.RegExp("u|u8|U|L") },
        Tokenizer = States(
            ("root",
            [
                // C++ 11 Raw String
                R(@"@encoding?R\""(?:([^ ()\\\t]*))\(", Token("string.raw.begin", next: "@raw.$1")),
            ]),
            ("raw",
            [
                R(@".*\)$S2\""", "string.raw", "@pop"),
                R(".*", "string.raw"),
            ])),
    });

    [Fact]
    public void Monaco_editor_3128_allow_state_access_within_rules()
    {
        var tokenizer = CreateRawStringTokenizer();

        string[] lines =
        [
            "int main(){",
            "",
            "\tauto s = R\"\"\"\"(",
            "\tHello World",
            "\t)\"\"\"\";",
            "",
            "\tstd::cout << \"hello\";",
            "",
            "}",
        ];

        AssertTokens(GetTokens(tokenizer, lines),
        [
            [new(0, "source.test", "test")],
            [],
            [new(0, "source.test", "test"), new(10, "string.raw.begin.test", "test")],
            [new(0, "string.raw.test", "test")],
            [new(0, "string.raw.test", "test"), new(6, "source.test", "test")],
            [],
            [new(0, "source.test", "test")],
            [],
            [new(0, "source.test", "test")],
        ]);
    }

    [Fact]
    public void Monaco_editor_4775_Raw_strings_in_cpp_can_break_monarch()
    {
        var tokenizer = CreateRawStringTokenizer();

        AssertTokens(GetTokens(tokenizer, ["R\"[())\""]),
        [
            [new(0, "string.raw.begin.test", "test"), new(4, "string.raw.test", "test")],
        ]);
    }
}
