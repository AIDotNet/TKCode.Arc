using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Carries the suggestion a person pressed and the text it sends.</summary>
internal sealed class AiChatSuggestionEventArgs : RoutedEventArgs
{
    public AiChatSuggestionEventArgs(RoutedEvent routedEvent, object? source, AiChatSuggestion suggestion)
        : base(routedEvent, source)
    {
        Suggestion = suggestion;
        Prompt = string.IsNullOrEmpty(suggestion.Prompt) ? suggestion.Label : suggestion.Prompt!;
    }

    /// <summary>The suggestion that was pressed.</summary>
    public AiChatSuggestion Suggestion { get; }

    /// <summary>What to send: the suggestion's prompt, or its label when it has none.</summary>
    public string Prompt { get; }
}

/// <summary>
/// The prompt buttons of an empty chat: a column 368px wide (narrower when its container is), 6px apart, of at most
/// <see cref="MaxItems"/> buttons (three). Each is 44px high with a 1px border on the surface and an 18px radius: a 16px
/// icon in the muted text color, 12px, then the label in 14px secondary text; hover strengthens the border, tints the
/// fill and darkens the label. The buttons are ordinary tab stops; a click, Enter or Space raises <see cref="Invoked"/>.
/// The list follows changes of <see cref="ItemsSource"/> and of each suggestion while it is in a window.
/// </summary>
[TemplatePart(ItemsPart, typeof(Panel))]
internal class AiChatSuggestionList : TemplatedControl
{
    public const string ItemsPart = "PART_Items";

    /// <summary>The suggestions; only the first <see cref="MaxItems"/> show.</summary>
    public static readonly StyledProperty<IEnumerable<AiChatSuggestion>?> ItemsSourceProperty =
        AvaloniaProperty.Register<AiChatSuggestionList, IEnumerable<AiChatSuggestion>?>(nameof(ItemsSource));

    /// <summary>How many suggestions show at most (3).</summary>
    public static readonly StyledProperty<int> MaxItemsProperty =
        AvaloniaProperty.Register<AiChatSuggestionList, int>(nameof(MaxItems), 3);

    /// <summary>Raised when a suggestion is pressed (click, Enter or Space).</summary>
    public static readonly RoutedEvent<AiChatSuggestionEventArgs> InvokedEvent =
        RoutedEvent.Register<AiChatSuggestionList, AiChatSuggestionEventArgs>(nameof(Invoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<AiChatSuggestion>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="MaxItemsProperty"/>
    public int MaxItems { get => GetValue(MaxItemsProperty); set => SetValue(MaxItemsProperty, value); }

    /// <inheritdoc cref="InvokedEvent"/>
    public event EventHandler<AiChatSuggestionEventArgs>? Invoked { add => AddHandler(InvokedEvent, value); remove => RemoveHandler(InvokedEvent, value); }

    private readonly List<AiChatSuggestionButton> _buttons = new();
    private Panel? _items;
    private INotifyCollectionChanged? _observed;
    private bool _attached;

    /// <summary>The buttons on screen, in order.</summary>
    internal IReadOnlyList<AiChatSuggestionButton> Buttons => _buttons;

    /// <summary>True when at least one suggestion shows.</summary>
    internal bool HasItems => _buttons.Count > 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _items?.Children.Clear();
        _items = e.NameScope.Find<Panel>(ItemsPart);
        if (_items is not null)
        {
            foreach (var button in _buttons)
            {
                _items.Children.Add(button);
            }
        }
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            Observe();
            Rebuild();
        }
        else if (change.Property == MaxItemsProperty)
        {
            Rebuild();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Observe();
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Observe();
    }

    // The suggestions are app data that can outlive the list: followed only while the list is in a window.
    private void Observe()
    {
        var next = _attached ? ItemsSource as INotifyCollectionChanged : null;
        if (ReferenceEquals(next, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnItemsChanged;
        }
        _observed = next;
        if (next is not null)
        {
            next.CollectionChanged += OnItemsChanged;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <summary>One button per shown suggestion; buttons are reused by position.</summary>
    private void Rebuild()
    {
        var shown = new List<AiChatSuggestion>();
        if (ItemsSource is { } source)
        {
            var max = Math.Max(0, MaxItems);
            foreach (var suggestion in source)
            {
                if (shown.Count >= max)
                {
                    break;
                }
                if (suggestion is not null)
                {
                    shown.Add(suggestion);
                }
            }
        }
        while (_buttons.Count > shown.Count)
        {
            var last = _buttons[^1];
            _buttons.RemoveAt(_buttons.Count - 1);
            last.Click -= OnButtonClick;
            last.Suggestion = null;
            _items?.Children.Remove(last);
        }
        for (var i = 0; i < shown.Count; i++)
        {
            if (i == _buttons.Count)
            {
                var button = new AiChatSuggestionButton();
                button.Click += OnButtonClick;
                _buttons.Add(button);
                _items?.Children.Add(button);
            }
            _buttons[i].Suggestion = shown[i];
        }
        IsVisible = shown.Count > 0;
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AiChatSuggestionButton { Suggestion: { } suggestion })
        {
            e.Handled = true;
            RaiseEvent(new AiChatSuggestionEventArgs(InvokedEvent, this, suggestion));
        }
    }
}
