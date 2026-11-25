// Ported from vscode (src/vs/editor/test/common/viewModel/inlineDecorations.test.ts, commit b57d5c9e), MIT license.
// Suites 'InlineModelDecorationsComputer' (InlineModelDecorationsComputerTests) and
// 'InjectedTextInlineDecorationsComputer' (InjectedTextInlineDecorationsComputerTests). The contexts are small classes
// over delegates and values; upstream's `affectsFont: true` is a font family here, which is what makes AffectsFont true
// on the ported options.

using Shouldly;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class InlineModelDecorationsComputerTests
{
    private static ModelDecoration CreateModelDecoration(string id, TextRange range, ModelDecorationOptions options) =>
        new(id, 0, range, options);

    private sealed class Context(Func<IReadOnlyList<IModelDecoration>> getModelDecorations) : IInlineModelDecorationsComputerContext
    {
        public IReadOnlyList<IModelDecoration> GetModelDecorations(TextRange viewRange, bool onlyMinimapDecorations, bool onlyMarginDecorations) =>
            getModelDecorations();
    }

    private static void ShouldBeLines(IReadOnlyList<IReadOnlyList<InlineDecoration>> actual, params InlineDecoration[][] expected)
    {
        actual.Count.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            actual[i].ShouldBe(expected[i]);
        }
    }

    [Fact]
    public void No_Decorations()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() => []);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        result.Decorations.ShouldBeEmpty();
        ShouldBeLines(result.InlineDecorations, [[]]);
        result.HasVariableFonts.ShouldBe([false]);
    }

    [Fact]
    public void Inline_Class_Name_Decoration_On_A_Single_Line()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 1, 1, 6), new ModelDecorationOptions("test") { InlineClassName = "test-class" }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        result.Decorations.Count.ShouldBe(1);
        ShouldBeLines(result.InlineDecorations, [new InlineDecoration(new TextRange(1, 1, 1, 6), "test-class", InlineDecorationType.Regular)]);
        result.HasVariableFonts.ShouldBe([false]);
    }

    [Fact]
    public void InlineClassName_With_AffectsLetterSpacing()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 1, 1, 6), new ModelDecorationOptions("test")
            {
                InlineClassName = "test-class",
                InlineClassNameAffectsLetterSpacing = true,
            }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        ShouldBeLines(result.InlineDecorations, [new InlineDecoration(new TextRange(1, 1, 1, 6), "test-class", InlineDecorationType.RegularAffectingLetterSpacing)]);
    }

    [Fact]
    public void BeforeContentClassName_Decoration()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 3, 1, 8), new ModelDecorationOptions("test") { BeforeContentClassName = "before-class" }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        ShouldBeLines(result.InlineDecorations, [new InlineDecoration(new TextRange(1, 3, 1, 3), "before-class", InlineDecorationType.Before)]);
    }

    [Fact]
    public void AfterContentClassName_Decoration()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 3, 1, 8), new ModelDecorationOptions("test") { AfterContentClassName = "after-class" }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        ShouldBeLines(result.InlineDecorations, [new InlineDecoration(new TextRange(1, 8, 1, 8), "after-class", InlineDecorationType.After)]);
    }

    [Fact]
    public void All_Decoration_Types_Combined()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 2, 1, 6), new ModelDecorationOptions("test")
            {
                InlineClassName = "inline-class",
                BeforeContentClassName = "before-class",
                AfterContentClassName = "after-class",
            }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        ShouldBeLines(
            result.InlineDecorations,
            [
                new InlineDecoration(new TextRange(1, 2, 1, 6), "inline-class", InlineDecorationType.Regular),
                new InlineDecoration(new TextRange(1, 2, 1, 2), "before-class", InlineDecorationType.Before),
                new InlineDecoration(new TextRange(1, 6, 1, 6), "after-class", InlineDecorationType.After),
            ]);
    }

    [Fact]
    public void Decoration_Spanning_Multiple_Lines()
    {
        using var model = TestTextModel.Create("line one\nline two\nline three");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 3, 3, 5), new ModelDecorationOptions("test") { InlineClassName = "multi-line" }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 3, 11), false, false);
        var expectedInlineDecoration = new InlineDecoration(new TextRange(1, 3, 3, 5), "multi-line", InlineDecorationType.Regular);
        ShouldBeLines(
            result.InlineDecorations,
            [expectedInlineDecoration],
            [expectedInlineDecoration],
            [expectedInlineDecoration]);
    }

    [Fact]
    public void Decoration_With_AffectsFont_Sets_HasVariableFonts()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 1, 1, 6), new ModelDecorationOptions("test")
            {
                InlineClassName = "font-class",
                FontFamily = "monospace",
            }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        result.HasVariableFonts.ShouldBe([true]);
    }

    [Fact]
    public void Multiple_Decorations_On_Different_Lines()
    {
        using var model = TestTextModel.Create("line one\nline two");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 1, 1, 5), new ModelDecorationOptions("test") { InlineClassName = "class-a" }),
            CreateModelDecoration("dec2", new TextRange(2, 1, 2, 5), new ModelDecorationOptions("test") { InlineClassName = "class-b" }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetDecorations(new TextRange(1, 1, 2, 9), false, false);
        ShouldBeLines(
            result.InlineDecorations,
            [new InlineDecoration(new TextRange(1, 1, 1, 5), "class-a", InlineDecorationType.Regular)],
            [new InlineDecoration(new TextRange(2, 1, 2, 5), "class-b", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Decoration_Cache_Is_Used_For_Same_Decoration_Id()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var dec = CreateModelDecoration("dec1", new TextRange(1, 1, 1, 6), new ModelDecorationOptions("test") { InlineClassName = "test-class" });
        var context = new Context(() => [dec]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result1 = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        var result2 = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        result2.Decorations[0].ShouldBeSameAs(result1.Decorations[0]);
    }

    [Fact]
    public void Reset_Clears_Decoration_Cache()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var dec = CreateModelDecoration("dec1", new TextRange(1, 1, 1, 6), new ModelDecorationOptions("test") { InlineClassName = "test-class" });
        var context = new Context(() => [dec]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result1 = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        computer.Reset();
        var result2 = computer.GetDecorations(new TextRange(1, 1, 1, 12), false, false);
        result2.Decorations[0].ShouldNotBeSameAs(result1.Decorations[0]);
    }

    [Fact]
    public void GetInlineDecorations_Returns_Inline_Decorations_For_A_Model_Line()
    {
        using var model = TestTextModel.Create("hello world");
        var coordinatesConverter = new IdentityCoordinatesConverter(model);
        var context = new Context(() =>
        [
            CreateModelDecoration("dec1", new TextRange(1, 1, 1, 6), new ModelDecorationOptions("test") { InlineClassName = "test-class" }),
        ]);
        var computer = new InlineModelDecorationsComputer(context, model, coordinatesConverter);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 1, 1, 6), "test-class", InlineDecorationType.Regular)]);
    }
}

public sealed class InjectedTextInlineDecorationsComputerTests
{
    private sealed class Context(
        IReadOnlyList<ModelDecorationInjectedTextOptions>? injectionOptions,
        IReadOnlyList<int>? injectionOffsets,
        IReadOnlyList<int> breakOffsets,
        int wrappedTextIndentLength,
        int baseViewLineNumber) : IInjectedTextInlineDecorationsComputerContext
    {
        public IReadOnlyList<ModelDecorationInjectedTextOptions>? GetInjectionOptions(int modelLineNumber) => injectionOptions;

        public IReadOnlyList<int>? GetInjectionOffsets(int modelLineNumber) => injectionOffsets;

        public IReadOnlyList<int> GetBreakOffsets(int modelLineNumber) => breakOffsets;

        public int GetWrappedTextIndentLength(int modelLineNumber) => wrappedTextIndentLength;

        public int GetBaseViewLineNumber(int modelLineNumber) => baseViewLineNumber;
    }

    private static ModelDecorationInjectedTextOptions Options(string content, string? inlineClassName = null, double? widthInEm = null, bool inlineClassNameAffectsLetterSpacing = false) =>
        new(content)
        {
            InlineClassName = inlineClassName,
            WidthInEm = widthInEm,
            InlineClassNameAffectsLetterSpacing = inlineClassNameAffectsLetterSpacing,
        };

    private static void ShouldBeLines(IReadOnlyList<IReadOnlyList<InlineDecoration>> actual, params InlineDecoration[][] expected)
    {
        actual.Count.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            actual[i].ShouldBe(expected[i]);
        }
    }

    [Fact]
    public void No_Injections_Returns_Empty()
    {
        var context = new Context(null, null, [10], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Single_Injection_With_InlineClassName_On_A_Single_Output_Line()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("injected", "injected-class")];
        var context = new Context(injectionOptions, [5], [18] /* 10 (original) + 8 (injected) */, 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 6, 1, 14), "injected-class", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Injection_Without_InlineClassName_Produces_No_Inline_Decorations()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("injected")];
        var context = new Context(injectionOptions, [5], [18], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [[] /* empty - no inlineClassName */]);
    }

    [Fact]
    public void Fixed_Width_Injection_Uses_Its_Regular_Inline_Decoration()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options(" ", "fixed-width", widthInEm: 3)];
        var context = new Context(injectionOptions, [5], [11], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 6, 1, 7), "fixed-width", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Injection_With_InlineClassNameAffectsLetterSpacing()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("abc", "ls-class", inlineClassNameAffectsLetterSpacing: true)];
        var context = new Context(injectionOptions, [0], [13] /* 10 + 3 */, 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 1, 1, 4), "ls-class", InlineDecorationType.RegularAffectingLetterSpacing)]);
    }

    [Fact]
    public void Multiple_Injections_On_A_Single_Output_Line()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("AA", "class-a"), Options("BBB", "class-b")];
        var context = new Context(injectionOptions, [2, 5], [15] /* 10 + 2 + 3 */, 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(
            result,
            [
                new InlineDecoration(new TextRange(1, 3, 1, 5), "class-a", InlineDecorationType.Regular),
                new InlineDecoration(new TextRange(1, 8, 1, 11), "class-b", InlineDecorationType.Regular),
            ]);
    }

    [Fact]
    public void Injection_Spanning_Across_Wrapped_Lines()
    {
        // Original text is 20 chars, injection of 10 chars at offset 8
        // Break offsets split at 15 and 30 (two wrapped lines)
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("1234567890", "injected")];
        var context = new Context(injectionOptions, [8], [15, 30], 0, 5);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        // Injected text starts at offset 8 in the input with injections
        // Line 0: [0, 15), injected text occupies [8, 18) -> clipped to [8, 15)
        // Line 1: [15, 30), injected text occupies [8, 18) -> clipped to [15, 18) -> relative: [0, 3)
        ShouldBeLines(
            result,
            [new InlineDecoration(new TextRange(5, 9, 5, 16), "injected", InlineDecorationType.Regular)],
            [new InlineDecoration(new TextRange(6, 1, 6, 4), "injected", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Injection_With_WrappedTextIndentLength_On_Wrapped_Lines()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("12345678901234567890", "injected")];
        var context = new Context(injectionOptions, [0], [15, 30], 4, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        // Line 0 (outputLineIndex 0): no offset, start=0, end=15 -> columns 1 to 16
        // Line 1 (outputLineIndex 1): wrappedTextIndentLength=4, start=4+0=4, end=4+5=9 -> columns 5 to 10
        ShouldBeLines(
            result,
            [new InlineDecoration(new TextRange(1, 1, 1, 16), "injected", InlineDecorationType.Regular)],
            [new InlineDecoration(new TextRange(2, 5, 2, 10), "injected", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Injection_Starting_In_Later_Wrapped_Line()
    {
        // Injection at offset 20 which is past the first line break
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("ab", "late-class")];
        var context = new Context(injectionOptions, [20], [15, 32] /* 30 + 2 */, 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        // Line 0: [0, 15) -> injection at offset 20 is past this line -> empty
        // Line 1: [15, 32) -> injection at offset 20 -> start=20-15=5, end=22-15=7 -> columns 6 to 8
        ShouldBeLines(
            result,
            [],
            [new InlineDecoration(new TextRange(2, 6, 2, 8), "late-class", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Base_View_Line_Number_Offsets_Correctly()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("test", "test-class")];
        var context = new Context(injectionOptions, [0], [14], 0, 10);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(10, 1, 10, 5), "test-class", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Range_Uses_View_Line_Number_Not_Model_Line_Number()
    {
        // Model line 3 maps to view line 7 (e.g. due to previous lines wrapping).
        // The range in the resulting InlineDecoration must use the view line number (7),
        // not the model line number (3) that is passed to getInlineDecorations().
        const int modelLineNumber = 3;
        const int baseViewLineNumber = 7;
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("ghost", "ghost-class")];
        var context = new Context(injectionOptions, [0], [15] /* 10 (original) + 5 (injected) */, 0, baseViewLineNumber);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(modelLineNumber);
        // The range must reference view line 7, not model line 3
        ShouldBeLines(result, [new InlineDecoration(new TextRange(7, 1, 7, 6), "ghost-class", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Range_Uses_View_Line_Number_On_Wrapped_Lines_Not_Model_Line_Number()
    {
        // Model line 2 wraps into view lines 5 and 6.
        // Both output lines must use view line numbers, not model line 2.
        const int modelLineNumber = 2;
        const int baseViewLineNumber = 5;
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("1234567890", "wrap-class")];
        var context = new Context(injectionOptions, [0], [8, 20], 0, baseViewLineNumber);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(modelLineNumber);
        // First wrapped line uses view line 5, second uses view line 6
        ShouldBeLines(
            result,
            [new InlineDecoration(new TextRange(5, 1, 5, 9), "wrap-class", InlineDecorationType.Regular)],
            [new InlineDecoration(new TextRange(6, 1, 6, 3), "wrap-class", InlineDecorationType.Regular)]);
    }

    [Fact]
    public void Spacing_Only_Injection_In_The_Middle_Of_A_Line()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("", "spacer", widthInEm: 1)];
        var context = new Context(injectionOptions, [5], [10], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 6, 1, 6), "spacer", InlineDecorationType.WidthOnly)]);
    }

    [Fact]
    public void Spacing_Only_Injection_At_The_Beginning_Of_A_Line()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("", "spacer", widthInEm: 1)];
        var context = new Context(injectionOptions, [0], [10], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 1, 1, 1), "spacer", InlineDecorationType.WidthOnly)]);
    }

    [Fact]
    public void Spacing_Only_Injection_On_An_Empty_Line()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("", "spacer", widthInEm: 1)];
        var context = new Context(injectionOptions, [0], [0], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 1, 1, 1), "spacer", InlineDecorationType.WidthOnly)]);
    }

    [Fact]
    public void Spacing_Only_Injection_At_A_Wrap_Boundary_Belongs_To_The_Following_Line()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("", "spacer", widthInEm: 1)];
        var context = new Context(injectionOptions, [5], [5, 10], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(
            result,
            [],
            [new InlineDecoration(new TextRange(2, 1, 2, 1), "spacer", InlineDecorationType.WidthOnly)]);
    }

    [Fact]
    public void Spacing_Only_Injection_At_The_End_Of_A_Line()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("", "spacer", widthInEm: 1)];
        var context = new Context(injectionOptions, [10], [10], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        // There is no following output line to move it to, so it stays at the end of this one.
        ShouldBeLines(result, [new InlineDecoration(new TextRange(1, 11, 1, 11), "spacer", InlineDecorationType.WidthOnly)]);
    }

    [Fact]
    public void Spacing_Only_Injection_Next_To_An_Injection_With_Content()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("", "spacer", widthInEm: 1), Options("AB", "text-class")];
        var context = new Context(injectionOptions, [2, 5], [12] /* 10 (original) + 0 + 2 */, 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        // The spacing-only injection must not shift the offsets of the injections that follow it.
        ShouldBeLines(
            result,
            [
                new InlineDecoration(new TextRange(1, 3, 1, 3), "spacer", InlineDecorationType.WidthOnly),
                new InlineDecoration(new TextRange(1, 6, 1, 8), "text-class", InlineDecorationType.Regular),
            ]);
    }

    [Fact]
    public void Spacing_Only_Injection_Without_InlineClassName_Produces_No_Inline_Decorations()
    {
        ModelDecorationInjectedTextOptions[] injectionOptions = [Options("", widthInEm: 1)];
        var context = new Context(injectionOptions, [5], [10], 0, 1);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var result = computer.GetInlineDecorations(1);
        ShouldBeLines(result, [[]]);
    }
}
