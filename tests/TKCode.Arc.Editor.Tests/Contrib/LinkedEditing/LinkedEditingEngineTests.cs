// Own tests of the engine half of linked editing (src/vs/editor/contrib/linkedEditing/browser/linkedEditing.ts,
// commit b57d5c9e): getLinkedEditingRanges and the edits of _syncRanges. Upstream's linkedEditing.test.ts drives a
// whole editor; it is ported in the control tests (TKCode.Arc.Extras.Tests/Editor/CodeEditorLinkedEditingTests.cs).

using System.Text.RegularExpressions;
using Shouldly;
using TKCode.Arc.Editor.Contrib.LinkedEditing;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.LinkedEditing;

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class LinkedEditingEngineTests
{
    private static TextRange R(int a, int b, int c, int d) => new(a, b, c, d);

    [Fact]
    public void Mirrors_get_only_the_changed_middle()
    {
        using var model = TestTextModel.Create("<iooo></ooo>");
        var edits = LinkedEditingSupport.ComputeSyncEdits(model, R(1, 2, 1, 6), [R(1, 9, 1, 12)], null)!;
        edits.ShouldBe([new SingleEditOperation(R(1, 9, 1, 9), "i")]);

        using var shorter = TestTextModel.Create("<oo></ooo>");
        LinkedEditingSupport.ComputeSyncEdits(shorter, R(1, 2, 1, 4), [R(1, 7, 1, 10)], null)!
            .ShouldBe([new SingleEditOperation(R(1, 9, 1, 10), string.Empty)]);
    }

    [Fact]
    public void Equal_mirrors_need_no_edit_and_multi_line_mirrors_are_replaced_whole()
    {
        using var model = TestTextModel.Create("<a>\n</a\nb>");
        LinkedEditingSupport.ComputeSyncEdits(model, R(1, 2, 1, 3), [R(2, 3, 2, 4)], null)!.ShouldBeEmpty();
        LinkedEditingSupport.ComputeSyncEdits(model, R(1, 2, 1, 3), [R(2, 3, 3, 2), null], null)!
            .ShouldBe([new SingleEditOperation(R(2, 3, 3, 2), "a")]);
    }

    [Fact]
    public void The_reference_must_be_one_word_of_the_pattern_and_one_line()
    {
        using var model = TestTextModel.Create("<a b></a>\n<c\nd>");
        var word = new Regex("[a-z]+");
        LinkedEditingSupport.ComputeSyncEdits(model, R(1, 2, 1, 5), [R(1, 8, 1, 9)], word).ShouldBeNull();
        LinkedEditingSupport.ComputeSyncEdits(model, R(1, 2, 1, 3), [R(1, 8, 1, 9)], word).ShouldNotBeNull();
        LinkedEditingSupport.ComputeSyncEdits(model, R(2, 2, 3, 2), [R(1, 8, 1, 9)], null).ShouldBeNull();
    }

    [Fact]
    public async Task The_first_provider_in_score_order_with_ranges_answers()
    {
        var registry = new LanguageFeatureRegistry<ILinkedEditingRangeProvider>();
        using var model = TestTextModel.Create("<a></a>", "html");
        registry.Register("*", new Provider(_ => new LinkedEditingRanges([R(1, 2, 1, 3)])));
        registry.Register("html", new Provider(_ => new LinkedEditingRanges([])));
        registry.Register("html", new Provider(_ => throw new InvalidOperationException("broken")));

        var errors = new List<Exception>();
        void OnError(Exception e) => errors.Add(e);
        UnexpectedErrors.Occurred += OnError;
        try
        {
            var result = await LinkedEditingSupport.GetLinkedEditingRanges(registry, model, new TextPosition(1, 2), default);
            result!.Ranges.ShouldBe([R(1, 2, 1, 3)]);
        }
        finally
        {
            UnexpectedErrors.Occurred -= OnError;
        }

        errors.Count.ShouldBe(1);
        using var other = TestTextModel.Create("x", "other");
        var empty = new LanguageFeatureRegistry<ILinkedEditingRangeProvider>();
        (await LinkedEditingSupport.GetLinkedEditingRanges(empty, other, new TextPosition(1, 1), default)).ShouldBeNull();
    }

    private sealed class Provider(Func<TextPosition, LinkedEditingRanges?> ranges) : ILinkedEditingRangeProvider
    {
        public Task<LinkedEditingRanges?> ProvideLinkedEditingRanges(TextModel model, TextPosition position, CancellationToken token) =>
            Task.FromResult(ranges(position));
    }
}
