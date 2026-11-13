using System.Globalization;

namespace TKCode.Arc;

public partial class OnScreenKeyboardLayout
{
    /// <summary>The name of the letters page of the QWERTY layout.</summary>
    public const string LettersPage = "letters";

    /// <summary>The name of the numbers and symbols page of the QWERTY layout.</summary>
    public const string SymbolsPage = "symbols";

    /// <summary>The name of the second symbols page of the QWERTY layout.</summary>
    public const string MoreSymbolsPage = "more-symbols";

    /// <summary>
    /// Creates the built-in layout <paramref name="preset"/>. Each call returns a new layout, so it can be changed (keys
    /// renamed, disabled or added) before it is given to a keyboard.
    /// </summary>
    /// <param name="preset">The layout.</param>
    /// <param name="culture">The culture of the number pads' decimal separator and minus sign; null uses the current culture.</param>
    public static OnScreenKeyboardLayout Create(OnScreenKeyboardPreset preset, CultureInfo? culture = null) => preset switch
    {
        OnScreenKeyboardPreset.Numeric => CreateNumeric(culture),
        OnScreenKeyboardPreset.SignedNumeric => CreateNumeric(culture, includeSign: true),
        OnScreenKeyboardPreset.Digits => CreateDigits(),
        _ => CreateQwerty(),
    };

    /// <summary>
    /// Creates the QWERTY layout: a letters page (Shift, Backspace, a key to the symbols, comma, space, period, Enter), a
    /// numbers and symbols page and a second symbols page.
    /// </summary>
    public static OnScreenKeyboardLayout CreateQwerty()
    {
        var layout = new OnScreenKeyboardLayout { Name = "QWERTY" };
        layout.Pages.Add(new OnScreenKeyboardPage(LettersPage,
            OnScreenKeyboardRow.FromCharacters("qwertyuiop"),
            OnScreenKeyboardRow.FromCharacters("asdfghjkl"),
            Row(new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Shift, 1.5), "zxcvbnm", new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Backspace, 1.5)),
            BottomRow(PageKey(SymbolsPage, "123", "Numbers and symbols")))
        {
            Label = "Letters",
        });
        layout.Pages.Add(new OnScreenKeyboardPage(SymbolsPage,
            OnScreenKeyboardRow.FromCharacters("1234567890"),
            OnScreenKeyboardRow.FromCharacters("@#$_&-+()/"),
            Row(PageKey(MoreSymbolsPage, "#+=", "More symbols"), "*\"':;!?", new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Backspace, 1.5)),
            BottomRow(PageKey(LettersPage, "ABC", "Letters")))
        {
            Label = "Numbers and symbols",
        });
        layout.Pages.Add(new OnScreenKeyboardPage(MoreSymbolsPage,
            OnScreenKeyboardRow.FromCharacters("~`|\\{}[]<>"),
            OnScreenKeyboardRow.FromCharacters("%^=€£¥°©®™"),
            Row(PageKey(SymbolsPage, "123", "Numbers and symbols"), "×÷•…¿¡§", new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Backspace, 1.5)),
            BottomRow(PageKey(LettersPage, "ABC", "Letters")))
        {
            Label = "More symbols",
        });
        return layout;
    }

    /// <summary>
    /// Creates a number pad: 1–9 in three rows from the top (like a phone), Backspace, Left and Right down the right side,
    /// and a bottom row of 0, the decimal separator and Enter. With <paramref name="includeSign"/> the bottom row starts
    /// with a minus key.
    /// </summary>
    /// <param name="culture">The culture of the decimal separator and the minus sign; null uses the current culture.</param>
    /// <param name="includeSign">Adds a key that types the culture's minus sign.</param>
    public static OnScreenKeyboardLayout CreateNumeric(CultureInfo? culture = null, bool includeSign = false)
    {
        var format = (culture ?? CultureInfo.CurrentCulture).NumberFormat;
        var separator = new OnScreenKeyboardKey(format.NumberDecimalSeparator) { AutomationName = "Decimal separator" };
        var layout = new OnScreenKeyboardLayout { Name = includeSign ? "Signed numeric" : "Numeric" };
        var page = new OnScreenKeyboardPage("numbers",
            Row("123", new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Backspace)),
            Row("456", new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Left)),
            Row("789", new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Right)));
        var bottom = new OnScreenKeyboardRow();
        if (includeSign)
        {
            bottom.Keys.Add(new OnScreenKeyboardKey(format.NegativeSign) { Label = "−", AutomationName = "Minus" });
            bottom.Keys.Add(new OnScreenKeyboardKey("0"));
        }
        else
        {
            bottom.Keys.Add(new OnScreenKeyboardKey("0") { Width = 2 });
        }
        bottom.Keys.Add(separator);
        bottom.Keys.Add(new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Enter));
        page.Rows.Add(bottom);
        layout.Pages.Add(page);
        return layout;
    }

    /// <summary>Creates a PIN pad: 1–9 in three rows from the top, then Backspace, 0 and Enter.</summary>
    public static OnScreenKeyboardLayout CreateDigits()
    {
        var layout = new OnScreenKeyboardLayout { Name = "Digits" };
        layout.Pages.Add(new OnScreenKeyboardPage("digits",
            OnScreenKeyboardRow.FromCharacters("123"),
            OnScreenKeyboardRow.FromCharacters("456"),
            OnScreenKeyboardRow.FromCharacters("789"),
            new OnScreenKeyboardRow(
                new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Backspace),
                new OnScreenKeyboardKey("0"),
                new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Enter))));
        return layout;
    }

    private static OnScreenKeyboardKey PageKey(string page, string label, string name) =>
        new(OnScreenKeyboardKeyKind.Page, 1.5) { TargetPage = page, Label = label, AutomationName = name };

    /// <summary>A row: a key, a key per character, a key.</summary>
    private static OnScreenKeyboardRow Row(OnScreenKeyboardKey first, string characters, OnScreenKeyboardKey last)
    {
        var row = OnScreenKeyboardRow.FromCharacters(characters);
        row.Keys.Insert(0, first);
        row.Keys.Add(last);
        return row;
    }

    /// <summary>A row: a key per character, then a key.</summary>
    private static OnScreenKeyboardRow Row(string characters, OnScreenKeyboardKey last)
    {
        var row = OnScreenKeyboardRow.FromCharacters(characters);
        row.Keys.Add(last);
        return row;
    }

    /// <summary>The bottom row of the QWERTY pages: a page key, comma, space, period, Enter.</summary>
    private static OnScreenKeyboardRow BottomRow(OnScreenKeyboardKey pageKey) => new(
        pageKey,
        new OnScreenKeyboardKey(","),
        new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Space, 5),
        new OnScreenKeyboardKey("."),
        new OnScreenKeyboardKey(OnScreenKeyboardKeyKind.Enter, 1.5));
}
