using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Projects;

public class ProjectsSharedTests
{
    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 600;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>A column of fixed-height children in <see cref="Order"/>, reporting every arrange to a FLIP tracker.</summary>
    private sealed class FlipColumn : Panel
    {
        public FlipColumn()
        {
            Flip = new ProjectsFlip(this);
        }

        public ProjectsFlip Flip { get; }

        public List<Control> Order { get; } = new();

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
            {
                child.Measure(new Size(availableSize.Width, 40));
            }
            return new Size(200, Children.Count * 40);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            for (var i = 0; i < Order.Count; i++)
            {
                var rect = new Rect(0, i * 40, 200, 40);
                Order[i].Arrange(rect);
                Flip.Place(Order[i], rect);
            }
            return finalSize;
        }
    }

    [Fact]
    public void A_Person_Derives_Initials_Short_Name_And_A_Stable_Hue()
    {
        var noor = new ProjectPerson("Noor Haddad");
        noor.DisplayInitials.ShouldBe("NH");
        noor.DisplayShortName.ShouldBe("Noor");
        noor.Identity.ShouldBe("Noor Haddad");
        new ProjectPerson("Noor Haddad").DisplayHue.ShouldBe(noor.DisplayHue);
        noor.DisplayHue.ShouldBeInRange(0, 360);

        var single = new ProjectPerson("cher") { ShortName = "C." };
        single.DisplayInitials.ShouldBe("C");
        single.DisplayShortName.ShouldBe("C.");
        new ProjectPerson("Ines Maria Okafor").DisplayInitials.ShouldBe("IO");
        new ProjectPerson("Ines", key: "u7") { Initials = "IN", Hue = 400 }.DisplayHue.ShouldBe(40);
        new ProjectPerson("Ines", key: "u7").Identity.ShouldBe("u7");
        new ProjectPerson(string.Empty).DisplayInitials.ShouldBe(string.Empty);
    }

    [Fact]
    public void Names_Join_Into_An_English_List()
    {
        ProjectsText.JoinNames([]).ShouldBe(string.Empty);
        ProjectsText.JoinNames(["Noor"]).ShouldBe("Noor");
        ProjectsText.JoinNames(["Noor", "Tomás"]).ShouldBe("Noor and Tomás");
        ProjectsText.JoinNames(["Noor", "Tomás", "Ingrid"], "y").ShouldBe("Noor, Tomás y Ingrid");
        ProjectsText.Count(1, "1 task", "{0} tasks").ShouldBe("1 task");
        ProjectsText.Count(3, "1 task", "{0} tasks").ShouldBe("3 tasks");
        ProjectsText.Format("{0} of {1", 1).ShouldBe("{0} of {1");
    }

    [AvaloniaFact]
    public void The_Announcer_Says_Repeated_Text_Again_And_Can_Be_Cleared()
    {
        var announcer = new ProjectsAnnouncer();
        announcer.Announce("Card moved.");
        AutomationProperties.GetName(announcer).ShouldBe("Card moved.");
        announcer.Announce("Card moved.");
        AutomationProperties.GetName(announcer).ShouldNotBe("Card moved.");
        AutomationProperties.GetName(announcer)!.TrimEnd('\u200B').ShouldBe("Card moved.");
        announcer.Announce(string.Empty);
        announcer.Announcement.ShouldBe("Card moved.");
        announcer.History.Count.ShouldBe(2);
        announcer.Clear();
        announcer.Announcement.ShouldBeNull();
        AutomationProperties.GetName(announcer).ShouldBe(string.Empty);

        AutomationProperties.GetLiveSetting(announcer).ShouldBe(AutomationLiveSetting.Polite);
        announcer.IsAssertive = true;
        AutomationProperties.GetLiveSetting(announcer).ShouldBe(AutomationLiveSetting.Assertive);
    }

    [AvaloniaFact]
    public void An_Avatar_Stack_Overlaps_Its_Avatars_And_Names_Them()
    {
        var people = new AvaloniaList<ProjectPerson>
        {
            new("Noor Haddad"),
            new("Tomás Ferreira"),
            new("Ingrid Solberg"),
        };
        var stack = new ProjectsAvatarStack { People = people, LabelPrefix = "Attendees: " };
        var window = Show(new StackPanel { Children = { stack } });
        try
        {
            stack.DesiredSize.Width.ShouldBe(3 * 28 - 2 * 6);
            stack.DesiredSize.Height.ShouldBe(28);
            AutomationProperties.GetName(stack).ShouldBe("Attendees: Noor Haddad, Tomás Ferreira and Ingrid Solberg");

            people.RemoveAt(2);
            Dispatcher.UIThread.RunJobs();
            stack.VisiblePeople.Count.ShouldBe(2);
            AutomationProperties.GetName(stack).ShouldBe("Attendees: Noor Haddad and Tomás Ferreira");

            stack.MaxVisible = 1;
            stack.VisiblePeople.Count.ShouldBe(1);
            stack.Label = "Team";
            AutomationProperties.GetName(stack).ShouldBe("Team");
            stack.IsHitTestVisible.ShouldBeFalse();
            stack.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Armed_Flip_Springs_Children_From_Where_They_Were_Drawn()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var a = new Border();
        var b = new Border();
        var column = new FlipColumn();
        column.Children.AddRange([a, b]);
        column.Order.AddRange([a, b]);
        var window = Show(column, ArcMotionLevel.Full);
        try
        {
            column.Flip.Get(a).Layout!.Value.Y.ShouldBe(0);

            // Unarmed: a relayout moves children at once.
            column.Order.Reverse();
            column.InvalidateArrange();
            column.UpdateLayout();
            column.Flip.Get(a).Offset.Value.ShouldBe(default);
            column.Flip.Get(a).Layout!.Value.Y.ShouldBe(40);

            // Armed: each child starts where it was drawn and springs home.
            column.Flip.Arm();
            column.Order.Reverse();
            column.UpdateLayout();
            column.Flip.Get(a).Offset.Value.Y.ShouldBe(40);
            column.Flip.Get(b).Offset.Value.Y.ShouldBe(-40);
            column.Flip.Get(a).VisualTopLeft.Y.ShouldBe(40);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            column.Flip.Get(a).Offset.Value.Y.ShouldBeInRange(1, 39);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            column.Flip.Get(a).Offset.Value.Y.ShouldBe(0, 0.5);
            column.Flip.Get(a).IsMoving.ShouldBeFalse();

            // The arm is spent after one layout pass.
            Dispatcher.UIThread.RunJobs();
            column.Flip.IsArmed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Retargeted_Flight_Continues_From_Its_Drawn_Position()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var a = new Border();
        var b = new Border();
        var c = new Border();
        var column = new FlipColumn();
        column.Children.AddRange([a, b, c]);
        column.Order.AddRange([a, b, c]);
        var window = Show(column, ArcMotionLevel.Full);
        try
        {
            column.Flip.Arm();
            column.Order.Remove(a);
            column.Order.Add(a);
            column.UpdateLayout();
            clock.Advance(TimeSpan.FromMilliseconds(80));
            var drawn = column.Flip.Get(a).VisualTopLeft.Y;
            drawn.ShouldBeInRange(1, 79);

            column.Flip.Arm();
            column.Order.Remove(a);
            column.Order.Insert(0, a);
            column.UpdateLayout();
            column.Flip.Get(a).VisualTopLeft.Y.ShouldBe(drawn, 0.01);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            column.Flip.Get(a).VisualTopLeft.Y.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_Motion_Jumps_And_Forget_Returns_The_Transform()
    {
        var a = new Border();
        var b = new Border();
        var column = new FlipColumn();
        column.Children.AddRange([a, b]);
        column.Order.AddRange([a, b]);
        var window = Show(column);
        try
        {
            column.Flip.Arm();
            column.Order.Reverse();
            column.UpdateLayout();
            column.Flip.Get(a).Offset.Value.ShouldBe(default);
            a.RenderTransform.ShouldNotBeNull();
            column.Flip.Forget(a);
            a.RenderTransform.ShouldBeNull();
            column.Flip.TryGet(a, out _).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Progress_Line_Shows_Its_First_Value_And_Springs_To_The_Next()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var line = new ProjectsProgressLine { Maximum = 6, Value = 1 };
        var window = Show(new StackPanel { Width = 300, Children = { line } }, ArcMotionLevel.Full);
        try
        {
            line.ShownFraction.ShouldBe(1.0 / 6, 0.0001);
            line.Bounds.Height.ShouldBe(2);
            line.Value = 2;
            line.ShownFraction.ShouldBe(1.0 / 6, 0.0001);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            line.ShownFraction.ShouldBeInRange(1.0 / 6 + 0.01, 2.0 / 6 - 0.01);
            clock.Advance(TimeSpan.FromSeconds(1));
            line.ShownFraction.ShouldBe(2.0 / 6, 0.002);

            line.Value = 99;
            line.Fraction.ShouldBe(1);
            line.Maximum = 0;
            line.Fraction.ShouldBe(0);
            AutomationProperties.GetAccessibilityView(line).ShouldBe(AccessibilityView.Raw);
            line.IsDecorative = false;
            AutomationProperties.GetAccessibilityView(line).ShouldBe(AccessibilityView.Content);
        }
        finally
        {
            window.Close();
        }
    }
}
