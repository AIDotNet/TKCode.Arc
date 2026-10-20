using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Messages;

public class CookieConsentTests
{
    private static (Window Window, CookieConsent Consent) Show(CookieConsent consent, double width = 900)
    {
        var host = new Grid { Height = 520, Children = { new Border(), consent } };
        var window = TestHelpers.Show(host);
        window.Width = width;
        window.Height = 600;
        Settle(window);
        return (window, consent);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static Button Part(CookieConsent consent, string name)
        => consent.GetVisualDescendants().OfType<Button>().First(b => b.Name == name);

    private static void Press(Window window, Button button)
    {
        button.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);
    }

    [AvaloniaFact]
    public void Undecided_Visitor_Sees_The_Bar()
    {
        var (window, consent) = Show(new CookieConsent { Storage = new InMemoryMessageStorage() });
        try
        {
            consent.Mode.ShouldBe(CookieConsentMode.Bar);
            var shape = consent.Surface!.Shape;
            shape.Width.ShouldBe(660, 0.5);
            // Centered in the 900 - 32 layer and resting on its bottom edge.
            shape.X.ShouldBe((868 - 660) / 2.0, 0.5);
            consent.Surface.ShapeRadius.ShouldBe(22);
            Part(consent, "PART_BarReject").Content.ShouldBe("Reject all");
            Part(consent, "PART_BarAccept").Content.ShouldBe("Accept all");
            consent.Consent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Accept_All_Folds_Into_The_Reopen_Button_And_Stores_The_Choice()
    {
        var storage = new InMemoryMessageStorage();
        var (window, consent) = Show(new CookieConsent { Storage = storage, StorageKey = "consent" });
        try
        {
            CookieConsentChangedEventArgs? args = null;
            consent.ConsentChanged += (_, e) => args = e;
            Press(window, Part(consent, "PART_BarAccept"));
            args.ShouldNotBeNull();
            args.Action.ShouldBe(CookieConsentAction.AcceptAll);
            consent.Consent!["marketing"].ShouldBeTrue();
            consent.Consent["necessary"].ShouldBeTrue();
            consent.Mode.ShouldBe(CookieConsentMode.Chip);
            consent.Surface!.Shape.Width.ShouldBe(44, 0.5);
            consent.Surface.Shape.X.ShouldBe(0, 0.5);
            Part(consent, "PART_Chip").IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(Part(consent, "PART_Chip")).ShouldBe("Cookie preferences");
            var record = CookieConsent.Read("consent", "1", storage);
            record.ShouldNotBeNull();
            record.Value["analytics"].ShouldBeTrue();
            CookieConsent.Read("consent", "2", storage).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reject_All_Keeps_Only_Required_Categories()
    {
        var (window, consent) = Show(new CookieConsent { Storage = new InMemoryMessageStorage() });
        try
        {
            Press(window, Part(consent, "PART_BarReject"));
            consent.Consent!["necessary"].ShouldBeTrue();
            consent.Consent["preferences"].ShouldBeFalse();
            consent.Consent["analytics"].ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Preferences_Save_Draft_And_Escape_Closes_Without_Saving()
    {
        var (window, consent) = Show(new CookieConsent { Storage = new InMemoryMessageStorage() });
        try
        {
            Press(window, Part(consent, "PART_Customize"));
            consent.IsPreferencesOpen.ShouldBeTrue();
            consent.Mode.ShouldBe(CookieConsentMode.Panel);
            consent.Surface!.Shape.Width.ShouldBe(420, 0.5);
            consent.Surface.ShapeRadius.ShouldBe(26);
            consent.Switches.Count.ShouldBe(3);
            consent.Switches["analytics"].IsChecked.ShouldBe(false);
            var save = Part(consent, "PART_Save");
            AutomationProperties.GetHelpText(save).ShouldBe("0 of 3 optional categories allowed");
            consent.Switches["analytics"].IsChecked = true;
            AutomationProperties.GetHelpText(save).ShouldBe("1 of 3 optional categories allowed");

            consent.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Title").Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle(window);
            consent.IsPreferencesOpen.ShouldBeFalse();
            consent.Consent.ShouldBeNull();
            consent.Mode.ShouldBe(CookieConsentMode.Bar);

            consent.OpenPreferences();
            Settle(window);
            consent.Switches["analytics"].IsChecked.ShouldBe(false);
            consent.Switches["analytics"].IsChecked = true;
            Press(window, save);
            consent.Consent!["analytics"].ShouldBeTrue();
            consent.Consent["marketing"].ShouldBeFalse();
            consent.Mode.ShouldBe(CookieConsentMode.Chip);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stored_Choice_Starts_Folded_And_Reset_Asks_Again()
    {
        var storage = new InMemoryMessageStorage();
        storage.Write("consent", new CookieConsentRecord(new Dictionary<string, bool> { ["analytics"] = true }, "1", DateTimeOffset.Now).ToJson());
        var (window, consent) = Show(new CookieConsent { Storage = storage, StorageKey = "consent", Position = CookieConsentPosition.BottomRight });
        try
        {
            consent.Mode.ShouldBe(CookieConsentMode.Chip);
            consent.Consent!["analytics"].ShouldBeTrue();
            consent.Surface!.Shape.Right.ShouldBe(868, 0.5);
            consent.Reset();
            Settle(window);
            consent.Consent.ShouldBeNull();
            consent.Mode.ShouldBe(CookieConsentMode.Bar);
            storage.Read("consent").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Reopen_Button_A_Choice_Hides_The_Surface()
    {
        var (window, consent) = Show(new CookieConsent { Storage = new InMemoryMessageStorage(), IsReopenButtonVisible = false });
        try
        {
            Press(window, Part(consent, "PART_BarAccept"));
            consent.Mode.ShouldBe(CookieConsentMode.None);
            consent.Surface!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Layer_Stacks_The_Bar()
    {
        var (window, consent) = Show(new CookieConsent { Storage = new InMemoryMessageStorage() }, width: 420);
        try
        {
            consent.Classes.ShouldContain(":bar-stacked");
            consent.Surface!.Shape.Width.ShouldBe(388, 0.5);
            var reject = Part(consent, "PART_BarReject");
            Grid.GetRow(reject).ShouldBe(0);
            Grid.GetRow(Part(consent, "PART_Customize")).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Items_Source_Replaces_The_Default_Categories()
    {
        var consent = new CookieConsent
        {
            Storage = new InMemoryMessageStorage(),
            ItemsSource = new List<CookieCategory>
            {
                new() { Id = "essential", Label = "Essential", IsRequired = true },
                new() { Id = "telemetry", Label = "Telemetry", IsEnabledByDefault = true }
            }
        };
        var (window, _) = Show(consent);
        try
        {
            consent.OpenPreferences();
            Settle(window);
            consent.Switches.Count.ShouldBe(1);
            consent.Switches["telemetry"].IsChecked.ShouldBe(true);
            Press(window, Part(consent, "PART_Save"));
            consent.Consent!["essential"].ShouldBeTrue();
            consent.Consent["telemetry"].ShouldBeTrue();
            consent.Consent.ContainsKey("marketing").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
