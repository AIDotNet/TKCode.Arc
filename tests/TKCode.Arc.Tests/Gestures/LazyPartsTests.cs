using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Xunit;

namespace TKCode.Arc.Tests;

/// <summary>Focus rings built on demand must look like the template rings they replaced; enums reach the themes as pseudo-classes.</summary>
public class LazyPartsTests
{
    private static Border? Ring(Visual control) =>
        control.GetVisualDescendants().OfType<Border>().FirstOrDefault(c => c.Name == LazyFocusRing.PartName);

    private static void AssertRing(InputElement control, Thickness margin, ArcDesignTokenKind radius)
    {
        Ring(control).ShouldBeNull();
        control.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        var ring = Ring(control);
        ring.ShouldNotBeNull();
        ring.IsVisible.ShouldBeTrue();
        ring.IsHitTestVisible.ShouldBeFalse();
        ring.Margin.ShouldBe(margin);
        ring.CornerRadius.ShouldBe(ArcTokens.GetCornerRadius((Control)control, radius, default));
        // Last child of the template panel, so it draws above everything else in the control.
        ((Panel)ring.Parent!).Children[^1].ShouldBeSameAs(ring);
    }

    [AvaloniaFact]
    public void Pill_Builds_Its_Ring_On_Keyboard_Focus_And_Hides_It_After()
    {
        var pill = new ReactionsPill();
        var other = new Avalonia.Controls.Button();
        var window = TestHelpers.Show(new StackPanel { Children = { pill, other } });
        try
        {
            AssertRing(pill, new Thickness(-4), ArcDesignTokenKind.RadiusPill);
            other.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Ring(pill)!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Illustration_Ring_Keeps_Its_Margin_And_Radius()
    {
        var illustration = new DataFlow();
        var window = TestHelpers.Show(illustration);
        try
        {
            AssertRing(illustration, new Thickness(-3), ArcDesignTokenKind.RadiusSurface);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Round_Media_Button_Ring_Is_A_Pill_And_Shape_And_Tone_Are_Pseudo_Classes()
    {
        var button = new MediaButton { Shape = MediaButtonShape.Round, Tone = MediaButtonTone.Quiet };
        var window = TestHelpers.Show(button);
        try
        {
            button.Classes.Contains(":round").ShouldBeTrue();
            button.Classes.Contains(":wide").ShouldBeFalse();
            button.Classes.Contains(":quiet").ShouldBeTrue();
            button.Height.ShouldBe(ArcTokens.Get<double>(button, ArcDesignTokenKind.ControlSizeSM));
            AssertRing(button, new Thickness(-4), ArcDesignTokenKind.RadiusPill);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Levels_Are_Pseudo_Classes_That_Follow_The_Property()
    {
        var reveal = new TextReveal();
        reveal.Classes.Contains(":h2").ShouldBeTrue();
        reveal.Level = TextLevel.Paragraph;
        reveal.Classes.Contains(":h2").ShouldBeFalse();
        reveal.Classes.Contains(":paragraph").ShouldBeTrue();
        var title = new InViewTitle { Level = TextLevel.H1 };
        title.Classes.Contains(":h1").ShouldBeTrue();
    }
}
