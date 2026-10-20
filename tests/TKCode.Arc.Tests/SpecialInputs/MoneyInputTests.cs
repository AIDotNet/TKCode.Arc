using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.SpecialInputs;

public class MoneyInputTests
{
    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off, double width = 600)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } }, level);
        window.Width = width;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        foreach (var c in text)
        {
            window.KeyTextInput(c.ToString());
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static MoneyInput Focused(MoneyInput input)
    {
        input.Focus(NavigationMethod.Pointer);
        Dispatcher.UIThread.RunJobs();
        return input;
    }

    private static void Blur(Window window, MoneyInput input)
    {
        InputElement other = window.GetVisualDescendants().OfType<MoneyInputChip>().FirstOrDefault()
                             ?? (InputElement)window.GetVisualDescendants().OfType<MoneyInputCurrencyButton>().First();
        other.Focus(NavigationMethod.Pointer);
        Dispatcher.UIThread.RunJobs();
        input.IsFocused.ShouldBeFalse();
    }

    // ------------------------------------------------------------------ draft model (pure)

    [AvaloniaFact]
    public void Sanitize_Keeps_Digits_And_One_Decimal_And_Counts_The_Caret()
    {
        MoneyInputDraft.Sanitize("1,2345", 6, ',', '.', 2).ShouldBe(("12345", 5));
        MoneyInputDraft.Sanitize("007", 3, ',', '.', 2).ShouldBe(("7", 1));
        MoneyInputDraft.Sanitize(".5", 2, ',', '.', 2).ShouldBe(("0.5", 3));
        MoneyInputDraft.Sanitize("00", 2, ',', '.', 2).ShouldBe(("0", 1));
        MoneyInputDraft.Sanitize("12.345", 6, ',', '.', 2).ShouldBeNull();
        MoneyInputDraft.Sanitize("1234567890123", 13, ',', '.', 2).ShouldBeNull();
        MoneyInputDraft.Sanitize("123456789012", 12, ',', '.', 2).ShouldBe(("123456789012", 12));
        // JPY: no decimal at all; de-DE: "," is the decimal and "." is a group.
        MoneyInputDraft.Sanitize("12.5", 4, ',', '.', 0).ShouldBe(("125", 3));
        MoneyInputDraft.Sanitize("1.234,5", 7, '.', ',', 2).ShouldBe(("1234.5", 6));
    }

    [AvaloniaFact]
    public void Draft_Round_Trips_Through_Minor_Units()
    {
        MoneyInputDraft.ToMinor("1234.5", 2).ShouldBe(123450m);
        MoneyInputDraft.ToMinor("", 2).ShouldBeNull();
        MoneyInputDraft.FromMinor(123450m, 2).ShouldBe("1234.50");
        MoneyInputDraft.FromMinor(5m, 2).ShouldBe("0.05");
        MoneyInputDraft.FromMinor(1200m, 0).ShouldBe("1200");
        MoneyInputDraft.Settle("12.5", 2).ShouldBe("12.50");
        MoneyInputDraft.Settle("007", 2).ShouldBe("7.00");
        MoneyInputDraft.Convert("12.345", 2).ShouldBe("12.34");
        MoneyInputDraft.Convert("12.5", 0).ShouldBe("12");
        MoneyInputDraft.Convert("", 0).ShouldBe("");
        MoneyInputDraft.Display("1234567.5", ',', '.').ShouldBe("1,234,567.5");
        MoneyInputDraft.Display("1234.5", '.', ',').ShouldBe("1.234,5");
        MoneyInputDraft.PositionAfter("1,234.5", 2, '.').ShouldBe(3);
        MoneyInputDraft.PositionAfter("1,234.5", 0, '.').ShouldBe(0);
        MoneyInputDraft.PositionAfter("1,234.5", 99, '.').ShouldBe(7);
    }

    [AvaloniaFact]
    public void Parts_Key_Columns_By_Place_With_Ghost_Cents()
    {
        var parts = new List<MoneyInputPart>();
        MoneyInputDraft.Parts("1234.5", 2, ',', '.', parts);
        new string(parts.Select(p => p.Char).ToArray()).ShouldBe("1,234.50");
        parts.Select(p => p.Ghost).ShouldBe([false, false, false, false, false, false, false, true]);
        parts[0].Rank.ShouldBe(-6);
        parts[1].Rank.ShouldBe(-5);
        parts[^1].Rank.ShouldBe(1002);
        MoneyInputDraft.Parts("", 2, ',', '.', parts);
        new string(parts.Select(p => p.Char).ToArray()).ShouldBe("0.00");
        parts.ShouldAllBe(p => p.Ghost);
        MoneyInputDraft.Parts("12", 0, ',', '.', parts);
        new string(parts.Select(p => p.Char).ToArray()).ShouldBe("12");
    }

    [AvaloniaFact]
    public void Paste_Parsing_Finds_The_Final_Decimal()
    {
        MoneyInputDraft.ParsePaste("$1,234.56", 2).ShouldBe("1234.56");
        MoneyInputDraft.ParsePaste("1.234,56 €", 2).ShouldBe("1234.56");
        MoneyInputDraft.ParsePaste("1234.5", 2).ShouldBe("1234.5");
        MoneyInputDraft.ParsePaste("1,234", 2).ShouldBe("1234");
        MoneyInputDraft.ParsePaste("¥1,234.56", 0).ShouldBe("1234");
        MoneyInputDraft.ParsePaste("abc", 2).ShouldBeNull();
        MoneyInputDraft.ParsePaste("   ", 2).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Currency_Table_And_Formatters_Match_Intl()
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        var usd = MoneyInputCurrencyInfo.Get("USD", us);
        usd.Symbol.ShouldBe("$");
        usd.Digits.ShouldBe(2);
        usd.Name.ShouldBe("US Dollar");
        usd.IsSuffix.ShouldBeFalse();
        usd.Format(1234.5m).ShouldBe("$1,234.50");
        usd.FormatWhole(10).ShouldBe("$10");
        var jpy = MoneyInputCurrencyInfo.Get("jpy", us);
        jpy.Code.ShouldBe("JPY");
        jpy.Digits.ShouldBe(0);
        jpy.Format(1200).ShouldBe("¥1,200");
        MoneyInputCurrencyInfo.Get("CHF", us).Format(12).ShouldBe("CHF 12.00");
        MoneyInputCurrencyInfo.Get("GBP", us).Name.ShouldBe("British Pound");
        MoneyInputCurrencyInfo.Get("CAD", us).Symbol.ShouldBe("$");
        MoneyInputCurrencyInfo.Get("INR", us).Symbol.ShouldBe("₹");
        var eur = MoneyInputCurrencyInfo.Get("EUR", CultureInfo.GetCultureInfo("de-DE"));
        eur.IsSuffix.ShouldBeTrue();
        eur.Format(1234.56m).ShouldBe("1.234,56 €");
        ReferenceEquals(MoneyInputCurrencyInfo.Get("EUR", CultureInfo.GetCultureInfo("de-DE")), eur).ShouldBeTrue();
        MoneyInputCurrencyInfo.Get("XYZ", us).Code.ShouldBe("XYZ");
    }

    // ------------------------------------------------------------------ defaults

    [AvaloniaFact]
    public void Defaults_Match_Arcs_Demo()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            input.Currency.ShouldBe("USD");
            input.Currencies.ShouldBe(["USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "INR"]);
            input.QuickAddAmounts.ShouldBe([10m, 50m, 100m]);
            input.Culture!.Name.ShouldBe("en-US");
            input.Value.ShouldBeNull();
            input.MinorUnits.ShouldBeNull();
            input.RangeText.ShouldBe("");
            MoneyInput.ValueProperty.GetMetadata(typeof(MoneyInput)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
            MoneyInput.CurrencyProperty.GetMetadata(typeof(MoneyInput)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);

            var frame = TestHelpers.Descendant<FieldFrame>(input);
            frame.Bounds.Height.ShouldBe(76);
            frame.CornerRadius.ShouldBe(new CornerRadius(26));
            frame.Padding.ShouldBe(new Thickness(18, 0, 8, 0));

            input.Amount!.ShownText.ShouldBe("0.00");
            input.Amount.GhostFlags.ShouldAllBe(g => g);
            var button = input.CurrencyButton!;
            button.IsVisible.ShouldBeTrue();
            button.Content.ShouldBe("USD");
            button.Bounds.Height.ShouldBe(36);
            AutomationProperties.GetName(button).ShouldBe("Currency, US Dollar");

            var chips = input.GetVisualDescendants().OfType<MoneyInputChip>().ToList();
            chips.Select(c => c.Content).ShouldBe(["+$10", "+$50", "+$100"]);
            chips.Select(AutomationProperties.GetName).ShouldBe(["Add $10", "Add $50", "Add $100"]);
            chips[0].Bounds.Height.ShouldBe(30);
            TestHelpers.Descendant<TextBlock>(input, "PART_Label").Text.ShouldBe("Amount");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editor_And_Painted_Amount_Share_One_Origin_And_Font()
    {
        var input = new MoneyInput { Label = "Amount", Value = 1234.5m };
        var window = Show(input);
        try
        {
            var presenter = TestHelpers.Descendant<Avalonia.Controls.Presenters.TextPresenter>(input);
            var amount = input.Amount!;
            presenter.Bounds.Position.ShouldBe(amount.Bounds.Position);
            presenter.FontSize.ShouldBe(36);
            presenter.FontWeight.ShouldBe(Avalonia.Media.FontWeight.Medium);
            presenter.LineHeight.ShouldBe(43.2, 0.001);
            // Each painted column is as wide as the editor's glyph, so the caret lands between painted digits.
            var text = presenter.Text!;
            text.ShouldBe("1,234.50");
            var layout = presenter.TextLayout;
            var x = 0.0;
            for (var i = 0; i < text.Length; i++)
            {
                layout.HitTestTextPosition(i).X.ShouldBe(x, 0.01);
                x += amount.Advance(text[i]);
            }
            layout.WidthIncludingTrailingWhitespace.ShouldBe(x, 0.01);
            amount.Bounds.Width.ShouldBe(x, 1);
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ typing

    [AvaloniaFact]
    public void Typing_Groups_Live_And_Reports_Major_And_Minor_Units()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            var events = new List<MoneyInputValueChangedEventArgs>();
            input.ValueChanged += (_, e) => events.Add(e);
            Focused(input);
            Type(window, "1234.5");
            input.Text.ShouldBe("1,234.5");
            input.CaretIndex.ShouldBe(7);
            input.Value.ShouldBe(1234.5m);
            input.MinorUnits.ShouldBe(123450);
            input.FormattedValue.ShouldBe("$1,234.50");
            input.Amount!.ShownText.ShouldBe("1,234.50");
            input.Amount.GhostFlags.Last().ShouldBeTrue();
            events.Count.ShouldBe(5);
            var last = events[^1];
            last.OldValue.ShouldBe(1234m);
            last.NewValue.ShouldBe(1234.5m);
            last.Currency.ShouldBe("USD");
            last.MinorUnits.ShouldBe(123450);
            last.Formatted.ShouldBe("$1,234.50");
            events.ShouldAllBe(e => e.ChangeSource == MoneyInputChangeSource.Type);
            // Typing never announces; the live region stays quiet.
            input.LiveRegion!.Message.ShouldBe("");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Caret_Returns_After_The_Same_Significant_Characters()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "1234");
            input.Text.ShouldBe("1,234");
            input.CaretIndex = 1;
            Type(window, "5");
            input.Text.ShouldBe("15,234");
            input.CaretIndex.ShouldBe(2);
            // Leading zeros are stripped and the caret follows.
            input.CaretIndex = 0;
            Type(window, "0");
            input.Text.ShouldBe("15,234");
            input.CaretIndex.ShouldBe(0);
            // Letters are dropped and keep the caret.
            input.CaretIndex = 6;
            Type(window, "x");
            input.Text.ShouldBe("15,234");
            input.CaretIndex.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Refused_Keystrokes_Keep_The_Caret()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "12.34");
            Type(window, "5");
            input.Text.ShouldBe("12.34");
            input.CaretIndex.ShouldBe(5);
            input.CaretIndex = 4;
            Type(window, "9");
            input.Text.ShouldBe("12.34");
            input.CaretIndex.ShouldBe(4);
            // A second decimal point is dropped.
            Type(window, ".");
            input.Text.ShouldBe("12.34");
            // Twelve integer digits at most.
            input.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Type(window, "1234567890123");
            input.Text.ShouldBe("123,456,789,012");
            input.MinorUnits.ShouldBe(12345678901200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Past_The_Maximum_Is_Refused_With_The_Kick()
    {
        var input = new MoneyInput { Label = "Amount", Maximum = 100 };
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "100");
            input.IsLimitFlashing.ShouldBeFalse();
            Type(window, "0");
            input.Text.ShouldBe("100");
            input.Value.ShouldBe(100m);
            input.CaretIndex.ShouldBe(3);
            input.IsLimitFlashing.ShouldBeTrue();
            input.LimitFlash.ShouldBe(1);
            input.LiveRegion!.Message.ShouldBe("Maximum is $100.00");
            input.RangeText.ShouldBe("Up to $100.00");
            // The minimum is never checked while typing.
            input.Minimum = 50;
            input.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Type(window, "2");
            input.Value.ShouldBe(2m);
            input.ShownError.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_And_Delete_On_A_Separator_Delete_The_Neighbouring_Digit()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "1234");
            input.CaretIndex = 2;
            Press(window, PhysicalKey.Backspace);
            input.Text.ShouldBe("234");
            input.CaretIndex.ShouldBe(0);

            input.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Type(window, "1234");
            input.CaretIndex = 1;
            Press(window, PhysicalKey.Delete);
            input.Text.ShouldBe("134");
            input.CaretIndex.ShouldBe(1);

            // A plain Backspace regroups the rest.
            input.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Type(window, "1234");
            input.CaretIndex = 5;
            Press(window, PhysicalKey.Backspace);
            input.Text.ShouldBe("123");
            input.CaretIndex.ShouldBe(3);
            input.Value.ShouldBe(123m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_And_Blur_Settle_The_Fraction()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            Focused(input);
            Type(window, "12.5");
            input.CaretIndex = 1;
            Press(window, PhysicalKey.Enter);
            input.Text.ShouldBe("12.50");
            input.CaretIndex.ShouldBe(5);
            input.Value.ShouldBe(12.5m);

            input.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Type(window, "7");
            Blur(window, input);
            input.Text.ShouldBe("7.00");
            input.Amount!.GhostFlags.ShouldAllBe(g => !g);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_Replaces_A_Whole_Selection_Or_Inserts_At_The_Caret()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            Focused(input);
            input.PasteText("$1,234.56");
            input.Text.ShouldBe("1,234.56");
            input.Value.ShouldBe(1234.56m);
            input.SelectAll();
            input.PasteText("1.234,5 €");
            input.Text.ShouldBe("1,234.5");
            input.CaretIndex.ShouldBe(7);
            input.CaretIndex = 1;
            input.PasteText("9");
            input.Text.ShouldBe("19,234.5");
            input.CaretIndex.ShouldBe(2);
            input.PasteText("hello");
            input.Text.ShouldBe("19,234.5");
            var sources = new List<MoneyInputChangeSource>();
            input.ValueChanged += (_, e) => sources.Add(e.ChangeSource);
            input.SelectAll();
            input.PasteText("$5");
            sources.ShouldBe([MoneyInputChangeSource.Paste]);
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ steps and chips

    [AvaloniaFact]
    public void Arrow_And_Page_Keys_Step_And_Clamp()
    {
        var input = new MoneyInput { Label = "Amount", Maximum = 50 };
        var window = Show(input);
        try
        {
            Focused(input);
            var sources = new List<MoneyInputChangeSource>();
            input.ValueChanged += (_, e) => sources.Add(e.ChangeSource);
            Press(window, PhysicalKey.ArrowUp);
            sources.ShouldBe([MoneyInputChangeSource.Key]);
            input.Text.ShouldBe("1.00");
            input.CaretIndex.ShouldBe(4);
            input.LiveRegion!.Message.ShouldBe("$1.00");
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            input.Value.ShouldBe(11m);
            Press(window, PhysicalKey.PageUp);
            input.Value.ShouldBe(21m);
            Press(window, PhysicalKey.PageDown);
            input.Value.ShouldBe(11m);
            Press(window, PhysicalKey.ArrowDown);
            input.Value.ShouldBe(10m);
            // Past the maximum: clamps and kicks; at the maximum: refuses.
            input.Value = 45;
            Dispatcher.UIThread.RunJobs();
            input.IsLimitFlashing.ShouldBeFalse();
            Press(window, PhysicalKey.PageUp);
            input.Value.ShouldBe(50m);
            input.IsLimitFlashing.ShouldBeTrue();
            input.LiveRegion.Message.ShouldBe("$50.00");
            Press(window, PhysicalKey.ArrowUp);
            input.Value.ShouldBe(50m);
            input.LiveRegion.Message.ShouldBe("Maximum is $50.00");
            // The floor is the minimum or zero.
            input.Value = 0.5m;
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            input.Value.ShouldBe(0m);
            Press(window, PhysicalKey.ArrowDown);
            input.Value.ShouldBe(0m);
            input.LiveRegion.Message.ShouldBe("Minimum is $0.00");
            // A custom step in major units.
            input.Step = 0.25m;
            Press(window, PhysicalKey.ArrowUp);
            input.Value.ShouldBe(0.25m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Quick_Add_Chips_Add_And_Look_Disabled_At_The_Maximum()
    {
        var input = new MoneyInput { Label = "Amount", Maximum = 60 };
        var window = Show(input);
        try
        {
            var chips = input.GetVisualDescendants().OfType<MoneyInputChip>().ToList();
            MoneyInputValueChangedEventArgs? added = null;
            input.ValueChanged += (_, e) => added ??= e;
            chips[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(10m);
            added!.ChangeSource.ShouldBe(MoneyInputChangeSource.Chip);
            input.LiveRegion!.Message.ShouldBe("$10.00");
            chips[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(60m);
            input.IsLimitFlashing.ShouldBeFalse();
            chips[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(60m);
            input.IsLimitFlashing.ShouldBeTrue();
            input.LiveRegion.Message.ShouldBe("Maximum is $60.00");
            chips.ShouldAllBe(c => c.IsAtLimit && c.Opacity < 0.5);
            chips.ShouldAllBe(c => c.IsEnabled);

            input.QuickAddAmounts = [];
            Dispatcher.UIThread.RunJobs();
            input.GetVisualDescendants().OfType<MoneyInputChip>().ShouldBeEmpty();
            TestHelpers.Descendant<MoneyInputToolbar>(input).IsVisible.ShouldBeTrue();
            input.Maximum = null;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<MoneyInputToolbar>(input).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ limits and messages

    [AvaloniaFact]
    public void Range_Text_And_Built_In_Errors_Follow_Arc()
    {
        var input = new MoneyInput { Label = "Amount", Minimum = 5, Maximum = 1000, Description = "Pick an amount." };
        var window = Show(input);
        try
        {
            input.RangeText.ShouldBe("$5.00 to $1,000.00");
            TestHelpers.Descendant<TextBlock>(input, "PART_Range").Text.ShouldBe("$5.00 to $1,000.00");
            var field = TestHelpers.Descendant<Field>(input);
            field.Description.ShouldBe("Pick an amount.");
            Focused(input);
            Type(window, "2");
            input.ShownError.ShouldBeNull();
            Blur(window, input);
            input.ShownError.ShouldBe("The minimum is $5.00");
            field.ErrorMessage.ShouldBe("The minimum is $5.00");
            field.Description.ShouldBeNull();
            TestHelpers.Descendant<FieldFrame>(input).IsInvalid.ShouldBeTrue();
            AutomationProperties.GetHelpText(input).ShouldBe("$5.00 to $1,000.00 The minimum is $5.00");
            // An explicit error replaces the built-in one.
            input.ErrorMessage = "Enter the invoice amount.";
            Dispatcher.UIThread.RunJobs();
            field.ErrorMessage.ShouldBe("Enter the invoice amount.");
            input.ErrorMessage = null;
            input.Value = 20;
            Dispatcher.UIThread.RunJobs();
            input.ShownError.ShouldBeNull();
            field.Description.ShouldBe("Pick an amount.");
            TestHelpers.Descendant<FieldFrame>(input).IsInvalid.ShouldBeFalse();

            input.Minimum = null;
            input.RangeText.ShouldBe("Up to $1,000.00");
            input.Minimum = 5;
            input.Maximum = null;
            input.RangeText.ShouldBe("At least $5.00");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Programmatic_Value_Raises_ValueChanged_Without_Feedback()
    {
        var input = new MoneyInput { Label = "Amount", Value = 12.5m, Maximum = 20 };
        var events = new List<MoneyInputValueChangedEventArgs>();
        input.ValueChanged += (_, e) => events.Add(e);
        var window = Show(input);
        try
        {
            // Template application raises nothing.
            events.ShouldBeEmpty();
            input.Text.ShouldBe("12.50");
            input.Value = 42;
            Dispatcher.UIThread.RunJobs();
            input.Text.ShouldBe("42.00");
            input.MinorUnits.ShouldBe(4200);
            input.Value = null;
            Dispatcher.UIThread.RunJobs();
            input.Text.ShouldBe("");
            input.Amount!.ShownText.ShouldBe("0.00");
            events.Count.ShouldBe(2);
            events.ShouldAllBe(e => e.ChangeSource == MoneyInputChangeSource.Programmatic);
            events[0].OldValue.ShouldBe(12.5m);
            events[0].NewValue.ShouldBe(42m);
            events[0].MinorUnits.ShouldBe(4200);
            events[0].Formatted.ShouldBe("$42.00");
            events[0].Currency.ShouldBe("USD");
            events[1].OldValue.ShouldBe(42m);
            events[1].NewValue.ShouldBeNull();
            events[1].MinorUnits.ShouldBeNull();
            events[1].Formatted.ShouldBe("");
            // Past the maximum by code: no announcement, no kick, no flash.
            input.LiveRegion!.Message.ShouldBe("");
            input.IsLimitFlashing.ShouldBeFalse();
            input.Stage!.IsKicking.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void German_Euros_Put_The_Symbol_After_And_Swap_Separators()
    {
        var input = new MoneyInput
        {
            Label = "Betrag",
            Culture = CultureInfo.GetCultureInfo("de-DE"),
            Currency = "EUR",
            Value = 1234.56m
        };
        var window = Show(input);
        try
        {
            input.Text.ShouldBe("1.234,56");
            input.FormattedValue.ShouldBe("1.234,56 €");
            TestHelpers.Descendant<MoneyInputSymbol>(input, "PART_PrefixSymbol").IsVisible.ShouldBeFalse();
            var suffix = TestHelpers.Descendant<MoneyInputSymbol>(input, "PART_SuffixSymbol");
            suffix.IsVisible.ShouldBeTrue();
            suffix.Symbol.ShouldBe("€");
            input.GetVisualDescendants().OfType<MoneyInputChip>().First().Content.ShouldBe("+10 €");
            input.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Focused(input);
            Type(window, "1.5");
            input.Text.ShouldBe("15");
            Type(window, ",25");
            input.Text.ShouldBe("15,25");
            input.Value.ShouldBe(15.25m);
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ currency menu

    [AvaloniaFact]
    public void Currency_Menu_Opens_From_The_Keyboard_And_Chooses()
    {
        var input = new MoneyInput { Label = "Amount", Value = 12.5m };
        var window = Show(input);
        try
        {
            var currencyEvents = new List<MoneyInputCurrencyChangedEventArgs>();
            var valueEvents = new List<MoneyInputValueChangedEventArgs>();
            input.CurrencyChanged += (_, e) => currencyEvents.Add(e);
            input.ValueChanged += (_, e) => valueEvents.Add(e);
            var button = input.CurrencyButton!;
            button.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            input.IsCurrencyMenuOpen.ShouldBeTrue();
            button.IsOpen.ShouldBeTrue();
            input.CurrencyPopup!.IsOpen.ShouldBeTrue();
            var list = input.CurrencyList!;
            list.IsFocused.ShouldBeTrue();
            input.ActiveCurrency.ShouldBe("USD");
            list.Options.Single(o => o.IsSelected).Code.ShouldBe("USD");
            list.Highlight.IsShown.ShouldBeTrue();
            var usdRow = list.Options.First();
            list.Highlight.Top.ShouldBe(usdRow.Bounds.Y);
            usdRow.Bounds.Height.ShouldBe(36);

            Press(window, PhysicalKey.ArrowDown);
            input.ActiveCurrency.ShouldBe("EUR");
            list.Highlight.Top.ShouldBe(38);
            Press(window, PhysicalKey.End);
            input.ActiveCurrency.ShouldBe("INR");
            Press(window, PhysicalKey.Home);
            input.ActiveCurrency.ShouldBe("USD");
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);

            input.IsCurrencyMenuOpen.ShouldBeFalse();
            input.Currency.ShouldBe("EUR");
            button.Content.ShouldBe("EUR");
            button.IsFocused.ShouldBeTrue();
            input.Text.ShouldBe("12.50");
            currencyEvents.Single().NewCurrency.ShouldBe("EUR");
            currencyEvents.Single().OldCurrency.ShouldBe("USD");
            currencyEvents.Single().ChangeSource.ShouldBe(MoneyInputCurrencyChangeSource.Menu);
            var changed = valueEvents.Single();
            changed.ChangeSource.ShouldBe(MoneyInputChangeSource.Currency);
            changed.Currency.ShouldBe("EUR");
            changed.NewValue.ShouldBe(12.5m);
            changed.Formatted.ShouldBe("€12.50");
            input.LiveRegion!.Message.ShouldBe("Euro, €12.50");
            AutomationProperties.GetName(button).ShouldBe("Currency, Euro");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Currency_Menu_Typeahead_Escape_And_Conversion()
    {
        var input = new MoneyInput { Label = "Amount", Value = 12.5m };
        var window = Show(input);
        try
        {
            var button = input.CurrencyButton!;
            button.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowUp);
            input.IsCurrencyMenuOpen.ShouldBeTrue();
            Press(window, PhysicalKey.J);
            input.ActiveCurrency.ShouldBe("JPY");
            Press(window, PhysicalKey.Escape);
            input.IsCurrencyMenuOpen.ShouldBeFalse();
            button.IsFocused.ShouldBeTrue();
            input.Currency.ShouldBe("USD");

            // Choosing yen keeps the major amount and drops the cents.
            input.OpenCurrencyMenu();
            Dispatcher.UIThread.RunJobs();
            // The typeahead buffer keeps keys for 600ms: "j" then "s" matches nothing.
            Press(window, PhysicalKey.S);
            input.ActiveCurrency.ShouldBe("USD");
            Thread.Sleep(700);
            Press(window, PhysicalKey.S);
            input.ActiveCurrency.ShouldBe("CHF");
            Thread.Sleep(700);
            Press(window, PhysicalKey.C);
            Press(window, PhysicalKey.A);
            input.ActiveCurrency.ShouldBe("CAD");
            Thread.Sleep(700);
            Press(window, PhysicalKey.J);
            Press(window, PhysicalKey.Space);
            input.Currency.ShouldBe("JPY");
            input.Text.ShouldBe("12");
            input.Value.ShouldBe(12m);
            input.MinorUnits.ShouldBe(12);
            input.Amount!.ShownText.ShouldBe("12");
            input.LiveRegion!.Message.ShouldBe("Japanese Yen, ¥12");
            input.GetVisualDescendants().OfType<MoneyInputChip>().First().Content.ShouldBe("+¥10");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Currency_Button_Click_Toggles_And_Rows_Choose_On_Tap()
    {
        var input = new MoneyInput { Label = "Amount" };
        var window = Show(input);
        try
        {
            var button = input.CurrencyButton!;
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.IsCurrencyMenuOpen.ShouldBeTrue();
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.IsCurrencyMenuOpen.ShouldBeFalse();

            input.OpenCurrencyMenu();
            Dispatcher.UIThread.RunJobs();
            var gbp = input.CurrencyList!.Options.Single(o => o.Code == "GBP");
            var point = gbp.TranslatePoint(new Point(40, 18), window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            input.ActiveCurrency.ShouldBe("GBP");
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.Currency.ShouldBe("GBP");
            input.IsCurrencyMenuOpen.ShouldBeFalse();

            // A press outside closes the menu.
            input.OpenCurrencyMenu();
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(new Point(590, 690), MouseButton.Left);
            window.MouseUp(new Point(590, 690), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.IsCurrencyMenuOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Programmatic_Currency_Raises_CurrencyChanged_And_Follows_Value()
    {
        var input = new MoneyInput { Label = "Amount", Value = 12.5m };
        var window = Show(input);
        try
        {
            var currencyEvents = new List<MoneyInputCurrencyChangedEventArgs>();
            var valueEvents = 0;
            input.CurrencyChanged += (_, e) => currencyEvents.Add(e);
            input.ValueChanged += (_, _) => valueEvents++;
            input.Currency = "JPY";
            Dispatcher.UIThread.RunJobs();
            var changed = currencyEvents.Single();
            changed.OldCurrency.ShouldBe("USD");
            changed.NewCurrency.ShouldBe("JPY");
            changed.ChangeSource.ShouldBe(MoneyInputCurrencyChangeSource.Programmatic);
            // The display follows Value in the new minor units; Value itself is not rewritten.
            input.Value.ShouldBe(12.5m);
            input.Text.ShouldBe("13");
            input.MinorUnits.ShouldBe(13);
            valueEvents.ShouldBe(0);
            input.LiveRegion!.Message.ShouldBe("");
            input.CurrencyButton!.Content.ShouldBe("JPY");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Single_Currency_Shows_The_Static_Code()
    {
        var input = new MoneyInput { Label = "Budget", Currency = "EUR", Currencies = ["EUR"] };
        var window = Show(input);
        try
        {
            input.CurrencyButton!.IsVisible.ShouldBeFalse();
            var code = TestHelpers.Descendant<TextBlock>(input, "PART_StaticCode");
            code.IsVisible.ShouldBeTrue();
            code.Text.ShouldBe("EUR");
            input.OpenCurrencyMenu();
            input.IsCurrencyMenuOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ fit, motion, states, automation

    [AvaloniaFact]
    public void Long_Amounts_Scale_Down_To_Fit()
    {
        var input = new MoneyInput { Label = "Amount", Width = 380, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(input);
        try
        {
            input.Stage!.TargetScale.ShouldBe(1);
            input.Stage.NaturalWidth.ShouldBeLessThan(input.Stage.Bounds.Width);
            input.Value = 123456789012m;
            Dispatcher.UIThread.RunJobs();
            input.Stage.NaturalWidth.ShouldBeGreaterThan(input.Stage.Bounds.Width);
            input.Stage.TargetScale.ShouldBe(input.Stage.Bounds.Width / input.Stage.NaturalWidth, 0.01);
            input.Stage.Scale.ShouldBe(input.Stage.TargetScale, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Rolls_Chips_But_Typing_Cuts()
    {
        var input = new MoneyInput { Label = "Amount", Maximum = 15 };
        var window = Show(input, ArcMotionLevel.Full);
        try
        {
            Focused(input);
            Type(window, "1");
            input.Amount!.IsRolling.ShouldBeFalse();
            input.Text.ShouldBe("1");
            var chip = input.GetVisualDescendants().OfType<MoneyInputChip>().First();
            chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(11m);
            input.Amount.IsRolling.ShouldBeTrue();
            chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(15m);
            input.Stage!.IsKicking.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Has_No_Kick_Or_Roll()
    {
        var input = new MoneyInput { Label = "Amount", Maximum = 15, Value = 10 };
        var window = Show(input, ArcMotionLevel.Reduced);
        try
        {
            var chip = input.GetVisualDescendants().OfType<MoneyInputChip>().First();
            chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(15m);
            input.Stage!.IsKicking.ShouldBeFalse();
            input.Stage.KickOffset.ShouldBe(0);
            input.IsLimitFlashing.ShouldBeTrue();
            input.LimitFlash.ShouldBe(1);
            input.Amount!.ColumnCount.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Field_Ignores_Input()
    {
        var input = new MoneyInput { Label = "Amount", Value = 250, IsEnabled = false };
        var window = Show(input);
        try
        {
            TestHelpers.Descendant<FieldFrame>(input).Opacity.ShouldBe(0.5);
            input.GetVisualDescendants().OfType<MoneyInputChip>().ShouldAllBe(c => !c.IsEffectivelyEnabled);
            input.OpenCurrencyMenu();
            input.IsCurrencyMenuOpen.ShouldBeFalse();
            input.Value.ShouldBe(250m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Exposes_A_Spin_Button_With_Currency_Value_Text()
    {
        var input = new MoneyInput { Label = "Donation", IsLabelVisible = false, Minimum = 5, Maximum = 100 };
        var window = Show(input);
        try
        {
            AutomationProperties.GetName(input).ShouldBe("Donation");
            TestHelpers.Descendant<TextBlock>(input, "PART_Label").IsEffectivelyVisible.ShouldBeFalse();
            var peer = ControlAutomationPeer.CreatePeerForElement(input);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Spinner);
            var value = (IValueProvider)peer;
            var range = (IRangeValueProvider)peer;
            value.Value.ShouldBe("Empty");
            range.Minimum.ShouldBe(5);
            range.Maximum.ShouldBe(100);
            range.SmallChange.ShouldBe(1);
            range.LargeChange.ShouldBe(10);
            MoneyInputChangeSource? source = null;
            input.ValueChanged += (_, e) => source = e.ChangeSource;
            range.SetValue(12.5);
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe(12.5m);
            source.ShouldBe(MoneyInputChangeSource.Automation);
            value.Value.ShouldBe("$12.50");
            range.Value.ShouldBe(12.5);
            AutomationProperties.GetHelpText(input).ShouldBe("$5.00 to $100.00");
        }
        finally
        {
            window.Close();
        }
    }
}
