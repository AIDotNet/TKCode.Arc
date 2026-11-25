using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CodeLens;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using static TKCode.Arc.Tests.Editor.CodeEditorCodeActionTests;
using CodeLens = TKCode.Arc.Editor.Languages.CodeLens;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Code lenses (upstream's codelensController.ts and codelensWidget.ts): rows above the lines in view zones, resolving
/// the visible ones, a click runs the command, rows follow edits, the cache shows rows at once.
/// </summary>
public class CodeEditorCodeLensTests
{
    private const string Code = "class A\n{\n    void Run() {}\n}";

    private sealed class Provider : ICodeLensProvider
    {
        public int ProvideCalls { get; private set; }

        public int ResolveCalls { get; private set; }

        public bool SupportsResolveCodeLens => true;

        public Task<CodeLensList?> ProvideCodeLenses(TextModel model, CancellationToken token)
        {
            ProvideCalls++;
            var lenses = new List<CodeLens>();
            for (var line = 1; line <= model.GetLineCount(); line++)
            {
                var text = model.GetLineContent(line);
                if (text.StartsWith("class", StringComparison.Ordinal))
                {
                    lenses.Add(new CodeLens(new TextRange(line, 1, line, 6)) { Command = new LanguageCommand("lens.refs", "2 references") { Arguments = [line] } });
                    lenses.Add(new CodeLens(new TextRange(line, 7, line, 8)) { Command = new LanguageCommand("lens.tests", "$(beaker) Run tests") });
                }
                else if (text.Contains("Run()", StringComparison.Ordinal))
                {
                    // resolved later
                    lenses.Add(new CodeLens(new TextRange(line, 10, line, 13)));
                }
            }

            return Task.FromResult<CodeLensList?>(new CodeLensList(lenses));
        }

        public Task<CodeLens?> ResolveCodeLens(TextModel model, CodeLens codeLens, CancellationToken token)
        {
            ResolveCalls++;
            return Task.FromResult<CodeLens?>(codeLens with { Command = new LanguageCommand("lens.run", "Run | Debug") });
        }
    }

    [AvaloniaFact]
    public void Lenses_show_in_rows_above_their_lines_and_unresolved_ones_are_resolved()
    {
        var provider = new Provider();
        var (editor, window) = Show(Code, e => e.LanguageFeatures.CodeLensProvider.Register("plaintext", provider));

        var controller = CodeLensController.Get(editor).ShouldNotBeNull();
        TestHelpers.PumpUntil(() => controller.Lenses.Count == 2 && controller.Lenses[1].Text.Length > 0).ShouldBeTrue();
        Render(window);

        var first = controller.Lenses[0];
        editor.GetViewZone(first.ViewZoneId)!.AfterLineNumber.ShouldBe(0);
        editor.GetViewZone(controller.Lenses[1].ViewZoneId)!.AfterLineNumber.ShouldBe(2);
        first.Text.ShouldBe("2 references | Run tests");
        editor.GetViewZoneBounds(first.ViewZoneId)!.Value.Height.ShouldBe(controller.CodeLensHeight, 0.5);
        provider.ResolveCalls.ShouldBe(1);
        controller.Lenses[1].Text.ShouldBe("Run | Debug");

        // the text of line 1 moves down by the row
        var view = editor.View!;
        view.GetY(1).ShouldBe(controller.CodeLensHeight, 0.5);
    }

    [AvaloniaFact]
    public void A_click_on_a_lens_runs_its_command_and_rows_follow_edits()
    {
        var ran = new List<string>();
        var provider = new Provider();
        var (editor, window) = Show(Code, e =>
        {
            e.LanguageFeatures.CodeLensProvider.Register("plaintext", provider);
            e.ActionHost.ExecuteCommand = command =>
            {
                ran.Add($"{command.Id}:{string.Join(',', command.Arguments ?? [])}");
                return Task.CompletedTask;
            };
        });
        var controller = CodeLensController.Get(editor).ShouldNotBeNull();
        TestHelpers.PumpUntil(() => controller.Lenses.Count == 2).ShouldBeTrue();
        Render(window);

        var link = controller.Lenses[0].Links.First();
        var center = link.TranslatePoint(new Point(link.Bounds.Width / 2, link.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        ran.ShouldBe(["lens.refs:1"]);

        // a line inserted above Run(): its row moves with it
        editor.ExecuteEdits("test", [EditOperation.Insert(new TextPosition(2, 2), "\n// a")]);
        Render(window);
        editor.Text.ShouldBe("class A\n{\n// a\n    void Run() {}\n}");
        editor.GetViewZone(controller.Lenses[0].ViewZoneId)!.AfterLineNumber.ShouldBe(0);
        editor.GetViewZone(controller.Lenses[1].ViewZoneId)!.AfterLineNumber.ShouldBe(3);
        TestHelpers.PumpUntil(() => provider.ProvideCalls >= 2).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Cached_lenses_show_at_once_when_the_contribution_comes_back()
    {
        var provider = new Provider();
        var (editor, window) = Show(Code, e => e.LanguageFeatures.CodeLensProvider.Register("plaintext", provider));
        var controller = CodeLensController.Get(editor).ShouldNotBeNull();
        TestHelpers.PumpUntil(() => controller.Lenses.Count == 2).ShouldBeTrue();
        editor.ActionHost.CodeLensCache.Get(editor.Model).ShouldNotBeNull().Lenses.Count.ShouldBe(3);

        // out of the visual tree and back: a new contribution shows the cached rows before asking again
        window.Content = null;
        window.Content = editor;
        Render(window);
        var again = CodeLensController.Get(editor).ShouldNotBeNull();
        again.ShouldNotBeSameAs(controller);
        again.Lenses.Count.ShouldBe(2);
        again.Lenses[0].Text.ShouldBe("2 references | Run tests");
    }
}
