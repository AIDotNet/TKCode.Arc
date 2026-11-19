using System.Collections.Immutable;
using System.Text;
using StreamMark.Core.Highlighting;
using StreamMark.Highlighting.TextMate;
using Xunit;

namespace StreamMark.Avalonia.Tests.Highlighting;

public sealed class TextMateHighlighterTests
{
    private static readonly TextMateSyntaxHighlighter Shared = new();

    private const string CSharpSample = """
        using System;

        namespace Demo;

        /* block comment
           still comment */
        public sealed class Greeter
        {
            private readonly string _name = "world";

            public void Greet(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    Console.WriteLine($"Hello, {_name}! #{i}"); // trailing comment
                }
            }
        }
        """;

    // ---------- 工具 ----------

    private static uint ColorAt(IReadOnlyList<ImmutableArray<HighlightToken>> lines, int line, int column)
    {
        foreach (HighlightToken token in lines[line])
        {
            if (column >= token.Start && column < token.Start + token.Length)
            {
                return token.Foreground;
            }
        }

        return 0;
    }

    private static uint ColorOf(IReadOnlyList<ImmutableArray<HighlightToken>> lines, string code, string needle, int occurrence = 0)
    {
        string[] split = code.Split('\n');
        int seen = 0;
        for (int i = 0; i < split.Length; i++)
        {
            int index = -1;
            while ((index = split[i].IndexOf(needle, index + 1, StringComparison.Ordinal)) >= 0)
            {
                if (seen++ == occurrence)
                {
                    return ColorAt(lines, i, index);
                }
            }
        }

        throw new InvalidOperationException($"'{needle}' not found");
    }

    private static IReadOnlyList<ImmutableArray<HighlightToken>> Highlight(string language, string code,
        HighlightThemeKind theme = HighlightThemeKind.Dark)
    {
        using IHighlightSession session = Shared.CreateSession(language, theme);
        return session.Update(code);
    }

    private static void AssertSameTokens(IReadOnlyList<ImmutableArray<HighlightToken>> expected,
        IReadOnlyList<ImmutableArray<HighlightToken>> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].SequenceEqual(actual[i]), $"line {i} differs");
        }
    }

    private static void AssertWellFormed(string code, IReadOnlyList<ImmutableArray<HighlightToken>> lines)
    {
        string[] split = code.Split('\n');
        Assert.Equal(split.Length, lines.Count);
        for (int i = 0; i < split.Length; i++)
        {
            int length = split[i].EndsWith('\r') ? split[i].Length - 1 : split[i].Length;
            int pos = 0;
            foreach (HighlightToken token in lines[i])
            {
                Assert.True(token.Start >= pos, $"line {i}: overlapping/unsorted tokens");
                Assert.True(token.Length > 0);
                pos = token.Start + token.Length;
            }

            Assert.True(pos <= length, $"line {i}: token past end of line");
        }
    }

    private static TextMateHighlightSession Internal(IHighlightSession session) => Assert.IsType<TextMateHighlightSession>(session);

    // ---------- 正确性 ----------

    [Fact]
    public void CSharp_KeywordsStringsCommentsAndIdentifiersGetDistinctColors()
    {
        var lines = Highlight("csharp", CSharpSample);
        AssertWellFormed(CSharpSample, lines);

        uint keyword = ColorOf(lines, CSharpSample, "public");
        uint identifier = ColorOf(lines, CSharpSample, "count)"); // 参数名
        uint str = ColorOf(lines, CSharpSample, "\"world\"");
        uint comment = ColorOf(lines, CSharpSample, "// trailing");
        uint blockComment = ColorOf(lines, CSharpSample, "still comment");

        Assert.NotEqual(0u, keyword);
        Assert.NotEqual(0u, str);
        Assert.NotEqual(0u, comment);
        Assert.NotEqual(keyword, identifier);
        Assert.NotEqual(keyword, str);
        Assert.NotEqual(str, comment);
        Assert.Equal(comment, blockComment); // 多行块注释的状态跨行延续
        Assert.Equal(keyword, ColorOf(lines, CSharpSample, "int "));
    }

    [Fact]
    public void Json_KeysStringsAndNumbersAreColored()
    {
        const string json = """
            {
              "name": "stream",
              "count": 42,
              "ok": true
            }
            """;
        var lines = Highlight("json", json);
        AssertWellFormed(json, lines);

        uint key = ColorOf(lines, json, "\"name\"");
        uint value = ColorOf(lines, json, "\"stream\"");
        uint number = ColorOf(lines, json, "42");
        Assert.NotEqual(0u, key);
        Assert.NotEqual(0u, value);
        Assert.NotEqual(0u, number);
        Assert.NotEqual(key, value);
        Assert.NotEqual(value, number);
    }

    [Fact]
    public void Python_DefKeywordAndStringAreColored()
    {
        const string py = "def greet(name):\n    # comment\n    return f'hi {name}'\n";
        var lines = Highlight("py", py);
        AssertWellFormed(py, lines);

        uint def = ColorOf(lines, py, "def");
        uint str = ColorOf(lines, py, "'hi");
        uint comment = ColorOf(lines, py, "# comment");
        Assert.NotEqual(0u, def);
        Assert.NotEqual(0u, str);
        Assert.NotEqual(def, str);
        Assert.NotEqual(str, comment);
        Assert.Equal(4, lines.Count); // 末尾换行产生一个空的最后一行
        Assert.Empty(lines[3]);
    }

    [Fact]
    public void LightAndDarkThemesProduceDifferentColors()
    {
        var dark = Highlight("cs", CSharpSample, HighlightThemeKind.Dark);
        var light = Highlight("cs", CSharpSample, HighlightThemeKind.Light);
        Assert.NotEqual(ColorOf(dark, CSharpSample, "public"), ColorOf(light, CSharpSample, "public"));

        var (darkFg, darkBg) = Shared.GetThemeColors(HighlightThemeKind.Dark);
        var (lightFg, lightBg) = Shared.GetThemeColors(HighlightThemeKind.Light);
        Assert.Equal(0xFF1E1E1Eu, darkBg);
        Assert.Equal(0xFFFFFFFFu, lightBg);
        Assert.NotEqual(darkFg, lightFg);
        Assert.Equal(0xFFu, darkFg >> 24);
    }

    [Fact]
    public void CarriageReturnsAreExcludedAndDoNotChangeColors()
    {
        string lf = CSharpSample.Replace("\r\n", "\n");
        string crlf = lf.Replace("\n", "\r\n");
        var a = Highlight("cs", lf);
        var b = Highlight("cs", crlf);
        AssertWellFormed(crlf, b);
        AssertSameTokens(a, b);
    }

    [Fact]
    public void ExtremelyLongLineIsNotHighlighted_ButFollowingLinesAre()
    {
        string code = "var s = \"" + new string('x', 5000) + "\";\nint y = 1;";
        var lines = Highlight("cs", code);
        Assert.Empty(lines[0]);
        Assert.NotEqual(0u, ColorAt(lines, 1, 0));
    }

    // ---------- 语言解析 ----------

    [Theory]
    [InlineData("cs")] [InlineData("csharp")] [InlineData("C#")] [InlineData("js")] [InlineData("javascript")] [InlineData("jsx")]
    [InlineData("ts")] [InlineData("typescript")] [InlineData("tsx")] [InlineData("py")] [InlineData("python")]
    [InlineData("sh")] [InlineData("bash")] [InlineData("shell")] [InlineData("zsh")] [InlineData("console")]
    [InlineData("ps1")] [InlineData("powershell")] [InlineData("json")] [InlineData("jsonc")] [InlineData("yaml")] [InlineData("yml")]
    [InlineData("md")] [InlineData("markdown")] [InlineData("rs")] [InlineData("rust")] [InlineData("go")] [InlineData("java")]
    [InlineData("swift")] [InlineData("cpp")] [InlineData("c++")] [InlineData("cc")] [InlineData("hpp")] [InlineData("c")] [InlineData("h")]
    [InlineData("rb")] [InlineData("ruby")] [InlineData("php")] [InlineData("sql")] [InlineData("html")] [InlineData("xml")]
    [InlineData("svg")] [InlineData("xaml")] [InlineData("axaml")] [InlineData("css")] [InlineData("scss")] [InlineData("less")]
    [InlineData("dockerfile")] [InlineData("toml")] [InlineData("ini")] [InlineData("diff")] [InlineData("patch")] [InlineData("lua")]
    [InlineData("r")] [InlineData("dart")] [InlineData("fsharp")] [InlineData("vb")] [InlineData("bat")] [InlineData(" Python ")]
    [InlineData("{.python}")] [InlineData("language-rust")] [InlineData("objc")] [InlineData("makefile")]
    public void KnownLanguagesAndAliasesAreSupported(string language) => Assert.True(Shared.SupportsLanguage(language), language);

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")] [InlineData("klingon")] [InlineData("text")] [InlineData("plaintext")]
    [InlineData("txt")] [InlineData("kotlin")]
    public void UnknownLanguagesAreNotSupported(string? language) => Assert.False(Shared.SupportsLanguage(language));

    [Fact]
    public void UnknownLanguageSession_ReturnsEmptyTokensPerLine()
    {
        using IHighlightSession session = Shared.CreateSession("klingon", HighlightThemeKind.Dark);
        var lines = session.Update("a\nb\nc");
        Assert.Equal(3, lines.Count);
        Assert.All(lines, l => Assert.Empty(l));
        Assert.Equal(4, session.Update("a\nb\nc\n").Count);
        Assert.Equal(0, Internal(session).TokenizedLineCount);
    }

    // ---------- 增量 ----------

    [Fact]
    public void AppendingLines_OnlyTokenizesTheNewTail()
    {
        string[] sourceLines = CSharpSample.Split('\n');
        using IHighlightSession session = Shared.CreateSession("cs", HighlightThemeKind.Dark);
        TextMateHighlightSession inner = Internal(session);

        var sb = new StringBuilder();
        IReadOnlyList<ImmutableArray<HighlightToken>> previous = session.Update("");
        foreach (string line in sourceLines)
        {
            sb.Append(line).Append('\n');
            int before = inner.TokenizedLineCount;
            var current = session.Update(sb.ToString());

            // 只重新分词：上次的未完成行（现在是完整的新行）+ 新的空末行
            Assert.Equal(2, inner.TokenizedLineCount - before);

            // 前面完整行的片段数组原样复用
            for (int i = 0; i < previous.Count - 1; i++)
            {
                Assert.True(previous[i] == current[i], $"line {i} was re-tokenized");
            }

            previous = current;
        }

        AssertSameTokens(Highlight("cs", sb.ToString()), previous);
    }

    [Fact]
    public void StreamingInArbitraryChunks_MatchesOneShotHighlighting()
    {
        string code = CSharpSample + "\n" + CSharpSample;
        var expected = Highlight("cs", code);

        using IHighlightSession session = Shared.CreateSession("cs", HighlightThemeKind.Dark);
        TextMateHighlightSession inner = Internal(session);
        var random = new Random(1234);
        IReadOnlyList<ImmutableArray<HighlightToken>> result = [];
        int updates = 0;
        for (int pos = 0; pos < code.Length;)
        {
            pos = Math.Min(code.Length, pos + random.Next(1, 12));
            result = session.Update(code[..pos]);
            AssertWellFormed(code[..pos], result);
            updates++;
        }

        AssertSameTokens(expected, result);

        // 每次更新最多重新分词：未完成行 + 本次新到达的行；总量与行数 + 更新次数同阶，而不是行数 × 更新次数
        int lineCount = code.Split('\n').Length;
        Assert.True(inner.TokenizedLineCount <= lineCount + updates, $"tokenized {inner.TokenizedLineCount} lines");
    }

    [Fact]
    public void NonAppendEdit_StateChange_RetokenizesFollowingLines()
    {
        const string code = "int a = 1;\nint b = 2;\nint c = 3;\nint d = 4;";
        using IHighlightSession session = Shared.CreateSession("cs", HighlightThemeKind.Dark);
        var before = session.Update(code);
        uint keyword = ColorAt(before, 3, 0);

        // 在第 1 行开启块注释：之后所有行都应变成注释色
        string edited = code.Replace("int b = 2;", "int b = 2; /*");
        var after = session.Update(edited);
        uint comment = ColorAt(after, 1, "int b = 2; /*".Length - 1);
        Assert.NotEqual(keyword, comment);
        Assert.Equal(comment, ColorAt(after, 2, 0));
        Assert.Equal(comment, ColorAt(after, 3, 0));
        AssertSameTokens(Highlight("cs", edited), after);

        // 撤销：恢复原样
        var reverted = session.Update(code);
        AssertSameTokens(before, reverted);
        Assert.Equal(keyword, ColorAt(reverted, 3, 0));
    }

    [Fact]
    public void NonAppendEdit_WithoutStateChange_StopsEarlyAndReusesTail()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 200; i++)
        {
            sb.Append("    var value").Append(i).Append(" = Compute(\"").Append(i).Append("\"); // note\n");
        }

        string code = sb.ToString();
        using IHighlightSession session = Shared.CreateSession("cs", HighlightThemeKind.Dark);
        TextMateHighlightSession inner = Internal(session);
        session.Update(code);

        // 改中间一行的标识符（不改变语法状态），且整体长度变化：后面的行应整体复用（偏移平移）
        string edited = code.Replace("var value100 =", "var renamedValue100 =");
        int before = inner.TokenizedLineCount;
        var result = session.Update(edited);
        Assert.True(inner.TokenizedLineCount - before <= 2, $"re-tokenized {inner.TokenizedLineCount - before} lines");
        AssertSameTokens(Highlight("cs", edited), result);

        // 删除若干行（同样不影响状态）
        string deleted = edited.Remove(edited.IndexOf("    var value50 ", StringComparison.Ordinal),
            edited.IndexOf("    var value60 ", StringComparison.Ordinal) - edited.IndexOf("    var value50 ", StringComparison.Ordinal));
        before = inner.TokenizedLineCount;
        result = session.Update(deleted);
        Assert.True(inner.TokenizedLineCount - before <= 2, $"re-tokenized {inner.TokenizedLineCount - before} lines");
        AssertSameTokens(Highlight("cs", deleted), result);

        // 完全替换为别的文本
        result = session.Update("class X {}\n");
        AssertSameTokens(Highlight("cs", "class X {}\n"), result);
    }

    [Fact]
    public void SameText_ReturnsCachedResultWithoutWork()
    {
        using IHighlightSession session = Shared.CreateSession("cs", HighlightThemeKind.Dark);
        var a = session.Update(CSharpSample);
        int count = Internal(session).TokenizedLineCount;
        var b = session.Update(CSharpSample);
        Assert.Same(a, b);
        Assert.Equal(count, Internal(session).TokenizedLineCount);
    }

    // ---------- 线程安全 ----------

    [Fact]
    public async Task ConcurrentSessionCreationAndHighlighting_IsConsistent()
    {
        const string py = "import os\n\nclass A:\n    def run(self):\n        return os.path.join('a', \"b\")  # c\n";
        const string json = "{\"a\": [1, 2, {\"b\": null}], \"c\": \"d\"}\n";
        (string Language, string Code)[] inputs = [("cs", CSharpSample), ("python", py), ("json", json), ("ts", "const x: number = 1;\n")];

        // 期望值用另一个高亮器顺序计算
        var reference = new TextMateSyntaxHighlighter();
        var expected = new Dictionary<(string, HighlightThemeKind), IReadOnlyList<ImmutableArray<HighlightToken>>>();
        foreach (var (language, code) in inputs)
        {
            foreach (HighlightThemeKind theme in new[] { HighlightThemeKind.Dark, HighlightThemeKind.Light })
            {
                using IHighlightSession s = reference.CreateSession(language, theme);
                expected[(language, theme)] = s.Update(code);
            }
        }

        // 全新的高亮器：语法冷加载本身也在并发中发生
        var highlighter = new TextMateSyntaxHighlighter();
        highlighter.Prewarm("python");
        var tasks = Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var (language, code) = inputs[i % inputs.Length];
            HighlightThemeKind theme = (i / inputs.Length) % 2 == 0 ? HighlightThemeKind.Dark : HighlightThemeKind.Light;
            using IHighlightSession session = highlighter.CreateSession(language, theme);

            // 按行流式喂入，最大化交错
            IReadOnlyList<ImmutableArray<HighlightToken>> result = [];
            for (int pos = 0; pos <= code.Length; pos += 7)
            {
                result = session.Update(code[..Math.Min(pos, code.Length)]);
            }

            result = session.Update(code);
            return (language, theme, result);
        })).ToArray();

        foreach (var (language, theme, result) in await Task.WhenAll(tasks))
        {
            AssertSameTokens(expected[(language, theme)], result);
        }
    }

    [Fact]
    public async Task PrewarmAsync_LoadsGrammarSoFirstUpdateIsCheap()
    {
        var highlighter = new TextMateSyntaxHighlighter();
        await highlighter.PrewarmAsync(HighlightThemeKind.Dark, "rust", "klingon");
        using IHighlightSession session = highlighter.CreateSession("rs", HighlightThemeKind.Dark);
        var lines = session.Update("fn main() { let x = 1; }");
        Assert.True(Internal(session).HasGrammar);
        Assert.NotEqual(0u, ColorAt(lines, 0, 0));
    }
}
