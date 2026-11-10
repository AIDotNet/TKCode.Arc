using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Features;

public class FeatureBentoTests
{
    private static Window Show(Control control, double width = 1200, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Samples_Pack_Into_Three_Rows_Of_Six_Columns()
    {
        var bento = new FeatureBento();
        var window = Show(bento);
        try
        {
            bento.Cards.Count.ShouldBe(6);
            bento.Cards.Select(c => c.Cell).ShouldBe([(0, 0, 4, 2), (4, 0, 2, 2), (0, 2, 3, 1), (3, 2, 3, 1), (0, 3, 4, 1), (4, 3, 2, 1)]);
            bento.Panel!.RowCount.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Packing_Fills_The_First_Free_Place()
    {
        var (cells, rows) = FeatureBentoPanel.Pack([FeatureBentoSize.Tall, FeatureBentoSize.Small, FeatureBentoSize.Small, FeatureBentoSize.Small, FeatureBentoSize.Small], 6);
        cells.ShouldBe([(0, 0, 2, 2), (2, 0, 2, 1), (4, 0, 2, 1), (2, 1, 2, 1), (4, 1, 2, 1)]);
        rows.ShouldBe(2);
        FeatureBentoPanel.Span(FeatureBentoSize.Large, 2).ShouldBe((2, 2));
        FeatureBentoPanel.Span(FeatureBentoSize.Large, 1).ShouldBe((1, 1));
    }

    [AvaloniaFact]
    public void Narrow_Widths_Use_Two_Then_One_Column()
    {
        var bento = new FeatureBento();
        var window = Show(bento, 700);
        try
        {
            bento.Classes.Contains(":compact").ShouldBeTrue();
            bento.Panel!.ColumnCount.ShouldBe(2);
            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            bento.Panel.ColumnCount.ShouldBe(1);
            bento.Cards.ShouldAllBe(c => c.Cell.ColumnSpan == 1 && c.Cell.RowSpan == 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drawings_Hold_Still_Unless_Motion_Is_Full()
    {
        var bento = new FeatureBento();
        var window = Show(bento, level: ArcMotionLevel.Reduced);
        try
        {
            var arts = bento.Cards.Select(c => TestHelpers.Descendant<FeatureBentoArtView>(c)).ToList();
            arts.Select(a => a.Art).ShouldBe([FeatureBentoArt.Pulse, FeatureBentoArt.Orbit, FeatureBentoArt.Typing,
                FeatureBentoArt.Bars, FeatureBentoArt.Sync, FeatureBentoArt.Shield]);
            arts.ShouldAllBe(a => !a.IsLooping);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Walk_The_Cards_And_Enter_Raises_ItemInvoked()
    {
        var bento = new FeatureBento();
        FeatureBentoItem? invoked = null;
        bento.ItemInvoked += (_, e) => invoked = e.Item;
        var window = Show(bento);
        try
        {
            bento.Cards[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bento.Cards[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            bento.Cards[5].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldBe(bento.Cards[5].Item);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Items_Replace_The_Samples()
    {
        var bento = new FeatureBento();
        bento.Items.Add(new FeatureBentoItem { Title = "Only", Art = FeatureBentoArt.None, Size = FeatureBentoSize.Wide });
        var window = Show(bento);
        try
        {
            bento.Cards.Count.ShouldBe(1);
            bento.Cards[0].Cell.ShouldBe((0, 0, 4, 1));
        }
        finally
        {
            window.Close();
        }
    }
}
