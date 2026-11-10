using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AiInlineEdit"/>: selection, the instruction, the per-change review, applying and errors.</summary>
public class AiInlineEditTests
{
    private const string Text = "We will utilize the new tool in order to ship faster.";

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (AiInlineEdit Editor, Window Window) Show(Func<AiInlineEditRequest, CancellationToken, Task<string>>? handler = null)
    {
        var editor = new AiInlineEdit
        {
            Width = 600,
            Text = Text,
            RewriteHandler = handler ?? ((request, _) => Task.FromResult(request.Selection.Replace("utilize", "use").Replace("in order to ", "to ")))
        };
        editor.SelectionStart = 0;
        editor.SelectionLength = Text.Length;
        var window = TestHelpers.Show(editor);
        Settle(window);
        return (editor, window);
    }

    [AvaloniaFact]
    public async Task Rewrite_Shows_Each_Change_With_Its_Own_Buttons()
    {
        var (editor, window) = Show();
        editor.BeginEdit();
        editor.Phase.ShouldBe(AiInlineEditPhase.Instructing);
        await editor.SubmitAsync("Make it shorter");
        Settle(window);
        editor.Phase.ShouldBe(AiInlineEditPhase.Reviewing);
        editor.ChangeCount.ShouldBe(2);
        editor.Hunks[0].Deleted.ShouldBe("utilize");
        editor.Hunks[0].Inserted.ShouldBe("use");
        var actions = editor.Document!.Inlines!.OfType<InlineUIContainer>().Select(c => c.Child).OfType<AiInlineEditChangeActions>().ToList();
        actions.Count.ShouldBe(2);
        AutomationProperties.GetName(actions[0].AcceptButton).ShouldBe("Accept: replace \"utilize\" with \"use\"");
        editor.Announcer!.Announcement.ShouldBe("2 changes suggested");
    }

    [AvaloniaFact]
    public async Task Deciding_Every_Change_Applies_Only_The_Accepted_Ones()
    {
        var (editor, window) = Show();
        AiInlineEditAppliedEventArgs? applied = null;
        editor.EditApplied += (_, e) => applied = e;
        editor.BeginEdit();
        await editor.SubmitAsync("Make it shorter");
        editor.AcceptChange(0);
        editor.Phase.ShouldBe(AiInlineEditPhase.Reviewing);
        editor.RejectChange(1);
        Settle(window);
        editor.Phase.ShouldBe(AiInlineEditPhase.Idle);
        editor.Text.ShouldBe("We will use the new tool in order to ship faster.");
        applied!.Accepted.ShouldBe(1);
        applied.Rejected.ShouldBe(1);
        applied.OldText.ShouldBe(Text);
        applied.Instruction.ShouldBe("Make it shorter");
        editor.SelectionLength.ShouldBe(editor.Text.Length);
    }

    [AvaloniaFact]
    public async Task Accept_All_And_Reject_All_Decide_What_Is_Left()
    {
        var (editor, _) = Show();
        editor.BeginEdit();
        await editor.SubmitAsync("Shorter");
        editor.AcceptAll();
        editor.Text.ShouldBe("We will use the new tool to ship faster.");

        editor.SelectionStart = 0;
        editor.SelectionLength = editor.Text.Length;
        editor.RewriteHandler = (_, _) => Task.FromResult("Nothing like it.");
        editor.BeginEdit();
        await editor.SubmitAsync("Rewrite");
        var before = editor.Text;
        editor.RejectAll();
        editor.Text.ShouldBe(before);
    }

    [AvaloniaFact]
    public async Task Only_The_Selected_Passage_Is_Rewritten()
    {
        var (editor, _) = Show((request, _) => Task.FromResult("adopt"));
        editor.SelectionStart = Text.IndexOf("utilize", StringComparison.Ordinal) - 1; // a leading space is trimmed
        editor.SelectionLength = "utilize".Length + 1;
        AiInlineEditRequest? seen = null;
        editor.RewriteHandler = (request, _) =>
        {
            seen = request;
            return Task.FromResult("adopt");
        };
        editor.BeginEdit();
        await editor.SubmitAsync("Simpler word");
        seen!.Selection.ShouldBe("utilize");
        seen.Text.ShouldBe(Text);
        editor.AcceptAll();
        editor.Text.ShouldBe("We will adopt the new tool in order to ship faster.");
    }

    [AvaloniaFact]
    public async Task A_Failed_Rewrite_Keeps_The_Field_Open_With_The_Message()
    {
        var (editor, window) = Show((_, _) => throw new InvalidOperationException("model offline"));
        editor.BeginEdit();
        await editor.SubmitAsync("Shorter");
        Settle(window);
        editor.Phase.ShouldBe(AiInlineEditPhase.Instructing);
        editor.ErrorMessage.ShouldBe("model offline");
        editor.Classes.ShouldContain(":error");
        editor.Announcer!.Announcement.ShouldBe("Rewrite failed: model offline");
    }

    [AvaloniaFact]
    public void Nothing_Opens_Without_A_Selection_And_Escape_Cancels()
    {
        var (editor, window) = Show();
        editor.SelectionLength = 0;
        editor.BeginEdit();
        editor.Phase.ShouldBe(AiInlineEditPhase.Idle);
        editor.SelectionLength = 5;
        TestHelpers.Descendant<SelectableTextBlock>(editor).Focus();
        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
        Settle(window);
        editor.Phase.ShouldBe(AiInlineEditPhase.Instructing);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);
        editor.Phase.ShouldBe(AiInlineEditPhase.Idle);
    }

    [AvaloniaFact]
    public void Sample_Rewriter_Drops_Filler_Words()
    {
        var request = new AiInlineEditRequest(AiInlineEditSamples.Text, 0, 10, "We are really going to utilize it in order to win.", "Make it shorter");
        AiInlineEditSamples.Rewrite(request).ShouldBe("We will use it to win.");
    }
}
