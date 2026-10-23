using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The options the chat added to shared parts: <see cref="ModelMenuLayout.Simple"/> (the chat's model menu),
/// <see cref="ModelPillAppearance.Header"/> (the chat's model button) and <see cref="AiReplyText"/>'s revealed source and
/// its stop that keeps every released word.
/// </summary>
public class AiChatPartOptionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static ModelMenu SimpleMenu()
    {
        var menu = new ModelMenu { Layout = ModelMenuLayout.Simple, ShowMoreModels = false };
        menu.Models.AddRange(AiChatSamples.Models());
        return menu;
    }

    #region ModelMenu Simple

    [AvaloniaFact]
    public void The_Simple_Menu_Lists_Every_Model_In_One_36px_Row_With_Its_Note()
    {
        using var host = ModelMenuHost.Show(SimpleMenu());
        host.Open();
        var rows = host.Main.Rows.OfType<ModelMenuModelRow>().ToList();
        rows.Count.ShouldBe(5);
        rows.ShouldAllBe(r => r.IsSimple && !r.IsInline && !r.IsCompact);
        rows.ShouldAllBe(r => r.Bounds.Height == 36);
        rows[0].NoteText.ShouldBe("Northwind Labs");
        var texts = rows[0].GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).ToList();
        texts.Select(t => t.Text).ShouldBe(["Northwind Labs", "Orbit Pro 3"], ignoreOrder: true);
        var note = texts.First(t => t.Text == "Northwind Labs");
        note.FontSize.ShouldBe(12);
        var name = texts.First(t => t.Text == "Orbit Pro 3");
        name.FontWeight.ShouldBe(FontWeight.Regular);
        // The note ends where the check begins, on every row (the check keeps its place).
        var noteEnds = rows.Select(r => r.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == r.NoteText))
                           .Select(t => t.TranslatePoint(new Point(t.Bounds.Width, 0), host.Main)!.Value.X).ToList();
        noteEnds.ShouldAllBe(x => Math.Abs(x - noteEnds[0]) < 0.5);
    }

    [AvaloniaFact]
    public void The_Simple_Menu_Is_As_Wide_As_Its_Rows_At_Least_240_With_The_Control_Radius()
    {
        using var host = ModelMenuHost.Show(SimpleMenu());
        host.Open();
        var main = host.Main;
        main.IsSimple.ShouldBeTrue();
        main.CornerRadius.ShouldBe(new CornerRadius(18));
        main.Bounds.Width.ShouldBeGreaterThanOrEqualTo(240);
        // Sized by its rows (the headless font is wider than Inter, so no fixed number here).
        double.IsNaN(main.Width).ShouldBeTrue();
        main.MinWidth.ShouldBe(240);
        var check = host.ModelRow(main, "orbit-pro").GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_SimpleCheck");
        check.Size.ShouldBe(15);
        check.Opacity.ShouldBe(1);
        host.ModelRow(main, "quill-2").GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_SimpleCheck").Opacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Default_Menu_Keeps_Its_Featured_Rows()
    {
        var menu = SimpleMenu();
        menu.Layout = ModelMenuLayout.Featured;
        using var host = ModelMenuHost.Show(menu);
        host.Open();
        host.Main.IsSimple.ShouldBeFalse();
        host.Main.Bounds.Width.ShouldBe(304);
        host.Main.Rows.OfType<ModelMenuModelRow>().ShouldAllBe(r => !r.IsSimple && r.Bounds.Height == 54);
    }

    [AvaloniaFact]
    public void Switching_To_Simple_While_Open_Refits_The_Menu()
    {
        var menu = SimpleMenu();
        menu.Layout = ModelMenuLayout.Featured;
        using var host = ModelMenuHost.Show(menu);
        host.Open();
        menu.Layout = ModelMenuLayout.Simple;
        host.Pump();
        host.Main.IsSimple.ShouldBeTrue();
        double.IsNaN(host.Main.Width).ShouldBeTrue();
        menu.Layout = ModelMenuLayout.Featured;
        host.Pump();
        host.Main.Width.ShouldBe(304);
        host.Main.MinWidth.ShouldBe(0);
        host.Main.Rows.OfType<ModelMenuModelRow>().ShouldAllBe(r => !r.IsSimple);
    }

    [AvaloniaFact]
    public void Keys_And_Picks_Work_In_The_Simple_Menu()
    {
        using var host = ModelMenuHost.Show(SimpleMenu());
        host.Open(ModelMenuOpenFocus.First);
        host.Key(PhysicalKey.ArrowDown);
        host.Key(PhysicalKey.Enter);
        host.Menu.SelectedModel!.Id.ShouldBe("orbit-swift");
        host.Menu.IsOpen.ShouldBeFalse();
    }

    #endregion

    #region ModelPill Header

    [AvaloniaFact]
    public void The_Header_Pill_Is_34_High_Borderless_Regular_With_A_15px_Mark()
    {
        var pill = new ModelPill { Appearance = ModelPillAppearance.Header, ShowEffort = false, Model = AiChatSamples.Models()[0] };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { pill } });
        try
        {
            window.UpdateLayout();
            pill.Bounds.Height.ShouldBe(34);
            pill.BorderThickness.ShouldBe(default(Thickness));
            pill.Padding.ShouldBe(new Thickness(10, 0, 8, 0));
            TestHelpers.ColorOf(pill.Background).A.ShouldBe((byte)0);
            pill.CurrentMark!.Size.ShouldBe(15);
            var chevron = TestHelpers.Descendant<ArcGlyph>(pill, "PART_Chevron");
            chevron.Size.ShouldBe(14);
            chevron.StrokeThickness.ShouldBe(1.75);
            pill.CurrentText!.IsRegular.ShouldBeTrue();
            pill.CurrentText.Text.ShouldBe("Orbit Pro 3");
            TestHelpers.Descendant<TextBlock>(pill.CurrentText, "PART_Name").FontWeight.ShouldBe(FontWeight.Regular);

            window.MouseMove(pill.TranslatePoint(new Point(20, 17), window)!.Value, RawInputModifiers.None);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, TKCode.Arc.DesignTokens.ArcDesignTokenKind.SurfaceMuted));
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region AiReplyText revealed source

    [AvaloniaFact]
    public void The_Revealed_Source_Is_What_The_Clock_Released_And_Stop_Shows_Exactly_That()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new AiReplyText();
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        try
        {
            window.Width = 640;
            window.UpdateLayout();
            text.Sources = AiChatSamples.Sources();
            text.Push("The key changed [1] so the cache missed every time and the build compiled from scratch. ");
            for (var i = 0; i < 4; i++)
            {
                clock.Advance(Frame, Frame);
                Dispatcher.UIThread.RunJobs();
            }
            var released = text.RevealedSource;
            released.ShouldNotBeEmpty();
            "The key changed [1] so the cache missed every time and the build compiled from scratch. ".ShouldStartWith(released);
            text.Stop();
            text.RevealedSource.ShouldBe(released);
            // Everything released shows: the citation that waited for the fade is in too.
            text.HasText.ShouldBeTrue();
            if (released.Contains("[1]"))
            {
                text.ShownCitations.Count.ShouldBe(1);
            }
            text.SetText("Whole text.");
            text.RevealedSource.ShouldBe("Whole text.");
            text.Reset();
            text.RevealedSource.ShouldBe(string.Empty);
            text.HasText.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
