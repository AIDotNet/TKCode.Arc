using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A public board of feature requests: filter by status (under review, planned, in progress, done), sort by top, newest
/// or trending, vote with a count that rolls, open a request to read it and join its comment thread, and suggest a new
/// one from the form under the header. Rows glide to their new places when the order changes.
/// <code>
/// &lt;arc:FeedbackBoard Title="Product feedback" SortOrder="Top"&gt;
///     &lt;arc:FeedbackRequest Id="r1" Title="Dark mode for the mobile app" Votes="214" Status="Planned" /&gt;
/// &lt;/arc:FeedbackBoard&gt;
/// </code>
/// Votes, comments and new requests change the <see cref="FeedbackRequest"/> objects in place and are reported through
/// <see cref="VoteChanged"/>, <see cref="RequestSubmitted"/> and <see cref="CommentsChanged"/>. A board with no requests
/// when it first loads shows a sample.
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(StackedPseudoClass)]
public class FeedbackBoard : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set while the board is narrower than its stacked width: the open request replaces the list.</summary>
    public const string StackedPseudoClass = ":stacked";

    #region Properties

    /// <summary>The heading ("Feedback").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FeedbackBoard, string>(nameof(Title), "Feedback");

    /// <summary>The line under the heading; null or empty shows none.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FeedbackBoard, string?>(nameof(Description), "Tell us what to build next. Vote on the ideas you want most.");

    /// <summary>The status shown (two-way); null shows every request.</summary>
    public static readonly StyledProperty<FeedbackStatus?> StatusFilterProperty =
        AvaloniaProperty.Register<FeedbackBoard, FeedbackStatus?>(nameof(StatusFilter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The order of the list (two-way, default <see cref="FeedbackSort.Top"/>).</summary>
    public static readonly StyledProperty<FeedbackSort> SortOrderProperty =
        AvaloniaProperty.Register<FeedbackBoard, FeedbackSort>(nameof(SortOrder), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The request open in the detail panel (two-way); null shows none.</summary>
    public static readonly StyledProperty<FeedbackRequest?> SelectedRequestProperty =
        AvaloniaProperty.Register<FeedbackBoard, FeedbackRequest?>(nameof(SelectedRequest), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The person voting and commenting; their comments can be edited.</summary>
    public static readonly StyledProperty<CommentAuthor> CurrentUserProperty =
        AvaloniaProperty.Register<FeedbackBoard, CommentAuthor>(nameof(CurrentUser), new CommentAuthor("me", "You"));

    /// <summary>Whether a board with no requests when it first loads gets the sample (default true).</summary>
    public static readonly StyledProperty<bool> UsesSampleDataProperty =
        AvaloniaProperty.Register<FeedbackBoard, bool>(nameof(UsesSampleData), true);

    /// <summary>Whether people can suggest requests from the form under the header (default true).</summary>
    public static readonly StyledProperty<bool> CanSubmitProperty =
        AvaloniaProperty.Register<FeedbackBoard, bool>(nameof(CanSubmit), true);

    /// <summary>Raised when the viewer adds or takes back a vote.</summary>
    public static readonly RoutedEvent<FeedbackRequestEventArgs> VoteChangedEvent =
        RoutedEvent.Register<FeedbackBoard, FeedbackRequestEventArgs>(nameof(VoteChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after the viewer suggested a request (it is already on the board, under review).</summary>
    public static readonly RoutedEvent<FeedbackRequestEventArgs> RequestSubmittedEvent =
        RoutedEvent.Register<FeedbackBoard, FeedbackRequestEventArgs>(nameof(RequestSubmitted), RoutingStrategies.Bubble);

    /// <summary>Raised when the open request's comments change, with the exact change.</summary>
    public static readonly RoutedEvent<CommentsChangedEventArgs> CommentsChangedEvent =
        RoutedEvent.Register<FeedbackBoard, CommentsChangedEventArgs>(nameof(CommentsChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="StatusFilterProperty"/>
    public FeedbackStatus? StatusFilter { get => GetValue(StatusFilterProperty); set => SetValue(StatusFilterProperty, value); }

    /// <inheritdoc cref="SortOrderProperty"/>
    public FeedbackSort SortOrder { get => GetValue(SortOrderProperty); set => SetValue(SortOrderProperty, value); }

    /// <inheritdoc cref="SelectedRequestProperty"/>
    public FeedbackRequest? SelectedRequest { get => GetValue(SelectedRequestProperty); set => SetValue(SelectedRequestProperty, value); }

    /// <inheritdoc cref="CurrentUserProperty"/>
    public CommentAuthor CurrentUser { get => GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }

    /// <inheritdoc cref="UsesSampleDataProperty"/>
    public bool UsesSampleData { get => GetValue(UsesSampleDataProperty); set => SetValue(UsesSampleDataProperty, value); }

    /// <inheritdoc cref="CanSubmitProperty"/>
    public bool CanSubmit { get => GetValue(CanSubmitProperty); set => SetValue(CanSubmitProperty, value); }

    /// <summary>The requests, in any order (the board sorts them).</summary>
    [Content]
    public AvaloniaList<FeedbackRequest> Requests { get; } = new();

    /// <inheritdoc cref="VoteChangedEvent"/>
    public event EventHandler<FeedbackRequestEventArgs>? VoteChanged
    {
        add => AddHandler(VoteChangedEvent, value);
        remove => RemoveHandler(VoteChangedEvent, value);
    }

    /// <inheritdoc cref="RequestSubmittedEvent"/>
    public event EventHandler<FeedbackRequestEventArgs>? RequestSubmitted
    {
        add => AddHandler(RequestSubmittedEvent, value);
        remove => RemoveHandler(RequestSubmittedEvent, value);
    }

    /// <inheritdoc cref="CommentsChangedEvent"/>
    public event EventHandler<CommentsChangedEventArgs>? CommentsChanged
    {
        add => AddHandler(CommentsChangedEvent, value);
        remove => RemoveHandler(CommentsChangedEvent, value);
    }

    #endregion

    internal static readonly StyledProperty<double> StackedWidthProperty =
        AvaloniaProperty.Register<FeedbackBoard, double>(nameof(StackedWidth), 760);

    internal static readonly StyledProperty<double> DetailWidthProperty =
        AvaloniaProperty.Register<FeedbackBoard, double>(nameof(DetailWidth), 380);

    internal double StackedWidth { get => GetValue(StackedWidthProperty); set => SetValue(StackedWidthProperty, value); }

    internal double DetailWidth { get => GetValue(DetailWidthProperty); set => SetValue(DetailWidthProperty, value); }

    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static readonly (FeedbackStatus? Status, string Label)[] s_filters =
    [
        (null, "All"), (FeedbackStatus.UnderReview, "Under review"), (FeedbackStatus.Planned, "Planned"),
        (FeedbackStatus.InProgress, "In progress"), (FeedbackStatus.Done, "Done"),
    ];

    private readonly Grid _root;
    private readonly TextBlock _titleText;
    private readonly TextBlock _descriptionText;
    private readonly Button _suggest;
    private readonly PresenceHost _formHost;
    private readonly Input _formTitle;
    private readonly Textarea _formDescription;
    private readonly List<(SupportKitChip Chip, FeedbackStatus? Status, string Label)> _chips = new();
    private readonly SegmentedControl _sort;
    private readonly SupportKitFlipStack _list;
    private readonly ScrollViewer _listScroller;
    private readonly TextBlock _emptyText;
    private readonly Grid _body;
    private readonly Border _detailFrame;
    private readonly FeedbackBoardDetail _detail;
    private readonly SupportKitAnnouncer _announcer = new();
    private readonly Dictionary<FeedbackRequest, FeedbackBoardRow> _rows = new();
    private Decorator? _host;
    private bool _loaded;
    private bool _stacked;

    /// <summary>Creates the board.</summary>
    public FeedbackBoard()
    {
        BlockFocusModality.Watch();
        _titleText = SupportKit.Heading(Title, ArcDesignTokenKind.TypeXL);
        _descriptionText = SupportKit.Text(Description, ArcDesignTokenKind.TextSecondary, wrap: true);
        _suggest = SupportKit.Button("Suggest a feature", ButtonVariant.Primary, ArcIcons.Plus, ControlSize.Medium);
        _suggest.Click += (_, _) => ToggleForm();
        _suggest.VerticalAlignment = VerticalAlignment.Top;
        var heading = new StackPanel { Spacing = 4, Children = { _titleText, _descriptionText } };
        var header = SupportKit.Grid("*,Auto", columnSpacing: 16);
        header.Children.Add(heading.At(0));
        header.Children.Add(_suggest.At(1));

        _formTitle = new Input { Label = "Title", Placeholder = "Short and specific, like “Export boards to PDF”" };
        _formDescription = new Textarea { Label = "Details", Placeholder = "What are you trying to do, and what gets in the way?", FrameHeight = 88 };
        var post = SupportKit.Button("Post request", ButtonVariant.Primary);
        post.Click += (_, _) => SubmitForm();
        var cancel = SupportKit.Button("Cancel", ButtonVariant.Ghost);
        cancel.Click += (_, _) => CloseForm();
        var formButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, post } };
        var form = SupportKit.Panel(new StackPanel { Spacing = 12, Children = { _formTitle, _formDescription, formButtons } }, fill: ArcDesignTokenKind.SurfaceMuted, edge: false);
        form.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseForm();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
            {
                SubmitForm();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        _formHost = new PresenceHost { Mode = PresenceMode.Fade, Collapse = PresenceCollapse.Height, Child = form, IsPresent = false };

        var chips = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (status, label) in s_filters)
        {
            var chip = new SupportKitChip { Content = label, Item = status };
            chip.Click += (_, _) => SetCurrentValue(StatusFilterProperty, status);
            _chips.Add((chip, status, label));
            chips.Children.Add(chip);
        }
        AutomationProperties.SetName(chips, "Status");
        _sort = new SegmentedControl
        {
            ItemsSource = new[]
            {
                new SegmentedOption(FeedbackSort.Top, "Top"),
                new SegmentedOption(FeedbackSort.Newest, "Newest"),
                new SegmentedOption(FeedbackSort.Trending, "Trending"),
            },
            SelectedIndex = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_sort, "Sort");
        _sort.SelectionChanged += (_, _) =>
        {
            if (_sort.SelectedItem is SegmentedOption { Value: FeedbackSort sort })
            {
                SetCurrentValue(SortOrderProperty, sort);
            }
        };
        var toolbar = new WrapPanel { ItemSpacing = 16, LineSpacing = 10, Children = { chips, _sort } };

        _list = new SupportKitFlipStack { Spacing = 2 };
        AutomationProperties.SetName(_list, "Requests");
        _emptyText = SupportKit.Text("No requests here yet.", ArcDesignTokenKind.TextMuted, wrap: true);
        _emptyText.Margin = new Thickness(12, 24);
        _emptyText.HorizontalAlignment = HorizontalAlignment.Center;
        _listScroller = new ScrollViewer
        {
            Content = new StackPanel { Children = { _list, _emptyText } },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        _detail = new FeedbackBoardDetail(this);
        _detailFrame = SupportKit.Panel(_detail, ArcDesignTokenKind.RadiusStep20, ArcDesignTokenKind.Surface, new Thickness(18));
        _detailFrame.VerticalAlignment = VerticalAlignment.Top;
        _detailFrame.IsVisible = false;

        _body = SupportKit.Grid("*,Auto", columnSpacing: 20);
        _body.Children.Add(_listScroller.At(0));
        _body.Children.Add(_detailFrame.At(1));

        _root = SupportKit.Grid("*", "Auto,Auto,Auto,*", rowSpacing: 16);
        _root.Children.Add(header.At(0, 0));
        _root.Children.Add(_formHost.At(0, 1));
        _root.Children.Add(toolbar.At(0, 2));
        _root.Children.Add(_body.At(0, 3));
        _root.Children.Add(_announcer.At(0, 3));

        Requests.CollectionChanged += OnRequestsChanged;
        Rebuild();
    }

    #region Internals for tests

    internal IReadOnlyList<FeedbackBoardRow> VisibleRows =>
        _list.Children.OfType<SupportKitRow>().Select(r => (FeedbackBoardRow)r.Item!).ToList();

    internal FeedbackBoardDetail Detail => _detail;

    internal bool IsFormOpen => _formHost.IsPresent;

    internal Input FormTitle => _formTitle;

    internal Textarea FormDescription => _formDescription;

    internal bool IsStacked => _stacked;

    internal bool IsDetailShown => _detailFrame.IsVisible;

    internal string? LastAnnouncement => _announcer.Announcement;

    internal SupportKitChip ChipFor(FeedbackStatus? status) => _chips.First(c => c.Status == status).Chip;

    #endregion

    #region Public actions

    /// <summary>Adds the viewer's vote to <paramref name="request"/>, or takes it back when they already voted.</summary>
    public void ToggleVote(FeedbackRequest request)
    {
        request.HasVoted = !request.HasVoted;
        request.Votes += request.HasVoted ? 1 : -1;
        _announcer.Announce($"{(request.HasVoted ? "Voted for" : "Removed your vote for")} {request.Title}. {SupportKit.Count(request.Votes, "vote", "votes")}.");
        RaiseEvent(new FeedbackRequestEventArgs(VoteChangedEvent, request));
    }

    /// <summary>Adds a request under review with the viewer's vote on it, opens it and raises <see cref="RequestSubmitted"/>.</summary>
    /// <returns>The new request, or null when <paramref name="title"/> is empty.</returns>
    public FeedbackRequest? Submit(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }
        var request = new FeedbackRequest($"new-{Guid.NewGuid():N}", title.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            FeedbackStatus.UnderReview, 1, Clock())
        {
            HasVoted = true,
            Author = CurrentUser.Name,
        };
        Requests.Add(request);
        if (StatusFilter is { } filter && filter != FeedbackStatus.UnderReview)
        {
            SetCurrentValue(StatusFilterProperty, null);
        }
        SetCurrentValue(SelectedRequestProperty, request);
        _announcer.Announce($"Posted {request.Title}. It is under review.");
        RaiseEvent(new FeedbackRequestEventArgs(RequestSubmittedEvent, request));
        return request;
    }

    #endregion

    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    #region Template, layout and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Decorator>(HostPart);
        SupportKit.Mount(_host, _root);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_loaded)
        {
            _loaded = true;
            if (UsesSampleData && Requests.Count == 0)
            {
                Requests.AddRange(FeedbackBoardSamples.Create(Clock()));
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var stacked = e.NewSize.Width < StackedWidth;
        if (stacked != _stacked)
        {
            _stacked = stacked;
            PseudoClasses.Set(StackedPseudoClass, stacked);
            UpdateDetailLayout();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TitleProperty)
        {
            _titleText.Text = Title;
        }
        else if (property == DescriptionProperty)
        {
            _descriptionText.Text = Description;
            _descriptionText.IsVisible = !string.IsNullOrEmpty(Description);
        }
        else if (property == StatusFilterProperty || property == SortOrderProperty)
        {
            if (property == SortOrderProperty)
            {
                _sort.SelectedIndex = (int)SortOrder;
            }
            Rebuild();
        }
        else if (property == SelectedRequestProperty)
        {
            OnSelectedChanged(change.GetOldValue<FeedbackRequest?>(), change.GetNewValue<FeedbackRequest?>());
        }
        else if (property == CurrentUserProperty)
        {
            _detail.Thread.CurrentUser = CurrentUser;
        }
        else if (property == CanSubmitProperty)
        {
            _suggest.IsVisible = CanSubmit;
            if (!CanSubmit)
            {
                CloseForm();
            }
        }
        else if (property == DetailWidthProperty)
        {
            UpdateDetailLayout();
        }
    }

    private void OnSelectedChanged(FeedbackRequest? old, FeedbackRequest? request)
    {
        foreach (var row in _rows.Values)
        {
            row.Row.IsChecked = ReferenceEquals(row.Request, request);
        }
        _detail.Show(request);
        UpdateDetailLayout();
        if (request is null && old is not null && _rows.TryGetValue(old, out var back) && IsKeyboardFocusWithin)
        {
            back.Row.Focus(BlockFocusModality.For(this));
        }
    }

    private void UpdateDetailLayout()
    {
        var open = SelectedRequest is not null;
        _detailFrame.IsVisible = open;
        _detail.IsBackVisible = _stacked;
        if (_stacked)
        {
            // One column: the open request takes the list's place.
            Grid.SetColumn(_detailFrame, 0);
            _listScroller.IsVisible = !open;
            _detailFrame.Width = double.NaN;
            _body.ColumnDefinitions[1].Width = new GridLength(0);
            _body.ColumnSpacing = 0;
        }
        else
        {
            Grid.SetColumn(_detailFrame, 1);
            _listScroller.IsVisible = true;
            _detailFrame.Width = DetailWidth;
            _body.ColumnDefinitions[1].Width = GridLength.Auto;
            _body.ColumnSpacing = open ? 20 : 0;
        }
    }

    #endregion

    #region List

    private void OnRequestsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (FeedbackRequest gone in e.OldItems)
            {
                gone.PropertyChanged -= OnRequestChanged;
                _rows.Remove(gone);
                if (ReferenceEquals(gone, SelectedRequest))
                {
                    SetCurrentValue(SelectedRequestProperty, null);
                }
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var request in _rows.Keys.Where(r => !Requests.Contains(r)).ToList())
            {
                request.PropertyChanged -= OnRequestChanged;
                _rows.Remove(request);
            }
        }
        Rebuild();
    }

    private void OnRequestChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FeedbackRequest request)
        {
            return;
        }
        if (_rows.TryGetValue(request, out var row))
        {
            row.Update();
        }
        if (ReferenceEquals(request, SelectedRequest))
        {
            _detail.Update();
        }
        // A vote or a status change can move the row or take it out of the filter.
        if (e.PropertyName is nameof(FeedbackRequest.Votes) or nameof(FeedbackRequest.Status))
        {
            Rebuild();
        }
    }

    /// <summary>Filters, sorts and lays out the rows; rows that stay glide to their new places.</summary>
    private void Rebuild()
    {
        foreach (var request in Requests)
        {
            if (!_rows.ContainsKey(request))
            {
                request.PropertyChanged += OnRequestChanged;
                _rows[request] = new FeedbackBoardRow(this, request);
            }
        }
        var now = Clock();
        IEnumerable<FeedbackRequest> shown = Requests.Where(r => StatusFilter is not { } s || r.Status == s);
        shown = SortOrder switch
        {
            FeedbackSort.Newest => shown.OrderByDescending(r => r.CreatedAt),
            FeedbackSort.Trending => shown.OrderByDescending(r => Trend(r, now)).ThenByDescending(r => r.Votes),
            _ => shown.OrderByDescending(r => r.Votes).ThenByDescending(r => r.CreatedAt),
        };
        var ordered = shown.Select(r => _rows[r].Row).ToList();
        if (!ordered.SequenceEqual(_list.Children.OfType<SupportKitRow>()))
        {
            // Keep rows that stay in the panel so they glide; only add and remove what changed.
            var keep = new HashSet<Control>(ordered);
            for (var i = _list.Children.Count - 1; i >= 0; i--)
            {
                if (!keep.Contains(_list.Children[i]))
                {
                    _list.Children.RemoveAt(i);
                }
            }
            for (var i = 0; i < ordered.Count; i++)
            {
                var at = _list.Children.IndexOf(ordered[i]);
                if (at == i)
                {
                    continue;
                }
                if (at >= 0)
                {
                    _list.Children.Move(at, i);
                }
                else
                {
                    _list.Children.Insert(i, ordered[i]);
                }
            }
        }
        _emptyText.IsVisible = ordered.Count == 0;
        foreach (var (chip, status, label) in _chips)
        {
            chip.IsChecked = status == StatusFilter;
            var count = status is { } s ? Requests.Count(r => r.Status == s) : Requests.Count;
            chip.Content = $"{label} {count}";
            AutomationProperties.SetName(chip, $"{label}, {SupportKit.Count(count, "request", "requests")}");
        }
    }

    /// <summary>Votes per day of age, softened so a day-old request with a few votes does not outrank everything.</summary>
    internal static double Trend(FeedbackRequest request, DateTimeOffset now)
    {
        var days = Math.Max(0, (now - request.CreatedAt).TotalDays);
        return request.Votes / Math.Pow(days + 2, 1.4);
    }

    #endregion

    #region Form

    private void ToggleForm()
    {
        if (_formHost.IsPresent)
        {
            CloseForm();
        }
        else
        {
            _formHost.IsPresent = true;
            _formTitle.Focus(BlockFocusModality.For(this));
        }
    }

    private void CloseForm()
    {
        if (!_formHost.IsPresent)
        {
            return;
        }
        var within = _formHost.IsKeyboardFocusWithin;
        _formHost.IsPresent = false;
        _formTitle.Text = string.Empty;
        _formDescription.Text = string.Empty;
        _formTitle.ErrorMessage = null;
        if (within)
        {
            _suggest.Focus(BlockFocusModality.For(this));
        }
    }

    private void SubmitForm()
    {
        if (string.IsNullOrWhiteSpace(_formTitle.Text))
        {
            _formTitle.ErrorMessage = "Give the request a title.";
            _formTitle.Focus(NavigationMethod.Tab);
            return;
        }
        Submit(_formTitle.Text, _formDescription.Text);
        CloseForm();
    }

    #endregion

    internal void RaiseCommentsChanged(CommentsChangedEventArgs e) =>
        RaiseEvent(new CommentsChangedEventArgs(CommentsChangedEvent, e.Comments, e.Change));
}
