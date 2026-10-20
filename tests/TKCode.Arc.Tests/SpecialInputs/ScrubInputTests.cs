using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.SpecialInputs;

public class ScrubInputTests
{
    private static string[] None => [];

    private static (ScrubInput Input, Window Window) Show(ScrubInput input, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        input.Width = 300;
        var window = TestHelpers.Show(input, level);
        return (input, window);
    }

    private static ScrubInputEditor Editor(ScrubInput input) => TestHelpers.Descendant<ScrubInputEditor>(input);

    private static ContentPresenter Handle(ScrubInput input) => TestHelpers.Descendant<ContentPresenter>(input, ScrubInput.HandlePart);

    private static Point HandleCenter(ScrubInput input, Window window)
    {
        var handle = Handle(input);
        return handle.TranslatePoint(new Point(handle.Bounds.Width / 2, handle.Bounds.Height / 2), window)!.Value;
    }

    private static void Type(Window window, ScrubInput input, string text)
    {
        var editor = Editor(input);
        editor.Focus();
        editor.SelectAll();
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<ScrubInputValueChangedEventArgs> Record(ScrubInput input, out List<ScrubInputValueChangedEventArgs> committed)
    {
        var changed = new List<ScrubInputValueChangedEventArgs>();
        var commits = new List<ScrubInputValueChangedEventArgs>();
        input.ValueChanged += (_, e) => changed.Add(e);
        input.ValueCommitted += (_, e) => commits.Add(e);
        committed = commits;
        return changed;
    }

    #region Expression and formatting

    [Theory]
    [InlineData("12*2", 24)]
    [InlineData("(100-24)/2", 38)]
    [InlineData("1,5", 1.5)]
    [InlineData("3x4", 12)]
    [InlineData("3 × 4", 12)]
    [InlineData("10÷4", 2.5)]
    [InlineData("−5+2", -3)]
    [InlineData("-(2+3)*2", -10)]
    [InlineData("1e3", 1000)]
    [InlineData(".5", 0.5)]
    [InlineData("  7  ", 7)]
    public void Expression_Evaluates_Arithmetic(string text, double expected)
    {
        var result = ScrubExpression.Evaluate(text, None);
        result.ShouldNotBeNull();
        result.Value.Value.ShouldBe(expected, 1e-9);
        result.Value.Unit.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("4/0")]
    [InlineData("0/0")]
    [InlineData("(1+2")]
    [InlineData("1+")]
    [InlineData("12px")]
    [InlineData("1..2")]
    [InlineData("2 3")]
    public void Expression_Rejects_Junk(string text)
    {
        ScrubExpression.Evaluate(text, ["%"]).ShouldBeNull();
    }

    [Fact]
    public void Expression_Reads_Units_And_Degree_Aliases()
    {
        ScrubExpression.Evaluate("50%", ["px", "%"]).ShouldBe(new ScrubExpressionResult(50, "%"));
        ScrubExpression.Evaluate("2EM", ["px", "em"]).ShouldBe(new ScrubExpressionResult(2, "em"));
        ScrubExpression.Evaluate("90deg", ["°"]).ShouldBe(new ScrubExpressionResult(90, "°"));
        ScrubExpression.Evaluate("45°", ["deg"]).ShouldBe(new ScrubExpressionResult(45, "deg"));
        ScrubExpression.Evaluate("10px*2", ["px"]).ShouldBe(new ScrubExpressionResult(20, "px"));
        ScrubExpression.Evaluate("10pt", ["px"]).ShouldBeNull();
    }

    [Fact]
    public void Numbers_Format_Like_JavaScript()
    {
        ScrubInputNumber.ToJsString(123.456).ShouldBe("123.456");
        ScrubInputNumber.ToJsString(1e21).ShouldBe("1e+21");
        ScrubInputNumber.ToJsString(1e-7).ShouldBe("1e-7");
        ScrubInputNumber.ToJsString(0.000001).ShouldBe("0.000001");
        ScrubInputNumber.ToJsString(1e20).ShouldBe("100000000000000000000");
        ScrubInputNumber.ToJsString(-2.5).ShouldBe("-2.5");
        ScrubInputNumber.DecimalsOf(0.1).ShouldBe(1);
        ScrubInputNumber.DecimalsOf(0.25).ShouldBe(2);
        ScrubInputNumber.DecimalsOf(1e-7).ShouldBe(7);
        ScrubInputNumber.DecimalsOf(10).ShouldBe(0);
        ScrubInputNumber.JsRound(-2.5).ShouldBe(-2);
        ScrubInputNumber.JsRound(2.5).ShouldBe(3);
        ScrubInputNumber.Format(0.1 + 0.2, 2).ShouldBe("0.3");
        ScrubInputNumber.Format(-0.0001, 2).ShouldBe("0");
        ScrubInputNumber.Format(1.25, 1).ShouldBe("1.3");
        ScrubInputNumber.Format(40, 1).ShouldBe("40");
    }

    #endregion

    #region Defaults and display

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var (input, window) = Show(new ScrubInput { Label = "X" });
        try
        {
            input.Value.ShouldBe(0);
            input.Minimum.ShouldBe(double.NegativeInfinity);
            input.Maximum.ShouldBe(double.PositiveInfinity);
            input.Step.ShouldBe(1);
            input.EffectiveFineStep.ShouldBe(0.1);
            input.EffectiveCoarseStep.ShouldBe(10);
            input.EffectivePrecision.ShouldBe(1);
            input.PixelsPerStep.ShouldBe(2);
            input.IsWrapping.ShouldBeFalse();
            input.IsPointerLockEnabled.ShouldBeTrue();
            input.Size.ShouldBe(ControlSize.Medium);
            input.Bounds.Height.ShouldBe(36);
            input.FontSize.ShouldBe(14);
            Editor(input).Text.ShouldBe("0");
            input.ShowTrack.ShouldBeFalse();
            TestHelpers.Descendant<ScrubInputUnitButton>(input).IsVisible.ShouldBeFalse();
            input.Classes.Contains(ScrubInput.TextLabelPseudoClass).ShouldBeTrue();
            Handle(input).FontWeight.ShouldBe(Avalonia.Media.FontWeight.Medium);
            ScrubInput.ValueProperty.GetMetadata(typeof(ScrubInput)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
            ScrubInput.UnitProperty.GetMetadata(typeof(ScrubInput)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_Is_Muted_Rounded_And_Small_Is_30px()
    {
        var (input, window) = Show(new ScrubInput { Label = "X" });
        try
        {
            var shell = TestHelpers.Descendant<ScrubInputShell>(input);
            shell.CornerRadius.TopLeft.ShouldBe(12);
            TestHelpers.ColorOf(shell.ShellBorderBrush).A.ShouldBe((byte)0);
            TestHelpers.ColorOf(shell.ShellBackground)
                .ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.SurfaceMuted));
            Handle(input).MinWidth.ShouldBe(30);
            Handle(input).Padding.ShouldBe(new Thickness(10, 0, 4, 0));
            input.Size = ControlSize.Small;
            Dispatcher.UIThread.RunJobs();
            input.Bounds.Height.ShouldBe(30);
            input.FontSize.ShouldBe(12);
            shell.CornerRadius.TopLeft.ShouldBe(9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Shows_Rounded_Value_And_Programmatic_Changes_Raise_ValueChanged_Only()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 1.25, Unit = "px" });
        try
        {
            var changed = Record(input, out var committed);
            Editor(input).Text.ShouldBe("1.3");
            input.Value = -0.01;
            Dispatcher.UIThread.RunJobs();
            Editor(input).Text.ShouldBe("0");
            changed.Count.ShouldBe(1);
            changed[0].OldValue.ShouldBe(1.25);
            changed[0].NewValue.ShouldBe(-0.01);
            changed[0].ChangeSource.ShouldBe(ScrubInputChangeSource.Programmatic);
            changed[0].Unit.ShouldBe("px");
            input.Value = -0.01;
            input.Step = 0.25;
            Dispatcher.UIThread.RunJobs();
            input.EffectivePrecision.ShouldBe(3);
            Editor(input).Text.ShouldBe("-0.01");
            changed.Count.ShouldBe(1);
            committed.ShouldBeEmpty();
            input.IsInvalid.ShouldBeFalse();
            input.DisplayedStrain.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Template_Application_Raises_No_Event()
    {
        var input = new ScrubInput { Label = "X", Value = 7, Width = 300 };
        var changed = Record(input, out var committed);
        var window = TestHelpers.Show(input);
        try
        {
            Editor(input).Text.ShouldBe("7");
            changed.ShouldBeEmpty();
            committed.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Two_Way_Binding_Change_Is_Reported_As_Programmatic()
    {
        var source = new ScrubInput { Value = 3 };
        var (input, window) = Show(new ScrubInput { Label = "X" });
        try
        {
            input.Bind(ScrubInput.ValueProperty, new Avalonia.Data.Binding(nameof(ScrubInput.Value)) { Source = source, Mode = Avalonia.Data.BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(3);
            var changed = Record(input, out _);
            source.Value = 9;
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(9);
            changed.Single().ChangeSource.ShouldBe(ScrubInputChangeSource.Programmatic);
            Editor(input).Focus();
            Press(window, PhysicalKey.ArrowUp);
            source.Value.ShouldBe(10);
            changed.Count.ShouldBe(2);
            changed[1].ChangeSource.ShouldBe(ScrubInputChangeSource.Key);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Track_Shows_Fraction_Between_Finite_Limits()
    {
        var (input, window) = Show(new ScrubInput { Label = "O", Minimum = 0, Maximum = 100, Value = 25, Unit = "%" });
        try
        {
            input.ShowTrack.ShouldBeTrue();
            input.DisplayedFraction.ShouldBe(0.25, 1e-9);
            input.Value = 80;
            Dispatcher.UIThread.RunJobs();
            input.DisplayedFraction.ShouldBe(0.8, 1e-9);
            input.IsTrackVisible = false;
            input.ShowTrack.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Typing

    [AvaloniaFact]
    public void Enter_Commits_Arithmetic_And_Selects_All()
    {
        var (input, window) = Show(new ScrubInput { Label = "W", Value = 10, Unit = "px" });
        try
        {
            var changed = Record(input, out var committed);
            Type(window, input, "12*2");
            Editor(input).Text.ShouldBe("12*2");
            input.Value.ShouldBe(10);
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(24);
            Editor(input).Text.ShouldBe("24");
            Editor(input).SelectedText.ShouldBe("24");
            changed.Count.ShouldBe(1);
            changed[0].OldValue.ShouldBe(10);
            changed[0].NewValue.ShouldBe(24);
            changed[0].ChangeSource.ShouldBe(ScrubInputChangeSource.Type);
            changed[0].Unit.ShouldBe("px");
            committed.Count.ShouldBe(1);
            committed[0].ChangeSource.ShouldBe(ScrubInputChangeSource.Type);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Reverts_A_Draft_And_Blur_Commits()
    {
        var other = new Button { Content = "Other" };
        var input = new ScrubInput { Label = "X", Value = 5, Width = 300 };
        var window = TestHelpers.Show(new StackPanel { Children = { input, other } });
        try
        {
            var changed = Record(input, out _);
            Type(window, input, "9");
            Press(window, PhysicalKey.Escape);
            Editor(input).Text.ShouldBe("5");
            Editor(input).SelectedText.ShouldBe("5");
            input.Value.ShouldBe(5);
            changed.ShouldBeEmpty();

            Type(window, input, "7");
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(7);
            changed.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Invalid_Entry_Keeps_The_Old_Value_And_Flags_Invalid()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 3 });
        try
        {
            var changed = Record(input, out var committed);
            Type(window, input, "abc");
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(3);
            Editor(input).Text.ShouldBe("3");
            input.IsInvalid.ShouldBeTrue();
            input.Classes.Contains(ScrubInput.InvalidPseudoClass).ShouldBeTrue();
            var shell = TestHelpers.Descendant<ScrubInputShell>(input);
            TestHelpers.ColorOf(shell.ShellBorderBrush).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.Danger));
            var peer = ControlAutomationPeer.CreatePeerForElement(Editor(input));
            peer.GetItemStatus().ShouldBe("Invalid entry");
            changed.ShouldBeEmpty();
            committed.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Out_Of_Range_Entry_Clamps_And_Commits()
    {
        var (input, window) = Show(new ScrubInput { Label = "O", Minimum = 0, Maximum = 100, Value = 40 });
        try
        {
            var changed = Record(input, out var committed);
            Type(window, input, "150");
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(100);
            changed.Count.ShouldBe(1);
            Type(window, input, "120");
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(100);
            changed.Count.ShouldBe(1);
            committed.Count.ShouldBe(2);
            input.IsInvalid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typed_Unit_Switches_Unit()
    {
        var (input, window) = Show(new ScrubInput { Label = "W", Value = 10, Units = ["px", "%"] });
        try
        {
            input.EffectiveUnit.ShouldBe("px");
            var changed = Record(input, out _);
            Type(window, input, "50%");
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(50);
            input.Unit.ShouldBe("%");
            changed[0].Unit.ShouldBe("%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_Into_The_Number_Selects_It()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 1234 });
        try
        {
            var editor = Editor(input);
            var point = editor.TranslatePoint(new Point(editor.Bounds.Width - 20, editor.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.IsFocused.ShouldBeTrue();
            editor.SelectedText.ShouldBe("1234");
            var shell = TestHelpers.Descendant<ScrubInputShell>(input);
            TestHelpers.ColorOf(shell.ShellBackground).ShouldBe(ArcTokens.GetColor(input, DesignTokens.ArcDesignTokenKind.Surface));
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Keys

    [AvaloniaFact]
    public void Arrows_Step_With_Coarse_Grid_And_Fine_Step()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 13 });
        try
        {
            var changed = Record(input, out var committed);
            Editor(input).Focus();
            Press(window, PhysicalKey.ArrowUp);
            input.Value.ShouldBe(14);
            Press(window, PhysicalKey.ArrowDown);
            input.Value.ShouldBe(13);
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            input.Value.ShouldBe(20);
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            input.Value.ShouldBe(30);
            Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            input.Value.ShouldBe(20);
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            input.Value.ShouldBe(20.1, 1e-9);
            Editor(input).Text.ShouldBe("20.1");
            Editor(input).SelectedText.ShouldBe("20.1");
            changed.Count.ShouldBe(6);
            committed.Count.ShouldBe(6);
            changed.ShouldAllBe(e => e.ChangeSource == ScrubInputChangeSource.Key);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stepping_At_A_Limit_Bumps_Without_Events()
    {
        var (input, window) = Show(new ScrubInput { Label = "O", Minimum = 0, Maximum = 10, Value = 10 }, ArcMotionLevel.Full);
        try
        {
            var changed = Record(input, out var committed);
            Editor(input).Focus();
            Press(window, PhysicalKey.ArrowUp);
            input.Value.ShouldBe(10);
            changed.ShouldBeEmpty();
            committed.ShouldBeEmpty();
            Press(window, PhysicalKey.ArrowDown);
            input.Value.ShouldBe(9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wrapping_Wraps_Instead_Of_Clamping()
    {
        var (input, window) = Show(new ScrubInput { Label = "R", Minimum = 0, Maximum = 360, IsWrapping = true, Value = 359, Unit = "°" });
        try
        {
            Editor(input).Focus();
            Press(window, PhysicalKey.ArrowUp);
            input.Value.ShouldBe(0);
            Press(window, PhysicalKey.ArrowDown);
            input.Value.ShouldBe(359);
            Type(window, input, "370");
            Press(window, PhysicalKey.Enter);
            input.Value.ShouldBe(10);
            input.ValueText.ShouldBe("10 degrees");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Units

    [AvaloniaFact]
    public void Unit_Button_Cycles_Units_And_Is_Named()
    {
        var (input, window) = Show(new ScrubInput { Label = "W", Value = 10, Units = ["px", "%", "em"] });
        try
        {
            var button = TestHelpers.Descendant<ScrubInputUnitButton>(input);
            button.IsVisible.ShouldBeTrue();
            button.IsCycling.ShouldBeTrue();
            button.Focusable.ShouldBeTrue();
            AutomationProperties.GetName(button).ShouldBe("Unit: px. Change unit");
            button.Focus();
            Press(window, PhysicalKey.Enter);
            input.Unit.ShouldBe("%");
            AutomationProperties.GetName(button).ShouldBe("Unit: %. Change unit");
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.Enter);
            input.Unit.ShouldBe("px");
            TestHelpers.Descendant<ScrubInputUnitSwap>(button).CurrentLayer.Text.ShouldBe("px");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Unit_Is_Plain_Text()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Unit = "px" });
        try
        {
            var button = TestHelpers.Descendant<ScrubInputUnitButton>(input);
            button.IsVisible.ShouldBeTrue();
            button.IsCycling.ShouldBeFalse();
            button.Focusable.ShouldBeFalse();
            button.IsHitTestVisible.ShouldBeFalse();
            button.Bounds.Height.ShouldBe(22);
            button.FontSize.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unit_Swap_Animates_Only_When_Cycling_With_Motion()
    {
        var (input, window) = Show(new ScrubInput { Label = "W", Units = ["px", "%"] }, ArcMotionLevel.Full);
        try
        {
            var swap = TestHelpers.Descendant<ScrubInputUnitSwap>(input);
            swap.IsExiting.ShouldBeFalse();
            input.Unit = "%";
            Dispatcher.UIThread.RunJobs();
            swap.CurrentLayer.Text.ShouldBe("%");
            swap.IsExiting.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Scrubbing

    [AvaloniaFact]
    public void Dragging_The_Label_Scrubs_And_Commits_On_Release()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 10 });
        try
        {
            var changed = Record(input, out var committed);
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.IsScrubbing.ShouldBeTrue();
            input.Classes.Contains(ScrubInput.ScrubbingPseudoClass).ShouldBeTrue();
            input.Value.ShouldBe(15);
            window.MouseMove(start + new Vector(30, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(25);
            Editor(input).Text.ShouldBe("25");
            window.MouseMove(start + new Vector(20, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(20);
            committed.ShouldBeEmpty();
            window.MouseUp(start + new Vector(20, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.IsScrubbing.ShouldBeFalse();
            changed.Count.ShouldBe(3);
            changed.ShouldAllBe(e => e.ChangeSource == ScrubInputChangeSource.Scrub);
            committed.Count.ShouldBe(1);
            committed[0].OldValue.ShouldBe(10);
            committed[0].NewValue.ShouldBe(20);
            committed[0].ChangeSource.ShouldBe(ScrubInputChangeSource.Scrub);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Modifiers_Switch_Step_Mid_Drag_And_Show_The_Tag()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 0 });
        try
        {
            var start = HandleCenter(input, window);
            var tag = TestHelpers.Descendant<Avalonia.Controls.Border>(input, ScrubInput.TagPart);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(4, 0), RawInputModifiers.Shift | RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(20);
            input.MultiplierText.ShouldBe("×10");
            tag.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(tag).Text.ShouldBe("×10");
            window.MouseMove(start + new Vector(8, 0), RawInputModifiers.Alt | RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(20.2, 1e-9);
            input.MultiplierText.ShouldBe("×0.1");
            window.MouseMove(start + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.MultiplierText.ShouldBeNull();
            tag.IsVisible.ShouldBeFalse();
            input.Value.ShouldBe(21);
            window.MouseUp(start + new Vector(10, 0), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrubbing_Past_A_Limit_Strains_Then_Releases()
    {
        var (input, window) = Show(new ScrubInput { Label = "O", Minimum = 0, Maximum = 10, Value = 8 }, ArcMotionLevel.Full);
        try
        {
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(4, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Vector(44, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(10);
            // The first 4px reach the max (8 + 2 steps); the next 40px are all overshoot.
            var expected = RubberBandStrain(40);
            input.DisplayedStrain.ShouldBe(expected, 1e-6);
            input.DisplayedStrain.ShouldBeLessThan(12);
            TestHelpers.Descendant<ScrubInputShell>(input).Strain.ShouldBe(expected, 1e-6);
            window.MouseMove(start + new Vector(4000, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.DisplayedStrain.ShouldBe(RubberBandStrain(90), 1e-6);
            // The stored overshoot is capped at 90px: reversing 100px relaxes and moves the value again.
            window.MouseMove(start + new Vector(4000 - 100, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(5);
            input.DisplayedStrain.ShouldBe(0);
            window.MouseUp(start + new Vector(3900, 0), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    private static double RubberBandStrain(double over) => Math.Sign(over) * (1 - 1 / (Math.Abs(over) * 0.55 / 12 + 1)) * 12;

    [AvaloniaFact]
    public void Reduced_Motion_Has_No_Strain()
    {
        var (input, window) = Show(new ScrubInput { Label = "O", Minimum = 0, Maximum = 10, Value = 10 }, ArcMotionLevel.Reduced);
        try
        {
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(60, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(10);
            input.DisplayedStrain.ShouldBe(0);
            window.MouseUp(start + new Vector(60, 0), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Cancels_A_Scrub_Back_To_The_Start()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 4 });
        try
        {
            var changed = Record(input, out var committed);
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(20, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(14);
            Press(window, PhysicalKey.Escape);
            input.Value.ShouldBe(4);
            input.IsScrubbing.ShouldBeFalse();
            changed.Last().NewValue.ShouldBe(4);
            committed.Count.ShouldBe(1);
            committed[0].NewValue.ShouldBe(4);
            committed[0].ChangeSource.ShouldBe(ScrubInputChangeSource.Scrub);
            window.MouseMove(start + new Vector(40, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(40, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(4);
            committed.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrub_Commits_An_Open_Draft_And_Blurs_The_Editor()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 1 });
        try
        {
            var changed = Record(input, out _);
            Type(window, input, "50");
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(50);
            changed[0].ChangeSource.ShouldBe(ScrubInputChangeSource.Type);
            window.MouseMove(start + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Editor(input).IsFocused.ShouldBeFalse();
            input.Value.ShouldBe(55);
            window.MouseUp(start + new Vector(10, 0), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Label_Focuses_And_Selects_The_Number()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 42 });
        try
        {
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start + new Vector(1, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Editor(input).IsFocused.ShouldBeTrue();
            Editor(input).SelectedText.ShouldBe("42");
            input.IsScrubbing.ShouldBeFalse();
            input.Classes.Contains(":focus-within").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Double_Click_On_The_Label_Resets()
    {
        var (input, window) = Show(new ScrubInput { Label = "W", Value = 42, ResetValue = 100 });
        try
        {
            var changed = Record(input, out var committed);
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start, MouseButton.Left);
            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(100);
            changed.Single().ChangeSource.ShouldBe(ScrubInputChangeSource.Reset);
            committed.Single().ChangeSource.ShouldBe(ScrubInputChangeSource.Reset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Touch_Does_Not_Scrub()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 3 });
        try
        {
            var start = HandleCenter(input, window);
            var touch = window.TouchBegin(start, RawInputModifiers.None);
            window.TouchMove(touch, start + new Vector(1, 30), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            input.IsScrubbing.ShouldBeFalse();
            window.TouchEnd(touch, start + new Vector(1, 30), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(3);
            Editor(input).IsFocused.ShouldBeFalse();

            var horizontal = window.TouchBegin(start, RawInputModifiers.None);
            window.TouchMove(horizontal, start + new Vector(20, 2), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(13);
            window.TouchEnd(horizontal, start + new Vector(20, 2), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            input.IsScrubbing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Field_Dims_And_Ignores_Scrubbing()
    {
        var (input, window) = Show(new ScrubInput { Label = "X", Value = 3, IsEnabled = false });
        try
        {
            input.Opacity.ShouldBe(0.5);
            var start = HandleCenter(input, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(40, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(40, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(3);
            input.IsScrubbing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Automation

    [AvaloniaFact]
    public void Editor_Is_A_Named_Spinner_With_Value_Text_And_Range()
    {
        var (input, window) = Show(new ScrubInput { Label = "Opacity", Minimum = 0, Maximum = 100, Value = 50, Unit = "%" });
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(Editor(input));
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Spinner);
            peer.GetName().ShouldBe("Opacity");
            peer.GetHelpText().ShouldBe("50 percent");
            var range = peer.GetProvider<IRangeValueProvider>();
            range.ShouldNotBeNull();
            range.Minimum.ShouldBe(0);
            range.Maximum.ShouldBe(100);
            range.Value.ShouldBe(50);
            range.SetValue(120);
            input.Value.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unbounded_Field_Has_No_Range_And_Glyph_Label_Uses_Automation_Name()
    {
        var glyph = new ArcGlyph { Data = ArcIcons.RotateCw };
        var input = new ScrubInput { Label = glyph, Value = 90, Unit = "°" };
        AutomationProperties.SetName(input, "Rotation");
        var (_, window) = Show(input);
        try
        {
            glyph.Size.ShouldBe(14);
            input.Classes.Contains(ScrubInput.TextLabelPseudoClass).ShouldBeFalse();
            var peer = ControlAutomationPeer.CreatePeerForElement(Editor(input));
            peer.GetName().ShouldBe("Rotation");
            peer.GetHelpText().ShouldBe("90 degrees");
            peer.GetProvider<IRangeValueProvider>().ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
