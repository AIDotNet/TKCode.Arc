using System.Collections.Immutable;
using System.Text.Json;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Themes.Pixel;

namespace TKCode.Arc.Foundation.Tests;

/// <summary>
/// The Pixel and Corners presets (<c>ArcPixelPresets.cs</c>, <c>ArcCornerPresets.cs</c>, linked into this project so they
/// compile without their packages) hold exactly the overrides of the AtomUI-era definitions they replaced. Those were
/// captured before AtomUI was removed (phase F3) into <c>tests/Baselines/presets/*.json</c>; this test compares both
/// directions: nothing missing, nothing extra, same text.
/// </summary>
public class ThemePresetPortTests
{
    public static TheoryData<string> Presets => new("pixel", "corners-square", "corners-scale-0.5", "corners-scale-2");

    [Theory]
    [MemberData(nameof(Presets))]
    public void Preset_matches_the_AtomUI_definition(string name)
    {
        var preset = name switch
        {
            "pixel" => ArcPixelPresets.Pixel,
            "corners-square" => ArcCornerPresets.Square,
            "corners-scale-0.5" => ArcCornerPresets.Half,
            "corners-scale-2" => ArcCornerPresets.Create(ArcCornerStyle.Scale(2)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(BaselineFolder(), $"{name}.json")));
        var problems = new List<string>();
        var count = 0;
        foreach (var (appearance, isDark) in new[] { ("light", false), ("dark", true) })
        {
            var expected = Flatten(document.RootElement.GetProperty(appearance));
            var actual = Flatten(preset.TokensFor(isDark));
            count += expected.Count;
            foreach (var key in expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal))
            {
                var hasExpected = expected.TryGetValue(key, out var before);
                var hasActual = actual.TryGetValue(key, out var after);
                if (!hasActual)
                {
                    problems.Add($"{appearance} {key}: missing in the preset (AtomUI: {before})");
                }
                else if (!hasExpected)
                {
                    problems.Add($"{appearance} {key}: only in the preset ({after})");
                }
                else if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    problems.Add($"{appearance} {key}: AtomUI {before}, preset {after}");
                }
            }
        }
        problems.ShouldBeEmpty(string.Join('\n', problems));
        count.ShouldBeGreaterThan(name == "pixel" ? 700 : 600);
    }

    [Fact]
    public void Corner_presets_share_a_slot_and_the_default_style_is_empty()
    {
        ArcCornerPresets.Square.Slot.ShouldBe(ArcCornerPresets.Slot);
        ArcCornerPresets.Half.Name.ShouldBe("ArcCornersScale0.5");
        var reset = ArcCornerPresets.Create(ArcCornerStyle.Default);
        reset.IsEmpty.ShouldBeTrue();
        reset.Slot.ShouldBe(ArcCornerPresets.Slot);
        ArcPixelPresets.Pixel.Slot.ShouldNotBe(ArcCornerPresets.Slot);
    }

    private static Dictionary<string, string> Flatten(JsonElement overrides)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var identity in overrides.EnumerateObject())
        {
            foreach (var token in identity.Value.EnumerateObject())
            {
                result[$"{identity.Name}.{token.Name}"] = token.Value.GetString()!;
            }
        }
        return result;
    }

    /// <summary>The global identity ("") is Arc's ArcDesign in the AtomUI files.</summary>
    private static Dictionary<string, string> Flatten(IReadOnlyDictionary<string, ImmutableDictionary<string, string>> overrides)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (identity, tokens) in overrides)
        {
            var name = identity == ArcThemePreset.GlobalIdentity ? "ArcDesign" : identity;
            foreach (var (token, value) in tokens)
            {
                result.TryAdd($"{name}.{token}", value).ShouldBeTrue($"{name}.{token} is set both globally and on ArcDesign");
            }
        }
        return result;
    }

    private static string BaselineFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TKCode.Arc.slnx")))
            {
                return Path.Combine(folder.FullName, "tests", "Baselines", "presets");
            }
        }
        throw new InvalidOperationException("TKCode.Arc.slnx was not found above the test folder");
    }
}
