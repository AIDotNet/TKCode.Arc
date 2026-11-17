using System.Text;

namespace TKCode.Terminal.Avalonia;

/// <summary>The Unicode properties the renderer needs: emoji presentation and cluster text.</summary>
internal static class TerminalUnicode
{
    private const int TextPresentationSelector = 0xFE0E;
    private const int EmojiPresentationSelector = 0xFE0F;
    private const int ZeroWidthJoiner = 0x200D;
    private const int CombiningKeycap = 0x20E3;

    // Emoji_Presentation=Yes (Unicode 16 emoji-data.txt), as sorted inclusive ranges.
    private static readonly int[] s_emojiPresentation =
    [
        0x231A, 0x231B, 0x23E9, 0x23EC, 0x23F0, 0x23F0, 0x23F3, 0x23F3, 0x25FD, 0x25FE, 0x2614, 0x2615, 0x2648, 0x2653,
        0x267F, 0x267F, 0x2693, 0x2693, 0x26A1, 0x26A1, 0x26AA, 0x26AB, 0x26BD, 0x26BE, 0x26C4, 0x26C5, 0x26CE, 0x26CE,
        0x26D4, 0x26D4, 0x26EA, 0x26EA, 0x26F2, 0x26F3, 0x26F5, 0x26F5, 0x26FA, 0x26FA, 0x26FD, 0x26FD, 0x2705, 0x2705,
        0x270A, 0x270B, 0x2728, 0x2728, 0x274C, 0x274C, 0x274E, 0x274E, 0x2753, 0x2755, 0x2757, 0x2757, 0x2795, 0x2797,
        0x27B0, 0x27B0, 0x27BF, 0x27BF, 0x2B1B, 0x2B1C, 0x2B50, 0x2B50, 0x2B55, 0x2B55, 0x1F004, 0x1F004, 0x1F0CF, 0x1F0CF,
        0x1F18E, 0x1F18E, 0x1F191, 0x1F19A, 0x1F1E6, 0x1F1FF, 0x1F201, 0x1F201, 0x1F21A, 0x1F21A, 0x1F22F, 0x1F22F,
        0x1F232, 0x1F236, 0x1F238, 0x1F23A, 0x1F250, 0x1F251, 0x1F300, 0x1F320, 0x1F32D, 0x1F335, 0x1F337, 0x1F37C,
        0x1F37E, 0x1F393, 0x1F3A0, 0x1F3CA, 0x1F3CF, 0x1F3D3, 0x1F3E0, 0x1F3F0, 0x1F3F4, 0x1F3F4, 0x1F3F8, 0x1F43E,
        0x1F440, 0x1F440, 0x1F442, 0x1F4FC, 0x1F4FF, 0x1F53D, 0x1F54B, 0x1F54E, 0x1F550, 0x1F567, 0x1F57A, 0x1F57A,
        0x1F595, 0x1F596, 0x1F5A4, 0x1F5A4, 0x1F5FB, 0x1F64F, 0x1F680, 0x1F6C5, 0x1F6CC, 0x1F6CC, 0x1F6D0, 0x1F6D2,
        0x1F6D5, 0x1F6D7, 0x1F6DC, 0x1F6DF, 0x1F6EB, 0x1F6EC, 0x1F6F4, 0x1F6FC, 0x1F7E0, 0x1F7EB, 0x1F7F0, 0x1F7F0,
        0x1F90C, 0x1F93A, 0x1F93C, 0x1F945, 0x1F947, 0x1F9FF, 0x1FA70, 0x1FA7C, 0x1FA80, 0x1FA89, 0x1FA8F, 0x1FAC6,
        0x1FACE, 0x1FADC, 0x1FADF, 0x1FAE9, 0x1FAF0, 0x1FAF8,
    ];

    /// <summary>Whether the code point is shown as emoji when nothing selects a presentation.</summary>
    public static bool IsDefaultEmoji(int cp)
    {
        if (cp < 0x231A)
        {
            return false;
        }
        int lo = 0, hi = s_emojiPresentation.Length / 2 - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (cp < s_emojiPresentation[2 * mid])
            {
                hi = mid - 1;
            }
            else if (cp > s_emojiPresentation[2 * mid + 1])
            {
                lo = mid + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether a cluster (<paramref name="cp"/> plus <paramref name="extra"/>) is drawn as emoji: VS15 selects text, VS16,
    /// a keycap or a ZWJ sequence select emoji, otherwise the first code point's default presentation decides.
    /// </summary>
    public static bool IsEmojiPresentation(int cp, int[]? extra)
    {
        if (extra is { Length: > 0 })
        {
            foreach (var e in extra)
            {
                if (e == TextPresentationSelector)
                {
                    return false;
                }
                if (e is EmojiPresentationSelector or CombiningKeycap)
                {
                    return true;
                }
            }
            if (Array.IndexOf(extra, ZeroWidthJoiner) >= 0 && IsDefaultEmoji(cp))
            {
                return true;
            }
        }
        return IsDefaultEmoji(cp);
    }

    /// <summary>The cluster's text: the base code point followed by the extra ones.</summary>
    public static string ClusterText(int cp, int[]? extra)
    {
        if (extra is not { Length: > 0 })
        {
            return char.ConvertFromUtf32(cp);
        }
        var sb = new StringBuilder(2 + 2 * extra.Length);
        sb.Append(char.ConvertFromUtf32(cp));
        foreach (var e in extra)
        {
            sb.Append(char.ConvertFromUtf32(e));
        }
        return sb.ToString();
    }
}
