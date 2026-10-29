using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Theme.TokenBaseline;

/// <summary>
/// One theme state the baseline captures. The names are the baseline file names, so keep them stable. <paramref name="Pixel"/>
/// applies the Pixel style (every Arc package), <paramref name="CornerScale"/> a corner style of the Corners package over
/// every Arc package (0 is <c>ArcCornerStyle.Square</c>, otherwise <c>ArcCornerStyle.Scale(k)</c>).
/// </summary>
public sealed record TokenVariant(string Name, bool Dark, ArcAccentPreset? Preset = null, Color? Seed = null, ArcMotionLevel? Motion = null,
    bool Pixel = false, double? CornerScale = null)
{
    public override string ToString() => Name;
}

/// <summary>
/// Token value baseline (design/roadmap/atomui-replacement.md, phase F0): every ArcDesign token and every control token of
/// every Arc package the test app registers, resolved as a control in a themed tree sees it, in light and dark for the
/// default theme, every accent preset and two custom seeds, plus the reduced and off motion modes. Values are written to
/// <c>tests/Baselines/tokens/&lt;variant&gt;.json</c>. Set <c>ARC_UPDATE_BASELINES=1</c> to (re)write the files; otherwise
/// the test compares with them and lists every differing <c>identity.token</c>. How the theme is applied and how a token is
/// looked up lives in <see cref="TokenBaselineResolver"/> only.
/// </summary>
public class TokenBaselineTests
{
    private const string Missing = "<missing>";

    private static readonly TokenVariant[] s_variants = BuildVariants();

    public static TheoryData<string> Variants => new(s_variants.Select(v => v.Name));

    private static TokenVariant[] BuildVariants()
    {
        var variants = new List<TokenVariant>();
        foreach (var dark in new[] { false, true })
        {
            var appearance = dark ? "dark" : "light";
            variants.Add(new TokenVariant($"{appearance}-default", dark));
            foreach (var preset in Enum.GetValues<ArcAccentPreset>())
            {
                variants.Add(new TokenVariant($"{appearance}-{preset.ToString().ToLowerInvariant()}", dark, Preset: preset));
            }
            foreach (var seed in new[] { "#0562EF", "#E5484D" })
            {
                variants.Add(new TokenVariant($"{appearance}-seed-{seed[1..]}", dark, Seed: Color.Parse(seed)));
            }
        }
        variants.Add(new TokenVariant("light-default-motion-reduced", false, Motion: ArcMotionLevel.Reduced));
        variants.Add(new TokenVariant("light-default-motion-off", false, Motion: ArcMotionLevel.Off));
        // Theme packages (phase F2): the Pixel style in both appearances, and the corner styles the Gallery offers (square,
        // half) plus a factor above 1 (pills must stay pills). Corner overrides do not depend on the appearance; dark-square
        // proves that against dark-default.
        variants.Add(new TokenVariant("light-pixel", false, Pixel: true));
        variants.Add(new TokenVariant("dark-pixel", true, Pixel: true));
        variants.Add(new TokenVariant("light-corners-square", false, CornerScale: 0));
        variants.Add(new TokenVariant("dark-corners-square", true, CornerScale: 0));
        variants.Add(new TokenVariant("light-corners-scale-0.5", false, CornerScale: 0.5));
        variants.Add(new TokenVariant("light-corners-scale-2", false, CornerScale: 2));
        return variants.ToArray();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Variants))]
    public async Task Token_Values_Match_The_Baseline(string name)
    {
        var variant = s_variants.Single(v => v.Name == name);
        var actual = await CaptureAsync(variant);
        var path = Path.Combine(BaselineFolder(), $"{variant.Name}.json");

        if (Environment.GetEnvironmentVariable("ARC_UPDATE_BASELINES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Serialize(variant, actual), new UTF8Encoding(false));
            return;
        }

        File.Exists(path).ShouldBeTrue($"No token baseline at {path}. Run the tests with ARC_UPDATE_BASELINES=1 to write it.");
        var expected = Deserialize(File.ReadAllText(path));
        var differences = Compare(expected, actual);
        if (differences.Count > 0)
        {
            const int shown = 200;
            var report = new StringBuilder()
                .AppendLine($"{differences.Count} token value(s) of '{variant.Name}' differ from {path} (expected -> actual):");
            foreach (var line in differences.Take(shown))
            {
                report.AppendLine("  " + line);
            }
            if (differences.Count > shown)
            {
                report.AppendLine($"  ... and {differences.Count - shown} more");
            }
            report.AppendLine("If the change is intended, rerun with ARC_UPDATE_BASELINES=1 and commit the updated baseline.");
            Assert.Fail(report.ToString());
        }
    }

    [AvaloniaFact]
    public async Task Every_Token_Resolves_In_The_Default_Theme()
    {
        var actual = await CaptureAsync(s_variants[0]);
        var missing = actual.SelectMany(identity => identity.Value
                                            .Where(token => token.Value == Missing)
                                            .Select(token => $"{identity.Key}.{token.Key}"))
                            .ToList();
        missing.ShouldBeEmpty();
    }

    private static async Task<SortedDictionary<string, SortedDictionary<string, string>>> CaptureAsync(TokenVariant variant)
    {
        var host = new Avalonia.Controls.Border();
        var window = TestHelpers.Show(host);
        try
        {
            await TokenBaselineResolver.ApplyAsync(variant);
            var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var (identity, kindType) in TokenKindTypes())
            {
                var tokens = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var kind in Enum.GetValues(kindType).Cast<Enum>())
                {
                    tokens[kind.ToString()] = TokenBaselineResolver.TryResolve(host, kind, out var value)
                        ? TokenValueFormatter.Format(value)
                        : Missing;
                }
                result.Add(identity, tokens);
            }
            return result;
        }
        finally
        {
            window.Close();
            await TokenBaselineResolver.ResetAsync();
        }
    }

    /// <summary>
    /// The generated <c>TKCode.Arc.DesignTokens.XxxTokenKind</c> enums of every Arc assembly the tests reference, keyed by
    /// identity name (<c>ArcDesign</c>, <c>Button</c>, ...).
    /// </summary>
    private static IEnumerable<(string Identity, Type KindType)> TokenKindTypes()
    {
        var result = new SortedDictionary<string, Type>(StringComparer.Ordinal);
        foreach (var assembly in ArcAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type is { IsEnum: true, IsPublic: true, Namespace: "TKCode.Arc.DesignTokens" } &&
                    type.Name.EndsWith("TokenKind", StringComparison.Ordinal))
                {
                    var identity = type.Name[..^"TokenKind".Length];
                    if (result.TryGetValue(identity, out var other))
                    {
                        throw new InvalidOperationException(
                            $"Token identity {identity} is declared in both {other.Assembly.GetName().Name} and {assembly.GetName().Name}.");
                    }
                    result.Add(identity, type);
                }
            }
        }
        return result.Select(pair => (pair.Key, pair.Value));
    }

    private static IEnumerable<Assembly> ArcAssemblies()
    {
        var self = typeof(TokenBaselineTests).Assembly;
        var seen = new SortedDictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>([self]);
        while (pending.Count > 0)
        {
            foreach (var reference in pending.Dequeue().GetReferencedAssemblies())
            {
                if (reference.Name is { } name && name.StartsWith("TKCode.Arc", StringComparison.Ordinal) && !seen.ContainsKey(name))
                {
                    var assembly = Assembly.Load(reference);
                    seen.Add(name, assembly);
                    pending.Enqueue(assembly);
                }
            }
        }
        return seen.Values;
    }

    private static string Serialize(TokenVariant variant, SortedDictionary<string, SortedDictionary<string, string>> tokens)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Indented = true,
                   NewLine = "\n",
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
               }))
        {
            writer.WriteStartObject();
            writer.WriteString("variant", variant.Name);
            writer.WriteStartObject("tokens");
            foreach (var (identity, values) in tokens)
            {
                writer.WriteStartObject(identity);
                foreach (var (token, value) in values)
                {
                    writer.WriteString(token, value);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static SortedDictionary<string, SortedDictionary<string, string>> Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var identity in document.RootElement.GetProperty("tokens").EnumerateObject())
        {
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in identity.Value.EnumerateObject())
            {
                values[token.Name] = token.Value.GetString() ?? "null";
            }
            result[identity.Name] = values;
        }
        return result;
    }

    private static List<string> Compare(SortedDictionary<string, SortedDictionary<string, string>> expected,
                                        SortedDictionary<string, SortedDictionary<string, string>> actual)
    {
        static IEnumerable<KeyValuePair<string, string>> Flatten(SortedDictionary<string, SortedDictionary<string, string>> tokens)
            => tokens.SelectMany(identity => identity.Value.Select(token =>
                   new KeyValuePair<string, string>($"{identity.Key}.{token.Key}", token.Value)));

        var expectedFlat = new SortedDictionary<string, string>(Flatten(expected).ToDictionary(), StringComparer.Ordinal);
        var actualFlat = new SortedDictionary<string, string>(Flatten(actual).ToDictionary(), StringComparer.Ordinal);
        var differences = new List<string>();
        foreach (var key in expectedFlat.Keys.Union(actualFlat.Keys).Order(StringComparer.Ordinal))
        {
            var hasExpected = expectedFlat.TryGetValue(key, out var before);
            var hasActual = actualFlat.TryGetValue(key, out var after);
            if (!hasActual)
            {
                differences.Add($"{key}: {before} -> (token no longer exists)");
            }
            else if (!hasExpected)
            {
                differences.Add($"{key}: (not in baseline) -> {after}");
            }
            else if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                differences.Add($"{key}: {before} -> {after}");
            }
        }
        return differences;
    }

    private static string BaselineFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TKCode.Arc.slnx")))
            {
                return Path.Combine(folder.FullName, "tests", "Baselines", "tokens");
            }
        }
        throw new InvalidOperationException("TKCode.Arc.slnx was not found above the test folder");
    }
}
