// Ported from Arc Onboarding checklist (MIT, Copyright (c) 2026 Elia Kuratli): the reducer, its messages and the rule
// that hands the open panel on to the next unfinished step.
using System.Globalization;

namespace TKCode.Arc;

/// <summary>The checklist's state and its transitions, free of any view (Arc's <c>reducer</c>).</summary>
internal sealed class OnboardingChecklistModel
{
    private readonly HashSet<string> _done = new();
    private readonly HashSet<string> _settled = new();
    private readonly HashSet<string> _pending = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<OnboardingChecklistStep> _steps = Array.Empty<OnboardingChecklistStep>();

    public IReadOnlyList<OnboardingChecklistStep> Steps => _steps;

    public OnboardingChecklistStrings Strings { get; set; } = OnboardingChecklistStrings.Default;

    public string? Expanded { get; private set; }

    public bool GroupOpen { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public int Total => _steps.Count;

    /// <summary>Done steps in step order; ids of steps not in the list do not count.</summary>
    public List<string> Done => Ordered(_done);

    public int DoneCount => Done.Count;

    public bool AllDone => Total > 0 && DoneCount == Total;

    public bool IsDone(string id) => _done.Contains(id);

    public bool IsSettled(string id) => _settled.Contains(id);

    public bool IsPending(string id) => _pending.Contains(id);

    public string? ErrorOf(string id) => _errors.GetValueOrDefault(id);

    public void Init(IReadOnlyList<OnboardingChecklistStep> steps, IEnumerable<string>? completed)
    {
        _steps = steps;
        SetDone(completed);
        Expanded = FirstOpen();
    }

    /// <summary>The step list changed: an open panel whose step is gone moves on, and nothing open opens the first unfinished step.</summary>
    public void SetSteps(IReadOnlyList<OnboardingChecklistStep> steps)
    {
        _steps = steps;
        if (Expanded is null || !HasStep(Expanded))
        {
            Expanded = FirstOpen();
        }
    }

    /// <summary>Code replaced the completed set: those steps are settled at once, as in <see cref="Init"/>.</summary>
    public void SetCompleted(IEnumerable<string>? completed)
    {
        SetDone(completed);
        _pending.ExceptWith(_done);
        foreach (var id in _done)
        {
            _errors.Remove(id);
        }
        if (Expanded is null || _done.Contains(Expanded))
        {
            Expanded = FirstOpen();
        }
    }

    public bool Check(string id)
    {
        if (_done.Contains(id) || !HasStep(id))
        {
            return false;
        }
        _done.Add(id);
        _pending.Remove(id);
        _errors.Remove(id);
        var count = DoneCount;
        Message = count == Total
            ? Format(Strings.AllDoneFormat, Total)
            : Format(Strings.StepDoneFormat, TitleOf(id), count, Total);
        return true;
    }

    public bool Uncheck(string id)
    {
        if (!_done.Remove(id))
        {
            return false;
        }
        _settled.Remove(id);
        Expanded ??= id;
        Message = Format(Strings.StepUndoneFormat, TitleOf(id), DoneCount, Total);
        return true;
    }

    public bool Settle(string id)
    {
        if (!_done.Contains(id) || !_settled.Add(id))
        {
            return false;
        }
        // The step that just finished hands the open panel to the next unfinished step, wrapping to the top.
        if (Expanded == id)
        {
            Expanded = NextOpenAfter(id);
        }
        return true;
    }

    public void Expand(string id) => Expanded = Expanded == id ? null : id;

    public void Start(string id)
    {
        _pending.Add(id);
        _errors.Remove(id);
    }

    public void Fail(string id, string message)
    {
        _pending.Remove(id);
        _errors[id] = message;
        Message = message;
    }

    public void ToggleGroup() => GroupOpen = !GroupOpen;

    private void SetDone(IEnumerable<string>? completed)
    {
        _done.Clear();
        _settled.Clear();
        if (completed is not null)
        {
            _done.UnionWith(completed);
            _settled.UnionWith(completed);
        }
    }

    private bool HasStep(string id)
    {
        foreach (var step in _steps)
        {
            if (step.Key == id)
            {
                return true;
            }
        }
        return false;
    }

    private string? FirstOpen() => _steps.FirstOrDefault(s => !_done.Contains(s.Key))?.Key;

    private string? NextOpenAfter(string id)
    {
        var count = _steps.Count;
        var start = 0;
        for (var i = 0; i < count; i++)
        {
            if (_steps[i].Key == id)
            {
                start = i;
                break;
            }
        }
        for (var offset = 1; offset <= count; offset++)
        {
            var key = _steps[(start + offset) % count].Key;
            if (!_done.Contains(key))
            {
                return key;
            }
        }
        return null;
    }

    private List<string> Ordered(HashSet<string> ids)
    {
        var list = new List<string>(ids.Count);
        foreach (var step in _steps)
        {
            if (ids.Contains(step.Key))
            {
                list.Add(step.Key);
            }
        }
        return list;
    }

    private string TitleOf(string id) => _steps.FirstOrDefault(s => s.Key == id)?.Title ?? "Step";

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
