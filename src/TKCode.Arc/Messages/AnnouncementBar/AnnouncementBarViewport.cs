using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The message window of an <see cref="AnnouncementBar"/>. Faces overlap in place: the next one rises from below
/// (the previous one drops from above) out of a soft blur on Arc's 0.46s spring while the old one leaves the other
/// way, and the window springs to the new face's height (0.42s, no bounce). Faces are pooled.
/// </summary>
internal sealed class AnnouncementBarViewport : Control
{
    public static readonly StyledProperty<bool> IsCenteredProperty =
        AvaloniaProperty.Register<AnnouncementBarViewport, bool>(nameof(IsCentered), true);

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<AnnouncementBarViewport, IBrush?>(nameof(MutedForeground));

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AnnouncementBarViewport, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<double> FaceMinHeightProperty =
        AvaloniaProperty.Register<AnnouncementBarViewport, double>(nameof(FaceMinHeight), 32);

    public bool IsCentered { get => GetValue(IsCenteredProperty); set => SetValue(IsCenteredProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double FaceMinHeight { get => GetValue(FaceMinHeightProperty); set => SetValue(FaceMinHeightProperty, value); }

    internal static readonly SpringSpec Rise = SpringSpec.FromVisual(0.46, 0.1);
    internal static readonly SpringSpec HeightSpring = SpringSpec.FromVisual(0.42, 0);

    private readonly List<AnnouncementBarFace> _exiting = new();
    private readonly Stack<AnnouncementBarFace> _pool = new();
    private readonly MotionValue<double> _height;
    private AnnouncementBarFace? _current;
    private double _measured;

    public AnnouncementBarViewport()
    {
        ClipToBounds = true;
        _height = new MotionValue<double>(this, 0, MotionVectors.Double);
        _height.Changed += _ => InvalidateMeasure();
    }

    public AnnouncementBar? Owner { get; set; }

    internal AnnouncementBarFace? CurrentFace => _current;

    internal int ExitingCount => _exiting.Count;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCenteredProperty || change.Property == MutedForegroundProperty ||
            change.Property == LineHeightProperty || change.Property == FaceMinHeightProperty)
        {
            _current?.ApplyStyle(this);
            foreach (var face in _exiting)
            {
                face.ApplyStyle(this);
            }
        }
    }

    /// <summary>Shows a message; <paramref name="direction"/> 1 rises from below (next), -1 drops from above (previous).</summary>
    public void Show(Announcement? announcement, int position, int total, int direction)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = _current is not null && IsLoaded && IsEffectivelyVisible && level != ArcMotionLevel.Off;
        if (_current is not null && announcement is not null && ReferenceEquals(_current.Announcement, announcement))
        {
            _current.Bind(announcement, position, total, this);
            return;
        }
        if (_current is not null)
        {
            if (animate)
            {
                var outgoing = _current;
                _exiting.Add(outgoing);
                outgoing.SetInert(true);
                RemoveWhenGone(outgoing, outgoing.Leave(level == ArcMotionLevel.Full, direction));
            }
            else
            {
                Release(_current);
            }
            _current = null;
        }
        if (announcement is null)
        {
            InvalidateMeasure();
            return;
        }
        var face = _pool.Count > 0 ? _pool.Pop() : new AnnouncementBarFace();
        face.Bind(announcement, position, total, this);
        face.ApplyStyle(this);
        face.SetInert(false);
        LogicalChildren.Add(face);
        VisualChildren.Add(face);
        _current = face;
        if (animate)
        {
            face.Enter(level == ArcMotionLevel.Full, direction);
        }
        else
        {
            face.Settle();
        }
        InvalidateMeasure();
    }

    private void RemoveWhenGone(AnnouncementBarFace face, Task task)
    {
        task.ThenOnUiThread(Dispatcher, () =>
        {
            if (_exiting.Remove(face))
            {
                Release(face);
            }
        });
    }

    private void Release(AnnouncementBarFace face)
    {
        face.StopMotion();
        LogicalChildren.Remove(face);
        VisualChildren.Remove(face);
        face.Unbind();
        if (_pool.Count < 2)
        {
            _pool.Push(face);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var constraint = availableSize.WithHeight(double.PositiveInfinity);
        foreach (var face in _exiting)
        {
            face.Measure(constraint);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Measure(constraint);
        var next = _current.DesiredSize.Height;
        if (Math.Abs(next - _measured) >= 0.5)
        {
            var first = _measured == 0;
            _measured = next;
            if (first || !IsLoaded || !ArcMotion.IsFull(this))
            {
                _height.Jump(next);
            }
            else
            {
                _ = _height.AnimateTo(next, MotionSpec.Spring(HeightSpring));
            }
        }
        var width = double.IsInfinity(availableSize.Width) ? _current.DesiredSize.Width : availableSize.Width;
        return new Size(width, Math.Max(0, _height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var face in _exiting)
        {
            face.Arrange(new Rect(0, 0, finalSize.Width, face.DesiredSize.Height));
        }
        _current?.Arrange(new Rect(0, 0, finalSize.Width, _current.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>One message face: the text, an optional countdown and an optional call to action, wrapping and centered.</summary>
internal sealed class AnnouncementBarFace : Decorator
{
    private readonly WrapPanel _panel;
    private readonly TextBlock _text;
    private readonly TranslateTransform _translate = new();
    private readonly MotionValue<double> _y;
    private readonly MotionValue<double> _fade;
    private AnnouncementBarCountdown? _countdown;
    private AnnouncementBarLink? _link;
    private BlurEffect? _blur;
    private AnnouncementBar? _owner;
    private IBrush? _muted;

    public AnnouncementBarFace()
    {
        _text = new TextBlock { TextWrapping = TextWrapping.Wrap, MinWidth = 0 };
        _panel = new WrapPanel
        {
            ItemSpacing = 12,
            LineSpacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _text }
        };
        Child = _panel;
        Padding = new Thickness(0, 6);
        RenderTransform = _translate;
        _y = new MotionValue<double>(this, 0, MotionVectors.Double);
        _y.Changed += v => _translate.Y = v * Bounds.Height;
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += ApplyFade;
    }

    public Announcement? Announcement { get; private set; }

    internal TextBlock MessageBlock => _text;

    internal AnnouncementBarCountdown? Countdown => _countdown is { IsVisible: true } ? _countdown : null;

    internal AnnouncementBarLink? Link => _link is { IsVisible: true } ? _link : null;

    public void Bind(Announcement announcement, int position, int total, AnnouncementBarViewport viewport)
    {
        Announcement = announcement;
        _owner = viewport.Owner;
        _text.Text = announcement.Message;
        if (announcement.Countdown is { } countdown)
        {
            if (_countdown is null)
            {
                _countdown = new AnnouncementBarCountdown { MutedForeground = _muted };
                _panel.Children.Add(_countdown);
            }
            _countdown.IsVisible = true;
            _countdown.Start(countdown, () => _owner?.OnCountdownEnded(announcement));
        }
        else if (_countdown is not null)
        {
            _countdown.Stop();
            _countdown.IsVisible = false;
        }
        if (announcement.Action is { } action)
        {
            if (_link is null)
            {
                _link = new AnnouncementBarLink();
                _link.Click += (_, _) =>
                {
                    if (Announcement is { } current)
                    {
                        _owner?.InvokeAction(current);
                    }
                };
                _panel.Children.Add(_link);
            }
            _link.IsVisible = true;
            _link.Content = action.Label;
            AutomationProperties.SetName(_link, action.Label);
        }
        else if (_link is not null)
        {
            _link.IsVisible = false;
        }
        AutomationProperties.SetName(this, total > 1 ? $"{position} of {total}" : announcement.Message);
    }

    public void Unbind()
    {
        _countdown?.Stop();
        Announcement = null;
    }

    public void ApplyStyle(AnnouncementBarViewport viewport)
    {
        var centered = viewport.IsCentered;
        _panel.ItemsAlignment = centered ? WrapPanelItemsAlignment.Center : WrapPanelItemsAlignment.Start;
        _panel.ItemSpacing = centered ? 12 : 10;
        _text.TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left;
        _text.LineHeight = viewport.LineHeight;
        MinHeight = viewport.FaceMinHeight;
        _muted = viewport.MutedForeground;
        if (_countdown is not null)
        {
            _countdown.MutedForeground = _muted;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            _translate.Y = _y.Value * Bounds.Height;
        }
    }

    public void SetInert(bool inert)
    {
        IsHitTestVisible = !inert;
        if (_link is not null)
        {
            _link.IsTabStop = !inert;
            _link.Focusable = !inert;
        }
        AutomationProperties.SetAccessibilityView(this, inert ? Avalonia.Automation.AccessibilityView.Raw : Avalonia.Automation.AccessibilityView.Default);
    }

    private void ApplyFade(double v)
    {
        Opacity = Math.Clamp(v, 0, 1);
        if (_blur is null)
        {
            return;
        }
        var radius = 4 * (1 - Math.Clamp(v, 0, 1));
        if (radius < 0.05)
        {
            Effect = null;
        }
        else
        {
            _blur.Radius = radius;
            Effect = _blur;
        }
    }

    public void Settle()
    {
        _y.Jump(0);
        _blur = null;
        Effect = null;
        _fade.Jump(1);
    }

    public void Enter(bool full, int direction)
    {
        if (full)
        {
            _blur ??= new BlurEffect();
            _y.Jump(0.7 * direction);
            _fade.Jump(0);
            _ = _y.AnimateTo(0, MotionSpec.Spring(AnnouncementBarViewport.Rise));
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        else
        {
            _blur = null;
            Effect = null;
            _y.Jump(0);
            _fade.Jump(0);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
    }

    public Task Leave(bool full, int direction)
    {
        if (full)
        {
            _blur ??= new BlurEffect();
            _ = _y.AnimateTo(-0.6 * direction, MotionSpec.Spring(AnnouncementBarViewport.Rise));
            return _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        _blur = null;
        Effect = null;
        return _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
    }

    public void StopMotion()
    {
        _y.Stop();
        _fade.Stop();
    }
}
