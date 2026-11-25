// Ported from vscode (src/vs/platform/markers/test/common/markerService.test.ts, commit b57d5c9e), MIT license.
// All 20 upstream tests (the changeOne / changeAll pruning test is one theory with two cases). The pruning test reads
// the service's index sizes through MarkerService.IndexSizes instead of its private maps. MarkerChanged is synchronous
// here, which the event test relies on as upstream's does on its listener running before the end of the test.

using Shouldly;
using TKCode.Arc.Editor.Platform.Markers;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class MarkerServiceTests : IDisposable
{
    private readonly MarkerService _service = new();

    public void Dispose() => _service.Dispose();

    private static MarkerData RandomMarkerData(MarkerSeverity severity = MarkerSeverity.Error) => new()
    {
        Severity = severity,
        Message = Random.Shared.NextDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        StartLineNumber = 1,
        StartColumn = 1,
        EndLineNumber = 1,
        EndColumn = 1,
    };

    private static Uri U(string uri) => new(uri);

    [Fact]
    public void Query()
    {
        _service.ChangeAll("far", [new ResourceMarker(U("file:///c/test/file.cs"), RandomMarkerData(MarkerSeverity.Error))]);

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(1);
        _service.Read(new() { Resource = U("file:///c/test/file.cs") }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "far", Resource = U("file:///c/test/file.cs") }).Count.ShouldBe(1);

        _service.ChangeAll("boo", [new ResourceMarker(U("file:///c/test/file.cs"), RandomMarkerData(MarkerSeverity.Warning))]);

        _service.Read().Count.ShouldBe(2);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "boo" }).Count.ShouldBe(1);

        _service.Read(new() { Severities = MarkerSeverity.Error }).Count.ShouldBe(1);
        _service.Read(new() { Severities = MarkerSeverity.Warning }).Count.ShouldBe(1);
        _service.Read(new() { Severities = MarkerSeverity.Hint }).Count.ShouldBe(0);
        _service.Read(new() { Severities = MarkerSeverity.Error | MarkerSeverity.Warning }).Count.ShouldBe(2);
    }

    [Fact]
    public void ChangeOne_override()
    {
        _service.ChangeOne("far", U("file:///path/only.cs"), [RandomMarkerData()]);
        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(1);

        _service.ChangeOne("boo", U("file:///path/only.cs"), [RandomMarkerData()]);
        _service.Read().Count.ShouldBe(2);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "boo" }).Count.ShouldBe(1);

        _service.ChangeOne("far", U("file:///path/only.cs"), [RandomMarkerData(), RandomMarkerData()]);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(2);
        _service.Read(new() { Owner = "boo" }).Count.ShouldBe(1);
    }

    [Fact]
    public void ChangeOne_All_clears()
    {
        _service.ChangeOne("far", U("file:///path/only.cs"), [RandomMarkerData()]);
        _service.ChangeOne("boo", U("file:///path/only.cs"), [RandomMarkerData()]);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "boo" }).Count.ShouldBe(1);
        _service.Read().Count.ShouldBe(2);

        _service.ChangeOne("far", U("file:///path/only.cs"), []);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(0);
        _service.Read(new() { Owner = "boo" }).Count.ShouldBe(1);
        _service.Read().Count.ShouldBe(1);

        _service.ChangeAll("boo", []);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(0);
        _service.Read(new() { Owner = "boo" }).Count.ShouldBe(0);
        _service.Read().Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("changeOne")]
    [InlineData("changeAll")]
    public void Change_prunes_empty_indexes_and_preserves_remaining_associations(string method)
    {
        var resource = U("file:///path/shared.cs");
        var otherResource = U("file:///path/other.cs");
        _service.ChangeOne("first", resource, [RandomMarkerData()]);
        _service.ChangeOne("first", otherResource, [RandomMarkerData()]);
        _service.ChangeOne("second", resource, [RandomMarkerData()]);

        void Clear(string owner, Uri r)
        {
            if (method == "changeOne")
            {
                _service.ChangeOne(owner, r, []);
            }
            else
            {
                _service.ChangeAll(owner, []);
            }
        }

        (int Resources, int Owners, string[] Markers) Snapshot()
        {
            var (resources, owners) = _service.IndexSizes;
            return (resources, owners, _service.Read().Select(m => $"{m.Owner}:{m.Resource.AbsolutePath}").Order().ToArray());
        }

        Clear("first", otherResource);
        var afterFirstClear = Snapshot();
        Clear("first", resource);
        var afterOwnerClear = Snapshot();
        Clear("second", resource);
        var afterFinalClear = Snapshot();

        afterFirstClear.Resources.ShouldBe(1);
        afterFirstClear.Owners.ShouldBe(method == "changeOne" ? 2 : 1);
        afterFirstClear.Markers.ShouldBe(method == "changeOne"
            ? ["first:/path/shared.cs", "second:/path/shared.cs"]
            : ["second:/path/shared.cs"]);
        afterOwnerClear.Resources.ShouldBe(1);
        afterOwnerClear.Owners.ShouldBe(1);
        afterOwnerClear.Markers.ShouldBe(["second:/path/shared.cs"]);
        afterFinalClear.Resources.ShouldBe(0);
        afterFinalClear.Owners.ShouldBe(0);
        afterFinalClear.Markers.ShouldBeEmpty();
    }

    [Fact]
    public void ChangeAll_sends_event_for_cleared()
    {
        _service.ChangeAll("far",
        [
            new ResourceMarker(U("file:///d/path"), RandomMarkerData()),
            new ResourceMarker(U("file:///d/path"), RandomMarkerData()),
        ]);

        _service.Read(new() { Owner = "far" }).Count.ShouldBe(2);

        var calls = 0;
        void Listener(IReadOnlyList<Uri> changedResources)
        {
            calls++;
            changedResources.Count.ShouldBe(1);
            changedResources.ShouldAllBe(u => u.ToString() == "file:///d/path");
            _service.Read(new() { Owner = "far" }).Count.ShouldBe(0);
        }

        _service.MarkerChanged += Listener;
        _service.ChangeAll("far", []);
        _service.MarkerChanged -= Listener;
        calls.ShouldBe(1);
    }

    [Fact]
    public void ChangeAll_merges()
    {
        _service.ChangeAll("far",
        [
            new ResourceMarker(U("file:///c/test/file.cs"), RandomMarkerData()),
            new ResourceMarker(U("file:///c/test/file.cs"), RandomMarkerData()),
        ]);

        _service.Read(new() { Owner = "far" }).Count.ShouldBe(2);
    }

    [Fact]
    public void ChangeAll_must_not_break_integrity_issue_12635()
    {
        _service.ChangeAll("far",
        [
            new ResourceMarker(U("scheme:path1"), RandomMarkerData()),
            new ResourceMarker(U("scheme:path2"), RandomMarkerData()),
        ]);

        _service.ChangeAll("boo", [new ResourceMarker(U("scheme:path1"), RandomMarkerData())]);

        _service.ChangeAll("far",
        [
            new ResourceMarker(U("scheme:path1"), RandomMarkerData()),
            new ResourceMarker(U("scheme:path2"), RandomMarkerData()),
        ]);

        _service.Read(new() { Owner = "far" }).Count.ShouldBe(2);
        _service.Read(new() { Resource = U("scheme:path1") }).Count.ShouldBe(2);
    }

    [Fact]
    public void Invalid_marker_data()
    {
        var data = RandomMarkerData() with { Message = null };
        _service.ChangeOne("far", U("some:uri/path"), [data]);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(0);

        data = data with { Message = "" };
        _service.ChangeOne("far", U("some:uri/path"), [data]);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(0);

        data = data with { Message = "null" };
        _service.ChangeOne("far", U("some:uri/path"), [data]);
        _service.Read(new() { Owner = "far" }).Count.ShouldBe(1);
    }

    [Fact]
    public void MapMap_remove_returns_bad_values_issue_13548()
    {
        _service.ChangeOne("o", U("some:uri/1"), [RandomMarkerData()]);
        _service.ChangeOne("o", U("some:uri/2"), []);
    }

    [Fact]
    public void Error_code_of_zero_in_markers_get_removed_31275()
    {
        var data = new MarkerData
        {
            Code = "0",
            StartLineNumber = 1,
            StartColumn = 2,
            EndLineNumber = 1,
            EndColumn = 5,
            Message = "test",
            Severity = 0,
            Source = "me",
        };

        _service.ChangeOne("far", U("some:thing"), [data]);
        var marker = _service.Read(new() { Resource = U("some:thing") });

        marker.Count.ShouldBe(1);
        marker[0].Code.ShouldBe("0");
    }

    [Fact]
    public void ModelVersionId_is_preserved_on_IMarker_when_present_in_IMarkerData()
    {
        var resource = U("file:///path/file.ts");

        // Test with modelVersionId present
        var dataWithVersion = RandomMarkerData() with { ModelVersionId = 42 };
        _service.ChangeOne("owner", resource, [dataWithVersion]);

        var markersWithVersion = _service.Read(new() { Resource = resource });
        markersWithVersion.Count.ShouldBe(1);
        markersWithVersion[0].ModelVersionId.ShouldBe(42);

        // Test without modelVersionId (should be undefined)
        _service.ChangeOne("owner", resource, [RandomMarkerData()]);

        var markersWithoutVersion = _service.Read(new() { Resource = resource });
        markersWithoutVersion.Count.ShouldBe(1);
        markersWithoutVersion[0].ModelVersionId.ShouldBeNull();
    }

    [Fact]
    public void Tracks_diagnostic_truncation_per_resource_across_owners()
    {
        var resource = U("file:///path/file.ts");
        var otherResource = U("file:///path/other.ts");
        _service.ChangeOne("first", resource, [RandomMarkerData()], new MarkerChangeOptions { IsTruncated = true });
        _service.ChangeOne("second", resource, [RandomMarkerData()]);
        _service.ChangeOne("first", resource, [RandomMarkerData()]);
        _service.ChangeOne("first", otherResource, [RandomMarkerData()], new MarkerChangeOptions { IsTruncated = true });
        bool[] afterChangeAll = [_service.IsSourceTruncated(resource), _service.IsSourceTruncated(otherResource)];
        _service.ChangeAll("first", []);

        afterChangeAll.ShouldBe([false, true]);
        new[] { _service.IsSourceTruncated(resource), _service.IsSourceTruncated(otherResource) }.ShouldBe([false, false]);
    }

    [Fact]
    public void Resource_filter_hides_markers_for_the_filtered_resource()
    {
        var resource1 = U("file:///path/file1.cs");
        var resource2 = U("file:///path/file2.cs");

        _service.ChangeOne("owner1", resource1, [RandomMarkerData()]);
        _service.ChangeOne("owner1", resource2, [RandomMarkerData()]);

        _service.Read().Count.ShouldBe(2);
        _service.Read(new() { Resource = resource1 }).Count.ShouldBe(1);
        _service.Read(new() { Resource = resource2 }).Count.ShouldBe(1);

        var filter = _service.InstallResourceFilter(resource1, "Test filter");

        _service.Read().Count.ShouldBe(2); // 1 real + 1 info
        _service.Read(new() { Resource = resource1 }).Count.ShouldBe(1); // 1 info
        _service.Read(new() { Resource = resource2 }).Count.ShouldBe(1);

        filter.Dispose();

        _service.Read().Count.ShouldBe(2);
        _service.Read(new() { Resource = resource1 }).Count.ShouldBe(1);
        _service.Read(new() { Resource = resource2 }).Count.ShouldBe(1);
    }

    [Fact]
    public void Resource_filter_hides_markers_for_the_filtered_resource_UNLESS_explicit_read()
    {
        var resource1 = U("file:///path/file1.cs");
        var resource2 = U("file:///path/file2.cs");

        _service.ChangeOne("owner1", resource1, [RandomMarkerData()]);
        _service.ChangeOne("owner1", resource2, [RandomMarkerData()]);

        var filter = _service.InstallResourceFilter(resource1, "Test filter");

        _service.Read().Count.ShouldBe(2); // 1 real + 1 info
        _service.Read(new() { Resource = resource1 }).Count.ShouldBe(1); // 1 info
        _service.Read(new() { Resource = resource2 }).Count.ShouldBe(1);

        _service.Read(new() { IgnoreResourceFilters = true }).Count.ShouldBe(2);
        _service.Read(new() { Resource = resource1, IgnoreResourceFilters = true }).Count.ShouldBe(1);
        _service.Read(new() { Resource = resource1, IgnoreResourceFilters = true })[0].Severity.ShouldBe(MarkerSeverity.Error);
        _service.Read(new() { Resource = resource2, IgnoreResourceFilters = true }).Count.ShouldBe(1);
        _service.Read(new() { Resource = resource2, IgnoreResourceFilters = true })[0].Severity.ShouldBe(MarkerSeverity.Error);

        filter.Dispose();
    }

    [Fact]
    public void Resource_filter_affects_all_filter_combinations()
    {
        var resource = U("file:///path/file.cs");

        _service.ChangeOne("owner1", resource, [RandomMarkerData(MarkerSeverity.Error)]);
        _service.ChangeOne("owner2", resource, [RandomMarkerData(MarkerSeverity.Warning)]);

        _service.Read().Count.ShouldBe(2);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(2);
        _service.Read(new() { Owner = "owner1" }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "owner2" }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "owner1", Resource = resource }).Count.ShouldBe(1);
        _service.Read(new() { Severities = MarkerSeverity.Error }).Count.ShouldBe(1);
        _service.Read(new() { Severities = MarkerSeverity.Warning }).Count.ShouldBe(1);

        var filter = _service.InstallResourceFilter(resource, "Filter reason");

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "owner1" }).Count.ShouldBe(1);
        _service.Read(new() { Owner = "owner2" }).Count.ShouldBe(1);

        var ownerResourceMarkers = _service.Read(new() { Owner = "owner1", Resource = resource });
        ownerResourceMarkers.Count.ShouldBe(1);
        ownerResourceMarkers[0].Severity.ShouldBe(MarkerSeverity.Info);
        ownerResourceMarkers[0].Owner.ShouldBe("markersFilter");

        _service.Read(new() { Severities = MarkerSeverity.Error }).Count.ShouldBe(1);
        _service.Read(new() { Severities = MarkerSeverity.Warning }).Count.ShouldBe(1);
        _service.Read(new() { Severities = MarkerSeverity.Info }).Count.ShouldBe(1);

        filter.Dispose();
        _service.Read().Count.ShouldBe(2);
    }

    [Fact]
    public void Multiple_filters_for_same_resource_are_handled_correctly()
    {
        var resource = U("file:///path/file.cs");
        _service.ChangeOne("owner1", resource, [RandomMarkerData()]);

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);

        var filter1 = _service.InstallResourceFilter(resource, "First filter");
        var filter2 = _service.InstallResourceFilter(resource, "Second filter");

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);

        filter1.Dispose();

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);

        filter2.Dispose();

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);
    }

    [Fact]
    public void Resource_filter_with_reason_shows_info_marker_when_markers_are_filtered()
    {
        var resource = U("file:///path/file.cs");
        _service.ChangeOne("owner1", resource, [RandomMarkerData(MarkerSeverity.Error), RandomMarkerData(MarkerSeverity.Warning)]);

        _service.Read().Count.ShouldBe(2);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(2);

        const string filterReason = "Test filter reason";
        var filter = _service.InstallResourceFilter(resource, filterReason);

        var markers = _service.Read(new() { Resource = resource });
        markers.Count.ShouldBe(1);
        markers[0].Severity.ShouldBe(MarkerSeverity.Info);
        markers[0].Message.ShouldContain(filterReason);

        filter.Dispose();
        _service.Read(new() { Resource = resource }).Count.ShouldBe(2);
    }

    [Fact]
    public void Reading_all_markers_shows_info_marker_for_filtered_resources()
    {
        var resource1 = U("file:///path/file1.cs");
        var resource2 = U("file:///path/file2.cs");
        _service.ChangeOne("owner1", resource1, [RandomMarkerData()]);
        _service.ChangeOne("owner1", resource2, [RandomMarkerData()]);
        _service.Read().Count.ShouldBe(2);

        const string filterReason = "Resource is being edited";
        var filter = _service.InstallResourceFilter(resource1, filterReason);

        var allMarkers = _service.Read();
        allMarkers.Count.ShouldBe(2);

        var infoMarker = allMarkers.FirstOrDefault(marker => marker.Owner == "markersFilter" && marker.Severity == MarkerSeverity.Info);
        infoMarker.ShouldNotBeNull();
        infoMarker.Resource.ToString().ShouldBe(resource1.ToString());
        infoMarker.Message.ShouldContain(filterReason);

        filter.Dispose();
    }

    [Fact]
    public void Out_of_order_filter_disposal_works_correctly()
    {
        var resource = U("file:///path/file.cs");
        _service.ChangeOne("owner1", resource, [RandomMarkerData()]);

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);

        var filter1 = _service.InstallResourceFilter(resource, "First filter");
        var filter2 = _service.InstallResourceFilter(resource, "Second filter");
        var filter3 = _service.InstallResourceFilter(resource, "Third filter");

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);

        filter2.Dispose();

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);

        var markers = _service.Read(new() { Resource = resource });
        markers[0].Message.ShouldContain("Problems are paused because");

        filter3.Dispose();
        filter1.Dispose();

        _service.Read().Count.ShouldBe(1);
        _service.Read(new() { Resource = resource }).Count.ShouldBe(1);
    }

    // ---- not upstream

    [Fact]
    public void Statistics_count_by_severity_and_leave_out_inmemory_resources()
    {
        _service.ChangeOne("o", U("file:///a.cs"), [RandomMarkerData(MarkerSeverity.Error), RandomMarkerData(MarkerSeverity.Warning), RandomMarkerData(MarkerSeverity.Hint)]);
        _service.ChangeOne("o", U("inmemory://model/1"), [RandomMarkerData(MarkerSeverity.Error)]);

        _service.GetStatistics().ShouldBe(new MarkerStatistics(1, 1, 0, 1));
    }

    [Fact]
    public void MakeKey_joins_the_fields()
    {
        var data = new MarkerData { Source = "s", Code = "c¦d", Severity = MarkerSeverity.Warning, Message = "m", StartLineNumber = 1, StartColumn = 2, EndLineNumber = 3, EndColumn = 4 };
        MarkerData.MakeKey(data).ShouldBe("¦s¦c\\¦d¦Warning¦m¦1¦2¦3¦4¦");
        MarkerData.MakeKeyOptionalMessage(data, false).ShouldBe("¦s¦c\\¦d¦Warning¦¦1¦2¦3¦4¦");
    }
}
