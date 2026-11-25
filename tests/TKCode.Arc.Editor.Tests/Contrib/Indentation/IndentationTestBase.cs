// Adaptation of the suite setup of vscode's indentation.test.ts (src/vs/editor/contrib/indentation/test/browser/
// indentation.test.ts: createTestServiceCollection, createTextModel and withTestCodeEditor with { autoIndent,
// serviceCollection }, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - each suite is a class over this base, which owns the suite's language configuration service with the suite's
//    language registered (registerLanguageConfiguration); there is no language service;
//  - the AutoIndentOnPaste controller is the engine's AutoIndentOnPaste.Trigger on the test editor;
//  - registerTokenizationSupport is IndentationTestUtils.RegisterTokenizationSupport on the model;
//  - `[...].join('\n')` is JoinLines([...]).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>A suite of indentation.test.ts on one language.</summary>
public abstract class IndentationTestBase : IDisposable
{
    private readonly LanguageConfigurationService _languageConfigurationService = new();

    private protected IndentationTestBase(string languageId)
    {
        LanguageId = languageId;
        IndentationTestUtils.RegisterLanguageConfiguration(_languageConfigurationService, languageId);
    }

    /// <summary>The suite's language.</summary>
    private protected string LanguageId { get; }

    // the tests refer to the suite's language as `languageId`
#pragma warning disable IDE1006
    private protected string languageId => LanguageId;
#pragma warning restore IDE1006

    private protected static string JoinLines(string[] lines) => string.Join("\n", lines);

    /// <summary>Upstream's createTextModel(text, languageId, {}).</summary>
    private protected TextModel CreateTextModel(string text) => TestTextModel.Create(text, LanguageId);

    /// <summary>Upstream's withTestCodeEditor(model, { autoIndent, serviceCollection }, ...).</summary>
    private protected void WithIndentEditor(TextModel model, EditorAutoIndentStrategy autoIndent, Action<EditingTestEditor, TestViewModel> callback)
    {
        var options = new TestCodeEditorOptions
        {
            EditorOptions = EditorOptions.Default with { AutoIndent = autoIndent },
            LanguageConfigurationService = _languageConfigurationService,
        };
        TestCodeEditor.WithTestCodeEditor(model, options, (editor, viewModel) => callback(new EditingTestEditor(editor), viewModel));
    }

    public void Dispose() => _languageConfigurationService.Dispose();
}
