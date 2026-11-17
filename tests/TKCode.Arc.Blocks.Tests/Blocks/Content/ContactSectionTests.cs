using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Content;

public class ContactSectionTests
{
    private static Window Show(Control control, double width = 1100)
    {
        var window = TestHelpers.Show(control);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Fill(ContactFormView form, string name = "Ada Moreno", string email = "ada@example.com", string message = "I would like to know more about pricing.")
    {
        form.NameField.Text = name;
        form.EmailField.Text = email;
        form.MessageField.Text = message;
        Dispatcher.UIThread.RunJobs();
    }

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("first.last@sub.example.org", true)]
    [InlineData("a@b.c", false)]
    [InlineData("a@b", false)]
    [InlineData("@b.co", false)]
    [InlineData("a b@c.co", false)]
    [InlineData("a@@b.co", false)]
    [InlineData("a@.co", false)]
    public void Email_Rule(string value, bool valid) => ContactRules.IsEmail(value).ShouldBe(valid);

    [Fact]
    public void Validation_Messages()
    {
        ContactRules.ValidateName(" ").ShouldBe("Enter your name");
        ContactRules.ValidateName("Ada").ShouldBeNull();
        ContactRules.ValidateEmail("").ShouldBe("Enter your email address");
        ContactRules.ValidateEmail("nope").ShouldBe("Enter an email like name@example.com");
        ContactRules.ValidateMessage("").ShouldBe("Tell us how we can help");
        ContactRules.ValidateMessage("short").ShouldBe("Add a little more detail, at least 20 characters");
        ContactRules.ValidateMessage(new string('x', 20)).ShouldBeNull();
    }

    [Theory]
    [InlineData(18, "6pm")]
    [InlineData(9, "9am")]
    [InlineData(12, "12pm")]
    [InlineData(24, "12am")]
    public void Hour_Label(int hour, string label) => ContactRules.HourLabel(hour).ShouldBe(label);

    [AvaloniaFact]
    public async Task Submit_With_Wrong_Fields_Shows_Errors_And_Focuses_The_First()
    {
        var section = new ContactSection { SubmitHandler = _ => throw new InvalidOperationException("should not run") };
        Show(section);
        var form = section.Form!;
        await form.SubmitAsync();
        form.NameField.ErrorMessage.ShouldBe("Enter your name");
        form.EmailField.ErrorMessage.ShouldBe("Enter your email address");
        form.MessageField.ErrorMessage.ShouldBe("Tell us how we can help");
        form.NameField.IsFocused.ShouldBeTrue();
        form.IsSent.ShouldBeFalse();

        // After the first attempt, errors update as you type.
        form.NameField.Text = "Ada";
        Dispatcher.UIThread.RunJobs();
        form.NameField.ErrorMessage.ShouldBeNull();
        form.EmailField.ErrorMessage.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task A_Valid_Message_Reaches_The_Handler_Trimmed_And_Shows_The_Confirmation()
    {
        ContactMessage? received = null;
        var section = new ContactSection
        {
            SubmitHandler = message =>
            {
                received = message;
                return Task.CompletedTask;
            }
        };
        Show(section);
        var form = section.Form!;
        Fill(form, "  Ada Moreno ", " ada@example.com ", "  I would like to know more about pricing.  ");
        form.Picker.Select("Support");
        await form.SubmitAsync();
        received.ShouldNotBeNull();
        received.Name.ShouldBe("Ada Moreno");
        received.Email.ShouldBe("ada@example.com");
        received.Topic.ShouldBe("Support");
        received.Message.ShouldBe("I would like to know more about pricing.");
        form.IsSent.ShouldBeTrue();
        form.SuccessTitle.ShouldBe("Thanks, Ada");
        form.SuccessText!.ShouldContain("support team");
        form.SuccessText!.ShouldContain("ada@example.com");
    }

    [AvaloniaFact]
    public async Task A_Failed_Send_Keeps_The_Form_And_Shows_A_Message()
    {
        var section = new ContactSection { SubmitHandler = _ => Task.FromException(new InvalidOperationException("down")) };
        Show(section);
        var form = section.Form!;
        Fill(form);
        await form.SubmitAsync();
        form.IsSent.ShouldBeFalse();
        form.IsSending.ShouldBeFalse();
        form.NameField.Text.ShouldBe("Ada Moreno");
        form.NameField.IsReadOnly.ShouldBeFalse();
        form.FailureLine.Text.ShouldNotBeNull();
        form.FailureLine.Text!.ShouldContain("couldn't send");
    }

    [AvaloniaFact]
    public async Task The_Form_Is_Read_Only_While_It_Sends()
    {
        var gate = new TaskCompletionSource();
        var section = new ContactSection { SubmitHandler = _ => gate.Task };
        Show(section);
        var form = section.Form!;
        Fill(form);
        var sending = form.SubmitAsync();
        form.IsSending.ShouldBeTrue();
        form.SubmitButton.IsLoading.ShouldBeTrue();
        form.EmailField.IsReadOnly.ShouldBeTrue();
        gate.SetResult();
        await sending;
        form.IsSending.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Message_Counts_The_Characters_Left()
    {
        var section = new ContactSection();
        Show(section);
        var form = section.Form!;
        form.MessageField.Description.ShouldBe("500 characters left");
        form.MessageField.Text = "hello";
        Dispatcher.UIThread.RunJobs();
        form.MessageField.Description.ShouldBe("495 characters left");
        form.MessageField.MaxLength.ShouldBe(500);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_The_Topic_And_Wrap()
    {
        var section = new ContactSection();
        section.Topics.Add("One");
        section.Topics.Add("Two");
        var window = Show(section);
        var picker = section.Form!.Picker;
        picker.Selected.ShouldBe("One");
        picker.Chips[0].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        picker.Selected.ShouldBe("Two");
        picker.Chips[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        picker.Selected.ShouldBe("One");
        picker.Chips[0].IsTabStop.ShouldBeTrue();
        picker.Chips[1].IsTabStop.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Channels_Show_The_Samples_And_The_First_Is_Chosen()
    {
        var section = new ContactSection { Variant = ContactSectionVariant.Channels };
        Show(section);
        section.Tabs.Count.ShouldBe(4);
        section.Tabs[0].IsSelected.ShouldBeTrue();
        section.ChannelPanel!.Current.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Channel_Keys_Move_The_Choice()
    {
        var section = new ContactSection { Variant = ContactSectionVariant.Channels };
        var window = Show(section);
        section.Tabs[0].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.SelectedChannel.ShouldBe(section.Tabs[1].Channel.Value);
        section.Tabs[1].IsSelected.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Tabs[3].IsSelected.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Tabs[0].IsSelected.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        section.Tabs[0].IsSelected.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Coming_Back_To_A_Channel_Reuses_Its_Detail()
    {
        var detail = new TextBlock { Text = "First" };
        var section = new ContactSection { Variant = ContactSectionVariant.Channels };
        section.Channels.Add(new ContactChannel { Value = "a", Label = "A", Detail = detail });
        section.Channels.Add(new ContactChannel { Value = "b", Label = "B", Detail = "Second" });
        Show(section);
        section.SelectedChannel = "b";
        Dispatcher.UIThread.RunJobs();
        section.SelectedChannel = "a";
        Dispatcher.UIThread.RunJobs();
        section.Tabs[0].IsSelected.ShouldBeTrue();
        TopLevel.GetTopLevel(detail).ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Channels_Run_Sideways_When_Narrow()
    {
        var section = new ContactSection { Variant = ContactSectionVariant.Channels };
        var window = Show(section, 1100);
        section.Tabs[0].IsCompact.ShouldBeFalse();
        window.Width = 600;
        Dispatcher.UIThread.RunJobs();
        section.Tabs[0].IsCompact.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Sample_Confirm_Action_Confirms_And_Resets()
    {
        var note = new TextBlock { Text = "Done" };
        var action = new ContactConfirmAction("Do it", "Did it", note, isBusyFirst: false);
        var window = Show(action);
        var button = action.ActionButton;
        button.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        action.IsDone.ShouldBeTrue();
        AutomationProperties.GetName(button).ShouldBe("Did it");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        action.IsDone.ShouldBeFalse();
        AutomationProperties.GetName(button).ShouldBe("Do it");
    }

    [AvaloniaFact]
    public void Office_Status_Follows_The_Local_Time()
    {
        var section = new ContactSection { Variant = ContactSectionVariant.Offices };
        section.Offices.Add(new ContactOffice { City = "Utc City", TimeZone = "UTC", Address = new(["1 Road"]), OpenHour = 9, CloseHour = 18 });
        // Monday 2026-11-09, 10:30 UTC.
        section.Now = () => new DateTimeOffset(2026, 11, 9, 10, 30, 0, TimeSpan.Zero);
        Show(section);
        section.RefreshOffices();
        var card = section.OfficeCards[0];
        card.Time.ShouldBe("10:30 AM");
        card.Status.ShouldBe("Open until 6pm");
        card.IsOpen.ShouldBeTrue();

        section.Now = () => new DateTimeOffset(2026, 11, 9, 20, 0, 0, TimeSpan.Zero);
        section.RefreshOffices();
        card.Status.ShouldBe("Closed now");
        card.IsOpen.ShouldBeFalse();

        // Saturday.
        section.Now = () => new DateTimeOffset(2026, 11, 14, 10, 30, 0, TimeSpan.Zero);
        section.RefreshOffices();
        card.Status.ShouldBe("Closed now");
    }

    [AvaloniaFact]
    public void An_Unknown_Time_Zone_Says_So()
    {
        var section = new ContactSection { Variant = ContactSectionVariant.Offices };
        section.Offices.Add(new ContactOffice { City = "Nowhere", TimeZone = "Not/AZone" });
        Show(section);
        section.OfficeCards[0].Time.ShouldBe("--:--");
        section.OfficeCards[0].Status.ShouldBe("Checking hours");
    }

    [AvaloniaTheory]
    [InlineData(1100, 3)]
    [InlineData(700, 2)]
    [InlineData(320, 1)]
    public void Office_Cards_Fit_As_Many_Columns_As_They_Have_Room_For(double width, int columns)
    {
        var section = new ContactSection { Variant = ContactSectionVariant.Offices };
        Show(section, width);
        section.OfficeColumns.ShouldBe(columns);
    }

    [AvaloniaFact]
    public void The_Form_Stacks_Under_Its_Heading_At_820()
    {
        var section = new ContactSection();
        var window = Show(section, 1100);
        section.IsLayoutStacked.ShouldBeFalse();
        window.Width = 700;
        Dispatcher.UIThread.RunJobs();
        section.IsLayoutStacked.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Section_Is_Named_By_Its_Title()
    {
        var section = new ContactSection { Title = "Say hello" };
        Show(section);
        AutomationProperties.GetName(section).ShouldBe("Say hello");
    }
}
