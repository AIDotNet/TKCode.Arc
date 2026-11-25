using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using static TKCode.Arc.Tests.Editor.CodeEditorCodeActionTests;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Rename (upstream's rename.ts and renameWidget.ts): F2 opens the box over the word, Enter applies the provider's
/// edits as one undo step, Escape cancels, rejections show as a message.
/// </summary>
public class CodeEditorRenameTests
{
    private sealed class Provider : IRenameProvider
    {
        public string? Reject { get; init; }

        public string? LocationReject { get; init; }

        public List<string> Names { get; } = new();

        public bool SupportsResolveRenameLocation => LocationReject is not null;

        public Task<RenameLocationResult?> ResolveRenameLocation(TextModel model, TextPosition position, CancellationToken token) =>
            Task.FromResult<RenameLocationResult?>(new RenameLocationResult(default, string.Empty) { RejectReason = LocationReject });

        public Task<RenameEdits?> ProvideRenameEdits(TextModel model, TextPosition position, string newName, CancellationToken token)
        {
            Names.Add(newName);
            if (Reject is not null)
            {
                return Task.FromResult<RenameEdits?>(new RenameEdits(new WorkspaceEdit([])) { RejectReason = Reject });
            }

            // every occurrence of "foo"
            var edits = new List<WorkspaceEditEntry>();
            for (var line = 1; line <= model.GetLineCount(); line++)
            {
                var text = model.GetLineContent(line);
                for (var i = text.IndexOf("foo", StringComparison.Ordinal); i >= 0; i = text.IndexOf("foo", i + 3, StringComparison.Ordinal))
                {
                    edits.Add(new WorkspaceTextEdit(model.Uri, new TextEdit(new TextRange(line, i + 1, line, i + 4), newName)));
                }
            }

            return Task.FromResult<RenameEdits?>(new RenameEdits(new WorkspaceEdit(edits)));
        }
    }

    [AvaloniaFact]
    public void F2_opens_the_box_and_Enter_renames_every_occurrence()
    {
        var provider = new Provider();
        var (editor, window) = Show("let foo = 1;\nfoo + foo", e => e.LanguageFeatures.RenameProvider.Register("plaintext", provider));
        editor.SetPosition(new TextPosition(1, 6));

        window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Render(window);

        var controller = RenameController.Get(editor).ShouldNotBeNull();
        var widget = controller.Widget;
        widget.IsVisible.ShouldBeTrue();
        widget.Input.Text.ShouldBe("foo");
        widget.Input.SelectionStart.ShouldBe(0);
        widget.Input.SelectionEnd.ShouldBe(3);
        editor.ContextKeyService.ContextMatchesRules(CodeEditorContextKeys.RenameInputVisible).ShouldBeTrue();
        var placement = editor.GetContentWidgetPlacement(widget.Id).ShouldNotBeNull();
        placement.Preference.ShouldBe(ContentWidgetPositionPreference.Below);

        widget.Input.Text = "bar";
        var kb = editor.View!.Resolver.LookupKeybindings("acceptRenameInput");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);

        provider.Names.ShouldBe(["bar"]);
        editor.Text.ShouldBe("let bar = 1;\nbar + bar");
        widget.IsVisible.ShouldBeFalse();
        editor.IsTextFocused.ShouldBeTrue();

        editor.Trigger("keyboard", "undo", null);
        editor.Text.ShouldBe("let foo = 1;\nfoo + foo");
    }

    [AvaloniaFact]
    public void Escape_cancels_and_an_unchanged_name_renames_nothing()
    {
        var provider = new Provider();
        var (editor, window) = Show("foo", e => e.LanguageFeatures.RenameProvider.Register("plaintext", provider));

        window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Render(window);
        var widget = RenameController.Get(editor)!.Widget;
        widget.Input.Text = "baz";
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Render(window);
        widget.IsVisible.ShouldBeFalse();
        editor.Text.ShouldBe("foo");

        window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Render(window);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);
        widget.IsVisible.ShouldBeFalse();
        provider.Names.ShouldBeEmpty();
        editor.ContextKeyService.ContextMatchesRules(CodeEditorContextKeys.RenameInputVisible).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Rejections_show_as_a_message()
    {
        var (editor, window) = Show("foo", e => e.LanguageFeatures.RenameProvider.Register("plaintext", new Provider { LocationReject = "You cannot rename this element." }));
        window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Render(window);
        RenameController.Get(editor)!.Widget.IsVisible.ShouldBeFalse();
        MessageController.Get(editor)!.Text.ShouldBe("You cannot rename this element.");

        var (editor2, window2) = Show("foo", e => e.LanguageFeatures.RenameProvider.Register("plaintext", new Provider { Reject = "Conflicts with 'bar'." }));
        window2.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Render(window2);
        RenameController.Get(editor2)!.Widget.Input.Text = "bar";
        window2.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window2);
        editor2.Text.ShouldBe("foo");
        MessageController.Get(editor2)!.Text.ShouldBe("Conflicts with 'bar'.");
    }

    [AvaloniaFact]
    public void F2_does_nothing_without_a_provider_or_in_a_read_only_editor()
    {
        var (editor, window) = Show("foo", e => e.LanguageFeatures.RenameProvider.Register("plaintext", new Provider()));
        editor.IsReadOnly = true;
        window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Render(window);
        editor.GetContributionIds().ShouldNotContain(RenameController.Id);

        var (editor2, window2) = Show("foo");
        window2.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Render(window2);
        editor2.GetContributionIds().ShouldNotContain(RenameController.Id);
        Dispatcher.UIThread.RunJobs();
    }
}
