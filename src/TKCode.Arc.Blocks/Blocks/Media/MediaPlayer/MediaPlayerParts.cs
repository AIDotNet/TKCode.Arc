using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A round icon button of the player (previous, play, next, mute): transparent with a muted plate on hover, or solid for
/// the play button (class <c>play</c>); pressing scales it to 0.94.
/// </summary>
internal sealed class MediaPlayerButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(MediaPlayerButton);
}

/// <summary>
/// One playlist row: the whole row is a button that plays its track, or pauses and resumes it when it is the current one.
/// It shows the title and, at the right, the length, or the playing bars while its track plays. Rows after the first
/// carry a 1px line on top (pseudo-class <c>:separated</c>).
/// </summary>
internal sealed class MediaPlayerTrackRow : AvaloniaButton
{
    /// <summary>The track the row stands for.</summary>
    public static readonly StyledProperty<MediaTrack?> TrackProperty =
        AvaloniaProperty.Register<MediaPlayerTrackRow, MediaTrack?>(nameof(Track));

    /// <summary>Whether it is the player's current track.</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<MediaPlayerTrackRow, bool>(nameof(IsCurrent));

    /// <summary>Whether the current track is playing (only matters on the current row).</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<MediaPlayerTrackRow, bool>(nameof(IsPlaying));

    /// <summary>Whether a line separates the row from the one above.</summary>
    public static readonly StyledProperty<bool> IsSeparatedProperty =
        AvaloniaProperty.Register<MediaPlayerTrackRow, bool>(nameof(IsSeparated));

    /// <summary>The track's title as shown.</summary>
    public static readonly StyledProperty<string?> TitleTextProperty =
        AvaloniaProperty.Register<MediaPlayerTrackRow, string?>(nameof(TitleText));

    /// <summary>The length as shown at the right.</summary>
    public static readonly StyledProperty<string?> LengthTextProperty =
        AvaloniaProperty.Register<MediaPlayerTrackRow, string?>(nameof(LengthText));

    /// <summary>The playing indicator's accessible name.</summary>
    public static readonly StyledProperty<string?> PlayingTextProperty =
        AvaloniaProperty.Register<MediaPlayerTrackRow, string?>(nameof(PlayingText));

    /// <inheritdoc cref="TrackProperty"/>
    public MediaTrack? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    /// <inheritdoc cref="IsCurrentProperty"/>
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <inheritdoc cref="IsSeparatedProperty"/>
    public bool IsSeparated { get => GetValue(IsSeparatedProperty); set => SetValue(IsSeparatedProperty, value); }

    /// <inheritdoc cref="TitleTextProperty"/>
    public string? TitleText { get => GetValue(TitleTextProperty); set => SetValue(TitleTextProperty, value); }

    /// <inheritdoc cref="LengthTextProperty"/>
    public string? LengthText { get => GetValue(LengthTextProperty); set => SetValue(LengthTextProperty, value); }

    /// <inheritdoc cref="PlayingTextProperty"/>
    public string? PlayingText { get => GetValue(PlayingTextProperty); set => SetValue(PlayingTextProperty, value); }

    protected override Type StyleKeyOverride => typeof(MediaPlayerTrackRow);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCurrentProperty || change.Property == IsPlayingProperty)
        {
            PseudoClasses.Set(":current", IsCurrent);
            PseudoClasses.Set(":playing", IsCurrent && IsPlaying);
        }
        else if (change.Property == IsSeparatedProperty)
        {
            PseudoClasses.Set(":separated", IsSeparated);
        }
    }
}

/// <summary>
/// Stacks the playlist rows and carries the selection rail (a 2×20 <see cref="GlidingIndicator"/>, 2 in from the left,
/// centered on the current row). The rail springs from row to row (Smooth) when the current track changes and follows
/// the rows without animating when they only move (a resize, a row added above).
/// </summary>
internal sealed class MediaPlayerQueue : Panel
{
    /// <summary>The rail's color.</summary>
    public static readonly StyledProperty<IBrush?> RailBrushProperty =
        AvaloniaProperty.Register<MediaPlayerQueue, IBrush?>(nameof(RailBrush));

    private readonly GlidingIndicator _rail = new() { IsHitTestVisible = false, Spring = ArcSpring.Smooth };
    private int _current = -1;
    private bool _animateNext;

    public MediaPlayerQueue()
    {
        _rail.BindCornerRadius(ArcDesignTokenKind.RadiusStep2);
        Children.Add(_rail);
    }

    /// <inheritdoc cref="RailBrushProperty"/>
    public IBrush? RailBrush { get => GetValue(RailBrushProperty); set => SetValue(RailBrushProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RailBrushProperty)
        {
            _rail.Fill = RailBrush;
        }
    }

    /// <summary>The rail.</summary>
    public GlidingIndicator Rail => _rail;

    /// <summary>The rail's width (2), height (20) and distance from the row's left edge (2).</summary>
    public double RailWidth { get; set; } = 2;

    public double RailHeight { get; set; } = 20;

    public double RailInset { get; set; } = 2;

    /// <summary>The rows, in order (every child but the rail).</summary>
    public IEnumerable<MediaPlayerTrackRow> Rows => Children.OfType<MediaPlayerTrackRow>();

    /// <summary>Marks a row as current; the rail springs there after the next layout when <paramref name="animate"/>.</summary>
    public void SetCurrent(int index, bool animate)
    {
        _current = index;
        _animateNext |= animate;
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var row in Rows)
        {
            row.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, row.DesiredSize.Width);
            height += row.DesiredSize.Height;
        }
        _rail.Measure(availableSize);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        var index = 0;
        Rect? target = null;
        foreach (var row in Rows)
        {
            var height = row.DesiredSize.Height;
            var rect = new Rect(0, y, finalSize.Width, height);
            row.Arrange(rect);
            if (index == _current)
            {
                // The line on top of a separated row belongs to the row, so center the rail on what is under it.
                var top = row.IsSeparated ? row.BorderThickness.Top : 0;
                var body = height - top;
                target = new Rect(RailInset, y + top + (body - RailHeight) / 2, RailWidth, RailHeight);
            }
            y += height;
            index++;
        }
        _rail.Arrange(new Rect(finalSize));
        if (target is { } railRect)
        {
            var animate = _animateNext && _rail.HasTarget;
            _rail.MoveTo(railRect, animate);
        }
        else
        {
            _rail.Hide(animate: false);
        }
        _animateNext = false;
        return finalSize;
    }
}

/// <summary>A frame whose height follows its width at <see cref="Aspect"/> (width ÷ height), clipping its child to its rounded corners.</summary>
internal sealed class MediaPlayerCoverFrame : Border
{
    /// <summary>Width divided by height (2.2).</summary>
    public static readonly StyledProperty<double> AspectProperty =
        AvaloniaProperty.Register<MediaPlayerCoverFrame, double>(nameof(Aspect), 2.2);

    static MediaPlayerCoverFrame()
    {
        AffectsMeasure<MediaPlayerCoverFrame>(AspectProperty);
    }

    /// <inheritdoc cref="AspectProperty"/>
    public double Aspect { get => GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width;
        var height = width / Math.Max(0.1, Aspect);
        Child?.Measure(new Size(width, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>The current track's heading: the title (display face) and "artist · album" under it.</summary>
internal sealed class MediaPlayerHeading : TemplatedControl
{
    /// <summary>The track shown.</summary>
    public static readonly StyledProperty<MediaTrack?> TrackProperty =
        AvaloniaProperty.Register<MediaPlayerHeading, MediaTrack?>(nameof(Track));

    /// <summary>Whether the player uses its compact layout (a smaller title).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<MediaPlayerHeading, bool>(nameof(IsCompact));

    /// <summary>The title shown.</summary>
    public static readonly StyledProperty<string?> TitleTextProperty =
        AvaloniaProperty.Register<MediaPlayerHeading, string?>(nameof(TitleText));

    /// <summary>The line under the title.</summary>
    public static readonly StyledProperty<string?> BylineTextProperty =
        AvaloniaProperty.Register<MediaPlayerHeading, string?>(nameof(BylineText));

    /// <inheritdoc cref="TitleTextProperty"/>
    public string? TitleText { get => GetValue(TitleTextProperty); set => SetValue(TitleTextProperty, value); }

    /// <inheritdoc cref="BylineTextProperty"/>
    public string? BylineText { get => GetValue(BylineTextProperty); set => SetValue(BylineTextProperty, value); }

    /// <inheritdoc cref="TrackProperty"/>
    public MediaTrack? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override Type StyleKeyOverride => typeof(MediaPlayerHeading);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(":compact", IsCompact);
        }
        else if (change.Property == TrackProperty)
        {
            var track = change.GetNewValue<MediaTrack?>();
            TitleText = track?.Title;
            BylineText = track?.Byline;
        }
    }
}

/// <summary>A cover picture: the track's image filling the frame (cropped), or a muted plate when there is none.</summary>
internal sealed class MediaPlayerCover : Border
{
    public MediaPlayerCover(MediaTrack? track)
    {
        Track = track;
        if (track?.Cover is { } image)
        {
            Child = new Image { Source = image, Stretch = Stretch.UniformToFill };
            AutomationProperties.SetName(this, track.CoverDescription ?? string.Empty);
        }
        else
        {
            ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        }
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>The track whose cover this is.</summary>
    public MediaTrack? Track { get; }
}
