using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>The pure rules of Env variables: .env parsing, key validation, fingerprints, order, conflicts and comparison.</summary>
public class EnvVariablesParserTests
{
    private static readonly string[] DevStaging = ["development", "staging"];

    private static EnvDotenvResult Parse(string text, IReadOnlyCollection<string>? envs = null, params EnvVariable[] existing) =>
        EnvVariableDotenvParser.Parse(text, envs ?? DevStaging, existing);

    [Fact]
    public void Blank_lines_and_comments_are_ignored_and_line_numbers_count_them()
    {
        var result = Parse("# header\n\nFIRST=1\n   # indented comment\nSECOND=2\n");
        result.Lines.Select(l => (l.LineNumber, l.Key)).ShouldBe([(3, "FIRST"), (5, "SECOND")]);
        result.ImportCount.ShouldBe(2);
        result.SkippedCount.ShouldBe(0);
    }

    [Fact]
    public void Crlf_line_ends_are_read_like_lf()
    {
        var result = Parse("A=one\r\nB=two\r\n\r\nC=three");
        result.Lines.Select(l => (l.Key, l.Value)).ShouldBe([("A", "one"), ("B", "two"), ("C", "three")]);
        result.Lines[2].LineNumber.ShouldBe(4);
    }

    [Fact]
    public void Export_prefix_is_dropped()
    {
        var line = Parse("export   RELAY_TOKEN=abc").Lines.Single();
        line.Key.ShouldBe("RELAY_TOKEN");
        line.Value.ShouldBe("abc");
        line.Status.ShouldBe(EnvDotenvStatus.New);
        // "exported=1" is a key, not a prefix.
        Parse("exported=1").Lines.Single().Key.ShouldBe("exported");
    }

    [Theory]
    [InlineData("K=\"quoted value\"", "quoted value")]
    [InlineData("K='single # not a comment'", "single # not a comment")]
    [InlineData("K=\"with trailing\" # comment", "with trailing")]
    [InlineData("K=plain # trailing comment", "plain")]
    [InlineData("K=hash#inside", "hash#inside")]
    [InlineData("K=  padded  ", "padded")]
    [InlineData("K=", "")]
    [InlineData("K=a=b=c", "a=b=c")]
    [InlineData("K=\"unterminated", "unterminated")]
    public void Values_strip_quotes_and_trailing_comments(string text, string expected)
    {
        var line = Parse(text).Lines.Single();
        line.Value.ShouldBe(expected);
        line.IsImported.ShouldBeTrue();
    }

    [Fact]
    public void Empty_value_is_still_new()
    {
        Parse("EMPTY=").Lines.Single().Status.ShouldBe(EnvDotenvStatus.New);
    }

    [Fact]
    public void Duplicates_keep_the_last_line()
    {
        var result = Parse("CACHE_TTL=45\nOTHER=1\nCACHE_TTL=60\nCACHE_TTL=90");
        result.Lines[0].Status.ShouldBe(EnvDotenvStatus.Duplicate);
        result.Lines[0].SetAgainOn.ShouldBe(4);
        result.Lines[2].Status.ShouldBe(EnvDotenvStatus.Duplicate);
        result.Lines[2].SetAgainOn.ShouldBe(4);
        result.Lines[3].Status.ShouldBe(EnvDotenvStatus.New);
        result.Lines[3].Value.ShouldBe("90");
        result.ImportCount.ShouldBe(2);
        result.SkippedCount.ShouldBe(2);
    }

    [Fact]
    public void Invalid_keys_are_skipped_with_their_reason()
    {
        var result = Parse("9LIVES=1\nHAS-DASH=2\nSPACE KEY=3\n=nokey\nÜMLAUT=4");
        result.Lines.Select(l => l.Status).ShouldBe([
            EnvDotenvStatus.StartsWithDigit,
            EnvDotenvStatus.InvalidCharacters,
            EnvDotenvStatus.InvalidCharacters,
            EnvDotenvStatus.InvalidCharacters,
            EnvDotenvStatus.InvalidCharacters,
        ]);
        result.ImportCount.ShouldBe(0);
    }

    [Fact]
    public void Line_without_equals_is_skipped()
    {
        var line = Parse("JUST_A_WORD").Lines.Single();
        line.Status.ShouldBe(EnvDotenvStatus.MissingEquals);
        line.Key.ShouldBe("JUST_A_WORD");
        line.Value.ShouldBeNull();
    }

    [Fact]
    public void Invalid_line_does_not_take_part_in_duplicates()
    {
        var result = Parse("KEY=1\n1KEY=2\nKEY");
        result.Lines[0].Status.ShouldBe(EnvDotenvStatus.New);
        result.Lines[1].Status.ShouldBe(EnvDotenvStatus.StartsWithDigit);
        result.Lines[2].Status.ShouldBe(EnvDotenvStatus.MissingEquals);
    }

    [Fact]
    public void Keys_already_set_in_chosen_environments_replace()
    {
        var existing = new EnvVariable("CACHE_TTL", "30", "development", "staging");
        var prodOnly = new EnvVariable("FLAG", "on", "production");
        var result = Parse("CACHE_TTL=60\nFLAG=off", DevStaging, existing, prodOnly);
        result.Lines[0].Status.ShouldBe(EnvDotenvStatus.Replaces);
        result.Lines[0].ReplacedEnvironments.ShouldBe(["development", "staging"]);
        result.Lines[1].Status.ShouldBe(EnvDotenvStatus.New);
        Parse("CACHE_TTL=60", ["production"], existing).Lines.Single().Status.ShouldBe(EnvDotenvStatus.New);
    }

    [Fact]
    public void Empty_or_comment_only_text_gives_nothing()
    {
        Parse("").Lines.ShouldBeEmpty();
        Parse("# only\n\n   \n").Lines.ShouldBeEmpty();
    }

    [Fact]
    public void Gallery_sample_covers_every_status()
    {
        var existing = new EnvVariable("CACHE_TTL_SECONDS", "30", "development", "staging");
        var result = Parse(EnvVariablesSample, DevStaging, existing);
        result.Lines.Select(l => (l.LineNumber, l.Status)).ShouldBe([
            (2, EnvDotenvStatus.New),
            (3, EnvDotenvStatus.New),
            (4, EnvDotenvStatus.New),
            (5, EnvDotenvStatus.Duplicate),
            (6, EnvDotenvStatus.StartsWithDigit),
            (7, EnvDotenvStatus.Replaces),
        ]);
        result.ImportCount.ShouldBe(4);
        result.SkippedCount.ShouldBe(2);
    }

    private const string EnvVariablesSample =
        "# exported from a local shell\n" +
        "FEATURE_FLAGS_URL=https://flags.lumen.example\n" +
        "OBJECT_STORE_SECRET=os_9f2c7a1e0b\n" +
        "export WEBHOOK_RELAY_TOKEN=\"relay_test_3c8d\"\n" +
        "CACHE_TTL_SECONDS=45\n" +
        "9LIVES_TOKEN=abc123\n" +
        "CACHE_TTL_SECONDS=60\n";

    [Theory]
    [InlineData("API_BASE_URL", "None")]
    [InlineData("lower_case_ok", "None")]
    [InlineData("_LEADING", "None")]
    [InlineData("", "Empty")]
    [InlineData("2FA", "StartsWithDigit")]
    [InlineData("A.B", "InvalidCharacters")]
    [InlineData("A B", "InvalidCharacters")]
    public void Keys_are_validated(string key, string expected) =>
        EnvVariableDotenvParser.ValidateKey(key).ToString().ShouldBe(expected);

    [Fact]
    public void Fingerprint_is_stable_short_hex_and_does_not_leak_the_value()
    {
        var a = EnvVariableFingerprint.Compute("postgres://app:devpass@db.internal.example/lumen_dev");
        a.Length.ShouldBe(6);
        a.ShouldMatch("^[0-9a-f]{6}$");
        EnvVariableFingerprint.Compute("postgres://app:devpass@db.internal.example/lumen_dev").ShouldBe(a);
        EnvVariableFingerprint.Compute("postgres://app:devpass@db.internal.example/lumen_prd").ShouldNotBe(a);
        // SHA-256("abc") = ba7816bf...
        EnvVariableFingerprint.Compute("abc").ShouldBe("ba7816");
        EnvVariableFingerprint.Compute("").ShouldBe("e3b0c4");
    }

    [Fact]
    public void Variable_fingerprint_follows_its_value_unless_given()
    {
        var variable = new EnvVariable("K", "abc");
        variable.EffectiveFingerprint.ShouldBe("ba7816");
        variable.Value = "";
        variable.EffectiveFingerprint.ShouldBe("e3b0c4");
        variable.Fingerprint = "c0ffee";
        variable.EffectiveFingerprint.ShouldBe("c0ffee");
        new EnvVariable("S", null).EffectiveFingerprint.ShouldBeNull();
    }

    [Fact]
    public void Sort_is_ordinal_by_key_then_by_first_environment()
    {
        string[] order = ["development", "staging", "production"];
        var prod = new EnvVariable("DATA", "p", "production");
        var dev = new EnvVariable("DATA", "d", "development", "staging");
        var underscore = new EnvVariable("D_FOUR", "x", "development");
        var lower = new EnvVariable("aaa", "x", "development");
        var none = new EnvVariable("DATA", "n");
        var sorted = EnvVariableRules.Sort([lower, prod, underscore, none, dev], order);
        sorted.ShouldBe([dev, prod, none, underscore, lower]);
    }

    [Fact]
    public void Blocking_row_is_another_row_of_the_same_key()
    {
        var a = new EnvVariable("K", "1", "development");
        var b = new EnvVariable("K", "2", "production");
        var other = new EnvVariable("L", "3", "staging");
        EnvVariable[] all = [a, b, other];
        EnvVariableRules.FindBlocking(all, a, "production").ShouldBe(b);
        EnvVariableRules.FindBlocking(all, a, "staging").ShouldBeNull();
        EnvVariableRules.FindBlocking(all, b, "production").ShouldBeNull();
        EnvVariableRules.TakenEnvironments(all, "K", ["development", "staging", "production"]).ShouldBe(["development", "production"]);
    }

    [Fact]
    public void Counts_keys_and_rows_per_environment()
    {
        EnvVariable[] all =
        [
            new("A", "1", "development", "staging"),
            new("A", "2", "production"),
            new("B", "3", "production"),
        ];
        EnvVariableRules.CountKeys(all).ShouldBe(2);
        EnvVariableRules.CountSet(all, "production").ShouldBe(2);
        EnvVariableRules.CountSet(all, "staging").ShouldBe(1);
    }

    [Theory]
    [InlineData("CACHE_TTL", "ttl", true)]
    [InlineData("CACHE_TTL", " cache ", true)]
    [InlineData("CACHE_TTL", "", true)]
    [InlineData("CACHE_TTL", "secret", false)]
    public void Search_matches_keys_case_insensitively(string key, string query, bool expected) =>
        EnvVariableRules.Matches(key, query).ShouldBe(expected);

    [Fact]
    public void Compare_reports_missing_different_and_same()
    {
        var same = new EnvVariable("SAME", "x", "staging", "production");
        var diffStaging = new EnvVariable("DIFF", "a", "staging");
        var diffProd = new EnvVariable("DIFF", "b", "production");
        var onlyProd = new EnvVariable("ONLY_PROD", "p", "production");
        var onlyStaging = new EnvVariable("ONLY_STG", "s", "staging");
        var equalRows = new EnvVariable("EQUAL", "v", "staging");
        var equalRows2 = new EnvVariable("EQUAL", "v", "production");
        var elsewhere = new EnvVariable("DEV_ONLY", "d", "development");
        EnvVariable[] all = [same, diffStaging, diffProd, onlyProd, onlyStaging, equalRows, equalRows2, elsewhere];
        var rows = EnvVariableRules.Compare(all, "staging", "production", null, showMatching: true);
        rows.Select(r => (r.Key, r.Result)).ShouldBe([
            ("DIFF", EnvCompareResult.Different),
            ("EQUAL", EnvCompareResult.Same),
            ("ONLY_PROD", EnvCompareResult.MissingLeft),
            ("ONLY_STG", EnvCompareResult.MissingRight),
            ("SAME", EnvCompareResult.Same),
        ]);
        EnvVariableRules.Compare(all, "staging", "production", null, showMatching: false).Count.ShouldBe(3);
        EnvVariableRules.Compare(all, "staging", "production", "only", showMatching: true).Count.ShouldBe(2);
        EnvVariableRules.CompareCounts(all, "staging", "production", null).ShouldBe((2, 1, 2));
    }

    [Fact]
    public void Compare_uses_given_fingerprints_for_sensitive_rows()
    {
        var a = new EnvVariable("TOKEN", null, "staging") { IsSensitive = true, Fingerprint = "aaaaaa" };
        var b = new EnvVariable("TOKEN", null, "production") { IsSensitive = true, Fingerprint = "aaaaaa" };
        EnvVariableRules.Compare([a, b], "staging", "production", null, true).Single().Result.ShouldBe(EnvCompareResult.Same);
        b.Fingerprint = "bbbbbb";
        EnvVariableRules.Compare([a, b], "staging", "production", null, true).Single().Result.ShouldBe(EnvCompareResult.Different);
    }
}
