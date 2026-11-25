// Ported from vscode (src/vs/editor/test/browser/viewModel/viewModelDecorations.test.ts, commit b57d5c9e), MIT license.
// Upstream goes through testViewModel (the whole ViewModel). The ViewModel is not ported yet, so these tests build what
// the ViewModel builds for them: the projected view lines with TestConfiguration's values (ModelLineProjectionTestUtils,
// wrapping indent Same, upstream's default) and a ViewModelDecorations with editor id 1 that filters neither validation
// nor font decorations (upstream's defaults: renderValidationDecorations 'editable' on a writable editor, variable fonts
// allowed). getDecorationsInViewport and getViewportViewLineRenderingData(...).inlineDecorations are what the ViewModel
// does with them (viewModelImpl.ts): the viewport's decorations, and the viewport's inline decorations of the line
// followed by the line's own (injected text) inline decorations.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Suite 'ViewModelDecorations'.</summary>
public sealed class ViewModelDecorationsTests
{
    private const int EditorId = 1;

    private sealed class TestViewModel : IDisposable
    {
        public TestViewModel(string[] text, int wrappingColumn)
        {
            Model = TestTextModel.Create(string.Join("\n", text));
            Lines = ModelLineProjectionTestUtils.CreateLinesCollection(Model, wrappingColumn, WrappingIndent.Same, false);
            Decorations = new ViewModelDecorations(EditorId, Model, () => false, () => false, Lines, Lines.CreateCoordinatesConverter());
            Model.DidChangeDecorations += _ => Decorations.OnModelDecorationsChanged();
        }

        public TextModel Model { get; }

        public ViewModelLinesFromProjectedModel Lines { get; }

        public ViewModelDecorations Decorations { get; }

        public string GetLineContent(int lineNumber) => Lines.GetViewLineContent(lineNumber);

        public int GetLineMinColumn(int lineNumber) => Lines.GetViewLineMinColumn(lineNumber);

        public int GetLineMaxColumn(int lineNumber) => Lines.GetViewLineMaxColumn(lineNumber);

        public IReadOnlyList<ViewModelDecoration> GetDecorationsInViewport(TextRange visibleRange) =>
            Decorations.GetDecorationsViewportData(visibleRange).Decorations;

        public IReadOnlyList<InlineDecoration> GetViewportInlineDecorations(TextRange visibleRange, int lineNumber)
        {
            var collection = Decorations.GetDecorationsViewportData(visibleRange);
            var inlineDecorations = collection.InlineDecorations[lineNumber - visibleRange.StartLineNumber];
            var lineData = Lines.GetViewLineData(lineNumber);
            if (lineData.InlineDecorations is { } own)
            {
                return [.. inlineDecorations, .. own];
            }
            return inlineDecorations;
        }

        public void Dispose()
        {
            Decorations.Dispose();
            Lines.Dispose();
            Model.Dispose();
        }
    }

    private static ModelDecorationOptions CreateOpts(string id) => new("test")
    {
        ClassName = id,
        InlineClassName = "i-" + id,
        BeforeContentClassName = "b-" + id,
        AfterContentClassName = "a-" + id,
    };

    private static InlineDecoration Dec(int startLine, int startColumn, int endLine, int endColumn, string className, InlineDecorationType type) =>
        new(new TextRange(startLine, startColumn, endLine, endColumn), className, type);

    [Fact]
    public void GetDecorationsViewportData()
    {
        using var viewModel = new TestViewModel(["hello world, this is a buffer that will be wrapped"], 13);
        var model = viewModel.Model;

        viewModel.GetLineContent(1).ShouldBe("hello world, ");
        viewModel.GetLineContent(2).ShouldBe("this is a ");
        viewModel.GetLineContent(3).ShouldBe("buffer that ");
        viewModel.GetLineContent(4).ShouldBe("will be ");
        viewModel.GetLineContent(5).ShouldBe("wrapped");

        model.ChangeDecorations(accessor =>
        {
            // VIEWPORT will be (1,14) -> (1,36)

            // completely before viewport
            accessor.AddDecoration(new TextRange(1, 2, 1, 3), CreateOpts("dec1"));
            // starts before viewport, ends at viewport start
            accessor.AddDecoration(new TextRange(1, 2, 1, 14), CreateOpts("dec2"));
            // starts before viewport, ends inside viewport
            accessor.AddDecoration(new TextRange(1, 2, 1, 15), CreateOpts("dec3"));
            // starts before viewport, ends at viewport end
            accessor.AddDecoration(new TextRange(1, 2, 1, 36), CreateOpts("dec4"));
            // starts before viewport, ends after viewport
            accessor.AddDecoration(new TextRange(1, 2, 1, 51), CreateOpts("dec5"));

            // starts at viewport start, ends at viewport start (will not be visible on view line 2)
            accessor.AddDecoration(new TextRange(1, 14, 1, 14), CreateOpts("dec6"));
            // starts at viewport start, ends inside viewport
            accessor.AddDecoration(new TextRange(1, 14, 1, 16), CreateOpts("dec7"));
            // starts at viewport start, ends at viewport end
            accessor.AddDecoration(new TextRange(1, 14, 1, 36), CreateOpts("dec8"));
            // starts at viewport start, ends after viewport
            accessor.AddDecoration(new TextRange(1, 14, 1, 51), CreateOpts("dec9"));

            // starts inside viewport, ends inside viewport
            accessor.AddDecoration(new TextRange(1, 16, 1, 18), CreateOpts("dec10"));
            // starts inside viewport, ends at viewport end
            accessor.AddDecoration(new TextRange(1, 16, 1, 36), CreateOpts("dec11"));
            // starts inside viewport, ends after viewport
            accessor.AddDecoration(new TextRange(1, 16, 1, 51), CreateOpts("dec12"));

            // starts at viewport end, ends at viewport end
            accessor.AddDecoration(new TextRange(1, 36, 1, 36), CreateOpts("dec13"));
            // starts at viewport end, ends after viewport
            accessor.AddDecoration(new TextRange(1, 36, 1, 51), CreateOpts("dec14"));

            // starts after viewport, ends after viewport
            accessor.AddDecoration(new TextRange(1, 40, 1, 51), CreateOpts("dec15"));
            return true;
        });

        var actualDecorations = viewModel.GetDecorationsInViewport(
                new TextRange(2, viewModel.GetLineMinColumn(2), 3, viewModel.GetLineMaxColumn(3)))
            .Select(dec => dec.Options.ClassName)
            .Where(className => !string.IsNullOrEmpty(className))
            .ToArray();

        actualDecorations.ShouldBe(
        [
            "dec1",
            "dec2",
            "dec3",
            "dec4",
            "dec5",
            "dec6",
            "dec7",
            "dec8",
            "dec9",
            "dec10",
            "dec11",
            "dec12",
            "dec13",
            "dec14",
        ]);

        var inlineDecorations1 = viewModel.GetViewportInlineDecorations(
            new TextRange(1, viewModel.GetLineMinColumn(1), 2, viewModel.GetLineMaxColumn(2)),
            1);

        // view line 1: (1,1 -> 1,14)
        inlineDecorations1.ShouldBe(
        [
            Dec(1, 2, 1, 3, "i-dec1", InlineDecorationType.Regular),
            Dec(1, 2, 1, 2, "b-dec1", InlineDecorationType.Before),
            Dec(1, 3, 1, 3, "a-dec1", InlineDecorationType.After),
            Dec(1, 2, 1, 14, "i-dec2", InlineDecorationType.Regular),
            Dec(1, 2, 1, 2, "b-dec2", InlineDecorationType.Before),
            Dec(1, 14, 1, 14, "a-dec2", InlineDecorationType.After),
            Dec(1, 2, 2, 2, "i-dec3", InlineDecorationType.Regular),
            Dec(1, 2, 1, 2, "b-dec3", InlineDecorationType.Before),
            Dec(1, 2, 3, 13, "i-dec4", InlineDecorationType.Regular),
            Dec(1, 2, 1, 2, "b-dec4", InlineDecorationType.Before),
            Dec(1, 2, 5, 8, "i-dec5", InlineDecorationType.Regular),
            Dec(1, 2, 1, 2, "b-dec5", InlineDecorationType.Before),
        ]);

        var inlineDecorations2 = viewModel.GetViewportInlineDecorations(
            new TextRange(2, viewModel.GetLineMinColumn(2), 3, viewModel.GetLineMaxColumn(3)),
            2);

        // view line 2: (1,14 -> 1,24)
        inlineDecorations2.ShouldBe(
        [
            Dec(1, 2, 2, 2, "i-dec3", InlineDecorationType.Regular),
            Dec(2, 2, 2, 2, "a-dec3", InlineDecorationType.After),
            Dec(1, 2, 3, 13, "i-dec4", InlineDecorationType.Regular),
            Dec(1, 2, 5, 8, "i-dec5", InlineDecorationType.Regular),
            Dec(2, 1, 2, 1, "i-dec6", InlineDecorationType.Regular),
            Dec(2, 1, 2, 1, "b-dec6", InlineDecorationType.Before),
            Dec(2, 1, 2, 1, "a-dec6", InlineDecorationType.After),
            Dec(2, 1, 2, 3, "i-dec7", InlineDecorationType.Regular),
            Dec(2, 1, 2, 1, "b-dec7", InlineDecorationType.Before),
            Dec(2, 3, 2, 3, "a-dec7", InlineDecorationType.After),
            Dec(2, 1, 3, 13, "i-dec8", InlineDecorationType.Regular),
            Dec(2, 1, 2, 1, "b-dec8", InlineDecorationType.Before),
            Dec(2, 1, 5, 8, "i-dec9", InlineDecorationType.Regular),
            Dec(2, 1, 2, 1, "b-dec9", InlineDecorationType.Before),
            Dec(2, 3, 2, 5, "i-dec10", InlineDecorationType.Regular),
            Dec(2, 3, 2, 3, "b-dec10", InlineDecorationType.Before),
            Dec(2, 5, 2, 5, "a-dec10", InlineDecorationType.After),
            Dec(2, 3, 3, 13, "i-dec11", InlineDecorationType.Regular),
            Dec(2, 3, 2, 3, "b-dec11", InlineDecorationType.Before),
            Dec(2, 3, 5, 8, "i-dec12", InlineDecorationType.Regular),
            Dec(2, 3, 2, 3, "b-dec12", InlineDecorationType.Before),
        ]);

        var inlineDecorations3 = viewModel.GetViewportInlineDecorations(
            new TextRange(2, viewModel.GetLineMinColumn(2), 3, viewModel.GetLineMaxColumn(3)),
            3);

        // view line 3 (24 -> 36)
        inlineDecorations3.ShouldBe(
        [
            Dec(1, 2, 3, 13, "i-dec4", InlineDecorationType.Regular),
            Dec(3, 13, 3, 13, "a-dec4", InlineDecorationType.After),
            Dec(1, 2, 5, 8, "i-dec5", InlineDecorationType.Regular),
            Dec(2, 1, 3, 13, "i-dec8", InlineDecorationType.Regular),
            Dec(3, 13, 3, 13, "a-dec8", InlineDecorationType.After),
            Dec(2, 1, 5, 8, "i-dec9", InlineDecorationType.Regular),
            Dec(2, 3, 3, 13, "i-dec11", InlineDecorationType.Regular),
            Dec(3, 13, 3, 13, "a-dec11", InlineDecorationType.After),
            Dec(2, 3, 5, 8, "i-dec12", InlineDecorationType.Regular),
        ]);
    }

    [Fact]
    public void Issue_17208_Problem_Scrolling_In_1_8_0()
    {
        using var viewModel = new TestViewModel(["hello world, this is a buffer that will be wrapped"], 13);
        var model = viewModel.Model;

        viewModel.GetLineContent(1).ShouldBe("hello world, ");
        viewModel.GetLineContent(2).ShouldBe("this is a ");
        viewModel.GetLineContent(3).ShouldBe("buffer that ");
        viewModel.GetLineContent(4).ShouldBe("will be ");
        viewModel.GetLineContent(5).ShouldBe("wrapped");

        model.ChangeDecorations(accessor =>
        {
            accessor.AddDecoration(
                new TextRange(1, 50, 1, 51),
                new ModelDecorationOptions("test") { BeforeContentClassName = "dec1" });
            return true;
        });

        var decorations = viewModel.GetDecorationsInViewport(
                new TextRange(2, viewModel.GetLineMinColumn(2), 3, viewModel.GetLineMaxColumn(3)))
            .Where(x => !string.IsNullOrEmpty(x.Options.BeforeContentClassName))
            .ToArray();
        decorations.ShouldBeEmpty();

        var inlineDecorations1 = viewModel.GetViewportInlineDecorations(
            new TextRange(2, viewModel.GetLineMinColumn(2), 3, viewModel.GetLineMaxColumn(3)),
            2);
        inlineDecorations1.ShouldBeEmpty();

        var inlineDecorations2 = viewModel.GetViewportInlineDecorations(
            new TextRange(2, viewModel.GetLineMinColumn(2), 3, viewModel.GetLineMaxColumn(3)),
            3);
        inlineDecorations2.ShouldBeEmpty();
    }

    [Fact]
    public void Issue_37401_Allow_Both_Before_And_After_Decorations_On_Empty_Line()
    {
        // testViewModel with {}: wordWrap 'off', wrapping column -1.
        using var viewModel = new TestViewModel([""], -1);
        var model = viewModel.Model;

        model.ChangeDecorations(accessor =>
        {
            accessor.AddDecoration(
                new TextRange(1, 1, 1, 1),
                new ModelDecorationOptions("test")
                {
                    BeforeContentClassName = "before1",
                    AfterContentClassName = "after1",
                });
            return true;
        });

        var inlineDecorations = viewModel.GetViewportInlineDecorations(new TextRange(1, 1, 1, 1), 1);
        inlineDecorations.ShouldBe(
        [
            Dec(1, 1, 1, 1, "before1", InlineDecorationType.Before),
            Dec(1, 1, 1, 1, "after1", InlineDecorationType.After),
        ]);
    }

    // Written here (not upstream): the viewport cache, and what clears it.

    [Fact]
    public void GetDecorationsViewportData_Caches_The_Last_Viewport_Until_Decorations_Change()
    {
        using var viewModel = new TestViewModel(["hello world"], -1);
        var range = new TextRange(1, 1, 1, 12);

        var first = viewModel.Decorations.GetDecorationsViewportData(range);
        viewModel.Decorations.GetDecorationsViewportData(range).ShouldBeSameAs(first);
        viewModel.Decorations.GetDecorationsViewportData(new TextRange(1, 1, 1, 6)).ShouldNotBeSameAs(first);

        var second = viewModel.Decorations.GetDecorationsViewportData(range);
        second.ShouldNotBeSameAs(first);
        second.Decorations.ShouldBeEmpty();

        viewModel.Model.ChangeDecorations(accessor => accessor.AddDecoration(range, CreateOpts("dec1")));
        var third = viewModel.Decorations.GetDecorationsViewportData(range);
        third.ShouldNotBeSameAs(second);
        third.Decorations.Select(d => d.Options.ClassName).ShouldBe(["dec1"]);

        viewModel.Decorations.OnLineMappingChanged();
        viewModel.Decorations.GetDecorationsViewportData(range).ShouldNotBeSameAs(third);
    }

    [Fact]
    public void GetDecorationsOnLine_And_Filters()
    {
        var filterValidation = false;
        using var model = TestTextModel.Create("hello world");
        using var lines = ModelLineProjectionTestUtils.CreateLinesCollection(model, -1, WrappingIndent.Same, false);
        using var decorations = new ViewModelDecorations(EditorId, model, () => filterValidation, () => false, lines, lines.CreateCoordinatesConverter());

        model.ChangeDecorations(accessor => accessor.AddDecoration(
            new TextRange(1, 1, 1, 6),
            new ModelDecorationOptions("test") { ClassName = DecorationClassNames.EditorErrorDecoration }));
        model.ChangeDecorations(accessor => accessor.AddDecoration(new TextRange(1, 7, 1, 12), CreateOpts("plain")));
        // A decoration of another editor is not shown.
        model.ChangeDecorations(accessor => accessor.AddDecoration(new TextRange(1, 1, 1, 2), CreateOpts("other")), ownerId: 2);

        decorations.GetDecorationsOnLine(1).Decorations.Count.ShouldBe(2);

        filterValidation = true;
        decorations.GetDecorationsOnLine(1).Decorations.Select(d => d.Options.ClassName).ShouldBe(["plain"]);
    }
}
