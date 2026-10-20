using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="SearchField.SearchSubmitted"/>.</summary>
public sealed class SearchSubmittedEventArgs : RoutedEventArgs
{
    public SearchSubmittedEventArgs(RoutedEvent routedEvent, string query)
        : base(routedEvent)
    {
        Query = query;
    }

    /// <summary>The text in the field when Enter was pressed.</summary>
    public string Query { get; }
}

/// <summary>
/// Arc search field: a labeled search input with a leading glass that wakes up (full contrast) while the field
/// is focused or filled, and a clear button that pops into a reserved slot, so the field never changes width.
/// Every keystroke updates the two-way <c>Text</c> (debounce expensive filtering yourself); Escape clears the
/// field, Enter raises <see cref="SearchSubmitted"/>, and clearing with the button returns focus to the field.
/// </summary>
[TemplatePart(ClearButtonPart, typeof(FieldIconButton))]
[TemplatePart(ClearPresencePart, typeof(PresenceHost))]
[PseudoClasses(FilledPseudoClass)]
public class SearchField : FieldTextBox
{
    public const string ClearButtonPart = "PART_ClearButton";
    public const string ClearPresencePart = "PART_ClearPresence";

    /// <summary>Set while the field has text.</summary>
    public const string FilledPseudoClass = ":filled";

    /// <summary>Raised when Enter is pressed in the field.</summary>
    public static readonly RoutedEvent<SearchSubmittedEventArgs> SearchSubmittedEvent =
        RoutedEvent.Register<SearchField, SearchSubmittedEventArgs>(nameof(SearchSubmitted), RoutingStrategies.Bubble);

    /// <summary>Name of the clear button.</summary>
    public static readonly StyledProperty<string> ClearTextProperty =
        AvaloniaProperty.Register<SearchField, string>(nameof(ClearText), "Clear search");

    /// <summary>Name of the clear button.</summary>
    public string ClearText { get => GetValue(ClearTextProperty); set => SetValue(ClearTextProperty, value); }

    /// <summary>Raised when Enter is pressed in the field.</summary>
    public event EventHandler<SearchSubmittedEventArgs>? SearchSubmitted
    {
        add => AddHandler(SearchSubmittedEvent, value);
        remove => RemoveHandler(SearchSubmittedEvent, value);
    }

    private FieldIconButton? _clearButton;
    private PresenceHost? _clearPresence;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_clearButton is not null)
        {
            _clearButton.Click -= OnClearClick;
        }
        base.OnApplyTemplate(e);
        _clearButton = e.NameScope.Find<FieldIconButton>(ClearButtonPart);
        _clearPresence = e.NameScope.Find<PresenceHost>(ClearPresencePart);
        if (_clearButton is not null)
        {
            _clearButton.Click += OnClearClick;
            AutomationProperties.SetName(_clearButton, ClearText);
        }
        UpdateFilled();
    }

    /// <summary>Clears the field (raises the usual text change) and keeps focus in it.</summary>
    public void Clear(bool focus = true)
    {
        SetCurrentValue(TextProperty, string.Empty);
        if (focus)
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        // The button leaves under the pointer, so focus returns to the field.
        Clear();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text) && !IsReadOnly)
        {
            Clear(focus: false);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            RaiseEvent(new SearchSubmittedEventArgs(SearchSubmittedEvent, Text ?? string.Empty));
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IsReadOnlyProperty)
        {
            UpdateFilled();
        }
        else if (change.Property == ClearTextProperty && _clearButton is not null)
        {
            AutomationProperties.SetName(_clearButton, ClearText);
        }
    }

    private void UpdateFilled()
    {
        var filled = !string.IsNullOrEmpty(Text);
        PseudoClasses.Set(FilledPseudoClass, filled);
        if (_clearPresence is not null)
        {
            _clearPresence.IsPresent = filled && !IsReadOnly;
        }
    }
}
