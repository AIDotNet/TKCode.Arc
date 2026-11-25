using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Ported from vscode's <c>src/vs/editor/test/common/core/characterClassifier.test.ts</c>.</summary>
public sealed class CharacterClassifierTests
{
    [Fact]
    public void Works()
    {
        var classifier = new CharacterClassifier(0);

        foreach (var charCode in new[] { -1, 0, 'a', 'b', 'z', 255, 1000, 2000 })
        {
            classifier.Get(charCode).ShouldBe((byte)0);
        }

        classifier.Set('a', 1);
        classifier.Set('z', 2);
        classifier.Set(1000, 3);

        classifier.Get(-1).ShouldBe((byte)0);
        classifier.Get(0).ShouldBe((byte)0);
        classifier.Get('a').ShouldBe((byte)1);
        classifier.Get('b').ShouldBe((byte)0);
        classifier.Get('z').ShouldBe((byte)2);
        classifier.Get(255).ShouldBe((byte)0);
        classifier.Get(1000).ShouldBe((byte)3);
        classifier.Get(2000).ShouldBe((byte)0);
    }

    [Fact]
    public void Word_Classifier_Knows_Separators_And_Whitespace()
    {
        var classifier = WordCharacterClassifier.GetMapForWordSeparators(".,;");
        classifier.Get('.').ShouldBe(WordCharacterClass.WordSeparator);
        classifier.Get(' ').ShouldBe(WordCharacterClass.Whitespace);
        classifier.Get('\t').ShouldBe(WordCharacterClass.Whitespace);
        classifier.Get('a').ShouldBe(WordCharacterClass.Regular);
        classifier.Get('中').ShouldBe(WordCharacterClass.Regular);
        WordCharacterClassifier.GetMapForWordSeparators(".,;").ShouldBeSameAs(classifier);
    }
}
