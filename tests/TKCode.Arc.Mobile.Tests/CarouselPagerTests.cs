using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile.Tests;

public class CarouselPagerTests
{
    [AvaloniaFact]
    public void Pages_Navigation_Loop_And_Selection_Haptics_Work()
    {
        var recorded = new List<HapticKind>();
        var previous = Haptics.Platform;
        Haptics.Platform = new RecordingHaptics(recorded);
        try
        {
            var pager = new CarouselPager { Loop = true };
            pager.Items.Add(new TextBlock { Text = "Slide 1" });
            pager.Items.Add(new TextBlock { Text = "Slide 2" });
            pager.Items.Add(new TextBlock { Text = "Slide 3" });

            var changes = new List<(int Old, int New)>();
            pager.SelectionChanged += (_, e) => changes.Add((e.OldIndex, e.NewIndex));

            var window = new Window { Width = 360, Height = 260, Content = pager };
            window.Show();
            try
            {
                pager.PageCount.ShouldBe(3);
                pager.SelectedIndex.ShouldBe(0);

                pager.Next();
                pager.SelectedIndex.ShouldBe(1);
                pager.Next();
                pager.SelectedIndex.ShouldBe(2);
                pager.Next(); // Wraps around when Loop = true
                pager.SelectedIndex.ShouldBe(0);
                pager.Previous(); // Wraps back to last
                pager.SelectedIndex.ShouldBe(2);

                changes.ShouldBe([(0, 1), (1, 2), (2, 0), (0, 2)]);
                recorded.Count(k => k == HapticKind.Selection).ShouldBe(4);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            Haptics.Platform = previous;
        }
    }

    [AvaloniaFact]
    public void Keyboard_Navigation_And_TabbarItem_Badges_Work()
    {
        var pager = new CarouselPager();
        pager.Items.Add("One");
        pager.Items.Add("Two");
        pager.Items.Add("Three");

        var tab = new TabbarItem { Content = "Alerts", Badge = "5" };
        var dotTab = new TabbarItem { Content = "Profile", ShowBadgeDot = true };
        var bar = new Tabbar();
        bar.Items.Add(tab);
        bar.Items.Add(dotTab);

        var root = new StackPanel();
        root.Children.Add(pager);
        root.Children.Add(bar);

        var window = new Window { Width = 360, Height = 360, Content = root };
        window.Show();
        try
        {
            pager.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
            pager.SelectedIndex.ShouldBe(1);
            pager.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
            pager.SelectedIndex.ShouldBe(2);
            pager.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
            pager.SelectedIndex.ShouldBe(0);

            tab.Classes.Contains(":has-badge").ShouldBeTrue();
            tab.Classes.Contains(":has-badge-dot").ShouldBeFalse();
            dotTab.Classes.Contains(":has-badge-dot").ShouldBeTrue();

            var badgeBorder = tab.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Badge");
            badgeBorder.IsVisible.ShouldBeTrue();
            var dotBorder = dotTab.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BadgeDot");
            dotBorder.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RecordingHaptics(List<HapticKind> sink) : IHapticsPlatform
    {
        public void Trigger(HapticKind kind) => sink.Add(kind);
    }
}
