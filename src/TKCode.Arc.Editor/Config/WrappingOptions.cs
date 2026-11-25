// Ported from vscode (src/vs/editor/common/config/editorOptions.ts: WrappingIndent, wordBreak, wrappingStrategy,
// wordWrapBreakBeforeCharacters and wordWrapBreakAfterCharacters, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - upstream's string unions 'normal' | 'keepAll' and 'simple' | 'advanced' are the enums WordBreak and WrappingStrategy;
//  - the defaults of the two break character options are constants of WrappingOptions until the wrapping options join
//    EditorOptions with the controls.

namespace TKCode.Arc.Editor.Config;

/// <summary>How wrapped lines are indented.</summary>
internal enum WrappingIndent
{
    /// <summary>No indentation: wrapped lines begin at column 1.</summary>
    None = 0,

    /// <summary>Wrapped lines get the same indentation as the parent.</summary>
    Same = 1,

    /// <summary>Wrapped lines get +1 indentation toward the parent.</summary>
    Indent = 2,

    /// <summary>Wrapped lines get +2 indentation toward the parent.</summary>
    DeepIndent = 3,
}

/// <summary>The word break rules for Chinese, Japanese and Korean text (upstream's <c>wordBreak</c>).</summary>
internal enum WordBreak
{
    /// <summary>The default line break rule.</summary>
    Normal,

    /// <summary>Word breaks should not be used for Chinese, Japanese or Korean text; other text behaves as normal.</summary>
    KeepAll,
}

/// <summary>Which algorithm computes the wrapping points (upstream's <c>wrappingStrategy</c>).</summary>
internal enum WrappingStrategy
{
    /// <summary>Assumes that all characters are of the same width: fast, and right for monospace fonts.</summary>
    Simple,

    /// <summary>Delegates the computation to the renderer: slower, and right for any font.</summary>
    Advanced,
}

/// <summary>The defaults of the wrapping options.</summary>
internal static class WrappingOptions
{
    /// <summary>The characters a line may break before (upstream's <c>wordWrapBreakBeforeCharacters</c> default).</summary>
    public const string WordWrapBreakBeforeCharacters = "([{‘“〈《「『【〔（［｛｢£¥＄￡￥+＋";

    /// <summary>The characters a line may break after (upstream's <c>wordWrapBreakAfterCharacters</c> default).</summary>
    public const string WordWrapBreakAfterCharacters =
        " \t})]?|/&.,;¢°′″‰℃、。｡､￠，．：；？！％・･ゝゞヽヾーァィゥェォッャュョヮヵヶぁぃぅぇぉっゃゅょゎゕゖㇰㇱㇲㇳㇴㇵㇶㇷㇸㇹㇺㇻㇼㇽㇾㇿ々〻ｧｨｩｪｫｬｭｮｯｰ”〉》」』】〕）］｝｣";
}
