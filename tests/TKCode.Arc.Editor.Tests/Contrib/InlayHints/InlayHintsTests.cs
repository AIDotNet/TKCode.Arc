// Tests written for the port of vscode's inlay hints (src/vs/editor/contrib/inlayHints/browser/inlayHints.ts and the
// decoration part of inlayHintsController.ts, commit b57d5c9e, MIT license); upstream has no unit tests for them.

using Shouldly;
using TKCode.Arc.Editor.Contrib.InlayHints;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class InlayHintsTests
{
    internal sealed class Provider(Func<TextModel, TextRange, InlayHintList?> provide) : IInlayHintsProvider
    {
        public List<TextRange> Requests { get; } = new();

        public Func<InlayHint, InlayHint?>? Resolver { get; init; }

        public int Resolves { get; private set; }

        public bool SupportsResolveInlayHint => Resolver is not null;

        public Task<InlayHintList?> ProvideInlayHints(TextModel model, TextRange range, CancellationToken token)
        {
            Requests.Add(range);
            return Task.FromResult(provide(model, range));
        }

        public Task<InlayHint?> ResolveInlayHint(InlayHint hint, CancellationToken token)
        {
            Resolves++;
            return Task.FromResult(Resolver!(hint));
        }
    }

    [Fact]
    public async Task Fragments_anchor_hints_to_the_word_before_or_after_and_sort_them()
    {
        using var model = TestTextModel.Create("let value = compute(a, b);");
        var registry = new LanguageFeatureRegistry<IInlayHintsProvider>();
        var disposed = 0;
        using var _ = registry.Register("*", new Provider((_, _) => new InlayHintList(
        [
            new InlayHint(new TextPosition(1, 21)) { Label = "x:", Kind = InlayHintKind.Parameter, PaddingRight = true },
            new InlayHint(new TextPosition(1, 10)) { Label = ": number", Kind = InlayHintKind.Type },
        ]) { Dispose = () => disposed++ }));

        using (var fragments = await InlayHintsFragments.Create(registry, model, [new TextRange(1, 1, 1, 27)], CancellationToken.None))
        {
            fragments.Items.Select(i => i.Hint.Position.Column).ShouldBe([10, 21]);

            // after "value": the hint follows the word it ends
            fragments.Items[0].Anchor.ShouldBe(new InlayHintAnchor(new TextRange(1, 5, 1, 10), InlayHintDirection.After));
            // before "a": the hint precedes the word it starts
            fragments.Items[1].Anchor.ShouldBe(new InlayHintAnchor(new TextRange(1, 21, 1, 22), InlayHintDirection.Before));
            fragments.Providers.Count.ShouldBe(1);
        }

        disposed.ShouldBe(1);
    }

    [Fact]
    public async Task Fragments_ask_every_provider_for_every_range_and_throw_when_cancelled()
    {
        using var model = TestTextModel.Create(string.Join('\n', Enumerable.Repeat("abc", 100)));
        var registry = new LanguageFeatureRegistry<IInlayHintsProvider>();
        var first = new Provider((_, range) => new InlayHintList([new InlayHint(range.Start) { Label = "first" }]));
        var second = new Provider((_, _) => throw new InvalidOperationException("boom"));
        using var r1 = registry.Register("*", first);
        using var r2 = registry.Register("*", second);
        var errors = new List<Exception>();
        Action<Exception> onError = errors.Add;
        UnexpectedErrors.Occurred += onError;
        try
        {
            using var fragments = await InlayHintsFragments.Create(registry, model, [new TextRange(1, 1, 10, 1), new TextRange(50, 1, 60, 1)], CancellationToken.None);
            first.Requests.Count.ShouldBe(2);
            fragments.Items.Count.ShouldBe(2);
            errors.Count.ShouldBe(2);
        }
        finally
        {
            UnexpectedErrors.Occurred -= onError;
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => InlayHintsFragments.Create(registry, model, [new TextRange(1, 1, 1, 4)], cts.Token));
    }

    [Fact]
    public void Decorations_inject_padding_and_label_parts_with_kind_class_names()
    {
        var provider = new Provider((_, _) => null);
        var item = new InlayHintItem(
            new InlayHint(new TextPosition(1, 5))
            {
                LabelParts = [new InlayHintLabelPart("a"), new InlayHintLabelPart("bc") { Command = new LanguageCommand("go", "Go") }],
                Kind = InlayHintKind.Parameter,
                PaddingLeft = true,
                PaddingRight = true,
            },
            new InlayHintAnchor(new TextRange(1, 5, 1, 8), InlayHintDirection.Before),
            provider);

        var decorations = InlayHintsDecorations.Create([item], visible: true, maximumLength: 43, activePart: new RenderedInlayHintLabelPart(item, 1));

        decorations.Select(d => d.Decoration.Options.Before!.Content).ShouldBe([" ", "a", "bc", " "]);
        decorations.Select(d => d.Decoration.Options.Before!.InlineClassName).ShouldBe(
        [
            InlayHintsDecorations.PaddingClassName,
            "inlayHint inlayHintParameter",
            "inlayHint inlayHintParameter inlayHintActiveLink",
            InlayHintsDecorations.PaddingClassName,
        ]);
        decorations.Select(d => d.Decoration.Options.Before!.CursorStops).ShouldBe(
        [
            InjectedTextCursorStops.None, InjectedTextCursorStops.None, InjectedTextCursorStops.None, InjectedTextCursorStops.Right,
        ]);
        decorations[1].Decoration.Options.Before!.AttachedData.ShouldBe(new RenderedInlayHintLabelPart(item, 0));
        decorations.All(d => d.Decoration.Options.After is null && d.Decoration.Range == item.Anchor.Range).ShouldBeTrue();

        // invisible (Ctrl+Alt held with onUnlessPressed): the decorations stay, without text
        InlayHintsDecorations.Create([item], visible: false, 43, null).All(d => d.Decoration.Options.Before is null).ShouldBeTrue();
    }

    [Fact]
    public void Decorations_cut_labels_at_the_maximum_length_per_line()
    {
        var provider = new Provider((_, _) => null);
        InlayHintItem Hint(int line, int column, string label) => new(
            new InlayHint(new TextPosition(line, column)) { Label = label },
            new InlayHintAnchor(new TextRange(line, column, line, column), InlayHintDirection.After),
            provider);

        var decorations = InlayHintsDecorations.Create(
            [Hint(1, 1, "12345"), Hint(1, 2, "67890"), Hint(1, 3, "skipped"), Hint(2, 1, "1234567")],
            visible: true,
            maximumLength: 8,
            activePart: null);

        decorations.Select(d => d.Decoration.Options.After!.Content).ShouldBe(["12345", "678…", "1234567"]);
    }

    [Fact]
    public async Task Resolve_fills_in_tooltip_and_label_once()
    {
        var provider = new Provider((_, _) => null)
        {
            Resolver = hint => hint with { Tooltip = "resolved", Label = "better" },
        };
        var item = new InlayHintItem(new InlayHint(new TextPosition(1, 1)) { Label = "plain" }, new InlayHintAnchor(new TextRange(1, 1, 1, 1), InlayHintDirection.Before), provider);

        await item.Resolve(CancellationToken.None);
        await item.Resolve(CancellationToken.None);

        provider.Resolves.ShouldBe(1);
        item.Hint.Tooltip!.Value.Content.ShouldBe("resolved");
        item.GetLabelParts().Single().Label.ShouldBe("better");
        item.With(new InlayHintAnchor(new TextRange(2, 1, 2, 1), InlayHintDirection.After)).Hint.ShouldBe(item.Hint);
    }

    [Fact]
    public void Hint_ranges_grow_the_visible_ranges_by_30_lines_and_merge()
    {
        using var model = TestTextModel.Create(string.Join('\n', Enumerable.Repeat("x", 200)));
        InlayHintsDecorations.GetHintsRanges(model, [new TextRange(150, 1, 160, 2), new TextRange(10, 1, 20, 2), new TextRange(40, 1, 45, 2)])
            .ShouldBe([new TextRange(1, 1, 75, 2), new TextRange(120, 1, 190, 2)]);
    }

    [Fact]
    public void Injected_hints_show_in_the_view_lines_and_move_with_edits()
    {
        TestCodeEditor.WithTestCodeEditor("let value = 1;", null, (editor, viewModel) =>
        {
            var provider = new Provider((_, _) => null);
            var item = new InlayHintItem(
                new InlayHint(new TextPosition(1, 10)) { Label = ": number", PaddingLeft = false },
                new InlayHintAnchor(new TextRange(1, 5, 1, 10), InlayHintDirection.After),
                provider);
            var decorations = InlayHintsDecorations.Create([item], true, 43, null);
            var ids = editor.Model.DeltaDecorations(null, decorations.Select(d => d.Decoration).ToArray());

            viewModel.GetLineContent(1).ShouldBe("let value: number = 1;");

            editor.Model.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "  ")]);
            viewModel.GetLineContent(1).ShouldBe("  let value: number = 1;");
            editor.Model.GetDecorationRange(ids[0]).ShouldBe(new TextRange(1, 7, 1, 12));
        });
    }
}
