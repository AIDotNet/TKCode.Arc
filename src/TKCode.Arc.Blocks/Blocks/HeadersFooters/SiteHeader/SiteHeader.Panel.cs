// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli).
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The mega panel's surface: a raised, bordered, rounded panel whose height springs to the current face. It enters from
/// the bar (opacity 0 to 1 in 160 ms on the enter curve; y -6 to 0 and scale .985 to 1 on GROW) and leaves faster (180 ms
/// on the standard curve to opacity 0, y -4, scale .99); the transform origin is its top middle.
/// </summary>
/// <remarks>
/// Code-driven: the panel is added and removed with its own presence animation (it stays on screen while it leaves) and
/// its height follows the measured size of whichever face is current, carrying velocity (see <see cref="SiteHeightBox"/>).
/// </remarks>
internal sealed class SiteHeaderPanelView : Border
{
    private static readonly SpringSpec s_grow = SpringSpec.FromVisual(0.44, 0);

    private readonly SiteHeightBox _box = new();
    private readonly SiteHeaderFaceHost _host = new();
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _y;
    private MotionValue<double>? _scaleValue;
    private int _generation;

    public SiteHeaderPanelView()
    {
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        _box.Child = _host;
        Child = _box;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);
        ArcTokens.Bind(this, BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
    }

    /// <summary>The face the panel shows (the newest one).</summary>
    public SiteHeaderFace? CurrentFace { get; private set; }

    /// <summary>True from <see cref="Exit"/> until the panel is removed or re-entered.</summary>
    public bool IsExiting { get; private set; }

    /// <summary>The height box (tests read the spring state).</summary>
    public SiteHeightBox HeightBox => _box;

    /// <summary>The face host (tests count the faces).</summary>
    public SiteHeaderFaceHost Faces => _host;

    /// <summary>The panel's own opacity, offset and scale now (tests read them).</summary>
    public (double Opacity, double Y, double Scale) Pose => (Opacity, _translate.Y, _scale.ScaleX);

    /// <summary>Raised once the exit has finished and the panel can be removed.</summary>
    public event EventHandler? Exited;

    private MotionValue<double> OpacityValue()
    {
        if (_opacity is null)
        {
            _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            _opacity.Changed += v => Opacity = v;
        }
        return _opacity;
    }

    private MotionValue<double> YValue()
    {
        if (_y is null)
        {
            _y = new MotionValue<double>(this, 0, MotionVectors.Double);
            _y.Changed += v => _translate.Y = v;
        }
        return _y;
    }

    private MotionValue<double> ScaleValue()
    {
        if (_scaleValue is null)
        {
            _scaleValue = new MotionValue<double>(this, 1, MotionVectors.Double);
            _scaleValue.Changed += v => _scale.ScaleX = _scale.ScaleY = v;
        }
        return _scaleValue;
    }

    /// <summary>Shows the first face (no face animation: Arc's <c>AnimatePresence initial={false}</c>) and plays the panel's entrance.</summary>
    public void Show(SiteHeaderFace face)
    {
        CurrentFace = face;
        _host.Children.Add(face);
        PlayEnter(true);
    }

    /// <summary>Brings in <paramref name="face"/> from the side of <paramref name="direction"/> and sends the current face away.</summary>
    public void Switch(SiteHeaderFace face, int direction)
    {
        var old = CurrentFace;
        CurrentFace = face;
        _host.Children.Add(face);
        face.Enter(direction);
        if (old is not null)
        {
            _ = RemoveWhenGone(old, old.Leave(direction));
        }
    }

    /// <summary>Swaps the face for a rebuilt one without any motion (the header's items changed).</summary>
    public void Replace(SiteHeaderFace face)
    {
        if (CurrentFace is { } old)
        {
            _host.Children.Remove(old);
        }
        CurrentFace = face;
        _host.Children.Add(face);
    }

    private async Task RemoveWhenGone(SiteHeaderFace face, Task leaving)
    {
        await leaving;
        _host.Children.Remove(face);
    }

    private void PlayEnter(bool fresh)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level != ArcMotionLevel.Full)
        {
            // Reduced: Arc's reduced branch is an opacity-only entrance with duration 0, so it simply appears; Off too.
            OpacityValue().Jump(1);
            YValue().Jump(0);
            ScaleValue().Jump(1);
            return;
        }
        if (fresh)
        {
            OpacityValue().Jump(0);
            YValue().Jump(-6);
            ScaleValue().Jump(0.985);
        }
        _ = OpacityValue().AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        _ = YValue().AnimateTo(0, MotionSpec.Spring(s_grow));
        _ = ScaleValue().AnimateTo(1, MotionSpec.Spring(s_grow));
    }

    /// <summary>Plays the exit; <see cref="Exited"/> follows unless the panel is re-entered meanwhile.</summary>
    public async void Exit()
    {
        if (IsExiting)
        {
            return;
        }
        IsExiting = true;
        IsHitTestVisible = false;
        var generation = ++_generation;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Full)
        {
            var spec = MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard);
            await Task.WhenAll(OpacityValue().AnimateTo(0, spec), YValue().AnimateTo(-4, spec), ScaleValue().AnimateTo(0.99, spec));
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            await OpacityValue().AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        if (generation == _generation && IsExiting)
        {
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Cancels an exit in progress: the panel comes back from where it is.</summary>
    public void Reenter()
    {
        if (!IsExiting)
        {
            return;
        }
        IsExiting = false;
        IsHitTestVisible = true;
        _generation++;
        PlayEnter(false);
    }

    /// <summary>The panel's focusable destinations in reading order: the links, then the feature card.</summary>
    public List<Control> FocusTargets()
    {
        var list = new List<Control>();
        if (CurrentFace?.Child is Border { Child: SiteHeaderFaceGrid grid })
        {
            foreach (var child in grid.Children)
            {
                if (child is SiteHeaderLinkGrid links)
                {
                    list.AddRange(links.Children);
                }
                else
                {
                    list.Add(child);
                }
            }
        }
        return list;
    }
}

public partial class SiteHeader
{
    private SiteHeaderPanelView? _panelView;

    /// <summary>The mega panel while it is on screen (tests).</summary>
    internal SiteHeaderPanelView? PanelView => _panelView;

    private int IndexOf(string value) => _resolved.FindIndex(i => string.Equals(i.Value, value, StringComparison.Ordinal));

    private void OpenPanelCore(string value, bool focusFirst)
    {
        if (_panelLayer is null || IsCollapsed || ItemByValue(value) is not { } item || !HasPanel(item))
        {
            return;
        }
        if (string.Equals(_openValue, value, StringComparison.Ordinal) && _panelView is { IsExiting: false })
        {
            if (focusFirst)
            {
                FocusFirstPanelLink();
            }
            return;
        }
        // The direction is the sign of the index difference from the previously open item; from closed it is 0.
        var direction = 0;
        if (_openValue is not null && _panelView is { IsExiting: false })
        {
            direction = Math.Sign(IndexOf(value) - IndexOf(_openValue));
        }
        var face = BuildFace(item);
        if (_panelView is null)
        {
            var view = new SiteHeaderPanelView();
            SiteHeaderFloatLayer.SetPlacement(view, SiteHeaderPlacement.Panel);
            view.Exited += OnPanelExited;
            _panelView = view;
            _panelLayer.Children.Add(view);
            AutomationProperties.SetName(view, item.Label);
            view.Show(face);
        }
        else if (_panelView.IsExiting)
        {
            _panelView.Reenter();
            AutomationProperties.SetName(_panelView, item.Label);
            if (!string.Equals(_panelView.CurrentFace?.ItemValue, value, StringComparison.Ordinal))
            {
                _panelView.Switch(face, 0);
            }
        }
        else
        {
            AutomationProperties.SetName(_panelView, item.Label);
            _panelView.Switch(face, direction);
        }
        OpenItemValue = value;
        foreach (var (v, button) in _buttons)
        {
            button.IsExpanded = HasPanel(ItemByValue(v)!) && string.Equals(v, value, StringComparison.Ordinal);
        }
        UpdatePseudoClasses();
        UpdateOutside();
        if (focusFirst)
        {
            // Arc focuses the first link on the next frame, once the face is in place.
            Dispatcher.UIThread.Post(FocusFirstPanelLink, DispatcherPriority.Loaded);
        }
    }

    private void FocusFirstPanelLink()
    {
        if (_panelView is { IsExiting: false } view && view.FocusTargets().FirstOrDefault() is { } first)
        {
            first.Focus(NavigationMethod.Directional);
        }
    }

    private void ClosePanelCore(bool restoreFocus)
    {
        StopTimers();
        var was = _openValue;
        if (was is null && _panelView is null)
        {
            return;
        }
        OpenItemValue = null;
        foreach (var button in _buttons.Values)
        {
            button.IsExpanded = false;
        }
        UpdatePseudoClasses();
        UpdateOutside();
        _panelView?.Exit();
        if (restoreFocus && was is not null)
        {
            NavButton(was)?.Focus(NavigationMethod.Directional);
        }
    }

    private void OnPanelExited(object? sender, EventArgs e)
    {
        if (sender is not SiteHeaderPanelView view || !ReferenceEquals(view, _panelView))
        {
            return;
        }
        view.Exited -= OnPanelExited;
        _panelLayer?.Children.Remove(view);
        _panelView = null;
    }

    /// <summary>Items changed while a panel is open: show the rebuilt face without motion (or close when the item is gone).</summary>
    private void RefreshPanelFace()
    {
        if (_openValue is null || _panelView is null || ItemByValue(_openValue) is not { } item)
        {
            return;
        }
        _panelView.Replace(BuildFace(item));
    }

    private SiteHeaderFace BuildFace(SiteHeaderItem item)
    {
        // Metrics of SiteHeaderToken: FacePadding 12, FaceGap 8, FeatureMinWidth 200 / MaxWidth 260, LinkGridGap 2.
        var links = new SiteHeaderLinkGrid();
        foreach (var link in item.Links)
        {
            var button = new SitePanelLink { Icon = link.Icon, Title = link.Label, Description = link.Description, Tag = link };
            AutomationProperties.SetName(button, link.Label);
            if (!string.IsNullOrEmpty(link.Description))
            {
                AutomationProperties.SetHelpText(button, link.Description);
            }
            var captured = link;
            button.Click += (_, _) => Choose(SiteHeaderNavigateKind.PanelLink, captured.Label, captured.Href, item, captured, null);
            links.Children.Add(button);
        }
        var grid = new SiteHeaderFaceGrid();
        grid.Children.Add(links);
        if (item.Feature is { } feature)
        {
            var card = new SiteFeatureCard { Image = feature.Image, Title = feature.Title, Description = feature.Description, Tag = feature };
            AutomationProperties.SetName(card, feature.Title);
            if (!string.IsNullOrEmpty(feature.Description))
            {
                AutomationProperties.SetHelpText(card, feature.Description);
            }
            card.Click += (_, _) => Choose(SiteHeaderNavigateKind.Feature, feature.Title, feature.Href, item, null, feature);
            grid.Children.Add(card);
        }
        return new SiteHeaderFace { ItemValue = item.Value, Child = new Border { Padding = new Thickness(12), Child = grid } };
    }
}
