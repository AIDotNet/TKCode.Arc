// Not in vscode: compares the ported diff computers with vscode's own on 406 inputs. The expected results were made by
// running vscode's DefaultLinesDiffComputer and LegacyLinesDiffComputer (commit b57d5c9e) on the inputs, see
// tools/editor/diff-fixtures. Upstream has no unit tests for the default computer besides its node fixtures, which
// the checkout this was ported from does not contain.

using System.Text.Json;
using Shouldly;
using TKCode.Arc.Editor.Diff;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Diff;

public sealed class DiffFixtureTests
{
    private static readonly Lazy<IReadOnlyList<Fixture>> s_fixtures = new(Load);

    public static TheoryData<string> FixtureNames()
    {
        var data = new TheoryData<string>();
        foreach (var fixture in s_fixtures.Value)
        {
            data.Add(fixture.Name);
        }

        return data;
    }

    [Fact]
    public void All_Fixtures_Are_Loaded() => s_fixtures.Value.Count.ShouldBe(406);

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Default_Computer_Matches_Upstream(string name)
    {
        var fixture = s_fixtures.Value.Single(f => f.Name == name);
        var diff = new DefaultLinesDiffComputer().ComputeDiff(fixture.Original, fixture.Modified, fixture.Options);

        Serialize(diff.Changes).ShouldBe(fixture.ExpectedChanges);
        SerializeMoves(diff.Moves).ShouldBe(fixture.ExpectedMoves);
        diff.HitTimeout.ShouldBe(fixture.ExpectedHitTimeout);

        // and the inner changes turn the original text into the modified text (when whitespace counts; ignoring it,
        // upstream's own verifyLinesDiff finds some of these diffs invalid too)
        if (!fixture.Options.IgnoreTrimWhitespace)
        {
            DocumentDiffComputer.VerifyLinesDiff(diff.Changes, fixture.Original, fixture.Modified, false).ShouldBe("valid");
        }
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Legacy_Computer_Matches_Upstream(string name)
    {
        var fixture = s_fixtures.Value.Single(f => f.Name == name);
        var diff = new LegacyLinesDiffComputer().ComputeDiff(fixture.Original, fixture.Modified, fixture.Options);

        Serialize(diff.Changes).ShouldBe(fixture.LegacyChanges);
        diff.HitTimeout.ShouldBe(false);
    }

    private static string Serialize(IReadOnlyList<DetailedLineRangeMapping> changes) =>
        string.Join(" | ", changes.Select(c =>
            $"{c.Original.StartLineNumber},{c.Original.EndLineNumberExclusive}->{c.Modified.StartLineNumber},{c.Modified.EndLineNumberExclusive}"
            + (c.InnerChanges is null
                ? " null"
                : " [" + string.Join("; ", c.InnerChanges.Select(i =>
                    $"{i.OriginalRange.StartLineNumber},{i.OriginalRange.StartColumn},{i.OriginalRange.EndLineNumber},{i.OriginalRange.EndColumn}"
                    + $"/{i.ModifiedRange.StartLineNumber},{i.ModifiedRange.StartColumn},{i.ModifiedRange.EndLineNumber},{i.ModifiedRange.EndColumn}")) + "]")));

    private static string SerializeMoves(IReadOnlyList<MovedText> moves) =>
        string.Join(" || ", moves.Select(m =>
            $"{m.LineRangeMapping.Original.StartLineNumber},{m.LineRangeMapping.Original.EndLineNumberExclusive}->"
            + $"{m.LineRangeMapping.Modified.StartLineNumber},{m.LineRangeMapping.Modified.EndLineNumberExclusive}: {Serialize(m.Changes)}"));

    private sealed record Fixture(
        string Name,
        string[] Original,
        string[] Modified,
        LinesDiffComputerOptions Options,
        string ExpectedChanges,
        string ExpectedMoves,
        bool ExpectedHitTimeout,
        string LegacyChanges);

    private static IReadOnlyList<Fixture> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Diff", "TestData", "diff-fixtures.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var result = new List<Fixture>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var options = item.GetProperty("options");
            var expected = item.GetProperty("expected");
            result.Add(new Fixture(
                item.GetProperty("name").GetString()!,
                item.GetProperty("original").EnumerateArray().Select(e => e.GetString()!).ToArray(),
                item.GetProperty("modified").EnumerateArray().Select(e => e.GetString()!).ToArray(),
                new LinesDiffComputerOptions
                {
                    IgnoreTrimWhitespace = options.GetProperty("ignoreTrimWhitespace").GetBoolean(),
                    MaxComputationTimeMs = options.GetProperty("maxComputationTimeMs").GetInt32(),
                    ComputeMoves = options.GetProperty("computeMoves").GetBoolean(),
                    ExtendToSubwords = options.TryGetProperty("extendToSubwords", out var sub) && sub.GetBoolean(),
                },
                SerializeJsonChanges(expected.GetProperty("changes")),
                string.Join(" || ", expected.GetProperty("moves").EnumerateArray().Select(m =>
                    $"{m[0].GetInt32()},{m[1].GetInt32()}->{m[2].GetInt32()},{m[3].GetInt32()}: {SerializeJsonChanges(m[4])}")),
                expected.GetProperty("hitTimeout").GetBoolean(),
                SerializeJsonChanges(item.GetProperty("legacy").GetProperty("changes"))));
        }

        return result;
    }

    private static string SerializeJsonChanges(JsonElement changes) =>
        string.Join(" | ", changes.EnumerateArray().Select(c =>
            $"{c[0].GetInt32()},{c[1].GetInt32()}->{c[2].GetInt32()},{c[3].GetInt32()}"
            + (c[4].ValueKind == JsonValueKind.Null
                ? " null"
                : " [" + string.Join("; ", c[4].EnumerateArray().Select(i =>
                    $"{Col(i[0])},{Col(i[1])},{Col(i[2])},{Col(i[3])}/{Col(i[4])},{Col(i[5])},{Col(i[6])},{Col(i[7])}")) + "]")));

    // Number.MAX_SAFE_INTEGER columns are int.MaxValue here
    private static string Col(JsonElement e) => e.TryGetInt32(out var v) ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
