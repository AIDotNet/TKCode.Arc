using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Parsing;
using StreamMark.Core.Streaming;

namespace TKCode.Arc;

/// <summary>
/// An Arc-styled Markdown document rendered by StreamMark: headings, lists, tables, quotes, links, inline code and
/// fenced code highlighted in Arc's syntax palette. Show static text with <see cref="Text"/>, or stream a reply with
/// <see cref="Append"/> / <see cref="Complete"/> (or your own <see cref="Stream"/>). The document grows with its
/// content (no internal scrolling); put long documents in a <see cref="ScrollViewer"/>.
/// </summary>
/// <remarks>
/// Colors, fonts and radii follow the ArcDesign tokens (light/dark and accent changes included) through StreamMark's
/// <c>Md.*</c> resources, set on this control. Add <c>Md.*</c> keys to <see cref="StyledElement.Resources"/> to
/// override single values.
/// </remarks>
[TemplatePart(ViewPart, typeof(MarkdownView))]
[PseudoClasses(StreamingPseudoClass)]
public class Markdown : TemplatedControl
{
    public const string ViewPart = "PART_View";
    public const string StreamingPseudoClass = ":streaming";

    #region Properties

    /// <summary>The Markdown source to show. Setting it ends a stream started with <see cref="Append"/>.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Markdown, string?>(nameof(Text));

    /// <summary>
    /// A streamed reply to show instead of <see cref="Text"/>. <see cref="Append"/> creates one when none is set; you
    /// can also supply your own (for example one fed from a worker thread with <see cref="MarkdownStream.Push"/>).
    /// </summary>
    public static readonly StyledProperty<MarkdownStream?> StreamProperty =
        AvaloniaProperty.Register<Markdown, MarkdownStream?>(nameof(Stream));

    /// <summary>
    /// StreamMark services (plugins, code block options, link policy). Null uses services set on an ancestor with
    /// <c>MarkdownServices.SetServices</c>, else <see cref="DefaultServices"/>.
    /// </summary>
    public static readonly StyledProperty<MarkdownServices?> ServicesProperty =
        AvaloniaProperty.Register<Markdown, MarkdownServices?>(nameof(Services));

    /// <summary>Body line height in pixels; NaN (default) uses the font size times Arc's prose leading (1.65).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<Markdown, double>(nameof(LineHeight), double.NaN);

    /// <summary>Vertical space between blocks (paragraphs, lists, code, tables).</summary>
    public static readonly StyledProperty<double> BlockSpacingProperty =
        AvaloniaProperty.Register<Markdown, double>(nameof(BlockSpacing), 12);

    /// <summary>
    /// Opens allowed absolute links (http, https, mailto) with the system launcher when no <see cref="LinkClicked"/>
    /// handler marks the event handled and the link policy does not ask for confirmation.
    /// </summary>
    public static readonly StyledProperty<bool> OpensLinksProperty =
        AvaloniaProperty.Register<Markdown, bool>(nameof(OpensLinks), true);

    /// <summary>Whether a stream is attached and has not completed yet.</summary>
    public static readonly DirectProperty<Markdown, bool> IsStreamingProperty =
        AvaloniaProperty.RegisterDirect<Markdown, bool>(nameof(IsStreaming), o => o.IsStreaming);

    /// <summary>A link was clicked (anchors inside the document scroll by themselves and do not arrive here).</summary>
    public static readonly RoutedEvent<LinkClickedEventArgs> LinkClickedEvent = MarkdownView.LinkClickedEvent;

    /// <summary>Prose leading, set by the theme from <see cref="MarkdownToken"/>.</summary>
    internal static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<Markdown, double>(nameof(Leading), 1.65);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public MarkdownStream? Stream { get => GetValue(StreamProperty); set => SetValue(StreamProperty, value); }
    public MarkdownServices? Services { get => GetValue(ServicesProperty); set => SetValue(ServicesProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double BlockSpacing { get => GetValue(BlockSpacingProperty); set => SetValue(BlockSpacingProperty, value); }
    public bool OpensLinks { get => GetValue(OpensLinksProperty); set => SetValue(OpensLinksProperty, value); }
    internal double Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    private bool _isStreaming;

    /// <summary>Whether a stream is attached and has not completed yet.</summary>
    public bool IsStreaming { get => _isStreaming; private set => SetAndRaise(IsStreamingProperty, ref _isStreaming, value); }

    /// <summary>A link was clicked. Mark it handled to stop <see cref="OpensLinks"/> from opening it.</summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    #endregion

    /// <summary>
    /// The services used when neither <see cref="Services"/> nor an ancestor sets any: Arc-palette TextMate
    /// highlighting (<see cref="MarkdownHighlightPlugin"/>) and Arc's code block wording (copy only, no download,
    /// long blocks fold to 12 lines with "Show all N lines").
    /// </summary>
    public static MarkdownServices DefaultServices { get; } = new()
    {
        Plugins = [new MarkdownHighlightPlugin()],
        CodeBlocks = new CodeBlockOptions
        {
            ShowDownload = false,
            CollapsedLines = 12,
            CollapseText = static () => "Show fewer lines"
        }
    };

    private readonly ResourceDictionary _resources = CreateResources();
    private MarkdownView? _view;
    private StreamingDocument? _document;
    private MarkdownProfile? _documentProfile;
    private StreamingDocument? _watched;
    private int _applyPending;
    private MarkdownStream? _subscribed;
    private MarkdownStream? _ownStream;

    public Markdown()
    {
        Resources.MergedDictionaries.Add(_resources);
        UpdateTypography();
        AddHandler(LinkClickedEvent, OnLinkClicked);
    }

    internal MarkdownView? View => _view;

    /// <summary>
    /// Appends streamed text (from the UI thread). Starts a new reply when no stream is running, replacing what is
    /// shown (<see cref="Text"/> becomes null); the text is paced and fades in at the end of the document.
    /// </summary>
    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (Stream is null || !IsStreaming)
        {
            // The reply replaces the static text (Text reads null while a reply is shown).
            SetCurrentValue(TextProperty, null);
            _ownStream = new MarkdownStream(fadeDuration: FadeDuration(), profile: EffectiveServices().Profile);
            SetCurrentValue(StreamProperty, _ownStream);
        }
        Stream!.Push(text);
    }

    /// <summary>Ends the current stream; <see cref="IsStreaming"/> turns false once the paced backlog is shown.</summary>
    public void Complete() => Stream?.Complete();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _view = e.NameScope.Find<MarkdownView>(ViewPart);
        UpdateServices();
        UpdateSource();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            if (Stream is not null)
            {
                // Text wins over a stream (OnPropertyChanged runs again for Stream and updates the view).
                SetCurrentValue(StreamProperty, null);
            }
            else
            {
                UpdateSource();
            }
        }
        else if (change.Property == StreamProperty)
        {
            Subscribe(change.GetNewValue<MarkdownStream?>());
            UpdateSource();
        }
        else if (change.Property == ServicesProperty || change.Property == MarkdownServices.ServicesProperty)
        {
            UpdateServices();
        }
        else if (change.Property == FontSizeProperty || change.Property == FontFamilyProperty || change.Property == LineHeightProperty
                 || change.Property == LeadingProperty || change.Property == BlockSpacingProperty)
        {
            UpdateTypography();
        }
    }

    private MarkdownServices EffectiveServices()
    {
        if (Services is { } services)
        {
            return services;
        }
        var inherited = GetValue(MarkdownServices.ServicesProperty);
        return ReferenceEquals(inherited, MarkdownServices.Default) ? DefaultServices : inherited;
    }

    private void UpdateServices()
    {
        if (_view is null)
        {
            return;
        }
        var services = EffectiveServices();
        MarkdownServices.SetServices(_view, services);
        if (_document is not null && !ReferenceEquals(_documentProfile, services.Profile))
        {
            // Plugins can change the Markdown dialect: parse the static text again with the new profile.
            _document = null;
            UpdateSource();
        }
    }

    /// <summary>
    /// Static text goes through a document owned by this control (parsed on StreamMark's worker with the services'
    /// profile); a stream brings its own document. Either way the view shows it.
    /// </summary>
    private void UpdateSource()
    {
        if (_view is null)
        {
            return;
        }
        StreamingDocument document;
        if (Stream is { } stream)
        {
            document = stream.Document;
            _view.Document = null;
            _view.Stream = stream;
        }
        else
        {
            if (_document is null)
            {
                var services = EffectiveServices();
                _documentProfile = services.Profile;
                _document = new StreamingDocument(_documentProfile);
            }
            document = _document;
            document.Load(Text ?? string.Empty);
            _view.Stream = null;
            _view.Document = document;
        }
        Watch(document);
    }

    private void Watch(StreamingDocument? document)
    {
        if (ReferenceEquals(document, _watched))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.SnapshotReady -= OnSnapshotReady;
        }
        _watched = document;
        if (document is not null && IsAttached)
        {
            document.SnapshotReady += OnSnapshotReady;
        }
        OnSnapshotReady(null);
    }

    /// <summary>
    /// Safety net (worker thread). MarkdownView applies snapshots on its next animation frame and keeps one pending
    /// frame request; should that frame callback be lost, the document would stay empty. After each snapshot a
    /// background-priority check applies it if the view has not already (normally it has, and this does nothing).
    /// </summary>
    private void OnSnapshotReady(DocumentSnapshot? snapshot)
    {
        if (Interlocked.Exchange(ref _applyPending, 1) == 0)
        {
            Dispatcher.UIThread.Post(EnsureApplied, DispatcherPriority.Background);
        }
    }

    private void EnsureApplied()
    {
        Volatile.Write(ref _applyPending, 0);
        if (_view is { } view && _watched is { } document && TopLevel.GetTopLevel(view) is not null
            && !ReferenceEquals(view.Snapshot, document.Latest))
        {
            view.ApplyLatest();
        }
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_watched is { } document)
        {
            document.SnapshotReady -= OnSnapshotReady;
            document.SnapshotReady += OnSnapshotReady;
            OnSnapshotReady(null);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_watched is { } document)
        {
            document.SnapshotReady -= OnSnapshotReady;
        }
    }

    private void Subscribe(MarkdownStream? stream)
    {
        if (_subscribed is not null)
        {
            _subscribed.Completed -= OnStreamCompleted;
        }
        _subscribed = stream;
        if (!ReferenceEquals(stream, _ownStream))
        {
            _ownStream = null;
        }
        if (stream is not null)
        {
            stream.Completed += OnStreamCompleted;
        }
        SetStreaming(stream is not null && !stream.Document.IsComplete);
    }

    private void OnStreamCompleted() => Dispatcher.UIThread.Post(() =>
    {
        if (_subscribed is { } stream && stream.Document.IsComplete)
        {
            SetStreaming(false);
        }
    });

    private void SetStreaming(bool value)
    {
        IsStreaming = value;
        PseudoClasses.Set(StreamingPseudoClass, value);
    }

    /// <summary>New text fades in over Arc's standard duration; Reduced keeps a shorter fade (opacity only), Off none.</summary>
    private TimeSpan FadeDuration() => ArcMotion.GetEffectiveLevel(this) switch
    {
        ArcMotionLevel.Full => ArcMotionPresets.Standard,
        ArcMotionLevel.Reduced => ArcMotionPresets.Fast,
        _ => TimeSpan.Zero
    };

    /// <summary>
    /// Writes the typography StreamMark reads as <c>Md.*</c> resources. Changing them raises ResourcesChanged, which
    /// makes the view resolve its style again (inherited font changes alone would not).
    /// </summary>
    private void UpdateTypography()
    {
        var fontSize = FontSize;
        var lineHeight = double.IsNaN(LineHeight) ? Math.Round(fontSize * Leading) : LineHeight;
        _resources[MarkdownResourceKeys.FontSize] = fontSize;
        _resources[MarkdownResourceKeys.FontFamily] = FontFamily;
        _resources[MarkdownResourceKeys.LineHeight] = lineHeight;
        _resources[MarkdownResourceKeys.BlockSpacing] = BlockSpacing;
    }

    private void OnLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (!OpensLinks || e.RequiresConfirmation || !Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "mailto"))
        {
            return;
        }
        // Handlers added by the app run after this one; open only if none of them handled the click.
        Dispatcher.UIThread.Post(() =>
        {
            if (!e.Handled && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                _ = launcher.LaunchUriAsync(uri);
            }
        });
    }

    /// <summary>
    /// StreamMark's colors, fonts and radii as aliases of ArcDesign tokens, resolved from the view so they follow the
    /// theme scope, light/dark and the accent. Values follow Arc's rich text content styles (links in the strong
    /// accent, inline code on the muted surface, quotes with a strong border) and its CodeBlock (surface, border,
    /// mono font).
    /// </summary>
    private static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary();
        void Alias(string key, ArcDesignTokenKind token) => resources[key] = new MarkdownResourceAlias(token);
        Alias(MarkdownResourceKeys.Foreground, ArcDesignTokenKind.Foreground);
        Alias(MarkdownResourceKeys.SecondaryForeground, ArcDesignTokenKind.TextSecondary);
        Alias(MarkdownResourceKeys.LinkForeground, ArcDesignTokenKind.AccentStrong);
        Alias(MarkdownResourceKeys.SelectionBackground, ArcDesignTokenKind.AccentSubtle);
        Alias(MarkdownResourceKeys.CodeBackground, ArcDesignTokenKind.Surface);
        Alias(MarkdownResourceKeys.CodeBorderBrush, ArcDesignTokenKind.Border);
        Alias(MarkdownResourceKeys.InlineCodeBackground, ArcDesignTokenKind.SurfaceMuted);
        Alias(MarkdownResourceKeys.InlineCodeForeground, ArcDesignTokenKind.Foreground);
        Alias(MarkdownResourceKeys.ListMarkerForeground, ArcDesignTokenKind.TextMuted);
        Alias(MarkdownResourceKeys.MarkBackground, ArcDesignTokenKind.WarningSubtle);
        Alias(MarkdownResourceKeys.QuoteBorderBrush, ArcDesignTokenKind.BorderStrong);
        Alias(MarkdownResourceKeys.TableBorderBrush, ArcDesignTokenKind.Border);
        Alias(MarkdownResourceKeys.TableHeaderBackground, ArcDesignTokenKind.SurfaceMuted);
        Alias(MarkdownResourceKeys.HoverBackground, ArcDesignTokenKind.SurfaceMuted);
        Alias(MarkdownResourceKeys.MonospaceFontFamily, ArcDesignTokenKind.MonoFontFamily);
        Alias(MarkdownResourceKeys.CornerRadius, ArcDesignTokenKind.RadiusControl);
        return resources;
    }
}
