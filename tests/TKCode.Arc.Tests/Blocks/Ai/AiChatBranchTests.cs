using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The version tree (<see cref="AiChatBranch"/>): the visible branch through selected versions and their continuations,
/// what a responder is asked, where a new turn goes and how a new version takes the later turns with the old one.
/// </summary>
public class AiChatBranchTests
{
    private static AiChatTurn Turn(string prompt, string reply = "") =>
        new(new AiChatUserMessage { Text = prompt }, new AiChatReply { Text = reply, Status = AiChatReplyStatus.Done });

    private static string[] Prompts(AiChatConversation conversation) =>
        AiChatBranch.Visible(conversation).Select(e => e.Version.Prompt.Text).ToArray();

    [AvaloniaFact]
    public void A_Plain_List_Of_Turns_Is_The_Branch()
    {
        var conversation = new AiChatConversation();
        conversation.Turns.Add(Turn("a"));
        conversation.Turns.Add(Turn("b"));
        conversation.Turns.Add(Turn("c"));
        Prompts(conversation).ShouldBe(["a", "b", "c"]);
        AiChatBranch.Visible(null).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_Branch_Follows_The_Selected_Version_Into_Its_Continuation()
    {
        var conversation = new AiChatConversation();
        var first = Turn("a");
        conversation.Turns.Add(first);
        first.Versions[0].Continuation.Add(Turn("b1"));
        first.Versions[0].Continuation[0].Versions[0].Continuation.Add(Turn("c1"));
        var second = new AiChatTurnVersion { Prompt = new AiChatUserMessage { Text = "a2" }, Reply = new AiChatReply() };
        second.Continuation.Add(Turn("b2"));
        first.Versions.Add(second);

        Prompts(conversation).ShouldBe(["a", "b1", "c1"]);
        first.SelectedVersionIndex = 1;
        Prompts(conversation).ShouldBe(["a2", "b2"]);
    }

    [AvaloniaFact]
    public void A_Turn_Without_A_Selected_Version_Is_Skipped_And_A_Cycle_Ends_The_Walk()
    {
        var conversation = new AiChatConversation();
        var empty = new AiChatTurn();
        var a = Turn("a");
        conversation.Turns.Add(empty);
        conversation.Turns.Add(a);
        a.Versions[0].Continuation.Add(a);
        Prompts(conversation).ShouldBe(["a"]);
    }

    [AvaloniaFact]
    public void The_Request_Holds_Only_The_Visible_Branch_Up_To_The_Prompt()
    {
        var conversation = new AiChatConversation();
        var first = Turn("a", "A");
        conversation.Turns.Add(first);
        first.Versions[0].Continuation.Add(Turn("hidden", "H"));
        var second = new AiChatTurnVersion { Prompt = new AiChatUserMessage { Text = "a2" }, Reply = new AiChatReply { Text = "A2" } };
        second.Continuation.Add(Turn("b", "B"));
        second.Continuation.Add(Turn("c", "C"));
        first.Versions.Add(second);
        first.SelectedVersionIndex = 1;

        var branch = AiChatBranch.Visible(conversation);
        var messages = AiChatBranch.Request(branch, 2);
        messages.Select(m => (m.Role, m.Content)).ShouldBe(
        [
            (AiChatRole.User, "a2"), (AiChatRole.Assistant, "A2"),
            (AiChatRole.User, "b"), (AiChatRole.Assistant, "B"),
            (AiChatRole.User, "c")
        ]);
        AiChatBranch.Request(branch, 0).Select(m => m.Content).ShouldBe(["a2"]);
    }

    [AvaloniaFact]
    public void Append_Keeps_A_Plain_Conversation_Plain_And_Follows_A_Continuation()
    {
        var conversation = new AiChatConversation();
        AiChatBranch.Append(conversation, Turn("a"));
        AiChatBranch.Append(conversation, Turn("b"));
        conversation.Turns.Count.ShouldBe(2);

        var nested = new AiChatConversation();
        var first = Turn("x");
        nested.Turns.Add(first);
        first.Versions[0].Continuation.Add(Turn("y"));
        AiChatBranch.Append(nested, Turn("z"));
        first.Versions[0].Continuation.Select(t => t.Versions[0].Prompt.Text).ShouldBe(["y", "z"]);
        Prompts(nested).ShouldBe(["x", "y", "z"]);
    }

    [AvaloniaFact]
    public void A_New_Version_Takes_No_Later_Turns_And_The_Old_Version_Keeps_Them()
    {
        var conversation = new AiChatConversation();
        var a = Turn("a");
        conversation.Turns.Add(a);
        conversation.Turns.Add(Turn("b"));
        conversation.Turns.Add(Turn("c"));

        var added = AiChatBranch.AddVersion(conversation, a, AiChatBranch.CopyPrompt(a.Versions[0].Prompt), new AiChatReply());
        a.Versions.Count.ShouldBe(2);
        a.SelectedVersionIndex.ShouldBe(1);
        a.SelectedVersion.ShouldBeSameAs(added);
        added.Prompt.Text.ShouldBe("a");
        added.Prompt.ShouldNotBeSameAs(a.Versions[0].Prompt);
        conversation.Turns.Count.ShouldBe(1);
        Prompts(conversation).ShouldBe(["a"]);

        a.SelectedVersionIndex = 0;
        Prompts(conversation).ShouldBe(["a", "b", "c"]);
    }

    [AvaloniaFact]
    public void A_Version_In_The_Middle_Of_A_Branch_Moves_Only_The_Turns_After_It()
    {
        var conversation = new AiChatConversation();
        conversation.Turns.Add(Turn("a"));
        var b = Turn("b");
        conversation.Turns.Add(b);
        conversation.Turns.Add(Turn("c"));
        AiChatBranch.AddVersion(conversation, b, AiChatBranch.CopyPrompt(b.Versions[0].Prompt, "b edited"), new AiChatReply());
        Prompts(conversation).ShouldBe(["a", "b edited"]);
        b.SelectedVersionIndex = 0;
        Prompts(conversation).ShouldBe(["a", "b", "c"]);
    }

    [AvaloniaFact]
    public void A_Copied_Prompt_Keeps_The_Attachments()
    {
        var prompt = new AiChatUserMessage { Text = "look" };
        prompt.Attachments.Add(new AiAttachment { Name = "stage-timings.csv", Size = 1229 });
        var copy = AiChatBranch.CopyPrompt(prompt, "look again");
        copy.Text.ShouldBe("look again");
        copy.Attachments.Single().Name.ShouldBe("stage-timings.csv");
    }
}
