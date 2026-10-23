using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>What a row of the answer-again menu does.</summary>
internal enum AiRegenerateMenuRowKind
{
    /// <summary>"Try again".</summary>
    TryAgain,

    /// <summary>"More concise".</summary>
    Concise,

    /// <summary>"More detailed".</summary>
    Detailed,

    /// <summary>"Switch model": opens the list of models.</summary>
    SwitchModel,

    /// <summary>A model of the list.</summary>
    Model
}

/// <summary>
/// A surface of the answer-again menu or of its model list: at least 216 wide, 1px Border, radius 26 on the raised surface
/// (the floating shadow comes from the popup), the rows 5px in. It takes focus when the menu opens by pointer so the keys
/// still work, and hands every key to its <see cref="AiRegenerateMenu"/>.
/// </summary>
internal sealed class AiRegenerateMenuPanel : TemplatedControl
{
    static AiRegenerateMenuPanel()
    {
        FocusableProperty.OverrideDefaultValue<AiRegenerateMenuPanel>(true);
        IsTabStopProperty.OverrideDefaultValue<AiRegenerateMenuPanel>(false);
    }

    public AiRegenerateMenuPanel(AiRegenerateMenu owner, bool isSubmenu)
    {
        Owner = owner;
        IsSubmenu = isSubmenu;
    }

    /// <summary>The menu that owns the surface.</summary>
    public AiRegenerateMenu Owner { get; }

    /// <summary>True for the list of models.</summary>
    public bool IsSubmenu { get; }

    /// <summary>The rows, the separator and the heading (built by the owner).</summary>
    public StackPanel List { get; } = new();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<ContentPresenter>("PART_List") is { } presenter)
        {
            presenter.Content = List;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Owner.OnMenuKeyDown(this, e);
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    private sealed class PanelPeer(AiRegenerateMenuPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;
    }
}

/// <summary>
/// A row of the answer-again menu: 36px, padding 0 12 0 10, radius 20, 14 / 18.2 in the text color. The main rows lead with
/// a 16px glyph in the secondary text color (10px gap); "Switch model" ends with a ChevronRight 16. A model row leads with
/// the model's mark (15 in a 16px cell). The highlight (pointer or keyboard position, and "Switch model" while its list is
/// open) is SurfaceMuted, fading in 120ms, no ring. Rows are focusable but not tab stops; a click chooses.
/// </summary>
[PseudoClasses(HighlightedPseudoClass, SubmenuPseudoClass)]
internal sealed class AiRegenerateMenuRow : TemplatedControl
{
    public const string HighlightedPseudoClass = ":highlighted";

    /// <summary>Set on the row that opens the list of models.</summary>
    public const string SubmenuPseudoClass = ":submenu";

    /// <summary>The row's words.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiRegenerateMenuRow, string?>(nameof(Text));

    /// <summary>The leading Lucide glyph of a main row.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiRegenerateMenuRow, Geometry?>(nameof(Icon));

    /// <summary>A model's mark (a <see cref="Geometry"/> or an <see cref="IImage"/>), shown instead of <see cref="Icon"/>.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<AiRegenerateMenuRow, object?>(nameof(Mark));

    /// <summary>The fill of a geometry mark.</summary>
    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<AiRegenerateMenuRow, IBrush?>(nameof(MarkColor));

    private bool _pressed;

    static AiRegenerateMenuRow()
    {
        FocusableProperty.OverrideDefaultValue<AiRegenerateMenuRow>(true);
        IsTabStopProperty.OverrideDefaultValue<AiRegenerateMenuRow>(false);
    }

    public AiRegenerateMenuRow(AiRegenerateMenuRowKind kind, AiModel? model = null)
    {
        Kind = kind;
        Model = model;
        PseudoClasses.Set(SubmenuPseudoClass, kind == AiRegenerateMenuRowKind.SwitchModel);
    }

    /// <summary>What the row does.</summary>
    public AiRegenerateMenuRowKind Kind { get; }

    /// <summary>The model of a model row.</summary>
    public AiModel? Model { get; }

    /// <summary>The row's words.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The leading glyph.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The model's mark.</summary>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <summary>The fill of a geometry mark.</summary>
    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }

    /// <summary>Whether the row carries the highlight.</summary>
    public bool IsHighlighted { get; private set; }

    /// <summary>The menu that owns the row (through its surface).</summary>
    private AiRegenerateMenu? Owner => this.FindAncestorOfType<AiRegenerateMenuPanel>()?.Owner;

    internal void SetHighlighted(bool highlighted)
    {
        IsHighlighted = highlighted;
        PseudoClasses.Set(HighlightedPseudoClass, highlighted);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>() ?? string.Empty);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Owner?.OnRowPointerEntered(this, e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Owner?.OnRowPointerExited(this, e);
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
            Owner?.OnRowInvoked(this, NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    internal void Invoke() => Owner?.OnRowInvoked(this, NavigationMethod.Unspecified);

    protected override AutomationPeer OnCreateAutomationPeer() =>
        Kind == AiRegenerateMenuRowKind.SwitchModel ? new SubmenuRowPeer(this) : new RowPeer(this);

    /// <summary>A menu item; choosing it runs the action.</summary>
    private class RowPeer(AiRegenerateMenuRow owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected AiRegenerateMenuRow Row => (AiRegenerateMenuRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Row.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        public void Invoke() => Row.Invoke();
    }

    /// <summary>"Switch model": a menu item that opens the list of models (<c>aria-haspopup</c>, <c>aria-expanded</c>).</summary>
    private sealed class SubmenuRowPeer(AiRegenerateMenuRow owner) : RowPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState =>
            Row.Owner?.IsSubmenuOpen == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand() => Row.Owner?.OpenSubmenu(keyboard: true);

        public void Collapse() => Row.Owner?.CloseSubmenu(focusRow: true);
    }
}

/// <summary>The heading of the list of models ("Answer again with"): 12 / 16.8 in the muted text color, padding 6 10 4.</summary>
internal sealed class AiRegenerateMenuHeading : TemplatedControl
{
    /// <summary>The heading's words.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiRegenerateMenuHeading, string?>(nameof(Text));

    static AiRegenerateMenuHeading()
    {
        FocusableProperty.OverrideDefaultValue<AiRegenerateMenuHeading>(false);
    }

    /// <summary>The heading's words.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
