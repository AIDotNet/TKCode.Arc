// Ported from vscode (src/vs/editor/browser/widget/codeEditor/codeEditorContributions.ts and the parts of
// codeEditorWidget.ts that register the actions and dispatch trigger(), commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the contributions are created once the editor has a view model (upstream: in the widget's constructor) and
//    disposed when the editor leaves the visual tree; coming back creates them again (the editor has no Dispose);
//  - "when the window is idle" is a Background-priority dispatcher post (upstream's runWhenWindowIdle), with a 5 s
//    timer for Eventually as upstream;
//  - the editor commands and actions are registered into the editor's own registries (CodeEditor.Commands.cs) from
//    the lists the constructor gets, by default CodeEditorContributions;
//  - trigger() handles the type, paste, cut and composition handlers, then an action, then an editor command, then any
//    registered command; onWillTriggerEditorOperationEvent and begin/endUpdate are not ported.

using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;
using PlatformCommand = TKCode.Arc.Editor.Platform.Command;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    private readonly IReadOnlyList<EditorContributionDescription> _contributionDescriptions;
    private readonly Dictionary<string, CodeEditorAction> _actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EditorCommand> _editorCommands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EditorContributionDescription> _pendingContributions = new(StringComparer.Ordinal);
    private readonly List<(string Id, ICodeEditorContribution Instance)> _contributionInstances = new();
    private readonly bool[] _finishedInstantiation = new bool[4];
    private readonly List<IDisposable> _idleCallbacks = new();
    private bool _contributionsInitialized;

    /// <summary>Whether the editor's contributions exist (between the first view model and leaving the visual tree).</summary>
    internal bool HasContributions => _contributionsInitialized;

    /// <summary>
    /// The contribution <paramref name="id"/>, created now if it is still pending (upstream's getContribution);
    /// null when the editor has none of that id or its contributions do not exist yet.
    /// </summary>
    internal T? GetContribution<T>(string id)
        where T : class, ICodeEditorContribution
    {
        InstantiateById(id);
        foreach (var (instanceId, instance) in _contributionInstances)
        {
            if (instanceId == id)
            {
                return instance as T;
            }
        }

        return null;
    }

    /// <summary>The ids of the contributions that exist now, in creation order (for tests).</summary>
    internal IReadOnlyList<string> GetContributionIds() => [.. _contributionInstances.Select(static c => c.Id)];

    /// <summary>The action <paramref name="id"/>, or null (upstream's getAction).</summary>
    internal CodeEditorAction? GetAction(string id) => _actions.GetValueOrDefault(id);

    /// <summary>Every action of the editor (upstream's getActions).</summary>
    internal IReadOnlyList<CodeEditorAction> GetActions() => [.. _actions.Values];

    /// <summary>The actions whose precondition holds now (upstream's getSupportedActions).</summary>
    internal IReadOnlyList<CodeEditorAction> GetSupportedActions() => [.. _actions.Values.Where(static a => a.IsSupported())];

    /// <summary>
    /// Runs a handler as the user would (upstream's trigger): <see cref="EditorHandler"/>'s type, paste, cut and
    /// composition handlers, else the action, the editor command or the registered command of that id.
    /// <paramref name="source"/> says where it came from (<c>keyboard</c>, <c>mouse</c>, <c>api</c>, ...).
    /// </summary>
    internal void Trigger(string? source, string handlerId, object? payload)
    {
        var viewModel = ViewModel;
        switch (handlerId)
        {
            case EditorHandler.CompositionStart:
                viewModel?.StartComposition();
                return;
            case EditorHandler.CompositionEnd:
                viewModel?.EndComposition(source);
                return;
            case EditorHandler.Type:
                if (viewModel is not null && payload is TypePayload { Text.Length: > 0 } type)
                {
                    TypeText(source, type.Text);
                }

                return;
            case EditorHandler.ReplacePreviousChar:
                if (payload is ReplacePreviousCharPayload replace)
                {
                    viewModel?.CompositionType(replace.Text, replace.ReplaceCharCnt, 0, 0, source);
                }

                return;
            case EditorHandler.CompositionType:
                if (payload is CompositionTypePayload composition)
                {
                    viewModel?.CompositionType(composition.Text, composition.ReplacePrevCharCnt, composition.ReplaceNextCharCnt, composition.PositionDelta, source);
                }

                return;
            case EditorHandler.Paste:
                if (viewModel is not null && payload is PastePayload paste)
                {
                    PasteText(source, paste.Text, paste.PasteOnNewLine, paste.MulticursorText, paste.IsBlock);
                }

                return;
            case EditorHandler.Cut:
                viewModel?.Cut(source);
                return;
        }

        if (GetAction(handlerId) is { } action)
        {
            ReportFailure(action.Run(payload));
            return;
        }

        if (_editorCommands.TryGetValue(handlerId, out var command))
        {
            ReportFailure(RunEditorCommand(command, payload));
            return;
        }

        ReportFailure(ExecuteCommand(handlerId, payload));
    }

    /// <summary>Types text as the keyboard does, raising <see cref="WillType"/> and <see cref="DidType"/> for the keyboard source.</summary>
    internal void TypeText(string? source, string text)
    {
        if (ViewModel is not { } viewModel || text.Length == 0)
        {
            return;
        }

        var keyboard = source == "keyboard";
        if (keyboard)
        {
            _onWillType.Fire(text);
        }

        viewModel.Type(text, source);
        if (keyboard)
        {
            _onDidType.Fire(text);
        }
    }

    /// <summary>Pastes text, raising <see cref="DidPaste"/> for the keyboard source with the range the text took.</summary>
    internal void PasteText(string? source, string text, bool pasteOnNewLine, IReadOnlyList<string>? multicursorText, bool isBlock = false)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var start = viewModel.GetSelection().Range.Start;
        viewModel.Paste(text, pasteOnNewLine, multicursorText, source, isBlock);
        var end = viewModel.GetSelection().Range.Start;
        if (source == "keyboard")
        {
            _onDidPaste.Fire(new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column));
        }
    }

    // Command.register for each editor command: the command (precondition checked when it runs) and its keybindings
    // (their when clause and the precondition). Actions are also kept by id for GetAction.
    private void RegisterEditorCommands(IReadOnlyList<EditorCommand> commands)
    {
        foreach (var command in commands)
        {
            if (_editorCommands.ContainsKey(command.Id) || CommandsRegistry.GetCommand(command.Id) is not null)
            {
                UnexpectedErrors.Report(new InvalidOperationException($"Cannot have two actions with the same id {command.Id}"));
                continue;
            }

            _editorCommands[command.Id] = command;
            var captured = command;
            CommandsRegistry.RegisterCommand(new PlatformCommand(
                command.Id,
                (_, args) => RunEditorCommand(captured, args.Length > 0 ? args[0] : null)));

            foreach (var kb in command.Keybindings)
            {
                var when = command.Precondition is null ? kb.When : kb.When is null ? command.Precondition : ContextKeyExpr.And(kb.When, command.Precondition);
                KeybindingsRegistry.RegisterKeybindingRule(new KeybindingRule
                {
                    Id = command.Id,
                    Weight = kb.Weight,
                    Args = kb.Args,
                    When = when,
                    Primary = kb.Primary,
                    Secondary = kb.Secondary,
                    Win = kb.Win,
                    Linux = kb.Linux,
                    Mac = kb.Mac,
                });
            }

            if (command is EditorAction action)
            {
                _actions[action.Id] = new CodeEditorAction(this, action);
            }
        }

        InvalidateKeybindings();
    }

    // EditorCommand.runEditorCommand: nothing when the precondition does not hold.
    private Task RunEditorCommand(EditorCommand command, object? args)
    {
        if (!ContextKeyService.ContextMatchesRules(command.Precondition))
        {
            // precondition does not hold
            return Task.CompletedTask;
        }

        return command.RunEditorCommand(this, args);
    }

    private static void ReportFailure(Task task)
    {
        if (task.IsCompleted)
        {
            if (task.Exception is { } completedError)
            {
                UnexpectedErrors.Report(completedError.GetBaseException());
            }

            return;
        }

        task.ContinueWith(
            static t => UnexpectedErrors.Report(t.Exception!.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    // ---- contributions (upstream's CodeEditorContributions)

    /// <summary>Creates the eager contributions and schedules the others; once, after the first view model.</summary>
    private void InitializeContributions()
    {
        if (_contributionsInitialized || ViewModel is null)
        {
            return;
        }

        _contributionsInitialized = true;
        Array.Clear(_finishedInstantiation);
        _pendingContributions.Clear();
        foreach (var desc in _contributionDescriptions)
        {
            if (!_pendingContributions.TryAdd(desc.Id, desc))
            {
                UnexpectedErrors.Report(new InvalidOperationException($"Cannot have two contributions with the same id {desc.Id}"));
            }
        }

        InstantiateSome(EditorContributionInstantiation.Eager);

        // AfterFirstRender and BeforeFirstInteraction: when idle (BeforeFirstInteraction also before the first input)
        _idleCallbacks.Add(PostIdle(() => InstantiateSome(EditorContributionInstantiation.AfterFirstRender)));
        _idleCallbacks.Add(PostIdle(() => InstantiateSome(EditorContributionInstantiation.BeforeFirstInteraction)));

        // Eventually: when idle, at the latest after 5 s
        _idleCallbacks.Add(PostIdle(() => InstantiateSome(EditorContributionInstantiation.Eventually)));
        _idleCallbacks.Add(PostAfter(TimeSpan.FromSeconds(5), () => InstantiateSome(EditorContributionInstantiation.Eventually)));
    }

    /// <summary>Disposes the contributions, newest first (when the editor leaves the visual tree).</summary>
    private void DisposeContributions()
    {
        foreach (var callback in _idleCallbacks)
        {
            callback.Dispose();
        }

        _idleCallbacks.Clear();
        for (var i = _contributionInstances.Count - 1; i >= 0; i--)
        {
            try
            {
                _contributionInstances[i].Instance.Dispose();
            }
            catch (Exception e)
            {
                UnexpectedErrors.Report(e);
            }
        }

        _contributionInstances.Clear();
        _pendingContributions.Clear();
        _contributionsInitialized = false;
    }

    /// <summary>Called before every key and pointer event (upstream's onBeforeInteractionEvent).</summary>
    internal void OnBeforeInteractionEvent() => InstantiateSome(EditorContributionInstantiation.BeforeFirstInteraction);

    private void InstantiateSome(EditorContributionInstantiation instantiation)
    {
        if (!_contributionsInitialized || _finishedInstantiation[(int)instantiation])
        {
            // already done with this instantiation!
            return;
        }

        _finishedInstantiation[(int)instantiation] = true;

        var contribs = _pendingContributions.Values.Where(desc => desc.Instantiation == instantiation).ToList();
        foreach (var contrib in _contributionDescriptions)
        {
            if (contribs.Contains(contrib))
            {
                InstantiateById(contrib.Id);
            }
        }
    }

    private void InstantiateById(string id)
    {
        if (!_pendingContributions.Remove(id, out var desc))
        {
            return;
        }

        try
        {
            var instance = desc.Create(this);
            _contributionInstances.Add((desc.Id, instance));
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }
}
