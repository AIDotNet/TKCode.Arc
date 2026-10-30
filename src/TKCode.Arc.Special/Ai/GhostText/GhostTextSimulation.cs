using System.Runtime.CompilerServices;
using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Where the simulation of a <see cref="GhostText"/> with no <see cref="GhostText.Suggest"/> is.</summary>
internal enum GhostTextSampleState
{
    /// <summary>Not decided yet (the control has not been shown, or has no simulation).</summary>
    None,

    /// <summary>The simulation types, pauses and accepts on its own.</summary>
    Playing,

    /// <summary>It played once and stopped (reduced motion).</summary>
    Finished,

    /// <summary>The person clicked or focused it: the field is theirs, with the simulation's model.</summary>
    TakenOver,

    /// <summary>The app gave the field text or a suggest function: no simulation.</summary>
    Declined
}

/// <summary>
/// One line of a <see cref="GhostTextSimulation"/>: the words it types, and the suggestion that continues them.
/// </summary>
public sealed class GhostTextSimulationLine
{
    /// <summary>Creates an empty line; set <see cref="Typed"/> and <see cref="Reply"/>.</summary>
    public GhostTextSimulationLine()
    {
    }

    /// <summary>Creates a line from what is typed and the suggestion for it.</summary>
    public GhostTextSimulationLine(string typed, string reply)
    {
        Typed = typed;
        Reply = reply;
    }

    /// <summary>The words the simulation types (lines after the first follow a space).</summary>
    public string Typed { get; set; } = string.Empty;

    /// <summary>The suggestion streamed for them, usually starting with a space.</summary>
    public string Reply { get; set; } = string.Empty;
}

/// <summary>
/// A simulated session for a <see cref="GhostText"/>, for demos, prototypes and tests: it types each line of the
/// <see cref="Script"/>, pauses, lets the scripted suggestion stream in 3 to 6 word chunks after a short think, and accepts
/// it; lines alternate between Tab and a word at a time with Cmd+Right, then it rests and starts over. Every step goes
/// through the field's own handling (typing, asking, Tab, the word key), so it shows exactly what a person would see.
/// <para>
/// Set it as the field's <see cref="GhostText.Simulation"/>: it plays while the field has no <see cref="GhostText.Suggest"/>
/// and no text, at every motion level, pausing off screen; with reduced motion (or none) it plays once and stops. Clicking
/// or focusing the field hands it to the person, who then gets the scripted suggestions and, for anything else,
/// <see cref="Suggest"/>. Label, placeholder, footer and model stay on the field. Use one simulation per field.
/// </para>
/// </summary>
public sealed class GhostTextSimulation : AvaloniaObject, IDisposable
{
    /// <summary>The lines to type and the suggestions that continue them, in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<GhostTextSimulationLine>?> ScriptProperty =
        AvaloniaProperty.Register<GhostTextSimulation, IReadOnlyList<GhostTextSimulationLine>?>(nameof(Script));

    /// <summary>
    /// Suggestions for text the script does not cover, such as what the person types after taking the field over; null
    /// suggests nothing there.
    /// </summary>
    public static readonly StyledProperty<GhostTextSuggest?> SuggestProperty =
        AvaloniaProperty.Register<GhostTextSimulation, GhostTextSuggest?>(nameof(Suggest));

    private enum StepKind
    {
        Type,
        Wait,
        Ask,
        Tab,
        Word,
        Reset
    }

    private readonly record struct Step(StepKind Kind, string Text = "", double Seconds = 0);

    /// <summary>Seconds per typed character (with a small deterministic variation).</summary>
    internal const double CharacterSeconds = 0.045;

    /// <summary>The longest the script waits for a suggestion before it moves on.</summary>
    internal const double AskTimeout = 4;

    /// <summary>How long the scripted model "thinks" before its first chunk.</summary>
    internal static readonly TimeSpan ThinkTime = TimeSpan.FromMilliseconds(520);

    /// <summary>The pause between two streamed chunks.</summary>
    internal static readonly TimeSpan ChunkGap = TimeSpan.FromMilliseconds(90);

    private GhostTextSuggest? _answer;
    private GhostText? _owner;
    private LiveLoop? _loop;
    private Step[] _steps = [];
    private int _step;
    private double _elapsed;
    private int _typed;
    private bool _asked;
    private bool _running;

    /// <summary>Creates a simulation with nothing to type; set <see cref="Script"/>.</summary>
    public GhostTextSimulation()
    {
    }

    /// <inheritdoc cref="ScriptProperty"/>
    public IReadOnlyList<GhostTextSimulationLine>? Script { get => GetValue(ScriptProperty); set => SetValue(ScriptProperty, value); }

    /// <inheritdoc cref="SuggestProperty"/>
    public GhostTextSuggest? Suggest { get => GetValue(SuggestProperty); set => SetValue(SuggestProperty, value); }

    /// <summary>True while it plays: from when the field starts it until it stops or finishes.</summary>
    public bool IsRunning => _running;

    /// <summary>Raised when the script reached its end and stopped (it plays once with reduced motion).</summary>
    internal event Action? Finished;

    /// <summary>The index of the current step (tests).</summary>
    internal int StepIndex => _step;

    /// <summary>
    /// The function the field asks while this simulation is on it: the scripted suggestion when the text ends with a line
    /// of the script, otherwise <see cref="Suggest"/>.
    /// </summary>
    internal GhostTextSuggest Answer => _answer ??= (text, cancellationToken) =>
    {
        foreach (var line in Script ?? [])
        {
            if (line.Typed.Length > 0 && text.EndsWith(line.Typed, StringComparison.Ordinal))
            {
                return GhostTextResponse.FromStream(Stream(line.Reply, cancellationToken));
            }
        }
        return Suggest?.Invoke(text, cancellationToken);
    };

    /// <summary>Plays on <paramref name="owner"/>, with a clock gated by it.</summary>
    internal void Attach(GhostText owner)
    {
        Detach();
        _owner = owner;
        _loop = new LiveLoop(owner, Advance) { MinimumLevel = ArcMotionLevel.Off };
        _steps = BuildSteps();
        _step = 0;
        _elapsed = 0;
        _typed = 0;
        _asked = false;
    }

    /// <summary>Stops and lets go of the field.</summary>
    internal void Detach()
    {
        Stop();
        _loop?.Dispose();
        _loop = null;
        _owner = null;
    }

    internal void Start()
    {
        if (_running || _loop is null)
        {
            return;
        }
        _running = true;
        _loop.Start();
    }

    internal void Stop()
    {
        _running = false;
        _loop?.Stop();
    }

    /// <summary>Runs <paramref name="seconds"/> of the script by hand (tests).</summary>
    internal void Tick(double seconds) => Advance(seconds);

    /// <inheritdoc />
    public void Dispose() => Detach();

    /// <summary>Splits <paramref name="text"/> into chunks of 3 to 6 words (each keeps its leading space).</summary>
    internal static List<string> Chunks(string text)
    {
        var chunks = new List<string>();
        var start = 0;
        var words = 0;
        var target = 3;
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            words++;
            if (words >= target)
            {
                chunks.Add(text[start..i]);
                start = i;
                words = 0;
                target = 3 + chunks.Count * 2 % 4;
            }
        }
        if (start < text.Length)
        {
            chunks.Add(text[start..]);
        }
        return chunks;
    }

    private static async IAsyncEnumerable<string> Stream(string reply, [EnumeratorCancellation] CancellationToken token)
    {
        await Task.Delay(ThinkTime, token);
        var first = true;
        foreach (var chunk in Chunks(reply))
        {
            if (!first)
            {
                await Task.Delay(ChunkGap, token);
            }
            first = false;
            yield return chunk;
        }
    }

    /// <summary>
    /// The steps for the script: a short wait, then each line typed and asked for; even lines are accepted with Tab, odd
    /// lines a word at a time and then with Tab; a rest, and the field empties for the next loop.
    /// </summary>
    private Step[] BuildSteps()
    {
        var lines = Script ?? [];
        var steps = new List<Step> { new(StepKind.Wait, Seconds: 0.6) };
        for (var i = 0; i < lines.Count; i++)
        {
            steps.Add(new Step(StepKind.Type, (i > 0 ? " " : string.Empty) + lines[i].Typed));
            steps.Add(new Step(StepKind.Ask));
            if (i % 2 == 0)
            {
                steps.Add(new Step(StepKind.Wait, Seconds: 1.3));
                steps.Add(new Step(StepKind.Tab));
                if (i < lines.Count - 1)
                {
                    steps.Add(new Step(StepKind.Wait, Seconds: 0.9));
                }
            }
            else
            {
                steps.Add(new Step(StepKind.Wait, Seconds: 0.9));
                steps.Add(new Step(StepKind.Word));
                steps.Add(new Step(StepKind.Wait, Seconds: 0.45));
                steps.Add(new Step(StepKind.Word));
                steps.Add(new Step(StepKind.Wait, Seconds: 0.7));
                steps.Add(new Step(StepKind.Tab));
            }
        }
        steps.Add(new Step(StepKind.Wait, Seconds: 2.8));
        steps.Add(new Step(StepKind.Reset));
        return [.. steps];
    }

    private bool Advance(double dt)
    {
        if (!_running || _owner is null)
        {
            return false;
        }
        _elapsed += Math.Max(0, dt);
        // Several short steps may complete in one frame.
        for (var guard = 0; guard < 8 && _running; guard++)
        {
            if (!RunStep(_owner))
            {
                break;
            }
        }
        return _running;
    }

    /// <summary>Runs the current step; true when it finished and the next one may start in the same frame.</summary>
    private bool RunStep(GhostText owner)
    {
        var step = _steps[_step];
        switch (step.Kind)
        {
            case StepKind.Wait:
                if (_elapsed < step.Seconds)
                {
                    return false;
                }
                break;
            case StepKind.Type:
            {
                var due = 0.0;
                while (_typed < step.Text.Length)
                {
                    due += CharacterSeconds * (0.7 + (_typed * 7 % 6) * 0.12);
                    if (_elapsed < due)
                    {
                        return false;
                    }
                    _elapsed -= due;
                    due = 0;
                    owner.SimulateTyping(step.Text[_typed].ToString());
                    _typed++;
                }
                _typed = 0;
                break;
            }
            case StepKind.Ask:
                if (!_asked)
                {
                    _asked = true;
                    _elapsed = 0;
                    if (!owner.Request())
                    {
                        break;
                    }
                    return false;
                }
                if ((owner.Status == GhostTextStatus.Loading || owner.IsStreaming) && _elapsed < AskTimeout)
                {
                    return false;
                }
                break;
            case StepKind.Tab:
                owner.AcceptAll();
                break;
            case StepKind.Word:
                owner.AcceptWord();
                break;
            case StepKind.Reset:
                if (!ArcMotion.IsFull(owner))
                {
                    // Reduced motion: the preview plays once and stops.
                    Stop();
                    Finished?.Invoke();
                    return false;
                }
                owner.SimulateReset();
                break;
        }
        _asked = false;
        _elapsed = 0;
        _step = (_step + 1) % _steps.Length;
        return true;
    }
}

public partial class GhostText
{
    private GhostTextSimulation? _simulation;
    private GhostTextSampleState _sample;

    /// <summary>Where the simulation is (tests).</summary>
    internal GhostTextSampleState SampleState => _sample;

    /// <summary>The simulation playing on the field (or taken over), if any.</summary>
    internal GhostTextSimulation? PlayingSimulation => _simulation;

    /// <summary>True while the simulation is on screen (playing or finished, not yet taken over).</summary>
    internal bool IsSampling => Suggest is null && _sample is GhostTextSampleState.Playing or GhostTextSampleState.Finished;

    internal bool IsTakenOver => Suggest is null && _sample == GhostTextSampleState.TakenOver;

    /// <summary>Starts the simulation when the control is shown with one, no suggest function and no text.</summary>
    private void UpdateSimulation()
    {
        if (Suggest is not null)
        {
            if (_sample is GhostTextSampleState.Playing or GhostTextSampleState.Finished or GhostTextSampleState.None)
            {
                StopSimulation(takeOver: false);
            }
            return;
        }
        if (_simulation is not null && !ReferenceEquals(_simulation, Simulation))
        {
            // The simulation was removed or replaced: let it go and start fresh.
            ReleaseSimulation();
        }
        if (Simulation is null)
        {
            return;
        }
        if (_sample == GhostTextSampleState.None && _editor is not null && VisualRoot is not null)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                _sample = GhostTextSampleState.Declined;
                return;
            }
            _sample = GhostTextSampleState.Playing;
            _simulation = Simulation;
            _simulation.Attach(this);
            _simulation.Finished += OnSimulationFinished;
            UpdateParts();
            UpdateChip(force: true);
        }
        if (_sample == GhostTextSampleState.Playing && VisualRoot is not null)
        {
            _simulation?.Start();
        }
    }

    private void OnSimulationFinished() => _sample = GhostTextSampleState.Finished;

    /// <summary>Lets go of the simulation and empties what it typed; the field waits for a new one.</summary>
    private void ReleaseSimulation()
    {
        if (_simulation is null)
        {
            return;
        }
        var played = _sample is GhostTextSampleState.Playing or GhostTextSampleState.Finished;
        _simulation.Finished -= OnSimulationFinished;
        _simulation.Detach();
        _simulation = null;
        if (played)
        {
            SimulateReset();
        }
        if (_sample is not GhostTextSampleState.Declined)
        {
            _sample = GhostTextSampleState.None;
        }
        UpdateParts();
        UpdateChip(force: true);
    }

    /// <summary>Ends the simulation: taken over by the person (the field and its model are theirs) or declined by the app.</summary>
    private void StopSimulation(bool takeOver)
    {
        if (_sample is not (GhostTextSampleState.Playing or GhostTextSampleState.Finished or GhostTextSampleState.None))
        {
            return;
        }
        if (_sample == GhostTextSampleState.None && takeOver)
        {
            return;
        }
        var wasPlaying = _simulation?.IsRunning == true;
        _simulation?.Stop();
        _sample = takeOver ? GhostTextSampleState.TakenOver : GhostTextSampleState.Declined;
        if (wasPlaying)
        {
            CancelRequest();
            ClearSuggestion(fade: false);
            HideAccepted();
            _editor?.CaretToEnd();
        }
        UpdateParts();
        UpdateChip(force: true);
    }

    /// <summary>Types <paramref name="text"/> at the end as if from the keyboard (the simulation).</summary>
    internal void SimulateTyping(string text)
    {
        if (_editor is not { } editor)
        {
            return;
        }
        editor.CaretToEnd();
        editor.SimulateInput(text);
    }

    /// <summary>Empties the field for the next loop of the simulation.</summary>
    internal void SimulateReset()
    {
        CancelRequest();
        ClearSuggestion(fade: false);
        HideAccepted();
        if (_editor is { } editor)
        {
            editor.EndMerges();
            editor.Text = string.Empty;
        }
    }
}
