// Ported from vscode (src/vs/editor/contrib/codeAction/test/browser/codeAction.test.ts, commit b57d5c9e), MIT license.
// All 7 upstream tests. The test data's command objects and edits are CodeAction records; the sort test compares the
// titles (and diagnostics) in order instead of deep-equal item objects. Below them, tests of the filters, the kinds,
// the user arguments and resolving written for this port.

using Shouldly;
using TKCode.Arc.Editor.Contrib.CodeAction;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using Xunit;
using CodeAction = TKCode.Arc.Editor.Languages.CodeAction;

namespace TKCode.Arc.Editor.Tests;

public sealed class CodeActionTests : IDisposable
{
    private const string LangId = "fooLang";
    private readonly TextModel _model = TestTextModel.Create("test1\ntest2\ntest3", LangId, uri: new Uri("untitled:path"));
    private readonly LanguageFeatureRegistry<ICodeActionProvider> _registry = new();
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _model.Dispose();
    }

    internal sealed class StaticProvider(params CodeAction[] actions) : ICodeActionProvider
    {
        public int DisposeCount { get; private set; }

        public Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token) =>
            Task.FromResult<CodeActionList?>(new CodeActionList(actions) { Dispose = () => DisposeCount++ });
    }

    private sealed class DelegateProvider(Func<CodeActionContext, CodeActionList?> provide, IReadOnlyList<string>? kinds = null) : ICodeActionProvider
    {
        public IReadOnlyList<string>? ProvidedCodeActionKinds => kinds;

        public Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token) =>
            Task.FromResult(provide(context));
    }

    private static readonly MarkerData[] s_abcDiagnostics =
    [
        new() { StartLineNumber = 1, StartColumn = 1, EndLineNumber = 2, EndColumn = 1, Severity = MarkerSeverity.Error, Message = "abc" },
    ];

    private static readonly MarkerData[] s_bcdDiagnostics =
    [
        new() { StartLineNumber = 1, StartColumn = 1, EndLineNumber = 2, EndColumn = 1, Severity = MarkerSeverity.Error, Message = "bcd" },
    ];

    private static readonly CodeAction s_diagnosticsAbc = new("bTitle") { Diagnostics = s_abcDiagnostics };
    private static readonly CodeAction s_diagnosticsBcd = new("aTitle") { Diagnostics = s_bcdDiagnostics };
    private static readonly CodeAction s_commandAbc = new("Extract to inner function in function \"test\"") { Command = new LanguageCommand("1", "abc") };
    private static readonly CodeAction s_spellingBcd = new("abc") { Diagnostics = [], Edit = new WorkspaceEdit([]) };
    private static readonly CodeAction s_tsLintAbc = new("abc") { Command = new LanguageCommand("_internal_command_delegation", "abc") { Arguments = [] } };
    private static readonly CodeAction s_tsLintBcd = new("bcd") { Command = new LanguageCommand("_internal_command_delegation", "bcd") { Arguments = [] } };

    private static readonly TextRange s_range = new(1, 1, 2, 1);

    private Task<CodeActionSet> GetCodeActions(CodeActionTrigger trigger) =>
        CodeActionService.GetCodeActions(_registry, _model, s_range, trigger, CancellationToken.None);

    private static CodeActionTrigger Auto(CodeActionTriggerSource source = CodeActionTriggerSource.Default, CodeActionFilter? filter = null) =>
        new(CodeActionTriggerType.Auto, source) { Filter = filter };

    [Fact]
    public async Task CodeActions_are_sorted_by_type_38623()
    {
        var provider = new StaticProvider(s_commandAbc, s_diagnosticsBcd, s_spellingBcd, s_tsLintBcd, s_tsLintAbc, s_diagnosticsAbc);
        _disposables.Add(_registry.Register(LangId, provider));

        using var set = await CodeActionService.GetCodeActions(_registry, _model, s_range, new CodeActionTrigger(CodeActionTriggerType.Invoke, CodeActionTriggerSource.Default), CancellationToken.None);

        // code actions with a diagnostics array come first without further sorting; the others in the given order
        set.ValidActions.Select(item => item.Action).ShouldBe([s_diagnosticsBcd, s_diagnosticsAbc, s_commandAbc, s_spellingBcd, s_tsLintBcd, s_tsLintAbc]);
        set.ValidActions.ShouldAllBe(item => ReferenceEquals(item.Provider, provider));
    }

    [Fact]
    public async Task GetCodeActions_should_filter_by_scope()
    {
        _disposables.Add(_registry.Register(LangId, new StaticProvider(new CodeAction("a") { Kind = "a" }, new CodeAction("b") { Kind = "b" }, new CodeAction("a.b") { Kind = "a.b" })));

        using (var set = await GetCodeActions(Auto(filter: new CodeActionFilter { Include = new HierarchicalKind("a") })))
        {
            set.ValidActions.Select(item => item.Action.Title).ShouldBe(["a", "a.b"]);
        }

        using (var set = await GetCodeActions(Auto(filter: new CodeActionFilter { Include = new HierarchicalKind("a.b") })))
        {
            set.ValidActions.Select(item => item.Action.Title).ShouldBe(["a.b"]);
        }

        using (var set = await GetCodeActions(Auto(filter: new CodeActionFilter { Include = new HierarchicalKind("a.b.c") })))
        {
            set.ValidActions.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task GetCodeActions_should_forward_requested_scope_to_providers()
    {
        _disposables.Add(_registry.Register(LangId, new DelegateProvider(context =>
            new CodeActionList([new CodeAction(context.Only ?? string.Empty) { Kind = context.Only }]))));

        using var set = await GetCodeActions(Auto(filter: new CodeActionFilter { Include = new HierarchicalKind("a") }));
        set.ValidActions.Select(item => item.Action.Title).ShouldBe(["a"]);
    }

    [Fact]
    public async Task GetCodeActions_should_not_return_source_code_action_by_default()
    {
        _disposables.Add(_registry.Register(LangId, new StaticProvider(new CodeAction("a") { Kind = CodeActionKind.Source.Value }, new CodeAction("b") { Kind = "b" })));

        using (var set = await GetCodeActions(Auto(CodeActionTriggerSource.SourceAction)))
        {
            set.ValidActions.Select(item => item.Action.Title).ShouldBe(["b"]);
        }

        using (var set = await GetCodeActions(Auto(filter: new CodeActionFilter { Include = CodeActionKind.Source, IncludeSourceActions = true })))
        {
            set.ValidActions.Select(item => item.Action.Title).ShouldBe(["a"]);
        }
    }

    [Fact]
    public async Task GetCodeActions_should_support_filtering_out_some_requested_source_code_actions_84602()
    {
        _disposables.Add(_registry.Register(LangId, new StaticProvider(
            new CodeAction("a") { Kind = CodeActionKind.Source.Value },
            new CodeAction("b") { Kind = CodeActionKind.Source.Append("test").Value },
            new CodeAction("c") { Kind = "c" })));

        using var set = await GetCodeActions(Auto(CodeActionTriggerSource.SourceAction, new CodeActionFilter
        {
            Include = CodeActionKind.Source.Append("test"),
            Excludes = [CodeActionKind.Source],
            IncludeSourceActions = true,
        }));
        set.ValidActions.Select(item => item.Action.Title).ShouldBe(["b"]);
    }

    [Fact]
    public async Task GetCodeActions_no_invoke_a_provider_that_has_been_excluded_84602()
    {
        var baseType = CodeActionKind.Refactor;
        var subType = CodeActionKind.Refactor.Append("sub");
        _disposables.Add(_registry.Register(LangId, new StaticProvider(new CodeAction("a") { Kind = baseType.Value })));

        var didInvoke = false;
        _disposables.Add(_registry.Register(LangId, new DelegateProvider(_ =>
        {
            didInvoke = true;
            return new CodeActionList([new CodeAction("x") { Kind = subType.Value }]);
        }, [subType.Value])));

        using var set = await GetCodeActions(Auto(CodeActionTriggerSource.Refactor, new CodeActionFilter { Include = baseType, Excludes = [subType] }));
        didInvoke.ShouldBeFalse();
        set.ValidActions.Select(item => item.Action.Title).ShouldBe(["a"]);
    }

    [Fact]
    public async Task GetCodeActions_should_not_invoke_code_action_providers_filtered_out_by_providedCodeActionKinds()
    {
        var wasInvoked = false;
        _disposables.Add(_registry.Register(LangId, new DelegateProvider(_ =>
        {
            wasInvoked = true;
            return new CodeActionList([]);
        }, [CodeActionKind.Refactor.Value])));

        using var set = await GetCodeActions(Auto(CodeActionTriggerSource.Refactor, new CodeActionFilter { Include = CodeActionKind.QuickFix }));
        set.ValidActions.ShouldBeEmpty();
        wasInvoked.ShouldBeFalse();
    }

    // ---- written for this port

    [Fact]
    public void HierarchicalKind_contains_intersects_and_appends()
    {
        var refactor = new HierarchicalKind("refactor");
        refactor.Contains(new HierarchicalKind("refactor.extract")).ShouldBeTrue();
        refactor.Contains(new HierarchicalKind("refactorx")).ShouldBeFalse();
        new HierarchicalKind("refactor.extract").Contains(refactor).ShouldBeFalse();
        new HierarchicalKind("refactor.extract").Intersects(refactor).ShouldBeTrue();
        HierarchicalKind.Empty.Contains(refactor).ShouldBeTrue();
        HierarchicalKind.None.Contains(refactor).ShouldBeFalse();
        HierarchicalKind.Empty.Append("a", "b").Value.ShouldBe("a.b");
        CodeActionKind.SourceOrganizeImports.Value.ShouldBe("source.organizeImports");
    }

    [Fact]
    public async Task Disabled_actions_are_kept_in_all_actions_only_and_AI_actions_go_last()
    {
        _disposables.Add(_registry.Register(LangId, new StaticProvider(
            new CodeAction("ai") { IsAI = true },
            new CodeAction("disabled") { Disabled = "not now" },
            new CodeAction("preferred") { IsPreferred = true, Kind = "quickfix" })));

        using var set = await GetCodeActions(Auto());
        set.AllActions.Select(item => item.Action.Title).ShouldBe(["preferred", "disabled", "ai"]);
        set.ValidActions.Select(item => item.Action.Title).ShouldBe(["preferred", "ai"]);
        set.HasAutoFix.ShouldBeTrue();
        set.HasAIFix.ShouldBeTrue();
        set.AllAIFixes.ShouldBeFalse();
    }

    [Fact]
    public async Task Only_preferred_actions_when_asked_and_the_set_disposes_the_lists()
    {
        var provider = new StaticProvider(new CodeAction("a") { Kind = "quickfix", IsPreferred = true }, new CodeAction("b") { Kind = "quickfix" });
        _disposables.Add(_registry.Register(LangId, provider));

        var set = await GetCodeActions(Auto(filter: new CodeActionFilter { OnlyIncludePreferredActions = true }));
        set.ValidActions.Select(item => item.Action.Title).ShouldBe(["a"]);
        provider.DisposeCount.ShouldBe(0);
        set.Dispose();
        set.Dispose();
        provider.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_provider_that_throws_returns_nothing()
    {
        var reported = new List<Exception>();
        void OnError(Exception e) => reported.Add(e);
        UnexpectedErrors.Occurred += OnError;
        try
        {
            _disposables.Add(_registry.Register(LangId, new DelegateProvider(_ => throw new InvalidOperationException("boom"))));
            _disposables.Add(_registry.Register(LangId, new StaticProvider(new CodeAction("ok"))));
            using var set = await GetCodeActions(Auto());
            set.ValidActions.Select(item => item.Action.Title).ShouldBe(["ok"]);
            reported.ShouldContain(e => e.Message == "boom");
        }
        finally
        {
            UnexpectedErrors.Occurred -= OnError;
        }
    }

    [Fact]
    public async Task Resolve_fills_in_the_edit_once()
    {
        var provider = new ResolvingProvider();
        var item = new CodeActionItem(new CodeAction("x"), provider);
        await item.Resolve(CancellationToken.None);
        item.Action.Edit.ShouldNotBeNull();
        await item.Resolve(CancellationToken.None);
        provider.ResolveCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(null, "refactor")]
    [InlineData("quickfix", "quickfix")]
    public void Command_args_from_user(string? kind, string expectedKind)
    {
        var args = CodeActionCommandArgs.FromUser(kind is null ? null : new CodeActionUserArgs(kind), CodeActionKind.Refactor, CodeActionAutoApply.Never);
        args.Kind.Value.ShouldBe(expectedKind);
        args.Apply.ShouldBe(CodeActionAutoApply.Never);
        args.Preferred.ShouldBeFalse();

        var fromDictionary = CodeActionCommandArgs.FromUser(
            new Dictionary<string, object?> { ["kind"] = "source", ["apply"] = "IfSingle", ["preferred"] = true },
            CodeActionKind.Refactor, CodeActionAutoApply.Never);
        fromDictionary.ShouldBe(new CodeActionCommandArgs(CodeActionKind.Source, CodeActionAutoApply.IfSingle, true));
    }

    private sealed class ResolvingProvider : ICodeActionProvider
    {
        public int ResolveCount { get; private set; }

        public Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token) =>
            Task.FromResult<CodeActionList?>(null);

        public bool SupportsResolveCodeAction => true;

        public Task<CodeAction?> ResolveCodeAction(CodeAction codeAction, CancellationToken token)
        {
            ResolveCount++;
            return Task.FromResult<CodeAction?>(codeAction with { Edit = new WorkspaceEdit([]) });
        }
    }
}
