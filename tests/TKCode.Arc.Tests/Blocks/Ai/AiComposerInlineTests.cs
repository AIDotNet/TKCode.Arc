using System.Windows.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiComposer.Layout"/> = <see cref="AiComposerLayout.Inline"/>, the AI chat's input (spec ai-chat 3.16): the
/// single row and its geometry, growth to 180 then scrolling, the compact chip row, the paperclip, the solid send button
/// in every state, Escape and Enter while generating, sending only attachments, what the layout leaves out, switching at
/// run time, motion levels and narrow widths. The state machine, events and labels are the full composer's.
/// </summary>
public class AiComposerInlineTests
{
    private const string Stop = "Stop reply";

    /// <summary>The chat's composer: inline, its words, no limit; <paramref name="samples"/> adds the full composer's commands, mentions, tools and models.</summary>
    private static AiComposerRig Rig(Action<AiComposer>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 776,
                                     bool samples = false, bool useFrameClock = false) =>
        new(c =>
        {
            c.Layout = AiComposerLayout.Inline;
            c.Placeholder = "Ask";
            c.MaxLength = null;
            c.Labels = new AiComposerLabels { Attach = "Attach files", Stop = Stop, SendEmpty = "Send message" };
            configure?.Invoke(c);
        }, level, width, samples: samples, useFrameClock: useFrameClock);

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static Point At(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value;

    private static async Task<IStorageFile> FileAsync(AiComposerRig rig, string name, long size) =>
        await AiAttachmentSamples.FileAsync(rig.Window, AiAttachmentSamples.NewFolder(), name, size);

    private sealed class Recorder : ICommand
    {
        public List<object?> Calls { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Calls.Add(parameter);
    }

    #region Structure and geometry

    [AvaloniaFact]
    public void An_Empty_Inline_Composer_Is_A_52px_Row_Of_Paperclip_Text_And_Send()
    {
        using var rig = Rig();
        var composer = rig.Composer;
        composer.Classes.ShouldContain(AiComposer.InlinePseudoClass);
        var surface = composer.Surface!;
        surface.CornerRadius.ShouldBe(new CornerRadius(24));
        surface.BorderThickness.ShouldBe(new Thickness(1));
        surface.Padding.ShouldBe(new Thickness(7));
        TestHelpers.ColorOf(surface.Background).ShouldBe(Token(composer, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(surface.BorderBrush).ShouldBe(Token(composer, ArcDesignTokenKind.Border));
        // 1 + 7 + 36 (one 24px line + 2 × 6) + 7 + 1
        composer.Bounds.Height.ShouldBe(52, 0.5);

        // Paperclip 34 (18px icon), 4, text (padding 6 4), 4, send 36; bottom aligned.
        var clip = composer.PlusButton!;
        clip.Bounds.Size.ShouldBe(new Size(34, 34));
        clip.Icon.ShouldBeSameAs(ArcIcons.Paperclip);
        clip.IconSize.ShouldBe(18);
        TestHelpers.Descendant<ArcGlyph>(clip).Size.ShouldBe(18);
        At(clip, composer).ShouldBe(new Point(8, 8 + 2));
        var editor = rig.Editor;
        editor.Padding.ShouldBe(new Thickness(4, 6));
        editor.Bounds.Height.ShouldBe(36, 0.5);
        At(editor, composer).ShouldBe(new Point(8 + 34 + 4, 8));
        var send = rig.Send;
        send.Bounds.Size.ShouldBe(new Size(36, 36));
        send.Appearance.ShouldBe(AiSendButtonAppearance.Solid);
        At(send, composer).ShouldBe(new Point(composer.Bounds.Width - 8 - 36, 8));
        (At(editor, composer).X + editor.Bounds.Width).ShouldBe(At(send, composer).X - 4, 0.5);
        // The text: 16 / 24 as in the full composer.
        editor.FontSize.ShouldBe(16);
        editor.LineHeight.ShouldBe(24);
    }

    [AvaloniaFact]
    public void The_Inline_Template_Has_No_Toolbar_Tools_Model_Pill_Or_Counter()
    {
        using var rig = Rig(samples: true);
        var composer = rig.Composer;
        composer.Toolbar.ShouldBeNull();
        composer.ToolsPanel.ShouldBeNull();
        composer.Counter.ShouldBeNull();
        composer.ModelPillControl.ShouldBeNull();
        composer.ToolToggles.ShouldBeEmpty();
        composer.GetVisualDescendants().OfType<AiToolToggle>().ShouldBeEmpty();
        composer.GetVisualDescendants().OfType<ModelPill>().ShouldBeEmpty();
        composer.GetVisualDescendants().OfType<AiCounter>().ShouldBeEmpty();
        composer.GetVisualDescendants().OfType<ArcRing>().ShouldBeEmpty();
        rig.Send.Ring.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Paperclip_Is_Named_With_A_Tooltip_And_Turns_SurfaceMuted_Under_The_Mouse()
    {
        using var rig = Rig();
        var clip = rig.Composer.PlusButton!;
        AutomationProperties.GetName(clip).ShouldBe("Attach files");
        Tooltip.GetTip(clip).ShouldBe("Attach files");
        Tooltip.GetSide(clip).ShouldBe(TooltipSide.Top);
        TestHelpers.ColorOf(clip.Foreground).ShouldBe(Token(clip, ArcDesignTokenKind.TextSecondary));
        rig.Window.MouseMove(rig.CenterOf(clip), RawInputModifiers.None);
        rig.Pump();
        TestHelpers.ColorOf(clip.Background).ShouldBe(Token(clip, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(clip.Foreground).ShouldBe(Token(clip, ArcDesignTokenKind.Foreground));
    }

    [AvaloniaFact]
    public void The_Border_Does_Not_Change_Under_The_Pointer_And_Turns_BorderStrong_With_The_Focus()
    {
        using var rig = Rig();
        var composer = rig.Composer;
        Color Border() => TestHelpers.ColorOf(composer.Surface!.BorderBrush);
        rig.Window.MouseMove(rig.CenterOf(rig.Editor), RawInputModifiers.None);
        rig.Pump();
        Border().ShouldBe(Token(composer, ArcDesignTokenKind.Border));
        rig.Focus();
        Border().ShouldBe(Token(composer, ArcDesignTokenKind.BorderStrong));
    }

    #endregion

    #region Growing

    [AvaloniaFact]
    public void The_Text_Grows_A_Line_At_A_Time_To_180_Then_Scrolls()
    {
        using var rig = Rig();
        rig.Editor.MaxLines.ShouldBe(7);
        rig.Focus();
        rig.TypeEach("one\ntwo\nthree");
        rig.Editor.Bounds.Height.ShouldBe(36 + 2 * 24, 0.5);
        rig.Composer.Bounds.Height.ShouldBe(52 + 2 * 24, 0.5);
        // The buttons stay at the bottom.
        At(rig.Send, rig.Composer).Y.ShouldBe(8 + 2 * 24, 0.5);
        At(rig.Composer.PlusButton!, rig.Composer).Y.ShouldBe(10 + 2 * 24, 0.5);
        rig.TypeEach("\n4\n5\n6\n7\n8\n9\n10");
        // 7 lines: 7 × 24 + 12 = 180, then the text scrolls inside.
        rig.Editor.Bounds.Height.ShouldBe(180, 0.5);
        rig.Composer.Bounds.Height.ShouldBe(180 + 16, 0.5);
        var scroller = TestHelpers.Descendant<ScrollViewer>(rig.Editor);
        scroller.Extent.Height.ShouldBeGreaterThan(scroller.Viewport.Height + 24);
        scroller.Offset.Y.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void MaxLines_Set_By_The_Host_Wins_Over_The_Inline_Default()
    {
        using var rig = Rig(c => c.MaxLines = 3);
        rig.Editor.MaxLines.ShouldBe(3);
        rig.Focus();
        rig.TypeEach("1\n2\n3\n4\n5");
        rig.Editor.Bounds.Height.ShouldBe(3 * 24 + 12, 0.5);
    }

    #endregion

    #region Attachments

    [AvaloniaFact]
    public void Chips_Open_A_40px_Row_Of_Compact_Pills_Above_The_Text()
    {
        using var rig = Rig();
        var composer = rig.Composer;
        composer.Tray!.ChipAppearance.ShouldBe(AiAttachmentChipAppearance.Compact);
        composer.Attachments.Add(AiComposerSamples.Attachment("stage-timings.csv", 1229));
        composer.Attachments.Add(AiComposerSamples.Attachment("pipeline.png", 84 * 1024, kind: AiAttachmentKind.Image));
        rig.Pump();
        // Row padding 2 2 0, pills 32, gap 6 to the text row.
        composer.Bounds.Height.ShouldBe(52 + 40, 0.5);
        var chips = composer.Tray.LiveItems.Select(i => i.Chip).ToList();
        chips.ShouldAllBe(c => c.Appearance == AiAttachmentChipAppearance.Compact);
        At(chips[0], composer).ShouldBe(new Point(8 + 2, 8 + 2));
        At(chips[1], composer).X.ShouldBe(8 + 2 + chips[0].Bounds.Width + 6, 0.5);
        chips[0].MetaText.ShouldBe("1 KB");
        AutomationProperties.GetName(chips[0].ActionButton!).ShouldBe("Remove stage-timings.csv");
        At(rig.Editor, composer).Y.ShouldBe(8 + 40, 0.5);
        composer.SendState.ShouldBe(AiSendState.Ready);

        // A chip's button removes it; the row folds away with the last one.
        rig.Click(chips[0].ActionButton!);
        rig.Click(composer.Tray.LiveItems[0].Chip.ActionButton!);
        rig.Pump();
        composer.Attachments.ShouldBeEmpty();
        composer.Bounds.Height.ShouldBe(52, 0.5);
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Chip_Row_Opens_On_A_Spring()
    {
        using var rig = Rig(level: ArcMotionLevel.Full);
        rig.Composer.Attachments.Add(AiComposerSamples.Attachment("stage-timings.csv", 1229));
        rig.Pump();
        rig.Run(0.1);
        rig.Composer.Bounds.Height.ShouldBeInRange(52.5, 91.5);
        rig.Run(1.2);
        rig.Composer.Bounds.Height.ShouldBe(92, 0.5);
    }

    [AvaloniaFact]
    public async Task The_Paperclip_Opens_The_File_Picker_Directly()
    {
        FilePickerOpenOptions? asked = null;
        IStorageFile? picked = null;
        var picks = 0;
        using var rig = Rig(c => c.PickFilesAsync = options =>
        {
            picks++;
            asked = options;
            return Task.FromResult<IReadOnlyList<IStorageFile>>(picked is null ? [] : [picked]);
        });
        picked = await FileAsync(rig, "stage-timings.csv", 1229);
        rig.Click(rig.Composer.PlusButton!);
        for (var i = 0; i < 100 && rig.Composer.Attachments.Count == 0; i++)
        {
            await Task.Delay(5);
            rig.Pump();
        }
        picks.ShouldBe(1);
        asked!.AllowMultiple.ShouldBeTrue();
        rig.Composer.AttachMenu.ShouldBeNull();
        var attachment = rig.Composer.Attachments.Single();
        attachment.Name.ShouldBe("stage-timings.csv");
        rig.Composer.Tray!.ChipFor(attachment)!.Appearance.ShouldBe(AiAttachmentChipAppearance.Compact);

        // Keys that open the full composer's plus menu open nothing here.
        rig.Composer.PlusButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Composer.AttachMenu.ShouldBeNull();
        rig.Key(PhysicalKey.Enter);
        picks.ShouldBe(2);
        rig.Composer.AttachMenu.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Without_Attachments_The_Paperclip_Hides_And_Files_Are_Refused()
    {
        using var rig = Rig(c => c.IsAttachmentEnabled = false);
        var composer = rig.Composer;
        composer.PlusButton!.IsVisible.ShouldBeFalse();
        // The text starts where the paperclip was, without its gap.
        At(rig.Editor, composer).X.ShouldBe(8, 0.5);
        (await composer.AddFilesAsync([await FileAsync(rig, "brief.pdf", 10)])).ShouldBeFalse();
        await composer.PasteAsync([await FileAsync(rig, "notes.txt", 10)], null);
        composer.Attachments.ShouldBeEmpty();
        // A long paste is typed as text.
        await composer.PasteAsync(null, AiComposerSamples.LongPaste());
        composer.Attachments.ShouldBeEmpty();
        composer.Text.ShouldStartWith("- Item 1:");
        composer.IsAttachmentEnabled = true;
        composer.PlusButton.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task Pasting_And_Dropping_Still_Attach_And_A_Long_Paste_Becomes_A_Chip()
    {
        using var rig = Rig();
        var composer = rig.Composer;
        await composer.PasteAsync(null, AiComposerSamples.LongPaste());
        var pasted = composer.Attachments.Single();
        pasted.Kind.ShouldBe(AiAttachmentKind.PastedText);
        composer.Text.ShouldBe(string.Empty);
        composer.Tray!.ChipFor(pasted)!.Appearance.ShouldBe(AiAttachmentChipAppearance.Compact);
        await composer.PasteAsync([await FileAsync(rig, "pipeline.png", 2048)], null);
        composer.Attachments.Count.ShouldBe(2);
        composer.SetDragging(true);
        rig.Pump();
        composer.DropHost!.Progress.ShouldBe(1);
        composer.DropTitle!.Text.ShouldBe("Drop to attach");
        composer.DropHint.ShouldBeNull();
        composer.SetDragging(false);
    }

    #endregion

    #region Send and stop

    [AvaloniaFact]
    public void The_Send_Button_Is_Solid_And_Named_In_Every_State()
    {
        using var rig = Rig();
        var composer = rig.Composer;
        var send = rig.Send;
        send.State.ShouldBe(AiSendState.Empty);
        TestHelpers.ColorOf(send.Background).ShouldBe(Token(send, ArcDesignTokenKind.SurfaceMuted));
        send.CurrentName.ShouldBe("Send message");
        Tooltip.GetTip(send).ShouldBeNull();

        composer.Text = "Plan it";
        send.State.ShouldBe(AiSendState.Ready);
        TestHelpers.ColorOf(send.Background).ShouldBe(Token(send, ArcDesignTokenKind.Accent));
        TestHelpers.ColorOf(send.Glyph!.Foreground).ShouldBe(Token(send, ArcDesignTokenKind.AccentForeground));
        AutomationProperties.GetName(send).ShouldBe("Send message");

        composer.IsGenerating = true;
        send.State.ShouldBe(AiSendState.Generating);
        TestHelpers.ColorOf(send.Background).ShouldBe(Token(send, ArcDesignTokenKind.Accent));
        TestHelpers.ColorOf(send.Glyph.Foreground).ShouldBe(Token(send, ArcDesignTokenKind.AccentForeground));
        send.Glyph.ShowsStop.ShouldBeTrue();
        send.Glyph.StopOpacity.ShouldBe(1);
        AutomationProperties.GetName(send).ShouldBe(Stop);
        Tooltip.GetTip(send).ShouldBeNull();
    }

    [AvaloniaFact]
    public void While_Generating_It_Is_Always_Stop_And_The_Progress_Is_Not_Shown()
    {
        using var rig = Rig(c => c.IsGenerating = true);
        var composer = rig.Composer;
        rig.Send.State.ShouldBe(AiSendState.Generating);
        composer.GenerationProgress = 0.4;
        rig.Pump();
        rig.Send.DisplayedProgress.ShouldBe(0);
        rig.Send.Ring.ShouldBeNull();
        // With text, an upload or an attachment it stays the stop button.
        composer.Text = "next";
        composer.Attachments.Add(AiComposerSamples.Attachment("brief.pdf", 99, AiAttachmentStatus.Uploading, 0.3));
        rig.Send.State.ShouldBe(AiSendState.Generating);
        rig.Send.Glyph!.ShowsStop.ShouldBeTrue();
        composer.GenerationProgress = 1;
        composer.IsGenerating = false;
        rig.Send.State.ShouldBe(AiSendState.Blocked);
        rig.Send.Glyph.ShowsStop.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Escape_And_The_Stop_Button_Stop_And_Enter_Keeps_The_Text_While_Generating()
    {
        var stop = new Recorder();
        using var rig = Rig(c => c.StopCommand = stop);
        rig.Focus();
        rig.Key(PhysicalKey.Escape);
        rig.Stops.ShouldBe(0);
        rig.Composer.IsGenerating = true;
        rig.Type("follow-up");
        rig.Key(PhysicalKey.Enter);
        rig.Sent.ShouldBeEmpty();
        rig.Composer.Text.ShouldBe("follow-up");
        rig.Key(PhysicalKey.Escape);
        rig.Stops.ShouldBe(1);
        rig.Announcement.ShouldBe("Stopped");
        rig.Click(rig.Send);
        rig.Stops.ShouldBe(2);
        stop.Calls.Count.ShouldBe(2);
        // The host ends the reply; then Enter sends what was typed.
        rig.Composer.IsGenerating = false;
        rig.Focus();
        rig.Key(PhysicalKey.Enter);
        rig.Sent.Single().Text.ShouldBe("follow-up");
    }

    [AvaloniaFact]
    public void A_Message_Of_Only_Attachments_Can_Be_Sent()
    {
        using var rig = Rig();
        var composer = rig.Composer;
        var file = AiComposerSamples.Attachment("stage-timings.csv", 1229);
        composer.Attachments.Add(file);
        composer.SendState.ShouldBe(AiSendState.Ready);
        rig.Click(rig.Send);
        var message = rig.Sent.Single();
        message.Text.ShouldBe(string.Empty);
        message.Attachments.ShouldBe([file]);
        message.Model.ShouldBeNull();
        message.Tools.ShouldBeEmpty();
        composer.Attachments.ShouldBeEmpty();
        rig.Removed.ShouldBeEmpty();
        rig.Announcement.ShouldBe("Message sent");
    }

    [AvaloniaFact]
    public void Slash_And_At_Open_No_List_In_The_Inline_Layout()
    {
        using var rig = Rig(samples: true);
        rig.Focus();
        rig.Type("/");
        rig.ListOpen.ShouldBeFalse();
        rig.Type(" @");
        rig.ListOpen.ShouldBeFalse();
        rig.Composer.Text.ShouldBe("/ @");
        rig.Composer.Value.Tokens.ShouldBeEmpty();
        rig.Key(PhysicalKey.Enter);
        rig.Sent.Single().Text.ShouldBe("/ @");
    }

    #endregion

    #region Switching

    [AvaloniaFact]
    public void The_Layout_Switches_At_Run_Time_Keeping_The_Text_The_Attachments_And_The_Focus()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        var file = AiComposerSamples.Attachment("roadmap.pdf", 4096);
        composer.Attachments.Add(file);
        rig.Focus();
        rig.Type("keep me");
        composer.Toolbar.ShouldNotBeNull();
        composer.Tray!.ChipFor(file)!.Appearance.ShouldBe(AiAttachmentChipAppearance.Card);

        composer.Layout = AiComposerLayout.Inline;
        rig.Pump();
        composer.Classes.ShouldContain(AiComposer.InlinePseudoClass);
        composer.Toolbar.ShouldBeNull();
        composer.ModelPillControl.ShouldBeNull();
        composer.PlusButton!.Icon.ShouldBeSameAs(ArcIcons.Paperclip);
        rig.Editor.Text.ShouldBe("keep me");
        composer.Tray!.ChipFor(file)!.Appearance.ShouldBe(AiAttachmentChipAppearance.Compact);
        rig.Send.Appearance.ShouldBe(AiSendButtonAppearance.Solid);
        composer.Surface!.CornerRadius.ShouldBe(new CornerRadius(24));
        rig.EditorFocused.ShouldBeTrue();
        composer.Bounds.Height.ShouldBe(52 + 40, 0.5);

        composer.Layout = AiComposerLayout.Full;
        rig.Pump();
        composer.Classes.ShouldNotContain(AiComposer.InlinePseudoClass);
        composer.Toolbar.ShouldNotBeNull();
        composer.ToolToggles.Count.ShouldBe(2);
        composer.ModelPillControl!.IsVisible.ShouldBeTrue();
        composer.PlusButton!.Icon.ShouldBeSameAs(ArcIcons.Plus);
        composer.PlusButton.IconSize.ShouldBe(20);
        rig.Send.Appearance.ShouldBe(AiSendButtonAppearance.Ring);
        composer.Tray!.ChipFor(file)!.Appearance.ShouldBe(AiAttachmentChipAppearance.Card);
        rig.Editor.Text.ShouldBe("keep me");
        rig.Editor.MaxLines.ShouldBe(8);
        composer.Surface!.CornerRadius.ShouldBe(new CornerRadius(26));
        rig.EditorFocused.ShouldBeTrue();
        // The plus menu is back.
        rig.Click(composer.PlusButton);
        composer.AttachMenu!.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Switching_Closes_An_Open_List_And_Turns_The_Triggers_Off()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("/");
        rig.ListOpen.ShouldBeTrue();
        rig.Composer.Layout = AiComposerLayout.Inline;
        rig.Pump();
        rig.ListOpen.ShouldBeFalse();
        rig.Focus();
        rig.Key(PhysicalKey.Backspace);
        rig.Type("/");
        rig.ListOpen.ShouldBeFalse();
    }

    #endregion

    #region Motion levels and widths

    private static TimeSpan BorderDuration(AiComposer composer) =>
        composer.Transitions?.OfType<ArcBrushTransition>().FirstOrDefault(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty)?.Duration
        ?? TimeSpan.Zero;

    [AvaloniaFact]
    public void The_Focus_Border_Moves_In_160ms_Full_120ms_Reduced_And_Jumps_Off()
    {
        using (var full = Rig(level: ArcMotionLevel.Full))
        {
            BorderDuration(full.Composer).ShouldBe(TimeSpan.FromMilliseconds(160));
        }
        using (var reduced = Rig(level: ArcMotionLevel.Reduced))
        {
            BorderDuration(reduced.Composer).ShouldBe(TimeSpan.FromMilliseconds(120));
        }
        using var off = Rig();
        BorderDuration(off.Composer).ShouldBe(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_The_Chip_Row_At_Once_And_Off_Too()
    {
        using (var reduced = Rig(level: ArcMotionLevel.Reduced, useFrameClock: true))
        {
            reduced.Composer.Attachments.Add(AiComposerSamples.Attachment("stage-timings.csv", 1229));
            reduced.Pump();
            reduced.Run(0.016);
            reduced.Composer.Bounds.Height.ShouldBe(92, 0.5);
            // A new line jumps too.
            reduced.Focus();
            reduced.Type("one");
            reduced.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
            reduced.Editor.Bounds.Height.ShouldBe(60, 0.5);
        }
        using var off = Rig();
        off.Composer.Attachments.Add(AiComposerSamples.Attachment("stage-timings.csv", 1229));
        off.Pump();
        off.Composer.Bounds.Height.ShouldBe(92, 0.5);
    }

    [AvaloniaFact]
    public void At_Full_Motion_A_New_Line_Grows_The_Box_On_A_160ms_Ease()
    {
        using var rig = Rig(level: ArcMotionLevel.Full);
        rig.Focus();
        rig.Type("one");
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Editor.TargetHeight.ShouldBe(60, 0.5);
        rig.Run(0.016);
        rig.Editor.Bounds.Height.ShouldBeInRange(36.5, 59.5);
        rig.Run(0.2);
        rig.Editor.Bounds.Height.ShouldBe(60, 0.5);
    }

    [AvaloniaTheory]
    [InlineData(390)]
    [InlineData(320)]
    public void At_Narrow_Widths_The_Row_Still_Fits(double width)
    {
        using var rig = Rig(c => c.Text = "Draft a rollback plan if the migration fails", width: width);
        var composer = rig.Composer;
        composer.Bounds.Width.ShouldBe(width, 0.5);
        var clip = composer.PlusButton!;
        var send = rig.Send;
        // Paperclip and send keep their sizes at the ends; the text takes the rest and wraps.
        clip.Bounds.Size.ShouldBe(new Size(34, 34));
        send.Bounds.Size.ShouldBe(new Size(36, 36));
        At(send, composer).X.ShouldBe(width - 8 - 36, 0.5);
        rig.Editor.Bounds.Width.ShouldBe(width - 16 - 34 - 4 - 4 - 36, 0.5);
        (At(clip, composer).Y + 34).ShouldBe(At(send, composer).Y + 36, 0.5);
        rig.Editor.Bounds.Height.ShouldBeGreaterThan(36);
    }

    #endregion
}
