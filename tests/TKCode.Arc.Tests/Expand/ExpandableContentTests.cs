using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Expand;

public class ExpandableContentTests
{
    private const string Long =
        "We moved our whole design team onto the new workspace in a single afternoon. The import kept every comment and version, " +
        "and the shared libraries meant nobody had to rebuild components from scratch. Two weeks in, review cycles are noticeably " +
        "shorter: feedback lands on the exact frame and threads resolve themselves when the change ships.";

    private static readonly string Huge = string.Join("\n", Enumerable.Range(1, 400).Select(i => $"Line {i}: " + Long));

    private static ExpandableTextViewport Viewport(ExpandableClamp clamp) => clamp.GetVisualDescendants().OfType<ExpandableTextViewport>().First();

    private static ExpandableTextToggle Toggle(ExpandableClamp clamp) => clamp.GetVisualDescendants().OfType<ExpandableTextToggle>().First();

    [AvaloniaFact]
    public void Selectable_Text_Clamps_At_Its_Own_Lines()
    {
        var text = new SelectableTextBlock { Text = Long, TextWrapping = TextWrapping.Wrap };
        var content = new ExpandableContent { Content = text, Width = 320 };
        var window = TestHelpers.Show(content);
        try
        {
            content.IsOverflowing.ShouldBeTrue();
            // The content takes the Arc body line height, so the clamp matches ExpandableText's.
            Viewport(content).Bounds.Height.ShouldBe(3 * 14 * 1.4, 1);
            Toggle(content).IsVisible.ShouldBeTrue();
            Viewport(content).OpacityMask.ShouldNotBeNull();
            content.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            Viewport(content).Bounds.Height.ShouldBe(text.Bounds.Height, 0.5);
            Viewport(content).OpacityMask.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Is_Found_Through_Plain_Containers_And_Hidden_Children_Are_Skipped()
    {
        var hidden = new TextBlock { Text = "Hidden", IsVisible = false };
        var text = new TextBlock { Text = Long, TextWrapping = TextWrapping.Wrap };
        var content = new ExpandableContent
        {
            Content = new Border { Padding = new Thickness(0, 10, 0, 0), Child = new Panel { Children = { hidden, text } } },
            Width = 320,
            Lines = 2,
        };
        var window = TestHelpers.Show(content);
        try
        {
            content.IsOverflowing.ShouldBeTrue();
            // Lines count from the top of the text, below the 10px padding.
            Viewport(content).Bounds.Height.ShouldBe(10 + 2 * 14 * 1.4, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Short_Content_Has_No_Toggle()
    {
        var content = new ExpandableContent { Content = new SelectableTextBlock { Text = "Fits." }, Width = 320 };
        var window = TestHelpers.Show(content);
        try
        {
            content.IsOverflowing.ShouldBeFalse();
            Toggle(content).IsVisible.ShouldBeFalse();
            Viewport(content).OpacityMask.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_Without_Text_Clamps_At_The_Fixed_Line_Height()
    {
        var content = new ExpandableContent { Content = new Border { Height = 400 }, LineHeight = 22, Lines = 4, Width = 320 };
        var window = TestHelpers.Show(content);
        try
        {
            content.IsOverflowing.ShouldBeTrue();
            Viewport(content).Bounds.Height.ShouldBe(88, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Long_Text_Is_Laid_Out_Only_Past_The_Clamp_While_Collapsed()
    {
        var text = new SelectableTextBlock { Text = Huge, TextWrapping = TextWrapping.Wrap };
        var content = new ExpandableContent { Content = text, Width = 320 };
        var window = TestHelpers.Show(content);
        try
        {
            content.IsOverflowing.ShouldBeTrue();
            Viewport(content).IsLayoutLimited.ShouldBeTrue();
            text.TextLayout.TextLines.Count.ShouldBe(4);
            content.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            Viewport(content).IsLayoutLimited.ShouldBeFalse();
            text.TextLayout.TextLines.Count.ShouldBeGreaterThan(400);
            content.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            Viewport(content).IsLayoutLimited.ShouldBeTrue();
            text.TextLayout.TextLines.Count.ShouldBe(4);
            Viewport(content).Bounds.Height.ShouldBe(3 * 14 * 1.4, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_A_Limited_Text_Glides_To_Its_Full_Height()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var text = new SelectableTextBlock { Text = string.Join("\n", Enumerable.Repeat(Long, 4)), TextWrapping = TextWrapping.Wrap };
        var content = new ExpandableContent { Content = text, Width = 320 };
        var window = TestHelpers.Show(content, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            Viewport(content).IsLayoutLimited.ShouldBeTrue();
            content.IsExpanded = true;
            clock.Advance(TimeSpan.FromMilliseconds(150));
            Dispatcher.UIThread.RunJobs();
            Viewport(content).IsMoving.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            Viewport(content).Bounds.Height.ShouldBe(text.Bounds.Height, 0.5);
            text.Bounds.Height.ShouldBeGreaterThan(12 * 14 * 1.4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Data_Context_Applies_Without_Motion()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var content = new ExpandableContent { Content = new SelectableTextBlock { Text = Long, TextWrapping = TextWrapping.Wrap }, Width = 320 };
        content.Bind(ExpandableClamp.IsExpandedProperty, new Avalonia.Data.Binding(nameof(Item.Open)));
        content.DataContext = new Item { Open = false };
        var window = TestHelpers.Show(content, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            content.DataContext = new Item { Open = true };
            Dispatcher.UIThread.RunJobs();
            content.IsExpanded.ShouldBeTrue();
            Viewport(content).IsMoving.ShouldBeFalse();
            Viewport(content).OpacityMask.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Item
    {
        public bool Open { get; set; }
    }
}
