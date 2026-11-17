using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>The CSV reader behind <see cref="ImportWizard"/>: RFC 4180 quoting, row ends, delimiters and the row limit.</summary>
public class ImportWizardCsvTests
{
    [Fact]
    public void Header_and_rows_are_read()
    {
        var table = ImportWizardCsv.Parse("name,email\nAda,ada@example.com\nLin,lin@example.com\n");
        table.Problem.ShouldBe(ImportCsvProblem.None);
        table.Headers.ShouldBe(["name", "email"]);
        table.Rows.Count.ShouldBe(2);
        table.Rows[1].ShouldBe(["Lin", "lin@example.com"]);
        table.TotalRows.ShouldBe(2);
    }

    [Fact]
    public void Quoted_fields_keep_delimiters_doubled_quotes_and_line_breaks()
    {
        var table = ImportWizardCsv.Parse("a,b,c\r\n\"x, y\",\"say \"\"hi\"\"\",\"two\nlines\"\r\n");
        table.Rows.Single().ShouldBe(["x, y", "say \"hi\"", "two\nlines"]);
    }

    [Fact]
    public void Bom_crlf_cr_and_blank_lines_are_handled()
    {
        var table = ImportWizardCsv.Parse("﻿a,b\r\n1,2\r\n\r\n3,4\r5,6");
        table.Headers.ShouldBe(["a", "b"]);
        table.Rows.Select(r => r[0]).ShouldBe(["1", "3", "5"]);
    }

    [Fact]
    public void Short_rows_are_padded_and_long_rows_cut()
    {
        var table = ImportWizardCsv.Parse("a,b,c\n1\n1,2,3,4");
        table.Rows[0].ShouldBe(["1", "", ""]);
        table.Rows[1].ShouldBe(["1", "2", "3"]);
    }

    [Fact]
    public void Unquoted_fields_are_trimmed_and_blank_headers_named()
    {
        var table = ImportWizardCsv.Parse(" a , ,c\n  1 , 2 ,\" 3 \"");
        table.Headers.ShouldBe(["a", "Column 2", "c"]);
        table.Rows[0].ShouldBe(["1", "2", " 3 "]);
    }

    [Theory]
    [InlineData("a;b;c\n1;2;3", ';')]
    [InlineData("a\tb\tc\n1\t2\t3", '\t')]
    [InlineData("a,b\n1,2", ',')]
    [InlineData("\"x;y\",b\n1,2", ',')]
    [InlineData("single\n1", ',')]
    public void Delimiter_is_detected_from_the_header(string text, char expected)
    {
        ImportWizardCsv.Parse(text).Delimiter.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", (int)ImportCsvProblem.Empty)]
    [InlineData("  \n \n", (int)ImportCsvProblem.Empty)]
    [InlineData("a,b\n", (int)ImportCsvProblem.NoRows)]
    [InlineData("a,b\n\"open,1", (int)ImportCsvProblem.UnclosedQuote)]
    public void Problems_are_reported(string text, int expected)
    {
        var table = ImportWizardCsv.Parse(text);
        table.Problem.ShouldBe((ImportCsvProblem)expected);
        table.IsUsable.ShouldBeFalse();
    }

    [Fact]
    public void Too_many_rows_reports_the_total()
    {
        var text = "a\n" + string.Join("\n", Enumerable.Range(0, 12).Select(i => i.ToString()));
        var table = ImportWizardCsv.Parse(text, maxRows: 10);
        table.Problem.ShouldBe(ImportCsvProblem.TooManyRows);
        table.TotalRows.ShouldBe(12);
        table.Rows.ShouldBeEmpty();
    }
}
