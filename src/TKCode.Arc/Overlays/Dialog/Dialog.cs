using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A button of a code-built dialog (<see cref="Dialog.ShowAsync(Visual, string, object?, string?, DialogAction[])"/>).</summary>
/// <param name="Label">Button text.</param>
/// <param name="Result">What <see cref="Dialog.ShowAsync(Visual, string, object?, string?, DialogAction[])"/> returns when it is clicked.</param>
/// <param name="Variant">Button variant (Secondary for dismissive actions, Primary or Danger for the main one).</param>
public sealed record DialogAction(string Label, object? Result, ButtonVariant Variant = ButtonVariant.Secondary);

/// <summary>
/// A focused surface for decisions that need attention: confirmations, short forms, step flows whose title crossfades.
/// Declare it anywhere in XAML; it renders nothing in place and shows its panel above a blurred scrim while
/// <see cref="ModalOverlay.IsOpen"/> is true:
/// <code>
/// &lt;arc:Dialog Title="Rename project" Description="…" IsOpen="{Binding IsRenaming}"&gt;
///     &lt;TextBox Text="{Binding Name}"/&gt;
///     &lt;arc:Dialog.Actions&gt;
///         &lt;StackPanel Orientation="Horizontal" Spacing="8"&gt;
///             &lt;arc:Button Variant="Secondary" Content="Cancel" arc:Dialog.Result="{x:False}"/&gt;
///             &lt;arc:Button Content="Save" arc:Dialog.Result="{x:True}"/&gt;
///         &lt;/StackPanel&gt;
///     &lt;/arc:Dialog.Actions&gt;
/// &lt;/arc:Dialog&gt;
/// </code>
/// The overlay fades in while the panel rises 8px from 0.96 on the smooth spring; closing is shorter and starts from
/// wherever the entrance is. Escape and a press on the scrim close it (cancellable through <see cref="ModalOverlay.Closing"/>);
/// Tab cycles inside; focus returns to the opener. Code can await a result with <see cref="ShowAsync(Visual, string, object?, string?, DialogAction[])"/>
/// or <see cref="ConfirmAsync"/>.
/// </summary>
public class Dialog : ModalOverlay
{
    /// <summary>Panel width: Small 360, Medium 440 (Arc), Large 560 — always within the viewport minus 32px.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        DialogSurface.SizeProperty.AddOwner<Dialog>();

    /// <summary>
    /// Set on a button inside a dialog, drawer or bottom sheet: clicking it closes that overlay with this value as its
    /// <see cref="ModalOverlay.CloseResult"/> (and the result of <see cref="ModalOverlay.ShowAsync(Visual?)"/>).
    /// </summary>
    public static readonly AttachedProperty<object?> ResultProperty =
        AvaloniaProperty.RegisterAttached<Dialog, AvaloniaButton, object?>("Result");

    public static object? GetResult(AvaloniaButton element) => element.GetValue(ResultProperty);

    public static void SetResult(AvaloniaButton element, object? value) => element.SetValue(ResultProperty, value);

    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    static Dialog()
    {
        CloseButtonNameProperty.OverrideDefaultValue<Dialog>("Close dialog");
        AvaloniaButton.ClickEvent.AddClassHandler<AvaloniaButton>(OnAnyButtonClick, RoutingStrategies.Bubble);
    }

    private static void OnAnyButtonClick(AvaloniaButton button, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, button) || !button.IsSet(ResultProperty))
        {
            return;
        }
        foreach (var ancestor in button.GetLogicalAncestors())
        {
            if (ancestor is IOverlayResultTarget target)
            {
                target.CloseWithResult(button.GetValue(ResultProperty));
                return;
            }
        }
    }

    protected override ModalSurface CreateSurface()
    {
        var surface = new DialogSurface();
        surface.Bind(DialogSurface.SizeProperty, this.GetObservable(SizeProperty));
        return surface;
    }

    protected override void ConfigurePresence(ModalPresence presence) => presence.Entrance = ModalEntrance.Rise;

    #region Code helpers

    /// <summary>
    /// Shows a dialog built from arguments (title, optional body content and description, action buttons) in the window
    /// of <paramref name="owner"/> and returns the <see cref="DialogAction.Result"/> of the clicked action (null when
    /// dismissed with Escape, the scrim or the close button).
    /// </summary>
    public static Task<object?> ShowAsync(Visual owner, string title, object? content = null, string? description = null,
                                          params DialogAction[] actions)
    {
        var dialog = new Dialog
        {
            Title = title,
            Description = description,
            Content = content,
            Actions = actions.Length == 0 ? null : BuildActions(actions)
        };
        return dialog.ShowAsync(owner);
    }

    /// <summary>
    /// Asks for confirmation with a Cancel and a confirm button. Returns true only when the confirm button was clicked.
    /// </summary>
    public static async Task<bool> ConfirmAsync(Visual owner, string title, string? description = null, string confirmText = "Confirm",
                                                string cancelText = "Cancel", bool isDanger = false)
    {
        var result = await ShowAsync(owner, title, null, description,
            new DialogAction(cancelText, false),
            new DialogAction(confirmText, true, isDanger ? ButtonVariant.Danger : ButtonVariant.Primary));
        return result is true;
    }

    /// <summary>Builds a right-aligned row of buttons that close the dialog with their results.</summary>
    public static Control BuildActions(IEnumerable<DialogAction> actions)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var action in actions)
        {
            var button = new Button { Content = action.Label, Variant = action.Variant };
            SetResult(button, action.Result);
            row.Children.Add(button);
        }
        return row;
    }

    #endregion
}
