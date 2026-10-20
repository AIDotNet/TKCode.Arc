using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Editors;

/// <summary>The editors' motion at the Full and Reduced levels, frame by frame on the manual clock.</summary>
public class EditorsMotionTests
{
    private static void Frames(ManualFrameClock clock, int count, int step = 16)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(step));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Enters_Glides_And_Morphs_To_The_Link_Face()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var editor = new RichTextEditor { Width = 480, Markdown = "first words here and more words over there" };
            var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 90, 20, 20), Child = editor }, ArcMotionLevel.Full);
            try
            {
                Frames(clock, 3);
                editor.FocusEditor();
                editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 0, 5));
                Dispatcher.UIThread.RunJobs();
                var toolbar = editor.Toolbar!;
                toolbar.IsShown.ShouldBeTrue();
                toolbar.Opacity.ShouldBeLessThan(1);
                Frames(clock, 30);
                toolbar.Opacity.ShouldBe(1, 0.01);
                var before = toolbar.RenderTransform!.Value;

                // A new selection glides the same surface instead of reopening it.
                editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 30, 41));
                Dispatcher.UIThread.RunJobs();
                Frames(clock, 2);
                var moving = toolbar.RenderTransform!.Value;
                Frames(clock, 40);
                var after = toolbar.RenderTransform!.Value;
                moving.M31.ShouldBeGreaterThan(before.M31);
                after.M31.ShouldBeGreaterThan(moving.M31);

                editor.EditLink();
                Dispatcher.UIThread.RunJobs();
                toolbar.IsLinkFace.ShouldBeTrue();
                Frames(clock, 40);
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Frames(clock, 40);
                toolbar.IsLinkFace.ShouldBeFalse();

                editor.Engine.SetSelection(RichTextEditorSelection.Caret(0, 2));
                Dispatcher.UIThread.RunJobs();
                Frames(clock, 20);
                toolbar.IsShown.ShouldBeFalse();
                toolbar.IsVisible.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Slash_Menu_Grows_And_Its_Highlight_Glides()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var editor = new RichTextEditor { Width = 480 };
            var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = editor }, ArcMotionLevel.Full);
            try
            {
                Frames(clock, 3);
                editor.FocusEditor();
                Dispatcher.UIThread.RunJobs();
                window.KeyTextInput("/");
                Dispatcher.UIThread.RunJobs();
                Frames(clock, 30);
                var menu = editor.SlashMenu!;
                menu.IsShown.ShouldBeTrue();
                window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
                window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Frames(clock, 30);
                menu.ActiveIndex.ShouldBe(2);
                window.KeyTextInput("q");
                Dispatcher.UIThread.RunJobs();
                Frames(clock, 40);
                menu.Items.Count.ShouldBe(1);
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Frames(clock, 20);
                editor.Engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Quote);
                menu.IsVisible.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Signature_Wipe_Clears_After_The_Sweep_And_Replay_Runs_Its_Timeline()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var pad = new SignaturePad { Width = 600 };
            var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = pad }, ArcMotionLevel.Full);
            try
            {
                Frames(clock, 3);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var canvas = pad.Pad!;
                window.MouseDown(canvas.TranslatePoint(new Point(100, 120), window)!.Value, MouseButton.Left);
                window.MouseMove(canvas.TranslatePoint(new Point(200, 140), window)!.Value);
                window.MouseMove(canvas.TranslatePoint(new Point(300, 120), window)!.Value);
                window.MouseUp(canvas.TranslatePoint(new Point(300, 120), window)!.Value, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                pad.Strokes.Count.ShouldBe(1);

                pad.Replay();
                pad.IsReplaying.ShouldBeTrue();
                pad.Classes.ShouldContain(":replaying");
                Frames(clock, 2);
                canvas.Ink.Replay.ShouldNotBeNull();
                Frames(clock, 60);
                pad.IsReplaying.ShouldBeFalse();
                pad.Status!.Message.ShouldBe("Replay finished");

                pad.Clear();
                pad.IsWiping.ShouldBeTrue();
                pad.Strokes.Count.ShouldBe(1);
                Frames(clock, 10);
                canvas.Ink.Wipe.ShouldNotBeNull();
                Frames(clock, 40);
                pad.IsWiping.ShouldBeFalse();
                pad.Strokes.Count.ShouldBe(0);
                canvas.Ink.Wipe.ShouldBeNull();
                Frames(clock, 30);
                canvas.IsHintShown.ShouldBeTrue();
                canvas.HintText.Opacity.ShouldBe(1, 0.01);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Clears_Without_A_Wipe()
    {
        var pad = new SignaturePad { Width = 600 };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = pad }, ArcMotionLevel.Reduced);
        try
        {
            pad.Reset(new[] { new InkStroke("s", new[] { new InkPoint(10, 10, -1, 0), new InkPoint(90, 40, -1, 50) }, SignatureInkColor.Black, SignatureInkWidth.Medium, 0) });
            pad.Clear();
            pad.IsWiping.ShouldBeFalse();
            pad.Strokes.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Dropzone_Rows_Drop_In_And_Fold_Away()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var zone = new FileDropzone { Width = 520 };
            var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = zone }, ArcMotionLevel.Full);
            try
            {
                Frames(clock, 3);
                await zone.AddFilesAsync(new[] { await DropzoneFixtures.File(window, "motion-a.pdf", 10), await DropzoneFixtures.File(window, "motion-b.pdf", 10) });
                Dispatcher.UIThread.RunJobs();
                var host = zone.List.Children.OfType<FileDropzoneRowHost>().First();
                Frames(clock, 2);
                host.Bounds.Height.ShouldBeLessThan(40);
                Frames(clock, 60);
                host.Bounds.Height.ShouldBeGreaterThan(60);
                host.Row.Opacity.ShouldBe(1, 0.01);

                zone.Remove(zone.Items[0]);
                Dispatcher.UIThread.RunJobs();
                zone.List.Children.Count.ShouldBe(2);
                Frames(clock, 60);
                zone.List.Children.Count.ShouldBe(1);

                zone.SetDragOver(true);
                Frames(clock, 80);
                zone.SetDragOver(false);
                Frames(clock, 20);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
