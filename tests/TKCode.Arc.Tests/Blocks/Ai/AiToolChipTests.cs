using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiToolChip"/>: the three states (icons, names, marks, summary), toggling and its limits, the hover and open looks,
/// in-place updates versus face swaps, automation, and the spinner and width at each motion level.
/// </summary>
public class AiToolChipTests
{
    private static AiToolChip Chip(AiChatTool tool) => new() { Tool = tool };

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static AiToolChipFace Face(AiToolChip chip) => chip.Face.ShouldNotBeNull();

    [AvaloniaFact]
    public void Chip_Is_A_30px_Bordered_Pill_On_Surface()
    {
        using var rig = new AiReplyRig(Chip(AiReplySamples.Search()));
        var chip = (AiToolChip)rig.Content;
        chip.Bounds.Height.ShouldBe(30);
        chip.Padding.ShouldBe(new Thickness(10, 0, 12, 0));
        var frame = TestHelpers.Descendant<Border>(chip, "PART_Frame");
        frame.BorderThickness.ShouldBe(new Thickness(1));
        TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(Token(chip, ArcDesignTokenKind.Border));
        TestHelpers.ColorOf(frame.Background).ShouldBe(Token(chip, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(chip.Foreground).ShouldBe(Token(chip, ArcDesignTokenKind.TextSecondary));
        // The icon cell 10px in, the name 8px after it, the last text 12px before the edge.
        var cell = TestHelpers.Descendant<Panel>(chip, "PART_IconCell");
        cell.TranslatePoint(default, chip)!.Value.X.ShouldBe(10, 0.01);
        cell.Bounds.Width.ShouldBe(14);
        var name = TestHelpers.Descendant<TextBlock>(Face(chip), "PART_Name");
        name.TranslatePoint(default, chip)!.Value.X.ShouldBe(32, 0.01);
        var summary = TestHelpers.Descendant<TextBlock>(Face(chip), "PART_Summary");
        (summary.TranslatePoint(default, chip)!.Value.X + summary.Bounds.Width + 12).ShouldBe(chip.Bounds.Width, 0.6);
    }

    [AvaloniaFact]
    public void Running_Spins_Shimmers_And_Cannot_Open()
    {
        var tool = AiReplySamples.Search(AiChatToolStatus.Running);
        using var rig = new AiReplyRig(Chip(tool));
        var chip = (AiToolChip)rig.Content;
        var face = Face(chip);
        face.Status.ShouldBe(AiChatToolStatus.Running);
        chip.Classes.ShouldContain(AiToolChip.RunningPseudoClass);
        chip.IsEffectivelyEnabled.ShouldBeFalse();
        TestHelpers.Descendant<AiToolSpinner>(face, "PART_Spinner").IsVisible.ShouldBeTrue();
        var shimmer = TestHelpers.Descendant<TextShimmer>(face, "PART_Shimmer");
        shimmer.IsVisible.ShouldBeTrue();
        shimmer.IsActive.ShouldBeTrue();
        shimmer.Text.ShouldBe("Searching the docs");
        TestHelpers.Descendant<TextBlock>(face, "PART_Name").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<TextBlock>(face, "PART_Summary").IsVisible.ShouldBeFalse();
        rig.Click(chip);
        chip.IsExpanded.ShouldBeFalse();
        var peer = ControlAutomationPeer.CreatePeerForElement(chip);
        peer.GetProvider<IExpandCollapseProvider>()!.ExpandCollapseState.ShouldBe(ExpandCollapseState.LeafNode);
        peer.IsEnabled().ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Done_Search_Shows_Search_Glyph_Name_One_Mark_Per_Site_And_Summary()
    {
        using var rig = new AiReplyRig(Chip(AiReplySamples.Search()));
        var chip = (AiToolChip)rig.Content;
        var face = Face(chip);
        chip.IsEffectivelyEnabled.ShouldBeTrue();
        var icon = TestHelpers.Descendant<ContentPresenter>(face, "PART_Icon").Content.ShouldBeOfType<ArcGlyph>();
        icon.Data.ShouldBeSameAs(ArcIcons.Search);
        icon.Size.ShouldBe(14);
        TestHelpers.Descendant<TextBlock>(face, "PART_Name").Text.ShouldBe("Searched the docs");
        // Four sources from three sites: three marks, 13px, 3px apart.
        face.MarkSources.Select(s => s.Domain).ShouldBe(["docs.example.org", "blog.example.dev", "handbook.example.com"]);
        var marks = TestHelpers.Descendant<StackPanel>(face, "PART_Marks");
        marks.IsVisible.ShouldBeTrue();
        marks.Spacing.ShouldBe(3);
        marks.Children.OfType<AiToolSourceMark>().Select(m => m.Size).ShouldBe([13.0, 13, 13]);
        var summary = TestHelpers.Descendant<TextBlock>(face, "PART_Summary");
        summary.Text.ShouldBe("4 sources");
        TestHelpers.ColorOf(summary.Foreground).ShouldBe(Token(chip, ArcDesignTokenKind.TextMuted));
        summary.FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");
        AutomationProperties.GetName(chip).ShouldBe("Searched the docs");
        AutomationProperties.GetHelpText(chip).ShouldBe("4 sources");
    }

    [AvaloniaFact]
    public void Marks_Stop_At_Three_Sites_And_Only_Searches_Show_Them()
    {
        var tool = AiReplySamples.Search();
        tool.Sources.Add(new AiSource { Index = 5, Title = "Fifth", Domain = "fifth.example.net" });
        AiToolChip.MarksFor(tool).Count.ShouldBe(AiToolChip.MaxMarks);
        AiToolChip.MarksFor(AiReplySamples.Query()).ShouldBeEmpty();
        AiToolChip.MarksFor(AiReplySamples.Search(AiChatToolStatus.Running)).ShouldBeEmpty();
        // Without domains, sources with the same mark are one site.
        var shared = Geometry.Parse("M0 0h24v24H0z");
        var anonymous = new AiChatTool { Kind = AiChatToolKind.Search, Status = AiChatToolStatus.Done };
        anonymous.Sources.Add(new AiSource { Title = "A", Mark = shared });
        anonymous.Sources.Add(new AiSource { Title = "B", Mark = shared });
        AiToolChip.MarksFor(anonymous).Count.ShouldBe(1);
        AiToolSourceMark.FallbackLetter(new AiSource { Domain = "www.handbook.example.com" }).ShouldBe("H");
        AiToolSourceMark.FallbackLetter(new AiSource()).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Done_Query_Defaults_To_Database_And_Takes_An_Icon_Of_Its_Own()
    {
        var tool = AiReplySamples.Query();
        using var rig = new AiReplyRig(Chip(tool));
        var chip = (AiToolChip)rig.Content;
        TestHelpers.Descendant<ContentPresenter>(Face(chip), "PART_Icon").Content.ShouldBeOfType<ArcGlyph>().Data.ShouldBeSameAs(ArcIcons.Database);
        TestHelpers.Descendant<StackPanel>(Face(chip), "PART_Marks").IsVisible.ShouldBeFalse();

        tool.Icon = ArcIcons.Cpu;
        rig.Settle();
        TestHelpers.Descendant<ContentPresenter>(Face(chip), "PART_Icon").Content.ShouldBeOfType<ArcGlyph>().Data.ShouldBeSameAs(ArcIcons.Cpu);

        var bitmap = new WriteableBitmap(new PixelSize(4, 4), new Vector(96, 96));
        tool.Icon = bitmap;
        rig.Settle();
        TestHelpers.Descendant<ContentPresenter>(Face(chip), "PART_Icon").Content.ShouldBeOfType<Image>().Source.ShouldBeSameAs(bitmap);

        var mark = new Border { Width = 14, Height = 14 };
        tool.Icon = mark;
        rig.Settle();
        TestHelpers.Descendant<ContentPresenter>(Face(chip), "PART_Icon").Content.ShouldBeSameAs(mark);
        // The owner's control moves to the new face when the state changes.
        tool.Status = AiChatToolStatus.Running;
        rig.Settle();
        tool.Status = AiChatToolStatus.Done;
        rig.Settle();
        TestHelpers.Descendant<ContentPresenter>(Face(chip), "PART_Icon").Content.ShouldBeSameAs(mark);
    }

    [AvaloniaFact]
    public void Error_Shows_A_Danger_Alert_The_Running_Name_And_The_Reason()
    {
        using var rig = new AiReplyRig(Chip(AiReplySamples.Query(AiChatToolStatus.Error)));
        var chip = (AiToolChip)rig.Content;
        var face = Face(chip);
        var alert = TestHelpers.Descendant<ArcGlyph>(face, "PART_Alert");
        alert.IsVisible.ShouldBeTrue();
        alert.Data.ShouldBeSameAs(ArcIcons.CircleAlert);
        TestHelpers.ColorOf(alert.Foreground).ShouldBe(Token(chip, ArcDesignTokenKind.Danger));
        TestHelpers.Descendant<ContentPresenter>(face, "PART_Icon").Content.ShouldBeNull();
        TestHelpers.Descendant<TextBlock>(face, "PART_Name").Text.ShouldBe("Querying ci-runner-7");
        TestHelpers.Descendant<TextBlock>(face, "PART_Summary").Text.ShouldBe("Timed out");
        chip.IsEffectivelyEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Press_Enter_And_Space_Toggle_Two_Way()
    {
        var chip = Chip(AiReplySamples.Search());
        var changes = 0;
        chip.PropertyChanged += (_, e) =>
        {
            if (e.Property == AiToolChip.IsExpandedProperty)
            {
                changes++;
            }
        };
        using var rig = new AiReplyRig(chip);
        rig.Click(chip);
        chip.IsExpanded.ShouldBeTrue();
        chip.Classes.ShouldContain(AiToolChip.ExpandedPseudoClass);
        chip.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        chip.IsExpanded.ShouldBeFalse();
        rig.Key(PhysicalKey.Space);
        chip.IsExpanded.ShouldBeTrue();
        changes.ShouldBe(3);
        var expand = ControlAutomationPeer.CreatePeerForElement(chip).GetProvider<IExpandCollapseProvider>()!;
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        expand.Collapse();
        chip.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Hover_Strengthens_The_Edge_And_Open_Fills_It_Muted()
    {
        using var rig = new AiReplyRig(Chip(AiReplySamples.Search()));
        var chip = (AiToolChip)rig.Content;
        var frame = TestHelpers.Descendant<Border>(chip, "PART_Frame");
        rig.Hover(chip);
        chip.Classes.ShouldContain(AiToolChip.HoverPseudoClass);
        TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(Token(chip, ArcDesignTokenKind.BorderStrong));
        TestHelpers.ColorOf(chip.Foreground).ShouldBe(Token(chip, ArcDesignTokenKind.Foreground));
        rig.Window.MouseMove(new Point(700, 500), RawInputModifiers.None);
        rig.Settle();
        chip.IsExpanded = true;
        rig.Settle();
        TestHelpers.ColorOf(frame.Background).ShouldBe(Token(chip, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(Token(chip, ArcDesignTokenKind.Border));
        TestHelpers.ColorOf(chip.Foreground).ShouldBe(Token(chip, ArcDesignTokenKind.Foreground));
    }

    [AvaloniaFact]
    public void Running_Chip_Shows_No_Hover()
    {
        using var rig = new AiReplyRig(Chip(AiReplySamples.Search(AiChatToolStatus.Running)));
        var chip = (AiToolChip)rig.Content;
        rig.Hover(chip);
        chip.Classes.ShouldNotContain(AiToolChip.HoverPseudoClass);
    }

    [AvaloniaFact]
    public void A_New_State_Swaps_The_Face_And_A_New_Summary_Updates_In_Place()
    {
        var tool = AiReplySamples.Search(AiChatToolStatus.Running);
        using var rig = new AiReplyRig(Chip(tool));
        var chip = (AiToolChip)rig.Content;
        var running = Face(chip);
        tool.Summary = "searching";
        rig.Settle();
        Face(chip).ShouldBeSameAs(running);

        tool.Status = AiChatToolStatus.Done;
        rig.Settle();
        var done = Face(chip);
        done.ShouldNotBeSameAs(running);
        done.Status.ShouldBe(AiChatToolStatus.Done);
        done.Label.ShouldBe("Searched the docs");

        foreach (var source in AiReplySamples.Sources())
        {
            tool.Sources.Add(source);
        }
        tool.Summary = "4 sources";
        rig.Settle();
        Face(chip).ShouldBeSameAs(done);
        done.Summary.ShouldBe("4 sources");
        done.MarkSources.Count.ShouldBe(3);
        chip.IsEffectivelyEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Chip_Lets_Go_Of_Its_Tool_Out_Of_A_Window()
    {
        var tool = AiReplySamples.Search(AiChatToolStatus.Running);
        var chip = Chip(tool);
        var rig = new AiReplyRig(chip);
        var panel = (StackPanel)chip.Parent!;
        panel.Children.Remove(chip);
        rig.Settle();
        tool.Status = AiChatToolStatus.Done;
        // Back in a window, it shows where the tool is now.
        panel.Children.Add(chip);
        rig.Settle();
        Face(chip).Status.ShouldBe(AiChatToolStatus.Done);
        rig.Dispose();
    }

    [AvaloniaFact]
    public void Spinner_Turns_At_Full_Motion_Only()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using (var rig = new AiReplyRig(Chip(AiReplySamples.Search(AiChatToolStatus.Running)), ArcMotionLevel.Full, clock))
        {
            var spinner = TestHelpers.Descendant<AiToolSpinner>(rig.Content, "PART_Spinner");
            spinner.IsSpinning.ShouldBeTrue();
            var before = spinner.Angle;
            rig.Frames(450);
            // 900ms a turn: about half a turn later.
            ((spinner.Angle - before + 360) % 360).ShouldBe(180, 12);
        }
        using (var rig = new AiReplyRig(Chip(AiReplySamples.Search(AiChatToolStatus.Running)), ArcMotionLevel.Reduced, clock))
        {
            var spinner = TestHelpers.Descendant<AiToolSpinner>(rig.Content, "PART_Spinner");
            rig.Frames(200);
            spinner.IsSpinning.ShouldBeFalse();
            spinner.Angle.ShouldBe(0);
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Width_Springs_To_The_Done_Face()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var tool = AiReplySamples.Search(AiChatToolStatus.Running);
        using var rig = new AiReplyRig(Chip(tool), ArcMotionLevel.Full, clock);
        var chip = (AiToolChip)rig.Content;
        var before = chip.Bounds.Width;
        foreach (var source in AiReplySamples.Sources())
        {
            tool.Sources.Add(source);
        }
        tool.Summary = "4 sources";
        tool.Status = AiChatToolStatus.Done;
        rig.Settle();
        rig.Frames(60);
        var size = TestHelpers.Descendant<SpringSizeDecorator>(chip, "PART_FaceSize");
        size.IsMoving.ShouldBeTrue();
        chip.Bounds.Width.ShouldBeGreaterThan(before);
        rig.Frames(700);
        size.IsMoving.ShouldBeFalse();
        chip.Bounds.Width.ShouldBe(10 + Face(chip).DesiredSize.Width + 12, 0.6);
    }
}
