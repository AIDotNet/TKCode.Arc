using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="ArcContentSwap"/>: the outgoing copy is placed by the content alignment.</summary>
public class ArcContentSwapTests
{
    private static (ContentPresenter Outgoing, ContentPresenter Incoming) Swap(ArcContentSwap swap, Window window, string next)
    {
        var old = swap.Content;
        swap.Content = next;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var presenters = swap.GetVisualChildren().OfType<ContentPresenter>().ToList();
        return (presenters.Single(p => Equals(p.Content, old)), presenters.Single(p => Equals(p.Content, next)));
    }

    private static Window Show(ArcContentSwap swap) =>
        TestHelpers.Show(new StackPanel { Children = { swap } }, ArcMotionLevel.Full);

    [AvaloniaFact]
    public void Left_Aligned_Outgoing_Text_Stays_On_The_Left_Edge()
    {
        using var _ = new ManualFrameClock().Install();
        var swap = new ArcContentSwap { Width = 240, HorizontalContentAlignment = HorizontalAlignment.Left, Content = "Saving changes" };
        var window = Show(swap);
        try
        {
            var (outgoing, incoming) = Swap(swap, window, "Saved");
            outgoing.Bounds.X.ShouldBe(0, 0.01);
            outgoing.Bounds.Width.ShouldBeLessThan(240);
            incoming.Bounds.Width.ShouldBe(240, 0.01);
            incoming.HorizontalContentAlignment.ShouldBe(HorizontalAlignment.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Right_Aligned_Outgoing_Text_Stays_On_The_Right_Edge()
    {
        using var _ = new ManualFrameClock().Install();
        var swap = new ArcContentSwap { Width = 240, HorizontalContentAlignment = HorizontalAlignment.Right, Content = "1,204" };
        var window = Show(swap);
        try
        {
            var (outgoing, _) = Swap(swap, window, "1,205");
            outgoing.Bounds.Right.ShouldBe(240, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Centered_And_Stretched_Outgoing_Content_Stays_Centered()
    {
        using var _ = new ManualFrameClock().Install();
        var centered = new ArcContentSwap { Width = 240, HorizontalContentAlignment = HorizontalAlignment.Center, Content = "Monthly" };
        var stretched = new ArcContentSwap { Width = 240, Content = "Monthly" };
        var window = TestHelpers.Show(new StackPanel { Children = { centered, stretched } }, ArcMotionLevel.Full);
        try
        {
            var (a, _) = Swap(centered, window, "Yearly");
            (a.Bounds.X + a.Bounds.Width / 2).ShouldBe(120, 0.01);
            var (b, _) = Swap(stretched, window, "Yearly");
            (b.Bounds.X + b.Bounds.Width / 2).ShouldBe(120, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Alignment_Places_The_Outgoing_Copy_Too()
    {
        using var _ = new ManualFrameClock().Install();
        var swap = new ArcContentSwap { Width = 240, Height = 60, VerticalContentAlignment = VerticalAlignment.Bottom, Content = "Draft" };
        var window = Show(swap);
        try
        {
            var (outgoing, _) = Swap(swap, window, "Published");
            outgoing.Bounds.Bottom.ShouldBe(60, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Alignment_Updates_The_Shown_Content()
    {
        var swap = new ArcContentSwap { Width = 240, Content = "Label" };
        var window = Show(swap);
        try
        {
            swap.HorizontalContentAlignment = HorizontalAlignment.Right;
            swap.VerticalContentAlignment = VerticalAlignment.Top;
            var presenter = swap.GetVisualChildren().OfType<ContentPresenter>().Single();
            presenter.HorizontalContentAlignment.ShouldBe(HorizontalAlignment.Right);
            presenter.VerticalContentAlignment.ShouldBe(VerticalAlignment.Top);
        }
        finally
        {
            window.Close();
        }
    }
}
