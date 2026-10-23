using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

public partial class MixtureOfExperts
{
    private MixtureOfExpertsInspectKind _hoverKind;
    private int _hoverIndex = -1;
    private MixtureOfExpertsInspectKind _focusKind;
    private int _focusIndex = -1;
    private MixtureOfExpertsInspectKind _pinKind;
    private int _pinIndex = -1;
    private bool _resumeOnUnpin;
    private MixtureOfExpertsInspectKind _inspectKind;
    private int _inspectIndex = -1;

    /// <summary>True while something is inspected: the story holds still.</summary>
    internal bool IsHeld => _inspectKind != MixtureOfExpertsInspectKind.None;

    /// <summary>What is pinned (tests).</summary>
    internal (MixtureOfExpertsInspectKind Kind, int Index) Pinned => (_pinKind, _pinIndex);

    internal void OnPartHover(MixtureOfExpertsPart part, bool over)
    {
        var kind = part.IsExpert ? MixtureOfExpertsInspectKind.Expert : MixtureOfExpertsInspectKind.Token;
        if (over)
        {
            _hoverKind = kind;
            _hoverIndex = part.Index;
            _hoverReleaseAt = -1;
            UpdateInspect();
        }
        else if (_hoverKind == kind && _hoverIndex == part.Index)
        {
            // Let go a moment later, so crossing the 4 px gap to the next token or tile does not flash the live scene.
            _hoverReleaseAt = WallTime + HoverGrace;
            RequestFrames(HoverGrace * 2);
        }
    }

    /// <summary>How long a hover outlives the pointer leaving its part (wall seconds).</summary>
    internal const double HoverGrace = 0.06;

    private double _hoverReleaseAt = -1;

    /// <summary>Releases a hover whose grace has run out (called every frame).</summary>
    private void ReleaseExpiredHover()
    {
        if (_hoverReleaseAt >= 0 && WallTime >= _hoverReleaseAt)
        {
            _hoverReleaseAt = -1;
            _hoverKind = MixtureOfExpertsInspectKind.None;
            _hoverIndex = -1;
            UpdateInspect();
        }
    }

    internal void OnPartFocus(MixtureOfExpertsPart part, bool focused, NavigationMethod method)
    {
        var kind = part.IsExpert ? MixtureOfExpertsInspectKind.Expert : MixtureOfExpertsInspectKind.Token;
        if (focused)
        {
            if (part.IsExpert)
            {
                if (_stage is not null)
                {
                    KeyboardNavigation.SetTabOnceActiveElement(_stage, part);
                }
            }
            else if (_sequence is not null)
            {
                KeyboardNavigation.SetTabOnceActiveElement(_sequence, part);
            }
            // Only keyboard focus inspects; focus that follows a click does not.
            if (method is NavigationMethod.Tab or NavigationMethod.Directional)
            {
                _focusKind = kind;
                _focusIndex = part.Index;
            }
        }
        else if (_focusKind == kind && _focusIndex == part.Index)
        {
            _focusKind = MixtureOfExpertsInspectKind.None;
            _focusIndex = -1;
        }
        UpdateInspect();
    }

    internal void TogglePin(MixtureOfExpertsPart part)
    {
        var kind = part.IsExpert ? MixtureOfExpertsInspectKind.Expert : MixtureOfExpertsInspectKind.Token;
        if (_pinKind == kind && _pinIndex == part.Index)
        {
            ReleasePin(announce: true);
        }
        else
        {
            Pin(kind, part.Index);
        }
    }

    internal void OnPartKey(MixtureOfExpertsPart part, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                TogglePin(part);
                e.Handled = true;
                break;
            case Key.Escape:
                if (_pinKind != MixtureOfExpertsInspectKind.None)
                {
                    ReleasePin(announce: true);
                    e.Handled = true;
                }
                break;
            case Key.Right:
            case Key.Down:
                e.Handled = MoveFocus(part, part.Index + 1);
                break;
            case Key.Left:
            case Key.Up:
                e.Handled = MoveFocus(part, part.Index - 1);
                break;
            case Key.Home:
                e.Handled = MoveFocus(part, 0);
                break;
            case Key.End:
                e.Handled = MoveFocus(part, int.MaxValue);
                break;
        }
    }

    /// <summary>Moves keyboard focus within the part's group (no wrapping).</summary>
    private bool MoveFocus(MixtureOfExpertsPart part, int index)
    {
        IReadOnlyList<MixtureOfExpertsPart> group = part.IsExpert
            ? (IReadOnlyList<MixtureOfExpertsPart>?)_stage?.Tiles ?? []
            : (IReadOnlyList<MixtureOfExpertsPart>?)_sequence?.Tokens ?? [];
        if (group.Count == 0)
        {
            return false;
        }
        index = Math.Clamp(index, 0, group.Count - 1);
        if (index != part.Index)
        {
            group[index].Focus(NavigationMethod.Directional);
        }
        return true;
    }

    private void Pin(MixtureOfExpertsInspectKind kind, int index)
    {
        var wasPinned = _pinKind != MixtureOfExpertsInspectKind.None;
        _pinKind = kind;
        _pinIndex = index;
        if (!wasPinned && IsPlaying && !IsFinished)
        {
            // Pinning pauses the story (the play button offers to resume); releasing the pin plays it on.
            _resumeOnUnpin = true;
            Pause();
        }
        UpdateInspect();
        Announce(Labels.Pinned(kind == MixtureOfExpertsInspectKind.Token ? SpokenToken(index) : _experts[index].Name));
    }

    private void ReleasePin(bool announce)
    {
        if (_pinKind == MixtureOfExpertsInspectKind.None)
        {
            return;
        }
        _pinKind = MixtureOfExpertsInspectKind.None;
        _pinIndex = -1;
        var resume = _resumeOnUnpin;
        _resumeOnUnpin = false;
        UpdateInspect();
        if (resume && !IsPlaying)
        {
            Play();
        }
        if (announce)
        {
            Announce(resume ? Labels.UnpinnedResumed : Labels.Unpinned);
        }
    }

    /// <summary>
    /// Works out what is inspected (a pin wins over keyboard focus, which wins over the pointer), holds or releases the
    /// story, and shows it.
    /// </summary>
    private void UpdateInspect()
    {
        var (kind, index) = _pinKind != MixtureOfExpertsInspectKind.None ? (_pinKind, _pinIndex)
            : _focusKind != MixtureOfExpertsInspectKind.None ? (_focusKind, _focusIndex)
            : (_hoverKind, _hoverIndex);
        if (_stage is not null)
        {
            foreach (var tile in _stage.Tiles)
            {
                tile.SetPinned(_pinKind == MixtureOfExpertsInspectKind.Expert && _pinIndex == tile.Index);
            }
        }
        if (_sequence is not null)
        {
            foreach (var token in _sequence.Tokens)
            {
                token.SetPinned(_pinKind == MixtureOfExpertsInspectKind.Token && _pinIndex == token.Index);
            }
        }
        if (kind == _inspectKind && index == _inspectIndex)
        {
            return;
        }
        _inspectKind = kind;
        _inspectIndex = index;
        PseudoClasses.Set(InspectingPseudoClass, kind != MixtureOfExpertsInspectKind.None);
        InspectedTokenIndex = kind == MixtureOfExpertsInspectKind.Token ? index : -1;
        InspectedExpertIndex = kind == MixtureOfExpertsInspectKind.Expert ? index : -1;
        RefreshScene(force: true);
    }

    /// <summary>A token as spoken: its text without the surrounding spaces (a lone space stays a space).</summary>
    private string SpokenToken(int index)
    {
        var text = index >= 0 && index < _tokens.Count ? _tokens[index].Text : string.Empty;
        var trimmed = text.Trim();
        return trimmed.Length > 0 ? trimmed : text;
    }

    /// <summary>Names every sequence token by its planned route ("Ada: People 71%, Syntax 29%").</summary>
    private void UpdateTokenNames()
    {
        if (_sequence is null)
        {
            return;
        }
        var labels = Labels;
        var tokens = _sequence.Tokens;
        for (var t = 0; t < tokens.Count && t < _plan.Routes.Length; t++)
        {
            var route = _plan.Routes[t];
            var parts = new string[route.Experts.Count];
            for (var r = 0; r < parts.Length; r++)
            {
                var e = route.Experts[r];
                var name = e < _experts.Count ? _experts[e].Name : string.Empty;
                parts[r] = route.IsDropped(e) ? labels.DroppedSpoken(name) : labels.ChoiceSpoken(name, MixtureOfExpertsPlanner.Percent(route.Weights[r]));
            }
            AutomationProperties.SetName(tokens[t], labels.TokenSpoken(SpokenToken(t), string.Join(", ", parts)));
        }
    }

    /// <summary>Names every expert tile by its load, whether it is full and its part in the route on show.</summary>
    private void UpdateExpertNames(MixtureOfExpertsScene scene, MixtureOfExpertsMoment moment)
    {
        if (_stage is null)
        {
            return;
        }
        var labels = Labels;
        var loads = _plan.LoadsAfter(scene.Counted);
        var shownToken = _inspectKind == MixtureOfExpertsInspectKind.Token ? _inspectIndex
            : !moment.IsSummary && MixtureOfExpertsTimeline.IsLit(moment) ? moment.Token : -1;
        var route = shownToken >= 0 && shownToken < _plan.Routes.Length ? _plan.Routes[shownToken] : null;
        var tiles = _stage.Tiles;
        for (var e = 0; e < tiles.Count && e < loads.Count; e++)
        {
            var name = labels.ExpertSpoken(_experts[e].Name, loads[e]);
            if (!_plan.IsUnlimited && loads[e] >= _plan.Capacity)
            {
                name += ", " + labels.AtCapacity;
            }
            var rank = route?.RankOf(e) ?? -1;
            if (route is not null && rank >= 0)
            {
                var token = SpokenToken(shownToken);
                name += ", " + (route.IsDropped(e) ? labels.SkippedFor(token) : rank == 0 ? labels.FirstChoiceFor(token) : labels.SecondChoiceFor(token));
            }
            AutomationProperties.SetName(tiles[e], name);
        }
    }
}
