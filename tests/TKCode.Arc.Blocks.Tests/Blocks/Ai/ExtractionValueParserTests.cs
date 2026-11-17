using Avalonia.Headless.XUnit;
using ErrorKind = TKCode.Arc.ExtractionValueParser.ErrorKind;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// How the document extraction reads the text of a value into its type: dates, amounts with either separator, integers,
/// currency codes and IBANs (check digits included). The IBANs are the invented ones of the fixtures and the standard
/// examples of ISO 13616.
/// </summary>
public class ExtractionValueParserTests
{
    private static ExtractionValueParser.ParseResult Parse(ExtractionFieldType type, string text) => ExtractionValueParser.Parse(type, text);

    [AvaloniaTheory]
    [InlineData(ExtractionFieldType.Text)]
    [InlineData(ExtractionFieldType.Date)]
    [InlineData(ExtractionFieldType.Money)]
    [InlineData(ExtractionFieldType.Currency)]
    [InlineData(ExtractionFieldType.Iban)]
    [InlineData(ExtractionFieldType.Integer)]
    [InlineData(ExtractionFieldType.Number)]
    public void Empty_And_Blank_Text_Is_Valid_And_Has_No_Value(ExtractionFieldType type)
    {
        Parse(type, "").ShouldBe(new ExtractionValueParser.ParseResult(null, ErrorKind.None));
        Parse(type, "   ").IsValid.ShouldBeTrue();
        Parse(type, "   ").Value.ShouldBeNull();
        ExtractionValueParser.Parse(type, null).IsValid.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Text_Is_Trimmed()
    {
        Parse(ExtractionFieldType.Text, "  Oakridge Ceramics Ltd ").Value.ShouldBe("Oakridge Ceramics Ltd");
    }

    [AvaloniaTheory]
    [InlineData("2026-09-03", 2026, 9, 3)]
    [InlineData(" 2026-09-17 ", 2026, 9, 17)]
    [InlineData("2028-02-29", 2028, 2, 29)]
    [InlineData("0001-01-01", 1, 1, 1)]
    public void Dates_Must_Be_Iso_Days(string text, int year, int month, int day)
    {
        var result = Parse(ExtractionFieldType.Date, text);
        result.Error.ShouldBe(ErrorKind.None);
        result.Value.ShouldBe(new DateOnly(year, month, day));
    }

    [AvaloniaTheory]
    [InlineData("16 Oct")]
    [InlineData("3 September 2026")]
    [InlineData("2026-02-30")]
    [InlineData("2027-02-29")]
    [InlineData("2026-13-01")]
    [InlineData("2026-00-10")]
    [InlineData("2026-9-3")]
    [InlineData("2026/09/03")]
    [InlineData("20260903")]
    [InlineData("0000-01-01")]
    [InlineData("2026-09-03T10:00")]
    [InlineData("٢٠٢٦-٠٩-٠٣")]
    public void Anything_Else_Is_A_Date_Error(string text)
    {
        Parse(ExtractionFieldType.Date, text).ShouldBe(new ExtractionValueParser.ParseResult(null, ErrorKind.Date));
    }

    [AvaloniaTheory]
    [InlineData("1,400.00", "1400.00")]
    [InlineData("1.400,50", "1400.50")]
    [InlineData("€24.00", "24.00")]
    [InlineData("-3", "-3")]
    [InlineData("640.00", "640.00")]
    [InlineData("2,686.20", "2686.20")]
    [InlineData("1,280", "1280")]
    [InlineData("1.400", "1400")]
    [InlineData("1 400,50", "1400.50")]
    [InlineData("1 400,50 €", "1400.50")]
    [InlineData("1'400.50", "1400.50")]
    [InlineData("EUR 2,686.20", "2686.20")]
    [InlineData("2,686.20 EUR", "2686.20")]
    [InlineData("$ -12.5", "-12.5")]
    [InlineData("-€3", "-3")]
    [InlineData("−3.10", "-3.10")]
    [InlineData("+7", "7")]
    [InlineData("0.125", "0.125")]
    [InlineData(".5", "0.5")]
    [InlineData("3.14159", "3.14159")]
    [InlineData("1234.567", "1234.567")]
    [InlineData("1,234,567", "1234567")]
    [InlineData("1.234.567,89", "1234567.89")]
    [InlineData("1,234.5678", "1234.5678")]
    [InlineData("1,23,456.00", "123456.00")]
    [InlineData("0", "0")]
    public void Amounts_Drop_Symbols_And_Thousands_And_Keep_Their_Digits(string text, string expected)
    {
        var result = Parse(ExtractionFieldType.Money, text);
        result.Error.ShouldBe(ErrorKind.None, text);
        var value = result.Value.ShouldBeOfType<decimal>();
        value.ToString(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(expected);
        Parse(ExtractionFieldType.Number, text).Value.ShouldBe(value);
    }

    [AvaloniaTheory]
    [InlineData("2 pallets")]
    [InlineData("abc")]
    [InlineData("EUR")]
    [InlineData("12.")]
    [InlineData("1,2,3")]
    [InlineData("12,34,5")]
    [InlineData("1e5")]
    [InlineData("--3")]
    [InlineData("(3.00)")]
    [InlineData("3-")]
    [InlineData("€")]
    [InlineData("1.2.3,4.5")]
    public void Anything_Else_Is_A_Number_Error(string text)
    {
        Parse(ExtractionFieldType.Money, text).ShouldBe(new ExtractionValueParser.ParseResult(null, ErrorKind.Number));
        Parse(ExtractionFieldType.Number, text).Error.ShouldBe(ErrorKind.Number);
    }

    [AvaloniaTheory]
    [InlineData("2", 2L)]
    [InlineData("1,000", 1000L)]
    [InlineData("3.0", 3L)]
    [InlineData("-4", -4L)]
    [InlineData("9,223,372,036,854,775,807", long.MaxValue)]
    public void Integers_Are_Whole_Longs(string text, long expected)
    {
        var result = Parse(ExtractionFieldType.Integer, text);
        result.Error.ShouldBe(ErrorKind.None);
        result.Value.ShouldBeOfType<long>().ShouldBe(expected);
    }

    [AvaloniaTheory]
    [InlineData("2.5")]
    [InlineData("two")]
    [InlineData("9,223,372,036,854,775,808")]
    public void Fractions_And_Words_Are_Not_Integers(string text)
    {
        Parse(ExtractionFieldType.Integer, text).ShouldBe(new ExtractionValueParser.ParseResult(null, ErrorKind.Number));
    }

    [AvaloniaTheory]
    [InlineData("EUR")]
    [InlineData(" GBP ")]
    public void Currencies_Are_Three_Capital_Letters(string text)
    {
        Parse(ExtractionFieldType.Currency, text).ShouldBe(new ExtractionValueParser.ParseResult(text.Trim(), ErrorKind.None));
    }

    [AvaloniaTheory]
    [InlineData("eur")]
    [InlineData("€")]
    [InlineData("EURO")]
    [InlineData("EU")]
    [InlineData("E1R")]
    [InlineData("ÉUR")]
    public void Other_Codes_Are_Currency_Errors(string text)
    {
        Parse(ExtractionFieldType.Currency, text).ShouldBe(new ExtractionValueParser.ParseResult(null, ErrorKind.Currency));
    }

    [AvaloniaTheory]
    [InlineData("NL70 ZZTE 0417 2635 98", "NL70ZZTE0417263598")]
    [InlineData("NL63 ZZTE 0398 1147 60", "NL63ZZTE0398114760")]
    [InlineData("nl70zzte0417263598", "NL70ZZTE0417263598")]
    [InlineData("GB82 WEST 1234 5698 7654 32", "GB82WEST12345698765432")]
    [InlineData("DE89 3704 0044 0532 0130 00", "DE89370400440532013000")]
    [InlineData("CH93 0076 2011 6238 5295 7", "CH9300762011623852957")]
    public void Ibans_Lose_Their_Spaces_And_Pass_The_Mod_97_Check(string text, string expected)
    {
        Parse(ExtractionFieldType.Iban, text).ShouldBe(new ExtractionValueParser.ParseResult(expected, ErrorKind.None));
    }

    [AvaloniaTheory]
    [InlineData("NL70 ZZTE 0417 2635 99")]
    [InlineData("NL07 ZZTE 0417 2635 98")]
    [InlineData("NL70 ZZTE 0417 2635")]
    [InlineData("70NL ZZTE 0417 2635 98")]
    [InlineData("NL70 ZZTE 0417-2635-98")]
    [InlineData("GB82 WEST 1234 5698 7654 32 1234 5678 9012 3")]
    [InlineData("1234")]
    public void Wrong_Check_Digits_Or_Structure_Are_Iban_Errors(string text)
    {
        Parse(ExtractionFieldType.Iban, text).ShouldBe(new ExtractionValueParser.ParseResult(null, ErrorKind.Iban));
    }

    [AvaloniaFact]
    public void Check_Digits_00_01_And_99_Are_Refused_Even_When_The_Remainder_Fits()
    {
        // 98 and 01 differ by 97, so NL01 passes mod 97 wherever NL98 would; the standard allows 02 to 98 only.
        var valid = Enumerable.Range(2, 97).Select(d => $"NL{d:00}ZZTE0417263598").Single(i => Parse(ExtractionFieldType.Iban, i).IsValid);
        valid.ShouldBe("NL70ZZTE0417263598");
        Parse(ExtractionFieldType.Iban, "NL00ZZTE0417263598").IsValid.ShouldBeFalse();
        Parse(ExtractionFieldType.Iban, "NL01ZZTE0417263598").IsValid.ShouldBeFalse();
        Parse(ExtractionFieldType.Iban, "NL99ZZTE0417263598").IsValid.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Output_Values_Are_Iso_Dates_And_Plain_Values()
    {
        ExtractionValueParser.ToOutput(new DateOnly(2026, 9, 3)).ShouldBe("2026-09-03");
        ExtractionValueParser.ToOutput(1400.00m).ShouldBe(1400.00m);
        ExtractionValueParser.ToOutput("EUR").ShouldBe("EUR");
        ExtractionValueParser.ToOutput(null).ShouldBeNull();
    }
}
