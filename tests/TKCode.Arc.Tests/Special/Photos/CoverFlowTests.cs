using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Photos;

/// <summary>The cover flow: its perspective arithmetic, then the control's covers, selection, keyboard, hit testing and events.</summary>
public class CoverFlowTests
{
    // --- perspective ---------------------------------------------------------------------------------------------------

    [Fact]
    public void An_unturned_cover_is_drawn_where_it_is_laid_out()
    {
        var m = CoverFlowProjection.CoverMatrix(200, 200, 0, 1, 0, 900);
        CoverFlowProjection.Apply(m, new Point(0, 0)).ShouldBe(new Point(0, 0));
        CoverFlowProjection.Apply(m, new Point(200, 200)).ShouldBe(new Point(200, 200));
    }

    [Fact]
    public void A_cover_turned_away_on_the_right_has_its_far_edge_shorter_and_nearer_the_middle()
    {
        var m = CoverFlowProjection.CoverMatrix(200, 200, 0, 1, 50, 900);
        var leftTop = CoverFlowProjection.Apply(m, new Point(0, 0));
        var leftBottom = CoverFlowProjection.Apply(m, new Point(0, 200));
        var rightTop = CoverFlowProjection.Apply(m, new Point(200, 0));
        var rightBottom = CoverFlowProjection.Apply(m, new Point(200, 200));
        // The right edge went away from the viewer: it is shorter than the left edge, which came closer.
        (rightBottom.Y - rightTop.Y).ShouldBeLessThan(leftBottom.Y - leftTop.Y);
        (leftBottom.Y - leftTop.Y).ShouldBeGreaterThan(200);
        // And the cover is narrower on screen.
        (rightTop.X - leftTop.X).ShouldBeLessThan(200);
        // The vertical centre line stays put.
        CoverFlowProjection.Apply(m, new Point(100, 100)).X.ShouldBe(100, 1e-6);
    }

    [Fact]
    public void Unprojecting_a_drawn_point_finds_the_point_of_the_cover()
    {
        var m = CoverFlowProjection.CoverMatrix(180, 240, 130, 0.86, -55, 900);
        var local = new Point(37, 201);
        var screen = CoverFlowProjection.Apply(m, local);
        CoverFlowProjection.TryUnproject(m, screen, out var back).ShouldBeTrue();
        back.X.ShouldBe(local.X, 1e-6);
        back.Y.ShouldBe(local.Y, 1e-6);
    }

    [Fact]
    public void Covers_blend_from_the_centre_to_their_side_pose()
    {
        var centre = CoverFlowProjection.Pose(0, 200, 0.6, 50, 55, 0.8);
        centre.ShouldBe((0d, 0d, 1d));
        var half = CoverFlowProjection.Pose(0.5, 200, 0.6, 50, 55, 0.8);
        half.Yaw.ShouldBe(27.5, 1e-9);
        half.OffsetX.ShouldBe(60, 1e-9);
        var far = CoverFlowProjection.Pose(-3, 200, 0.6, 50, 55, 0.8);
        far.OffsetX.ShouldBe(-(120 + 100), 1e-9);
        far.Yaw.ShouldBe(-55, 1e-9);
        far.Scale.ShouldBe(0.8, 1e-9);
    }

    // --- control -------------------------------------------------------------------------------------------------------

    private static CoverFlow Flow(int count = 6)
    {
        var flow = new CoverFlow { Width = 400, CoverWidth = 120 };
        for (var i = 0; i < count; i++)
        {
            flow.Items.Add(new CoverFlowItem { Source = PhotosTestPictures.Picture(20, 20), Title = $"Album {i}", Subtitle = $"Artist {i}" });
        }
        return flow;
    }

    [AvaloniaFact]
    public void The_first_cover_is_centred_and_announced()
    {
        var flow = Flow();
        var window = TestHelpers.Show(flow);
        try
        {
            flow.SelectedIndex.ShouldBe(0);
            flow.SelectedItem!.Title.ShouldBe("Album 0");
            flow.Stage!.Covers.Count.ShouldBe(6);
            AutomationProperties.GetHelpText(flow).ShouldBe("Album 0, Artist 0, 1 of 6");
            AutomationProperties.GetName(flow).ShouldBe("Covers");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_page_keys_and_home_and_end_move_the_row()
    {
        var flow = Flow(12);
        var window = TestHelpers.Show(flow);
        try
        {
            flow.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            flow.SelectedIndex.ShouldBe(1);
            flow.Stage!.Position.ShouldBe(1, 1e-9);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            flow.SelectedIndex.ShouldBe(6);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            flow.SelectedIndex.ShouldBe(11);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            flow.SelectedIndex.ShouldBe(11);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            flow.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_opens_the_centred_cover()
    {
        var flow = Flow();
        flow.SelectedIndex = 2;
        CoverFlowItemInvokedEventArgs? invoked = null;
        flow.ItemInvoked += (_, e) => invoked = e;
        var window = TestHelpers.Show(flow);
        try
        {
            flow.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldNotBeNull();
            invoked.Index.ShouldBe(2);
            invoked.Item.Title.ShouldBe("Album 2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_on_a_side_cover_centres_it_and_a_click_on_the_centre_opens_it()
    {
        var flow = Flow();
        var invoked = -1;
        flow.ItemInvoked += (_, e) => invoked = e.Index;
        var window = TestHelpers.Show(flow);
        try
        {
            var stage = flow.Stage!;
            var centre = stage.TranslatePoint(new Point(stage.Bounds.Width / 2, stage.CoverSize.Height / 2), window)!.Value;
            stage.HitCover(new Point(stage.Bounds.Width / 2, stage.CoverSize.Height / 2)).ShouldBe(0);
            // The first neighbour sits to the right of the centred cover.
            var side = new Point(stage.Bounds.Width / 2 + stage.CoverSize.Width * 0.75, stage.CoverSize.Height / 2);
            stage.HitCover(side).ShouldBe(1);
            var sidePoint = stage.TranslatePoint(side, window)!.Value;
            window.MouseDown(sidePoint, MouseButton.Left);
            window.MouseUp(sidePoint, MouseButton.Left);
            flow.SelectedIndex.ShouldBe(1);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(centre, MouseButton.Left);
            window.MouseUp(centre, MouseButton.Left);
            invoked.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nearer_covers_draw_over_farther_ones()
    {
        var flow = Flow();
        flow.SelectedIndex = 3;
        var window = TestHelpers.Show(flow);
        try
        {
            var covers = flow.Stage!.Covers;
            covers[3].ZIndex.ShouldBeGreaterThan(covers[2].ZIndex);
            covers[2].ZIndex.ShouldBeGreaterThan(covers[0].ZIndex);
            covers[4].ZIndex.ShouldBe(covers[2].ZIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_out_of_range_index_is_clamped_and_new_items_keep_a_valid_one()
    {
        var flow = Flow(4);
        var window = TestHelpers.Show(flow);
        try
        {
            flow.SelectedIndex = 9;
            flow.SelectedIndex.ShouldBe(3);
            flow.Items.RemoveAt(3);
            flow.SelectedIndex.ShouldBe(2);
            flow.Items.Clear();
            flow.SelectedIndex.ShouldBe(-1);
            flow.SelectedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Addresses_in_the_items_source_become_covers()
    {
        var flow = new CoverFlow { Width = 400, ItemsSource = new[] { "avares://TKCode.Arc.Tests/missing-a.png", "avares://TKCode.Arc.Tests/missing-b.png" } };
        var window = TestHelpers.Show(flow);
        try
        {
            flow.Count.ShouldBe(2);
            flow.SelectedItem!.Source.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
