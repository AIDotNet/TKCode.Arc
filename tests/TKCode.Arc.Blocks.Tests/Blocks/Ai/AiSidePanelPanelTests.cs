using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The side panel's selection flow, opening and closing, the sheet and the focus rules.</summary>
public class AiSidePanelPanelTests
{
    #region Selection

    [AvaloniaFact]
    public void Asking_about_a_selection_adds_the_pill_marks_the_block_and_focuses_the_box()
    {
        using var rig = new AiSidePanelRig();
        var selection = rig.AskAboutBlock("scope");
        rig.Panel.Selection.ShouldBe(selection);
        selection.BlockId.ShouldBe("scope");
        rig.Selections.ShouldBe([selection]);
        rig.Panel.IsSelectionPillShown.ShouldBeTrue();
        var pill = rig.Panel.SelectionPill!;
        pill.IsVisible.ShouldBeTrue();
        pill.Text.ShouldBe("Selected: Scope");
        pill.FullText.ShouldBe(selection.Text);
        Tooltip.GetTip(pill).ShouldBe(selection.Text);
        AutomationProperties.GetName(pill.RemoveButton!).ShouldBe("Remove Scope from context");
        rig.PageView.MarkedBlockId.ShouldBe("scope");
        rig.PageView.ViewOf("scope")!.IsMarked.ShouldBeTrue();
        rig.Box.Placeholder.ShouldBe("Ask about scope");
        rig.Focused.ShouldBeSameAs(rig.Input);
        rig.Announcement.ShouldBe("Asking about the selection in Scope");
        rig.Panel.Classes.Contains(":selection").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_new_selection_replaces_the_old_one()
    {
        using var rig = new AiSidePanelRig();
        rig.AskAboutBlock("scope");
        var second = rig.AskAboutBlock("limits");
        rig.Panel.Selection.ShouldBe(second);
        rig.Panel.SelectionPill!.Text.ShouldBe("Selected: Limits");
        rig.PageView.MarkedBlockId.ShouldBe("limits");
        rig.PageView.ViewOf("scope")!.IsMarked.ShouldBeFalse();
        rig.Selections.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_question_asked_with_a_selection_carries_it_and_the_pill_stays()
    {
        using var rig = new AiSidePanelRig();
        var selection = rig.AskAboutBlock("summary");
        rig.Send("What does this say?");
        rig.Call.Request.Selection.ShouldBe(selection);
        rig.LastTurn.ContextLabel.ShouldBe("Summary");
        var view = rig.LastView;
        view.ContextLine!.IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(view, AiPanelTurnView.ContextTextPart).Text.ShouldBe("Selected: Summary");
        rig.Panel.Selection.ShouldBe(selection);
        rig.Panel.IsSelectionPillShown.ShouldBeTrue();
        // Without a selection there is no context line.
        rig.Panel.Stop();
        rig.Panel.ClearSelection();
        rig.Send("And now?");
        rig.LastView.ContextLine!.IsVisible.ShouldBeFalse();
        rig.Call.Request.Selection.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Removing_the_pill_clears_the_selection_and_focuses_the_box()
    {
        using var rig = new AiSidePanelRig();
        rig.AskAboutBlock("scope");
        rig.Panel.CloseButton!.Focus();
        rig.Settle();
        rig.Click(rig.Panel.SelectionPill!.RemoveButton!);
        rig.Panel.Selection.ShouldBeNull();
        rig.Panel.IsSelectionPillShown.ShouldBeFalse();
        rig.PageView.MarkedBlockId.ShouldBeNull();
        rig.Box.Placeholder.ShouldBe("Ask about this page");
        rig.Focused.ShouldBeSameAs(rig.Input);
        rig.Announcement.ShouldBe("Selection cleared");
    }

    [AvaloniaFact]
    public void Asking_about_a_selection_opens_a_closed_panel()
    {
        using var rig = new AiSidePanelRig();
        rig.Panel.Close();
        rig.Run(1);
        rig.Panel.IsOpen.ShouldBeFalse();
        rig.AskAboutBlock("limits");
        rig.Panel.IsOpen.ShouldBeTrue();
        rig.Run(1);
        rig.Dock.DockWidth.ShouldBe(360, 0.5);
        rig.Focused.ShouldBeSameAs(rig.Input);
    }

    #endregion

    #region Open and close

    [AvaloniaFact]
    public void The_close_button_closes_and_moves_the_focus_to_the_reopen_pill()
    {
        using var rig = new AiSidePanelRig();
        Tooltip.GetTip(rig.Panel.CloseButton!).ShouldBe("Close");
        rig.Panel.CloseButton!.Glyph.ShouldBe(ArcIcons.PanelRightClose);
        rig.Click(rig.Panel.CloseButton!);
        rig.Panel.IsOpen.ShouldBeFalse();
        rig.Panel.Classes.Contains(":open").ShouldBeFalse();
        rig.Focused.ShouldBeSameAs(rig.Dock.ReopenButton);
        rig.Announcement.ShouldBe("Helper closed");
        rig.Run(1);
        rig.Dock.DockWidth.ShouldBe(0, 0.5);
        rig.Dock.ReopenButton!.Text.ShouldBe("Helper");
    }

    [AvaloniaFact]
    public void Escape_in_the_panel_closes_it_and_the_reopen_pill_opens_it_with_the_focus_in_the_box()
    {
        using var rig = new AiSidePanelRig();
        rig.Box.FocusInput(NavigationMethod.Tab);
        rig.Settle();
        rig.Key(PhysicalKey.Escape);
        rig.Panel.IsOpen.ShouldBeFalse();
        rig.Focused.ShouldBeSameAs(rig.Dock.ReopenButton);
        rig.Run(1);
        rig.Key(PhysicalKey.Enter);
        rig.Panel.IsOpen.ShouldBeTrue();
        rig.Focused.ShouldBeSameAs(rig.Input);
        rig.Announcement.ShouldBe("Helper opened");
    }

    [AvaloniaFact]
    public void Open_and_Close_drive_the_dock_and_IsOpen_binds_both_ways()
    {
        var host = new OpenHost();
        using var rig = new AiSidePanelRig(p => p.Bind(AiSidePanel.IsOpenProperty, new Binding(nameof(OpenHost.Open)) { Source = host, Mode = BindingMode.TwoWay }));
        rig.Panel.IsOpen.ShouldBeTrue();
        rig.Panel.Close();
        rig.Settle();
        host.Open.ShouldBeFalse();
        rig.Dock.IsOpen.ShouldBeFalse();
        host.Open = true;
        rig.Settle();
        rig.Panel.IsOpen.ShouldBeTrue();
        rig.Dock.IsOpen.ShouldBeTrue();
        rig.Click(rig.Panel.CloseButton!);
        host.Open.ShouldBeFalse();
        rig.Run(1);
        rig.Panel.Open();
        rig.Settle();
        host.Open.ShouldBeTrue();
        rig.Focused.ShouldBeSameAs(rig.Input);
    }

    private sealed class OpenHost : AvaloniaObject
    {
        public static readonly StyledProperty<bool> OpenProperty = AvaloniaProperty.Register<OpenHost, bool>(nameof(Open), true);

        public bool Open { get => GetValue(OpenProperty); set => SetValue(OpenProperty, value); }
    }

    #endregion

    #region Sheet

    [AvaloniaFact]
    public void A_narrow_container_makes_the_assistant_a_sheet()
    {
        using var rig = new AiSidePanelRig(width: 600);
        rig.Panel.IsSheetMode.ShouldBeTrue();
        rig.Panel.Classes.Contains(":sheet").ShouldBeTrue();
        rig.PageView.IsCompact.ShouldBeTrue();
        rig.Panel.CloseButton!.Glyph.ShouldBe(ArcIcons.X);
        rig.Window.Width = 1100;
        rig.Settle();
        rig.Panel.IsSheetMode.ShouldBeFalse();
        rig.PageView.IsCompact.ShouldBeFalse();
        rig.Panel.CloseButton!.Glyph.ShouldBe(ArcIcons.PanelRightClose);
    }

    [AvaloniaFact]
    public void A_selection_pulls_a_closed_sheet_up()
    {
        using var rig = new AiSidePanelRig(width: 390, height: 800);
        rig.Panel.Close();
        rig.Run(1);
        rig.AskAboutBlock("summary");
        rig.Panel.IsOpen.ShouldBeTrue();
        rig.Run(1);
        rig.Dock.SheetOffset.ShouldBe(0, 0.5);
        rig.Panel.SelectionPill!.Text.ShouldBe("Selected: Summary");
    }

    #endregion

    #region Words and identity

    [AvaloniaFact]
    public void Labels_and_identity_can_change()
    {
        using var rig = new AiSidePanelRig();
        rig.Panel.Labels = new AiSidePanelLabels { Placeholder = "Frage stellen", ThisPage = "Diese Seite", CloseTip = "Schließen" };
        rig.Panel.AssistantName = "Orbit";
        rig.Panel.ModelName = null;
        rig.Settle();
        rig.Box.Placeholder.ShouldBe("Frage stellen");
        rig.Panel.PagePill!.Text.ShouldBe("Diese Seite");
        Tooltip.GetTip(rig.Panel.CloseButton!).ShouldBe("Schließen");
        rig.Panel.NameText!.Text.ShouldBe("Orbit");
        rig.Panel.ModelText!.IsVisible.ShouldBeFalse();
        rig.Dock.ReopenButton!.Text.ShouldBe("Orbit");
        // Without a model name, announcements use the assistant's name.
        rig.Send("Hello");
        rig.Announcement.ShouldBe("Orbit is reading the page");
    }

    [AvaloniaFact]
    public void The_page_column_follows_PageMaxWidth()
    {
        using var rig = new AiSidePanelRig(p => p.PageMaxWidth = 600);
        rig.PageView.MaxColumnWidth.ShouldBe(600);
        rig.PageView.Article!.Bounds.Width.ShouldBe(600, 0.5);
        rig.Panel.PageMaxWidth = 680;
        rig.Settle();
        rig.PageView.Article!.Bounds.Width.ShouldBe(680, 0.5);
    }

    #endregion

    #region Tab order

    [AvaloniaFact]
    public void Tab_goes_from_the_close_button_through_the_thread_to_the_box()
    {
        using var rig = new AiSidePanelRig();
        rig.Answer("Shorten the summary", "Shorter.",
            new AiPanelChangeProposal("summary", new AiPanelParagraph { Id = "summary", Text = AiSidePanelSamples.ShortSummary }));
        rig.Panel.CloseButton!.Focus(NavigationMethod.Tab);
        rig.Settle();
        var order = new List<IInputElement?> { rig.Focused };
        for (var i = 0; i < 6; i++)
        {
            rig.Key(PhysicalKey.Tab);
            order.Add(rig.Focused);
        }
        var card = rig.LastView.Card!;
        var suggestions = rig.Panel.FollowingSuggestions!.Pills.ToList();
        order.ShouldBe(new IInputElement?[]
        {
            rig.Panel.CloseButton, card.DiscardButton, card.ApplyButton, suggestions[0], suggestions[1], rig.Input, rig.Box.SendButton
        });
    }

    #endregion
}
