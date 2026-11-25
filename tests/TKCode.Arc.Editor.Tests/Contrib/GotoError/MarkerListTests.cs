// Tests of the marker list behind next / previous problem (src/vs/editor/contrib/gotoError/browser/
// markerNavigationService.ts, commit b57d5c9e; upstream has no test file for it, these were written for the port).

using Shouldly;
using TKCode.Arc.Editor.Contrib.GotoError;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using TKCode.Arc.Editor.Services;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class MarkerListTests : IDisposable
{
    private static readonly Uri s_uri = new("file:///a.ts");
    private static readonly Uri s_other = new("file:///b.ts");

    private readonly MarkerService _markerService = new();
    private readonly MarkerDecorationsService _decorations;
    private readonly TextModel _model;

    public MarkerListTests()
    {
        _decorations = new MarkerDecorationsService(_markerService);
        _model = TestTextModel.Create("line one\nline two\nline three\nline four", uri: s_uri);
    }

    public void Dispose()
    {
        _model.Dispose();
        _decorations.Dispose();
        _markerService.Dispose();
    }

    private static MarkerData Data(MarkerSeverity severity, int line, int startColumn, int endColumn, string message = "m") => new()
    {
        Severity = severity,
        Message = message,
        StartLineNumber = line,
        StartColumn = startColumn,
        EndLineNumber = line,
        EndColumn = endColumn,
    };

    private MarkerList CreateList(MarkerSortOrder order = MarkerSortOrder.Severity) =>
        new(s_uri, _markerService, _decorations.GetWordAtPosition, order);

    [Fact]
    public void Lists_Errors_Warnings_And_Infos_Of_The_Resource_By_Severity_Then_Position()
    {
        _markerService.ChangeOne("lint", s_uri,
        [
            Data(MarkerSeverity.Warning, 1, 1, 5, "w1"),
            Data(MarkerSeverity.Error, 3, 1, 5, "e3"),
            Data(MarkerSeverity.Hint, 2, 1, 5, "hint"),
            Data(MarkerSeverity.Info, 2, 1, 5, "i2"),
            Data(MarkerSeverity.Error, 2, 1, 5, "e2"),
        ]);
        _markerService.ChangeOne("lint", s_other, [Data(MarkerSeverity.Error, 1, 1, 2, "other")]);

        using var list = CreateList();
        list.Markers.Select(m => m.Message).ShouldBe(["e2", "e3", "w1", "i2"]);

        using var byPosition = CreateList(MarkerSortOrder.Position);
        byPosition.Markers.Select(m => m.Message).ShouldBe(["w1", "e2", "i2", "e3"]);
    }

    [Fact]
    public void Move_Starts_At_The_Marker_At_Or_After_The_Cursor_And_Wraps()
    {
        _markerService.ChangeOne("lint", s_uri,
        [
            Data(MarkerSeverity.Error, 1, 1, 5, "a"),
            Data(MarkerSeverity.Error, 3, 1, 5, "b"),
        ]);
        using var list = CreateList(MarkerSortOrder.Position);

        list.Selected.ShouldBeNull();
        list.Move(true, _model, new TextPosition(2, 1)).ShouldBeTrue();
        list.Selected!.Marker.Message.ShouldBe("b");
        list.Selected.Index.ShouldBe(2);
        list.Selected.Total.ShouldBe(2);

        list.Move(true, _model, new TextPosition(2, 1)).ShouldBeTrue();
        list.Selected!.Marker.Message.ShouldBe("a");

        list.Move(false, _model, new TextPosition(2, 1)).ShouldBeTrue();
        list.Selected!.Marker.Message.ShouldBe("b");
    }

    [Fact]
    public void Previous_From_Between_Markers_Goes_To_The_One_Before()
    {
        _markerService.ChangeOne("lint", s_uri,
        [
            Data(MarkerSeverity.Error, 1, 1, 5, "a"),
            Data(MarkerSeverity.Error, 3, 1, 5, "b"),
        ]);
        using var list = CreateList(MarkerSortOrder.Position);

        list.Move(false, _model, new TextPosition(2, 1)).ShouldBeTrue();
        list.Selected!.Marker.Message.ShouldBe("a");
    }

    [Fact]
    public void Inside_A_Marker_Next_Selects_It_And_Past_The_Last_Wraps_To_The_First()
    {
        _markerService.ChangeOne("lint", s_uri,
        [
            Data(MarkerSeverity.Error, 1, 1, 5, "a"),
            Data(MarkerSeverity.Error, 3, 1, 5, "b"),
        ]);
        using var list = CreateList(MarkerSortOrder.Position);
        list.Move(true, _model, new TextPosition(3, 3));
        list.Selected!.Marker.Message.ShouldBe("b");

        list.ResetIndex();
        list.Move(true, _model, new TextPosition(4, 3));
        list.Selected!.Marker.Message.ShouldBe("a");

        list.ResetIndex();
        list.Move(false, _model, new TextPosition(4, 3));
        list.Selected!.Marker.Message.ShouldBe("b");
    }

    [Fact]
    public void An_Empty_Marker_Covers_Its_Word()
    {
        _markerService.ChangeOne("lint", s_uri, [Data(MarkerSeverity.Error, 2, 6, 6, "empty")]);
        using var list = CreateList();

        // the cursor in "two" is inside the word the empty marker grows to: not past it
        list.Move(false, _model, new TextPosition(2, 8));
        list.Selected!.Marker.Message.ShouldBe("empty");
    }

    [Fact]
    public void Changes_Of_The_Markers_Reset_The_Selection_And_Fire()
    {
        _markerService.ChangeOne("lint", s_uri, [Data(MarkerSeverity.Error, 1, 1, 5, "a")]);
        using var list = CreateList();
        var changes = 0;
        list.OnDidChange += () => changes++;
        list.Move(true, _model, new TextPosition(1, 1));
        list.Selected.ShouldNotBeNull();

        // the same markers again: nothing changes
        _markerService.ChangeOne("lint", s_uri, [Data(MarkerSeverity.Error, 1, 1, 5, "a")]);
        changes.ShouldBe(0);
        list.Selected.ShouldNotBeNull();

        // another resource: not this list's
        _markerService.ChangeOne("lint", s_other, [Data(MarkerSeverity.Error, 1, 1, 5, "x")]);
        changes.ShouldBe(0);

        _markerService.ChangeOne("lint", s_uri, [Data(MarkerSeverity.Error, 1, 1, 5, "a"), Data(MarkerSeverity.Warning, 2, 1, 2, "b")]);
        changes.ShouldBe(1);
        list.Selected.ShouldBeNull();
        list.Markers.Count.ShouldBe(2);
    }

    [Fact]
    public void Move_Without_Markers_Does_Nothing_And_Find_Finds_The_Marker_At_A_Position()
    {
        using var list = CreateList();
        list.Move(true, _model, new TextPosition(1, 1)).ShouldBeFalse();

        _markerService.ChangeOne("lint", s_uri, [Data(MarkerSeverity.Warning, 2, 1, 5, "w"), Data(MarkerSeverity.Error, 3, 2, 4, "e")]);
        list.Find(s_uri, new TextPosition(3, 3))!.Marker.Message.ShouldBe("e");
        list.Find(s_uri, new TextPosition(3, 3))!.Index.ShouldBe(1);
        list.Find(s_uri, new TextPosition(4, 1)).ShouldBeNull();
        list.Find(s_other, new TextPosition(1, 1)).ShouldBeNull();
    }

    [Fact]
    public void A_List_For_All_Resources_Starts_At_The_Model_Resource()
    {
        _markerService.ChangeOne("lint", s_other, [Data(MarkerSeverity.Error, 1, 1, 5, "other")]);
        _markerService.ChangeOne("lint", s_uri, [Data(MarkerSeverity.Error, 4, 1, 5, "mine")]);
        using var list = new MarkerList(_ => true, _markerService, _decorations.GetWordAtPosition);

        list.Matches(s_uri).ShouldBeTrue();
        list.Markers.Select(m => m.Message).ShouldBe(["mine", "other"]);
        list.Move(true, _model, new TextPosition(1, 1));
        list.Selected!.Marker.Message.ShouldBe("mine");
        list.Move(true, _model, new TextPosition(1, 1));
        list.Selected!.Marker.Message.ShouldBe("other");

        using var single = CreateList();
        single.Matches(s_uri).ShouldBeTrue();
        single.Matches(s_other).ShouldBeFalse();
        single.Matches(null).ShouldBeFalse();
    }
}
