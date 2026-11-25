// Ported from vscode (src/vs/editor/common/cursor/cursorTypeEditOperations.ts: BaseTypeWithAutoClosingCommand,
// TypeWithAutoClosingCommand, TypeWithIndentationAndAutoClosingCommand and the module functions, commit b57d5c9e),
// MIT license.
//
// Differences from upstream:
//  - ReplaceCommandWithOffsetCursorState is sealed here, so BaseTypeWithAutoClosingCommand implements ICommand with
//    the same range, text and offsets instead of deriving from it; its cursor state is computed the same way;
//  - the module functions (getTypingOperation, shouldPushStackElementBetween, isAutoClosingOvertype, typeCommand,
//    shiftIndent, unshiftIndent, shouldSurroundChar) are in the static class TypeEditOperations; normalizeOperationType
//    returns -1 for upstream's 'space';
//  - JsString holds the JavaScript string semantics the operations rely on (substring clamps and swaps its
//    arguments, charAt gives "" out of range); IndentConverter is upstream's inline { shiftIndent, unshiftIndent }.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>
/// Types an opening character and its closing characters; remembers where the closing characters and the pair ended up
/// so that the controller can track them as auto-closed.
/// </summary>
internal abstract class BaseTypeWithAutoClosingCommand : ICommand
{
    private readonly TextRange _range;
    private readonly string _text;
    private readonly int _lineNumberDeltaOffset;
    private readonly int _columnDeltaOffset;
    private readonly string _openCharacter;
    private readonly string _closeCharacter;

    protected BaseTypeWithAutoClosingCommand(
        TextRange selection,
        string text,
        int lineNumberDeltaOffset,
        int columnDeltaOffset,
        string openCharacter,
        string closeCharacter)
    {
        _range = selection;
        _text = text;
        _lineNumberDeltaOffset = lineNumberDeltaOffset;
        _columnDeltaOffset = columnDeltaOffset;
        _openCharacter = openCharacter;
        _closeCharacter = closeCharacter;
        CloseCharacterRange = null;
        EnclosingRange = null;
    }

    /// <summary>The auto-closed characters, once the command ran.</summary>
    public TextRange? CloseCharacterRange { get; private set; }

    /// <summary>The opening and closing characters, once the command ran.</summary>
    public TextRange? EnclosingRange { get; private set; }

    public virtual void GetEditOperations(TextModel model, IEditOperationBuilder builder) =>
        builder.AddTrackedEditOperation(_range, _text);

    public abstract TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper);

    protected TextSelection ComputeCursorStateWithRange(TextModel model, TextRange range, ICursorStateComputerData helper)
    {
        CloseCharacterRange = new TextRange(range.StartLineNumber, range.EndColumn - _closeCharacter.Length, range.EndLineNumber, range.EndColumn);
        EnclosingRange = new TextRange(range.StartLineNumber, range.EndColumn - _openCharacter.Length - _closeCharacter.Length, range.EndLineNumber, range.EndColumn);
        // super.computeCursorState: ReplaceCommandWithOffsetCursorState's
        var inverseEditOperations = helper.GetInverseEditOperations();
        var srcRange = inverseEditOperations[0].Range;
        return new TextSelection(srcRange.End.Delta(_lineNumberDeltaOffset, _columnDeltaOffset));
    }
}

/// <summary>Types the opening character (unless it is already typed) and the closing characters after it.</summary>
internal sealed class TypeWithAutoClosingCommand : BaseTypeWithAutoClosingCommand
{
    public TypeWithAutoClosingCommand(TextRange selection, string openCharacter, bool insertOpenCharacter, string closeCharacter)
        : base(
            selection,
            (insertOpenCharacter ? openCharacter : "") + closeCharacter,
            lineNumberDeltaOffset: 0,
            columnDeltaOffset: -closeCharacter.Length,
            openCharacter,
            closeCharacter)
    {
    }

    public override TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var range = inverseEditOperations[0].Range;
        return ComputeCursorStateWithRange(model, range, helper);
    }
}

/// <summary>Re-indents the line and types the opening and closing characters in one command.</summary>
internal sealed class TypeWithIndentationAndAutoClosingCommand : BaseTypeWithAutoClosingCommand
{
    private readonly (TextRange Range, string Text) _autoIndentationEdit;
    private readonly (TextRange Range, string Text) _autoClosingEdit;

    public TypeWithIndentationAndAutoClosingCommand(
        (TextRange Range, string Text) autoIndentationEdit,
        TextRange selection,
        string openCharacter,
        string closeCharacter)
        : base(
            selection,
            openCharacter + closeCharacter,
            lineNumberDeltaOffset: 0,
            columnDeltaOffset: openCharacter.Length,
            openCharacter,
            closeCharacter)
    {
        _autoIndentationEdit = autoIndentationEdit;
        _autoClosingEdit = (selection, openCharacter + closeCharacter);
    }

    public override void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        builder.AddTrackedEditOperation(_autoIndentationEdit.Range, _autoIndentationEdit.Text);
        builder.AddTrackedEditOperation(_autoClosingEdit.Range, _autoClosingEdit.Text);
    }

    public override TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        if (inverseEditOperations.Count != 2)
        {
            throw new InvalidOperationException("There should be two inverse edit operations!");
        }
        var range1 = inverseEditOperations[0].Range;
        var range2 = inverseEditOperations[1].Range;
        var range = range1.PlusRange(range2);
        return ComputeCursorStateWithRange(model, range, helper);
    }
}

/// <summary>The module functions of cursorTypeEditOperations.ts.</summary>
internal static class TypeEditOperations
{
    private const int SpaceOperationType = -1;

    public static EditOperationType GetTypingOperation(string typedText, EditOperationType previousTypingOperation)
    {
        if (typedText == " ")
        {
            return previousTypingOperation == EditOperationType.TypingFirstSpace
                || previousTypingOperation == EditOperationType.TypingConsecutiveSpace
                ? EditOperationType.TypingConsecutiveSpace
                : EditOperationType.TypingFirstSpace;
        }

        return EditOperationType.TypingOther;
    }

    public static bool ShouldPushStackElementBetween(EditOperationType previousTypingOperation, EditOperationType typingOperation)
    {
        if (IsTypingOperation(previousTypingOperation) && !IsTypingOperation(typingOperation))
        {
            // Always set an undo stop before non-type operations
            return true;
        }
        if (previousTypingOperation == EditOperationType.TypingFirstSpace)
        {
            // `abc |d`: No undo stop
            // `abc  |d`: Undo stop
            return false;
        }
        // Insert undo stop between different operation types
        return NormalizeOperationType(previousTypingOperation) != NormalizeOperationType(typingOperation);
    }

    private static int NormalizeOperationType(EditOperationType type) =>
        type == EditOperationType.TypingConsecutiveSpace || type == EditOperationType.TypingFirstSpace
            ? SpaceOperationType
            : (int)type;

    private static bool IsTypingOperation(EditOperationType type) =>
        type == EditOperationType.TypingOther
        || type == EditOperationType.TypingFirstSpace
        || type == EditOperationType.TypingConsecutiveSpace;

    public static bool IsAutoClosingOvertype(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<TextRange> autoClosedCharacters,
        string ch)
    {
        if (config.AutoClosingOvertype == EditorAutoClosingEditStrategy.Never)
        {
            return false;
        }
        if (!config.AutoClosingPairs.AutoClosingPairsCloseSingleChar.ContainsKey(ch))
        {
            return false;
        }
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            if (!selection.IsEmpty)
            {
                return false;
            }
            var position = selection.Position;
            var lineText = model.GetLineContent(position.LineNumber);
            var afterCharacter = JsString.CharAt(lineText, position.Column - 1);
            if (afterCharacter != ch)
            {
                return false;
            }
            // Do not over-type quotes after a backslash
            var chIsQuote = ch.Length == 1 && CursorCommon.IsQuote(ch[0]);
            var beforeCharacter = position.Column > 2 ? lineText[position.Column - 2] : '\0';
            if (beforeCharacter == '\\' && chIsQuote)
            {
                return false;
            }
            // Must over-type a closing character typed by the editor
            if (config.AutoClosingOvertype == EditorAutoClosingEditStrategy.Auto)
            {
                var found = false;
                for (int j = 0, lenJ = autoClosedCharacters.Count; j < lenJ; j++)
                {
                    var autoClosedCharacter = autoClosedCharacters[j];
                    if (position.LineNumber == autoClosedCharacter.StartLineNumber && position.Column == autoClosedCharacter.StartColumn)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public static ICommand TypeCommand(TextRange range, string text, bool keepPosition)
    {
        if (keepPosition)
        {
            return new ReplaceCommandWithoutChangingPosition(range, text, true);
        }
        else
        {
            return new ReplaceCommand(range, text, true);
        }
    }

    public static string ShiftIndent(CursorConfiguration config, string indentation, int? count = null)
    {
        var n = count is null or 0 ? 1 : count.Value;
        return ShiftCommand.ShiftIndent(indentation, indentation.Length + n, config.TabSize, config.IndentSize, config.InsertSpaces);
    }

    public static string UnshiftIndent(CursorConfiguration config, string indentation, int? count = null)
    {
        var n = count is null or 0 ? 1 : count.Value;
        return ShiftCommand.UnshiftIndent(indentation, indentation.Length + n, config.TabSize, config.IndentSize, config.InsertSpaces);
    }

    public static bool ShouldSurroundChar(CursorConfiguration config, string ch)
    {
        if (ch.Length == 1 && CursorCommon.IsQuote(ch[0]))
        {
            return config.AutoSurround == EditorAutoSurroundStrategy.Quotes || config.AutoSurround == EditorAutoSurroundStrategy.LanguageDefined;
        }
        else
        {
            // Character is a bracket
            return config.AutoSurround == EditorAutoSurroundStrategy.Brackets || config.AutoSurround == EditorAutoSurroundStrategy.LanguageDefined;
        }
    }

    /// <summary>upstream's <c>isQuote(str)</c> on a string: true only for a single quote character.</summary>
    internal static bool IsQuote(string str) => str.Length == 1 && CursorCommon.IsQuote(str[0]);
}

/// <summary>Shifts and unshifts indentation by the cursor configuration (upstream's inline indent converters).</summary>
internal sealed class IndentConverter : IIndentConverter
{
    private readonly CursorConfiguration _config;

    public IndentConverter(CursorConfiguration config)
    {
        _config = config;
    }

    public string ShiftIndent(string indentation) => TypeEditOperations.ShiftIndent(_config, indentation);

    public string UnshiftIndent(string indentation) => TypeEditOperations.UnshiftIndent(_config, indentation);
}

/// <summary>JavaScript string semantics the typing operations rely on.</summary>
internal static class JsString
{
    /// <summary><c>String.prototype.substring</c>: clamps both indices to the string and swaps them if needed.</summary>
    public static string Substring(string str, int start, int? end = null)
    {
        var s = Math.Clamp(start, 0, str.Length);
        var e = Math.Clamp(end ?? str.Length, 0, str.Length);
        if (s > e)
        {
            (s, e) = (e, s);
        }
        return str.Substring(s, e - s);
    }

    /// <summary><c>String.prototype.charAt</c>: the character at an index, or "" out of range.</summary>
    public static string CharAt(string str, int index) =>
        index >= 0 && index < str.Length ? str[index].ToString() : "";

    /// <summary><c>/^\s*$/.test(str)</c>, with JavaScript's <c>\s</c>.</summary>
    public static bool IsWhitespaceOnly(string str)
    {
        foreach (var ch in str)
        {
            if (!IsJsWhitespace(ch))
            {
                return false;
            }
        }
        return true;
    }

    // JavaScript's \s: WhiteSpace and LineTerminator.
    private static bool IsJsWhitespace(char ch) =>
        ch is (char)0x09 or (char)0x0a or (char)0x0b or (char)0x0c or (char)0x0d or (char)0x20 or (char)0xa0 or (char)0x1680
            or (char)0x2028 or (char)0x2029 or (char)0x202f or (char)0x205f or (char)0x3000 or (char)0xfeff
        || (ch >= (char)0x2000 && ch <= (char)0x200a);
}
