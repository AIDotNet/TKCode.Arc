using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Arguments of the attachment chip's and tray's requests: the attachment the person acted on.</summary>
internal sealed class AiAttachmentRequestEventArgs : RoutedEventArgs
{
    public AiAttachmentRequestEventArgs(RoutedEvent routedEvent, AiAttachment attachment) : base(routedEvent)
    {
        Attachment = attachment;
    }

    /// <summary>The attachment of the chip.</summary>
    public AiAttachment Attachment { get; }
}

/// <summary>How <see cref="AiAttachmentChip"/> looks.</summary>
internal enum AiAttachmentChipAppearance
{
    /// <summary>The AI composer's 212 × 52 card with a thumbnail or type icon, the name over a meta line, and a round button.</summary>
    Card,

    /// <summary>
    /// The AI chat's 32px pill on SurfaceMuted: a 14px file icon, the name, the size and a 24px remove button, on one line
    /// (pictures show the file icon too).
    /// </summary>
    Compact
}

/// <summary>
/// One attachment of the AI composer (Arc's attachment chip): a 212 × 52 card with a 38px thumbnail or a type icon, the
/// name (ellipsized), a meta line and a 28px round action button. The look follows <see cref="AiAttachment.Status"/>:
/// <list type="bullet">
/// <item>Uploading: a progress ring in the button (it cancels; "Stop uploading {name}") and "40% uploaded";</item>
/// <item>Done: the ring turns into a check (the button removes);</item>
/// <item>Ready: the check turns into an X that removes ("Remove {name}") and the meta line ("PDF, 469 KB");</item>
/// <item>Error: a red border, a warning glyph and the short reason in the meta line, and the X.</item>
/// </list>
/// The chip only asks: <see cref="RemoveRequested"/> and <see cref="CancelRequested"/> bubble with the attachment, and
/// the owner decides. Every word comes from the owner through <see cref="RemoveText"/>, <see cref="CancelText"/>,
/// <see cref="UploadingText"/>, <see cref="ErrorText"/>, <see cref="ImageText"/> and <see cref="LinesText"/>.
/// The chip itself takes no focus; its button is a normal tab stop. Status swaps follow Arc's motion (meta line
/// crossfade, ring → check → X on a spring); reduced motion crossfades in 120ms, Off swaps at once.
/// <para>
/// <see cref="Appearance"/> <see cref="AiAttachmentChipAppearance.Compact"/> is the AI chat's chip (spec ai-chat 3.16):
/// 32px high, 12 corners, SurfaceMuted, padding 0 4 0 10, 6 between the parts: the file icon (14, TextSecondary; the
/// attachment's own icon when it has one, the clipboard for a pasted text), the name (14, Foreground, ellipsized past
/// 200), the size (12, TextMuted, tabular figures; the meta line when the size is unknown) and the 24px button (8
/// corners, a 13px glyph in TextMuted). The states and their motion are the card's.
/// </para>
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(ThumbnailPart, typeof(Border))]
[TemplatePart(IconPart, typeof(ArcGlyph))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(ArcContentSwap))]
[TemplatePart(ActionPart, typeof(AiAttachmentChipButton))]
[TemplatePart(GlyphPart, typeof(AiAttachmentStatusGlyph))]
[PseudoClasses(UploadingPseudoClass, DonePseudoClass, ReadyPseudoClass, ErrorPseudoClass, ThumbnailPseudoClass, CompactPseudoClass)]
internal class AiAttachmentChip : TemplatedControl
{
    public const string FramePart = "PART_Frame";
    public const string ThumbnailPart = "PART_Thumbnail";
    public const string IconPart = "PART_Icon";
    public const string NamePart = "PART_Name";
    public const string MetaPart = "PART_Meta";
    public const string ActionPart = "PART_Action";
    public const string GlyphPart = "PART_Glyph";

    public const string UploadingPseudoClass = ":uploading";
    public const string DonePseudoClass = ":done";
    public const string ReadyPseudoClass = ":ready";
    public const string ErrorPseudoClass = ":error";

    /// <summary>Set while the chip shows a picture instead of a type icon.</summary>
    public const string ThumbnailPseudoClass = ":thumbnail";

    /// <summary>Set while <see cref="Appearance"/> is <see cref="AiAttachmentChipAppearance.Compact"/>.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>How much <c>Danger</c> the error border mixes into <c>Border</c> (OKLab), as measured.</summary>
    internal const double ErrorBorderMix = 0.35;

    #region Properties

    /// <summary>The composer's card (default) or the chat's compact pill.</summary>
    public static readonly StyledProperty<AiAttachmentChipAppearance> AppearanceProperty =
        AvaloniaProperty.Register<AiAttachmentChip, AiAttachmentChipAppearance>(nameof(Appearance));

    /// <summary>The attachment shown; the chip follows its changes while it is in a window.</summary>
    public static readonly StyledProperty<AiAttachment?> AttachmentProperty =
        AvaloniaProperty.Register<AiAttachmentChip, AiAttachment?>(nameof(Attachment));

    /// <summary>The remove button's accessible name for a file name ("Remove {0}"); null names it by the file name alone.</summary>
    public static readonly StyledProperty<Func<string, string>?> RemoveTextProperty =
        AvaloniaProperty.Register<AiAttachmentChip, Func<string, string>?>(nameof(RemoveText));

    /// <summary>The cancel button's accessible name for a file name ("Stop uploading {0}"); null names it by the file name alone.</summary>
    public static readonly StyledProperty<Func<string, string>?> CancelTextProperty =
        AvaloniaProperty.Register<AiAttachmentChip, Func<string, string>?>(nameof(CancelText));

    /// <summary>The meta line while uploading, for a whole percentage ("{0}% uploaded"); null shows "{0}%".</summary>
    public static readonly StyledProperty<Func<int, string>?> UploadingTextProperty =
        AvaloniaProperty.Register<AiAttachmentChip, Func<int, string>?>(nameof(UploadingText));

    /// <summary>The reason shown for an error that brought none of its own (<see cref="AiAttachment.Error"/> empty).</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<AiAttachmentChip, string?>(nameof(ErrorText));

    /// <summary>The type word of a picture's meta line ("Picture" in "Picture, 240 KB"); null uses the extension.</summary>
    public static readonly StyledProperty<string?> ImageTextProperty =
        AvaloniaProperty.Register<AiAttachmentChip, string?>(nameof(ImageText));

    /// <summary>The meta line of a pasted text without its own <see cref="AiAttachment.Meta"/>, for a line count ("{0} lines").</summary>
    public static readonly StyledProperty<Func<int, string>?> LinesTextProperty =
        AvaloniaProperty.Register<AiAttachmentChip, Func<int, string>?>(nameof(LinesText));

    /// <summary>The error border: <c>Danger</c> mixed into <c>Border</c>; follows the theme.</summary>
    internal static readonly StyledProperty<IBrush?> ErrorBorderBrushProperty =
        AvaloniaProperty.Register<AiAttachmentChip, IBrush?>(nameof(ErrorBorderBrush));

    // The two theme colors the error border is mixed from, bound to their tokens.
    private static readonly StyledProperty<IBrush?> DangerSourceProperty =
        AvaloniaProperty.Register<AiAttachmentChip, IBrush?>("DangerSource");

    private static readonly StyledProperty<IBrush?> BorderSourceProperty =
        AvaloniaProperty.Register<AiAttachmentChip, IBrush?>("BorderSource");

    /// <summary>Raised (bubbling) when the person presses the button of a chip that is done, ready or in error.</summary>
    public static readonly RoutedEvent<AiAttachmentRequestEventArgs> RemoveRequestedEvent =
        RoutedEvent.Register<AiAttachmentChip, AiAttachmentRequestEventArgs>(nameof(RemoveRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person presses the button of a chip that is uploading.</summary>
    public static readonly RoutedEvent<AiAttachmentRequestEventArgs> CancelRequestedEvent =
        RoutedEvent.Register<AiAttachmentChip, AiAttachmentRequestEventArgs>(nameof(CancelRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="AppearanceProperty"/>
    public AiAttachmentChipAppearance Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }

    /// <inheritdoc cref="AttachmentProperty"/>
    public AiAttachment? Attachment { get => GetValue(AttachmentProperty); set => SetValue(AttachmentProperty, value); }

    /// <inheritdoc cref="RemoveTextProperty"/>
    public Func<string, string>? RemoveText { get => GetValue(RemoveTextProperty); set => SetValue(RemoveTextProperty, value); }

    /// <inheritdoc cref="CancelTextProperty"/>
    public Func<string, string>? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <inheritdoc cref="UploadingTextProperty"/>
    public Func<int, string>? UploadingText { get => GetValue(UploadingTextProperty); set => SetValue(UploadingTextProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="ImageTextProperty"/>
    public string? ImageText { get => GetValue(ImageTextProperty); set => SetValue(ImageTextProperty, value); }

    /// <inheritdoc cref="LinesTextProperty"/>
    public Func<int, string>? LinesText { get => GetValue(LinesTextProperty); set => SetValue(LinesTextProperty, value); }

    /// <inheritdoc cref="ErrorBorderBrushProperty"/>
    internal IBrush? ErrorBorderBrush { get => GetValue(ErrorBorderBrushProperty); set => SetValue(ErrorBorderBrushProperty, value); }

    /// <inheritdoc cref="RemoveRequestedEvent"/>
    public event EventHandler<AiAttachmentRequestEventArgs>? RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    /// <inheritdoc cref="CancelRequestedEvent"/>
    public event EventHandler<AiAttachmentRequestEventArgs>? CancelRequested
    {
        add => AddHandler(CancelRequestedEvent, value);
        remove => RemoveHandler(CancelRequestedEvent, value);
    }

    #endregion

    /// <summary>Which meta line is showing; a change of kind crossfades, a change of text within a kind does not.</summary>
    private enum MetaKind
    {
        None,
        Uploading,
        Info,
        Error
    }

    private AiAttachment? _watched;
    private Border? _thumbnail;
    private TextBlock? _name;
    private ArcGlyph? _icon;
    private ArcContentSwap? _meta;
    private AiAttachmentChipButton? _action;
    private AiAttachmentStatusGlyph? _glyph;
    private AiAttachmentChipMeta? _metaLine;
    private MetaKind _metaKind;

    public AiAttachmentChip()
    {
        ArcTokens.Bind(this, DangerSourceProperty, ArcDesignTokenKind.Danger);
        ArcTokens.Bind(this, BorderSourceProperty, ArcDesignTokenKind.Border);
    }

    /// <summary>The meta line as shown ("PDF, 469 KB", "40% uploaded", the error reason).</summary>
    internal string MetaText => _metaLine?.Text ?? string.Empty;

    /// <summary>The button part.</summary>
    internal AiAttachmentChipButton? ActionButton => _action;

    /// <summary>The ring / check / X part.</summary>
    internal AiAttachmentStatusGlyph? StatusGlyph => _glyph;

    /// <summary>The meta line control showing now.</summary>
    internal AiAttachmentChipMeta? MetaLine => _metaLine;

    /// <summary>The whole percentage shown while uploading (progress rounded, 0 to 100).</summary>
    internal static int Percent(double progress) =>
        (int)Math.Round(Math.Clamp(double.IsNaN(progress) ? 0 : progress, 0, 1) * 100, MidpointRounding.AwayFromZero);

    #region Template and state

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        _thumbnail = e.NameScope.Find<Border>(ThumbnailPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _icon = e.NameScope.Find<ArcGlyph>(IconPart);
        _meta = e.NameScope.Find<ArcContentSwap>(MetaPart);
        _action = e.NameScope.Find<AiAttachmentChipButton>(ActionPart);
        _glyph = e.NameScope.Find<AiAttachmentStatusGlyph>(GlyphPart);
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        _metaKind = MetaKind.None;
        _metaLine = null;
        Refresh(instant: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AttachmentProperty)
        {
            Watch(this.IsAttachedToVisualTree() ? change.GetNewValue<AiAttachment?>() : null);
            // Another attachment is another chip: no swap motion from the old one.
            Refresh(instant: true);
        }
        else if (change.Property == RemoveTextProperty || change.Property == CancelTextProperty || change.Property == UploadingTextProperty
                 || change.Property == ErrorTextProperty || change.Property == ImageTextProperty || change.Property == LinesTextProperty)
        {
            Refresh(instant: false);
        }
        else if (change.Property == DangerSourceProperty || change.Property == BorderSourceProperty)
        {
            UpdateErrorBorder();
        }
        else if (change.Property == AppearanceProperty)
        {
            // The theme swaps the template; the parts of the new one are filled in when it is applied.
            PseudoClasses.Set(CompactPseudoClass, change.GetNewValue<AiAttachmentChipAppearance>() == AiAttachmentChipAppearance.Compact);
            Refresh(instant: true);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Attachment);
        // The attachment may have moved on while the chip was away.
        Refresh(instant: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Out of a window the chip lets go of the attachment, so a long-lived attachment does not keep it alive.
        Watch(null);
    }

    private void Watch(AiAttachment? attachment)
    {
        if (ReferenceEquals(_watched, attachment))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnAttachmentChanged;
        }
        _watched = attachment;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnAttachmentChanged;
        }
    }

    private void OnAttachmentChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiAttachment.ProgressProperty)
        {
            // Progress moves the ring and the percentage only: no relayout, no swap.
            UpdateProgress();
            return;
        }
        Refresh(instant: false);
    }

    /// <summary>Brings every part in line with the attachment; <paramref name="instant"/> skips the swap motion.</summary>
    private void Refresh(bool instant)
    {
        var attachment = Attachment;
        var status = attachment?.Status ?? AiAttachmentStatus.Ready;
        PseudoClasses.Set(UploadingPseudoClass, attachment is not null && status == AiAttachmentStatus.Uploading);
        PseudoClasses.Set(DonePseudoClass, attachment is not null && status == AiAttachmentStatus.Done);
        PseudoClasses.Set(ReadyPseudoClass, attachment is not null && status == AiAttachmentStatus.Ready);
        PseudoClasses.Set(ErrorPseudoClass, attachment is not null && status == AiAttachmentStatus.Error);
        var name = attachment?.Name ?? string.Empty;
        if (_name is not null)
        {
            _name.Text = name;
        }
        AutomationProperties.SetName(this, name);
        Tooltip.SetTip(this, string.IsNullOrEmpty(name) ? null : name);

        // Lead: the picture, or the type icon (the compact chip always shows an icon).
        var compact = IsCompact;
        var thumbnail = !compact && attachment is { Kind: AiAttachmentKind.Image, Thumbnail: not null } ? attachment.Thumbnail : null;
        PseudoClasses.Set(ThumbnailPseudoClass, thumbnail is not null);
        if (_thumbnail?.Child is Image image)
        {
            image.Source = thumbnail;
        }
        if (_icon is not null)
        {
            _icon.Data = attachment is null ? null : compact ? CompactIconFor(attachment) : AiFileIntake.IconFor(attachment);
        }

        // Button: cancel while uploading, remove otherwise.
        if (_action is not null)
        {
            var uploading = status == AiAttachmentStatus.Uploading;
            var format = uploading ? CancelText : RemoveText;
            AutomationProperties.SetName(_action, attachment is null ? string.Empty : format?.Invoke(name) ?? name);
        }
        if (_glyph is not null)
        {
            _glyph.Progress = attachment?.Progress ?? 0;
            _glyph.SetStatus(status, instant);
        }
        UpdateMeta(instant);
    }

    private bool IsCompact => Appearance == AiAttachmentChipAppearance.Compact;

    /// <summary>The compact chip's icon: the attachment's own, the clipboard for a pasted text, else a page of text (pictures too, as Arc's chat does).</summary>
    internal static Geometry CompactIconFor(AiAttachment attachment) =>
        attachment.Icon ?? (attachment.Kind == AiAttachmentKind.PastedText ? ArcIcons.ClipboardPaste : ArcIcons.FileText);

    /// <summary>The compact chip's info line: the size when it is known ("84 KB"), else the meta line (a pasted text's lines).</summary>
    private string CompactInfo(AiAttachment attachment) =>
        attachment.Kind != AiAttachmentKind.PastedText && attachment.Size is { } bytes
            ? AiFileIntake.FormatBytes(bytes)
            : attachment.Meta ?? AiFileIntake.DefaultMeta(attachment, ImageText, LinesText);

    private void UpdateProgress()
    {
        if (_glyph is not null)
        {
            _glyph.Progress = Attachment?.Progress ?? 0;
        }
        if (_metaKind == MetaKind.Uploading && _metaLine is not null)
        {
            _metaLine.Text = UploadingLine();
            AutomationProperties.SetHelpText(this, _metaLine.Text);
        }
    }

    private string UploadingLine()
    {
        var percent = Percent(Attachment?.Progress ?? 0);
        return UploadingText?.Invoke(percent) ?? percent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>The meta line: a new line crossfades in when its kind changes; within a kind the text changes in place.</summary>
    private void UpdateMeta(bool instant)
    {
        var attachment = Attachment;
        var kind = attachment is null ? MetaKind.None : attachment.Status switch
        {
            AiAttachmentStatus.Uploading => MetaKind.Uploading,
            AiAttachmentStatus.Error => MetaKind.Error,
            _ => MetaKind.Info
        };
        var text = kind switch
        {
            MetaKind.Uploading => UploadingLine(),
            MetaKind.Error => string.IsNullOrWhiteSpace(attachment!.Error) ? ErrorText ?? string.Empty : attachment.Error,
            MetaKind.Info => IsCompact ? CompactInfo(attachment!) : attachment!.Meta ?? AiFileIntake.DefaultMeta(attachment, ImageText, LinesText),
            _ => string.Empty
        };
        AutomationProperties.SetHelpText(this, text);
        if (_meta is null)
        {
            return;
        }
        if (kind == _metaKind && _metaLine is not null)
        {
            _metaLine.Text = text;
            return;
        }
        _metaKind = kind;
        // A new line each time: the old one may still be fading out in the swap.
        _metaLine = new AiAttachmentChipMeta
        {
            Text = text,
            IsError = kind == MetaKind.Error,
            IsProgress = kind == MetaKind.Uploading,
            IsTabular = IsCompact && kind == MetaKind.Info
        };
        if (instant)
        {
            var mode = _meta.Mode;
            _meta.Mode = ContentSwapMode.None;
            _meta.Content = _metaLine;
            _meta.Mode = mode;
        }
        else
        {
            _meta.Content = _metaLine;
        }
    }

    private void UpdateErrorBorder()
    {
        var danger = GetValue(DangerSourceProperty) is ISolidColorBrush d ? d.Color : (Color?)null;
        var border = GetValue(BorderSourceProperty) is ISolidColorBrush b ? b.Color : (Color?)null;
        ErrorBorderBrush = danger is { } dc && border is { } bc ? new ImmutableSolidColorBrush(Oklch.MixOklab(bc, dc, ErrorBorderMix)) : null;
    }

    #endregion

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Attachment is not { } attachment)
        {
            return;
        }
        var routed = attachment.Status == AiAttachmentStatus.Uploading ? CancelRequestedEvent : RemoveRequestedEvent;
        RaiseEvent(new AiAttachmentRequestEventArgs(routed, attachment));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChipAutomationPeer(this);

    /// <summary>A list item named by the file; its meta line is the help text and its button is its only child.</summary>
    private sealed class ChipAutomationPeer(AiAttachmentChip owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
