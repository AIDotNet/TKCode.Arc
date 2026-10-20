using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.SpecialInputs;

public class PhoneInputTests
{
    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } });
        window.Width = 600;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static PhoneInput Field(string? value = null)
    {
        var input = new PhoneInput { Label = "Phone number", Width = 360, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        if (value is not null)
        {
            input.Value = value;
        }
        return input;
    }

    private static void Type(Window window, string text)
    {
        foreach (var ch in text)
        {
            window.KeyTextInput(ch.ToString());
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Focus(Control control)
    {
        control.Focus(NavigationMethod.Pointer);
        Dispatcher.UIThread.RunJobs();
    }

    // ------------------------------------------------------------------ table and helpers

    [AvaloniaFact]
    public void Table_Matches_Arc()
    {
        PhoneNumbers.Countries.Count.ShouldBe(49);
        PhoneNumbers.Countries[0].Iso.ShouldBe("US");
        PhoneNumbers.Countries[^1].Iso.ShouldBe("NZ");
        var canada = PhoneNumbers.Find("ca")!;
        canada.AreaCodes!.Count.ShouldBe(54);
        canada.AreaCodes.ShouldContain("416");
        canada.Trunk.ShouldBe("1");
        PhoneNumbers.Find("MX")!.Trunk.ShouldBe(string.Empty);
        PhoneNumbers.Find("BR")!.Lengths.ShouldBe(new[] { 10, 11 });
        PhoneNumbers.LengthCopy(PhoneNumbers.Find("GB")!).ShouldBe("10");
        PhoneNumbers.LengthCopy(PhoneNumbers.Find("BR")!).ShouldBe("10 or 11");
        PhoneNumbers.LengthCopy(PhoneNumbers.Find("ID")!).ShouldBe("10, 11 or 12");
        PhoneNumbers.Flag("US").ShouldBe("\U0001F1FA\U0001F1F8");
        PhoneNumbers.Find("US")!.Flag.ShouldBe(PhoneNumbers.Flag("US"));
    }

    [AvaloniaFact]
    public void Taiwan_And_Hong_Kong_Show_The_China_Flag_And_Name()
    {
        var chinaFlag = "\U0001F1E8\U0001F1F3";
        var taiwan = PhoneNumbers.Find("TW")!;
        taiwan.Name.ShouldBe("Taiwan (China)");
        taiwan.Flag.ShouldBe(PhoneNumbers.Flag("CN"));
        PhoneNumbers.MatchesQuery(taiwan, "taiwan").ShouldBeTrue();
        PhoneNumbers.MatchesQuery(taiwan, "tw").ShouldBeTrue();
        var hongKong = PhoneNumbers.Find("HK")!;
        hongKong.Name.ShouldBe("Hong Kong (China)");
        hongKong.Flag.ShouldBe(chinaFlag);
        PhoneNumbers.MatchesQuery(hongKong, "hong kong").ShouldBeTrue();
        // Macao is not in the table, but the helper (and a custom entry built on it) follows the same rule.
        foreach (var iso in new[] { "tw", "HK", "mo" })
        {
            PhoneNumbers.Flag(iso).ShouldBe(chinaFlag);
        }
        PhoneNumbers.Flag("JP").ShouldBe("\U0001F1EF\U0001F1F5");
    }

    [AvaloniaFact]
    public void Formats_National_Numbers_As_Typed()
    {
        var us = PhoneNumbers.Find("US")!;
        PhoneNumbers.FormatNational(us, "201").ShouldBe("(201");
        PhoneNumbers.FormatNational(us, "2015").ShouldBe("(201) 5");
        PhoneNumbers.FormatNational(us, "201555").ShouldBe("(201) 555");
        PhoneNumbers.FormatNational(us, "2015550").ShouldBe("(201) 555-0");
        PhoneNumbers.FormatNational(us, "2015550123").ShouldBe("(201) 555-0123");
        PhoneNumbers.FormatNational(us, "12015550123").ShouldBe("1 (201) 555-0123");
        PhoneNumbers.FormatNational(us, "1").ShouldBe("1");
        var gb = PhoneNumbers.Find("GB")!;
        PhoneNumbers.FormatNational(gb, "07911123456").ShouldBe("07911 123456");
        PhoneNumbers.FormatNational(gb, "7400123456").ShouldBe("7400 123456");
        var br = PhoneNumbers.Find("BR")!;
        PhoneNumbers.FormatNational(br, "1196123456").ShouldBe("(11) 9612-3456");
        PhoneNumbers.FormatNational(br, "11961234567").ShouldBe("(11) 96123-4567");
        // The guide: the rest of the example in the country's shape.
        PhoneNumbers.Guide(us, string.Empty, string.Empty).ShouldBe("(201) 555-0123");
        PhoneNumbers.Guide(us, "415", "(415").ShouldBe(") 555-0123");
        PhoneNumbers.Guide(us, "4155", "(415) 5").ShouldBe("55-0123");
        PhoneNumbers.Guide(us, "4155550132", "(415) 555-0132").ShouldBe(string.Empty);
        PhoneNumbers.CaretAfterDigits("(201) 555-0123", 4).ShouldBe(7);
        PhoneNumbers.CaretAfterDigits("(201) 555", 0).ShouldBe(1);
        PhoneNumbers.CaretAfterDigits("(201) 555", 9).ShouldBe(9);
    }

    [AvaloniaFact]
    public void Status_And_E164()
    {
        var us = PhoneNumbers.Find("US")!;
        PhoneNumbers.StatusOf(us, string.Empty).ShouldBe(PhoneNumberStatus.Empty);
        PhoneNumbers.StatusOf(us, "1").ShouldBe(PhoneNumberStatus.Empty);
        PhoneNumbers.StatusOf(us, "201").ShouldBe(PhoneNumberStatus.Incomplete);
        PhoneNumbers.StatusOf(us, "2015550123").ShouldBe(PhoneNumberStatus.Valid);
        PhoneNumbers.StatusOf(us, "12015550123").ShouldBe(PhoneNumberStatus.Valid);
        PhoneNumbers.StatusOf(us, "20155501234").ShouldBe(PhoneNumberStatus.TooLong);
        PhoneNumbers.StatusOf(PhoneNumbers.Find("BR")!, "11961234567").ShouldBe(PhoneNumberStatus.Valid);
        PhoneNumbers.ToE164(us, "12015550123").ShouldBe("+12015550123");
        PhoneNumbers.ToE164(PhoneNumbers.Find("GB")!, "0").ShouldBe(string.Empty);
        PhoneNumbers.ToE164(PhoneNumbers.Find("GB")!, "07911123456").ShouldBe("+447911123456");
    }

    [AvaloniaFact]
    public void Parses_International_Numbers()
    {
        var gb = PhoneNumbers.Parse("+44 (0)7911 123456")!.Value;
        gb.Country.Iso.ShouldBe("GB");
        gb.National.ShouldBe("7911123456");
        PhoneNumbers.Parse("0044 7911 123456")!.Value.Country.Iso.ShouldBe("GB");
        var ca = PhoneNumbers.Parse("+14165550123")!.Value;
        ca.Country.Iso.ShouldBe("CA");
        ca.National.ShouldBe("4165550123");
        PhoneNumbers.Parse("+12015550123")!.Value.Country.Iso.ShouldBe("US");
        PhoneNumbers.Parse("+353 85 012 3456")!.Value.Country.Iso.ShouldBe("IE");
        PhoneNumbers.Parse("+1")!.Value.National.ShouldBe(string.Empty);
        PhoneNumbers.Parse("2015550123").ShouldBeNull();
        PhoneNumbers.Parse("+999 123").ShouldBeNull();
        // A pool without Canada reads +1 416 as the United States.
        PhoneNumbers.Parse("+14165550123", PhoneNumbers.Countries.Where(c => c.Iso != "CA"))!.Value.Country.Iso.ShouldBe("US");
        PhoneNumbers.Format("+447400123456").ShouldBe("+44 7400 123456");
        PhoneNumbers.Format("+14155550132").ShouldBe("+1 (415) 555-0132");
        PhoneNumbers.Format("not a number").ShouldBe("not a number");
    }

    [AvaloniaFact]
    public void Search_Matches_Names_Codes_And_Iso()
    {
        var gb = PhoneNumbers.Find("GB")!;
        PhoneNumbers.MatchesQuery(gb, "king").ShouldBeTrue();
        PhoneNumbers.MatchesQuery(gb, "gb").ShouldBeTrue();
        PhoneNumbers.MatchesQuery(gb, "+44").ShouldBeTrue();
        PhoneNumbers.MatchesQuery(gb, "4").ShouldBeTrue();
        PhoneNumbers.MatchesQuery(gb, "447").ShouldBeTrue();
        PhoneNumbers.MatchesQuery(gb, "+1").ShouldBeFalse();
        PhoneNumbers.MatchesQuery(gb, "+").ShouldBeTrue();
        PhoneNumbers.MatchesQuery(gb, "fr").ShouldBeFalse();
    }

    // ------------------------------------------------------------------ control

    [AvaloniaFact]
    public void Defaults_Look_Like_Arcs_Demo()
    {
        var input = Field();
        var window = Show(input);
        try
        {
            input.Value.ShouldBe(string.Empty);
            input.Country.ShouldBe("US");
            input.PreferredCountries.ShouldBe(new[] { "US", "CA", "GB" });
            input.IsValidationEnabled.ShouldBeTrue();
            input.IsRequired.ShouldBeFalse();
            input.SearchPlaceholder.ShouldBe("Country or code");
            input.Status.ShouldBe(PhoneNumberStatus.Empty);
            PhoneInput.ValueProperty.GetMetadata(typeof(PhoneInput)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
            PhoneInput.CountryProperty.GetMetadata(typeof(PhoneInput)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
            var frame = TestHelpers.Descendant<FieldFrame>(input);
            frame.Bounds.Height.ShouldBe(44);
            var trigger = TestHelpers.Descendant<PhoneInputTrigger>(input);
            AutomationProperties.GetName(trigger).ShouldBe("Country, United States +1");
            trigger.Bounds.Height.ShouldBe(36);
            var anchor = TestHelpers.Descendant<PhoneInputAnchor>(input);
            var face = TestHelpers.Descendant<PhoneInputFace>(input);
            anchor.Bounds.Width.ShouldBe(Math.Ceiling(12 + face.ContentWidth + 36), 1);
            var guide = TestHelpers.Descendant<PhoneInputGuide>(input);
            guide.Guide.ShouldBe("(201) 555-0123");
            TestHelpers.Descendant<PhoneInputPresence>(input, PhoneInput.ValidPart).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(input).ShouldBe("Phone number");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Formats_And_Emits_E164()
    {
        var input = Field();
        var events = new List<PhoneInputValueChangedEventArgs>();
        input.ValueChanged += (_, e) => events.Add(e);
        var window = Show(input);
        try
        {
            Focus(input);
            Type(window, "2015550123");
            input.Text.ShouldBe("(201) 555-0123");
            input.Value.ShouldBe("+12015550123");
            input.Status.ShouldBe(PhoneNumberStatus.Valid);
            input.CaretIndex.ShouldBe(14);
            events.Count.ShouldBe(10);
            var last = events[^1];
            last.OldValue.ShouldBe("+1201555012");
            last.NewValue.ShouldBe("+12015550123");
            last.Formatted.ShouldBe("(201) 555-0123");
            last.Status.ShouldBe(PhoneNumberStatus.Valid);
            last.IsValid.ShouldBeTrue();
            last.Country.Iso.ShouldBe("US");
            TestHelpers.Descendant<PhoneInputPresence>(input, PhoneInput.ValidPart).IsVisible.ShouldBeTrue();
            input.LiveText.ShouldBe("Valid United States number");
            // Capped at the longest length: an eleventh digit is refused and the caret stays at the end.
            Type(window, "4");
            input.Text.ShouldBe("(201) 555-0123");
            input.CaretIndex.ShouldBe(14);
            // Letters are refused.
            Type(window, "a");
            input.Text.ShouldBe("(201) 555-0123");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Caret_Stays_After_The_Same_Digit()
    {
        var input = Field("+12015550123");
        var window = Show(input);
        try
        {
            Focus(input);
            input.CaretIndex = 4; // "(201|) 555-0123"
            Type(window, "9");
            input.Text.ShouldBe("(201) 955-5012");
            input.CaretIndex.ShouldBe(7);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_And_Delete_On_A_Separator_Remove_The_Digit_Beside_It()
    {
        var input = Field("+12015550123");
        var window = Show(input);
        try
        {
            Focus(input);
            input.CaretIndex = 10; // "(201) 555-|0123"
            Press(window, PhysicalKey.Backspace);
            input.Text.ShouldBe("(201) 550-123");
            input.CaretIndex.ShouldBe(8);
            input.Value.ShouldBe("+1201550123");

            input.CaretIndex = 4; // "(201|) 550-123"
            Press(window, PhysicalKey.Delete);
            input.Text.ShouldBe("(201) 501-23");
            input.CaretIndex.ShouldBe(4);

            // A plain digit deletes as usual and the number reflows.
            input.CaretIndex = input.Text!.Length;
            Press(window, PhysicalKey.Backspace);
            input.Text.ShouldBe("(201) 501-2");
            Press(window, PhysicalKey.Backspace);
            input.Text.ShouldBe("(201) 501");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Trunk_Prefix_Is_Kept_And_Earns_Its_Room()
    {
        var input = Field();
        input.Country = "GB";
        var window = Show(input);
        try
        {
            Focus(input);
            Type(window, "0");
            input.Text.ShouldBe("0");
            input.Value.ShouldBe(string.Empty);
            Type(window, "79111234567");
            input.Text.ShouldBe("07911 123456");
            input.Value.ShouldBe("+447911123456");
            input.Status.ShouldBe(PhoneNumberStatus.Valid);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_Reads_International_Numbers_And_Fragments()
    {
        var input = Field();
        var window = Show(input);
        try
        {
            Focus(input);
            input.PasteText("+44 (0)7911 123456");
            input.Country.ShouldBe("GB");
            input.Text.ShouldBe("7911 123456");
            input.Value.ShouldBe("+447911123456");
            input.LiveText.ShouldBe("Country set to United Kingdom");

            input.PasteText("+1 416 555 0123");
            input.Country.ShouldBe("CA");
            input.Value.ShouldBe("+14165550123");
            input.LiveText.ShouldBe("Country set to Canada");

            // A whole national number replaces the field.
            input.PasteText("(902) 555 0199");
            input.Value.ShouldBe("+19025550199");

            // A fragment goes in at the caret.
            input.Value = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Type(window, "902");
            input.PasteText("555");
            input.Text.ShouldBe("(902) 555");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typed_International_Text_Picks_Its_Country()
    {
        var input = Field();
        var window = Show(input);
        try
        {
            Focus(input);
            window.KeyTextInput("+33 6 12 34 56 78");
            Dispatcher.UIThread.RunJobs();
            input.Country.ShouldBe("FR");
            input.Text.ShouldBe("6 12 34 56 78");
            input.Value.ShouldBe("+33612345678");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Programmatic_Value_Reads_Back_Without_Announcing()
    {
        var input = Field();
        var events = new List<PhoneInputValueChangedEventArgs>();
        input.ValueChanged += (_, e) => events.Add(e);
        var window = Show(input);
        try
        {
            input.Value = "+447400123456";
            Dispatcher.UIThread.RunJobs();
            input.Country.ShouldBe("GB");
            input.Text.ShouldBe("7400 123456");
            input.SelectedCountry.Name.ShouldBe("United Kingdom");
            input.Status.ShouldBe(PhoneNumberStatus.Valid);
            input.LiveText.ShouldBe(string.Empty);
            events.Count.ShouldBe(1);
            events[0].OldValue.ShouldBe(string.Empty);
            events[0].NewValue.ShouldBe("+447400123456");

            // Formatting in the value is normalized to E.164.
            input.Value = "+44 7400 123457";
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe("+447400123457");

            // A country set from outside keeps the digits.
            input.Country = "IE";
            Dispatcher.UIThread.RunJobs();
            input.Value.ShouldBe("+3537400123457");
            AutomationProperties.GetName(TestHelpers.Descendant<PhoneInputTrigger>(input)).ShouldBe("Country, Ireland +353");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Validates_After_Blur_Only_When_Digits_Were_Entered()
    {
        var input = Field();
        var other = new TextBox();
        var window = TestHelpers.Show(new StackPanel { Children = { input, other } });
        try
        {
            Focus(input);
            Focus(other);
            input.DisplayErrorMessage.ShouldBeNull();

            Focus(input);
            Type(window, "201");
            input.DisplayErrorMessage.ShouldBeNull();
            Focus(other);
            input.ValidationMessage.ShouldBe("Numbers in United States have 10 digits");
            input.DisplayErrorMessage.ShouldBe("Numbers in United States have 10 digits");
            input.DisplayInvalid.ShouldBeTrue();
            TestHelpers.Descendant<FieldFrame>(input).IsInvalid.ShouldBeTrue();

            input.ErrorMessage = "Use your mobile number.";
            input.DisplayErrorMessage.ShouldBe("Use your mobile number.");
            input.ErrorMessage = null;

            input.IsValidationEnabled = false;
            input.DisplayErrorMessage.ShouldBeNull();
            input.DisplayInvalid.ShouldBeFalse();
            input.IsValidationEnabled = true;

            input.Value = "+12015550123";
            Dispatcher.UIThread.RunJobs();
            input.DisplayErrorMessage.ShouldBeNull();

            // Description shows only while there is no message.
            input.Description = "We text a code.";
            input.DisplayDescription.ShouldBe("We text a code.");
            input.Value = "+1201";
            Dispatcher.UIThread.RunJobs();
            input.DisplayDescription.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ picker

    [AvaloniaFact]
    public void Trigger_Arrows_Open_On_The_Current_Country()
    {
        var input = Field();
        var window = Show(input);
        try
        {
            var trigger = TestHelpers.Descendant<PhoneInputTrigger>(input);
            trigger.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            input.IsPickerOpen.ShouldBeTrue();
            input.Query.ShouldBe(string.Empty);
            var list = input.CountryList!;
            list.Sections.Count.ShouldBe(2);
            list.Sections[0].Label.ShouldBe("Suggested");
            list.Sections[0].Countries.Select(c => c.Iso).ShouldBe(new[] { "US", "CA", "GB" });
            list.Sections[1].Label.ShouldBe("All countries");
            list.Sections[1].Countries.Count.ShouldBe(49);
            list.Sections[1].Countries[0].Name.ShouldBe("Argentina");
            list.RowCount.ShouldBe(52);
            list.KeyAt(list.ActiveRow).ShouldBe("a-US");
            input.SearchBox!.IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(input.SearchBox).ShouldBe("Search countries or calling codes");
            input.ActiveOptionText.ShouldBe("United States +1");

            // Arc's extent: label 28.8, rows 36 with 2px gaps, 6px between groups.
            list.Extent.ShouldBe(2 * 28.8 + 52 * 36 + 51 * 2 + 4 + 2 * 2, 0.01);
            var surface = input.Surface!;
            surface.ShapeSize.Width.ShouldBe(340);
            surface.ShapeSize.Height.ShouldBe(42 + 2 + 6 + 280 + 6, 0.01);
            surface.ShapeRadius.ShouldBe(22);
            surface.IsOpenShape.ShouldBeTrue();

            Press(window, PhysicalKey.ArrowDown);
            list.KeyAt(list.ActiveRow).ShouldBe("a-VN");
            Press(window, PhysicalKey.Home);
            list.ActiveRow.ShouldBe(0);
            Press(window, PhysicalKey.PageDown);
            list.ActiveRow.ShouldBe(8);
            Press(window, PhysicalKey.PageUp);
            list.ActiveRow.ShouldBe(0);
            Press(window, PhysicalKey.ArrowUp);
            list.ActiveRow.ShouldBe(0);
            Press(window, PhysicalKey.End);
            list.ActiveRow.ShouldBe(51);

            Press(window, PhysicalKey.Escape);
            input.IsPickerOpen.ShouldBeFalse();
            trigger.IsFocused.ShouldBeTrue();
            input.Country.ShouldBe("US");
            surface.ShapeRadius.ShouldBe(17);
            surface.IsOpenShape.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plus_In_The_Number_Searches_Calling_Codes()
    {
        var input = Field("+12015550123");
        var events = new List<PhoneInputValueChangedEventArgs>();
        input.ValueChanged += (_, e) => events.Add(e);
        var window = Show(input);
        try
        {
            Focus(input);
            Type(window, "+");
            input.IsPickerOpen.ShouldBeTrue();
            input.Query.ShouldBe("+");
            input.Text.ShouldBe("(201) 555-0123");
            var list = input.CountryList!;
            list.Sections[0].Label.ShouldBe("Suggested");
            list.ActiveRow.ShouldBe(0);

            Type(window, "44");
            input.Query.ShouldBe("+44");
            list.Sections.Count.ShouldBe(1);
            list.Sections[0].Label.ShouldBeNull();
            list.RowCount.ShouldBe(1);
            input.LiveText.ShouldBe("1 country");

            Press(window, PhysicalKey.Enter);
            input.IsPickerOpen.ShouldBeFalse();
            input.Country.ShouldBe("GB");
            input.Value.ShouldBe("+442015550123");
            input.LiveText.ShouldBe("United Kingdom, +44");
            input.IsFocused.ShouldBeTrue();
            input.CaretIndex.ShouldBe(input.Text!.Length);
            events.Count.ShouldBe(1);
            events[0].Country.Iso.ShouldBe("GB");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Calling_Code_Results_Put_The_Exact_Code_First()
    {
        var input = Field();
        var window = Show(input);
        try
        {
            input.OpenPicker();
            Dispatcher.UIThread.RunJobs();
            input.SearchBox!.Text = "1";
            Dispatcher.UIThread.RunJobs();
            var list = input.CountryList!;
            // Only +1 matches "1": Canada and the United States, by name.
            list.CountryAt(0)!.Dial.ShouldBe("1");
            list.CountryAt(1)!.Dial.ShouldBe("1");
            input.LiveText.ShouldBe($"{list.RowCount} countries");

            input.SearchBox.Text = "zzz";
            Dispatcher.UIThread.RunJobs();
            list.RowCount.ShouldBe(0);
            list.EmptyText.ShouldBe("No countries match “zzz”");
            input.LiveText.ShouldBe("No matches");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Printable_Key_On_The_Trigger_Opens_A_Search()
    {
        var input = Field();
        var window = Show(input);
        try
        {
            var trigger = TestHelpers.Descendant<PhoneInputTrigger>(input);
            trigger.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("n");
            Dispatcher.UIThread.RunJobs();
            input.IsPickerOpen.ShouldBeTrue();
            input.Query.ShouldBe("n");
            input.Text.ShouldBe(string.Empty);
            var list = input.CountryList!;
            list.ActiveRow.ShouldBe(0);
            list.Sections.Count.ShouldBe(1);
            // Home / End move the caret while there is a query.
            Press(window, PhysicalKey.End);
            list.ActiveRow.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_Keeps_Digits_Capped_And_Rolls()
    {
        var input = Field("+12015550123");
        var window = Show(input);
        try
        {
            input.OpenPicker();
            Dispatcher.UIThread.RunJobs();
            var list = input.CountryList!;
            var norway = Enumerable.Range(0, list.RowCount).First(i => list.KeyAt(i) == "a-NO");
            list.InvokeRow(norway);
            Dispatcher.UIThread.RunJobs();
            input.IsPickerOpen.ShouldBeFalse();
            input.Country.ShouldBe("NO");
            input.Text.ShouldBe("201 55 501");
            input.Value.ShouldBe("+4720155501");
            TestHelpers.Descendant<PhoneInputFace>(input).Country!.Iso.ShouldBe("NO");
            AutomationProperties.GetName(TestHelpers.Descendant<PhoneInputTrigger>(input)).ShouldBe("Country, Norway +47");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Restricted_And_Unpreferred_Lists()
    {
        var restricted = Field();
        restricted.Countries = new[] { "GB", "IE", "FR" };
        restricted.PreferredCountries = new[] { "US", "IE" };
        var plain = Field();
        plain.PreferredCountries = Array.Empty<string>();
        var window = Show(new StackPanel { Children = { restricted, plain } });
        try
        {
            restricted.OpenPicker();
            Dispatcher.UIThread.RunJobs();
            var list = restricted.CountryList!;
            list.Sections[0].Countries.Select(c => c.Iso).ShouldBe(new[] { "IE" });
            list.Sections[1].Countries.Select(c => c.Iso).ShouldBe(new[] { "FR", "IE", "GB" });
            restricted.AvailableCountries.Count.ShouldBe(3);
            restricted.ClosePicker();
            Dispatcher.UIThread.RunJobs();

            plain.OpenPicker();
            Dispatcher.UIThread.RunJobs();
            plain.CountryList!.Sections.Count.ShouldBe(1);
            plain.CountryList.Sections[0].Label.ShouldBeNull();
            plain.CountryList.RowCount.ShouldBe(49);
            plain.CountryList.Extent.ShouldBe(49 * 36 + 48 * 2, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Press_Outside_Closes_And_Disabled_Never_Opens()
    {
        var input = Field();
        var disabled = Field();
        disabled.IsEnabled = false;
        var window = Show(new StackPanel { Children = { input, disabled, new Border { Height = 200, Background = Avalonia.Media.Brushes.Transparent } } });
        try
        {
            input.OpenPicker();
            Dispatcher.UIThread.RunJobs();
            input.IsPickerOpen.ShouldBeTrue();
            window.MouseDown(new Point(300, 650), MouseButton.Left);
            window.MouseUp(new Point(300, 650), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            input.IsPickerOpen.ShouldBeFalse();

            disabled.OpenPicker();
            disabled.IsPickerOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Order_Is_Button_Then_Number()
    {
        var before = new TextBox();
        var input = Field();
        var after = new TextBox();
        var window = Show(new StackPanel { Children = { before, input, after } });
        try
        {
            var trigger = TestHelpers.Descendant<PhoneInputTrigger>(input);
            before.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Tab);
            trigger.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Tab);
            input.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Tab);
            after.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            input.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            trigger.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            before.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_On_The_Trigger_Never_Edit_The_Number()
    {
        var input = Field("+12015550123");
        var window = Show(input);
        try
        {
            var trigger = TestHelpers.Descendant<PhoneInputTrigger>(input);
            trigger.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Backspace);
            Press(window, PhysicalKey.Delete);
            input.Text.ShouldBe("(201) 555-0123");
            trigger.Classes.Contains(":focus-visible").ShouldBeTrue();
            // Only the number's own focus darkens the shell border.
            TestHelpers.ColorOf(TestHelpers.Descendant<FieldFrame>(input).BorderBrush)
                .ShouldBe(ArcPalette.Neutrals(false).BorderStrong);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Surface()
    {
        var input = Field();
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { input } }, Motion.ArcMotionLevel.Reduced);
        try
        {
            window.Width = 600;
            window.Height = 700;
            Dispatcher.UIThread.RunJobs();
            input.OpenPicker();
            Dispatcher.UIThread.RunJobs();
            input.Surface!.ShapeSize.Width.ShouldBe(340);
            input.Surface.ShapeRadius.ShouldBe(22);
        }
        finally
        {
            window.Close();
        }
    }
}
