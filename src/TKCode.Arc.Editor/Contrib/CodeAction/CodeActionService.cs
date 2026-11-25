// Ported from vscode (src/vs/editor/contrib/codeAction/browser/codeAction.ts: the command ids, getCodeActions,
// getCodeActionProviders, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - getCodeActions returns a CodeActionSet that disposes the providers' lists (upstream's DisposableStore);
//  - the progress report after 1250 ms, the providers' documentation and _getAdditionalMenuItems are not ported;
//  - applyCodeAction (bulk edit service, command service, notifications) is in the controls package
//    (CodeActionController), and the _executeCodeActionProvider API command is not ported (there is no command
//    registry of API commands here);
//  - a provider that throws is reported to UnexpectedErrors and returns nothing, as upstream; a cancellation ends the
//    request with OperationCanceledException.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.CodeAction;

/// <summary>Asks the code action providers of a model (upstream's codeAction.ts).</summary>
internal static class CodeActionService
{
    public const string CodeActionCommandId = "editor.action.codeAction";
    public const string QuickFixCommandId = "editor.action.quickFix";
    public const string AutoFixCommandId = "editor.action.autoFix";
    public const string RefactorCommandId = "editor.action.refactor";
    public const string RefactorPreviewCommandId = "editor.action.refactor.preview";
    public const string SourceActionCommandId = "editor.action.sourceAction";
    public const string OrganizeImportsCommandId = "editor.action.organizeImports";
    public const string FixAllCommandId = "editor.action.fixAll";

    /// <summary>
    /// The code actions for <paramref name="rangeOrSelection"/> of every provider that may return actions the trigger's
    /// filter wants, filtered and sorted (upstream's getCodeActions). The request is cancelled when the model's content
    /// changes or the providers change.
    /// </summary>
    public static async Task<CodeActionSet> GetCodeActions(
        LanguageFeatureRegistry<ICodeActionProvider> registry,
        TextModel model,
        TextRange rangeOrSelection,
        CodeActionTrigger trigger,
        CancellationToken token)
    {
        var filter = trigger.Filter ?? new CodeActionFilter();
        var notebookFilter = filter with { Excludes = [.. filter.Excludes ?? [], CodeActionKind.Notebook] };
        var codeActionContext = new CodeActionContext(trigger.Type) { Only = filter.Include?.Value };

        using var cts = new TextModelCancellationTokenSource(model, token);

        // if the trigger is auto (autosave, lightbulb, ...), notebook code actions are excluded
        var excludeNotebookCodeActions = trigger.Type == CodeActionTriggerType.Auto;
        var providers = GetCodeActionProviders(registry, model, excludeNotebookCodeActions ? notebookFilter : filter);

        var lists = new List<CodeActionList>();
        void OnRegistryChanged(int _)
        {
            var newProviders = registry.All(model);
            if (!newProviders.SequenceEqual(providers))
            {
                cts.Cancel();
            }
        }

        registry.DidChange += OnRegistryChanged;
        try
        {
            var tasks = providers.Select(async provider =>
            {
                try
                {
                    var provided = await provider.ProvideCodeActions(model, rangeOrSelection, codeActionContext, cts.Token).ConfigureAwait(true);
                    if (cts.Token.IsCancellationRequested)
                    {
                        provided?.Dispose?.Invoke();
                        return (IReadOnlyList<CodeActionItem>)[];
                    }

                    if (provided is null)
                    {
                        return [];
                    }

                    lock (lists)
                    {
                        lists.Add(provided);
                    }

                    return provided.Actions
                        .Where(action => action is not null && CodeActionFilters.FiltersAction(filter, action))
                        .Select(action => new CodeActionItem(action, provider))
                        .ToList();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    UnexpectedErrors.Report(error);
                    return [];
                }
            }).ToList();

            IReadOnlyList<CodeActionItem>[] results;
            try
            {
                results = await Task.WhenAll(tasks).ConfigureAwait(true);
            }
            catch
            {
                DisposeLists(lists);
                throw;
            }

            return CodeActionSet.Create(results.SelectMany(static x => x), [], () => DisposeLists(lists));
        }
        finally
        {
            registry.DidChange -= OnRegistryChanged;
        }
    }

    private static IReadOnlyList<ICodeActionProvider> GetCodeActionProviders(
        LanguageFeatureRegistry<ICodeActionProvider> registry,
        TextModel model,
        CodeActionFilter filter) =>
        registry.All(model)
            // don't include providers that will not return code actions of interest
            .Where(provider => provider.ProvidedCodeActionKinds is not { } kinds
                || kinds.Any(kind => CodeActionFilters.MayIncludeActionsOfKind(filter, new HierarchicalKind(kind))))
            .ToList();

    private static void DisposeLists(List<CodeActionList> lists)
    {
        foreach (var list in lists)
        {
            try
            {
                list.Dispose?.Invoke();
            }
            catch (Exception error)
            {
                UnexpectedErrors.Report(error);
            }
        }

        lists.Clear();
    }
}
