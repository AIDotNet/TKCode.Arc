using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AgentMemory"/> assembled from its parts, on the invented samples (Orbit, eleven memories, capacity 900):
/// the view (groups, filter, search), editing, forgetting with undo, filing the captured memory, adding, the switches,
/// the meter, sources, outside lists, the narrow layout, motion levels, the keyboard and the live region. Every clock is
/// manual (<see cref="AgentMemoryRig"/>).
/// </summary>
public class AgentMemoryTests
{
    private static string[] Groups(AgentMemory memory) => memory.ShownGroups.Select(g => g.Key).ToArray();

    private static string[] Rows(AgentMemory memory, string group) => memory.ShownMemories(group).Select(m => m.Id).ToArray();

    private static void Click(AvaloniaButton? button)
    {
        Assert.NotNull(button);
        button!.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    #region View

    [AvaloniaFact]
    public void The_All_View_Shows_Every_Group_With_Headings_And_Counts()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        Assert.Equal(["about", "preferences", "projects", "people"], Groups(memory));
        foreach (var view in memory.ShownGroups)
        {
            Assert.True(view.IsHeadingShown);
            Assert.False(view.IsEmptyShown);
        }
        Assert.Equal("3", memory.GroupViewOf("about")!.CountText);
        Assert.Equal("2", memory.GroupViewOf("projects")!.CountText);
        Assert.Equal("About you", memory.GroupViewOf("about")!.Label);
        // Newest first, as the list gives them.
        Assert.Equal(["about-3", "about-2", "about-1"], Rows(memory, "about"));
        Assert.Equal(["people-3", "people-1", "people-2"], Rows(memory, "people"));
        // The filter pills: All and the four default groups.
        Assert.Equal(["", "about", "preferences", "projects", "people"], memory.FilterBar!.Options!.Select(o => o.Key ?? "").ToArray());
        Assert.Equal(["All", "About you", "Preferences", "Projects", "People"], memory.FilterBar.Options!.Select(o => o.Label).ToArray());
        Assert.Contains(":first", memory.GroupViewOf("about")!.Classes);
        Assert.DoesNotContain(":first", memory.GroupViewOf("people")!.Classes);
    }

    [AvaloniaFact]
    public void The_Header_Uses_The_Assistant_Name_And_The_Meter_The_Characters()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        Assert.Equal("Memory", memory.TitleText!.Text);
        Assert.Equal("What Orbit remembers to keep your chats on track", memory.SubtitleText!.Text);
        memory.Title = "What I know";
        memory.Subtitle = "Things to keep in mind";
        Assert.Equal("What I know", memory.TitleText.Text);
        Assert.Equal("Things to keep in mind", memory.SubtitleText.Text);

        Assert.Equal(523, memory.UsedCharacters);
        Assert.Equal(523, memory.Meter!.Used);
        Assert.Equal(900, memory.Meter.Capacity);
        Assert.Equal(58, memory.Meter.DisplayedPercent);
        Assert.Equal("Memory used", AutomationProperties.GetName(memory.Meter));
        Assert.True(memory.Meter.IsEffectivelyVisible);

        memory.Capacity = 0;
        rig.Pump();
        Assert.Contains(":no-meter", memory.Classes);
        Assert.False(memory.Meter.IsEffectivelyVisible);

        memory.Capacity = 560;
        rig.Pump();
        Assert.True(memory.Meter.IsWarning);
    }

    [AvaloniaFact]
    public void A_Single_Group_Shows_Its_Rows_Without_A_Heading()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.SelectedGroupKey = "preferences";
        rig.Pump();
        Assert.Equal(["preferences"], Groups(memory));
        Assert.False(memory.GroupViewOf("preferences")!.IsHeadingShown);
        Assert.Equal("preferences", memory.FilterBar!.SelectedKey);

        memory.SelectedGroupKey = null;
        rig.Pump();
        Assert.Equal(4, Groups(memory).Length);
        Assert.True(memory.GroupViewOf("preferences")!.IsHeadingShown);
    }

    [AvaloniaFact]
    public void Choosing_A_Pill_Filters_And_An_Unknown_Key_Falls_Back_To_All()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var people = memory.FilterBar!.Pills.First(p => p.Key == "people");
        Click(people);
        rig.Pump();
        Assert.Equal("people", memory.SelectedGroupKey);
        Assert.Equal(["people"], Groups(memory));

        memory.SelectedGroupKey = "nowhere";
        rig.Pump();
        // The bar falls back to All and the card follows.
        Assert.Null(memory.SelectedGroupKey);
        Assert.Equal(4, Groups(memory).Length);
    }

    [AvaloniaFact]
    public void An_Empty_Group_Hides_In_All_And_Shows_Its_Sentence_Alone()
    {
        using var rig = new AgentMemoryRig(configure: m =>
        {
            foreach (var item in m.Memories.Where(i => i.GroupKey == "projects").ToList())
            {
                m.Memories.Remove(item);
            }
        });
        var memory = rig.Memory;
        Assert.Equal(["about", "preferences", "people"], Groups(memory));

        memory.SelectedGroupKey = "projects";
        rig.Pump();
        var projects = memory.GroupViewOf("projects")!;
        Assert.Equal(["projects"], Groups(memory));
        Assert.True(projects.IsEmptyShown);
        Assert.Equal("No projects yet. Ongoing work you bring up will be listed here.", projects.EmptyText);
        Assert.False(projects.IsHeadingShown);

        // The preferences sentence names the assistant.
        memory.SelectedGroupKey = "preferences";
        rig.Pump();
        Assert.Equal("No preferences yet. Tell Orbit how you like answers.", memory.GroupViewOf("preferences")!.EmptyText);
    }

    [AvaloniaFact]
    public void Custom_Groups_Replace_The_Defaults_And_Unknown_Keys_Show_In_The_First()
    {
        using var rig = new AgentMemoryRig(configure: m =>
        {
            m.Groups.Add(new MemoryGroup { Key = "people", Label = "Colleagues", EmptyText = "Nobody yet" });
            m.Groups.Add(new MemoryGroup { Key = "rest", Label = "Everything else", EmptyText = "Nothing else" });
        });
        var memory = rig.Memory;
        // "rest" has no memories: the All view hides it.
        Assert.Equal(["people"], Groups(memory));
        Assert.Equal("Colleagues", memory.GroupViewOf("people")!.Label);
        // about, preferences and projects are not groups of this card: they show in the first group.
        Assert.Equal(11, memory.ShownMemories("people").Count());
        Assert.Equal(["", "people", "rest"], memory.FilterBar!.Options!.Select(o => o.Key ?? "").ToArray());

        memory.Groups[1].Label = "Other";
        rig.Pump();
        Assert.Equal("Other", memory.FilterBar.Options!.Last().Label);
    }

    [AvaloniaFact]
    public void The_Card_Turns_Compact_Below_440()
    {
        using var rig = new AgentMemoryRig(width: 700);
        var memory = rig.Memory;
        Assert.False(memory.IsCompact);
        Assert.DoesNotContain(":compact", memory.Classes);
        rig.Window.Width = 320;
        rig.Pump();
        Assert.True(memory.IsCompact);
        Assert.Contains(":compact", memory.Classes);
        rig.Window.Width = 440;
        rig.Pump();
        Assert.False(memory.IsCompact);
    }

    [AvaloniaFact]
    public void The_Card_Is_At_Most_640_Wide_And_Centered()
    {
        using var rig = new AgentMemoryRig(width: 900);
        var card = TestHelpers.Descendant<Border>(rig.Memory, "PART_Card");
        Assert.Equal(640, card.Bounds.Width, 0.5);
        Assert.Equal((900 - 640) / 2.0, card.Bounds.X, 1);
    }

    #endregion

    #region Search

    [AvaloniaFact]
    public void Search_Covers_Every_Group_Whatever_The_Filter_With_Hit_Counts_And_Marks()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.SelectedGroupKey = "people";
        rig.Pump();
        memory.OpenSearch();
        rig.Pump();
        Assert.True(memory.IsSearchOpen);
        Assert.True(memory.FilterBar!.IsConcealed);
        rig.Type("PLAN");
        Assert.Equal("PLAN", memory.SearchText);
        // "explanation" (preferences) and "depot planner" (projects); "platform" is not a match.
        Assert.Equal(["preferences", "projects"], Groups(memory));
        Assert.Equal(["preferences-1"], Rows(memory, "preferences"));
        Assert.Equal("1", memory.GroupViewOf("projects")!.CountText);
        Assert.True(memory.GroupViewOf("projects")!.IsHeadingShown);
        Assert.Equal(2, memory.MatchCount);
        var text = memory.ViewOf(rig.Item("projects-1"))!.TextBlock!;
        Assert.Equal("PLAN", text.Query);
        Assert.Equal(1, text.MatchCount);
        Assert.Equal("2 matches", memory.LastAnnouncement);
        Assert.False(memory.EmptySearch!.IsShown);

        // The filter is untouched: closing the search brings People back.
        rig.Key(PhysicalKey.Escape);
        Assert.False(memory.IsSearchOpen);
        Assert.Equal(string.Empty, memory.SearchText);
        Assert.Equal("people", memory.SelectedGroupKey);
        Assert.Equal(["people"], Groups(memory));
        Assert.Equal(string.Empty, memory.ViewOf(rig.Item("projects-1"))!.Query);
    }

    [AvaloniaFact]
    public void A_Search_Without_Matches_Shows_The_Empty_State_And_Clear_Search_Keeps_It_Open()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.OpenSearch();
        rig.Type("qzx");
        Assert.Empty(Groups(memory));
        Assert.True(memory.EmptySearch!.IsShown);
        Assert.Equal("No memories match “qzx”", memory.NoMatchesText!.Text);
        Assert.Equal("No matches", memory.LastAnnouncement);

        Click(memory.ClearSearchButton);
        rig.Pump();
        Assert.True(memory.IsSearchOpen);
        Assert.Equal(string.Empty, memory.SearchText);
        Assert.False(memory.EmptySearch.IsShown);
        Assert.Equal(4, Groups(memory).Length);
        Assert.Same(memory.SearchBox!.Input, rig.Focused);
    }

    [AvaloniaFact]
    public void Escape_Clears_And_Closes_The_Search_And_Focuses_The_Magnifier_With_Its_Ring()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var search = memory.SearchBox!;
        search.ToggleButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.True(memory.IsSearchOpen);
        Assert.Same(search.Input, rig.Focused);
        rig.Type("tables");
        Assert.Equal(["preferences"], Groups(memory));
        rig.Key(PhysicalKey.Escape);
        Assert.False(memory.IsSearchOpen);
        Assert.Same(search.ToggleButton, rig.Focused);
        Assert.Contains(":focus-visible", search.ToggleButton.Classes);
        Assert.False(memory.FilterBar!.IsConcealed);
    }

    [AvaloniaFact]
    public void Leaving_An_Empty_Search_Closes_It_And_Leaving_One_With_Text_Keeps_It()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.OpenSearch();
        rig.Pump();
        memory.AddField!.Input!.Focus();
        rig.Pump();
        Assert.False(memory.IsSearchOpen);

        memory.OpenSearch();
        rig.Type("bill");
        memory.AddField.Input.Focus();
        rig.Pump();
        Assert.True(memory.IsSearchOpen);
        Assert.Equal("bill", memory.SearchText);
    }

    [AvaloniaFact]
    public void The_Search_Properties_Work_From_Code()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.SearchText = "Tuesday";
        rig.Pump();
        Assert.True(memory.IsSearchOpen);
        Assert.True(memory.SearchBox!.IsOpen);
        Assert.Equal("Tuesday", memory.SearchBox.Text);
        Assert.Equal(["people"], Groups(memory));
        Assert.Equal(["people-3"], Rows(memory, "people"));

        memory.IsSearchOpen = false;
        rig.Pump();
        Assert.Equal(string.Empty, memory.SearchText);
        Assert.Equal(4, Groups(memory).Length);
    }

    [AvaloniaFact]
    public void The_Magnifier_Opens_The_Search_With_The_Focus_In_The_Input()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var search = memory.SearchBox!;
        Assert.True(search.ToggleButton!.Expanded == false);
        Click(search.ToggleButton);
        rig.Pump();
        Assert.True(memory.IsSearchOpen);
        Assert.True(search.ToggleButton.Expanded);
        Assert.False(search.ToggleButton.IsTabStop);
        Assert.True(search.Input!.IsVisible);
        Assert.Same(search.Input, rig.Focused);
        Assert.Equal(1, search.Openness, 3);
        Click(search.CloseButton);
        rig.Pump();
        Assert.False(memory.IsSearchOpen);
        Assert.Equal(0, search.Openness, 3);
        Assert.False(search.Input.IsVisible);
    }

    #endregion

    #region Editing

    [AvaloniaFact]
    public void Edit_Opens_The_Editor_With_The_Caret_At_The_End()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        Click(memory.ViewOf(item)!.EditButton);
        rig.Pump();
        var editor = memory.EditorOf(item)!;
        Assert.NotNull(editor);
        Assert.Same(editor, memory.RowOf(item)!.Content);
        Assert.Equal(item.Text, editor.Text);
        Assert.Same(editor.TextBox, rig.Focused);
        Assert.Equal(item.Text.Length, editor.TextBox!.CaretIndex);
        Assert.Equal("Edit memory: Is training for a spring half marathon", editor.AccessibleName);
        Assert.Equal(200, editor.MaxLength);
        Assert.Equal("Enter to save, Esc to cancel", editor.HintText);
    }

    [AvaloniaFact]
    public void Saving_Without_A_Hook_Shows_A_Short_Save_Then_The_New_Sentence()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.BeginEdit(item);
        rig.Pump();
        var editor = memory.EditorOf(item)!;
        editor.TextBox!.Text = "Is training for an autumn marathon";
        rig.Key(PhysicalKey.Enter);
        Assert.True(editor.IsSaving);
        Assert.Equal([TimeSpan.FromMilliseconds(380)], rig.Delays.Pending);
        Assert.Equal("Is training for a spring half marathon", item.Text);

        rig.Delays.Finish(TimeSpan.FromMilliseconds(380));
        rig.Pump();
        Assert.Equal("Is training for an autumn marathon", item.Text);
        Assert.Null(memory.EditorOf(item));
        var view = memory.ViewOf(item)!;
        Assert.Same(view, memory.RowOf(item)!.Content);
        Assert.Equal("Is training for an autumn marathon", view.Text);
        Assert.Same(view.EditButton, rig.Focused);
        Assert.Equal("Memory updated", memory.LastAnnouncement);
        var change = Assert.Single(rig.Changes).Change;
        Assert.Equal(MemoryChangeKind.Edit, change.Kind);
        Assert.Same(item, change.Memory);
        Assert.Equal("Is training for a spring half marathon", change.PreviousText);
        Assert.Equal(523 - 38 + 34, memory.UsedCharacters);
    }

    [AvaloniaFact]
    public void Unchanged_Text_Closes_The_Editor_Without_A_Change()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.BeginEdit(item);
        rig.Pump();
        memory.EditorOf(item)!.TextBox!.Text = item.Text + "  ";
        rig.Key(PhysicalKey.Enter);
        Assert.Null(memory.EditorOf(item));
        Assert.Empty(rig.Delays.Pending);
        Assert.Empty(rig.Changes);
        Assert.Null(memory.LastAnnouncement);
        Assert.Same(memory.ViewOf(item)!.EditButton, rig.Focused);
    }

    [AvaloniaFact]
    public void Empty_And_Too_Long_Drafts_Never_Reach_The_Hook()
    {
        var calls = 0;
        using var rig = new AgentMemoryRig(configure: m => m.CommitAsync = (_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.BeginEdit(item);
        rig.Pump();
        var editor = memory.EditorOf(item)!;
        editor.TextBox!.Text = "   ";
        rig.Key(PhysicalKey.Enter);
        Assert.Equal("Write something to remember", editor.ErrorText);
        editor.TextBox.Text = new string('x', 201);
        rig.Pump();
        Assert.True(editor.IsOverLimit);
        rig.Key(PhysicalKey.Enter);
        Assert.Equal("Keep it under 200 characters", editor.ErrorText);
        Assert.Equal(0, calls);
        Assert.Same(editor, memory.EditorOf(item));
    }

    [AvaloniaFact]
    public void The_Hook_Gets_A_Proposal_And_The_Memory_Changes_Only_After_It()
    {
        var pending = new TaskCompletionSource();
        MemoryChange? seen = null;
        using var rig = new AgentMemoryRig(configure: m => m.CommitAsync = (change, _) =>
        {
            seen = change;
            return pending.Task;
        });
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.BeginEdit(item);
        rig.Pump();
        var editor = memory.EditorOf(item)!;
        editor.TextBox!.Text = "Runs a half marathon in April";
        rig.Key(PhysicalKey.Enter);
        Assert.NotNull(seen);
        Assert.Equal(MemoryChangeKind.Edit, seen!.Kind);
        Assert.Equal("Runs a half marathon in April", seen.Memory.Text);
        Assert.Equal("about-2", seen.Memory.Id);
        Assert.Equal("about", seen.Memory.GroupKey);
        Assert.Equal("Is training for a spring half marathon", seen.PreviousText);
        Assert.Equal("Is training for a spring half marathon", item.Text);
        Assert.True(editor.IsSaving);
        Assert.True(editor.SaveButton!.IsLoading);

        pending.SetResult();
        rig.Pump();
        Assert.Equal("Runs a half marathon in April", item.Text);
        Assert.Single(rig.Changes);
    }

    [AvaloniaFact]
    public void A_Failed_Save_Shows_Its_Message_And_Changes_Nothing()
    {
        using var rig = new AgentMemoryRig(configure: m => m.CommitAsync = (_, _) => throw new InvalidOperationException("Offline"));
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.BeginEdit(item);
        rig.Pump();
        var editor = memory.EditorOf(item)!;
        editor.TextBox!.Text = "Runs a half marathon in April";
        rig.Key(PhysicalKey.Enter);
        Assert.Equal("Offline", editor.ErrorText);
        Assert.False(editor.IsSaving);
        Assert.Same(editor, memory.EditorOf(item));
        Assert.Equal("Is training for a spring half marathon", item.Text);
        Assert.Empty(rig.Changes);

        // Without a message the editor says the generic sentence.
        memory.CommitAsync = (_, _) => throw new SilentException();
        editor.TextBox.Text = "Runs a half marathon in May";
        rig.Key(PhysicalKey.Enter);
        Assert.Equal("Couldn’t save. Try again", editor.ErrorText);
    }

    private sealed class SilentException : Exception
    {
        public override string Message => string.Empty;
    }

    [AvaloniaFact]
    public void A_Cancelled_Save_Rolls_Back_Silently()
    {
        using var rig = new AgentMemoryRig(configure: m => m.CommitAsync = (_, _) => throw new OperationCanceledException());
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.BeginEdit(item);
        rig.Pump();
        var editor = memory.EditorOf(item)!;
        editor.TextBox!.Text = "Runs a half marathon in April";
        rig.Key(PhysicalKey.Enter);
        Assert.Null(editor.ErrorText);
        Assert.False(editor.IsSaving);
        Assert.Same(editor, memory.EditorOf(item));
        Assert.Equal("Is training for a spring half marathon", item.Text);
        Assert.Empty(rig.Changes);
        Assert.Null(memory.LastAnnouncement);
    }

    [AvaloniaFact]
    public void Escape_And_Cancel_Close_The_Editor_And_Focus_Edit()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.BeginEdit(item);
        rig.Pump();
        memory.EditorOf(item)!.TextBox!.Text = "Something else";
        rig.Key(PhysicalKey.Escape);
        Assert.Null(memory.EditorOf(item));
        Assert.Equal("Is training for a spring half marathon", item.Text);
        Assert.Same(memory.ViewOf(item)!.EditButton, rig.Focused);

        memory.BeginEdit(item);
        rig.Pump();
        Click(memory.EditorOf(item)!.CancelButton);
        rig.Pump();
        Assert.Null(memory.EditorOf(item));
        Assert.Same(memory.ViewOf(item)!.EditButton, rig.Focused);
        Assert.Empty(rig.Changes);
    }

    [AvaloniaFact]
    public void Several_Rows_Can_Be_Edited_At_Once()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        Assert.True(memory.BeginEdit(rig.Item("about-2")));
        Assert.True(memory.BeginEdit(rig.Item("about-3")));
        rig.Pump();
        Assert.NotNull(memory.EditorOf(rig.Item("about-2")));
        Assert.NotNull(memory.EditorOf(rig.Item("about-3")));
        Assert.False(memory.BeginEdit(new MemoryItem { Id = "stranger" }));
    }

    #endregion

    #region Forgetting

    [AvaloniaFact]
    public void Forget_Shows_The_Undo_Bar_And_Drops_The_Count_And_The_Meter_At_Once()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        var view = memory.ViewOf(item)!;
        view.ForgetButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        var bar = memory.UndoBarOf(item)!;
        Assert.NotNull(bar);
        Assert.Same(bar, memory.RowOf(item)!.Content);
        Assert.Equal("Forgot ", bar.Prefix);
        Assert.Equal("“Is training for a spring half marathon”", bar.Quote);
        Assert.Equal("Forgotten memory", AutomationProperties.GetName(bar));
        Assert.True(bar.Undo.IsRunning);
        Assert.Equal("5s", bar.Undo.SecondsLabel);
        Assert.Same(bar.Undo, rig.Focused);
        Assert.Equal("2", memory.GroupViewOf("about")!.CountText);
        Assert.Equal(523 - 38, memory.UsedCharacters);
        Assert.Equal(523 - 38, memory.Meter!.Used);
        Assert.Equal("Forgot “Is training for a spring half marathon”. You can undo for 5 seconds", memory.LastAnnouncement);
        // Still in the list until the window ends.
        Assert.Contains(item, memory.Memories);
        Assert.Empty(rig.Changes);
    }

    [AvaloniaFact]
    public void Two_Undo_Bars_Coexist_And_Undo_Brings_A_Row_Back()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var first = rig.Item("about-2");
        var second = rig.Item("about-3");
        Assert.True(memory.Forget(first));
        Assert.True(memory.Forget(second));
        Assert.False(memory.Forget(second));
        rig.Pump();
        Assert.NotNull(memory.UndoBarOf(first));
        Assert.NotNull(memory.UndoBarOf(second));
        Assert.Equal("1", memory.GroupViewOf("about")!.CountText);
        Assert.Equal(2, rig.Clock.Count);

        var bar = memory.UndoBarOf(first)!;
        bar.Undo.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.Null(memory.UndoBarOf(first));
        Assert.Same(memory.ViewOf(first), memory.RowOf(first)!.Content);
        Assert.Equal("2", memory.GroupViewOf("about")!.CountText);
        Assert.Equal(523 - 49, memory.UsedCharacters);
        Assert.Equal("Memory restored", memory.LastAnnouncement);
        Assert.Same(memory.ViewOf(first)!.ForgetButton, rig.Focused);
        Assert.Equal(1, rig.Clock.Count);
        Assert.False(memory.UndoForget(first));
    }

    [AvaloniaFact]
    public void When_The_Window_Ends_The_Bar_Folds_Then_The_Hook_Then_The_Event_Then_The_List()
    {
        var order = new List<string>();
        AgentMemory? card = null;
        MemoryItem? target = null;
        using var rig = new AgentMemoryRig(ArcMotionLevel.Full, m =>
        {
            card = m;
            m.CommitAsync = (change, _) =>
            {
                order.Add($"commit {change.Kind} {(m.RowOf(change.Memory)?.IsShown == true ? "shown" : "folded")}");
                return Task.CompletedTask;
            };
            m.MemoryChanged += (_, e) => order.Add($"event {e.Change.Kind} in-list:{m.Memories.Contains(e.Change.Memory)} args:{e.Memories.Count}");
        });
        var memory = rig.Memory;
        target = rig.Item("about-2");
        memory.Forget(target);
        rig.Run(1);
        rig.Advance(4.9);
        Assert.Empty(order);
        rig.Advance(0.2);
        // Folding: nothing is committed yet.
        Assert.Empty(order);
        Assert.False(memory.RowOf(target)!.IsShown);
        Assert.True(memory.RowOf(target)!.IsLeaving);
        rig.Run(1.5);
        Assert.Equal(["commit Forget folded", "event Forget in-list:True args:10"], order);
        Assert.DoesNotContain(target, memory.Memories);
        Assert.Equal("Memory forgotten", memory.LastAnnouncement);
        Assert.Null(memory.RowOf(target));
        Assert.Equal(["about-3", "about-1"], Rows(memory, "about"));
        Assert.Same(card, memory);
    }

    [AvaloniaFact]
    public void Focus_In_A_Bar_That_Times_Out_Moves_To_The_Selected_Filter()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.SelectedGroupKey = "about";
        rig.Pump();
        memory.Forget(item);
        rig.Pump();
        memory.UndoBarOf(item)!.Undo.Focus(NavigationMethod.Tab);
        rig.Advance(5.1);
        Assert.Same(memory.FilterBar!.SelectedPill, rig.Focused);
        Assert.DoesNotContain(item, memory.Memories);
        var change = Assert.Single(rig.Changes);
        Assert.Equal(MemoryChangeKind.Forget, change.Change.Kind);
        Assert.Same(item, change.Change.Memory);
    }

    [AvaloniaFact]
    public void A_Failed_Forget_Brings_The_Row_Back_And_Says_So()
    {
        using var rig = new AgentMemoryRig(configure: m => m.CommitAsync = (change, _) =>
            change.Kind == MemoryChangeKind.Forget ? throw new SilentException() : Task.CompletedTask);
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.Forget(item);
        rig.Pump();
        rig.Advance(5.1);
        Assert.Contains(item, memory.Memories);
        Assert.True(memory.RowOf(item)!.IsShown);
        Assert.Same(memory.ViewOf(item), memory.RowOf(item)!.Content);
        Assert.Equal("3", memory.GroupViewOf("about")!.CountText);
        Assert.Equal(523, memory.UsedCharacters);
        Assert.Equal("Couldn’t forget that memory. It is back in the list", memory.LastAnnouncement);
        Assert.Empty(rig.Changes);
        // It can be forgotten again.
        Assert.True(memory.Forget(item));
    }

    #endregion

    #region Captured pill

    [AvaloniaFact]
    public void The_Pill_Shows_The_First_Captured_Memory()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var pill = memory.CapturedPill!;
        Assert.True(memory.PillHost!.IsShown);
        Assert.Equal("Memory updated", pill.Title);
        Assert.Equal("Noted: you run builds with the dotnet CLI, not the IDE", pill.Note);
        Assert.Null(pill.SavedText);
        Assert.Equal("Memory updated. Noted: you run builds with the dotnet CLI, not the IDE. File under Preferences", pill.AccessibleName);
        Assert.Contains("Saved to People", pill.SizingTexts!);

        using var empty = new AgentMemoryRig(captured: false);
        Assert.False(empty.Memory.PillHost!.IsShown);
        Assert.False(empty.Memory.FileCaptured());
    }

    [AvaloniaFact]
    public void Filing_Lands_At_The_Top_Of_Its_Group_Then_The_Pill_Folds()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var captured = memory.Captured[0];
        var pill = memory.CapturedPill!;
        pill.Activate();
        rig.Pump();
        var item = captured.Memory!;
        Assert.Same(item, memory.Memories[0]);
        Assert.Equal("preferences-2", Rows(memory, "preferences")[1]);
        Assert.Equal("captured-1", Rows(memory, "preferences")[0]);
        Assert.Equal("4", memory.GroupViewOf("preferences")!.CountText);
        Assert.Equal("Saved to Preferences", pill.SavedText);
        Assert.Equal("Saved to Preferences: Runs builds with the dotnet CLI rather than the IDE", memory.LastAnnouncement);
        var change = Assert.Single(rig.Changes);
        Assert.Equal(MemoryChangeKind.Capture, change.Change.Kind);
        Assert.Same(item, change.Change.Memory);
        Assert.Equal(12, change.Memories.Count);
        Assert.Equal(574, memory.UsedCharacters);
        Assert.True(memory.PillHost!.IsShown);
        Assert.False(memory.FileCaptured());

        Assert.True(rig.Delays.Finish(TimeSpan.FromMilliseconds(1800)));
        Assert.False(memory.PillHost.IsShown);
        Assert.Null(memory.PendingCaptured);
        // The captured entry itself stays in the list it came from.
        Assert.Single(memory.Captured);
    }

    [AvaloniaFact]
    public void Filing_Switches_A_Filter_That_Hides_The_Target()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.SelectedGroupKey = "people";
        rig.Pump();
        memory.FileCaptured();
        rig.Pump();
        Assert.Equal("preferences", memory.SelectedGroupKey);
        Assert.Equal(["preferences"], Groups(memory));
        Assert.Equal("captured-1", Rows(memory, "preferences")[0]);
    }

    [AvaloniaFact]
    public void Filing_Clears_A_Search_That_Hides_The_Target_And_Keeps_One_That_Shows_It()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.SearchText = "marathon";
        rig.Pump();
        memory.FileCaptured();
        rig.Pump();
        Assert.False(memory.IsSearchOpen);
        Assert.Equal(string.Empty, memory.SearchText);
        Assert.Contains("captured-1", Rows(memory, "preferences"));

        using var second = new AgentMemoryRig();
        second.Memory.SearchText = "dotnet";
        second.Pump();
        second.Memory.FileCaptured();
        second.Pump();
        Assert.True(second.Memory.IsSearchOpen);
        Assert.Equal(["captured-1"], Rows(second.Memory, "preferences"));
    }

    [AvaloniaFact]
    public void The_List_Scrolls_To_A_Landing_Row_Out_Of_View()
    {
        using var rig = new AgentMemoryRig(captured: false, configure: m => m.Captured.Add(new CapturedMemory
        {
            Note = "Noted: Tomás now leads the routing service",
            Memory = new MemoryItem { Id = "captured-people", GroupKey = "people", Text = "Tomás Ferreira now leads the routing service", PersonName = "Tomás Ferreira" }
        }));
        var memory = rig.Memory;
        var list = memory.List!;
        Assert.Equal(0, list.Offset.Y);
        Assert.True(list.Extent.Height > list.Viewport.Height);
        memory.FileCaptured();
        rig.Pump();
        var row = memory.RowOf(memory.Captured[0].Memory!)!;
        var top = row.TranslatePoint(default, (Visual)list.Content!)!.Value.Y;
        Assert.True(list.Offset.Y > 0);
        Assert.True(top >= list.Offset.Y - 0.5);
        Assert.True(top + row.Bounds.Height <= list.Offset.Y + list.Viewport.Height + 0.5);
    }

    [AvaloniaFact]
    public void A_Failed_Filing_Keeps_The_Pill()
    {
        using var rig = new AgentMemoryRig(configure: m => m.CommitAsync = (_, _) => throw new InvalidOperationException("Try later"));
        var memory = rig.Memory;
        Assert.True(memory.FileCaptured());
        rig.Pump();
        Assert.Equal("Try later", memory.LastAnnouncement);
        Assert.Equal(11, memory.Memories.Count);
        Assert.Null(memory.CapturedPill!.SavedText);
        Assert.True(memory.PillHost!.IsShown);
        Assert.Empty(rig.Changes);
        // It can be tried again.
        memory.CommitAsync = null;
        Assert.True(memory.FileCaptured());
    }

    [AvaloniaFact]
    public void A_Pill_With_The_Keyboard_Focus_Hands_It_To_The_New_Rows_Edit_Button()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var pill = memory.CapturedPill!;
        pill.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.Equal("Saved to Preferences", pill.SavedText);
        Assert.Same(pill, rig.Focused);
        rig.Delays.Finish(TimeSpan.FromMilliseconds(1800));
        rig.Pump();
        var item = memory.Captured[0].Memory!;
        Assert.Same(memory.ViewOf(item)!.EditButton, rig.Focused);
        Assert.True(Tooltip.GetIsOpen(memory.ViewOf(item)!.EditButton!));
    }

    [AvaloniaFact]
    public void The_Next_Captured_Memory_Follows_After_The_Hold()
    {
        using var rig = new AgentMemoryRig(configure: m => m.Captured.Add(new CapturedMemory
        {
            Note = "Noted: Ingrid moved the one-to-one to Wednesdays",
            Memory = new MemoryItem { Id = "captured-2", GroupKey = "people", Text = "Meets Ingrid on Wednesdays now", PersonName = "Ingrid Holm" }
        }));
        var memory = rig.Memory;
        memory.FileCaptured();
        rig.Pump();
        rig.Delays.Finish(TimeSpan.FromMilliseconds(1800));
        Assert.True(memory.PillHost!.IsShown);
        Assert.Null(memory.CapturedPill!.SavedText);
        Assert.Equal("Noted: Ingrid moved the one-to-one to Wednesdays", memory.CapturedPill.Note);
        Assert.True(memory.FileCaptured());
    }

    [AvaloniaFact]
    public void Paused_Saved_Memories_Swap_The_Pill_For_The_Notice()
    {
        using var rig = new AgentMemoryRig(captured: false);
        var memory = rig.Memory;
        Assert.False(memory.PillHost!.IsShown);
        memory.IsSavedMemoriesEnabled = false;
        rig.Pump();
        Assert.True(memory.PillHost.IsShown);
        Assert.True(memory.CapturedPill!.IsPaused);
        Assert.Equal("Saved memories are paused. Orbit will not note or use new ones until you turn them back on.", memory.CapturedPill.PausedText);
        Assert.Contains(":paused", memory.Classes);
        Assert.Equal(0.5, memory.ListHost!.Opacity, 3);
        Assert.False(memory.FileCaptured());
        // The list stays editable.
        Assert.True(memory.BeginEdit(rig.Item("about-1")));
        Assert.True(memory.Forget(rig.Item("about-2")));
    }

    #endregion

    #region Adding

    [AvaloniaFact]
    public void Adding_Sorts_Then_Lands_At_The_Top_Of_The_Group_And_Says_Where()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var field = memory.AddField!;
        field.Input!.Focus();
        rig.Type("Mentors two junior engineers on the team");
        Assert.Equal(AgentMemoryAddStatus.Ready, field.Status);
        Assert.NotNull(field.AddButton);
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(AgentMemoryAddStatus.Sorting, field.Status);
        Assert.Equal("Sorting into a group", field.StatusText);
        Assert.True(field.IsBusy);
        Assert.True(field.Input.IsReadOnly);
        Assert.Equal([TimeSpan.FromMilliseconds(650)], rig.Delays.Pending);

        rig.Delays.Finish(TimeSpan.FromMilliseconds(650));
        rig.Pump();
        var added = memory.Memories[0];
        Assert.Equal("Mentors two junior engineers on the team", added.Text);
        Assert.Equal("about", added.GroupKey);
        Assert.Equal(MemorySourceKind.Manual, added.SourceKind);
        Assert.Equal(rig.Time.Now, added.SourceDate);
        Assert.False(string.IsNullOrEmpty(added.Id));
        Assert.Equal(added.Id, Rows(memory, "about")[0]);
        Assert.Equal(string.Empty, field.Text);
        Assert.False(field.IsBusy);
        Assert.Equal(AgentMemoryAddStatus.Added, field.Status);
        Assert.Equal("Added to About you", field.StatusText);
        Assert.Equal("Added to About you: Mentors two junior engineers on the team", memory.LastAnnouncement);
        Assert.Same(field.Input, rig.Focused);
        var change = Assert.Single(rig.Changes).Change;
        Assert.Equal(MemoryChangeKind.Add, change.Kind);
        Assert.Same(added, change.Memory);

        rig.Delays.Finish(TimeSpan.FromMilliseconds(2000));
        Assert.Equal(AgentMemoryAddStatus.None, field.Status);
    }

    [AvaloniaFact]
    public void A_Custom_Classifier_Decides_And_An_Unknown_Key_Lands_In_The_First_Group()
    {
        string? asked = null;
        using var rig = new AgentMemoryRig(configure: m => m.ClassifyAsync = (text, _) =>
        {
            asked = text;
            return ValueTask.FromResult(text.Contains("lunch") ? "nowhere" : "projects");
        });
        var memory = rig.Memory;
        var field = memory.AddField!;
        field.Text = "  Ships the routing beta next week  ";
        rig.Pump();
        rig.Memory.AddField!.Input!.Focus();
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        Assert.Equal("Ships the routing beta next week", asked);
        Assert.Equal("projects", memory.Memories[0].GroupKey);
        Assert.Equal("Added to Projects", field.StatusText);
        Assert.DoesNotContain(TimeSpan.FromMilliseconds(650), rig.Delays.Pending);

        field.Text = "Eats lunch at noon";
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        Assert.Equal("about", memory.Memories[0].GroupKey);
    }

    [AvaloniaFact]
    public void Adding_Past_The_Capacity_Is_Refused_And_Keeps_The_Text()
    {
        using var rig = new AgentMemoryRig(configure: m => m.Capacity = 540);
        var memory = rig.Memory;
        var field = memory.AddField!;
        field.Input!.Focus();
        rig.Type("Mentors two junior engineers on the team");
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(AgentMemoryAddStatus.Alert, field.Status);
        Assert.Equal("Memory is full", field.StatusText);
        Assert.Equal("Memory is full. Forget something to add more", memory.LastAnnouncement);
        Assert.Equal("Mentors two junior engineers on the team", field.Text);
        Assert.Equal(11, memory.Memories.Count);
        Assert.Empty(rig.Delays.Pending);

        // Forgetting makes room (the forgotten one no longer counts).
        memory.Forget(rig.Item("about-1"));
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(AgentMemoryAddStatus.Sorting, field.Status);
    }

    [AvaloniaFact]
    public void A_Long_Memory_Shows_The_Live_Count_And_Is_Refused()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var field = memory.AddField!;
        field.Text = new string('a', 201);
        rig.Pump();
        Assert.Equal(AgentMemoryAddStatus.Alert, field.Status);
        Assert.Equal("201 of 200 characters", field.StatusText);
        var line = field.StatusContent;
        field.Text = new string('a', 202);
        rig.Pump();
        // The count changes its digits in place.
        Assert.Same(line, field.StatusContent);
        Assert.Equal("202 of 200 characters", field.StatusText);
        field.Input!.Focus();
        rig.Key(PhysicalKey.Enter);
        Assert.Equal("Keep it under 200 characters", field.StatusText);
        Assert.Empty(rig.Delays.Pending);
        field.Text = "Short again";
        rig.Pump();
        Assert.Equal(AgentMemoryAddStatus.Ready, field.Status);
    }

    [AvaloniaFact]
    public void A_Failed_Sort_Or_Save_Keeps_The_Text_And_Says_Why()
    {
        using var rig = new AgentMemoryRig(configure: m => m.ClassifyAsync = (_, _) => throw new InvalidOperationException("model down"));
        var memory = rig.Memory;
        var field = memory.AddField!;
        field.Text = "Mentors two junior engineers";
        field.Input!.Focus();
        rig.Key(PhysicalKey.Enter);
        Assert.Equal("Couldn’t sort. Try again", field.StatusText);
        Assert.Equal("Mentors two junior engineers", field.Text);
        Assert.False(field.IsBusy);

        memory.ClassifyAsync = null;
        memory.CommitAsync = (_, _) => throw new InvalidOperationException("No space on the server");
        rig.Key(PhysicalKey.Enter);
        rig.Delays.Finish(TimeSpan.FromMilliseconds(650));
        rig.Pump();
        Assert.Equal("No space on the server", field.StatusText);
        Assert.Equal("Mentors two junior engineers", field.Text);
        Assert.Equal(11, memory.Memories.Count);
        Assert.Empty(rig.Changes);
    }

    [AvaloniaFact]
    public void Adding_Switches_The_Filter_To_The_Target_Group_And_Closes_A_Hiding_Search()
    {
        using var rig = new AgentMemoryRig(configure: m => m.ClassifyAsync = (_, _) => ValueTask.FromResult("people"));
        var memory = rig.Memory;
        memory.SelectedGroupKey = "projects";
        rig.Pump();
        var field = memory.AddField!;
        field.Text = "Lunches with Ana Lima on Fridays";
        field.Input!.Focus();
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        Assert.Equal("people", memory.SelectedGroupKey);
        Assert.Equal(["people"], Groups(memory));

        memory.SearchText = "marathon";
        rig.Pump();
        field.Text = "Plays chess with Ana Lima";
        field.Input.Focus();
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        Assert.False(memory.IsSearchOpen);
    }

    [AvaloniaFact]
    public void Enter_On_An_Empty_Field_Does_Nothing_And_The_Field_Can_Be_Hidden()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var field = memory.AddField!;
        field.Input!.Focus();
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(AgentMemoryAddStatus.None, field.Status);
        Assert.Empty(rig.Delays.Pending);
        Assert.True(field.IsEffectivelyVisible);
        memory.IsAddEnabled = false;
        rig.Pump();
        Assert.False(field.IsEffectivelyVisible);
    }

    #endregion

    #region Switches

    [AvaloniaFact]
    public void The_Switches_Report_The_Persons_Toggles_And_The_List_Dims()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var saved = memory.SavedSettingRow!;
        Assert.Equal("Reference saved memories", saved.Title);
        Assert.Equal("Orbit uses these memories to personalize replies", saved.Explanation);
        Assert.Equal("Reference saved memories", AutomationProperties.GetName(saved.Switch!));
        saved.Switch!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Space);
        Assert.False(memory.IsSavedMemoriesEnabled);
        var toggle = Assert.Single(rig.Toggles);
        Assert.Equal(MemorySetting.SavedMemories, toggle.Setting);
        Assert.False(toggle.IsOn);
        Assert.Equal("Saved memories paused", memory.LastAnnouncement);
        Assert.Equal("Paused. Orbit does not use saved memories", saved.Explanation);
        Assert.True(memory.CapturedPill!.IsPaused);
        Assert.Equal(0.5, memory.ListHost!.Opacity, 3);

        var history = memory.HistorySettingRow!;
        history.Switch!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Space);
        Assert.False(memory.IsChatHistoryEnabled);
        Assert.Equal("Orbit starts every chat fresh", history.Explanation);
        Assert.Equal("Chat history off", memory.LastAnnouncement);
        Assert.Equal(2, rig.Toggles.Count);

        // From code: the switch follows without an event.
        memory.IsSavedMemoriesEnabled = true;
        rig.Pump();
        Assert.True(saved.Switch.IsChecked);
        Assert.Equal(2, rig.Toggles.Count);
        Assert.Equal(1, memory.ListHost.Opacity, 3);
    }

    #endregion

    #region Sources, lists and item changes

    [AvaloniaFact]
    public void A_Source_With_A_Link_Raises_The_Event_And_Runs_The_Command()
    {
        var command = new RecordingCommand();
        MemoryItem? requested = null;
        using var rig = new AgentMemoryRig(configure: m =>
        {
            m.Memories[0].SourceUri = new Uri("https://example.com/chats/42");
            m.SourceCommand = command;
            m.SourceRequested += (_, e) => requested = e.Memory;
        });
        var memory = rig.Memory;
        var item = memory.Memories[0];
        var view = memory.ViewOf(item)!;
        Assert.True(view.IsSourceLink);
        Assert.True(view.SourceButton!.IsVisible);
        Click(view.SourceButton);
        Assert.Same(item, requested);
        Assert.Same(item, command.Parameter);

        // A source without a link is plain text.
        var plain = memory.ViewOf(memory.Memories[1])!;
        Assert.False(plain.IsSourceLink);
        Assert.Equal("From a chat", plain.SourceText);
        Assert.Equal("Oct 17", plain.DateText);
        Assert.Equal("Added by you", view.SourceText);
    }

    private sealed class RecordingCommand : ICommand
    {
        public object? Parameter { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameter = parameter;
    }

    [AvaloniaFact]
    public void An_Outside_List_Wins_And_Is_Followed()
    {
        var list = new ObservableCollection<MemoryItem>(AgentMemorySamples.Memories().Take(3));
        using var rig = new AgentMemoryRig(configure: m => m.MemoriesSource = list);
        var memory = rig.Memory;
        Assert.Equal(["projects", "people"], Groups(memory));
        list.Add(new MemoryItem { Id = "x", GroupKey = "about", Text = "Lives by the river" });
        rig.Pump();
        Assert.Equal(["about", "projects", "people"], Groups(memory));
        list.RemoveAt(0);
        rig.Pump();
        Assert.Equal(["about", "projects"], Groups(memory));

        // Changes made by the card go to the outside list.
        memory.Forget(list[0]);
        rig.Pump();
        rig.Advance(5.1);
        Assert.Equal(2, list.Count);
        Assert.Equal(11, memory.Memories.Count);

        memory.MemoriesSource = null;
        rig.Pump();
        Assert.Equal(4, Groups(memory).Length);
    }

    [AvaloniaFact]
    public void Changing_A_Memory_Moves_Its_Row_And_Updates_The_Meter()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        item.Text = "Runs";
        rig.Pump();
        Assert.Equal("Runs", memory.ViewOf(item)!.Text);
        Assert.Equal(523 - 38 + 4, memory.UsedCharacters);
        Assert.Equal(523 - 38 + 4, memory.Meter!.Used);
        item.GroupKey = "people";
        rig.Pump();
        Assert.Equal(["about-3", "about-1"], Rows(memory, "about"));
        Assert.Contains("about-2", Rows(memory, "people"));
        Assert.Equal("4", memory.GroupViewOf("people")!.CountText);
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.Labels = new AgentMemoryLabels
        {
            Title = "Gedächtnis",
            FilterAll = "Alle",
            GroupPeople = "Leute",
            Edit = "Bearbeiten",
            SourceChat = "Aus einem Chat"
        };
        rig.Pump();
        Assert.Equal("Gedächtnis", memory.TitleText!.Text);
        Assert.Equal("Alle", memory.FilterBar!.Options![0].Label);
        Assert.Equal("Leute", memory.GroupViewOf("people")!.Label);
        Assert.Equal("Bearbeiten", memory.ViewOf(rig.Item("people-1"))!.EditTip);
        Assert.Equal("Aus einem Chat", memory.ViewOf(rig.Item("people-1"))!.SourceText);
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void Full_Motion_Settles_With_The_Same_Heights_As_Off()
    {
        using var off = new AgentMemoryRig();
        Script(off);
        using var full = new AgentMemoryRig(ArcMotionLevel.Full);
        Script(full);
        full.Run(3);
        AssertSettled(full.Memory);
        Assert.Equal(Heights(off.Memory), Heights(full.Memory));

        static void Script(AgentMemoryRig rig)
        {
            rig.Memory.BeginEdit(rig.Item("about-2"));
            rig.Memory.Forget(rig.Item("about-3"));
            rig.Memory.SelectedGroupKey = "about";
            rig.Pump();
            rig.Memory.SelectedGroupKey = null;
            rig.Pump();
            rig.Memory.FileCaptured();
            rig.Pump();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Leaves_No_Spring_Running_And_Keeps_The_Crossfade_Short()
    {
        using var rig = new AgentMemoryRig(ArcMotionLevel.Reduced);
        var memory = rig.Memory;
        var item = rig.Item("about-2");
        memory.Forget(item);
        rig.Pump();
        var row = memory.RowOf(item)!;
        // No height motion: the row's height is already the bar's.
        Assert.False(row.SizeHost.IsMoving);
        memory.SelectedGroupKey = "people";
        rig.Pump();
        foreach (var view in memory.ShownGroups)
        {
            Assert.Equal(1, view.Host!.HeightFraction, 3);
        }
        rig.Run(0.2);
        AssertSettled(memory);
    }

    [AvaloniaFact]
    public void A_Filed_Memory_Lands_With_A_Tint_That_Holds_Then_Fades()
    {
        using var rig = new AgentMemoryRig(ArcMotionLevel.Full);
        var memory = rig.Memory;
        memory.FileCaptured();
        rig.Pump();
        var row = memory.RowOf(memory.Captured[0].Memory!)!;
        rig.Run(0.3);
        Assert.Equal(AgentMemoryRow.ArrivalAlpha, row.ArrivalOpacity, 3);
        Assert.True(row.HeightFraction > 0.5);
        rig.Run(1.4);
        Assert.Equal(0, row.ArrivalOpacity, 3);
        Assert.Equal(1, row.HeightFraction, 3);
    }

    [AvaloniaFact]
    public void Off_Shows_No_Tint_And_Long_Lists_Skip_The_Row_Height_Motion()
    {
        using var rig = new AgentMemoryRig();
        rig.Memory.FileCaptured();
        rig.Pump();
        Assert.Equal(0, rig.Memory.RowOf(rig.Memory.Captured[0].Memory!)!.ArrivalOpacity);

        using var big = new AgentMemoryRig(ArcMotionLevel.Full, m =>
        {
            for (var i = 0; i < AgentMemory.RowMotionLimit; i++)
            {
                m.Memories.Add(new MemoryItem { Id = "n" + i, GroupKey = "projects", Text = "Note " + i });
            }
        });
        big.Memory.SelectedGroupKey = "about";
        big.Pump();
        big.Memory.SelectedGroupKey = null;
        big.Pump();
        var row = big.Memory.RowOf(big.Item("n0"))!;
        Assert.False(row.AnimatesHeight);
        Assert.Equal(1, row.HeightFraction, 3);
    }

    private static double[] Heights(AgentMemory memory) =>
        memory.ShownGroups.SelectMany(g => g.Rows.Children.OfType<AgentMemoryRow>().Where(r => r.IsShown)).Select(r => Math.Round(r.Bounds.Height, 1)).ToArray();

    private static void AssertSettled(AgentMemory memory)
    {
        foreach (var view in memory.ShownGroups)
        {
            Assert.False(view.Host!.IsMoving, $"group {view.Key}");
            foreach (var row in view.Rows.Children.OfType<AgentMemoryRow>())
            {
                Assert.False(row.IsMoving, $"row {row.Memory.Id} ({row.HeightFraction}, {row.Opacity})");
                Assert.False(row.SizeHost.IsMoving, $"row height {row.Memory.Id}");
            }
        }
        Assert.False(memory.PillHost!.IsMoving, "pill");
    }

    #endregion

    #region Keyboard and automation

    [AvaloniaFact]
    public void Tab_Goes_Switches_Selected_Pill_Search_Pill_Then_Each_Rows_Edit_And_Forget()
    {
        using var rig = new AgentMemoryRig();
        var memory = rig.Memory;
        memory.SavedSettingRow!.Switch!.Focus(NavigationMethod.Tab);
        var expected = new Control?[]
        {
            memory.HistorySettingRow!.Switch,
            memory.FilterBar!.SelectedPill,
            memory.SearchBox!.ToggleButton,
            memory.CapturedPill,
            memory.ViewOf(rig.Item("about-3"))!.EditButton,
            memory.ViewOf(rig.Item("about-3"))!.ForgetButton,
            memory.ViewOf(rig.Item("about-2"))!.EditButton
        };
        foreach (var control in expected)
        {
            rig.Key(PhysicalKey.Tab);
            Assert.Same(control, rig.Focused);
        }
    }

    [AvaloniaFact]
    public void Row_Buttons_Are_Named_After_The_Sentence_And_Show_Their_Tooltip_On_Keyboard_Focus()
    {
        using var rig = new AgentMemoryRig();
        var view = rig.Memory.ViewOf(rig.Item("about-2"))!;
        Assert.Equal("Edit memory: Is training for a spring half marathon", AutomationProperties.GetName(view.EditButton!));
        Assert.Equal("Forget memory: Is training for a spring half marathon", AutomationProperties.GetName(view.ForgetButton!));
        Assert.Equal("Edit", Tooltip.GetTip(view.EditButton!));
        view.EditButton!.Focus(NavigationMethod.Tab);
        rig.Pump();
        Assert.True(Tooltip.GetIsOpen(view.EditButton));
        Assert.Contains(":focus-within", view.Classes);
        view.ForgetButton!.Focus(NavigationMethod.Tab);
        rig.Pump();
        Assert.False(Tooltip.GetIsOpen(view.EditButton));
    }

    [AvaloniaFact]
    public void Touch_Reveals_The_Row_Actions_At_Rest()
    {
        using var rig = new AgentMemoryRig();
        var view = rig.Memory.ViewOf(rig.Item("about-2"))!;
        Assert.False(view.IsRevealed);
        var touch = new Avalonia.Input.Pointer(1, PointerType.Touch, true);
        var point = view.TranslatePoint(new Point(20, 10), rig.Window)!.Value;
        var args = new PointerPressedEventArgs(view, touch, rig.Window, point, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None);
        view.RaiseEvent(args);
        Assert.True(view.IsRevealed);
        Assert.Contains(":reveal", view.Classes);
    }

    [AvaloniaFact]
    public void The_Group_Headings_Are_Headings_And_The_Groups_Named()
    {
        using var rig = new AgentMemoryRig();
        var group = rig.Memory.GroupViewOf("about")!;
        Assert.Equal("About you", AutomationProperties.GetName(group));
        var heading = TestHelpers.Descendant<StackPanel>(group, "PART_Heading");
        Assert.Equal(3, AutomationProperties.GetHeadingLevel(heading));
        Assert.Equal("Filter memories", rig.Memory.FilterBar!.AccessibleName);
        Assert.Equal("Search memories", AutomationProperties.GetName(rig.Memory.SearchBox!.ToggleButton!));
    }

    #endregion
}
