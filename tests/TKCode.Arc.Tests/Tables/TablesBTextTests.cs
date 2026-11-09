using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Tables;

/// <summary>
/// <see cref="TablesBText"/> replaced the cached <c>FormattedText</c> of table cells; its lines must match what
/// <c>FormattedText</c> draws, including the capped last line that takes the rest of the text with an ellipsis.
/// </summary>
public class TablesBTextTests
{
    private static readonly string[] s_texts =
    [
        "Hello world foo bar baz qux quux corge grault garply waldo fred plugh xyzzy thud",
        "Supercalifragilisticexpialidocious_antidisestablishmentarianism_pneumonoultramicroscopic short words here",
        "First line\nSecond line\nThird line",
        "Short",
        "12,345.67"
    ];

    [AvaloniaTheory]
    [InlineData(0, 120.0)]
    [InlineData(1, 120.0)]
    [InlineData(2, 120.0)]
    [InlineData(3, 120.0)]
    [InlineData(1, 40.0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Matches_FormattedText_Metrics(int maxLines, double width)
    {
        var typeface = new Typeface(FontFamily.Default);
        foreach (var text in s_texts)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.Black)
            {
                MaxTextWidth = width,
                MaxLineCount = maxLines == 0 ? int.MaxValue : maxLines,
                Trimming = TextTrimming.CharacterEllipsis
            };
            var cached = new TablesBText(text, typeface, 14, Brushes.Black, width, maxLines);
            cached.Height.ShouldBe(formatted.Height, 0.001, text);
            cached.Width.ShouldBe(formatted.Width, 0.001, text);
            cached.WidthIncludingTrailingWhitespace.ShouldBe(formatted.WidthIncludingTrailingWhitespace, 0.001, text);
            cached.Baseline.ShouldBe(formatted.Baseline, 0.001, text);
        }
    }

    [AvaloniaFact]
    public void Right_Aligned_Numbers_Keep_Their_Metrics()
    {
        var typeface = new Typeface(FontFamily.Default);
        var formatted = new FormattedText("1,234", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.Black)
        {
            MaxTextWidth = 200,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Right
        };
        formatted.SetFontFeatures(TablePalette.TabularNumbers);
        var cached = new TablesBText("1,234", typeface, 14, Brushes.Black, 200, 1, TextAlignment.Right, TablePalette.TabularNumbers);
        cached.Height.ShouldBe(formatted.Height, 0.001);
        cached.Width.ShouldBe(formatted.Width, 0.001);
        cached.LineCount.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Empty_Text_Has_No_Size()
    {
        var cached = new TablesBText(string.Empty, new Typeface(FontFamily.Default), 14, Brushes.Black, 100, 1);
        cached.Height.ShouldBe(0);
        cached.LineCount.ShouldBe(0);
    }
}
