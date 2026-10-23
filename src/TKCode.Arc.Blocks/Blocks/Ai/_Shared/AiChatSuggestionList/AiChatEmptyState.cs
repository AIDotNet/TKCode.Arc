using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The empty chat: the greeting in the display font (22 / 24.2, regular, tracked −0.02em) over the suggestion buttons,
/// 22px apart, centered both ways in the space it is given with 32 28 of padding; without suggestions only the greeting
/// shows. <see cref="IsShown"/> fades the whole thing in (160ms, rising 4px) or out (100ms), with no stagger between the
/// buttons; the first state applies without motion. A pressed suggestion raises <see cref="SuggestionInvoked"/>.
/// </summary>
[TemplatePart(RootPart, typeof(Control))]
[TemplatePart(ListPart, typeof(AiChatSuggestionList))]
internal class AiChatEmptyState : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string ListPart = "PART_Suggestions";

    /// <summary>The rise of the way in (px).</summary>
    internal const double EnterRise = 4;

    /// <summary>The greeting ("What should we look at today?").</summary>
    public static readonly StyledProperty<string?> GreetingProperty =
        AvaloniaProperty.Register<AiChatEmptyState, string?>(nameof(Greeting));

    /// <summary>The suggestions; at most <see cref="MaxSuggestions"/> show.</summary>
    public static readonly StyledProperty<IEnumerable<AiChatSuggestion>?> SuggestionsProperty =
        AvaloniaProperty.Register<AiChatEmptyState, IEnumerable<AiChatSuggestion>?>(nameof(Suggestions));

    /// <summary>How many suggestions show at most (3).</summary>
    public static readonly StyledProperty<int> MaxSuggestionsProperty =
        AvaloniaProperty.Register<AiChatEmptyState, int>(nameof(MaxSuggestions), 3);

    /// <summary>Whether the empty state is shown (default true); changes fade it in or out.</summary>
    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<AiChatEmptyState, bool>(nameof(IsShown), true);

    /// <summary>Raised when a suggestion is pressed.</summary>
    public static readonly RoutedEvent<AiChatSuggestionEventArgs> SuggestionInvokedEvent =
        RoutedEvent.Register<AiChatEmptyState, AiChatSuggestionEventArgs>(nameof(SuggestionInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="GreetingProperty"/>
    public string? Greeting { get => GetValue(GreetingProperty); set => SetValue(GreetingProperty, value); }

    /// <inheritdoc cref="SuggestionsProperty"/>
    public IEnumerable<AiChatSuggestion>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }

    /// <inheritdoc cref="MaxSuggestionsProperty"/>
    public int MaxSuggestions { get => GetValue(MaxSuggestionsProperty); set => SetValue(MaxSuggestionsProperty, value); }

    /// <inheritdoc cref="IsShownProperty"/>
    public bool IsShown { get => GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }

    /// <inheritdoc cref="SuggestionInvokedEvent"/>
    public event EventHandler<AiChatSuggestionEventArgs>? SuggestionInvoked
    {
        add => AddHandler(SuggestionInvokedEvent, value);
        remove => RemoveHandler(SuggestionInvokedEvent, value);
    }

    private AiChatSuggestionList? _list;
    private AiFadePresence? _presence;

    /// <summary>The suggestion list part.</summary>
    internal AiChatSuggestionList? List => _list;

    /// <summary>The fade of the content.</summary>
    internal AiFadePresence? Presence => _presence;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_list is not null)
        {
            _list.Invoked -= OnListInvoked;
        }
        _presence?.Dispose();
        _presence = null;
        _list = e.NameScope.Find<AiChatSuggestionList>(ListPart);
        if (_list is not null)
        {
            _list.Invoked += OnListInvoked;
        }
        if (e.NameScope.Find<Control>(RootPart) is { } root)
        {
            _presence = new AiFadePresence(root, IsShown, EnterRise);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsShownProperty)
        {
            _presence?.SetShown(change.GetNewValue<bool>());
        }
    }

    private void OnListInvoked(object? sender, AiChatSuggestionEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new AiChatSuggestionEventArgs(SuggestionInvokedEvent, this, e.Suggestion));
    }
}
