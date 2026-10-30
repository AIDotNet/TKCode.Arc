using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// DesignToCode: the simulation it opens on (the gallery's; or the drop zone with a generator or none), the simulated run (scan, regions, the paced
/// stream, done), stopping with Escape, an app generator (regions, linked chunks, completion, failure, cancellation),
/// file checks, the drop zone's buttons and Import, the region and code highlight link, Code / Preview, the sample loop,
/// the breakpoints, labels and the pure rules.
/// </summary>
public class DesignToCodeTests
{
    private static (DesignToCode Card, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off, Action<DesignToCode>? setup = null,
        double width = 820)
    {
        var card = new DesignToCode
        {
            Simulation = DesignToCodeTestData.Simulation(), AutoPlay = false, UsesManualTime = true,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (card, window);
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

    private static void Run(DesignToCode card, double seconds)
    {
        card.Advance(seconds);
        Pump();
    }

    private static void RunUntil(DesignToCode card, Func<bool> condition, double limit = 20)
    {
        var spent = 0.0;
        while (!condition() && spent < limit)
        {
            card.Advance(0.1);
            spent += 0.1;
            Dispatcher.UIThread.RunJobs();
        }
        Pump();
        condition().ShouldBeTrue();
    }

    /// <summary>The gallery simulation's whole code.</summary>
    private static string Code => string.Concat(DesignToCodeTestData.Parts.Select(p => p.Text));

    private static int Words(string text) => text.Split((char[])[' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    [AvaloniaFact]
    public void Opens_On_The_Sample_Design()
    {
        var (card, window) = Open();
        try
        {
            card.Phase.ShouldBe(DesignToCodePhase.Ready);
            card.EffectiveSource.ShouldBeSameAs(DesignToCodeTestData.Source);
            card.Source.ShouldBeNull();
            card.Canvas!.IsVisible.ShouldBeTrue();
            card.DropZone!.IsVisible.ShouldBeFalse();
            card.MainButton!.Content.ShouldBe("Generate code");
            card.MainButton.IsEnabled.ShouldBeTrue();
            card.StatusParts.Label.ShouldBe("Ready");
            card.StatusParts.File.ShouldBe("pricing-card.png");
            card.ModelNameText!.Text.ShouldBe(DesignToCodeTestData.Model.Name);
            card.Code.ShouldBeEmpty();
            card.CopyButton!.IsEnabled.ShouldBeFalse();
            card.View.ShouldBe(DesignToCodeView.Code);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Generator_Without_A_Source_Opens_On_The_Drop_Zone()
    {
        var (card, window) = Open(setup: c => c.Generator = (_, _) => Task.CompletedTask);
        try
        {
            card.Phase.ShouldBe(DesignToCodePhase.Empty);
            card.EffectiveSource.ShouldBeNull();
            card.DropZone!.IsVisible.ShouldBeTrue();
            card.Canvas!.IsVisible.ShouldBeFalse();
            card.MainButton!.IsEnabled.ShouldBeFalse();
            card.StatusParts.Label.ShouldBe("Add a design to start");
            card.EffectiveAutoPlay.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Null_Source_Opens_On_The_Drop_Zone_Even_Without_A_Generator()
    {
        var (card, window) = Open(setup: c => c.Source = null);
        try
        {
            card.Phase.ShouldBe(DesignToCodePhase.Empty);
            card.DropZone!.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulated_Run_Analyzes_Writes_And_Completes()
    {
        var (card, window) = Open();
        DesignToCodeCompletedEventArgs? completed = null;
        card.Completed += (_, e) => completed = e;
        try
        {
            card.Generate();
            card.Phase.ShouldBe(DesignToCodePhase.Analyzing);
            card.IsSimulated.ShouldBeTrue();
            card.Regions.Count.ShouldBe(5);
            card.MainButton!.Content.ShouldBe("Stop");
            card.LastAnnouncement!.ShouldContain("nothing was uploaded or opened");
            // Motion off: no scan line, every region at once.
            card.Canvas!.ScanPosition.ShouldBe(double.NaN);
            card.Canvas.Views.ShouldAllBe(v => v.IsRevealed);

            Run(card, 1.1);
            card.Phase.ShouldBe(DesignToCodePhase.Writing);
            card.LastAnnouncement.ShouldBe("Found 5 regions: Header, Billing toggle, Price, Features, Button. Writing code.");
            card.StatusParts.Label.ShouldBe("Writing");
            card.StatusParts.File.ShouldBe("PricingCard.tsx");
            card.StatusParts.Meta.ShouldBe("5 regions");

            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            card.Code.ShouldBe(Code);
            card.CodeView!.Code.ShouldBe(Code);
            completed.ShouldNotBeNull();
            completed.Code.ShouldBe(Code);
            completed.LineCount.ShouldBe(DesignToCodeFormat.LineCount(Code));
            completed.Design.ShouldBeSameAs(DesignToCodeTestData.Source);
            completed.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(1));
            card.MainButton.Content.ShouldBe("Regenerate");
            card.StatusParts.Label.ShouldBe("Done");
            card.StatusParts.Meta!.ShouldStartWith(completed.LineCount + " lines in ");
            card.LastAnnouncement!.ShouldStartWith("Done. " + completed.LineCount + " lines in ");
            card.CopyButton!.Value.ShouldBe(Code);
            card.CopyButton.IsEnabled.ShouldBeTrue();
            card.Canvas.Active.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Writes_A_Part_At_A_Time_Without_Fades()
    {
        var (card, window) = Open(ArcMotionLevel.Reduced);
        try
        {
            card.Generate();
            card.Canvas!.Views.ShouldAllBe(v => v.IsRevealed);
            RunUntil(card, () => card.Phase == DesignToCodePhase.Writing && card.Code.Length > 0, 3);
            // The first part (the component's frame) arrives whole.
            card.Code.ShouldBe(DesignToCodeTestData.Parts[0].Text);
            card.CodeView!.Fades.ShouldBeEmpty();
            RunUntil(card, () => card.Code.Length > DesignToCodeTestData.Parts[0].Text.Length, 3);
            card.Code.ShouldBe(DesignToCodeTestData.Parts[0].Text + DesignToCodeTestData.Parts[1].Text);
            card.Canvas.Active.ShouldBe("header");
            card.CodeView.WritingRegion.ShouldBe("header");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Scans_Regions_In_And_Streams_Words_With_Fades()
    {
        var (card, window) = Open(ArcMotionLevel.Full);
        try
        {
            card.Generate();
            Run(card, 0.2);
            var scan = card.Canvas!.ScanPosition;
            scan.ShouldBeInRange(0.15, 0.25);
            card.Canvas.Views.Single(v => v.Region.Id == "header").IsRevealed.ShouldBeTrue();
            card.Canvas.Views.Single(v => v.Region.Id == "billing").IsRevealed.ShouldBeFalse();
            Run(card, 0.5);
            card.Canvas.Views.Single(v => v.Region.Id == "features").IsRevealed.ShouldBeTrue();
            card.Canvas.Views.Single(v => v.Region.Id == "cta").IsRevealed.ShouldBeFalse();

            RunUntil(card, () => card.Code.Length > 0, 2);
            card.Phase.ShouldBe(DesignToCodePhase.Writing);
            card.Canvas.ScanPosition.ShouldBe(double.NaN);
            card.Canvas.Views.ShouldAllBe(v => v.IsRevealed);
            // Batches of whole words, at most 8 at a time at reading speed.
            var before = card.Code;
            card.Advance(0.05);
            Words(card.Code[before.Length..]).ShouldBeLessThanOrEqualTo(8);
            card.CodeView!.Fades.ShouldNotBeEmpty();
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            card.Code.ShouldBe(Code);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Stops_The_Run_And_Keeps_The_Partial_Code()
    {
        var (card, window) = Open();
        try
        {
            card.Generate();
            Run(card, 2.0);
            card.Phase.ShouldBe(DesignToCodePhase.Writing);
            var partial = card.Code;
            partial.ShouldNotBeEmpty();
            card.MainButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            card.Phase.ShouldBe(DesignToCodePhase.Stopped);
            card.Code.ShouldBe(partial);
            card.MainButton.Content.ShouldBe("Regenerate");
            card.StatusParts.Label.ShouldBe("Stopped");
            card.StatusParts.Meta.ShouldBe("Partial code kept");
            card.LastAnnouncement.ShouldBe("Stopped. The partial code is kept.");
            Run(card, 3);
            card.Code.ShouldBe(partial);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Main_Button_Stops_And_Regenerates()
    {
        var (card, window) = Open();
        try
        {
            card.MainButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.Phase.ShouldBe(DesignToCodePhase.Analyzing);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.Phase.ShouldBe(DesignToCodePhase.Stopped);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_App_Generator_Adds_Regions_And_Links_Its_Chunks()
    {
        var finish = new TaskCompletionSource();
        DesignToCodeContext? context = null;
        DesignToCodeSource? given = null;
        var source = new DesignToCodeSource("signup.png", null, DesignToCodeSourceKind.File);
        var (card, window) = Open(setup: c =>
        {
            c.Source = source;
            c.Generator = (s, ctx) =>
            {
                given = s;
                context = ctx;
                return finish.Task;
            };
        });
        try
        {
            card.Generate();
            given.ShouldBeSameAs(source);
            card.IsSimulated.ShouldBeFalse();
            context!.AddRegion(new DesignToCodeRegion("form", "Form", 10, 20, 80, 50));
            context.AddRegion(new DesignToCodeRegion("submit", "Submit", 10, 75, 80, 10));
            card.Regions.Select(r => r.Id).ShouldBe(["form", "submit"]);
            context.Write("export function Signup() {\n");
            context.Write("  <form>\n", "form");
            context.Write("  <button>Sign up</button>\n", "submit");
            context.Write("}\n");
            Run(card, 1.2);
            card.Phase.ShouldBe(DesignToCodePhase.Writing);
            finish.SetResult();
            await Task.Yield();
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done, 5);
            card.Code.ShouldBe("export function Signup() {\n  <form>\n  <button>Sign up</button>\n}\n");
            card.CodeView!.LineRegions.ShouldBe([null, "form", "submit", null]);
            card.CodeView.LinesOf("submit").ShouldBe((2, 2));
            card.RegionAt(0).ShouldBeNull();
            card.RegionAt(28).ShouldBe("form");
            card.Canvas!.Views.ShouldAllBe(v => v.IsRevealed);
            // No renderer and not the sample: the preview says so.
            card.View = DesignToCodeView.Preview;
            Pump();
            card.PreviewContent.ShouldNotBeOfType<DesignToCodeTestPreview>();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Failing_Generator_Shows_The_Problem_And_The_Fix()
    {
        var fail = new TaskCompletionSource();
        var (card, window) = Open(setup: c =>
        {
            c.Source = DesignToCodeTestData.Source;
            c.Generator = async (_, ctx) =>
            {
                ctx.Write("const a = 1;\n");
                await fail.Task;
            };
        });
        try
        {
            card.Generate();
            Run(card, 1.2);
            fail.SetException(new InvalidOperationException("the model timed out"));
            await Task.Yield();
            Pump();
            card.Phase.ShouldBe(DesignToCodePhase.Error);
            card.ErrorMessage.ShouldBe("Generation failed: the model timed out. Try again.");
            card.StatusParts.Label.ShouldBe(card.ErrorMessage);
            card.MainButton!.Content.ShouldBe("Try again");
            card.LastAnnouncement.ShouldBe(card.ErrorMessage);
            card.Generator = (_, ctx) => Task.Delay(Timeout.Infinite, ctx.CancellationToken);
            card.Generate();
            card.ErrorMessage.ShouldBeNull();
            card.Phase.ShouldBe(DesignToCodePhase.Analyzing);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stopping_Cancels_The_Generator_And_Ignores_Late_Writes()
    {
        DesignToCodeContext? context = null;
        var (card, window) = Open(setup: c =>
        {
            c.Source = DesignToCodeTestData.Source;
            c.Generator = (_, ctx) =>
            {
                context = ctx;
                return Task.Delay(Timeout.Infinite, ctx.CancellationToken);
            };
        });
        try
        {
            card.Generate();
            context!.CancellationToken.IsCancellationRequested.ShouldBeFalse();
            card.Stop();
            context.CancellationToken.IsCancellationRequested.ShouldBeTrue();
            context.Write("late");
            context.AddRegion(new DesignToCodeRegion("late", "Late", 0, 0, 10, 10));
            Run(card, 2);
            card.Code.ShouldBeEmpty();
            card.Regions.ShouldBeEmpty();
            card.Phase.ShouldBe(DesignToCodePhase.Stopped);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focusing_A_Region_Tag_Links_The_Box_And_Its_Lines()
    {
        var (card, window) = Open();
        try
        {
            card.Generate();
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            var price = card.Canvas!.Views.Single(v => v.Region.Id == "price").Tag;
            Avalonia.Automation.AutomationProperties.GetName(price).ShouldBe("Price: show its code");
            price.Focus(NavigationMethod.Tab);
            Pump();
            card.HighlightedRegion.ShouldBe("price");
            card.Canvas.Highlighted.ShouldBe("price");
            card.Canvas.IsHighlighted("price").ShouldBeTrue();
            card.CodeView!.HighlightedRegion.ShouldBe("price");
            price.Classes.Contains(DesignToCodeRegionTag.HighlightedPseudoClass).ShouldBeTrue();
            var lines = card.CodeView.LinesOf("price");
            lines.ShouldNotBeNull();
            (lines.Value.Last - lines.Value.First).ShouldBe(3);
            card.MainButton!.Focus(NavigationMethod.Tab);
            Pump();
            card.HighlightedRegion.ShouldBeNull();
            card.CodeView.HighlightedRegion.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Canvas_Finds_The_Smallest_Region_Under_A_Point()
    {
        var (card, window) = Open();
        try
        {
            card.Generate();
            Pump();
            var canvas = card.Canvas!;
            var price = canvas.BoxOf(canvas.Views.Single(v => v.Region.Id == "price").Region);
            price.Width.ShouldBeGreaterThan(0);
            canvas.RegionAt(price.Center).ShouldBe("price");
            canvas.RegionAt(new Point(1, 1)).ShouldBeNull();
            canvas.ShotRect.Width.ShouldBeLessThanOrEqualTo(canvas.Bounds.Width);
            (canvas.ShotRect.Width / canvas.ShotRect.Height).ShouldBe(0.8, 0.02);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_No_Generator_And_No_Simulation_It_Opens_On_The_Drop_Zone()
    {
        var (card, window) = Open(setup: c => c.Simulation = null);
        try
        {
            card.EffectiveSource.ShouldBeNull();
            card.Phase.ShouldBe(DesignToCodePhase.Empty);
            card.DropZone!.IsVisible.ShouldBeTrue();
            card.UseSampleButton!.IsVisible.ShouldBeFalse();
            card.ModelNameText!.Text.ShouldBeNullOrEmpty();
            card.UseSample();
            card.EffectiveSource.ShouldBeNull();
            card.Source = new DesignToCodeSource("shot.png");
            Pump();
            card.Generate();
            card.Phase.ShouldBe(DesignToCodePhase.Ready);
            card.IsRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_App_Generator_Wins_Over_The_Simulation()
    {
        var calls = 0;
        var (card, window) = Open(setup: c => c.Generator = (_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });
        try
        {
            card.EffectiveSource.ShouldBeNull();
            card.Source = DesignToCodeTestData.Source;
            Pump();
            card.Generate();
            Run(card, 0.1);
            calls.ShouldBe(1);
            card.IsSimulated.ShouldBeFalse();
            card.ModelNameText!.Text.ShouldBeNullOrEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_Simulation_To_Null_Takes_Its_Design_Away()
    {
        var (card, window) = Open();
        try
        {
            card.EffectiveSource.ShouldBeSameAs(DesignToCodeTestData.Source);
            card.Simulation = null;
            Pump();
            card.EffectiveSource.ShouldBeNull();
            card.Phase.ShouldBe(DesignToCodePhase.Empty);
            card.IsRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Code_And_Preview_Switch_And_A_New_Run_Returns_To_Code()
    {
        var (card, window) = Open();
        try
        {
            card.View = DesignToCodeView.Preview;
            Pump();
            card.Layers!.ShowsPreview.ShouldBeTrue();
            card.PreviewContent.ShouldNotBeOfType<DesignToCodeTestPreview>();
            card.View = DesignToCodeView.Code;
            card.Generate();
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            card.ViewToggle!.SelectedValue = DesignToCodeView.Preview;
            Pump();
            card.View.ShouldBe(DesignToCodeView.Preview);
            card.Layers.ShowsPreview.ShouldBeTrue();
            var preview = card.PreviewContent.ShouldBeOfType<DesignToCodeTestPreview>();
            preview.ShownPrice.ShouldBe(24);
            preview.IsYearly = true;
            preview.ShownPrice.ShouldBe(19);
            card.Generate();
            card.View.ShouldBe(DesignToCodeView.Code);
            card.Layers.ShowsPreview.ShouldBeFalse();
            ((DesignToCodeView)card.ViewToggle.SelectedValue!).ShouldBe(DesignToCodeView.Code);
            // The new run renders a fresh preview, back on the monthly price.
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            card.View = DesignToCodeView.Preview;
            Pump();
            var next = card.PreviewContent.ShouldBeOfType<DesignToCodeTestPreview>();
            next.ShouldNotBeSameAs(preview);
            next.IsYearly.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Preview_Factory_Renders_The_Finished_Code()
    {
        string? rendered = null;
        var (card, window) = Open(setup: c => c.PreviewFactory = (code, _) =>
        {
            rendered = code;
            return new TextBlock { Text = "rendered" };
        });
        try
        {
            card.Generate();
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            card.View = DesignToCodeView.Preview;
            Pump();
            rendered.ShouldBe(Code);
            card.PreviewContent.ShouldBeOfType<TextBlock>().Text.ShouldBe("rendered");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Remove_Choose_File_Use_Sample_And_Import()
    {
        var (card, window) = Open();
        try
        {
            card.RemoveButton!.IsVisible.ShouldBeTrue();
            card.RemoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            card.Phase.ShouldBe(DesignToCodePhase.Empty);
            card.DropZone!.IsVisible.ShouldBeTrue();
            card.MainButton!.IsEnabled.ShouldBeFalse();

            // Nobody handles Choose file: the sample stands in and says nothing was opened.
            card.ChooseFileButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            card.Phase.ShouldBe(DesignToCodePhase.Ready);
            card.Source.ShouldBeSameAs(DesignToCodeTestData.Source);
            card.LastAnnouncement!.ShouldContain("nothing was uploaded or opened");

            card.RemoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var asked = 0;
            card.ChooseFileRequested += (_, e) =>
            {
                asked++;
                e.Handled = true;
            };
            card.ChooseFileButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            asked.ShouldBe(1);
            card.Phase.ShouldBe(DesignToCodePhase.Empty);

            card.UseSampleButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            card.Source.ShouldBeSameAs(DesignToCodeTestData.Source);

            card.ImportButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            card.Source!.Kind.ShouldBe(DesignToCodeSourceKind.DesignFile);
            card.Source.Name.ShouldBe("Pricing / Pro card");
            card.Source.Image.ShouldBeNull();
            card.Phase.ShouldBe(DesignToCodePhase.Ready);
            card.LastAnnouncement.ShouldBe("Pricing / Pro card added. This is a sample: nothing was uploaded or opened.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_App_Importer_Brings_In_Its_Frame()
    {
        var frame = new DesignToCodeSource("Checkout / Summary", null, DesignToCodeSourceKind.DesignFile);
        var (card, window) = Open(setup: c => c.DesignFileImporter = _ => Task.FromResult<DesignToCodeSource?>(frame));
        try
        {
            await card.ImportDesignFileAsync();
            card.Source.ShouldBeSameAs(frame);
            card.DesignFileImporter = _ => throw new InvalidOperationException("no access");
            await card.ImportDesignFileAsync();
            card.ErrorMessage.ShouldBe("Import failed: no access. Try again.");
            card.Source.ShouldBeSameAs(frame);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rejected_Files_Show_An_Error_Instead_Of_A_Source()
    {
        var (card, window) = Open(setup: c => c.Source = null);
        try
        {
            card.CheckFile("shot.png", 1000).ShouldBeNull();
            card.CheckFile("shot.gif", null).ShouldBe("That file type isn't supported. Choose a PNG, JPEG or WebP image.");
            card.CheckFile("big.webp", 11L * 1024 * 1024).ShouldBe("That file is over 10 MB. Choose a smaller image.");
            card.Reject(card.CheckFile("shot.gif", null)!);
            Pump();
            card.ErrorMessage.ShouldNotBeNull();
            card.DropError!.IsVisible.ShouldBeTrue();
            card.DropError.Text.ShouldBe(card.ErrorMessage);
            card.StatusParts.Label.ShouldBe(card.ErrorMessage);
            card.Phase.ShouldBe(DesignToCodePhase.Empty);
            card.UseSample();
            card.ErrorMessage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Shows_The_Cover()
    {
        var (card, window) = Open();
        try
        {
            card.SetDragging(true);
            card.IsDragging.ShouldBeTrue();
            card.Classes.Contains(DesignToCode.DraggingPseudoClass).ShouldBeTrue();
            card.SetDragging(false);
            card.IsDragging.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Layout_Follows_The_Cards_Width()
    {
        var (card, window) = Open(width: 820);
        try
        {
            card.IsStackedLayout.ShouldBeFalse();
            var sideBySide = card.Bounds.Height;
            card.Canvas!.Bounds.Width.ShouldBe(344, 1);
            window.Width = 600;
            Pump();
            card.IsStackedLayout.ShouldBeTrue();
            card.IsCompactLayout.ShouldBeFalse();
            (card.Bounds.Height - sideBySide).ShouldBe(352, 1);
            card.Canvas.Bounds.Width.ShouldBe(600, 2);
            window.Width = 500;
            Pump();
            card.IsCompactLayout.ShouldBeTrue();
            card.StatusParts.File.ShouldBeNull();
            card.ImportButton!.Content.ShouldBeOfType<StackPanel>().Children.Count.ShouldBe(1);
            Avalonia.Automation.AutomationProperties.GetName(card.ImportButton).ShouldBe("Import design file");
            card.ModelNameText!.IsVisible.ShouldBeTrue();
            window.Width = 400;
            Pump();
            card.IsNarrowLayout.ShouldBeTrue();
            card.ModelNameText.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Width_Does_Not_Follow_The_Streaming_Code()
    {
        var (card, window) = Open(width: 820);
        try
        {
            var width = card.Bounds.Width;
            var height = card.Bounds.Height;
            card.Generate();
            Run(card, 1.5);
            card.Bounds.Width.ShouldBe(width);
            card.Bounds.Height.ShouldBe(height);
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            card.Bounds.Width.ShouldBe(width);
            card.Bounds.Height.ShouldBe(height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Sample_Loop_Plays_Once_Without_Full_Motion_And_Stops_At_A_Key_Press()
    {
        var (card, window) = Open(setup: c => c.AutoPlay = null);
        try
        {
            card.EffectiveAutoPlay.ShouldBeTrue();
            Run(card, 0.05);
            card.Phase.ShouldBe(DesignToCodePhase.Analyzing);
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            Run(card, DesignToCode.HoldCodeSeconds + DesignToCode.HoldPreviewSeconds + 1);
            card.Phase.ShouldBe(DesignToCodePhase.Done);
            card.View.ShouldBe(DesignToCodeView.Code);

            card.MainButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump();
            card.IsAutoPlayStopped.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Sample_Loop_Shows_The_Preview_And_Starts_Again_Under_Full_Motion()
    {
        var (card, window) = Open(ArcMotionLevel.Full, c => c.AutoPlay = true);
        try
        {
            Run(card, 0.05);
            card.Phase.ShouldBe(DesignToCodePhase.Analyzing);
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            Run(card, DesignToCode.HoldCodeSeconds + 0.1);
            card.View.ShouldBe(DesignToCodeView.Preview);
            Run(card, DesignToCode.PreviewActionSeconds + 0.1);
            card.PreviewContent.ShouldBeOfType<DesignToCodeTestPreview>().IsYearly.ShouldBeTrue();
            Run(card, DesignToCode.HoldPreviewSeconds - DesignToCode.PreviewActionSeconds);
            card.Phase.ShouldBe(DesignToCodePhase.Analyzing);
            card.View.ShouldBe(DesignToCodeView.Code);

            // A pointer press inside the card stops the loop; the run in progress finishes.
            var point = card.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();
            card.IsAutoPlayStopped.ShouldBeTrue();
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            Run(card, DesignToCode.HoldCodeSeconds + 1);
            card.View.ShouldBe(DesignToCodeView.Code);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_The_Model_Are_Used()
    {
        var (card, window) = Open(setup: c =>
        {
            c.Labels = new DesignToCodeLabels { Generate = "Build it", StatusReady = "Set", Code = "Source" };
            c.Model = new AgentModel("Your model", ArcIcons.Eye);
            c.FileName = "Hero.tsx";
        });
        try
        {
            card.MainButton!.Content.ShouldBe("Build it");
            card.StatusParts.Label.ShouldBe("Set");
            card.ModelNameText!.Text.ShouldBe("Your model");
            card.ViewToggle!.Items.Cast<SegmentedOption>().First().Label.ShouldBe("Source");
            Avalonia.Automation.AutomationProperties.GetName(card.CopyButton!).ShouldBe("Copy Hero.tsx");
            Avalonia.Automation.AutomationProperties.GetName(card.CodeScroll!).ShouldBe("Generated code, Hero.tsx");
            Avalonia.Automation.AutomationProperties.GetName(card).ShouldBe("Design to code");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Code_Area_Is_Busy_While_Writing()
    {
        var (card, window) = Open();
        try
        {
            card.Generate();
            Run(card, 1.2);
            Avalonia.Automation.AutomationProperties.GetItemStatus(card.CodeScroll!).ShouldBe("Writing");
            RunUntil(card, () => card.Phase == DesignToCodePhase.Done);
            Avalonia.Automation.AutomationProperties.GetItemStatus(card.CodeScroll!).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Accept_Lists_Read_Like_The_Input_Attribute()
    {
        DesignToCodeFormat.Accepts("image/png,image/jpeg,image/webp", "a.PNG").ShouldBeTrue();
        DesignToCodeFormat.Accepts("image/png,image/jpeg,image/webp", "a.jpg").ShouldBeTrue();
        DesignToCodeFormat.Accepts("image/png,image/jpeg,image/webp", "a.gif").ShouldBeFalse();
        DesignToCodeFormat.Accepts("image/*", "a.gif").ShouldBeTrue();
        DesignToCodeFormat.Accepts(".fig, .png", "a.fig").ShouldBeTrue();
        DesignToCodeFormat.Accepts("", "anything.bin").ShouldBeTrue();
        DesignToCodeFormat.TypesText("image/png,image/jpeg,image/webp").ShouldBe("PNG, JPEG or WebP");
        DesignToCodeFormat.TypesText("image/png").ShouldBe("PNG");
        DesignToCodeFormat.TypesText(".png,.jpg").ShouldBe("PNG or JPEG");
        DesignToCodeFormat.SizeText(10 * 1024 * 1024).ShouldBe("10 MB");
        DesignToCodeFormat.SizeText(1536 * 1024).ShouldBe("1.5 MB");
        DesignToCodeFormat.SizeText(500 * 1024).ShouldBe("500 KB");
        DesignToCodeFormat.LineCount("").ShouldBe(0);
        DesignToCodeFormat.LineCount("a").ShouldBe(1);
        DesignToCodeFormat.LineCount("a\nb\n").ShouldBe(2);
        DesignToCodeFormat.LineCount("a\nb").ShouldBe(2);
    }

    [Fact]
    public void The_Sample_Code_Covers_Every_Region_In_Order()
    {
        DesignToCodeTestData.Parts.Where(p => p.RegionId is not null).Select(p => p.RegionId)
            .ShouldBe(DesignToCodeTestData.Regions.Select(r => r.Id));
        DesignToCodeTestData.Parts.ShouldAllBe(p => p.Text.EndsWith('\n'));
        foreach (var region in DesignToCodeTestData.Regions)
        {
            (region.Y + region.Height).ShouldBeLessThanOrEqualTo(100);
            (region.X + region.Width).ShouldBeLessThanOrEqualTo(100);
        }
    }

    [Fact]
    public void Labels_Format_Their_Sentences()
    {
        var labels = DesignToCodeLabels.Default;
        labels.Regions(1).ShouldBe("1 region");
        labels.Regions(5).ShouldBe("5 regions");
        labels.DoneMeta(52, TimeSpan.FromSeconds(3.44)).ShouldBe("52 lines in 3.4s");
        labels.Failed("").ShouldBe("Generation failed. Try again.");
        labels.AnnounceRegions([]).ShouldBe("Writing code.");
        labels.AnnounceRegions(["Price"]).ShouldBe("Found 1 region: Price. Writing code.");
        labels.DropHint("PNG", "10 MB").ShouldBe("PNG, up to 10 MB");
    }

    [Fact]
    public void Regions_Clamp_To_The_Design()
    {
        var region = new DesignToCodeRegion("a", "A", 90, -5, 30, 120);
        var f = region.Fraction;
        f.X.ShouldBe(0.9, 1e-9);
        f.Y.ShouldBe(0, 1e-9);
        f.Width.ShouldBe(0.1, 1e-9);
        f.Height.ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void The_Main_Button_Follows_Its_Label_On_The_Quick_Spring()
    {
        var (card, window) = Open();
        try
        {
            var main = card.MainButton.ShouldBeOfType<Button>();
            main.LabelSpring.ShouldBe(ArcSpring.Quick);
            main.LabelSwapMode.ShouldBe(TKCode.Arc.Primitives.ContentSwapMode.BlurSwap);
            TestHelpers.Descendant<TKCode.Arc.Primitives.SpringSizeDecorator>(main, "PART_LabelSlot").Spring.ShouldBe(ArcSpring.Quick);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>
/// The invented fixture these tests play: a copy of the gallery's DesignToCode demo data with invented model names, so
/// the tests do not depend on the gallery's content.
/// </summary>
internal static class DesignToCodeTestData
{
    /// <summary>The drawn card's natural size (it scales to fit the design pane).</summary>
    public const double Width = 240;

    public const double Height = 300;

    /// <summary>The card's words, shared by the drawing and the live preview.</summary>
    public const string Badge = "Pro";

    public const string Title = "Pro plan";

    public const string Subtitle = "For growing teams";

    public const string Monthly = "Monthly";

    public const string Yearly = "Yearly";

    public const int MonthlyPrice = 24;

    public const int YearlyPrice = 19;

    public const string PerMonth = "/month";

    public const string Cta = "Start free trial";

    public static readonly string[] Features = ["Unlimited projects", "Shared workspaces", "Priority support"];

    /// <summary>The design: the drawn pricing card.</summary>
    public static DesignToCodeSource Source { get; } = new("pricing-card.png", null, DesignToCodeSourceKind.Sample)
    {
        Sketch = Sketch,
        SketchSize = new Size(Width, Height),
    };

    /// <summary>The model credited in the code header: a real model that reads images (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new("Model A", ArcIcons.Sparkles);

    /// <summary>The file the code is written to.</summary>
    public const string FileName = "PricingCard.tsx";

    /// <summary>The frame Import design file brings in.</summary>
    public const string FrameName = "Pricing / Pro card";

    /// <summary>The five regions, in percent of the drawing (240 × 300 units).</summary>
    public static IList<DesignToCodeRegion> Regions { get; } =
    [
        new("header", "Header", 7.5, 6, 85, 21.5),
        new("billing", "Billing toggle", 7.5, 30.5, 85, 9.5),
        new("price", "Price", 7.5, 42.5, 52, 14.5),
        new("features", "Features", 7.5, 59, 85, 22),
        new("cta", "Button", 7.5, 83, 85, 12),
    ];

    /// <summary>The code, part by part, each tagged with the region it implements (null for the component's frame).</summary>
    public static IList<DesignToCodeSimulationPart> Parts { get; } =
    [
        new("""
         import { useState } from "react";
         import { Check } from "./icons";

         const features = ["Unlimited projects", "Shared workspaces", "Priority support"];

         export function PricingCard() {
           const [yearly, setYearly] = useState(false);
           const price = yearly ? 19 : 24;

           return (
             <section className="w-full max-w-sm rounded-2xl border bg-white p-6 shadow-sm">

         """, null),
        new("""
               <header className="space-y-1">
                 <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-600">
                   Pro
                 </span>
                 <h2 className="text-lg font-semibold">Pro plan</h2>
                 <p className="text-sm text-gray-500">For growing teams</p>
               </header>

         """, "header"),
        new("""
               <div role="radiogroup" className="mt-4 grid grid-cols-2 rounded-full bg-gray-100 p-1 text-sm">
                 {["Monthly", "Yearly"].map((label, i) => (
                   <button
                     key={label}
                     role="radio"
                     aria-checked={yearly === (i === 1)}
                     onClick={() => setYearly(i === 1)}
                     className="rounded-full py-1.5 aria-checked:bg-white aria-checked:shadow-sm"
                   >
                     {label}
                   </button>
                 ))}
               </div>

         """, "billing"),
        new("""
               <p className="mt-5 flex items-baseline gap-1">
                 <span className="text-4xl font-semibold tabular-nums">${price}</span>
                 <span className="text-sm text-gray-500">/month</span>
               </p>

         """, "price"),
        new("""
               <ul className="mt-5 space-y-2 text-sm">
                 {features.map(feature => (
                   <li key={feature} className="flex items-center gap-2">
                     <Check className="size-4 text-indigo-600" />
                     {feature}
                   </li>
                 ))}
               </ul>

         """, "features"),
        new("""
               <button className="mt-6 w-full rounded-xl bg-indigo-600 py-2.5 text-sm font-medium text-white">
                 Start free trial
               </button>

         """, "cta"),
        new("""
             </section>
           );
         }

         """, null),
    ];

    /// <summary>Renders the finished code's preview: the pricing card as working controls.</summary>
    public static Func<string, DesignToCodeSource, Control?> Preview { get; } = (_, _) => new DesignToCodeTestPreview();

    /// <summary>What the loop does on the preview: it flips the billing switch to Yearly, so the preview visibly works.</summary>
    public static Action<Control> FlipBilling { get; } = control =>
    {
        if (control is DesignToCodeTestPreview card)
        {
            card.IsYearly = true;
        }
    };

    /// <summary>A new simulation of the design (tests and code use it; the XAML demos spell it out).</summary>
    public static DesignToCodeSimulation Simulation() => new()
    {
        Source = Source,
        Regions = Regions,
        Parts = Parts,
        Model = Model,
        FileName = FileName,
        FrameName = FrameName,
        PreviewFactory = Preview,
        PreviewAction = FlipBilling,
    };

    /// <summary>Draws the pricing card on a 240 × 300 grid scaled to the frame, in the card's theme colors.</summary>
    public static void Sketch(DrawingContext context, Rect bounds, DesignToCodeSketchPalette palette)
    {
        var s = bounds.Width / Width;
        var radius = palette.CornerRadius;
        Rect R(double x, double y, double w, double h) => new(bounds.X + x * s, bounds.Y + y * s, w * s, h * s);

        var line = palette.Line is { } lineBrush ? new Pen(lineBrush) : null;
        context.DrawRectangle(palette.Paper, null, new RoundedRect(bounds, radius));
        if (line is not null)
        {
            context.DrawRectangle(null, line, new RoundedRect(bounds.Deflate(0.5), radius));
        }

        // Header: badge, title and subtitle.
        var badge = R(20, 20, 34, 16);
        context.DrawRectangle(palette.AccentSubtle, null, new RoundedRect(badge, badge.Height / 2));
        DrawText(context, palette.FontFamily, s, Badge, 9, FontWeight.SemiBold, palette.Accent, badge, center: true);
        DrawText(context, palette.FontFamily, s, Title, 17, FontWeight.SemiBold, palette.Ink, R(20, 42, 200, 22));
        DrawText(context, palette.FontFamily, s, Subtitle, 10.5, FontWeight.Normal, palette.MutedInk, R(20, 66, 200, 14));

        // Billing toggle: a well with a raised thumb on Monthly.
        var well = R(20, 94, 200, 24);
        context.DrawRectangle(palette.Well, null, new RoundedRect(well, well.Height / 2));
        var thumb = R(22, 96, 98, 20);
        context.DrawRectangle(palette.Paper, line, new RoundedRect(thumb, thumb.Height / 2));
        DrawText(context, palette.FontFamily, s, Monthly, 10, FontWeight.Medium, palette.Ink, thumb, center: true);
        DrawText(context, palette.FontFamily, s, Yearly, 10, FontWeight.Medium, palette.MutedInk, R(120, 96, 98, 20), center: true);

        // Price.
        var price = DrawText(context, palette.FontFamily, s, "$" + MonthlyPrice, 30, FontWeight.SemiBold, palette.Ink, R(20, 128, 120, 38));
        DrawText(context, palette.FontFamily, s, PerMonth, 10.5, FontWeight.Normal, palette.MutedInk,
            new Rect(bounds.X + 20 * s + price + 4 * s, bounds.Y + 147 * s, 80 * s, 14 * s));

        // Features: a check dot and a line of text each.
        for (var i = 0; i < Features.Length; i++)
        {
            var y = 184 + i * 20;
            var dot = R(20, y + 2, 10, 10);
            context.DrawEllipse(palette.AccentSubtle, null, dot);
            context.DrawEllipse(palette.Accent, null, dot.Center, 2.2 * s, 2.2 * s);
            DrawText(context, palette.FontFamily, s, Features[i], 10.5, FontWeight.Normal, palette.Ink, R(38, y, 180, 14));
        }

        // Call to action.
        var cta = R(20, 252, 200, 30);
        context.DrawRectangle(palette.Accent, null, new RoundedRect(cta, 9 * s));
        DrawText(context, palette.FontFamily, s, Cta, 11, FontWeight.SemiBold, palette.AccentForeground, cta, center: true);
    }

    /// <summary>Draws one line of text in <paramref name="box"/> (vertically centered) and returns its width.</summary>
    private static double DrawText(DrawingContext context, FontFamily font, double s, string text, double size, FontWeight weight, IBrush? brush, Rect box, bool center = false)
    {
        using var layout = new TextLayout(text, new Typeface(font, FontStyle.Normal, weight), Math.Max(1, size * s), brush ?? Brushes.Black);
        var x = center ? box.X + (box.Width - layout.Width) / 2 : box.X;
        var y = box.Y + (box.Height - layout.Height) / 2;
        layout.Draw(context, new Point(x, y));
        return layout.Width;
    }
}

/// <summary>The fixture's preview: remembers the billing choice the way the gallery's working card does.</summary>
internal sealed class DesignToCodeTestPreview : Border
{
    /// <summary>Whether the yearly price shows.</summary>
    public bool IsYearly { get; set; }

    /// <summary>The price on show.</summary>
    public int ShownPrice => IsYearly ? DesignToCodeTestData.YearlyPrice : DesignToCodeTestData.MonthlyPrice;
}
