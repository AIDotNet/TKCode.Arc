using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Cell renderers: they draw the visible cells, feed copy and screen readers through GetText, and take clicks.</summary>
public class DataGridRendererTests
{
    private sealed class RecordingRenderer : DataGridCellRenderer
    {
        public List<(string Text, object? Value, object Row, double Width)> Calls { get; } = [];

        public override void Render(in DataGridCellRenderContext context) => Calls.Add((context.Text, context.Value, context.Row, context.Bounds.Width));

        public override string GetText(object? value, DataGridCellFormat format, CultureInfo culture) =>
            value is decimal d ? $"<{d:0}>" : base.GetText(value, format, culture);
    }

    /// <summary>A renderer with a setting, like the built-in ones: changing it tells the column to draw again.</summary>
    private sealed class PrefixRenderer : DataGridCellRenderer
    {
        private string _prefix = string.Empty;

        public List<string> Texts { get; } = [];

        public string Prefix
        {
            get => _prefix;
            set
            {
                _prefix = value;
                NotifyChanged();
            }
        }

        public override void Render(in DataGridCellRenderContext context) => Texts.Add(context.Text);

        public override string GetText(object? value, DataGridCellFormat format, CultureInfo culture) =>
            value is null ? string.Empty : _prefix + base.GetText(value, format, culture);
    }

    private static void Settle()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static Point CellPoint(TKCode.Arc.DataGrid grid, Window window, int row, int column, double dx = 0, double dy = 0)
    {
        var layout = grid.Layout;
        var viewport = grid.Viewport;
        var x = layout.Lefts[column] + layout.Widths[column] / 2 + dx - (column < layout.PinCount ? 0 : viewport.Offset.X);
        var y = viewport.BodyTop + grid.LineOfRow(row) * grid.RowHeight + grid.RowHeight / 2 + dy - viewport.Offset.Y;
        return viewport.TranslatePoint(new Point(x, y), window)!.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    [Fact]
    public void ToneMap_Reads_Pairs_Ignores_Unknown_Tones_And_Matches_Ignoring_Case()
    {
        var badge = new BadgeRenderer { ToneMap = "Won=Success, At risk=Warning; bogus=Nope, Lost = danger" };
        badge.Tones.Count.ShouldBe(3);
        badge.Tones["won"].ShouldBe(DataGridTone.Success);
        badge.Tones["AT RISK"].ShouldBe(DataGridTone.Warning);
        badge.Tones["Lost"].ShouldBe(DataGridTone.Danger);
        badge.ToneMap = null;
        badge.Tones.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Renderer_Draws_Every_Visible_Cell_With_Its_Value_And_Text()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        var renderer = new RecordingRenderer();
        grid.Columns[2].Renderer = renderer;
        DataGridBehaviorTests.Show(grid);
        Settle();
        Settle();
        renderer.Calls.Select(c => c.Text).Distinct().ShouldBe(["<1200>", "<800>", "<3000>", ""], ignoreOrder: true);
        renderer.Calls.Select(c => c.Value).ShouldContain(800m);
        renderer.Calls.Select(c => c.Row).ShouldContain(rows[1]);
        renderer.Calls.ShouldAllBe(c => c.Width > 20 && c.Width < grid.Layout.Widths[2]);
    }

    [AvaloniaFact]
    public void GetText_Feeds_Copy_And_The_Accessible_Name_While_Sort_And_Filter_Keep_Using_The_Value()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.Columns[2].Renderer = new RecordingRenderer();
        DataGridBehaviorTests.Show(grid);
        Settle();
        grid.CellText(1, grid.Layout.Accessors[2]).ShouldBe("<800>");
        grid.Matrix(new DataGridCellRange(0, 2, 1, 2), header: false, formatted: true).Select(r => r[0]).ShouldBe(["<1200>", "<800>"]);
        grid.Matrix(new DataGridCellRange(0, 2, 1, 2), header: false, formatted: false).Select(r => r[0]).ShouldBe(["1200", "800"]);
        grid.ApplySort([DataGridSort.Descending("arr")]);
        grid.SetFilter("arr", DataGridFilter.Text(">1000"));
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(2);
        grid.CellText(0, grid.Layout.Accessors[2]).ShouldBe("<3000>");
    }

    [AvaloniaFact]
    public void A_Click_On_A_Link_Raises_CellInvoked_Without_Selecting_And_A_Click_Beside_It_Selects()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.Columns[0].Renderer = new LinkRenderer { Part = "open" };
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var invoked = new List<DataGridCellInvokedEventArgs>();
        grid.CellInvoked += (_, e) => invoked.Add(e);
        var start = grid.ActiveCell;
        var width = grid.Layout.Widths[0];
        Click(window, CellPoint(grid, window, 1, 0, dx: -width / 2 + grid.CellInlinePadding + 6));
        invoked.Count.ShouldBe(1);
        invoked[0].Part.ShouldBe("open");
        invoked[0].Row.ShouldBe(rows[1]);
        invoked[0].Value.ShouldBe("Basalt Freight");
        invoked[0].Column.Key.ShouldBe("account");
        grid.ActiveCell.ShouldBe(start);
        Click(window, CellPoint(grid, window, 1, 0, dx: width / 2 - 6));
        invoked.Count.ShouldBe(1);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 0));
    }

    [AvaloniaFact]
    public void Double_Clicking_A_Link_Does_Not_Open_The_Editor()
    {
        var grid = DataGridBehaviorTests.Create();
        grid.Columns[0].Renderer = new LinkRenderer();
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var point = CellPoint(grid, window, 1, 0, dx: -grid.Layout.Widths[0] / 2 + grid.CellInlinePadding + 6);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
        grid.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Tone_Palette_Follows_The_Theme_Tokens()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        Settle();
        var palette = grid.RenderPalette;
        var tones = Enum.GetValues<DataGridTone>().Select(t => palette.Tone(t).Fill).ToArray();
        tones.Distinct().Count().ShouldBe(tones.Length);
        palette.Tone(DataGridTone.Success).Foreground.ShouldNotBe(palette.Tone(DataGridTone.Danger).Foreground);
        palette.Accent.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Built_In_Renderers_Draw_Without_Throwing_For_Empty_And_Odd_Values()
    {
        ObservableCollection<DataGridRow> rows =
        [
            new("a") { { "account", "Lumen" }, { "region", "" }, { "arr", null }, { "health", 150m } },
            new("b") { { "account", null }, { "region", "Elsewhere" }, { "arr", -5m }, { "health", -20m } }
        ];
        var grid = DataGridBehaviorTests.Create(rows);
        grid.Columns[0].Renderer = new LinkRenderer();
        grid.Columns[1].Renderer = new BadgeRenderer { ToneMap = "Europe=Success" };
        var icons = new IconTextRenderer { DefaultIcon = ArcIcons.Check };
        icons.Tones["Elsewhere"] = DataGridTone.Warning;
        grid.Columns[2].Renderer = icons;
        grid.Columns[3].Renderer = new ProgressRenderer { Tone = DataGridTone.Neutral };
        DataGridBehaviorTests.Show(grid);
        Should.NotThrow(() =>
        {
            Settle();
            Settle();
        });
    }

    [AvaloniaFact]
    public void A_Grid_Is_Collected_While_A_Renderer_Shared_From_Resources_Lives_On()
    {
        var shared = new BadgeRenderer();
        var weak = TestHelpers.ShowAndClose(() =>
        {
            var grid = DataGridBehaviorTests.Create();
            grid.Columns[1].Renderer = shared;
            return grid;
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        shared.ToneMap = "Europe=Success";
    }

    [AvaloniaFact]
    public void Clearing_The_Columns_Lets_Them_Go_And_The_Grid_Stops_Following_Them()
    {
        var shared = new BadgeRenderer();
        var grid = WithRenderer(shared);
        var kept = grid.Columns[0];
        DataGridBehaviorTests.Show(grid);
        Settle();
        var weak = Clear(grid);
        grid.UpdateLayout();
        Settle();
        TestHelpers.Collect(weak);
        // The shared renderer no longer holds the column it drew.
        weak.IsAlive.ShouldBeFalse();
        // A Clear reports no old items: the grid still stops listening to the columns it had.
        var version = grid.ColumnsVersion;
        kept.Header = "Customer";
        shared.ToneMap = "Europe=Success";
        grid.EnsureView();
        grid.ColumnsVersion.ShouldBe(version);

        // Both made out of line so no hidden temporary in this method keeps the cleared column.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static TKCode.Arc.DataGrid WithRenderer(DataGridCellRenderer renderer)
        {
            var grid = DataGridBehaviorTests.Create();
            grid.Columns[1].Renderer = renderer;
            return grid;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference Clear(TKCode.Arc.DataGrid grid)
        {
            var column = grid.Columns[1];
            grid.Columns.Clear();
            grid.EnsureView();
            return new WeakReference(column);
        }
    }

    [AvaloniaFact]
    public void A_Grid_Put_Back_Picks_Up_Column_And_Renderer_Changes_Made_While_It_Was_Away()
    {
        var renderer = new PrefixRenderer();
        var grid = DataGridBehaviorTests.Create();
        grid.Columns[2].Renderer = renderer;
        grid.Columns[2].Rules.Add(new RangeRule { Minimum = 1000 });
        var host = new Border { Child = grid };
        var window = TestHelpers.Show(host);
        window.Width = 1000;
        window.Height = 700;
        Settle();
        grid.VisibleAccessors.Length.ShouldBe(5);
        grid.ValidateAll().ShouldBe(1);

        // A trip away and back with nothing changed keeps the flags.
        host.Child = null;
        host.Child = grid;
        Settle();
        grid.ErrorCount.ShouldBe(1);

        // Away, a column is hidden and the renderer changes.
        host.Child = null;
        grid.Columns[1].IsHidden = true;
        renderer.Prefix = "#";
        renderer.Texts.Clear();
        host.Child = grid;
        Settle();
        Settle();
        grid.VisibleAccessors.Length.ShouldBe(4);
        renderer.Texts.Where(t => t.Length > 0).ShouldNotBeEmpty();
        renderer.Texts.Where(t => t.Length > 0).ShouldAllBe(t => t.StartsWith('#'));

        // Back in the tree it follows the columns and the renderer again.
        grid.Columns[1].IsHidden = false;
        grid.VisibleAccessors.Length.ShouldBe(5);
        renderer.Prefix = "~";
        renderer.Texts.Clear();
        Settle();
        renderer.Texts.Where(t => t.Length > 0).ShouldNotBeEmpty();
        renderer.Texts.Where(t => t.Length > 0).ShouldAllBe(t => t.StartsWith('~'));
    }
}
