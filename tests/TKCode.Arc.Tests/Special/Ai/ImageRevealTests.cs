using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ImageReveal: the pose engine (the simulation's script, the 200ms follow, the band's speed limit, blurring back out
/// before a reset, the sheen and the pill), the control in its modes (empty, the app's own image, a simulation, controlled,
/// generator), the pill's words and count, the error state with Retry and its keyboard model, the accessible names and
/// announcements, labels and sizing.
/// </summary>
public class ImageRevealTests
{
    private static (ImageReveal Reveal, Window Window) Open(Action<ImageReveal>? setup = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var reveal = new ImageReveal { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        setup?.Invoke(reveal);
        var window = TestHelpers.Show(reveal, level);
        window.Width = 600;
        window.Height = 800;
        Pump();
        return (reveal, window);
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

    private static void Step(ImageReveal reveal, double seconds, double frame = 1 / 60.0)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += frame)
        {
            reveal.Advance(Math.Min(frame, seconds - t));
        }
    }

    private static void Step(ImageRevealEngine engine, double seconds, double frame = 1 / 60.0)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += frame)
        {
            engine.Tick(Math.Min(frame, seconds - t));
        }
    }

    private static IImage Picture() => new RenderTargetBitmap(new PixelSize(64, 80), new Vector(96, 96));

    /// <summary>An invented simulation of three pictures (the test's own data; the Gallery passes painted scenes).</summary>
    private static ImageRevealSimulation Simulation() => new()
    {
        Pictures =
        [
            new ImageRevealSimulationPicture { Image = Picture(), AltText = "A calm alpine lake at golden hour" },
            new ImageRevealSimulationPicture { Image = Picture(), AltText = "Desert dunes under a rising moon at dusk" },
            new ImageRevealSimulationPicture { Image = Picture(), AltText = "A lighthouse on sea cliffs at dawn" },
        ],
    };

    // ---- The engine ----

    [Fact]
    public void Simulation_Queues_Generates_Resolves_Holds_And_Loops()
    {
        var engine = new ImageRevealEngine { HasImage = true, Level = ArcMotionLevel.Full };
        var changes = new List<(ImageRevealStatus Old, ImageRevealStatus New)>();
        var completed = 0;
        var runs = 0;
        engine.StatusChanged += (o, n) => changes.Add((o, n));
        engine.Completed += () => completed++;
        engine.RunStarting += () => runs++;

        engine.Restart();
        runs.ShouldBe(1);
        engine.Status.ShouldBe(ImageRevealStatus.Queued);
        Step(engine, 0.85);
        engine.Status.ShouldBe(ImageRevealStatus.Queued);
        Step(engine, 0.1);
        engine.Status.ShouldBe(ImageRevealStatus.Generating);

        // Ease out: the composition resolves early (more than half way at 40% of the time).
        Step(engine, 5.2 * 0.4);
        engine.Target.ShouldBeGreaterThan(0.6);
        engine.Preview.ShouldBe(1);
        engine.Reveal.ShouldBeGreaterThan(0.4);

        Step(engine, 5.2 * 0.6 + 0.05);
        engine.Status.ShouldBe(ImageRevealStatus.Done);
        Step(engine, 0.6);
        engine.Reveal.ShouldBe(1);
        completed.ShouldBe(1);
        engine.IsResolved.ShouldBeTrue();
        // The sheen plays once (1.1s), then the pill fades out (240ms).
        engine.Sheen.ShouldBeGreaterThanOrEqualTo(0);
        Step(engine, 1.2);
        engine.Sheen.ShouldBe(-1);
        Step(engine, 0.3);
        engine.PillOpacity.ShouldBe(0);

        // After the 2.6s hold the image blurs back out (500ms) and a new run starts.
        var frames = 0;
        while (engine.Status != ImageRevealStatus.Queued && frames++ < 600)
        {
            engine.Tick(1 / 60.0);
        }
        engine.Status.ShouldBe(ImageRevealStatus.Queued);
        engine.IsResetting.ShouldBeTrue();
        runs.ShouldBe(1);
        Step(engine, 0.55);
        engine.IsResetting.ShouldBeFalse();
        runs.ShouldBe(2);
        engine.Reveal.ShouldBe(0);
        engine.Focus.ShouldBe(1);
        completed.ShouldBe(1);
        changes.ShouldBe([
            (ImageRevealStatus.Queued, ImageRevealStatus.Generating),
            (ImageRevealStatus.Generating, ImageRevealStatus.Done),
            (ImageRevealStatus.Done, ImageRevealStatus.Queued),
        ]);
    }

    [Fact]
    public void Start_Delay_Adds_Queue_Time_And_No_Loop_Rests_When_Done()
    {
        var engine = new ImageRevealEngine { HasImage = true, StartDelay = 0.42, Loops = false, Level = ArcMotionLevel.Full };
        engine.Restart();
        Step(engine, 1.2);
        engine.Status.ShouldBe(ImageRevealStatus.Queued);
        Step(engine, 0.2);
        engine.Status.ShouldBe(ImageRevealStatus.Generating);
        Step(engine, 12);
        engine.Status.ShouldBe(ImageRevealStatus.Done);
        engine.IsResolved.ShouldBeTrue();
        engine.WantsFrames.ShouldBeFalse();
    }

    [Fact]
    public void Coarse_Progress_Is_Smoothed_With_A_200ms_Follow()
    {
        var engine = new ImageRevealEngine { IsAuto = false, HasImage = true, Level = ArcMotionLevel.Full };
        engine.Request(ImageRevealStatus.Generating);
        engine.RequestedProgress = 0.3;
        engine.Tick(0.2);
        // One time constant: about 63% of the way, but never faster than a 0.9s sweep.
        engine.PillValue.ShouldBe(0.3 * (1 - Math.Exp(-1)), 0.001);
        engine.Reveal.ShouldBeLessThanOrEqualTo(0.2 / ImageRevealEngine.FastestSweep + 1e-9);
        Step(engine, 1.5);
        engine.Reveal.ShouldBe(0.3);
        engine.PillValue.ShouldBe(0.3);
        engine.StepOf(30).ShouldBe(9);
    }

    [Fact]
    public void Without_Progress_The_Estimate_Waits_Short_Of_The_End()
    {
        var engine = new ImageRevealEngine { IsAuto = false, HasImage = false, Duration = 2, Level = ArcMotionLevel.Full };
        engine.Request(ImageRevealStatus.Generating);
        Step(engine, 6);
        engine.Status.ShouldBe(ImageRevealStatus.Generating);
        engine.PillValue.ShouldBe(ImageRevealEngine.EstimateCap, 0.002);
        // No image yet: the canvas stays in the calm look, the band has not moved.
        engine.Reveal.ShouldBe(0);
        engine.Preview.ShouldBe(0);
        engine.HasImage = true;
        engine.Request(ImageRevealStatus.Done);
        Step(engine, 0.45);
        // The late image still sweeps in (at most one canvas per 0.9s).
        engine.Reveal.ShouldBeInRange(0.3, 0.55);
        Step(engine, 1.6);
        engine.Reveal.ShouldBe(1);
        engine.IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Failing_Blurs_The_Sharp_Layers_Out_And_A_New_Run_Resets_Underneath()
    {
        var engine = new ImageRevealEngine { IsAuto = false, HasImage = true, Level = ArcMotionLevel.Full };
        engine.Request(ImageRevealStatus.Generating);
        engine.RequestedProgress = 0.6;
        Step(engine, 2);
        engine.Request(ImageRevealStatus.Error);
        engine.Tick(0.25);
        engine.Focus.ShouldBe(0.5, 0.01);
        Step(engine, 0.5);
        engine.Focus.ShouldBe(0);
        engine.Dim.ShouldBe(ImageRevealEngine.ErrorDim);
        engine.Reveal.ShouldBe(0.6);

        engine.RequestedProgress = 0;
        engine.Request(ImageRevealStatus.Generating);
        engine.Reveal.ShouldBe(0);
        engine.Focus.ShouldBe(1);
        engine.Status.ShouldBe(ImageRevealStatus.Generating);
    }

    [Fact]
    public void Restarting_A_Finished_Image_Blurs_It_Out_Before_Anything_Resets()
    {
        var engine = new ImageRevealEngine { IsAuto = false, HasImage = true, Level = ArcMotionLevel.Full };
        engine.Settle(ImageRevealStatus.Done);
        engine.Reveal.ShouldBe(1);
        engine.IsResolved.ShouldBeTrue();
        engine.Request(ImageRevealStatus.Generating);
        engine.IsResetting.ShouldBeTrue();
        engine.Tick(0.2);
        engine.Reveal.ShouldBe(1);
        engine.Focus.ShouldBeLessThan(1);
        Step(engine, 0.4);
        engine.IsResetting.ShouldBeFalse();
        engine.Reveal.ShouldBeLessThan(0.2);
    }

    [Fact]
    public void Reduced_Motion_Drops_The_Sheen_And_Off_Jumps()
    {
        var reduced = new ImageRevealEngine { IsAuto = false, HasImage = true, Level = ArcMotionLevel.Reduced };
        reduced.Request(ImageRevealStatus.Done);
        Step(reduced, 2);
        reduced.IsResolved.ShouldBeTrue();
        reduced.Sheen.ShouldBe(-1);

        var off = new ImageRevealEngine { IsAuto = false, HasImage = true, Level = ArcMotionLevel.Off };
        var completed = 0;
        off.Completed += () => completed++;
        off.Request(ImageRevealStatus.Done);
        off.Tick(1 / 60.0);
        off.Reveal.ShouldBe(1);
        off.Tick(1 / 60.0);
        completed.ShouldBe(1);
        off.Sheen.ShouldBe(-1);
        off.Tick(1 / 60.0);
        off.PillOpacity.ShouldBe(0);
    }

    [Fact]
    public void The_Progress_Curve_And_Steps_Stay_In_Range()
    {
        ImageRevealEngine.Ease(0).ShouldBe(0);
        ImageRevealEngine.Ease(1).ShouldBe(1);
        ImageRevealEngine.Ease(2).ShouldBe(1);
        for (var t = 0.0; t < 1; t += 0.05)
        {
            ImageRevealEngine.Ease(t + 0.05).ShouldBeGreaterThan(ImageRevealEngine.Ease(t));
            ImageRevealEngine.Ease(t).ShouldBeGreaterThanOrEqualTo(t);
        }
        new ImageRevealEngine().StepOf(0).ShouldBe(0);
    }

    [Fact]
    public void Cover_Crop_Keeps_The_Center()
    {
        ImageRevealChain.CoverCrop(new Size(1000, 500), 1).ShouldBe(new Rect(250, 0, 500, 500));
        ImageRevealChain.CoverCrop(new Size(500, 1000), 1).ShouldBe(new Rect(0, 250, 500, 500));
        ImageRevealChain.CoverCrop(new Size(800, 1000), 0.8).ShouldBe(new Rect(0, 0, 800, 1000));
        ImageRevealChain.NearWidthFor(1120).ShouldBe(368);
        ImageRevealChain.NearWidthFor(100).ShouldBe(64);
    }

    // ---- The control ----

    [AvaloniaFact]
    public void With_No_Data_And_No_Simulation_The_Canvas_Rests_Empty()
    {
        var (reveal, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            var changes = 0;
            reveal.StatusChanged += (_, _) => changes++;
            reveal.IsEmpty.ShouldBeTrue();
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            reveal.Engine.IsAuto.ShouldBeFalse();
            reveal.Engine.IsBlank.ShouldBeTrue();
            reveal.DisplayedImage.ShouldBeNull();
            AutomationProperties.GetName(reveal).ShouldBe("No image");
            TestHelpers.Descendant<Control>(reveal, ImageReveal.PillHostPart).IsVisible.ShouldBeFalse();
            reveal.Announcement.ShouldBeNullOrEmpty();
            // No shimmer and no frames: nothing moves on an empty canvas.
            reveal.Engine.WantsFrames.ShouldBeFalse();
            Step(reveal, 10);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            changes.ShouldBe(0);
            reveal.Restart();
            Step(reveal, 2);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Own_Image_Wins_Over_The_Simulation_And_Rests_Finished()
    {
        var picture = Picture();
        var simulation = Simulation();
        var (reveal, window) = Open(r =>
        {
            r.Source = picture;
            r.AltText = "A sunlit greenhouse";
            r.Simulation = simulation;
        });
        try
        {
            var completed = 0;
            reveal.Completed += (_, _) => completed++;
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Done);
            reveal.Engine.IsAuto.ShouldBeFalse();
            reveal.Engine.IsResolved.ShouldBeTrue();
            reveal.DisplayedImage.ShouldBeSameAs(picture);
            AutomationProperties.GetName(reveal).ShouldBe("A sunlit greenhouse");
            Step(reveal, 12);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Done);
            reveal.DisplayedImage.ShouldBeSameAs(picture);
            completed.ShouldBe(0);

            // Clearing the image hands the canvas to the simulation.
            reveal.Source = null;
            reveal.Engine.IsAuto.ShouldBeTrue();
            Step(reveal, 1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
            reveal.DisplayedImage.ShouldBeSameAs(simulation.Pictures[0].Image);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clearing_The_Simulation_Stops_It_And_Empties_The_Canvas()
    {
        var (reveal, window) = Open(r => r.Simulation = Simulation());
        try
        {
            Step(reveal, 1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
            reveal.Simulation = null;
            reveal.IsEmpty.ShouldBeTrue();
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            reveal.DisplayedImage.ShouldBeNull();
            AutomationProperties.GetName(reveal).ShouldBe("No image");
            Step(reveal, 10);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            TestHelpers.Descendant<Control>(reveal, ImageReveal.PillHostPart).IsVisible.ShouldBeFalse();

            // A new simulation plays again from its first picture.
            var next = Simulation();
            reveal.Simulation = next;
            Step(reveal, 1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
            next.Index.ShouldBe(0);
            reveal.DisplayedImage.ShouldBeSameAs(next.Pictures[0].Image);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plays_A_Simulation_Over_Its_Pictures()
    {
        var simulation = Simulation();
        var (reveal, window) = Open(r => r.Simulation = simulation);
        try
        {
            var changes = new List<ImageRevealStatusChangedEventArgs>();
            var completed = 0;
            reveal.StatusChanged += (_, e) => changes.Add(e);
            reveal.Completed += (_, _) => completed++;
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            reveal.Engine.IsAuto.ShouldBeTrue();
            simulation.Index.ShouldBe(0);
            reveal.DisplayedImage.ShouldBeSameAs(simulation.Pictures[0].Image);
            reveal.PillText.ShouldBe("Queued");
            AutomationProperties.GetName(reveal).ShouldBe("Queued");

            Step(reveal, 1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
            changes.Count.ShouldBe(1);
            changes[0].OldStatus.ShouldBe(ImageRevealStatus.Queued);
            changes[0].NewStatus.ShouldBe(ImageRevealStatus.Generating);
            reveal.PillText.ShouldBe("Generating");
            reveal.CountText.ShouldNotBeNull().ShouldEndWith(" / 30");
            AutomationProperties.GetName(reveal).ShouldBe("Generating image");
            reveal.Announcement.ShouldBe("Generating image (simulated)");

            Step(reveal, 5.4);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Done);
            completed.ShouldBe(1);
            reveal.PillText.ShouldBe("Done");
            reveal.CountText.ShouldBeNull();
            reveal.Announcement.ShouldBe("Image ready (simulated)");
            AutomationProperties.GetName(reveal).ShouldBe("A calm alpine lake at golden hour");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Loop_Moves_On_To_The_Next_Picture_And_Seeds_Pick_The_First()
    {
        var simulation = Simulation();
        var (reveal, window) = Open(r =>
        {
            r.Seed = 2;
            r.Simulation = simulation;
        });
        try
        {
            simulation.Index.ShouldBe(1);
            Step(reveal, 0.9 + 5.2 + 0.2 + 2.7);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            simulation.Index.ShouldBe(2);
            reveal.DisplayedImage.ShouldBeSameAs(simulation.Pictures[2].Image);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_Key_And_Restart_Run_Again_And_Auto_Play_Off_Waits()
    {
        var (reveal, window) = Open(r =>
        {
            r.AutoPlay = false;
            r.IsLooping = false;
            r.Simulation = Simulation();
        });
        try
        {
            Step(reveal, 3);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            reveal.Restart();
            Step(reveal, 1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
            Step(reveal, 6);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Done);
            reveal.RunKey = 1;
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            Step(reveal, 1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Controlled_Status_And_Progress_Drive_The_Pill()
    {
        var picture = Picture();
        var (reveal, window) = Open(r =>
        {
            r.Source = picture;
            r.Status = ImageRevealStatus.Generating;
            r.Progress = 18 / 30.0;
        });
        try
        {
            reveal.Engine.IsAuto.ShouldBeFalse();
            reveal.DisplayedImage.ShouldBeSameAs(picture);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
            Step(reveal, 0.1);
            reveal.CountText.ShouldBe("18 / 30");
            var pill = TestHelpers.Descendant<ImageRevealPill>(reveal, ImageReveal.PillPart);
            pill.IsProgress.ShouldBeTrue();
            pill.Value.ShouldBe(18);
            pill.Maximum.ShouldBe(30);
            AutomationProperties.GetName(pill).ShouldBe("Step 18 of 30");

            reveal.Steps = 50;
            reveal.Progress = 0.5;
            Step(reveal, 0.1);
            reveal.CountText.ShouldBe("25 / 50");

            var completed = 0;
            reveal.Completed += (_, _) => completed++;
            reveal.AltText = "A calm alpine lake at golden hour";
            reveal.Status = ImageRevealStatus.Done;
            Step(reveal, 0.1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Done);
            completed.ShouldBe(1);
            pill.IsProgress.ShouldBeFalse();
            AutomationProperties.GetName(reveal).ShouldBe("A calm alpine lake at golden hour");
            reveal.Announcement.ShouldBe("Image ready");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Job_Restored_As_Done_Rests_There()
    {
        var picture = Picture();
        var (reveal, window) = Open(r =>
        {
            r.Source = picture;
            r.Status = ImageRevealStatus.Done;
        });
        try
        {
            var completed = 0;
            reveal.Completed += (_, _) => completed++;
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Done);
            reveal.Engine.Reveal.ShouldBe(1);
            reveal.Engine.PillOpacity.ShouldBe(0);
            Step(reveal, 0.5);
            completed.ShouldBe(0);
            AutomationProperties.GetName(reveal).ShouldBe("Generated image");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Queued_Shows_The_Queue_Label()
    {
        var (reveal, window) = Open(r =>
        {
            r.Status = ImageRevealStatus.Queued;
            r.QueueLabel = "Queued, 2 ahead";
        });
        try
        {
            reveal.PillText.ShouldBe("Queued, 2 ahead");
            reveal.CountText.ShouldBeNull();
            TestHelpers.Descendant<ArcSpinner>(reveal, ImageReveal.SpinnerPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<ArcRing>(reveal, ImageReveal.RingPart).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(reveal).ShouldBe("Queued, 2 ahead");
            reveal.DisplayedImage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Shows_The_Message_And_Retry_Is_A_Keyboard_Button()
    {
        var (reveal, window) = Open(r =>
        {
            r.Status = ImageRevealStatus.Error;
            r.ErrorMessage = "The image could not be generated.";
        });
        try
        {
            var retries = 0;
            reveal.RetryRequested += (_, _) => retries++;
            reveal.Classes.Contains(ImageReveal.ErrorPseudoClass).ShouldBeTrue();
            var error = TestHelpers.Descendant<StackPanel>(reveal, ImageReveal.ErrorPart);
            error.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(reveal, ImageReveal.ErrorTextPart).Text.ShouldBe("The image could not be generated.");
            TestHelpers.Descendant<Control>(reveal, ImageReveal.PillHostPart).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(reveal).ShouldBe("The image could not be generated.");
            reveal.Focusable.ShouldBeFalse();

            var retry = TestHelpers.Descendant<Avalonia.Controls.Button>(reveal, ImageReveal.RetryPart);
            retry.Content.ShouldBe("Retry");
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump();
            retry.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            retries.ShouldBe(1);

            reveal.AllowsRetry = false;
            retry.IsVisible.ShouldBeFalse();
            reveal.Status = ImageRevealStatus.Generating;
            error.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Generator_Reports_Progress_Returns_The_Image_And_Retries_After_A_Failure()
    {
        var picture = Picture();
        var calls = 0;
        IProgress<ImageRevealProgress>? report = null;
        TaskCompletionSource<IImage?>? finish = null;
        var (reveal, window) = Open(r => r.Generator = (progress, _) =>
        {
            calls++;
            report = progress;
            finish = new TaskCompletionSource<IImage?>();
            return finish.Task;
        });
        try
        {
            calls.ShouldBe(1);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
            report!.Report(new ImageRevealProgress(0.5, picture));
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Generating);
            reveal.DisplayedImage.ShouldBeSameAs(picture);
            Step(reveal, 0.1);
            reveal.CountText.ShouldBe("15 / 30");
            reveal.Announcement.ShouldBe("Generating image");

            finish!.SetResult(picture);
            Pump();
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Done);

            reveal.Restart();
            calls.ShouldBe(2);
            finish!.SetException(new InvalidOperationException("boom"));
            Pump();
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Error);
            reveal.Announcement.ShouldBe("Generation failed");

            var retries = 0;
            reveal.RetryRequested += (_, _) => retries++;
            var retry = TestHelpers.Descendant<Avalonia.Controls.Button>(reveal, ImageReveal.RetryPart);
            retry.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            retries.ShouldBe(1);
            calls.ShouldBe(3);
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Cancelled_Run_Never_Reaches_The_Canvas()
    {
        var tokens = new List<CancellationToken>();
        var finishes = new List<TaskCompletionSource<IImage?>>();
        var (reveal, window) = Open(r => r.Generator = (_, token) =>
        {
            tokens.Add(token);
            var finish = new TaskCompletionSource<IImage?>();
            finishes.Add(finish);
            return finish.Task;
        });
        try
        {
            reveal.Restart();
            tokens.Count.ShouldBe(2);
            tokens[0].IsCancellationRequested.ShouldBeTrue();
            finishes[0].SetException(new OperationCanceledException(tokens[0]));
            Pump();
            reveal.ActualStatus.ShouldBe(ImageRevealStatus.Queued);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Small_Shows_Only_The_Ring_And_The_Count()
    {
        var (reveal, window) = Open(r =>
        {
            r.Size = ControlSize.Small;
            r.Status = ImageRevealStatus.Generating;
            r.Progress = 0.1;
        });
        try
        {
            reveal.Classes.Contains(ImageReveal.SmallPseudoClass).ShouldBeTrue();
            reveal.PillText.ShouldBeNull();
            TestHelpers.Descendant<ArcContentSwap>(reveal, ImageReveal.LabelPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<ArcRing>(reveal, ImageReveal.RingPart).IsVisible.ShouldBeTrue();
            Step(reveal, 0.1);
            reveal.CountText.ShouldBe("3 / 30");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (reveal, window) = Open(r =>
        {
            r.Labels = new ImageRevealLabels
            {
                Generating = "Génération",
                GeneratingImage = "Image en cours",
                Count = (step, total) => $"{step} sur {total}",
                StepOf = (step, total) => $"Étape {step} sur {total}",
            };
            r.Status = ImageRevealStatus.Generating;
            r.Progress = 0.2;
        });
        try
        {
            Step(reveal, 0.1);
            reveal.PillText.ShouldBe("Génération");
            reveal.CountText.ShouldBe("6 sur 30");
            AutomationProperties.GetName(reveal).ShouldBe("Image en cours");
            AutomationProperties.GetName(TestHelpers.Descendant<ImageRevealPill>(reveal, ImageReveal.PillPart)).ShouldBe("Étape 6 sur 30");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fills_Its_Container_Up_To_560_And_Keeps_The_Aspect_Ratio()
    {
        var (reveal, window) = Open(r => r.AspectRatio = 1.5);
        try
        {
            reveal.Bounds.Width.ShouldBe(560);
            reveal.Bounds.Height.ShouldBe(560 / 1.5, 1);
            window.Width = 300;
            Pump();
            reveal.Bounds.Width.ShouldBe(300);
            reveal.Bounds.Height.ShouldBe(200, 1);
            reveal.AspectRatio = 0.8;
            Pump();
            reveal.Bounds.Height.ShouldBe(375, 1);
            // While it generates, its size never follows what it shows.
            reveal.Status = ImageRevealStatus.Generating;
            reveal.Progress = 0.5;
            Step(reveal, 0.5);
            Pump();
            reveal.Bounds.Width.ShouldBe(300);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Frame_Loop_Rests_Once_Resolved()
    {
        var picture = Picture();
        var (reveal, window) = Open(r =>
        {
            r.Source = picture;
            r.Status = ImageRevealStatus.Generating;
            r.Progress = 1;
        });
        try
        {
            reveal.Status = ImageRevealStatus.Done;
            Step(reveal, 0.2);
            reveal.Engine.IsResolved.ShouldBeTrue();
            reveal.Engine.WantsFrames.ShouldBeFalse();
            var canvas = TestHelpers.Descendant<ImageRevealCanvas>(reveal, ImageReveal.CanvasPart);
            canvas.Engine.ShouldBeSameAs(reveal.Engine);
        }
        finally
        {
            window.Close();
        }
    }
}
