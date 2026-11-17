using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Mobile.DesignTokens;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The long-press edit menu of a text field: a row of Cut, Copy, Paste and Select All above the text, as on a phone.
/// <see cref="TextField"/> and <see cref="Searchbar"/> use it as their <c>ContextFlyout</c>; set it as the
/// <c>ContextFlyout</c> of any other <see cref="TextBox"/> to give it the same menu.
/// </summary>
/// <remarks>
/// The menu shows only the actions that apply: Cut and Copy need a selection, so a password box (<c>PasswordChar</c>
/// set) never offers them; Cut and Paste need an editable box; Select All needs text that is not all selected yet. When
/// none applies the menu does not open. It opens without taking focus, so the soft keyboard stays up, and a tap outside
/// closes it and still reaches what was tapped. Avalonia places it over the selection when it opens from the selection
/// handles; at a press it sits above the field at the finger.
/// <para>
/// A platform package can show the system's own edit menu instead through <see cref="Platform"/>:
/// <c>TKCode.Arc.Mobile.iOS</c> does on iOS 16 and later, where the system menu also pastes without the "Allow Paste"
/// prompt. Everywhere else the Arc surface shows.
/// </para>
/// </remarks>
public class TextEditMenu : PopupFlyoutBase
{
    static TextEditMenu()
    {
        PlacementProperty.OverrideDefaultValue<TextEditMenu>(PlacementMode.TopEdgeAlignedLeft);
        ShowModeProperty.OverrideDefaultValue<TextEditMenu>(FlyoutShowMode.Transient);
        OverlayDismissEventPassThroughProperty.OverrideDefaultValue<TextEditMenu>(true);
    }

    /// <summary>Creates the menu. Its popup uses the window's overlay layer, as other Arc popups do.</summary>
    public TextEditMenu()
    {
        Popup.ShouldUseOverlayLayer = true;
    }

    private Rect? _pressAnchor;

    /// <summary>
    /// The platform's own edit menu, shown in place of the Arc surface when set; null (the default) shows the Arc
    /// surface. Set once at startup by a platform package (<c>TKCode.Arc.Mobile.iOS</c>), not per menu.
    /// </summary>
    public static ITextEditMenuPlatform? Platform { get; set; }

    /// <summary>The text box the menu is open for, or null while it is closed.</summary>
    internal TextBox? TextBox => Target as TextBox;

    internal TextEditMenuPresenter? Presenter => Popup.Child as TextEditMenuPresenter;

    protected override Control CreatePresenter() => new TextEditMenuPresenter(this);

    protected override bool ShowAtCore(Control placementTarget, bool showAtPointer = false)
    {
        if (Platform is { } platform && placementTarget is TextBox box)
        {
            if (IsOpen)
            {
                Hide();
            }
            if (platform.TryShow(box, _pressAnchor ?? SelectionAnchor(box)))
            {
                return true;
            }
        }
        return base.ShowAtCore(placementTarget, showAtPointer);
    }

    protected override void OnOpening(CancelEventArgs args)
    {
        base.OnOpening(args);
        if (args.Cancel)
        {
            return;
        }
        if (TextBox is not { } box || Presenter is not { } presenter || !presenter.Update(box))
        {
            args.Cancel = true;
        }
    }

    /// <summary>
    /// Opens the menu above <paramref name="target"/>, horizontally centered on <paramref name="x"/> (target
    /// coordinates), the way a phone opens its edit menu at a long press. Returns whether it opened.
    /// </summary>
    internal bool ShowAbove(Control target, double x)
    {
        var gap = target.TryFindResource(TextEditMenuTokens.Key(TextEditMenuTokenKind.AnchorGap), out var value) && value is double d ? d : 8;
        var placement = Placement;
        var callback = CustomPopupPlacementCallback;
        // For a platform menu: the finger's column, over the whole height of the field, which the menu keeps clear of.
        _pressAnchor = new Rect(x, 0, 0, target.Bounds.Height);
        try
        {
            Placement = PlacementMode.Custom;
            CustomPopupPlacementCallback = p =>
            {
                p.Anchor = PopupAnchor.TopLeft;
                p.Gravity = PopupGravity.Top;
                p.Offset = new Point(x, -gap);
            };
            return ShowAtCore(target);
        }
        finally
        {
            _pressAnchor = null;
            Placement = placement;
            CustomPopupPlacementCallback = callback;
        }
    }

    /// <summary>Closes the menu, the Arc surface or the platform's, if it is showing for <paramref name="box"/>.</summary>
    internal void HideFor(TextBox box)
    {
        if (IsOpen && Target == box)
        {
            Hide();
        }
        Platform?.Hide(box);
    }

    /// <summary>
    /// Where a platform menu points: the selection's bounds, or the caret's when nothing is selected, in
    /// <paramref name="box"/> coordinates and inside its width. A platform menu sits above the top of this rectangle.
    /// </summary>
    internal static Rect SelectionAnchor(TextBox box)
    {
        var fallback = new Rect(box.Bounds.Width / 2, 0, 0, 0);
        if (box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is not { } presenter)
        {
            return fallback;
        }
        var layout = presenter.TextLayout;
        var start = Math.Min(box.SelectionStart, box.SelectionEnd);
        var end = Math.Max(box.SelectionStart, box.SelectionEnd);
        Rect rect;
        if (end > start)
        {
            var parts = layout.HitTestTextRange(start, end - start).ToList();
            if (parts.Count == 0)
            {
                return fallback;
            }
            rect = parts.Aggregate((a, b) => a.Union(b));
        }
        else
        {
            rect = layout.HitTestTextPosition(box.CaretIndex);
        }
        if (presenter.TranslatePoint(rect.TopLeft, box) is not { } origin)
        {
            return fallback;
        }
        var left = Math.Clamp(origin.X, 0, box.Bounds.Width);
        var right = Math.Clamp(origin.X + rect.Width, 0, box.Bounds.Width);
        return new Rect(left, origin.Y, right - left, rect.Height);
    }

    internal void Invoke(TextEditMenuAction action)
    {
        var box = TextBox;
        Hide();
        if (box is null)
        {
            return;
        }
        switch (action)
        {
            case TextEditMenuAction.Cut:
                box.Cut();
                break;
            case TextEditMenuAction.Copy:
                box.Copy();
                break;
            case TextEditMenuAction.Paste:
                box.Paste();
                break;
            case TextEditMenuAction.SelectAll:
                box.SelectAll();
                // As on a phone, the menu comes back for the new selection (Cut, Copy, Paste).
                Dispatcher.UIThread.Post(() =>
                {
                    if (box.ContextFlyout == this && box.IsEffectivelyVisible && box.SelectionStart != box.SelectionEnd)
                    {
                        box.RaiseEvent(new ContextRequestedEventArgs());
                    }
                });
                break;
        }
    }
}

/// <summary>The actions of <see cref="TextEditMenu"/>, in the order they appear.</summary>
internal enum TextEditMenuAction
{
    Cut,
    Copy,
    Paste,
    SelectAll,
}
