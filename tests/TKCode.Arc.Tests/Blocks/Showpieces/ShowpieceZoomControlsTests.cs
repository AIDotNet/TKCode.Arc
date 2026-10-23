using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceZoomControls"/>: geometry, readout, Fit at 1.0, commands and stage wiring.</summary>
public class ShowpieceZoomControlsTests
{
    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Calls { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Calls.Add(parameter);
    }

    private static ShowpieceZoomControls Bar(bool readout = false) => new()
    {
        ShowReadout = readout,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };

    [AvaloniaFact]
    public void Bars_Have_The_Measured_Sizes()
    {
        var galaxy = Bar();
        var semantic = Bar(readout: true);
        var compact = new ShowpieceZoomControls { ShowReadout = true, IsCompact = true, HorizontalAlignment = HorizontalAlignment.Left };
        var window = TestHelpers.Show(new StackPanel { Children = { galaxy, semantic, compact } });
        try
        {
            galaxy.Bounds.Width.ShouldBe(94, 0.01);
            galaxy.Bounds.Height.ShouldBe(34, 0.01);
            semantic.Bounds.Width.ShouldBe(142, 0.01);
            compact.Bounds.Width.ShouldBe(134, 0.01);
            var zoomIn = TestHelpers.Descendant<ShowpieceZoomButton>(semantic, ShowpieceZoomControls.ZoomInPart);
            zoomIn.Bounds.Width.ShouldBe(28, 0.01);
            AutomationProperties.GetName(zoomIn).ShouldBe("Zoom in");
            AutomationProperties.GetName(TestHelpers.Descendant<ShowpieceZoomButton>(semantic, ShowpieceZoomControls.FitPart)).ShouldBe("Fit to view");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Readout_Uses_One_Decimal_Under_Ten()
    {
        var bar = Bar(readout: true);
        var window = TestHelpers.Show(bar);
        try
        {
            var readout = TestHelpers.Descendant<TextBlock>(bar, "PART_Readout");
            readout.Text.ShouldBe("1.0×");
            bar.Zoom = 2.43;
            readout.Text.ShouldBe("2.4×");
            bar.Zoom = 53.04;
            bar.ReadoutText.ShouldBe("53×");
            readout.Text.ShouldBe("53×");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fit_Is_Disabled_At_One()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        try
        {
            var fit = TestHelpers.Descendant<ShowpieceZoomButton>(bar, ShowpieceZoomControls.FitPart);
            fit.IsEnabled.ShouldBeFalse();
            bar.CanFit.ShouldBeFalse();
            bar.Classes.Contains(ShowpieceZoomControls.AtFitPseudoClass).ShouldBeTrue();
            bar.Zoom = 1.6;
            fit.IsEnabled.ShouldBeTrue();
            // The block may say explicitly (Customer galaxy: at fit means zoom 1 and centered).
            bar.IsAtFit = true;
            fit.IsEnabled.ShouldBeFalse();
            bar.IsAtFit = false;
            bar.Zoom = 1;
            fit.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Buttons_Run_Commands_With_The_Step()
    {
        var zoomIn = new RecordingCommand();
        var zoomOut = new RecordingCommand();
        var fitCommand = new RecordingCommand();
        var bar = Bar();
        bar.ZoomInCommand = zoomIn;
        bar.ZoomOutCommand = zoomOut;
        bar.FitCommand = fitCommand;
        var window = TestHelpers.Show(bar);
        try
        {
            bar.Request(ShowpieceZoomAction.ZoomIn);
            zoomIn.Calls.ShouldBe(new object?[] { 1.6 });
            bar.Request(ShowpieceZoomAction.ZoomOut);
            ((double)zoomOut.Calls[0]!).ShouldBe(0.625, 1e-12);
            // Fit does nothing while at fit.
            bar.Request(ShowpieceZoomAction.Fit);
            fitCommand.Calls.ShouldBeEmpty();
            bar.Zoom = 3;
            bar.Request(ShowpieceZoomAction.Fit);
            fitCommand.Calls.Count.ShouldBe(1);

            // A real button press (Enter on the focused button) goes the same way.
            var plus = TestHelpers.Descendant<ShowpieceZoomButton>(bar, ShowpieceZoomControls.ZoomInPart);
            plus.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            zoomIn.Calls.Count.ShouldBe(2);

            // A handled event stops the default.
            bar.ZoomRequested += (_, e) => e.Handled = true;
            bar.Request(ShowpieceZoomAction.ZoomIn);
            zoomIn.Calls.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Commands_The_Bar_Drives_Its_Stage()
    {
        var camera = new ShowpieceCamera2D { MaxZoom = 9 };
        camera.SetContent(new Rect(0, 0, 400, 300), fitScale: 1);
        var stage = new ShowpieceStageTests.TestStage(camera);
        var bar = Bar();
        bar.Stage = stage;
        var window = TestHelpers.Show(new Panel { Children = { stage, bar } });
        try
        {
            bar.Request(ShowpieceZoomAction.ZoomIn);
            camera.Zoom.ShouldBe(1.6, 1e-9);
            bar.Zoom = camera.Zoom;
            bar.Request(ShowpieceZoomAction.Fit);
            camera.Zoom.ShouldBe(1, 1e-9);
            stage.Log.ShouldContain("reset");
        }
        finally
        {
            window.Close();
        }
    }
}
