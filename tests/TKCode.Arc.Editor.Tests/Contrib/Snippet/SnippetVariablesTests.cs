// Ported from vscode (src/vs/editor/contrib/snippet/test/browser/snippetVariables.test.ts, commit b57d5c9e), MIT license.
// All 17 upstream tests, plus 2 of what upstream does not test (the comment variables; RANDOM, RANDOM_HEX and UUID).
// Differences: the label and workspace services are ISnippetLabelService / ISnippetWorkspaceService
// stand-ins; sinon's fake timers are a TimeProvider (a fixed clock in UTC, so the expected values do not depend on the
// machine's time zone); URI.parse is new Uri and URI.file('/x') is new Uri("file:///x").

using System.Globalization;
using Shouldly;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SnippetVariablesTests : IDisposable
{
    private sealed class FsPathLabelService : ISnippetLabelService
    {
        public string GetUriLabel(Uri uri, bool relative = false, bool noPrefix = false) => SnippetUris.FsPath(uri);
    }

    private static readonly ISnippetLabelService s_labelService = new FsPathLabelService();

    private readonly TextModel _model;
    private IVariableResolver _resolver;

    public SnippetVariablesTests()
    {
        _model = TestTextModel.Create(
            string.Join('\n', "this is line one", "this is line two", "    this is line three"),
            uri: new Uri("file:///foo/files/text.txt"));

        _resolver = new CompositeSnippetVariableResolver(
        [
            new ModelBasedVariableResolver(s_labelService, _model),
            new SelectionBasedVariableResolver(_model, new TextSelection(1, 1, 1, 1), 0, null),
        ]);
    }

    public void Dispose() => _model.Dispose();

    private static void AssertVariableResolve(IVariableResolver resolver, string varName, string? expected)
    {
        var snippet = new SnippetParser().Parse("$" + varName);
        var variable = (Variable)snippet.Children[0];
        variable.Resolve(resolver);
        if (variable.Children.Count == 0)
        {
            expected.ShouldBeNull();
        }
        else
        {
            variable.ToString().ShouldBe(expected);
        }
    }

    [Fact]
    public void Editor_variables_basics()
    {
        AssertVariableResolve(_resolver, "TM_FILENAME", "text.txt");
        AssertVariableResolve(_resolver, "something", null);
    }

    [Fact]
    public void Editor_variables_file_dir()
    {
        AssertVariableResolve(_resolver, "TM_FILENAME", "text.txt");
        if (!OperatingSystem.IsWindows())
        {
            AssertVariableResolve(_resolver, "TM_DIRECTORY", "/foo/files");
            AssertVariableResolve(_resolver, "TM_DIRECTORY_BASE", "files");
            AssertVariableResolve(_resolver, "TM_FILEPATH", "/foo/files/text.txt");
        }

        using (var model = TestTextModel.Create("", uri: new Uri("http://www.pb.o/abc/def/ghi")))
        {
            _resolver = new ModelBasedVariableResolver(s_labelService, model);
            AssertVariableResolve(_resolver, "TM_FILENAME", "ghi");
            if (!OperatingSystem.IsWindows())
            {
                AssertVariableResolve(_resolver, "TM_DIRECTORY", "/abc/def");
                AssertVariableResolve(_resolver, "TM_DIRECTORY_BASE", "def");
                AssertVariableResolve(_resolver, "TM_FILEPATH", "/abc/def/ghi");
            }
        }

        using (var model = TestTextModel.Create("", uri: new Uri("mem:fff.ts")))
        {
            _resolver = new ModelBasedVariableResolver(s_labelService, model);
            AssertVariableResolve(_resolver, "TM_DIRECTORY", "");
            AssertVariableResolve(_resolver, "TM_DIRECTORY_BASE", "");
            AssertVariableResolve(_resolver, "TM_FILEPATH", "fff.ts");
        }
    }

    private sealed class PipeLabelService : ISnippetLabelService
    {
        public string GetUriLabel(Uri uri, bool relative = false, bool noPrefix = false) =>
            SnippetUris.FsPath(uri).Replace('/', '|').Replace('\\', '|');
    }

    [Fact]
    public void Path_delimiters_in_code_snippet_variables_arent_specific_to_remote_OS_76840()
    {
        using var model = TestTextModel.Create("", uri: new Uri("foo:///foo/files/text.txt"));
        var resolver = new CompositeSnippetVariableResolver([new ModelBasedVariableResolver(new PipeLabelService(), model)]);
        AssertVariableResolve(resolver, "TM_FILEPATH", "|foo|files|text.txt");
    }

    [Fact]
    public void Editor_variables_selection()
    {
        _resolver = new SelectionBasedVariableResolver(_model, new TextSelection(1, 2, 2, 3), 0, null);
        AssertVariableResolve(_resolver, "TM_SELECTED_TEXT", "his is line one\nth");
        AssertVariableResolve(_resolver, "TM_CURRENT_LINE", "this is line two");
        AssertVariableResolve(_resolver, "TM_LINE_INDEX", "1");
        AssertVariableResolve(_resolver, "TM_LINE_NUMBER", "2");
        AssertVariableResolve(_resolver, "CURSOR_INDEX", "0");
        AssertVariableResolve(_resolver, "CURSOR_NUMBER", "1");

        _resolver = new SelectionBasedVariableResolver(_model, new TextSelection(1, 2, 2, 3), 4, null);
        AssertVariableResolve(_resolver, "CURSOR_INDEX", "4");
        AssertVariableResolve(_resolver, "CURSOR_NUMBER", "5");

        _resolver = new SelectionBasedVariableResolver(_model, new TextSelection(2, 3, 1, 2), 0, null);
        AssertVariableResolve(_resolver, "TM_SELECTED_TEXT", "his is line one\nth");
        AssertVariableResolve(_resolver, "TM_CURRENT_LINE", "this is line one");
        AssertVariableResolve(_resolver, "TM_LINE_INDEX", "0");
        AssertVariableResolve(_resolver, "TM_LINE_NUMBER", "1");

        _resolver = new SelectionBasedVariableResolver(_model, new TextSelection(1, 2, 1, 2), 0, null);
        AssertVariableResolve(_resolver, "TM_SELECTED_TEXT", null);

        AssertVariableResolve(_resolver, "TM_CURRENT_WORD", "this");

        _resolver = new SelectionBasedVariableResolver(_model, new TextSelection(3, 1, 3, 1), 0, null);
        AssertVariableResolve(_resolver, "TM_CURRENT_WORD", null);
    }

    [Fact]
    public void TextmateSnippet_resolve_variable()
    {
        var snippet = new SnippetParser().Parse("\"$TM_CURRENT_WORD\"", true);
        snippet.ToString().ShouldBe("\"\"");
        snippet.ResolveVariables(_resolver);
        snippet.ToString().ShouldBe("\"this\"");
    }

    [Fact]
    public void TextmateSnippet_resolve_variable_with_default()
    {
        var snippet = new SnippetParser().Parse("\"${TM_CURRENT_WORD:foo}\"", true);
        snippet.ToString().ShouldBe("\"foo\"");
        snippet.ResolveVariables(_resolver);
        snippet.ToString().ShouldBe("\"this\"");
    }

    [Fact]
    public void More_useful_environment_variables_for_snippets_32737()
    {
        AssertVariableResolve(_resolver, "TM_FILENAME_BASE", "text");

        using (var model = TestTextModel.Create("", uri: new Uri("http://www.pb.o/abc/def/ghi")))
        {
            AssertVariableResolve(new ModelBasedVariableResolver(s_labelService, model), "TM_FILENAME_BASE", "ghi");
        }

        using (var model = TestTextModel.Create("", uri: new Uri("mem:.git")))
        {
            AssertVariableResolve(new ModelBasedVariableResolver(s_labelService, model), "TM_FILENAME_BASE", ".git");
        }

        using (var model = TestTextModel.Create("", uri: new Uri("mem:foo.")))
        {
            AssertVariableResolve(new ModelBasedVariableResolver(s_labelService, model), "TM_FILENAME_BASE", "foo");
        }
    }

    private sealed class FuncResolver(Func<Variable, string?> resolve) : IVariableResolver
    {
        public string? Resolve(Variable variable) => resolve(variable);
    }

    private static void AssertVariableResolve2(string input, string expected, string? varValue = null)
    {
        var snippet = new SnippetParser().Parse(input)
            .ResolveVariables(new FuncResolver(variable => string.IsNullOrEmpty(varValue) ? variable.Name : varValue));
        snippet.ToString().ShouldBe(expected);
    }

    [Fact]
    public void Variable_snippet_transform()
    {
        var snippet = new SnippetParser().Parse("name=${TM_FILENAME/(.*)\\..+$/$1/}", true);
        snippet.ResolveVariables(_resolver);
        snippet.ToString().ShouldBe("name=text");

        AssertVariableResolve2("${ThisIsAVar/([A-Z]).*(Var)/$2/}", "Var");
        AssertVariableResolve2("${ThisIsAVar/([A-Z]).*(Var)/$2-${1:/downcase}/}", "Var-t");
        AssertVariableResolve2("${Foo/(.*)/${1:+Bar}/img}", "Bar");

        //https://github.com/microsoft/vscode/issues/33162
        AssertVariableResolve2("export default class ${TM_FILENAME/(\\w+)\\.js/$1/g}", "export default class FooFile", "FooFile.js");

        AssertVariableResolve2("${foobarfoobar/(foo)/${1:+FAR}/g}", "FARbarFARbar"); // global
        AssertVariableResolve2("${foobarfoobar/(foo)/${1:+FAR}/}", "FARbarfoobar"); // first match
        AssertVariableResolve2("${foobarfoobar/(bazz)/${1:+FAR}/g}", "foobarfoobar"); // no match, no else

        AssertVariableResolve2("${foobarfoobar/(foo)/${2:+FAR}/g}", "barbar"); // bad group reference
    }

    [Fact]
    public void Snippet_transforms_with_regex_alternatives_or_optional_matches_36089()
    {
        AssertVariableResolve2("${TM_FILENAME/^(.)|(?:-(.))|(\\.js)/${1:/upcase}${2:/upcase}/g}", "MyClass", "my-class.js");

        // no hyphens
        AssertVariableResolve2("${TM_FILENAME/^(.)|(?:-(.))|(\\.js)/${1:/upcase}${2:/upcase}/g}", "Myclass", "myclass.js");

        // none matching suffix
        AssertVariableResolve2("${TM_FILENAME/^(.)|(?:-(.))|(\\.js)/${1:/upcase}${2:/upcase}/g}", "Myclass.foo", "myclass.foo");

        // more than one hyphen
        AssertVariableResolve2("${TM_FILENAME/^(.)|(?:-(.))|(\\.js)/${1:/upcase}${2:/upcase}/g}", "ThisIsAFile", "this-is-a-file.js");

        // KEBAB CASE
        AssertVariableResolve2("${TM_FILENAME_BASE/([A-Z][a-z]+)([A-Z][a-z]+$)?/${1:/downcase}-${2:/downcase}/g}", "capital-case", "CapitalCase");
        AssertVariableResolve2("${TM_FILENAME_BASE/([A-Z][a-z]+)([A-Z][a-z]+$)?/${1:/downcase}-${2:/downcase}/g}", "capital-case-more", "CapitalCaseMore");
    }

    [Fact]
    public void Add_variable_to_insert_value_from_clipboard_40153()
    {
        AssertVariableResolve(new ClipboardBasedVariableResolver(() => null, 1, 0, true), "CLIPBOARD", null);
        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "", 1, 0, true), "CLIPBOARD", null);
        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "foo", 1, 0, true), "CLIPBOARD", "foo");

        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "foo", 1, 0, true), "foo", null);
        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "foo", 1, 0, true), "cLIPBOARD", null);
    }

    [Fact]
    public void Add_variable_to_insert_value_from_clipboard_40153_2()
    {
        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "line1", 1, 2, true), "CLIPBOARD", "line1");
        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "line1\nline2\nline3", 1, 2, true), "CLIPBOARD", "line1\nline2\nline3");

        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "line1\nline2", 1, 2, true), "CLIPBOARD", "line2");
        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "line1\nline2", 0, 2, true), "CLIPBOARD", "line1");

        AssertVariableResolve(new ClipboardBasedVariableResolver(() => "line1\nline2", 0, 2, false), "CLIPBOARD", "line1\nline2");
    }

    private static void AssertVariableResolve3(IVariableResolver resolver, string varName)
    {
        var snippet = new SnippetParser().Parse("$" + varName);
        var variable = (Variable)snippet.Children[0];
        variable.Resolve(resolver).ShouldBeTrue($"{varName} failed to resolve");
    }

    [Fact]
    public void Add_time_variables_for_snippets_41631_43140()
    {
        var resolver = new TimeBasedVariableResolver();

        AssertVariableResolve3(resolver, "CURRENT_YEAR");
        AssertVariableResolve3(resolver, "CURRENT_YEAR_SHORT");
        AssertVariableResolve3(resolver, "CURRENT_MONTH");
        AssertVariableResolve3(resolver, "CURRENT_DATE");
        AssertVariableResolve3(resolver, "CURRENT_HOUR");
        AssertVariableResolve3(resolver, "CURRENT_MINUTE");
        AssertVariableResolve3(resolver, "CURRENT_SECOND");
        AssertVariableResolve3(resolver, "CURRENT_MILLISECOND");
        AssertVariableResolve3(resolver, "CURRENT_DAY_NAME");
        AssertVariableResolve3(resolver, "CURRENT_DAY_NAME_SHORT");
        AssertVariableResolve3(resolver, "CURRENT_MONTH_NAME");
        AssertVariableResolve3(resolver, "CURRENT_MONTH_NAME_SHORT");
        AssertVariableResolve3(resolver, "CURRENT_SECONDS_UNIX");
        AssertVariableResolve3(resolver, "CURRENT_MILLISECONDS_UNIX");
        AssertVariableResolve3(resolver, "CURRENT_TIMEZONE_OFFSET");
        AssertVariableResolve3(resolver, "CURRENT_TIMEZONE_NAME");
    }

    /// <summary>sinon's fake timers: a clock that moves only when told to, in a fixed time zone.</summary>
    private sealed class FakeTimeProvider(DateTimeOffset now, TimeZoneInfo? zone = null) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone { get; } = zone ?? TimeZoneInfo.Utc;

        public void Tick(TimeSpan span) => _now += span;
    }

    [Fact]
    public void Time_based_snippet_variables_have_deterministic_millisecond_and_unix_values()
    {
        var now = new DateTimeOffset(2024, 4, 15, 12, 34, 56, 7, TimeSpan.Zero);
        var resolver = new TimeBasedVariableResolver(new FakeTimeProvider(now));
        var inv = CultureInfo.InvariantCulture;

        AssertVariableResolve(resolver, "CURRENT_YEAR", "2024");
        AssertVariableResolve(resolver, "CURRENT_YEAR_SHORT", "24");
        AssertVariableResolve(resolver, "CURRENT_MONTH", "04");
        AssertVariableResolve(resolver, "CURRENT_DATE", "15");
        AssertVariableResolve(resolver, "CURRENT_HOUR", "12");
        AssertVariableResolve(resolver, "CURRENT_MINUTE", "34");
        AssertVariableResolve(resolver, "CURRENT_SECOND", "56");
        AssertVariableResolve(resolver, "CURRENT_MILLISECOND", "007");
        AssertVariableResolve(resolver, "CURRENT_SECONDS_UNIX", now.ToUnixTimeSeconds().ToString(inv));
        AssertVariableResolve(resolver, "CURRENT_MILLISECONDS_UNIX", now.ToUnixTimeMilliseconds().ToString(inv));

        // not in upstream: the names and the offset in a fixed zone
        AssertVariableResolve(resolver, "CURRENT_DAY_NAME", "Monday");
        AssertVariableResolve(resolver, "CURRENT_DAY_NAME_SHORT", "Mon");
        AssertVariableResolve(resolver, "CURRENT_MONTH_NAME", "April");
        AssertVariableResolve(resolver, "CURRENT_MONTH_NAME_SHORT", "Apr");
        AssertVariableResolve(resolver, "CURRENT_TIMEZONE_OFFSET", "+00:00");
    }

    [Fact]
    public void Time_based_snippet_variables_resolve_to_the_same_values_even_as_time_progresses()
    {
        const string snippetText = """

            $CURRENT_YEAR
            $CURRENT_YEAR_SHORT
            $CURRENT_MONTH
            $CURRENT_DATE
            $CURRENT_HOUR
            $CURRENT_MINUTE
            $CURRENT_SECOND
            $CURRENT_MILLISECOND
            $CURRENT_DAY_NAME
            $CURRENT_DAY_NAME_SHORT
            $CURRENT_MONTH_NAME
            $CURRENT_MONTH_NAME_SHORT
            $CURRENT_SECONDS_UNIX
            $CURRENT_MILLISECONDS_UNIX
            $CURRENT_TIMEZONE_OFFSET
            $CURRENT_TIMEZONE_NAME

            """;

        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var resolver = new TimeBasedVariableResolver(clock);

        var firstResolve = new SnippetParser().Parse(snippetText).ResolveVariables(resolver);
        clock.Tick(TimeSpan.FromMilliseconds((365L * 24 * 3600 * 1000) + (24 * 3600 * 1000) + (3661 * 1000))); // 1 year + 1 day + 1 hour + 1 minute + 1 second
        var secondResolve = new SnippetParser().Parse(snippetText).ResolveVariables(resolver);

        firstResolve.ToString().ShouldBe(secondResolve.ToString(), "Time-based snippet variables resolved differently");
    }

    [Fact]
    public void Creating_snippet_format_condition_doesnt_work_53617()
    {
        var snippet = new SnippetParser().Parse("${TM_LINE_NUMBER/(10)/${1:?It is:It is not}/} line 10", true);
        snippet.ResolveVariables(new FuncResolver(_ => "10"));
        snippet.ToString().ShouldBe("It is line 10");

        snippet.ResolveVariables(new FuncResolver(_ => "11"));
        snippet.ToString().ShouldBe("It is not line 10");
    }

    private sealed class TestWorkspaceService : ISnippetWorkspaceService
    {
        public SnippetWorkspace? Workspace { get; set; }

        public SnippetWorkspace? GetWorkspace() => Workspace;
    }

    [Fact]
    public void Add_workspace_name_and_folder_variables_for_snippets_68261()
    {
        var workspaceService = new TestWorkspaceService();
        var resolver = new WorkspaceBasedVariableResolver(workspaceService);

        // empty workspace
        workspaceService.Workspace = new SnippetWorkspace([]);
        AssertVariableResolve(resolver, "WORKSPACE_NAME", null);
        AssertVariableResolve(resolver, "WORKSPACE_FOLDER", null);

        // single folder workspace without config
        workspaceService.Workspace = new SnippetWorkspace([new Uri("file:///folderName")]);
        AssertVariableResolve(resolver, "WORKSPACE_NAME", "folderName");
        if (!OperatingSystem.IsWindows())
        {
            AssertVariableResolve(resolver, "WORKSPACE_FOLDER", "/folderName");
        }

        // workspace with config (upstream: URI.file('testWorkspace.code-workspace'), a file at the root)
        var workspaceConfigPath = new Uri("file:///testWorkspace.code-workspace");
        workspaceService.Workspace = new SnippetWorkspace([new Uri("file:///folderName")], workspaceConfigPath);
        AssertVariableResolve(resolver, "WORKSPACE_NAME", "testWorkspace");
        if (!OperatingSystem.IsWindows())
        {
            AssertVariableResolve(resolver, "WORKSPACE_FOLDER", "/");
        }
    }

    private sealed class WorkspaceLabelService(string rootPath) : ISnippetLabelService
    {
        public string GetUriLabel(Uri uri, bool relative = false, bool noPrefix = false)
        {
            var rootFsPath = SnippetUris.FsPath(new Uri("file://" + (rootPath.Length == 0 ? "/" : rootPath))) + Path.DirectorySeparatorChar;
            var fsPath = SnippetUris.FsPath(uri);
            if (relative && rootPath.Length > 0 && fsPath.StartsWith(rootFsPath, StringComparison.Ordinal))
            {
                return fsPath[rootFsPath.Length..];
            }

            return fsPath;
        }
    }

    [Fact]
    public void Add_RELATIVE_FILEPATH_snippet_variable_114208()
    {
        using var model = TestTextModel.Create("", uri: new Uri("file:///foo/files/text.txt"));

        // empty workspace
        var resolver = new ModelBasedVariableResolver(new WorkspaceLabelService(""), model);
        AssertVariableResolve(resolver, "RELATIVE_FILEPATH", OperatingSystem.IsWindows() ? "\\foo\\files\\text.txt" : "/foo/files/text.txt");

        // single folder workspace
        resolver = new ModelBasedVariableResolver(new WorkspaceLabelService("/foo"), model);
        AssertVariableResolve(resolver, "RELATIVE_FILEPATH", OperatingSystem.IsWindows() ? "files\\text.txt" : "files/text.txt");
    }

    // ---- not in upstream ----

    [Fact]
    public void Comment_variables_come_from_the_language_configuration()
    {
        using var service = new LanguageConfigurationService();
        using var registration = service.Register("snippet-lang", new LanguageConfiguration
        {
            Comments = new CommentRule { LineComment = "//", BlockComment = ("/*", "*/") },
        });
        using var model = TestTextModel.Create("x", "snippet-lang");
        var resolver = new CommentBasedVariableResolver(model, new TextSelection(1, 1, 1, 1), service);

        AssertVariableResolve(resolver, "LINE_COMMENT", "//");
        AssertVariableResolve(resolver, "BLOCK_COMMENT_START", "/*");
        AssertVariableResolve(resolver, "BLOCK_COMMENT_END", "*/");
        AssertVariableResolve(resolver, "TM_FILENAME", null);
    }

    [Fact]
    public void Random_variables_have_their_shapes()
    {
        var resolver = new RandomBasedVariableResolver(new Random(42));
        var snippet = new SnippetParser().Parse("$RANDOM|$RANDOM_HEX|$UUID").ResolveVariables(resolver);
        var parts = snippet.ToString().Split('|');

        parts[0].Length.ShouldBe(6);
        parts[0].All(char.IsAsciiDigit).ShouldBeTrue();
        parts[1].Length.ShouldBe(6);
        parts[1].All(char.IsAsciiHexDigitLower).ShouldBeTrue();
        Guid.TryParse(parts[2], out _).ShouldBeTrue();
        parts[2].ShouldBe(parts[2].ToLowerInvariant());
    }
}
