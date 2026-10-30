using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// SmartFormFill: the contact form it opens with, reading (simulated and through an app extractor), Stop and Escape, the
/// fill (at once with reduced motion or off, 140ms apart with full motion), confidence badges, review, checks, edits,
/// source highlights, Undo, Save, the breakpoints, the keyboard model, the labels, the autoplay loop and the built-in parser.
/// </summary>
public class SmartFormFillTests
{
    /// <summary>The invented texts and model.</summary>
    private static SmartFormFillSimulation Demo() => new()
    {
        Samples = SmartFormFillFixture.Samples,
        ModelName = SmartFormFillFixture.ModelName,
    };

    /// <summary>A card playing the fixture texts (pass <paramref name="simulate"/> false for a card with no data).</summary>
    private static (SmartFormFill Card, Window Window, SmartFormFillTestTimers Timers) Open(ArcMotionLevel level = ArcMotionLevel.Off,
        Action<SmartFormFill>? setup = null, double width = 960, bool simulate = true)
    {
        var timers = new SmartFormFillTestTimers();
        var card = new SmartFormFill { Timers = timers, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (simulate)
        {
            card.Simulation = Demo();
        }
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 1000;
        Pump();
        return (card, window, timers);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void ReadSample(SmartFormFill card, SmartFormFillTestTimers timers, string text = SmartFormFillFixture.Signature)
    {
        _ = card.ReadAsync(text);
        Pump();
        timers.Advance(SmartFormFillReader.ReadTime.TotalMilliseconds);
        Pump();
    }

    [AvaloniaFact]
    public void Opens_On_The_Contact_Form_Ready_For_Text()
    {
        var (card, window, _) = Open();
        try
        {
            card.FieldViews.Select(v => v.Field.Id).ShouldBe(["name", "title", "company", "email", "phone", "address"]);
            card.FieldViews.Select(v => v.Label).ShouldBe(["Name", "Job title", "Company", "Email", "Phone", "Address"]);
            card.Phase.ShouldBe(SmartFormFillPhase.Idle);
            card.StatusText.ShouldBe("Paste text and AI fills the form");
            card.CanUndo.ShouldBeFalse();
            card.UndoButton!.IsEnabled.ShouldBeFalse();
            card.FillButton!.IsEnabled.ShouldBeFalse();
            card.SampleButton!.IsVisible.ShouldBeTrue();
            card.SubmitButton!.Content.ShouldBe("Save contact");
            card.ShowingSource.ShouldBeFalse();
            card.Values.Count.ShouldBe(6);
            card.Values.Values.ShouldAllBe(v => v.Length == 0);
            card.FieldViews.ShouldAllBe(v => v.BadgeText.Length == 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reading_The_Signature_Fills_Every_Field_With_Confidence_And_Flags_Guesses()
    {
        var (card, window, timers) = Open();
        try
        {
            SmartFormFillFilledEventArgs? filled = null;
            var changes = 0;
            card.Filled += (_, e) => filled = e;
            card.ValuesChanged += (_, _) => changes++;

            _ = card.ReadAsync(SmartFormFillFixture.Signature);
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Reading);
            card.StatusText.ShouldBe("Reading with " + SmartFormFillFixture.ModelName);
            card.ShowingSource.ShouldBeTrue();
            card.FillButton!.Content.ShouldBe("Stop");
            card.Values["name"].ShouldBe(string.Empty);

            timers.Advance(SmartFormFillReader.ReadTime.TotalMilliseconds);
            Pump();

            card.Phase.ShouldBe(SmartFormFillPhase.Filled);
            card.Values["name"].ShouldBe("Maya Okafor");
            card.Values["email"].ShouldBe("maya.okafor@example.com");
            card.Values["company"].ShouldBe("Fernbrook Freight");
            card.ReviewCount.ShouldBe(2);
            card.StatusText.ShouldBe("Filled 6 fields, 2 to review");
            card.Announcement.ShouldBe("Filled 6 fields, 2 to review. Simulated");
            card.ViewOf("name")!.BadgeText.ShouldBe("98%");
            card.ViewOf("phone")!.BadgeText.ShouldBe("Review");
            card.ViewOf("phone")!.State.ShouldBe(SmartFormFillFieldState.Review);
            card.ViewOf("phone")!.NoteText!.Text.ShouldBe("Two numbers in the text; picked the mobile.");
            card.ViewOf("phone")!.NoteText!.IsVisible.ShouldBeTrue();
            card.ViewOf("phone")!.CheckButton!.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(card.ViewOf("phone")!.CheckButton!).ShouldBe("Mark Phone as checked");
            card.ViewOf("name")!.CheckButton!.IsVisible.ShouldBeFalse();
            AutomationProperties.GetHelpText(card.ViewOf("name")!.InputPartControl!).ShouldBe("Filled by AI, 98% confident");
            AutomationProperties.GetHelpText(card.ViewOf("phone")!.InputPartControl!)
                .ShouldBe("Needs review, 74% confident. Two numbers in the text; picked the mobile.");
            card.CanUndo.ShouldBeTrue();
            card.UndoButton!.IsEnabled.ShouldBeTrue();

            filled.ShouldNotBeNull();
            filled.FilledCount.ShouldBe(6);
            filled.ReviewCount.ShouldBe(2);
            filled.Result["title"].Value.ShouldBe("Head of Partnerships");
            changes.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Source_Lights_Up_In_The_Pasted_Text_And_The_Active_Field_Takes_The_Strong_Tint()
    {
        var (card, window, timers) = Open();
        try
        {
            ReadSample(card, timers);
            var text = SmartFormFillFixture.Signature;
            var email = card.HighlightOf("email")!;
            email.Start.ShouldBe(text.IndexOf("maya.okafor@example.com", StringComparison.Ordinal));
            email.Length.ShouldBe("maya.okafor@example.com".Length);
            email.Opacity.ShouldBe(0.6, 0.001);
            card.SourceBlock!.Decorations.Count.ShouldBe(6);
            card.SourceBlock.Text.ShouldBe(text);

            // A guess is tinted with the warning color until it is checked.
            var phone = card.HighlightOf("phone")!;
            phone.Background.ShouldBe(card.ReviewHighlightBrush);
            email.Background.ShouldBe(card.HighlightBrush);

            card.OnFieldFocus(card.ViewOf("email")!, true);
            card.ActiveField.ShouldBe(card.ViewOf("email"));
            email.Opacity.ShouldBe(1, 0.001);
            card.OnFieldHover(card.ViewOf("phone")!, true);
            phone.Opacity.ShouldBe(1, 0.001);
            email.Opacity.ShouldBe(0.6, 0.001);
            card.OnFieldHover(card.ViewOf("phone")!, false);
            email.Opacity.ShouldBe(1, 0.001);

            card.Check("phone").ShouldBeTrue();
            phone.Background.ShouldBe(card.HighlightBrush);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Stops_A_Read_And_Nothing_Changes()
    {
        CancellationToken seen = default;
        var started = new TaskCompletionSource<SmartFormFillResult>();
        var (card, window, _) = Open(setup: c => c.Extract = (_, token) =>
        {
            seen = token;
            token.Register(() => started.TrySetCanceled(token));
            return started.Task;
        });
        try
        {
            card.SetFieldValue("name", "Kept Name");
            _ = card.ReadAsync("Someone New\nsomeone@example.com");
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Reading);
            seen.CanBeCanceled.ShouldBeTrue();

            TestHelpers.Descendant<ScrollViewer>(card, "PART_SourceScroll").Focus().ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();

            seen.IsCancellationRequested.ShouldBeTrue();
            card.Phase.ShouldBe(SmartFormFillPhase.Idle);
            card.StatusText.ShouldBe("Stopped. Nothing changed.");
            card.Values["name"].ShouldBe("Kept Name");
            card.Values["email"].ShouldBe(string.Empty);
            card.CanUndo.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_App_Extractor_Gets_The_Text_And_A_New_Read_Cancels_The_Old_One()
    {
        var calls = new List<(string Text, CancellationToken Token, TaskCompletionSource<SmartFormFillResult> Done)>();
        var (card, window, _) = Open(setup: c => c.Extract = (text, token) =>
        {
            var done = new TaskCompletionSource<SmartFormFillResult>();
            calls.Add((text, token, done));
            return done.Task;
        });
        try
        {
            _ = card.ReadAsync("first");
            Pump();
            _ = card.ReadAsync("second");
            Pump();
            calls.Count.ShouldBe(2);
            calls[0].Token.IsCancellationRequested.ShouldBeTrue();
            calls[1].Text.ShouldBe("second");

            // The stale read finishing late changes nothing.
            calls[0].Done.SetResult(new SmartFormFillResult { ["name"] = new("Stale", 1) });
            Pump();
            card.Values["name"].ShouldBe(string.Empty);

            calls[1].Done.SetResult(new SmartFormFillResult
            {
                ["name"] = new("Ava Lindqvist", 0.97, "Ava"),
                ["email"] = new("ava@example.com", 0.6, "ava@example.com", "Personal address."),
                ["unknown"] = new("ignored", 1),
            });
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Filled);
            card.Values["name"].ShouldBe("Ava Lindqvist");
            card.ReviewCount.ShouldBe(1);
            card.Announcement.ShouldBe("Filled 2 fields, 1 to review");
            card.HighlightOf("name").ShouldBeNull(); // "Ava" is not in "second"
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Read_Says_So_And_Keeps_The_Values()
    {
        var (card, window, _) = Open(setup: c => c.Extract = (_, _) => Task.FromException<SmartFormFillResult>(new InvalidOperationException()));
        try
        {
            card.SetFieldValue("email", "kept@example.com");
            card.ReadAsync("anything").Result.ShouldBeFalse();
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Failed);
            card.StatusText.ShouldBe("Couldn't read the text. Try again.");
            card.Values["email"].ShouldBe("kept@example.com");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Fills_In_Form_Order_140ms_Apart()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Full, c => c.AutoPlay = false);
        try
        {
            ReadSample(card, timers);
            card.Phase.ShouldBe(SmartFormFillPhase.Filling);
            card.Values["name"].ShouldBe("Maya Okafor");
            card.Values["title"].ShouldBe(string.Empty);
            card.ActiveField.ShouldBe(card.ViewOf("name"));
            timers.Requested.ShouldContain(140);
            timers.Requested.ShouldContain(700);

            timers.Advance(140);
            Pump();
            card.Values["title"].ShouldBe("Head of Partnerships");
            card.Values["company"].ShouldBe(string.Empty);

            timers.Advance(140 * 4);
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Filled);
            card.Values["address"].ShouldStartWith("500 Harbor Way");
            card.ActiveField.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_During_A_Fill_Lands_Every_Value_At_Once()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Full, c => c.AutoPlay = false);
        try
        {
            ReadSample(card, timers);
            card.Phase.ShouldBe(SmartFormFillPhase.Filling);
            card.Stop().ShouldBeTrue();
            card.Phase.ShouldBe(SmartFormFillPhase.Filled);
            card.Values["address"].ShouldStartWith("500 Harbor Way");
            // The values that had not arrived land without the arrival tint.
            card.FieldViews.Skip(1).ShouldAllBe(v => v.TintOpacity == 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fills_Every_Field_At_Once_Without_The_Tint()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Reduced);
        try
        {
            card.AutoplayEnabled.ShouldBeFalse();
            ReadSample(card, timers);
            card.Phase.ShouldBe(SmartFormFillPhase.Filled);
            card.Values.Values.ShouldAllBe(v => v.Length > 0);
            card.FieldViews.ShouldAllBe(v => v.TintOpacity == 0);
            timers.Requested.ShouldNotContain(140);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checking_The_Guesses_Clears_Review_And_Save_Waits_For_Them()
    {
        var (card, window, timers) = Open();
        try
        {
            ReadSample(card, timers);
            card.SubmitAsync().Result.ShouldBeFalse();
            Pump();
            card.StatusText.ShouldBe("Check 2 fields before saving");
            card.ViewOf("phone")!.InputPartControl!.IsFocused.ShouldBeTrue();
            card.Phase.ShouldBe(SmartFormFillPhase.Filled);

            card.Check("name").ShouldBeFalse();
            card.Check("phone").ShouldBeTrue();
            card.ViewOf("phone")!.BadgeText.ShouldBe("Checked");
            card.ViewOf("phone")!.CheckButton!.IsVisible.ShouldBeFalse();
            card.ViewOf("phone")!.NoteText!.IsVisible.ShouldBeFalse();
            card.ReviewCount.ShouldBe(1);
            card.StatusText.ShouldBe("Phone checked, 1 left to review");

            card.ViewOf("address")!.CheckButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.ReviewCount.ShouldBe(0);
            card.StatusText.ShouldBe("All fields checked");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editing_A_Filled_Value_Marks_It_Edited_And_Ends_Its_Review()
    {
        var (card, window, timers) = Open();
        try
        {
            ReadSample(card, timers);
            var changed = new List<IReadOnlyDictionary<string, string>>();
            card.ValuesChanged += (_, e) => changed.Add(e.Values);
            card.ViewOf("address")!.InputPartControl!.Text = "1 Pier Road, Oakland, CA";
            Pump();
            card.ViewOf("address")!.State.ShouldBe(SmartFormFillFieldState.Edited);
            card.ViewOf("address")!.BadgeText.ShouldBe("Edited");
            card.ReviewCount.ShouldBe(1);
            card.HighlightOf("address").ShouldBeNull();
            changed.Count.ShouldBe(1);
            changed[0]["address"].ShouldBe("1 Pier Road, Oakland, CA");
            card.Values["address"].ShouldBe("1 Pier Road, Oakland, CA");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Undo_Restores_The_Values_From_Before_The_Fill()
    {
        var (card, window, timers) = Open(setup: c => c.DefaultValues = new Dictionary<string, string> { ["name"] = "Draft Name", ["phone"] = "+1 555 0100" });
        try
        {
            card.Values["name"].ShouldBe("Draft Name");
            ReadSample(card, timers);
            card.Values["name"].ShouldBe("Maya Okafor");

            IReadOnlyDictionary<string, string>? restored = null;
            card.Undone += (_, e) => restored = e.Values;
            card.Undo().ShouldBeTrue();
            Pump();
            card.Values["name"].ShouldBe("Draft Name");
            card.Values["phone"].ShouldBe("+1 555 0100");
            card.Values["email"].ShouldBe(string.Empty);
            restored!["name"].ShouldBe("Draft Name");
            card.CanUndo.ShouldBeFalse();
            card.ReviewCount.ShouldBe(0);
            card.Phase.ShouldBe(SmartFormFillPhase.Idle);
            card.StatusText.ShouldBe("Restored the previous values");
            card.SourceBlock!.Decorations.Count.ShouldBe(0);
            card.FieldViews.ShouldAllBe(v => v.BadgeText.Length == 0);
            card.Undo().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Save_Shows_Loading_Then_Saved_And_A_Fault_Shows_An_Error()
    {
        var pending = new TaskCompletionSource();
        IReadOnlyDictionary<string, string>? saved = null;
        var (card, window, _) = Open(setup: c => c.Submit = values =>
        {
            saved = values;
            return pending.Task;
        });
        try
        {
            IReadOnlyDictionary<string, string>? submitted = null;
            card.Submitted += (_, e) => submitted = e.Values;
            card.SetFieldValue("name", "Rin Takeda");
            var save = card.SubmitAsync();
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Saving);
            ((Button)card.SubmitButton!).IsLoading.ShouldBeTrue();
            saved!["name"].ShouldBe("Rin Takeda");
            pending.SetResult();
            Pump();
            save.IsCompleted.ShouldBeTrue();
            save.Result.ShouldBeTrue();
            card.Phase.ShouldBe(SmartFormFillPhase.Saved);
            card.StatusText.ShouldBe("Contact saved");
            ((Button)card.SubmitButton!).IsLoading.ShouldBeFalse();
            submitted!["name"].ShouldBe("Rin Takeda");

            card.Submit = _ => Task.FromException(new InvalidOperationException());
            card.SubmitAsync().Result.ShouldBeFalse();
            card.Phase.ShouldBe(SmartFormFillPhase.Failed);
            card.StatusText.ShouldBe("Couldn't save. Try again.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulated_Save_Takes_Its_Time()
    {
        var (card, window, timers) = Open();
        try
        {
            var save = card.SubmitAsync();
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Saving);
            timers.Advance(SmartFormFillReader.SaveTime.TotalMilliseconds);
            Pump();
            save.Result.ShouldBeTrue();
            card.Phase.ShouldBe(SmartFormFillPhase.Saved);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Layout_Follows_The_Card_Width()
    {
        var (card, window, _) = Open(width: 960);
        try
        {
            card.Classes.Contains(":stacked").ShouldBeFalse();
            Grid.GetColumn(card.FormPanel!).ShouldBe(1);
            card.FormPanel!.IsSingleColumn.ShouldBeFalse();
            var name = card.ViewOf("name")!;
            var title = card.ViewOf("title")!;
            name.Bounds.Y.ShouldBe(title.Bounds.Y, 0.5);
            title.Bounds.X.ShouldBeGreaterThan(name.Bounds.X);
            card.ViewOf("company")!.Bounds.Width.ShouldBe(card.FormPanel.Bounds.Width, 0.5);
            card.WellBorder!.Bounds.Right.ShouldBeLessThan(card.FormPanel.Bounds.X + 1);

            window.Width = 600;
            Pump();
            card.Classes.Contains(":stacked").ShouldBeTrue();
            Grid.GetColumn(card.FormPanel).ShouldBe(0);
            Grid.GetRow(card.FormPanel).ShouldBe(1);
            card.FormPanel.IsSingleColumn.ShouldBeFalse();
            card.WellBorder.Bounds.Bottom.ShouldBeLessThan(card.FormPanel.Bounds.Y + 1);

            window.Width = 400;
            Pump();
            card.FormPanel.IsSingleColumn.ShouldBeTrue();
            name.Bounds.Width.ShouldBe(card.FormPanel.Bounds.Width, 0.5);
            title.Bounds.Y.ShouldBeGreaterThan(name.Bounds.Y);
            Grid.GetRow(card.UndoButton!.Parent as Control ?? card.UndoButton).ShouldBe(1);
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(400);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Enter_Reads_The_Typed_Text_And_Fill_Reads_It_Too()
    {
        var reads = new List<string>();
        var (card, window, _) = Open(setup: c => c.Extract = (text, _) =>
        {
            reads.Add(text);
            return Task.FromResult(new SmartFormFillResult { ["email"] = new("lee@example.com", 0.95, "lee@example.com") });
        });
        try
        {
            card.Editor!.Text = "Lee Park, lee@example.com";
            Pump();
            card.Text.ShouldBe("Lee Park, lee@example.com");
            card.FillButton!.IsEnabled.ShouldBeTrue();
            card.Editor.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            Pump();
            reads.ShouldBe(["Lee Park, lee@example.com"]);
            card.Editor.Text.ShouldBe("Lee Park, lee@example.com");
            card.Values["email"].ShouldBe("lee@example.com");
            card.ShowingSource.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Try_Sample_Cycles_The_Samples_And_An_Empty_List_Hides_It()
    {
        var (card, window, timers) = Open();
        try
        {
            AutomationProperties.GetName(card.SampleButton!).ShouldBe("Try sample: Email signature");
            _ = card.TrySampleAsync();
            Pump();
            card.Text.ShouldBe(SmartFormFillFixture.Signature);
            AutomationProperties.GetName(card.SampleButton!).ShouldBe("Try sample: Messy notes");
            timers.Advance(1100);
            Pump();
            _ = card.TrySampleAsync();
            Pump();
            card.Text.ShouldBe(SmartFormFillFixture.Notes);
            timers.Advance(1100);
            Pump();
            card.Values["title"].ShouldBe("Head of Operations");
            card.ViewOf("title")!.State.ShouldBe(SmartFormFillFieldState.Review);

            card.Samples = [];
            Pump();
            card.SampleButton!.IsVisible.ShouldBeFalse();
            card.AutoplayEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Fields_Labels_And_Logo()
    {
        var (card, window, timers) = Open(setup: c =>
        {
            c.Fields.Add(new SmartFormFillField("vendor", "Vendor", SmartFormFillFieldKind.Company));
            c.Fields.Add(new SmartFormFillField("contact", "Billing email", SmartFormFillFieldKind.Email, span: SmartFormFillSpan.Full));
            c.Title = "New vendor";
            c.Description = string.Empty;
            c.SubmitLabel = "Add vendor";
            c.ResolveLogo = name => name == "Acme Tools" ? new Border { Name = "OwnLogo" } : null;
            c.Labels = new SmartFormFillLabels { Undo = "Rückgängig", Confidence = v => $"{v:P0}x" };
        });
        try
        {
            card.FieldViews.Select(v => v.Field.Id).ShouldBe(["vendor", "contact"]);
            card.SubmitButton!.Content.ShouldBe("Add vendor");
            card.UndoButton!.Content.ShouldBe("Rückgängig");
            ReadSample(card, timers, "Invoices from Acme Tools\nbilling@acme.example.com");
            card.Values["contact"].ShouldBe("billing@acme.example.com");
            card.ViewOf("contact")!.BadgeText.ShouldEndWith("x");
            card.ViewOf("vendor")!.InputPartControl!.Text = "Acme Tools";
            Pump();
            card.ViewOf("vendor")!.GetVisualDescendants().OfType<Border>().Any(b => b.Name == "OwnLogo").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Loops_While_On_Screen_And_Stops_For_Good_On_A_Press()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Full);
        try
        {
            card.AutoplayEnabled.ShouldBeTrue();
            card.SetOnScreen(true);
            card.IsAutoplaying.ShouldBeTrue();
            card.AutoplayNext.ShouldBe(SmartFormFillAutoplayStep.Paste);

            timers.Advance(SmartFormFill.AutoplayPasteDelay.TotalMilliseconds);
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Reading);
            timers.Advance(SmartFormFillReader.ReadTime.TotalMilliseconds + 140 * 6);
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Filled);
            card.AutoplayNext.ShouldBe(SmartFormFillAutoplayStep.Review);

            // Off screen the loop waits; back on screen it resumes at the same step.
            card.SetOnScreen(false);
            card.IsAutoplaying.ShouldBeFalse();
            timers.Advance(10_000);
            card.ReviewCount.ShouldBe(2);
            card.SetOnScreen(true);
            timers.Advance(600);
            Pump();
            card.ReviewCount.ShouldBe(1);
            card.AutoplayNext.ShouldBe(SmartFormFillAutoplayStep.Undo);
            timers.Advance(SmartFormFill.AutoplayUndoDelay.TotalMilliseconds);
            Pump();
            card.CanUndo.ShouldBeFalse();
            timers.Advance(SmartFormFill.AutoplayClearDelay.TotalMilliseconds);
            Pump();
            card.Text.ShouldBe(string.Empty);
            card.ShowingSource.ShouldBeFalse();
            card.AutoplayNext.ShouldBe(SmartFormFillAutoplayStep.Paste);

            card.Editor!.Focus();
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
            Pump();
            card.AutoplayStopped.ShouldBeTrue();
            card.IsAutoplaying.ShouldBeFalse();
            timers.Advance(5_000);
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Is_Off_With_An_Extractor_Or_When_Turned_Off()
    {
        var (card, window, _) = Open(ArcMotionLevel.Full, c =>
        {
            c.Extract = (_, _) => Task.FromResult(new SmartFormFillResult());
            c.Samples = SmartFormFillFixture.Samples;
        });
        try
        {
            card.AutoplayEnabled.ShouldBeFalse();
            card.AutoPlay = true;
            card.AutoplayEnabled.ShouldBeTrue();
            card.AutoPlay = false;
            card.SetOnScreen(true);
            card.IsAutoplaying.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Read_That_Finds_Nothing_Says_So()
    {
        var (card, window, timers) = Open();
        try
        {
            ReadSample(card, timers, "see you thursday");
            card.Phase.ShouldBe(SmartFormFillPhase.Idle);
            card.StatusText.ShouldBe("No contact details found in the text");
            card.CanUndo.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Local_Parser_Reads_A_Plain_Signature()
    {
        const string text = "Cheers,\nJonas Weber\nSenior Product Designer | Lumen Labs\njonas.weber@lumenlabs.example.com\n+49 30 5550 1234\n12 Mill Street, Leeds";
        var result = SmartFormFillReader.Parse(text, SmartFormFillField.ContactFields());
        result["name"].Value.ShouldBe("Jonas Weber");
        result["title"].Value.ShouldBe("Senior Product Designer");
        result["company"].Value.ShouldBe("Lumen Labs");
        result["email"].Value.ShouldBe("jonas.weber@lumenlabs.example.com");
        result["email"].Confidence.ShouldBe(0.97);
        result["phone"].Value.ShouldBe("+49 30 5550 1234");
        result["address"].Value.ShouldBe("12 Mill Street, Leeds");
        result["address"].Confidence.ShouldBeLessThan(0.8);
        foreach (var value in result.Values)
        {
            text.Contains(value.Source!, StringComparison.Ordinal).ShouldBeTrue();
        }
    }

    [Fact]
    public void The_Fixture_Samples_Have_Known_Results_With_Sources_In_Their_Text()
    {
        var samples = SmartFormFillFixture.Samples;
        foreach (var sample in samples)
        {
            var result = sample.Result!;
            SmartFormFillReader.Read(" " + sample.Text + "\n", SmartFormFillField.ContactFields(), samples).ShouldBeSameAs(result);
            result.Count.ShouldBe(6);
            foreach (var value in result.Values)
            {
                sample.Text.Contains(value.Source!, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
                value.Value.ShouldNotContain("@example.org");
            }
            result.Values.Where(v => v.Confidence < 0.8).ShouldAllBe(v => v.Note != null);
        }
        // Text without a known result goes to the plain rules.
        SmartFormFillReader.Read("jo@example.com", SmartFormFillField.ContactFields(), samples)["email"].Value.ShouldBe("jo@example.com");
        new SmartFormFillValue("x", 3).Confidence.ShouldBe(1);
        new SmartFormFillValue("x", double.NaN).Confidence.ShouldBe(0);
    }

    [AvaloniaFact]
    public void No_Extractor_And_No_Simulation_Shows_The_Empty_Form()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Full, simulate: false);
        try
        {
            card.EffectiveSamples.Count.ShouldBe(0);
            card.SampleButton!.IsVisible.ShouldBeFalse();
            card.AutoplayEnabled.ShouldBeFalse();
            card.SetOnScreen(true);
            card.IsAutoplaying.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, SmartFormFill.ModelNamePart).IsVisible.ShouldBeFalse();
            card.Values.Values.ShouldAllBe(v => v.Length == 0);
            // Text you paste is still read by the plain rules.
            ReadSample(card, timers, "Write to jo@example.com");
            card.Values["email"].ShouldBe("jo@example.com");
            card.StatusText.ShouldNotContain("Reading with");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Set_Later_Offers_Its_Samples_And_Plays()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Full, simulate: false);
        try
        {
            card.SetOnScreen(true);
            card.Simulation = Demo();
            Pump();
            card.SampleButton!.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, SmartFormFill.ModelNamePart).Text.ShouldBe("Read by " + SmartFormFillFixture.ModelName);
            card.AutoplayEnabled.ShouldBeTrue();
            card.IsAutoplaying.ShouldBeTrue();
            timers.Advance(SmartFormFill.AutoplayPasteDelay.TotalMilliseconds);
            Pump();
            card.Text.ShouldBe(SmartFormFillFixture.Signature);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Extractor_Wins_Over_The_Simulation()
    {
        var (card, window, _) = Open(setup: c =>
        {
            c.Extract = (_, _) => Task.FromResult(new SmartFormFillResult { ["name"] = new("Sam Lee", 0.9, "Sam") });
            c.AutoPlay = null;
        });
        try
        {
            card.PlayingSimulation.ShouldBeNull();
            card.EffectiveSamples.Count.ShouldBe(0);
            card.AutoplayEnabled.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, SmartFormFill.ModelNamePart).IsVisible.ShouldBeFalse();
            _ = card.ReadAsync("Sam");
            TestHelpers.PumpUntil(() => card.Values["name"] == "Sam Lee").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_Playback_And_Clears_What_It_Filled()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Full);
        try
        {
            card.SetOnScreen(true);
            timers.Advance(SmartFormFill.AutoplayPasteDelay.TotalMilliseconds);
            Pump();
            timers.Advance(SmartFormFillReader.ReadTime.TotalMilliseconds + 140 * 6);
            Pump();
            card.Phase.ShouldBe(SmartFormFillPhase.Filled);
            card.Values["name"].ShouldBe("Maya Okafor");

            card.Simulation = null;
            Pump();
            card.AutoplayEnabled.ShouldBeFalse();
            card.IsAutoplaying.ShouldBeFalse();
            card.SampleButton!.IsVisible.ShouldBeFalse();
            card.Text.ShouldBe(string.Empty);
            card.Values.Values.ShouldAllBe(v => v.Length == 0);
            timers.Advance(10000);
            Pump();
            card.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Timers for SmartFormFill tests: nothing fires until <see cref="Advance"/> moves the test clock past it.</summary>
internal sealed class SmartFormFillTestTimers : ISmartFormFillTimers
{
    private readonly List<Shot> _shots = [];

    public double Now { get; private set; }

    /// <summary>The delays asked for, in order (milliseconds).</summary>
    public List<double> Requested { get; } = [];

    public IDisposable Start(TimeSpan delay, Action callback)
    {
        Requested.Add(Math.Round(delay.TotalMilliseconds));
        var shot = new Shot(Now + delay.TotalMilliseconds, callback);
        _shots.Add(shot);
        return shot;
    }

    /// <summary>Moves the clock and fires every timer that comes due, in order (a timer started by a callback can fire too).</summary>
    public void Advance(double milliseconds)
    {
        var end = Now + milliseconds;
        while (true)
        {
            var next = _shots.Where(s => !s.Done && s.Due <= end + 0.001).OrderBy(s => s.Due).FirstOrDefault();
            if (next is null)
            {
                break;
            }
            Now = Math.Max(Now, next.Due);
            next.Fire();
            Dispatcher.UIThread.RunJobs();
        }
        Now = end;
    }

    private sealed class Shot(double due, Action callback) : IDisposable
    {
        public double Due { get; } = due;

        public bool Done { get; private set; }

        public void Fire()
        {
            if (!Done)
            {
                Done = true;
                callback();
            }
        }

        public void Dispose() => Done = true;
    }
}

/// <summary>Invented test data for the SmartFormFill tests (people, companies and numbers are made up).</summary>
file static class SmartFormFillFixture
{
    /// <summary>An email signature under a short reply.</summary>
    public const string Signature =
        "Thanks again for the walkthrough today. Happy to set up a pilot with our dispatch team next week.\n\n" +
        "Best,\n" +
        "Maya Okafor\n" +
        "Head of Partnerships | Fernbrook Freight\n" +
        "maya.okafor@example.com\n" +
        "m. +1 (415) 555-0142 · o. +1 (415) 555-0100\n" +
        "500 Harbor Way, Suite 300, San Francisco";

    /// <summary>Messy call notes.</summary>
    public const string Notes =
        "call notes 10/14 - spoke w/ daniel reyes, runs ops at bluepine studio (i think?). wants a demo for his team before Q1.\n" +
        "email: d.reyes@example.com / cell 312.555.0187\n" +
        "they're moving offices, somewhere in chicago, will confirm";

    /// <summary>The model named beside the paste area.</summary>
    public static string ModelName => "DeepSeek-V4.1-Flash-1017";

    /// <summary>The samples behind Try sample, each with its hand-checked result.</summary>
    public static IReadOnlyList<SmartFormFillSample> Samples { get; } =
    [
        new("Email signature", Signature)
        {
            Result = new SmartFormFillResult
            {
                ["name"] = new("Maya Okafor", 0.98, "Maya Okafor"),
                ["title"] = new("Head of Partnerships", 0.95, "Head of Partnerships"),
                ["company"] = new("Fernbrook Freight", 0.96, "Fernbrook Freight"),
                ["email"] = new("maya.okafor@example.com", 0.99, "maya.okafor@example.com"),
                ["phone"] = new("+1 (415) 555-0142", 0.74, "+1 (415) 555-0142", "Two numbers in the text; picked the mobile."),
                ["address"] = new("500 Harbor Way, Suite 300, San Francisco, CA", 0.68, "500 Harbor Way, Suite 300, San Francisco",
                    "The state was inferred from the city."),
            },
        },
        new("Messy notes", Notes)
        {
            Result = new SmartFormFillResult
            {
                ["name"] = new("Daniel Reyes", 0.92, "daniel reyes"),
                ["title"] = new("Head of Operations", 0.56, "runs ops", "Paraphrased from \"runs ops\"."),
                ["company"] = new("Bluepine Studio", 0.71, "bluepine studio", "The notes add \"i think?\"."),
                ["email"] = new("d.reyes@example.com", 0.99, "d.reyes@example.com"),
                ["phone"] = new("+1 312 555 0187", 0.9, "312.555.0187"),
                ["address"] = new("Chicago, IL", 0.45, "somewhere in chicago", "Only the city is known; the office is moving."),
            },
        },
    ];
}
