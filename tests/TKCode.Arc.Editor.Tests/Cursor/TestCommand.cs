// Adaptation of vscode (src/vs/editor/test/browser/testCommand.ts: testCommand and getEditOperation, commit b57d5c9e),
// MIT license. Upstream runs the command through a test code editor's view model (viewModel.executeCommand); without a
// view model yet, this runs it through CommandExecutor directly, which is what executeCommand does for one cursor.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

internal static class TestCommand
{
    internal static void Run(
        string[] lines,
        string? languageId,
        TextSelection selection,
        Func<TextSelection, ICommand> commandFactory,
        string[] expectedLines,
        TextSelection expectedSelection)
    {
        using var model = TestTextModel.Create(string.Join('\n', lines), languageId);

        var command = commandFactory(selection);
        var selections = CommandExecutor.ExecuteCommands(model, [selection], [command]) ?? [selection];

        model.GetLinesContent().ShouldBe(expectedLines);
        selections[0].ToString().ShouldBe(expectedSelection.ToString());
    }

    /// <summary>
    /// Upstream's testCommand with a command that needs the language configuration service, and the optional
    /// <paramref name="prepare"/> callback that registers languages before the model is created.
    /// </summary>
    internal static void Run(
        string[] lines,
        string? languageId,
        TextSelection selection,
        Func<LanguageConfigurationService, TextSelection, ICommand> commandFactory,
        string[] expectedLines,
        TextSelection expectedSelection,
        Action<LanguageConfigurationService>? prepare = null)
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        prepare?.Invoke(languageConfigurationService);
        Run(lines, languageId, selection, sel => commandFactory(languageConfigurationService, sel), expectedLines, expectedSelection);
    }

    /// <summary>The edit operations a command produces, without applying them.</summary>
    internal static List<SingleEditOperation> GetEditOperation(TextModel model, ICommand command)
    {
        var builder = new CollectingBuilder();
        command.GetEditOperations(model, builder);
        return builder.Operations;
    }

    private sealed class CollectingBuilder : IEditOperationBuilder
    {
        public List<SingleEditOperation> Operations { get; } = new();

        public void AddEditOperation(TextRange range, string? text, bool forceMoveMarkers = false) =>
            Operations.Add(new SingleEditOperation(range, text, forceMoveMarkers));

        public void AddTrackedEditOperation(TextRange range, string? text, bool forceMoveMarkers = false) =>
            Operations.Add(new SingleEditOperation(range, text, forceMoveMarkers));

        public string TrackSelection(TextSelection selection, bool? trackPreviousOnEmpty = null) => string.Empty;
    }
}
