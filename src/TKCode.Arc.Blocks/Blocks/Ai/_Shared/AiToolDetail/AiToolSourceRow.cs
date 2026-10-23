using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One source in a tool's detail panel (Arc's numbered source list): a 32px row, 8px sides and radius 10 (it reaches 8px past
/// the panel's text on both sides so the hover fill is wider than the words), the 14px site mark, 10px, the title (Foreground,
/// trimmed) and the domain (12px, TextMuted) at the right, hidden in a narrow panel. Hover fills it SurfaceMuted. It is a
/// link: a press, Enter or Space raises <see cref="AiToolDetail.SourceInvokedEvent"/> with the source, and its accessible
/// name comes from the panel's <see cref="AiToolDetail.SourceLinkName"/> ("Source 1: title, domain").
/// </summary>
[PseudoClasses(HoverPseudoClass, NarrowPseudoClass)]
internal sealed class AiToolSourceRow : AvaloniaButton
{
    /// <summary>Mouse or pen over the row.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The panel is narrow: the domain is hidden.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The source of the row.</summary>
    public static readonly StyledProperty<AiSource?> SourceProperty =
        AvaloniaProperty.Register<AiToolSourceRow, AiSource?>(nameof(Source));

    /// <summary>Hides the domain (inherited from the panel).</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AiToolDetail.IsNarrowProperty.AddOwner<AiToolSourceRow>();

    /// <summary>Names the row (inherited from the panel).</summary>
    public static readonly StyledProperty<Func<int, string, string?, string>?> SourceLinkNameProperty =
        AiToolDetail.SourceLinkNameProperty.AddOwner<AiToolSourceRow>();

    /// <inheritdoc cref="SourceProperty"/>
    public AiSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="SourceLinkNameProperty"/>
    public Func<int, string, string?, string>? SourceLinkName { get => GetValue(SourceLinkNameProperty); set => SetValue(SourceLinkNameProperty, value); }

    private AiSource? _watched;

    protected override Type StyleKeyOverride => typeof(AiToolSourceRow);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Watch(this.IsAttachedToVisualTree() ? change.GetNewValue<AiSource?>() : null);
            UpdateName();
        }
        else if (change.Property == SourceLinkNameProperty)
        {
            UpdateName();
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Source);
        UpdateName();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    private void Watch(AiSource? source)
    {
        if (ReferenceEquals(_watched, source))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnSourceChanged;
        }
        _watched = source;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => UpdateName();

    private void UpdateName()
    {
        if (Source is not { } source)
        {
            ClearValue(AutomationProperties.NameProperty);
            return;
        }
        var name = SourceLinkName?.Invoke(source.Index, source.Title, source.Domain) ?? source.Title;
        AutomationProperties.SetName(this, name);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Source is { } source)
        {
            RaiseEvent(new AiChatSourceEventArgs(AiToolDetail.SourceInvokedEvent, this, source));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiToolSourceRowAutomationPeer(this);

    /// <summary>A hyperlink named by the panel's wording; the mark, title and domain inside are not read again.</summary>
    private sealed class AiToolSourceRowAutomationPeer(AiToolSourceRow owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
