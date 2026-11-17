using System.Globalization;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Formatting and parsing of grid values: the contract for what people type, paste and see.</summary>
public class DataGridValueTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo ZhCn = CultureInfo.GetCultureInfo("zh-CN");
    private static readonly CultureInfo ZhTw = CultureInfo.GetCultureInfo("zh-TW");

    [Fact]
    public void Numbers_Use_Culture_Separators_And_A_True_Minus_Sign()
    {
        DataGridValueFormat.Format(1234.5m, new DataGridCellFormat(DataGridColumnType.Number, 1), En).ShouldBe("1,234.5");
        DataGridValueFormat.Format(1234.5m, new DataGridCellFormat(DataGridColumnType.Number, 1), De).ShouldBe("1.234,5");
        DataGridValueFormat.Format(-12m, new DataGridCellFormat(DataGridColumnType.Number), En).ShouldBe("−12");
        DataGridValueFormat.Format(-0.004m, new DataGridCellFormat(DataGridColumnType.Number, 2), En).ShouldBe("0.00");
    }

    [Fact]
    public void Currency_Keeps_The_Column_Symbol_And_Percent_Appends_The_Sign()
    {
        DataGridValueFormat.Format(-1500m, new DataGridCellFormat(DataGridColumnType.Currency, 0, "$"), ZhCn).ShouldBe("−$1,500");
        DataGridValueFormat.Format(32.5m, new DataGridCellFormat(DataGridColumnType.Percent, 1), En).ShouldBe("32.5%");
        DataGridValueFormat.Format(null, new DataGridCellFormat(DataGridColumnType.Number), En).ShouldBe(string.Empty);
    }

    [Fact]
    public void Days_Use_The_Column_Format_Or_The_Culture_Default()
    {
        var day = new DateOnly(2026, 1, 31);
        DataGridValueFormat.Format(day, new DataGridCellFormat(DataGridColumnType.Date), En).ShouldBe("Jan 31, 2026");
        DataGridValueFormat.Format(day, new DataGridCellFormat(DataGridColumnType.Date), ZhCn).ShouldBe("2026年1月31日");
        DataGridValueFormat.Format(day, new DataGridCellFormat(DataGridColumnType.Date, DateFormat: "yyyy-MM-dd"), En).ShouldBe("2026-01-31");
    }

    [Theory]
    [InlineData("1,234.50", 1234.5)]
    [InlineData("$1,200", 1200)]
    [InlineData("(12)", -12)]
    [InlineData("−12", -12)]
    [InlineData("12%", 12)]
    [InlineData(" 7 ", 7)]
    [InlineData("1e3", 1000)]
    public void Numbers_Parse_Symbols_Signs_And_Parentheses(string text, double expected)
    {
        DataGridValueParser.TryParse(text, new DataGridCellFormat(DataGridColumnType.Currency), En, null, out var value).ShouldBeTrue();
        value.ShouldBe((decimal)expected);
    }

    [Fact]
    public void Numbers_Parse_With_The_Culture_Decimal_Separator()
    {
        DataGridValueParser.TryParse("1.234,5", new DataGridCellFormat(DataGridColumnType.Number), De, null, out var value).ShouldBeTrue();
        value.ShouldBe(1234.5m);
    }

    [Fact]
    public void Unreadable_Numbers_Fail_And_Empty_Text_Clears()
    {
        DataGridValueParser.TryParse("twelve", new DataGridCellFormat(DataGridColumnType.Number), En, null, out _).ShouldBeFalse();
        DataGridValueParser.TryParse("  ", new DataGridCellFormat(DataGridColumnType.Number), En, null, out var empty).ShouldBeTrue();
        empty.ShouldBeNull();
    }

    [Theory]
    [InlineData("2026-01-31")]
    [InlineData("2026/1/31")]
    [InlineData("2026.1.31")]
    [InlineData("Jan 31 2026")]
    [InlineData("Jan 31, 2026")]
    [InlineData("31 January 2026")]
    [InlineData("2026年1月31日")]
    public void Days_Parse_Iso_And_Month_Name_Forms(string text)
    {
        DataGridValueParser.TryParseDate(text, En, out var day).ShouldBeTrue();
        day.ShouldBe(new DateOnly(2026, 1, 31));
    }

    [Theory]
    [InlineData("01/02/2026")]
    [InlineData("2026-02-30")]
    [InlineData("31.01.2026")]
    [InlineData("soon")]
    public void Ambiguous_Or_Impossible_Days_Are_Refused(string text)
    {
        DataGridValueParser.TryParseDate(text, En, out _).ShouldBeFalse();
    }

    [Fact]
    public void Select_Values_Match_Options_Ignoring_Case()
    {
        var format = new DataGridCellFormat(DataGridColumnType.Select);
        DataGridValueParser.TryParse("proposal", format, En, ["Discovery", "Proposal"], out var value).ShouldBeTrue();
        value.ShouldBe("Proposal");
        DataGridValueParser.TryParse("Won", format, En, ["Discovery", "Proposal"], out _).ShouldBeFalse();
        DataGridValueParser.TryParse("Anything", format, En, null, out var free).ShouldBeTrue();
        free.ShouldBe("Anything");
    }

    [Fact]
    public void Row_Values_Normalize_To_Decimal_Day_Or_Text()
    {
        DataGridValueParser.Normalize(12, DataGridColumnType.Number).ShouldBe(12m);
        DataGridValueParser.Normalize(1.5, DataGridColumnType.Percent).ShouldBe(1.5m);
        DataGridValueParser.Normalize(double.NaN, DataGridColumnType.Number).ShouldBe("NaN");
        DataGridValueParser.Normalize("2026-03-04", DataGridColumnType.Date).ShouldBe(new DateOnly(2026, 3, 4));
        DataGridValueParser.Normalize(new DateTime(2026, 3, 4, 23, 30, 0), DataGridColumnType.Date).ShouldBe(new DateOnly(2026, 3, 4));
        DataGridValueParser.Normalize(string.Empty, DataGridColumnType.Text).ShouldBeNull();
    }

    [Fact]
    public void Totals_Shorten_With_K_M_B_Or_Chinese_Units()
    {
        DataGridValueFormat.FormatCompact(1_250_000m, En).ShouldBe(new DataGridCompactNumber(1.3m, 1, "M"));
        DataGridValueFormat.FormatCompact(345_678m, En).ShouldBe(new DataGridCompactNumber(346m, 0, "K"));
        DataGridValueFormat.FormatCompact(2_500_000_000m, En).ShouldBe(new DataGridCompactNumber(2.5m, 1, "B"));
        DataGridValueFormat.FormatCompact(12.34m, En).ShouldBe(new DataGridCompactNumber(12.3m, 1, string.Empty));
        DataGridValueFormat.FormatCompact(123_456m, ZhCn).ShouldBe(new DataGridCompactNumber(12.3m, 1, "万"));
        DataGridValueFormat.FormatCompact(3_456_789m, ZhCn).ShouldBe(new DataGridCompactNumber(346m, 0, "万"));
        DataGridValueFormat.FormatCompact(250_000_000m, ZhTw).ShouldBe(new DataGridCompactNumber(2.5m, 1, "億"));
    }

    [Fact]
    public void Column_Letters_Follow_Spreadsheets()
    {
        DataGridValueFormat.ColumnLetters(0).ShouldBe("A");
        DataGridValueFormat.ColumnLetters(25).ShouldBe("Z");
        DataGridValueFormat.ColumnLetters(26).ShouldBe("AA");
        DataGridValueFormat.ColumnLetters(701).ShouldBe("ZZ");
    }
}

/// <summary>The filter row syntax.</summary>
public class DataGridFilterExpressionTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(">100", 101, true)]
    [InlineData(">100", 100, false)]
    [InlineData(">=100", 100, true)]
    [InlineData("<5", 4, true)]
    [InlineData("<=5", 5, true)]
    [InlineData("!=0", 0, false)]
    [InlineData("<>0", 1, true)]
    [InlineData("=12", 12, true)]
    [InlineData("12", 12, true)]
    [InlineData("10..50", 50, true)]
    [InlineData("50..10", 10, true)]
    [InlineData("10 to 50", 51, false)]
    [InlineData("10–50", 30, true)]
    [InlineData("-5..-1", -3, true)]
    [InlineData("> $1,000", 1500, true)]
    public void Number_Expressions(string expression, double value, bool expected)
    {
        DataGridFilterExpression.TryParseNumber(expression, En, out var predicate).ShouldBeTrue();
        predicate.ShouldNotBeNull();
        predicate((decimal)value).ShouldBe(expected);
    }

    [Fact]
    public void Unreadable_Number_Expressions_Are_Invalid_And_Empty_Ones_Do_Not_Filter()
    {
        DataGridFilterExpression.TryParseNumber("> many", En, out _).ShouldBeFalse();
        DataGridFilterExpression.TryParseNumber("  ", En, out var none).ShouldBeTrue();
        none.ShouldBeNull();
        DataGridFilterExpression.IsInvalid(DataGridColumnType.Number, ">>", En).ShouldBeTrue();
        DataGridFilterExpression.IsInvalid(DataGridColumnType.Text, ">>", En).ShouldBeFalse();
    }

    [Theory]
    [InlineData("2026-03-15", "2026-03-15", true)]
    [InlineData("2026-03", "2026-03-31", true)]
    [InlineData("2026-03", "2026-04-01", false)]
    [InlineData("2026", "2026-12-31", true)]
    [InlineData("> 2026-03", "2026-04-01", true)]
    [InlineData("> 2026-03", "2026-03-31", false)]
    [InlineData(">= 2026-03", "2026-03-01", true)]
    [InlineData("< 2026-03", "2026-02-28", true)]
    [InlineData("<= 2026-03", "2026-03-31", true)]
    [InlineData("!= 2026", "2026-06-01", false)]
    [InlineData("2026-01-01..2026-03-31", "2026-02-10", true)]
    [InlineData("2026-03..2026-01", "2026-03-31", true)]
    public void Date_Expressions_Compare_Whole_Days_Months_And_Years(string expression, string day, bool expected)
    {
        DataGridFilterExpression.TryParseDate(expression, En, out var predicate).ShouldBeTrue();
        predicate!(DateOnly.Parse(day, CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [Fact]
    public void Text_Expressions_Contain_Equal_Or_Exclude_Ignoring_Case_And_Accents()
    {
        var contains = DataGridFilterExpression.CreateText("alva", En)!;
        contains("Sofía Álvarez").ShouldBeTrue();
        var exact = DataGridFilterExpression.CreateText("=europe", En)!;
        exact("Europe").ShouldBeTrue();
        exact("Europe West").ShouldBeFalse();
        var exclude = DataGridFilterExpression.CreateText("!won", En)!;
        exclude("Closed won").ShouldBeFalse();
        exclude("Proposal").ShouldBeTrue();
        DataGridFilterExpression.CreateText(" ", En).ShouldBeNull();
    }

    [Fact]
    public void Value_Lists_Are_Always_Active()
    {
        DataGridFilterExpression.IsActive(DataGridColumnType.Select, DataGridFilter.OneOf([]), En).ShouldBeTrue();
        DataGridFilterExpression.IsActive(DataGridColumnType.Number, DataGridFilter.Text("abc"), En).ShouldBeFalse();
        DataGridFilterExpression.IsActive(DataGridColumnType.Number, DataGridFilter.Text(">1"), En).ShouldBeTrue();
    }
}

/// <summary>Sorting, totals and the fill handle series.</summary>
public class DataGridSeriesTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void Sort_Is_Stable_Multi_Key_And_Sinks_Empty_Values_Either_Way()
    {
        var rows = new (string Name, object? Region, object? Arr)[]
        {
            ("a", "Europe", 10m), ("b", null, 30m), ("c", "Asia", 20m), ("d", "Europe", 5m), ("e", "Asia", null)
        };
        object? Value((string Name, object? Region, object? Arr) r, string key) => key == "region" ? r.Region : r.Arr;
        DataGridSorting.Sort(rows, [DataGridSort.Ascending("region"), DataGridSort.Descending("arr")], Value, culture: En)
            .Select(r => r.Name).ShouldBe(["c", "e", "a", "d", "b"]);
        DataGridSorting.Sort(rows, [DataGridSort.Descending("region")], Value, culture: En)
            .Select(r => r.Name).ShouldBe(["a", "d", "c", "e", "b"]);
    }

    [Fact]
    public void Text_Sorts_Naturally_And_Select_Columns_Follow_Their_Options()
    {
        var items = new[] { "Item 10", "item 2", "Item 1" };
        DataGridSorting.Sort(items, [DataGridSort.Ascending("k")], (s, _) => s, culture: En).ShouldBe(["Item 1", "item 2", "Item 10"]);
        var stages = new[] { "Won", "Discovery", "Proposal" };
        var order = new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["k"] = DataGridSorting.OptionOrder(["Discovery", "Proposal", "Won"])
        };
        DataGridSorting.Sort(stages, [DataGridSort.Ascending("k")], (s, _) => s, order, En).ShouldBe(["Discovery", "Proposal", "Won"]);
    }

    [Fact]
    public void Aggregates_Use_Numbers_And_Count_Non_Empty_Cells()
    {
        object?[] values = [10m, null, 20m, "n/a", 30m, ""];
        DataGridAggregates.Compute(values, DataGridAggregate.Sum).ShouldBe(60m);
        DataGridAggregates.Compute(values, DataGridAggregate.Average).ShouldBe(20m);
        DataGridAggregates.Compute(values, DataGridAggregate.Min).ShouldBe(10m);
        DataGridAggregates.Compute(values, DataGridAggregate.Max).ShouldBe(30m);
        DataGridAggregates.Compute(values, DataGridAggregate.Count).ShouldBe(4m);
        DataGridAggregates.Compute(["x"], DataGridAggregate.Sum).ShouldBeNull();
        DataGridAggregates.DefaultFor(DataGridColumnType.Percent).ShouldBe(DataGridAggregate.Average);
        DataGridAggregates.DefaultFor(DataGridColumnType.Currency).ShouldBe(DataGridAggregate.Sum);
        DataGridAggregates.DefaultFor(DataGridColumnType.Date).ShouldBe(DataGridAggregate.None);
    }

    [Fact]
    public void Even_Number_Runs_Continue_Exactly_Both_Ways()
    {
        DataGridFillSeries.Extend([0.1m, 0.2m, 0.3m], 3, forward: true).ShouldBe([0.4m, 0.5m, 0.6m]);
        DataGridFillSeries.Extend([10m, 20m], 2, forward: false).ShouldBe([0m, -10m]);
    }

    [Fact]
    public void Other_Values_Repeat_And_Backward_Fills_Repeat_From_The_End()
    {
        DataGridFillSeries.Extend(["a", "b"], 3, forward: true).ShouldBe(["a", "b", "a"]);
        DataGridFillSeries.Extend(["a", "b"], 3, forward: false).ShouldBe(["b", "a", "b"]);
        DataGridFillSeries.Extend([1m, 2m, 4m], 2, forward: true).ShouldBe([1m, 2m]);
        DataGridFillSeries.Extend([5m], 2, forward: true).ShouldBe([5m, 5m]);
    }

    [Fact]
    public void Days_Continue_By_Days_Or_By_Months()
    {
        DataGridFillSeries.Extend([new DateOnly(2026, 1, 30), new DateOnly(2026, 1, 31)], 2, true, DataGridColumnType.Date)
            .ShouldBe([new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 2)]);
        DataGridFillSeries.Extend([new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15)], 2, true, DataGridColumnType.Date)
            .ShouldBe([new DateOnly(2026, 3, 15), new DateOnly(2026, 4, 15)]);
        DataGridFillSeries.Extend([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28)], 2, true, DataGridColumnType.Date)
            .ShouldBe([new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30)]);
        DataGridFillSeries.Extend([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28)], 1, false, DataGridColumnType.Date)
            .ShouldBe([new DateOnly(2025, 12, 31)]);
        DataGridFillSeries.Extend([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28)], 1, true)
            .ShouldBe([new DateOnly(2026, 1, 31)]);
    }
}
