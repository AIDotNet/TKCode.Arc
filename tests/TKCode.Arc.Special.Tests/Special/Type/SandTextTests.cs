using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>Sand text: the sand field (pour, automaton, digging, healing, text changes) and the control around it.</summary>
public class SandTextTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private const int Columns = 40;
    private const int Rows = 30;

    private static bool[] Mask(Func<int, int, bool> letter)
    {
        var mask = new bool[Columns * Rows];
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                mask[row * Columns + column] = letter(column, row);
            }
        }
        return mask;
    }

    // A block letter in the middle of the grid, with room for piles below it.
    private static readonly bool[] Block = Mask((c, r) => c >= 10 && c < 30 && r >= 8 && r < 18);

    private static SandTextField Field(bool[]? formed = null)
    {
        var field = new SandTextField();
        field.Resize(Columns, Rows);
        if (formed is not null)
        {
            field.SetFormed(formed);
        }
        return field;
    }

    private static int Run(SandTextField field, int maxFrames, Func<bool>? until = null)
    {
        for (var frame = 1; frame <= maxFrames; frame++)
        {
            field.Step();
            if (until is not null && until())
            {
                return frame;
            }
        }
        return maxFrames;
    }

    // --- field ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Formed_letters_fill_every_letter_cell_and_nothing_else()
    {
        var field = Field(Block);
        field.MaskCount.ShouldBe(200);
        field.StuckCount.ShouldBe(200);
        field.LooseCount.ShouldBe(0);
        field.IsActive.ShouldBeFalse();
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                (field.State(column, row) == SandTextField.Stuck).ShouldBe(field.IsLetter(column, row));
            }
        }
    }

    [Fact]
    public void The_pour_fills_bottom_rows_first_and_reports_the_letters_whole_once()
    {
        var field = Field();
        field.SetPoured(Block, SandTextField.ChangeWindow);
        var filled = new int[Rows];
        var frames = Run(field, 200, () =>
        {
            for (var row = 8; row < 18; row++)
            {
                if (filled[row] == 0 && field.State(15, row) == SandTextField.Stuck)
                {
                    filled[row] = 1 + Enumerable.Range(0, Rows).Count(r => filled[r] > 0);
                }
            }
            return field.PouringCount == 0;
        });
        frames.ShouldBeLessThan(90);
        field.StuckCount.ShouldBe(field.MaskCount);
        filled[17].ShouldBeLessThan(filled[8]);
        field.TakeGathered().ShouldBeTrue();
        field.TakeGathered().ShouldBeFalse();
    }

    [Fact]
    public void Pour_grains_fall_through_the_letter_above_to_reach_their_cell()
    {
        // Two bars in the same columns; the lower one is dug out and refilled through the intact upper one.
        var bars = Mask((c, r) => c >= 10 && c < 20 && ((r >= 4 && r < 7) || (r >= 14 && r < 17)));
        var field = Field(bars);
        field.Dig(15, 15.5, 6, 0, 0).ShouldBeGreaterThan(0);
        var upper = Enumerable.Range(10, 10).All(c => field.State(c, 5) == SandTextField.Stuck);
        upper.ShouldBeTrue();
        Run(field, 400, () => !field.IsActive);
        field.StuckCount.ShouldBe(field.MaskCount);
        field.LooseCount.ShouldBe(0);
    }

    [Fact]
    public void Loose_sand_falls_two_cells_a_frame()
    {
        var field = Field(Mask((c, r) => c == 5 && r == 0));
        field.Retarget(new bool[Columns * Rows], 0);
        field.State(5, 0).ShouldBe(SandTextField.Loose);
        field.Step();
        field.State(5, 2).ShouldBe(SandTextField.Loose);
        field.Step();
        field.State(5, 4).ShouldBe(SandTextField.Loose);
    }

    [Fact]
    public void Old_letters_collapse_into_a_sloped_pile_that_then_disappears()
    {
        var column = Mask((c, r) => c >= 18 && c < 22 && r < 20);
        var field = Field(column);
        field.Retarget(new bool[Columns * Rows], 0);
        field.LooseCount.ShouldBe(80);
        Run(field, 40);
        // At rest: every loose grain stands on something, and the pile has spread wider than the column.
        var left = Columns;
        var right = -1;
        for (var row = 0; row < Rows; row++)
        {
            for (var c = 0; c < Columns; c++)
            {
                if (field.State(c, row) != SandTextField.Loose)
                {
                    continue;
                }
                left = Math.Min(left, c);
                right = Math.Max(right, c);
                if (row < Rows - 1)
                {
                    field.State(c, row + 1).ShouldNotBe(SandTextField.Empty);
                }
            }
        }
        (right - left + 1).ShouldBeGreaterThan(8);
        Run(field, 400, () => !field.IsActive).ShouldBeLessThan(200);
        field.LooseCount.ShouldBe(0);
    }

    [Fact]
    public void Digging_throws_only_the_grains_within_the_radius()
    {
        var field = Field(Block);
        var thrown = field.Dig(20, 12, 3, 0, 0);
        var expected = 0;
        for (var row = 8; row < 18; row++)
        {
            for (var c = 10; c < 30; c++)
            {
                var dx = c + 0.5 - 20;
                var dy = row + 0.5 - 12;
                if (dx * dx + dy * dy <= 9)
                {
                    expected++;
                    field.State(c, row).ShouldBe(SandTextField.Empty);
                }
            }
        }
        thrown.ShouldBe(expected);
        field.FlyingCount.ShouldBe(expected);
        field.StuckCount.ShouldBe(200 - expected);
    }

    [Fact]
    public void Dug_letters_heal_once_the_sand_rests_and_the_spill_disappears()
    {
        var field = Field(Block);
        field.Dig(20, 12, 4, 8, 0);
        field.IsResting.ShouldBeFalse();
        Run(field, SandTextField.RestFrames - 2);
        field.StuckCount.ShouldBeLessThan(field.MaskCount);
        var frames = Run(field, 600, () => !field.IsActive);
        frames.ShouldBeLessThan(400);
        field.StuckCount.ShouldBe(field.MaskCount);
        field.LooseCount.ShouldBe(0);
        field.FlyingCount.ShouldBe(0);
        field.TakeGathered().ShouldBeTrue();
    }

    [Fact]
    public void A_new_text_keeps_grains_already_on_its_letters_and_pours_the_rest()
    {
        var field = Field(Block);
        var shifted = Mask((c, r) => c >= 20 && c < 36 && r >= 8 && r < 18);
        field.Retarget(shifted, SandTextField.ChangeWindow);
        field.StuckCount.ShouldBe(100);
        field.LooseCount.ShouldBe(100);
        field.State(25, 10).ShouldBe(SandTextField.Stuck);
        Run(field, 600, () => !field.IsActive).ShouldBeLessThan(400);
        field.MaskCount.ShouldBe(160);
        field.StuckCount.ShouldBe(160);
        field.LooseCount.ShouldBe(0);
    }

    [Fact]
    public void Dissolving_lets_every_grain_go_and_gathering_pours_them_back()
    {
        var field = Field(Block);
        field.SetDissolved(true, animate: true);
        field.StuckCount.ShouldBe(0);
        field.TakeDissolved().ShouldBeFalse();
        Run(field, 600, () => !field.IsActive).ShouldBeLessThan(400);
        field.LooseCount.ShouldBe(0);
        field.TakeDissolved().ShouldBeTrue();
        field.TakeDissolved().ShouldBeFalse();
        field.SetDissolved(false, animate: true);
        Run(field, 400, () => !field.IsActive);
        field.StuckCount.ShouldBe(200);
        field.TakeGathered().ShouldBeTrue();
    }

    [Fact]
    public void Settling_ends_all_motion_with_the_letters_whole()
    {
        var field = Field(Block);
        field.Dig(20, 12, 5, 0, 0);
        field.Step();
        field.Settle();
        field.StuckCount.ShouldBe(200);
        field.LooseCount.ShouldBe(0);
        field.FlyingCount.ShouldBe(0);
        field.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Grains_take_one_of_five_shades_eighteen_apart()
    {
        var colors = new uint[256];
        SandTextField.BuildColors([(100, 150, 245)], colors);
        uint Rgb(int r, int g, int b) => 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        colors[0].ShouldBe(Rgb(64, 114, 209));
        colors[1].ShouldBe(Rgb(82, 132, 227));
        colors[2].ShouldBe(Rgb(100, 150, 245));
        colors[3].ShouldBe(Rgb(118, 168, 255));
        colors[4].ShouldBe(Rgb(136, 186, 255));
        // A shade past the palette wraps to it.
        colors[7].ShouldBe(colors[2]);
    }

    [Fact]
    public void A_frame_allocates_nothing()
    {
        var field = Field(Block);
        field.Dig(20, 12, 4, 6, -2);
        field.Retarget(Mask((c, r) => c >= 5 && c < 25 && r >= 6 && r < 16), SandTextField.ChangeWindow);
        var pixels = new uint[Columns * Rows];
        var colors = new uint[256];
        field.Step();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 120; i++)
        {
            field.Step();
            field.Paint(pixels, colors);
        }
        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
    }

    // --- control ---------------------------------------------------------------------------------------------------

    private static (SandText Text, Window Window) Show(ArcMotionLevel level = ArcMotionLevel.Off, string text = "Sand")
    {
        var sand = new SandText { Text = text, FontSize = 40, VerticalAlignment = VerticalAlignment.Top };
        var host = new Border { Width = 400, Child = sand, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = TestHelpers.Show(host, level);
        return (sand, window);
    }

    [AvaloniaFact]
    public void Fills_its_width_and_is_one_and_a_half_em_tall_with_room_for_piles()
    {
        var (sand, window) = Show();
        sand.Bounds.Width.ShouldBe(400);
        sand.Bounds.Height.ShouldBe(62, 0.6);
        sand.FontSizeRatio = 0.2;
        window.UpdateLayout();
        sand.Bounds.Height.ShouldBe(124, 0.6);
        sand.Field.Columns.ShouldBe(200);
        sand.Field.Rows.ShouldBe(62);
        sand.Field.MaskCount.ShouldBeGreaterThan(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Reads_as_a_heading_named_by_its_text_and_takes_no_focus()
    {
        var (sand, window) = Show();
        sand.Focusable.ShouldBeFalse();
        var peer = ControlAutomationPeer.CreatePeerForElement(sand);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        peer.GetName().ShouldBe("Sand");
        AutomationProperties.GetHeadingLevel(sand).ShouldBe(2);
        sand.HeadingLevel = 1;
        AutomationProperties.GetHeadingLevel(sand).ShouldBe(1);
        sand.Text = "Dust";
        peer.GetName().ShouldBe("Dust");
        window.Close();
    }

    [AvaloniaFact]
    public void Without_full_motion_a_new_text_and_dissolving_happen_at_once()
    {
        var (sand, window) = Show(ArcMotionLevel.Reduced);
        var dissolved = 0;
        var gathered = 0;
        sand.Dissolved += (_, _) => dissolved++;
        sand.Gathered += (_, _) => gathered++;
        sand.Field.StuckCount.ShouldBe(sand.Field.MaskCount);
        sand.Text = "Dust storm";
        window.UpdateLayout();
        sand.Field.StuckCount.ShouldBe(sand.Field.MaskCount);
        sand.Field.IsActive.ShouldBeFalse();
        sand.Dissolve();
        sand.Progress.ShouldBe(1);
        dissolved.ShouldBe(1);
        sand.Classes.Contains(":dissolved").ShouldBeTrue();
        sand.Gather();
        sand.Progress.ShouldBe(0);
        gathered.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_does_not_dig()
    {
        var (sand, window) = Show(ArcMotionLevel.Reduced);
        sand.Dig(new Point(200, 40), 100);
        sand.Field.FlyingCount.ShouldBe(0);
        sand.Progress.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Under_full_motion_the_text_pours_in_digs_and_heals()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (sand, window) = Show(ArcMotionLevel.Full);
        var gathered = 0;
        sand.Gathered += (_, _) => gathered++;
        sand.Progress.ShouldBe(1);
        frames.Advance(TimeSpan.FromSeconds(0.4), Frame);
        sand.Progress.ShouldBeInRange(0.01, 0.99);
        frames.Advance(TimeSpan.FromSeconds(1.2), Frame);
        sand.Progress.ShouldBe(0);
        gathered.ShouldBe(1);

        var rect = sand.Field;
        var center = FirstLetterCell(sand);
        sand.Dig(center, 1000);
        sand.Progress.ShouldBeGreaterThan(0);
        frames.Advance(TimeSpan.FromSeconds(5), Frame);
        sand.Progress.ShouldBe(0);
        rect.LooseCount.ShouldBe(0);
        gathered.ShouldBe(2);
        sand.Loop.IsRunning.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Under_full_motion_dissolving_ends_with_nothing_left_and_one_event()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (sand, window) = Show(ArcMotionLevel.Full);
        frames.Advance(TimeSpan.FromSeconds(2), Frame);
        var dissolved = 0;
        sand.Dissolved += (_, _) => dissolved++;
        sand.Dissolve();
        sand.Progress.ShouldBe(1);
        frames.Advance(TimeSpan.FromSeconds(0.2), Frame);
        dissolved.ShouldBe(0);
        frames.Advance(TimeSpan.FromSeconds(6), Frame);
        dissolved.ShouldBe(1);
        sand.Field.LooseCount.ShouldBe(0);
        sand.Dig(FirstLetterCell(sand), 100);
        sand.Field.FlyingCount.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void A_first_state_that_is_dissolved_shows_nothing_and_raises_nothing()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var sand = new SandText { Text = "Gone", IsDissolved = true };
        var dissolved = 0;
        sand.Dissolved += (_, _) => dissolved++;
        var window = TestHelpers.Show(new Border { Width = 300, Child = sand }, ArcMotionLevel.Full);
        frames.Advance(TimeSpan.FromSeconds(0.5), Frame);
        sand.Progress.ShouldBe(1);
        sand.Field.StuckCount.ShouldBe(0);
        dissolved.ShouldBe(0);
        window.Close();
    }

    private static Point FirstLetterCell(SandText sand)
    {
        var field = sand.Field;
        for (var row = 0; row < field.Rows; row++)
        {
            for (var column = 0; column < field.Columns; column++)
            {
                if (field.IsLetter(column, row))
                {
                    return new Point(sand.GridOrigin.X + (column + 0.5) * sand.Grain, sand.GridOrigin.Y + (row + 0.5) * sand.Grain);
                }
            }
        }
        throw new InvalidOperationException("no letters");
    }

#pragma warning disable CS0618
    [AvaloniaFact]
    public void Obsolete_trigger_forwards_to_digging_and_the_other_old_settings_keep_their_values()
    {
        var (sand, window) = Show();
        sand.Trigger.ShouldBe(SandTextTrigger.Click);
        sand.IsDigEnabled.ShouldBeTrue();
        sand.Trigger = SandTextTrigger.None;
        sand.IsDigEnabled.ShouldBeFalse();
        sand.Trigger = SandTextTrigger.Hover;
        sand.IsDigEnabled.ShouldBeTrue();
        sand.Drift.ShouldBe(SandTextDrift.Right);
        sand.Duration.ShouldBe(TimeSpan.FromSeconds(1.6));
        sand.Distance.ShouldBe(110);
        sand.MaxGrains.ShouldBe(4000);
        sand.Drift = SandTextDrift.Up;
        sand.MaxGrains = 10;
        sand.Drift.ShouldBe(SandTextDrift.Up);
        sand.Field.StuckCount.ShouldBe(sand.Field.MaskCount);
        window.Close();
    }
#pragma warning restore CS0618
}
