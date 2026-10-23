using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pill that reopens a closed assistant ("✳ Assistant", spec 3.9): 32 high, padding 0 12 0 10, the 14px mark and the
/// text 7px apart (12 / 16.8, weight 500, Foreground), a pill with a 1px Border on Surface; hover SurfaceMuted (160ms
/// standard); the keyboard ring 2px out. It reports itself collapsed (Arc's <c>aria-expanded="false"</c>) and is named by
/// its text.
/// </summary>
internal sealed class AiPanelReopenButton : AvaloniaButton
{
    public const string HoverPseudoClass = ":hover";

    /// <summary>The label (the assistant's name).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiPanelReopenButton, string?>(nameof(Text));

    /// <summary>The assistant's mark; null draws the default spark.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<AiPanelReopenButton, object?>(nameof(Mark));

    /// <summary>The mark drawn (<see cref="Mark"/> or the spark).</summary>
    public static readonly DirectProperty<AiPanelReopenButton, object> ShownMarkProperty =
        AvaloniaProperty.RegisterDirect<AiPanelReopenButton, object>(nameof(ShownMark), o => o.ShownMark);

    private object _shownMark = AiAssistantSpark.Geometry;

    protected override Type StyleKeyOverride => typeof(AiPanelReopenButton);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="ShownMarkProperty"/>
    public object ShownMark { get => _shownMark; private set => SetAndRaise(ShownMarkProperty, ref _shownMark, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
        else if (change.Property == MarkProperty)
        {
            ShownMark = AiAssistantSpark.Resolve(Mark);
        }
    }

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

    protected override AutomationPeer OnCreateAutomationPeer() => new ReopenPeer(this);

    /// <summary>A button that expands the assistant; it is only there while the assistant is collapsed.</summary>
    private sealed class ReopenPeer(AiPanelReopenButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => Invoke();

        public void Collapse()
        {
        }

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IExpandCollapseProvider) ? this : base.GetProviderCore(providerType);
    }
}
