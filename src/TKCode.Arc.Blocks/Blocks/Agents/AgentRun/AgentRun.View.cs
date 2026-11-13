using Avalonia;

namespace TKCode.Arc;

public partial class AgentRun
{
    /// <summary>The state word as shown (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> StateTextProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(StateText), o => o.StateText);

    /// <summary>The elapsed time as shown (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> ElapsedTextProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(ElapsedText), o => o.ElapsedText);

    /// <summary>The cost as shown (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> CostTextProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(CostText), o => o.CostText);

    /// <summary>The tokens as shown; null hides them (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> TokensTextProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(TokensText), o => o.TokensText);

    /// <summary>The progress line (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> ProgressLineProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(ProgressLine), o => o.ProgressLine);

    /// <summary>The note under the timeline; null hides it (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> NoteLineProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(NoteLine), o => o.NoteLine);

    /// <summary>The caption over the goal (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> GoalCaptionProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(GoalCaption), o => o.GoalCaption);

    /// <summary>The accessible name of the timeline (template use).</summary>
    internal static readonly DirectProperty<AgentRun, string?> RegionNameProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, string?>(nameof(RegionName), o => o.RegionName);

    private string? _goalCaption;
    private string? _regionName;
    private string? _stateText;
    private string? _elapsedText;
    private string? _costText;
    private string? _tokensText;
    private string? _progressLine;
    private string? _noteLine;

    internal string? GoalCaption { get => _goalCaption; private set => SetAndRaise(GoalCaptionProperty, ref _goalCaption, value); }

    internal string? RegionName { get => _regionName; private set => SetAndRaise(RegionNameProperty, ref _regionName, value); }

    internal string? StateText { get => _stateText; private set => SetAndRaise(StateTextProperty, ref _stateText, value); }

    internal string? ElapsedText { get => _elapsedText; private set => SetAndRaise(ElapsedTextProperty, ref _elapsedText, value); }

    internal string? CostText { get => _costText; private set => SetAndRaise(CostTextProperty, ref _costText, value); }

    internal string? TokensText { get => _tokensText; private set => SetAndRaise(TokensTextProperty, ref _tokensText, value); }

    internal string? ProgressLine { get => _progressLine; private set => SetAndRaise(ProgressLineProperty, ref _progressLine, value); }

    internal string? NoteLine { get => _noteLine; private set => SetAndRaise(NoteLineProperty, ref _noteLine, value); }

    /// <summary>Writes every text of the header and the footer from the current values.</summary>
    private void UpdateTemplateTexts()
    {
        var labels = Labels;
        GoalCaption = labels.Goal;
        RegionName = labels.Region;
        StateText = labels.State(State);
        ElapsedText = labels.Elapsed(Elapsed);
        CostText = labels.Cost(Cost, CurrencySymbol);
        TokensText = Tokens > 0 ? labels.Tokens(Tokens) : null;
        ProgressLine = _shown.Count == 0 ? labels.Waiting : ProgressText;
        NoteLine = NoteText;
    }
}
