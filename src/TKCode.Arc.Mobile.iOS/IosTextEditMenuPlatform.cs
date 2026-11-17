using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using CoreGraphics;
using UIKit;

namespace TKCode.Arc.Mobile;

/// <summary>
/// <see cref="TextEditMenu"/> as the system edit menu (<c>UIEditMenuInteraction</c>, iOS 16+). The menu lists the
/// standard edit actions that the responder chain can perform, which <see cref="ArcIosTextEditing"/> answers for the
/// focused text box, so it shows the same actions as the Arc surface would and pastes without a prompt.
/// </summary>
[SupportedOSPlatform("ios16.0")]
internal sealed class IosTextEditMenuPlatform : ITextEditMenuPlatform
{
    // UIEditMenuInteraction holds its delegate weakly; this one lives as long as the platform.
    private readonly EditMenuDelegate _delegate = new();
    private readonly ConditionalWeakTable<UIView, UIEditMenuInteraction> _interactions = new();
    private UIEditMenuInteraction? _shown;
    private WeakReference<TextBox>? _shownFor;

    public bool TryShow(TextBox box, Rect anchor)
    {
        if (TopLevel.GetTopLevel(box) is not { } topLevel || topLevel.TryGetPlatformHandle() is not UIViewControlHandle handle)
        {
            return false;
        }
        // TopLevel coordinates are the AvaloniaView's points.
        if (box.TranslatePoint(anchor.TopLeft, topLevel) is not { } origin)
        {
            return false;
        }
        var target = new CGRect(origin.X, origin.Y, anchor.Width, anchor.Height);
        if (!box.IsFocused)
        {
            // The actions go to the focused text box.
            box.Focus();
        }
        var interaction = _interactions.GetValue(handle.View, view =>
        {
            var created = new UIEditMenuInteraction(_delegate);
            view.AddInteraction(created);
            return created;
        });
        // The menu points at the target's top center and keeps clear of the whole target (above it when there is room).
        _delegate.TargetRect = target;
        var configuration = UIEditMenuConfiguration.Create(null, new CGPoint(target.GetMidX(), target.Y));
        // Above the text, as in a UITextField; UIKit still flips it below when there is no room.
        configuration.PreferredArrowDirection = UIEditMenuArrowDirection.Down;
        interaction.PresentEditMenu(configuration);
        _shown = interaction;
        _shownFor = new WeakReference<TextBox>(box);
        return true;
    }

    public void Hide(TextBox box)
    {
        if (_shown is null || _shownFor is null || !_shownFor.TryGetTarget(out var shownFor) || shownFor != box)
        {
            return;
        }
        _shown.DismissMenu();
        _shown = null;
        _shownFor = null;
    }

    private sealed class EditMenuDelegate : UIEditMenuInteractionDelegate
    {
        public CGRect TargetRect { get; set; }

        public override CGRect GetTargetRect(UIEditMenuInteraction interaction, UIEditMenuConfiguration configuration) => TargetRect;
    }
}
