// What the engine's hover participants read from a CodeEditor (upstream: the ICodeEditor they get).

using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

/// <summary>The hover participants' view of a <see cref="CodeEditor"/>.</summary>
internal sealed class CodeEditorHoverEditor(CodeEditor editor, Func<HoverOptions> options) : IHoverEditor
{
    public TextModel? Model => editor.Model;

    public IReadOnlyList<IModelDecoration> GetLineDecorations(int lineNumber) =>
        lineNumber >= 1 && lineNumber <= editor.Model.GetLineCount() ? editor.Model.GetLineDecorations(lineNumber) : [];

    public LanguageFeatures LanguageFeatures => editor.LanguageFeatures;

    public MarkerDecorationsService? MarkerDecorationsService => editor.MarkerDecorationsService;

    public EditorViewModel? ViewModel => editor.ViewModel;

    public HoverOptions HoverOptions => options();
}
