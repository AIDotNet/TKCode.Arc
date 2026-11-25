// Ported from vscode (src/vs/editor/common/languages/linkComputer.ts, commit b57d5c9e), MIT license.
// Differences:
//  - the result is ComputedLink (range and URL text) instead of languages.ILink, since the URL is not parsed here;
//    ComputedLinkProvider turns them into Links for the link provider registry;
//  - the state machine and the classifier are immutable statics built once (upstream: lazily created module
//    singletons); Uint8Matrix is a byte array inside StateMachine.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>What the link computer reads (upstream's ILinkComputerTarget).</summary>
internal interface ILinkComputerTarget
{
    int GetLineCount();

    string GetLineContent(int lineNumber);
}

/// <summary>A link found in plain text: its range and its text (upstream's ILink as computeLinks returns it).</summary>
internal sealed record ComputedLink(TextRange Range, string Url);

/// <summary>The states of the scheme recognizer (upstream's State).</summary>
internal enum LinkComputerState : byte
{
    Invalid = 0,
    Start = 1,
    H = 2,
    HT = 3,
    HTT = 4,
    HTTP = 5,
    F = 6,
    FI = 7,
    FIL = 8,
    BeforeColon = 9,
    AfterColon = 10,
    AlmostThere = 11,
    End = 12,
    Accept = 13,
    LastKnownState = 14,
}

/// <summary>A table-driven state machine over char codes (upstream's StateMachine).</summary>
internal sealed class LinkStateMachine
{
    private readonly byte[] _states;
    private readonly int _cols;
    private readonly int _maxCharCode;

    public LinkStateMachine(IReadOnlyList<(LinkComputerState From, char ChCode, LinkComputerState To)> edges)
    {
        var maxCharCode = 0;
        var maxState = (int)LinkComputerState.Invalid;
        foreach (var (from, chCode, to) in edges)
        {
            if (chCode > maxCharCode)
            {
                maxCharCode = chCode;
            }

            if ((int)from > maxState)
            {
                maxState = (int)from;
            }

            if ((int)to > maxState)
            {
                maxState = (int)to;
            }
        }

        maxCharCode++;
        maxState++;

        _states = new byte[maxState * maxCharCode];
        _cols = maxCharCode;
        foreach (var (from, chCode, to) in edges)
        {
            _states[((int)from * _cols) + chCode] = (byte)to;
        }

        _maxCharCode = maxCharCode;
    }

    public LinkComputerState NextState(LinkComputerState currentState, int chCode)
    {
        if (chCode < 0 || chCode >= _maxCharCode)
        {
            return LinkComputerState.Invalid;
        }

        return (LinkComputerState)_states[((int)currentState * _cols) + chCode];
    }
}

/// <summary>Finds http(s) and file links in text (upstream's LinkComputer).</summary>
internal static class LinkComputer
{
    private const byte None = 0;
    private const byte ForceTermination = 1;
    private const byte CannotEndIn = 2;

    // State machine for http:// or https:// or file://
    private static readonly LinkStateMachine s_stateMachine = new(
    [
        (LinkComputerState.Start, 'h', LinkComputerState.H),
        (LinkComputerState.Start, 'H', LinkComputerState.H),
        (LinkComputerState.Start, 'f', LinkComputerState.F),
        (LinkComputerState.Start, 'F', LinkComputerState.F),

        (LinkComputerState.H, 't', LinkComputerState.HT),
        (LinkComputerState.H, 'T', LinkComputerState.HT),

        (LinkComputerState.HT, 't', LinkComputerState.HTT),
        (LinkComputerState.HT, 'T', LinkComputerState.HTT),

        (LinkComputerState.HTT, 'p', LinkComputerState.HTTP),
        (LinkComputerState.HTT, 'P', LinkComputerState.HTTP),

        (LinkComputerState.HTTP, 's', LinkComputerState.BeforeColon),
        (LinkComputerState.HTTP, 'S', LinkComputerState.BeforeColon),
        (LinkComputerState.HTTP, ':', LinkComputerState.AfterColon),

        (LinkComputerState.F, 'i', LinkComputerState.FI),
        (LinkComputerState.F, 'I', LinkComputerState.FI),

        (LinkComputerState.FI, 'l', LinkComputerState.FIL),
        (LinkComputerState.FI, 'L', LinkComputerState.FIL),

        (LinkComputerState.FIL, 'e', LinkComputerState.BeforeColon),
        (LinkComputerState.FIL, 'E', LinkComputerState.BeforeColon),

        (LinkComputerState.BeforeColon, ':', LinkComputerState.AfterColon),

        (LinkComputerState.AfterColon, '/', LinkComputerState.AlmostThere),

        (LinkComputerState.AlmostThere, '/', LinkComputerState.End),
    ]);

    private static readonly CharacterClassifier s_classifier = CreateClassifier();

    private static CharacterClassifier CreateClassifier()
    {
        var classifier = new CharacterClassifier(None);

        const string forceTerminationCharacters = " \t<>'\"、。｡､，．：；‘〈「『〔（［｛｢｣｝］）〕』」〉’｀～…|";
        foreach (var ch in forceTerminationCharacters)
        {
            classifier.Set(ch, ForceTermination);
        }

        const string cannotEndWithCharacters = ".,;:";
        foreach (var ch in cannotEndWithCharacters)
        {
            classifier.Set(ch, CannotEndIn);
        }

        return classifier;
    }

    private static ComputedLink CreateLink(string line, int lineNumber, int linkBeginIndex, int linkEndIndex)
    {
        // Do not allow to end link in certain characters...
        var lastIncludedCharIndex = linkEndIndex - 1;
        do
        {
            var chCode = line[lastIncludedCharIndex];
            var chClass = s_classifier.Get(chCode);
            if (chClass != CannotEndIn)
            {
                break;
            }

            lastIncludedCharIndex--;
        }
        while (lastIncludedCharIndex > linkBeginIndex);

        // Handle links enclosed in parens, square brackets and curlys.
        if (linkBeginIndex > 0)
        {
            var charCodeBeforeLink = line[linkBeginIndex - 1];
            var lastCharCodeInLink = line[lastIncludedCharIndex];

            if ((charCodeBeforeLink == '(' && lastCharCodeInLink == ')')
                || (charCodeBeforeLink == '[' && lastCharCodeInLink == ']')
                || (charCodeBeforeLink == '{' && lastCharCodeInLink == '}'))
            {
                // Do not end in ) if ( is before the link start
                // Do not end in ] if [ is before the link start
                // Do not end in } if { is before the link start
                lastIncludedCharIndex--;
            }
        }

        return new ComputedLink(
            new TextRange(lineNumber, linkBeginIndex + 1, lineNumber, lastIncludedCharIndex + 2),
            line.Substring(linkBeginIndex, lastIncludedCharIndex + 1 - linkBeginIndex));
    }

    /// <summary>The links of <paramref name="model"/>, or none for a null model (upstream's computeLinks).</summary>
    public static List<ComputedLink> ComputeLinks(ILinkComputerTarget? model) =>
        model is null ? [] : ComputeLinks(model, s_stateMachine);

    /// <summary>The links of every line of <paramref name="model"/> (upstream's LinkComputer.computeLinks).</summary>
    public static List<ComputedLink> ComputeLinks(ILinkComputerTarget model, LinkStateMachine stateMachine)
    {
        var result = new List<ComputedLink>();
        for (int i = 1, lineCount = model.GetLineCount(); i <= lineCount; i++)
        {
            var line = model.GetLineContent(i);
            var len = line.Length;

            var j = 0;
            var linkBeginIndex = 0;
            var linkBeginChCode = 0;
            var state = LinkComputerState.Start;
            var hasOpenParens = false;
            var hasOpenSquareBracket = false;
            var inSquareBrackets = false;
            var hasOpenCurlyBracket = false;

            while (j < len)
            {
                var resetStateMachine = false;
                int chCode = line[j];

                if (state == LinkComputerState.Accept)
                {
                    byte chClass;
                    switch (chCode)
                    {
                        case '(':
                            hasOpenParens = true;
                            chClass = None;
                            break;
                        case ')':
                            chClass = hasOpenParens ? None : ForceTermination;
                            break;
                        case '[':
                            inSquareBrackets = true;
                            hasOpenSquareBracket = true;
                            chClass = None;
                            break;
                        case ']':
                            inSquareBrackets = false;
                            chClass = hasOpenSquareBracket ? None : ForceTermination;
                            break;
                        case '{':
                            hasOpenCurlyBracket = true;
                            chClass = None;
                            break;
                        case '}':
                            chClass = hasOpenCurlyBracket ? None : ForceTermination;
                            break;

                        // The following three rules make it that ' or " or ` are allowed inside links
                        // only if the link is wrapped by some other quote character
                        case '\'':
                        case '"':
                        case '`':
                            if (linkBeginChCode == chCode)
                            {
                                chClass = ForceTermination;
                            }
                            else if (linkBeginChCode is '\'' or '"' or '`')
                            {
                                chClass = None;
                            }
                            else
                            {
                                chClass = ForceTermination;
                            }

                            break;
                        case '*':
                            // `*` terminates a link if the link began with `*`
                            chClass = linkBeginChCode == '*' ? ForceTermination : None;
                            break;
                        case ' ':
                            // ` ` allow space in between [ and ]
                            chClass = inSquareBrackets ? None : ForceTermination;
                            break;
                        default:
                            chClass = s_classifier.Get(chCode);
                            break;
                    }

                    // Check if character terminates link
                    if (chClass == ForceTermination)
                    {
                        result.Add(CreateLink(line, i, linkBeginIndex, j));
                        resetStateMachine = true;
                    }
                }
                else if (state == LinkComputerState.End)
                {
                    byte chClass;
                    if (chCode == '[')
                    {
                        // Allow for the authority part to contain ipv6 addresses which contain [ and ]
                        hasOpenSquareBracket = true;
                        chClass = None;
                    }
                    else
                    {
                        chClass = s_classifier.Get(chCode);
                    }

                    // Check if character terminates link
                    if (chClass == ForceTermination)
                    {
                        resetStateMachine = true;
                    }
                    else
                    {
                        state = LinkComputerState.Accept;
                    }
                }
                else
                {
                    state = stateMachine.NextState(state, chCode);
                    if (state == LinkComputerState.Invalid)
                    {
                        resetStateMachine = true;
                    }
                }

                if (resetStateMachine)
                {
                    state = LinkComputerState.Start;
                    hasOpenParens = false;
                    hasOpenSquareBracket = false;
                    hasOpenCurlyBracket = false;

                    // Record where the link started
                    linkBeginIndex = j + 1;
                    linkBeginChCode = chCode;
                }

                j++;
            }

            if (state == LinkComputerState.Accept)
            {
                result.Add(CreateLink(line, i, linkBeginIndex, len));
            }
        }

        return result;
    }
}

/// <summary>
/// The links the link computer finds in a model, as a link provider (upstream: the editor worker's computeLinks,
/// which the standalone editor registers as a link provider for every language).
/// </summary>
internal sealed class ComputedLinkProvider : ILinkProvider
{
    public Task<LinksList?> ProvideLinks(TextModel model, CancellationToken token)
    {
        if (model.IsDisposed())
        {
            return Task.FromResult<LinksList?>(null);
        }

        var links = LinkComputer.ComputeLinks(new TextModelLinkTarget(model));
        var result = new List<Link>(links.Count);
        foreach (var link in links)
        {
            result.Add(Uri.TryCreate(link.Url, UriKind.Absolute, out var uri)
                ? new Link(link.Range) { Url = uri }
                : new Link(link.Range) { UrlText = link.Url });
        }

        return Task.FromResult<LinksList?>(new LinksList(result));
    }

    private sealed class TextModelLinkTarget(TextModel model) : ILinkComputerTarget
    {
        public int GetLineCount() => model.GetLineCount();

        public string GetLineContent(int lineNumber) => model.GetLineContent(lineNumber);
    }
}
