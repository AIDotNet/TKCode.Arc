// The hover editor of the engine tests: a TestCodeEditor (model and view model, word wrap) with its own language
// features and markers, as upstream's withTestCodeEditor gives a hover participant.

using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using TKCode.Arc.Editor.Services;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Tests;

internal sealed class TestHoverEditor : IHoverEditor, IDisposable
{
    private readonly TestCodeEditor _editor;
    private readonly IDisposable _markerRegistration;

    public TestHoverEditor(string text, TestCodeEditorOptions? options = null, Uri? uri = null)
    {
        var model = TestTextModel.Create(text, uri: uri ?? new Uri("file:///test.txt"));
        _editor = TestCodeEditor.Create(model, options);
        MarkerService = new MarkerService();
        MarkerDecorationsService = new MarkerDecorationsService(MarkerService);
        _markerRegistration = MarkerDecorationsService.AddModel(model);
        OwnedModel = model;
    }

    private TextModel OwnedModel { get; }

    public TextModel? Model => OwnedModel;

    public TextModel TextModel => OwnedModel;

    public LanguageFeatures LanguageFeatures { get; } = new();

    public MarkerService MarkerService { get; }

    public MarkerDecorationsService? MarkerDecorationsService { get; }

    public EditorViewModel? ViewModel => _editor.ViewModel;

    public HoverOptions HoverOptions { get; set; } = HoverOptions.Default;

    public IReadOnlyList<IModelDecoration> GetLineDecorations(int lineNumber) => OwnedModel.GetLineDecorations(lineNumber);

    public void SetMarkers(params MarkerData[] markers) => MarkerService.ChangeOne("test", OwnedModel.Uri, markers);

    public void Dispose()
    {
        _markerRegistration.Dispose();
        MarkerDecorationsService!.Dispose();
        MarkerService.Dispose();
        _editor.Dispose();
        OwnedModel.Dispose();
    }
}
