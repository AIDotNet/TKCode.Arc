// Parameter hints in a real editor (upstream's parameterHints.ts and parameterHintsWidget.ts, commit b57d5c9e); the
// model's own upstream tests are in the engine suite (ParameterHintsModelTests). Written for this port.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.ParameterHints;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using static TKCode.Arc.Tests.Editor.HoverTestSupport;

namespace TKCode.Arc.Tests.Editor;

public class CodeEditorParameterHintsTests
{
    private static readonly SignatureHelp s_overloads = new(
    [
        new SignatureInformation("log(message: string): void", [new ParameterInformation { Label = "message", Documentation = "The text to log." }])
        {
            Documentation = new MarkdownString("Writes **one** line."),
        },
        new SignatureInformation("log(message: string, level: number): void",
        [
            new ParameterInformation { Label = "message" },
            new ParameterInformation { LabelOffsets = (21, 34) },
        ]),
    ], 0, 0);

    private static (CodeEditor Editor, Window Window, ParameterHintsController Controller, ManualTimers Timers, SignatureProvider Provider) Show(string text = "")
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 240 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        var controller = ParameterHintsController.Get(editor).ShouldNotBeNull();
        var timers = new ManualTimers();
        controller.SetScheduler(timers.Scheduler);
        var provider = new SignatureProvider(["("], context =>
        {
            var active = context.TriggerCharacter == "," || (context.ActiveSignatureHelp?.ActiveParameter ?? 0) > 0 ? 1 : 0;
            return new SignatureHelpResult(s_overloads with { ActiveSignature = context.ActiveSignatureHelp?.ActiveSignature ?? 0, ActiveParameter = active });
        });
        editor.LanguageFeatures.SignatureHelpProvider.Register("*", provider);
        return (editor, window, controller, timers, provider);
    }

    [AvaloniaFact]
    public void Typing_a_trigger_character_shows_the_signature_with_the_active_parameter()
    {
        var (editor, window, controller, timers, provider) = Show();

        window.KeyTextInput("log(");
        timers.Advance(ParameterHintsModel.DefaultDelay);
        Render(window);

        var widget = controller.Widget.ShouldNotBeNull();
        widget.IsVisible.ShouldBeTrue();
        provider.Contexts.ShouldHaveSingleItem().TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        var rendered = widget.Rendered;
        rendered.Signature.ShouldBe("log(message: string): void");
        rendered.ActiveParameter.ShouldBe("message");
        rendered.Overloads.ShouldBe("1/2");
        rendered.HasDocs.ShouldBeTrue();
        TextOf(widget.Docs).ShouldContain("The text to log.");
        TextOf(widget.Docs).ShouldContain("Writes one line.");
        editor.ContextKeyService.GetContextKeyValue("parameterHintsVisible").ShouldBe(true);
        editor.ContextKeyService.GetContextKeyValue("parameterHintsMultipleSignatures").ShouldBe(true);

        // on the first line there is no room above the cursor
        var placement = editor.GetContentWidgetPlacement(ParameterHintsWidget.WidgetId).ShouldNotBeNull();
        placement.Preference.ShouldBe(ContentWidgetPositionPreference.Below);
    }

    [AvaloniaFact]
    public void Up_and_down_cycle_the_signatures_and_escape_closes()
    {
        var (editor, window, controller, timers, _) = Show(new string('\n', 9));
        editor.SetPosition(new TextPosition(10, 1));
        window.KeyTextInput("log(");
        timers.Advance(ParameterHintsModel.DefaultDelay);
        Render(window);
        var widget = controller.Widget!;
        editor.GetContentWidgetPlacement(ParameterHintsWidget.WidgetId)!.Preference.ShouldBe(ContentWidgetPositionPreference.Above);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Render(window);
        widget.Rendered.Signature.ShouldBe("log(message: string, level: number): void");
        widget.Rendered.Overloads.ShouldBe("2/2");
        editor.GetPosition().ShouldBe(new TextPosition(10, 5));

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Render(window);
        widget.Rendered.Overloads.ShouldBe("1/2");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Render(window);
        widget.IsVisible.ShouldBeFalse();
        editor.ContextKeyService.GetContextKeyValue("parameterHintsVisible").ShouldBe(false);
    }

    [AvaloniaFact]
    public void Label_offsets_mark_the_active_parameter_and_a_retrigger_character_moves_it()
    {
        var (editor, window, controller, timers, provider) = Show();
        window.KeyTextInput("log(");
        timers.Advance(ParameterHintsModel.DefaultDelay);
        controller.Next();
        Render(window);
        window.KeyTextInput("x,");
        timers.Advance(ParameterHintsModel.DefaultDelay);
        Render(window);

        provider.Contexts[^1].IsRetrigger.ShouldBeTrue();
        provider.Contexts[^1].TriggerCharacter.ShouldBe(",");
        var widget = controller.Widget!;
        widget.Rendered.Signature.ShouldBe("log(message: string, level: number): void");
        widget.Rendered.ActiveParameter.ShouldBe("level: number");
        widget.Rendered.HasDocs.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Ctrl_shift_space_triggers_and_a_click_elsewhere_cancels()
    {
        var (editor, window, controller, timers, provider) = Show("log()");
        editor.SetPosition(new TextPosition(1, 5));

        window.KeyPressQwerty(PhysicalKey.Space, HoverTestSupport.CtrlCmd | RawInputModifiers.Shift);
        timers.Advance(0);
        Render(window);
        provider.Contexts.ShouldHaveSingleItem().TriggerKind.ShouldBe(SignatureHelpTriggerKind.Invoke);
        controller.Widget!.IsVisible.ShouldBeTrue();

        editor.SetPosition(new TextPosition(1, 1), "mouse");
        Render(window);
        controller.Widget.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_active_parameter_uses_the_highlight_colour()
    {
        var (_, window, controller, timers, _) = Show();
        window.KeyTextInput("log(");
        timers.Advance(ParameterHintsModel.DefaultDelay);
        Render(window);

        var widget = controller.Widget!;
        var signature = TestHelpers.Descendant<TextBlock>(widget.Surface, "PART_Signature");
        var active = signature.Inlines!.OfType<Run>().Single(r => r.Classes.Contains("active"));
        active.FontWeight.ShouldBe(FontWeight.Bold);
        active.Foreground.ShouldBe(widget.Surface.HighlightForeground);
        widget.Surface.HighlightForeground.ShouldNotBeNull();
    }
}
