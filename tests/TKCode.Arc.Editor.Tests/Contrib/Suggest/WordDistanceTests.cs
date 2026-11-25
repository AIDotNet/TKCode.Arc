// Ported from vscode (src/vs/editor/contrib/suggest/test/browser/wordDistance.test.ts, commit b57d5c9e), MIT license.
// Differences: there is no editor worker; WordDistance.Create reads the word ranges from the model.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class WordDistanceTests
{
    [Fact]
    public void Suggest_locality_bonus_can_boost_current_word_90515()
    {
        const string languageId = "bracketMode";
        using var service = new LanguageConfigurationService();
        service.Register(languageId, new LanguageConfiguration { Brackets = [("{", "}"), ("[", "]"), ("(", ")")] });
        using var model = TestTextModel.Create("function abc(aa, ab){\na\n}", languageId, uri: new Uri("test:///some.path"));
        var position = new TextPosition(2, 2);

        var distance = WordDistance.Create(model, position, () => position, service, localityBonus: true);

        var d1 = distance.Distance(position, CompletionModelTests.CreateSuggestItem("a", 1, position: position).Completion);
        var d2 = distance.Distance(position, CompletionModelTests.CreateSuggestItem("aa", 1, position: position).Completion);
        var d3 = distance.Distance(position, CompletionModelTests.CreateSuggestItem("ab", 1, position: position).Completion);

        d1.ShouldBeGreaterThan(d2);
        d2.ShouldBe(d3);
    }

    [Fact]
    public void Without_locality_bonus_every_distance_is_zero()
    {
        using var service = new LanguageConfigurationService();
        using var model = TestTextModel.Create("function abc(aa, ab){\na\n}");
        var position = new TextPosition(2, 2);
        WordDistance.Create(model, position, () => position, service, localityBonus: false).ShouldBeSameAs(WordDistance.None);
    }
}
