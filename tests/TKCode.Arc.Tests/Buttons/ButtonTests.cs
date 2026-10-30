using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Buttons;

public class ButtonTests
{
    [AvaloniaFact]
    public void Loading_Swallows_Clicks_But_Keeps_Focus()
    {
        var clicks = 0;
        var button = new Button { Content = "Save", IsLoading = true };
        button.Click += (_, _) => clicks++;
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            button.IsFocused.ShouldBeTrue();
            button.Classes.Contains(Button.LoadingPseudoClass).ShouldBeTrue();
            button.IsLoading = false;
            button.Classes.Contains(Button.LoadingPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spinner_Is_Built_On_First_Load_And_Sized_By_Token()
    {
        var button = new Button { Content = "Save" };
        var window = TestHelpers.Show(button);
        try
        {
            button.GetVisualDescendants().OfType<Primitives.ArcSpinner>().ShouldBeEmpty();
            button.IsLoading = true;
            window.UpdateLayout();
            var spinner = TestHelpers.Descendant<Primitives.ArcSpinner>(button, "PART_Spinner");
            spinner.IsVisible.ShouldBeTrue();
            spinner.Width.ShouldBe(16);
            button.IsLoading = false;
            spinner.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Template_Uses_Token_Heights_For_Sizes()
    {
        var small = new Button { Content = "A", Size = ControlSize.Small };
        var large = new Button { Content = "A", Size = ControlSize.Large };
        var panel = new Avalonia.Controls.StackPanel { Children = { small, large } };
        var window = TestHelpers.Show(panel);
        try
        {
            small.MinHeight.ShouldBe(36);
            large.MinHeight.ShouldBe(50);
            small.Bounds.Height.ShouldBe(36);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_Only_The_Arc_Ring()
    {
        var button = new Button { Content = "Save" };
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus(Avalonia.Input.NavigationMethod.Tab);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var layer = Avalonia.Controls.Primitives.AdornerLayer.GetAdornerLayer(button);
            layer.ShouldNotBeNull();
            // The window's default focus adorner would be added to the adorner layer; Arc draws PART_FocusRing instead.
            layer!.Children.Count.ShouldBe(0);
            TestHelpers.Descendant<Avalonia.Controls.Border>(button, "PART_FocusRing").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Label_Morph_Keeps_Blur_And_Morph_Unless_A_Button_Opts_In()
    {
        var plain = new Button { Content = "Save" };
        var quiet = new Button { Content = "Save", LabelSwapMode = Primitives.ContentSwapMode.Label, LabelSpring = Motion.ArcSpring.Quick };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { plain, quiet } });
        try
        {
            // Every existing button keeps the blur swap and the morph spring.
            plain.LabelSwapMode.ShouldBe(Primitives.ContentSwapMode.BlurSwap);
            plain.LabelSpring.ShouldBe(Motion.ArcSpring.Morph);
            TestHelpers.Descendant<Primitives.ArcContentSwap>(plain, "PART_Label").Mode.ShouldBe(Primitives.ContentSwapMode.BlurSwap);
            TestHelpers.Descendant<Primitives.SpringSizeDecorator>(plain, "PART_LabelSlot").Spring.ShouldBe(Motion.ArcSpring.Morph);

            // The opt-in reaches the template, and follows later changes.
            TestHelpers.Descendant<Primitives.ArcContentSwap>(quiet, "PART_Label").Mode.ShouldBe(Primitives.ContentSwapMode.Label);
            TestHelpers.Descendant<Primitives.SpringSizeDecorator>(quiet, "PART_LabelSlot").Spring.ShouldBe(Motion.ArcSpring.Quick);
            quiet.LabelSwapMode = Primitives.ContentSwapMode.Crossfade;
            TestHelpers.Descendant<Primitives.ArcContentSwap>(quiet, "PART_Label").Mode.ShouldBe(Primitives.ContentSwapMode.Crossfade);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Quick_Label_Spring_Grows_Without_Overshoot_Where_Morph_Bounces()
    {
        // Morph has a slight bounce (0.16), Quick none: a longer label makes the morph slot pass its final width once.
        double Peak(Motion.ArcSpring spring, out double final)
        {
            var clock = new Motion.ManualFrameClock();
            using var _ = clock.Install();
            var button = new Button { Content = "Go", LabelSpring = spring, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { button } }, Motion.ArcMotionLevel.Full);
            try
            {
                // Wide enough that the longer label is not cut to the window.
                window.Width = 900;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var slot = TestHelpers.Descendant<Primitives.SpringSizeDecorator>(button, "PART_LabelSlot");
                button.Content = "Continue to checkout with card";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var peak = 0.0;
                for (var i = 0; i < 90; i++)
                {
                    clock.Advance(TimeSpan.FromSeconds(1.0 / 60.0));
                    window.UpdateLayout();
                    peak = Math.Max(peak, slot.Bounds.Width);
                }
                slot.IsMoving.ShouldBeFalse();
                final = slot.Bounds.Width;
                return peak;
            }
            finally
            {
                window.Close();
            }
        }

        var morphPeak = Peak(Motion.ArcSpring.Morph, out var morphFinal);
        var quickPeak = Peak(Motion.ArcSpring.Quick, out var quickFinal);
        quickFinal.ShouldBe(morphFinal, 0.5);
        quickFinal.ShouldBeLessThan(880);
        morphPeak.ShouldBeGreaterThan(morphFinal + 0.5);
        quickPeak.ShouldBeLessThanOrEqualTo(quickFinal + 0.01);
    }
}
