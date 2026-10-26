using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>Event data of <see cref="ActionSheet.ActionInvoked"/>.</summary>
public sealed class ActionSheetActionEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    public ActionSheetActionEventArgs(ActionSheetItem action)
    {
        Action = action;
    }

    /// <summary>The row the person chose.</summary>
    public ActionSheetItem Action { get; }
}

/// <summary>
/// A bottom list of actions: an optional <see cref="Sheet.Title"/> and <see cref="Message"/>, a card of
/// <see cref="ActionSheetItem"/> rows and, set apart under it, a Cancel row. Choosing a row raises
/// <see cref="ActionInvoked"/> (and runs the row's own <c>Command</c>) and closes the sheet; so does Cancel. It is a
/// <see cref="Sheet"/>, so it opens with <see cref="Sheet.IsOpen"/> in <see cref="Page.Overlay"/>, closes on a scrim tap, a
/// pull down or Escape, and moves by the same physics: its position is a motion value written on every drag move, not a
/// ControlTheme transition, and it jumps instead of gliding under reduced motion. Rows shade while pressed.
/// </summary>
[TemplatePart("PART_Cancel", typeof(ActionSheetItem))]
public class ActionSheet : Sheet
{
    /// <summary>A second, smaller line under the title.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ActionSheet, string?>(nameof(Message));

    /// <summary>The label of the Cancel row: "Cancel". Null or empty hides the row.</summary>
    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<ActionSheet, string?>(nameof(CancelText), "Cancel");

    /// <summary>Whether there is a title or a message, so the header shows.</summary>
    public static readonly DirectProperty<ActionSheet, bool> HasHeaderProperty =
        AvaloniaProperty.RegisterDirect<ActionSheet, bool>(nameof(HasHeader), o => o.HasHeader);

    private ActionSheetItem? _cancel;
    private bool _hasHeader;

    /// <summary>Creates an action sheet with no actions.</summary>
    public ActionSheet()
    {
        AddHandler(AvaloniaButton.ClickEvent, OnItemClick);
    }

    /// <summary>The action rows, in order.</summary>
    [Content]
    public AvaloniaList<ActionSheetItem> Actions { get; } = [];

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <inheritdoc cref="CancelTextProperty"/>
    public string? CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    /// <inheritdoc cref="HasHeaderProperty"/>
    public bool HasHeader
    {
        get => _hasHeader;
        private set => SetAndRaise(HasHeaderProperty, ref _hasHeader, value);
    }

    /// <summary>The person chose an action; the sheet is closing.</summary>
    public event EventHandler<ActionSheetActionEventArgs>? ActionInvoked;

    protected override Type StyleKeyOverride => typeof(ActionSheet);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _cancel = e.NameScope.Find<ActionSheetItem>("PART_Cancel");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == MessageProperty)
        {
            HasHeader = !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Message);
        }
    }

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not ActionSheetItem item)
        {
            return;
        }
        if (!ReferenceEquals(item, _cancel))
        {
            ActionInvoked?.Invoke(this, new ActionSheetActionEventArgs(item));
        }
        IsOpen = false;
    }
}
