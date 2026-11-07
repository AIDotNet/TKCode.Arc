using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class VideoPlayer
{
    private VideoFullScreenHost? _fullScreenHost;
    private Panel? _fullScreenLayer;
    private WindowState? _savedWindowState;
    private bool _changingFullScreen;

    /// <summary>The host of the root in the overlay while in full screen (for tests).</summary>
    internal VideoFullScreenHost? FullScreenHost => _fullScreenHost;

    private void OnIsFullScreenChanged(bool fullScreen)
    {
        if (_changingFullScreen)
        {
            return;
        }
        var changed = fullScreen ? TryEnterFullScreen() : LeaveFullScreen();
        if (!changed)
        {
            // Could not change (not attached, no overlay): keep the property truthful.
            _changingFullScreen = true;
            try
            {
                SetCurrentValue(IsFullScreenProperty, _fullScreenHost is not null);
            }
            finally
            {
                _changingFullScreen = false;
            }
            return;
        }
        PseudoClasses.Set(FullScreenPseudoClass, _fullScreenHost is not null);
        UpdateChrome();
        UpdateTexts();
        UpdateCompact();
        Activity();
        Announce(VideoText.Get(_fullScreenHost is not null ? VideoLangResourceKind.EnteredFullScreen : VideoLangResourceKind.ExitedFullScreen));
        RaiseEvent(new RoutedEventArgs(FullScreenChangedEvent, this));
    }

    /// <summary>
    /// Moves the root into the top level's overlay layer (Arc's <see cref="ArcOverlayLayer"/>), leaving the frame behind as a
    /// same-size placeholder; the backend player and the surface's player stay the same (spec section 10).
    /// </summary>
    private bool TryEnterFullScreen()
    {
        if (_fullScreenHost is not null)
        {
            return false;
        }
        if (!_attached || _root is null || _frame is null || ArcOverlayLayer.Find(this) is not { } layer)
        {
            return false;
        }
        _captionsButton?.CloseMenu();
        _speedButton?.CloseMenu();
        var hadFocus = _root.IsKeyboardFocusWithin;
        _frame.Children.Remove(_root);
        var host = new VideoFullScreenHost();
        host.Children.Add(_root);
        _fullScreenHost = host;
        _fullScreenLayer = layer;
        SizeHost();
        layer.PropertyChanged += OnFullScreenLayerChanged;
        layer.Children.Add(host);
        if (FullScreenMode == VideoFullScreenMode.System && TopLevel.GetTopLevel(this) is Window window)
        {
            _savedWindowState = window.WindowState;
            window.WindowState = WindowState.FullScreen;
        }
        if (hadFocus || _root.Focusable)
        {
            _root.Focus(NavigationMethod.Unspecified);
        }
        return true;
    }

    /// <summary>Puts the root back into the frame and restores the window.</summary>
    private bool LeaveFullScreen()
    {
        if (_fullScreenHost is not { } host)
        {
            return false;
        }
        _captionsButton?.CloseMenu();
        _speedButton?.CloseMenu();
        var hadFocus = _root?.IsKeyboardFocusWithin == true;
        if (_fullScreenLayer is { } layer)
        {
            layer.PropertyChanged -= OnFullScreenLayerChanged;
            layer.Children.Remove(host);
        }
        host.Children.Clear();
        _fullScreenHost = null;
        _fullScreenLayer = null;
        if (_root is not null && _frame is not null)
        {
            _frame.Children.Insert(0, _root);
            if (hadFocus)
            {
                _root.Focus(NavigationMethod.Unspecified);
            }
        }
        if (_savedWindowState is { } state && TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = state;
        }
        _savedWindowState = null;
        if (IsFullScreen)
        {
            _changingFullScreen = true;
            try
            {
                SetCurrentValue(IsFullScreenProperty, false);
            }
            finally
            {
                _changingFullScreen = false;
            }
        }
        return true;
    }

    private void OnFullScreenLayerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            SizeHost();
        }
    }

    private void SizeHost()
    {
        if (_fullScreenHost is null || _fullScreenLayer is null)
        {
            return;
        }
        if (_fullScreenLayer is Canvas)
        {
            var size = _fullScreenLayer.Bounds.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                size = ArcViewport.GetSize(this) ?? size;
            }
            _fullScreenHost.Width = size.Width;
            _fullScreenHost.Height = size.Height;
            Canvas.SetLeft(_fullScreenHost, 0);
            Canvas.SetTop(_fullScreenHost, 0);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new VideoPlayerAutomationPeer(this);

    /// <summary>The player for screen readers: a group named by its title, localized as "Video player" (spec section 12).</summary>
    private sealed class VideoPlayerAutomationPeer(VideoPlayer owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => VideoText.Get(VideoLangResourceKind.VideoPlayer);

        protected override string? GetNameCore() => owner.AccessibleName;

        protected override string GetClassNameCore() => nameof(VideoPlayer);
    }
}
