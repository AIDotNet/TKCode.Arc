// Ported from vscode (src/vs/editor/test/common/modes/languageSelector.test.ts, commit b57d5c9e), MIT license.
// All 12 upstream tests. URI.parse is new Uri; URI.file('/x') is new Uri("file:///x"); `null` / `undefined` selectors
// are a null LanguageSelector and `{}` an empty LanguageFilter.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class LanguageSelectorTests
{
    private const string ModelLanguage = "farboo";
    private static readonly Uri ModelUri = new("file:///testbed/file.fb");

    private static int Score(LanguageSelector? selector, Uri uri, string language, bool isSynchronized = true, Uri? notebookUri = null, string? notebookType = null) =>
        LanguageSelector.Score(selector, uri, language, isSynchronized, notebookUri, notebookType);

    [Fact]
    public void Score_invalid_selector()
    {
        Score(new LanguageFilter(), ModelUri, ModelLanguage).ShouldBe(0);
        Score(null, ModelUri, ModelLanguage).ShouldBe(0);
        Score("", ModelUri, ModelLanguage).ShouldBe(0);
    }

    [Fact]
    public void Score_any_language()
    {
        Score(new LanguageFilter { Language = "*" }, ModelUri, ModelLanguage).ShouldBe(5);
        Score("*", ModelUri, ModelLanguage).ShouldBe(5);

        Score("*", new Uri("foo:bar"), ModelLanguage).ShouldBe(5);
        Score("farboo", new Uri("foo:bar"), ModelLanguage).ShouldBe(10);
    }

    [Fact]
    public void Score_default_schemes()
    {
        var uri = new Uri("git:foo/file.txt");
        const string language = "farboo";

        Score("*", uri, language).ShouldBe(5);
        Score("farboo", uri, language).ShouldBe(10);
        Score(new LanguageFilter { Language = "farboo", Scheme = "" }, uri, language).ShouldBe(10);
        Score(new LanguageFilter { Language = "farboo", Scheme = "git" }, uri, language).ShouldBe(10);
        Score(new LanguageFilter { Language = "farboo", Scheme = "*" }, uri, language).ShouldBe(10);
        Score(new LanguageFilter { Language = "farboo" }, uri, language).ShouldBe(10);
        Score(new LanguageFilter { Language = "*" }, uri, language).ShouldBe(5);

        Score(new LanguageFilter { Scheme = "*" }, uri, language).ShouldBe(5);
        Score(new LanguageFilter { Scheme = "git" }, uri, language).ShouldBe(10);
    }

    [Fact]
    public void Score_filter()
    {
        Score("farboo", ModelUri, ModelLanguage).ShouldBe(10);
        Score(new LanguageFilter { Language = "farboo" }, ModelUri, ModelLanguage).ShouldBe(10);
        Score(new LanguageFilter { Language = "farboo", Scheme = "file" }, ModelUri, ModelLanguage).ShouldBe(10);
        Score(new LanguageFilter { Language = "farboo", Scheme = "http" }, ModelUri, ModelLanguage).ShouldBe(0);

        Score(new LanguageFilter { Pattern = "**/*.fb" }, ModelUri, ModelLanguage).ShouldBe(10);
        Score(new LanguageFilter { Pattern = "**/*.fb", Scheme = "file" }, ModelUri, ModelLanguage).ShouldBe(10);
        Score(new LanguageFilter { Pattern = "**/*.fb" }, new Uri("foo:bar"), ModelLanguage).ShouldBe(0);
        Score(new LanguageFilter { Pattern = "**/*.fb", Scheme = "foo" }, new Uri("foo:bar"), ModelLanguage).ShouldBe(0);

        var docUri = new Uri("git:/my/file.js");
        const string docLangId = "javascript";
        Score("javascript", docUri, docLangId).ShouldBe(10); // 0;
        Score(new LanguageFilter { Language = "javascript", Scheme = "git" }, docUri, docLangId).ShouldBe(10); // 10;
        Score("*", docUri, docLangId).ShouldBe(5); // 5
        Score("fooLang", docUri, docLangId).ShouldBe(0); // 0
        Score(LanguageSelector.Of("fooLang", "*"), docUri, docLangId).ShouldBe(5); // 5
    }

    [Fact]
    public void Score_max_filters()
    {
        var match = new LanguageFilter { Language = "farboo", Scheme = "file" };
        var fail = new LanguageFilter { Language = "farboo", Scheme = "http" };

        Score(match, ModelUri, ModelLanguage).ShouldBe(10);
        Score(fail, ModelUri, ModelLanguage).ShouldBe(0);
        Score(LanguageSelector.Of(match, fail), ModelUri, ModelLanguage).ShouldBe(10);
        Score(LanguageSelector.Of(fail, fail), ModelUri, ModelLanguage).ShouldBe(0);
        Score(LanguageSelector.Of("farboo", "*"), ModelUri, ModelLanguage).ShouldBe(10);
        Score(LanguageSelector.Of("*", "farboo"), ModelUri, ModelLanguage).ShouldBe(10);
    }

    [Fact]
    public void Score_hasAccessToAllModels()
    {
        var docUri = new Uri("file:///my/file.js"); // upstream: file:/my/file.js, which System.Uri does not parse
        const string docLangId = "javascript";
        Score("javascript", docUri, docLangId, false).ShouldBe(0);
        Score(new LanguageFilter { Language = "javascript", Scheme = "file" }, docUri, docLangId, false).ShouldBe(0);
        Score("*", docUri, docLangId, false).ShouldBe(0);
        Score("fooLang", docUri, docLangId, false).ShouldBe(0);
        Score(LanguageSelector.Of("fooLang", "*"), docUri, docLangId, false).ShouldBe(0);

        Score(new LanguageFilter { Language = "javascript", Scheme = "file", HasAccessToAllModels = true }, docUri, docLangId, false).ShouldBe(10);
        Score(LanguageSelector.Of("fooLang", "*", new LanguageFilter { Language = "*", HasAccessToAllModels = true }), docUri, docLangId, false).ShouldBe(5);
    }

    [Fact]
    public void Score_notebookType()
    {
        var uri = new Uri("vscode-notebook-cell:///my/file.js#blabla");
        const string langId = "javascript";
        const string notebookType = "fooBook";
        var notebookUri = new Uri("file:///my/file.js");

        Score("javascript", uri, langId).ShouldBe(10);
        Score("javascript", uri, langId, true, notebookUri, notebookType).ShouldBe(10);
        Score(new LanguageFilter { NotebookType = "fooBook" }, uri, langId, true, notebookUri, notebookType).ShouldBe(10);
        Score(new LanguageFilter { NotebookType = "fooBook", Language = "javascript", Scheme = "file" }, uri, langId, true, notebookUri, notebookType).ShouldBe(10);
        Score(new LanguageFilter { NotebookType = "fooBook", Language = "*" }, uri, langId, true, notebookUri, notebookType).ShouldBe(10);
        Score(new LanguageFilter { NotebookType = "*", Language = "*" }, uri, langId, true, notebookUri, notebookType).ShouldBe(5);
        Score(new LanguageFilter { NotebookType = "*", Language = "javascript" }, uri, langId, true, notebookUri, notebookType).ShouldBe(10);
    }

    [Fact]
    public void Snippet_choices_lost_149363()
    {
        var selector = new LanguageFilter
        {
            Scheme = "vscode-notebook-cell",
            Pattern = "/some/path/file.py",
            Language = "python",
        };

        var modelUri = new Uri("vscode-notebook-cell:///some/path/file.py");
        var nbUri = new Uri("file:///some/path/file.py");
        Score(selector, modelUri, "python", true, nbUri, "jupyter").ShouldBe(10);

        var selector2 = selector with { NotebookType = "jupyter" };

        Score(selector2, modelUri, "python", true, nbUri, "jupyter").ShouldBe(0);
    }

    [Fact]
    public void Document_selector_match_unexpected_result_value_60232()
    {
        var selector = new LanguageFilter
        {
            Language = "json",
            Scheme = "file",
            Pattern = "**/*.interface.json",
        };
        var value = Score(selector, new Uri("file:///C:/Users/zlhe/Desktop/test.interface.json"), "json");
        value.ShouldBe(10);
    }

    [Fact]
    public void Document_selector_match_platform_paths_99938()
    {
        var selector = new LanguageFilter
        {
            Pattern = new GlobPattern("*.json", "/home/user/Desktop"),
        };
        var value = Score(selector, new Uri("file:///home/user/Desktop/test.json"), "json");
        value.ShouldBe(10);
    }

    [Fact]
    public void NotebookType_without_notebook()
    {
        var uri = new Uri("file:///my/file.bat");
        const string langId = "bat";

        var value = Score(new LanguageFilter { Language = "bat", NotebookType = "xxx" }, uri, langId);
        value.ShouldBe(0);

        value = Score(new LanguageFilter { Language = "bat", NotebookType = "*" }, uri, langId);
        value.ShouldBe(0);
    }

    [Fact]
    public void SelectLanguageIds()
    {
        var result = new HashSet<string>();

        LanguageSelector.SelectLanguageIds("typescript", result);
        result.ShouldBe(["typescript"]);

        result.Clear();
        LanguageSelector.SelectLanguageIds(new LanguageFilter { Language = "python", Scheme = "file" }, result);
        result.ShouldBe(["python"]);

        result.Clear();
        LanguageSelector.SelectLanguageIds(new LanguageFilter { Scheme = "file" }, result);
        result.ShouldBeEmpty();

        result.Clear();
        LanguageSelector.SelectLanguageIds(LanguageSelector.Of("javascript", new LanguageFilter { Language = "css" }, new LanguageFilter { Scheme = "untitled" }), result);
        result.Order().ShouldBe(["css", "javascript"]);

        result.Clear();
        LanguageSelector.SelectLanguageIds("*", result);
        result.ShouldBe(["*"]);
    }

    // ---- not upstream: the glob subset document selectors use

    [Theory]
    [InlineData("**/*.cs", "/a/b/c.cs", true)]
    [InlineData("**/*.cs", "c.cs", true)]
    [InlineData("**/*.cs", "/a/b/c.csx", false)]
    [InlineData("*.cs", "/a/c.cs", false)]
    [InlineData("/a/*.cs", "/a/c.cs", true)]
    [InlineData("/a/*.cs", "/a/b/c.cs", false)]
    [InlineData("/a/**", "/a/b/c.cs", true)]
    [InlineData("**/*.{ts,js}", "/x/y.js", true)]
    [InlineData("**/*.{ts,js}", "/x/y.css", false)]
    [InlineData("**/file?.txt", "/x/file1.txt", true)]
    [InlineData("**/file[0-9].txt", "/x/file7.txt", true)]
    [InlineData("**/file[!0-9].txt", "/x/file7.txt", false)]
    public void Glob_matches_paths(string pattern, string path, bool expected) =>
        Glob.Match(pattern, path).ShouldBe(expected);
}
