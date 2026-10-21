using System.Globalization;
using System.IO.Compression;
using System.Text;
using AtomUI;
using AtomUI.Localization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Menus, the value checklist, exports, built-in text and motion.</summary>
public class DataGridChromeTests
{
    private static MenuSurface OpenMenu(TKCode.Arc.DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        return grid.CurrentMenu!;
    }

    private static void Invoke(MenuEntry entry)
    {
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(entry)).Invoke();
        Dispatcher.UIThread.RunJobs();
    }

    private static MenuEntry Entry(MenuSurface menu, string header) =>
        menu.Items.OfType<MenuEntry>().First(e => e.Header as string == header);

    [AvaloniaFact]
    public void The_Column_Menu_Sorts_Groups_Pins_And_Hides()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        var anchor = grid.Viewport.Header;
        var region = grid.Store.AccessorIndex("region");
        grid.OpenColumnMenu(anchor, region);
        var menu = OpenMenu(grid);
        menu.Items.OfType<MenuEntry>().Select(e => e.Header).ShouldBe(
            ["Sort ascending", "Sort descending", "Filter", "Group by Region", "Pin column", "Hide column"]);
        Invoke(Entry(menu, "Sort descending"));
        grid.SortDescriptions.ShouldBe([DataGridSort.Descending("region")]);
        grid.OpenColumnMenu(anchor, region);
        Invoke(Entry(OpenMenu(grid), "Group by Region"));
        grid.GroupBy.ShouldBe(["region"]);
        grid.OpenColumnMenu(anchor, region);
        Invoke(Entry(OpenMenu(grid), "Pin column"));
        grid.PinCount.ShouldBe(2);
        grid.OpenColumnMenu(anchor, region);
        Invoke(Entry(OpenMenu(grid), "Hide column"));
        grid.IsHidden("region").ShouldBeTrue();
        grid.OpenColumnMenu(anchor, grid.Store.AccessorIndex("arr"));
        OpenMenu(grid).Items.OfType<MenuEntry>().Select(e => e.Header).ShouldNotContain("Group by ARR");
    }

    [AvaloniaFact]
    public void The_Columns_Menu_Keeps_One_Column_Visible_And_Shows_All()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        grid.HiddenColumns = ["region", "arr", "health", "renewal"];
        grid.OpenColumnsMenu(grid.Viewport.Header);
        var menu = OpenMenu(grid);
        Entry(menu, "Account").IsEnabled.ShouldBeFalse();
        Invoke(Entry(menu, "ARR"));
        grid.IsHidden("arr").ShouldBeFalse();
        Invoke(Entry(menu, "Show all columns"));
        grid.VisibleAccessors.Length.ShouldBe(5);
    }

    [AvaloniaFact]
    public void The_Density_Menu_Changes_Row_Height()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        grid.OpenDensityMenu(grid.Viewport.Header);
        Invoke(Entry(OpenMenu(grid), "Comfortable"));
        grid.Density.ShouldBe(DataGridDensity.Comfortable);
        grid.RowHeight.ShouldBe(48);
    }

    [AvaloniaFact]
    public void The_Value_Checklist_Keeps_The_Checked_Values()
    {
        var grid = DataGridBehaviorTests.Create();
        var window = DataGridBehaviorTests.Show(grid);
        grid.IsFilterRowOpen = true;
        Dispatcher.UIThread.RunJobs();
        grid.OpenSelectFilter(grid.Viewport.FilterRow, grid.Store.AccessorIndex("region"));
        Dispatcher.UIThread.RunJobs();
        var boxes = window.GetVisualDescendants().OfType<Checkbox>().ToList();
        boxes.Select(b => b.Content).ShouldBe(["Europe", "Asia Pacific", "North America"]);
        boxes[1].IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        grid.FilterOf("region")!.Values.ShouldBe(["Europe", "North America"]);
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Exports_Write_The_Visible_Columns_In_View_Order_And_Groups_As_Outlines()
    {
        var grid = DataGridBehaviorTests.Create();
        grid.ExportFormats = DataGridExportFormats.All;
        DataGridBehaviorTests.Show(grid);
        grid.ApplySort([DataGridSort.Descending("arr")]);
        grid.HiddenColumns = ["health"];
        grid.EnsureView();
        using (var csv = new MemoryStream())
        {
            grid.WriteExport(csv, DataGridExportFormat.Csv, grid.View.Filtered.Select(i => grid.Store.ItemAt(i)).ToArray(), grouped: true);
            var text = Encoding.UTF8.GetString(csv.ToArray()).TrimStart('﻿');
            text.Split("\r\n").ShouldBe([
                "Account,Region,ARR,Renews",
                "Cobalt Clinic,Europe,3000,",
                "Lumen Labs,Europe,1200,2026-01-31",
                "Basalt Freight,Asia Pacific,800,2026-03-01",
                "Delta Dental,North America,,2027-05-09"
            ]);
        }
        grid.GroupBy = ["region"];
        grid.EnsureView();
        using var xlsx = new MemoryStream();
        grid.WriteExport(xlsx, DataGridExportFormat.Xlsx, grid.View.Filtered.Select(i => grid.Store.ItemAt(i)).ToArray(), grouped: true);
        using var zip = new ZipArchive(new MemoryStream(xlsx.ToArray()), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = reader.ReadToEnd();
        sheet.ShouldContain("Region: Europe");
        sheet.ShouldContain("outlineLevel=\"1\"");
        sheet.ShouldContain("SUBTOTAL(9,C3:C4)");
    }

    [AvaloniaFact]
    public void Built_In_Text_Follows_The_Language()
    {
        var languages = Application.Current!.GetLanguageManager()!;
        var grid = DataGridBehaviorTests.Create();
        grid.Culture = null;
        DataGridBehaviorTests.Show(grid);
        try
        {
            languages.ChangeLanguage(LanguageTag.Parse("zh-CN"));
            Dispatcher.UIThread.RunJobs();
            grid.RowCountText().ShouldBe("4 行");
            grid.Toolbar!.SearchBox.PlaceholderText.ShouldBe("搜索");
            grid.ClearRange(new DataGridCellRange(0, 2, 0, 2));
            grid.StatusBar!.MessageText.ShouldBe("已清空 1 个单元格");
            grid.EffectiveCulture.TwoLetterISOLanguageName.ShouldBe("zh");
            languages.ChangeLanguage(LanguageTag.Parse("zh-TW"));
            Dispatcher.UIThread.RunJobs();
            grid.RowCountText().ShouldBe("4 列");
            grid.SetFilter("arr", DataGridFilter.Text(">0"));
            grid.EnsureView();
            grid.RowCountText().ShouldBe("4 列中的 2 列");
        }
        finally
        {
            languages.ChangeLanguage(LanguageTag.Parse("en-US"));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void The_Selection_Springs_Between_Ranges_And_Jumps_When_Motion_Is_Off()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var grid = DataGridBehaviorTests.Create();
        var window = TestHelpers.Show(grid, ArcMotionLevel.Full);
        window.Width = 1000;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromSeconds(1));
        var layer = grid.Viewport.SelectionLayer;
        var start = layer.RangeRect;
        grid.Select(new DataGridCellAddress(3, 3));
        layer.IsMoving.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(40));
        layer.RangeRect.Y.ShouldBeGreaterThan(start.Y);
        layer.RangeRect.Y.ShouldBeLessThan(3 * grid.RowHeight);
        clock.Advance(TimeSpan.FromSeconds(1.5));
        layer.IsMoving.ShouldBeFalse();
        layer.RangeRect.Y.ShouldBe(3 * grid.RowHeight, 0.01);
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        grid.Select(new DataGridCellAddress(0, 0));
        layer.IsMoving.ShouldBeFalse();
        layer.RangeRect.Y.ShouldBe(0, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Sorting_Slides_Rows_From_Their_Old_Places()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var grid = DataGridBehaviorTests.Create();
        var window = TestHelpers.Show(grid, ArcMotionLevel.Full);
        window.Width = 1000;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromSeconds(1));
        grid.ApplySort([DataGridSort.Descending("arr")]);
        grid.EnsureView();
        var viewport = grid.Viewport;
        viewport.IsSliding.ShouldBeTrue();
        // r3 was drawn in the third row and is now first: it starts two rows down and slides up.
        viewport.SlideOffset("r3").ShouldBe(2 * grid.RowHeight, 0.5);
        clock.Advance(TimeSpan.FromSeconds(1));
        viewport.IsSliding.ShouldBeFalse();
        viewport.SlideOffset("r3").ShouldBe(0, 0.01);
        window.Close();
    }
}

/// <summary>Typed rows read and written through column bindings (R7).</summary>
public class DataGridBindingTests
{
    public sealed class Item : System.ComponentModel.INotifyPropertyChanged
    {
        private double _price;

        public string Name { get; set; } = string.Empty;

        public double Price
        {
            get => _price;
            set
            {
                _price = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Price)));
            }
        }

        public int Stock { get; set; }

        public DateTime Due { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [AvaloniaFact]
    public void Bindings_Read_Normalized_Values_And_Write_Back_Two_Way()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<Item>
        {
            new() { Name = "Lamp", Price = 49.5, Stock = 3, Due = new DateTime(2026, 5, 1, 13, 0, 0) }
        };
        var grid = new TKCode.Arc.DataGrid { ItemsSource = items, Width = 600 };
        grid.Columns.Add(new DataGridColumn("name", "Name") { Binding = new Avalonia.Data.Binding(nameof(Item.Name)) });
        grid.Columns.Add(new DataGridColumn("price", "Price", DataGridColumnType.Currency) { Binding = new Avalonia.Data.Binding(nameof(Item.Price)) });
        grid.Columns.Add(new DataGridColumn("stock", "Stock", DataGridColumnType.Number) { Binding = new Avalonia.Data.Binding(nameof(Item.Stock)) });
        grid.Columns.Add(new DataGridColumn("due", "Due", DataGridColumnType.Date) { Binding = new Avalonia.Data.Binding(nameof(Item.Due)) });
        DataGridBehaviorTests.Show(grid);
        grid.ValueAt(0, 1).ShouldBe(49.5m);
        grid.ValueAt(0, 3).ShouldBe(new DateOnly(2026, 5, 1));
        grid.WriteCells([(0, 0, "Desk lamp"), (0, 1, 60.25m), (0, 2, 7m), (0, 3, new DateOnly(2026, 6, 2))], null, DataGridChangeReason.Paste).ShouldBe(4);
        items[0].Due.ShouldBe(new DateTime(2026, 6, 2));
        items[0].Name.ShouldBe("Desk lamp");
        items[0].Price.ShouldBe(60.25);
        items[0].Stock.ShouldBe(7);
        items[0].Price = 10;
        Dispatcher.UIThread.RunJobs();
        grid.ValueAt(0, 1).ShouldBe(10m);
    }
}
