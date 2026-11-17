using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The "/" and "@" list: rows, groups, the active row, scrolling, pointer, accept and accessibility.</summary>
public class AiSuggestionPopTests
{
    [AvaloniaFact]
    public void Commands_Show_With_A_Slash_Over_Their_Description_And_The_First_Row_Active()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows()).ShouldBeTrue();
        host.Pop.Kind.ShouldBe(AiSuggestionKind.Command);
        host.Pop.Rows.Count.ShouldBe(4);
        host.Pop.ActiveIndex.ShouldBe(0);
        host.Pop.ActiveRow!.Id.ShouldBe("recap");
        host.List.RowViews.Take(4).Select(v => v.DisplayTitle).ShouldBe(["/recap", "/rewrite", "/outline", "/debug"]);
        host.List.RowViews[0].IsActive.ShouldBeTrue();
        host.List.RowViews.Skip(1).Take(3).ShouldAllBe(v => !v.IsActive);
        // Commands never show headings, even when their rows carry a group.
        host.List.Entries.ShouldAllBe(e => !e.IsHeading);
        host.Pop.Show(AiSuggestionKind.Command, AiSuggestionSamples.ManyRows(3));
        host.Pump();
        host.List.Entries.ShouldAllBe(e => !e.IsHeading);
    }

    [AvaloniaFact]
    public void The_Surface_Is_300_Wide_With_Two_Line_Rows_Of_46_4_In_Padding_5()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        host.Pop.Bounds.Width.ShouldBe(300, 0.01);
        // 4 × (5 + 19.6 + 16.8 + 5) + 2 × 5 padding + 2 × 1 border (laid out on whole pixels).
        host.Pop.Bounds.Height.ShouldBe(4 * 46.4 + 12, 0.5);
        host.Pop.NaturalHeight.ShouldBe(4 * 46.4 + 12, 0.01);
        var first = host.BoundsOf(host.RowView(0));
        var surface = host.BoundsOf(host.Pop);
        first.X.ShouldBe(surface.X + 6, 0.01);
        first.Y.ShouldBe(surface.Y + 6, 0.01);
        first.Width.ShouldBe(288, 0.01);
        first.Height.ShouldBe(46.4, 1);
        host.Pop.CornerRadius.ShouldBe(new CornerRadius(20));
        host.Pop.BorderThickness.ShouldBe(new Thickness(1));
    }

    [AvaloniaFact]
    public void The_Natural_Height_Is_Known_Before_The_List_Was_Ever_Shown()
    {
        var pop = new AiSuggestionPop();
        pop.Show(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("export")).ShouldBeTrue();
        // Two headings, two rows, padding and border; a long list stops at 280.
        pop.NaturalHeight.ShouldBe(2 * 26.8 + 2 * 46.4 + 12, 0.001);
        pop.Show(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        pop.NaturalHeight.ShouldBe(280);
        pop.Show(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        pop.NaturalHeight.ShouldBe(4 * 46.4 + 12, 0.001);
    }

    [AvaloniaFact]
    public void A_Row_Without_A_Hint_Is_44_High()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, [new AiSuggestionRow("x", "explain", null, ArcIcons.Lightbulb, null)]);
        host.RowView(0).Bounds.Height.ShouldBe(44, 0.01);
        host.Pop.Bounds.Height.ShouldBe(44 + 12, 0.01);
    }

    [AvaloniaFact]
    public void Mentions_Are_Grouped_Under_Headings_And_A_Long_List_Scrolls_Inside_280()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        var entries = host.List.Entries;
        entries.Select(e => e.IsHeading ? "#" + e.Heading : host.Pop.Rows[e.RowIndex].Title).ShouldBe(
            ["#Files", "export-worker.ts", "usage-oct.csv", "style-guide.pdf", "#Docs", "Sprint 15 review", "Export timeouts", "Onboarding flow v2"]);
        // Headings: 6 + 16.8 + 4.
        entries[0].Height.ShouldBe(26.8, 0.01);
        entries[1].Y.ShouldBe(26.8, 0.01);
        entries[4].Y.ShouldBe(26.8 + 3 * 46.4, 0.01);
        host.List.HeadingViews.Where(h => h.IsVisible).Select(h => h.Text).ShouldBe(["Files", "Docs"]);
        // 2 × 26.8 + 6 × 46.4 + 12 is taller than 280: the surface stops at 280 and the rows scroll.
        host.Pop.Bounds.Height.ShouldBe(280, 0.01);
        host.Scroll.Extent.Height.ShouldBeGreaterThan(host.Scroll.Viewport.Height + 60);
        host.Scroll.Viewport.Height.ShouldBe(278, 0.01);
    }

    [AvaloniaFact]
    public void Rows_Without_A_Known_Group_Follow_Without_A_Heading()
    {
        using var host = AiSuggestionHost.Show();
        AiSuggestionRow[] rows =
        [
            new("a", "main.ts", "src", null, "Files"),
            new("b", "scratch.txt", null, null, null),
            new("c", "loose.txt", null, null, null)
        ];
        host.Open(AiSuggestionKind.Mention, rows);
        host.List.Entries.Count(e => e.IsHeading).ShouldBe(1);
        host.List.Entries[0].Heading.ShouldBe("Files");
        // No icons anywhere: the text starts at the row padding.
        host.List.HasIcons.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Active_Row_Moves_With_Wrapping_At_Both_Ends()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        var changes = 0;
        host.Pop.ActiveChanged += (_, _) => changes++;
        host.Pop.MoveActive(1).ShouldBeTrue();
        host.Pop.ActiveIndex.ShouldBe(1);
        host.List.RowViews[1].IsActive.ShouldBeTrue();
        host.List.RowViews[0].IsActive.ShouldBeFalse();
        host.Pop.MoveActive(1);
        host.Pop.MoveActive(1);
        host.Pop.ActiveIndex.ShouldBe(3);
        host.Pop.MoveActive(1);
        host.Pop.ActiveIndex.ShouldBe(0);
        host.Pop.MoveActive(-1);
        host.Pop.ActiveIndex.ShouldBe(3);
        host.Pop.MoveActive(-6);
        host.Pop.ActiveIndex.ShouldBe(1);
        changes.ShouldBe(6);
        host.Pop.ActiveRowName.ShouldBe("/rewrite, Rewrite in a clearer or friendlier tone");
    }

    [AvaloniaFact]
    public void Keys_Routed_By_The_Owner_Move_And_Accept()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        host.Pop.TryHandleKey(Key.Down, KeyModifiers.None).ShouldBeTrue();
        host.Pop.TryHandleKey(Key.Down, KeyModifiers.None).ShouldBeTrue();
        host.Pop.TryHandleKey(Key.Up, KeyModifiers.None).ShouldBeTrue();
        host.Pop.ActiveIndex.ShouldBe(1);
        // Modified keys, Escape, Home and End stay with the owner.
        host.Pop.TryHandleKey(Key.Tab, KeyModifiers.Shift).ShouldBeFalse();
        host.Pop.TryHandleKey(Key.Enter, KeyModifiers.Shift).ShouldBeFalse();
        host.Pop.TryHandleKey(Key.Escape, KeyModifiers.None).ShouldBeFalse();
        host.Pop.TryHandleKey(Key.Home, KeyModifiers.None).ShouldBeFalse();
        host.Accepted.ShouldBeEmpty();
        host.Pop.TryHandleKey(Key.Enter, KeyModifiers.None).ShouldBeTrue();
        host.Pop.TryHandleKey(Key.Tab, KeyModifiers.None).ShouldBeTrue();
        host.Accepted.Count.ShouldBe(2);
        host.Accepted[0].Row.Id.ShouldBe("rewrite");
        host.Accepted[0].Index.ShouldBe(1);
        host.Accepted[0].Kind.ShouldBe(AiSuggestionKind.Command);
        host.Accepted[0].RoutedEvent.ShouldBe(AiSuggestionPop.AcceptedEvent);
    }

    [AvaloniaFact]
    public void Moving_Through_Twenty_Rows_Keeps_The_Active_Row_In_View()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.ManyRows(20));
        var viewport = host.Scroll.Viewport.Height;
        host.Scroll.Offset.Y.ShouldBe(0);
        for (var i = 1; i < 20; i++)
        {
            host.Pop.MoveActive(1);
            host.Pump(1);
            var rect = host.InViewport(i);
            rect.Y.ShouldBeGreaterThanOrEqualTo(-0.01, $"row {i}");
            rect.Bottom.ShouldBeLessThanOrEqualTo(viewport + 0.01, $"row {i}");
        }
        // The last row brings the list's bottom padding along: the list is scrolled to its end.
        host.Scroll.Offset.Y.ShouldBe(host.Scroll.Extent.Height - viewport, 0.01);
        // Wrapping to the first row scrolls back to the top, its heading and the padding in view.
        host.Pop.MoveActive(1);
        host.Pump(1);
        host.Pop.ActiveIndex.ShouldBe(0);
        host.Scroll.Offset.Y.ShouldBe(0);
        // And up from the first row to the last scrolls to the end in one go.
        host.Pop.MoveActive(-1);
        host.Pump(1);
        host.Scroll.Offset.Y.ShouldBe(host.Scroll.Extent.Height - viewport, 0.01);
    }

    [AvaloniaFact]
    public void Scrolling_Up_To_The_First_Row_Of_A_Group_Brings_Its_Heading()
    {
        using var host = AiSuggestionHost.Show();
        var rows = AiSuggestionSamples.ManyRows(6, "Files").Concat(AiSuggestionSamples.ManyRows(6, "Docs").Select(r => r with { Id = "d" + r.Id })).ToArray();
        host.Open(AiSuggestionKind.Mention, rows);
        host.Pop.MoveActive(-1);
        host.Pump(1);
        host.Pop.ActiveIndex.ShouldBe(11);
        // Up to the first "Docs" row (index 6): its heading is in view too.
        for (var i = 0; i < 5; i++)
        {
            host.Pop.MoveActive(-1);
        }
        host.Pump(1);
        host.Pop.ActiveIndex.ShouldBe(6);
        var heading = host.List.Entries.First(e => e.IsHeading && e.Heading == "Docs");
        var headingView = host.List.HeadingViews.First(h => h.Text == "Docs");
        headingView.TranslatePoint(default, host.Scroll)!.Value.Y.ShouldBeGreaterThanOrEqualTo(-0.01);
        heading.Height.ShouldBe(26.8, 0.01);
    }

    [AvaloniaFact]
    public void Hovering_A_Row_Makes_It_Active_Without_Scrolling()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.ManyRows(12));
        var offset = host.Scroll.Offset;
        host.Move(host.RowView(2));
        host.Pop.ActiveIndex.ShouldBe(2);
        host.List.RowViews[2].IsActive.ShouldBeTrue();
        // The bottom row is only partly visible: hovering it does not scroll the list under the pointer.
        var viewport = host.BoundsOf(host.Scroll);
        var partial = Enumerable.Range(0, 12).First(i => host.BoundsOf(host.RowView(i)).Bottom > viewport.Bottom);
        host.Move(new Point(viewport.X + 100, viewport.Bottom - 3));
        host.Pop.ActiveIndex.ShouldBe(partial);
        host.Scroll.Offset.ShouldBe(offset);
        // Out-of-range indexes are ignored.
        host.Pop.SetActive(40);
        host.Pop.ActiveIndex.ShouldBe(partial);
    }

    [AvaloniaFact]
    public void Clicking_A_Row_Accepts_It_And_The_Prompt_Keeps_Focus()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        host.Focused.ShouldBe(host.Box);
        host.Click(host.RowView(1));
        host.Accepted.Count.ShouldBe(1);
        host.Accepted[0].Row.Title.ShouldBe("usage-oct.csv");
        host.Accepted[0].Kind.ShouldBe(AiSuggestionKind.Mention);
        host.Pop.ActiveIndex.ShouldBe(1);
        host.Focused.ShouldBe(host.Box);
        // Pressing a heading or the padding neither accepts nor moves focus.
        var heading = host.List.HeadingViews[0];
        host.Click(host.Center(heading));
        host.Accepted.Count.ShouldBe(1);
        host.Focused.ShouldBe(host.Box);
    }

    [AvaloniaFact]
    public void AcceptActive_Raises_Accepted_For_The_Active_Row_Only_When_There_Is_One()
    {
        using var host = AiSuggestionHost.Show();
        host.Pop.AcceptActive().ShouldBeFalse();
        host.Pop.MoveActive(1).ShouldBeFalse();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("timeouts"));
        host.Pop.AcceptActive().ShouldBeTrue();
        host.Accepted.Single().Row.ShouldBe(new AiSuggestionRow("d-timeouts", "Export timeouts", "Tracker, OPS-318",
            AiSuggestionSamples.Mentions()[4].Icon, "Docs"), new RowComparer());
    }

    [AvaloniaFact]
    public void No_Rows_Close_The_List()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows()).ShouldBeTrue();
        host.Popup.IsOpen.ShouldBeTrue();
        var closed = 0;
        host.Popup.Closed += (_, _) => closed++;
        host.Pop.Show(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows("zz")).ShouldBeFalse();
        host.Pump();
        host.Popup.IsOpen.ShouldBeFalse();
        host.Popup.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        host.Pop.IsEmpty.ShouldBeTrue();
        host.Pop.ActiveIndex.ShouldBe(-1);
        host.Pop.ActiveRow.ShouldBeNull();
        host.Pop.AcceptActive().ShouldBeFalse();
        // Opening without rows does nothing.
        host.Popup.Open(host.Anchor, AiSuggestionHost.LineStart).ShouldBeFalse();
        host.Popup.Popup.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Same_Rows_Keep_The_Active_Row_And_New_Rows_Start_At_The_First()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        host.Pop.MoveActive(2);
        // The owner filters again on every keystroke: equal rows (new instances) change nothing.
        host.Pop.Show(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows()).ShouldBeTrue();
        host.Pump();
        host.Pop.ActiveIndex.ShouldBe(2);
        // Different rows: the first match is active again and the list starts at the top.
        host.Pop.MoveActive(3);
        host.Pump();
        host.Scroll.Offset.Y.ShouldBeGreaterThan(0);
        host.Pop.Show(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("s"));
        host.Pump();
        host.Pop.ActiveIndex.ShouldBe(0);
        host.Scroll.Offset.Y.ShouldBe(0);
        // The same rows as commands are a different list.
        host.Pop.Show(AiSuggestionKind.Command, host.Pop.Rows);
        host.Pump();
        host.Pop.Kind.ShouldBe(AiSuggestionKind.Command);
        host.RowView(0).DisplayTitle.ShouldStartWith("/");
    }

    [AvaloniaFact]
    public void The_Number_Of_Rows_Is_Announced_When_It_Opens_And_When_It_Changes()
    {
        using var host = AiSuggestionHost.Show();
        var said = new List<int>();
        host.Pop.AnnounceCount = n =>
        {
            said.Add(n);
            return $"{n} results";
        };
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        said.ShouldBe([6]);
        host.Pop.LastAnnouncement.ShouldBe("6 results");
        // Narrowing to another list of the same length says nothing new.
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("e"));
        host.Pop.Rows.Count.ShouldBe(6);
        said.ShouldBe([6]);
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("export"));
        said.ShouldBe([6, 2]);
        // Closed and opened again: announced again.
        host.Popup.Close();
        host.Pump();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("export"));
        said.ShouldBe([6, 2, 2]);
        // No match then a match: announced again.
        host.Pop.Show(AiSuggestionKind.Mention, []);
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("export"));
        said.ShouldBe([6, 2, 2, 2]);
    }

    [AvaloniaFact]
    public void Assistive_Technology_Sees_A_Named_List_Of_Items_With_The_Active_One_Selected()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        var list = ControlAutomationPeer.CreatePeerForElement(host.List);
        list.GetAutomationControlType().ShouldBe(AutomationControlType.List);
        list.GetName().ShouldBe("Files and docs");
        var items = list.GetChildren().Where(p => p.GetAutomationControlType() == AutomationControlType.ListItem).ToList();
        items.Count.ShouldBe(6);
        items[0].GetName().ShouldBe("export-worker.ts, services/export");
        ((ISelectionItemProvider)items[0]).IsSelected.ShouldBeTrue();
        ((ISelectionItemProvider)items[1]).IsSelected.ShouldBeFalse();
        AutomationProperties.GetPositionInSet(host.RowView(1)).ShouldBe(2);
        AutomationProperties.GetSizeOfSet(host.RowView(1)).ShouldBe(6);
        // Headings are not part of the control view.
        list.GetChildren().Where(p => p.IsControlElement()).ShouldAllBe(p => p.GetAutomationControlType() == AutomationControlType.ListItem);
        // Selecting an item makes it active; invoking it accepts it.
        ((ISelectionItemProvider)items[3]).Select();
        host.Pop.ActiveIndex.ShouldBe(3);
        host.Pop.ActiveRowName.ShouldBe("Sprint 15 review, Wiki, edited Mar 3");
        ((IInvokeProvider)items[4]).Invoke();
        host.Accepted.Single().Row.Id.ShouldBe("d-timeouts");
        // The command list has its own name.
        host.Pop.Show(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        host.Pump();
        ControlAutomationPeer.CreatePeerForElement(host.List).GetName().ShouldBe("Commands");
    }

    [AvaloniaFact]
    public void The_Active_Fill_Is_Surface_Muted_And_Text_Uses_The_Body_Colors()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        var tokens = host.Pop;
        TestHelpers.ColorOf(host.List.ActiveBackground).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(host.List.TitleForeground).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.Foreground));
        TestHelpers.ColorOf(host.List.HintForeground).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.TextMuted));
        TestHelpers.ColorOf(host.List.IconForeground).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.TextSecondary));
        TestHelpers.ColorOf(host.Pop.Background).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.SurfaceRaised));
        TestHelpers.ColorOf(host.Pop.BorderBrush).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.Border));
        host.List.TitleFontSize.ShouldBe(14);
        host.List.HintFontSize.ShouldBe(12);
    }

    private sealed class RowComparer : IEqualityComparer<AiSuggestionRow>
    {
        public bool Equals(AiSuggestionRow? x, AiSuggestionRow? y) =>
            x is not null && y is not null && x.Id == y.Id && x.Title == y.Title && x.Hint == y.Hint && x.Group == y.Group &&
            (x.Icon is null) == (y.Icon is null);

        public int GetHashCode(AiSuggestionRow obj) => obj.Id.GetHashCode();
    }
}
