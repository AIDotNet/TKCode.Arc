// Ported from vscode (src/vs/editor/test/common/services/markerDecorationsService.test.ts, commit b57d5c9e), MIT
// license. All 8 upstream tests. There is no model service: a model is created with TestTextModel and added to the
// decoration service with AddModel (upstream's onModelAdded), which disposing the model's registration undoes
// (onModelRemoved). The marker service's event is synchronous, so the awaits on it are gone. URI.from({ scheme:
// vscode-remote, authority }) is a Uri with that scheme and authority. Below them, tests of what the decorations look
// like (class names, z-index, word growth) written for this port.

using Shouldly;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using TKCode.Arc.Editor.Services;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class MarkerDecorationsServiceTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private readonly MarkerService _markerService = new();
    private MarkerDecorationsService _decorationService;

    public MarkerDecorationsServiceTests()
    {
        _decorationService = new MarkerDecorationsService(_markerService);
    }

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }

        _decorationService.Dispose();
        _markerService.Dispose();
    }

    private TextModel CreateModel(Uri? uri = null, MarkerDecorationsService? service = null)
    {
        var model = TestTextModel.Create(string.Join('\n', Enumerable.Repeat("value", 5100)), uri: uri ?? new Uri("file:///diagnostics.txt"));
        _disposables.Add(model);
        _disposables.Add((service ?? _decorationService).AddModel(model));
        return model;
    }

    private static List<MarkerData> CreateMarkers(int count)
    {
        MarkerSeverity[] severities = [MarkerSeverity.Error, MarkerSeverity.Warning, MarkerSeverity.Info, MarkerSeverity.Hint];
        return Enumerable.Range(0, count).Select(index => new MarkerData
        {
            Severity = severities[index % severities.Length],
            Message = $"problem {index + 1}",
            StartLineNumber = index + 1,
            StartColumn = 1,
            EndLineNumber = index + 1,
            EndColumn = 2,
        }).ToList();
    }

    private void ChangeMarkers(TextModel model, int count, string owner = "test", bool isTruncated = false) =>
        _markerService.ChangeOne(owner, model.Uri, CreateMarkers(count), new MarkerChangeOptions { IsTruncated = isTruncated });

    private string[] DecorationIds(TextModel model) => model.GetAllDecorations().Select(d => d.Id).ToArray();

    [Fact]
    public void Reports_overflow_only_above_5000_while_preserving_all_diagnostics()
    {
        var model = CreateModel();
        var states = new List<(int Diagnostics, int Decorations, int? Limited)>();
        foreach (var count in new[] { 4999, 5000, 5001, 5100, 5000, 0 })
        {
            ChangeMarkers(model, count);
            states.Add((
                _markerService.Read(new() { Resource = model.Uri }).Count,
                _decorationService.GetLiveMarkers(model.Uri).Count,
                _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated));
        }

        states.ShouldBe(
        [
            (4999, 4999, null),
            (5000, 5000, null),
            (5001, 5000, 5000),
            (5100, 5000, 5000),
            (5000, 5000, null),
            (0, 0, null),
        ]);
    }

    [Fact]
    public void Separately_notifies_when_only_the_limit_changes_without_replacing_decorations()
    {
        var model = CreateModel();
        ChangeMarkers(model, 5000);
        var decorationIds = DecorationIds(model);
        var markerChanges = new List<Uri>();
        _decorationService.DidChangeMarker += m => markerChanges.Add(m.Uri);
        var exceededLimitChanges = new List<int?>();
        _decorationService.DidChangeMarkerLimit += m => exceededLimitChanges.Add(_decorationService.GetMarkerLimits(m.Uri)?.IsMarkersTruncated);

        ChangeMarkers(model, 1, "overflow");
        ChangeMarkers(model, 2, "overflow");
        ChangeMarkers(model, 0, "overflow");

        markerChanges.ShouldBeEmpty();
        exceededLimitChanges.ShouldBe([5000, null]);
        DecorationIds(model).ShouldBe(decorationIds);
    }

    [Fact]
    public void Reports_diagnostic_truncation_independently_of_the_decoration_limit()
    {
        var model = CreateModel();
        ChangeMarkers(model, 5001);
        var decorationIds = DecorationIds(model);
        var markerChanges = new List<Uri>();
        _decorationService.DidChangeMarker += m => markerChanges.Add(m.Uri);
        var limitChanges = new List<MarkerLimit?>();
        _decorationService.DidChangeMarkerLimit += m => limitChanges.Add(_decorationService.GetMarkerLimits(m.Uri));

        ChangeMarkers(model, 1, "truncated", true);
        var truncatedDecorationIds = DecorationIds(model);
        ChangeMarkers(model, 1, "truncated");
        var restoredDecorationIds = DecorationIds(model);
        ChangeMarkers(model, 0);
        ChangeMarkers(model, 1, "truncated", true);

        limitChanges.ShouldBe(
        [
            new MarkerLimit(true, 5000),
            new MarkerLimit(false, 5000),
            new MarkerLimit(false, null),
            new MarkerLimit(true, null),
        ]);
        markerChanges.ShouldBe([model.Uri, model.Uri]);
        truncatedDecorationIds.ShouldBe(decorationIds);
        restoredDecorationIds.ShouldBe(decorationIds);
    }

    [Fact]
    public void Honors_resource_filters_and_restores_the_limit_when_filtering_ends()
    {
        var model = CreateModel();
        ChangeMarkers(model, 5100);

        var filter = _markerService.InstallResourceFilter(model.Uri, "test");
        var filteredState = (
            _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated,
            _decorationService.GetLiveMarkers(model.Uri).Count,
            _markerService.Read(new() { Resource = model.Uri, IgnoreResourceFilters = true }).Count);

        filter.Dispose();

        filteredState.ShouldBe((null, 1, 5100));
        _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated.ShouldBe(5000);
        _decorationService.GetLiveMarkers(model.Uri).Count.ShouldBe(5000);
    }

    [Fact]
    public void Preserves_suppression_after_applying_the_decoration_limit()
    {
        var model = CreateModel();
        ChangeMarkers(model, 5001);
        var suppression = _decorationService.AddMarkerSuppression(model.Uri, new TextRange(1, 1, 100, 2));
        var suppressedLimited = _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated;
        var suppressedLines = _decorationService.GetLiveMarkers(model.Uri).Select(m => m.Range.StartLineNumber).Order().ToArray();

        suppression.Dispose();

        suppressedLimited.ShouldBe(5000);
        suppressedLines.ShouldBe(Enumerable.Range(101, 4900).ToArray());
        _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated.ShouldBe(5000);
        _decorationService.GetLiveMarkers(model.Uri).Count.ShouldBe(5000);
    }

    [Fact]
    public void Keeps_limits_separate_for_resources_with_the_same_path()
    {
        var local = CreateModel();
        var remote = CreateModel(new Uri("vscode-remote://first" + local.Uri.AbsolutePath));
        var otherRemote = CreateModel(new Uri("vscode-remote://second" + local.Uri.AbsolutePath));
        ChangeMarkers(local, 5001);
        ChangeMarkers(remote, 5000);
        ChangeMarkers(otherRemote, 5100);

        new[] { local, remote, otherRemote }.Select(model => _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated)
            .ShouldBe([5000, null, 5000]);
    }

    [Fact]
    public void Clears_the_limit_on_model_disposal_and_restores_it_when_reopening()
    {
        var model = TestTextModel.Create(string.Join('\n', Enumerable.Repeat("value", 5100)), uri: new Uri("file:///diagnostics.txt"));
        var registration = _decorationService.AddModel(model);
        ChangeMarkers(model, 5001);
        registration.Dispose();
        model.Dispose();
        var afterDisposal = _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated;
        var reopened = CreateModel(model.Uri);

        afterDisposal.ShouldBeNull();
        _decorationService.GetMarkerLimits(reopened.Uri)?.IsMarkersTruncated.ShouldBe(5000);
        _decorationService.GetLiveMarkers(reopened.Uri).Count.ShouldBe(5000);
    }

    [Fact]
    public void Initializes_limits_for_existing_models_and_diagnostics()
    {
        var model = CreateModel();
        ChangeMarkers(model, 5001);
        _decorationService.Dispose();
        _decorationService = new MarkerDecorationsService(_markerService);
        _disposables.Add(_decorationService.AddModel(model));

        _decorationService.GetMarkerLimits(model.Uri)?.IsMarkersTruncated.ShouldBe(5000);
        _decorationService.GetLiveMarkers(model.Uri).Count.ShouldBe(5000);
    }

    // ---- not upstream: what the decorations look like

    [Fact]
    public void Each_severity_gets_its_squiggle_class_and_z_index()
    {
        using var model = TestTextModel.Create("alpha beta gamma delta", uri: new Uri("file:///look.txt"));
        using var registration = _decorationService.AddModel(model);
        _markerService.ChangeOne("o", model.Uri,
        [
            new MarkerData { Severity = MarkerSeverity.Error, Message = "e", StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 6 },
            new MarkerData { Severity = MarkerSeverity.Warning, Message = "w", StartLineNumber = 1, StartColumn = 7, EndLineNumber = 1, EndColumn = 11 },
            new MarkerData { Severity = MarkerSeverity.Info, Message = "i", StartLineNumber = 1, StartColumn = 12, EndLineNumber = 1, EndColumn = 17 },
            new MarkerData { Severity = MarkerSeverity.Hint, Message = "h", StartLineNumber = 1, StartColumn = 18, EndLineNumber = 1, EndColumn = 23 },
        ]);

        var decorations = model.GetAllDecorations().OrderBy(d => d.Range.StartColumn).ToArray();
        decorations.Select(d => d.Options.ClassName).ShouldBe(
        [
            DecorationClassNames.EditorErrorDecoration, DecorationClassNames.EditorWarningDecoration,
            DecorationClassNames.EditorInfoDecoration, DecorationClassNames.EditorHintDecoration,
        ]);
        decorations.Select(d => d.Options.ZIndex).ShouldBe([30, 20, 10, 0]);

        // a hint is three characters wide, never more
        decorations[3].Range.ShouldBe(new TextRange(1, 18, 1, 20));

        // the decoration leads back to its marker
        _decorationService.GetMarker(model.Uri, decorations[0])!.Message.ShouldBe("e");
    }

    [Fact]
    public void An_empty_marker_grows_to_the_word_and_tags_get_inline_classes()
    {
        using var model = TestTextModel.Create("let value = other;", uri: new Uri("file:///word.txt"));
        using var registration = _decorationService.AddModel(model);
        _markerService.ChangeOne("o", model.Uri,
        [
            new MarkerData { Severity = MarkerSeverity.Error, Message = "e", StartLineNumber = 1, StartColumn = 6, EndLineNumber = 1, EndColumn = 6 },
            new MarkerData { Severity = MarkerSeverity.Hint, Message = "u", StartLineNumber = 1, StartColumn = 13, EndLineNumber = 1, EndColumn = 18, Tags = [MarkerTag.Unnecessary] },
        ]);

        var decorations = model.GetAllDecorations().OrderBy(d => d.Range.StartColumn).ToArray();
        decorations[0].Range.ShouldBe(new TextRange(1, 5, 1, 10));
        decorations[1].Options.ClassName.ShouldBe(DecorationClassNames.EditorUnnecessaryDecoration);
        decorations[1].Options.InlineClassName.ShouldBe(DecorationClassNames.EditorUnnecessaryInlineDecoration);
    }

    [Fact]
    public void Removing_an_inmemory_model_removes_its_markers()
    {
        var model = TestTextModel.Create("x");
        var registration = _decorationService.AddModel(model);
        _markerService.ChangeOne("o", model.Uri, [new MarkerData { Severity = MarkerSeverity.Error, Message = "e", StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 2 }]);
        model.GetAllDecorations().Count.ShouldBe(1);

        registration.Dispose();

        model.GetAllDecorations().ShouldBeEmpty();
        _markerService.Read(new() { Resource = model.Uri }).ShouldBeEmpty();
        model.Dispose();
    }
}
