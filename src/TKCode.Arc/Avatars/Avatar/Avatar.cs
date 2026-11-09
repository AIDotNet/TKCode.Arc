using TKCode.Arc.DesignTokens;
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
/// halo, the muted or success fill and the fill's transition) is its own ControlTheme in AvatarTheme.axaml, so a code-built
/// part is styled like the template part it replaces; the avatar mirrors its status into <c>:online</c>.
/// </summary>
[PseudoClasses(OnlinePseudoClass)]
internal sealed class AvatarStatusDot : Border
{
    public const string OnlinePseudoClass = ":online";

    protected override Type StyleKeyOverride => typeof(AvatarStatusDot);

    internal void SetOnline(bool online) => PseudoClasses.Set(OnlinePseudoClass, online);
}

public enum AvatarSize
{
    Small,
    Medium,
    Large,
    XLarge
}

public enum AvatarStatus
{
    None,
    Online,
    Offline
}

/// <summary>
/// A person as a photo or initials. A photo that is still downloading fades in from a soft blur (cached photos
/// show at once); a failed photo falls back to initials; the status dot pops in on a snappy spring.
/// </summary>
[TemplatePart(ImagePart, typeof(ArcImage))]
[PseudoClasses(ImageLoadingPseudoClass, ShowImagePseudoClass, FallbackPseudoClass, SmallPseudoClass, LargePseudoClass, XLargePseudoClass,
    OnlinePseudoClass, OfflinePseudoClass)]
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

    /// <summary><see cref="Status"/> Online or Offline as a pseudo-class (None has none).</summary>
    public const string OnlinePseudoClass = ":online";

    /// <inheritdoc cref="OnlinePseudoClass"/>
    public const string OfflinePseudoClass = ":offline";

    /// <summary>The person's name; initials are the first letters of its first two words.</summary>
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(DisplayName));

    /// <summary>Photo to show. A photo that fails to load falls back to initials.</summary>
    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<Avatar, ImageSource?>(nameof(Source));

    /// <summary>Avatar size: Small, Medium (default), Large or XLarge.</summary>
    public static readonly StyledProperty<AvatarSize> SizeProperty =
        AvaloniaProperty.Register<Avatar, AvatarSize>(nameof(Size), AvatarSize.Medium);

    /// <summary>Presence dot: None (default, no dot), Online or Offline.</summary>
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
            dot.SetOnline(Status == AvatarStatus.Online);
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
        _statusDot?.SetOnline(Status == AvatarStatus.Online);
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
