namespace TKCode.Arc.Localization;

/// <summary>Built-in text of <see cref="TKCode.Arc.Form"/> and <see cref="TKCode.Arc.FormItem"/>: the "Optional" mark, the checking status and the error summary announced after a failed submit (Localization/*.xlf).</summary>
[LanguageCatalog]
public enum FormLangResourceKind
{
    Optional,
    Validating,
    InvalidSummaryOne,
    InvalidSummaryMany
}
