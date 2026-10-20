using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Avatars;

public class AvatarTests
{
    [Theory]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("  grace   brewster hopper ", "GB")]
    [InlineData("Plato", "P")]
    [InlineData("", "")]
    public void Initials_Use_The_First_Two_Words(string name, string expected) => Avatar.ComputeInitials(name).ShouldBe(expected);

    [AvaloniaFact]
    public void Sizes_Follow_Arc()
    {
        var avatar = new Avatar { DisplayName = "Alan Turing", Size = AvatarSize.Large, Status = AvatarStatus.Online };
        var window = TestHelpers.Show(avatar);
        try
        {
            avatar.Bounds.Width.ShouldBe(48);
            avatar.Initials.ShouldBe("AT");
            avatar.Size = AvatarSize.XLarge;
            Dispatcher.UIThread.RunJobs();
            avatar.Bounds.Width.ShouldBe(88);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Online_Status_Uses_The_Success_Color()
    {
        var avatar = new Avatar { DisplayName = "Leah Fox", Status = AvatarStatus.Offline };
        var window = TestHelpers.Show(avatar);
        try
        {
            var dot = TestHelpers.Descendant<Avalonia.Controls.Border>(avatar, "PART_Status");
            var offline = TestHelpers.ColorOf(dot.Background);
            avatar.Status = AvatarStatus.Online;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(dot.Background).ShouldNotBe(offline);
            TestHelpers.ColorOf(dot.Background).ShouldBe(ArcTokens.GetColor(avatar, DesignTokens.ArcDesignTokenKind.Success));
        }
        finally
        {
            window.Close();
        }
    }
}
