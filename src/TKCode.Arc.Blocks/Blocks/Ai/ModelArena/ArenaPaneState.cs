using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// What one of the arena's two answers shows. The arena owns and updates it; read it to follow a round. Before the vote
/// <see cref="Model"/> is set but nothing the person sees carries its name (see <see cref="ModelArena.IsBlind"/>).
/// </summary>
public class ArenaPaneState : AvaloniaObject
{
    private ArenaSlot _slot;
    private AiModel? _model;
    private ArenaPaneStatus _status;
    private ArenaMetrics _metrics = ArenaMetrics.Empty;
    private string? _errorTitle;
    private string? _errorText;
    private bool _isRetryable = true;
    private string _text = string.Empty;

    /// <summary>The slot.</summary>
    public static readonly DirectProperty<ArenaPaneState, ArenaSlot> SlotProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, ArenaSlot>(nameof(Slot), o => o.Slot);

    /// <summary>The model that answers in this slot in the current round; null before the first round.</summary>
    public static readonly DirectProperty<ArenaPaneState, AiModel?> ModelProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, AiModel?>(nameof(Model), o => o.Model);

    /// <summary>Where the answer is.</summary>
    public static readonly DirectProperty<ArenaPaneState, ArenaPaneStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, ArenaPaneStatus>(nameof(Status), o => o.Status);

    /// <summary>What the answer cost and how fast it came so far.</summary>
    public static readonly DirectProperty<ArenaPaneState, ArenaMetrics> MetricsProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, ArenaMetrics>(nameof(Metrics), o => o.Metrics);

    /// <summary>The error card's title; null when the answer did not fail.</summary>
    public static readonly DirectProperty<ArenaPaneState, string?> ErrorTitleProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, string?>(nameof(ErrorTitle), o => o.ErrorTitle);

    /// <summary>The error card's text; null when the answer did not fail.</summary>
    public static readonly DirectProperty<ArenaPaneState, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, string?>(nameof(ErrorText), o => o.ErrorText);

    /// <summary>Whether a failed answer can be retried.</summary>
    public static readonly DirectProperty<ArenaPaneState, bool> IsRetryableProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, bool>(nameof(IsRetryable), o => o.IsRetryable);

    /// <summary>The text shown so far (what the pacer has released), as Markdown.</summary>
    public static readonly DirectProperty<ArenaPaneState, string> TextProperty =
        AvaloniaProperty.RegisterDirect<ArenaPaneState, string>(nameof(Text), o => o.Text);

    internal ArenaPaneState(ArenaSlot slot)
    {
        _slot = slot;
    }

    /// <inheritdoc cref="SlotProperty"/>
    public ArenaSlot Slot { get => _slot; internal set => SetAndRaise(SlotProperty, ref _slot, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AiModel? Model { get => _model; internal set => SetAndRaise(ModelProperty, ref _model, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public ArenaPaneStatus Status { get => _status; internal set => SetAndRaise(StatusProperty, ref _status, value); }

    /// <inheritdoc cref="MetricsProperty"/>
    public ArenaMetrics Metrics { get => _metrics; internal set => SetAndRaise(MetricsProperty, ref _metrics, value); }

    /// <inheritdoc cref="ErrorTitleProperty"/>
    public string? ErrorTitle { get => _errorTitle; internal set => SetAndRaise(ErrorTitleProperty, ref _errorTitle, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => _errorText; internal set => SetAndRaise(ErrorTextProperty, ref _errorText, value); }

    /// <inheritdoc cref="IsRetryableProperty"/>
    public bool IsRetryable { get => _isRetryable; internal set => SetAndRaise(IsRetryableProperty, ref _isRetryable, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => _text; internal set => SetAndRaise(TextProperty, ref _text, value); }
}
