using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What the status slot of the add field shows.</summary>
internal enum AgentMemoryAddStatus
{
    /// <summary>Nothing (the field is empty).</summary>
    None,

    /// <summary>The Add button (the field has text).</summary>
    Ready,

    /// <summary>The shimmering "Sorting into a group" while the memory is sorted and saved.</summary>
    Sorting,

    /// <summary>A check and "Added to Preferences".</summary>
    Added,

    /// <summary>A circle-alert and a danger-colored message: the live count over the limit, "Memory is full", an error.</summary>
    Alert
}

/// <summary>
/// The field at the bottom of the memory card that adds a memory by hand: a plus glyph, the borderless input and, at
/// the end, a status slot that crossfades between the Add button, the shimmering "Sorting into a group", "Added to …"
/// and alerts. Focus inside tints the whole row. Enter (or Add) raises <see cref="Submitted"/> when there is text;
/// Escape does nothing (the text stays). While <see cref="IsBusy"/> the input is read-only. The owner decides the status.
/// </summary>
[TemplatePart(InputPart, typeof(AgentMemoryInput))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
internal sealed class AgentMemoryAddField : TemplatedControl
{
    public const string InputPart = "PART_Input";
    public const string StatusPart = "PART_Status";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentMemoryAddField, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<AgentMemoryAddField, string?>(nameof(PlaceholderText));

    public static readonly StyledProperty<string?> AddTextProperty =
        AvaloniaProperty.Register<AgentMemoryAddField, string?>(nameof(AddText));

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<AgentMemoryAddField, bool>(nameof(IsBusy));

    private AgentMemoryInput? _input;
    private ArcContentSwap? _status;
    private Control? _statusContent;
    private AgentMemoryAddStatus _statusKind;
    private string? _statusText;
    private Button? _add;

    static AgentMemoryAddField()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemoryAddField>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    public string? AddText { get => GetValue(AddTextProperty); set => SetValue(AddTextProperty, value); }

    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <summary>The status on screen.</summary>
    public AgentMemoryAddStatus Status => _statusKind;

    /// <summary>The text of the status on screen (the label of the sorting, added or alert state).</summary>
    public string? StatusText => _statusText;

    /// <summary>The control in the status slot (an Add button, a shimmer or a status line), or null.</summary>
    internal Control? StatusContent => _statusContent;

    /// <summary>The input (template part).</summary>
    public AgentMemoryInput? Input => _input;

    /// <summary>The Add button while it shows.</summary>
    public Button? AddButton => _statusKind == AgentMemoryAddStatus.Ready ? _add : null;

    /// <summary>Enter or Add with text in the field.</summary>
    public event EventHandler? Submitted;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _input?.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        base.OnApplyTemplate(e);
        _input = e.NameScope.Find<AgentMemoryInput>(InputPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _input?.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        if (_status is not null)
        {
            // A new host starts with the current status (its first content never animates).
            _status.Content = _statusContent;
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        UpdateAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlaceholderTextProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == AddTextProperty && _add is not null)
        {
            _add.Content = change.GetNewValue<string?>();
        }
    }

    /// <summary>
    /// Shows <paramref name="status"/> in the slot, crossfading from the last one. The same status with the same text
    /// stays as it is (typing does not replay the Add button), and a live count (<paramref name="isCount"/>) that is
    /// already on screen changes its digits in place.
    /// </summary>
    public void SetStatus(AgentMemoryAddStatus status, string? text = null, bool isCount = false)
    {
        var line = _statusContent as AgentMemoryStatusLine;
        if (status == _statusKind && text == _statusText && (line?.IsCount ?? false) == isCount)
        {
            return;
        }
        _statusText = text;
        if (status == AgentMemoryAddStatus.Alert && _statusKind == AgentMemoryAddStatus.Alert && isCount && line is { IsCount: true })
        {
            line.Text = text;
            UpdateAutomation();
            return;
        }
        _statusKind = status;
        _statusContent = status switch
        {
            AgentMemoryAddStatus.Ready => CreateAddButton(),
            AgentMemoryAddStatus.Sorting => new TextShimmer
            {
                Text = text,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            },
            AgentMemoryAddStatus.Added => new AgentMemoryStatusLine { Text = text },
            AgentMemoryAddStatus.Alert => new AgentMemoryStatusLine { Text = text, IsAlert = true, IsCount = isCount },
            _ => null
        };
        if (_status is not null)
        {
            _status.Content = _statusContent;
        }
        UpdateAutomation();
    }

    private Button CreateAddButton()
    {
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        // A new button each time: the last one may still be fading out in the slot.
        _add = new Button
        {
            Variant = ButtonVariant.Primary,
            Size = ControlSize.Small,
            Content = AddText,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        _add.Click += OnAddClick;
        return _add;
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!ReferenceEquals(sender, _add) || IsBusy)
        {
            return;
        }
        Submit();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || _input is { IsComposing: true } || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        e.Handled = true;
        if (!IsBusy)
        {
            Submit();
        }
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            return;
        }
        Submitted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The input is named by the placeholder and described by the status (Arc's <c>aria-describedby</c>).</summary>
    private void UpdateAutomation()
    {
        if (_input is null)
        {
            return;
        }
        AutomationProperties.SetName(_input, PlaceholderText);
        AutomationProperties.SetHelpText(_input, _statusKind is AgentMemoryAddStatus.None or AgentMemoryAddStatus.Ready ? null : _statusText);
    }
}

/// <summary>
/// One status of the add field: a check and the text ("Added to Preferences"), or with <see cref="IsAlert"/> a
/// circle-alert and danger-colored text ("Memory is full"). The text tells the state; the glyph is decoration.
/// </summary>
[PseudoClasses(AlertPseudoClass)]
internal sealed class AgentMemoryStatusLine : TemplatedControl
{
    public const string AlertPseudoClass = ":alert";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentMemoryStatusLine, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsAlertProperty =
        AvaloniaProperty.Register<AgentMemoryStatusLine, bool>(nameof(IsAlert));

    static AgentMemoryStatusLine()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemoryStatusLine>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<AgentMemoryStatusLine>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsAlert { get => GetValue(IsAlertProperty); set => SetValue(IsAlertProperty, value); }

    /// <summary>A live count (changes its digits in place instead of crossfading).</summary>
    public bool IsCount { get; init; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAlertProperty)
        {
            PseudoClasses.Set(AlertPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StatusPeer(this);

    private sealed class StatusPeer(AgentMemoryStatusLine owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((AgentMemoryStatusLine)Owner).Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
