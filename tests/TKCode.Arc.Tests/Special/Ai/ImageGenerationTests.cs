using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ImageGeneration: the sample it opens on, a batch driven by a test generator (progress, finish, failure and retry, stop),
/// picking, upscaling and closing, vary, download, the chips' and grid's keyboard model, labels, the breakpoints, and the
/// pure parts (mosaic phases and averages, the grid's size, file names, the scene painter and the simulation's curve).
/// </summary>
public class ImageGenerationTests
{
    /// <summary>A generator whose images the test finishes, fails or reports on.</summary>
    private sealed class TestGenerator
    {
        public List<(ImageGenerationRequest Request, ImageGenerationContext Context, TaskCompletionSource<ImageGenerationImage> Result)> Calls { get; } = [];

        public Task<ImageGenerationImage> Generate(ImageGenerationRequest request, ImageGenerationContext context)
        {
            var result = new TaskCompletionSource<ImageGenerationImage>();
            context.CancellationToken.Register(() => result.TrySetCanceled());
            Calls.Add((request, context, result));
            return result.Task;
        }

        public void Finish(int call, int width = 1024, int height = 1024) =>
            Calls[call].Result.SetResult(new ImageGenerationImage
            {
                Id = "img-" + call,
                Description = Calls[call].Request.Prompt,
                Width = width,
                Height = height,
            });

        public void FinishAll()
        {
            for (var i = 0; i < Calls.Count; i++)
            {
                if (!Calls[i].Result.Task.IsCompleted)
                {
                    Finish(i);
                }
            }
        }
    }

    private static (ImageGeneration Card, Window Window, TestGenerator Generator) Open(Action<ImageGeneration>? setup = null,
        double width = 640, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var generator = new TestGenerator();
        var card = new ImageGeneration
        {
            Simulation = ImageGenerationTestData.Simulation(),
            Generator = generator.Generate,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 1000;
        Pump();
        return (card, window, generator);
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

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static void Click(Control control)
    {
        control.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Pump();
    }

    private static AvaloniaButton Part(ImageGeneration card, string name) => TestHelpers.Descendant<AvaloniaButton>(card, name);

    private static List<ImageGenerationChip> Chips(ImageGeneration card, string panel) =>
        TestHelpers.Descendant<StackPanel>(card, panel).Children.OfType<ImageGenerationChip>().ToList();

    private static void FinishBatch(ImageGeneration card, TestGenerator generator)
    {
        Click(Part(card, ImageGeneration.GeneratePart));
        generator.FinishAll();
        Pump();
    }

    [AvaloniaFact]
    public void Opens_On_The_Sample_Chips_And_An_Empty_Grid()
    {
        var (card, window, generator) = Open();
        try
        {
            card.Prompt.ShouldBe(ImageGenerationTestData.Prompt);
            card.IsGenerating.ShouldBeFalse();
            generator.Calls.ShouldBeEmpty();
            var models = Chips(card, ImageGeneration.ModelsPart);
            models.Select(c => c.Text).ShouldBe(["Image A", "Image B", "Image C"]);
            models[0].IsChecked.ShouldBe(true);
            models[0].IsTabStop.ShouldBeTrue();
            models[1].IsTabStop.ShouldBeFalse();
            AutomationProperties.GetName(models[1]).ShouldBe("Image B");
            Chips(card, ImageGeneration.AspectsPart).Select(c => c.Text).ShouldBe(["1:1", "4:5", "16:9"]);
            card.Tiles.Count.ShouldBe(4);
            card.Tiles.ShouldAllBe(t => t.State == ImageGenerationTileState.Idle && !t.Focusable);
            card.SuggestionsPanel!.IsVisible.ShouldBeTrue();
            card.SuggestionsPanel.Children.OfType<AvaloniaButton>().Count().ShouldBe(3);
            card.StatusText.ShouldBe("Write a prompt or pick one to start");
            card.GenerateFaces!.ShownText.ShouldBe("Generate");
            Part(card, ImageGeneration.UpscalePart).IsEnabled.ShouldBeFalse();
            Part(card, ImageGeneration.VaryPart).IsEnabled.ShouldBeFalse();
            Part(card, ImageGeneration.DownloadPart).IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(Part(card, ImageGeneration.GeneratePart)).ShouldBe("Generate");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generate_Runs_Four_Tiles_With_Progress_Then_Finishes()
    {
        var (card, window, generator) = Open();
        try
        {
            Click(Part(card, ImageGeneration.GeneratePart));
            card.IsGenerating.ShouldBeTrue();
            card.Classes.Contains(ImageGeneration.GeneratingPseudoClass).ShouldBeTrue();
            generator.Calls.Count.ShouldBe(4);
            generator.Calls.Select(c => c.Request.Index).ShouldBe([0, 1, 2, 3]);
            generator.Calls.ShouldAllBe(c => c.Request.Model.Id == "image-a" && c.Request.Aspect.Id == "1:1" && c.Request.VariationOf == null);
            card.Tiles.ShouldAllBe(t => t.State == ImageGenerationTileState.Running);
            card.GenerateFaces!.ShownText.ShouldBe("Stop");
            card.StatusText.ShouldBe("Generating 4 images with Image A");
            card.SuggestionsPanel!.IsVisible.ShouldBeFalse();
            card.Announcer!.Announcement.ShouldBe("Generating 4 images with Image A");

            generator.Calls[1].Context.ReportProgress(0.5);
            Pump();
            card.Tiles[1].Shown.ShouldBe(0.5, 0.001);
            card.Tiles[1].PercentText.ShouldBe("50%");
            card.Tiles[1].Canvas!.Progress.ShouldBe(0.5, 0.001);
            // Progress never goes back.
            generator.Calls[1].Context.ReportProgress(0.3);
            Pump();
            card.Tiles[1].Shown.ShouldBe(0.5, 0.001);

            generator.FinishAll();
            Pump();
            card.IsGenerating.ShouldBeFalse();
            card.Tiles.ShouldAllBe(t => t.State == ImageGenerationTileState.Done);
            card.StatusText.ShouldBe("4 images ready, pick one");
            card.Announcer.Announcement.ShouldBe("4 images ready, pick one");
            card.GenerateFaces.ShownText.ShouldBe("Generate");
            AutomationProperties.GetName(card.Tiles[0]).ShouldBe("Image 1, " + ImageGenerationTestData.Prompt);
            // A run started by the person never picks on its own.
            card.SelectedImage.ShouldBeNull();
            // One tab stop in the grid.
            card.Tiles.Count(t => t.IsTabStop).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Tile_Says_So_And_Retries_When_Pressed()
    {
        var (card, window, generator) = Open();
        try
        {
            Click(Part(card, ImageGeneration.GeneratePart));
            generator.Calls[2].Result.SetException(new InvalidOperationException("boom"));
            Pump();
            var tile = card.Tiles[2];
            tile.State.ShouldBe(ImageGenerationTileState.Failed);
            tile.Classes.Contains(ImageGenerationTile.FailedPseudoClass).ShouldBeTrue();
            AutomationProperties.GetName(tile).ShouldBe("Image 3 failed, press to retry");
            card.Announcer!.History.ShouldContain("Image 3 failed");
            generator.FinishAll();
            Pump();
            card.StatusText.ShouldBe("3 of 4 images ready");

            Click(tile);
            generator.Calls.Count.ShouldBe(5);
            generator.Calls[4].Request.ShouldBeSameAs(generator.Calls[2].Request);
            tile.State.ShouldBe(ImageGenerationTileState.Running);
            card.IsGenerating.ShouldBeTrue();
            generator.Finish(4);
            Pump();
            tile.State.ShouldBe(ImageGenerationTileState.Done);
            card.IsGenerating.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Cancels_The_Run_And_Marks_Unfinished_Tiles()
    {
        var (card, window, generator) = Open();
        try
        {
            Click(Part(card, ImageGeneration.GeneratePart));
            generator.Finish(0);
            Pump();
            Click(Part(card, ImageGeneration.GeneratePart));
            card.IsGenerating.ShouldBeFalse();
            generator.Calls.ShouldAllBe(c => c.Context.CancellationToken.IsCancellationRequested);
            card.Tiles[0].State.ShouldBe(ImageGenerationTileState.Done);
            card.Tiles.Skip(1).ShouldAllBe(t => t.State == ImageGenerationTileState.Stopped);
            AutomationProperties.GetName(card.Tiles[1]).ShouldBe("Image 2 stopped, press to retry");
            card.StatusText.ShouldBe("Stopped, 1 of 4 finished");
            card.GenerateFaces!.ShownText.ShouldBe("Generate");

            // A stopped tile retries with a fresh token.
            Click(card.Tiles[1]);
            generator.Calls[^1].Context.CancellationToken.IsCancellationRequested.ShouldBeFalse();
            card.Tiles[1].State.ShouldBe(ImageGenerationTileState.Running);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_A_Finished_Tile_Picks_It_And_Again_Clears_It()
    {
        var (card, window, generator) = Open();
        try
        {
            var events = new List<ImageGenerationSelectionChangedEventArgs>();
            card.SelectionChanged += (_, e) => events.Add(e);
            FinishBatch(card, generator);
            Click(card.Tiles[1]);
            card.SelectedIndex.ShouldBe(1);
            card.Tiles[1].IsChecked.ShouldBe(true);
            card.Tiles[0].IsChecked.ShouldBe(false);
            card.SelectedImage!.Id.ShouldBe("img-1");
            events.Count.ShouldBe(1);
            events[0].Index.ShouldBe(1);
            events[0].Image!.Id.ShouldBe("img-1");
            card.StatusText.ShouldBe("Image 2 · 1024 × 1024");
            card.Tiles[1].IsTabStop.ShouldBeTrue();
            card.Tiles.Count(t => t.IsTabStop).ShouldBe(1);
            Part(card, ImageGeneration.UpscalePart).IsEnabled.ShouldBeTrue();
            Part(card, ImageGeneration.VaryPart).IsEnabled.ShouldBeTrue();
            Part(card, ImageGeneration.DownloadPart).IsEnabled.ShouldBeTrue();

            Click(card.Tiles[1]);
            card.SelectedImage.ShouldBeNull();
            events.Count.ShouldBe(2);
            events[1].Image.ShouldBeNull();
            events[1].Index.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Upscale_Expands_The_Pick_Reports_The_New_Image_And_Escape_Closes()
    {
        var upscales = new List<(ImageGenerationImage Image, ImageGenerationContext Context, TaskCompletionSource<ImageGenerationImage> Result)>();
        var (card, window, generator) = Open(c => c.Upscaler = (image, context) =>
        {
            var result = new TaskCompletionSource<ImageGenerationImage>();
            upscales.Add((image, context, result));
            return result.Task;
        });
        try
        {
            FinishBatch(card, generator);
            Click(card.Tiles[2]);
            var events = new List<ImageGenerationSelectionChangedEventArgs>();
            card.SelectionChanged += (_, e) => events.Add(e);
            Click(Part(card, ImageGeneration.UpscalePart));
            card.IsExpanded.ShouldBeTrue();
            card.GridPartControl!.ExpandedIndex.ShouldBe(2);
            card.GridPartControl.ExpandProgress.ShouldBe(1);
            // The grid behind the expanded image is inert.
            card.Tiles[0].IsHitTestVisible.ShouldBeFalse();
            card.Tiles[0].Focusable.ShouldBeFalse();
            card.Tiles[0].Opacity.ShouldBe(0);
            card.Tiles[2].Bounds.Size.ShouldBe(card.GridPartControl.GridRect.Size);
            card.CloseButtonControl!.IsVisible.ShouldBeTrue();
            upscales.Count.ShouldBe(1);
            upscales[0].Image.Id.ShouldBe("img-2");
            card.UpscaleFaces!.ShownText.ShouldBe("Upscaling");
            card.StatusText.ShouldBe("Upscaling image 3");
            card.Tiles[2].Classes.Contains(ImageGenerationTile.WorkingPseudoClass).ShouldBeTrue();

            upscales[0].Context.ReportProgress(0.4);
            Pump();
            card.Tiles[2].Shown.ShouldBe(0.4, 0.001);

            upscales[0].Result.SetResult(new ImageGenerationImage { Id = "img-2-up", Width = 2048, Height = 2048 });
            Pump();
            card.SelectedImage!.Id.ShouldBe("img-2-up");
            events.Count.ShouldBe(1);
            events[0].Image!.Id.ShouldBe("img-2-up");
            card.Tiles[2].IsUpscaled.ShouldBeTrue();
            card.UpscaleFaces.ShownText.ShouldBe("Upscaled");
            card.StatusText.ShouldBe("Image 3 upscaled to 2048 × 2048");
            card.Announcer!.Announcement.ShouldBe("Image 3 upscaled to 2048 × 2048");

            card.Tiles[2].Focus();
            Press(window, PhysicalKey.Escape);
            card.IsExpanded.ShouldBeFalse();
            card.GridPartControl.ExpandedIndex.ShouldBe(-1);
            card.Tiles[0].IsHitTestVisible.ShouldBeTrue();
            card.Tiles[0].Opacity.ShouldBe(1);
            card.CloseButtonControl.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_While_Upscaling_Cancels_It()
    {
        ImageGenerationContext? upscale = null;
        var (card, window, generator) = Open(c => c.Upscaler = (image, context) =>
        {
            upscale = context;
            var result = new TaskCompletionSource<ImageGenerationImage>();
            context.CancellationToken.Register(() => result.TrySetCanceled());
            return result.Task;
        });
        try
        {
            FinishBatch(card, generator);
            Click(card.Tiles[0]);
            Click(Part(card, ImageGeneration.UpscalePart));
            Click(card.CloseButtonControl!);
            upscale!.CancellationToken.IsCancellationRequested.ShouldBeTrue();
            card.IsExpanded.ShouldBeFalse();
            card.Tiles[0].IsUpscaled.ShouldBeFalse();
            card.UpscaleFaces!.ShownText.ShouldBe("Upscale");
            card.SelectedImage!.Id.ShouldBe("img-0");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vary_Starts_A_Batch_From_The_Pick()
    {
        var (card, window, generator) = Open();
        try
        {
            FinishBatch(card, generator);
            Click(card.Tiles[3]);
            var picked = card.SelectedImage;
            Click(Part(card, ImageGeneration.VaryPart));
            generator.Calls.Count.ShouldBe(8);
            generator.Calls.Skip(4).ShouldAllBe(c => ReferenceEquals(c.Request.VariationOf, picked));
            card.SelectedImage.ShouldBeNull();
            card.IsGenerating.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Download_Raises_The_Image_With_A_File_Name_And_Reads_Saved()
    {
        var (card, window, generator) = Open();
        try
        {
            ImageGenerationDownloadEventArgs? args = null;
            card.DownloadRequested += (_, e) => args = e;
            FinishBatch(card, generator);
            Click(card.Tiles[1]);
            Click(Part(card, ImageGeneration.DownloadPart));
            args.ShouldNotBeNull();
            args.Index.ShouldBe(1);
            args.Image.Id.ShouldBe("img-1");
            args.FileName.ShouldBe("a-quiet-mountain-lake-at-golden-2-1024x1024.png");
            card.DownloadFaces!.ShownText.ShouldBe("Saved");
            AutomationProperties.GetName(Part(card, ImageGeneration.DownloadPart)).ShouldBe("Saved");
            card.Announcer!.Announcement.ShouldBe("Downloaded image 2");
            TestHelpers.PumpUntil(() => card.DownloadFaces.Shown == 0, 4000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Chips_And_Choose()
    {
        var (card, window, _) = Open();
        try
        {
            var models = Chips(card, ImageGeneration.ModelsPart);
            models[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            card.SelectedModelId.ShouldBe("image-b");
            models[1].IsChecked.ShouldBe(true);
            models[1].IsFocused.ShouldBeTrue();
            models[1].IsTabStop.ShouldBeTrue();
            models[0].IsTabStop.ShouldBeFalse();
            Press(window, PhysicalKey.ArrowLeft);
            Press(window, PhysicalKey.ArrowLeft);
            // Wraps to the last chip.
            card.SelectedModelId.ShouldBe("image-c");

            var aspects = Chips(card, ImageGeneration.AspectsPart);
            aspects[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.End);
            card.SelectedAspectId.ShouldBe("16:9");
            // Before the first batch the empty grid takes the chosen shape.
            card.GridPartControl!.Ratio.ShouldBe(756.0 / 1344, 0.0001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Tiles_And_Space_Picks()
    {
        var (card, window, generator) = Open();
        try
        {
            FinishBatch(card, generator);
            card.Tiles[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            card.Tiles[1].IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowDown);
            card.Tiles[3].IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowLeft);
            card.Tiles[2].IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Space);
            card.SelectedIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Enter_In_The_Prompt_Generates()
    {
        var (card, window, generator) = Open();
        try
        {
            var prompt = TestHelpers.Descendant<Textarea>(card, ImageGeneration.PromptPart);
            prompt.Text.ShouldBe(ImageGenerationTestData.Prompt);
            prompt.Text = "A city street at night";
            Pump();
            card.Prompt.ShouldBe("A city street at night");
            prompt.Focus();
            Press(window, PhysicalKey.Enter, RawInputModifiers.Control);
            card.IsGenerating.ShouldBeTrue();
            generator.Calls[0].Request.Prompt.ShouldBe("A city street at night");
            prompt.Text.ShouldBe("A city street at night");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Prompt_Does_Not_Generate()
    {
        var (card, window, generator) = Open(c => c.Prompt = "   ");
        try
        {
            card.Generate();
            Pump();
            generator.Calls.ShouldBeEmpty();
            card.IsGenerating.ShouldBeFalse();
            Part(card, ImageGeneration.GeneratePart).IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Suggestion_Fills_The_Prompt_And_Generates()
    {
        var (card, window, generator) = Open();
        try
        {
            var suggestion = card.SuggestionsPanel!.Children.OfType<AvaloniaButton>().First();
            Click(suggestion);
            card.Prompt.ShouldBe(ImageGenerationTestData.Suggestions[0]);
            generator.Calls.Count.ShouldBe(4);
            card.SuggestionsPanel.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Grid_Keeps_Its_Batch_Shape_When_The_Aspect_Changes()
    {
        var (card, window, generator) = Open(c => c.SelectedAspectId = "4:5");
        try
        {
            card.GridPartControl!.Ratio.ShouldBe(1120.0 / 896, 0.0001);
            FinishBatch(card, generator);
            card.SelectedAspectId = "16:9";
            Pump();
            card.GridPartControl.Ratio.ShouldBe(1120.0 / 896, 0.0001);
            // A portrait grid is capped at 420px and narrows, centred.
            card.GridPartControl.GridRect.Height.ShouldBe(420, 0.5);
            card.GridPartControl.GridRect.Width.ShouldBeLessThan(card.GridPartControl.Bounds.Width);
            Click(Part(card, ImageGeneration.GeneratePart));
            card.GridPartControl.Ratio.ShouldBe(756.0 / 1344, 0.0001);
            generator.Calls[^1].Request.Aspect.Id.ShouldBe("16:9");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Wrap_The_Chips_And_Stack_The_Footer()
    {
        var (card, window, _) = Open(width: 380);
        try
        {
            card.Classes.Contains(ImageGeneration.NarrowPseudoClass).ShouldBeTrue();
            card.Classes.Contains(ImageGeneration.StackedPseudoClass).ShouldBeTrue();
            Grid.GetRow(TestHelpers.Descendant<StackPanel>(card, ImageGeneration.AspectsPart)).ShouldBe(1);
            Grid.GetRow(TestHelpers.Descendant<Grid>(card, "PART_Actions")).ShouldBe(1);
            window.Width = 500;
            Pump();
            card.Classes.Contains(ImageGeneration.NarrowPseudoClass).ShouldBeFalse();
            card.Classes.Contains(ImageGeneration.StackedPseudoClass).ShouldBeTrue();
            window.Width = 700;
            Pump();
            card.Classes.Contains(ImageGeneration.StackedPseudoClass).ShouldBeFalse();
            Grid.GetRow(TestHelpers.Descendant<Grid>(card, "PART_Actions")).ShouldBe(0);
            card.Bounds.Width.ShouldBe(640, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_The_Card()
    {
        var (card, window, generator) = Open(c => c.Labels = new ImageGenerationLabels
        {
            Generate = "Créer",
            Stop = "Arrêter",
            Empty = "Écrivez une invite",
            Generating = (count, model) => $"Création de {count} images avec {model}",
        });
        try
        {
            card.GenerateFaces!.ShownText.ShouldBe("Créer");
            card.StatusText.ShouldBe("Écrivez une invite");
            TestHelpers.Descendant<Textarea>(card, ImageGeneration.PromptPart).Placeholder.ShouldBe("Describe the image you want");
            card.Placeholder = "Décrivez l'image";
            TestHelpers.Descendant<Textarea>(card, ImageGeneration.PromptPart).Placeholder.ShouldBe("Décrivez l'image");
            Click(Part(card, ImageGeneration.GeneratePart));
            card.GenerateFaces.ShownText.ShouldBe("Arrêter");
            card.StatusText.ShouldBe("Création de 4 images avec Image A");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_No_Generator_And_No_Simulation_Generate_Is_Unavailable()
    {
        var (card, window, _) = Open(c =>
        {
            c.Simulation = null;
            c.Generator = null;
        });
        try
        {
            card.CanGenerate.ShouldBeFalse();
            card.UsesSimulation.ShouldBeFalse();
            card.EffectiveModels.Count.ShouldBe(1);
            card.EffectiveModels[0].Name.ShouldBe(string.Empty);
            card.EffectiveSuggestions.ShouldBeEmpty();
            card.Prompt = "A lake";
            card.Generate();
            card.IsGenerating.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Generator_Wins_Over_The_Simulation()
    {
        var (card, window, generator) = Open();
        try
        {
            card.UsesSimulation.ShouldBeFalse();
            card.Generate();
            Pump();
            generator.Calls.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_Simulation_To_Null_Stops_Its_Run()
    {
        var card = new ImageGeneration { Simulation = ImageGenerationTestData.Simulation(), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(card);
        window.Width = 640;
        window.Height = 1000;
        try
        {
            TestHelpers.PumpUntil(() => card.IsGenerating, 3000).ShouldBeTrue();
            card.Simulation = null;
            Pump();
            card.IsGenerating.ShouldBeFalse();
            card.CanGenerate.ShouldBeFalse();
            card.Prompt.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulation_Generates_On_Its_Own_When_Shown()
    {
        var card = new ImageGeneration { Simulation = ImageGenerationTestData.Simulation(), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(card);
        window.Width = 640;
        window.Height = 1000;
        try
        {
            TestHelpers.PumpUntil(() => card.IsGenerating, 3000).ShouldBeTrue();
            card.UsesSimulation.ShouldBeTrue();
            card.Announcer!.Announcement.ShouldBe("Generating 4 images with Image A. Simulated results");
            card.Stop();
            card.IsGenerating.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_A_Generator_It_Does_Not_Generate_On_Its_Own()
    {
        var (card, window, generator) = Open();
        try
        {
            TestHelpers.PumpUntil(() => generator.Calls.Count > 0, 300).ShouldBeFalse();
            card.IsGenerating.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Auto_Run_Picks_Image_One_When_Untouched()
    {
        var (card, window, generator) = Open(c => c.AutoGenerate = true);
        try
        {
            TestHelpers.PumpUntil(() => generator.Calls.Count == 4, 3000).ShouldBeTrue();
            generator.FinishAll();
            Pump();
            card.SelectedIndex.ShouldBe(0);
            card.SelectedImage!.Id.ShouldBe("img-0");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Pure parts ----

    [Fact]
    public void Mosaic_Phases_Step_Through_Five_Levels_Then_The_Image()
    {
        ImageGenerationMosaic.Phase(0).ShouldBe((0, 0));
        ImageGenerationMosaic.Phase(0.09).Stage.ShouldBe(0);
        ImageGenerationMosaic.Phase(0.18).Stage.ShouldBe(1);
        ImageGenerationMosaic.Phase(0.5).Stage.ShouldBe(3);
        ImageGenerationMosaic.Phase(0.83).Stage.ShouldBe(5);
        ImageGenerationMosaic.Phase(1).ShouldBe((5, 1));
        ImageGenerationMosaic.Phase(double.NaN).ShouldBe((0, 0));
        ImageGenerationMosaic.Phase(2).ShouldBe((5, 1));
    }

    [Fact]
    public void Mosaic_Levels_Are_True_Averages_That_Halve()
    {
        // 64 × 64: the left half red, the right half blue, one green pixel in the top-left corner.
        const int size = 64;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = (y * size + x) * 4;
                pixels[i] = (byte)(x < size / 2 ? 0 : 255);
                pixels[i + 2] = (byte)(x < size / 2 ? 255 : 0);
                pixels[i + 3] = 255;
            }
        }
        var mosaic = ImageGenerationMosaic.Build(pixels, size, size, 1, seed: 7);
        mosaic.Levels.Select(l => (l.Columns, l.Rows)).ShouldBe([(4, 4), (8, 8), (16, 16), (32, 32), (64, 64)]);
        var coarse = mosaic.Levels[0];
        ((coarse.Colors[0] >> 16) & 0xFF).ShouldBe(255);
        (coarse.Colors[0] & 0xFF).ShouldBe(0);
        (coarse.Colors[3] & 0xFF).ShouldBe(255);
        ((coarse.Colors[3] >> 16) & 0xFF).ShouldBe(0);
        // A 16:9 shape keeps the block rows in step: 4 × 2 doubling to 64 × 32, cropped to the centre.
        var wide = ImageGenerationMosaic.Build(pixels, size, size, 9.0 / 16, seed: 7);
        wide.Levels.Select(l => (l.Columns, l.Rows)).ShouldBe([(4, 2), (8, 4), (16, 8), (32, 16), (64, 32)]);
        wide.Crop.Height.ShouldBe(36);
        wide.Crop.Y.ShouldBe(14);
    }

    [Fact]
    public void Coarse_Blocks_Fade_In_From_The_Centre()
    {
        var rank = ImageGenerationMosaic.Rank(4, 4, 3);
        rank.OrderBy(r => r).ShouldBe(Enumerable.Range(0, 16));
        var centre = new[] { 5, 6, 9, 10 }.Max(i => rank[i]);
        var corners = new[] { 0, 3, 12, 15 }.Min(i => rank[i]);
        centre.ShouldBeLessThan(corners);
        ImageGenerationMosaic.Rank(4, 4, 3).ShouldBe(rank);
    }

    [Fact]
    public void The_Grid_Keeps_Its_Shape_And_Caps_Its_Height()
    {
        ImageGenerationGrid.GridSize(608, 1, 8, 420).ShouldBe(new Size(420, 420));
        ImageGenerationGrid.GridSize(300, 1, 8, 420).ShouldBe(new Size(300, 300));
        var wide = ImageGenerationGrid.GridSize(608, 9.0 / 16, 8, 420);
        wide.Width.ShouldBe(608);
        wide.Height.ShouldBe(300 * 9.0 / 16 * 2 + 8, 0.001);
    }

    [Fact]
    public void File_Names_Come_From_The_Prompt_Number_And_Size()
    {
        ImageGeneration.FileName("A quiet mountain lake at golden hour, mist", 2, 1024, 1024)
            .ShouldBe("a-quiet-mountain-lake-at-golden-2-1024x1024.png");
        ImageGeneration.FileName("  Neon!! city  ", 1, 1344, 756).ShouldBe("neon-city-1-1344x756.png");
        ImageGeneration.FileName("", 4, 10, 20).ShouldBe("image-4-10x20.png");
    }

    [Fact]
    public void The_Simulation_Paints_With_Its_Painter_And_Its_Sizes_Keep_The_Shape()
    {
        var simulation = ImageGenerationTestData.Simulation();
        var pixels = simulation.Painter!("x", 42, 40, 30);
        pixels.Length.ShouldBe(40 * 30 * 4);
        simulation.Painter("x", 42, 40, 30).ShouldBe(pixels);
        ImageGenerationSimulation.PaintSize(1344, 756, 320).ShouldBe((320, 180));
        ImageGenerationSimulation.PaintSize(896, 1120, 320).ShouldBe((256, 320));
    }

    [Fact]
    public void The_Simulated_Progress_Stays_Inside_The_Range()
    {
        var previous = 0.0;
        for (var t = 0.0; t <= 1.0; t += 0.01)
        {
            var p = ImageGenerationSimulation.Curve(t, 1234);
            p.ShouldBeInRange(0, 0.99);
            previous = p;
        }
        previous.ShouldBeGreaterThan(0.9);
        var model = ImageGenerationTestData.Models[0];
        var aspect = new ImageGenerationAspect { Id = "1:1", Label = "1:1", Width = 1024, Height = 1024 };
        var a = ImageGenerationSimulation.Seed(new ImageGenerationRequest { Prompt = "x", Model = model, Aspect = aspect, Index = 0 });
        var b = ImageGenerationSimulation.Seed(new ImageGenerationRequest { Prompt = "x", Model = model, Aspect = aspect, Index = 1 });
        a.ShouldNotBe(b);
    }

    [AvaloniaFact]
    public void The_Prompt_Box_Is_Three_Lines_Tall()
    {
        var (card, window, _) = Open();
        try
        {
            var prompt = card.GetVisualDescendants().OfType<Textarea>().Single();
            prompt.FrameMinHeight.ShouldBe(85);
            prompt.GetVisualDescendants().OfType<FieldFrame>().First().Bounds.Height.ShouldBe(85, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>The invented generator data these tests play: a prompt, three models, three suggestions and a flat painter.</summary>
internal static class ImageGenerationTestData
{
    public const string Prompt = "A quiet mountain lake at golden hour, mist over the water, pines on the far shore, soft film grain";

    public static IReadOnlyList<ImageGenerationModel> Models { get; } =
    [
        new() { Id = "image-a", Name = "Image A", Mark = ArcIcons.Sparkles },
        new() { Id = "image-b", Name = "Image B", Mark = ArcIcons.Palette },
        new() { Id = "image-c", Name = "Image C", Mark = ArcIcons.Zap },
    ];

    public static IReadOnlyList<string> Suggestions { get; } =
    [
        "A ceramic coffee cup on a sunlit windowsill, soft morning shadows",
        "A neon city street after rain at night, reflections on the asphalt",
        "A cosy pastel reading nook with an arched window and a plant",
    ];

    /// <summary>Opaque BGRA that depends only on the seed and the position.</summary>
    public static ImageGenerationPainter Painter { get; } = (_, seed, width, height) =>
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4] = (byte)(seed + (uint)i);
            pixels[i * 4 + 1] = (byte)(i * 7);
            pixels[i * 4 + 2] = (byte)(i / Math.Max(1, width));
            pixels[i * 4 + 3] = 255;
        }
        return pixels;
    };

    public static ImageGenerationSimulation Simulation() => new()
    {
        Prompt = Prompt,
        Models = Models,
        Suggestions = Suggestions,
        Painter = Painter,
    };
}
