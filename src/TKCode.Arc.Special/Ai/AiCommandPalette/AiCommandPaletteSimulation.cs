using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// An action an <see cref="AiCommandPaletteSimulationAnswer"/> offers: pressing it pretends to run for 1.1s, then reports
/// <see cref="Result"/> with "Simulated, nothing was sent".
/// </summary>
public sealed class AiCommandPaletteSimulationAction
{
    /// <summary>Creates an empty action; set <see cref="Id"/> and <see cref="Label"/>.</summary>
    public AiCommandPaletteSimulationAction()
    {
    }

    /// <summary>Creates an action with its label, a muted meta, its mark and what it reports when done.</summary>
    public AiCommandPaletteSimulationAction(string id, string label, string? meta, Geometry? mark, string result)
    {
        Id = id;
        Label = label;
        Meta = meta;
        Mark = mark;
        Result = result;
    }

    /// <summary>A key unique within the answer.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What the action does, such as "Create issue".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>A muted note after the label, such as where it goes.</summary>
    public string? Meta { get; set; }

    /// <summary>The action's mark, a 24 × 24 stroked glyph.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>What the action reports once it has pretended to run, such as "Created issue 2481".</summary>
    public string Result { get; set; } = string.Empty;
}

/// <summary>
/// A canned answer of an <see cref="AiCommandPaletteSimulation"/>: the question words it answers, its sources, its text
/// with [n] citations and its actions.
/// </summary>
public sealed class AiCommandPaletteSimulationAnswer
{
    /// <summary>
    /// Word starts that pick this answer, separated by spaces ("invoice acme fail"): the answer whose keywords the question
    /// mentions most is played. An answer without keywords answers any question nothing else matches.
    /// </summary>
    public string? Keywords { get; set; }

    /// <summary>The sources, shown after the simulated search.</summary>
    public IReadOnlyList<AiCommandPaletteSource>? Sources { get; set; }

    /// <summary>The answer; [n] cites source n, a blank line splits paragraphs, and <c>{question}</c> is replaced by the question.</summary>
    public string? Text { get; set; }

    /// <summary>The actions offered under the answer.</summary>
    public IReadOnlyList<AiCommandPaletteSimulationAction>? Actions { get; set; }
}

/// <summary>
/// A simulated workspace for an <see cref="AiCommandPalette"/>, for demos, prototypes and tests: the pages and actions to
/// search (<see cref="Items"/>), the questions offered in Ask AI (<see cref="Questions"/>), and a simulated model that
/// answers from <see cref="Answers"/>. It "searches" for 700ms, lands the sources, waits 250ms and streams the text in
/// small uneven pieces every 45ms, then offers the actions, which pretend to run and say "Simulated, nothing was sent".
/// Nothing talks to any service.
/// <para>
/// Set it as the palette's <see cref="AiCommandPalette.Simulation"/>. Each part fills in only what the palette lacks:
/// <see cref="Items"/> while the palette has no items, <see cref="Questions"/> while it has no suggested questions, and
/// the answers while it has no responder. While the palette has neither items nor a responder of its own it also plays
/// the walkthrough (unless <see cref="AiCommandPalette.AutoPlay"/> says otherwise): it searches for
/// <see cref="SearchQuery"/>, opens Ask AI, asks the first question, runs its first action and starts over.
/// </para>
/// </summary>
public sealed class AiCommandPaletteSimulation : AvaloniaObject
{
    /// <summary>The pages and actions to search.</summary>
    public static readonly StyledProperty<IReadOnlyList<AiCommandPaletteItem>?> ItemsProperty =
        AvaloniaProperty.Register<AiCommandPaletteSimulation, IReadOnlyList<AiCommandPaletteItem>?>(nameof(Items));

    /// <summary>The questions offered while Ask AI is open and empty; the walkthrough asks the first.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> QuestionsProperty =
        AvaloniaProperty.Register<AiCommandPaletteSimulation, IReadOnlyList<string>?>(nameof(Questions));

    /// <summary>The canned answers the simulated model picks from.</summary>
    public static readonly StyledProperty<IReadOnlyList<AiCommandPaletteSimulationAnswer>?> AnswersProperty =
        AvaloniaProperty.Register<AiCommandPaletteSimulation, IReadOnlyList<AiCommandPaletteSimulationAnswer>?>(nameof(Answers));

    /// <summary>What the walkthrough types into the search before it looks through the results; null skips the search.</summary>
    public static readonly StyledProperty<string?> SearchQueryProperty =
        AvaloniaProperty.Register<AiCommandPaletteSimulation, string?>(nameof(SearchQuery));

    /// <summary>Creates a simulation with an empty workspace; set its parts.</summary>
    public AiCommandPaletteSimulation()
    {
    }

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<AiCommandPaletteItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="QuestionsProperty"/>
    public IReadOnlyList<string>? Questions { get => GetValue(QuestionsProperty); set => SetValue(QuestionsProperty, value); }

    /// <inheritdoc cref="AnswersProperty"/>
    public IReadOnlyList<AiCommandPaletteSimulationAnswer>? Answers { get => GetValue(AnswersProperty); set => SetValue(AnswersProperty, value); }

    /// <inheritdoc cref="SearchQueryProperty"/>
    public string? SearchQuery { get => GetValue(SearchQueryProperty); set => SetValue(SearchQueryProperty, value); }

    /// <summary>The answer for a question: the one whose keywords it mentions most, else one without keywords, else none.</summary>
    internal AiCommandPaletteSimulationAnswer? Pick(string question)
    {
        var words = AiCommandPaletteSearch.Words(question);
        AiCommandPaletteSimulationAnswer? best = null;
        AiCommandPaletteSimulationAnswer? fallback = null;
        var bestScore = 0;
        foreach (var answer in Answers ?? [])
        {
            var keys = (answer.Keywords ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (keys.Length == 0)
            {
                fallback ??= answer;
                continue;
            }
            var score = keys.Count(k => words.Any(w => w.StartsWith(k.ToLowerInvariant(), StringComparison.Ordinal)));
            if (score > bestScore)
            {
                best = answer;
                bestScore = score;
            }
        }
        return best ?? fallback;
    }

    /// <summary>
    /// The simulated model: it "searches" for 700ms, lands the sources, waits 250ms and streams the text in small uneven
    /// pieces every 45ms (the palette then reveals it 3 to 7 words at a time), and resolves with the actions.
    /// </summary>
    internal Func<AiCommandPaletteAskContext, Task<AiCommandPaletteAnswer?>> Responder(IAiCommandPaletteScheduler scheduler,
        Func<AiCommandPaletteLabels> labels) =>
        async context =>
        {
            var answer = Pick(context.Question);
            var token = context.CancellationToken;
            await scheduler.Delay(TimeSpan.FromMilliseconds(700), token);
            if (answer is null)
            {
                return null;
            }
            context.SetSources(answer.Sources ?? []);
            await scheduler.Delay(TimeSpan.FromMilliseconds(250), token);
            var text = (answer.Text ?? string.Empty).Replace("{question}", context.Question.Trim().TrimEnd('?'), StringComparison.Ordinal);
            var random = new Random(text.Length);
            for (var i = 0; i < text.Length;)
            {
                var size = Math.Min(text.Length - i, 6 + random.Next(18));
                context.Append(text.Substring(i, size));
                i += size;
                await scheduler.Delay(TimeSpan.FromMilliseconds(45), token);
            }
            var simulated = labels().Simulated;
            var actions = new List<AiCommandPaletteAction>();
            foreach (var action in answer.Actions ?? [])
            {
                var result = action.Result;
                actions.Add(new AiCommandPaletteAction(action.Id, action.Label, action.Meta, action.Mark, async run =>
                {
                    await scheduler.Delay(TimeSpan.FromMilliseconds(1100), run);
                    return result;
                })
                {
                    DoneMeta = simulated,
                });
            }
            return new AiCommandPaletteAnswer { Actions = actions };
        };
}
