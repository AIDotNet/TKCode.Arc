// Ported from vscode (src/vs/editor/contrib/indentation/test/browser/indentationLineProcessor.test.ts, commit
// b57d5c9e), MIT license. Differences from upstream: no test code editor is created (the processors read the model
// only); the tokens are given by IndentationTestUtils.RegisterTokenizationSupport on the model.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class IndentationContextProcessorTests
{
    private const string LanguageId = Language.TypeScript;

    private static void WithModel(string[] lines, StandardTokenTypeData[][] tokens, Action<TextModel, LanguageConfigurationService> callback)
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        using var registration = IndentationTestUtils.RegisterLanguageConfiguration(languageConfigurationService, LanguageId);
        using var model = TestTextModel.Create(string.Join('\n', lines), LanguageId);
        IndentationTestUtils.RegisterTokenizationSupport(model, tokens, LanguageId);
        callback(model, languageConfigurationService);
    }

    [Fact]
    public void Brackets_Inside_Of_String()
    {
        WithModel(
            [
                "const someVar = \"{some text}\"",
            ],
            [
                [
                    new(0, StandardTokenType.Other),
                    new(16, StandardTokenType.String),
                    new(28, StandardTokenType.String),
                ],
            ],
            (model, languageConfigurationService) =>
            {
                var indentationContextProcessor = new IndentationContextProcessor(model, languageConfigurationService);
                var processedContext = indentationContextProcessor.GetProcessedTokenContextAroundRange(new TextRange(1, 23, 1, 23));
                processedContext.BeforeRangeProcessedTokens.GetLineContent().ShouldBe("const someVar = \"some");
                processedContext.AfterRangeProcessedTokens.GetLineContent().ShouldBe(" text\"");
                processedContext.PreviousLineProcessedTokens.GetLineContent().ShouldBe("");
            });
    }

    [Fact]
    public void Brackets_Inside_Of_Comment()
    {
        WithModel(
            [
                "const someVar2 = /*(a])*/",
                "const someVar = /* [()] some other t{e}xt() */ \"some text\"",
            ],
            [
                [
                    new(0, StandardTokenType.Other),
                    new(17, StandardTokenType.Comment),
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(16, StandardTokenType.Comment),
                    new(46, StandardTokenType.Other),
                    new(47, StandardTokenType.String),
                ],
            ],
            (model, languageConfigurationService) =>
            {
                var indentationContextProcessor = new IndentationContextProcessor(model, languageConfigurationService);
                var processedContext = indentationContextProcessor.GetProcessedTokenContextAroundRange(new TextRange(2, 29, 2, 35));
                processedContext.BeforeRangeProcessedTokens.GetLineContent().ShouldBe("const someVar = /*  some");
                processedContext.AfterRangeProcessedTokens.GetLineContent().ShouldBe(" text */ \"some text\"");
                processedContext.PreviousLineProcessedTokens.GetLineContent().ShouldBe("const someVar2 = /*a*/");
            });
    }

    [Fact]
    public void Brackets_Inside_Of_Regex()
    {
        WithModel(
            [
                "const someRegex2 = /(()))]/;",
                "const someRegex = /()a{h}{s}[(a}87(9a9()))]/;",
            ],
            [
                [
                    new(0, StandardTokenType.Other),
                    new(19, StandardTokenType.RegEx),
                    new(27, StandardTokenType.Other),
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(18, StandardTokenType.RegEx),
                    new(44, StandardTokenType.Other),
                ],
            ],
            (model, languageConfigurationService) =>
            {
                var indentationContextProcessor = new IndentationContextProcessor(model, languageConfigurationService);
                var processedContext = indentationContextProcessor.GetProcessedTokenContextAroundRange(new TextRange(1, 25, 2, 33));
                processedContext.BeforeRangeProcessedTokens.GetLineContent().ShouldBe("const someRegex2 = /");
                processedContext.AfterRangeProcessedTokens.GetLineContent().ShouldBe("879a9/;");
                processedContext.PreviousLineProcessedTokens.GetLineContent().ShouldBe("");
            });
    }
}

public sealed class ProcessedIndentRulesSupportTests
{
    private const string LanguageId = Language.TypeScript;

    private static void WithSupport(string[] lines, StandardTokenTypeData[][] tokens, Action<ProcessedIndentRulesSupport> callback)
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        using var registration = IndentationTestUtils.RegisterLanguageConfiguration(languageConfigurationService, LanguageId);
        using var model = TestTextModel.Create(string.Join('\n', lines), LanguageId);
        IndentationTestUtils.RegisterTokenizationSupport(model, tokens, LanguageId);
        var indentationRulesSupport = languageConfigurationService.GetLanguageConfiguration(LanguageId).IndentRulesSupport;
        indentationRulesSupport.ShouldNotBeNull("indentationRulesSupport should be defined");
        var processedIndentRulesSupport = new ProcessedIndentRulesSupport(model, indentationRulesSupport, languageConfigurationService);
        callback(processedIndentRulesSupport);
    }

    [Fact]
    public void Should_Increase()
    {
        WithSupport(
            [
                "const someVar = {",
                "const someVar2 = \"{\"",
                "const someVar3 = /*{*/",
            ],
            [
                [
                    new(0, StandardTokenType.Other),
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(17, StandardTokenType.String),
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(17, StandardTokenType.Comment),
                ],
            ],
            processedIndentRulesSupport =>
            {
                processedIndentRulesSupport.ShouldIncrease(1).ShouldBe(true);
                processedIndentRulesSupport.ShouldIncrease(2).ShouldBe(false);
                processedIndentRulesSupport.ShouldIncrease(3).ShouldBe(false);
            });
    }

    [Fact]
    public void Should_Decrease()
    {
        WithSupport(
            [
                "}",
                "\"])some text}\"",
                "])*/",
            ],
            [
                [new(0, StandardTokenType.Other)],
                [new(0, StandardTokenType.String)],
                [new(0, StandardTokenType.Comment)],
            ],
            processedIndentRulesSupport =>
            {
                processedIndentRulesSupport.ShouldDecrease(1).ShouldBe(true);
                processedIndentRulesSupport.ShouldDecrease(2).ShouldBe(false);
                processedIndentRulesSupport.ShouldDecrease(3).ShouldBe(false);
            });
    }

    [Fact]
    public void Should_Increase_Next_Line()
    {
        WithSupport(
            [
                "if()",
                "const someString = \"if()\"",
                "const someRegex = /if()/",
            ],
            [
                [
                    new(0, StandardTokenType.Other),
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(19, StandardTokenType.String),
                ],
                [
                    new(0, StandardTokenType.Other),
                    new(18, StandardTokenType.RegEx),
                ],
            ],
            processedIndentRulesSupport =>
            {
                processedIndentRulesSupport.ShouldIndentNextLine(1).ShouldBe(true);
                processedIndentRulesSupport.ShouldIndentNextLine(2).ShouldBe(false);
                processedIndentRulesSupport.ShouldIndentNextLine(3).ShouldBe(false);
            });
    }
}
