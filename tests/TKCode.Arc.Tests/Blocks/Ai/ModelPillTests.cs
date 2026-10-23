using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The model pill: what it shows, what it asks its owner, how it looks and how its content changes move.</summary>
public class ModelPillTests
{
    private static readonly ModelEffort[] FiveLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
    private static readonly ModelEffort[] ThreeLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High];

    // Invented geometric marks (24 grid): a diamond, a hexagon outline and a filled triangle.
    private static readonly Geometry Diamond = Geometry.Parse("M12 2 L22 12 L12 22 L2 12 Z");
    private static readonly Geometry Hexagon = Geometry.Parse("M12 2 L21 7 L21 17 L12 22 L3 17 L3 7 Z M12 5 L5.5 8.7 L5.5 15.3 L12 19 L18.5 15.3 L18.5 8.7 Z");
    private static readonly Geometry Triangle = Geometry.Parse("M12 3 L22 20 L2 20 Z");

    private static AiModelProvider[] Providers() =>
    [
        new() { Id = "aurora", Name = "Aurora Labs", Mark = Diamond, MarkColor = new SolidColorBrush(Color.Parse("#D9822B")) },
        new() { Id = "basalt", Name = "Basalt AI", Mark = Hexagon }
    ];

    private static AiModel Prime() => new()
    {
        Id = "aurora-prime-3", Name = "Aurora Prime 3", ProviderId = "aurora", EffortLevels = FiveLevels, DefaultEffort = ModelEffort.Medium
    };

    private static AiModel Swift() => new()
    {
        Id = "aurora-swift-3", Name = "Aurora Swift 3", ProviderId = "aurora", EffortLevels = FiveLevels, DefaultEffort = ModelEffort.High
    };

    private static AiModel Basalt() => new() { Id = "basalt-4", Name = "Basalt 4", ProviderId = "basalt", EffortLevels = ThreeLevels };

    private static AiModel Granite() => new() { Id = "granite-70b", Name = "Granite 70B", ProviderId = "granite", Mark = Triangle };

    private static AiModel Summit() => new()
    {
        Id = "aurora-summit-29", Name = "Aurora Summit 2.9 Extended Thinking", ProviderId = "aurora", EffortLevels = FiveLevels
    };

    private static ModelPill Create(AiModel? model = null, ModelEffort? effort = ModelEffort.Medium) =>
        new() { Model = model, Effort = effort, Providers = Providers() };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } }, level);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ArcContentSwap MarkSwap(ModelPill pill) => TestHelpers.Descendant<ArcContentSwap>(pill, ModelPill.MarkPart);

    private static ArcContentSwap TextSwap(ModelPill pill) => TestHelpers.Descendant<ArcContentSwap>(pill, ModelPill.TextPart);

    private static SpringSizeDecorator TextSize(ModelPill pill) => TestHelpers.Descendant<SpringSizeDecorator>(pill, ModelPill.TextSizePart);

    private static ArcGlyph Chevron(ModelPill pill) => TestHelpers.Descendant<ArcGlyph>(pill, "PART_Chevron");

    private static Border FocusRing(ModelPill pill) => TestHelpers.Descendant<Border>(pill, "PART_FocusRing");

    /// <summary>Layers in a content swap: 1 at rest, 2 while the old content fades out.</summary>
    private static int Layers(ArcContentSwap swap) => swap.GetVisualChildren().Count();

    private static Point CenterOf(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs posted jobs until <paramref name="done"/> holds (continuations come back from the thread pool).</summary>
    private static bool WaitFor(Func<bool> done)
    {
        for (var i = 0; i < 200; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (done())
            {
                return true;
            }
            Thread.Sleep(5);
        }
        return false;
    }

    private static List<ModelPillOpenRequestedEventArgs> Record(ModelPill pill)
    {
        var raised = new List<ModelPillOpenRequestedEventArgs>();
        pill.OpenRequested += (_, e) => raised.Add(e);
        return raised;
    }

    #region Defaults and text

    [AvaloniaFact]
    public void Defaults_Match_The_Standalone_Pill()
    {
        var pill = new ModelPill();
        pill.ShowEffort.ShouldBeTrue();
        pill.EmptyText.ShouldBe("Choose a model");
        pill.IsOpen.ShouldBeFalse();
        pill.Labels.ShouldNotBeNull();
        pill.Model.ShouldBeNull();
        pill.Effort.ShouldBeNull();
        var window = Show(pill);
        try
        {
            pill.Bounds.Height.ShouldBe(40);
            pill.Padding.ShouldBe(new Thickness(13, 0, 12, 0));
            pill.BorderThickness.ShouldBe(new Thickness(1));
            pill.CornerRadius.TopLeft.ShouldBeGreaterThanOrEqualTo(20);
            pill.Focusable.ShouldBeTrue();
            pill.Classes.ShouldContain(ModelPill.EmptyPseudoClass);
            pill.Classes.ShouldNotContain(ModelPill.OpenPseudoClass);
            FocusRing(pill).IsVisible.ShouldBeFalse();
            Chevron(pill).Size.ShouldBe(15);
            Chevron(pill).StrokeThickness.ShouldBe(1.75);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shows_The_Name_And_The_Effort()
    {
        var pill = Create(Prime());
        var window = Show(pill);
        try
        {
            pill.CurrentText!.Text.ShouldBe("Aurora Prime 3");
            pill.CurrentText.Effort.ShouldBe("Medium");
            pill.CurrentText.IsPlaceholder.ShouldBeFalse();
            pill.Classes.ShouldNotContain(ModelPill.EmptyPseudoClass);
            var name = TestHelpers.Descendant<TextBlock>(pill, "PART_Name");
            var effort = TestHelpers.Descendant<TextBlock>(pill, "PART_Effort");
            name.Text.ShouldBe("Aurora Prime 3");
            name.FontWeight.ShouldBe(FontWeight.Medium);
            name.FontSize.ShouldBe(14);
            name.LineHeight.ShouldBe(19.6, 0.01);
            name.LetterSpacing.ShouldBe(-0.14, 0.001);
            effort.IsVisible.ShouldBeTrue();
            effort.Text.ShouldBe("Medium");
            effort.FontWeight.ShouldBe(FontWeight.Normal);
            // 7px after the name, on the same line (same size and line height: same baseline).
            (effort.Bounds.X - name.Bounds.Right).ShouldBe(7, 0.5);
            effort.Bounds.Y.ShouldBe(name.Bounds.Y);
            TestHelpers.ColorOf(effort.Foreground).ShouldNotBe(TestHelpers.ColorOf(name.Foreground));

            pill.Effort = ModelEffort.ExtraHigh;
            pill.CurrentText!.Effort.ShouldBe("Extra high");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Model_Without_Effort_Shows_Its_Name_Only()
    {
        var pill = Create(Granite(), ModelEffort.High);
        var window = Show(pill);
        try
        {
            pill.CurrentText!.Text.ShouldBe("Granite 70B");
            pill.CurrentText.Effort.ShouldBeNull();
            TestHelpers.Descendant<TextBlock>(pill, "PART_Effort").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Missing_Effort_Shows_The_Name_Only()
    {
        var pill = Create(Prime(), effort: null);
        var window = Show(pill);
        try
        {
            pill.CurrentText!.Effort.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShowEffort_False_Shows_The_Name_Only()
    {
        var pill = Create(Prime());
        pill.ShowEffort = false;
        var window = Show(pill);
        try
        {
            pill.CurrentText!.Text.ShouldBe("Aurora Prime 3");
            pill.CurrentText.Effort.ShouldBeNull();
            AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3. Change model");
            pill.ShowEffort = true;
            pill.CurrentText!.Effort.ShouldBe("Medium");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Model_Shows_The_Muted_Empty_Text_Without_A_Mark()
    {
        var pill = Create(model: null);
        var window = Show(pill);
        try
        {
            pill.CurrentText!.Text.ShouldBe("Choose a model");
            pill.CurrentText.IsPlaceholder.ShouldBeTrue();
            pill.CurrentText.Classes.ShouldContain(ModelPillText.PlaceholderPseudoClass);
            pill.Classes.ShouldContain(ModelPill.EmptyPseudoClass);
            MarkSwap(pill).IsVisible.ShouldBeFalse();
            var name = TestHelpers.Descendant<TextBlock>(pill, "PART_Name");
            name.FontWeight.ShouldBe(FontWeight.Normal);
            pill.EmptyText = "Pick a model";
            pill.CurrentText!.Text.ShouldBe("Pick a model");
            window.UpdateLayout();
            var muted = TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(pill, "PART_Name").Foreground);

            pill.Model = Prime();
            window.UpdateLayout();
            pill.Classes.ShouldNotContain(ModelPill.EmptyPseudoClass);
            MarkSwap(pill).IsVisible.ShouldBeTrue();
            TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(pill, "PART_Name").Foreground).ShouldNotBe(muted);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Follows_Changes_Of_The_Model_Data_While_In_A_Window()
    {
        var model = Basalt();
        var pill = Create(model);
        var window = Show(pill);
        try
        {
            model.Name = "Basalt 4.1";
            pill.CurrentText!.Text.ShouldBe("Basalt 4.1");
            model.EffortLevels = null;
            pill.CurrentText!.Effort.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
        // Out of the window the model is no longer followed (the pill catches up when it comes back).
        model.Name = "Basalt 5";
        pill.CurrentText!.Text.ShouldBe("Basalt 4.1");
    }

    #endregion

    #region Marks

    [AvaloniaFact]
    public void A_Model_Without_A_Mark_Uses_Its_Providers_Mark()
    {
        var pill = Create(Prime());
        var window = Show(pill);
        try
        {
            var mark = pill.CurrentMark.ShouldNotBeNull();
            mark.Mark.ShouldBeSameAs(Diamond);
            TestHelpers.ColorOf(mark.MarkColor).ShouldBe(Color.Parse("#D9822B"));
            mark.Size.ShouldBe(16);
            mark.Bounds.Width.ShouldBe(16);

            // Its own mark wins; a mono mark has no color and paints with the text color.
            pill.Model = Granite();
            pill.CurrentMark!.Mark.ShouldBeSameAs(Triangle);
            pill.CurrentMark.MarkColor.ShouldBeNull();
            pill.Model = Basalt();
            pill.CurrentMark!.Mark.ShouldBeSameAs(Hexagon);
            pill.CurrentMark.MarkColor.ShouldBeNull();
            TestHelpers.ColorOf(pill.CurrentMark.Foreground).ShouldBe(TestHelpers.ColorOf(pill.Foreground));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Providers_That_Arrive_Later_Fill_In_The_Mark_Without_A_Crossfade()
    {
        var providers = new ObservableCollection<AiModelProvider>();
        var pill = new ModelPill { Model = Prime(), Effort = ModelEffort.Medium, Providers = providers };
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            MarkSwap(pill).IsVisible.ShouldBeFalse();
            pill.CurrentMark.ShouldBeNull();
            foreach (var provider in Providers())
            {
                providers.Add(provider);
            }
            pill.CurrentMark!.Mark.ShouldBeSameAs(Diamond);
            MarkSwap(pill).IsVisible.ShouldBeTrue();
            Layers(MarkSwap(pill)).ShouldBe(1);
            Layers(TextSwap(pill)).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Opening

    [AvaloniaFact]
    public void A_Primary_Press_Asks_To_Open_With_The_Surface_Focused()
    {
        var pill = Create(Prime());
        var raised = Record(pill);
        var window = Show(pill);
        try
        {
            Settle(window);
            var point = CenterOf(pill, window);
            window.MouseDown(point, MouseButton.Left);
            // On press, not on release.
            raised.Count.ShouldBe(1);
            raised[0].Focus.ShouldBe(ModelMenuOpenFocus.Surface);
            raised[0].Source.ShouldBeSameAs(pill);
            raised[0].RoutedEvent.ShouldBe(ModelPill.OpenRequestedEvent);
            window.MouseUp(point, MouseButton.Left);
            raised.Count.ShouldBe(1);
            // No pressed look, focus without a ring.
            pill.Classes.ShouldNotContain(":pressed");
            pill.IsFocused.ShouldBeTrue();
            Settle(window);
            FocusRing(pill).IsVisible.ShouldBeFalse();

            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            raised.Count.ShouldBe(1);
            // Ctrl + press is a context click on macOS (Radix ignores it too).
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.Control);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.Control);
            raised.Count.ShouldBe(1);
            pill.Classes.ShouldNotContain(":pressed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Touch_Asks_On_Contact_And_Leaves_No_Hover_Look()
    {
        var pill = Create(Prime());
        var raised = Record(pill);
        var window = Show(pill);
        try
        {
            Settle(window);
            var point = CenterOf(pill, window);
            var touch = window.TouchBegin(point);
            raised.Count.ShouldBe(1);
            raised[0].Focus.ShouldBe(ModelMenuOpenFocus.Surface);
            window.TouchEnd(touch, point);
            raised.Count.ShouldBe(1);
            pill.Classes.ShouldNotContain(ModelPill.HoverPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Request_Bubbles_To_The_Owner()
    {
        var pill = Create(Prime());
        var host = new Border { Child = pill };
        ModelPillOpenRequestedEventArgs? seen = null;
        host.AddHandler(ModelPill.OpenRequestedEvent, (_, e) => seen = e);
        var window = Show(host);
        try
        {
            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            seen.ShouldNotBeNull().Focus.ShouldBe(ModelMenuOpenFocus.Selected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Space_And_ArrowDown_Ask_With_The_Selected_Row_ArrowUp_Does_Not()
    {
        var pill = Create(Prime());
        var raised = Record(pill);
        var window = Show(pill);
        try
        {
            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            raised.Count.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            raised.Count.ShouldBe(2);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            raised.Count.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            raised.Count.ShouldBe(3);
            raised.ShouldAllBe(e => e.Focus == ModelMenuOpenFocus.Selected);

            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
            raised.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Open_Pill_Still_Asks_So_The_Owner_Can_Toggle()
    {
        var pill = Create(Prime());
        pill.IsOpen = true;
        var raised = Record(pill);
        var window = Show(pill);
        try
        {
            Settle(window);
            window.MouseDown(CenterOf(pill, window), MouseButton.Left);
            window.MouseUp(CenterOf(pill, window), MouseButton.Left);
            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            raised.Select(e => e.Focus).ShouldBe([ModelMenuOpenFocus.Surface, ModelMenuOpenFocus.Selected]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Disabled_Pill_Asks_Nothing_And_Fades()
    {
        var pill = Create(Prime());
        pill.IsEnabled = false;
        var raised = Record(pill);
        var window = Show(pill);
        try
        {
            Settle(window);
            pill.Opacity.ShouldBe(0.5);
            window.MouseDown(CenterOf(pill, window), MouseButton.Left);
            window.MouseUp(CenterOf(pill, window), MouseButton.Left);
            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            pill.RequestOpen(ModelMenuOpenFocus.Selected);
            raised.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Pill_Never_Clicks()
    {
        var pill = Create(Prime());
        var command = new CountingCommand();
        pill.Command = command;
        var clicks = 0;
        pill.Click += (_, _) => clicks++;
        var raised = Record(pill);
        var window = Show(pill);
        try
        {
            Settle(window);
            var point = CenterOf(pill, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            raised.Count.ShouldBe(3);
            clicks.ShouldBe(0);
            command.Executed.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsOpen_Sets_Open_And_Turns_The_Chevron()
    {
        var pill = Create(Prime());
        var window = Show(pill);
        try
        {
            var rest = TestHelpers.ColorOf(pill.Background);
            pill.IsOpen = true;
            pill.Classes.ShouldContain(ModelPill.OpenPseudoClass);
            Chevron(pill).RenderTransform!.Value.M11.ShouldBe(-1, 1e-9);
            TestHelpers.ColorOf(pill.Background).ShouldNotBe(rest);
            pill.IsOpen = false;
            pill.Classes.ShouldNotContain(ModelPill.OpenPseudoClass);
            Chevron(pill).RenderTransform!.Value.M11.ShouldBe(1, 1e-9);
            TestHelpers.ColorOf(pill.Background).ShouldBe(rest);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Looks_Like_Open()
    {
        var pill = Create(Prime());
        var window = Show(pill);
        try
        {
            Settle(window);
            var rest = (Background: TestHelpers.ColorOf(pill.Background), Border: TestHelpers.ColorOf(pill.BorderBrush));
            window.MouseMove(CenterOf(pill, window));
            pill.Classes.ShouldContain(ModelPill.HoverPseudoClass);
            var hover = (Background: TestHelpers.ColorOf(pill.Background), Border: TestHelpers.ColorOf(pill.BorderBrush));
            hover.ShouldNotBe(rest);
            window.MouseMove(new Point(390, 290));
            pill.Classes.ShouldNotContain(ModelPill.HoverPseudoClass);
            pill.IsOpen = true;
            (TestHelpers.ColorOf(pill.Background), TestHelpers.ColorOf(pill.BorderBrush)).ShouldBe(hover);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Focus_Ring_Shows_For_Keyboard_Focus_Only()
    {
        var pill = Create(Prime());
        var other = new Avalonia.Controls.Button { Content = "Other" };
        var window = Show(new StackPanel { Children = { pill, other } });
        try
        {
            pill.Focus(NavigationMethod.Pointer);
            Settle(window);
            FocusRing(pill).IsVisible.ShouldBeFalse();
            other.Focus(NavigationMethod.Pointer);
            // The owner returns focus this way after a keyboard close.
            pill.Focus(NavigationMethod.Tab);
            Settle(window);
            pill.Classes.ShouldContain(":focus-visible");
            var ring = FocusRing(pill);
            ring.IsVisible.ShouldBeTrue();
            ring.BorderThickness.ShouldBe(new Thickness(2));
            ring.Margin.ShouldBe(new Thickness(-4));
            pill.FocusAdorner.ShouldBeNull();
            pill.ClipToBounds.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void A_New_Effort_Crossfades_The_Text_But_Not_The_Mark()
    {
        var pill = Create(Prime());
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            Layers(MarkSwap(pill)).ShouldBe(1);
            Layers(TextSwap(pill)).ShouldBe(1);
            var mark = pill.CurrentMark;
            pill.Effort = ModelEffort.High;
            Layers(TextSwap(pill)).ShouldBe(2);
            Layers(MarkSwap(pill)).ShouldBe(1);
            pill.CurrentMark.ShouldBeSameAs(mark);
            MarkSwap(pill).Mode.ShouldBe(ContentSwapMode.Crossfade);
            TextSwap(pill).Mode.ShouldBe(ContentSwapMode.Crossfade);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            WaitFor(() => Layers(TextSwap(pill)) == 1).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Model_Crossfades_The_Mark_And_The_Text()
    {
        var pill = Create(Prime());
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var old = pill.CurrentMark;
            pill.Model = Swift();
            Layers(MarkSwap(pill)).ShouldBe(2);
            Layers(TextSwap(pill)).ShouldBe(2);
            pill.CurrentMark.ShouldNotBeSameAs(old);
            // The same words for another model do not swap the text; another model with the same mark still swaps the mark.
            clock.Advance(TimeSpan.FromMilliseconds(400));
            WaitFor(() => Layers(TextSwap(pill)) == 1 && Layers(MarkSwap(pill)) == 1).ShouldBeTrue();
            var twin = Swift();
            pill.Model = twin;
            Layers(MarkSwap(pill)).ShouldBe(2);
            Layers(TextSwap(pill)).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Width_Follows_The_New_Text_On_The_Quick_Spring()
    {
        var pill = Create(Basalt());
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var size = TextSize(pill);
            size.Spring.ShouldBe(ArcSpring.Quick);
            size.CapsToAvailable.ShouldBeTrue();
            size.ClipOverflow.ShouldBe(0);
            size.ClipToBounds.ShouldBeTrue();
            var before = size.Bounds.Width;
            var pillBefore = pill.Bounds.Width;
            pill.Model = Prime();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var target = pill.CurrentText!.DesiredSize.Width;
            target.ShouldBeGreaterThan(before);
            // Nothing jumped: the new text is clipped by the old width and revealed as the pill grows.
            size.Bounds.Width.ShouldBe(before, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            window.UpdateLayout();
            size.IsMoving.ShouldBeTrue();
            size.Bounds.Width.ShouldBeGreaterThan(before);
            size.Bounds.Width.ShouldBeLessThan(target);
            pill.Bounds.Width.ShouldBeGreaterThan(pillBefore);
            clock.Advance(TimeSpan.FromSeconds(1));
            window.UpdateLayout();
            size.IsMoving.ShouldBeFalse();
            size.Bounds.Width.ShouldBe(target, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Width_Spring_Settles_Without_Overshoot()
    {
        var pill = Create(Basalt());
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var size = TextSize(pill);
            pill.Model = Summit();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var target = pill.CurrentText!.DesiredSize.Width;
            var widest = 0.0;
            var frames = 0;
            for (var i = 0; i < 60; i++)
            {
                clock.Advance(CoreFrames.Step);
                window.UpdateLayout();
                widest = Math.Max(widest, size.Bounds.Width);
                if (size.IsMoving)
                {
                    frames++;
                }
            }
            widest.ShouldBeLessThanOrEqualTo(target + 0.5);
            // Quick (visual 0.25s, no bounce) settles in well under 40 frames.
            frames.ShouldBeLessThan(40);
            size.Bounds.Width.ShouldBe(target, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Slot_Caps_The_Width_And_Clips_Only_The_Text()
    {
        var pill = Create(Basalt());
        pill.MaxWidth = 170;
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            pill.Model = Summit();
            pill.Effort = ModelEffort.Medium;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            Settle(window);
            pill.Bounds.Width.ShouldBeLessThanOrEqualTo(170);
            var size = TextSize(pill);
            size.IsMoving.ShouldBeFalse();
            // The text is wider than its region and is clipped there (no ellipsis).
            var effort = TestHelpers.Descendant<TextBlock>(pill, "PART_Effort");
            effort.TranslatePoint(new Point(effort.Bounds.Width, 0), size)!.Value.X.ShouldBeGreaterThan(size.Bounds.Width + 20);
            size.ClipToBounds.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(pill, "PART_Name").TextTrimming.ShouldBe(TextTrimming.None);
            // The mark and the chevron keep their size and stay inside the pill.
            var chevron = Chevron(pill);
            chevron.Bounds.Width.ShouldBe(15);
            var right = chevron.TranslatePoint(new Point(chevron.Bounds.Width, 0), pill)!.Value.X;
            right.ShouldBeLessThanOrEqualTo(pill.Bounds.Width - 12 - 1 + 0.5);
            pill.CurrentMark!.Bounds.Width.ShouldBe(16);
            pill.Bounds.Height.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Long_Name_Stays_On_One_Line()
    {
        var pill = Create(Summit());
        var window = Show(pill);
        try
        {
            var name = TestHelpers.Descendant<TextBlock>(pill, "PART_Name");
            name.TextWrapping.ShouldBe(TextWrapping.NoWrap);
            name.Bounds.Height.ShouldBe(19.6, 0.6);
            pill.Bounds.Height.ShouldBe(40);
            // Unconstrained, the pill is as wide as its content: padding, mark, gaps, text, chevron, border.
            var expected = 13 + 16 + 8 + pill.CurrentText!.Bounds.Width + 8 + 15 + 12 + 2;
            pill.Bounds.Width.ShouldBe(expected, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_And_Jumps_The_Width()
    {
        var pill = Create(Basalt());
        var window = Show(pill, ArcMotionLevel.Reduced);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            pill.Model = Prime();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Layers(TextSwap(pill)).ShouldBe(2);
            Layers(MarkSwap(pill)).ShouldBe(2);
            var size = TextSize(pill);
            size.IsMoving.ShouldBeFalse();
            size.Bounds.Width.ShouldBe(pill.CurrentText!.DesiredSize.Width, 0.5);
            // 120ms linear: gone after the fade.
            clock.Advance(TimeSpan.FromMilliseconds(150));
            WaitFor(() => Layers(TextSwap(pill)) == 1 && Layers(MarkSwap(pill)) == 1).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Changes_Everything_At_Once()
    {
        var pill = Create(Basalt());
        var window = Show(pill);
        try
        {
            pill.Model = Prime();
            window.UpdateLayout();
            Layers(TextSwap(pill)).ShouldBe(1);
            Layers(MarkSwap(pill)).ShouldBe(1);
            TextSize(pill).Bounds.Width.ShouldBe(pill.CurrentText!.DesiredSize.Width, 0.5);
            pill.IsOpen = true;
            pill.Transitions.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Declares_The_Color_And_Chevron_Transitions()
    {
        var pill = Create(Prime());
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            Settle(window);
            pill.Classes.ShouldContain(":motion-full");
            var transitions = pill.Transitions.ShouldNotBeNull();
            transitions.OfType<ArcBrushTransition>().Single(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty)
                .Duration.ShouldBe(TimeSpan.FromMilliseconds(120));
            transitions.OfType<ArcBrushTransition>().Single(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty)
                .Duration.ShouldBe(TimeSpan.FromMilliseconds(160));
            var turn = Chevron(pill).Transitions.ShouldNotBeNull().OfType<ArcTransformOperationsTransition>().Single();
            turn.Duration.ShouldBe(TimeSpan.FromMilliseconds(580));
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void The_Automation_Name_Carries_The_Model_And_The_Effort()
    {
        var pill = Create(Prime());
        var window = Show(pill);
        try
        {
            AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3, medium effort. Change model");
            pill.Effort = ModelEffort.ExtraHigh;
            AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3, extra high effort. Change model");
            pill.Model = Granite();
            AutomationProperties.GetName(pill).ShouldBe("Model: Granite 70B. Change model");
            pill.Model = null;
            AutomationProperties.GetName(pill).ShouldBe("Choose a model");
            pill.Labels = new ModelPillLabels
            {
                EffortName = static effort => effort == ModelEffort.Medium ? "Mittel" : effort.ToString(),
                AutomationName = static (model, effort) => $"Modell {model.Name} ({effort})",
                AutomationNameEmpty = static text => text + "!"
            };
            AutomationProperties.GetName(pill).ShouldBe("Choose a model!");
            pill.Model = Prime();
            pill.Effort = ModelEffort.Medium;
            AutomationProperties.GetName(pill).ShouldBe("Modell Aurora Prime 3 (Medium)");
            pill.CurrentText!.Effort.ShouldBe("Mittel");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Peer_Is_An_Expandable_Menu_Button_Without_Children()
    {
        var pill = Create(Prime());
        var raised = Record(pill);
        var window = Show(pill);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(pill);
            peer.GetName().ShouldBe("Model: Aurora Prime 3, medium effort. Change model");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetChildren().ShouldBeEmpty();
            var expand = peer.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
            expand.ShowsMenu.ShouldBeTrue();
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            expand.Expand();
            raised.Count.ShouldBe(1);
            expand.Collapse();
            raised.Count.ShouldBe(1);
            pill.IsOpen = true;
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            expand.Expand();
            raised.Count.ShouldBe(1);
            expand.Collapse();
            raised.Count.ShouldBe(2);
            peer.GetProvider<IInvokeProvider>().ShouldNotBeNull().Invoke();
            raised.Count.ShouldBe(3);
            raised.ShouldAllBe(e => e.Focus == ModelMenuOpenFocus.Selected);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    private sealed class CountingCommand : ICommand
    {
        public int Executed { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executed++;
    }
}
