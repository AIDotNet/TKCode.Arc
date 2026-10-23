using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The measured motion of <see cref="AgentPlan"/>: spinner, check, wash, row springs, streaming, drag and reduced motion.</summary>
public class AgentPlanMotionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static AgentPlan Running(int count = 4)
    {
        var plan = new AgentPlan { Status = AgentPlanStatus.Running, Goal = "Goal", Model = new AgentModel("Model"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Width = 560 };
        for (var i = 1; i <= count; i++)
        {
            plan.Steps.Add(new AgentPlanStep("s" + i, "Step " + i));
        }
        plan.Steps[0].State = AgentPlanStepState.Active;
        return plan;
    }

    private static Window Open(AgentPlan plan, ArcMotionLevel level)
    {
        var window = TestHelpers.Show(plan, level);
        window.Width = 640;
        window.Height = 1000;
        Pump();
        return window;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Run(ManualFrameClock clock, double milliseconds)
    {
        var frames = (int)Math.Round(milliseconds / Frame.TotalMilliseconds);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Pump();
        }
    }

    private static IReadOnlyList<AgentPlanRow> Rows(AgentPlan plan) => plan.Rows.ToList();

    [AvaloniaFact]
    public void The_Active_Marks_Arc_Turns_450_Degrees_A_Second()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = Running();
        var window = Open(plan, ArcMotionLevel.Full);
        try
        {
            var glyph = Rows(plan)[0].Glyph;
            Assert.True(glyph.IsSpinning);
            var start = glyph.Angle;
            Run(clock, 400);
            var turned = (glyph.Angle - start + 360) % 360;
            Assert.InRange(turned, 170, 190);
            Assert.False(Rows(plan)[1].Glyph.IsSpinning);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Keeps_The_Arc_Still_And_Draws_The_Check_At_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = Running();
        var window = Open(plan, ArcMotionLevel.Reduced);
        try
        {
            var row = Rows(plan)[0];
            Assert.False(row.Glyph.IsSpinning);
            plan.Steps[0].Duration = TimeSpan.FromSeconds(3);
            plan.Steps[0].State = AgentPlanStepState.Done;
            Pump();
            Assert.Equal(AgentPlanGlyphKind.Done, row.Glyph.Kind);
            Assert.Equal(1.0, row.Glyph.CheckProgress);
            Assert.False(row.Title.IsShimmering);
            Assert.Equal("3s", row.Time.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Check_Draws_After_45ms_Over_200ms_And_The_Title_Color_Settles_In_240ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = Running();
        var window = Open(plan, ArcMotionLevel.Full);
        try
        {
            var row = Rows(plan)[0];
            var before = row.Title.CurrentColor;
            plan.Steps[0].Duration = TimeSpan.FromSeconds(3);
            plan.Steps[0].State = AgentPlanStepState.Done;
            Pump();
            Assert.Equal(0, row.Glyph.CheckProgress, 3);
            Run(clock, 30);
            Assert.True(row.Glyph.CheckProgress < 0.01, "still waiting for the 45ms delay");
            Run(clock, 90);
            Assert.InRange(row.Glyph.CheckProgress, 0.2, 0.99);
            Run(clock, 200);
            Assert.Equal(1.0, row.Glyph.CheckProgress, 3);
            Assert.NotEqual(before, row.Title.CurrentColor);
            var settled = row.Title.CurrentColor;
            Run(clock, 400);
            Assert.Equal(settled, row.Title.CurrentColor);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Inserted_Row_Springs_Open_Without_Overshoot_And_Its_Wash_Holds_Then_Fades_To_Nothing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = Running();
        var window = Open(plan, ArcMotionLevel.Full);
        try
        {
            plan.Steps.Insert(1, new AgentPlanStep("extra", "Inserted") { NoteKind = AgentPlanNoteKind.AddedByAgent });
            Pump();
            var row = Rows(plan)[1];
            var heights = new List<double>();
            var wash = new List<double>();
            for (var i = 0; i < 150; i++)
            {
                Run(clock, 1000.0 / 60);
                heights.Add(row.Bounds.Height);
                wash.Add(row.WashAmount);
            }
            var target = row.ContentHeight;
            Assert.True(target > 50, "a row with a note is 54 tall");
            Assert.True(heights[0] < target * 0.2, "starts from nothing");
            for (var i = 1; i < heights.Count; i++)
            {
                Assert.True(heights[i] >= heights[i - 1] - 0.51, $"monotonic at {i}");
                Assert.True(heights[i] <= target + 0.51, $"no overshoot at {i}");
            }
            Assert.Equal(target, heights[^1], 0.51);
            // About 250ms to within a pixel (critically damped, omega about 20).
            var withinOnePixel = heights.FindIndex(h => target - h <= 1) * Frame.TotalMilliseconds;
            Assert.InRange(withinOnePixel, 180, 330);
            // The wash is full while it holds (640ms), fading afterwards, gone at about 1.8s.
            Assert.True(wash[30] > 0.99, "holding at 500ms");
            Assert.InRange(wash[60], 0.01, 0.99);
            Assert.True(wash[^1] < 0.01, "gone");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Inserts_At_Once_Without_A_Wash()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = Running();
        var window = Open(plan, ArcMotionLevel.Reduced);
        try
        {
            plan.Steps.Insert(1, new AgentPlanStep("extra", "Inserted") { NoteKind = AgentPlanNoteKind.AddedByAgent });
            Pump();
            Run(clock, 50);
            var row = Rows(plan)[1];
            Assert.Equal(row.ContentHeight, row.Bounds.Height, 0.51);
            Assert.Equal(0, row.WashAmount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Activity_Line_Grows_The_Row_On_The_Layout_Spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = Running();
        var window = Open(plan, ArcMotionLevel.Full);
        try
        {
            var row = Rows(plan)[0];
            var before = row.Bounds.Height;
            plan.Steps[0].Activities.Add("Read 5 files");
            Pump();
            Run(clock, 60);
            var mid = row.Bounds.Height;
            Assert.True(mid > before && mid < before + 20, $"growing: {before} → {mid}");
            Run(clock, 600);
            Assert.Equal(before + 20, row.Bounds.Height, 0.51);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Moved_Row_Slides_On_A_Render_Transform_And_Keeps_Its_Focus()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = new AgentPlan { Status = AgentPlanStatus.Draft, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Width = 560 };
        for (var i = 1; i <= 3; i++)
        {
            plan.Steps.Add(new AgentPlanStep("s" + i, "Step " + i));
        }
        var window = Open(plan, ArcMotionLevel.Full);
        try
        {
            var rows = Rows(plan);
            rows[0].TitleBox.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Pump();
            Run(clock, 30);
            // The other row starts 36px away and is on its way (a spring: no relayout of the list).
            Assert.InRange(rows[1].Offset.Value, 1, 35);
            Run(clock, 700);
            Assert.Equal(0, rows[1].Offset.Value, 0.6);
            Assert.Same(rows[0].TitleBox, TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Suggestion_Streams_In_Chunks_Then_Becomes_The_Editable_Field()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = new AgentPlan { Status = AgentPlanStatus.Draft, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Width = 560 };
        plan.Steps.Add(new AgentPlanStep("a", "First"));
        plan.Steps.Add(new AgentPlanStep("b", "Last"));
        var window = Open(plan, ArcMotionLevel.Full);
        try
        {
            const string title = "Highlight every matched word in the result snippets so people find it fast";
            plan.StreamSuggestion(new AgentPlanStep("sug", title));
            Pump();
            Run(clock, 20);
            var row = Rows(plan)[1];
            Assert.True(row.IsStreaming);
            Assert.False(row.TitleBox.IsVisible);
            var first = row.Title.VisibleLength;
            Assert.InRange(first, 1, title.Length - 1);
            var heightWhileStreaming = row.Bounds.Height;
            Run(clock, 68);
            Assert.True(row.Title.VisibleLength > first, "a chunk every 34ms");
            // The row's height was settled from the full text: streaming never reflows it (only its own enter spring moves).
            Run(clock, 1500);
            Assert.False(row.IsStreaming);
            Assert.True(row.TitleBox.IsVisible);
            Assert.Equal(title, row.TitleBox.Text);
            Assert.Equal("Suggested", row.NoteText.Text);
            Assert.True(row.Bounds.Height >= heightWhileStreaming);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Count_Rolls_In_A_Reserved_Width_At_Full_Motion_And_Changes_In_Place_Otherwise()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plan = Running();
        var window = Open(plan, ArcMotionLevel.Full);
        try
        {
            var count = plan.HeadingCount;
            Assert.Equal("4", count.Text);
            plan.Steps.Add(new AgentPlanStep("x", "More"));
            Pump();
            Assert.Equal("5", count.Text);
            Assert.True(count.IsRolling);
            Run(clock, 400);
            Assert.False(count.IsRolling);
            var width = count.Bounds.Width;
            for (var i = 0; i < 5; i++)
            {
                plan.Steps.Add(new AgentPlanStep("y" + i, "More"));
            }
            Pump();
            Run(clock, 400);
            Assert.Equal("10", count.Text);
            Assert.True(count.Bounds.Width > width, "a second digit gets its own cell");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_A_Grip_Reorders_On_The_Pointer_And_Reports_The_Move_Once_On_Release()
    {
        var plan = new AgentPlan { Status = AgentPlanStatus.Draft, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Width = 560 };
        for (var i = 1; i <= 4; i++)
        {
            plan.Steps.Add(new AgentPlanStep("s" + i, "Step " + i));
        }
        var moves = new List<AgentPlanChangeKind>();
        plan.Changed += (_, e) => moves.Add(e.Kind);
        var window = Open(plan, ArcMotionLevel.Off);
        try
        {
            var grip = Rows(plan)[0].Grip;
            var from = grip.TranslatePoint(new Point(12, 14), window)!.Value;
            window.MouseMove(from, RawInputModifiers.None);
            window.MouseDown(from, Avalonia.Input.MouseButton.Left, RawInputModifiers.None);
            Pump();
            Assert.True(plan.List!.Dragged is not null);
            Assert.True(Rows(plan)[0].Grip.IsDragging);
            // 40px down: past the middle of the next row (18px) but not of the one after (54px).
            window.MouseMove(from + new Vector(0, 40), RawInputModifiers.LeftMouseButton);
            Pump();
            Assert.Equal(["s2", "s1", "s3", "s4"], plan.Steps.Select(s => s.Id));
            Assert.Empty(moves);
            window.MouseMove(from + new Vector(0, 80), RawInputModifiers.LeftMouseButton);
            Pump();
            Assert.Equal(["s2", "s3", "s1", "s4"], plan.Steps.Select(s => s.Id));
            // Back up to the first slot: it swaps back.
            window.MouseMove(from + new Vector(0, 0), RawInputModifiers.LeftMouseButton);
            Pump();
            Assert.Equal(["s1", "s2", "s3", "s4"], plan.Steps.Select(s => s.Id));
            window.MouseMove(from + new Vector(0, 100), RawInputModifiers.LeftMouseButton);
            Pump();
            window.MouseUp(from + new Vector(0, 100), Avalonia.Input.MouseButton.Left, RawInputModifiers.None);
            Pump();
            Assert.Null(plan.List!.Dragged);
            Assert.Equal(["s2", "s3", "s4", "s1"], plan.Steps.Select(s => s.Id));
            Assert.Equal([AgentPlanChangeKind.Move], moves);
            Assert.Equal("s1", Rows(plan)[3].Step.Id);
            Assert.False(Rows(plan)[3].Grip.IsDragging);
            Assert.Equal(0, Rows(plan)[3].Offset.Value, 0.51);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Drag_Dropped_In_Place_Reports_Nothing()
    {
        var plan = new AgentPlan { Status = AgentPlanStatus.Draft, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Width = 560 };
        for (var i = 1; i <= 3; i++)
        {
            plan.Steps.Add(new AgentPlanStep("s" + i, "Step " + i));
        }
        var moves = 0;
        plan.Changed += (_, _) => moves++;
        var window = Open(plan, ArcMotionLevel.Off);
        try
        {
            var from = Rows(plan)[1].Grip.TranslatePoint(new Point(12, 14), window)!.Value;
            window.MouseMove(from, RawInputModifiers.None);
            window.MouseDown(from, Avalonia.Input.MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(from + new Vector(0, 8), RawInputModifiers.LeftMouseButton);
            window.MouseUp(from + new Vector(0, 8), Avalonia.Input.MouseButton.Left, RawInputModifiers.None);
            Pump();
            Assert.Equal(0, moves);
            Assert.Equal(["s1", "s2", "s3"], plan.Steps.Select(s => s.Id));
        }
        finally
        {
            window.Close();
        }
    }
}
