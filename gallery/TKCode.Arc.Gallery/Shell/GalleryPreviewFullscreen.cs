using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The Preview's Fullscreen view (design/engineering/gallery-mobile-preview.md, section 3.5): a panel over the whole
/// window that grows out of the stage, with the component's name, the Fit / Mobile switch and a close button. The demo
/// view moves in (its state stays) and goes back when the panel closes. Escape and the close button close it; focus
/// returns to the button that opened it (<see cref="ModalPresence"/>).
/// </summary>
/// <remarks>
/// The grow and shrink are code-driven (<see cref="ModalEntrance.Custom"/>): they run from the stage's measured place
/// on the page, which no ControlTheme transition knows.
/// </remarks>
public sealed class GalleryPreviewFullscreen : Border
{
    private const double StageRadius = 26;
    private const double BodyPadding = 24;

    private readonly Control _movable;
    private readonly Border _fit;
    private readonly Border _body;
    private readonly GalleryPreviewPhone? _phone;
    private readonly GallerySegmented? _size;
    private readonly ModalPresence _presence;
    private readonly MotionValue<double> _progress;
    private Rect _from;

    /// <summary>
    /// Builds the panel around <paramref name="movable"/> (the demo view, or the phone frame a TKCode.Arc.Mobile demo
    /// runs in). <paramref name="canMobile"/> offers the Fit / Mobile switch, starting at <paramref name="mobile"/>;
    /// <paramref name="opener"/> is the button that opened it.
    /// </summary>
    public GalleryPreviewFullscreen(string title, Control movable, bool canMobile, bool mobile, Control opener)
    {
        _movable = movable;
        IsMobile = canMobile && mobile;

        var heading = new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        CloseButton = GalleryPreviewTools.IconButton(GalleryIcons.X, GalleryShellLangResourceKind.PreviewCloseFullscreen);
        CloseButton.Click += (_, _) => Close();
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(20, 12) };
        header.Children.Add(heading);
        if (canMobile)
        {
            _size = GalleryPreviewTools.SizeSwitch();
            _size.SelectedIndex = IsMobile ? 1 : 0;
            _size.SelectionChanged += index => ShowMobile(index == 1);
            Grid.SetColumn(_size, 1);
            header.Children.Add(_size);
            _phone = new GalleryPreviewPhone();
        }
        CloseButton.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(CloseButton, 2);
        header.Children.Add(CloseButton);
        var rule = new Border { Height = 1 };
        ArcTokens.Bind(rule, BackgroundProperty, ArcDesignTokenKind.BorderSubtle);

        _fit = new Border { VerticalAlignment = VerticalAlignment.Center };
        _body = new Border { Padding = new Thickness(BodyPadding), ClipToBounds = true };
        ArcTokens.Bind(_body, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _body.SizeChanged += (_, _) => FitPhone();

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(rule, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(rule);
        layout.Children.Add(_body);

        ClipToBounds = true;
        Child = layout;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        AutomationProperties.SetName(this, title);
        Place();

        _progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        _progress.Changed += Apply;
        _presence = new ModalPresence(this, opener)
        {
            Entrance = ModalEntrance.Custom,
            CustomMotion = PlayAsync,
            DismissOnScrimPress = false,
            InitialFocus = CloseButton
        };
        _presence.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised once the panel has closed (after its exit); the opener takes the demo back then.</summary>
    public event EventHandler? Closed;

    /// <summary>The close button in the header.</summary>
    public Button CloseButton { get; }

    /// <summary>The Fit / Mobile switch (null for a TKCode.Arc.Mobile demo, which is shown as it is).</summary>
    public GallerySegmented? SizeSwitch => _size;

    /// <summary>The phone the Mobile size shows the demo on (null without the switch).</summary>
    public GalleryPreviewPhone? Phone => _phone;

    /// <summary>Whether the demo is on the phone.</summary>
    public bool IsMobile { get; private set; }

    /// <summary>Whether the panel is open or opening.</summary>
    public bool IsOpen => _presence.IsOpen;

    /// <summary>Opens the panel, growing it from <paramref name="from"/> (the stage, in window coordinates).</summary>
    public void Open(Rect from)
    {
        _from = from;
        _presence.Open();
    }

    /// <summary>Closes the panel, shrinking it back to the stage.</summary>
    public void Close() => _presence.Close();

    /// <summary>Takes the demo out of the panel (once it has closed), so the opener can put it back.</summary>
    public Control Release()
    {
        GalleryPreviewTools.CloseOverlays(_movable);
        if (_phone is not null && ReferenceEquals(_phone.Screen, _movable))
        {
            _phone.Screen = null;
        }
        if (ReferenceEquals(_fit.Child, _movable))
        {
            _fit.Child = null;
        }
        _presence.Dispose();
        return _movable;
    }

    /// <summary>Switches between Fit and Mobile inside the panel (no morph: the panel is already the whole window).</summary>
    public void ShowMobile(bool mobile)
    {
        if (_phone is null)
        {
            return;
        }
        if (_size is not null && _size.SelectedIndex != (mobile ? 1 : 0))
        {
            _size.SelectedIndex = mobile ? 1 : 0;
        }
        if (mobile == IsMobile)
        {
            return;
        }
        IsMobile = mobile;
        GalleryPreviewTools.CloseOverlays(_movable);
        var focused = GalleryPreviewTools.FocusedIn(this, _movable);
        Place();
        GalleryPreviewTools.Refocus(focused);
    }

    private void Place()
    {
        if (IsMobile && _phone is not null)
        {
            _fit.Child = null;
            _phone.Screen = _movable;
            _body.Child = _phone;
            FitPhone();
        }
        else
        {
            if (_phone is not null)
            {
                _phone.Screen = null;
            }
            _fit.Child = _movable;
            _body.Child = _fit;
        }
    }

    /// <summary>The phone takes the body's height (and width), up to the viewport's real size.</summary>
    private void FitPhone()
    {
        if (_phone is null || _body.Bounds.Height <= 0)
        {
            return;
        }
        var room = _body.Bounds.Size.Deflate(new Thickness(BodyPadding));
        var bezel = 2 * GalleryPreviewPhone.Bezel;
        var scale = Math.Min(1, Math.Min((room.Height - bezel) / GalleryPreviewPhone.ViewportSize.Height,
                                         (room.Width - bezel) / GalleryPreviewPhone.ViewportSize.Width));
        _phone.SetScale(Math.Max(0.3, scale));
    }

    // 0: the panel covers the stage's rectangle with the stage's corners; 1: it covers the window, square.
    private void Apply(double progress)
    {
        var layer = _presence.LayerSize;
        if (layer.Width <= 0 || layer.Height <= 0)
        {
            return;
        }
        var t = Math.Clamp(progress, 0, 1.2);
        var left = _from.Left * (1 - t);
        var top = _from.Top * (1 - t);
        var right = Math.Max(0, layer.Width - _from.Right) * (1 - t);
        var bottom = Math.Max(0, layer.Height - _from.Bottom) * (1 - t);
        Margin = new Thickness(Math.Max(0, left), Math.Max(0, top), Math.Max(0, right), Math.Max(0, bottom));
        CornerRadius = new CornerRadius(StageRadius * Math.Clamp(1 - t, 0, 1));
        Opacity = Math.Clamp(progress * 4, 0, 1);
        _presence.ScrimOpacity = Math.Clamp(progress, 0, 1);
    }

    private Task<bool> PlayAsync(ModalPresence presence, bool entering)
    {
        if (entering)
        {
            _progress.Jump(0);
            Apply(0);
            return _progress.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        }
        return _progress.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(220), new CubicEaseInOut()));
    }
}

/// <summary>The pieces the Preview and its Fullscreen view share: the size switch, icon buttons and moving the demo.</summary>
internal static class GalleryPreviewTools
{
    /// <summary>The Fit / Mobile switch (lucide <c>scan</c> and <c>smartphone</c>).</summary>
    public static GallerySegmented SizeSwitch()
    {
        var size = new GallerySegmented((GalleryIcons.Scan, GalleryShellLangResourceKind.PreviewFit),
                                        (ArcIcons.Smartphone, GalleryShellLangResourceKind.PreviewMobile))
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        GalleryText.Bind(size, AutomationProperties.NameProperty, GalleryShellLangResourceKind.PreviewSize);
        return size;
    }

    /// <summary>A small ghost button that shows only a glyph; its tooltip and automation name follow the language.</summary>
    public static Button IconButton(Geometry glyph, GalleryShellLangResourceKind name)
    {
        var button = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Width = 30,
            Height = 30,
            MinHeight = 30,
            Padding = default,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new ArcGlyph { Data = glyph, Size = 15 }
        };
        GalleryText.Bind(button, Tooltip.TipProperty, name);
        GalleryText.Bind(button, AutomationProperties.NameProperty, name);
        return button;
    }

    /// <summary>The element inside <paramref name="view"/> that has keyboard focus, if any (moving a view drops focus).</summary>
    public static IInputElement? FocusedIn(Visual anyInWindow, Control view)
    {
        var focused = TopLevel.GetTopLevel(anyInWindow)?.FocusManager?.GetFocusedElement();
        return focused is Visual visual && (ReferenceEquals(visual, view) || Avalonia.VisualTree.VisualExtensions.IsVisualAncestorOf(view, visual))
            ? focused
            : null;
    }

    /// <summary>
    /// Gives focus back after a move. The focus manager drops focus from a detached element after the move, so focus goes
    /// back once that has run.
    /// </summary>
    public static void Refocus(IInputElement? element)
    {
        if (element is not InputElement input)
        {
            return;
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (input.IsEffectivelyVisible && TopLevel.GetTopLevel(input) is not null)
            {
                input.Focus();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Closes the popups and modals opened from <paramref name="view"/>: they belong to the place it leaves.</summary>
    public static void CloseOverlays(Control view)
    {
        foreach (var item in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view).Prepend(view).ToList())
        {
            if (item is Avalonia.Controls.Primitives.Popup { IsOpen: true } popup)
            {
                popup.IsOpen = false;
            }
            else if (item is ModalOverlay { IsOpen: true } modal)
            {
                modal.IsOpen = false;
            }
        }
        foreach (var popup in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<Avalonia.Controls.Primitives.Popup>().Where(p => p.IsOpen).ToList())
        {
            popup.IsOpen = false;
        }
    }
}
