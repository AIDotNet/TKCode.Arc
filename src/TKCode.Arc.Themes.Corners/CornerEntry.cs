namespace TKCode.Arc.Themes.Corners;

/// <summary>One radius token: the control, the token, its default as written in the source, and whether it is a double.</summary>
internal readonly record struct CornerEntry(string Control, string Token, string Default, bool IsDouble);
