// Test cases ported from vercel/streamdown packages/remend/__tests__ (custom-handlers.test.ts,
// basic-input.test.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.
//
// Calls whose options carry handler functions cannot be recorded as JSON
// fixtures, so they are ported by hand here.

using System.Text.RegularExpressions;
using StreamMark.Core.Remend;
using Xunit;
using RemendApi = StreamMark.Core.Remend.Remend;

namespace StreamMark.Core.Tests.Remend;

public sealed partial class RemendCustomHandlerTests
{
    [GeneratedRegex("<<<JOKE>>>([^<]*)\\z")]
    private static partial Regex JokeMarkerPattern();

    private static RemendOptions With(params RemendHandler[] handlers) => new() { Handlers = handlers };

    [Fact]
    public void ReturnsNullAndEmptyInputUnchanged()
    {
        Assert.Null(RemendApi.Complete(null!));
        Assert.Equal("", RemendApi.Complete(""));
    }

    [Fact]
    public void ExecutesCustomHandlers()
    {
        var handler = new RemendHandler("test", text => text.Replace("foo", "bar", StringComparison.Ordinal));
        Assert.Equal("bar", RemendApi.Complete("foo", With(handler)));
    }

    [Fact]
    public void RunsCustomHandlersAfterBuiltInsByDefault()
    {
        // Bold should be completed first, then custom handler adds "!"
        var handler = new RemendHandler("test", text => text + "!");
        Assert.Equal("**bold**!", RemendApi.Complete("**bold", With(handler)));
    }

    [Fact]
    public void RespectsCustomHandlerPriority()
    {
        var results = new List<string>();
        var low = new RemendHandler("low", text => { results.Add("low"); return text; }, 200);
        var high = new RemendHandler("high", text => { results.Add("high"); return text; }, 5);

        RemendApi.Complete("test", With(low, high));
        Assert.Equal(["high", "low"], results);
    }

    [Fact]
    public void AllowsCustomHandlersToRunBeforeBuiltIns()
    {
        var results = new List<string>();
        var beforeSetext = new RemendHandler("beforeSetext", text => { results.Add("custom"); return text; }, -1);

        RemendApi.Complete("test\n-", With(beforeSetext));
        Assert.Equal("custom", results[0]);
    }

    [Fact]
    public void ChainsMultipleCustomHandlers()
    {
        var first = new RemendHandler("replace-a", text => text.Replace('a', 'b'));
        var second = new RemendHandler("replace-b", text => text.Replace('b', 'c'));
        Assert.Equal("ccc", RemendApi.Complete("aaa", With(first, second)));
    }

    [Fact]
    public void KeepsOrderOfHandlersWithSamePriority()
    {
        var results = new List<string>();
        var first = new RemendHandler("first", text => { results.Add("first"); return text; }, 100);
        var second = new RemendHandler("second", text => { results.Add("second"); return text; }, 100);

        RemendApi.Complete("test", With(first, second));
        Assert.Equal(["first", "second"], results);
    }

    [Fact]
    public void RunsBuiltInsBeforeCustomHandlersOfEqualPriority()
    {
        // Stable sort: a custom handler at a built-in's priority runs after it
        var seen = "";
        var handler = new RemendHandler("probe", text => seen = text, 35);
        RemendApi.Complete("**bold", With(handler));
        Assert.Equal("**bold**", seen);
    }

    [Fact]
    public void WorksWithDisabledBuiltInHandlers()
    {
        var handler = new RemendHandler("test", text => text + "!");
        Assert.Equal("**bold!", RemendApi.Complete("**bold", new RemendOptions { Bold = false, Handlers = [handler] }));
    }

    [Fact]
    public void WorksWithNoBuiltInHandlersEnabled()
    {
        var handler = new RemendHandler("uppercase", text => text.ToUpperInvariant());
        var options = new RemendOptions
        {
            Bold = false,
            Italic = false,
            BoldItalic = false,
            InlineCode = false,
            Strikethrough = false,
            Katex = false,
            Links = false,
            Images = false,
            SetextHeadings = false,
            Handlers = [handler],
        };
        Assert.Equal("HELLO", RemendApi.Complete("hello", options));
    }

    [Fact]
    public void HandlesEmptyHandlersList()
    {
        Assert.Equal("**bold**", RemendApi.Complete("**bold", new RemendOptions { Handlers = [] }));
    }

    [Fact]
    public void LinkEarlyReturnSkipsLaterHandlers()
    {
        var ran = false;
        var handler = new RemendHandler("late", text => { ran = true; return text; });
        Assert.Equal("[text](" + RemendApi.IncompleteLinkUrl + ")", RemendApi.Complete("[text", With(handler)));
        Assert.False(ran);
    }

    private static RemendHandler JokeHandler() => new("joke", text =>
    {
        // Complete <<<JOKE>>> marks
        if (JokeMarkerPattern().IsMatch(text) && !text.EndsWith("<<</JOKE>>>", StringComparison.Ordinal))
        {
            return text + "<<</JOKE>>>";
        }
        return text;
    }, 80);

    [Fact]
    public void CompletesJokeMarkers()
    {
        Assert.Equal("<<<JOKE>>>Why did the chicken<<</JOKE>>>",
            RemendApi.Complete("<<<JOKE>>>Why did the chicken", With(JokeHandler())));
    }

    [Fact]
    public void DoesNotDoubleCompleteJokeMarkers()
    {
        Assert.Equal("<<<JOKE>>>complete<<</JOKE>>>",
            RemendApi.Complete("<<<JOKE>>>complete<<</JOKE>>>", With(JokeHandler())));
    }

    [Fact]
    public void NestedCompleteInsideHandlerIsIsolated()
    {
        // A handler may itself call Complete; the per-call scan cache must survive it
        var handler = new RemendHandler("nested", text => text + RemendApi.Complete(" and **inner"), 36);
        Assert.Equal("**outer** and **inner**", RemendApi.Complete("**outer", With(handler)));
    }
}
