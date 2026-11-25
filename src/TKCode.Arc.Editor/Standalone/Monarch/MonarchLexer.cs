// Ported from vscode (src/vs/editor/standalone/common/monarch/monarchLexer.ts, commit b57d5c9e), MIT license.
//
// Differences:
// - The stack element and line state factories are per tokenizer, not upstream's static instances (AGENTS.md: no static
//   mutable state shared between buffers); they are thread-safe, as tokenizers may run off the UI thread. States from two
//   tokenizers still compare by their stacks, as upstream's equals does.
// - The language service and the tokenization registry are given to the tokenizer (IMonarchLanguageService, a
//   TokenizationRegistry instance); maxTokenizationLineLength is a constructor value (upstream: the configuration
//   service's editor.maxTokenizationLineLength, 20000 by default).
// - A rule matches with Regex.Match(line, pos, length) on the slice from pos (see MonarchCompile.cs), not on a copy of
//   the rest of the line.
// - getLoadStatus / embeddedLoaded are left out: embedded languages are resolved through the registry's factories
//   (GetOrCreateAsync), whose change event retokenizes the model as upstream's does.

using System.Collections.Concurrent;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Standalone.Monarch;

/// <summary>What the Monarch tokenizer needs from the language service.</summary>
internal interface IMonarchLanguageService
{
    ILanguageIdCodec LanguageIdCodec { get; }

    string? GetLanguageIdByLanguageName(string languageName);

    string? GetLanguageIdByMimeType(string mimeType);

    bool IsRegisteredLanguageId(string languageId);

    /// <summary>A language is about to be used (upstream: requestBasicLanguageFeatures, which loads its configuration).</summary>
    void RequestBasicLanguageFeatures(string languageId);
}

/// <summary>An element of the state stack.</summary>
internal sealed class MonarchStackElement
{
    private readonly MonarchStackFactories _factories;

    internal MonarchStackElement(MonarchStackFactories factories, MonarchStackElement? parent, string state)
    {
        _factories = factories;
        Parent = parent;
        State = state;
        Depth = (parent?.Depth ?? 0) + 1;
    }

    public MonarchStackElement? Parent { get; }

    public string State { get; }

    public int Depth { get; }

    public static string GetStackElementId(MonarchStackElement? element)
    {
        var result = string.Empty;
        while (element is not null)
        {
            if (result.Length > 0)
            {
                result += "|";
            }

            result += element.State;
            element = element.Parent;
        }

        return result;
    }

    private static bool EqualsCore(MonarchStackElement? a, MonarchStackElement? b)
    {
        while (a is not null && b is not null)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a.State != b.State)
            {
                return false;
            }

            a = a.Parent;
            b = b.Parent;
        }

        return a is null && b is null;
    }

    public bool Equals(MonarchStackElement other) => EqualsCore(this, other);

    public MonarchStackElement Push(string state) => _factories.CreateStackElement(this, state);

    public MonarchStackElement? Pop() => Parent;

    public MonarchStackElement PopAll()
    {
        var result = this;
        while (result.Parent is not null)
        {
            result = result.Parent;
        }

        return result;
    }

    public MonarchStackElement SwitchTo(string state) => _factories.CreateStackElement(Parent, state);
}

/// <summary>The state of an embedded language.</summary>
internal sealed class EmbeddedLanguageData(string languageId, IState state)
{
    public string LanguageId { get; } = languageId;

    public IState State { get; } = state;

    public bool Equals(EmbeddedLanguageData other) => LanguageId == other.LanguageId && State.Equals(other.State);

    public EmbeddedLanguageData Clone()
    {
        var stateClone = State.Clone();
        // save an object
        if (ReferenceEquals(stateClone, State))
        {
            return this;
        }

        return new EmbeddedLanguageData(LanguageId, State);
    }
}

/// <summary>A tokenizer's line state: the stack and the embedded language's state.</summary>
internal sealed class MonarchLineState : IState
{
    private readonly MonarchStackFactories _factories;

    internal MonarchLineState(MonarchStackFactories factories, MonarchStackElement stack, EmbeddedLanguageData? embeddedLanguageData)
    {
        _factories = factories;
        Stack = stack;
        EmbeddedLanguageData = embeddedLanguageData;
    }

    public MonarchStackElement Stack { get; }

    public EmbeddedLanguageData? EmbeddedLanguageData { get; }

    public IState Clone()
    {
        var embeddedlanguageDataClone = EmbeddedLanguageData?.Clone();
        // save an object
        if (ReferenceEquals(embeddedlanguageDataClone, EmbeddedLanguageData))
        {
            return this;
        }

        return _factories.CreateLineState(Stack, EmbeddedLanguageData);
    }

    public bool Equals(IState other)
    {
        if (other is not MonarchLineState otherState)
        {
            return false;
        }

        if (!Stack.Equals(otherState.Stack))
        {
            return false;
        }

        if (EmbeddedLanguageData is null && otherState.EmbeddedLanguageData is null)
        {
            return true;
        }

        if (EmbeddedLanguageData is null || otherState.EmbeddedLanguageData is null)
        {
            return false;
        }

        return EmbeddedLanguageData.Equals(otherState.EmbeddedLanguageData);
    }
}

/// <summary>Reuses the same stack elements and line states up to a certain depth (upstream's two factories).</summary>
internal sealed class MonarchStackFactories
{
    private const int CacheStackDepth = 5;

    private readonly ConcurrentDictionary<string, MonarchStackElement> _stackEntries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MonarchLineState> _lineStateEntries = new(StringComparer.Ordinal);

    public MonarchStackElement CreateStackElement(MonarchStackElement? parent, string state)
    {
        if (parent is not null && parent.Depth >= CacheStackDepth)
        {
            // no caching above a certain depth
            return new MonarchStackElement(this, parent, state);
        }

        var stackElementId = MonarchStackElement.GetStackElementId(parent);
        if (stackElementId.Length > 0)
        {
            stackElementId += "|";
        }

        stackElementId += state;
        return _stackEntries.GetOrAdd(stackElementId, _ => new MonarchStackElement(this, parent, state));
    }

    public MonarchLineState CreateLineState(MonarchStackElement stack, EmbeddedLanguageData? embeddedLanguageData)
    {
        if (embeddedLanguageData is not null)
        {
            // no caching when embedding
            return new MonarchLineState(this, stack, embeddedLanguageData);
        }

        if (stack.Depth >= CacheStackDepth)
        {
            // no caching above a certain depth
            return new MonarchLineState(this, stack, embeddedLanguageData);
        }

        var stackElementId = MonarchStackElement.GetStackElementId(stack);
        return _lineStateEntries.GetOrAdd(stackElementId, _ => new MonarchLineState(this, stack, null));
    }
}

/// <summary>Collects the tokens of a line, as strings or encoded.</summary>
internal interface IMonarchTokensCollector
{
    void EnterLanguage(string languageId);

    void Emit(int startOffset, string type);

    IState NestedLanguageTokenize(string embeddedLanguageLine, bool hasEOL, EmbeddedLanguageData embeddedLanguageData, int offsetDelta);
}

internal sealed class MonarchClassicTokensCollector(TokenizationRegistry<ITokenizationSupport> registry) : IMonarchTokensCollector
{
    private readonly List<Token> _tokens = new();
    private string? _languageId;
    private string? _lastTokenType;
    private string? _lastTokenLanguage;

    public void EnterLanguage(string languageId) => _languageId = languageId;

    public void Emit(int startOffset, string type)
    {
        if (_lastTokenType == type && _lastTokenLanguage == _languageId)
        {
            return;
        }

        _lastTokenType = type;
        _lastTokenLanguage = _languageId;
        _tokens.Add(new Token(startOffset, type, _languageId!));
    }

    public IState NestedLanguageTokenize(string embeddedLanguageLine, bool hasEOL, EmbeddedLanguageData embeddedLanguageData, int offsetDelta)
    {
        var nestedLanguageId = embeddedLanguageData.LanguageId;
        var embeddedModeState = embeddedLanguageData.State;

        var nestedLanguageTokenizationSupport = registry.Get(nestedLanguageId);
        if (nestedLanguageTokenizationSupport is null)
        {
            EnterLanguage(nestedLanguageId);
            Emit(offsetDelta, string.Empty);
            return embeddedModeState;
        }

        var nestedResult = nestedLanguageTokenizationSupport.Tokenize(embeddedLanguageLine, hasEOL, embeddedModeState);
        foreach (var token in nestedResult.Tokens)
        {
            _tokens.Add(offsetDelta != 0 ? new Token(token.Offset + offsetDelta, token.Type, token.Language) : token);
        }

        _lastTokenType = null;
        _lastTokenLanguage = null;
        _languageId = null;
        return nestedResult.EndState;
    }

    public TokenizationResult Finalize(MonarchLineState endState) => new(_tokens, endState);
}

internal sealed class MonarchModernTokensCollector(
    IMonarchLanguageService languageService,
    TokenizationRegistry<ITokenizationSupport> registry,
    TokenTheme theme) : IMonarchTokensCollector
{
    private uint[]? _prependTokens;
    private List<uint> _tokens = new();
    private LanguageId _currentLanguageId = LanguageId.Null;
    private uint _lastTokenMetadata;

    public void EnterLanguage(string languageId) =>
        _currentLanguageId = languageService.LanguageIdCodec.EncodeLanguageId(languageId);

    public void Emit(int startOffset, string type)
    {
        var metadata = theme.Match(_currentLanguageId, type) | MetadataConsts.BalancedBracketsMask;
        if (_lastTokenMetadata == metadata)
        {
            return;
        }

        _lastTokenMetadata = metadata;
        _tokens.Add((uint)startOffset);
        _tokens.Add(metadata);
    }

    private static uint[] Merge(uint[]? a, List<uint> b, uint[]? c)
    {
        var aLen = a?.Length ?? 0;
        var bLen = b.Count;
        var cLen = c?.Length ?? 0;

        if (aLen == 0 && bLen == 0 && cLen == 0)
        {
            return [];
        }

        if (aLen == 0 && bLen == 0)
        {
            return c!;
        }

        if (bLen == 0 && cLen == 0)
        {
            return a!;
        }

        var result = new uint[aLen + bLen + cLen];
        a?.CopyTo(result, 0);
        b.CopyTo(result, aLen);
        c?.CopyTo(result, aLen + bLen);
        return result;
    }

    public IState NestedLanguageTokenize(string embeddedLanguageLine, bool hasEOL, EmbeddedLanguageData embeddedLanguageData, int offsetDelta)
    {
        var nestedLanguageId = embeddedLanguageData.LanguageId;
        var embeddedModeState = embeddedLanguageData.State;

        var nestedLanguageTokenizationSupport = registry.Get(nestedLanguageId);
        if (nestedLanguageTokenizationSupport is null)
        {
            EnterLanguage(nestedLanguageId);
            Emit(offsetDelta, string.Empty);
            return embeddedModeState;
        }

        var nestedResult = nestedLanguageTokenizationSupport.TokenizeEncoded(embeddedLanguageLine, hasEOL, embeddedModeState);
        var nestedTokens = nestedResult.Tokens;
        if (offsetDelta != 0)
        {
            nestedTokens = (uint[])nestedTokens.Clone();
            for (var i = 0; i < nestedTokens.Length; i += 2)
            {
                nestedTokens[i] += (uint)offsetDelta;
            }
        }

        _prependTokens = Merge(_prependTokens, _tokens, nestedTokens);
        _tokens = new List<uint>();
        _currentLanguageId = LanguageId.Null;
        _lastTokenMetadata = 0;
        return nestedResult.EndState;
    }

    public EncodedTokenizationResult Finalize(MonarchLineState endState) => new(Merge(_prependTokens, _tokens, null), endState);
}

/// <summary>A tokenizer for a compiled Monarch language (upstream's MonarchTokenizer).</summary>
internal sealed class MonarchTokenizer : ITokenizationSupport, IDisposable
{
    /// <summary>Upstream's default of editor.maxTokenizationLineLength.</summary>
    public const int DefaultMaxTokenizationLineLength = 20_000;

    private readonly IMonarchLanguageService _languageService;
    private readonly IStandaloneThemeService _standaloneThemeService;
    private readonly TokenizationRegistry<ITokenizationSupport> _registry;
    private readonly string _languageId;
    private readonly CompiledLexer _lexer;
    private readonly ConcurrentDictionary<string, bool> _embeddedLanguages = new(StringComparer.Ordinal);
    private readonly MonarchStackFactories _factories = new();
    private readonly int _maxTokenizationLineLength;
    private bool _emitting;

    public MonarchTokenizer(
        IMonarchLanguageService languageService,
        IStandaloneThemeService standaloneThemeService,
        TokenizationRegistry<ITokenizationSupport> registry,
        string languageId,
        CompiledLexer lexer,
        int maxTokenizationLineLength = DefaultMaxTokenizationLineLength)
    {
        _languageService = languageService;
        _standaloneThemeService = standaloneThemeService;
        _registry = registry;
        _languageId = languageId;
        _lexer = lexer;
        _maxTokenizationLineLength = maxTokenizationLineLength;

        // Set up listening for embedded modes
        _registry.OnDidChange += HandleRegistryChange;
    }

    public CompiledLexer Lexer => _lexer;

    private void HandleRegistryChange(TokenizationSupportChangedEvent e)
    {
        if (_emitting)
        {
            return;
        }

        var isOneOfMyEmbeddedModes = false;
        foreach (var language in e.ChangedLanguages)
        {
            if (_embeddedLanguages.ContainsKey(language))
            {
                isOneOfMyEmbeddedModes = true;
                break;
            }
        }

        if (isOneOfMyEmbeddedModes)
        {
            _emitting = true;
            try
            {
                _registry.HandleChange([_languageId]);
            }
            finally
            {
                _emitting = false;
            }
        }
    }

    public void Dispose() => _registry.OnDidChange -= HandleRegistryChange;

    public IState GetInitialState()
    {
        var rootState = _factories.CreateStackElement(null, _lexer.Start!);
        return _factories.CreateLineState(rootState, null);
    }

    public TokenizationResult Tokenize(string line, bool hasEOL, IState lineState)
    {
        if (line.Length >= _maxTokenizationLineLength)
        {
            return NullTokenize.Tokenize(_languageId, lineState);
        }

        var tokensCollector = new MonarchClassicTokensCollector(_registry);
        var endLineState = TokenizeCore(line, hasEOL, (MonarchLineState)lineState, tokensCollector);
        return tokensCollector.Finalize(endLineState);
    }

    public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState lineState)
    {
        if (line.Length >= _maxTokenizationLineLength)
        {
            return NullTokenize.TokenizeEncoded(_languageService.LanguageIdCodec.EncodeLanguageId(_languageId), lineState);
        }

        var tokensCollector = new MonarchModernTokensCollector(_languageService, _registry, _standaloneThemeService.GetColorTheme().TokenTheme);
        var endLineState = TokenizeCore(line, hasEOL, (MonarchLineState)lineState, tokensCollector);
        return tokensCollector.Finalize(endLineState);
    }

    private MonarchLineState TokenizeCore(string line, bool hasEOL, MonarchLineState lineState, IMonarchTokensCollector collector) =>
        lineState.EmbeddedLanguageData is not null
            ? NestedTokenize(line, hasEOL, lineState, 0, collector)
            : MyTokenize(line, hasEOL, lineState, 0, collector);

    private int FindLeavingNestedLanguageOffset(string line, MonarchLineState state)
    {
        if (!_lexer.Tokenizer.TryGetValue(state.Stack.State, out var rules))
        {
            rules = MonarchCommon.FindRules(_lexer, state.Stack.State) // do parent matching
                ?? throw MonarchCommon.CreateError(_lexer, "tokenizer state is not defined: " + state.Stack.State);
        }

        var popOffset = -1;
        var hasEmbeddedPopRule = false;

        foreach (var rule in rules)
        {
            if (rule.Action.IsString || !(rule.Action.NextEmbedded == "@pop" || rule.Action.HasEmbeddedEndInCases))
            {
                continue;
            }

            hasEmbeddedPopRule = true;

            var match = rule.ResolveSearchRegex(state.Stack.State).Match(line);
            var result = match.Success ? match.Index : -1;
            if (result == -1 || (result != 0 && rule.MatchOnlyAtLineStart))
            {
                continue;
            }

            if (popOffset == -1 || result < popOffset)
            {
                popOffset = result;
            }
        }

        if (!hasEmbeddedPopRule)
        {
            throw MonarchCommon.CreateError(_lexer, "no rule containing nextEmbedded: \"@pop\" in tokenizer embedded state: " + state.Stack.State);
        }

        return popOffset;
    }

    private MonarchLineState NestedTokenize(string line, bool hasEOL, MonarchLineState lineState, int offsetDelta, IMonarchTokensCollector tokensCollector)
    {
        var popOffset = FindLeavingNestedLanguageOffset(line, lineState);

        if (popOffset == -1)
        {
            // tokenization will not leave nested language
            var nestedEndState = tokensCollector.NestedLanguageTokenize(line, hasEOL, lineState.EmbeddedLanguageData!, offsetDelta);
            return _factories.CreateLineState(lineState.Stack, new EmbeddedLanguageData(lineState.EmbeddedLanguageData!.LanguageId, nestedEndState));
        }

        var nestedLanguageLine = line[..popOffset];
        if (nestedLanguageLine.Length > 0)
        {
            // tokenize with the nested language
            tokensCollector.NestedLanguageTokenize(nestedLanguageLine, false, lineState.EmbeddedLanguageData!, offsetDelta);
        }

        var restOfTheLine = line[popOffset..];
        return MyTokenize(restOfTheLine, hasEOL, lineState, offsetDelta + popOffset, tokensCollector);
    }

    private static string SafeRuleName(CompiledRule? rule) => rule?.Name ?? "(unknown)";

    /// <summary>A rule's match at <paramref name="pos"/> as upstream's matches array (unmatched groups empty).</summary>
    private static string[]? MatchAt(System.Text.RegularExpressions.Regex regex, string line, int pos)
    {
        var match = regex.Match(line, pos, line.Length - pos);
        if (!match.Success)
        {
            return null;
        }

        var groups = match.Groups;
        var result = new string[groups.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = groups[i].Success ? groups[i].Value : string.Empty;
        }

        return result;
    }

    private sealed class GroupMatching(string[] matches, CompiledRule? rule)
    {
        public string[] Matches { get; } = matches;

        public CompiledRule? Rule { get; } = rule;

        public Queue<(CompiledAction Action, string Matched)> Groups { get; } = new();
    }

    private MonarchLineState MyTokenize(string lineWithoutLF, bool hasEOL, MonarchLineState lineState, int offsetDelta, IMonarchTokensCollector tokensCollector)
    {
        tokensCollector.EnterLanguage(_languageId);

        var lineWithoutLFLength = lineWithoutLF.Length;
        var line = hasEOL && _lexer.IncludeLF ? lineWithoutLF + "\n" : lineWithoutLF;
        var lineLength = line.Length;

        var embeddedLanguageData = lineState.EmbeddedLanguageData;
        var stack = lineState.Stack;
        var pos = 0;

        // regular expression group matching
        // these never need cloning or equality since they are only used within a line match
        GroupMatching? groupMatching = null;

        // See https://github.com/microsoft/monaco-editor/issues/1235
        // Evaluate rules at least once for an empty line
        var forceEvaluation = true;

        while (forceEvaluation || pos < lineLength)
        {
            var pos0 = pos;
            var stackLen0 = stack.Depth;
            var groupLen0 = groupMatching?.Groups.Count ?? 0;
            var state = stack.State;

            string[]? matches = null;
            string? matched = null;
            CompiledAction? action = null;
            CompiledRule? rule = null;

            string? enteringEmbeddedLanguage = null;

            // check if we need to process group matches first
            if (groupMatching is not null)
            {
                matches = groupMatching.Matches;
                var groupEntry = groupMatching.Groups.Dequeue();
                matched = groupEntry.Matched;
                action = groupEntry.Action;
                rule = groupMatching.Rule;

                // cleanup if necessary
                if (groupMatching.Groups.Count == 0)
                {
                    groupMatching = null;
                }
            }
            else
            {
                // otherwise we match on the token stream

                if (!forceEvaluation && pos >= lineLength)
                {
                    // nothing to do
                    break;
                }

                forceEvaluation = false;

                // get the rules for this state
                if (!_lexer.Tokenizer.TryGetValue(state, out var rules))
                {
                    rules = MonarchCommon.FindRules(_lexer, state) // do parent matching
                        ?? throw MonarchCommon.CreateError(_lexer, "tokenizer state is not defined: " + state);
                }

                // try each rule until we match
                foreach (var candidate in rules)
                {
                    if (pos == 0 || !candidate.MatchOnlyAtLineStart)
                    {
                        matches = MatchAt(candidate.ResolveRegex(state), line, pos);
                        if (matches is not null)
                        {
                            matched = matches[0];
                            action = candidate.Action;
                            rule = candidate;
                            break;
                        }
                    }
                }
            }

            // We matched 'rule' with 'matches' and 'action'
            if (matches is null)
            {
                matches = [string.Empty];
                matched = string.Empty;
            }

            if (action is null)
            {
                // bad: we didn't match anything, and there is no action to take
                // we need to advance the stream or we get progress trouble
                if (pos < lineLength)
                {
                    matches = [line[pos].ToString()];
                    matched = matches[0];
                }

                action = CompiledAction.FromString(_lexer.DefaultToken);
            }

            if (matched is null)
            {
                // should never happen, needed for strict null checking
                break;
            }

            // advance stream
            pos += matched.Length;

            // maybe call action function (used for 'cases')
            while (!action.IsString && action.Test is { } test)
            {
                action = test(matched, matches, state, pos == lineLength);
            }

            // set the result: either a string or an array of actions
            string? resultString = null;
            IReadOnlyList<CompiledAction>? resultGroup = null;
            if (action.IsString)
            {
                resultString = action.Text;
            }
            else if (action.Group is { } group)
            {
                resultGroup = group;
            }
            else if (action.Token is not null)
            {
                // do $n replacements?
                resultString = action.TokenSubst
                    ? MonarchCommon.SubstituteMatches(_lexer, action.Token, matched, matches, state)
                    : action.Token;

                // enter embedded language?
                if (!string.IsNullOrEmpty(action.NextEmbedded))
                {
                    if (action.NextEmbedded == "@pop")
                    {
                        if (embeddedLanguageData is null)
                        {
                            throw MonarchCommon.CreateError(_lexer, "cannot pop embedded language if not inside one");
                        }

                        embeddedLanguageData = null;
                    }
                    else if (embeddedLanguageData is not null)
                    {
                        throw MonarchCommon.CreateError(_lexer, "cannot enter embedded language from within an embedded language");
                    }
                    else
                    {
                        enteringEmbeddedLanguage = MonarchCommon.SubstituteMatches(_lexer, action.NextEmbedded, matched, matches, state);
                    }
                }

                // state transformations
                if (action.GoBack is { } goBack && goBack != 0)
                {
                    // back up the stream..
                    pos = Math.Max(0, pos - goBack);
                }

                if (!string.IsNullOrEmpty(action.SwitchTo))
                {
                    var nextState = MonarchCommon.SubstituteMatches(_lexer, action.SwitchTo, matched, matches, state); // switch state without a push...
                    if (nextState.Length > 0 && nextState[0] == '@')
                    {
                        nextState = nextState[1..]; // peel off starting '@'
                    }

                    if (MonarchCommon.FindRules(_lexer, nextState) is null)
                    {
                        throw MonarchCommon.CreateError(_lexer, "trying to switch to a state '" + nextState + "' that is undefined in rule: " + SafeRuleName(rule));
                    }

                    stack = stack.SwitchTo(nextState);
                }
                else if (!string.IsNullOrEmpty(action.Next))
                {
                    if (action.Next == "@push")
                    {
                        if (stack.Depth >= _lexer.MaxStack)
                        {
                            throw MonarchCommon.CreateError(_lexer, "maximum tokenizer stack size reached: [" + stack.State + "," + stack.Parent!.State + ",...]");
                        }

                        stack = stack.Push(state);
                    }
                    else if (action.Next == "@pop")
                    {
                        if (stack.Depth <= 1)
                        {
                            throw MonarchCommon.CreateError(_lexer, "trying to pop an empty stack in rule: " + SafeRuleName(rule));
                        }

                        stack = stack.Pop()!;
                    }
                    else if (action.Next == "@popall")
                    {
                        stack = stack.PopAll();
                    }
                    else
                    {
                        var nextState = MonarchCommon.SubstituteMatches(_lexer, action.Next, matched, matches, state);
                        if (nextState.Length > 0 && nextState[0] == '@')
                        {
                            nextState = nextState[1..]; // peel off starting '@'
                        }

                        if (MonarchCommon.FindRules(_lexer, nextState) is null)
                        {
                            throw MonarchCommon.CreateError(_lexer, "trying to set a next state '" + nextState + "' that is undefined in rule: " + SafeRuleName(rule));
                        }

                        stack = stack.Push(nextState);
                    }
                }

                if (!string.IsNullOrEmpty(action.Log))
                {
                    MonarchCommon.Log(_lexer, _lexer.LanguageId + ": " + MonarchCommon.SubstituteMatches(_lexer, action.Log, matched, matches, state));
                }
            }

            // check result
            if (resultString is null && resultGroup is null)
            {
                throw MonarchCommon.CreateError(_lexer, "lexer rule has no well-defined action in rule: " + SafeRuleName(rule));
            }

            MonarchLineState ComputeNewStateForEmbeddedLanguage(string entering)
            {
                // support language names, mime types, and language ids
                var languageId =
                    _languageService.GetLanguageIdByLanguageName(entering)
                    ?? _languageService.GetLanguageIdByMimeType(entering)
                    ?? entering;

                var nestedData = GetNestedEmbeddedLanguageData(languageId);

                if (pos < lineLength)
                {
                    // there is content from the embedded language on this line
                    var restOfLine = pos < lineWithoutLF.Length ? lineWithoutLF[pos..] : string.Empty;
                    return NestedTokenize(restOfLine, hasEOL, _factories.CreateLineState(stack, nestedData), offsetDelta + pos, tokensCollector);
                }

                return _factories.CreateLineState(stack, nestedData);
            }

            // is the result a group match?
            if (resultGroup is not null)
            {
                if (groupMatching is not null && groupMatching.Groups.Count > 0)
                {
                    throw MonarchCommon.CreateError(_lexer, "groups cannot be nested: " + SafeRuleName(rule));
                }

                if (matches.Length != resultGroup.Count + 1)
                {
                    throw MonarchCommon.CreateError(_lexer, "matched number of groups does not match the number of actions in rule: " + SafeRuleName(rule));
                }

                var totalLen = 0;
                for (var i = 1; i < matches.Length; i++)
                {
                    totalLen += matches[i].Length;
                }

                if (totalLen != matched.Length)
                {
                    throw MonarchCommon.CreateError(_lexer, "with groups, all characters should be matched in consecutive groups in rule: " + SafeRuleName(rule));
                }

                groupMatching = new GroupMatching(matches, rule);
                for (var i = 0; i < resultGroup.Count; i++)
                {
                    groupMatching.Groups.Enqueue((resultGroup[i], matches[i + 1]));
                }

                pos -= matched.Length;
                // call recursively to initiate first result match
                continue;
            }

            // regular result
            var result = resultString!;

            // check for '@rematch'
            if (result == "@rematch")
            {
                pos -= matched.Length;
                matched = string.Empty; // better set the next state too..
                matches = null;
                result = string.Empty;

                // Even though `@rematch` was specified, if `nextEmbedded` also specified,
                // a state transition should occur.
                if (enteringEmbeddedLanguage is not null)
                {
                    return ComputeNewStateForEmbeddedLanguage(enteringEmbeddedLanguage);
                }
            }

            // check progress
            if (matched.Length == 0)
            {
                if (lineLength == 0 || stackLen0 != stack.Depth || state != stack.State || (groupMatching?.Groups.Count ?? 0) != groupLen0)
                {
                    continue;
                }

                throw MonarchCommon.CreateError(_lexer, "no progress in tokenizer in rule: " + SafeRuleName(rule));
            }

            // return the result (and check for brace matching)
            // todo: for efficiency we could pre-sanitize tokenPostfix and substitutions
            string tokenType;
            if (result.StartsWith("@brackets", StringComparison.Ordinal))
            {
                var rest = result["@brackets".Length..];
                var bracket = FindBracket(_lexer, matched)
                    ?? throw MonarchCommon.CreateError(_lexer, "@brackets token returned but no bracket defined as: " + matched);
                tokenType = MonarchCommon.Sanitize(bracket.Token + rest);
            }
            else
            {
                var token = result.Length == 0 ? string.Empty : result + _lexer.TokenPostfix;
                tokenType = MonarchCommon.Sanitize(token);
            }

            if (pos0 < lineWithoutLFLength)
            {
                tokensCollector.Emit(pos0 + offsetDelta, tokenType);
            }

            if (enteringEmbeddedLanguage is not null)
            {
                return ComputeNewStateForEmbeddedLanguage(enteringEmbeddedLanguage);
            }
        }

        return _factories.CreateLineState(stack, embeddedLanguageData);
    }

    private EmbeddedLanguageData GetNestedEmbeddedLanguageData(string languageId)
    {
        if (!_languageService.IsRegisteredLanguageId(languageId))
        {
            return new EmbeddedLanguageData(languageId, NullState.Instance);
        }

        if (languageId != _languageId)
        {
            // Fire language loading event
            _languageService.RequestBasicLanguageFeatures(languageId);
            _ = _registry.GetOrCreateAsync(languageId);
            _embeddedLanguages[languageId] = true;
        }

        var tokenizationSupport = _registry.Get(languageId);
        if (tokenizationSupport is not null)
        {
            return new EmbeddedLanguageData(languageId, tokenizationSupport.GetInitialState());
        }

        return new EmbeddedLanguageData(languageId, NullState.Instance);
    }

    /// <summary>Searches for a bracket in the 'brackets' attribute that matches the input.</summary>
    private static (string Token, MonarchBracket BracketType)? FindBracket(CompiledLexer lexer, string matched)
    {
        if (string.IsNullOrEmpty(matched))
        {
            return null;
        }

        matched = MonarchCommon.FixCase(lexer, matched);

        foreach (var bracket in lexer.Brackets)
        {
            if (bracket.Open == matched)
            {
                return (bracket.Token, MonarchBracket.Open);
            }

            if (bracket.Close == matched)
            {
                return (bracket.Token, MonarchBracket.Close);
            }
        }

        return null;
    }
}
