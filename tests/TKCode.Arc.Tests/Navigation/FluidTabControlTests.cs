using System.Collections.ObjectModel;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Navigation;

public class FluidTabControlTests
{
    private const double SectionHeight = 200;

    private static FluidTabControl Inline(params string[] headers)
    {
        var control = new FluidTabControl { Width = 640, Height = 300 };
        foreach (var header in headers)
        {
            control.Items.Add(new FluidTabItem
            {
                Header = header,
                Content = new Border { Height = SectionHeight, Child = new TextBlock { Text = header + " section" } }
            });
        }
        return control;
    }

    private static Window Show(FluidTabControl control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 700;
        window.Height = 360;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ScrollViewer Content(FluidTabControl control) => TestHelpers.Descendant<ScrollViewer>(control, FluidTabControl.ContentViewportPart);

    /// <summary>The offset that puts a section at the top: its place in the stack (the content padding stays above it).</summary>
    private static double SectionTop(FluidTabControl control, int index)
        => control.SectionPresenter(index)!.Bounds.Top;

    private static void ScrollBy(FluidTabControl control, double offset)
    {
        var viewport = Content(control);
        viewport.Offset = new Vector(0, offset);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void All_Sections_Are_Stacked_In_Order_And_The_First_Tab_Is_Selected()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy");
        var window = Show(control);
        try
        {
            control.SelectedIndex.ShouldBe(0);
            control.SectionCount.ShouldBe(4);
            for (var i = 0; i < 4; i++)
            {
                var presenter = control.SectionPresenter(i)!;
                presenter.Content.ShouldBeSameAs(((FluidTabItem)control.Items[i]!).Content);
                presenter.IsVisible.ShouldBeTrue();
                if (i > 0)
                {
                    SectionTop(control, i).ShouldBeGreaterThan(SectionTop(control, i - 1) + SectionHeight - 0.5);
                }
            }
            var first = (FluidTabItem)control.ContainerFromIndex(0)!;
            first.IsSelected.ShouldBeTrue();
            first.Classes.Contains(":selected").ShouldBeTrue();
            control.Classes.Contains(":left").ShouldBeTrue();
            var highlight = TestHelpers.Descendant<GlidingIndicator>(control, FluidTabControl.SelectionHighlightPart);
            highlight.HasTarget.ShouldBeTrue();
            highlight.CurrentRect.Height.ShouldBe(first.Bounds.Height, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_A_Tab_Scrolls_Its_Section_To_The_Top()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy", "Advanced");
        var window = Show(control);
        try
        {
            control.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 2), 0.5);
            control.SelectedIndex.ShouldBe(2);

            control.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Content(control).Offset.Y.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Target_Past_The_End_Is_Clamped_And_Keeps_The_Chosen_Tab()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy", "Advanced");
        var window = Show(control);
        try
        {
            var viewport = Content(control);
            control.SelectedIndex = 4;
            Dispatcher.UIThread.RunJobs();
            var max = viewport.Extent.Height - viewport.Viewport.Height;
            viewport.Offset.Y.ShouldBe(max, 0.5);
            control.SelectedIndex.ShouldBe(4);

            // The second last section cannot reach the top either; the selection stays on it after the scroll settles.
            control.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolling_The_Content_Selects_The_Section_At_The_Top()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy", "Advanced");
        var window = Show(control);
        try
        {
            ScrollBy(control, SectionTop(control, 1) + 20);
            control.SelectedIndex.ShouldBe(1);
            ((FluidTabItem)control.ContainerFromIndex(1)!).IsSelected.ShouldBeTrue();
            // The selection followed the scroll; it does not pull the content back to the section's top.
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 1) + 20, 0.5);

            ScrollBy(control, SectionTop(control, 2) + 5);
            control.SelectedIndex.ShouldBe(2);

            var viewport = Content(control);
            ScrollBy(control, viewport.Extent.Height - viewport.Viewport.Height);
            control.SelectedIndex.ShouldBe(4, "the bottom selects the last tab");

            ScrollBy(control, 0);
            control.SelectedIndex.ShouldBe(0, "the top selects the first tab");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Center_Alignment_Centers_The_Section_And_Reads_The_Middle_Line()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy", "Advanced");
        control.SnapAlignment = FluidTabSnapAlignment.Center;
        var window = Show(control);
        try
        {
            var viewport = Content(control);
            control.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            var expected = SectionTop(control, 2) + 24 + SectionHeight / 2 - viewport.Viewport.Height / 2;
            viewport.Offset.Y.ShouldBe(expected, 0.5);

            // The section across the middle line is the current one, though the previous one still shows at the top.
            ScrollBy(control, SectionTop(control, 1) + 24 - viewport.Viewport.Height / 2 + 10);
            control.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Home_And_End_Move_Through_Enabled_Tabs()
    {
        var control = Inline("One", "Two", "Three", "Four");
        ((FluidTabItem)control.Items[2]!).IsEnabled = false;
        var window = Show(control);
        try
        {
            control.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(1);
            control.ContainerFromIndex(1)!.IsFocused.ShouldBeTrue();
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 1), 0.5);

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(3, "the disabled tab is skipped");

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(3, "the last tab stays selected");

            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(0);

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(3);

            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(1);

            control.ContainerFromIndex(0)!.IsTabStop.ShouldBeFalse();
            control.ContainerFromIndex(1)!.IsTabStop.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_In_A_Section_Do_Not_Change_The_Tab()
    {
        var control = new FluidTabControl { Width = 640, Height = 300 };
        var input = new TextBox { Text = "Name" };
        control.Items.Add(new FluidTabItem { Header = "Profile", Content = input });
        control.Items.Add(new FluidTabItem { Header = "Security", Content = new Border { Height = 400 } });
        var window = Show(control);
        try
        {
            input.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Tab_Selects_It_And_Scrolls_To_Its_Section()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy");
        var window = Show(control);
        try
        {
            var third = control.ContainerFromIndex(2)!;
            var center = third.TranslatePoint(new Point(third.Bounds.Width / 2, third.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(2);
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 2), 0.5);

            // Clicking the selected tab after scrolling a little away brings its section back.
            ScrollBy(control, SectionTop(control, 2) + 30);
            control.SelectedIndex.ShouldBe(2);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 2), 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Right_Placement_Moves_The_List_And_The_Indicator()
    {
        var control = Inline("General", "Appearance");
        control.TabPlacement = FluidTabPlacement.Right;
        var window = Show(control);
        try
        {
            control.Classes.Contains(":right").ShouldBeTrue();
            control.Classes.Contains(":left").ShouldBeFalse();
            var list = TestHelpers.Descendant<Border>(control, FluidTabControl.TabListPart);
            var content = Content(control);
            list.Bounds.X.ShouldBeGreaterThan(content.Bounds.X);
            var tab = control.ContainerFromIndex(0)!;
            var indicator = TestHelpers.Descendant<GlidingIndicator>(control, FluidTabControl.SelectionIndicatorPart);
            var origin = tab.TranslatePoint(default, indicator)!.Value;
            indicator.CurrentRect.Right.ShouldBe(origin.X + tab.Bounds.Width, 0.5);

            control.TabPlacement = FluidTabPlacement.Left;
            Dispatcher.UIThread.RunJobs();
            list.Bounds.X.ShouldBeLessThan(content.Bounds.X);
            origin = tab.TranslatePoint(default, indicator)!.Value;
            indicator.CurrentRect.X.ShouldBe(origin.X, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    public sealed record Section(string Title, string Body);

    [AvaloniaFact]
    public void Data_Items_Use_The_Templates_And_Bind_Selection_Both_Ways()
    {
        var sections = new ObservableCollection<Section>
        {
            new("General", "Startup"), new("Appearance", "Theme"), new("Account", "Email"), new("Privacy", "Diagnostics")
        };
        var control = new FluidTabControl
        {
            Width = 640,
            Height = 300,
            ItemsSource = sections,
            ItemTemplate = new FuncDataTemplate<Section>((s, _) => new TextBlock { Text = s.Title }),
            ContentTemplate = new FuncDataTemplate<Section>((s, _) => new Border { Height = SectionHeight, Child = new TextBlock { Text = s.Body } }),
            IconTemplate = new FuncDataTemplate<Section>((s, _) => new TextBlock { Text = s.Title[..1] })
        };
        var window = Show(control);
        try
        {
            var tab = (FluidTabItem)control.ContainerFromIndex(1)!;
            tab.Header.ShouldBe(sections[1]);
            tab.Icon.ShouldBe(sections[1]);
            tab.Classes.Contains(":has-icon").ShouldBeTrue();
            control.SectionPresenter(1)!.Content.ShouldBe(sections[1]);
            control.SectionPresenter(1)!.Child.ShouldBeOfType<Border>();

            control.SelectedItem = sections[2];
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(2);
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 2), 0.5);

            ScrollBy(control, SectionTop(control, 1) + 10);
            control.SelectedItem.ShouldBe(sections[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_Removing_And_Moving_Items_Keeps_The_Sections_In_Step()
    {
        var sections = new ObservableCollection<string> { "General", "Appearance", "Account" };
        var control = new FluidTabControl
        {
            Width = 640,
            Height = 300,
            ItemsSource = sections,
            ContentTemplate = new FuncDataTemplate<string>((s, _) => new Border { Height = SectionHeight, Child = new TextBlock { Text = s } })
        };
        var window = Show(control);
        try
        {
            sections.Add("Advanced");
            Dispatcher.UIThread.RunJobs();
            control.SectionCount.ShouldBe(4);
            control.SectionPresenter(3)!.Content.ShouldBe("Advanced");

            sections.RemoveAt(1);
            Dispatcher.UIThread.RunJobs();
            control.SectionCount.ShouldBe(3);
            Enumerable.Range(0, 3).Select(i => control.SectionPresenter(i)!.Content).ShouldBe(new object[] { "General", "Account", "Advanced" });

            sections.Move(2, 0);
            Dispatcher.UIThread.RunJobs();
            Enumerable.Range(0, 3).Select(i => control.SectionPresenter(i)!.Content).ShouldBe(new object[] { "Advanced", "General", "Account" });

            sections[1] = "Notifications";
            Dispatcher.UIThread.RunJobs();
            control.SectionPresenter(1)!.Content.ShouldBe("Notifications");

            sections.Clear();
            Dispatcher.UIThread.RunJobs();
            control.SectionCount.ShouldBe(0);
            control.SelectedIndex.ShouldBe(-1);

            sections.Add("Again");
            Dispatcher.UIThread.RunJobs();
            control.SectionCount.ShouldBe(1);
            control.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Selected_Tab_Selects_Another_And_Keeps_It_In_View()
    {
        var sections = new ObservableCollection<string> { "General", "Appearance", "Account", "Privacy", "Advanced" };
        var control = new FluidTabControl
        {
            Width = 640,
            Height = 300,
            ItemsSource = sections,
            ContentTemplate = new FuncDataTemplate<string>((s, _) => new Border { Height = SectionHeight, Child = new TextBlock { Text = s } })
        };
        var window = Show(control);
        try
        {
            control.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            sections.RemoveAt(2);
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBeGreaterThanOrEqualTo(0);
            control.SelectedItem.ShouldNotBe("Account");
            var index = control.SelectedIndex;
            var viewport = Content(control);
            var max = viewport.Extent.Height - viewport.Viewport.Height;
            viewport.Offset.Y.ShouldBe(Math.Min(SectionTop(control, index), max), 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Tabs_Move_Their_Content_And_Hide_With_Their_Section()
    {
        var control = Inline("General", "Appearance", "Account");
        var window = Show(control);
        try
        {
            var appearance = (FluidTabItem)control.Items[1]!;
            var content = (Control)appearance.Content!;
            control.Items.RemoveAt(1);
            control.Items.Add(appearance);
            Dispatcher.UIThread.RunJobs();
            control.SectionPresenter(2)!.Content.ShouldBeSameAs(content);
            content.GetVisualParent().ShouldBeSameAs(control.SectionPresenter(2));

            appearance.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            control.SectionPresenter(2)!.IsVisible.ShouldBeFalse();

            var replacement = new TextBlock { Text = "New" };
            ((FluidTabItem)control.Items[0]!).Content = replacement;
            Dispatcher.UIThread.RunJobs();
            control.SectionPresenter(0)!.Content.ShouldBeSameAs(replacement);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Content_Template_Is_Never_Built_With_Null()
    {
        var probe = new NullTemplateProbe<string>();
        var sections = new ObservableCollection<string> { "General", "Appearance", "Account" };
        var control = new FluidTabControl { Width = 640, Height = 300, ItemsSource = sections, ContentTemplate = probe.Template };
        var window = Show(control);
        try
        {
            sections.RemoveAt(0);
            sections.Insert(1, "Privacy");
            sections.Move(0, 2);
            sections.Clear();
            control.ContentTemplate = new NullTemplateProbe<string>().Template;
            Dispatcher.UIThread.RunJobs();
            probe.Built.ShouldNotBeEmpty();
            probe.NullBuilds.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Selection_Made_Before_Showing_Starts_In_Place()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy", "Advanced");
        control.SelectedIndex = 2;
        var window = Show(control, ArcMotionLevel.Full);
        try
        {
            control.IsScrollAnimating.ShouldBeFalse();
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 2), 0.5);
            control.SelectedIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Smooth_Scroll_Runs_At_Full_Motion_Without_Bouncing_The_Selection()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy", "Advanced");
        control.AnimationDuration = TimeSpan.FromMilliseconds(200);
        var window = Show(control, ArcMotionLevel.Full);
        try
        {
            var seen = new List<int>();
            control.SelectionChanged += (_, _) => seen.Add(control.SelectedIndex);
            control.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            control.IsScrollAnimating.ShouldBeTrue();
            TestHelpers.PumpUntil(() => !control.IsScrollAnimating).ShouldBeTrue();
            TestHelpers.PumpUntil(() => Math.Abs(Content(control).Offset.Y - SectionTop(control, 3)) < 0.5).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            control.SelectedIndex.ShouldBe(3);
            string.Join(",", seen).ShouldBe("3", "the scroll passes sections 1 and 2 without selecting them");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_Animation_Off_Or_Reducing_Motion_Jumps()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy");
        control.IsAnimationEnabled = false;
        var window = Show(control, ArcMotionLevel.Full);
        try
        {
            control.SelectedIndex = 2;
            control.IsScrollAnimating.ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 2), 0.5);

            control.IsAnimationEnabled = true;
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            control.SelectedIndex = 1;
            control.IsScrollAnimating.ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            Content(control).Offset.Y.ShouldBe(SectionTop(control, 1), 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wheel_Input_Takes_Over_From_A_Running_Scroll()
    {
        var control = Inline("General", "Appearance", "Account", "Privacy", "Advanced");
        control.AnimationDuration = TimeSpan.FromSeconds(5);
        var window = Show(control, ArcMotionLevel.Full);
        try
        {
            control.SelectedIndex = 4;
            Dispatcher.UIThread.RunJobs();
            control.IsScrollAnimating.ShouldBeTrue();
            var viewport = Content(control);
            var point = viewport.TranslatePoint(new Point(viewport.Bounds.Width / 2, viewport.Bounds.Height / 2), window)!.Value;
            window.MouseWheel(point, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            control.IsScrollAnimating.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tokens_Set_The_Default_Geometry()
    {
        var control = Inline("General");
        var window = Show(control);
        try
        {
            control.TabListWidth.ShouldBe(200);
            control.SectionSpacing.ShouldBe(40);
            control.ContentPadding.ShouldBe(new Thickness(24));
            control.AnimationDuration.ShouldBe(TimeSpan.FromMilliseconds(480));
            var tab = (FluidTabItem)control.ContainerFromIndex(0)!;
            tab.MinHeight.ShouldBe(36);
            tab.Padding.ShouldBe(new Thickness(14, 8));
            TestHelpers.Descendant<Border>(control, FluidTabControl.TabListPart).BorderThickness.ShouldBe(new Thickness(0, 0, 1, 0));
        }
        finally
        {
            window.Close();
        }
    }
}
