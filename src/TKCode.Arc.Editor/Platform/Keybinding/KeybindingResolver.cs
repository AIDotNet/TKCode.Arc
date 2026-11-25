// Ported from vscode (src/vs/platform/keybinding/common/keybindingResolver.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - When clauses are the ported ContextKeyExpression (Platform/ContextKey), the context an IContext, and
//   LookupPrimaryKeybinding takes an IContextKeyService for its contextMatchesRules, as upstream.
// - ResolutionResult is a class with the kind and, for KbFound, the command; NoMatchingKb and MoreChordsNeeded are
//   shared instances as upstream's are.
// - applyAliases returns a tuple (Defaults, Overrides).
// - The log callback is an Action<string>; the chord list prints joined by commas as a JavaScript array does.

namespace TKCode.Arc.Editor.Platform;

internal enum ResultKind
{
    /// <summary>No keybinding found this sequence of chords</summary>
    NoMatchingKb,

    /// <summary>There're several keybindings that have the given sequence of chords as a prefix</summary>
    MoreChordsNeeded,

    /// <summary>A single keybinding found to be dispatched/invoked</summary>
    KbFound,
}

/// <summary>What a sequence of chords resolves to.</summary>
internal sealed class ResolutionResult
{
    public static readonly ResolutionResult NoMatchingKb = new(ResultKind.NoMatchingKb, null, null, false);

    public static readonly ResolutionResult MoreChordsNeeded = new(ResultKind.MoreChordsNeeded, null, null, false);

    private ResolutionResult(ResultKind kind, string? commandId, object? commandArgs, bool isBubble)
    {
        Kind = kind;
        CommandId = commandId;
        CommandArgs = commandArgs;
        IsBubble = isBubble;
    }

    public ResultKind Kind { get; }

    /// <summary>The command to run (<see cref="ResultKind.KbFound"/> only); null for a keybinding that runs nothing.</summary>
    public string? CommandId { get; }

    public object? CommandArgs { get; }

    public bool IsBubble { get; }

    public static ResolutionResult KbFound(string? commandId, object? commandArgs, bool isBubble) =>
        new(ResultKind.KbFound, commandId, commandArgs, isBubble);
}

/// <summary>
/// Stores mappings from keybindings to commands and from commands to keybindings. Given a sequence of chords,
/// <see cref="Resolve"/>s which keybinding it matches.
/// </summary>
internal sealed class KeybindingResolver
{
    private readonly Action<string> _log;
    private readonly IReadOnlyList<ResolvedKeybindingItem> _defaultKeybindings;
    private readonly IReadOnlyList<ResolvedKeybindingItem> _keybindings;
    private readonly Dictionary<string, bool> _defaultBoundCommands;
    private readonly Dictionary<string, List<ResolvedKeybindingItem>> _map; // 1st chord's keypress
    private readonly Dictionary<string, List<ResolvedKeybindingItem>> _lookupMap; // commandId

    /// <param name="defaultKeybindings">built-in and extension-provided keybindings</param>
    /// <param name="overrides">user's keybindings</param>
    /// <param name="log">receives the trace of each resolution</param>
    public KeybindingResolver(
        IReadOnlyList<ResolvedKeybindingItem> defaultKeybindings,
        IReadOnlyList<ResolvedKeybindingItem> overrides,
        Action<string> log)
    {
        _log = log;
        _defaultKeybindings = defaultKeybindings;

        _defaultBoundCommands = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var defaultKeybinding in defaultKeybindings)
        {
            var command = defaultKeybinding.Command;
            if (!string.IsNullOrEmpty(command) && command[0] != '-')
            {
                _defaultBoundCommands[command] = true;
            }
        }

        _map = new Dictionary<string, List<ResolvedKeybindingItem>>(StringComparer.Ordinal);
        _lookupMap = new Dictionary<string, List<ResolvedKeybindingItem>>(StringComparer.Ordinal);

        var all = new List<ResolvedKeybindingItem>(defaultKeybindings.Count + overrides.Count);
        all.AddRange(defaultKeybindings);
        all.AddRange(overrides);
        _keybindings = HandleRemovals(all);
        for (var i = 0; i < _keybindings.Count; i++)
        {
            var k = _keybindings[i];
            if (k.Chords.Count == 0)
            {
                // unbound
                continue;
            }

            // substitute with constants that are registered after startup - https://github.com/microsoft/vscode/issues/174218#issuecomment-1437972127
            var when = k.When?.SubstituteConstants();

            if (when is not null && when.Type == ContextKeyExprType.False)
            {
                // when condition is false
                continue;
            }

            AddKeyPress(k.Chords[0], k);
        }
    }

    private static bool IsTargetedForRemoval(ResolvedKeybindingItem defaultKb, IReadOnlyList<string>? keypress, ContextKeyExpression? when)
    {
        if (keypress is not null)
        {
            for (var i = 0; i < keypress.Count; i++)
            {
                // upstream compares with undefined past the end of the default's chords
                if (i >= defaultKb.Chords.Count || !string.Equals(keypress[i], defaultKb.Chords[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        // `true` means always, as does `undefined`
        // so we will treat `true` === `undefined`
        if (when is not null && when.Type != ContextKeyExprType.True)
        {
            if (defaultKb.When is null)
            {
                return false;
            }

            // Use implication instead of strict equality so that a removal still matches
            // when the default keybinding's when clause becomes more specific across
            // updates (e.g. "inChatInput" → "inChatInput && !withinEditSessionDiff").
            // See https://github.com/microsoft/vscode/issues/293802
            var defaultWhen = defaultKb.When.SubstituteConstants();
            var removalWhen = when.SubstituteConstants();
            if (!WhenIsEntirelyIncluded(defaultWhen, removalWhen))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Looks for rules containing "-commandId" and removes them.</summary>
    public static IReadOnlyList<ResolvedKeybindingItem> HandleRemovals(IReadOnlyList<ResolvedKeybindingItem> rules)
    {
        // Do a first pass and construct a hash-map for removals
        var removals = new Dictionary<string, List<ResolvedKeybindingItem>>(StringComparer.Ordinal);
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (!string.IsNullOrEmpty(rule.Command) && rule.Command[0] == '-')
            {
                var command = rule.Command[1..];
                if (!removals.TryGetValue(command, out var list))
                {
                    removals[command] = [rule];
                }
                else
                {
                    list.Add(rule);
                }
            }
        }

        if (removals.Count == 0)
        {
            // There are no removals
            return rules;
        }

        // Do a second pass and keep only non-removed keybindings
        var result = new List<ResolvedKeybindingItem>();
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];

            if (string.IsNullOrEmpty(rule.Command))
            {
                result.Add(rule);
                continue;
            }

            if (rule.Command[0] == '-')
            {
                continue;
            }

            if (!removals.TryGetValue(rule.Command, out var commandRemovals) || !rule.IsDefault)
            {
                result.Add(rule);
                continue;
            }

            var isRemoved = false;
            foreach (var commandRemoval in commandRemovals)
            {
                var when = commandRemoval.When;
                if (IsTargetedForRemoval(rule, commandRemoval.Chords, when))
                {
                    isRemoved = true;
                    break;
                }
            }

            if (!isRemoved)
            {
                result.Add(rule);
            }
        }

        return result;
    }

    /// <summary>
    /// Binds the command of each alias to the keybindings of its source, right after them, so that it outranks the source
    /// where the alias applies. The bindings are defaults of the alias's command, which the user can remove or rebind.
    /// Removing a keybinding of the source removes the default keybindings of the alias's command at that key, but not
    /// those inherited from the user's keybindings of the source, which may rebind that key.
    /// </summary>
    public static (IReadOnlyList<ResolvedKeybindingItem> Defaults, IReadOnlyList<ResolvedKeybindingItem> Overrides) ApplyAliases(
        IReadOnlyList<ResolvedKeybindingItem> defaults,
        IReadOnlyList<ResolvedKeybindingItem> overrides,
        IReadOnlyList<KeybindingAlias> aliases)
    {
        if (aliases.Count == 0)
        {
            return (defaults, overrides);
        }

        List<ResolvedKeybindingItem> Inherit(IReadOnlyList<ResolvedKeybindingItem> rules)
        {
            var result = new List<ResolvedKeybindingItem>();
            foreach (var rule in rules)
            {
                result.Add(rule);
                foreach (var alias in aliases)
                {
                    if (rule.Command == alias.Source)
                    {
                        result.Add(new ResolvedKeybindingItem(rule.ResolvedKeybinding, alias.Command, null, alias.When, true, rule.ExtensionId, rule.IsBuiltinExtension));
                    }
                }
            }

            return result;
        }

        // A removal's when clause describes where the source applies, so it removes the alias's keybindings wherever the alias applies.
        var sourceRemovals = new List<ResolvedKeybindingItem>();
        foreach (var rule in defaults.Concat(overrides))
        {
            if (aliases.Any(alias => rule.Command == "-" + alias.Source))
            {
                sourceRemovals.Add(rule);
            }
        }

        bool IsRemovedWithSource(ResolvedKeybindingItem rule) => aliases.Any(alias => rule.Command == alias.Command
            && sourceRemovals.Any(removal => removal.Command == "-" + alias.Source && IsTargetedForRemoval(rule, removal.Chords, null)));

        return (Inherit(defaults).Where(rule => !IsRemovedWithSource(rule)).ToList(), Inherit(overrides));
    }

    private void AddKeyPress(string keypress, ResolvedKeybindingItem item)
    {
        if (!_map.TryGetValue(keypress, out var conflicts))
        {
            // There is no conflict so far
            _map[keypress] = [item];
            AddToLookupMap(item);
            return;
        }

        for (var i = conflicts.Count - 1; i >= 0; i--)
        {
            var conflict = conflicts[i];

            if (conflict.Command == item.Command)
            {
                continue;
            }

            // Test if the shorter keybinding is a prefix of the longer one.
            // If the shorter keybinding is a prefix, it effectively will shadow the longer one and is considered a conflict.
            var isShorterKbPrefix = true;
            for (var j = 1; j < conflict.Chords.Count && j < item.Chords.Count; j++)
            {
                if (conflict.Chords[j] != item.Chords[j])
                {
                    // The ith step does not conflict
                    isShorterKbPrefix = false;
                    break;
                }
            }

            if (!isShorterKbPrefix)
            {
                continue;
            }

            if (WhenIsEntirelyIncluded(conflict.When, item.When))
            {
                // `item` completely overwrites `conflict`
                // Remove conflict from the lookupMap
                RemoveFromLookupMap(conflict);
            }
        }

        conflicts.Add(item);
        AddToLookupMap(item);
    }

    private void AddToLookupMap(ResolvedKeybindingItem item)
    {
        if (string.IsNullOrEmpty(item.Command))
        {
            return;
        }

        if (!_lookupMap.TryGetValue(item.Command, out var arr))
        {
            _lookupMap[item.Command] = [item];
        }
        else
        {
            arr.Add(item);
        }
    }

    private void RemoveFromLookupMap(ResolvedKeybindingItem item)
    {
        if (string.IsNullOrEmpty(item.Command))
        {
            return;
        }

        if (!_lookupMap.TryGetValue(item.Command, out var arr))
        {
            return;
        }

        for (var i = 0; i < arr.Count; i++)
        {
            if (ReferenceEquals(arr[i], item))
            {
                arr.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>Returns true if it is provable `a` implies `b`.</summary>
    public static bool WhenIsEntirelyIncluded(ContextKeyExpression? a, ContextKeyExpression? b)
    {
        if (b is null || b.Type == ContextKeyExprType.True)
        {
            return true;
        }

        if (a is null || a.Type == ContextKeyExprType.True)
        {
            return false;
        }

        return ContextKeyExpressions.Implies(a, b);
    }

    public IReadOnlyDictionary<string, bool> GetDefaultBoundCommands() => _defaultBoundCommands;

    public IReadOnlyList<ResolvedKeybindingItem> GetDefaultKeybindings() => _defaultKeybindings;

    public IReadOnlyList<ResolvedKeybindingItem> GetKeybindings() => _keybindings;

    public IReadOnlyList<ResolvedKeybindingItem> LookupKeybindings(string commandId)
    {
        if (!_lookupMap.TryGetValue(commandId, out var items) || items.Count == 0)
        {
            return [];
        }

        // Reverse to get the most specific item first
        var result = new ResolvedKeybindingItem[items.Count];
        var resultLen = 0;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            result[resultLen++] = items[i];
        }

        return result;
    }

    public ResolvedKeybindingItem? LookupPrimaryKeybinding(string commandId, IContextKeyService context, bool enforceContextCheck = false)
    {
        if (!_lookupMap.TryGetValue(commandId, out var items) || items.Count == 0)
        {
            return null;
        }

        if (items.Count == 1 && !enforceContextCheck)
        {
            return items[0];
        }

        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (context.ContextMatchesRules(item.When))
            {
                return item;
            }
        }

        if (enforceContextCheck)
        {
            return null;
        }

        return items[^1];
    }

    /// <summary>
    /// Looks up a keybinding triggered as a result of pressing a sequence of chords - `[...currentChords, keypress]`.
    /// Example: resolving 3 chords pressed sequentially - `cmd+k cmd+p cmd+i`: `currentChords = [ 'cmd+k' , 'cmd+p' ]`
    /// and `keypress = cmd+i` - last pressed chord.
    /// </summary>
    public ResolutionResult Resolve(IContext context, IReadOnlyList<string> currentChords, string keypress)
    {
        var pressedChords = new List<string>(currentChords.Count + 1);
        pressedChords.AddRange(currentChords);
        pressedChords.Add(keypress);

        _log($"| Resolving {string.Join(",", pressedChords)}");

        if (!_map.TryGetValue(pressedChords[0], out var kbCandidates))
        {
            // No bindings with such 0-th chord
            _log("\\ No keybinding entries.");
            return ResolutionResult.NoMatchingKb;
        }

        List<ResolvedKeybindingItem> lookupMap;

        if (pressedChords.Count < 2)
        {
            lookupMap = kbCandidates;
        }
        else
        {
            // Fetch all chord bindings for `currentChords`
            lookupMap = [];
            for (var i = 0; i < kbCandidates.Count; i++)
            {
                var candidate = kbCandidates[i];

                if (pressedChords.Count > candidate.Chords.Count)
                {
                    // # of pressed chords can't be less than # of chords in a keybinding to invoke
                    continue;
                }

                var prefixMatches = true;
                for (var j = 1; j < pressedChords.Count; j++)
                {
                    if (candidate.Chords[j] != pressedChords[j])
                    {
                        prefixMatches = false;
                        break;
                    }
                }

                if (prefixMatches)
                {
                    lookupMap.Add(candidate);
                }
            }
        }

        // check there's a keybinding with a matching when clause
        var result = FindCommand(context, lookupMap);
        if (result is null)
        {
            _log($"\\ From {lookupMap.Count} keybinding entries, no when clauses matched the context.");
            return ResolutionResult.NoMatchingKb;
        }

        // check we got all chords necessary to be sure a particular keybinding needs to be invoked
        if (pressedChords.Count < result.Chords.Count)
        {
            // The chord sequence is not complete
            _log($"\\ From {lookupMap.Count} keybinding entries, awaiting {result.Chords.Count - pressedChords.Count} more chord(s), when: {PrintWhenExplanation(result.When)}, source: {PrintSourceExplanation(result)}.");
            return ResolutionResult.MoreChordsNeeded;
        }

        _log($"\\ From {lookupMap.Count} keybinding entries, matched {result.Command}, when: {PrintWhenExplanation(result.When)}, source: {PrintSourceExplanation(result)}.");

        return ResolutionResult.KbFound(result.Command, result.CommandArgs, result.Bubble);
    }

    private static ResolvedKeybindingItem? FindCommand(IContext context, List<ResolvedKeybindingItem> matches)
    {
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var k = matches[i];

            if (!ContextMatchesRules(context, k.When))
            {
                continue;
            }

            return k;
        }

        return null;
    }

    private static bool ContextMatchesRules(IContext context, ContextKeyExpression? rules)
    {
        if (rules is null)
        {
            return true;
        }

        return rules.Evaluate(context);
    }

    private static string PrintWhenExplanation(ContextKeyExpression? when)
    {
        if (when is null)
        {
            return "no when condition";
        }

        return when.Serialize();
    }

    private static string PrintSourceExplanation(ResolvedKeybindingItem kb)
    {
        return kb.ExtensionId is { Length: > 0 }
            ? (kb.IsBuiltinExtension ? $"built-in extension {kb.ExtensionId}" : $"user extension {kb.ExtensionId}")
            : (kb.IsDefault ? "built-in" : "user");
    }
}
