// Tests of the port of vscode's src/vs/editor/contrib/format/browser/format.ts (commit b57d5c9e, MIT license). Upstream
// has no unit tests for format.ts (it is tested through the workbench); these cover the formatter order, the selector,
// range merging, the model path, on-type formatting and cancellation by an edit.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Format;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Format;

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class FormattingTests
{
    private static TextModel Model(params string[] lines) => TestTextModel.Create(string.Join('\n', lines), "fmt", uri: new Uri("file:///a.fmt"));

    private static TextRange R(int a, int b, int c, int d) => new(a, b, c, d);

    [Fact]
    public void Range_formatters_follow_document_formatters_unless_the_same_extension_formats_documents()
    {
        var features = new LanguageFeatures();
        using var model = Model("x");
        var doc = new DocumentFormatter("ext.a", _ => []);
        features.DocumentFormattingEditProvider.Register("fmt", doc);
        features.DocumentRangeFormattingEditProvider.Register("fmt", new RangeFormatter("ext.a", (_, _) => []));
        var other = new RangeFormatter("ext.b", (_, _) => []);
        features.DocumentRangeFormattingEditProvider.Register("fmt", other);

        var formatters = Formatting.GetRealAndSyntheticDocumentFormattersOrdered(features.DocumentFormattingEditProvider, features.DocumentRangeFormattingEditProvider, model);
        formatters.Count.ShouldBe(2);
        formatters[0].ShouldBe(doc);
        formatters[1].ExtensionId.ShouldBe("ext.b");
    }

    [Fact]
    public async Task A_synthetic_document_formatter_formats_the_full_model_range()
    {
        var features = new LanguageFeatures();
        using var model = Model("a", "b");
        TextRange? asked = null;
        features.DocumentRangeFormattingEditProvider.Register("fmt", new RangeFormatter(null, (_, range) =>
        {
            asked = range;
            return [new TextEdit(R(1, 1, 1, 2), "A")];
        }));

        (await Formatting.FormatDocumentWithSelectedProvider(features, model, FormattingMode.Explicit, default)).ShouldBeTrue();
        asked.ShouldBe(R(1, 1, 2, 2));
        model.GetValue().ShouldBe("A\nb");
    }

    [Fact]
    public async Task The_selector_picks_the_formatter_and_null_formats_nothing()
    {
        var features = new LanguageFeatures();
        using var model = Model("a");
        features.DocumentFormattingEditProvider.Register("fmt", new DocumentFormatter("first", _ => [new TextEdit(R(1, 1, 1, 2), "1")]));
        features.DocumentFormattingEditProvider.Register("fmt", new DocumentFormatter("second", _ => [new TextEdit(R(1, 1, 1, 2), "2")]));

        (await Formatting.FormatDocumentWithSelectedProvider(features, model, FormattingMode.Explicit, default,
            (formatters, _, _, _) => Task.FromResult<IDocumentFormattingEditProvider?>(formatters.Single(f => f.ExtensionId == "first")))).ShouldBeTrue();
        model.GetValue().ShouldBe("1");

        (await Formatting.FormatDocumentWithSelectedProvider(features, model, FormattingMode.Explicit, default,
            (_, _, _, _) => Task.FromResult<IDocumentFormattingEditProvider?>(null))).ShouldBeFalse();

        // no selector: the first (Monaco's), which is the newest registration
        (await Formatting.FormatDocumentWithSelectedProvider(features, model, FormattingMode.Explicit, default)).ShouldBeTrue();
        model.GetValue().ShouldBe("2");
    }

    [Fact]
    public async Task Formatting_is_one_undo_step_and_uses_minimal_edits()
    {
        using var model = Model("if (a) {", "b();", "}");
        model.PushStackElement();
        var provider = new DocumentFormatter(null, m => [new TextEdit(m.GetFullModelRange(), "if (a) {\n    b();\n}")]);
        var edits = await Formatting.ComputeDocumentFormattingEdits(provider, model, default);
        edits.ShouldBe([new TextEdit(R(2, 1, 2, 1), "    ")]);

        (await Formatting.FormatDocumentWithProvider(provider, model, default)).ShouldBeTrue();
        model.GetValue().ShouldBe("if (a) {\n    b();\n}");
        await model.Undo();
        model.GetValue().ShouldBe("if (a) {\nb();\n}");

        // nothing left to change
        (await Formatting.FormatDocumentWithProvider(new DocumentFormatter(null, m => [new TextEdit(m.GetFullModelRange(), m.GetValue())]), model, default)).ShouldBeFalse();
    }

    [Fact]
    public async Task Touching_ranges_are_formatted_as_one()
    {
        using var model = Model("a", "b", "c", "d");
        var asked = new List<TextRange>();
        var provider = new RangeFormatter(null, (_, range) =>
        {
            asked.Add(range);
            return [];
        });

        await Formatting.ComputeDocumentRangesFormattingEdits(provider, model, [R(3, 1, 4, 2), R(1, 1, 2, 2), R(2, 2, 3, 1)], default);
        asked.ShouldBe([R(1, 1, 4, 2)]);
    }

    [Fact]
    public async Task Ranges_whose_edits_intersect_are_formatted_again_as_one()
    {
        using var model = Model("aaaa", "bbbb", "cccc", "dddd");
        var asked = new List<TextRange>();
        var provider = new RangeFormatter(null, (_, range) =>
        {
            asked.Add(range);
            // every answer touches line 2: the edits of the two ranges intersect
            return [new TextEdit(R(2, 1, 2, 2), "B")];
        });

        var edits = await Formatting.ComputeDocumentRangesFormattingEdits(provider, model, [R(1, 1, 1, 3), R(4, 1, 4, 3)], default);
        asked.ShouldBe([R(1, 1, 1, 3), R(4, 1, 4, 3), R(1, 1, 4, 3)]);
        edits.ShouldBe([new TextEdit(R(2, 1, 2, 2), "B")]);
    }

    [Fact]
    public async Task Ranges_formatting_uses_the_multi_range_call_when_supported()
    {
        using var model = Model("a", "b", "c");
        var provider = new RangeFormatter(null, (_, _) => throw new InvalidOperationException("single range asked"))
        {
            RangesEdits = ranges => [.. ranges.Select(r => new TextEdit(new TextRange(r.Start, r.Start), ">"))],
        };

        var edits = await Formatting.ComputeDocumentRangesFormattingEdits(provider, model, [R(1, 1, 1, 2), R(3, 1, 3, 2)], default);
        edits.ShouldBe([new TextEdit(R(1, 1, 1, 1), ">"), new TextEdit(R(3, 1, 3, 1), ">")]);
    }

    [Fact]
    public async Task An_edit_while_the_formatter_runs_cancels_the_result()
    {
        using var model = Model("a");
        var gate = new TaskCompletionSource();
        var provider = new AsyncDocumentFormatter(async () =>
        {
            await gate.Task;
            return [new TextEdit(R(1, 1, 1, 2), "b")];
        });

        var pending = Formatting.ComputeDocumentFormattingEdits(provider, model, default);
        model.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "x")]);
        gate.SetResult();
        (await pending).ShouldBeNull();
    }

    [Fact]
    public async Task On_type_formatting_asks_only_for_trigger_characters()
    {
        var features = new LanguageFeatures();
        using var model = Model("a;");
        var asked = new List<string>();
        features.OnTypeFormattingEditProvider.Register("fmt", new OnTypeFormatter([";", "}"], (position, ch) =>
        {
            asked.Add(ch);
            return [new TextEdit(new TextRange(position, position), " ")];
        }));

        var options = Formatting.GetFormattingOptions(model);
        (await Formatting.GetOnTypeFormattingEdits(features, model, new TextPosition(1, 3), "x", options, default)).ShouldBeNull();
        var edits = await Formatting.GetOnTypeFormattingEdits(features, model, new TextPosition(1, 3), ";", options, default);
        edits.ShouldBe([new TextEdit(R(1, 3, 1, 3), " ")]);
        asked.ShouldBe([";"]);
    }

    [Fact]
    public async Task Until_result_skips_formatters_that_give_nothing_or_throw()
    {
        var features = new LanguageFeatures();
        using var model = Model("a");
        features.DocumentFormattingEditProvider.Register("fmt", new DocumentFormatter(null, _ => [new TextEdit(R(1, 1, 1, 2), "z")]));
        features.DocumentFormattingEditProvider.Register("fmt", new DocumentFormatter(null, _ => throw new InvalidOperationException("broken")));
        features.DocumentFormattingEditProvider.Register("fmt", new DocumentFormatter(null, _ => []));

        var errors = new List<Exception>();
        void OnError(Exception e) => errors.Add(e);
        UnexpectedErrors.Occurred += OnError;
        try
        {
            var edits = await Formatting.GetDocumentFormattingEditsUntilResult(features, model, Formatting.GetFormattingOptions(model), default);
            edits.ShouldBe([new TextEdit(R(1, 1, 1, 2), "z")]);
            errors.Count.ShouldBe(1);
        }
        finally
        {
            UnexpectedErrors.Occurred -= OnError;
        }
    }

    private sealed class DocumentFormatter(string? extensionId, Func<TextModel, IReadOnlyList<TextEdit>> edits) : IDocumentFormattingEditProvider
    {
        public string? ExtensionId => extensionId;

        public Task<IReadOnlyList<TextEdit>?> ProvideDocumentFormattingEdits(TextModel model, FormattingOptions options, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<TextEdit>?>(edits(model));
    }

    private sealed class AsyncDocumentFormatter(Func<Task<IReadOnlyList<TextEdit>?>> edits) : IDocumentFormattingEditProvider
    {
        public Task<IReadOnlyList<TextEdit>?> ProvideDocumentFormattingEdits(TextModel model, FormattingOptions options, CancellationToken token) => edits();
    }

    private sealed class RangeFormatter(string? extensionId, Func<TextModel, TextRange, IReadOnlyList<TextEdit>> edits) : IDocumentRangeFormattingEditProvider
    {
        public Func<IReadOnlyList<TextRange>, IReadOnlyList<TextEdit>>? RangesEdits { get; init; }

        public string? ExtensionId => extensionId;

        public bool SupportsRangesFormatting => RangesEdits is not null;

        public Task<IReadOnlyList<TextEdit>?> ProvideDocumentRangeFormattingEdits(TextModel model, TextRange range, FormattingOptions options, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<TextEdit>?>(edits(model, range));

        public Task<IReadOnlyList<TextEdit>?> ProvideDocumentRangesFormattingEdits(TextModel model, IReadOnlyList<TextRange> ranges, FormattingOptions options, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<TextEdit>?>(RangesEdits!(ranges));
    }

    private sealed class OnTypeFormatter(IReadOnlyList<string> triggers, Func<TextPosition, string, IReadOnlyList<TextEdit>> edits) : IOnTypeFormattingEditProvider
    {
        public IReadOnlyList<string> AutoFormatTriggerCharacters => triggers;

        public Task<IReadOnlyList<TextEdit>?> ProvideOnTypeFormattingEdits(TextModel model, TextPosition position, string ch, FormattingOptions options, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<TextEdit>?>(edits(position, ch));
    }
}
