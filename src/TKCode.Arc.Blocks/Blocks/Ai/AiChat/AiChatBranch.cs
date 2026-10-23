using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>One turn of the visible branch: the turn, the version shown and the list the turn lives in.</summary>
/// <param name="Turn">The turn.</param>
/// <param name="Version">Its selected version.</param>
/// <param name="Owner">The list holding the turn (<see cref="AiChatConversation.Turns"/> or a <see cref="AiChatTurnVersion.Continuation"/>).</param>
internal readonly record struct AiChatBranchEntry(AiChatTurn Turn, AiChatTurnVersion Version, AvaloniaList<AiChatTurn> Owner);

/// <summary>
/// The version tree of a conversation as the chat walks it. The visible branch starts at
/// <see cref="AiChatConversation.Turns"/>, takes the selected version of each turn and goes on in that version's
/// <see cref="AiChatTurnVersion.Continuation"/> when it has one, else with the next turn of the same list (a plain list of
/// turns without versions is a branch too). Turns without a selected version are skipped. Pure data: no controls.
/// </summary>
internal static class AiChatBranch
{
    /// <summary>The visible branch of <paramref name="conversation"/>, in order (empty for none).</summary>
    public static List<AiChatBranchEntry> Visible(AiChatConversation? conversation)
    {
        var branch = new List<AiChatBranchEntry>();
        if (conversation is null)
        {
            return branch;
        }
        var seen = new HashSet<AiChatTurn>(ReferenceEqualityComparer.Instance);
        var owner = conversation.Turns;
        var index = 0;
        while (index < owner.Count)
        {
            var turn = owner[index];
            // A turn listed twice (or a cycle through continuations) ends the walk instead of looping.
            if (turn is null || !seen.Add(turn))
            {
                break;
            }
            if (turn.SelectedVersion is not { } version)
            {
                index++;
                continue;
            }
            branch.Add(new AiChatBranchEntry(turn, version, owner));
            if (version.Continuation.Count > 0)
            {
                owner = version.Continuation;
                index = 0;
            }
            else
            {
                index++;
            }
        }
        return branch;
    }

    /// <summary>
    /// The messages a responder is asked to answer the prompt of <paramref name="branch"/>[<paramref name="upTo"/>]: the
    /// prompts and replies of the turns before it, user and assistant alternating, then that prompt.
    /// </summary>
    public static List<AiChatRequestMessage> Request(IReadOnlyList<AiChatBranchEntry> branch, int upTo)
    {
        var messages = new List<AiChatRequestMessage>(Math.Max(0, upTo) * 2 + 1);
        for (var i = 0; i <= upTo && i < branch.Count; i++)
        {
            var version = branch[i].Version;
            messages.Add(new AiChatRequestMessage(AiChatRole.User, version.Prompt.Text ?? string.Empty, version.Prompt.Attachments.ToArray()));
            if (i < upTo)
            {
                messages.Add(new AiChatRequestMessage(AiChatRole.Assistant, version.Reply.Text ?? string.Empty, Array.Empty<AiAttachment>()));
            }
        }
        return messages;
    }

    /// <summary>
    /// Appends <paramref name="turn"/> at the end of the visible branch: after the last visible turn in the list that holds
    /// it (a plain conversation stays a plain list), or as the first turn of an empty conversation.
    /// </summary>
    public static void Append(AiChatConversation conversation, AiChatTurn turn)
    {
        var branch = Visible(conversation);
        if (branch.Count == 0)
        {
            conversation.Turns.Add(turn);
            return;
        }
        var last = branch[^1];
        var index = last.Owner.IndexOf(last.Turn);
        last.Owner.Insert(index + 1, turn);
    }

    /// <summary>
    /// Adds a version to <paramref name="turn"/> and selects it. The turns that followed the version shown so far belong to
    /// it: turns after <paramref name="turn"/> in its list move into that version's (empty) continuation, so the new
    /// version starts with nothing after it.
    /// </summary>
    public static AiChatTurnVersion AddVersion(AiChatConversation conversation, AiChatTurn turn, AiChatUserMessage prompt, AiChatReply reply)
    {
        if (turn.SelectedVersion is { } current && current.Continuation.Count == 0 && FindOwner(conversation, turn) is { } owner)
        {
            var index = owner.IndexOf(turn);
            if (index >= 0 && index < owner.Count - 1)
            {
                var later = owner.Skip(index + 1).ToArray();
                owner.RemoveRange(index + 1, later.Length);
                current.Continuation.AddRange(later);
            }
        }
        var version = new AiChatTurnVersion { Prompt = prompt, Reply = reply };
        turn.Versions.Add(version);
        turn.SelectedVersionIndex = turn.Versions.Count - 1;
        return version;
    }

    /// <summary>The list that holds <paramref name="turn"/> on the visible branch, or null when it is not on it.</summary>
    public static AvaloniaList<AiChatTurn>? FindOwner(AiChatConversation conversation, AiChatTurn turn)
    {
        foreach (var entry in Visible(conversation))
        {
            if (ReferenceEquals(entry.Turn, turn))
            {
                return entry.Owner;
            }
        }
        return null;
    }

    /// <summary>A copy of <paramref name="prompt"/> (its text and attachments) for a new version.</summary>
    public static AiChatUserMessage CopyPrompt(AiChatUserMessage prompt, string? text = null)
    {
        var copy = new AiChatUserMessage { Text = text ?? prompt.Text };
        copy.Attachments.AddRange(prompt.Attachments);
        return copy;
    }
}
