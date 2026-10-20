using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Shared panel of the modal overlays (Dialog, Drawer, BottomSheet; also reusable for Card's quick preview): a title
/// that crossfades when it changes while open, an optional description, the body content, optional actions and the
/// close button. Hosted by a <see cref="TKCode.Arc.Primitives.ModalPresence"/>.
/// </summary>
[TemplatePart(CloseButtonPart, typeof(AvaloniaButton))]
[PseudoClasses(HasDescriptionPseudoClass, HasActionsPseudoClass)]
public abstract class ModalSurface : ContentControl
{
    public const string CloseButtonPart = "PART_CloseButton";
    public const string HasDescriptionPseudoClass = ":has-description";
    public const string HasActionsPseudoClass = ":has-actions";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModalSurface, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ModalSurface, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<ModalSurface, object?>(nameof(Actions));

    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        AvaloniaProperty.Register<ModalSurface, bool>(nameof(ShowCloseButton), true);

    public static readonly StyledProperty<string> CloseButtonNameProperty =
        AvaloniaProperty.Register<ModalSurface, string>(nameof(CloseButtonName), "Close");

    /// <summary>Heading; when it changes while open the new copy rises in with a soft blur.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Secondary line under the title.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Optional footer content (usually buttons), right aligned under the body.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    public bool ShowCloseButton { get => GetValue(ShowCloseButtonProperty); set => SetValue(ShowCloseButtonProperty, value); }

    /// <summary>Accessible name of the close button.</summary>
    public string CloseButtonName { get => GetValue(CloseButtonNameProperty); set => SetValue(CloseButtonNameProperty, value); }

    /// <summary>The close button was clicked.</summary>
    public event EventHandler? CloseRequested;

    private AvaloniaButton? _closeButton;

    /// <summary>The template's close button (after the template is applied).</summary>
    public AvaloniaButton? CloseButton => _closeButton;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_closeButton is not null)
        {
            _closeButton.Click -= OnCloseClick;
        }
        base.OnApplyTemplate(e);
        _closeButton = e.NameScope.Find<AvaloniaButton>(CloseButtonPart);
        if (_closeButton is not null)
        {
            _closeButton.Click += OnCloseClick;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title ?? string.Empty);
        }
        else if (change.Property == DescriptionProperty)
        {
            AutomationProperties.SetHelpText(this, Description ?? string.Empty);
            PseudoClasses.Set(HasDescriptionPseudoClass, !string.IsNullOrEmpty(Description));
        }
        else if (change.Property == ActionsProperty)
        {
            PseudoClasses.Set(HasActionsPseudoClass, change.NewValue is not null);
            // Action controls join the logical tree here (so DataContext flows) unless their owner already holds them.
            if (change.OldValue is ILogical { LogicalParent: var oldParent } oldLogical && ReferenceEquals(oldParent, this))
            {
                LogicalChildren.Remove(oldLogical);
            }
            if (change.NewValue is ILogical { LogicalParent: null } newLogical)
            {
                LogicalChildren.Add(newLogical);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ModalSurfaceAutomationPeer(this);

    private sealed class ModalSurfaceAutomationPeer : ControlAutomationPeer
    {
        public ModalSurfaceAutomationPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
