// Ported from vscode (src/vs/editor/test/common/model/modelInjectedText.test.ts, commit b57d5c9e), MIT license.
// The spy view model implements IViewModelContentListener directly instead of mock<IViewModel>(). Upstream checks that undo() returns
// undefined (it ran synchronously); here Undo returns a ValueTask, which must have completed before it is awaited.

using Shouldly;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ModelInjectedTextTests
{
    private sealed class SpyViewModel : IViewModelContentListener
    {
        public List<object> RecordedChanges { get; } = new();

        public void OnDidChangeContentOrInjectedText(IModelContentOrInjectedTextChangedEvent e)
        {
            IEnumerable<ModelRawChange> changes = e switch
            {
                InternalModelContentChangeEvent content => content.RawContentChangedEvent.Changes,
                ModelInjectedTextChangedEvent injected => injected.Changes,
                _ => [],
            };
            foreach (var change in changes)
            {
                RecordedChanges.Add(MapChange(change));
            }
        }

        public void EmitContentChangeEvent(IModelContentOrInjectedTextChangedEvent e)
        {
        }

        /// <summary>Upstream's <c>recordedChanges.splice(0)</c>: returns the recorded changes and clears them.</summary>
        public List<object> Splice()
        {
            var result = RecordedChanges.ToList();
            RecordedChanges.Clear();
            return result;
        }
    }

    private sealed record LineChanged(int LineNumber, int LineNumberPostEdit);

    private sealed record LinesInserted(int FromLineNumber, int Count);

    private sealed record LinesDeleted;

    private sealed record EolChanged;

    private sealed record Flush;

    private sealed record Unknown;

    private static object MapChange(ModelRawChange change) => change switch
    {
        ModelRawLineChanged c => new LineChanged(c.LineNumber, c.LineNumberPostEdit),
        ModelRawLinesInserted c => new LinesInserted(c.FromLineNumber, c.Count),
        ModelRawLinesDeleted => new LinesDeleted(),
        ModelRawEOLChanged => new EolChanged(),
        ModelRawFlush => new Flush(),
        _ => new Unknown(),
    };

    [Fact]
    public async Task Basic()
    {
        using var thisModel = TestTextModel.Create("First Line\nSecond Line");

        var spyViewModel = new SpyViewModel();
        thisModel.RegisterViewModel(spyViewModel);

        // Initial decoration
        var decorations = thisModel.DeltaDecorations([],
        [
            new ModelDeltaDecoration(
                new TextRange(1, 1, 1, 1),
                new ModelDecorationOptions("test1")
                {
                    After = new ModelDecorationInjectedTextOptions("injected1"),
                    ShowIfCollapsed = true,
                }),
        ]);
        spyViewModel.Splice().ShouldBe(
        [
            new LineChanged(1, 1),
        ]);

        // Decoration change
        decorations = thisModel.DeltaDecorations(decorations,
        [
            new ModelDeltaDecoration(
                new TextRange(2, 1, 2, 1),
                new ModelDecorationOptions("test1")
                {
                    After = new ModelDecorationInjectedTextOptions("injected1"),
                    ShowIfCollapsed = true,
                }),
            new ModelDeltaDecoration(
                new TextRange(2, 2, 2, 2),
                new ModelDecorationOptions("test2")
                {
                    After = new ModelDecorationInjectedTextOptions("injected2"),
                    ShowIfCollapsed = true,
                }),
        ]);
        spyViewModel.Splice().ShouldBe(
        [
            new LineChanged(1, 1),
            new LineChanged(2, 2),
        ]);

        // Simple Insert
        thisModel.ApplyEdits([EditOperation.Replace(new TextRange(2, 2, 2, 2), "Hello")]);
        spyViewModel.Splice().ShouldBe(
        [
            new LineChanged(2, 2),
        ]);

        // Multi-Line Insert
        thisModel.PushEditOperations(null, [EditOperation.Replace(new TextRange(2, 2, 2, 2), "\n\n\n")], null);
        thisModel.GetAllDecorations().Select(d => (Description: d.Options.Description, Range: d.Range.ToString())).ShouldBe(
        [
            ("test1", "[2,1 -> 2,1]"),
            ("test2", "[2,2 -> 5,6]"),
        ]);
        spyViewModel.Splice().ShouldBe(
        [
            new LineChanged(2, 2),
            new LinesInserted(3, 3),
        ]);

        // Multi-Line Replace
        thisModel.PushEditOperations(null, [EditOperation.Replace(new TextRange(3, 1, 5, 1), "\n\n\n\n\n\n\n\n\n\n\n\n\n")], null);
        spyViewModel.Splice().ShouldBe(
        [
            new LineChanged(5, 5),
            new LineChanged(4, 4),
            new LineChanged(3, 3),
            new LinesInserted(6, 11),
        ]);

        // Multi-Line Replace undo
        var undo = thisModel.Undo();
        undo.IsCompletedSuccessfully.ShouldBeTrue();
        await undo;
        spyViewModel.Splice().ShouldBe(
        [
            new LineChanged(2, 2),
            new LinesDeleted(),
        ]);

        thisModel.UnregisterViewModel(spyViewModel);
    }
}
