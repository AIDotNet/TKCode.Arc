// Tests of the port of vscode's WordBasedCompletionItemProvider (editorWorkerService.ts) and $textualSuggest
// (editorWebWorker.ts), commit b57d5c9e, MIT license. Upstream tests $textualSuggest through the worker in
// editorWebWorker.test.ts ('words', 'textualSuggest'); these follow those cases on the snapshot.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class WordBasedCompletionItemProviderTests
{
    [Fact]
    public async Task Suggests_the_words_of_the_document_except_the_current_word_and_numbers()
    {
        using var model = TestTextModel.Create("foo bar 42 foobar\nbaz foo\nfo");
        var provider = new WordBasedCompletionItemProvider(() => WordBasedSuggestions.CurrentDocument, null);

        var list = (await provider.ProvideCompletionItems(model, new TextPosition(3, 3), new CompletionContext(CompletionTriggerKind.Invoke), default)).ShouldNotBeNull();

        list.Suggestions.Select(s => s.Label.Label).ShouldBe(["foo", "bar", "foobar", "baz"]);
        var item = list.Suggestions[0];
        item.Kind.ShouldBe(CompletionItemKind.Text);
        item.Range.Insert.ShouldBe(new TextRange(3, 1, 3, 3));
        item.Range.Replace.ShouldBe(new TextRange(3, 1, 3, 3));
    }

    [Fact]
    public async Task Replace_range_covers_the_whole_word_and_insert_range_ends_at_the_cursor()
    {
        using var model = TestTextModel.Create("alpha beta\nalp");
        var provider = new WordBasedCompletionItemProvider(() => WordBasedSuggestions.CurrentDocument, null);

        var list = (await provider.ProvideCompletionItems(model, new TextPosition(1, 3), new CompletionContext(CompletionTriggerKind.Invoke), default)).ShouldNotBeNull();
        list.Suggestions.Select(s => s.Label.Label).ShouldBe(["beta", "alp"]);
        list.Suggestions[0].Range.Insert.ShouldBe(new TextRange(1, 1, 1, 3));
        list.Suggestions[0].Range.Replace.ShouldBe(new TextRange(1, 1, 1, 6));
    }

    [Fact]
    public async Task Off_suggests_nothing()
    {
        using var model = TestTextModel.Create("foo bar");
        var provider = new WordBasedCompletionItemProvider(() => WordBasedSuggestions.Off, null);
        (await provider.ProvideCompletionItems(model, new TextPosition(1, 1), new CompletionContext(CompletionTriggerKind.Invoke), default)).ShouldBeNull();
    }

    [Fact]
    public void TextualSuggest_stops_after_the_limit()
    {
        var text = string.Join(' ', Enumerable.Range(0, SuggestWords.SuggestionsLimit + 50).Select(i => "w" + i));
        using var model = TestTextModel.Create(text);
        var words = SuggestWords.TextualSuggest(model.CreateTextSnapshot(), null, WordHelper.DefaultWordRegExp);
        words.Count.ShouldBe(SuggestWords.SuggestionsLimit + 1); // upstream adds the word, then stops above the limit
    }

    [Theory]
    [InlineData("42", true)]
    [InlineData("4.2e3", true)]
    [InlineData("0x1F", true)]
    [InlineData("Infinity", true)]
    [InlineData("1a", false)]
    [InlineData("foo", false)]
    [InlineData("NaN", false)]
    public void JsNumber_is_JavaScripts_Number_check(string text, bool isNumber) => JsNumber.IsNumber(text).ShouldBe(isNumber);
}
