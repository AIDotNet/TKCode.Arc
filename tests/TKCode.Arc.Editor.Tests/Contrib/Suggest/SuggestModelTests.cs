// Ported from vscode (src/vs/editor/contrib/suggest/test/browser/suggestModel.test.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the editor is a SuggestTestEditor (a TestCodeEditor with the events the model reads); providers answer
//    synchronously, so an action and the events it causes happen in one call: assertEvent is "the first (or n-th) event
//    raised while the action ran", and timers run by hand after each typing step;
//  - left out: the tests of embedded languages (no encoded-tokens test language here), of the inline completions
//    controller (it is another track), and the insert test of #39893 (in the control tests, as it needs the
//    SuggestController).

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class SuggestModelContextTests
{
    private static void AssertAutoTrigger(TextModel model, int offset, bool expected, string message)
    {
        var pos = model.GetPositionAt(offset);
        using var editor = new SuggestTestEditor(model, new LanguageFeatureRegistry<ICompletionItemProvider>());
        editor.Editor.SetPosition(pos);
        SuggestLineContext.ShouldAutoTrigger(editor).ShouldBe(expected, message);
    }

    [Fact]
    public void Context_shouldAutoTrigger()
    {
        using var model = TestTextModel.Create("Das Pferd frisst keinen Gurkensalat - Philipp Reis 1861.\nWer hat's erfunden?");

        AssertAutoTrigger(model, 3, true, "end of word, Das|");
        AssertAutoTrigger(model, 4, false, "no word Das |");
        AssertAutoTrigger(model, 1, true, "typing a single character before a word: D|as");
        AssertAutoTrigger(model, 55, false, "number, 1861|");
    }
}

public sealed class SuggestModelTriggerAndCancelOracleTests : IDisposable
{
    private readonly TextModel _model = TestTextModel.Create("abc def", uri: new Uri("test:somefile.ttt"));
    private readonly LanguageFeatureRegistry<ICompletionItemProvider> _registry = new();
    private readonly List<IDisposable> _disposables = new();
    private static readonly LanguageFilter s_test = new() { Scheme = "test" };

    public void Dispose()
    {
        foreach (var d in _disposables)
        {
            d.Dispose();
        }
        _model.Dispose();
    }

    private sealed class Provider(Func<TextModel, TextPosition, CompletionContext, CompletionList?> provide, IReadOnlyList<string>? triggerCharacters = null) : ICompletionItemProvider
    {
        public string DebugDisplayName => "test";

        public IReadOnlyList<string>? TriggerCharacters => triggerCharacters;

        public Func<CompletionItem, CompletionItem?>? Resolve { get; init; }

        public bool SupportsResolveCompletionItem => Resolve is not null;

        public Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token) =>
            Task.FromResult(provide(model, position, context));

        public Task<CompletionItem?> ResolveCompletionItem(CompletionItem item, CancellationToken token) => Task.FromResult(Resolve!(item));
    }

    private static TextRange GetDefaultSuggestRange(TextModel model, TextPosition position)
    {
        var wordUntil = SuggestWords.GetWordUntilPosition(model, position, null);
        return new TextRange(position.LineNumber, wordUntil.StartColumn, position.LineNumber, wordUntil.EndColumn);
    }

    private static CompletionItem Item(string label, CompletionItemKind kind, string insertText, TextRange range, string? sortText = null) =>
        new() { Label = label, Kind = kind, InsertText = insertText, Range = range, SortText = sortText };

    private static readonly Provider s_alwaysEmptySupport = new((_, _, _) => new CompletionList([]));

    private static readonly Provider s_alwaysSomethingSupport = new((doc, pos, _) => new CompletionList(
        [Item(SuggestWords.GetWordUntilPosition(doc, pos, null).Word, CompletionItemKind.Property, "foofoo", GetDefaultSuggestRange(doc, pos))]));

    private void Register(ICompletionItemProvider provider, LanguageSelector? selector = null) =>
        _disposables.Add(_registry.Register(selector ?? s_test, provider));

    private sealed class Oracle : IDisposable
    {
        public Oracle(TextModel model, LanguageFeatureRegistry<ICompletionItemProvider> registry)
        {
            Editor = new SuggestTestEditor(model, registry);
            Model = new SuggestModel(Editor);
            Model.DidSuggest += e => Events.Add(e);
            Model.DidCancel += e => Events.Add(e);
            Model.DidTrigger += e => Events.Add(e);
        }

        public SuggestTestEditor Editor { get; }

        public SuggestModel Model { get; }

        public List<object> Events { get; } = new();

        /// <summary>Runs the action and returns the events of type T it raised.</summary>
        public List<T> Collect<T>(Action action, bool runTimers = true)
        {
            var start = Events.Count;
            action();
            if (runTimers)
            {
                Editor.RunTimers();
            }
            return Events.Skip(start).OfType<T>().ToList();
        }

        /// <summary>The first event of type T the action raised (upstream's assertEvent).</summary>
        public T AssertEvent<T>(Action action, bool runTimers = true)
        {
            var events = Collect<T>(action, runTimers);
            events.ShouldNotBeEmpty($"no {typeof(T).Name}");
            return events[0];
        }

        public void Dispose()
        {
            Model.Dispose();
            Editor.Dispose();
        }
    }

    private Oracle WithOracle() => new(_model, _registry);

    [Fact]
    public void Events_cancel_trigger()
    {
        using var o = WithOracle();
        // upstream's assertEvent resolves on the event, before the request completes
        var e1 = o.AssertEvent<SuggestTriggerEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = true }), runTimers: false);
        e1.Auto.ShouldBeTrue();
        o.AssertEvent<SuggestCancelEvent>(() => o.Model.Cancel(), runTimers: false).Retrigger.ShouldBeFalse();
        o.Editor.RunTimers();

        o.AssertEvent<SuggestTriggerEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = true }), runTimers: false).Auto.ShouldBeTrue();
        o.AssertEvent<SuggestTriggerEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = false }), runTimers: false).Auto.ShouldBeFalse();
        o.Editor.RunTimers();
    }

    [Fact]
    public void Events_suggest_empty()
    {
        Register(s_alwaysEmptySupport);
        using var o = WithOracle();

        o.AssertEvent<SuggestCancelEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = true })).Retrigger.ShouldBeFalse();

        var e = o.AssertEvent<SuggestEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = false }));
        e.TriggerOptions.Auto.ShouldBeFalse();
        e.IsFrozen.ShouldBeFalse();
        e.CompletionModel.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void Trigger_on_type()
    {
        Register(s_alwaysSomethingSupport);
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 4);
            o.Editor.Type("d");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);
        e.CompletionModel.Items[0].Provider.ShouldBeSameAs(s_alwaysSomethingSupport);
    }

    [Fact]
    public void Keep_filtering_after_space_17400()
    {
        Register(new Provider((doc, pos, _) => new CompletionList([Item("My Table", CompletionItemKind.Property, "My Table", GetDefaultSuggestRange(doc, pos))])));
        _model.SetValue("");
        using var o = WithOracle();

        // make sure completionModel starts here!
        o.AssertEvent<SuggestEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = true }));

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 1);
            o.Editor.Type("My");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);
        e.CompletionModel.Items[0].Completion.Label.Label.ShouldBe("My Table");

        e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 3);
            o.Editor.Type(" ");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);
        e.CompletionModel.Items[0].Completion.Label.Label.ShouldBe("My Table");
    }

    [Fact]
    public void Trigger_character_always_force_a_new_completion_session_21484()
    {
        Register(new Provider((_, pos, _) => new CompletionList([Item("foo.bar", CompletionItemKind.Property, "foo.bar", new TextRange(pos.With(column: 1), pos))])));
        Register(new Provider((doc, pos, _) => new CompletionList(
        [
            Item("boom", CompletionItemKind.Property, "boom",
                new TextRange(pos with { Column = pos.Column + (doc.GetLineContent(pos.LineNumber)[pos.Column - 2] == '.' ? 0 : -1) }, pos)),
        ]), ["."]));
        _model.SetValue("");
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 1);
            o.Editor.Type("foo");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Select(i => i.Completion.Label.Label).ShouldBe(["foo.bar"]);

        var events = o.Collect<SuggestEvent>(() => o.Editor.Type("."));
        events.Count.ShouldBe(2);
        // SYNC
        events[0].TriggerOptions.Auto.ShouldBeTrue();
        events[0].CompletionModel.Items.Select(i => i.Completion.Label.Label).ShouldBe(["foo.bar"]);
        // ASYNC: triggered by the trigger character
        events[1].TriggerOptions.Auto.ShouldBeTrue();
        events[1].CompletionModel.Items.Select(i => i.Completion.Label.Label).ShouldBe(["foo.bar", "boom"]);
    }

    [Theory]
    [InlineData("+")]
    [InlineData(" ")]
    public void Intellisense_completion_does_not_respect_space_after_equal_sign_29353(string typed)
    {
        Register(s_alwaysSomethingSupport);
        using var o = WithOracle();
        _model.SetValue("fo");
        o.Editor.SetPosition(1, 3);

        var e = o.AssertEvent<SuggestEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = false }));
        e.TriggerOptions.Auto.ShouldBeFalse();
        e.IsFrozen.ShouldBeFalse();
        e.CompletionModel.Items.Count.ShouldBe(1);

        o.AssertEvent<SuggestCancelEvent>(() => o.Editor.Type(typed)).Retrigger.ShouldBeFalse();
    }

    [Fact]
    public void Incomplete_suggestion_results_cause_retriggering_when_typing_without_further_context_28400_1()
    {
        Register(new Provider((_, pos, _) => new CompletionList([Item("foo", CompletionItemKind.Property, "foo", new TextRange(pos.With(column: 1), pos))]) { Incomplete = true }));
        using var o = WithOracle();
        _model.SetValue("foo");
        o.Editor.SetPosition(1, 4);

        var e = o.AssertEvent<SuggestEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = false }));
        e.TriggerOptions.Auto.ShouldBeFalse();
        e.CompletionModel.GetIncompleteProvider().Count.ShouldBe(1);
        e.CompletionModel.Items.Count.ShouldBe(1);

        o.AssertEvent<SuggestCancelEvent>(() => o.Editor.Type(";")).Retrigger.ShouldBeFalse();
    }

    [Fact]
    public void Incomplete_suggestion_results_cause_retriggering_when_typing_without_further_context_28400_2()
    {
        Register(new Provider((_, pos, _) => new CompletionList([Item("foo;", CompletionItemKind.Property, "foo", new TextRange(pos.With(column: 1), pos))]) { Incomplete = true }));
        using var o = WithOracle();
        _model.SetValue("foo");
        o.Editor.SetPosition(1, 4);

        var e = o.AssertEvent<SuggestEvent>(() => o.Model.Trigger(new SuggestTriggerOptions { Auto = false }));
        e.TriggerOptions.Auto.ShouldBeFalse();
        e.CompletionModel.GetIncompleteProvider().Count.ShouldBe(1);
        e.CompletionModel.Items.Count.ShouldBe(1);

        // while we cancel incrementally enriching the set of completions we still filter against those that we have
        // until now
        e = o.AssertEvent<SuggestEvent>(() => o.Editor.Type(";"));
        e.TriggerOptions.Auto.ShouldBeFalse();
        e.CompletionModel.GetIncompleteProvider().Count.ShouldBe(1);
        e.CompletionModel.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Trigger_character_is_provided_in_suggest_context()
    {
        var triggerCharacter = string.Empty;
        Register(new Provider((_, pos, context) =>
        {
            context.TriggerKind.ShouldBe(CompletionTriggerKind.TriggerCharacter);
            triggerCharacter = context.TriggerCharacter!;
            return new CompletionList([Item("foo.bar", CompletionItemKind.Property, "foo.bar", new TextRange(pos.With(column: 1), pos))]);
        }, ["."]));
        _model.SetValue("");
        using var o = WithOracle();

        o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 1);
            o.Editor.Type("foo.");
        });
        triggerCharacter.ShouldBe(".");
    }

    [Fact]
    public void Mac_press_and_hold_accent_character_insertion_does_not_update_suggestions_35269()
    {
        Register(new Provider((_, pos, _) => new CompletionList(
        [
            Item("abc", CompletionItemKind.Property, "abc", new TextRange(pos.With(column: 1), pos)),
            Item("äbc", CompletionItemKind.Property, "äbc", new TextRange(pos.With(column: 1), pos)),
        ]) { Incomplete = true }));
        _model.SetValue("");
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 1);
            o.Editor.Type("a");
        });
        e.CompletionModel.Items.Select(i => i.Completion.Label.Label).ShouldBe(["abc"]);

        e = o.AssertEvent<SuggestEvent>(() => o.Editor.ExecuteEdits("test", [EditOperation.Replace(new TextRange(1, 1, 1, 2), "ä")]));
        // suggest model changed to äbc
        e.CompletionModel.Items.Select(i => i.Completion.Label.Label).ShouldBe(["äbc"]);
    }

    [Fact]
    public void Backspace_should_not_always_cancel_code_completion_36491()
    {
        Register(s_alwaysSomethingSupport);
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 4);
            o.Editor.Type("d");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);
        e.CompletionModel.Items[0].Provider.ShouldBeSameAs(s_alwaysSomethingSupport);

        e = o.AssertEvent<SuggestEvent>(() => o.Editor.DeleteLeft());
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);
        e.CompletionModel.Items[0].Provider.ShouldBeSameAs(s_alwaysSomethingSupport);
    }

    [Fact]
    public void Completion_unexpectedly_triggers_on_second_keypress_of_an_edit_group_in_a_snippet_43523()
    {
        Register(s_alwaysSomethingSupport);
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("d");
            o.Editor.SetSelection(1, 1, 1, 2);
            o.Editor.Type("e");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);
        e.CompletionModel.Items[0].Provider.ShouldBeSameAs(s_alwaysSomethingSupport);
    }

    [Fact]
    public void Fails_to_render_completion_details_47988()
    {
        var disposeA = 0;
        var disposeB = 0;
        Register(new Provider((doc, pos, _) => new CompletionList([Item("CompleteNot", CompletionItemKind.Folder, "Incomplete", GetDefaultSuggestRange(doc, pos), "a")])
        {
            Incomplete = true,
            Dispose = () => disposeA += 1,
        }));
        Register(new Provider((doc, pos, _) => new CompletionList([Item("Complete", CompletionItemKind.Folder, "Complete", GetDefaultSuggestRange(doc, pos), "z")])
        {
            Dispose = () => disposeB += 1,
        })
        { Resolve = item => item });
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("");
            o.Editor.SetSelection(1, 1, 1, 1);
            o.Editor.Type("c");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(2);
        disposeA.ShouldBe(0);
        disposeB.ShouldBe(0);

        e = o.AssertEvent<SuggestEvent>(() => o.Editor.Type("o"));
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(2);

        // clean up
        o.Model.Clear();
        disposeA.ShouldBe(2); // provide got called two times!
        disposeB.ShouldBe(1);
    }

    [Fact]
    public void Trigger_full_completions_when_incomplete_completions_are_already_active_99504()
    {
        var countA = 0;
        var countB = 0;
        Register(new Provider((_, pos, _) =>
        {
            countA += 1;
            return new CompletionList([Item("Z aaa", CompletionItemKind.Class, "Z aaa", new TextRange(1, 1, pos.LineNumber, pos.Column))]);
        }));
        Register(new Provider((doc, pos, _) =>
        {
            countB += 1;
            if (!SuggestWords.GetWordUntilPosition(doc, pos, null).Word.StartsWith('a'))
            {
                return null;
            }
            return new CompletionList([Item("aaa", CompletionItemKind.Folder, "aaa", GetDefaultSuggestRange(doc, pos))]);
        }));
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("");
            o.Editor.SetSelection(1, 1, 1, 1);
            o.Editor.Type("Z");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["Z aaa"]);

        // started another word: Z a|; items should be: Z aaa, aaa
        var events = o.Collect<SuggestEvent>(() => o.Editor.Type(" a"));
        e = events[^1];
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["Z aaa", "aaa"]);
        countA.ShouldBe(1); // keep the suggestions from the "active" provider (#106573)
        countB.ShouldBe(2);
    }

    [Fact]
    public void Letters_as_trigger_characters_do_not_block_other_completion_items_127815()
    {
        Register(new Provider((_, pos, _) => new CompletionList([Item("AAAA", CompletionItemKind.Class, "WordTriggerA", new TextRange(pos))])));
        Register(new Provider((_, pos, _) => new CompletionList([Item("AAAA", CompletionItemKind.Class, "AutoTriggerA", new TextRange(pos))]), ["a", "."]));
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("");
            o.Editor.SetSelection(1, 1, 1, 1);
            o.Editor.Type(".");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);

        _model.SetValue("");

        e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("");
            o.Editor.SetSelection(1, 1, 1, 1);
            o.Editor.Type("a");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void Unexpected_suggest_scoring_167242()
    {
        // word-based
        Register(new Provider((doc, pos, _) =>
        {
            var word = SuggestWords.GetWordUntilPosition(doc, pos, null);
            return new CompletionList([Item("pull", CompletionItemKind.Text, "pull", new TextRange(pos.LineNumber, word.StartColumn, pos.LineNumber, word.EndColumn))]);
        }), "*");
        // JSON-based
        Register(new Provider((_, pos, _) => new CompletionList([Item("git.pull", CompletionItemKind.Class, "git.pull", new TextRange(pos.LineNumber, 1, pos.LineNumber, pos.Column))])));
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("gi");
            o.Editor.SetSelection(1, 3, 1, 3);
            o.Editor.Type("t");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["git.pull"]);

        o.Editor.Type(".");

        e = o.Collect<SuggestEvent>(() => o.Editor.Type("p"))[^1];
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["git.pull"]);
    }

    [Fact]
    public void Completion_list_closes_unexpectedly_when_typing_a_digit_after_a_word_separator_169390()
    {
        var requestCounts = new[] { 0, 0 };
        Register(new Provider((_, pos, _) =>
        {
            requestCounts[0] += 1;
            return new CompletionList(
            [
                Item("foo-20", CompletionItemKind.Text, "foo-20", new TextRange(pos.LineNumber, 1, pos.LineNumber, pos.Column)),
                Item("foo-hello", CompletionItemKind.Text, "foo-hello", new TextRange(pos.LineNumber, 1, pos.LineNumber, pos.Column)),
            ]);
        }));
        Register(new Provider((_, pos, ctx) =>
        {
            requestCounts[1] += 1;
            if (ctx.TriggerKind != CompletionTriggerKind.TriggerCharacter)
            {
                return null;
            }
            return new CompletionList([Item("foo-210", CompletionItemKind.Class, "foo-210", new TextRange(pos.LineNumber, 1, pos.LineNumber, pos.Column))]);
        }, ["2"]));
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("foo");
            o.Editor.SetSelection(1, 4, 1, 4);
            o.Model.Trigger(new SuggestTriggerOptions { Auto = false });
        });
        e.TriggerOptions.Auto.ShouldBeFalse();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["foo-20", "foo-hello"]);

        o.Editor.Type("-");

        e = o.Collect<SuggestEvent>(() => o.Editor.Type("2"))[^1];
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["foo-20", "foo-210"]);
        requestCounts.ShouldBe([1, 2]);
    }

    [Fact]
    public void Set_refilter_flag_keep_triggerKind()
    {
        Register(new Provider((doc, pos, _) =>
        {
            var word = SuggestWords.GetWordUntilPosition(doc, pos, null).Word;
            return new CompletionList([Item(word.Length > 0 ? word : "hello", CompletionItemKind.Property, "foofoo", GetDefaultSuggestRange(doc, pos))]);
        }, ["."]));
        using var o = WithOracle();

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("foo");
            o.Editor.SetSelection(1, 4, 1, 4);
            o.Editor.Type("o");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.TriggerOptions.TriggerCharacter.ShouldBeNull();
        e.TriggerOptions.TriggerKind.ShouldBeNull();
        e.CompletionModel.Items.Count.ShouldBe(1);

        e = o.Collect<SuggestEvent>(() => o.Editor.Type("."))[^1];
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.TriggerOptions.Refilter.ShouldBeFalse();
        e.TriggerOptions.TriggerCharacter.ShouldBe(".");
        e.TriggerOptions.TriggerKind.ShouldBe(CompletionTriggerKind.TriggerCharacter);
        e.CompletionModel.Items.Count.ShouldBe(1);

        e = o.AssertEvent<SuggestEvent>(() => o.Editor.Type("h"));
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.TriggerOptions.Refilter.ShouldBeTrue();
        e.TriggerOptions.TriggerCharacter.ShouldBe(".");
        e.TriggerOptions.TriggerKind.ShouldBe(CompletionTriggerKind.TriggerCharacter);
        e.CompletionModel.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Snippets_gone_from_IntelliSense_173244()
    {
        var snippetProvider = new Provider((doc, pos, _) => new CompletionList([Item("log", CompletionItemKind.Snippet, "log", GetDefaultSuggestRange(doc, pos))]));
        Register(new Provider((doc, pos, _) => new CompletionList([Item("locals", CompletionItemKind.Property, "locals", GetDefaultSuggestRange(doc, pos))]) { Incomplete = true }, ["."]));
        using var o = WithOracle();
        o.Editor.SnippetSuggestSupport = snippetProvider;

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetValue("");
            o.Editor.SetSelection(1, 1, 1, 1);
            o.Editor.Type("l");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.TriggerOptions.TriggerCharacter.ShouldBeNull();
        e.TriggerOptions.TriggerKind.ShouldBeNull();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["locals", "log"]);

        e = o.Collect<SuggestEvent>(() => o.Editor.Type("o"))[^1];
        e.TriggerOptions.TriggerKind.ShouldBe(CompletionTriggerKind.TriggerForIncompleteCompletions);
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Select(i => i.TextLabel).ShouldBe(["locals", "log"]);
    }

    [Fact]
    public void Quick_suggestions_off_disables_quick_suggestions_for_all_token_types()
    {
        Register(s_alwaysSomethingSupport);
        using var o = WithOracle();
        o.Editor.UpdateOptions(x => x with { QuickSuggestions = new QuickSuggestionsOptions(QuickSuggestionsValue.Off, QuickSuggestionsValue.Off, QuickSuggestionsValue.Off) });

        o.Collect<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 4);
            o.Editor.Type("d");
        }).ShouldBeEmpty();
    }

    [Fact]
    public void OffWhenInlineCompletions_allows_quick_suggest_when_no_inline_suggestion_shows()
    {
        Register(s_alwaysSomethingSupport);
        using var o = WithOracle();
        o.Editor.UpdateOptions(x => x with { QuickSuggestions = new QuickSuggestionsOptions(QuickSuggestionsValue.OffWhenInlineCompletions, QuickSuggestionsValue.Off, QuickSuggestionsValue.Off) });

        var e = o.AssertEvent<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 4);
            o.Editor.Type("d");
        });
        e.TriggerOptions.Auto.ShouldBeTrue();
        e.CompletionModel.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Composition_defers_triggering_until_it_ends()
    {
        Register(s_alwaysSomethingSupport);
        using var o = WithOracle();

        o.Editor.FireCompositionStart();
        o.Collect<SuggestEvent>(() =>
        {
            o.Editor.SetPosition(1, 4);
            o.Editor.Type("d");
        }).ShouldBeEmpty();

        o.Collect<SuggestEvent>(() => o.Editor.FireCompositionEnd()).ShouldNotBeEmpty();
    }

    // ---- reopenOnDelete (recordSuggestions)

    private static readonly Provider s_mentionSupport = new((doc, pos, _) => new CompletionList(
        new[] { "abc", "index" }.Select(label => Item(label, CompletionItemKind.Property, label, GetDefaultSuggestRange(doc, pos))).ToList())
    {
        Incomplete = true,
    }, ["@"]);

    /// <summary>Types the keys into an empty editor with the given options and records what suggestions show after each of them, or that they closed.</summary>
    private List<string> RecordSuggestions(Func<SuggestOptions, SuggestOptions> options, string[] keys)
    {
        _model.SetValue("");
        using var o = WithOracle();
        o.Editor.UpdateOptions(options);
        var transcript = new List<string>();
        Action<SuggestEvent> onSuggest = e => transcript.Add($"{_model.GetValue()}: {string.Join(", ", e.CompletionModel.Items.Select(item => item.TextLabel))}");
        Action<SuggestCancelEvent> onCancel = e =>
        {
            if (!e.Retrigger)
            {
                transcript.Add($"{_model.GetValue()}: closed");
            }
        };
        o.Model.DidSuggest += onSuggest;
        o.Model.DidCancel += onCancel;
        foreach (var key in keys)
        {
            switch (key)
            {
                case "Backspace":
                    o.Editor.DeleteLeft();
                    break;
                case "Left":
                    o.Editor.CursorLeft();
                    break;
                case "Right":
                    o.Editor.CursorRight();
                    break;
                case "Escape":
                    o.Model.Cancel();
                    break;
                case "TurnOffReopenOnDelete":
                    o.Editor.UpdateOptions(x => x with { ReopenOnDelete = false });
                    break;
                default:
                    o.Editor.Type(key);
                    break;
            }
        }
        // upstream disposes the listeners before the model
        o.Model.DidSuggest -= onSuggest;
        o.Model.DidCancel -= onCancel;
        return transcript;
    }

    private static readonly QuickSuggestionsOptions s_quickOff = new(QuickSuggestionsValue.Off, QuickSuggestionsValue.Off, QuickSuggestionsValue.Off);
    private static readonly QuickSuggestionsOptions s_quickOn = new(QuickSuggestionsValue.On, QuickSuggestionsValue.On, QuickSuggestionsValue.On);

    [Fact]
    public void ReopenOnDelete_brings_back_suggestions_on_Backspace()
    {
        Register(s_mentionSupport);

        RecordSuggestions(x => x with { QuickSuggestions = s_quickOff, ReopenOnDelete = false }, ["@", "i", "q", "Backspace", "Backspace"])
            .ShouldBe(["@: abc, index", "@i: index", "@iq: closed"]);
        RecordSuggestions(x => x with { QuickSuggestions = s_quickOn, QuickSuggestionsDelay = 0, ReopenOnDelete = false }, ["i", "q", "Backspace"])
            .ShouldBe(["i: index", "iq: closed"]);
        // Backspace after nothing matched does what it does while suggestions show, and Backspace back to the trigger
        // character retriggers it rather than closing suggestions
        RecordSuggestions(x => x with { QuickSuggestions = s_quickOff, ReopenOnDelete = true }, ["@", "i", "q", "Backspace", "Backspace"])
            .ShouldBe(["@: abc, index", "@i: index", "@iq: closed", "@i: index", "@: abc, index"]);
        RecordSuggestions(x => x with { QuickSuggestions = s_quickOn, QuickSuggestionsDelay = 0, ReopenOnDelete = true }, ["i", "q", "Backspace"])
            .ShouldBe(["i: index", "iq: closed", "i: index"]);
    }

    [Fact]
    public void ReopenOnDelete_does_not_bring_back_suggestions_after_Escape_cursor_keys_typing_past_the_text_or_turning_it_off()
    {
        Register(s_mentionSupport);
        Func<SuggestOptions, SuggestOptions> options = x => x with { QuickSuggestions = s_quickOff, ReopenOnDelete = true };

        RecordSuggestions(options, ["@", "i", "Escape", "Backspace"]).ShouldBe(["@: abc, index", "@i: index", "@i: closed"]);
        RecordSuggestions(options, ["@", "i", "q", "Left", "Right", "Backspace"]).ShouldBe(["@: abc, index", "@i: index", "@iq: closed"]);
        RecordSuggestions(options, ["@", "q", " ", "a", "b", "Backspace"]).ShouldBe(["@: abc, index", "@q: closed"]);
        RecordSuggestions(options, ["@", "i", "Left"]).ShouldBe(["@: abc, index", "@i: index", "@i: closed"]);
        RecordSuggestions(options, ["@", "i", "q", "TurnOffReopenOnDelete", "Backspace"]).ShouldBe(["@: abc, index", "@i: index", "@iq: closed"]);
    }
}
