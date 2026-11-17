using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Developer;

public class IntegrationsQueryTests
{
    private static readonly Dictionary<string, string> Labels = new() { ["files"] = "Files", ["insights"] = "Insights" };

    private static string? Label(string? key) => key is not null && Labels.TryGetValue(key, out var label) ? label : null;

    private static List<IntegrationItem> Sample() =>
    [
        new() { Key = "a", Name = "Vaultline", Category = "files", Description = "Mirror design files into boards." },
        new() { Key = "b", Name = "Pulsegrid", Category = "insights", Description = "Chart adoption.", Status = IntegrationStatus.Connected },
        new() { Key = "c", Name = "Coldstore", Category = "files", Description = "Keep every invoice for seven years.", Status = IntegrationStatus.Connecting },
        new() { Key = "d", Name = "Tallymark", Category = "insights", Description = "Count visitors.", Status = IntegrationStatus.Error },
    ];

    [Fact]
    public void Normalize_trims_and_lowers()
    {
        IntegrationsQuery.Normalize("  Files ").ShouldBe("files");
        IntegrationsQuery.Normalize(null).ShouldBe(string.Empty);
    }

    [Fact]
    public void Query_matches_name_category_label_and_description_ignoring_case()
    {
        var items = Sample();
        IntegrationsQuery.Visible(items, "all", "VAULT", Label).Select(i => i.Key).ShouldBe(["a"]);
        IntegrationsQuery.Visible(items, "all", "files", Label).Select(i => i.Key).ShouldBe(["a", "c"]);
        IntegrationsQuery.Visible(items, "all", " invoice ", Label).Select(i => i.Key).ShouldBe(["c"]);
        IntegrationsQuery.Visible(items, "all", "nothing", Label).ShouldBeEmpty();
    }

    [Fact]
    public void Filter_and_query_combine_and_keep_order()
    {
        var items = Sample();
        IntegrationsQuery.Visible(items, "insights", null, Label).Select(i => i.Key).ShouldBe(["b", "d"]);
        IntegrationsQuery.Visible(items, "insights", "count", Label).Select(i => i.Key).ShouldBe(["d"]);
        IntegrationsQuery.Visible(items, "files", "chart", Label).ShouldBeEmpty();
    }

    [Fact]
    public void Connected_filter_takes_connected_apps_only()
    {
        var items = Sample();
        IntegrationsQuery.Visible(items, "connected", "", Label).Select(i => i.Key).ShouldBe(["b"]);
        IntegrationsQuery.Count(items, "connected").ShouldBe(1);
    }

    [Fact]
    public void Counts_ignore_the_search()
    {
        var items = Sample();
        IntegrationsQuery.Count(items, "all").ShouldBe(4);
        IntegrationsQuery.Count(items, "files").ShouldBe(2);
        IntegrationsQuery.Count(items, "unknown").ShouldBe(0);
    }

    [Fact]
    public void Generated_mark_is_stable_and_uses_the_monogram()
    {
        IntegrationMark.SeriesIndex("chirp").ShouldBe(IntegrationMark.SeriesIndex("chirp"));
        IntegrationMark.SeriesIndex("chirp").ShouldBeInRange(0, 3);
        IntegrationMark.MonogramOf(new IntegrationItem { Name = "inkwell Mail" }).ShouldBe("I");
        IntegrationMark.MonogramOf(new IntegrationItem { Name = "Inkwell Mail", Monogram = "im" }).ShouldBe("IM");
        IntegrationMark.MonogramOf(new IntegrationItem()).ShouldBe(string.Empty);
    }
}
