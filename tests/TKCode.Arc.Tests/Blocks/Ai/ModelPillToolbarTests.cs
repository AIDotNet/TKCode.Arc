using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The model pill in a composer toolbar (<see cref="ModelPillAppearance.Toolbar"/>): 36 high, borderless, the short name
/// and the muted effort, tinted only on hover and while open; and the compact pill (<see cref="ModelPill.ShowText"/> false)
/// that keeps only the mark and the chevron.
/// </summary>
public class ModelPillToolbarTests
{
    private static readonly ModelEffort[] FiveLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];

    // Invented geometric marks (24 grid): a diamond and a filled triangle.
    private static readonly Geometry Diamond = Geometry.Parse("M12 2 L22 12 L12 22 L2 12 Z");
    private static readonly Geometry Triangle = Geometry.Parse("M12 3 L22 20 L2 20 Z");

    private static AiModelProvider[] Providers() =>
    [
        new() { Id = "aurora", Name = "Aurora Labs", Mark = Diamond, MarkColor = new SolidColorBrush(Color.Parse("#D9822B")) }
    ];

    private static AiModel Prime() => new()
    {
        Id = "aurora-prime-3", Name = "Aurora Prime 3", ShortName = "Prime 3", ProviderId = "aurora", EffortLevels = FiveLevels,
        DefaultEffort = ModelEffort.Medium
    };

    private static AiModel Swift() => new()
    {
        Id = "aurora-swift-3", Name = "Aurora Swift 3", ShortName = "Swift", ProviderId = "aurora", EffortLevels = FiveLevels
    };

    private static AiModel Granite() => new() { Id = "granite-70b", Name = "Granite 70B", Mark = Triangle };

    private static ModelPill Toolbar(AiModel? model = null, ModelEffort? effort = ModelEffort.Medium) =>
        new() { Appearance = ModelPillAppearance.Toolbar, Model = model ?? Prime(), Effort = effort, Providers = Providers() };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } }, level);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

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

    private static ArcContentSwap TextSwap(ModelPill pill) => TestHelpers.Descendant<ArcContentSwap>(pill, ModelPill.TextPart);

    private static ArcContentSwap MarkSwap(ModelPill pill) => TestHelpers.Descendant<ArcContentSwap>(pill, ModelPill.MarkPart);

    private static SpringSizeDecorator TextSize(ModelPill pill) => TestHelpers.Descendant<SpringSizeDecorator>(pill, ModelPill.TextSizePart);

    private static ArcGlyph Chevron(ModelPill pill) => TestHelpers.Descendant<ArcGlyph>(pill, "PART_Chevron");

    private static int Layers(ArcContentSwap swap) => swap.GetVisualChildren().Count();

    private static Rect BoundsIn(Visual visual, Visual root) => new(visual.TranslatePoint(default, root)!.Value, visual.Bounds.Size);

    /// <summary>The theme's foreground at <paramref name="alpha"/>: the toolbar tints are neutral, not accent-colored.</summary>
    private static void ShouldBeForegroundAt(Color actual, ModelPill pill, double alpha)
    {
        var foreground = ArcTokens.GetColor(pill, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground);
        actual.R.ShouldBe(foreground.R);
        actual.G.ShouldBe(foreground.G);
        actual.B.ShouldBe(foreground.B);
        ((double)actual.A).ShouldBe(alpha * 255, 1.5);
    }

    #region Defaults and geometry

    [AvaloniaFact]
    public void Defaults_Keep_The_Standalone_Pill_With_Its_Text()
    {
        var pill = new ModelPill();
        pill.Appearance.ShouldBe(ModelPillAppearance.Default);
        pill.ShowText.ShouldBeTrue();
        pill.Classes.ShouldNotContain(ModelPill.CompactPseudoClass);
    }

    [AvaloniaFact]
    public void The_Toolbar_Pill_Is_36_High_Borderless_And_Clear_At_Rest()
    {
        var pill = Toolbar();
        var window = Show(pill);
        try
        {
            pill.Bounds.Height.ShouldBe(36);
            pill.BorderThickness.ShouldBe(default(Thickness));
            pill.Padding.ShouldBe(new Thickness(10, 0, 8, 0));
            TestHelpers.ColorOf(pill.Background).A.ShouldBe((byte)0);
            pill.CornerRadius.TopLeft.ShouldBeGreaterThanOrEqualTo(18);
            var chevron = Chevron(pill);
            chevron.Size.ShouldBe(14);
            chevron.StrokeThickness.ShouldBe(2);
            pill.CurrentMark!.Size.ShouldBe(16);
            pill.CurrentMark.Bounds.Width.ShouldBe(16);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Toolbar_Shows_The_Short_Name_Then_The_Muted_Effort_Six_Apart()
    {
        var pill = Toolbar();
        var window = Show(pill);
        try
        {
            pill.CurrentText!.Text.ShouldBe("Prime 3");
            pill.CurrentText.Effort.ShouldBe("Medium");
            var name = TestHelpers.Descendant<TextBlock>(pill, "PART_Name");
            var effort = TestHelpers.Descendant<TextBlock>(pill, "PART_Effort");
            name.FontWeight.ShouldBe(FontWeight.Medium);
            name.FontSize.ShouldBe(14);
            effort.FontWeight.ShouldBe(FontWeight.Normal);
            (effort.Bounds.X - name.Bounds.Right).ShouldBe(6, 0.5);
            effort.Bounds.Y.ShouldBe(name.Bounds.Y);
            TestHelpers.ColorOf(effort.Foreground).ShouldBe(ArcTokens.GetColor(pill, TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(name.Foreground).ShouldBe(ArcTokens.GetColor(pill, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground));

            // A model without a short name shows its name.
            pill.Model = Granite();
            pill.CurrentText!.Text.ShouldBe("Granite 70B");
            pill.CurrentText.Effort.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Parts_Sit_Six_Apart_Inside_Ten_And_Eight()
    {
        var pill = Toolbar();
        var window = Show(pill);
        try
        {
            var mark = BoundsIn(pill.CurrentMark!, pill);
            var text = BoundsIn(TextSize(pill), pill);
            var chevron = BoundsIn(Chevron(pill), pill);
            mark.X.ShouldBe(10, 0.5);
            (text.X - mark.Right).ShouldBe(6, 0.5);
            (chevron.X - text.Right).ShouldBe(6, 0.5);
            (pill.Bounds.Width - chevron.Right).ShouldBe(8, 0.5);
            pill.Bounds.Width.ShouldBe(10 + 16 + 6 + pill.CurrentText!.Bounds.Width + 6 + 14 + 8, 1);
            // Mark, text and chevron share the pill's vertical center.
            (mark.Y + mark.Height / 2).ShouldBe(18, 0.5);
            (chevron.Y + chevron.Height / 2).ShouldBe(18, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_The_Appearance_Restyles_The_Pill_Both_Ways()
    {
        var pill = new ModelPill { Model = Prime(), Effort = ModelEffort.Medium, Providers = Providers() };
        var window = Show(pill);
        try
        {
            pill.Bounds.Height.ShouldBe(40);
            pill.CurrentText!.Text.ShouldBe("Aurora Prime 3");
            pill.Appearance = ModelPillAppearance.Toolbar;
            Settle(window);
            pill.Bounds.Height.ShouldBe(36);
            pill.BorderThickness.ShouldBe(default(Thickness));
            pill.CurrentText!.Text.ShouldBe("Prime 3");
            Chevron(pill).Size.ShouldBe(14);
            pill.Appearance = ModelPillAppearance.Default;
            Settle(window);
            pill.Bounds.Height.ShouldBe(40);
            pill.BorderThickness.ShouldBe(new Thickness(1));
            pill.Padding.ShouldBe(new Thickness(13, 0, 12, 0));
            pill.CurrentText!.Text.ShouldBe("Aurora Prime 3");
            Chevron(pill).Size.ShouldBe(15);
            Chevron(pill).StrokeThickness.ShouldBe(1.75);
            (TestHelpers.Descendant<TextBlock>(pill, "PART_Effort").Bounds.X -
             TestHelpers.Descendant<TextBlock>(pill, "PART_Name").Bounds.Right).ShouldBe(7, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region States

    [AvaloniaFact]
    public void Hover_Tints_With_The_Foreground_At_6_Percent_And_Open_At_10()
    {
        var pill = Toolbar();
        var window = Show(pill);
        try
        {
            Settle(window);
            var center = pill.TranslatePoint(new Point(pill.Bounds.Width / 2, pill.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Settle(window);
            pill.Classes.ShouldContain(ModelPill.HoverPseudoClass);
            ShouldBeForegroundAt(TestHelpers.ColorOf(pill.Background), pill, 0.06);
            pill.BorderThickness.ShouldBe(default(Thickness));
            // Open wins over hover.
            pill.IsOpen = true;
            Settle(window);
            ShouldBeForegroundAt(TestHelpers.ColorOf(pill.Background), pill, 0.10);
            window.MouseMove(new Point(390, 290));
            Settle(window);
            ShouldBeForegroundAt(TestHelpers.ColorOf(pill.Background), pill, 0.10);
            Chevron(pill).RenderTransform!.Value.M11.ShouldBe(-1, 0.001);
            pill.IsOpen = false;
            Settle(window);
            TestHelpers.ColorOf(pill.Background).A.ShouldBe((byte)0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_The_Ring_Pointer_Focus_Does_Not()
    {
        var pill = Toolbar();
        var other = new Avalonia.Controls.Button { Content = "Other" };
        var window = Show(new StackPanel { Children = { pill, other } });
        try
        {
            pill.Focus(NavigationMethod.Pointer);
            Settle(window);
            TestHelpers.Descendant<Border>(pill, "PART_FocusRing").IsVisible.ShouldBeFalse();
            other.Focus(NavigationMethod.Pointer);
            pill.Focus(NavigationMethod.Tab);
            Settle(window);
            var ring = TestHelpers.Descendant<Border>(pill, "PART_FocusRing");
            ring.IsVisible.ShouldBeTrue();
            ring.Margin.ShouldBe(new Thickness(-4));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Toolbar_Pill_Asks_To_Open_Like_The_Standalone_One()
    {
        var pill = Toolbar();
        var raised = new List<ModelPillOpenRequestedEventArgs>();
        pill.OpenRequested += (_, e) => raised.Add(e);
        var window = Show(pill);
        try
        {
            var center = pill.TranslatePoint(new Point(pill.Bounds.Width / 2, pill.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            raised.Count.ShouldBe(1);
            raised[0].Focus.ShouldBe(ModelMenuOpenFocus.Surface);
            pill.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            raised.Count.ShouldBe(3);
            raised[1].Focus.ShouldBe(ModelMenuOpenFocus.Selected);
            raised[2].Focus.ShouldBe(ModelMenuOpenFocus.Selected);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Compact

    [AvaloniaFact]
    public void Compact_Keeps_Only_The_Mark_And_The_Chevron_50_Wide()
    {
        var pill = Toolbar();
        pill.ShowText = false;
        var window = Show(pill);
        try
        {
            pill.Classes.ShouldContain(ModelPill.CompactPseudoClass);
            TextSize(pill).IsVisible.ShouldBeFalse();
            pill.Padding.ShouldBe(new Thickness(10, 0, 6, 0));
            pill.Bounds.Height.ShouldBe(36);
            pill.Bounds.Width.ShouldBe(50, 0.5);
            var mark = BoundsIn(pill.CurrentMark!, pill);
            var chevron = BoundsIn(Chevron(pill), pill);
            mark.X.ShouldBe(10, 0.5);
            (chevron.X - mark.Right).ShouldBe(4, 0.5);
            (pill.Bounds.Width - chevron.Right).ShouldBe(6, 0.5);

            pill.ShowText = true;
            Settle(window);
            pill.Classes.ShouldNotContain(ModelPill.CompactPseudoClass);
            TextSize(pill).IsVisible.ShouldBeTrue();
            pill.Padding.ShouldBe(new Thickness(10, 0, 8, 0));
            pill.Bounds.Width.ShouldBeGreaterThan(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Accessible_Name_Keeps_The_Full_Name_And_The_Effort_When_Text_Is_Hidden()
    {
        var pill = Toolbar();
        var window = Show(pill);
        try
        {
            // The toolbar shows the short name, the name says the full one.
            AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3, medium effort. Change model");
            pill.ShowEffort = false;
            pill.CurrentText!.Effort.ShouldBeNull();
            AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3, medium effort. Change model");
            pill.ShowText = false;
            AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3, medium effort. Change model");
            pill.Effort = ModelEffort.High;
            AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3, high effort. Change model");
            pill.Model = Granite();
            AutomationProperties.GetName(pill).ShouldBe("Model: Granite 70B. Change model");
            pill.Model = null;
            AutomationProperties.GetName(pill).ShouldBe("Choose a model");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void A_New_Effort_Crossfades_And_The_Width_Springs_On_Quick()
    {
        var pill = Toolbar(effort: ModelEffort.ExtraHigh);
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var size = TextSize(pill);
            size.Spring.ShouldBe(ArcSpring.Quick);
            var before = size.Bounds.Width;
            pill.Effort = ModelEffort.Low;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Layers(TextSwap(pill)).ShouldBe(2);
            Layers(MarkSwap(pill)).ShouldBe(1);
            var target = pill.CurrentText!.DesiredSize.Width;
            target.ShouldBeLessThan(before);
            size.Bounds.Width.ShouldBe(before, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            window.UpdateLayout();
            size.IsMoving.ShouldBeTrue();
            size.Bounds.Width.ShouldBeLessThan(before);
            size.Bounds.Width.ShouldBeGreaterThan(target);
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
    public void Layout_Switches_Jump_Without_A_Crossfade_Or_A_Spring()
    {
        var pill = Toolbar();
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            Settle(window);
            var size = TextSize(pill);
            var full = pill.Bounds.Width;

            // The composer narrows: the effort goes at once.
            pill.ShowEffort = false;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Layers(TextSwap(pill)).ShouldBe(1);
            size.IsMoving.ShouldBeFalse();
            size.Bounds.Width.ShouldBe(pill.CurrentText!.DesiredSize.Width, 0.5);
            pill.Bounds.Width.ShouldBeLessThan(full);

            // Narrower still: only the mark; wider again: the text is back at its width at once.
            pill.ShowText = false;
            window.UpdateLayout();
            pill.Bounds.Width.ShouldBe(50, 0.5);
            pill.Model = Swift();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            pill.ShowText = true;
            pill.ShowEffort = true;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Layers(TextSwap(pill)).ShouldBe(1);
            size.IsMoving.ShouldBeFalse();
            size.Bounds.Width.ShouldBe(pill.CurrentText!.DesiredSize.Width, 0.5);
            pill.CurrentText.Text.ShouldBe("Swift");

            // The appearance is a layout choice too.
            pill.Appearance = ModelPillAppearance.Default;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Layers(TextSwap(pill)).ShouldBe(1);
            size.IsMoving.ShouldBeFalse();
            pill.CurrentText!.Text.ShouldBe("Aurora Swift 3");
            size.Bounds.Width.ShouldBe(pill.CurrentText.DesiredSize.Width, 0.5);

            // ...while a data change still springs afterwards.
            pill.Effort = ModelEffort.Low;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            window.UpdateLayout();
            size.IsMoving.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_In_120ms_And_Jumps_The_Width_Off_Changes_At_Once()
    {
        var pill = Toolbar();
        var window = Show(pill, ArcMotionLevel.Reduced);
        try
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            pill.Effort = ModelEffort.Max;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Layers(TextSwap(pill)).ShouldBe(2);
            TextSize(pill).IsMoving.ShouldBeFalse();
            TextSize(pill).Bounds.Width.ShouldBe(pill.CurrentText!.DesiredSize.Width, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            WaitFor(() => Layers(TextSwap(pill)) == 1).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        var still = Toolbar();
        var offWindow = Show(still);
        try
        {
            still.Effort = ModelEffort.Max;
            offWindow.UpdateLayout();
            Layers(TextSwap(still)).ShouldBe(1);
            TextSize(still).Bounds.Width.ShouldBe(still.CurrentText!.DesiredSize.Width, 0.5);
            still.IsOpen = true;
            still.Transitions.ShouldBeNull();
        }
        finally
        {
            offWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Tints_In_120ms_And_Turns_The_Chevron_On_The_Css_Spring()
    {
        var pill = Toolbar();
        var window = Show(pill, ArcMotionLevel.Full);
        try
        {
            Settle(window);
            var transitions = pill.Transitions.ShouldNotBeNull();
            transitions.OfType<ArcBrushTransition>().Single(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty)
                .Duration.ShouldBe(TimeSpan.FromMilliseconds(120));
            Chevron(pill).Transitions.ShouldNotBeNull().OfType<ArcTransformOperationsTransition>().Single()
                .Duration.ShouldBe(TimeSpan.FromMilliseconds(580));
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
