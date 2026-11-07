using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>
/// <see cref="ArcShrinkRow"/>: a horizontal StackPanel whose marked child gets only the width that is left, so text in a
/// row can trim or wrap.
/// </summary>
public class ArcShrinkRowTests
{
    private static Control[] Children(out TextBlock text)
    {
        text = new TextBlock { Text = "A rather long title that needs room", TextTrimming = TextTrimming.CharacterEllipsis };
        ArcShrinkRow.SetIsShrinkable(text, true);
        return [new Border { Width = 20, Height = 20 }, text, new Border { Width = 30, Height = 16 }];
    }

    private static Window Show(Control content, double width)
    {
        var window = TestHelpers.Show(new Border { Width = width, HorizontalAlignment = HorizontalAlignment.Left, Child = content });
        window.Width = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void With_Room_It_Lays_Out_Like_A_Horizontal_StackPanel()
    {
        var stackKids = Children(out _);
        var rowKids = Children(out _);
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { stackKids[0], stackKids[1], stackKids[2] } };
        var row = new ArcShrinkRow { Spacing = 6, Children = { rowKids[0], rowKids[1], rowKids[2] } };
        var window = Show(new StackPanel { Children = { stack, row } }, 800);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                rowKids[i].Bounds.X.ShouldBe(stackKids[i].Bounds.X, 0.01);
                rowKids[i].Bounds.Width.ShouldBe(stackKids[i].Bounds.Width, 0.01);
            }
            row.DesiredSize.Width.ShouldBe(stack.DesiredSize.Width, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void When_Narrow_Only_The_Marked_Child_Shrinks_And_Its_Text_Trims()
    {
        var kids = Children(out var text);
        var row = new ArcShrinkRow { Spacing = 6, Children = { kids[0], kids[1], kids[2] } };
        var window = Show(row, 160);
        try
        {
            kids[0].Bounds.Width.ShouldBe(20);
            kids[2].Bounds.Width.ShouldBe(30);
            // 160 - 20 - 30 - 2 × 6 = 98 for the text, and the last child still fits inside the row.
            text.Bounds.Width.ShouldBe(98, 0.5);
            kids[2].Bounds.Right.ShouldBeLessThanOrEqualTo(160 + 0.5);
            text.TextLayout.TextLines[0].HasCollapsed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Children_Take_No_Spacing()
    {
        var kids = Children(out _);
        kids[0].IsVisible = false;
        var row = new ArcShrinkRow { Spacing = 6, Children = { kids[0], kids[1], kids[2] } };
        var window = Show(row, 800);
        try
        {
            kids[1].Bounds.X.ShouldBe(0);
            kids[2].Bounds.X.ShouldBe(kids[1].Bounds.Right + 6, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wrapping_Child_Grows_The_Row_Instead_Of_Running_Past_It()
    {
        var text = new TextBlock { Text = "A note long enough to need two or three lines at this width", TextWrapping = TextWrapping.Wrap };
        ArcShrinkRow.SetIsShrinkable(text, true);
        var row = new ArcShrinkRow { Spacing = 6, Children = { new Border { Width = 14, Height = 14 }, text } };
        var window = Show(row, 150);
        try
        {
            text.Bounds.Right.ShouldBeLessThanOrEqualTo(150 + 0.5);
            text.TextLayout.TextLines.Count.ShouldBeGreaterThan(1);
            row.Bounds.Height.ShouldBeGreaterThan(text.TextLayout.TextLines[0].Height);
        }
        finally
        {
            window.Close();
        }
    }
}
