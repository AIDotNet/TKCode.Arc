using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The content of a <see cref="ConfirmPopover"/>'s surface: the mark, the question, the line under it and the two
/// buttons. It mirrors its owner's text and tone; the owner decides what the buttons do.
/// </summary>
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(ConfirmPart, typeof(Button))]
[PseudoClasses(DestructivePseudoClass, HasMessagePseudoClass, HasIconPseudoClass)]
internal sealed class ConfirmPopoverPanel : TemplatedControl
{
    public const string CancelPart = "PART_Cancel";
    public const string ConfirmPart = "PART_Confirm";
    public const string DestructivePseudoClass = ":destructive";
    public const string HasMessagePseudoClass = ":has-message";
    public const string HasIconPseudoClass = ":has-icon";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ConfirmPopoverPanel, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ConfirmPopoverPanel, string?>(nameof(Message));

    public static readonly StyledProperty<string?> ConfirmTextProperty =
        AvaloniaProperty.Register<ConfirmPopoverPanel, string?>(nameof(ConfirmText));

    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<ConfirmPopoverPanel, string?>(nameof(CancelText));

    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<ConfirmPopoverPanel, bool>(nameof(IsDestructive), true);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ConfirmPopoverPanel, Geometry?>(nameof(Icon));

    private readonly ConfirmPopover _owner;
    private Button? _cancel;
    private Button? _confirm;
    private bool _busy;

    public ConfirmPopoverPanel(ConfirmPopover owner)
    {
        _owner = owner;
        Bind(TitleProperty, owner.GetObservable(ConfirmPopover.TitleProperty));
        Bind(MessageProperty, owner.GetObservable(ConfirmPopover.MessageProperty));
        Bind(ConfirmTextProperty, owner.GetObservable(ConfirmPopover.ConfirmTextProperty));
        Bind(CancelTextProperty, owner.GetObservable(ConfirmPopover.CancelTextProperty));
        Bind(IsDestructiveProperty, owner.GetObservable(ConfirmPopover.IsDestructiveProperty));
        Bind(IconProperty, owner.GetObservable(ConfirmPopover.IconProperty));
        UpdatePseudoClasses();
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public string? ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }

    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    public bool IsDestructive { get => GetValue(IsDestructiveProperty); set => SetValue(IsDestructiveProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public Button? CancelButton => _cancel;

    public Button? ConfirmButton => _confirm;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_confirm is not null)
        {
            _confirm.Click -= OnConfirmClick;
        }
        _cancel = e.NameScope.Find<Button>(CancelPart);
        _confirm = e.NameScope.Find<Button>(ConfirmPart);
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_confirm is not null)
        {
            _confirm.Click += OnConfirmClick;
        }
        UpdateButtons();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDestructiveProperty || change.Property == MessageProperty || change.Property == IconProperty)
        {
            UpdatePseudoClasses();
            UpdateButtons();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateButtons();
        }
    }

    public void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateButtons();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(DestructivePseudoClass, IsDestructive);
        PseudoClasses.Set(HasMessagePseudoClass, !string.IsNullOrEmpty(Message));
        PseudoClasses.Set(HasIconPseudoClass, Icon is not null);
    }

    private void UpdateButtons()
    {
        if (_confirm is not null)
        {
            _confirm.Variant = IsDestructive ? ButtonVariant.Danger : ButtonVariant.Primary;
            _confirm.IsLoading = _busy;
            // "Delete: Delete this comment?" reads the action with its question.
            AutomationProperties.SetHelpText(_confirm, Title ?? string.Empty);
        }
        if (_cancel is not null)
        {
            _cancel.IsEnabled = !_busy;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        // Left and Right move between the two buttons, as in a toolbar.
        if (e.Key is Key.Left or Key.Right && _cancel is not null && _confirm is not null)
        {
            var next = e.Key == Key.Right ? _confirm : _cancel;
            if (next.IsEffectivelyEnabled && (_cancel.IsFocused || _confirm.IsFocused))
            {
                next.Focus(NavigationMethod.Directional);
                e.Handled = true;
            }
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.CancelFromPanel();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = _owner.ConfirmCoreAsync();
    }
}
