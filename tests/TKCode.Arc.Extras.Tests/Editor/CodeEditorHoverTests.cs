// The content hover in a real editor (upstream's contentHoverController.ts, contentHoverWidgetWrapper.ts and the
// rendering of markerHoverParticipant.ts / markdownHoverParticipant.ts, commit b57d5c9e). The two marker rendering tests
// port the rendering half of upstream's markerHoverParticipant.test.ts; the rest were written for this port.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using static TKCode.Arc.Tests.Editor.HoverTestSupport;

namespace TKCode.Arc.Tests.Editor;

public class CodeEditorHoverTests
{
    private static (CodeEditor Editor, CodeEditorView View, Window Window, ContentHoverController Controller, ManualTimers Timers) Show(string text, double height = 200)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = height };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        var controller = ContentHoverController.Get(editor).ShouldNotBeNull();
        var timers = new ManualTimers();
        controller.SetScheduler(timers.Scheduler);
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window, controller, timers);
    }

    private static string Lines(int count) => string.Join('\n', Enumerable.Range(1, count).Select(i => "line " + i));

    [AvaloniaFact]
    public void Resting_the_pointer_on_text_shows_the_provider_hover_after_the_delay()
    {
        var (editor, view, window, controller, timers) = Show("1\n2\n3\n4\nlet value = 1;\nlast");
        var provider = new HoverProvider((_, position, _) => new Hover([new MarkdownString($"**value** at {position.LineNumber}:{position.Column}")]));
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", provider);

        window.MouseMove(At(window, view, 5, 6));
        timers.Advance(299);
        controller.IsHoverVisible.ShouldBeFalse();

        timers.Advance(1);
        Render(window);
        controller.IsHoverVisible.ShouldBeTrue();
        provider.Calls.ShouldBe(1);
        var widget = controller.Widget.ShouldNotBeNull();
        TextOf(widget.Surface).ShouldContain("value at 5:6");
        editor.ContextKeyService.GetContextKeyValue("editorHoverVisible").ShouldBe(true);

        // above the line when there is room (editor.hover.above)
        var placement = editor.GetContentWidgetPlacement(ContentHoverWidget.WidgetId).ShouldNotBeNull();
        placement.Preference.ShouldBe(ContentWidgetPositionPreference.Above);
        placement.Bounds.Bottom.ShouldBe(view.GetY(5), 0.5);

        // the bold run is bold
        widget.Surface.GetVisualDescendants().OfType<HoverTextBlock>().First().Inlines!.OfType<Run>()
            .Single(r => r.Text == "value").FontWeight.ShouldBe(FontWeight.Bold);
    }

    [AvaloniaFact]
    public void On_the_first_line_the_hover_goes_below_and_a_key_hides_it()
    {
        var (editor, view, window, controller, timers) = Show("hello world\nsecond");
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _, _) => new Hover([new MarkdownString("docs")])));

        window.MouseMove(At(window, view, 1, 3));
        timers.Advance(300);
        Render(window);
        controller.IsHoverVisible.ShouldBeTrue();
        editor.GetContentWidgetPlacement(ContentHoverWidget.WidgetId)!.Preference.ShouldBe(ContentWidgetPositionPreference.Below);

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        controller.IsHoverVisible.ShouldBeFalse();
        editor.ContextKeyService.GetContextKeyValue("editorHoverVisible").ShouldBe(false);
    }

    [AvaloniaFact]
    public void Nothing_shows_without_providers_markers_or_decorations()
    {
        var (_, view, window, controller, timers) = Show("plain text");
        window.MouseMove(At(window, view, 1, 3));
        timers.Advance(1000);
        controller.IsHoverVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Problems_at_the_pointer_show_most_severe_first_with_view_problem()
    {
        var (editor, view, window, controller, timers) = Show("void foo(List<(baz)> l) {}");
        editor.MarkerService.ChangeOne("test", editor.Model.Uri,
        [
            Marker(MarkerSeverity.Hint, "Hint diagnostic", 14),
            Marker(MarkerSeverity.Info, "Info diagnostic", 10),
            Marker(MarkerSeverity.Warning, "First warning", 10),
            Marker(MarkerSeverity.Warning, "Second warning", 12),
            Marker(MarkerSeverity.Error, "Expected to find )", 14),
        ]);
        Render(window);

        window.MouseMove(At(window, view, 1, 15));
        timers.Advance(300);
        Render(window);

        var rendered = controller.Widget!.RenderedHover.ShouldNotBeNull();
        rendered.RenderedParts.Select(p => ((MarkerHover)p.HoverPart).Marker.Message)
            .ShouldBe(["Expected to find )", "First warning", "Second warning", "Info diagnostic", "Hint diagnostic"]);
        rendered.RenderedParts.Select(p => Avalonia.Automation.AutomationProperties.GetName(p.Row.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PART_Severity")))
            .ShouldBe(["Error", "Warning", "Warning", "Info", "Hint"]);
        Enumerable.Range(0, 5).Select(rendered.GetAccessibleWidgetContentAtIndex)
            .ShouldBe(["Error: Expected to find )", "Warning: First warning", "Warning: Second warning", "Info: Info diagnostic", "Hint: Hint diagnostic"]);

        var statusBar = rendered.StatusBar.ShouldNotBeNull();
        TextOf(statusBar).ShouldContain("View Problem");
    }

    [AvaloniaFact]
    public void A_problem_shows_its_multiline_message_code_link_and_related_information()
    {
        var (editor, view, window, controller, timers) = Show("some text");
        editor.MarkerService.ChangeOne("test", editor.Model.Uri,
        [
            new MarkerData
            {
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
            },
        ]);
        var opened = new List<string>();
        controller.OpenLinkHandler = (target, _) =>
        {
            opened.Add(target);
            return true;
        };

        window.MouseMove(At(window, view, 1, 2));
        timers.Advance(300);
        Render(window);

        var row = controller.Widget!.RenderedHover!.RenderedParts.ShouldHaveSingleItem().Row;
        var blocks = row.GetVisualDescendants().OfType<HoverTextBlock>().ToList();
        var message = blocks.Single(b => b.Name == "PART_MarkerMessage");
        var text = string.Concat(message.Inlines!.OfType<Run>().Select(r => r.Text));
        text.ShouldBe("First line\nSecond line  test-source(test-code)");
        message.Links.ShouldHaveSingleItem().Href.ShouldBe("https://example.com/diagnostic");
        string.Concat(blocks[1].Inlines!.OfType<Run>().Select(r => r.Text)).ShouldBe("related.ts(2, 3): Related message");
        controller.Widget.RenderedHover.GetAccessibleWidgetContent().ShouldBe("Warning: First line\nSecond line\nrelated.ts(2, 3): Related message");

        // clicking the code opens it
        var (start, length, _) = message.Links[0];
        var bounds = message.TextLayout.HitTestTextRange(start, length).First();
        message.OpenLinkAt(bounds.Center).ShouldBeTrue();
        opened.ShouldBe(["https://example.com/diagnostic"]);
    }

    private static MarkerData Marker(MarkerSeverity severity, string message, int startColumn) => new()
    {
        Severity = severity,
        Message = message,
        StartLineNumber = 1,
        StartColumn = startColumn,
        EndLineNumber = 1,
        EndColumn = 27,
    };

    [AvaloniaFact]
    public void Ctrl_K_Ctrl_I_shows_the_hover_at_the_cursor_at_once_and_again_focuses_it()
    {
        var (editor, _, window, controller, _) = Show("alpha beta");
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _, _) => new Hover([new MarkdownString("about beta")])));
        editor.SetPosition(new TextPosition(1, 8));

        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd);
        window.KeyPressQwerty(PhysicalKey.I, CtrlCmd);
        Render(window);

        controller.IsHoverVisible.ShouldBeTrue();
        controller.Widget!.IsVisibleFromKeyboard.ShouldBeTrue();
        controller.Widget.IsFocused.ShouldBeFalse();

        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd);
        window.KeyPressQwerty(PhysicalKey.I, CtrlCmd);
        Dispatcher.UIThread.RunJobs();
        controller.IsHoverVisible.ShouldBeTrue();
        controller.Widget.IsFocused.ShouldBeTrue();
        editor.ContextKeyService.GetContextKeyValue("editorHoverFocused").ShouldBe(true);

        // Escape in the focused hover hides it and gives the editor its focus back
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        controller.IsHoverVisible.ShouldBeFalse();
        editor.IsKeyboardFocusWithin.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_sticky_hover_stays_while_the_pointer_moves_into_it_and_scrolling_hides_it()
    {
        var (editor, view, window, controller, timers) = Show(Lines(40));
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _, _) => new Hover([new MarkdownString("docs for the line")])));

        window.MouseMove(At(window, view, 5, 2));
        timers.Advance(300);
        Render(window);
        controller.IsHoverVisible.ShouldBeTrue();
        var bounds = editor.GetContentWidgetPlacement(ContentHoverWidget.WidgetId)!.Bounds;

        // toward the hover (it is above line 5), onto it over line 4: kept
        window.MouseMove(At(window, view, 4, 2));
        timers.Advance(1000);
        controller.IsHoverVisible.ShouldBeTrue();

        // onto the hover: the view sees the pointer leave, the hover stays
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        window.MouseMove(new Point(origin.X + bounds.Center.X, origin.Y + bounds.Center.Y));
        timers.Advance(1000);
        controller.IsHoverVisible.ShouldBeTrue();

        // scrolling hides it
        view.ViewModel!.ViewLayout.SetScrollPosition(new NewScrollPosition(null, view.LineHeight * 3), TKCode.Arc.Editor.ViewModel.ScrollType.Immediate);
        Dispatcher.UIThread.RunJobs();
        controller.IsHoverVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Moving_away_from_a_sticky_hover_hides_it_after_the_hiding_delay()
    {
        var (editor, view, window, controller, timers) = Show(Lines(20));
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, position, _) =>
            position.LineNumber == 3 ? new Hover([new MarkdownString("only line 3")]) : null));

        window.MouseMove(At(window, view, 3, 2));
        timers.Advance(300);
        Render(window);
        controller.IsHoverVisible.ShouldBeTrue();

        // away from the hover (it is above line 3): first the grace period, then the hover goes
        window.MouseMove(At(window, view, 8, 4));
        window.MouseMove(At(window, view, 9, 4));
        controller.IsHoverVisible.ShouldBeTrue();
        timers.Advance(300);
        timers.Advance(600);
        controller.IsHoverVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Code_blocks_are_coloured_with_the_editor_tokenizer_and_links_open()
    {
        var (editor, view, window, controller, timers) = Show("call()");
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _, _) => new Hover(
        [
            new MarkdownString("```typescript\nfunction call(): void // comment\n```\nSee [the docs](https://example.com/docs) and [run](command:editor.action.hideHover).") { IsTrusted = true },
        ])));
        var opened = new List<(string, bool)>();
        controller.OpenLinkHandler = (target, trusted) =>
        {
            opened.Add((target, trusted));
            return true;
        };

        window.MouseMove(At(window, view, 1, 2));
        timers.Advance(300);
        Render(window);

        var surface = controller.Widget!.Surface;
        var code = surface.GetVisualDescendants().OfType<TextBlock>().First(t => t is not HoverTextBlock && t.Inlines is { Count: > 1 });
        var runs = code.Inlines!.OfType<Run>().ToList();
        string.Concat(runs.Select(r => r.Text)).ShouldBe("function call(): void // comment");
        runs.Select(r => (r.Foreground as ISolidColorBrush)?.Color).Distinct().Count().ShouldBeGreaterThan(1);

        var paragraph = surface.GetVisualDescendants().OfType<HoverTextBlock>().Single(t => t.Links.Count == 2);
        paragraph.Links.Select(l => l.Href).ShouldBe(["https://example.com/docs", "command:editor.action.hideHover"]);
        var first = paragraph.TextLayout.HitTestTextRange(paragraph.Links[0].Start, paragraph.Links[0].Length).First();
        paragraph.OpenLinkAt(first.Center).ShouldBeTrue();
        opened.ShouldBe([("https://example.com/docs", true)]);
    }

    [AvaloniaFact]
    public void A_trusted_command_link_runs_the_command_without_a_handler()
    {
        var (editor, _, _, controller, _) = Show("x");
        controller.ShowContentHover(new TextRange(1, 1, 1, 1), HoverStartMode.Immediate, HoverStartSource.Keyboard, false);
        controller.OpenLink("command:editor.action.hideHover", isTrusted: false).ShouldBeFalse();
        controller.OpenLink("command:editor.action.hideHover", isTrusted: true).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Verbosity_buttons_ask_the_provider_for_more()
    {
        var (editor, view, window, controller, timers) = Show("some text");
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _, context) =>
        {
            var lineCount = context?.VerbosityRequest is not null ? 3 : 1;
            return new Hover([new MarkdownString(string.Join("\n\n", Enumerable.Range(0, lineCount).Select(i => $"Line {i}")))])
            {
                CanIncreaseVerbosity = true,
                CanDecreaseVerbosity = context?.VerbosityRequest is not null,
            };
        }));

        window.MouseMove(At(window, view, 1, 2));
        timers.Advance(300);
        Render(window);
        var rendered = controller.Widget!.RenderedHover!;
        rendered.DoesHoverAtIndexSupportVerbosityAction(0, HoverVerbosityAction.Increase).ShouldBeTrue();
        rendered.DoesHoverAtIndexSupportVerbosityAction(0, HoverVerbosityAction.Decrease).ShouldBeFalse();

        editor.ExecuteCommand(ContentHoverController.IncreaseHoverVerbosityActionId, 0);
        Dispatcher.UIThread.RunJobs();
        Render(window);
        TextOf(rendered.RenderedParts[0].Row).ShouldContain("Line 2");
        rendered.DoesHoverAtIndexSupportVerbosityAction(0, HoverVerbosityAction.Decrease).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Hover_on_keyboard_modifier_needs_the_modifier()
    {
        var (editor, view, window, controller, timers) = Show("hello world");
        controller.Options = HoverOptions.Default with { Enabled = HoverEnabled.OnKeyboardModifier };
        using var registration = editor.LanguageFeatures.HoverProvider.Register("*", new HoverProvider((_, _, _) => new Hover([new MarkdownString("docs")])));

        window.MouseMove(At(window, view, 1, 3));
        timers.Advance(1000);
        controller.IsHoverVisible.ShouldBeFalse();

        window.MouseMove(At(window, view, 1, 4), CtrlCmd == RawInputModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control);
        timers.Advance(300);
        controller.IsHoverVisible.ShouldBeTrue();
    }
}
