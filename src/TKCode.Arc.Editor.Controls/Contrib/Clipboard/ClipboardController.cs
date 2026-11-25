// Ported from vscode (src/vs/editor/contrib/clipboard/browser/clipboard.ts: the cut, copy and paste commands and their
// keybindings; src/vs/editor/browser/controller/editContext/clipboardUtils.ts: generateDataToCopy, getDataToCopy,
// ClipboardStoredMetadata, InMemoryClipboardMetadataManager and ClipboardEventUtils' 'vscode-editor-data' format;
// commit b57d5c9e), MIT license.
// Differences:
//  - the clipboard is Avalonia's (TopLevel.Clipboard): text plus the metadata as the application format
//    'vscode-editor-data' (JSON written and read with System.Text.Json's reader and writer, no serializer);
//  - the in-memory metadata (for a clipboard that drops the application format) is kept per editor by this
//    contribution instead of a process-wide singleton;
//  - no rich text (copyWithSyntaxHighlighting), no MultiCommand fallback to the DOM: the commands run when the
//    editor's text has focus; a widget inside the editor keeps its own clipboard keys.

using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

/// <summary>What the editor writes next to copied text (upstream's ClipboardStoredMetadata).</summary>
internal sealed record ClipboardStoredMetadata
{
    public int Version { get; init; } = 1;

    public string? Id { get; init; }

    /// <summary>The text is the line of an empty selection; pasting it inserts it as a line above the cursor.</summary>
    public bool IsFromEmptySelection { get; init; }

    /// <summary>The text came from a column selection.</summary>
    public bool IsBlock { get; init; }

    /// <summary>The text of each selection, when several were copied.</summary>
    public IReadOnlyList<string>? MulticursorText { get; init; }

    /// <summary>The language of the text (upstream's mode, for rich text).</summary>
    public string? Mode { get; init; }

    /// <summary>The JSON upstream writes as 'vscode-editor-data'.</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            if (Id is not null)
            {
                writer.WriteString("id", Id);
            }

            writer.WriteBoolean("isFromEmptySelection", IsFromEmptySelection);
            writer.WriteBoolean("isBlock", IsBlock);
            if (MulticursorText is { } texts)
            {
                writer.WriteStartArray("multicursorText");
                foreach (var text in texts)
                {
                    writer.WriteStringValue(text);
                }

                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull("multicursorText");
            }

            if (Mode is not null)
            {
                writer.WriteString("mode", Mode);
            }
            else
            {
                writer.WriteNull("mode");
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Reads the metadata, or null for anything else (a version other than 1 included).</summary>
    public static ClipboardStoredMetadata? FromJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || version.GetInt32() != 1)
            {
                return null;
            }

            List<string>? multicursorText = null;
            if (root.TryGetProperty("multicursorText", out var texts) && texts.ValueKind == JsonValueKind.Array)
            {
                multicursorText = [];
                foreach (var text in texts.EnumerateArray())
                {
                    multicursorText.Add(text.GetString() ?? string.Empty);
                }
            }

            return new ClipboardStoredMetadata
            {
                Id = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null,
                IsFromEmptySelection = root.TryGetProperty("isFromEmptySelection", out var empty) && empty.ValueKind == JsonValueKind.True,
                IsBlock = root.TryGetProperty("isBlock", out var block) && block.ValueKind == JsonValueKind.True,
                MulticursorText = multicursorText,
                Mode = root.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String ? mode.GetString() : null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The text a copy writes and its metadata (upstream's ClipboardDataToCopy, without rich text).</summary>
internal sealed record ClipboardDataToCopy(string Text, IReadOnlyList<TextRange> SourceRanges, ClipboardStoredMetadata Metadata);

/// <summary>
/// The editor's clipboard: cut, copy and paste with the editor's metadata, so that a copied line pastes as a line and
/// the selections of several cursors paste into as many cursors (upstream's clipboard contribution and clipboardUtils).
/// </summary>
internal sealed class ClipboardController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.clipboard";

    /// <summary>The clipboard format of the metadata (upstream's 'vscode-editor-data').</summary>
    public static readonly DataFormat<string> EditorDataFormat = DataFormat.CreateStringApplicationFormat("vscode-editor-data");

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new ClipboardController(editor), EditorContributionInstantiation.Lazy);

    private readonly CodeEditor _editor;
    private (string LastCopiedValue, ClipboardStoredMetadata Data)? _lastState;

    private ClipboardController(CodeEditor editor)
    {
        _editor = editor;
    }

    /// <summary>Whether copying an empty selection copies its line (upstream's emptySelectionClipboard, default true).</summary>
    public bool EmptySelectionClipboard { get; set; } = true;

    public static ClipboardController? Get(CodeEditor editor) => editor.GetContribution<ClipboardController>(Id);

    /// <summary>The text and metadata of the editor's selections (upstream's generateDataToCopy).</summary>
    public static ClipboardDataToCopy GenerateDataToCopy(EditorViewModel viewModel, bool emptySelectionClipboard, bool forceCRLF)
    {
        var modelSelections = viewModel.GetCursorStates().Select(static s => s.ModelState.Selection.Range).ToList();
        var isBlock = viewModel.GetCursorColumnSelectData().IsReal;
        var (sourceRanges, sourceText, sourceTexts) = viewModel.GetPlainTextToCopy(modelSelections, emptySelectionClipboard, forceCRLF);
        var newLineCharacter = viewModel.Model.GetEOL();

        var isFromEmptySelection = emptySelectionClipboard && modelSelections.Count == 1 && modelSelections[0].IsEmpty;
        var text = sourceTexts is not null ? string.Join(newLineCharacter, sourceTexts) : sourceText ?? string.Empty;
        var metadata = new ClipboardStoredMetadata
        {
            Id = Guid.NewGuid().ToString(),
            IsFromEmptySelection = isFromEmptySelection,
            IsBlock = isBlock,
            MulticursorText = sourceTexts,
        };
        return new ClipboardDataToCopy(text, sourceRanges, metadata);
    }

    /// <summary>The metadata stored with <paramref name="pastedText"/> when it is what this editor copied last (upstream's InMemoryClipboardMetadataManager.get).</summary>
    public ClipboardStoredMetadata? GetInMemoryMetadata(string pastedText)
    {
        if (_lastState is { } state && state.LastCopiedValue == pastedText)
        {
            return state.Data;
        }

        _lastState = null;
        return null;
    }

    /// <summary>Copies (or cuts) the selections to the clipboard.</summary>
    public async Task CopyAsync(bool cut)
    {
        if (_editor.ViewModel is not { } viewModel)
        {
            return;
        }

        var selection = viewModel.GetSelection();
        if (selection.IsEmpty && !EmptySelectionClipboard)
        {
            return;
        }

        var data = GenerateDataToCopy(viewModel, EmptySelectionClipboard, OperatingSystem.IsWindows());
        _lastState = (data.Text, data.Metadata);
        if (TopLevel.GetTopLevel(_editor)?.Clipboard is { } clipboard)
        {
            var item = new DataTransferItem();
            item.SetText(data.Text);
            item.Set(EditorDataFormat, data.Metadata.ToJson());
            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
        }

        if (cut && !_editor.IsReadOnly)
        {
            _editor.Trigger("keyboard", EditorHandler.Cut, null);
        }
    }

    /// <summary>Pastes the clipboard's text, with its metadata when the editor wrote it.</summary>
    public async Task PasteAsync()
    {
        if (_editor.ViewModel is null || _editor.IsReadOnly || TopLevel.GetTopLevel(_editor)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        string? json = null;
        try
        {
            json = await clipboard.TryGetValueAsync(EditorDataFormat);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // a platform clipboard that cannot read application formats: the in-memory metadata stands in
        }

        Paste(text, ClipboardStoredMetadata.FromJson(json) ?? GetInMemoryMetadata(text));
    }

    /// <summary>Pastes <paramref name="text"/> with what the copy recorded (upstream's ViewController.paste).</summary>
    public void Paste(string text, ClipboardStoredMetadata? metadata) =>
        _editor.Trigger("keyboard", EditorHandler.Paste, new PastePayload(text)
        {
            PasteOnNewLine = metadata?.IsFromEmptySelection ?? false,
            MulticursorText = metadata?.MulticursorText,
            IsBlock = metadata?.IsBlock ?? false,
        });

    public void Dispose()
    {
        _lastState = null;
    }

    private static Task Run(CodeEditor editor, Func<ClipboardController, Task> run) =>
        Get(editor) is { } controller ? run(controller) : Task.CompletedTask;

    public static readonly EditorCommand CutCommand = EditorCommand.Create(
        "editor.action.clipboardCutAction",
        null,
        (editor, _) => Run(editor, static c => c.CopyAsync(cut: true)),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyX)
        {
            When = EditorContextKeys.EditorTextFocus,
            Win = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | (int)EditorKeyCode.KeyX, Secondary = [KeyMod.Shift | (int)EditorKeyCode.Delete] },
        });

    public static readonly EditorCommand CopyCommand = EditorCommand.Create(
        "editor.action.clipboardCopyAction",
        null,
        (editor, _) => Run(editor, static c => c.CopyAsync(cut: false)),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyC)
        {
            When = EditorContextKeys.EditorTextFocus,
            Win = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | (int)EditorKeyCode.KeyC, Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.Insert] },
        });

    public static readonly EditorCommand PasteCommand = EditorCommand.Create(
        "editor.action.clipboardPasteAction",
        null,
        (editor, _) => Run(editor, static c => c.PasteAsync()),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyV)
        {
            When = EditorContextKeys.EditorTextFocus,
            Win = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | (int)EditorKeyCode.KeyV, Secondary = [KeyMod.Shift | (int)EditorKeyCode.Insert] },
            Linux = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | (int)EditorKeyCode.KeyV, Secondary = [KeyMod.Shift | (int)EditorKeyCode.Insert] },
        });
}
