// Ported from vscode (src/vs/editor/test/common/modes/linkComputer.test.ts, commit b57d5c9e), MIT license.
// All 14 upstream tests (the 'Parsing' test is a theory with one case per assertLink); plus 2 own tests of the link
// provider over a model.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class LinkComputerTests
{
    private sealed class SimpleLinkComputerTarget(string[] lines) : ILinkComputerTarget
    {
        public int GetLineCount() => lines.Length;

        public string GetLineContent(int lineNumber) => lines[lineNumber - 1];
    }

    private static List<ComputedLink> MyComputeLinks(string[] lines) => LinkComputer.ComputeLinks(new SimpleLinkComputerTarget(lines));

    private static void AssertLink(string text, string extractedLink)
    {
        int startColumn = 0, endColumn = 0;
        for (var i = 0; i < extractedLink.Length; i++)
        {
            var chr = extractedLink[i];
            if (chr != ' ' && chr != '\t')
            {
                startColumn = i + 1;
                break;
            }
        }

        for (var i = extractedLink.Length - 1; i >= 0; i--)
        {
            var chr = extractedLink[i];
            if (chr != ' ' && chr != '\t')
            {
                endColumn = i + 2;
                break;
            }
        }

        var r = MyComputeLinks([text]);
        r.ShouldBe([new ComputedLink(new TextRange(1, startColumn, 1, endColumn), extractedLink.Substring(startColumn - 1, endColumn - startColumn))]);
    }

    [Fact]
    public void Null_model()
    {
        LinkComputer.ComputeLinks(null).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("x = \"http://foo.bar\";", "     http://foo.bar  ")]
    [InlineData("x = (http://foo.bar);", "     http://foo.bar  ")]
    [InlineData("x = [http://foo.bar];", "     http://foo.bar  ")]
    [InlineData("x = 'http://foo.bar';", "     http://foo.bar  ")]
    [InlineData("x =  http://foo.bar ;", "     http://foo.bar  ")]
    [InlineData("x = <http://foo.bar>;", "     http://foo.bar  ")]
    [InlineData("x = {http://foo.bar};", "     http://foo.bar  ")]
    [InlineData("(see http://foo.bar)", "     http://foo.bar  ")]
    [InlineData("[see http://foo.bar]", "     http://foo.bar  ")]
    [InlineData("{see http://foo.bar}", "     http://foo.bar  ")]
    [InlineData("<see http://foo.bar>", "     http://foo.bar  ")]
    [InlineData("<url>http://mylink.com</url>", "     http://mylink.com      ")]
    [InlineData("// Click here to learn more. https://go.microsoft.com/fwlink/?LinkID=513275&clcid=0x409", "                             https://go.microsoft.com/fwlink/?LinkID=513275&clcid=0x409")]
    [InlineData("// Click here to learn more. https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx", "                             https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx")]
    [InlineData("// https://github.com/projectkudu/kudu/blob/master/Kudu.Core/Scripts/selectNodeVersion.js", "   https://github.com/projectkudu/kudu/blob/master/Kudu.Core/Scripts/selectNodeVersion.js")]
    [InlineData("<!-- !!! Do not remove !!!   WebContentRef(link:https://go.microsoft.com/fwlink/?LinkId=166007, area:Admin, updated:2015, nextUpdate:2016, tags:SqlServer)   !!! Do not remove !!! -->", "                                                https://go.microsoft.com/fwlink/?LinkId=166007                                                                                        ")]
    [InlineData("For instructions, see https://go.microsoft.com/fwlink/?LinkId=166007.</value>", "                      https://go.microsoft.com/fwlink/?LinkId=166007         ")]
    [InlineData("For instructions, see https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx.</value>", "                      https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx         ")]
    [InlineData("x = \"https://en.wikipedia.org/wiki/Zürich\";", "     https://en.wikipedia.org/wiki/Zürich  ")]
    [InlineData("請參閱 http://go.microsoft.com/fwlink/?LinkId=761051。", "    http://go.microsoft.com/fwlink/?LinkId=761051 ")]
    [InlineData("（請參閱 http://go.microsoft.com/fwlink/?LinkId=761051）", "     http://go.microsoft.com/fwlink/?LinkId=761051 ")]
    [InlineData("x = \"file:///foo.bar\";", "     file:///foo.bar  ")]
    [InlineData("x = \"file://c:/foo.bar\";", "     file://c:/foo.bar  ")]
    [InlineData("x = \"file://shares/foo.bar\";", "     file://shares/foo.bar  ")]
    [InlineData("x = \"file://shäres/foo.bar\";", "     file://shäres/foo.bar  ")]
    [InlineData("Some text, then http://www.bing.com.", "                http://www.bing.com ")]
    [InlineData("let url = `http://***/_api/web/lists/GetByTitle('Teambuildingaanvragen')/items`;", "           http://***/_api/web/lists/GetByTitle('Teambuildingaanvragen')/items  ")]
    public void Parsing(string text, string extractedLink) => AssertLink(text, extractedLink);

    [Fact]
    public void Issue_7855() => AssertLink(
        "7. At this point, ServiceMain has been called.  There is no functionality presently in ServiceMain, but you can consult the [MSDN documentation](https://msdn.microsoft.com/en-us/library/windows/desktop/ms687414(v=vs.85).aspx) to add functionality as desired!",
        "                                                                                                                                                 https://msdn.microsoft.com/en-us/library/windows/desktop/ms687414(v=vs.85).aspx                                  ");

    [Fact]
    public void Issue_62278_Ctrl_Click_To_Follow_Link_For_IPv6_URLs() => AssertLink(
        "let x = \"http://[::1]:5000/connect/token\"",
        "         http://[::1]:5000/connect/token  ");

    [Fact]
    public void Issue_70254_Bold_Links_Dont_Open_In_Markdown_File() => AssertLink(
        "2. Navigate to **https://portal.azure.com**",
        "                 https://portal.azure.com  ");

    [Fact]
    public void Issue_86358_URL_Wrong_Recognition_Pattern() => AssertLink(
        "POST|https://portal.azure.com|2019-12-05|",
        "     https://portal.azure.com            ");

    [Fact]
    public void Issue_67022_Space_As_End_Of_Hyperlink_Isnt_Always_Good_Idea() => AssertLink(
        "aa  https://foo.bar/[this is foo site]  aa",
        "    https://foo.bar/[this is foo site]    ");

    [Fact]
    public void Issue_100353_Link_Detection_Stops_At_Double_Byte_Ampersand() => AssertLink(
        "aa  http://tree-mark.chips.jp/レーズン＆ベリーミックス  aa",
        "    http://tree-mark.chips.jp/レーズン＆ベリーミックス    ");

    [Fact]
    public void Issue_121438_Link_Detection_Stops_At_Lenticular_Brackets() => AssertLink(
        "aa  https://zh.wikipedia.org/wiki/【我推的孩子】 aa",
        "    https://zh.wikipedia.org/wiki/【我推的孩子】   ");

    [Fact]
    public void Issue_121438_Link_Detection_Stops_At_Double_Angle_Brackets() => AssertLink(
        "aa  https://zh.wikipedia.org/wiki/《新青年》编辑部旧址 aa",
        "    https://zh.wikipedia.org/wiki/《新青年》编辑部旧址   ");

    [Fact]
    public void Issue_121438_Link_Detection_Stops_At_Curly_Quotes() => AssertLink(
        "aa  https://zh.wikipedia.org/wiki/“常凯申”误译事件 aa",
        "    https://zh.wikipedia.org/wiki/“常凯申”误译事件   ");

    [Fact]
    public void Issue_150905_Colon_After_Bare_Hyperlink_Is_Treated_As_Its_Part() => AssertLink(
        "https://site.web/page.html: blah blah blah",
        "https://site.web/page.html                ");

    [Fact]
    public void Issue_156875_Links_Include_Quotes() => AssertLink(
        "\"This file has been converted from https://github.com/jeff-hykin/better-c-syntax/blob/master/autogenerated/c.tmLanguage.json\",",
        "                                   https://github.com/jeff-hykin/better-c-syntax/blob/master/autogenerated/c.tmLanguage.json  ");

    [Fact]
    public void Issue_225513_Cmd_Click_On_JSDoc_Link_With_Text() => AssertLink(
        " * {@link https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Promise/withResolvers|Promise.withResolvers}",
        "          https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Promise/withResolvers                       ");

    // ---- own tests

    [Fact]
    public void Finds_Several_Links_On_Several_Lines()
    {
        var r = MyComputeLinks(["a http://a.b c https://c.d", "", "see FILE:///x/y"]);
        r.Select(static l => l.Url).ShouldBe(["http://a.b", "https://c.d", "FILE:///x/y"]);
        r[2].Range.ShouldBe(new TextRange(3, 5, 3, 16));
    }

    [Fact]
    public async Task The_Provider_Gives_Uris_For_The_Links_Of_A_Model()
    {
        using var model = TestTextModel.Create("go to https://example.com/a?b=c now\nnot a link: ftp://x");
        var list = await new ComputedLinkProvider().ProvideLinks(model, CancellationToken.None);

        var link = list.ShouldNotBeNull().Links.ShouldHaveSingleItem();
        link.Range.ShouldBe(new TextRange(1, 7, 1, 32));
        link.Url.ShouldBe(new Uri("https://example.com/a?b=c"));
    }
}
