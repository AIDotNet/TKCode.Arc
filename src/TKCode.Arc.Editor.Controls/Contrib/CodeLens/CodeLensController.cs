// Ported from vscode (src/vs/editor/contrib/codelens/browser/codelensController.ts: CodeLensContribution and the
// codelens.showLensesInCurrentLine action, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the debounce of providing and resolving is a fixed 250 ms (upstream's feature debounce service starts there and
//    adapts to the providers' speed); the timers are the editor's IdleScheduler;
//  - the cache is the host's CodeLensCache (CodeEditorActionHost.CodeLensCache: one per editor unless the host shares
//    one), so the rows show at once when the contribution is created again;
//  - the lens font is the editor's at 90 % (editor.codeLensFontFamily / codeLensFontSize are not ported); the row
//    height is upstream's: the lens font size times max(1.3, line height / font size);
//  - a click on a lens runs its command through CodeEditor.RunLanguageCommand (the editor's commands, else the host);
//  - showLensesInCurrentLine runs the line's only command, or does nothing when there are several (there is no quick
//    pick); the scroll state is not captured around changes (StableEditorScrollState).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CodeLens;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Shows the code lenses of the model in rows above their lines (upstream's CodeLensContribution).</summary>
internal sealed class CodeLensController : ICodeEditorContribution
{
    public const string Id = "css.editor.codeLens";

    public static readonly EditorContributionDescription Description = new(Id, editor => new CodeLensController(editor), EditorContributionInstantiation.AfterFirstRender);

    /// <summary>Show CodeLens Commands for Current Line.</summary>
    public static readonly EditorAction ShowLensesInCurrentLineAction = EditorAction.Create(
        "codelens.showLensesInCurrentLine", "Show CodeLens Commands for Current Line", EditorContextKeys.HasCodeLensProvider,
        static async (editor, _) =>
        {
            if (Get(editor) is not { } controller || editor.GetSelection() is not { } selection)
            {
                return;
            }

            var model = await controller.GetModel().ConfigureAwait(true);
            var commands = model?.Lenses
                .Where(lens => lens.Symbol.Command is not null && lens.Symbol.Range.StartLineNumber == selection.PositionLineNumber)
                .Select(lens => lens.Symbol.Command!)
                .ToList() ?? [];
            if (commands.Count == 1)
            {
                await editor.RunLanguageCommand(commands[0]).ConfigureAwait(true);
            }
        });

    private static readonly TimeSpan s_provideDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan s_resolveDelay = TimeSpan.FromMilliseconds(250);

    private readonly CodeEditor _editor;
    private readonly List<CodeLensWidget> _lenses = new();
    private readonly List<IDisposable> _localToDispose = new();
    private readonly List<CodeLensModel> _oldCodeLensModels = new();
    private CodeLensModel? _currentCodeLensModel;
    private CancellationTokenSource? _getCodeLensModelCts;
    private Task<CodeLensModel>? _getCodeLensModelTask;
    private CancellationTokenSource? _resolveCodeLensesCts;
    private Task? _resolveCodeLensesTask;
    private IDisposable? _provideTimer;
    private IDisposable? _resolveTimer;
    private bool _disposed;

    private CodeLensController(CodeEditor editor)
    {
        _editor = editor;
        editor.DidChangeModelLanguage += OnLanguageChanged;
        editor.LanguageFeatures.CodeLensProvider.DidChange += OnProvidersChanged;
        editor.DidChangeViewModel += OnViewModelChanged;
        OnModelChange();
    }

    public static CodeLensController? Get(CodeEditor editor) => editor.GetContribution<CodeLensController>(Id);

    /// <summary>The rows (for tests).</summary>
    internal IReadOnlyList<CodeLensWidget> Lenses => _lenses;

    /// <summary>The lens row height in pixels (upstream's _getLayoutInfo).</summary>
    internal double CodeLensHeight
    {
        get
        {
            var fontSize = _editor.FontSize;
            var lineHeight = _editor.View?.LineHeight ?? fontSize * 1.35;
            var lineHeightFactor = Math.Max(1.3, lineHeight / fontSize);
            var lensFontSize = Math.Floor(fontSize * 0.9);
            return Math.Floor(lensFontSize * lineHeightFactor);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        LocalDispose();
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.LanguageFeatures.CodeLensProvider.DidChange -= OnProvidersChanged;
        _editor.DidChangeViewModel -= OnViewModelChanged;
        _currentCodeLensModel?.Dispose();
    }

    /// <summary>The current lenses once the pending requests are done (upstream's getModel).</summary>
    public async Task<CodeLensModel?> GetModel()
    {
        try
        {
            if (_getCodeLensModelTask is { } provide)
            {
                await provide.ConfigureAwait(true);
            }

            if (_resolveCodeLensesTask is { } resolve)
            {
                await resolve.ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return _currentCodeLensModel is { IsDisposed: false } model ? model : null;
    }

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => OnModelChange();

    private void OnProvidersChanged(int count) => OnModelChange();

    // a new view model: a new font or size (upstream's onDidChangeConfiguration of the font: _updateLensStyle)
    private void OnViewModelChanged(Editor.ViewModel.ViewModel viewModel)
    {
        var height = CodeLensHeight;
        _editor.ChangeViewZones(accessor =>
        {
            foreach (var lens in _lenses)
            {
                lens.UpdateHeight(height, accessor);
            }
        });
    }

    private void LocalDispose()
    {
        _getCodeLensModelCts?.Cancel();
        _getCodeLensModelCts = null;
        _getCodeLensModelTask = null;
        _resolveCodeLensesCts?.Cancel();
        _resolveCodeLensesCts = null;
        _resolveCodeLensesTask = null;
        _provideTimer?.Dispose();
        _provideTimer = null;
        _resolveTimer?.Dispose();
        _resolveTimer = null;
        for (var i = _localToDispose.Count - 1; i >= 0; i--)
        {
            _localToDispose[i].Dispose();
        }

        _localToDispose.Clear();
        foreach (var old in _oldCodeLensModels)
        {
            old.Dispose();
        }

        _oldCodeLensModels.Clear();
        _currentCodeLensModel?.Dispose();
    }

    private void OnModelChange()
    {
        LocalDispose();
        if (_disposed)
        {
            return;
        }

        var model = _editor.Model;
        var cache = _editor.ActionHost.CodeLensCache;
        var cachedLenses = cache.Get(model);
        if (cachedLenses is not null)
        {
            RenderCodeLensSymbols(cachedLenses);
        }

        var registry = _editor.LanguageFeatures.CodeLensProvider;
        if (!registry.Has(model))
        {
            // no provider: the cached lenses expire after 30 seconds
            if (cachedLenses is not null)
            {
                var timer = CodeEditor.IdleScheduler.RunAfter(TimeSpan.FromSeconds(30), () =>
                {
                    if (ReferenceEquals(cache.Get(model), cachedLenses))
                    {
                        cache.Delete(model);
                        OnModelChange();
                    }
                });
                _localToDispose.Add(timer);
            }
            else
            {
                DisposeAllLenses();
            }

            return;
        }

        foreach (var provider in registry.All(model))
        {
            void OnProviderChanged(ICodeLensProvider _) => ScheduleProvide();
            provider.DidChange += OnProviderChanged;
            _localToDispose.Add(new Unsubscribe(() => provider.DidChange -= OnProviderChanged));
        }

        void OnContent(ModelContentChangedEvent e) => OnModelContentChanged();
        void OnFocus(bool focused)
        {
            if (focused)
            {
                ScheduleProvide();
            }
            else
            {
                _provideTimer?.Dispose();
                _provideTimer = null;
            }
        }

        void OnScroll(ScrollChangedEvent e)
        {
            if (e.ScrollTopChanged && _lenses.Count > 0)
            {
                ResolveCodeLensesInViewportSoon();
            }
        }

        void OnLayout(Avalonia.Size size) => ResolveCodeLensesInViewportSoon();

        _editor.DidChangeModelContent += OnContent;
        _editor.DidChangeTextFocus += OnFocus;
        _editor.DidScrollChange += OnScroll;
        _editor.DidLayoutChange += OnLayout;
        _localToDispose.Add(new Unsubscribe(() =>
        {
            _editor.DidChangeModelContent -= OnContent;
            _editor.DidChangeTextFocus -= OnFocus;
            _editor.DidScrollChange -= OnScroll;
            _editor.DidLayoutChange -= OnLayout;
        }));
        _localToDispose.Add(new Unsubscribe(DisposeAllLenses));

        ScheduleProvide();
    }

    private void ScheduleProvide()
    {
        _provideTimer?.Dispose();
        _provideTimer = CodeEditor.IdleScheduler.RunAfter(s_provideDelay, () =>
        {
            _provideTimer = null;
            _ = Provide();
        });
    }

    // the scheduler's work: ask the providers, cache and render the result
    private async Task Provide()
    {
        var model = _editor.Model;
        _getCodeLensModelCts?.Cancel();
        var cts = new CancellationTokenSource();
        _getCodeLensModelCts = cts;
        var task = CodeLensService.GetCodeLensModel(_editor.LanguageFeatures.CodeLensProvider, model, cts.Token);
        _getCodeLensModelTask = task;
        CodeLensModel result;
        try
        {
            result = await task.ConfigureAwait(true);
        }
        catch (Exception error)
        {
            UnexpectedErrors.Report(error);
            return;
        }

        if (cts.IsCancellationRequested || _disposed)
        {
            result.Dispose();
            return;
        }

        if (_currentCodeLensModel is { } current)
        {
            _oldCodeLensModels.Add(current);
        }

        _currentCodeLensModel = result;

        // cache the model to reduce flicker
        _editor.ActionHost.CodeLensCache.Put(model, result);
        RenderCodeLensSymbols(result);
        ResolveCodeLensesInViewportSoon();
    }

    private void OnModelContentChanged()
    {
        _editor.ChangeViewZones(accessor =>
        {
            var toDispose = new List<CodeLensWidget>();
            var lastLensLineNumber = -1;
            foreach (var lens in _lenses)
            {
                if (!lens.IsValid() || lastLensLineNumber == lens.GetLineNumber())
                {
                    // invalid: the lens range collapsed; or two lenses on the same line
                    toDispose.Add(lens);
                }
                else
                {
                    lens.Update(accessor);
                    lastLensLineNumber = lens.GetLineNumber();
                }
            }

            foreach (var lens in toDispose)
            {
                lens.Dispose(accessor);
                _lenses.Remove(lens);
            }
        });

        // ask for all lenses again; cancel pending and active resolve requests
        ScheduleProvide();
        _resolveTimer?.Dispose();
        _resolveTimer = null;
        _resolveCodeLensesCts?.Cancel();
        _resolveCodeLensesCts = null;
        _resolveCodeLensesTask = null;
    }

    private void DisposeAllLenses()
    {
        if (_lenses.Count == 0)
        {
            return;
        }

        _editor.ChangeViewZones(accessor =>
        {
            foreach (var lens in _lenses)
            {
                lens.Dispose(accessor);
            }
        });
        _lenses.Clear();
    }

    // upstream's _renderCodeLensSymbols: one row per line, updated in place where a row already is
    private void RenderCodeLensSymbols(CodeLensModel symbols)
    {
        var maxLineNumber = _editor.Model.GetLineCount();
        var groups = new List<List<CodeLensItem>>();
        List<CodeLensItem>? lastGroup = null;
        foreach (var symbol in symbols.Lenses)
        {
            var line = symbol.Symbol.Range.StartLineNumber;
            if (line < 1 || line > maxLineNumber)
            {
                // invalid code lens
                continue;
            }

            if (lastGroup is not null && lastGroup[^1].Symbol.Range.StartLineNumber == line)
            {
                // on the same line as the previous one
                lastGroup.Add(symbol);
            }
            else
            {
                lastGroup = [symbol];
                groups.Add(lastGroup);
            }
        }

        if (groups.Count == 0 && _lenses.Count == 0)
        {
            return;
        }

        var height = CodeLensHeight;
        _editor.ChangeViewZones(accessor =>
        {
            var codeLensIndex = 0;
            var groupsIndex = 0;
            while (groupsIndex < groups.Count && codeLensIndex < _lenses.Count)
            {
                var symbolsLineNumber = groups[groupsIndex][0].Symbol.Range.StartLineNumber;
                var codeLensLineNumber = _lenses[codeLensIndex].GetLineNumber();
                if (codeLensLineNumber < symbolsLineNumber)
                {
                    _lenses[codeLensIndex].Dispose(accessor);
                    _lenses.RemoveAt(codeLensIndex);
                }
                else if (codeLensLineNumber == symbolsLineNumber)
                {
                    _lenses[codeLensIndex].UpdateCodeLensSymbols(groups[groupsIndex]);
                    groupsIndex++;
                    codeLensIndex++;
                }
                else
                {
                    _lenses.Insert(codeLensIndex, CreateWidget(groups[groupsIndex], accessor, height));
                    codeLensIndex++;
                    groupsIndex++;
                }
            }

            // delete extra code lenses
            while (codeLensIndex < _lenses.Count)
            {
                _lenses[codeLensIndex].Dispose(accessor);
                _lenses.RemoveAt(codeLensIndex);
            }

            // create extra symbols
            while (groupsIndex < groups.Count)
            {
                _lenses.Add(CreateWidget(groups[groupsIndex], accessor, height));
                groupsIndex++;
            }
        });
    }

    private CodeLensWidget CreateWidget(List<CodeLensItem> group, IViewZoneChangeAccessor accessor, double height) =>
        new(group, _editor, accessor, height, ResolveCodeLensesInViewportSoon, command => _ = RunCommand(command));

    private async Task RunCommand(LanguageCommand command)
    {
        try
        {
            await _editor.RunLanguageCommand(command).ConfigureAwait(true);
        }
        catch (Exception error)
        {
            if (_editor.GetPosition() is { } position)
            {
                MessageController.Get(_editor)?.ShowMessage(error.Message, position);
            }
        }
    }

    private void ResolveCodeLensesInViewportSoon()
    {
        if (_disposed)
        {
            return;
        }

        _resolveTimer?.Dispose();
        _resolveTimer = CodeEditor.IdleScheduler.RunAfter(s_resolveDelay, () =>
        {
            _resolveTimer = null;
            ResolveCodeLensesInViewport();
        });
    }

    // upstream's _resolveCodeLensesInViewport: resolve the commands of the visible rows that have none
    private void ResolveCodeLensesInViewport()
    {
        _resolveCodeLensesCts?.Cancel();
        _resolveCodeLensesCts = null;
        var model = _editor.Model;

        var toResolve = new List<IReadOnlyList<CodeLensItem>>();
        var lenses = new List<CodeLensWidget>();
        foreach (var lens in _lenses)
        {
            if (lens.ComputeIfNecessary(model) is { } request)
            {
                toResolve.Add(request);
                lenses.Add(lens);
            }
        }

        if (toResolve.Count == 0)
        {
            DisposeOldModels();
            return;
        }

        var cts = new CancellationTokenSource();
        _resolveCodeLensesCts = cts;
        _resolveCodeLensesTask = Resolve(model, toResolve, lenses, cts);
    }

    private async Task Resolve(TextModel model, List<IReadOnlyList<CodeLensItem>> toResolve, List<CodeLensWidget> lenses, CancellationTokenSource cts)
    {
        var token = cts.Token;
        var rows = toResolve.Select(async (request, i) =>
        {
            var resolvedSymbols = new Editor.Languages.CodeLens?[request.Count];
            var tasks = request.Select(async (item, j) =>
            {
                if (item.Symbol.Command is null && item.Provider.SupportsResolveCodeLens)
                {
                    try
                    {
                        resolvedSymbols[j] = await item.Provider.ResolveCodeLens(model, item.Symbol, token).ConfigureAwait(true);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        UnexpectedErrors.Report(error);
                    }
                }
                else
                {
                    resolvedSymbols[j] = item.Symbol;
                }
            });
            await Task.WhenAll(tasks).ConfigureAwait(true);
            if (!token.IsCancellationRequested && !lenses[i].IsDisposed)
            {
                lenses[i].UpdateCommands(resolvedSymbols);
            }
        });

        try
        {
            await Task.WhenAll(rows).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        // update the cache with the resolved items, dispose old models once the rows show the current one
        if (_currentCodeLensModel is { } current)
        {
            _editor.ActionHost.CodeLensCache.Put(model, current);
        }

        DisposeOldModels();
        if (ReferenceEquals(_resolveCodeLensesCts, cts))
        {
            _resolveCodeLensesTask = null;
        }
    }

    private void DisposeOldModels()
    {
        foreach (var old in _oldCodeLensModels)
        {
            old.Dispose();
        }

        _oldCodeLensModels.Clear();
    }

    private sealed class Unsubscribe(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
