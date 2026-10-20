using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class BillingToggleTests
{
    [AvaloniaFact]
    public void Defaults_To_Monthly_And_Yearly_With_First_Selected()
    {
        var toggle = new BillingToggle();
        var window = TestHelpers.Show(toggle);
        try
        {
            toggle.ItemCount.ShouldBe(2);
            toggle.SelectedIndex.ShouldBe(0, $"value={toggle.Value} sv={toggle.SelectedValue} item={toggle.SelectedItem}");
            toggle.Value.ShouldBe("monthly", $"value={toggle.Value} sv={toggle.SelectedValue} item={toggle.SelectedItem}");
            AutomationProperties.GetName(toggle).ShouldBe("Billing period");
            var yearly = toggle.ContainerFromIndex(1).ShouldBeOfType<BillingToggleItem>();
            yearly.CurrentBadge.ShouldBe("Save 20%");
            AutomationProperties.GetName(yearly).ShouldBe("Yearly Save 20%");
            yearly.Bounds.Height.ShouldBe(34);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Value_Set_Before_Items_Is_Kept()
    {
        var toggle = new BillingToggle { Value = "yearly" };
        var window = TestHelpers.Show(toggle);
        try
        {
            toggle.SelectedIndex.ShouldBe(1);
            toggle.Value.ShouldBe("yearly");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Value_Is_Two_Way_And_Raises_Typed_Event()
    {
        var model = new Model { Period = "yearly" };
        var toggle = new BillingToggle
        {
            DataContext = model,
            ItemsSource = new[]
            {
                new BillingToggleOption("monthly", "Monthly"),
                new BillingToggleOption("yearly", "Yearly", "Save 20%", "You save $48"),
                new BillingToggleOption("lifetime", "Lifetime", "Best value")
            }
        };
        toggle.Bind(BillingToggle.ValueProperty, new Binding(nameof(Model.Period)));
        var changes = new List<(string? Old, string? New)>();
        toggle.ValueChanged += (_, e) => changes.Add((e.OldValue, e.NewValue));
        var window = TestHelpers.Show(toggle);
        try
        {
            toggle.SelectedIndex.ShouldBe(1);
            toggle.ContainerFromIndex(1).ShouldBeOfType<BillingToggleItem>().CurrentBadge.ShouldBe("You save $48");
            toggle.ContainerFromIndex(1)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            model.Period.ShouldBe("lifetime");
            toggle.ContainerFromIndex(1).ShouldBeOfType<BillingToggleItem>().CurrentBadge.ShouldBe("Save 20%");
            changes.ShouldContain(("yearly", "lifetime"));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            model.Period.ShouldBe("monthly");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumb_Glides_On_A_Critically_Damped_Spring()
    {
        var clock = new TKCode.Arc.Motion.ManualFrameClock();
        using var _ = clock.Install();
        var toggle = new BillingToggle();
        var window = TestHelpers.Show(toggle, TKCode.Arc.Motion.ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var thumb = TestHelpers.Descendant<GlidingIndicator>(toggle, BillingToggle.ThumbPart);
            thumb.CornerRadius.TopLeft.ShouldBeGreaterThanOrEqualTo(thumb.CurrentRect.Height / 2);
            var from = thumb.CurrentRect;
            toggle.Value = "yearly";
            Dispatcher.UIThread.RunJobs();
            var yearly = toggle.ContainerFromIndex(1)!;
            var target = new Rect(yearly.TranslatePoint(default, thumb)!.Value, yearly.Bounds.Size);
            var t = CorePrimitives.CoreFrames.Run(clock, 6);
            // Arc's glide: visualDuration 0.34, bounce 0.
            BillingToggle.ThumbGlide.ShouldBe(TKCode.Arc.Motion.SpringSpec.FromVisual(0.34, 0));
            var (dx, _) = TKCode.Arc.Motion.SpringSolver.Evaluate(BillingToggle.ThumbGlide, from.X - target.X, 0, t);
            thumb.CurrentRect.X.ShouldBe(target.X + dx, 0.01);
            clock.Advance(TimeSpan.FromSeconds(1));
            thumb.CurrentRect.X.ShouldBe(target.X, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumb_Takes_The_Selected_Option_Box()
    {
        var toggle = new BillingToggle { Value = "yearly" };
        var window = TestHelpers.Show(toggle);
        try
        {
            var thumb = TestHelpers.Descendant<GlidingIndicator>(toggle, BillingToggle.ThumbPart);
            var yearly = toggle.ContainerFromIndex(1)!;
            thumb.CurrentRect.X.ShouldBe(yearly.Bounds.X, 0.01);
            thumb.CurrentRect.Width.ShouldBe(yearly.Bounds.Width, 0.01);
            toggle.Bounds.Height.ShouldBe(34 + 6 + 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Price_Shows_Old_Price_Only_When_Higher()
    {
        var price = new BillingPrice { Amount = 24, WasAmount = 30, Period = "per month" };
        var window = TestHelpers.Show(price);
        try
        {
            price.ShowsWas.ShouldBeTrue();
            price.WasText.ShouldBe("$30");
            AutomationProperties.GetName(price).ShouldBe("$24, was $30 per month");
            price.WasAmount = 20;
            price.ShowsWas.ShouldBeFalse();
            TestHelpers.Descendant<AnimatedCounter>(price).Value.ShouldBe(24);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Model : System.ComponentModel.INotifyPropertyChanged
    {
        private string? _period;

        public string? Period
        {
            get => _period;
            set
            {
                _period = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Period)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
