using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One prompt button of an <see cref="AiChatSuggestionList"/>: the suggestion's icon and label, named by the label. It
/// follows the suggestion's label and icon while it is in a window.
/// </summary>
internal sealed class AiChatSuggestionButton : AvaloniaButton
{
    /// <summary>The suggestion shown.</summary>
    public static readonly StyledProperty<AiChatSuggestion?> SuggestionProperty =
        AvaloniaProperty.Register<AiChatSuggestionButton, AiChatSuggestion?>(nameof(Suggestion));

    /// <summary>The label on screen.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AiChatSuggestionButton, string?>(nameof(Label));

    /// <summary>The icon on screen (24 × 24 geometry); null leaves no icon slot.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiChatSuggestionButton, Geometry?>(nameof(Icon));

    private AiChatSuggestion? _observed;
    private bool _attached;

    public AiChatSuggestion? Suggestion { get => GetValue(SuggestionProperty); set => SetValue(SuggestionProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiChatSuggestionButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SuggestionProperty)
        {
            Observe();
            Refresh();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Observe();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Observe();
        PseudoClasses.Set(":hover", false);
    }

    private void Observe()
    {
        var next = _attached ? Suggestion : null;
        if (ReferenceEquals(next, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnSuggestionChanged;
        }
        _observed = next;
        if (next is not null)
        {
            next.PropertyChanged += OnSuggestionChanged;
        }
    }

    private void OnSuggestionChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var suggestion = Suggestion;
        Label = suggestion?.Label;
        Icon = suggestion?.Icon;
        AutomationProperties.SetName(this, suggestion?.Label);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover): touch never leaves a hover look behind.
        PseudoClasses.Set(":hover", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":hover", false);
    }
}
