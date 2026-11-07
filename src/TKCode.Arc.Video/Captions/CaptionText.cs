using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>Text helpers for drawing cues.</summary>
internal static class CaptionText
{
    /// <summary>
    /// Whether the first strong directional character is right-to-left (the paragraph's base direction, Unicode rule P2,
    /// approximated by script blocks: Hebrew, Arabic, Syriac, Thaana, NKo, Samaritan, Mandaic and their presentation forms
    /// are right-to-left, other letters left-to-right, digits, punctuation and marks neutral). LRM / RLM count as strong.
    /// </summary>
    public static bool IsRightToLeft(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            if (value == 0x200F)
            {
                return true;
            }
            if (value == 0x200E)
            {
                return false;
            }
            if (IsRightToLeftBlock(value))
            {
                if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
                    or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.OtherPunctuation or UnicodeCategory.Format)
                {
                    continue;
                }
                return true;
            }
            if (Rune.IsLetter(rune))
            {
                return false;
            }
        }
        return false;
    }

    private static bool IsRightToLeftBlock(int c) =>
        c is (>= 0x0590 and <= 0x08FF) or (>= 0xFB1D and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFF)
            or (>= 0x10800 and <= 0x10FFF) or (>= 0x1E800 and <= 0x1EFFF);
}
