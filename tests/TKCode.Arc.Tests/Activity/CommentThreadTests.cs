using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Activity;

public class CommentThreadTests
{
    private static readonly CommentAuthor s_mina = new("mina", "Mina Park");
    private static readonly CommentAuthor s_theo = new("theo", "Theo Walsh");
    private static readonly CommentAuthor s_ana = new("ana", "Ana Ruiz");
    private static readonly CommentAuthor s_kai = new("kai", "Kai Moreno");

    private static IReadOnlyList<ThreadComment> Review() =>
    [
        new ThreadComment("c1", s_mina, "Looks good. @Theo Walsh can we tighten the subhead?", "2h")
        {
            Reactions = [new CommentReaction("👍", ["theo", "ana"]), new CommentReaction("👀", ["kai"])],
            Replies =
            [
                new ThreadComment("c2", s_theo, "Yes, trimming it now.", "1h") { Reactions = [new CommentReaction("🎉", ["mina"])] },
                new ThreadComment("c3", s_ana, "Fits on one line now.", "45m") { IsEdited = true }
            ]
        },
        new ThreadComment("c4", s_kai, "Start free or Get started? @Mina Park has the doc.", "20m")
    ];

    private static (CommentThread Thread, Window Window) Show(IReadOnlyList<ThreadComment>? comments = null, ArcMotionLevel level = ArcMotionLevel.Off,
                                                              double width = 520)
    {
        var thread = new CommentThread { CurrentUser = s_ana, Title = "Hero headline", Comments = comments ?? Review(), Width = width };
        var window = TestHelpers.Show(thread, level);
        window.Width = 700;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (thread, window);
    }

    private static void Click(Avalonia.Controls.Button button)
    {
        button.Focus();
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, TextBox field, string text)
    {
        field.Focus();
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Follow_Arc()
    {
        var thread = new CommentThread();
        thread.Placeholder.ShouldBe("Reply, or @mention someone");
        thread.Reactions.ShouldBe(["👍", "❤️", "🎉", "👀", "🚀", "✅"]);
        thread.MaxDepth.ShouldBe(2);
        thread.NowLabel.ShouldBe("Just now");
        thread.IsResolved.ShouldBeFalse();
        thread.Comments.ShouldBeEmpty();
        thread.People.ShouldBeNull();
        thread.Labels.ShouldBeSameAs(CommentThreadLabels.Default);
        var labels = CommentThreadLabels.Default;
        labels.CommentCount(1).ShouldBe("1 comment");
        labels.CommentCount(3).ShouldBe("3 comments");
        labels.PeopleCount(1).ShouldBe("1 person");
        labels.ShowReplies(2).ShouldBe("Show 2 replies");
        labels.ReplyingTo("Mina").ShouldBe("Replying to Mina");
        labels.ReactionCount("👍", 3, true).ShouldBe("👍 3, including you");
        labels.Empty.ShouldBe("No comments yet. Start the conversation below.");
        CommentThread.CommentsProperty.GetMetadata(typeof(CommentThread)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
        CommentThread.IsResolvedProperty.GetMetadata(typeof(CommentThread)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);

        var window = TestHelpers.Show(thread);
        try
        {
            thread.CornerRadius.ShouldBe(new CornerRadius(26));
            thread.EmptyPart.IsVisible.ShouldBeTrue();
            thread.EmptyText.Text.ShouldBe(labels.Empty);
            thread.Subtitle.Text.ShouldBe("0 comments");
            thread.ResolveButton.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(thread).ShouldBe("Comment thread");
            AutomationProperties.GetLandmarkType(thread).ShouldBe(AutomationLandmarkType.Region);
            thread.Composer.Field.MinHeight.ShouldBe(38);
            thread.Composer.Field.MaxHeight.ShouldBe(160);
            thread.Composer.Field.CornerRadius.ShouldBe(new CornerRadius(14));
            thread.Composer.SendButton.IsEnabled.ShouldBeFalse();
            thread.Composer.CancelButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Renders_The_Tree_With_Mentions_And_Ownership()
    {
        var (thread, window) = Show();
        try
        {
            thread.Items.Count.ShouldBe(4);
            thread.Subtitle.Text.ShouldBe("4 comments");
            thread.ResolveButton.IsEnabled.ShouldBeTrue();
            var c1 = thread.ItemOf("c1")!;
            c1.NameText.Text.ShouldBe("Mina Park");
            c1.Body.Mentions.ShouldBe(["@Theo Walsh"]);
            c1.Body.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Single(r => r.Text == "@Theo Walsh").FontWeight.ShouldBe(FontWeight.Medium);
            AutomationProperties.GetName(c1.Article).ShouldBe("Mina Park, 2h");
            ControlAutomationPeer.CreatePeerForElement(c1.Article).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            c1.EditButton.IsVisible.ShouldBeFalse();
            c1.DeleteButton.IsVisible.ShouldBeFalse();
            c1.ReactionBar.ChipOf("👍")!.IsChecked.ShouldBe(true);
            c1.ReactionBar.ChipOf("👀")!.IsChecked.ShouldBe(false);
            AutomationProperties.GetName(c1.ReactionBar.ChipOf("👍")!).ShouldBe("👍 2, including you");
            c1.Toggle.IsVisible.ShouldBeTrue();
            c1.Toggle.Label.ShouldBe("Hide replies");

            var c3 = thread.ItemOf("c3")!;
            c3.TimeText.Text.ShouldBe("45m · edited");
            c3.EditButton.IsVisible.ShouldBeTrue();
            c3.DeleteButton.IsVisible.ShouldBeTrue();
            c3.Depth.ShouldBe(1);
            c3.ParentItem.ShouldBeSameAs(c1);
            c1.RepliesPanel.Children.ShouldContain(c3);

            // Mentions are offered for everyone in the thread, the current user first.
            thread.Mentionable.Select(p => p.Id).ShouldBe(["ana", "mina", "theo", "kai"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Composer_Sends_With_Ctrl_Enter_And_Appends_A_Comment()
    {
        var (thread, window) = Show();
        try
        {
            var changes = new List<CommentsChangedEventArgs>();
            thread.CommentsChanged += (_, e) => changes.Add(e);
            var field = thread.Composer.Field;
            Type(window, field, "Ship it");
            thread.Composer.SendButton.IsEnabled.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            changes.ShouldBeEmpty(); // Enter alone adds a line
            field.Text = "Ship it";
            field.CaretIndex = field.Text.Length;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            var change = changes.ShouldHaveSingleItem();
            change.Change.Kind.ShouldBe(CommentThreadChangeKind.Reply);
            change.Change.ParentId.ShouldBeNull();
            var added = change.Change.Comment.ShouldNotBeNull();
            added.Body.ShouldBe("Ship it");
            added.Author.ShouldBe(s_ana);
            added.CreatedAt.ShouldBe("Just now");
            added.Id.ShouldStartWith("c-");
            thread.Comments.Count.ShouldBe(3);
            thread.Comments[^1].ShouldBeSameAs(added);
            change.Comments.ShouldBeSameAs(thread.Comments);
            thread.ItemOf(added.Id).ShouldNotBeNull();
            thread.Subtitle.Text.ShouldBe("5 comments");
            field.Text.ShouldBe(string.Empty);
            thread.Composer.SendButton.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replies_Nest_Up_To_MaxDepth()
    {
        var (thread, window) = Show();
        try
        {
            var changes = new List<CommentThreadChange>();
            thread.CommentsChanged += (_, e) => changes.Add(e.Change);
            Click(thread.ItemOf("c2")!.ReplyButton);
            thread.ReplyingToId.ShouldBe("c2");
            thread.ReplyingBar.IsPresent.ShouldBeTrue();
            thread.ReplyingText.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text).ShouldBe(["Replying to ", "Theo Walsh"]);
            thread.Composer.CancelButton.IsVisible.ShouldBeTrue();
            thread.Composer.Field.IsFocused.ShouldBeTrue();
            thread.Composer.Text = "Nested";
            thread.Composer.Send();

            // c2 is at depth 1: its reply is the deepest allowed (depth 2).
            changes[^1].ParentId.ShouldBe("c2");
            var nested = changes[^1].Comment!;
            CommentThreadTree.Find(thread.Comments, "c2")!.Replies.ShouldContain(nested);
            thread.ItemOf(nested.Id)!.Depth.ShouldBe(2);
            thread.ReplyingToId.ShouldBeNull();
            thread.ReplyingBar.IsPresent.ShouldBeFalse();

            // A reply to that reply attaches to the deepest allowed parent (still c2).
            thread.ReplyTo(nested.Id);
            thread.Composer.Text = "Deeper";
            thread.Composer.Send();
            changes[^1].ParentId.ShouldBe("c2");

            // Escape cancels a reply.
            thread.ReplyTo("c4");
            thread.Composer.Field.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            thread.ReplyingToId.ShouldBeNull();

            // MaxDepth 1 attaches every reply to its top-level comment.
            thread.MaxDepth = 1;
            thread.ReplyTo("c2");
            thread.Composer.Text = "Flat";
            thread.Composer.Send();
            changes[^1].ParentId.ShouldBe("c1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mention_Suggestions_Follow_The_Keyboard_Model()
    {
        var (thread, window) = Show();
        try
        {
            var composer = thread.Composer;
            var field = composer.Field;
            var peer = (IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(field);
            ControlAutomationPeer.CreatePeerForElement(field).GetAutomationControlType().ShouldBe(AutomationControlType.ComboBox);
            Type(window, field, "Hi @");
            composer.IsSuggestionsOpen.ShouldBeTrue();
            composer.Suggestions.Select(p => p.Name).ShouldBe(["Ana Ruiz", "Mina Park", "Theo Walsh", "Kai Moreno"]);
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);

            window.KeyTextInput("w");
            Dispatcher.UIThread.RunJobs();
            composer.Suggestions.Select(p => p.Name).ShouldBe(["Theo Walsh"]); // matches the start of any word

            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            composer.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            composer.ActiveIndex.ShouldBe(3); // wraps
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            composer.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            composer.ActivePerson!.Name.ShouldBe("Mina Park");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe("Hi @Mina Park ");
            field.CaretIndex.ShouldBe(field.Text!.Length);
            composer.IsSuggestionsOpen.ShouldBeFalse();
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);

            // Tab inserts too; Escape only closes.
            window.KeyTextInput("@ka");
            Dispatcher.UIThread.RunJobs();
            composer.Suggestions.Select(p => p.Name).ShouldBe(["Kai Moreno"]);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe("Hi @Mina Park @Kai Moreno ");
            field.IsFocused.ShouldBeTrue();
            window.KeyTextInput("@");
            Dispatcher.UIThread.RunJobs();
            composer.IsSuggestionsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            composer.IsSuggestionsOpen.ShouldBeFalse();
            field.Text.ShouldBe("Hi @Mina Park @Kai Moreno @");

            // An email address is not a mention, and the list stays closed without matches.
            field.Text = "mail me@x";
            field.CaretIndex = field.Text.Length;
            composer.ReadQuery();
            composer.IsSuggestionsOpen.ShouldBeFalse();
            field.Text = "@zz";
            field.CaretIndex = 3;
            composer.ReadQuery();
            composer.IsSuggestionsOpen.ShouldBeFalse();

            // People limits who can be mentioned; at most five are offered.
            thread.People = [s_mina, s_theo, s_ana, s_kai, new("a", "Avery Stone"), new("b", "Ben Ito")];
            field.Text = "@";
            field.CaretIndex = 1;
            composer.ReadQuery();
            composer.Suggestions.Count.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Edit_Saves_In_Place()
    {
        var (thread, window) = Show();
        try
        {
            var changes = new List<CommentThreadChange>();
            thread.CommentsChanged += (_, e) => changes.Add(e.Change);
            var c3 = thread.ItemOf("c3")!;
            Click(c3.EditButton);
            c3.Mode.ShouldBe(CommentThreadItemMode.Edit);
            var editor = c3.Editor.ShouldNotBeNull();
            editor.Text.ShouldBe("Fits on one line now.");
            editor.Field.IsFocused.ShouldBeTrue();
            editor.SendButton.IsEnabled.ShouldBeFalse(); // unchanged
            editor.CancelButton.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(editor.Field).ShouldBe("Edit comment");
            editor.SendButton.Content.ShouldBe("Save");

            // Escape cancels.
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            c3.Mode.ShouldBe(CommentThreadItemMode.View);
            changes.ShouldBeEmpty();

            Click(c3.EditButton);
            editor.Text = "Fits on one line at 375px.";
            editor.SendButton.IsEnabled.ShouldBeTrue();
            editor.Field.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            var change = changes.ShouldHaveSingleItem();
            change.Kind.ShouldBe(CommentThreadChangeKind.Edit);
            change.Id.ShouldBe("c3");
            change.Body.ShouldBe("Fits on one line at 375px.");
            var edited = CommentThreadTree.Find(thread.Comments, "c3")!;
            edited.IsEdited.ShouldBeTrue();
            thread.ItemOf("c3").ShouldBeSameAs(c3); // kept by id
            c3.Mode.ShouldBe(CommentThreadItemMode.View);
            string.Concat(c3.Body.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)).ShouldBe("Fits on one line at 375px.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delete_Confirms_And_Keeps_A_Placeholder_For_Comments_With_Replies()
    {
        var comments = Review().Select(c => c.Id == "c1" ? c with { Author = s_ana } : c).ToArray();
        var (thread, window) = Show(comments);
        try
        {
            var changes = new List<CommentThreadChange>();
            thread.CommentsChanged += (_, e) => changes.Add(e.Change);
            var c1 = thread.ItemOf("c1")!;
            Click(c1.DeleteButton);
            c1.Mode.ShouldBe(CommentThreadItemMode.Confirm);
            c1.Reveal.IsRevealed.ShouldBeTrue();
            c1.ConfirmDeleteButton.ShouldNotBeNull().IsFocused.ShouldBeTrue();
            ((CommentThreadText)c1.ConfirmRow!.Children[0]).Text.ShouldBe("Delete this comment?");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            c1.Mode.ShouldBe(CommentThreadItemMode.View);

            Click(c1.DeleteButton);
            Click(c1.ConfirmDeleteButton!);
            changes.ShouldHaveSingleItem().ShouldBe(CommentThreadChange.Delete("c1"));
            var deleted = CommentThreadTree.Find(thread.Comments, "c1")!;
            deleted.IsDeleted.ShouldBeTrue();
            deleted.Body.ShouldBeEmpty();
            deleted.Replies.Count.ShouldBe(2);
            c1.DeletedText.IsVisible.ShouldBeTrue();
            c1.DeletedText.Text.ShouldBe("This comment was deleted");
            c1.Portrait.IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(c1.Article).ShouldBe("Deleted comment");
            thread.Subtitle.Text.ShouldBe("3 comments");

            // A comment without replies goes away.
            var c3 = thread.ItemOf("c3")!;
            Click(c3.DeleteButton);
            Click(c3.ConfirmDeleteButton!);
            thread.ItemOf("c3").ShouldBeNull();
            c1.RepliesPanel.Children.ShouldNotContain(c3);
            CommentThreadTree.Find(thread.Comments, "c3").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reactions_Toggle_Through_The_Picker_And_The_Chips()
    {
        var (thread, window) = Show();
        try
        {
            var changes = new List<CommentThreadChange>();
            thread.CommentsChanged += (_, e) => changes.Add(e.Change);
            var c4 = thread.ItemOf("c4")!;
            c4.ReactionBar.Reactions.ShouldBeEmpty();
            Click(c4.AddReactionButton);
            c4.Mode.ShouldBe(CommentThreadItemMode.Picker);
            var emoji = c4.EmojiButtons;
            emoji.Select(b => (string)b.Content!).ShouldBe(thread.Reactions);
            emoji[0].IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(emoji[2]).ShouldBe("React with 🎉");
            AutomationProperties.GetName(c4.PickerRow!).ShouldBe("Reactions");

            Click(emoji[2]);
            changes[^1].ShouldBe(CommentThreadChange.React("c4", "🎉", added: true));
            c4.Mode.ShouldBe(CommentThreadItemMode.View);
            var chip = c4.ReactionBar.ChipOf("🎉").ShouldNotBeNull();
            chip.IsChecked.ShouldBe(true);
            chip.Count.ShouldBe(1);

            // Clicking the chip takes the reaction back; the last one removes the chip.
            Click(chip);
            changes[^1].ShouldBe(CommentThreadChange.React("c4", "🎉", added: false));
            CommentThreadTree.Find(thread.Comments, "c4")!.Reactions.ShouldBeEmpty();
            c4.ReactionBar.ChipOf("🎉").ShouldBeNull();

            // Joining someone else's reaction rolls its count.
            var c1 = thread.ItemOf("c1")!;
            var eyes = c1.ReactionBar.ChipOf("👀")!;
            Click(eyes);
            c1.ReactionBar.ChipOf("👀").ShouldBeSameAs(eyes);
            eyes.Count.ShouldBe(2);
            eyes.IsChecked.ShouldBe(true);
            CommentThreadTree.Find(thread.Comments, "c1")!.Reactions.Single(r => r.Emoji == "👀").UserIds.ShouldBe(["kai", "ana"]);

            // The picker marks emoji already chosen and closes on Escape.
            Click(c1.AddReactionButton);
            c1.EmojiButtons.Single(b => (string)b.Content! == "👀").IsOn.ShouldBeTrue();
            c1.EmojiButtons.Single(b => (string)b.Content! == "❤️").IsOn.ShouldBeFalse();
            ((IToggleProvider)ControlAutomationPeer.CreatePeerForElement(c1.EmojiButtons[0])).ToggleState.ShouldBe(ToggleState.On);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            c1.Mode.ShouldBe(CommentThreadItemMode.View);
            c1.AddReactionButton.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replies_Collapse_And_Expand()
    {
        var (thread, window) = Show();
        try
        {
            var c1 = thread.ItemOf("c1")!;
            var peer = (IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(c1.Toggle);
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            c1.Replies.IsPresent.ShouldBeTrue();
            Click(c1.Toggle);
            c1.Replies.IsPresent.ShouldBeFalse();
            c1.Toggle.Label.ShouldBe("Show 2 replies");
            AutomationProperties.GetName(c1.Toggle).ShouldBe("Show 2 replies");
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            c1.Toggle.Stack.IsVisible.ShouldBeTrue();
            c1.Toggle.Stack.Names.ShouldBe(["Theo Walsh", "Ana Ruiz"]);
            peer.Expand();
            Dispatcher.UIThread.RunJobs();
            c1.Replies.IsPresent.ShouldBeTrue();
            c1.Toggle.Label.ShouldBe("Hide replies");

            // Replying into a collapsed comment opens it.
            Click(c1.Toggle);
            thread.ReplyTo("c1");
            thread.Composer.Text = "Opening";
            thread.Composer.Send();
            c1.Replies.IsPresent.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resolving_Folds_The_Thread_Into_A_Chip_And_Moves_Focus()
    {
        var (thread, window) = Show();
        try
        {
            var resolved = new List<bool>();
            thread.ResolvedChanged += (_, e) => resolved.Add(e.IsResolved);
            Click(thread.ResolveButton);
            thread.IsResolved.ShouldBeTrue();
            resolved.ShouldBe([true]);
            thread.Classes.ShouldContain(":resolved");
            thread.CornerRadius.ShouldBe(new CornerRadius(22));
            thread.Faces.Active.ShouldBeSameAs(thread.ResolvedFace);
            thread.ThreadFace.IsVisible.ShouldBeFalse();
            thread.ResolvedTitleText.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text).ShouldBe(["Resolved · ", "Hero headline"]);
            thread.ResolvedMetaText.Text.ShouldBe("4 comments · 4 people");
            thread.ResolvedStack.Names.ShouldBe(["Mina Park", "Theo Walsh", "Ana Ruiz"]);
            thread.ReopenButton.IsFocused.ShouldBeTrue();
            thread.Bounds.Height.ShouldBeLessThan(80);

            Click(thread.ReopenButton);
            thread.IsResolved.ShouldBeFalse();
            resolved.ShouldBe([true, false]);
            thread.Faces.Active.ShouldBeSameAs(thread.ThreadFace);
            thread.ResolveButton.IsFocused.ShouldBeTrue();

            // Setting the property from outside folds it too (two-way binding).
            thread.IsResolved = true;
            Dispatcher.UIThread.RunJobs();
            thread.Faces.Active.ShouldBeSameAs(thread.ResolvedFace);
            resolved.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Thread_Without_Comments_Cannot_Be_Resolved()
    {
        var (thread, window) = Show([]);
        try
        {
            thread.Resolve();
            thread.IsResolved.ShouldBeFalse();
            thread.EmptyPart.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Controlled_Thread_Can_Refuse_A_Change()
    {
        var (thread, window) = Show();
        try
        {
            var original = thread.Comments;
            thread.CommentsChanged += (_, _) => thread.Comments = original;
            thread.Composer.Text = "Not saved";
            thread.Composer.Send();
            thread.Comments.ShouldBeSameAs(original);
            thread.Items.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var (thread, window) = Show();
        try
        {
            thread.Labels = new CommentThreadLabels
            {
                Thread = "Diskussion",
                Resolve = "Erledigen",
                Reply = "Antworten",
                CommentCount = n => $"{n} Kommentare",
                HideReplies = "Antworten ausblenden"
            };
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(thread).ShouldBe("Diskussion");
            thread.ResolveButton.Content.ShouldBe("Erledigen");
            thread.Subtitle.Text.ShouldBe("4 Kommentare");
            thread.ItemOf("c1")!.ReplyButton.Content.ShouldBe("Antworten");
            thread.ItemOf("c1")!.Toggle.Label.ShouldBe("Antworten ausblenden");
            thread.ItemOf("c1")!.DeleteButton.Content.ShouldBe("Delete"); // untouched words keep the default
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Actions_Come_Forward_On_Focus()
    {
        var (thread, window) = Show();
        try
        {
            var c1 = thread.ItemOf("c1")!;
            c1.Reveal.IsRevealed.ShouldBeFalse();
            c1.Reveal.Opacity.ShouldBe(0);
            c1.ReplyButton.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            c1.Reveal.IsRevealed.ShouldBeTrue();
            c1.Reveal.Opacity.ShouldBe(1);
            thread.Composer.Field.Focus();
            Dispatcher.UIThread.RunJobs();
            c1.Reveal.IsRevealed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Threads_Drop_The_Composer_Avatar()
    {
        var (thread, window) = Show(width: 360);
        try
        {
            thread.Classes.ShouldContain(":narrow");
            thread.Composer.Portrait!.IsVisible.ShouldBeFalse();
            var rail = TestHelpers.Descendant<CommentThreadRail>(thread.ItemOf("c1")!);
            rail.Margin.Left.ShouldBe(12);
            thread.Width = 520;
            Dispatcher.UIThread.RunJobs();
            thread.Composer.Portrait!.IsVisible.ShouldBeTrue();
            rail.Margin.Left.ShouldBe(24);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Comments_Grow_In_On_The_Height_Spring()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (thread, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var before = thread.Bounds.Height;
            thread.Composer.Text = "Fresh";
            thread.Composer.Send();
            Dispatcher.UIThread.RunJobs();
            var item = thread.Items.Values.Single(i => i.Comment!.Body == "Fresh");
            item.IsMoving.ShouldBeTrue();
            item.Opacity.ShouldBeLessThan(0.1);
            frames.Advance(TimeSpan.FromMilliseconds(120));
            Dispatcher.UIThread.RunJobs();
            item.Bounds.Height.ShouldBeGreaterThan(0);
            item.Bounds.Height.ShouldBeLessThan(item.Child!.DesiredSize.Height);
            item.Clip.ShouldNotBeNull();
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            item.IsMoving.ShouldBeFalse();
            item.Opacity.ShouldBe(1, 0.01);
            item.Clip.ShouldBeNull();
            item.Bounds.Height.ShouldBe(item.Child!.DesiredSize.Height, 0.5);
            thread.Bounds.Height.ShouldBeGreaterThan(before + 40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Deleted_Comments_And_Collapsed_Replies_Shrink_Away()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (thread, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var c3 = thread.ItemOf("c3")!;
            var full = c3.Bounds.Height;
            thread.BeginConfirm("c3");
            thread.Remove("c3");
            Dispatcher.UIThread.RunJobs();
            thread.ItemOf("c3").ShouldBeNull();
            c3.IsMoving.ShouldBeTrue();
            frames.Advance(TimeSpan.FromMilliseconds(100));
            Dispatcher.UIThread.RunJobs();
            c3.Bounds.Height.ShouldBeInRange(1, full - 1);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            thread.ItemOf("c1")!.RepliesPanel.Children.ShouldNotContain(c3);

            var c1 = thread.ItemOf("c1")!;
            thread.ToggleReplies("c1");
            Dispatcher.UIThread.RunJobs();
            c1.Replies.IsMoving.ShouldBeTrue();
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            c1.Replies.IsMoving.ShouldBeFalse();
            c1.Replies.Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resolving_Springs_The_Height_And_Cross_Fades_The_Faces()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (thread, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var open = thread.Bounds.Height;
            thread.Resolve();
            Dispatcher.UIThread.RunJobs();
            thread.AutoHeightHost!.IsMoving.ShouldBeTrue();
            thread.ThreadFace.IsVisible.ShouldBeTrue(); // fading out of the flow
            frames.Advance(TimeSpan.FromMilliseconds(32));
            Dispatcher.UIThread.RunJobs();
            thread.ResolvedFace.Opacity.ShouldBeInRange(0.05, 0.99);
            thread.ResolvedFace.Effect.ShouldNotBeNull(); // subtle blur while it fades in
            frames.Advance(TimeSpan.FromMilliseconds(70));
            Dispatcher.UIThread.RunJobs();
            var mid = thread.Bounds.Height;
            mid.ShouldBeLessThan(open);
            mid.ShouldBeGreaterThan(80);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            thread.AutoHeightHost.IsMoving.ShouldBeFalse();
            thread.ThreadFace.IsVisible.ShouldBeFalse();
            thread.ResolvedFace.Opacity.ShouldBe(1);
            thread.ResolvedFace.Effect.ShouldBeNull();
            thread.Bounds.Height.ShouldBeLessThan(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Slides_In_After_The_Actions_Fade_And_Staggers_Its_Emoji()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (thread, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var c4 = thread.ItemOf("c4")!;
            thread.OpenPicker("c4");
            Dispatcher.UIThread.RunJobs();
            // mode="wait": the actions fade out first.
            c4.Sequence.Displayed.ShouldNotBeSameAs(c4.PickerRow);
            frames.Advance(TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.RunJobs();
            c4.Sequence.Displayed.ShouldBeSameAs(c4.PickerRow);
            frames.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
            var row = c4.PickerRow!;
            ((TranslateTransform)row.RenderTransform!).X.ShouldBeLessThan(0); // from x -6
            var last = c4.EmojiButtons[^1];
            last.Opacity.ShouldBeLessThan(c4.EmojiButtons[0].Opacity + 0.001); // staggered
            frames.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            ((TranslateTransform)row.RenderTransform!).X.ShouldBe(0, 0.01);
            last.Opacity.ShouldBe(1, 0.01);
            ((ScaleTransform)last.RenderTransform!).ScaleX.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Without_Moving()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (thread, window) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            thread.Composer.Text = "Calm";
            thread.Composer.Send();
            Dispatcher.UIThread.RunJobs();
            var item = thread.Items.Values.Single(i => i.Comment!.Body == "Calm");
            item.IsMoving.ShouldBeFalse(); // height changes at once
            item.Bounds.Height.ShouldBe(item.Child!.DesiredSize.Height, 0.5);
            item.Opacity.ShouldBeLessThan(1);
            frames.Advance(TimeSpan.FromMilliseconds(300));
            Dispatcher.UIThread.RunJobs();
            item.Opacity.ShouldBe(1, 0.01);

            thread.Resolve();
            Dispatcher.UIThread.RunJobs();
            thread.AutoHeightHost!.IsMoving.ShouldBeFalse();
            thread.ThreadFace.IsVisible.ShouldBeFalse(); // swaps in place
            thread.Bounds.Height.ShouldBeLessThan(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Applies_The_End_State()
    {
        var (thread, window) = Show();
        try
        {
            thread.Composer.Text = "Instant";
            thread.Composer.Send();
            var item = thread.Items.Values.Single(i => i.Comment!.Body == "Instant");
            item.IsMoving.ShouldBeFalse();
            item.Opacity.ShouldBe(1);
            thread.OpenPicker(item.Id);
            item.Sequence.Displayed.ShouldBeSameAs(item.PickerRow);
            item.PickerRow!.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
