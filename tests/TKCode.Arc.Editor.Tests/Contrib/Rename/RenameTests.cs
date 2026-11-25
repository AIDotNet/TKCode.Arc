// Tests of the rename skeleton (vscode's src/vs/editor/contrib/rename/browser/rename.ts, commit b57d5c9e, MIT license).
// Upstream has no unit tests of rename; these are written for this port and follow the behavior of RenameSkeleton:
// the provider order, resolving the location, the word fallback, rejections and the fall-through to the next provider.

using Shouldly;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.Rename;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class RenameTests : IDisposable
{
    private const string LangId = "renameLang";
    private static readonly Uri s_uri = new("file:///a.txt");
    private readonly TextModel _model = TestTextModel.Create("let foo = 1;\nfoo + foo", LangId, uri: s_uri);
    private readonly LanguageFeatureRegistry<IRenameProvider> _registry = new();
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _model.Dispose();
    }

    private sealed class Provider : IRenameProvider
    {
        public Func<TextPosition, string, RenameEdits?> Edits { get; init; } = (_, _) => null;

        public Func<TextPosition, RenameLocationResult?>? Location { get; init; }

        public int EditCalls { get; private set; }

        public bool SupportsResolveRenameLocation => Location is not null;

        public Task<RenameEdits?> ProvideRenameEdits(TextModel model, TextPosition position, string newName, CancellationToken token)
        {
            EditCalls++;
            return Task.FromResult(Edits(position, newName));
        }

        public Task<RenameLocationResult?> ResolveRenameLocation(TextModel model, TextPosition position, CancellationToken token) =>
            Task.FromResult(Location!(position));
    }

    private static RenameEdits EditsFor(string newName, params TextRange[] ranges) =>
        new(new WorkspaceEdit([.. ranges.Select(range => new WorkspaceTextEdit(s_uri, new TextEdit(range, newName)))]));

    [Fact]
    public async Task Without_resolve_the_word_at_the_position_is_the_location()
    {
        _disposables.Add(_registry.Register(LangId, new Provider()));
        var location = await RenameService.PrepareRename(_registry, _model, new TextPosition(1, 6));
        location.ShouldNotBeNull();
        location.Range.ShouldBe(new TextRange(1, 5, 1, 8));
        location.Text.ShouldBe("foo");
        location.RejectReason.ShouldBeNull();
    }

    [Fact]
    public async Task No_word_gives_an_empty_location()
    {
        _disposables.Add(_registry.Register(LangId, new Provider()));
        var location = await RenameService.PrepareRename(_registry, _model, new TextPosition(1, 9));
        location.ShouldNotBeNull();
        location.Range.ShouldBe(new TextRange(1, 9, 1, 9));
        location.Text.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task The_first_provider_that_resolves_wins_and_rejections_are_collected_otherwise()
    {
        _disposables.Add(_registry.Register(LangId, new Provider { Location = _ => new RenameLocationResult(default, string.Empty) { RejectReason = "older says no" } }));
        _disposables.Add(_registry.Register(LangId, new Provider { Location = _ => new RenameLocationResult(default, string.Empty) { RejectReason = "newer says no" } }));

        // the newest provider is asked first
        var rejected = await RenameService.PrepareRename(_registry, _model, new TextPosition(1, 6));
        rejected.ShouldNotBeNull();
        rejected.RejectReason.ShouldBe("newer says no\nolder says no");
        rejected.Text.ShouldBe("foo");

        _disposables.Add(_registry.Register(LangId, new Provider { Location = _ => new RenameLocationResult(new TextRange(1, 5, 1, 8), "FOO") }));
        var resolved = await RenameService.PrepareRename(_registry, _model, new TextPosition(1, 6));
        resolved.ShouldNotBeNull();
        resolved.Text.ShouldBe("FOO");
        resolved.RejectReason.ShouldBeNull();
    }

    [Fact]
    public async Task A_null_location_asks_the_next_provider()
    {
        _disposables.Add(_registry.Register(LangId, new Provider { Location = _ => new RenameLocationResult(new TextRange(2, 1, 2, 4), "foo") }));
        _disposables.Add(_registry.Register(LangId, new Provider { Location = _ => null }));
        var location = await RenameService.PrepareRename(_registry, _model, new TextPosition(2, 2));
        location.ShouldNotBeNull();
        location.Range.ShouldBe(new TextRange(2, 1, 2, 4));
    }

    [Fact]
    public async Task Edits_come_from_the_resolving_provider_and_fall_through_on_rejection()
    {
        var older = new Provider { Edits = (_, name) => EditsFor(name, new TextRange(1, 5, 1, 8)) };
        var newer = new Provider { Edits = (_, _) => new RenameEdits(new WorkspaceEdit([])) { RejectReason = "cannot" } };
        _disposables.Add(_registry.Register(LangId, older));
        _disposables.Add(_registry.Register(LangId, newer));

        var result = await RenameService.Rename(_registry, _model, new TextPosition(1, 6), "bar");
        result.RejectReason.ShouldBeNull();
        result.Edit.Edits.Count.ShouldBe(1);
        newer.EditCalls.ShouldBe(1);
        older.EditCalls.ShouldBe(1);
    }

    [Fact]
    public async Task No_result_from_every_provider_rejects_with_their_reasons()
    {
        _disposables.Add(_registry.Register(LangId, new Provider()));
        _disposables.Add(_registry.Register(LangId, new Provider { Edits = (_, _) => new RenameEdits(new WorkspaceEdit([])) { RejectReason = "nope" } }));

        var result = await RenameService.RawRename(_registry, _model, new TextPosition(1, 6), "bar");
        result.RejectReason.ShouldBe("nope\n" + RenameSkeleton.NoResultMessage);
        result.Edit.Edits.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_rejected_location_rejects_the_rename_without_asking_for_edits()
    {
        var provider = new Provider
        {
            Location = _ => new RenameLocationResult(default, string.Empty) { RejectReason = "not a symbol" },
            Edits = (_, name) => EditsFor(name, new TextRange(1, 5, 1, 8)),
        };
        _disposables.Add(_registry.Register(LangId, provider));

        var result = await RenameService.Rename(_registry, _model, new TextPosition(1, 6), "bar");
        result.RejectReason.ShouldBe("not a symbol");
        provider.EditCalls.ShouldBe(0);
        RenameService.HasProvider(_registry, _model).ShouldBeTrue();
    }

    [Fact]
    public void Workspace_edits_split_between_the_model_and_the_host()
    {
        var other = new Uri("file:///b.txt");
        var edit = new WorkspaceEdit(
        [
            new WorkspaceTextEdit(s_uri, new TextEdit(new TextRange(1, 5, 1, 8), "bar")),
            new WorkspaceTextEdit(other, new TextEdit(new TextRange(1, 1, 1, 1), "x")),
            new WorkspaceTextEdit(s_uri, new TextEdit(new TextRange(2, 1, 2, 4), "bar")),
            new WorkspaceFileEdit(null, new Uri("file:///c.txt")),
        ]);

        var split = WorkspaceEdits.Split(edit, _model);
        split.Error.ShouldBeNull();
        split.ModelEdits.Select(e => e.Range).ShouldBe([new TextRange(1, 5, 1, 8), new TextRange(2, 1, 2, 4)]);
        split.Other.ShouldNotBeNull().Edits.Count.ShouldBe(2);
        split.ToEditOperations().Count.ShouldBe(2);
        WorkspaceEdits.Count(edit).ShouldBe((3, 2));

        var stale = WorkspaceEdits.Split(new WorkspaceEdit([new WorkspaceTextEdit(s_uri, new TextEdit(new TextRange(1, 1, 1, 1), "x")) { VersionId = _model.GetVersionId() + 1 }]), _model);
        stale.Error.ShouldBe(WorkspaceEdits.ModelChangedMessage);
        WorkspaceEdits.Split(new WorkspaceEdit([new WorkspaceTextEdit(s_uri, new TextEdit(new TextRange(1, 1, 1, 1), "x"))]), _model).Other.ShouldBeNull();
    }

    [Fact]
    public void Words_at_and_until_a_position()
    {
        ModelWords.GetWordAtPosition(_model, new TextPosition(1, 8)).ShouldNotBeNull().Word.ShouldBe("foo");
        ModelWords.GetWordAtPosition(_model, new TextPosition(1, 9)).ShouldBeNull();
        var until = ModelWords.GetWordUntilPosition(_model, new TextPosition(1, 7));
        until.ShouldBe(new WordAtPosition("fo", 5, 7));
        ModelWords.GetWordUntilPosition(_model, new TextPosition(1, -3)).ShouldBe(new WordAtPosition("", 1, 1));
    }
}
