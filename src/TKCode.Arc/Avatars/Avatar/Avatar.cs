using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// The presence dot of an <see cref="Avatar"/>, built by the avatar when it first has a status. Its look (size, ring,
/// halo, the fill per status and the fill's transition) is its own ControlTheme in AvatarTheme.axaml, so a code-built
/// part is styled like the template part it replaces; the avatar mirrors its status into <c>:online</c>, <c>:busy</c>,
/// <c>:away</c> or <c>:connecting</c>. Every status but Online and Offline also differs in shape, not only in color:
/// Busy carries a bar, Connecting is a hollow ring that breathes.
/// </summary>
[PseudoClasses(OnlinePseudoClass, BusyPseudoClass, AwayPseudoClass, ConnectingPseudoClass)]
internal sealed class AvatarStatusDot : Border
{
    public const string OnlinePseudoClass = ":online";
    public const string BusyPseudoClass = ":busy";
    public const string AwayPseudoClass = ":away";
    public const string ConnectingPseudoClass = ":connecting";

    private readonly AvatarStatusBar _bar = new() { Name = "PART_StatusBar", IsVisible = false };
    private LiveLoop? _breath;
    private double _phase;

    public AvatarStatusDot()
    {
        // Busy's bar (a ControlTheme cannot select a child, so the dot shows it).
        Child = _bar;
    }

    protected override Type StyleKeyOverride => typeof(AvatarStatusDot);

    internal AvatarStatus Status { get; private set; }

    internal void SetStatus(AvatarStatus status)
    {
        Status = status;
        PseudoClasses.Set(OnlinePseudoClass, status == AvatarStatus.Online);
        PseudoClasses.Set(BusyPseudoClass, status == AvatarStatus.Busy);
        PseudoClasses.Set(AwayPseudoClass, status == AvatarStatus.Away);
        PseudoClasses.Set(ConnectingPseudoClass, status == AvatarStatus.Connecting);
        _bar.IsVisible = status == AvatarStatus.Busy;
        UpdateBreath();
    }

    /// <summary>
    /// Connecting breathes in opacity (<see cref="ArcBusyPulse"/>). ControlTheme cannot express a repeating animation that
    /// pauses off screen and parks with motion off, so a <see cref="LiveLoop"/> drives it; an opacity breath is kept under
    /// reduced motion, and with motion off the dot rests at full opacity.
    /// </summary>
    private void UpdateBreath()
    {
        if (Status != AvatarStatus.Connecting)
        {
            _breath?.Stop();
            return;
        }
        if (_breath is null)
        {
            _breath = new LiveLoop(this, seconds =>
            {
                _phase = ArcBusyPulse.Advance(_phase, seconds);
                Opacity = ArcBusyPulse.Opacity(_phase);
                return true;
            }) { MinimumLevel = ArcMotionLevel.Reduced };
            _breath.RunningChanged += running =>
            {
                if (!running)
                {
                    _phase = 0;
                    Opacity = 1;
                }
            };
        }
        _breath.Start();
    }
}

public enum AvatarSize
{
    Small,
    Medium,
    Large,
    XLarge
}

/// <summary>The bar across a Busy <see cref="AvatarStatusDot"/>; its look is its own ControlTheme in AvatarTheme.axaml.</summary>
internal sealed class AvatarStatusBar : Border
{
    protected override Type StyleKeyOverride => typeof(AvatarStatusBar);
}

/// <summary>The presence an <see cref="Avatar"/> shows in its status dot.</summary>
public enum AvatarStatus
{
    /// <summary>No dot.</summary>
    None,

    /// <summary>Available: a success-colored dot.</summary>
    Online,

    /// <summary>Not available: a muted dot.</summary>
    Offline,

    /// <summary>Do not disturb: a danger-colored dot with a bar across it.</summary>
    Busy,

    /// <summary>Idle or stepped away: a warning-colored dot.</summary>
    Away,

    /// <summary>Reconnecting: a hollow ring that breathes (it rests when motion is off).</summary>
    Connecting
}

/// <summary>
/// A person as a photo or initials. A photo that is still downloading fades in from a soft blur (cached photos
/// show at once); a failed photo falls back to initials; the status dot pops in on a snappy spring.
/// </summary>
[TemplatePart(ImagePart, typeof(ArcImage))]
[PseudoClasses(ImageLoadingPseudoClass, ShowImagePseudoClass, FallbackPseudoClass, SmallPseudoClass, LargePseudoClass, XLargePseudoClass,
    OnlinePseudoClass, OfflinePseudoClass, BusyPseudoClass, AwayPseudoClass, ConnectingPseudoClass)]
public class Avatar : TemplatedControl
{
    public const string ImagePart = "PART_Image";
    public const string ImageLoadingPseudoClass = ":image-loading";
    public const string ShowImagePseudoClass = ":show-image";
    public const string FallbackPseudoClass = ":fallback";

    /// <summary><see cref="Size"/> Small, Large or XLarge as a pseudo-class (Medium has none; the theme selects on these).</summary>
    public const string SmallPseudoClass = ":small";

    /// <inheritdoc cref="SmallPseudoClass"/>
    public const string LargePseudoClass = ":large";

    /// <inheritdoc cref="SmallPseudoClass"/>
    public const string XLargePseudoClass = ":xlarge";

    /// <summary><see cref="Status"/> as a pseudo-class (None has none).</summary>
    public const string OnlinePseudoClass = ":online";

    /// <inheritdoc cref="OnlinePseudoClass"/>
    public const string OfflinePseudoClass = ":offline";

    /// <inheritdoc cref="OnlinePseudoClass"/>
    public const string BusyPseudoClass = ":busy";

    /// <inheritdoc cref="OnlinePseudoClass"/>
    public const string AwayPseudoClass = ":away";

    /// <inheritdoc cref="OnlinePseudoClass"/>
    public const string ConnectingPseudoClass = ":connecting";

    /// <summary>The person's name; initials are the first letters of its first two words.</summary>
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(DisplayName));

    /// <summary>Photo to show. A photo that fails to load falls back to initials.</summary>
    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<Avatar, ImageSource?>(nameof(Source));

    /// <summary>Avatar size: Small, Medium (default), Large or XLarge.</summary>
    public static readonly StyledProperty<AvatarSize> SizeProperty =
        AvaloniaProperty.Register<Avatar, AvatarSize>(nameof(Size), AvatarSize.Medium);

    /// <summary>Presence dot: None (default, no dot), Online, Offline, Busy, Away or Connecting. The status is part of the accessible name.</summary>
    public static readonly StyledProperty<AvatarStatus> StatusProperty =
        AvaloniaProperty.Register<Avatar, AvatarStatus>(nameof(Status));

    /// <summary>Initials computed from DisplayName: the first letters of its first two words.</summary>
    public static readonly DirectProperty<Avatar, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<Avatar, string>(nameof(Initials), o => o.Initials);

    internal static readonly DirectProperty<Avatar, bool> HasStatusProperty =
        AvaloniaProperty.RegisterDirect<Avatar, bool>(nameof(HasStatus), o => o.HasStatus);

    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    /// <inheritdoc cref="SourceProperty"/>
    public ImageSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    /// <inheritdoc cref="SizeProperty"/>
    public AvatarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <inheritdoc cref="StatusProperty"/>
    public AvatarStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    private string _initials = string.Empty;
    /// <inheritdoc cref="InitialsProperty"/>
    public string Initials
    {
        get => _initials;
        private set => SetAndRaise(InitialsProperty, ref _initials, value);
    }

    private bool _hasStatus;
    internal bool HasStatus
    {
        get => _hasStatus;
        private set => SetAndRaise(HasStatusProperty, ref _hasStatus, value);
    }

    private ArcImage? _image;
    private ImageSource? _failedSource;

    public Avatar()
    {
        UpdateSizePseudoClasses();
        UpdateStatusPseudoClasses();
    }

    // Pseudo-classes, not property selectors: a `^[Size=Small]` selector costs every avatar an activator and a
    // subscription per selector, and avatars repeat in every member list.
    private void UpdateSizePseudoClasses()
    {
        var size = Size;
        PseudoClasses.Set(SmallPseudoClass, size == AvatarSize.Small);
        PseudoClasses.Set(LargePseudoClass, size == AvatarSize.Large);
        PseudoClasses.Set(XLargePseudoClass, size == AvatarSize.XLarge);
    }

    private void UpdateStatusPseudoClasses()
    {
        var status = Status;
        PseudoClasses.Set(OnlinePseudoClass, status == AvatarStatus.Online);
        PseudoClasses.Set(OfflinePseudoClass, status == AvatarStatus.Offline);
        PseudoClasses.Set(BusyPseudoClass, status == AvatarStatus.Busy);
        PseudoClasses.Set(AwayPseudoClass, status == AvatarStatus.Away);
        PseudoClasses.Set(ConnectingPseudoClass, status == AvatarStatus.Connecting);
    }

    /// <summary>First letters of the first two words, upper-cased ("Ada Lovelace" → "AL").</summary>
    public static string ComputeInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        Span<char> buffer = stackalloc char[2];
        var count = 0;
        foreach (var part in name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            buffer[count++] = char.ToUpperInvariant(part[0]);
            if (count == 2)
            {
                break;
            }
        }
        return new string(buffer[..count]);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_image is not null)
        {
            _image.PropertyChanged -= OnImagePropertyChanged;
        }
        _image = e.NameScope.Find<ArcImage>(ImagePart);
        if (_image is not null)
        {
            _image.PropertyChanged += OnImagePropertyChanged;
        }
        UpdateImageState(initial: true);
        // The old template's status dot went with it.
        _root = VisualChildren.Count > 0 ? VisualChildren[0] as Panel : null;
        _statusHost = null;
        _statusDot = null;
        _statusMargin?.Dispose();
        _statusMargin = null;
        UpdateStatus(animate: false);
    }

    private Panel? _root;
    private PresenceHost? _statusHost;
    private AvatarStatusDot? _statusDot;
    private IDisposable? _statusMargin;

    /// <summary>
    /// The presence dot (PART_StatusHost with PART_Status) is built the first time the avatar has a status: most avatars
    /// in a list never show one, and the template used to carry the PresenceHost, the Border and six token bindings for
    /// every avatar. Once built it stays, and its PresenceHost pops it in and out as before.
    /// </summary>
    private void UpdateStatus(bool animate)
    {
        var present = HasStatus;
        if (_statusHost is null)
        {
            if (!present || _root is null)
            {
                return;
            }
            var dot = new AvatarStatusDot { Name = "PART_Status" };
            dot.SetStatus(Status);
            var host = new PresenceHost
            {
                Name = "PART_StatusHost",
                Mode = PresenceMode.Pop,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Child = dot
            };
            _statusHost = host;
            _statusDot = dot;
            UpdateStatusMargin();
            if (animate && IsLoaded)
            {
                // A status that arrives on a shown avatar pops in, as the template's host did: the host animates once it
                // is loaded, so it starts hidden and is told to show on its Loaded.
                host.IsPresent = false;
                host.Loaded += OnStatusHostLoaded;
            }
            else
            {
                host.IsPresent = true;
            }
            _root.Children.Add(host);
            return;
        }
        if (present)
        {
            // A dot that hides keeps its last look while it pops out.
            _statusDot?.SetStatus(Status);
        }
        _statusHost.IsPresent = present;
    }

    private void OnStatusHostLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is PresenceHost host)
        {
            host.Loaded -= OnStatusHostLoaded;
            host.IsPresent = HasStatus;
        }
    }

    /// <summary>XLarge avatars inset the dot (the theme's <c>StatusMarginXL</c>); the other sizes leave it in the corner.</summary>
    private void UpdateStatusMargin()
    {
        _statusMargin?.Dispose();
        _statusMargin = null;
        if (_statusHost is null)
        {
            return;
        }
        if (Size == AvatarSize.XLarge)
        {
            _statusMargin = _statusHost.Bind(MarginProperty, _statusHost.GetResourceObservable(AvatarTokens.Key(AvatarTokenKind.StatusMarginXL)), BindingPriority.Style);
        }
        else
        {
            _statusHost.ClearValue(MarginProperty);
        }
    }

    private void OnImagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ArcImage.IsLoadingProperty || e.Property == ArcImage.IsImageLoadedProperty ||
            e.Property == ArcImage.IsFailedProperty)
        {
            UpdateImageState(initial: false);
        }
    }

    private void UpdateImageState(bool initial)
    {
        var source = Source;
        if (source is not null && _image is { IsFailed: true })
        {
            _failedSource = source;
        }
        var showImage = source is not null && !ReferenceEquals(_failedSource, source);
        PseudoClasses.Set(ShowImagePseudoClass, showImage);
        PseudoClasses.Set(FallbackPseudoClass, source is not null && !showImage);
        // Only a photo that is still downloading after mount fades in; a decoded one shows at once.
        var loading = showImage && _image is { IsImageLoaded: false } && (_image.IsLoading || initial || PseudoClasses.Contains(ImageLoadingPseudoClass));
        PseudoClasses.Set(ImageLoadingPseudoClass, loading);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayNameProperty || change.Property == StatusProperty)
        {
            Initials = ComputeInitials(DisplayName);
            HasStatus = Status != AvatarStatus.None;
            UpdateStatusPseudoClasses();
            if (change.Property == StatusProperty)
            {
                UpdateStatus(animate: true);
            }
            var name = DisplayName ?? string.Empty;
            AutomationProperties.SetName(this, Status switch
            {
                AvatarStatus.Online => $"{name}, online",
                AvatarStatus.Offline => $"{name}, offline",
                AvatarStatus.Busy => $"{name}, busy",
                AvatarStatus.Away => $"{name}, away",
                AvatarStatus.Connecting => $"{name}, connecting",
                _ => name
            });
        }
        else if (change.Property == SizeProperty)
        {
            UpdateSizePseudoClasses();
            UpdateStatusMargin();
        }
        else if (change.Property == SourceProperty)
        {
            _failedSource = null;
            PseudoClasses.Set(ImageLoadingPseudoClass, Source is not null);
            UpdateImageState(initial: false);
        }
    }
}
