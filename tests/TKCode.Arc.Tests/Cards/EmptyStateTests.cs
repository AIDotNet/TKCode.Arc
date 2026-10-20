using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Cards;

public class EmptyStateTests
{
    [AvaloniaFact]
    public void Default_Empty_State_Shows_Folder_Title_And_Description()
    {
        var empty = new EmptyState { Title = "No projects yet", Description = "Projects keep your files, people and conversations in one place.", Label = "Projects" };
        var window = TestHelpers.Show(new StackPanel { Children = { empty } });
        try
        {
            var block = empty.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_IconBlock");
            block.Bounds.Width.ShouldBe(48);
            block.Opacity.ShouldBe(1);
            var glyph = TestHelpers.Descendant<ArcGlyph>(block);
            glyph.Data.ShouldBe(ArcIcons.Folder);
            glyph.StrokeThickness.ShouldBe(1.5);
            var title = empty.GetVisualDescendants().OfType<MessagesTextSwap>().First(s => s.Name == "PART_Title");
            title.CurrentBlock!.Text.ShouldBe("No projects yet");
            AutomationProperties.GetHeadingLevel(title).ShouldBe(3);
            var description = empty.GetVisualDescendants().OfType<MessagesTextSwap>().First(s => s.Name == "PART_Description");
            description.Bounds.Width.ShouldBeLessThanOrEqualTo(288);
            AutomationProperties.GetName(empty).ShouldBe("Projects");
            AutomationProperties.GetLandmarkType(empty).ShouldBe(AutomationLandmarkType.Region);
            empty.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(p => p.Name == "PART_Action").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Morphs_Icon_Copy_And_Action_In_Place()
    {
        var action = new Button { Content = "New project" };
        var empty = new EmptyState { Title = "No projects yet", Description = "Create one to get started.", Action = action };
        var window = TestHelpers.Show(new StackPanel { Children = { empty } });
        try
        {
            empty.Classes.ShouldContain(":has-action");
            empty.Icon = ArcIcons.Check;
            empty.Title = "Project created";
            empty.Action = null;
            Dispatcher.UIThread.RunJobs();
            var glyph = TestHelpers.Descendant<ArcGlyph>(empty);
            glyph.Data.ShouldBe(ArcIcons.Check);
            empty.GetVisualDescendants().OfType<MessagesTextSwap>().First(s => s.Name == "PART_Title").CurrentBlock!.Text.ShouldBe("Project created");
            empty.Classes.ShouldNotContain(":has-action");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Balanced_Description_Uses_Even_Lines()
    {
        var swap = new MessagesTextSwap
        {
            Text = "Projects keep your files, people and conversations in one place.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            IsBalanced = true,
            Width = 288
        };
        var window = TestHelpers.Show(new StackPanel { Children = { swap } });
        try
        {
            var block = swap.CurrentBlock!;
            var reference = new TextBlock { Text = swap.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = block.FontSize };
            reference.Measure(new Avalonia.Size(288, double.PositiveInfinity));
            // Balancing keeps the line count but narrows the lines, centered in the slot.
            block.TextLayout.TextLines.Count.ShouldBe(reference.TextLayout.TextLines.Count);
            block.Bounds.Width.ShouldBeLessThan(288);
            block.Bounds.X.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }
}
