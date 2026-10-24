using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Docs;

namespace TKCode.Arc.Tests.Blocks.Auth;

/// <summary>The Immersive login's Gallery demos: each one builds, shows a block and renders.</summary>
public class LoginImmersiveGalleryTests
{
    [AvaloniaFact]
    public void Every_Immersive_Demo_Renders()
    {
        var demos = GalleryDemos.All.Where(d => d.Component == "Login" && d.Name.StartsWith("LoginImmersive", StringComparison.Ordinal)).ToList();
        demos.Select(d => d.Name).ShouldBe(
            ["LoginImmersiveEvents", "LoginImmersiveFullScreen", "LoginImmersiveNarrow", "LoginImmersivePreview", "LoginImmersiveReducedMotion", "LoginImmersiveSignUp"],
            ignoreOrder: true);
        foreach (var demo in demos)
        {
            var control = demo.Create();
            var window = TestHelpers.Show(control);
            try
            {
                window.Width = 1000;
                window.Height = 1000;
                Dispatcher.UIThread.RunJobs();
                var block = control.GetVisualDescendants().OfType<LoginImmersive>().Single();
                block.Bounds.Height.ShouldBeGreaterThan(600, demo.Id);
                block.Step.ShouldBe(LoginImmersiveStep.Form, demo.Id);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void The_Events_Demo_Uses_Its_Own_Picture_And_Handlers()
    {
        var demo = GalleryDemos.All.Single(d => d.Component == "Login" && d.Name == "LoginImmersiveEvents");
        var control = demo.Create();
        var window = TestHelpers.Show(control);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var block = control.GetVisualDescendants().OfType<LoginImmersive>().Single();
            block.Photo.ShouldNotBeNull();
            block.SendLinkAsync.ShouldNotBeNull();
            block.OpenMailAppAsync.ShouldNotBeNull();
            block.SignInWithProviderAsync.ShouldNotBeNull();
            block.BrandName.ShouldBe("Harbor Desk");
            block.FormView!.SignInHeading!.Title.ShouldBe("Sign in to Harbor Desk");
        }
        finally
        {
            window.Close();
        }
    }
}
