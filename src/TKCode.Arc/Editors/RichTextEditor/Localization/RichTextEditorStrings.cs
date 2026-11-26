using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The editor's built-in text in the current Arc language (<c>Localization/*.xlf</c>).</summary>
internal static class RichTextEditorStrings
{
    public static string Get(RichTextEditorLangResourceKind kind) => RichTextEditorLangResources.Get(kind);

    public static string Format(RichTextEditorLangResourceKind kind, params object?[] arguments) =>
        RichTextEditorLangResources.Format(kind, arguments);

    /// <summary>A label with its shortcut, such as "Bold (⌘B)".</summary>
    public static string WithShortcut(RichTextEditorLangResourceKind kind, string shortcut) =>
        Format(RichTextEditorLangResourceKind.TooltipShortcut, Get(kind), shortcut);

    /// <summary>The name of a text or highlight color ("None" for <see cref="RichTextColor.Default"/>).</summary>
    public static string ColorName(RichTextColor color) => Get(color switch
    {
        RichTextColor.Gray => RichTextEditorLangResourceKind.ColorGray,
        RichTextColor.Red => RichTextEditorLangResourceKind.ColorRed,
        RichTextColor.Orange => RichTextEditorLangResourceKind.ColorOrange,
        RichTextColor.Yellow => RichTextEditorLangResourceKind.ColorYellow,
        RichTextColor.Green => RichTextEditorLangResourceKind.ColorGreen,
        RichTextColor.Blue => RichTextEditorLangResourceKind.ColorBlue,
        RichTextColor.Purple => RichTextEditorLangResourceKind.ColorPurple,
        _ => RichTextEditorLangResourceKind.ColorNone
    });

    public static string SpacingName(RichTextLineSpacing spacing) => Get(spacing switch
    {
        RichTextLineSpacing.Compact => RichTextEditorLangResourceKind.SpacingCompact,
        RichTextLineSpacing.Relaxed => RichTextEditorLangResourceKind.SpacingRelaxed,
        RichTextLineSpacing.Loose => RichTextEditorLangResourceKind.SpacingLoose,
        _ => RichTextEditorLangResourceKind.SpacingDefault
    });

    /// <summary>The label of a code language row: the built-in "Plain text" follows the language, the rest are names.</summary>
    public static string CodeLanguageLabel(RichTextCodeLanguage language) =>
        language.Id is null && language.Label == "Plain text" ? Get(RichTextEditorLangResourceKind.PlainText) : language.Label;
}
