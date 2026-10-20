using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The closed country trigger of <see cref="PhoneInput"/> (flag + calling code, chevron beside it). Its fill answers
/// mouse hover, press and keyboard focus with <c>SurfaceMuted</c> (Arc draws no focus ring here: keyboard focus reads as
/// the hover fill). ArrowDown / ArrowUp open the list; a printable key opens it seeded with that key. It reports the
/// country ("Country, Canada +1") and the list's expanded state to assistive technology.
/// </summary>
internal sealed class PhoneInputTrigger : AvaloniaButton
{
    /// <summary>Mouse (not touch) hover.</summary>
    public const string HoverPseudoClass = ":hover";

    internal PhoneInput? Field { get; set; }

    protected override Type StyleKeyOverride => typeof(PhoneInputTrigger);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && Field is { } field && e.Key is Key.Down or Key.Up &&
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
        {
            e.Handled = true;
            field.OpenPicker(string.Empty);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled)
        {
            return;
        }
        // Never let typing on the trigger bubble into the number editor that hosts it.
        e.Handled = true;
        var text = e.Text;
        if (Field is { } field && text is { Length: 1 } && text != " " && !char.IsControl(text[0]))
        {
            field.OpenPicker(text);
        }
    }

    protected override void OnClick()
    {
        base.OnClick();
        Field?.OnTriggerClicked();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PhoneInputTriggerAutomationPeer(this);

    internal void RaiseExpandChanged(bool open)
        => (ControlAutomationPeer.FromElement(this) as PhoneInputTriggerAutomationPeer)?.RaiseExpandChanged(open);
}

/// <summary>A button with expand / collapse (<c>aria-haspopup="listbox"</c>, <c>aria-expanded</c>).</summary>
internal sealed class PhoneInputTriggerAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
{
    private readonly PhoneInputTrigger _trigger;

    public PhoneInputTriggerAutomationPeer(PhoneInputTrigger owner)
        : base(owner)
    {
        _trigger = owner;
    }

    public ExpandCollapseState ExpandCollapseState =>
        _trigger.Field?.IsPickerOpen == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => true;

    public void Expand() => _trigger.Field?.OpenPicker(string.Empty);

    public void Collapse() => _trigger.Field?.ClosePicker(PhoneInputFocusTarget.Trigger);

    internal void RaiseExpandChanged(bool open) => RaisePropertyChangedEvent(
        ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
        open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
        open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}

/// <summary>
/// The picker's search field: a frameless single-line editor (TypeSM, TextMuted placeholder) that reports itself as a
/// combobox controlling the country list, with the active option as its help text (Arc's
/// <c>aria-activedescendant</c>).
/// </summary>
internal sealed class PhoneInputSearchBox : TextBox
{
    private static readonly FuncControlTemplate<TextBox> s_template = new((box, scope) =>
    {
        var placeholder = new TextBlock
        {
            Name = "PART_Placeholder",
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        placeholder.Bind(TextBlock.TextProperty, new TemplateBinding(PlaceholderTextProperty));
        placeholder.Bind(TextBlock.ForegroundProperty, new TemplateBinding(PlaceholderForegroundProperty));
        placeholder.Bind(IsVisibleProperty, new TemplateBinding(TextProperty) { Converter = StringConverters.IsNullOrEmpty });

        var presenter = new TextPresenter
        {
            Name = "PART_TextPresenter",
            VerticalAlignment = VerticalAlignment.Center
        };
        presenter.Bind(TextPresenter.TextProperty, new TemplateBinding(TextProperty) { Mode = BindingMode.TwoWay });
        presenter.Bind(TextPresenter.CaretIndexProperty, new TemplateBinding(CaretIndexProperty));
        presenter.Bind(TextPresenter.SelectionStartProperty, new TemplateBinding(SelectionStartProperty));
        presenter.Bind(TextPresenter.SelectionEndProperty, new TemplateBinding(SelectionEndProperty));
        presenter.Bind(TextPresenter.CaretBrushProperty, new TemplateBinding(CaretBrushProperty));
        presenter.Bind(TextPresenter.SelectionBrushProperty, new TemplateBinding(SelectionBrushProperty));
        presenter.Bind(TextPresenter.SelectionForegroundBrushProperty, new TemplateBinding(SelectionForegroundBrushProperty));
        presenter.Bind(TextPresenter.CaretBlinkIntervalProperty, new TemplateBinding(CaretBlinkIntervalProperty));
        presenter.Bind(TextPresenter.LineHeightProperty, new TemplateBinding(LineHeightProperty));
        presenter.RegisterInNameScope(scope);
        placeholder.RegisterInNameScope(scope);
        var scroll = new ScrollViewer
        {
            Name = "PART_ScrollViewer",
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Center,
            Content = presenter
        };
        scroll.RegisterInNameScope(scope);
        return new Panel
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Ibeam),
            Children = { placeholder, scroll }
        };
    });

    public PhoneInputSearchBox()
    {
        Template = s_template;
        Background = Brushes.Transparent;
        BorderThickness = default;
        Padding = default;
        MinHeight = 0;
        AcceptsReturn = false;
        TextWrapping = TextWrapping.NoWrap;
        VerticalAlignment = VerticalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Center;
        FocusAdorner = null;
        ArcTokens.Bind(this, ForegroundProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, CaretBrushProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, PlaceholderForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, SelectionBrushProperty, DesignTokens.ArcDesignTokenKind.AccentSubtle);
        ArcTokens.Bind(this, SelectionForegroundBrushProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(this, FontFamilyProperty, DesignTokens.ArcDesignTokenKind.BodyFontFamily);
    }

    internal PhoneInput? Field { get; set; }

    protected override Type StyleKeyOverride => typeof(PhoneInputSearchBox);

    protected override AutomationPeer OnCreateAutomationPeer() => new PhoneInputSearchBoxAutomationPeer(this);
}

/// <summary>The search field as a combobox: expanded while the list is open, the active option as its help text.</summary>
internal sealed class PhoneInputSearchBoxAutomationPeer : TextBoxAutomationPeer, IExpandCollapseProvider
{
    private readonly PhoneInputSearchBox _box;

    public PhoneInputSearchBoxAutomationPeer(PhoneInputSearchBox owner)
        : base(owner)
    {
        _box = owner;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

    protected override string? GetHelpTextCore()
    {
        var help = base.GetHelpTextCore();
        return string.IsNullOrEmpty(help) ? _box.Field?.ActiveOptionText : help;
    }

    public ExpandCollapseState ExpandCollapseState =>
        _box.Field?.IsPickerOpen == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => false;

    public void Expand()
    {
    }

    public void Collapse() => _box.Field?.ClosePicker(PhoneInputFocusTarget.Trigger);
}

/// <summary>Where focus goes when the picker closes.</summary>
internal enum PhoneInputFocusTarget
{
    None,
    Trigger,
    Number
}
