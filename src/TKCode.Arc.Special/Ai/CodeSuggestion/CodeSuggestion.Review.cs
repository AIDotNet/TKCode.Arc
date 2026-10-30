using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class CodeSuggestion
{
    private readonly Dictionary<string, CodeSuggestionDecision> _decisions = new(StringComparer.Ordinal);
    private bool _resolvedRaised;

    /// <summary>The decision about the hunk with <paramref name="id"/> (pending when it is unknown).</summary>
    public CodeSuggestionDecision GetDecision(string id) => DecisionOf(id);

    /// <summary>The file as it now stands: accepted hunks applied, the rest original.</summary>
    public string GetCode() =>
        CodeSuggestionDiff.Apply(_lines, _spans, span => DecisionOf(span.Id) == CodeSuggestionDecision.Accepted);

    /// <summary>Accepts the pending hunk with <paramref name="id"/>; false when it is not pending or the status is not ready.</summary>
    public bool Accept(string id) => DecideOne(id, CodeSuggestionDecision.Accepted);

    /// <summary>Rejects the pending hunk with <paramref name="id"/>; false when it is not pending or the status is not ready.</summary>
    public bool Reject(string id) => DecideOne(id, CodeSuggestionDecision.Rejected);

    /// <summary>Accepts every pending hunk (Cmd or Ctrl + Enter); returns how many were accepted.</summary>
    public int AcceptAll() => DecideAll(CodeSuggestionDecision.Accepted);

    /// <summary>Rejects every pending hunk (Cmd or Ctrl + Backspace); returns how many were rejected.</summary>
    public int RejectAll() => DecideAll(CodeSuggestionDecision.Rejected);

    internal CodeSuggestionDecision DecisionOf(string id) =>
        _decisions.TryGetValue(id, out var decision) ? decision : CodeSuggestionDecision.Pending;

    private void ClearDecisions()
    {
        _decisions.Clear();
        _resolvedRaised = false;
    }

    /// <summary>Records a decision without side effects; false when the hunk cannot take one now.</summary>
    private bool Record(string id, CodeSuggestionDecision decision)
    {
        if (decision == CodeSuggestionDecision.Pending || EffectiveStatus != CodeSuggestionStatus.Ready
            || DecisionOf(id) != CodeSuggestionDecision.Pending || !_spans.Exists(span => span.Id == id))
        {
            return false;
        }
        _decisions[id] = decision;
        if (_states.TryGetValue(id, out var state))
        {
            state.Decision = decision;
        }
        return true;
    }

    private CodeSuggestionSpan SpanOf(string id) => _spans.Find(span => span.Id == id)!;

    private bool DecideOne(string id, CodeSuggestionDecision decision)
    {
        if (!Record(id, decision))
        {
            return false;
        }
        var span = SpanOf(id);
        Sync(animate: true);
        RaiseEvent(new CodeSuggestionDecisionEventArgs(DecisionMadeEvent, span.Hunk, decision, GetCode()));
        var labels = Labels;
        var summary = SummaryOf(span);
        var said = decision == CodeSuggestionDecision.Accepted ? labels.AcceptedAnnouncement(summary) : labels.RejectedAnnouncement(summary);
        Announce(said + " " + (PendingCount > 0 ? labels.ToReview(PendingCount) : ResultText()));
        ResolveIfDone();
        return true;
    }

    private int DecideAll(CodeSuggestionDecision decision)
    {
        if (EffectiveStatus != CodeSuggestionStatus.Ready)
        {
            return 0;
        }
        var decided = new List<CodeSuggestionSpan>();
        foreach (var span in _spans)
        {
            if (Record(span.Id, decision))
            {
                decided.Add(span);
            }
        }
        if (decided.Count == 0)
        {
            return 0;
        }
        var hadFocus = _view?.IsKeyboardFocusWithin == true;
        Sync(animate: true);
        // One event per hunk, each with the file as it stood after that hunk.
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var span in _spans)
        {
            if (DecisionOf(span.Id) == CodeSuggestionDecision.Accepted && !decided.Contains(span))
            {
                accepted.Add(span.Id);
            }
        }
        foreach (var span in decided)
        {
            if (decision == CodeSuggestionDecision.Accepted)
            {
                accepted.Add(span.Id);
            }
            var code = CodeSuggestionDiff.Apply(_lines, _spans, s => accepted.Contains(s.Id));
            RaiseEvent(new CodeSuggestionDecisionEventArgs(DecisionMadeEvent, span.Hunk, decision, code));
        }
        Announce(ResultText());
        ResolveIfDone();
        if (hadFocus)
        {
            // A bar button had focus and every bar is folding away.
            MoveFocus(_codeArea);
        }
        return decided.Count;
    }

    private void DecideFromBar(string id, CodeSuggestionDecision decision)
    {
        var hadFocus = _states.TryGetValue(id, out var state) && state.Bar.IsKeyboardFocusWithin;
        if (!DecideOne(id, decision) || !hadFocus)
        {
            return;
        }
        // The pressed button leaves with its bar: go to the next pending hunk's Accept, or back to the code.
        var states = HunkStates;
        var at = -1;
        for (var i = 0; i < states.Count; i++)
        {
            if (states[i].Id == id)
            {
                at = i;
            }
        }
        CodeSuggestionHunkState? next = null;
        for (var step = 1; step <= states.Count && next is null; step++)
        {
            var candidate = states[(at + step + states.Count) % states.Count];
            if (candidate.Decision == CodeSuggestionDecision.Pending && candidate.IsShown)
            {
                next = candidate;
            }
        }
        MoveFocus((IInputElement?)next?.Bar.AcceptButton ?? _codeArea);
    }

    private string SummaryOf(CodeSuggestionSpan span) =>
        _states.TryGetValue(span.Id, out var state) ? SummaryOf(state)
            : string.IsNullOrWhiteSpace(span.Hunk.Summary) ? Labels.Change(_spans.IndexOf(span) + 1) : span.Hunk.Summary!;

    /// <summary>The finished review in words: "Applied 2 of 3 changes", "Rejected all 3 changes", "No changes".</summary>
    private string ResultText()
    {
        var labels = Labels;
        var total = _spans.Count;
        var accepted = _spans.Count(span => DecisionOf(span.Id) == CodeSuggestionDecision.Accepted);
        var text = total == 0 ? labels.NoChanges : accepted > 0 ? labels.Applied(accepted, total) : labels.RejectedAll(total);
        return text.EndsWith('.') ? text : text + ".";
    }

    private void ResolveIfDone()
    {
        if (_resolvedRaised || _spans.Count == 0 || _spans.Exists(span => DecisionOf(span.Id) == CodeSuggestionDecision.Pending))
        {
            return;
        }
        _resolvedRaised = true;
        var accepted = new List<string>();
        var rejected = new List<string>();
        foreach (var span in _spans)
        {
            (DecisionOf(span.Id) == CodeSuggestionDecision.Accepted ? accepted : rejected).Add(span.Id);
        }
        if (UsesSample)
        {
            Announce(ResultText() + " " + Labels.Simulated);
        }
        RaiseEvent(new CodeSuggestionResolvedEventArgs(ResolvedEvent, GetCode(), accepted, rejected));
        OnSampleResolved();
    }

    /// <summary>Says the state once when it changes (streaming is announced once, not per line).</summary>
    private void AnnounceStatus()
    {
        var labels = Labels;
        switch (EffectiveStatus)
        {
            case CodeSuggestionStatus.Thinking:
                Announce(labels.Reading(FileName));
                break;
            case CodeSuggestionStatus.Streaming:
                Announce(labels.Writing);
                break;
            case CodeSuggestionStatus.Error:
                Announce(ErrorText);
                break;
            default:
                Announce(PendingCount > 0 ? labels.ToReview(PendingCount) : ResultText());
                break;
        }
    }

    private void Announce(string text)
    {
        if (!Quiet)
        {
            _announcer?.Announce(text);
        }
    }

    private void PressStop()
    {
        if (UsesSample)
        {
            StopSample();
            return;
        }
        RaiseEvent(new RoutedEventArgs(StopRequestedEvent));
    }

    private void PressRegenerate()
    {
        if (UsesSample)
        {
            _simStarted = true;
            StartRun();
            return;
        }
        RaiseEvent(new RoutedEventArgs(RegenerateRequestedEvent));
    }
}
