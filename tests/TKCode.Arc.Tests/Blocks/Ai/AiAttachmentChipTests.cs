using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.Blocks.Ai.AiAttachmentSamples;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiAttachmentChip"/>: geometry against the reference, the four statuses (glyph, meta line, button name,
/// pseudo-classes, error border), pictures and pasted text, the caller's texts, the two requests, focus and accessibility,
/// following the attachment, and the swap motion at full, reduced and no motion.
/// </summary>
public class AiAttachmentChipTests
{
    private const long MB = 1024 * 1024;

    private static AiAttachmentChip Create(AiAttachment attachment) => new()
    {
        Attachment = attachment,
        RemoveText = n => $"Take out {n}",
        CancelText = n => $"Stop uploading {n}",
        UploadingText = p => $"{p}% uploaded",
        ErrorText = "Could not upload",
        ImageText = "Picture",
        LinesText = n => $"{n} lines of text"
    };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var host = new Border { Padding = new Thickness(16), Child = content, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = TestHelpers.Show(host, level);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static bool WaitFor(Func<bool> done)
    {
        for (var i = 0; i < 200; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (done())
            {
                return true;
            }
            Thread.Sleep(5);
        }
        return false;
    }

    private static T Part<T>(Control owner, string name) where T : Control => TestHelpers.Descendant<T>(owner, name);

    private static AiAttachmentStatusGlyph Glyph(AiAttachmentChip chip) => chip.StatusGlyph!;

    private static int MetaLayers(AiAttachmentChip chip) =>
        Part<ArcContentSwap>(chip, AiAttachmentChip.MetaPart).GetVisualChildren().Count();

    private static AiAttachmentStatusGlyph.Shown? Shown(AiAttachmentChip chip) => Glyph(chip).Current;

    #region Geometry

    [AvaloniaFact]
    public void Geometry_Matches_The_Reference()
    {
        var chip = Create(Sample("sprint-15-review.pdf", 480_256));
        var window = Show(chip);
        chip.Bounds.Size.ShouldBe(new Size(212, 52));
        var frame = Part<Border>(chip, AiAttachmentChip.FramePart);
        frame.BorderThickness.ShouldBe(new Thickness(1));
        frame.CornerRadius.ShouldBe(new CornerRadius(16));
        frame.Padding.ShouldBe(new Thickness(12, 6, 8, 6));

        // Type icon 20 / 1.75 at the left padding; the text column 10 after it.
        var icon = Part<ArcGlyph>(chip, AiAttachmentChip.IconPart);
        icon.Size.ShouldBe(20);
        icon.StrokeThickness.ShouldBe(1.75);
        icon.TranslatePoint(default, chip)!.Value.X.ShouldBe(13);
        Part<TextBlock>(chip, AiAttachmentChip.NamePart).TranslatePoint(default, chip)!.Value.X.ShouldBe(43);

        // The 28px round button ends at the right padding, centred; its glyph slot is 18 with a 16px ring of stroke 2.
        var button = chip.ActionButton!;
        button.Bounds.Size.ShouldBe(new Size(28, 28));
        var at = button.TranslatePoint(default, chip)!.Value;
        at.X.ShouldBe(212 - 1 - 8 - 28);
        at.Y.ShouldBe(12);
        Part<Border>(button, "PART_Face").CornerRadius.ShouldBe(new CornerRadius(14));
        var glyph = Glyph(chip);
        glyph.Bounds.Size.ShouldBe(new Size(18, 18));
        glyph.Ring.Width.ShouldBe(16);
        glyph.Ring.Height.ShouldBe(16);
        glyph.Ring.Thickness.ShouldBe(2);
        glyph.Ring.IsRoundCap.ShouldBeTrue();
        glyph.Ring.StartAngle.ShouldBe(-90);
        glyph.Ring.ChaseRate.ShouldBe(0.35);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Thumbnail_Is_38px_Six_Pixels_Further_Left()
    {
        var picture = Sample("whiteboard.png", 240 * 1024, kind: AiAttachmentKind.Image);
        picture.Thumbnail = Thumbnail();
        var chip = Create(picture);
        var window = Show(chip);
        chip.Classes.ShouldContain(AiAttachmentChip.ThumbnailPseudoClass);
        var thumbnail = Part<Border>(chip, AiAttachmentChip.ThumbnailPart);
        thumbnail.IsVisible.ShouldBeTrue();
        thumbnail.Bounds.Size.ShouldBe(new Size(38, 38));
        thumbnail.CornerRadius.ShouldBe(new CornerRadius(10));
        thumbnail.TranslatePoint(default, chip)!.Value.ShouldBe(new Point(7, 7));
        ((Image)thumbnail.Child!).Source.ShouldBeSameAs(picture.Thumbnail);
        ((Image)thumbnail.Child!).Stretch.ShouldBe(Stretch.UniformToFill);
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).IsVisible.ShouldBeFalse();
        // The text column moves with the picture: 38 − 6 + 10 after the padding.
        Part<TextBlock>(chip, AiAttachmentChip.NamePart).TranslatePoint(default, chip)!.Value.X.ShouldBe(13 - 6 + 38 + 10);
        chip.MetaText.ShouldBe("Picture, 240 KB");
        window.Close();
    }

    [AvaloniaFact]
    public void Text_Uses_The_Body_Sizes()
    {
        var chip = Create(Sample("sprint-15-review.pdf", 480_256));
        var window = Show(chip);
        var name = Part<TextBlock>(chip, AiAttachmentChip.NamePart);
        name.FontSize.ShouldBe(14);
        name.LineHeight.ShouldBe(18.2);
        name.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
        name.TextWrapping.ShouldBe(TextWrapping.NoWrap);
        var meta = TestHelpers.Descendant<TextBlock>(chip.MetaLine!, "PART_Text");
        meta.FontSize.ShouldBe(12);
        meta.LineHeight.ShouldBe(15.6);
        meta.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
        TestHelpers.ColorOf(meta.Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.TextMuted));
        TestHelpers.ColorOf(name.Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.Foreground));
        window.Close();
    }

    [AvaloniaFact]
    public void A_Long_Name_Is_Ellipsized_Within_The_Chip()
    {
        var chip = Create(Sample("quarterly-planning-notes-final-version-three.docx", 29 * 1024));
        var window = Show(chip);
        chip.Bounds.Width.ShouldBe(212);
        var name = Part<TextBlock>(chip, AiAttachmentChip.NamePart);
        var right = name.TranslatePoint(new Point(name.Bounds.Width, 0), chip)!.Value.X;
        right.ShouldBeLessThanOrEqualTo(212 - 1 - 8 - 28 - 10 + 0.5);
        window.Close();
    }

    #endregion

    #region Statuses

    [AvaloniaFact]
    public void Ready_Shows_The_Remove_Button_And_The_Type_And_Size()
    {
        var chip = Create(Sample("sprint-15-review.pdf", 480_256));
        var window = Show(chip);
        chip.Classes.ShouldContain(AiAttachmentChip.ReadyPseudoClass);
        chip.Classes.ShouldNotContain(AiAttachmentChip.UploadingPseudoClass);
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Remove);
        Glyph(chip).RemoveLayer.Opacity.ShouldBe(1);
        Glyph(chip).RingLayer.Opacity.ShouldBe(0);
        Glyph(chip).CheckLayer.Opacity.ShouldBe(0);
        chip.MetaText.ShouldBe("PDF, 469 KB");
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Take out sprint-15-review.pdf");
        Part<Border>(chip, "PART_ErrorEdge").IsVisible.ShouldBeFalse();
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBe(ArcIcons.FileText);
        window.Close();
    }

    [AvaloniaFact]
    public void Uploading_Shows_The_Ring_The_Percentage_And_A_Cancel_Button()
    {
        var chip = Create(Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.4));
        var window = Show(chip);
        chip.Classes.ShouldContain(AiAttachmentChip.UploadingPseudoClass);
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Ring);
        Glyph(chip).RingLayer.Opacity.ShouldBe(1);
        Glyph(chip).Ring.Value.ShouldBe(0.4);
        chip.MetaText.ShouldBe("40% uploaded");
        chip.MetaLine!.Classes.ShouldContain(AiAttachmentChipMeta.ProgressPseudoClass);
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Stop uploading launch-brief.pdf");
        window.Close();
    }

    [AvaloniaFact]
    public void Progress_Moves_The_Ring_And_The_Percentage_In_Place()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.1);
        var chip = Create(attachment);
        var window = Show(chip);
        var line = chip.MetaLine;
        attachment.Progress = 0.077;
        chip.MetaText.ShouldBe("8% uploaded");
        Glyph(chip).Ring.Value.ShouldBe(0.077);
        attachment.Progress = 0.385;
        chip.MetaText.ShouldBe("39% uploaded");
        // Same line, no crossfade per step.
        chip.MetaLine.ShouldBeSameAs(line);
        MetaLayers(chip).ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Done_Shows_The_Check_And_The_Final_Meta()
    {
        var attachment = Sample("usage-oct.csv", 12 * 1024, AiAttachmentStatus.Uploading, 0.9);
        var chip = Create(attachment);
        var window = Show(chip);
        attachment.Progress = 1;
        attachment.Status = AiAttachmentStatus.Done;
        chip.Classes.ShouldContain(AiAttachmentChip.DonePseudoClass);
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Check);
        Glyph(chip).CheckLayer.Opacity.ShouldBe(1);
        Glyph(chip).RingLayer.Opacity.ShouldBe(0);
        chip.MetaText.ShouldBe("CSV, 12 KB");
        chip.MetaLine!.Classes.ShouldNotContain(AiAttachmentChipMeta.ProgressPseudoClass);
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Take out usage-oct.csv");
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBe(ArcIcons.FileSpreadsheet);

        attachment.Status = AiAttachmentStatus.Ready;
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Remove);
        Glyph(chip).RemoveLayer.Opacity.ShouldBe(1);
        Glyph(chip).CheckLayer.Opacity.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Error_Draws_The_Danger_Border_And_The_Reason()
    {
        var chip = Create(Sample("standup-capture.mov", 31 * MB, AiAttachmentStatus.Error, error: "Larger than 30 MB"));
        var window = Show(chip);
        chip.Classes.ShouldContain(AiAttachmentChip.ErrorPseudoClass);
        var edge = Part<Border>(chip, "PART_ErrorEdge");
        edge.IsVisible.ShouldBeTrue();
        edge.BorderThickness.ShouldBe(new Thickness(1));
        edge.CornerRadius.ShouldBe(new CornerRadius(16));
        var expected = Oklch.MixOklab(ArcTokens.GetColor(chip, ArcDesignTokenKind.Border), ArcTokens.GetColor(chip, ArcDesignTokenKind.Danger), 0.35);
        TestHelpers.ColorOf(edge.BorderBrush).ShouldBe(expected);
        chip.MetaText.ShouldBe("Larger than 30 MB");
        var meta = chip.MetaLine!;
        meta.IsError.ShouldBeTrue();
        var alert = TestHelpers.Descendant<ArcGlyph>(meta, "PART_Alert");
        alert.IsVisible.ShouldBeTrue();
        alert.Data.ShouldBe(ArcIcons.CircleAlert);
        alert.Size.ShouldBe(12);
        alert.StrokeThickness.ShouldBe(2);
        var danger = ArcTokens.GetColor(chip, ArcDesignTokenKind.Danger);
        TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(meta, "PART_Text").Foreground).ShouldBe(danger);
        TestHelpers.ColorOf(alert.Foreground).ShouldBe(danger);
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Remove);
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Take out standup-capture.mov");
        window.Close();
    }

    [AvaloniaFact]
    public void An_Error_Without_A_Reason_Shows_The_Fallback()
    {
        var attachment = Sample("brief.pdf", 1_000, AiAttachmentStatus.Error);
        var chip = Create(attachment);
        var window = Show(chip);
        chip.MetaText.ShouldBe("Could not upload");
        attachment.Error = "Storage is full";
        chip.MetaText.ShouldBe("Storage is full");
        chip.ErrorText = null;
        attachment.Error = null;
        chip.MetaText.ShouldBe(string.Empty);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_Error_Border_Follows_The_Theme()
    {
        var chip = Create(Sample("brief.pdf", 1_000, AiAttachmentStatus.Error, error: "Larger than 30 MB"));
        var window = Show(chip);
        var light = TestHelpers.ColorOf(chip.ErrorBorderBrush);
        await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
        try
        {
            var dark = TestHelpers.ColorOf(chip.ErrorBorderBrush);
            dark.ShouldNotBe(light);
            dark.ShouldBe(Oklch.MixOklab(ArcTokens.GetColor(chip, ArcDesignTokenKind.Border), ArcTokens.GetColor(chip, ArcDesignTokenKind.Danger), 0.35));
        }
        finally
        {
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().Build());
            window.Close();
        }
    }

    #endregion

    #region Kinds and texts

    [AvaloniaFact]
    public void A_Picture_Without_A_Thumbnail_Shows_The_Image_Icon()
    {
        var chip = Create(Sample("whiteboard.png", 240 * 1024, kind: AiAttachmentKind.Image));
        var window = Show(chip);
        chip.Classes.ShouldNotContain(AiAttachmentChip.ThumbnailPseudoClass);
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBe(ArcIcons.Image);
        Part<Border>(chip, AiAttachmentChip.ThumbnailPart).IsVisible.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void A_Thumbnail_Arriving_Later_Replaces_The_Icon()
    {
        var picture = Sample("whiteboard.png", 240 * 1024, kind: AiAttachmentKind.Image);
        var chip = Create(picture);
        var window = Show(chip);
        picture.Thumbnail = Thumbnail();
        chip.Classes.ShouldContain(AiAttachmentChip.ThumbnailPseudoClass);
        Part<Border>(chip, AiAttachmentChip.ThumbnailPart).IsVisible.ShouldBeTrue();
        picture.Thumbnail = null;
        chip.Classes.ShouldNotContain(AiAttachmentChip.ThumbnailPseudoClass);
        window.Close();
    }

    [AvaloniaFact]
    public void Pasted_Text_Shows_The_Clipboard_And_Its_Lines()
    {
        var text = string.Join('\n', Enumerable.Repeat("Retry the export with smaller chunks.", 24));
        var chip = Create(AiFileIntake.FromPastedText(text, "Pasted snippet", n => $"{n} lines of text"));
        var window = Show(chip);
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBe(ArcIcons.ClipboardPaste);
        chip.MetaText.ShouldBe("24 lines of text");
        // Without its own meta the chip counts the lines through its own text.
        chip.Attachment = AiFileIntake.FromPastedText("a\nb\nc", "Pasted snippet");
        chip.MetaText.ShouldBe("3 lines of text");
        window.Close();
    }

    [AvaloniaFact]
    public void The_Attachments_Own_Meta_And_Icon_Win()
    {
        var source = Sample("Budget forecast", 380 * 1024, kind: AiAttachmentKind.Source);
        source.Meta = "Spreadsheet, 380 KB";
        source.Icon = ArcIcons.Cloud;
        var chip = Create(source);
        var window = Show(chip);
        chip.MetaText.ShouldBe("Spreadsheet, 380 KB");
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBe(ArcIcons.Cloud);
        source.Meta = null;
        chip.MetaText.ShouldBe("380 KB");
        window.Close();
    }

    [AvaloniaFact]
    public void Without_Texts_The_Chip_Shows_No_Words_Of_Its_Own()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.4);
        var chip = new AiAttachmentChip { Attachment = attachment };
        var window = Show(chip);
        chip.MetaText.ShouldBe("40%");
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("launch-brief.pdf");
        attachment.Status = AiAttachmentStatus.Ready;
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("launch-brief.pdf");
        var picture = Sample("whiteboard.png", 240 * 1024, kind: AiAttachmentKind.Image);
        chip.Attachment = picture;
        chip.MetaText.ShouldBe("PNG, 240 KB");
        window.Close();
    }

    [AvaloniaFact]
    public void Changing_A_Text_Updates_The_Chip()
    {
        var chip = Create(Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.5));
        var window = Show(chip);
        chip.UploadingText = p => $"Envoi {p} %";
        chip.MetaText.ShouldBe("Envoi 50 %");
        chip.CancelText = n => $"Annuler {n}";
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Annuler launch-brief.pdf");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(0.0, 0)]
    [InlineData(0.077, 8)]
    [InlineData(0.154, 15)]
    [InlineData(0.385, 39)]
    [InlineData(0.994, 99)]
    [InlineData(1.0, 100)]
    [InlineData(1.7, 100)]
    [InlineData(-1.0, 0)]
    [InlineData(double.NaN, 0)]
    public void The_Percentage_Is_Rounded(double progress, int percent) => AiAttachmentChip.Percent(progress).ShouldBe(percent);

    #endregion

    #region Requests, focus, accessibility

    [AvaloniaTheory]
    [InlineData(AiAttachmentStatus.Uploading, false)]
    [InlineData(AiAttachmentStatus.Done, true)]
    [InlineData(AiAttachmentStatus.Ready, true)]
    [InlineData(AiAttachmentStatus.Error, true)]
    public void The_Button_Asks_To_Cancel_While_Uploading_And_To_Remove_Otherwise(AiAttachmentStatus status, bool remove)
    {
        var attachment = Sample("launch-brief.pdf", 480_256, status, 0.3);
        var chip = Create(attachment);
        var host = new StackPanel { Children = { chip } };
        var window = Show(host);
        var removed = new List<AiAttachment>();
        var cancelled = new List<AiAttachment>();
        // They bubble: the owner listens above the chip.
        host.AddHandler(AiAttachmentChip.RemoveRequestedEvent, (_, e) => removed.Add(e.Attachment));
        host.AddHandler(AiAttachmentChip.CancelRequestedEvent, (_, e) => cancelled.Add(e.Attachment));
        chip.ActionButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        (remove ? removed : cancelled).ShouldBe([attachment]);
        (remove ? cancelled : removed).ShouldBeEmpty();
        // The chip decides nothing: the attachment is as it was.
        attachment.Status.ShouldBe(status);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Click_Raises_The_Request_With_The_Attachment()
    {
        var attachment = Sample("sprint-15-review.pdf", 480_256);
        var chip = Create(attachment);
        var window = Show(chip);
        AiAttachmentRequestEventArgs? seen = null;
        chip.RemoveRequested += (_, e) => seen = e;
        var center = chip.ActionButton!.TranslatePoint(new Point(14, 14), window)!.Value;
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        seen.ShouldNotBeNull();
        seen!.Attachment.ShouldBeSameAs(attachment);
        seen.Source.ShouldBeSameAs(chip);
        window.Close();
    }

    [AvaloniaFact]
    public void Only_The_Button_Takes_Focus_And_The_Ring_Shows_For_The_Keyboard()
    {
        var before = new Avalonia.Controls.Button { Content = "Before" };
        var chip = Create(Sample("sprint-15-review.pdf", 480_256));
        var window = Show(new StackPanel { Children = { before, chip } });
        chip.Focusable.ShouldBeFalse();
        var button = chip.ActionButton!;
        var ring = Part<Border>(button, "PART_FocusRing");
        before.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        button.IsFocused.ShouldBeTrue();
        ring.IsVisible.ShouldBeTrue();
        ring.Margin.ShouldBe(new Thickness(-4));
        ring.BorderThickness.ShouldBe(new Thickness(2));
        button.Focus(NavigationMethod.Pointer);
        before.Focus(NavigationMethod.Pointer);
        button.Focus(NavigationMethod.Pointer);
        ring.IsVisible.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void The_Mouse_Fills_The_Button_And_Darkens_Its_Glyph()
    {
        var chip = Create(Sample("sprint-15-review.pdf", 480_256));
        var window = Show(chip);
        var button = chip.ActionButton!;
        var fill = Part<Border>(button, "PART_Fill");
        fill.Opacity.ShouldBe(0);
        TestHelpers.ColorOf(fill.Background).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.Foreground));
        TestHelpers.ColorOf(Glyph(chip).Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.TextSecondary));
        var center = button.TranslatePoint(new Point(14, 14), window)!.Value;
        window.MouseMove(center, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        button.Classes.ShouldContain(AiAttachmentChipButton.HoverPseudoClass);
        fill.Opacity.ShouldBe(0.06);
        TestHelpers.ColorOf(Glyph(chip).Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.Foreground));
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        fill.Opacity.ShouldBe(0.1);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(new Point(390, 290), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        button.Classes.ShouldNotContain(AiAttachmentChipButton.HoverPseudoClass);
        fill.Opacity.ShouldBe(0);
        // The chip itself has no hover look.
        Part<Border>(chip, AiAttachmentChip.FramePart).Background.ShouldBe(chip.Background);
        window.Close();
    }

    [AvaloniaFact]
    public void Accessibility_Names_The_Chip_By_Its_File_And_Describes_It_By_The_Meta()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.25);
        var chip = Create(attachment);
        var window = Show(chip);
        AutomationProperties.GetName(chip).ShouldBe("launch-brief.pdf");
        AutomationProperties.GetHelpText(chip).ShouldBe("25% uploaded");
        Tooltip.GetTip(chip).ShouldBe("launch-brief.pdf");
        var peer = ControlAutomationPeer.CreatePeerForElement(chip);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.ListItem);
        peer.GetName().ShouldBe("launch-brief.pdf");
        var buttonPeer = ControlAutomationPeer.CreatePeerForElement(chip.ActionButton!);
        buttonPeer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        buttonPeer.GetName().ShouldBe("Stop uploading launch-brief.pdf");
        attachment.Status = AiAttachmentStatus.Ready;
        AutomationProperties.GetHelpText(chip).ShouldBe("PDF, 469 KB");
        window.Close();
    }

    #endregion

    #region Following the attachment

    [AvaloniaFact]
    public void Out_Of_The_Window_The_Chip_Lets_Go_And_Catches_Up_On_Return()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.2);
        var chip = Create(attachment);
        var host = new Border { Child = chip };
        var window = Show(host);
        host.Child = null;
        Dispatcher.UIThread.RunJobs();
        attachment.Status = AiAttachmentStatus.Error;
        attachment.Error = "Storage is full";
        chip.Classes.ShouldContain(AiAttachmentChip.UploadingPseudoClass);
        host.Child = chip;
        Dispatcher.UIThread.RunJobs();
        chip.Classes.ShouldContain(AiAttachmentChip.ErrorPseudoClass);
        chip.MetaText.ShouldBe("Storage is full");
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Remove);
        window.Close();
    }

    [AvaloniaFact]
    public void A_New_Attachment_Lets_Go_Of_The_Old_One()
    {
        var first = Sample("launch-brief.pdf", 480_256);
        var second = Sample("usage-oct.csv", 12 * 1024);
        var chip = Create(first);
        var window = Show(chip);
        chip.Attachment = second;
        Part<TextBlock>(chip, AiAttachmentChip.NamePart).Text.ShouldBe("usage-oct.csv");
        first.Status = AiAttachmentStatus.Error;
        chip.Classes.ShouldNotContain(AiAttachmentChip.ErrorPseudoClass);
        chip.Attachment = null;
        Part<TextBlock>(chip, AiAttachmentChip.NamePart).Text.ShouldBe(string.Empty);
        chip.MetaText.ShouldBe(string.Empty);
        window.Close();
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void Full_Motion_Turns_The_Ring_Into_A_Check_On_A_Spring()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.9);
        var chip = Create(attachment);
        var window = Show(chip, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var glyph = Glyph(chip);
        attachment.Progress = 1;
        attachment.Status = AiAttachmentStatus.Done;
        // First frame: the ring is still there, the check starts at 0.5.
        CoreFrames.Run(clock, 1);
        glyph.CheckLayer.Scale.ShouldBeInRange(0.5, 0.75);
        glyph.RingLayer.Opacity.ShouldBeInRange(0.5, 1);
        // The ring is gone after its 120ms; the check is in.
        clock.Advance(TimeSpan.FromMilliseconds(130));
        glyph.RingLayer.Opacity.ShouldBe(0);
        glyph.RingLayer.Scale.ShouldBe(0.6, 0.001);
        glyph.RingLayer.Visual.IsVisible.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1));
        glyph.CheckLayer.Opacity.ShouldBe(1);
        glyph.CheckLayer.Scale.ShouldBe(1, 0.001);
        // The meta line crossfaded: two layers during the swap, one after.
        WaitFor(() => MetaLayers(chip) == 1).ShouldBeTrue();
        chip.MetaText.ShouldBe("PDF, 469 KB");
        window.Close();
    }

    [AvaloniaFact]
    public void Full_Motion_Turns_The_Check_Into_An_X_From_Minus_45_Degrees()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Done, 1);
        var chip = Create(attachment);
        var window = Show(chip, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var glyph = Glyph(chip);
        attachment.Status = AiAttachmentStatus.Ready;
        CoreFrames.Run(clock, 1);
        glyph.RemoveLayer.Angle.ShouldBeInRange(-45, -30);
        glyph.RemoveLayer.Scale.ShouldBeInRange(0.6, 0.75);
        var angles = new List<double>();
        for (var i = 0; i < 40; i++)
        {
            CoreFrames.Run(clock, 1);
            angles.Add(glyph.RemoveLayer.Angle);
        }
        glyph.RemoveLayer.Angle.ShouldBe(0, 0.01);
        glyph.RemoveLayer.Scale.ShouldBe(1, 0.01);
        glyph.RemoveLayer.Opacity.ShouldBe(1);
        // A little bounce: the turn passes zero once and comes back.
        angles.Max().ShouldBeGreaterThan(0);
        angles.Max().ShouldBeLessThan(5);
        glyph.CheckLayer.Opacity.ShouldBe(0);
        glyph.CheckLayer.Scale.ShouldBe(0.6, 0.001);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Swap_Caught_Midway_Turns_Back_From_Where_It_Is()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Done, 1);
        var chip = Create(attachment);
        var window = Show(chip, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var glyph = Glyph(chip);
        attachment.Status = AiAttachmentStatus.Ready;
        CoreFrames.Run(clock, 3);
        var midway = glyph.CheckLayer.Opacity;
        midway.ShouldBeInRange(0.01, 0.99);
        var shrunk = glyph.CheckLayer.Scale;
        shrunk.ShouldBeInRange(0.6, 1);
        // Back to done (an app that resets the status): the check comes back from where it is (keeping its speed for a
        // moment), not from hidden and not from the 0.5 an entrance starts at.
        attachment.Status = AiAttachmentStatus.Done;
        CoreFrames.Run(clock, 1);
        glyph.CheckLayer.Opacity.ShouldBeGreaterThan(midway * 0.6);
        glyph.CheckLayer.Scale.ShouldBeGreaterThan(0.6);
        clock.Advance(TimeSpan.FromSeconds(1));
        glyph.CheckLayer.Opacity.ShouldBe(1);
        glyph.RemoveLayer.Opacity.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_Without_Scale_Or_Turn()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Done, 1);
        var chip = Create(attachment);
        var window = Show(chip, ArcMotionLevel.Reduced);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var glyph = Glyph(chip);
        attachment.Status = AiAttachmentStatus.Ready;
        clock.Advance(TimeSpan.FromMilliseconds(60));
        glyph.RemoveLayer.Opacity.ShouldBeInRange(0.3, 0.7);
        glyph.RemoveLayer.Scale.ShouldBe(1);
        glyph.RemoveLayer.Angle.ShouldBe(0);
        glyph.CheckLayer.Scale.ShouldBe(1);
        glyph.CheckLayer.Opacity.ShouldBeInRange(0.3, 0.7);
        clock.Advance(TimeSpan.FromMilliseconds(80));
        glyph.RemoveLayer.Opacity.ShouldBe(1);
        glyph.CheckLayer.Opacity.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Without_Motion_Everything_Swaps_At_Once()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.5);
        var chip = Create(attachment);
        var window = Show(chip);
        attachment.Status = AiAttachmentStatus.Done;
        Glyph(chip).CheckLayer.Opacity.ShouldBe(1);
        Glyph(chip).RingLayer.Opacity.ShouldBe(0);
        MetaLayers(chip).ShouldBe(1);
        attachment.Status = AiAttachmentStatus.Error;
        Glyph(chip).RemoveLayer.Opacity.ShouldBe(1);
        Glyph(chip).CheckLayer.Opacity.ShouldBe(0);
        MetaLayers(chip).ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Another_Attachment_Swaps_In_Without_Motion()
    {
        var chip = Create(Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.5));
        var window = Show(chip, ArcMotionLevel.Full);
        chip.Attachment = Sample("sprint-15-review.pdf", 480_256);
        MetaLayers(chip).ShouldBe(1);
        Glyph(chip).RemoveLayer.Opacity.ShouldBe(1);
        Glyph(chip).RingLayer.Opacity.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Ring_Smooths_Progress_Only_At_Full_Motion()
    {
        var attachment = Sample("launch-brief.pdf", 480_256, AiAttachmentStatus.Uploading, 0.2);
        var chip = Create(attachment);
        var window = Show(chip, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var ring = Glyph(chip).Ring;
        clock.Advance(TimeSpan.FromSeconds(1));
        attachment.Progress = 0.6;
        CoreFrames.Run(clock, 1);
        ring.DisplayedValue.ShouldBeInRange(0.21, 0.59);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        ring.DisplayedValue.ShouldBe(0.6, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Buttons_Colors_Move_Only_With_Motion()
    {
        var full = Create(Sample("a.pdf", 1));
        var reduced = Create(Sample("b.pdf", 1));
        var window = Show(new StackPanel { Children = { full } }, ArcMotionLevel.Full);
        var other = Show(new StackPanel { Children = { reduced } }, ArcMotionLevel.Reduced);
        full.ActionButton!.Transitions!.Count.ShouldBe(1);
        Part<Border>(full.ActionButton, "PART_Fill").Transitions!.Count.ShouldBe(1);
        reduced.ActionButton!.Transitions!.Count.ShouldBe(1);
        var off = Create(Sample("c.pdf", 1));
        var third = Show(off);
        (off.ActionButton!.Transitions is null || off.ActionButton.Transitions.Count == 0).ShouldBeTrue();
        window.Close();
        other.Close();
        third.Close();
    }

    #endregion

    #region Compact (the AI chat's chip)

    private static AiAttachmentChip CreateCompact(AiAttachment attachment)
    {
        var chip = Create(attachment);
        chip.Appearance = AiAttachmentChipAppearance.Compact;
        return chip;
    }

    [AvaloniaFact]
    public void Compact_Is_A_32px_Pill_On_SurfaceMuted_With_A_14px_Icon_And_A_24px_Button()
    {
        var chip = CreateCompact(Sample("stage-timings.csv", 1229));
        var window = Show(chip);
        chip.Classes.ShouldContain(AiAttachmentChip.CompactPseudoClass);
        chip.Bounds.Height.ShouldBe(32);
        var frame = Part<Border>(chip, AiAttachmentChip.FramePart);
        frame.CornerRadius.ShouldBe(new CornerRadius(12));
        frame.Padding.ShouldBe(new Thickness(10, 0, 4, 0));
        frame.BorderThickness.ShouldBe(new Thickness(0));
        TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.SurfaceMuted));

        var icon = Part<ArcGlyph>(chip, AiAttachmentChip.IconPart);
        icon.Size.ShouldBe(14);
        icon.StrokeThickness.ShouldBe(1.75);
        icon.Data.ShouldBeSameAs(ArcIcons.FileText);
        icon.TranslatePoint(default, chip)!.Value.ShouldBe(new Point(10, 9));
        TestHelpers.ColorOf(icon.Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.TextSecondary));

        // Icon, name, size and button 6 apart; the button 4 from the right edge, centred.
        var name = Part<TextBlock>(chip, AiAttachmentChip.NamePart);
        name.TranslatePoint(default, chip)!.Value.X.ShouldBe(10 + 14 + 6);
        name.MaxWidth.ShouldBe(200);
        var meta = Part<ArcContentSwap>(chip, AiAttachmentChip.MetaPart);
        meta.TranslatePoint(default, chip)!.Value.X.ShouldBe(10 + 14 + 6 + name.Bounds.Width + 6, 0.01);
        var button = chip.ActionButton!;
        button.Bounds.Size.ShouldBe(new Size(24, 24));
        button.TranslatePoint(default, chip)!.Value.ShouldBe(new Point(chip.Bounds.Width - 4 - 24, 4));
        Part<Border>(button, "PART_Face").CornerRadius.ShouldBe(new CornerRadius(8));
        Part<Border>(button, "PART_FocusRing").CornerRadius.ShouldBe(new CornerRadius(12));
        TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.TextMuted));
        var glyph = Glyph(chip);
        glyph.IsCompact.ShouldBeTrue();
        glyph.Bounds.Size.ShouldBe(new Size(16, 16));
        glyph.Ring.Width.ShouldBe(14);
        glyph.Ring.Thickness.ShouldBe(1.75);
        chip.Bounds.Width.ShouldBe(10 + 14 + 6 + name.Bounds.Width + 6 + meta.Bounds.Width + 6 + 24 + 4, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Compact_Shows_The_Size_In_Tabular_Figures_And_The_File_Icon_For_Pictures()
    {
        var picture = Sample("pipeline.png", 84 * 1024, kind: AiAttachmentKind.Image);
        picture.Thumbnail = Thumbnail();
        var chip = CreateCompact(picture);
        var window = Show(chip);
        chip.Classes.ShouldNotContain(AiAttachmentChip.ThumbnailPseudoClass);
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBeSameAs(ArcIcons.FileText);
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).IsVisible.ShouldBeTrue();
        chip.MetaText.ShouldBe("84 KB");
        chip.MetaLine!.IsTabular.ShouldBeTrue();
        chip.MetaLine.Classes.ShouldContain(AiAttachmentChipMeta.TabularPseudoClass);
        TestHelpers.Descendant<TextBlock>(chip.MetaLine, "PART_Text").FontFeatures!.Select(f => f.Tag).ShouldContain("tnum");

        // A pasted text keeps its clipboard and lines; an attachment's own icon and meta (no size) win.
        var pasted = AiFileIntake.FromPastedText("one\ntwo\nthree", "Pasted text");
        chip.Attachment = pasted;
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBeSameAs(ArcIcons.ClipboardPaste);
        chip.MetaText.ShouldBe("3 lines of text");
        var source = Sample("Budget forecast", null);
        source.Meta = "Spreadsheet, 380 KB";
        source.Icon = ArcIcons.FileSpreadsheet;
        chip.Attachment = source;
        Part<ArcGlyph>(chip, AiAttachmentChip.IconPart).Data.ShouldBeSameAs(ArcIcons.FileSpreadsheet);
        chip.MetaText.ShouldBe("Spreadsheet, 380 KB");
        window.Close();
    }

    [AvaloniaFact]
    public void Compact_Keeps_The_States_Uploading_Error_And_Their_Names()
    {
        var attachment = Sample("brief.pdf", 470 * 1024, AiAttachmentStatus.Uploading, 0.4);
        var chip = CreateCompact(attachment);
        var window = Show(chip);
        chip.MetaText.ShouldBe("40% uploaded");
        chip.MetaLine!.IsTabular.ShouldBeFalse();
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Stop uploading brief.pdf");
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Ring);

        attachment.Status = AiAttachmentStatus.Error;
        attachment.Error = "Over 30 MB";
        chip.Classes.ShouldContain(AiAttachmentChip.ErrorPseudoClass);
        Part<Border>(chip, "PART_ErrorEdge").IsVisible.ShouldBeTrue();
        Part<Border>(chip, "PART_ErrorEdge").BorderThickness.ShouldBe(new Thickness(1));
        chip.MetaText.ShouldBe("Over 30 MB");
        chip.MetaLine!.IsError.ShouldBeTrue();
        AutomationProperties.GetName(chip.ActionButton!).ShouldBe("Take out brief.pdf");
        Shown(chip).ShouldBe(AiAttachmentStatusGlyph.Shown.Remove);
        window.Close();
    }

    [AvaloniaFact]
    public void Compact_Ellipsizes_A_Long_Name_At_200_And_Its_Glyph_Darkens_Under_The_Mouse()
    {
        var chip = CreateCompact(Sample("a-really-long-file-name-for-the-quarterly-report-final-v3.xlsx", 2048));
        var window = Show(chip);
        Part<TextBlock>(chip, AiAttachmentChip.NamePart).Bounds.Width.ShouldBeLessThanOrEqualTo(200);
        var button = chip.ActionButton!;
        var center = button.TranslatePoint(new Point(12, 12), window)!.Value;
        window.MouseMove(center, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.Foreground));
        window.MouseMove(new Point(1, 1), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(chip, ArcDesignTokenKind.TextMuted));
        window.Close();
    }

    [AvaloniaFact]
    public void The_Appearance_Can_Change_Back_And_Forth()
    {
        var chip = Create(Sample("roadmap.pdf", 4096));
        var window = Show(chip);
        chip.Bounds.Size.ShouldBe(new Size(212, 52));
        chip.Appearance = AiAttachmentChipAppearance.Compact;
        window.UpdateLayout();
        chip.Bounds.Height.ShouldBe(32);
        chip.MetaText.ShouldBe("4 KB");
        chip.ActionButton!.Bounds.Size.ShouldBe(new Size(24, 24));
        chip.Appearance = AiAttachmentChipAppearance.Card;
        window.UpdateLayout();
        chip.Bounds.Size.ShouldBe(new Size(212, 52));
        chip.MetaText.ShouldBe("PDF, 4 KB");
        chip.ActionButton!.Bounds.Size.ShouldBe(new Size(28, 28));
        Glyph(chip).IsCompact.ShouldBeFalse();
        window.Close();
    }

    #endregion
}
