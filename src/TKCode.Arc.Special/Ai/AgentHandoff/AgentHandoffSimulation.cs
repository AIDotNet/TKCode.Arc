using Avalonia;
using Avalonia.Collections;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A simulated support chat for <see cref="AgentHandoff"/> (Arc's <c>useHandoffSimulation</c>), for demos, prototypes and
/// tests: it plays the <see cref="Script"/> you pass and exposes the stage, the messages, who is typing, the summary, the
/// person and a notice, and takes the same actions as the view (talk to a person, cancel, try again, send).
/// <para>
/// Set it as the card's <see cref="AgentHandoff.Simulation"/>: the card plays it while its own <see cref="AgentHandoff.Stage"/>
/// is unset, from the top each time it is shown. The customer opens, the assistant streams its answers in chunks of 3 to
/// 8 words about every 34ms, the customer asks for a person and the assistant hands off with a summary. In
/// <see cref="AgentHandoffScenario.Handoff"/> a person joins, resolves the issue and the chat, and the story starts over a
/// few seconds later; in <see cref="AgentHandoffScenario.Unavailable"/> nobody picks the transfer up and it waits for
/// <see cref="Retry"/>. Reduced motion: replies appear whole and the story plays once. The card's clock runs at every
/// motion level but only while the card is on screen, so the story pauses off screen. Use one simulation per card.
/// </para>
/// </summary>
public sealed class AgentHandoffSimulation : AvaloniaObject, IDisposable
{
    private const string HandoffCue = "handoff";
    private const string ConnectCue = "connect";

    /// <summary>Which story to play: a handoff a person picks up, or one nobody picks up. A change starts the story over.</summary>
    public static readonly StyledProperty<AgentHandoffScenario> ScenarioProperty =
        AvaloniaProperty.Register<AgentHandoffSimulation, AgentHandoffScenario>(nameof(Scenario));

    /// <summary>The words of the chat; null plays an empty chat that still moves through the stages.</summary>
    public static readonly StyledProperty<AgentHandoffScript?> ScriptProperty =
        AvaloniaProperty.Register<AgentHandoffSimulation, AgentHandoffScript?>(nameof(Script));

    private static readonly AgentHandoffScript s_silent = new();

    private LiveLoop? _loop;
    private readonly Random _random = new(4821);
    private readonly List<Cue> _cues = [];
    private int _index;
    private int _generation;
    private double _wait;
    private bool _running;
    private Stream? _stream;
    private AgentHandoffStage _stage;
    private AgentHandoffTyping _typing;
    private AgentHandoffSummary? _summary;
    private AgentHandoffAgent? _agent;
    private string? _notice;

    /// <summary>Creates a simulation; set <see cref="Script"/> and give it to an <see cref="AgentHandoff"/>.</summary>
    public AgentHandoffSimulation()
    {
    }

    /// <summary>A simulation without a clock: the caller advances it (tests).</summary>
    internal AgentHandoffSimulation(AgentHandoffScenario scenario, bool reduced, AgentHandoffScript? script)
    {
        Scenario = scenario;
        IsReduced = reduced;
        Script = script;
    }

    /// <summary>Raised when the stage, the typing state, the summary, the person or the notice changes. Messages are observable on their own.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="ScenarioProperty"/>
    public AgentHandoffScenario Scenario { get => GetValue(ScenarioProperty); set => SetValue(ScenarioProperty, value); }

    /// <inheritdoc cref="ScriptProperty"/>
    public AgentHandoffScript? Script { get => GetValue(ScriptProperty); set => SetValue(ScriptProperty, value); }

    /// <summary>Whether replies appear whole and the story plays once (the card sets it from its motion level).</summary>
    public bool IsReduced { get; internal set; }

    /// <summary>Where the conversation is.</summary>
    public AgentHandoffStage Stage => _stage;

    /// <summary>The conversation with the assistant, before the handoff.</summary>
    public AvaloniaList<AgentHandoffMessage> Messages { get; } = [];

    /// <summary>Everything after the handoff card.</summary>
    public AvaloniaList<AgentHandoffMessage> AgentMessages { get; } = [];

    /// <summary>Who is typing.</summary>
    public AgentHandoffTyping Typing => _typing;

    /// <summary>What the assistant passes to the person, from the handoff on.</summary>
    public AgentHandoffSummary? Summary => _summary;

    /// <summary>The person, once they join.</summary>
    public AgentHandoffAgent? Agent => _agent;

    /// <summary>The latest one-off message for screen readers.</summary>
    public string? Notice => _notice;

    /// <summary>Whether the story is playing (false once stopped).</summary>
    public bool IsPlaying => _running;

    /// <summary>Whether the story waits for the person (after cancelling, or when nobody is available).</summary>
    public bool IsWaiting => _running && _stream is null && _index >= _cues.Count;

    /// <summary>Starts the story from the beginning.</summary>
    public void Start()
    {
        Reset();
        _running = true;
        Play(Cues(), 0);
    }

    /// <summary>Stops the story where it is.</summary>
    public void Stop()
    {
        _running = false;
        _loop?.Stop();
    }

    /// <summary>
    /// The customer asks for a person, saying <paramref name="message"/> (the words of the quick reply they pressed;
    /// nothing is added when null): the assistant hands off. Only while the stage is Assistant.
    /// </summary>
    public void RequestHuman(string? message = null)
    {
        if (!_running || _stage != AgentHandoffStage.Assistant)
        {
            return;
        }
        FinishStream();
        SetTyping(AgentHandoffTyping.None);
        Add(Messages, AgentHandoffSender.Customer, message);
        Play(Cues(), IndexOf(HandoffCue));
    }

    /// <summary>The customer cancels the transfer: back to the assistant (only while transferring).</summary>
    public void CancelTransfer()
    {
        if (!_running || _stage != AgentHandoffStage.Transferring)
        {
            return;
        }
        FinishStream();
        AgentMessages.Clear();
        _summary = null;
        _stage = AgentHandoffStage.Assistant;
        _typing = AgentHandoffTyping.None;
        RaiseChanged();
        Play(
        [
            new Cue(0.3, () => SetTyping(AgentHandoffTyping.Assistant)),
            new Cue(0.7, () => StreamAssistant(Messages, Lines.AssistantCancelled)),
        ], 0);
    }

    /// <summary>Tries the transfer again: someone picks it up (only when nobody was available).</summary>
    public void Retry()
    {
        if (!_running || _stage != AgentHandoffStage.Unavailable)
        {
            return;
        }
        SetStage(AgentHandoffStage.Transferring);
        Play(Cues(), IndexOf(ConnectCue));
    }

    /// <summary>The customer sends <paramref name="text"/> (trimmed; empty text is ignored), and whoever is in the chat answers.</summary>
    public void Send(string? text)
    {
        var trimmed = text?.Trim();
        if (!_running || string.IsNullOrEmpty(trimmed))
        {
            return;
        }
        FinishStream();
        var message = new AgentHandoffMessage(AgentHandoffSender.Customer, trimmed);
        switch (_stage)
        {
            case AgentHandoffStage.Assistant:
                SetTyping(AgentHandoffTyping.None);
                Messages.Add(message);
                Play(
                [
                    new Cue(0.4, () => SetTyping(AgentHandoffTyping.Assistant)),
                    new Cue(0.9, () => StreamAssistant(Messages, Lines.AssistantReply)),
                ], 0);
                break;
            case AgentHandoffStage.Transferring:
                // A note for the team while waiting; the story goes on.
                AgentMessages.Add(message);
                break;
            case AgentHandoffStage.Unavailable:
                AgentMessages.Add(message);
                Play(
                [
                    new Cue(0.4, () => SetTyping(AgentHandoffTyping.Assistant)),
                    new Cue(0.9, () => StreamAssistant(AgentMessages, Lines.AssistantReply)),
                ], 0);
                break;
            default:
                SetTyping(AgentHandoffTyping.None);
                AgentMessages.Add(message);
                Play(
                [
                    new Cue(0.5, () => SetTyping(AgentHandoffTyping.Agent)),
                    new Cue(1.4, () => SayAgent(Lines.AgentReply)),
                ], 0);
                break;
        }
    }

    /// <summary>Moves the story on by <paramref name="seconds"/> (the clock calls it every frame; tests call it directly).</summary>
    internal void Advance(double seconds)
    {
        if (!_running || seconds <= 0)
        {
            return;
        }
        if (_stream is { } stream)
        {
            stream.Elapsed += seconds;
            while (stream.Elapsed >= AgentHandoffMotion.ChunkIntervalSeconds && _stream is not null)
            {
                stream.Elapsed -= AgentHandoffMotion.ChunkIntervalSeconds;
                EmitChunk(stream);
            }
            if (_stream is not null)
            {
                return;
            }
            _wait = _index < _cues.Count ? _cues[_index].Delay : 0;
            return;
        }
        _wait -= seconds;
        while (_running && _stream is null && _index < _cues.Count && _wait <= 0)
        {
            var cue = _cues[_index++];
            var generation = _generation;
            cue.Act?.Invoke();
            if (generation == _generation && _stream is null && _index < _cues.Count)
            {
                _wait += _cues[_index].Delay;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        Detach();
    }

    /// <summary>Gives the story a clock gated by <paramref name="owner"/> (the card that shows it): it runs while the owner is on screen.</summary>
    internal void Attach(Visual owner)
    {
        Detach();
        _loop = new LiveLoop(owner, seconds =>
        {
            Advance(seconds);
            return _running && (_stream is not null || _index < _cues.Count);
        })
        { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <summary>Lets go of the owner's clock.</summary>
    internal void Detach()
    {
        _loop?.Dispose();
        _loop = null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScenarioProperty && _running)
        {
            Start();
        }
    }

    private AgentHandoffScript Lines => Script ?? s_silent;

    private void Reset()
    {
        _stream = null;
        Messages.Clear();
        AgentMessages.Clear();
        _stage = AgentHandoffStage.Assistant;
        _typing = AgentHandoffTyping.None;
        _summary = null;
        _agent = null;
        _notice = null;
        RaiseChanged();
    }

    private void Play(List<Cue> cues, int from)
    {
        _generation++;
        _cues.Clear();
        _cues.AddRange(cues);
        _index = Math.Clamp(from, 0, _cues.Count);
        _wait = _index < _cues.Count ? _cues[_index].Delay : 0;
        _loop?.Start();
    }

    private int IndexOf(string label)
    {
        var index = Cues().FindIndex(c => c.Label == label);
        return index < 0 ? 0 : index;
    }

    private List<Cue> Cues()
    {
        var lines = Lines;
        var cues = new List<Cue>
        {
            new(0.6, () => Add(Messages, AgentHandoffSender.Customer, lines.CustomerOpening)),
            new(0.5, () => SetTyping(AgentHandoffTyping.Assistant)),
            new(1.1, () => StreamAssistant(Messages, lines.AssistantAnswer)),
            new(1.6, () => Add(Messages, AgentHandoffSender.Customer, lines.CustomerRequest)),
            new(0.5, () => SetTyping(AgentHandoffTyping.Assistant), HandoffCue),
            new(0.9, () => StreamAssistant(Messages, lines.AssistantHandoff)),
            new(0.5, () =>
            {
                _summary = lines.Summary;
                SetStage(AgentHandoffStage.Transferring);
            }),
            new(2.0, () => Add(AgentMessages, AgentHandoffSender.Customer, lines.CustomerNote)),
        };
        if (Scenario == AgentHandoffScenario.Unavailable)
        {
            cues.Add(new Cue(4.0, () => SetStage(AgentHandoffStage.Unavailable)));
            cues.Add(new Cue(0, Halt));
        }
        cues.AddRange(
        [
            new(2.4, () =>
            {
                _agent = lines.Agent;
                SetStage(AgentHandoffStage.Connected);
            }, ConnectCue),
            new(0.6, () => SetTyping(AgentHandoffTyping.Agent)),
            new(1.8, () => SayAgent(lines.AgentGreeting)),
            new(0.8, () => SetTyping(AgentHandoffTyping.Agent)),
            new(2.0, () =>
            {
                SayAgent(lines.AgentResolution);
                _notice = lines.ResolutionNotice;
                RaiseChanged();
            }),
            new(1.6, () => Add(AgentMessages, AgentHandoffSender.Customer, lines.CustomerThanks)),
            new(0.6, () => SetTyping(AgentHandoffTyping.Agent)),
            new(1.6, () => SayAgent(lines.AgentClosing)),
            new(1.2, () => SetStage(AgentHandoffStage.Resolved)),
            new(6.0, () =>
            {
                // The loop: full motion starts over, reduced motion plays once.
                if (!IsReduced)
                {
                    Start();
                }
            }),
        ]);
        return cues;
    }

    private void Halt() => _index = _cues.Count;

    private void SetStage(AgentHandoffStage stage)
    {
        _typing = AgentHandoffTyping.None;
        _stage = stage;
        RaiseChanged();
    }

    private void SetTyping(AgentHandoffTyping typing)
    {
        if (_typing == typing)
        {
            return;
        }
        _typing = typing;
        RaiseChanged();
    }

    private static void Add(AvaloniaList<AgentHandoffMessage> list, AgentHandoffSender from, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            list.Add(new AgentHandoffMessage(from, text));
        }
    }

    private void SayAgent(string? text)
    {
        SetTyping(AgentHandoffTyping.None);
        Add(AgentMessages, AgentHandoffSender.Agent, text);
    }

    private void StreamAssistant(AvaloniaList<AgentHandoffMessage> list, string? text)
    {
        SetTyping(AgentHandoffTyping.None);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (IsReduced)
        {
            list.Add(new AgentHandoffMessage(AgentHandoffSender.Assistant, text));
            return;
        }
        var message = new AgentHandoffMessage(AgentHandoffSender.Assistant, string.Empty) { IsStreaming = true };
        var stream = new Stream(message, text.Split(' '));
        _stream = stream;
        EmitChunk(stream);
        list.Add(message);
    }

    /// <summary>Appends the next 3 to 8 words; the last chunk ends the stream.</summary>
    private void EmitChunk(Stream stream)
    {
        var count = _random.Next(AgentHandoffMotion.ChunkMinWords, AgentHandoffMotion.ChunkMaxWords + 1);
        var end = Math.Min(stream.Words.Length, stream.Next + count);
        var chunk = string.Join(' ', stream.Words, stream.Next, end - stream.Next);
        stream.Message.Text = string.IsNullOrEmpty(stream.Message.Text) ? chunk : stream.Message.Text + " " + chunk;
        stream.Next = end;
        if (end >= stream.Words.Length)
        {
            stream.Message.IsStreaming = false;
            _stream = null;
        }
    }

    private void FinishStream()
    {
        if (_stream is not { } stream)
        {
            return;
        }
        stream.Message.Text = string.Join(' ', stream.Words);
        stream.Message.IsStreaming = false;
        _stream = null;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed record Cue(double Delay, Action? Act, string? Label = null);

    private sealed class Stream(AgentHandoffMessage message, string[] words)
    {
        public AgentHandoffMessage Message { get; } = message;

        public string[] Words { get; } = words;

        public int Next { get; set; }

        public double Elapsed { get; set; }
    }
}
