using AtomUI.Controls;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The arrival of a new message or of the typing row (P19): from its own side, slightly small and low, out of a subtle
/// blur, on Arc's <c>ARRIVE</c> spring with the opacity on the standard tween; leaving fades and shrinks. Reduced fades
/// only (fast); Off applies the end state. Drawn with the row's render transform, so the list never relayouts.
/// </summary>
internal sealed class ChatThreadRowMotion
{
    private static readonly SpringSpec s_arrive = SpringSpec.FromVisual(0.46, 0.12);
    private static readonly SpringSpec s_snappy = ArcMotionPresets.Snappy;

    private readonly Control _owner;
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private MotionValue<double>? _travel;
    private MotionValue<double>? _fade;
    private int _generation;
    private double _fromScale = 1;
    private Vector _fromOffset;
    private double _blur;

    public ChatThreadRowMotion(Control owner)
    {
        _owner = owner;
    }

    /// <summary>True while an arrival or exit plays.</summary>
    public bool IsMoving => _travel?.IsAnimating == true || _fade?.IsAnimating == true;

    /// <summary>
    /// Plays an arrival. <paramref name="typing"/> uses the typing row's pop (scale .9, 8px, snappy) instead of a
    /// message's (scale .92, 14px up, 8px from its side, ARRIVE).
    /// </summary>
    public void Arrive(bool mine, bool typing)
    {
        var level = ArcMotion.GetEffectiveLevel(_owner);
        _generation++;
        if (level == ArcMotionLevel.Off || !_owner.IsAttachedToVisualTree())
        {
            Rest();
            return;
        }
        Ensure(mine);
        if (level == ArcMotionLevel.Full)
        {
            _fromScale = typing ? 0.9 : 0.92;
            _fromOffset = typing ? new Vector(0, 8) : new Vector(mine ? 8 : -8, 14);
            _blur = typing ? 0 : 2;
            _travel!.Jump(0);
            _fade!.Jump(0);
            _ = _travel.AnimateTo(1, MotionSpec.Spring(typing ? s_snappy : s_arrive));
            _ = _fade.AnimateTo(1, typing ? MotionSpec.Spring(s_snappy) : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        else
        {
            _fromScale = 1;
            _fromOffset = default;
            _blur = 0;
            _travel!.Jump(1);
            _fade!.Jump(0);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        }
        Apply();
    }

    /// <summary>Plays the typing row's exit (scale .9, fading over the exit duration); true when it finished.</summary>
    public async Task<bool> LeaveAsync()
    {
        var level = ArcMotion.GetEffectiveLevel(_owner);
        var generation = ++_generation;
        if (level == ArcMotionLevel.Off || !_owner.IsAttachedToVisualTree())
        {
            return true;
        }
        Ensure(mine: false);
        _blur = 0;
        if (level == ArcMotionLevel.Full)
        {
            _fromScale = 0.9;
            _fromOffset = default;
            _ = _travel!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        }
        var done = await _fade!.AnimateTo(0, MotionSpec.Tween(level == ArcMotionLevel.Full ? ArcMotionPresets.Exit : ArcMotionPresets.Fast, ArcEasings.Standard));
        return done && generation == _generation;
    }

    /// <summary>Drops any motion (recycled rows).</summary>
    public void Rest()
    {
        _generation++;
        _travel?.Jump(1);
        _fade?.Jump(1);
        _fromScale = 1;
        _fromOffset = default;
        _blur = 0;
        Apply();
    }

    private void Ensure(bool mine)
    {
        _owner.RenderTransformOrigin = new RelativePoint(mine ? 1 : 0, 1, RelativeUnit.Relative);
        if (_owner.RenderTransform is null)
        {
            _owner.RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        }
        if (_travel is null)
        {
            _travel = new MotionValue<double>(_owner, 1, MotionVectors.Double);
            _travel.Changed += _ => Apply();
            _fade = new MotionValue<double>(_owner, 1, MotionVectors.Double);
            _fade.Changed += _ => Apply();
        }
    }

    private void Apply()
    {
        var travel = _travel?.Value ?? 1;
        var fade = Math.Clamp(_fade?.Value ?? 1, 0, 1);
        var scale = _fromScale + (1 - _fromScale) * travel;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        _translate.X = _fromOffset.X * (1 - travel);
        _translate.Y = _fromOffset.Y * (1 - travel);
        _owner.Opacity = fade;
        _owner.Effect = ArcBlur.Get(_blur * (1 - fade));
    }
}

/// <summary>A day separator (Arc <c>.day</c>): the label between two hairlines, xs muted medium text.</summary>
internal sealed class ChatThreadDayView : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ChatThreadDayView, string?>(nameof(Label));

    public static readonly StyledProperty<bool> IsFirstProperty =
        AvaloniaProperty.Register<ChatThreadDayView, bool>(nameof(IsFirst));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsFirst { get => GetValue(IsFirstProperty); set => SetValue(IsFirstProperty, value); }

    public ChatThreadDayRow? Row { get; private set; }

    public void Bind(ChatThreadDayRow row)
    {
        Row = row;
        Refresh();
    }

    public void Refresh()
    {
        if (Row is not { } row)
        {
            return;
        }
        Label = row.Label;
        IsFirst = row.IsFirst;
        AutomationProperties.SetName(this, row.Label);
    }

    public void Unbind() => Row = null;
}

/// <summary>The typing indicator row: the first typer's portrait and a 36px bubble of breathing dots (decorative).</summary>
[TemplatePart(LinePart, typeof(ChatThreadLinePanel))]
internal sealed class ChatThreadTypingView : TemplatedControl
{
    public const string LinePart = "PART_Line";
    public const string BubblePart = "PART_Bubble";

    private ChatThreadLinePanel? _line;
    private Avatar? _face;
    private readonly ChatThreadRowMotion _motion;

    public ChatThreadTypingView()
    {
        _motion = new ChatThreadRowMotion(this);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public ChatThreadTypingRow? Row { get; private set; }

    internal ChatThreadRowMotion Motion => _motion;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _line = e.NameScope.Find<ChatThreadLinePanel>(LinePart);
        if (_line is not null)
        {
            _line.Parts = e.NameScope.Find<Control>(BubblePart);
            _face ??= ActivityAvatar.Create(28);
            if (_face.Parent is Panel old)
            {
                old.Children.Remove(_face);
            }
            _line.Children.Insert(0, _face);
            _line.Face = _face;
        }
        Refresh();
    }

    public void Bind(ChatThreadTypingRow row)
    {
        Row = row;
        Refresh();
        if (row.Fresh)
        {
            row.Fresh = false;
            _motion.Arrive(mine: false, typing: true);
        }
        else
        {
            _motion.Rest();
        }
    }

    public void Refresh()
    {
        if (Row is { } row && _face is not null)
        {
            ActivityAvatar.Update(_face, row.Typer.Name, row.Typer.Avatar);
        }
    }

    public void Unbind()
    {
        Row = null;
        _motion.Rest();
    }
}

/// <summary>
/// One message (Arc <c>.row</c>): meta line, the line of pictures, files and bubble with the add-reaction button and
/// the author's portrait, the reactions row (springs open, P6) and the receipts line (status, Retry, readers).
/// Containers are recycled by the virtualizing list; <see cref="Bind"/> points one at another message.
/// </summary>
[TemplatePart(LinePart, typeof(ChatThreadLinePanel))]
[TemplatePart(PartsPart, typeof(StackPanel))]
[TemplatePart(ReactPart, typeof(ChatThreadButton))]
[TemplatePart(ReactionsHostPart, typeof(PresenceHost))]
[TemplatePart(ReactionsPart, typeof(ActivityReactionBar))]
[PseudoClasses(MinePseudoClass, FirstPseudoClass, LastPseudoClass, AfterDayPseudoClass, FailedPseudoClass, PickerOpenPseudoClass)]
internal sealed class ChatThreadMessageView : TemplatedControl
{
    public const string RowPart = "PART_Row";
    public const string MetaPart = "PART_Meta";
    public const string AuthorPart = "PART_Author";
    public const string TimePart = "PART_Time";
    public const string LinePart = "PART_Line";
    public const string PartsPart = "PART_Parts";
    public const string ReactPart = "PART_React";
    public const string ReactionsHostPart = "PART_ReactionsHost";
    public const string ReactionsClipPart = "PART_ReactionsClip";
    public const string ReactionsPart = "PART_Reactions";
    public const string ReceiptsPart = "PART_Receipts";
    public const string StatusPart = "PART_Status";
    public const string StatusTextPart = "PART_StatusText";
    public const string RetryPart = "PART_Retry";
    public const string ReadersPart = "PART_Readers";

    public const string MinePseudoClass = ":mine";
    public const string FirstPseudoClass = ":first";
    public const string LastPseudoClass = ":last";
    public const string AfterDayPseudoClass = ":after-day";
    public const string FailedPseudoClass = ":failed";
    public const string PickerOpenPseudoClass = ":picker-open";

    public static readonly StyledProperty<bool> IsMineProperty =
        AvaloniaProperty.Register<ChatThreadMessageView, bool>(nameof(IsMine));

    public static readonly StyledProperty<double> StackMaxRatioProperty =
        AvaloniaProperty.Register<ChatThreadMessageView, double>(nameof(StackMaxRatio), 0.78);

    /// <summary>Bubble radius and tight radius, set by the theme from <see cref="ChatThreadToken"/>.</summary>
    public static readonly StyledProperty<double> BubbleRadiusProperty =
        AvaloniaProperty.Register<ChatThreadMessageView, double>(nameof(BubbleRadius), 18);

    public static readonly StyledProperty<double> BubbleTightRadiusProperty =
        AvaloniaProperty.Register<ChatThreadMessageView, double>(nameof(BubbleTightRadius), 6);

    public static readonly StyledProperty<double> ReaderSizeProperty =
        AvaloniaProperty.Register<ChatThreadMessageView, double>(nameof(ReaderSize), 16);

    public static readonly StyledProperty<double> ReaderOverlapProperty =
        AvaloniaProperty.Register<ChatThreadMessageView, double>(nameof(ReaderOverlap), -4);

    public bool IsMine { get => GetValue(IsMineProperty); set => SetValue(IsMineProperty, value); }
    public double StackMaxRatio { get => GetValue(StackMaxRatioProperty); set => SetValue(StackMaxRatioProperty, value); }
    public double BubbleRadius { get => GetValue(BubbleRadiusProperty); set => SetValue(BubbleRadiusProperty, value); }
    public double BubbleTightRadius { get => GetValue(BubbleTightRadiusProperty); set => SetValue(BubbleTightRadiusProperty, value); }
    public double ReaderSize { get => GetValue(ReaderSizeProperty); set => SetValue(ReaderSizeProperty, value); }
    public double ReaderOverlap { get => GetValue(ReaderOverlapProperty); set => SetValue(ReaderOverlapProperty, value); }

    private ChatThread? _thread;
    private StackPanel? _meta;
    private TextBlock? _author;
    private TextBlock? _time;
    private ChatThreadLinePanel? _line;
    private StackPanel? _parts;
    private ChatThreadButton? _react;
    private PresenceHost? _reactionsHost;
    private Border? _reactionsClip;
    private ActivityReactionBar? _reactions;
    private Control? _receipts;
    private Control? _status;
    private TextBlock? _statusText;
    private ChatThreadButton? _retry;
    private StackPanel? _readers;

    private Avatar? _face;
    private readonly ChatThreadBubble _bubble;
    private readonly ChatThreadBubbleBody _body;
    private readonly SelectableTextBlock _plain;
    private ChatThreadGallery? _gallery;
    private readonly List<ChatThreadButton> _files = new();
    private readonly List<Avatar> _readerPool = new();
    private readonly ChatThreadRowMotion _motion;

    private IReadOnlyList<ChatAttachment>? _shownAttachments;
    private bool _hasShownReactions;

    public ChatThreadMessageView()
    {
        _motion = new ChatThreadRowMotion(this);
        _plain = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        _body = new ChatThreadBubbleBody();
        _bubble = new ChatThreadBubble { Child = _body };
    }

    public ChatThreadMessageRow? Row { get; private set; }

    /// <summary>The bubble (tests and the picker anchor).</summary>
    internal ChatThreadBubble Bubble => _bubble;

    /// <summary>The line the reaction picker anchors to.</summary>
    internal ChatThreadLinePanel? Line => _line;

    internal ChatThreadButton? ReactButton => _react;

    internal ChatThreadButton? RetryButton => _retry;

    internal ActivityReactionBar? ReactionBar => _reactions;

    internal PresenceHost? ReactionsHost => _reactionsHost;

    internal StackPanel? Readers => _readers;

    internal Control? Receipts => _receipts;

    internal TextBlock? StatusText => _statusText;

    internal Avatar? Face => _face;

    internal TextBlock? MetaAuthor => _author;

    internal TextBlock? MetaTime => _time;

    internal Control? Meta => _meta;

    internal ChatThreadGallery? Gallery => _gallery;

    internal IReadOnlyList<ChatThreadButton> FileRows => _files;

    internal ChatThreadRowMotion Motion => _motion;

    /// <summary>The body element shown in the bubble: the plain text block or a <see cref="Markdown"/>.</summary>
    internal Control? BodyContent => _body.Child;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_react is not null)
        {
            _react.Click -= OnReactClick;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_reactions is not null)
        {
            _reactions.Toggled -= OnReactionToggled;
        }
        _meta = e.NameScope.Find<StackPanel>(MetaPart);
        _author = e.NameScope.Find<TextBlock>(AuthorPart);
        _time = e.NameScope.Find<TextBlock>(TimePart);
        _line = e.NameScope.Find<ChatThreadLinePanel>(LinePart);
        _parts = e.NameScope.Find<StackPanel>(PartsPart);
        _react = e.NameScope.Find<ChatThreadButton>(ReactPart);
        _reactionsHost = e.NameScope.Find<PresenceHost>(ReactionsHostPart);
        _reactionsClip = e.NameScope.Find<Border>(ReactionsClipPart);
        _reactions = e.NameScope.Find<ActivityReactionBar>(ReactionsPart);
        _receipts = e.NameScope.Find<Control>(ReceiptsPart);
        _status = e.NameScope.Find<Control>(StatusPart);
        _statusText = e.NameScope.Find<TextBlock>(StatusTextPart);
        _retry = e.NameScope.Find<ChatThreadButton>(RetryPart);
        _readers = e.NameScope.Find<StackPanel>(ReadersPart);
        if (_react is not null)
        {
            _react.Content ??= new ArcGlyph { Data = ArcIcons.SmilePlus, Size = 16, StrokeThickness = 1.75 };
            _react.Click += OnReactClick;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_reactions is not null)
        {
            _reactions.Toggled += OnReactionToggled;
        }
        if (_line is not null)
        {
            _line.Parts = _parts;
            _line.Button = _react;
        }
        if (_parts is not null && _bubble.Parent is null)
        {
            _parts.Children.Add(_bubble);
        }
        Refresh();
    }

    /// <summary>Shows <paramref name="row"/> of <paramref name="thread"/>.</summary>
    public void Bind(ChatThread thread, ChatThreadMessageRow row)
    {
        var same = ReferenceEquals(Row?.Message, row.Message);
        _thread = thread;
        Row = row;
        if (!same)
        {
            _shownAttachments = null;
            _hasShownReactions = false;
            _reactions?.SetReactions(null, animate: false);
            _reactionsHost?.SetCurrentValue(PresenceHost.IsPresentProperty, false);
        }
        Refresh(animate: same);
        if (row.Fresh)
        {
            row.Fresh = false;
            _motion.Arrive(row.Mine, typing: false);
        }
        else if (!same)
        {
            _motion.Rest();
        }
    }

    /// <summary>Releases the message (the container goes back to the pool).</summary>
    public void Unbind()
    {
        _motion.Rest();
        if (_body.Child is Markdown)
        {
            _body.Child = null;
        }
        _thread = null;
        Row = null;
        foreach (var avatar in _readerPool)
        {
            avatar.Source = null;
        }
        if (_gallery is not null)
        {
            foreach (var tile in _gallery.Children.OfType<ChatThreadButton>())
            {
                if (tile.Content is Panel { Children: [AsyncImage image, ..] })
                {
                    image.Source = null;
                }
            }
        }
        _shownAttachments = null;
    }

    public void Refresh() => Refresh(animate: true);

    /// <summary>Re-reads the row: flags, meta, body, attachments, reactions and receipts.</summary>
    public void Refresh(bool animate)
    {
        if (Row is not { } row || _thread is not { } thread || _parts is null)
        {
            return;
        }
        var message = row.Message;
        var labels = thread.Labels;
        IsMine = row.Mine;
        StackMaxRatio = thread.StackMaxRatio;
        PseudoClasses.Set(MinePseudoClass, row.Mine);
        PseudoClasses.Set(FirstPseudoClass, row.First);
        PseudoClasses.Set(LastPseudoClass, row.Last);
        PseudoClasses.Set(AfterDayPseudoClass, row.AfterDay);
        PseudoClasses.Set(FailedPseudoClass, message.Status == ChatMessageStatus.Failed);
        PseudoClasses.Set(PickerOpenPseudoClass, thread.IsPickerOpenFor(message));
        if (_line is not null)
        {
            _line.IsMine = row.Mine;
        }
        if (_react is not null)
        {
            _react.IsOpen = thread.IsPickerOpenFor(message);
            AutomationProperties.SetName(_react, labels.AddReaction);
        }

        // Meta: author (others) and time on the first message of a group.
        var time = ActivityTime.Clock(message.CreatedAt, thread.EffectiveTimeZone, thread.EffectiveCulture);
        if (_meta is not null)
        {
            _meta.IsVisible = row.First;
            _meta.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        if (_author is not null)
        {
            _author.IsVisible = !row.Mine;
            _author.Text = row.Author.Name;
        }
        if (_time is not null)
        {
            _time.Text = time;
        }

        // The author's portrait hangs in the gutter beside the last message of a group.
        var showFace = !row.Mine && row.Last;
        if (showFace)
        {
            if (_face is null)
            {
                _face = ActivityAvatar.Create(28);
                _line?.Children.Insert(0, _face);
                if (_line is not null)
                {
                    _line.Face = _face;
                }
            }
            ActivityAvatar.Update(_face, row.Author.Name, row.Author.Avatar);
            _face.IsVisible = true;
        }
        else if (_face is not null)
        {
            _face.IsVisible = false;
            _face.Source = null;
        }

        UpdateParts(thread, row);
        UpdateReactions(thread, row, animate);
        UpdateReceipts(thread, row);

        var who = row.Mine ? string.Empty : row.Author.Name + ", ";
        AutomationProperties.SetName(this, $"{who}{time}: {message.Text}".TrimEnd(' ', ':'));
        _line?.InvalidateMeasure();
    }

    private void UpdateParts(ChatThread thread, ChatThreadMessageRow row)
    {
        var message = row.Message;
        var parts = _parts!;
        parts.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        // Attachments: pictures in the gallery, everything else as file rows (rebuilt only when the list changes).
        if (!ReferenceEquals(_shownAttachments, message.Attachments) || _shownAttachments is null)
        {
            _shownAttachments = message.Attachments;
            RebuildAttachments(thread, row);
        }
        foreach (var file in _files)
        {
            file.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        // The bubble.
        var text = message.Text;
        var hasText = !string.IsNullOrEmpty(text);
        _bubble.IsVisible = hasText;
        _bubble.IsMine = row.Mine;
        _bubble.IsFailed = message.Status == ChatMessageStatus.Failed;
        _bubble.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        _bubble.CornerRadius = ChatThreadBubble.Corners(row.Mine, row.First, row.Last, BubbleRadius, BubbleTightRadius);
        Tooltip.SetTip(_bubble, hasText ? ActivityTime.MediumDateTime(message.CreatedAt, thread.EffectiveTimeZone, thread.EffectiveCulture) : null);
        if (!hasText)
        {
            if (_body.Child is Markdown)
            {
                _body.Child = null;
            }
            return;
        }
        if (thread.UsesMarkdown(message))
        {
            var markdown = thread.AcquireMarkdown(message, row.Mine);
            if (!ReferenceEquals(_body.Child, markdown))
            {
                if (markdown.Parent is Decorator previous)
                {
                    previous.Child = null;
                }
                _body.Child = markdown;
            }
        }
        else
        {
            _plain.Text = text;
            _plain.Classes.Set("mine", row.Mine);
            if (!ReferenceEquals(_body.Child, _plain))
            {
                _body.Child = _plain;
            }
        }
    }

    private void RebuildAttachments(ChatThread thread, ChatThreadMessageRow row)
    {
        var parts = _parts!;
        var attachments = row.Message.Attachments ?? Array.Empty<ChatAttachment>();
        var images = attachments.Where(a => a.ShowsAsImage).ToList();
        var files = attachments.Where(a => !a.ShowsAsImage).ToList();

        if (images.Count > 0)
        {
            _gallery ??= new ChatThreadGallery();
            _gallery.Classes.Set("multi", images.Count > 1);
            if (_gallery.Parent is null)
            {
                parts.Children.Insert(0, _gallery);
            }
            var shown = Math.Min(4, images.Count);
            _gallery.SingleAspect = images.Count == 1 && images[0] is { Width: > 0, Height: > 0 } one ? one.Width!.Value / one.Height!.Value : 4.0 / 3.0;
            while (_gallery.Children.Count > shown)
            {
                _gallery.Children.RemoveAt(_gallery.Children.Count - 1);
            }
            for (var i = 0; i < shown; i++)
            {
                var image = images[i];
                ChatThreadButton tile;
                if (i < _gallery.Children.Count)
                {
                    tile = (ChatThreadButton)_gallery.Children[i];
                }
                else
                {
                    tile = CreateTile();
                    _gallery.Children.Add(tile);
                }
                var panel = (Panel)tile.Content!;
                var asyncImage = (AsyncImage)panel.Children[0];
                asyncImage.Source = image.ImageSource;
                var more = (Border)panel.Children[1];
                var extra = images.Count - 4;
                more.IsVisible = i == 3 && extra > 0;
                ((TextBlock)more.Child!).Text = $"+{extra}";
                tile.CornerRadius = ChatThreadGalleryCorners.For(i, shown, thread.GalleryRadius, thread.GalleryTileRadius);
                tile.Tag = image;
                AutomationProperties.SetName(tile, image.Alt ?? image.Name);
            }
            _gallery.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _gallery.InvalidateMeasure();
        }
        else if (_gallery?.Parent is Panel owner)
        {
            owner.Children.Remove(_gallery);
        }

        while (_files.Count > files.Count)
        {
            var last = _files[^1];
            _files.RemoveAt(_files.Count - 1);
            parts.Children.Remove(last);
        }
        for (var i = 0; i < files.Count; i++)
        {
            ChatThreadButton row2;
            if (i < _files.Count)
            {
                row2 = _files[i];
            }
            else
            {
                row2 = CreateFileRow(thread);
                _files.Add(row2);
                var at = parts.Children.IndexOf(_bubble);
                parts.Children.Insert(at < 0 ? parts.Children.Count : at, row2);
            }
            var file = files[i];
            var grid = (Grid)row2.Content!;
            var texts = (StackPanel)grid.Children[1];
            ((TextBlock)texts.Children[0]).Text = file.Name;
            var size = (TextBlock)texts.Children[1];
            size.IsVisible = file.Size is not null;
            size.Text = file.Size is { } bytes ? ActivityTime.FormatBytes(bytes) : null;
            row2.Tag = file;
            AutomationProperties.SetName(row2, file.Size is { } b ? $"{file.Name}, {ActivityTime.FormatBytes(b)}" : file.Name);
        }
    }

    private ChatThreadButton CreateTile()
    {
        var image = new AsyncImage { Stretch = Stretch.UniformToFill };
        var more = new ChatThreadMore
        {
            IsVisible = false,
            Child = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        var tile = new ChatThreadButton { Role = ChatThreadButtonRole.Image, Content = new Panel { Children = { image, more } } };
        tile.Click += OnAttachmentClick;
        return tile;
    }

    private ChatThreadButton CreateFileRow(ChatThread thread)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = thread.FileGap };
        var icon = new ArcGlyph { Data = ArcIcons.FileText, Size = 18, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
        var name = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(name, TextBlock.FontWeightProperty, DesignTokens.ArcDesignTokenKind.WeightMedium);
        var size = new TextBlock();
        ArcTokens.Bind(size, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(size, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, size } };
        Grid.SetColumn(texts, 1);
        grid.Children.Add(icon);
        grid.Children.Add(texts);
        var button = new ChatThreadButton { Role = ChatThreadButtonRole.File, Content = grid };
        button.Click += OnAttachmentClick;
        return button;
    }

    private void UpdateReactions(ChatThread thread, ChatThreadMessageRow row, bool animate)
    {
        if (_reactions is null || _reactionsHost is null)
        {
            return;
        }
        var labels = thread.Labels;
        var list = row.Message.Reactions;
        var items = list is null ? Array.Empty<ActivityReaction>() : list.Select(r => new ActivityReaction(r.Emoji, r.Count, r.Mine)).ToArray();
        _reactions.NameFormatter = r => labels.ReactionCount(r.Emoji, r.Count, r.Mine);
        _reactions.IsRightAligned = row.Mine;
        _reactions.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        AutomationProperties.SetName(_reactions, labels.Reactions);
        var present = items.Length > 0;
        var animateBar = animate && _hasShownReactions && _reactionsHost.IsPresent;
        _reactions.SetReactions(items, animateBar || (animate && present && !_reactionsHost.IsPresent));
        if (animate)
        {
            _reactionsHost.IsPresent = present;
        }
        else
        {
            _reactionsHost.SetCurrentValue(PresenceHost.IsPresentProperty, present);
        }
        _hasShownReactions = true;
        if (_reactionsClip is not null)
        {
            _reactionsClip.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
    }

    private void UpdateReceipts(ChatThread thread, ChatThreadMessageRow row)
    {
        var labels = thread.Labels;
        var showStatus = row.ShowsStatus;
        var readers = row.Readers;
        if (_receipts is not null)
        {
            _receipts.IsVisible = showStatus || readers.Count > 0;
            _receipts.HorizontalAlignment = row.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        if (_status is not null)
        {
            _status.IsVisible = showStatus;
            _status.Classes.Set("failed", row.Status == ChatMessageStatus.Failed);
        }
        if (_statusText is not null)
        {
            _statusText.Text = row.Status switch
            {
                ChatMessageStatus.Sending => labels.Sending,
                ChatMessageStatus.Delivered => labels.Delivered,
                ChatMessageStatus.Read => labels.Read,
                ChatMessageStatus.Failed => labels.Failed,
                _ => labels.Sent
            };
        }
        if (_retry is not null)
        {
            _retry.IsVisible = showStatus && row.Status == ChatMessageStatus.Failed && thread.HasRetryHandler;
            if (_retry.Content is StackPanel { Children: [_, TextBlock label] })
            {
                label.Text = labels.Retry;
            }
            AutomationProperties.SetName(_retry, labels.Retry);
        }
        if (_readers is null)
        {
            return;
        }
        _readers.IsVisible = readers.Count > 0;
        while (_readerPool.Count < readers.Count)
        {
            var avatar = ActivityAvatar.Create(ReaderSize);
            avatar.Classes.Add("reader");
            _readerPool.Add(avatar);
        }
        while (_readers.Children.Count > readers.Count)
        {
            _readers.Children.RemoveAt(_readers.Children.Count - 1);
        }
        for (var i = 0; i < readers.Count; i++)
        {
            var avatar = _readerPool[i];
            ActivityAvatar.Update(avatar, readers[i].Name, readers[i].Avatar);
            avatar.Tag = readers[i].Id;
            avatar.Margin = new Thickness(i == 0 ? 0 : ReaderOverlap, 0, 0, 0);
            if (avatar.Parent is null)
            {
                _readers.Children.Add(avatar);
            }
        }
        AutomationProperties.SetName(_readers, readers.Count > 0 ? labels.ReadBy(readers.Select(r => r.Name).ToArray()) : string.Empty);
    }

    /// <summary>The reader portrait of <paramref name="participantId"/> under this message, if any.</summary>
    internal Avatar? ReaderAvatar(string participantId) =>
        _readers?.Children.OfType<Avatar>().FirstOrDefault(a => Equals(a.Tag, participantId));

    private void OnReactClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Row is { } row)
        {
            _thread?.TogglePicker(this, row.Message);
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Row is { } row)
        {
            _thread?.RequestRetry(row.Message);
        }
    }

    private void OnReactionToggled(object? sender, ActivityReactionToggledEventArgs e)
    {
        if (Row is { } row)
        {
            _thread?.React(row.Message, e.Emoji);
        }
    }

    private void OnAttachmentClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Control { Tag: ChatAttachment attachment })
        {
            _thread?.OpenAttachment(attachment);
        }
    }
}

/// <summary>The "+N" scrim over the fourth picture (Arc <c>.more</c>: 45% black, white large text).</summary>
internal sealed class ChatThreadMore : Border
{
    protected override Type StyleKeyOverride => typeof(ChatThreadMore);
}
