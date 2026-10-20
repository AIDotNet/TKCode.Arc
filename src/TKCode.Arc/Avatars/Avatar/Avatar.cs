using AtomUI.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

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
[TemplatePart(ImagePart, typeof(AsyncImage))]
[PseudoClasses(ImageLoadingPseudoClass, ShowImagePseudoClass, FallbackPseudoClass)]
public class Avatar : TemplatedControl
{
    public const string ImagePart = "PART_Image";
    public const string ImageLoadingPseudoClass = ":image-loading";
    public const string ShowImagePseudoClass = ":show-image";
    public const string FallbackPseudoClass = ":fallback";

    /// <summary>The person's name; initials are the first letters of its first two words.</summary>
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(DisplayName));

    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<Avatar, ImageSource?>(nameof(Source));

    public static readonly StyledProperty<AvatarSize> SizeProperty =
        AvaloniaProperty.Register<Avatar, AvatarSize>(nameof(Size), AvatarSize.Medium);

    public static readonly StyledProperty<AvatarStatus> StatusProperty =
        AvaloniaProperty.Register<Avatar, AvatarStatus>(nameof(Status));

    public static readonly DirectProperty<Avatar, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<Avatar, string>(nameof(Initials), o => o.Initials);

    internal static readonly DirectProperty<Avatar, bool> HasStatusProperty =
        AvaloniaProperty.RegisterDirect<Avatar, bool>(nameof(HasStatus), o => o.HasStatus);

    public string? DisplayName { get => GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public ImageSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public AvatarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public AvatarStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    private string _initials = string.Empty;
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

    private AsyncImage? _image;
    private ImageSource? _failedSource;

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
        _image = e.NameScope.Find<AsyncImage>(ImagePart);
        if (_image is not null)
        {
            _image.PropertyChanged += OnImagePropertyChanged;
        }
        UpdateImageState(initial: true);
    }

    private void OnImagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AsyncImage.IsLoadingProperty || e.Property == AsyncImage.IsLoadedProperty ||
            e.Property == AsyncImage.IsFailedProperty)
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
        var loading = showImage && _image is { IsLoaded: false } && (_image.IsLoading || initial || PseudoClasses.Contains(ImageLoadingPseudoClass));
        PseudoClasses.Set(ImageLoadingPseudoClass, loading);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayNameProperty || change.Property == StatusProperty)
        {
            Initials = ComputeInitials(DisplayName);
            HasStatus = Status != AvatarStatus.None;
            var name = DisplayName ?? string.Empty;
            AutomationProperties.SetName(this, Status == AvatarStatus.None ? name : $"{name}, {Status.ToString().ToLowerInvariant()}");
        }
        else if (change.Property == SourceProperty)
        {
            _failedSource = null;
            PseudoClasses.Set(ImageLoadingPseudoClass, Source is not null);
            UpdateImageState(initial: false);
        }
    }
}
