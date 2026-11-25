// The options of the editor features of phase 6 (sticky scroll, color decorators, formatting, linked editing), with
// Monaco's names and defaults (src/vs/editor/common/config/editorOptions.ts, commit b57d5c9e: stickyScroll.enabled,
// stickyScroll.maxLineCount, colorDecorators, formatOnType, formatOnPaste, linkedEditing). The features' contributions
// read them and follow their changes (PropertyChanged).

using Avalonia;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    /// <summary>Whether the lines that start the scopes around the top of the view (classes, functions, blocks) stick to
    /// the top while their scope scrolls, as Monaco's <c>stickyScroll.enabled</c> option (default true). The scopes come
    /// from the language's document symbols, else its folding ranges, else indentation; clicking a sticky line reveals it.</summary>
    public static readonly StyledProperty<bool> StickyScrollProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(StickyScroll), true);

    /// <summary>The most lines that stick at the top, as Monaco's <c>stickyScroll.maxLineCount</c> option (1 to 20,
    /// default 5); never more than a quarter of the editor's height.</summary>
    public static readonly StyledProperty<int> StickyScrollMaxLineCountProperty =
        AvaloniaProperty.Register<CodeEditor, int>(nameof(StickyScrollMaxLineCount), 5, coerce: static (_, value) => Math.Clamp(value, 1, 20));

    /// <summary>Whether color values (CSS hex, rgb, rgba, hsl, hsla, or what a language's color provider finds) show a
    /// swatch before them that opens a color picker, as Monaco's <c>colorDecorators</c> option (default true).</summary>
    public static readonly StyledProperty<bool> ColorDecoratorsProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(ColorDecorators), true);

    /// <summary>Whether the language's on-type formatter formats the line after typing one of its trigger characters, as
    /// Monaco's <c>formatOnType</c> option (default false).</summary>
    public static readonly StyledProperty<bool> FormatOnTypeProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(FormatOnType));

    /// <summary>Whether pasted text is formatted by the language's range formatter, as Monaco's <c>formatOnPaste</c>
    /// option (default false).</summary>
    public static readonly StyledProperty<bool> FormatOnPasteProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(FormatOnPaste));

    /// <summary>Whether ranges the language links (an HTML tag's name in its start and end tag) change together while
    /// typing, as Monaco's <c>linkedEditing</c> option (default false).</summary>
    public static readonly StyledProperty<bool> LinkedEditingProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(LinkedEditing));

    /// <summary>Whether the lines that start the scopes around the top of the view stick to the top while their scope scrolls.</summary>
    public bool StickyScroll
    {
        get => GetValue(StickyScrollProperty);
        set => SetValue(StickyScrollProperty, value);
    }

    /// <summary>The most lines that stick at the top (1 to 20).</summary>
    public int StickyScrollMaxLineCount
    {
        get => GetValue(StickyScrollMaxLineCountProperty);
        set => SetValue(StickyScrollMaxLineCountProperty, value);
    }

    /// <summary>Whether color values show a swatch before them that opens a color picker.</summary>
    public bool ColorDecorators
    {
        get => GetValue(ColorDecoratorsProperty);
        set => SetValue(ColorDecoratorsProperty, value);
    }

    /// <summary>Whether the language's on-type formatter formats the line after typing one of its trigger characters.</summary>
    public bool FormatOnType
    {
        get => GetValue(FormatOnTypeProperty);
        set => SetValue(FormatOnTypeProperty, value);
    }

    /// <summary>Whether pasted text is formatted by the language's range formatter.</summary>
    public bool FormatOnPaste
    {
        get => GetValue(FormatOnPasteProperty);
        set => SetValue(FormatOnPasteProperty, value);
    }

    /// <summary>Whether ranges the language links change together while typing.</summary>
    public bool LinkedEditing
    {
        get => GetValue(LinkedEditingProperty);
        set => SetValue(LinkedEditingProperty, value);
    }
}
