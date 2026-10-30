using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class SourceCards
{
    /// <summary>The hover intent before a preview opens.</summary>
    internal static readonly TimeSpan PreviewIntent = TimeSpan.FromMilliseconds(320);

    /// <summary>The grace after the pointer leaves a card, so moving to the next card switches the preview instead of
    /// closing and reopening it.</summary>
    internal static readonly TimeSpan PreviewGrace = TimeSpan.FromMilliseconds(90);

    private DispatcherTimer? _intentTimer;
    private DispatcherTimer? _graceTimer;
    private SourceCardsCard? _hovered;
    private SourceCardsCard? _previewed;

    /// <summary>Whether a preview is open.</summary>
    internal bool IsPreviewOpen => _popup?.IsOpen == true && _previewed is not null;

    /// <summary>The card whose preview is open.</summary>
    internal SourceCardsCard? PreviewedCard => _previewed;

    internal SourceCardsPreview? PreviewView => _preview;

    private void OnCardPointerEntered(object? sender, PointerEventArgs e)
    {
        // Touch has no hover preview: tapping a card opens it.
        if (sender is not SourceCardsCard card || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _hovered = card;
        _graceTimer?.Stop();
        if (IsPreviewOpen)
        {
            // An open preview switches to the next card at once.
            ShowPreview(card);
            return;
        }
        _intentTimer?.Stop();
        _intentTimer = new DispatcherTimer { Interval = PreviewIntent };
        _intentTimer.Tick += (_, _) =>
        {
            _intentTimer?.Stop();
            if (_hovered is { } hovered)
            {
                ShowPreview(hovered);
            }
        };
        _intentTimer.Start();
    }

    private void OnCardPointerExited(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(sender, _hovered))
        {
            return;
        }
        _hovered = null;
        _intentTimer?.Stop();
        if (!IsPreviewOpen || _previewed?.IsKeyboardFocusWithin == true && _previewed.Classes.Contains(":focus-visible"))
        {
            return;
        }
        _graceTimer?.Stop();
        _graceTimer = new DispatcherTimer { Interval = PreviewGrace };
        _graceTimer.Tick += (_, _) =>
        {
            _graceTimer?.Stop();
            if (_hovered is null)
            {
                HidePreview(immediately: false);
            }
        };
        _graceTimer.Start();
    }

    private void OnCardGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // A card reached by the keyboard shows its preview at once.
        if (sender is SourceCardsCard card && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            ShowPreview(card);
        }
    }

    private void OnCardLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _previewed) && !ReferenceEquals(_hovered, sender))
        {
            HidePreview(immediately: false);
        }
    }

    /// <summary>
    /// Opens the preview on <paramref name="card"/>, or moves an open one there without motion. It aligns to the card's left
    /// edge when it fits inside the component and to its right edge otherwise, above the card, and drops below it when
    /// there is no room above (the popup flips).
    /// </summary>
    internal void ShowPreview(SourceCardsCard card)
    {
        if (_popup is null || _preview is null || card.Item is not { } source)
        {
            return;
        }
        _intentTimer?.Stop();
        _graceTimer?.Stop();
        if (_previewed is { } previous && !ReferenceEquals(previous, card))
        {
            AutomationProperties.SetHelpText(previous, null);
        }
        var domain = SourceCardsFormat.Domain(source.Url);
        _preview.Show(source, domain, SourceCardsFormat.Monogram(domain, source.Site ?? source.Title));
        // The preview never leaves the component: at most as wide, and on the side of the card where it fits.
        var wanted = double.IsFinite(_preview.Width) ? _preview.Width : 300;
        var width = Bounds.Width > 0 ? Math.Min(wanted, Bounds.Width) : wanted;
        _preview.MaxWidth = Bounds.Width > 0 ? Bounds.Width : double.PositiveInfinity;
        var left = card.TranslatePoint(default, this)?.X ?? 0;
        var fitsLeft = Bounds.Width <= 0 || left + width <= Bounds.Width + 0.5;
        _popup.Placement = fitsLeft ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.TopEdgeAlignedRight;
        _popup.PlacementTarget = card;
        // The preview is the card's description while it is open (Arc's aria-describedby).
        AutomationProperties.SetHelpText(card, string.IsNullOrWhiteSpace(source.Snippet) ? source.Title : source.Snippet);
        var wasOpen = IsPreviewOpen;
        _previewed = card;
        if (wasOpen)
        {
            _popup.Reposition();
        }
        else
        {
            _popup.IsOpen = true;
        }
    }

    /// <summary>Closes the preview (with its exit motion unless <paramref name="immediately"/>).</summary>
    internal void HidePreview(bool immediately)
    {
        _intentTimer?.Stop();
        _graceTimer?.Stop();
        if (_previewed is { } card)
        {
            AutomationProperties.SetHelpText(card, null);
        }
        _previewed = null;
        if (_popup is null || !_popup.IsOpen)
        {
            return;
        }
        if (immediately)
        {
            _popup.IsExitInstant = true;
        }
        _popup.IsOpen = false;
        _popup.IsExitInstant = false;
    }

    private void StopPreviewTimers()
    {
        _intentTimer?.Stop();
        _intentTimer = null;
        _graceTimer?.Stop();
        _graceTimer = null;
        _hovered = null;
    }
}
