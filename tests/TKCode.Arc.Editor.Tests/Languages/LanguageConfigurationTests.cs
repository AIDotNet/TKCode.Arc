// Ported from vscode (src/vs/editor/test/common/modes/languageConfiguration.test.ts, commit b57d5c9e), MIT license.
// TestLanguageConfigurationService is LanguageConfigurationService here (it has no configuration service to stub).

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class LanguageConfigurationTests
{
    private static void AssertIsOK(StandardAutoClosingPairConditional v, bool other, bool comment, bool str, bool regEx)
    {
        v.IsOK(StandardTokenType.Other).ShouldBe(other);
        v.IsOK(StandardTokenType.Comment).ShouldBe(comment);
        v.IsOK(StandardTokenType.String).ShouldBe(str);
        v.IsOK(StandardTokenType.RegEx).ShouldBe(regEx);
    }

    [Fact]
    public void Missing_NotIn()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}"));
        AssertIsOK(v, true, true, true, true);
    }

    [Fact]
    public void Empty_NotIn()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = [] });
        AssertIsOK(v, true, true, true, true);
    }

    [Fact]
    public void Invalid_NotIn()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["bla"] });
        AssertIsOK(v, true, true, true, true);
    }

    [Fact]
    public void NotIn_In_Strings()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["string"] });
        AssertIsOK(v, true, true, false, true);
    }

    [Fact]
    public void NotIn_In_Comments()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["comment"] });
        AssertIsOK(v, true, false, true, true);
    }

    [Fact]
    public void NotIn_In_Regex()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["regex"] });
        AssertIsOK(v, true, true, true, false);
    }

    [Fact]
    public void NotIn_In_Strings_Nor_Comments()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["string", "comment"] });
        AssertIsOK(v, true, false, false, true);
    }

    [Fact]
    public void NotIn_In_Strings_Nor_Regex()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["string", "regex"] });
        AssertIsOK(v, true, true, false, false);
    }

    [Fact]
    public void NotIn_In_Comments_Nor_Regex()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["comment", "regex"] });
        AssertIsOK(v, true, false, true, false);
    }

    [Fact]
    public void NotIn_In_Strings_Comments_Nor_Regex()
    {
        var v = new StandardAutoClosingPairConditional(new AutoClosingPairConditional("{", "}") { NotIn = ["string", "comment", "regex"] });
        AssertIsOK(v, true, false, false, false);
    }

    [Fact]
    public void Language_Configurations_Priorities()
    {
        var languageConfigurationService = new LanguageConfigurationService();
        var id = "testLang1";
        var d1 = languageConfigurationService.Register(id, new LanguageConfiguration { Comments = new CommentRule { LineComment = "1" } }, 100);
        var d2 = languageConfigurationService.Register(id, new LanguageConfiguration { Comments = new CommentRule { LineComment = "2" } }, 10);
        var comments = languageConfigurationService.GetLanguageConfiguration(id).Comments;
        comments.ShouldNotBeNull();
        comments.LineCommentToken.ShouldBe("1");
        d1.Dispose();
        d2.Dispose();
        languageConfigurationService.Dispose();
    }
}
