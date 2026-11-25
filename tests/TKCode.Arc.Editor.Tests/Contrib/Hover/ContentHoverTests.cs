// Ported from vscode (src/vs/editor/contrib/hover/test/browser/contentHover.test.ts and markerHoverParticipant.test.ts,
// commit b57d5c9e), MIT license. Ported: the two computeHoverPositions tests; of the marker hover tests the computing
// half (the order the rows render in, the accessible text; the rendered rows are tested in the control tests,
// CodeEditorHoverTests); of the copy tests the one without a link detector (copying a decoration's hover text); of the
// verbosity test the provider round trip (the row layout is DOM only). Not ported: the three link-detector copy tests
// (the link detector is another feature). Below them, tests written for this port: the participants against real
// providers, markers and decorations, the hover operation's timing and the computer.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ContentHoverTests
{
    private sealed class Part(TextRange range) : IHoverPart
    {
        public IEditorHoverParticipant Owner => null!;

        public TextRange Range { get; } = range;

        public bool IsValidForHoverAnchor(HoverAnchor anchor) => true;
    }

    [Fact]
    public void Issue_151235_Gitlens_Hover_Shows_Up_In_The_Wrong_Place()
    {
        using var editor = new TestHoverEditor("just some text");
        var actual = HoverUtils.ComputeHoverPositions(editor, new TextRange(5, 5, 5, 5), [new Part(new TextRange(4, 1, 5, 6))]);
        actual.ShowAtPosition.ShouldBe(new TextPosition(5, 5));
        actual.ShowAtSecondaryPosition.ShouldBe(new TextPosition(5, 5));
    }

    [Fact]
    public void Issue_95328_Hover_Placement_With_Word_Wrap()
    {
        using var editor = new TestHoverEditor("just some text", new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 6 });
        var actual = HoverUtils.ComputeHoverPositions(editor, new TextRange(1, 8, 1, 8), [new Part(new TextRange(1, 1, 1, 15))]);
        actual.ShowAtPosition.ShouldBe(new TextPosition(1, 8));
        actual.ShowAtSecondaryPosition.ShouldBe(new TextPosition(1, 6));
    }

    private static Marker CreateMarker(Uri resource, MarkerSeverity severity, string message, int startColumn, int endColumn = 15) => new()
    {
        Owner = "test",
        Resource = resource,
        Severity = severity,
        Message = message,
        StartLineNumber = 1,
        StartColumn = startColumn,
        EndLineNumber = 1,
        EndColumn = endColumn,
    };

    [Fact]
    public void Renders_Diagnostics_By_Severity_Without_Changing_Equal_Severity_Order_Or_Input()
    {
        using var editor = new TestHoverEditor("void foo(List<(baz)> l) {}");
        var participant = new MarkerHoverParticipant(editor);
        MarkerHover CreateHover(MarkerSeverity severity, string message, int startColumn)
        {
            var marker = CreateMarker(editor.TextModel.Uri, severity, message, startColumn);
            return new MarkerHover(participant, marker.Range, marker);
        }

        MarkerHover[] hoverParts =
        [
            CreateHover(MarkerSeverity.Hint, "Hint diagnostic", 10),
            CreateHover(MarkerSeverity.Info, "Info diagnostic", 10),
            CreateHover(MarkerSeverity.Warning, "First warning", 10),
            CreateHover(MarkerSeverity.Warning, "Second warning", 12),
            CreateHover(MarkerSeverity.Error, "Expected to find )", 14),
        ];
        var originalOrder = hoverParts.ToArray();

        var rendered = MarkerHoverParticipant.SortForRendering(hoverParts);

        rendered.Select(p => p.Marker.Message).ShouldBe(["Expected to find )", "First warning", "Second warning", "Info diagnostic", "Hint diagnostic"]);
        rendered.Select(p => MarkerHoverParticipant.GetSeverityLabel(p.Marker.Severity)).ShouldBe(["Error", "Warning", "Warning", "Info", "Hint"]);
        rendered.Select(participant.GetAccessibleContent).ShouldBe(
            ["Error: Expected to find )", "Warning: First warning", "Warning: Second warning", "Info: Info diagnostic", "Hint: Hint diagnostic"]);
        hoverParts.Select((part, index) => ReferenceEquals(part, originalOrder[index])).ShouldAllBe(same => same);
    }

    [Fact]
    public void Preserves_A_Single_Diagnostic_With_Multiline_Text_Code_And_Related_Information()
    {
        using var editor = new TestHoverEditor("some text");
        var participant = new MarkerHoverParticipant(editor);
        var marker = new Marker
        {
            Owner = "test",
            Resource = editor.TextModel.Uri,
            Severity = MarkerSeverity.Warning,
            Message = "First line\nSecond line",
            Source = "test-source",
            Code = "test-code",
            CodeTarget = new Uri("https://example.com/diagnostic"),
            StartLineNumber = 1,
            StartColumn = 1,
            EndLineNumber = 1,
            EndColumn = 5,
            RelatedInformation = [new RelatedInformation(new Uri("file:///related.ts"), "Related message", 2, 3, 2, 6)],
        };
        var hoverPart = new MarkerHover(participant, marker.Range, marker);

        MarkerHoverParticipant.SortForRendering([hoverPart]).ShouldBe([hoverPart]);
        MarkerHoverParticipant.FormatRelatedInformation(marker.RelatedInformation![0]).ShouldBe("related.ts(2, 3): Related message");
        participant.GetAccessibleContent(hoverPart).ShouldBe("Warning: First line\nSecond line\nrelated.ts(2, 3): Related message");
    }

    [Fact]
    public void Copies_Ordinary_Markdown_Hover_Text_Rather_Than_Its_Link_Destination()
    {
        using var editor = new TestHoverEditor("link text");
        var message = new MarkdownString(HoverMarkdown.AppendText("More information: ") + "[Documentation](https://example.com)");
        editor.TextModel.DeltaDecorations(null,
        [
            new ModelDeltaDecoration(new TextRange(1, 1, 1, 10), new ModelDecorationOptions("test-markdown-hover") { HoverMessage = [message] }),
        ]);
        var participant = new MarkdownHoverParticipant(editor);
        var anchor = new HoverRangeAnchor(0, new TextRange(1, 1, 1, 1), null, null);

        var parts = participant.ComputeSync(anchor, editor.GetLineDecorations(1), HoverStartSource.Mouse);

        var part = parts.ShouldHaveSingleItem().ShouldBeOfType<MarkdownHover>();
        participant.GetAccessibleContent(part).ShouldBe("More information: Documentation");
        participant.GetCopyContent(part).ShouldBe("More information: Documentation");
    }

    [Fact]
    public void Verbosity_Request_Asks_The_Provider_Again_With_The_Delta() => FakeTimers.Run(async _ =>
    {
        using var editor = new TestHoverEditor("some text");
        var range = new TextRange(1, 1, 1, 5);
        var contexts = new List<HoverContext?>();
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, context) =>
        {
            contexts.Add(context);
            var lineCount = context?.VerbosityRequest is not null ? 6 : 1;
            return new Hover([new MarkdownString(string.Join("\n\n", Enumerable.Range(0, lineCount).Select(i => $"Line {i}")))])
            {
                Range = range,
                CanIncreaseVerbosity = true,
                CanDecreaseVerbosity = context?.VerbosityRequest is not null,
            };
        }));
        var participant = new MarkdownHoverParticipant(editor);

        var parts = new List<IHoverPart>();
        await foreach (var part in participant.ComputeAsync(new HoverRangeAnchor(0, range, null, null), [], HoverStartSource.Keyboard, CancellationToken.None)!)
        {
            parts.Add(part);
        }

        var first = parts.ShouldHaveSingleItem().ShouldBeOfType<MarkdownHover>();
        first.Source!.SupportsVerbosityAction(HoverVerbosityAction.Increase).ShouldBeTrue();
        first.Source.SupportsVerbosityAction(HoverVerbosityAction.Decrease).ShouldBeFalse();

        var more = await participant.UpdateMarkdownHoverVerbosityLevel(first, HoverVerbosityAction.Increase);

        more.ShouldNotBeNull();
        HoverMarkdown.ToPlainText(more.Contents[0].Value).Split('\n').Length.ShouldBe(6);
        more.Source!.SupportsVerbosityAction(HoverVerbosityAction.Decrease).ShouldBeTrue();
        contexts[^1]!.VerbosityRequest!.VerbosityDelta.ShouldBe(1);
        contexts[^1]!.VerbosityRequest!.PreviousHover.ShouldBeSameAs(first.Source.Hover);
    });

    // ---- own tests

    [Fact]
    public void Marker_Participant_Finds_The_Markers_Under_The_Anchor()
    {
        using var editor = new TestHoverEditor("let nme = 1;\nnext line");
        editor.SetMarkers(
            new MarkerData { Severity = MarkerSeverity.Error, Message = "cannot find nme", StartLineNumber = 1, StartColumn = 5, EndLineNumber = 1, EndColumn = 8 },
            new MarkerData { Severity = MarkerSeverity.Warning, Message = "unused", StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 13 },
            new MarkerData { Severity = MarkerSeverity.Info, Message = "elsewhere", StartLineNumber = 2, StartColumn = 1, EndLineNumber = 2, EndColumn = 5 });
        var participant = new MarkerHoverParticipant(editor);

        IReadOnlyList<string> At(int column)
        {
            var anchor = new HoverRangeAnchor(0, new TextRange(1, column, 1, column), null, null);
            var decorations = ContentHoverComputer.GetLineDecorations(editor, anchor);
            return participant.ComputeSync(anchor, decorations, HoverStartSource.Mouse).Cast<MarkerHover>().Select(m => m.Marker.Message).ToList();
        }

        At(6).ShouldBe(["cannot find nme", "unused"], ignoreOrder: true);
        At(2).ShouldBe(["unused"]);
    }

    [Fact]
    public void Markdown_Participant_Shows_Decoration_Hovers_And_Long_Line_Messages()
    {
        using var editor = new TestHoverEditor(new string('x', 30));
        editor.HoverOptions = HoverOptions.Default with { StopRenderingLineAfter = 10 };
        var participant = new MarkdownHoverParticipant(editor);
        var anchor = new HoverRangeAnchor(0, new TextRange(1, 20, 1, 20), null, null);

        var parts = participant.ComputeSync(anchor, [], HoverStartSource.Mouse).Cast<MarkdownHover>().ToList();

        parts.ShouldHaveSingleItem().Contents[0].Value.ShouldStartWith("Rendering paused for long line");

        editor.HoverOptions = editor.HoverOptions with { ShowLongLineWarning = false };
        participant.ComputeSync(anchor, [], HoverStartSource.Mouse).ShouldBeEmpty();
    }

    [Fact]
    public void Line_Decorations_Are_Those_That_Touch_The_Anchor()
    {
        using var editor = new TestHoverEditor("abcdefghij");
        editor.TextModel.DeltaDecorations(null,
        [
            new ModelDeltaDecoration(new TextRange(1, 2, 1, 4), new ModelDecorationOptions("a")),
            new ModelDeltaDecoration(new TextRange(1, 6, 1, 6), new ModelDecorationOptions("collapsed") { ShowIfCollapsed = true }),
            new ModelDeltaDecoration(new TextRange(1, 1, 1, 1), new ModelDecorationOptions("whole") { IsWholeLine = true }),
        ]);

        IReadOnlyList<string> At(int column) =>
            ContentHoverComputer.GetLineDecorations(editor, new HoverRangeAnchor(0, new TextRange(1, column, 1, column), null, null))
                .Select(d => d.Options.Description).OrderBy(s => s, StringComparer.Ordinal).ToList();

        At(3).ShouldBe(["a", "whole"]);
        At(5).ShouldBe(["collapsed", "whole"]);
        At(9).ShouldBe(["whole"]);
    }

    [Fact]
    public void Hover_Operation_Waits_For_The_Hover_Time_And_Shows_Loading_While_Providers_Run() => FakeTimers.Run(async timers =>
    {
        using var editor = new TestHoverEditor("hello world");
        var pending = new TaskCompletionSource<Hover?>();
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _) => pending.Task));
        editor.SetMarkers(new MarkerData { Severity = MarkerSeverity.Error, Message = "bad", StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 6 });

        var markdown = new MarkdownHoverParticipant(editor);
        IEditorHoverParticipant[] participants = [new MarkerHoverParticipant(editor), markdown];
        var computer = new ContentHoverComputer(editor, participants);
        using var operation = new HoverOperation<ContentHoverComputerOptions, IHoverPart>(timers.Scheduler, computer, () => 300);
        var results = new List<HoverResult<ContentHoverComputerOptions, IHoverPart>>();
        operation.OnResult += results.Add;

        var anchor = new HoverRangeAnchor(0, new TextRange(1, 3, 1, 3), null, null);
        operation.Start(HoverStartMode.Delayed, new ContentHoverComputerOptions(anchor, HoverStartSource.Mouse, false, false));

        timers.Advance(299);
        results.ShouldBeEmpty();

        timers.Advance(1);
        var first = results.ShouldHaveSingleItem();
        first.IsComplete.ShouldBeFalse();
        first.HasLoadingMessage.ShouldBeFalse();
        first.Value.ShouldHaveSingleItem().ShouldBeOfType<MarkerHover>();

        timers.Advance(600);
        results[^1].HasLoadingMessage.ShouldBeTrue();

        pending.SetResult(new Hover([new MarkdownString("**docs**")]));
        await Task.Yield();
        timers.RunPosts();

        var last = results[^1];
        last.IsComplete.ShouldBeTrue();
        last.Value.Count.ShouldBe(2);
        last.Value.OfType<MarkdownHover>().ShouldHaveSingleItem().Range.ShouldBe(anchor.Range);
    });

    [Fact]
    public void Hover_Operation_Immediate_Shows_At_Once_And_Cancel_Drops_Late_Results() => FakeTimers.Run(async timers =>
    {
        using var editor = new TestHoverEditor("hello world");
        var pending = new TaskCompletionSource<Hover?>();
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _) => pending.Task));
        var computer = new ContentHoverComputer(editor, [new MarkdownHoverParticipant(editor)]);
        using var operation = new HoverOperation<ContentHoverComputerOptions, IHoverPart>(timers.Scheduler, computer, () => 300);
        var results = new List<HoverResult<ContentHoverComputerOptions, IHoverPart>>();
        operation.OnResult += results.Add;

        var anchor = new HoverRangeAnchor(0, new TextRange(1, 3, 1, 3), null, null);
        operation.Start(HoverStartMode.Immediate, new ContentHoverComputerOptions(anchor, HoverStartSource.Keyboard, true, false));
        results.ShouldHaveSingleItem().Value.ShouldBeEmpty();
        results[0].IsComplete.ShouldBeFalse();

        operation.Cancel();
        pending.SetResult(new Hover([new MarkdownString("late")]));
        await Task.Yield();
        timers.RunPosts();
        results.Count.ShouldBe(1);
        operation.Options.ShouldBeNull();
    });

    [Fact]
    public void Filtered_Result_Keeps_The_Parts_Valid_For_A_New_Anchor()
    {
        using var editor = new TestHoverEditor("abcdefghij");
        var participant = new MarkdownHoverParticipant(editor);
        var wide = new MarkdownHover(participant, new TextRange(1, 1, 1, 10), [new MarkdownString("wide")], false, 0);
        var narrow = new MarkdownHover(participant, new TextRange(1, 1, 1, 3), [new MarkdownString("narrow")], false, 1);
        var anchor = new HoverRangeAnchor(0, new TextRange(1, 2, 1, 2), null, null);
        var result = new ContentHoverResult([wide, narrow], true, new ContentHoverComputerOptions(anchor, HoverStartSource.Mouse, false, false));

        result.Filter(anchor).ShouldBeSameAs(result);
        var filtered = result.Filter(new HoverRangeAnchor(0, new TextRange(1, 6, 1, 6), null, null));
        filtered.HoverParts.ShouldBe([wide]);
        filtered.Filter(anchor).ShouldBeSameAs(result);
    }

    [Fact]
    public void Get_Hover_Drops_Empty_And_Failing_Providers()
    {
        using var editor = new TestHoverEditor("hello");
        using var a = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _) => Task.FromResult<Hover?>(new Hover([new MarkdownString("one")]))));
        using var b = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _) => Task.FromResult<Hover?>(new Hover([]))));
        using var c = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((Func<TextModel, HoverContext?, Hover?>)((_, _) => throw new InvalidOperationException("boom"))));

        var errors = new List<Exception>();
        void OnError(Exception e)
        {
            if (e.Message == "boom")
            {
                lock (errors)
                {
                    errors.Add(e);
                }
            }
        }

        UnexpectedErrors.Occurred += OnError;
        try
        {
            var hovers = GetHover.GetHoversPromise(editor.LanguageFeatures.HoverProvider, editor.TextModel, new TextPosition(1, 2), CancellationToken.None).GetAwaiter().GetResult();
            hovers.ShouldHaveSingleItem().Contents[0].Value.ShouldBe("one");
        }
        finally
        {
            UnexpectedErrors.Occurred -= OnError;
        }

        errors.ShouldNotBeEmpty();
    }

    private sealed class HoverProvider(Func<TextModel, HoverContext?, Task<Hover?>> provide) : IHoverProvider
    {
        public HoverProvider(Func<TextModel, HoverContext?, Hover?> provide)
            : this((model, context) => Task.FromResult(provide(model, context)))
        {
        }

        public Task<Hover?> ProvideHover(TextModel model, TextPosition position, CancellationToken token, HoverContext? context = null) =>
            provide(model, context);
    }
}
