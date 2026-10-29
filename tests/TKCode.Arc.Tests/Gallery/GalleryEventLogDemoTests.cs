using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The EnvVariables "event log" demo's search box: it used to be a built-in TextBox, which has no template, so it drew
/// nothing; it is Arc's Input now.
/// </summary>
public class GalleryEventLogDemoTests
{
    [AvaloniaFact]
    public void The_Search_Box_Renders_Its_Text_And_Binds_Both_Ways()
    {
        var demo = new EnvVariablesEventLogDemo();
        var window = TestHelpers.Show(demo);
        window.Width = 900;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        var search = demo.FindControl<Input>("Search").ShouldNotBeNull();
        var block = demo.FindControl<EnvVariables>("Block").ShouldNotBeNull();

        search.Focus();
        window.KeyTextInput("API");
        Dispatcher.UIThread.RunJobs();
        var presenter = search.GetVisualDescendants().OfType<TextPresenter>().ShouldHaveSingleItem();
        presenter.Text.ShouldBe("API");
        presenter.Bounds.Width.ShouldBeGreaterThan(0);
        block.SearchText.ShouldBe("API");

        block.SearchText = "DB";
        Dispatcher.UIThread.RunJobs();
        search.Text.ShouldBe("DB");
        window.Close();
    }
}
