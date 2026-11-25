// Ported from monaco-editor (src/languages/definitions/*/*.test.ts and test/testRunner.ts, commit a310cd1), MIT license.
// The cases are the languages' own tokenization tests, converted to JSON by tools/editor/monarch-convert (TestData/);
// each is run as upstream's testRunner does: the lines joined with \n, tokenized with tokenize(line, true, state) from
// the language's initial state, and every line's tokens compared as (startIndex, type).

using System.Text;
using System.Text.Json;
using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Languages.BuiltIn;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class BuiltInLanguageTokenizationTests
{
    private static readonly string s_dataDirectory = Path.Combine(AppContext.BaseDirectory, "Languages", "BuiltIn", "TestData");

    public static TheoryData<string> Languages =>
        [.. Directory.GetFiles(s_dataDirectory, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)!).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task Every_language_registers_and_compiles()
    {
        using var services = new LanguageServices();
        foreach (var language in BuiltInLanguages.All)
        {
            language.Register(services);
        }

        foreach (var language in BuiltInLanguages.All)
        {
            var support = await services.TokenizationRegistry.GetOrCreateAsync(language.Id);
            support.ShouldNotBeNull(language.Id);
            services.LanguageConfigurationService.GetLanguageConfiguration(language.Id).ShouldNotBeNull();
        }

        services.LanguageService.GuessLanguageIdByFilepathOrFirstLine("Program.cs").ShouldBe("csharp");
        services.LanguageService.GuessLanguageIdByFilepathOrFirstLine("Dockerfile").ShouldBe("dockerfile");
        services.LanguageService.GetLanguageIdByLanguageName("C#").ShouldBe("csharp");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public async Task Tokenization(string languageId)
    {
        using var services = new LanguageServices();
        foreach (var language in BuiltInLanguages.All)
        {
            language.Register(services);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(s_dataDirectory, languageId + ".json")));
        var failures = new StringBuilder();
        var total = 0;
        var failed = 0;
        foreach (var record in document.RootElement.EnumerateArray())
        {
            var languages = record.GetProperty("languages").EnumerateArray().Select(l => l.GetString()!).ToArray();
            foreach (var l in languages)
            {
                await services.TokenizationRegistry.GetOrCreateAsync(l);
            }

            var mainLanguage = languages[0];
            var support = await services.TokenizationRegistry.GetOrCreateAsync(mainLanguage)
                ?? throw new InvalidOperationException("no tokenizer for " + mainLanguage);
            foreach (var test in record.GetProperty("tests").EnumerateArray())
            {
                total++;
                var items = test.EnumerateArray().ToArray();
                var state = support.GetInitialState();
                var mismatch = false;
                var report = new StringBuilder();
                foreach (var item in items)
                {
                    var line = item.GetProperty("line").GetString()!;
                    var expected = item.GetProperty("tokens").EnumerateArray()
                        .Select(t => (t.GetProperty("startIndex").GetInt32(), t.GetProperty("type").GetString()!))
                        .ToArray();
                    string actualText;
                    (int, string)[] actual;
                    try
                    {
                        var result = support.Tokenize(line, true, state);
                        state = result.EndState;
                        actual = result.Tokens.Select(t => (t.Offset, t.Type)).ToArray();
                        actualText = Format(actual);
                    }
                    catch (Exception e)
                    {
                        actual = [];
                        actualText = "threw " + e.GetType().Name + ": " + e.Message;
                        mismatch = true;
                    }

                    if (!actual.SequenceEqual(expected))
                    {
                        mismatch = true;
                        report.AppendLine("    line:     " + line)
                            .AppendLine("    expected: " + Format(expected))
                            .AppendLine("    actual:   " + actualText);
                    }
                }

                if (mismatch)
                {
                    failed++;
                    if (failed <= 5)
                    {
                        failures.AppendLine($"  case {total}:").Append(report);
                    }
                }
            }
        }

        if (failed > 0)
        {
            Assert.Fail($"{languageId}: {failed} of {total} cases differ\n{failures}");
        }
    }

    private static string Format((int Offset, string Type)[] tokens) =>
        string.Join(" ", tokens.Select(t => $"{t.Offset}:{t.Type}"));
}
