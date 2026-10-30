using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class SourceCards
{
    private SourceCardsSimulation? _hooked;
    private SourceCardsSimulation? _playing;
    private TextBlock? _simulatedText;
    private int _answerShown;
    private LiveLoop? _simulationLoop;
    private bool _simulatedPending;

    /// <summary>Whether the cards play their <see cref="Simulation"/>: one is set and <see cref="Sources"/> is null.</summary>
    internal bool IsSimulating => Sources is null && Simulation is not null;

    /// <summary>The simulation on screen, or null.</summary>
    internal SourceCardsSimulation? ActiveSimulation => Sources is null ? Simulation : null;

    /// <summary>The simulated answer, once a word of it has streamed.</summary>
    internal TextBlock? SimulatedAnswerText => _simulatedText;

    /// <summary>Advances the simulation by <paramref name="seconds"/> (the loop's frame; tests call it by hand).</summary>
    internal bool StepSimulation(double seconds) => ActiveSimulation is { } simulation && simulation.Advance(seconds);

    /// <summary>
    /// Takes the simulation up when it is due (set, no sources, on screen), starting it the first time, or lets it go once
    /// sources are given or it is removed. The cards follow it only while attached, so a detached card holds no handler.
    /// </summary>
    private void UpdateSimulation()
    {
        var wanted = IsAttached ? ActiveSimulation : null;
        if (!ReferenceEquals(wanted, _hooked))
        {
            if (_hooked is not null)
            {
                _hooked.Changed -= OnSimulationChanged;
                _hooked.Cued -= OnSimulationCued;
            }
            _hooked = wanted;
            if (_hooked is not null)
            {
                _hooked.Changed += OnSimulationChanged;
                _hooked.Cued += OnSimulationCued;
            }
        }
        if (!ReferenceEquals(ActiveSimulation, _playing))
        {
            _playing = ActiveSimulation;
            ClearSimulatedAnswer();
        }
        if (wanted is null)
        {
            _simulationLoop?.Stop();
            return;
        }
        if (!wanted.HasStarted)
        {
            // Said once, as soon as the live region exists (the story may start before the template is applied).
            _simulatedPending = true;
            AnnounceSimulated();
            wanted.Restart();
        }
        _simulationLoop ??= new LiveLoop(this, StepSimulation) { MinimumLevel = ArcMotionLevel.Off };
        _simulationLoop.Start();
    }

    private void OnSimulationChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, ActiveSimulation))
        {
            return;
        }
        // A story that starts over (a new scenario, Retry) wants the clock again.
        _simulationLoop?.Start();
        SyncSimulatedAnswer();
        Refresh();
    }

    private void OnSimulationCued(SourceCardsSimulationCue cue)
    {
        switch (cue)
        {
            case SourceCardsSimulationCue.Reset:
                if (IsExpanded)
                {
                    SetCurrentValue(IsExpandedProperty, false);
                }
                if (Highlighted is not null)
                {
                    SetCurrentValue(HighlightedProperty, null);
                }
                break;
            case SourceCardsSimulationCue.FirstTour:
                Tour(2);
                break;
            case SourceCardsSimulationCue.SecondTour:
                // A card inside the stack, so the tour shows a citation opening it.
                Tour(Max + 2);
                break;
            case SourceCardsSimulationCue.Collapse:
                SetCurrentValue(IsExpandedProperty, false);
                break;
        }
    }

    private void Tour(int number)
    {
        var count = DisplayedSources.Count;
        if (count > 0)
        {
            Highlight(Math.Min(number, count), fromCitation: false);
        }
    }

    private void AnnounceSimulated()
    {
        if (_simulatedPending && _announcer is not null)
        {
            _simulatedPending = false;
            _announcer.Announce(Labels.AnnounceSimulated);
        }
    }

    private void ClearSimulatedAnswer()
    {
        _answerShown = 0;
        _simulatedText?.Inlines?.Clear();
        RefreshAnswer();
    }

    /// <summary>Brings the simulated answer in line with the words that have streamed, appending only the new ones.</summary>
    private void SyncSimulatedAnswer()
    {
        if (ActiveSimulation is not { } simulation)
        {
            return;
        }
        if (simulation.AnswerShown < _answerShown)
        {
            ClearSimulatedAnswer();
        }
        while (_answerShown < simulation.AnswerShown && _answerShown < simulation.AnswerWords.Count)
        {
            AppendAnswer(simulation.AnswerWords[_answerShown++]);
        }
        RefreshAnswer();
    }

    /// <summary>Appends one word (or an [n] citation chip) of the simulated answer.</summary>
    private void AppendAnswer(string token)
    {
        if (_simulatedText is null)
        {
            _simulatedText = new TextBlock { Classes = { "sample-answer" }, TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
            if (_sampleAnswer is not null)
            {
                _sampleAnswer.Content = _simulatedText;
            }
        }
        var inlines = _simulatedText.Inlines ??= [];
        if (SourceCardsFormat.CitationOf(token, out var trailing) is { } number)
        {
            inlines.Add(new InlineUIContainer(new SourceCitation { Number = number }) { BaselineAlignment = BaselineAlignment.Center });
            inlines.Add(new Run(trailing + " "));
        }
        else
        {
            inlines.Add(new Run(token + " "));
        }
    }
}
