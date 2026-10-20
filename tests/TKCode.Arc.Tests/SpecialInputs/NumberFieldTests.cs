using System.Globalization;
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
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.SpecialInputs;

public class NumberFieldTests
{
    private static NumberField Seats(decimal value = 3) => new()
    {
        Label = "Seats",
        Minimum = 1,
        Maximum = 10,
        Value = value,
        SuffixFormatter = n => n == 1 ? " seat" : " seats"
    };

    private static FieldIconButton Stepper(NumberField field, bool increase) =>
        TestHelpers.Descendant<FieldIconButton>(field, increase ? NumberField.IncreaseButtonPart : NumberField.DecreaseButtonPart);

    private static string LiveMessage(NumberField field) =>
        field.GetVisualDescendants().OfType<SpecialInputsLiveRegion>().First().Message;

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static Point Centre(Visual visual, Visual root) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var field = new NumberField { Label = "Seats" };
        var window = TestHelpers.Show(field);
        try
        {
            field.Value.ShouldBe(0m);
            field.Minimum.ShouldBe(0m);
            field.Maximum.ShouldBe(9007199254740991m);
            field.Step.ShouldBe(1m);
            field.LargeStep.ShouldBeNull();
            field.Culture.Name.ShouldBe("en-US");
            field.UseGrouping.ShouldBeTrue();
            field.IsScrubEnabled.ShouldBeFalse();
            field.IsLimitHintEnabled.ShouldBeTrue();
            field.Size.ShouldBe(ControlSize.Medium);
            field.Text.ShouldBe("0");
            field.ValueText.ShouldBe("0");

            var frame = TestHelpers.Descendant<FieldFrame>(field);
            frame.Bounds.Width.ShouldBe(196);
            frame.Bounds.Height.ShouldBe(44);
            Stepper(field, false).Bounds.Width.ShouldBe(34);
            Stepper(field, true).Bounds.Height.ShouldBe(34);
            Stepper(field, true).CornerRadius.ShouldBe(new CornerRadius(14));
            Stepper(field, true).FocusRingRadius.ShouldBe(new CornerRadius(18)); // concentric 4px outside
            frame.Padding.ShouldBe(new Thickness(3));
            frame.Spacing.ShouldBe(2);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcNeutralColors.Light.Border);
            field.FontSize.ShouldBe(16);
            field.FontWeight.ShouldBe(FontWeight.Medium);

            AutomationProperties.GetName(Stepper(field, false)).ShouldBe("Decrease Seats");
            AutomationProperties.GetName(Stepper(field, true)).ShouldBe("Increase Seats");
            var peer = ControlAutomationPeer.CreatePeerForElement(field);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Spinner);
            var range = peer.GetProvider<IRangeValueProvider>().ShouldNotBeNull();
            range.Minimum.ShouldBe(0);
            range.Value.ShouldBe(0);
            range.SmallChange.ShouldBe(1);
            range.LargeChange.ShouldBe(10);
            // At the minimum the minus stepper dims but stays enabled.
            field.Classes.Contains(NumberField.AtMinPseudoClass).ShouldBeTrue();
            Stepper(field, false).IsEffectivelyEnabled.ShouldBeTrue();
            Stepper(field, false).Opacity.ShouldBe(0.32, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_Answers_Hover_And_Editor_Focus_Only()
    {
        var field = Seats();
        var window = TestHelpers.Show(field);
        try
        {
            var frame = TestHelpers.Descendant<FieldFrame>(field);
            var light = ArcNeutralColors.Light;
            window.MouseMove(Centre(TestHelpers.Descendant<NumberFieldValueGroup>(field), window));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(light.BorderStrong);
            window.MouseMove(new Point(390, 290));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(light.Border);

            // Keyboard focus on a stepper fills the stepper but leaves the shell at rest.
            var plus = Stepper(field, true);
            plus.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(light.Border);
            TestHelpers.ColorOf(plus.Background).ShouldBe(light.SurfaceMuted);
            TestHelpers.ColorOf(plus.Foreground).ShouldBe(light.Foreground);

            field.Focus();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(light.Foreground);
            TestHelpers.ColorOf(plus.Foreground).ShouldBe(light.TextSecondary);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_And_Explicit_Width()
    {
        var small = new NumberField { Label = "S", Size = ControlSize.Small };
        var large = new NumberField { Label = "L", Size = ControlSize.Large };
        var wide = new NumberField { Label = "W", Width = 300 };
        var window = TestHelpers.Show(new StackPanel { Children = { small, large, wide } });
        try
        {
            TestHelpers.Descendant<FieldFrame>(small).Bounds.Width.ShouldBe(164);
            TestHelpers.Descendant<FieldFrame>(small).Bounds.Height.ShouldBe(36);
            Stepper(small, true).Bounds.Width.ShouldBe(28);
            small.FontSize.ShouldBe(14);
            TestHelpers.Descendant<FieldFrame>(large).Bounds.Width.ShouldBe(228);
            TestHelpers.Descendant<FieldFrame>(large).Bounds.Height.ShouldBe(50);
            TestHelpers.Descendant<FieldFrame>(large).Padding.ShouldBe(new Thickness(4));
            Stepper(large, true).Bounds.Width.ShouldBe(40);
            Stepper(large, true).CornerRadius.ShouldBe(new CornerRadius(13));
            large.FontSize.ShouldBe(18);
            wide.Classes.Contains(NumberField.SizedPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<FieldFrame>(wide).Bounds.Width.ShouldBe(300);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formats_With_Step_Precision_Grouping_And_Culture()
    {
        var price = new NumberField { Label = "Price", Prefix = "$", Step = 0.5m, Maximum = 99999, Value = 1234.5m };
        var cents = new NumberField { Label = "Cost", Step = 0.01m, Value = 3 };
        var loose = new NumberField { Label = "Loose", Step = 0.01m, MinimumFractionDigits = 0, Value = 1.5m };
        var plain = new NumberField { Label = "Plain", UseGrouping = false, Value = 12345 };
        var german = new NumberField { Label = "Preis", Step = 0.5m, Culture = CultureInfo.GetCultureInfo("de-DE"), Value = 1234.5m };
        var window = TestHelpers.Show(new StackPanel { Children = { price, cents, loose, plain, german } });
        try
        {
            price.Text.ShouldBe("1,234.5");
            price.ValueText.ShouldBe("$1,234.5");
            cents.Text.ShouldBe("3.00");
            loose.Text.ShouldBe("1.5");
            plain.Text.ShouldBe("12345");
            german.Text.ShouldBe("1.234,5");
            var digits = TestHelpers.Descendant<RollingDigits>(price);
            digits.Value.ShouldBe(1234.5);
            digits.Decimals.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Number_Model_Ports_Arc()
    {
        NumberFieldNumbers.DecimalsOf(0.50m).ShouldBe(1);
        NumberFieldNumbers.DecimalsOf(0.01m).ShouldBe(2);
        NumberFieldNumbers.DecimalsOf(-20m).ShouldBe(0);
        // Snap to the grid from the minimum; halves round up.
        NumberFieldNumbers.Snap(1.26m, 0, 0.5m, 1).ShouldBe(1.5m);
        NumberFieldNumbers.Snap(7m, 1, 2, 0).ShouldBe(7m);
        NumberFieldNumbers.Snap(6m, 1, 2, 0).ShouldBe(7m);
        // Off-grid values move to the next grid line in the direction of travel first.
        NumberFieldNumbers.StepFrom(1.3m, 1, 0, 0.5m, 1).ShouldBe(1.5m);
        NumberFieldNumbers.StepFrom(1.3m, -1, 0, 0.5m, 1).ShouldBe(1.0m);
        NumberFieldNumbers.StepFrom(1.5m, 1, 0, 0.5m, 1).ShouldBe(2.0m);
        NumberFieldNumbers.StepFrom(3m, -10, 0, 1, 0).ShouldBe(-7m);
        // Lenient parsing of drafts.
        NumberFieldNumbers.Parse("1,234.5", ",", ".").ShouldBe(1234.5m);
        NumberFieldNumbers.Parse("1.234,5", ".", ",").ShouldBe(1234.5m);
        NumberFieldNumbers.Parse("-12", ",", ".").ShouldBe(-12m);
        NumberFieldNumbers.Parse("5.", ",", ".").ShouldBe(5m);
        NumberFieldNumbers.Parse("-", ",", ".").ShouldBeNull();
        NumberFieldNumbers.Parse("", ",", ".").ShouldBeNull();
        NumberFieldNumbers.Parse("1-2", ",", ".").ShouldBeNull();
        NumberFieldNumbers.Filter("1a2.5-x", ",", ".", allowDecimal: false, allowMinus: false).ShouldBe("125");
        NumberFieldNumbers.Filter("-1.5", ",", ".", allowDecimal: true, allowMinus: true).ShouldBe("-1.5");
        NumberFieldNumbers.Format(1234.5m, CultureInfo.GetCultureInfo("en-US"), 0, 2, true).ShouldBe("1,234.5");
        NumberFieldNumbers.Format(2m, CultureInfo.GetCultureInfo("en-US"), 2, 2, true).ShouldBe("2.00");
    }

    [AvaloniaFact]
    public void Arrow_Keys_Step_And_Ignore_Auto_Repeat()
    {
        var field = Seats();
        var window = TestHelpers.Show(field);
        try
        {
            var sources = new List<NumberFieldChangeSource>();
            field.ValueChanged += (_, e) => sources.Add(e.ChangeSource);
            field.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(4m);
            field.HoldInterval.ShouldBe(TimeSpan.FromMilliseconds(400));
            // A key-down without a key-up is the platform's auto-repeat: the hold timer repeats instead.
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(4m);
            field.HoldTick();
            field.Value.ShouldBe(5m);
            field.HoldInterval.TotalMilliseconds.ShouldBe(150 * 0.86, 0.5);
            window.KeyReleaseQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            field.HoldInterval.ShouldBe(TimeSpan.Zero);

            Press(window, PhysicalKey.ArrowDown);
            field.Value.ShouldBe(4m);
            sources.ShouldAllBe(s => s == NumberFieldChangeSource.Key);
            field.Text.ShouldBe("4");
            field.CaretIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Large_Steps_Home_And_End()
    {
        var field = new NumberField { Label = "Items", Maximum = 1000, Value = 120 };
        var window = TestHelpers.Show(field);
        try
        {
            field.Focus();
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            field.Value.ShouldBe(130m);
            Press(window, PhysicalKey.PageDown);
            field.Value.ShouldBe(120m);
            field.LargeStep = 50;
            Press(window, PhysicalKey.PageUp);
            field.Value.ShouldBe(170m);
            Press(window, PhysicalKey.End);
            field.Value.ShouldBe(1000m);
            Press(window, PhysicalKey.Home);
            field.Value.ShouldBe(0m);
        }
        finally
        {
            window.Close();
        }

        // End does nothing while the maximum is Arc's default (Number.MAX_SAFE_INTEGER).
        var open = new NumberField { Label = "Open", Value = 5 };
        window = TestHelpers.Show(open);
        try
        {
            open.Focus();
            Press(window, PhysicalKey.End);
            open.Value.ShouldBe(5m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Press_At_A_Limit_Strains_Names_The_Limit_And_Announces()
    {
        var field = Seats(10);
        var window = TestHelpers.Show(field);
        try
        {
            field.Classes.Contains(NumberField.AtMaxPseudoClass).ShouldBeTrue();
            field.Focus();
            Press(window, PhysicalKey.ArrowUp);
            field.Value.ShouldBe(10m);
            field.Classes.Contains(NumberField.StrainPseudoClass).ShouldBeTrue();
            field.Classes.Contains(NumberField.StrainMaxPseudoClass).ShouldBeTrue();
            field.LimitNoteText.ShouldBe("Max 10 seats");
            LiveMessage(field).ShouldBe("10 seats, maximum");
            // The refused stepper takes the limit color at .6 while the strain lasts.
            var plus = Stepper(field, true);
            plus.Opacity.ShouldBe(0.6, 0.001);
            var limit = Oklch.MixOklab(ArcNeutralColors.Light.Foreground, ArcNeutralColors.Light.Warning, 0.72);
            TestHelpers.ColorOf(plus.Foreground).ShouldBe(limit);
            // Reduced / Off motion: the number briefly takes the limit color instead of kicking.
            TestHelpers.ColorOf(TestHelpers.Descendant<RollingDigits>(field).Foreground).ShouldBe(limit);
            field.ValueGroup!.BumpOffset.ShouldBe(0);

            Press(window, PhysicalKey.Home);
            field.Value.ShouldBe(1m);
            field.ValueText.ShouldBe("1 seat");
            Press(window, PhysicalKey.ArrowDown);
            field.LimitNoteText.ShouldBe("Min 1 seat");
            LiveMessage(field).ShouldBe("1 seat, minimum");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Kicks_And_Shakes_The_Refused_Stepper()
    {
        var field = Seats(10);
        var window = TestHelpers.Show(field, Motion.ArcMotionLevel.Full);
        try
        {
            field.Focus();
            Press(window, PhysicalKey.ArrowUp);
            TestHelpers.Descendant<NumberFieldStepGlyph>(field, "PART_IncreaseGlyph").ShakeCount.ShouldBe(1);
            TestHelpers.Descendant<NumberFieldStepGlyph>(field, "PART_DecreaseGlyph").ShakeCount.ShouldBe(0);
            // Full motion keeps the number's color; the kick carries the feedback.
            TestHelpers.ColorOf(TestHelpers.Descendant<RollingDigits>(field).Foreground).ShouldBe(ArcNeutralColors.Light.Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Glides_The_Group_Instead_Of_Hopping()
    {
        var field = new NumberField { Label = "Items", Maximum = 100000, Value = 1 };
        var window = TestHelpers.Show(field, Motion.ArcMotionLevel.Full);
        try
        {
            var group = TestHelpers.Descendant<NumberFieldValueGroup>(field);
            field.Focus();
            field.CaretIndex = 1;
            window.KeyTextInput("2345");
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(12345m);
            // The wider draft re-centres the group: it starts where it was (to the right) and glides home.
            group.ShiftOffset.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Limit_Hint_And_Disabled_Hint()
    {
        var custom = new NumberField
        {
            Label = "Guests",
            Maximum = 8,
            Value = 8,
            LimitHintFormatter = (edge, limit) => edge == NumberFieldLimitEdge.Maximum ? $"Room holds {limit}" : $"At least {limit}"
        };
        var quiet = new NumberField { Label = "Quiet", Maximum = 2, Value = 2, IsLimitHintEnabled = false };
        var window = TestHelpers.Show(new StackPanel { Children = { custom, quiet } });
        try
        {
            custom.Focus();
            Press(window, PhysicalKey.ArrowUp);
            custom.LimitNoteText.ShouldBe("Room holds 8");
            quiet.Focus();
            Press(window, PhysicalKey.ArrowUp);
            quiet.LimitNoteText.ShouldBeNull();
            quiet.Classes.Contains(NumberField.StrainPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Applies_Valid_Drafts_Live_And_Commits_On_Enter()
    {
        var field = Seats();
        var window = TestHelpers.Show(field);
        try
        {
            var events = new List<NumberFieldValueChangedEventArgs>();
            field.ValueChanged += (_, e) => events.Add(e);
            field.Focus();
            field.SelectAll();
            window.KeyTextInput("7");
            Dispatcher.UIThread.RunJobs();
            field.IsEditing.ShouldBeTrue();
            field.Classes.Contains(NumberField.EditingPseudoClass).ShouldBeTrue();
            field.Value.ShouldBe(7m);
            events.Last().ChangeSource.ShouldBe(NumberFieldChangeSource.Type);
            TestHelpers.Descendant<RollingDigits>(field).IsVisible.ShouldBeFalse();

            // Letters and a minus sign (minimum >= 0) never reach the draft.
            window.KeyTextInput("a-");
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe("7");

            // Past the maximum: warn at once, the note names the limit and joins the help text.
            window.KeyTextInput("5");
            Dispatcher.UIThread.RunJobs();
            field.Text.ShouldBe("75");
            field.Value.ShouldBe(7m);
            field.Classes.Contains(NumberField.WarnPseudoClass).ShouldBeTrue();
            field.LimitNoteText.ShouldBe("Max 10 seats");
            (AutomationProperties.GetHelpText(field) ?? string.Empty).ShouldContain("Max 10 seats");
            TestHelpers.ColorOf(TestHelpers.Descendant<FieldFrame>(field).BorderBrush).ShouldBe(
                Oklch.MixOklab(ArcNeutralColors.Light.Border, ArcNeutralColors.Light.Warning, 0.80));

            Press(window, PhysicalKey.Enter);
            field.IsEditing.ShouldBeFalse();
            field.Value.ShouldBe(10m);
            field.Text.ShouldBe("10");
            field.CaretIndex.ShouldBe(2);
            field.Classes.Contains(NumberField.WarnPseudoClass).ShouldBeFalse();
            field.Classes.Contains(NumberField.StrainPseudoClass).ShouldBeTrue();
            LiveMessage(field).ShouldBe("10 seats, maximum");
            TestHelpers.Descendant<RollingDigits>(field).IsVisible.ShouldBeTrue();

            // Enter without a draft selects the number.
            Press(window, PhysicalKey.Enter);
            field.SelectionStart.ShouldBe(0);
            field.SelectionEnd.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Reverts_And_Blur_Commits_Snapped()
    {
        var field = new NumberField { Label = "Price", Step = 0.5m, Maximum = 100, Value = 2 };
        var other = new TextBox();
        var window = TestHelpers.Show(new StackPanel { Children = { field, other } });
        try
        {
            field.Focus();
            field.SelectAll();
            window.KeyTextInput("9");
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(9m);
            Press(window, PhysicalKey.Escape);
            field.IsEditing.ShouldBeFalse();
            field.Value.ShouldBe(2m);
            field.Text.ShouldBe("2.0");

            field.SelectAll();
            window.KeyTextInput("3.3");
            Dispatcher.UIThread.RunJobs();
            // 3.3 is off the 0.5 grid: it waits for the commit, which snaps it.
            field.Value.ShouldBe(2m);
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            field.IsEditing.ShouldBeFalse();
            field.Value.ShouldBe(3.5m);
            field.Text.ShouldBe("3.5");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Negative_Range_Accepts_A_Minus_Sign()
    {
        var field = new NumberField { Label = "Temperature", Minimum = -20, Maximum = 40, Value = 21, Suffix = "°C" };
        var window = TestHelpers.Show(field);
        try
        {
            field.ValueText.ShouldBe("21°C");
            field.Focus();
            field.SelectAll();
            window.KeyTextInput("-5");
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(-5m);
            Press(window, PhysicalKey.Enter);
            field.Text.ShouldBe("-5");
            field.ValueText.ShouldBe("-5°C");
            Press(window, PhysicalKey.Home);
            field.Value.ShouldBe(-20m);
            Press(window, PhysicalKey.End);
            field.Value.ShouldBe(40m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_The_Minimum_Waits_Before_Warning()
    {
        var field = new NumberField { Label = "Seats", Minimum = 5, Maximum = 50, Value = 10 };
        var window = TestHelpers.Show(field);
        try
        {
            field.Focus();
            field.SelectAll();
            window.KeyTextInput("1");
            Dispatcher.UIThread.RunJobs();
            // "1" is often the start of "12": no warning yet.
            field.Classes.Contains(NumberField.WarnPseudoClass).ShouldBeFalse();
            field.Value.ShouldBe(10m);
            window.KeyTextInput("2");
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(12m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Press_On_A_Stepper_Steps_Without_Taking_Focus()
    {
        var field = Seats();
        var window = TestHelpers.Show(field);
        try
        {
            var plus = Stepper(field, true);
            var point = Centre(plus, window);
            var sources = new List<NumberFieldChangeSource>();
            field.ValueChanged += (_, e) => sources.Add(e.ChangeSource);
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(4m);
            plus.IsFocused.ShouldBeFalse();
            field.Classes.Contains(NumberField.IncreasePressedPseudoClass).ShouldBeTrue();
            TestHelpers.ColorOf(plus.Background).ShouldBe(Oklch.MixOklab(ArcNeutralColors.Light.Surface, ArcNeutralColors.Light.Foreground, 0.10));
            LiveMessage(field).ShouldBe("4 seats");
            field.HoldTick();
            field.Value.ShouldBe(5m);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            field.Classes.Contains(NumberField.IncreasePressedPseudoClass).ShouldBeFalse();
            field.HoldInterval.ShouldBe(TimeSpan.Zero);
            sources.ShouldAllBe(s => s == NumberFieldChangeSource.Button);

            // A focused editor keeps focus and its draft commits with the step.
            field.Focus();
            field.SelectAll();
            window.KeyTextInput("8");
            Dispatcher.UIThread.RunJobs();
            var minus = Stepper(field, false);
            var minusPoint = Centre(minus, window);
            window.MouseMove(minusPoint);
            window.MouseDown(minusPoint, MouseButton.Left);
            window.MouseUp(minusPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            field.IsFocused.ShouldBeTrue();
            field.IsEditing.ShouldBeFalse();
            field.Value.ShouldBe(7m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Activation_Of_A_Stepper_Takes_One_Step()
    {
        var field = Seats();
        var window = TestHelpers.Show(field);
        try
        {
            var minus = Stepper(field, false);
            minus.Focus();
            Press(window, PhysicalKey.Enter);
            field.Value.ShouldBe(2m);
            LiveMessage(field).ShouldBe("2 seats");
            // Keys pressed on a stepper never edit the draft.
            window.KeyTextInput("5");
            Dispatcher.UIThread.RunJobs();
            field.IsEditing.ShouldBeFalse();
            field.Text.ShouldBe("2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wheel_Steps_Only_While_Focused()
    {
        var field = Seats();
        var window = TestHelpers.Show(field);
        try
        {
            var point = Centre(TestHelpers.Descendant<NumberFieldValueGroup>(field), window);
            window.MouseWheel(point, new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(3m);
            field.Focus();
            window.MouseWheel(point, new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(4m);
            window.MouseWheel(point, new Vector(0, -2));
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(2m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Programmatic_Changes_Update_The_Display_Quietly()
    {
        var field = Seats();
        var window = TestHelpers.Show(field);
        try
        {
            NumberFieldValueChangedEventArgs? last = null;
            field.ValueChanged += (_, e) => last = e;
            field.Value = 1;
            Dispatcher.UIThread.RunJobs();
            last.ShouldNotBeNull();
            last.OldValue.ShouldBe(3m);
            last.NewValue.ShouldBe(1m);
            last.ChangeSource.ShouldBe(NumberFieldChangeSource.Programmatic);
            field.Text.ShouldBe("1");
            TestHelpers.Descendant<RollingDigits>(field).Value.ShouldBe(1);
            field.Classes.Contains(NumberField.AtMinPseudoClass).ShouldBeTrue();
            LiveMessage(field).ShouldBe(string.Empty);
            field.StepDown();
            field.Value.ShouldBe(1m);
            field.Classes.Contains(NumberField.StrainPseudoClass).ShouldBeFalse();
            field.StepUp();
            field.Value.ShouldBe(2m);
            last.ChangeSource.ShouldBe(NumberFieldChangeSource.Programmatic);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrub_Label_Steps_Every_Six_Pixels()
    {
        var field = new NumberField { Label = "Width", IsScrubEnabled = true, Maximum = 12, Value = 10 };
        var window = TestHelpers.Show(field);
        try
        {
            field.Classes.Contains(NumberField.ScrubPseudoClass).ShouldBeTrue();
            var label = TestHelpers.Descendant<TextBlock>(field, NumberField.LabelPart);
            label.Cursor.ShouldNotBeNull();
            var start = Centre(label, window);
            var sources = new List<NumberFieldChangeSource>();
            field.ValueChanged += (_, e) => sources.Add(e.ChangeSource);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(4, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Vector(13, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            field.Classes.Contains(NumberField.ScrubbingPseudoClass).ShouldBeTrue();
            field.Value.ShouldBe(12m);
            // Past the maximum: clamped quietly, no strain while scrubbing.
            window.MouseMove(start + new Vector(40, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(12m);
            field.Classes.Contains(NumberField.StrainPseudoClass).ShouldBeFalse();
            window.MouseMove(start + new Vector(-7, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(9m);
            window.MouseUp(start + new Vector(-7, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            field.Classes.Contains(NumberField.ScrubbingPseudoClass).ShouldBeFalse();
            sources.ShouldAllBe(s => s == NumberFieldChangeSource.Scrub);
            LiveMessage(field).ShouldBe("9");
            field.IsFocused.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Field_Ignores_Input()
    {
        var field = new NumberField { Label = "Disabled", Value = 4, Maximum = 10, IsEnabled = false };
        var window = TestHelpers.Show(field);
        try
        {
            var plus = Stepper(field, true);
            plus.IsEffectivelyEnabled.ShouldBeFalse();
            var frame = TestHelpers.Descendant<FieldFrame>(field);
            frame.Opacity.ShouldBe(0.5, 0.001);
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcNeutralColors.Light.SurfaceMuted);
            var point = Centre(plus, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            field.Value.ShouldBe(4m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Range_Value_Sets_And_Reads_The_Value()
    {
        var field = new NumberField { Label = "Price", Prefix = "$", Step = 0.5m, Maximum = 100, Value = 2 };
        var window = TestHelpers.Show(field);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(field);
            var range = peer.GetProvider<IRangeValueProvider>().ShouldNotBeNull();
            range.Maximum.ShouldBe(100);
            range.SmallChange.ShouldBe(0.5);
            range.SetValue(250);
            field.Value.ShouldBe(100m);
            peer.GetProvider<IValueProvider>()!.Value.ShouldBe("$100.0");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Affix_Characters_Are_Keyed_By_Position()
    {
        var field = Seats(2);
        var window = TestHelpers.Show(field);
        try
        {
            var suffix = TestHelpers.Descendant<NumberFieldAffixText>(field, "PART_SuffixText");
            suffix.Text.ShouldBe(" seats");
            suffix.EntryCount.ShouldBe(6);
            field.Value = 1;
            Dispatcher.UIThread.RunJobs();
            suffix.Text.ShouldBe(" seat");
            suffix.EntryCount.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }
}
