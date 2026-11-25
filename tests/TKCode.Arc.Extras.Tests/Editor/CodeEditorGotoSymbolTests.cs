using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using static TKCode.Arc.Tests.Editor.CodeEditorCodeActionTests;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Go to definition / references and peek (upstream's goToCommands.ts, referencesController.ts, referencesWidget.ts,
/// peekView.ts, zoneWidget.ts and goToDefinitionAtPosition.ts): jumps in the model, the host for other documents, the
/// peek view in a view zone with its list and preview, F4, Escape and the Ctrl+click link.
/// </summary>
public class CodeEditorGotoSymbolTests
{
    private const string Code = "function foo() {}\nfoo();\nlet x = foo;\nbar();";
    private static readonly Uri s_other = new("file:///lib/other.ts");

    private sealed class Definitions(Func<TextModel, TextPosition, IReadOnlyList<LocationLink>> provide) : IDefinitionProvider
    {
        public Task<IReadOnlyList<LocationLink>?> ProvideDefinition(TextModel model, TextPosition position, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<LocationLink>?>(provide(model, position));
    }

    private sealed class References(Func<TextModel, IReadOnlyList<Location>> provide) : IReferenceProvider
    {
        public Task<IReadOnlyList<Location>?> ProvideReferences(TextModel model, TextPosition position, ReferenceContext context, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<Location>?>(provide(model));
    }

    // 'foo' is defined on line 1; 'bar' has no definition
    private static IReadOnlyList<LocationLink> FooDefinition(TextModel model, TextPosition position) =>
        model.GetLineContent(position.LineNumber).Contains("foo", StringComparison.Ordinal)
            ? [new LocationLink(model.Uri, new TextRange(1, 1, 1, 18)) { TargetSelectionRange = new TextRange(1, 10, 1, 13) }]
            : [];

    private static IReadOnlyList<Location> FooReferences(TextModel model) =>
    [
        new(model.Uri, new TextRange(1, 10, 1, 13)),
        new(model.Uri, new TextRange(2, 1, 2, 4)),
        new(model.Uri, new TextRange(3, 9, 3, 12)),
        new(s_other, new TextRange(2, 5, 2, 8)),
    ];

    [AvaloniaFact]
    public void F12_goes_to_the_definition_and_highlights_it()
    {
        var (editor, window) = Show(Code, e => e.LanguageFeatures.DefinitionProvider.Register("plaintext", new Definitions(FooDefinition)));
        editor.SetPosition(new TextPosition(2, 2));

        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None);
        Render(window);

        editor.GetPosition().ShouldBe(new TextPosition(1, 10));
        editor.Model.GetLineDecorations(1).ShouldContain(d => d.Options.ClassName == SymbolNavigationAction.SymbolHighlightClassName);
    }

    [AvaloniaFact]
    public void F12_without_a_result_says_so_and_another_document_goes_to_the_host()
    {
        CodeEditorOpenRequest? opened = null;
        var (editor, window) = Show(Code, e =>
        {
            e.LanguageFeatures.DefinitionProvider.Register("plaintext", new Definitions((model, position) =>
                position.LineNumber == 3 ? [new LocationLink(s_other, new TextRange(7, 1, 7, 5))] : FooDefinition(model, position)));
            e.ActionHost.OpenCodeEditor = request =>
            {
                opened = request;
                return Task.FromResult(true);
            };
        });

        editor.SetPosition(new TextPosition(4, 2));
        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None);
        Render(window);
        MessageController.Get(editor)!.Text.ShouldBe("No definition found for 'bar'");
        editor.GetPosition().ShouldBe(new TextPosition(4, 2));

        editor.SetPosition(new TextPosition(3, 10));
        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None);
        Render(window);
        opened.ShouldNotBeNull();
        opened.Resource.ShouldBe(s_other);
        opened.Selection.ShouldBe(new TextRange(7, 1, 7, 1));
        opened.SideBySide.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Shift_F12_peeks_several_references_in_a_view_zone_and_Escape_closes_it()
    {
        var (editor, window) = Show(Code, e =>
        {
            e.LanguageFeatures.ReferenceProvider.Register("plaintext", new References(FooReferences));
            e.ActionHost.ResolveText = (uri, _) => Task.FromResult<CodeEditorResolvedText?>(
                uri == s_other ? new CodeEditorResolvedText("import x;\nlet foo = 1;\n") : null);
        });
        editor.SetPosition(new TextPosition(2, 2));

        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.Shift);
        Render(window);

        var controller = ReferencesController.Get(editor).ShouldNotBeNull();
        var widget = controller.Widget.ShouldNotBeNull();
        editor.ContextKeyService.ContextMatchesRules(CodeEditorContextKeys.ReferenceSearchVisible).ShouldBeTrue();
        editor.GetViewZoneBounds(widget.ViewZoneId!).ShouldNotBeNull().Height.ShouldBeGreaterThan(editor.View!.LineHeight * 5);
        editor.GetViewZone(widget.ViewZoneId!)!.AfterLineNumber.ShouldBe(2);

        // two files: file rows and their references; the nearest one is selected and shown in the preview
        widget.Rows.Count(r => r.File is not null).ShouldBe(2);
        widget.Rows.Count(r => r.Reference is not null).ShouldBe(4);
        widget.Titles.Meta.ShouldBe("References (4)");
        widget.Selected.ShouldNotBeNull().Range.ShouldBe(new TextRange(2, 1, 2, 4));
        widget.Preview.Text.ShouldBe(Code);
        widget.Preview.Model.GetLineDecorations(3).ShouldContain(d => d.Options.ClassName == "reference-decoration");

        // Up in the list shows the previous reference (files sort by URI: the other file comes first, file rows are
        // skipped); one in another file shows its text
        widget.List.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Render(window);
        widget.Selected!.Uri.ShouldBe(s_other);
        widget.Preview.Text.ShouldBe("import x;\nlet foo = 1;\n");
        widget.Titles.Title.ShouldBe("other.ts");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Render(window);
        controller.Widget.ShouldBeNull();
        editor.ContextKeyService.ContextMatchesRules(CodeEditorContextKeys.ReferenceSearchVisible).ShouldBeFalse();
        editor.GetViewZoneBounds(widget.ViewZoneId ?? "none").ShouldBeNull();
    }

    [AvaloniaFact]
    public void F12_at_the_definition_goes_to_references_and_F4_moves_through_them()
    {
        var (editor, window) = Show(Code, e =>
        {
            e.LanguageFeatures.DefinitionProvider.Register("plaintext", new Definitions(FooDefinition));
            e.LanguageFeatures.ReferenceProvider.Register("plaintext", new References(model => FooReferences(model).Take(3).ToList()));
        });

        // at the definition: the alternative, go to references (several: peek)
        editor.SetPosition(new TextPosition(1, 11));
        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None);
        Render(window);
        var controller = ReferencesController.Get(editor).ShouldNotBeNull();
        var widget = controller.Widget.ShouldNotBeNull();
        widget.Selected!.Range.ShouldBe(new TextRange(1, 10, 1, 13));

        // F4: the next reference, shown in the editor with the peek view below it
        window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.None);
        Render(window);
        editor.GetPosition().ShouldBe(new TextPosition(2, 1));
        controller.Widget.ShouldNotBeNull().Selected!.Range.ShouldBe(new TextRange(2, 1, 2, 4));
        editor.GetViewZone(controller.Widget!.ViewZoneId!)!.AfterLineNumber.ShouldBe(2);

        // Enter in the list: peek mode off (go to references) opens it and closes the view
        controller.Widget!.List.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);
        controller.Widget.ShouldBeNull();
        editor.GetPosition().ShouldBe(new TextPosition(3, 9));
    }

    [AvaloniaFact]
    public void Alt_F12_peeks_a_single_definition_and_the_close_button_closes_it()
    {
        var (editor, window) = Show(Code, e => e.LanguageFeatures.DefinitionProvider.Register("plaintext", new Definitions(FooDefinition)));
        editor.SetPosition(new TextPosition(3, 10));
        // Peek Definition: Alt+F12, or Ctrl+Shift+F10 on Linux (Monaco's keybinding)
        if (OperatingSystem.IsLinux())
        {
            window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Control | RawInputModifiers.Shift);
        }
        else
        {
            window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.Alt);
        }

        Render(window);

        var controller = ReferencesController.Get(editor).ShouldNotBeNull();
        var widget = controller.Widget.ShouldNotBeNull();
        widget.Titles.Meta.ShouldBe("Definitions (1)");
        widget.Rows.Count.ShouldBe(1);
        editor.GetPosition().ShouldBe(new TextPosition(3, 10));

        var button = widget.CloseButton;
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Render(window);
        controller.Widget.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Ctrl_hover_shows_a_link_and_Ctrl_click_goes_to_the_definition()
    {
        var (editor, window) = Show(Code, e => e.LanguageFeatures.DefinitionProvider.Register("plaintext", new Definitions(FooDefinition)));
        var view = editor.View!;
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        var point = new Point(origin.X + view.GetX(3, 10) + 1, origin.Y + view.GetY(3) + (view.LineHeight / 2));

        window.MouseMove(point, CtrlCmd);
        Render(window);
        var contribution = GotoDefinitionAtPosition.Get(editor).ShouldNotBeNull();
        if (contribution.PendingRequest is { } pending)
        {
            await pending;
        }

        contribution.LinkRange.ShouldBe(new TextRange(3, 9, 3, 12));
        editor.Model.GetLineDecorations(3).ShouldContain(d => d.Options.InlineClassName == GotoDefinitionAtPosition.LinkClassName);

        // the key goes up: the link goes
        window.KeyReleaseQwerty(OperatingSystem.IsMacOS() ? PhysicalKey.MetaLeft : PhysicalKey.ControlLeft, RawInputModifiers.None);
        contribution.LinkRange.ShouldBeNull();

        window.MouseMove(point, CtrlCmd);
        Render(window);
        window.MouseDown(point, MouseButton.Left, CtrlCmd);
        window.MouseUp(point, MouseButton.Left, CtrlCmd);
        Render(window);
        editor.GetPosition().ShouldBe(new TextPosition(1, 10));
        contribution.LinkRange.ShouldBeNull();
    }
}
