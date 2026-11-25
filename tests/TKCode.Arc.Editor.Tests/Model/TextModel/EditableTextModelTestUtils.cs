// Ported from vscode (src/vs/editor/test/common/model/editableTextModelTestUtils.ts, commit b57d5c9e), MIT license.
// The port has no MirrorTextModel (it lives in the extension host), so the part of upstream's
// src/vs/editor/common/model/mirrorTextModel.ts that the tests read (getText, version, onEvents) is ported here as
// TestMirrorTextModel, without its line starts prefix sum. The reverse edits that ApplyEdits returns are turned back
// into SingleEditOperation before they are applied, as upstream passes them to applyEdits as they are. The console
// warning when the version id goes backwards is dropped (it asserts nothing).

using Shouldly;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

internal static class EditableTextModelTestUtils
{
    public static void TestApplyEditsWithSyncedModels(
        string[] original,
        SingleEditOperation[] edits,
        string[] expected,
        bool inputEditsAreInvalid = false)
    {
        var originalStr = string.Join('\n', original);
        var expectedStr = string.Join('\n', expected);

        AssertSyncedModels(originalStr, (model, assertMirrorModels) =>
        {
            // Apply edits & collect inverse edits
            var inverseEdits = model.ApplyEdits(edits, computeUndoEdits: true)!;

            // Assert edits produced expected result
            model.GetValue(EndOfLinePreference.LF).ShouldBe(expectedStr);

            assertMirrorModels();

            // Apply the inverse edits
            var inverseInverseEdits = model.ApplyEdits(ToSingleEditOperations(inverseEdits), computeUndoEdits: true)!;

            // Assert the inverse edits brought back model to original state
            model.GetValue(EndOfLinePreference.LF).ShouldBe(originalStr);

            if (!inputEditsAreInvalid)
            {
                // Assert the inverse of the inverse edits are the original edits
                inverseInverseEdits.Select(e => (e.Range, (string?)e.Text, false))
                    .ShouldBe(edits.Select(e => (e.Range, e.Text, e.ForceMoveMarkers)));
            }

            assertMirrorModels();
        });
    }

    /// <summary>Upstream applies the reverse edits as they are; here they become plain edit operations.</summary>
    public static SingleEditOperation[] ToSingleEditOperations(IEnumerable<ValidEditOperation> edits) =>
        edits.Select(e => new SingleEditOperation(e.Range, e.Text)).ToArray();

    private enum AssertDocumentLineMappingDirection
    {
        OffsetToPosition,
        PositionToOffset,
    }

    private static void AssertOneDirectionLineMapping(TextModel model, AssertDocumentLineMappingDirection direction, string msg)
    {
        var allText = model.GetValue();

        int line = 1, column = 1;
        var previousIsCarriageReturn = false;
        for (var offset = 0; offset <= allText.Length; offset++)
        {
            // The position coordinate system cannot express the position between \r and \n
            var position = new TextPosition(line, column + (previousIsCarriageReturn ? -1 : 0));

            if (direction == AssertDocumentLineMappingDirection.OffsetToPosition)
            {
                var actualPosition = model.GetPositionAt(offset);
                actualPosition.ToString().ShouldBe(position.ToString(), msg + " - getPositionAt mismatch for offset " + offset);
            }
            else
            {
                // The position coordinate system cannot express the position between \r and \n
                var expectedOffset = offset + (previousIsCarriageReturn ? -1 : 0);
                var actualOffset = model.GetOffsetAt(position);
                actualOffset.ShouldBe(expectedOffset, msg + " - getOffsetAt mismatch for position " + position);
            }

            // JavaScript's charAt returns '' past the end
            var ch = offset < allText.Length ? allText[offset] : '\0';
            if (ch == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }

            previousIsCarriageReturn = ch == '\r';
        }
    }

    private static void AssertLineMapping(TextModel model, string msg)
    {
        AssertOneDirectionLineMapping(model, AssertDocumentLineMappingDirection.PositionToOffset, msg);
        AssertOneDirectionLineMapping(model, AssertDocumentLineMappingDirection.OffsetToPosition, msg);
    }

    public static void AssertSyncedModels(string text, Action<TextModel, Action> callback, Action<TextModel>? setup = null)
    {
        var model = TestTextModel.Create(text);
        model.SetEOL(EndOfLineSequence.LF);
        AssertLineMapping(model, "model");

        if (setup is not null)
        {
            setup(model);
            AssertLineMapping(model, "model");
        }

        var mirrorModel2 = new TestMirrorTextModel(model.GetLinesContent(), model.GetEOL(), model.GetVersionId());

        void OnDidChangeContent(ModelContentChangedEvent e) => mirrorModel2.OnEvents(e);

        model.DidChangeContent += OnDidChangeContent;

        void AssertMirrorModels()
        {
            AssertLineMapping(model, "model");
            mirrorModel2.GetText().ShouldBe(model.GetValue(), "mirror model 2 text OK");
            mirrorModel2.Version.ShouldBe(model.GetVersionId(), "mirror model 2 version OK");
        }

        callback(model, AssertMirrorModels);

        model.DidChangeContent -= OnDidChangeContent;
        model.Dispose();
        mirrorModel2.Dispose();
    }
}

/// <summary>
/// The text-keeping part of upstream's <c>MirrorTextModel</c> (src/vs/editor/common/model/mirrorTextModel.ts): a copy
/// of a model's lines that follows its content change events.
/// </summary>
internal sealed class TestMirrorTextModel : IDisposable
{
    private readonly List<string> _lines;
    private string _eol;
    private int _versionId;
    private string? _cachedTextValue;

    public TestMirrorTextModel(IEnumerable<string> lines, string eol, int versionId)
    {
        _lines = new List<string>(lines);
        _eol = eol;
        _versionId = versionId;
        _cachedTextValue = null;
    }

    public void Dispose() => _lines.Clear();

    public int Version => _versionId;

    public string GetText()
    {
        _cachedTextValue ??= string.Join(_eol, _lines);
        return _cachedTextValue;
    }

    public void OnEvents(ModelContentChangedEvent e)
    {
        if (!string.IsNullOrEmpty(e.Eol) && e.Eol != _eol)
        {
            _eol = e.Eol;
        }

        // Update my lines
        foreach (var change in e.Changes)
        {
            AcceptDeleteRange(change.Range);
            AcceptInsertText(new TextPosition(change.Range.StartLineNumber, change.Range.StartColumn), change.Text);
        }

        _versionId = e.VersionId;
        _cachedTextValue = null;
    }

    /// <summary>All changes to a line's text go through this method.</summary>
    private void SetLineText(int lineIndex, string newValue) => _lines[lineIndex] = newValue;

    private void AcceptDeleteRange(TextRange range)
    {
        if (range.StartLineNumber == range.EndLineNumber)
        {
            if (range.StartColumn == range.EndColumn)
            {
                // Nothing to delete
                return;
            }

            // Delete text on the affected line
            SetLineText(
                range.StartLineNumber - 1,
                Substring(_lines[range.StartLineNumber - 1], 0, range.StartColumn - 1)
                + Substring(_lines[range.StartLineNumber - 1], range.EndColumn - 1));
            return;
        }

        // Take remaining text on last line and append it to remaining text on first line
        SetLineText(
            range.StartLineNumber - 1,
            Substring(_lines[range.StartLineNumber - 1], 0, range.StartColumn - 1)
            + Substring(_lines[range.EndLineNumber - 1], range.EndColumn - 1));

        // Delete middle lines
        _lines.RemoveRange(range.StartLineNumber, range.EndLineNumber - range.StartLineNumber);
    }

    private void AcceptInsertText(TextPosition position, string insertText)
    {
        if (insertText.Length == 0)
        {
            // Nothing to insert
            return;
        }

        var insertLines = SplitLines(insertText);
        if (insertLines.Length == 1)
        {
            // Inserting text on one line
            SetLineText(
                position.LineNumber - 1,
                Substring(_lines[position.LineNumber - 1], 0, position.Column - 1)
                + insertLines[0]
                + Substring(_lines[position.LineNumber - 1], position.Column - 1));
            return;
        }

        // Append overflowing text from first line to the end of text to insert
        insertLines[^1] += Substring(_lines[position.LineNumber - 1], position.Column - 1);

        // Delete overflowing text from first line and insert text on first line
        SetLineText(
            position.LineNumber - 1,
            Substring(_lines[position.LineNumber - 1], 0, position.Column - 1) + insertLines[0]);

        // Insert new lines
        for (var i = 1; i < insertLines.Length; i++)
        {
            _lines.Insert(position.LineNumber + i - 1, insertLines[i]);
        }
    }

    /// <summary>Upstream's splitLines: splits on \r\n, \r and \n.</summary>
    private static string[] SplitLines(string str) => str.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// <summary>JavaScript's <c>substring(start)</c>, which clamps the index to the string.</summary>
    private static string Substring(string str, int start) => Substring(str, start, str.Length);

    /// <summary>JavaScript's <c>substring(start, end)</c>, which clamps both indices and swaps them when reversed.</summary>
    private static string Substring(string str, int start, int end)
    {
        start = Math.Clamp(start, 0, str.Length);
        end = Math.Clamp(end, 0, str.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return str.Substring(start, end - start);
    }
}
