using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

public partial class VideoPlayer
{
    // The browser's Native presentation (ADR 0036 decision 4, spec section 5.3): the player's own platform view (an HTML
    // video element) in the picture area. Nothing can be drawn over it, so:
    // - the control bar sits below the picture and always shows (the frame grows by its height);
    // - the view is hidden while something drawn in the picture area must be seen: the poster and big play button before
    //   the first play, the error layer, the replay button at the end, and the captions and speed menus;
    // - captions are shown by the platform view itself (IVideoNativePresentation.SetNativeCaptions).
    // Pointer input over the picture reaches the controls under the view (the backend makes the view ignore it), so a
    // click still plays or pauses and a double click toggles full screen.
    private Control? _nativeView;
    private IVideoNativePresentation? _nativeOwner;

    /// <summary>Whether the picture is the backend's platform view now (for tests).</summary>
    internal bool IsNativePresentationActive => _nativeView is not null;

    /// <summary>The platform view shown in the picture area, or null (for tests).</summary>
    internal Control? NativeView => _nativeView;

    /// <summary>Puts the platform view in or takes it out, following <see cref="BrowserPresentation"/>, the player and the template.</summary>
    private void UpdateNativePresentation()
    {
        var owner = BrowserPresentation == VideoBrowserPresentation.Native && _player is IVideoNativePresentation { CanPresentNatively: true } presentation
            ? presentation
            : null;
        var host = _surface?.Parent as Panel;
        if (owner is null || host is null)
        {
            RemoveNativeView();
            return;
        }
        if (_nativeView is not null && ReferenceEquals(owner, _nativeOwner) && ReferenceEquals(_nativeView.Parent, host))
        {
            UpdateNativeLayout();
            return;
        }
        RemoveNativeView();
        Control view;
        try
        {
            view = owner.CreateNativeView();
        }
        catch (NotSupportedException)
        {
            return;
        }
        view.IsHitTestVisible = false;
        _nativeOwner = owner;
        _nativeView = view;
        host.Children.Insert(host.Children.IndexOf(_surface!) + 1, view);
        _surface!.IsVisible = false;
        PseudoClasses.Set(NativePresentationPseudoClass, true);
        owner.SetNativeCaptions(SelectedTextTrack);
        UpdateNativeLayout();
        UpdateControls();
    }

    private void RemoveNativeView()
    {
        if (_nativeView is { } view)
        {
            _nativeOwner?.SetNativeCaptions(null);
            (view.Parent as Panel)?.Children.Remove(view);
            _nativeView = null;
            _nativeOwner = null;
            if (_surface is not null)
            {
                _surface.IsVisible = true;
            }
            if (_frame is not null)
            {
                _frame.ExtraHeight = 0;
            }
            if (_stateLayer is not null)
            {
                _stateLayer.Margin = default;
            }
            UpdateControls();
        }
        PseudoClasses.Set(NativePresentationPseudoClass, false);
    }

    /// <summary>Keeps the view above the control bar, grows the frame by the bar, and hides the view while the picture area must show Arc content.</summary>
    private void UpdateNativeLayout()
    {
        if (_nativeView is not { } view)
        {
            return;
        }
        var bar = _controls is { IsVisible: true } controls && ShowControls != VideoControlsVisibility.Never ? controls.Bounds.Height : 0;
        view.Margin = new Thickness(0, 0, 0, bar);
        if (_stateLayer is not null)
        {
            // The poster, big play button and error centre on the picture, not on picture plus bar.
            _stateLayer.Margin = view.Margin;
        }
        if (_frame is not null)
        {
            _frame.ExtraHeight = bar;
        }
        view.IsVisible = _hasStarted && State is not (VideoPlaybackState.Failed or VideoPlaybackState.Ended) && !IsMenuOpen;
    }
}
