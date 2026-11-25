// Ported from vscode (src/vs/editor/test/common/model/model.modes.test.ts, commit b57d5c9e), MIT license.
// Differences: the tokenization support goes into the model's own registry after the model is created (upstream
// registers it with the global TokenizationRegistry first); the two suites are two classes.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ModelModes1Tests : IDisposable
{
    private readonly List<string> _calledFor = new();
    private readonly TextModel _thisModel;
    private readonly IDisposable _languageRegistration;

    public ModelModes1Tests()
    {
        const string text =
            "1\r\n" +
            "2\n" +
            "3\n" +
            "4\r\n" +
            "5";
        const string languageId = "modelModeTest1";
        _thisModel = TestTextModel.Create(text, languageId);
        _languageRegistration = TestTokenizationSupports.Register(_thisModel, new Support(_calledFor));
    }

    public void Dispose()
    {
        _thisModel.Dispose();
        _languageRegistration.Dispose();
    }

    private string[] GetAndClear()
    {
        var result = _calledFor.ToArray();
        _calledFor.Clear();
        return result;
    }

    private sealed class Support(List<string> calledFor) : ITokenizationSupport
    {
        public IState GetInitialState() => NullState.Instance;

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state)
        {
            calledFor.Add(line.Length > 0 ? line[..1] : string.Empty);
            return new EncodedTokenizationResult([], state);
        }
    }

    [Fact]
    public void Model_calls_syntax_highlighter_1()
    {
        _thisModel.Tokenization.ForceTokenization(1);
        GetAndClear().ShouldBe(["1"]);
    }

    [Fact]
    public void Model_calls_syntax_highlighter_2()
    {
        _thisModel.Tokenization.ForceTokenization(2);
        GetAndClear().ShouldBe(["1", "2"]);

        _thisModel.Tokenization.ForceTokenization(2);
        GetAndClear().ShouldBeEmpty();
    }

    [Fact]
    public void Model_caches_states()
    {
        _thisModel.Tokenization.ForceTokenization(1);
        GetAndClear().ShouldBe(["1"]);

        _thisModel.Tokenization.ForceTokenization(2);
        GetAndClear().ShouldBe(["2"]);

        _thisModel.Tokenization.ForceTokenization(3);
        GetAndClear().ShouldBe(["3"]);

        _thisModel.Tokenization.ForceTokenization(4);
        GetAndClear().ShouldBe(["4"]);

        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["5"]);

        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBeEmpty();
    }

    [Fact]
    public void Model_invalidates_states_for_one_line_insert()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["1", "2", "3", "4", "5"]);

        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "-")]);
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["-"]);

        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBeEmpty();
    }

    [Fact]
    public void Model_invalidates_states_for_many_lines_insert()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["1", "2", "3", "4", "5"]);

        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "0\n-\n+")]);
        _thisModel.GetLineCount().ShouldBe(7);
        _thisModel.Tokenization.ForceTokenization(7);
        GetAndClear().ShouldBe(["0", "-", "+"]);

        _thisModel.Tokenization.ForceTokenization(7);
        GetAndClear().ShouldBeEmpty();
    }

    [Fact]
    public void Model_invalidates_states_for_one_new_line()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["1", "2", "3", "4", "5"]);

        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 2), "\n")]);
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(2, 1), "a")]);
        _thisModel.Tokenization.ForceTokenization(6);
        GetAndClear().ShouldBe(["1", "a"]);
    }

    [Fact]
    public void Model_invalidates_states_for_one_line_delete()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["1", "2", "3", "4", "5"]);

        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 2), "-")]);
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["1"]);

        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 2))]);
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["-"]);

        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBeEmpty();
    }

    [Fact]
    public void Model_invalidates_states_for_many_lines_delete()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["1", "2", "3", "4", "5"]);

        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 3, 1))]);
        _thisModel.Tokenization.ForceTokenization(3);
        GetAndClear().ShouldBe(["3"]);

        _thisModel.Tokenization.ForceTokenization(3);
        GetAndClear().ShouldBeEmpty();
    }
}

public sealed class ModelModes2Tests : IDisposable
{
    private readonly List<string> _calledFor = new();
    private readonly TextModel _thisModel;
    private readonly IDisposable _languageRegistration;

    public ModelModes2Tests()
    {
        const string text =
            "Line1" + "\r\n" +
            "Line2" + "\n" +
            "Line3" + "\n" +
            "Line4" + "\r\n" +
            "Line5";
        const string languageId = "modelModeTest2";
        _thisModel = TestTextModel.Create(text, languageId);
        _languageRegistration = TestTokenizationSupports.Register(_thisModel, new Support(_calledFor));
    }

    public void Dispose()
    {
        _thisModel.Dispose();
        _languageRegistration.Dispose();
    }

    private string[] GetAndClear()
    {
        var result = _calledFor.ToArray();
        _calledFor.Clear();
        return result;
    }

    private sealed class ModelState2(string prevLineContent) : IState
    {
        public string PrevLineContent { get; set; } = prevLineContent;

        public IState Clone() => new ModelState2(PrevLineContent);

        public bool Equals(IState other) => other is ModelState2 s && s.PrevLineContent == PrevLineContent;
    }

    private sealed class Support(List<string> calledFor) : ITokenizationSupport
    {
        public IState GetInitialState() => new ModelState2(string.Empty);

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state)
        {
            calledFor.Add(line);
            ((ModelState2)state).PrevLineContent = line;
            return new EncodedTokenizationResult([], state);
        }
    }

    [Fact]
    public void GetTokensForInvalidLines_one_text_insert()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1", "Line2", "Line3", "Line4", "Line5"]);
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 6), "-")]);
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1-", "Line2"]);
    }

    [Fact]
    public void GetTokensForInvalidLines_two_text_insert()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1", "Line2", "Line3", "Line4", "Line5"]);
        _thisModel.ApplyEdits([
            EditOperation.Insert(new TextPosition(1, 6), "-"),
            EditOperation.Insert(new TextPosition(3, 6), "-"),
        ]);

        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1-", "Line2", "Line3-", "Line4"]);
    }

    [Fact]
    public void GetTokensForInvalidLines_one_multi_line_text_insert_one_small_text_insert()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1", "Line2", "Line3", "Line4", "Line5"]);
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 6), "\nNew line\nAnother new line")]);
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(5, 6), "-")]);
        _thisModel.Tokenization.ForceTokenization(7);
        GetAndClear().ShouldBe(["Line1", "New line", "Another new line", "Line2", "Line3-", "Line4"]);
    }

    [Fact]
    public void GetTokensForInvalidLines_one_delete_text()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1", "Line2", "Line3", "Line4", "Line5"]);
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 5))]);
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["1", "Line2"]);
    }

    [Fact]
    public void GetTokensForInvalidLines_one_line_delete_text()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1", "Line2", "Line3", "Line4", "Line5"]);
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 2, 1))]);
        _thisModel.Tokenization.ForceTokenization(4);
        GetAndClear().ShouldBe(["Line2"]);
    }

    [Fact]
    public void GetTokensForInvalidLines_multiple_lines_delete_text()
    {
        _thisModel.Tokenization.ForceTokenization(5);
        GetAndClear().ShouldBe(["Line1", "Line2", "Line3", "Line4", "Line5"]);
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 3, 3))]);
        _thisModel.Tokenization.ForceTokenization(3);
        GetAndClear().ShouldBe(["ne3", "Line4"]);
    }
}
