// Written for the port: getBracketRegExp is cached per options (auto-indent asks for it on every Enter, and building a
// .NET Regex is dear, above all in the browser's interpreter).

using Shouldly;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class LanguageBracketsConfigurationTests
{
    [Fact]
    public void Bracket_RegExp_Is_Built_Once_Per_Options()
    {
        var configuration = new LanguageBracketsConfiguration("test", new LanguageConfiguration { Brackets = [("{", "}"), ("begin", "end")] });

        var global = configuration.GetBracketRegExp(new RegExpOptions(Global: true));
        configuration.GetBracketRegExp(new RegExpOptions(Global: true)).ShouldBeSameAs(global);
        configuration.GetBracketRegExp().ShouldNotBeSameAs(global);

        global.Replace("a { b } begin c end", "").ShouldBe("a  b   c ");
        global.IsMatch("beginning").ShouldBeFalse();
    }
}
