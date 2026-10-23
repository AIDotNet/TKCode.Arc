using TKCode.Arc.Motion;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests;

/// <summary>
/// The panel hosts of <see cref="Tabs"/> and <see cref="PageHeader"/> build a user's content template only with content, also
/// while panels animate (an exiting panel is cleared when the next switch starts) and for a tab that has no content at all.
/// <see cref="TemplateContractTests"/> covers the instant switch; these run at full motion with a probe that records null.
/// </summary>
public class TemplateContractMotionTests
{
    private static readonly int[] s_order = [1, 2, 1, 0, 2, 0];

    [AvaloniaFact]
    public void Tabs_Build_The_Panel_Template_Only_With_Content_While_Panels_Animate()
    {
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Full })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var probe = new NullTemplateProbe<string>();
            var tabs = new Tabs { ItemsSource = new[] { "Inbox", "Drafts", "Sent" }, ContentTemplate = probe.Template };
            var window = TestHelpers.Show(tabs, level);
            try
            {
                Switch(clock, index => tabs.SelectedIndex = index);
                probe.Built.Count.ShouldBeGreaterThan(0, level.ToString());
                probe.NullBuilds.ShouldBe(0, level.ToString());
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void A_Tab_Without_Content_Does_Not_Build_The_Panel_Template()
    {
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Full })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var probe = new NullTemplateProbe<string>();
            var tabs = new Tabs { ContentTemplate = probe.Template };
            tabs.Items.Add(new TabItem { Header = "One", Content = "first" });
            tabs.Items.Add(new TabItem { Header = "Empty" });
            tabs.Items.Add(new TabItem { Header = "Three", Content = "third" });
            var window = TestHelpers.Show(tabs, level);
            try
            {
                Switch(clock, index => tabs.SelectedIndex = index);
                probe.NullBuilds.ShouldBe(0, level.ToString());
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void PageHeader_Builds_The_Body_Template_Only_With_Content_While_Panels_Animate()
    {
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Full })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var probe = new NullTemplateProbe<string>();
            var header = new PageHeader
            {
                Title = "API",
                Width = 800,
                Height = 400,
                ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name }),
                ContentTemplate = probe.Template,
                ItemsSource = new AvaloniaList<string> { "Overview", "Endpoints", "Errors" }
            };
            var window = TestHelpers.Show(header, level);
            try
            {
                Switch(clock, index => header.SelectedIndex = index);
                probe.Built.Count.ShouldBeGreaterThan(0, level.ToString());
                probe.NullBuilds.ShouldBe(0, level.ToString());
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void A_PageHeader_Tab_Without_Content_Does_Not_Build_The_Body_Template()
    {
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Full })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var probe = new NullTemplateProbe<string>();
            var header = new PageHeader { Title = "API", Width = 800, Height = 400, ContentTemplate = probe.Template };
            header.Items.Add(new PageHeaderTab { Header = "One", Content = "first" });
            header.Items.Add(new PageHeaderTab { Header = "Empty" });
            header.Items.Add(new PageHeaderTab { Header = "Three", Content = "third" });
            var window = TestHelpers.Show(header, level);
            try
            {
                Switch(clock, index => header.SelectedIndex = index);
                probe.NullBuilds.ShouldBe(0, level.ToString());
            }
            finally
            {
                window.Close();
            }
        }
    }

    /// <summary>Selects tabs in a fixed order, letting each switch run for a few frames (the previous panel is still leaving).</summary>
    private static void Switch(ManualFrameClock clock, Action<int> select)
    {
        foreach (var index in s_order)
        {
            select(index);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(30));
            Dispatcher.UIThread.RunJobs();
        }
        clock.Advance(TimeSpan.FromSeconds(1));
        Dispatcher.UIThread.RunJobs();
    }
}
