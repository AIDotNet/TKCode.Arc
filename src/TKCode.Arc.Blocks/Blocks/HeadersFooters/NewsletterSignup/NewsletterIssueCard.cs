// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): IssueCard and the issue styles of newsletter-signup.module.css.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A story's thumbnail: a muted rounded square with the picture drawn over it, cropped to fill (CSS
/// <c>object-fit: cover</c>). Decorative: no input, hidden from screen readers.
/// </summary>
internal sealed class NewsletterThumb : Control
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<NewsletterThumb, IImage?>(nameof(Source));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<NewsletterThumb>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<NewsletterThumb>();

    public static readonly StyledProperty<double> EdgeProperty =
        AvaloniaProperty.Register<NewsletterThumb, double>(nameof(Edge), 44);

    static NewsletterThumb()
    {
        AffectsMeasure<NewsletterThumb>(EdgeProperty);
        AffectsRender<NewsletterThumb>(SourceProperty, BackgroundProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<NewsletterThumb>(false);
        FocusableProperty.OverrideDefaultValue<NewsletterThumb>(false);
    }

    public NewsletterThumb()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public double Edge { get => GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    protected override Size MeasureOverride(Size availableSize) => new(Edge, Edge);

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var radius = CornerRadius.TopLeft;
        context.DrawRectangle(Background, null, rect, radius, radius);
        if (Source is { } image)
        {
            using (context.PushClip(new RoundedRect(rect, radius)))
            {
                SiteFaceStack.DrawCover(context, image, rect);
            }
        }
    }
}

/// <summary>One story row: the thumbnail, the title (two lines at most) and the reading time.</summary>
[TemplatePart(ThumbPart, typeof(NewsletterThumb))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(MinutesPart, typeof(TextBlock))]
[PseudoClasses(TinyPseudoClass)]
internal sealed class NewsletterStoryRow : TemplatedControl
{
    public const string ThumbPart = "PART_Thumb";
    public const string TitlePart = "PART_Title";
    public const string MinutesPart = "PART_Minutes";
    public const string TinyPseudoClass = ":tiny";

    public static readonly StyledProperty<NewsletterStory?> StoryProperty =
        AvaloniaProperty.Register<NewsletterStoryRow, NewsletterStory?>(nameof(Story));

    public static readonly StyledProperty<string> MinutesFormatProperty =
        AvaloniaProperty.Register<NewsletterStoryRow, string>(nameof(MinutesFormat), "{0} min");

    public static readonly StyledProperty<bool> IsTinyProperty =
        AvaloniaProperty.Register<NewsletterStoryRow, bool>(nameof(IsTiny));

    private NewsletterThumb? _thumb;
    private TextBlock? _title;
    private TextBlock? _minutes;

    static NewsletterStoryRow()
    {
        FocusableProperty.OverrideDefaultValue<NewsletterStoryRow>(false);
    }

    public NewsletterStory? Story { get => GetValue(StoryProperty); set => SetValue(StoryProperty, value); }

    public string MinutesFormat { get => GetValue(MinutesFormatProperty); set => SetValue(MinutesFormatProperty, value); }

    public bool IsTiny { get => GetValue(IsTinyProperty); set => SetValue(IsTinyProperty, value); }

    /// <summary>The title text on screen (tests read it).</summary>
    public string? TitleText => _title?.Text;

    /// <summary>The reading time text on screen (tests read it).</summary>
    public string? MinutesText => _minutes?.Text;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _thumb = e.NameScope.Find<NewsletterThumb>(ThumbPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _minutes = e.NameScope.Find<TextBlock>(MinutesPart);
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StoryProperty || change.Property == MinutesFormatProperty)
        {
            Sync();
        }
        else if (change.Property == IsTinyProperty)
        {
            PseudoClasses.Set(TinyPseudoClass, IsTiny);
        }
    }

    private void Sync()
    {
        var story = Story;
        if (_thumb is not null)
        {
            _thumb.Source = story?.Image;
        }
        if (_title is not null)
        {
            _title.Text = story?.Title;
        }
        if (_minutes is not null)
        {
            _minutes.Text = story is null ? null : string.Format(CultureInfo.CurrentCulture, MinutesFormat, story.Minutes);
        }
    }
}

/// <summary>
/// One issue of the newsletter: the masthead and "Issue N", the date line (with "To address" once the issue is addressed to
/// a reader), the subject and three stories. <see cref="Depth"/> sets the paper: the front issue is raised, the ones
/// behind it fade to the muted surface (480 ms). The stack places the card; the card only draws.
/// </summary>
[TemplatePart(RecipientPart, typeof(TextBlock))]
[TemplatePart(StoriesPart, typeof(StackPanel))]
[PseudoClasses(Depth1PseudoClass, Depth2PseudoClass, SizerPseudoClass, TrayPseudoClass, NarrowPseudoClass, TinyPseudoClass, AddressedPseudoClass)]
internal sealed class NewsletterIssueCard : TemplatedControl
{
    public const string RecipientPart = "PART_Recipient";
    public const string StoriesPart = "PART_Stories";
    public const string Depth1PseudoClass = ":depth1";
    public const string Depth2PseudoClass = ":depth2";
    public const string SizerPseudoClass = ":sizer";
    public const string TrayPseudoClass = ":tray";
    public const string NarrowPseudoClass = ":narrow";
    public const string TinyPseudoClass = ":tiny";
    public const string AddressedPseudoClass = ":addressed";

    public static readonly StyledProperty<NewsletterIssue?> IssueProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, NewsletterIssue?>(nameof(Issue));

    public static readonly StyledProperty<string?> PublicationNameProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, string?>(nameof(PublicationName));

    /// <summary>The reader the issue is addressed to; null leaves the line with the date alone; a space reserves the line.</summary>
    public static readonly StyledProperty<string?> RecipientProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, string?>(nameof(Recipient));

    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, int>(nameof(Depth));

    /// <summary>The unseen copy that sizes the stack: no shadow.</summary>
    public static readonly StyledProperty<bool> IsSizerProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, bool>(nameof(IsSizer));

    public static readonly StyledProperty<bool> IsTrayProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, bool>(nameof(IsTray));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<bool> IsTinyProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, bool>(nameof(IsTiny));

    public static readonly StyledProperty<string> IssueFormatProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, string>(nameof(IssueFormat), "Issue {0}");

    public static readonly StyledProperty<string> MinutesFormatProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, string>(nameof(MinutesFormat), "{0} min");

    public static readonly StyledProperty<string> RecipientFormatProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, string>(nameof(RecipientFormat), "To {0}");

    public static readonly StyledProperty<BoxShadows> PaperShadowProperty =
        AvaloniaProperty.Register<NewsletterIssueCard, BoxShadows>(nameof(PaperShadow));

    internal static readonly DirectProperty<NewsletterIssueCard, string?> IssueLabelProperty =
        AvaloniaProperty.RegisterDirect<NewsletterIssueCard, string?>(nameof(IssueLabel), o => o.IssueLabel);

    internal static readonly DirectProperty<NewsletterIssueCard, bool> HasRecipientProperty =
        AvaloniaProperty.RegisterDirect<NewsletterIssueCard, bool>(nameof(HasRecipient), o => o.HasRecipient);

    internal static readonly DirectProperty<NewsletterIssueCard, string?> IssueDateProperty =
        AvaloniaProperty.RegisterDirect<NewsletterIssueCard, string?>(nameof(IssueDate), o => o.IssueDate);

    internal static readonly DirectProperty<NewsletterIssueCard, string?> IssueSubjectProperty =
        AvaloniaProperty.RegisterDirect<NewsletterIssueCard, string?>(nameof(IssueSubject), o => o.IssueSubject);

    private string? _issueDate;
    private string? _issueSubject;
    private TextBlock? _recipient;
    private StackPanel? _stories;
    private string? _issueLabel;
    private bool _hasRecipient;

    static NewsletterIssueCard()
    {
        FocusableProperty.OverrideDefaultValue<NewsletterIssueCard>(false);
    }

    public NewsletterIssue? Issue { get => GetValue(IssueProperty); set => SetValue(IssueProperty, value); }

    public string? PublicationName { get => GetValue(PublicationNameProperty); set => SetValue(PublicationNameProperty, value); }

    public string? Recipient { get => GetValue(RecipientProperty); set => SetValue(RecipientProperty, value); }

    public int Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }

    public bool IsSizer { get => GetValue(IsSizerProperty); set => SetValue(IsSizerProperty, value); }

    public bool IsTray { get => GetValue(IsTrayProperty); set => SetValue(IsTrayProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public bool IsTiny { get => GetValue(IsTinyProperty); set => SetValue(IsTinyProperty, value); }

    public string IssueFormat { get => GetValue(IssueFormatProperty); set => SetValue(IssueFormatProperty, value); }

    public string MinutesFormat { get => GetValue(MinutesFormatProperty); set => SetValue(MinutesFormatProperty, value); }

    public string RecipientFormat { get => GetValue(RecipientFormatProperty); set => SetValue(RecipientFormatProperty, value); }

    public BoxShadows PaperShadow { get => GetValue(PaperShadowProperty); set => SetValue(PaperShadowProperty, value); }

    /// <summary>The send date line.</summary>
    public string? IssueDate => _issueDate;

    /// <summary>The subject line.</summary>
    public string? IssueSubject => _issueSubject;

    /// <summary>"Issue 148".</summary>
    public string? IssueLabel => _issueLabel;

    /// <summary>True while the issue is addressed to a reader.</summary>
    public bool HasRecipient => _hasRecipient;

    /// <summary>The story rows on screen (tests read them).</summary>
    public IEnumerable<NewsletterStoryRow> Rows => _stories?.Children.OfType<NewsletterStoryRow>() ?? Enumerable.Empty<NewsletterStoryRow>();

    /// <summary>The text of the recipient line, for example "To ada@example.com" (tests read it).</summary>
    public string? RecipientText => _recipient?.Inlines is { } inlines ? string.Concat(inlines.OfType<Run>().Select(r => r.Text)) : null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _recipient = e.NameScope.Find<TextBlock>(RecipientPart);
        _stories = e.NameScope.Find<StackPanel>(StoriesPart);
        SyncRecipient();
        SyncStories();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IssueProperty || property == IssueFormatProperty)
        {
            var issue = Issue;
            SetAndRaise(IssueLabelProperty, ref _issueLabel, issue is null ? null : string.Format(CultureInfo.CurrentCulture, IssueFormat, issue.Number));
            SetAndRaise(IssueDateProperty, ref _issueDate, issue?.Date);
            SetAndRaise(IssueSubjectProperty, ref _issueSubject, issue?.Subject);
        }
        if (property == IssueProperty)
        {
            SyncStories();
        }
        else if (property == MinutesFormatProperty || property == IsTinyProperty)
        {
            SyncStories();
        }
        if (property == RecipientProperty || property == RecipientFormatProperty)
        {
            SyncRecipient();
        }
        if (property == DepthProperty)
        {
            PseudoClasses.Set(Depth1PseudoClass, Depth == 1);
            PseudoClasses.Set(Depth2PseudoClass, Depth >= 2);
        }
        else if (property == IsSizerProperty)
        {
            PseudoClasses.Set(SizerPseudoClass, IsSizer);
        }
        else if (property == IsTrayProperty)
        {
            PseudoClasses.Set(TrayPseudoClass, IsTray);
        }
        else if (property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        }
        else if (property == IsTinyProperty)
        {
            PseudoClasses.Set(TinyPseudoClass, IsTiny);
        }
    }

    private void SyncRecipient()
    {
        var address = Recipient;
        var has = !string.IsNullOrEmpty(address);
        SetAndRaise(HasRecipientProperty, ref _hasRecipient, has);
        PseudoClasses.Set(AddressedPseudoClass, has);
        if (_recipient is null)
        {
            return;
        }
        var inlines = _recipient.Inlines ??= new InlineCollection();
        inlines.Clear();
        if (!has)
        {
            return;
        }
        // "To" in the secondary color and the address in the foreground, split around the {0} of the format.
        var format = RecipientFormat;
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        var before = at < 0 ? format : format[..at];
        var after = at < 0 ? string.Empty : format[(at + 3)..];
        if (before.Length > 0)
        {
            inlines.Add(new Run(before));
        }
        var run = new Run(address);
        inlines.Add(run);
        ArcTokens.Bind(run, TextElement.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        if (after.Length > 0)
        {
            inlines.Add(new Run(after));
        }
    }

    private void SyncStories()
    {
        if (_stories is null)
        {
            return;
        }
        var stories = Issue?.Stories;
        var count = stories is null ? 0 : Math.Min(3, stories.Count);
        // The three rows are reused: a card never needs more.
        while (_stories.Children.Count > count)
        {
            _stories.Children.RemoveAt(_stories.Children.Count - 1);
        }
        for (var i = 0; i < count; i++)
        {
            NewsletterStoryRow row;
            if (i < _stories.Children.Count)
            {
                row = (NewsletterStoryRow)_stories.Children[i];
            }
            else
            {
                row = new NewsletterStoryRow();
                _stories.Children.Add(row);
            }
            row.Story = stories![i];
            row.MinutesFormat = MinutesFormat;
            row.IsTiny = IsTiny;
        }
    }
}
