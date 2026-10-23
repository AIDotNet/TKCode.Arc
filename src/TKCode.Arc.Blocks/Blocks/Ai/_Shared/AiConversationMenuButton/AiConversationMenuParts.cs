using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What a row of the conversation menu does.</summary>
internal enum AiConversationMenuRowKind
{
    /// <summary>"New chat".</summary>
    NewChat,

    /// <summary>Opens a conversation.</summary>
    Conversation
}

/// <summary>
/// The conversation menu's surface: 260 wide, 1px border, radius 18 on the raised surface (the floating shadow comes from
/// the popup), the rows 6px in, at most 420 high and scrolling inside. It takes focus when the menu opens by pointer so the
/// keys still work, and hands every key to its <see cref="AiConversationMenuButton"/>.
/// </summary>
[TemplatePart(ScrollPart, typeof(ScrollArea))]
internal sealed class AiConversationMenuPanel : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";

    private ScrollArea? _scroll;

    static AiConversationMenuPanel()
    {
        FocusableProperty.OverrideDefaultValue<AiConversationMenuPanel>(true);
        IsTabStopProperty.OverrideDefaultValue<AiConversationMenuPanel>(false);
    }

    /// <summary>The pill that owns the menu.</summary>
    public AiConversationMenuButton? Owner { get; init; }

    /// <summary>The rows, separators and headings (built by the owner).</summary>
    public StackPanel List { get; } = new();

    /// <summary>The scroll area part.</summary>
    internal ScrollArea? Scroll => _scroll;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroll is not null)
        {
            _scroll.Content = null;
        }
        _scroll = e.NameScope.Find<ScrollArea>(ScrollPart);
        if (_scroll is not null)
        {
            _scroll.Content = List;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Owner?.OnMenuKeyDown(e);
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    private sealed class PanelPeer(AiConversationMenuPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;
    }
}

/// <summary>
/// A row of the conversation menu: 36px, padding 0 10, radius 12; "New chat" leads with a SquarePen 15 in the secondary
/// text color (10px gap), the current conversation ends with a Check 15 in the text color. The highlight (pointer or
/// keyboard position) is SurfaceMuted, fading in 120ms. Rows are focusable but not tab stops; a click chooses.
/// </summary>
[PseudoClasses(HighlightedPseudoClass, CheckedPseudoClass)]
internal sealed class AiConversationMenuRow : TemplatedControl
{
    public const string HighlightedPseudoClass = ":highlighted";
    public const string CheckedPseudoClass = ":checked";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiConversationMenuRow, string?>(nameof(Text));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiConversationMenuRow, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<AiConversationMenuRow, bool>(nameof(IsChecked));

    private bool _pressed;

    static AiConversationMenuRow()
    {
        FocusableProperty.OverrideDefaultValue<AiConversationMenuRow>(true);
        IsTabStopProperty.OverrideDefaultValue<AiConversationMenuRow>(false);
    }

    public AiConversationMenuRow(AiConversationMenuRowKind kind, AiChatConversation? conversation)
    {
        Kind = kind;
        Conversation = conversation;
    }

    public AiConversationMenuRowKind Kind { get; }

    public AiChatConversation? Conversation { get; }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    public bool IsHighlighted { get; private set; }

    private AiConversationMenuPanel? FindPanel()
    {
        for (Visual? visual = this; visual is not null; visual = Avalonia.VisualTree.VisualExtensions.GetVisualParent(visual))
        {
            if (visual is AiConversationMenuPanel panel)
            {
                return panel;
            }
        }
        return null;
    }

    internal void SetHighlighted(bool highlighted)
    {
        IsHighlighted = highlighted;
        PseudoClasses.Set(HighlightedPseudoClass, highlighted);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        FindPanel()?.Owner?.OnRowPointerEntered(this, e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        FindPanel()?.Owner?.OnRowPointerExited(this, e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            FindPanel()?.Owner?.OnRowInvoked(this, NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    internal void Invoke() => FindPanel()?.Owner?.OnRowInvoked(this, NavigationMethod.Unspecified);

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>"New chat" is a menu item; a conversation is a radio menu item whose toggle state is the check.</summary>
    private sealed class RowPeer(AiConversationMenuRow owner) : ControlAutomationPeer(owner), IInvokeProvider, IToggleProvider
    {
        private AiConversationMenuRow Row => (AiConversationMenuRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Row.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => Row.Invoke();

        public ToggleState ToggleState => Row.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Row.Invoke();
    }
}

/// <summary>A group heading of the conversation menu: 12 / 16.8 in the muted text color, padding 10 10 4.</summary>
internal sealed class AiConversationMenuHeading : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiConversationMenuHeading, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}

/// <summary>The line under "New chat": 1px BorderSubtle, 6px above and below, 4px in from the sides.</summary>
internal sealed class AiConversationMenuSeparator : TemplatedControl
{
    static AiConversationMenuSeparator()
    {
        FocusableProperty.OverrideDefaultValue<AiConversationMenuSeparator>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiConversationMenuSeparator>(false);
    }
}
