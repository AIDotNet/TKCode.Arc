using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Buttons;

/// <summary>Parts built on demand appear when they are needed, and not before.</summary>
public class LazyPartsTests
{
    private static bool Has(Visual root, string name) => root.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name);

    private static void AssertRing(Control host, InputElement focusTarget)
    {
        var other = new Button { Content = "Other" };
        var window = TestHelpers.Show(new StackPanel { Children = { host, other } });
        try
        {
            Has(host, "PART_FocusRing").ShouldBeFalse();
            focusTarget.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.IsFocusRingVisible(host).ShouldBeTrue();
            other.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.IsFocusRingVisible(host).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ActionButton_Ring_Is_Built_On_Keyboard_Focus()
    {
        var button = new ActionButton { Label = "Deploy" };
        AssertRing(button, button);
    }

    [AvaloniaFact]
    public void CopyButton_Ring_Is_Built_On_Keyboard_Focus()
    {
        var button = new CopyButton { Value = "x" };
        AssertRing(button, button);
    }

    [AvaloniaFact]
    public void ActionSwap_Ring_Is_Built_On_Keyboard_Focus()
    {
        var swap = new ActionSwap();
        AssertRing(swap, swap);
    }

    [AvaloniaFact]
    public void Card_Ring_Follows_The_Title()
    {
        var card = new Card { Title = "Harbour", Details = "More", Width = 240 };
        var window = TestHelpers.Show(card);
        try
        {
            Has(card, "PART_FocusRing").ShouldBeFalse();
            TestHelpers.Descendant<CardTitle>(card, "PART_Title").Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.IsFocusRingVisible(card).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Builds_Its_Footer_When_A_Status_Arrives()
    {
        var card = new Card { Title = "Harbour", Width = 240 };
        var window = TestHelpers.Show(card);
        try
        {
            Has(card, "PART_Footer").ShouldBeFalse();
            card.Status = "Updated";
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Grid>(card, "PART_Footer").IsVisible.ShouldBeTrue();
            card.Status = null;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Grid>(card, "PART_Footer").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Avatar_Builds_Its_Dot_With_The_First_Status()
    {
        var avatar = new Avatar { DisplayName = "Ada Lovelace" };
        var window = TestHelpers.Show(avatar);
        try
        {
            Has(avatar, "PART_Status").ShouldBeFalse();
            avatar.Status = AvatarStatus.Online;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Border>(avatar, "PART_Status").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Badge_Builds_Its_Accent_Layers_Only_For_Info()
    {
        var badge = new Badge { Content = "Live" };
        var window = TestHelpers.Show(badge);
        try
        {
            Has(badge, "PART_Wash").ShouldBeFalse();
            badge.Tone = BadgeTone.Info;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Border>(badge, "PART_ToneRing").IsVisible.ShouldBeTrue();
            badge.Tone = BadgeTone.Success;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Border>(badge, "PART_ToneRing").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
