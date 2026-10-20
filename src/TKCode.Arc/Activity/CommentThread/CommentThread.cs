using System.Text.RegularExpressions;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A threaded discussion (Arc's Comment thread): replies nest under their comment and collapse, reactions toggle with a
/// count that rolls, authors edit and delete their own comments in place, mentions autocomplete in the composer, and
/// resolving folds the whole thread into a single chip that reopens it. Use it for review discussions on documents,
/// designs, tickets or pull requests; use <see cref="MentionInput"/> when only the input is needed.
/// </summary>
/// <remarks>
/// <see cref="Comments"/> and <see cref="IsResolved"/> bind two way: every change produces a new immutable tree that the
/// thread shows at once (uncontrolled) and reports through <see cref="CommentsChanged"/> with the exact change, so an app
/// can persist it without diffing trees. To keep a change from showing, set <see cref="Comments"/> back in the handler.
/// </remarks>
[TemplatePart(RootPart, typeof(Border))]
[TemplatePart(AutoHeightPart, typeof(Decorator))]
[PseudoClasses(ResolvedPseudoClass, NarrowPseudoClass)]
public class CommentThread : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string AutoHeightPart = "PART_AutoHeight";
    public const string ResolvedPseudoClass = ":resolved";
    public const string NarrowPseudoClass = ":narrow";

    private static readonly IReadOnlyList<string> s_defaultReactions = ["👍", "❤️", "🎉", "👀", "🚀", "✅"];
    private static readonly CommentAuthor s_anonymous = new("me", "You");

    #region Properties

    /// <summary>The comments, oldest first (two way). Each change replaces it with the next tree.</summary>
    public static readonly StyledProperty<IReadOnlyList<ThreadComment>> CommentsProperty =
        AvaloniaProperty.Register<CommentThread, IReadOnlyList<ThreadComment>>(nameof(Comments), Array.Empty<ThreadComment>(),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The person writing. Their comments can be edited and deleted.</summary>
    public static readonly StyledProperty<CommentAuthor> CurrentUserProperty =
        AvaloniaProperty.Register<CommentThread, CommentAuthor>(nameof(CurrentUser), s_anonymous);

    /// <summary>People who can be mentioned. Null (default) offers everyone in the thread and the current user.</summary>
    public static readonly StyledProperty<IReadOnlyList<CommentAuthor>?> PeopleProperty =
        AvaloniaProperty.Register<CommentThread, IReadOnlyList<CommentAuthor>?>(nameof(People));

    /// <summary>Whether the thread is folded into the resolved chip (two way).</summary>
    public static readonly StyledProperty<bool> IsResolvedProperty =
        AvaloniaProperty.Register<CommentThread, bool>(nameof(IsResolved), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What the thread is about, shown in its header and on the resolved chip ("Hero headline").</summary>
    public static readonly StyledProperty<object?> TitleProperty =
        AvaloniaProperty.Register<CommentThread, object?>(nameof(Title));

    /// <summary>Placeholder (and accessible name) of the composer.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<CommentThread, string>(nameof(Placeholder), "Reply, or @mention someone");

    /// <summary>Emoji offered by the reaction picker.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ReactionsProperty =
        AvaloniaProperty.Register<CommentThread, IReadOnlyList<string>>(nameof(Reactions), s_defaultReactions);

    /// <summary>Replies deeper than this attach to the deepest allowed parent (default 2).</summary>
    public static readonly StyledProperty<int> MaxDepthProperty =
        AvaloniaProperty.Register<CommentThread, int>(nameof(MaxDepth), 2, coerce: (_, value) => Math.Max(1, value));

    /// <summary>The time label of comments written now.</summary>
    public static readonly StyledProperty<string> NowLabelProperty =
        AvaloniaProperty.Register<CommentThread, string>(nameof(NowLabel), "Just now");

    /// <summary>Every word the thread shows or announces; English by default.</summary>
    public static readonly StyledProperty<CommentThreadLabels> LabelsProperty =
        AvaloniaProperty.Register<CommentThread, CommentThreadLabels>(nameof(Labels), CommentThreadLabels.Default);

    /// <summary>Raised after every change with the next tree and the change.</summary>
    public static readonly RoutedEvent<CommentsChangedEventArgs> CommentsChangedEvent =
        RoutedEvent.Register<CommentThread, CommentsChangedEventArgs>(nameof(CommentsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the thread is resolved or reopened.</summary>
    public static readonly RoutedEvent<CommentThreadResolvedChangedEventArgs> ResolvedChangedEvent =
        RoutedEvent.Register<CommentThread, CommentThreadResolvedChangedEventArgs>(nameof(ResolvedChanged), RoutingStrategies.Bubble);

    /// <summary>The comments, oldest first (two way).</summary>
    public IReadOnlyList<ThreadComment> Comments { get => GetValue(CommentsProperty); set => SetValue(CommentsProperty, value); }

    /// <summary>The person writing.</summary>
    public CommentAuthor CurrentUser { get => GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }

    /// <summary>People who can be mentioned (null: everyone in the thread and the current user).</summary>
    public IReadOnlyList<CommentAuthor>? People { get => GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }

    /// <summary>Whether the thread is resolved (two way).</summary>
    public bool IsResolved { get => GetValue(IsResolvedProperty); set => SetValue(IsResolvedProperty, value); }

    /// <summary>What the thread is about. A string shows in the header and on the resolved chip; a control only in the header.</summary>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Placeholder of the composer.</summary>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>Emoji offered by the reaction picker.</summary>
    public IReadOnlyList<string> Reactions { get => GetValue(ReactionsProperty); set => SetValue(ReactionsProperty, value); }

    /// <summary>Deepest reply level (default 2).</summary>
    public int MaxDepth { get => GetValue(MaxDepthProperty); set => SetValue(MaxDepthProperty, value); }

    /// <summary>The time label of comments written now ("Just now").</summary>
    public string NowLabel { get => GetValue(NowLabelProperty); set => SetValue(NowLabelProperty, value); }

    /// <summary>Every word the thread shows or announces.</summary>
    public CommentThreadLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Raised after every change with the next tree and the change.</summary>
    public event EventHandler<CommentsChangedEventArgs>? CommentsChanged
    {
        add => AddHandler(CommentsChangedEvent, value);
        remove => RemoveHandler(CommentsChangedEvent, value);
    }

    /// <summary>Raised when the thread is resolved or reopened.</summary>
    public event EventHandler<CommentThreadResolvedChangedEventArgs>? ResolvedChanged
    {
        add => AddHandler(ResolvedChangedEvent, value);
        remove => RemoveHandler(ResolvedChangedEvent, value);
    }

    #endregion

    static CommentThread()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<CommentThread>(AutomationLandmarkType.Region);
        FocusableProperty.OverrideDefaultValue<CommentThread>(false);
    }

    // UI state (Arc's ui / replyTo).
    private string? _editing;
    private string? _confirming;
    private string? _picker;
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private (string Id, string Name)? _replyTo;

    private readonly Dictionary<string, CommentThreadItem> _items = new(StringComparer.Ordinal);
    private Border? _root;
    private CommentThreadAutoHeight? _autoHeight;
    private readonly CommentThreadFaces _faces;
    private readonly StackPanel _threadFace;
    private readonly CommentThreadPart _header;
    private readonly ContentControl _titleHost;
    private readonly CommentThreadText _subtitle;
    private readonly CommentThreadButton _resolve;
    private readonly CommentThreadPart _list;
    private readonly StackPanel _listPanel;
    private readonly CommentThreadPart _empty;
    private readonly CommentThreadText _emptyText;
    private readonly CommentThreadGrow _replying;
    private readonly CommentThreadPart _replyingPart;
    private readonly CommentThreadText _replyingText;
    private readonly CommentThreadButton _cancelReply;
    private readonly CommentThreadComposer _composer;
    private readonly CommentThreadPart _resolvedFace;
    private readonly CommentThreadText _resolvedTitle;
    private readonly CommentThreadText _resolvedMeta;
    private readonly CommentThreadAvatarStack _resolvedStack;
    private readonly CommentThreadButton _reopen;
    private bool _rendered;
    private bool _narrow;
    private bool _committing;
    private Regex? _mentionPattern;
    private string? _mentionKey;
    private CommentThreadText? _titleText;
    private IReadOnlyList<CommentAuthor> _mentionable = Array.Empty<CommentAuthor>();

    public CommentThread()
    {
        // Header: title and comment count, then Resolve.
        _titleHost = new ContentControl { IsVisible = false };
        _subtitle = new CommentThreadText { Role = CommentThreadTextRole.Small };
        var heading = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Children = { _titleHost, _subtitle } };
        _resolve = new CommentThreadButton { Kind = CommentThreadButtonKind.Pill, Glyph = ArcIcons.Check, GlyphSize = 15, VerticalAlignment = VerticalAlignment.Center };
        _resolve.Classes.Add("resolve");
        _resolve.Click += (_, e) =>
        {
            e.Handled = true;
            Resolve();
        };
        _header = new CommentThreadPart
        {
            Role = CommentThreadPartRole.Header,
            Child = new CommentThreadRow { Role = CommentThreadRowRole.Header, FillIndex = 0, Children = { heading, _resolve } }
        };

        // Comments, or the empty text.
        _listPanel = new StackPanel { Orientation = Orientation.Vertical };
        _list = new CommentThreadPart { Role = CommentThreadPartRole.List, Child = _listPanel };
        _emptyText = new CommentThreadText { Role = CommentThreadTextRole.Empty, TextWrapping = TextWrapping.Wrap };
        _empty = new CommentThreadPart { Role = CommentThreadPartRole.Empty, Child = _emptyText };

        // Composer area: "Replying to" bar, then the composer.
        _replyingText = new CommentThreadText { Role = CommentThreadTextRole.Replying, VerticalAlignment = VerticalAlignment.Center };
        _cancelReply = new CommentThreadButton { Kind = CommentThreadButtonKind.Close, Glyph = ArcIcons.X, GlyphSize = 14, VerticalAlignment = VerticalAlignment.Center };
        _cancelReply.Click += (_, e) =>
        {
            e.Handled = true;
            CancelReply();
        };
        _replyingPart = new CommentThreadPart
        {
            Role = CommentThreadPartRole.Replying,
            Child = new CommentThreadStack
            {
                Role = CommentThreadStackRole.Replying,
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new ArcGlyph { Data = ArcIcons.CornerDownRight, Size = 13, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center },
                    _replyingText,
                    _cancelReply
                }
            }
        };
        _replying = new CommentThreadGrow { Child = _replyingPart };
        _replying.SetPresent(false);
        _composer = new CommentThreadComposer(compact: false, CurrentUser) { Submit = AddReply };
        var composerArea = new CommentThreadPart
        {
            Role = CommentThreadPartRole.ComposerArea,
            Child = new StackPanel { Orientation = Orientation.Vertical, Children = { _replying, _composer } }
        };

        _threadFace = new StackPanel { Orientation = Orientation.Vertical, Children = { _header, _list, _empty, composerArea } };

        // Resolved chip.
        _resolvedTitle = new CommentThreadText { Role = CommentThreadTextRole.ResolvedTitle };
        _resolvedMeta = new CommentThreadText { Role = CommentThreadTextRole.Small };
        _resolvedStack = new CommentThreadAvatarStack(22);
        _reopen = new CommentThreadButton { Kind = CommentThreadButtonKind.Pill, Glyph = ArcIcons.RotateCcw, GlyphSize = 14, VerticalAlignment = VerticalAlignment.Center };
        _reopen.Click += (_, e) =>
        {
            e.Handled = true;
            Reopen();
        };
        _resolvedFace = new CommentThreadPart
        {
            Role = CommentThreadPartRole.ResolvedFace,
            Child = new CommentThreadRow
            {
                Role = CommentThreadRowRole.Resolved,
                FillIndex = 1,
                Children =
                {
                    new CommentThreadResolvedIcon { VerticalAlignment = VerticalAlignment.Center },
                    new CommentThreadStack
                    {
                        Role = CommentThreadStackRole.Plain,
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children = { _resolvedTitle, _resolvedMeta }
                    },
                    _resolvedStack,
                    _reopen
                }
            }
        };

        _faces = new CommentThreadFaces { Blur = 2 };
        AddHandler(KeyDownEvent, (_, _) => _keyboard = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboard = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private bool _keyboard;

    /// <summary>
    /// How focus moves when the thread moves it (to the picker, the delete confirmation, Reopen): like a browser's
    /// <c>:focus-visible</c>, the ring shows only when the person is using the keyboard.
    /// </summary>
    internal NavigationMethod FocusMethod => _keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;

    #region Parts for tests

    internal CommentThreadComposer Composer => _composer;
    internal CommentThreadButton ResolveButton => _resolve;
    internal CommentThreadButton ReopenButton => _reopen;
    internal CommentThreadGrow ReplyingBar => _replying;
    internal CommentThreadText ReplyingText => _replyingText;
    internal CommentThreadText Subtitle => _subtitle;
    internal CommentThreadText EmptyText => _emptyText;
    internal CommentThreadPart EmptyPart => _empty;
    internal CommentThreadText ResolvedTitleText => _resolvedTitle;
    internal CommentThreadText ResolvedMetaText => _resolvedMeta;
    internal CommentThreadAvatarStack ResolvedStack => _resolvedStack;
    internal CommentThreadFaces Faces => _faces;
    internal Control ThreadFace => _threadFace;
    internal Control ResolvedFace => _resolvedFace;
    internal CommentThreadAutoHeight? AutoHeightHost => _autoHeight;
    internal StackPanel ListPanel => _listPanel;
    internal CommentThreadItem? ItemOf(string id) => _items.TryGetValue(id, out var item) ? item : null;
    internal IReadOnlyDictionary<string, CommentThreadItem> Items => _items;

    #endregion

    /// <summary>The "@Full Name" pattern of the people who can be mentioned.</summary>
    internal Regex? MentionPattern => _mentionPattern;

    /// <summary>The people who can be mentioned now.</summary>
    internal IReadOnlyList<CommentAuthor> Mentionable => _mentionable;

    /// <summary>The emoji the picker offers.</summary>
    internal IReadOnlyList<string> ReactionChoices => Reactions;

    /// <summary>A touch pointer was used: actions stay visible (Arc shows them on touch screens).</summary>
    internal bool UsesTouch { get; private set; }

    /// <summary>The id the composer replies to, or null.</summary>
    public string? ReplyingToId => _replyTo?.Id;

    #region Public methods

    /// <summary>Folds the thread into the resolved chip (when it has comments) and focuses Reopen.</summary>
    public void Resolve()
    {
        if (!IsResolved && CommentThreadTree.Count(Comments) > 0)
        {
            SetResolved(true);
        }
    }

    /// <summary>Opens a resolved thread and focuses Resolve.</summary>
    public void Reopen()
    {
        if (IsResolved)
        {
            SetResolved(false);
        }
    }

    /// <summary>Focuses the composer, caret at the end.</summary>
    public void FocusComposer()
    {
        if (!IsResolved)
        {
            _composer.FocusField();
        }
    }

    /// <summary>Starts a reply to the comment <paramref name="id"/> and focuses the composer.</summary>
    public void ReplyTo(string id)
    {
        if (CommentThreadTree.Find(Comments, id) is not { IsDeleted: false } comment)
        {
            return;
        }
        _replyTo = (id, comment.Author.Name);
        UpdateReplying(animate: true);
        _composer.FocusField();
    }

    /// <summary>Cancels the reply in progress.</summary>
    public void CancelReply()
    {
        if (_replyTo is null)
        {
            return;
        }
        _replyTo = null;
        UpdateReplying(animate: true);
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_autoHeight is not null)
        {
            _autoHeight.Child = null;
        }
        _root = e.NameScope.Find<Border>(RootPart);
        _autoHeight = e.NameScope.Find<CommentThreadAutoHeight>(AutoHeightPart);
        if (_autoHeight is not null)
        {
            _autoHeight.Child = _faces;
        }
        Refresh(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommentsProperty)
        {
            if (!_committing)
            {
                Refresh(animate: _rendered);
            }
        }
        else if (change.Property == IsResolvedProperty)
        {
            PseudoClasses.Set(ResolvedPseudoClass, IsResolved);
            if (_rendered)
            {
                ApplyResolved(animate: true, moveFocus: false);
            }
        }
        else if (change.Property == CurrentUserProperty)
        {
            _composer.SetLeading(CurrentUser);
            Refresh(animate: false);
        }
        else if (change.Property == PeopleProperty || change.Property == TitleProperty || change.Property == PlaceholderProperty
                 || change.Property == LabelsProperty || change.Property == ReactionsProperty)
        {
            Refresh(animate: false);
        }
        else if (change.Property == BoundsProperty)
        {
            var narrow = Bounds.Width > 0 && Bounds.Width < 420;
            if (narrow != _narrow)
            {
                _narrow = narrow;
                PseudoClasses.Set(NarrowPseudoClass, narrow);
                _composer.IsNarrow = narrow;
                _replyingPart.Classes.Set("narrow", narrow);
                foreach (var item in _items.Values)
                {
                    item.SetNarrow(narrow);
                }
            }
        }
    }

    #region Rendering

    /// <summary>Shows the current tree: header, comments (kept by id), composer and resolved chip.</summary>
    private void Refresh(bool animate)
    {
        var list = Comments ?? Array.Empty<ThreadComment>();
        var labels = Labels;
        var total = CommentThreadTree.Count(list);
        var participants = CommentThreadTree.Authors(list);
        _mentionable = People ?? CommentThreadTree.Authors(list, CurrentUser);
        var names = string.Join("\u0001", _mentionable.Select(person => person.Name));
        if (names != _mentionKey)
        {
            // Bodies keep their runs while the pattern is the same instance.
            _mentionKey = names;
            _mentionPattern = CommentThreadBody.CreatePattern(_mentionable.Select(person => person.Name));
        }

        AutomationProperties.SetName(this, labels.Thread);

        // Header.
        var title = Title;
        _titleHost.IsVisible = title is not null;
        if (title is string text)
        {
            _titleText ??= new CommentThreadText { Role = CommentThreadTextRole.Title };
            _titleText.Text = text;
            _titleHost.Content = _titleText;
        }
        else
        {
            _titleHost.Content = title;
        }
        _subtitle.Text = labels.CommentCount(total);
        _resolve.Content = labels.Resolve;
        _resolve.IsEnabled = total > 0;
        AutomationProperties.SetName(_resolve, labels.Resolve);

        // Comments.
        _list.IsVisible = list.Count > 0;
        _empty.IsVisible = list.Count == 0;
        _emptyText.Text = labels.Empty;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Sync(_listPanel, null, list, 0, animate && _rendered, seen);
        var gone = _items.Values.Where(item => !seen.Contains(item.Id)).ToList();
        foreach (var item in gone)
        {
            _items.Remove(item.Id);
        }
        foreach (var item in gone)
        {
            // A removed subtree leaves as one piece: only its topmost comment animates.
            if (item.ParentItem is { } parent && gone.Contains(parent))
            {
                continue;
            }
            if (item.Parent is Panel panel)
            {
                if (animate && _rendered)
                {
                    item.Exited += (_, _) => panel.Children.Remove(item);
                    item.Leave(animate: true);
                }
                else
                {
                    panel.Children.Remove(item);
                }
            }
        }

        // Composer.
        _composer.SetWords(Placeholder, labels.Send, labels.Cancel, labels.Mentions);
        _composer.SetPeople(_mentionable);
        _composer.SetLeading(CurrentUser);
        AutomationProperties.SetName(_cancelReply, labels.CancelReply);
        if (_replyTo is { } reply && CommentThreadTree.Find(list, reply.Id) is not { IsDeleted: false })
        {
            _replyTo = null;
        }
        UpdateReplying(animate && _rendered);

        // Resolved chip.
        var subject = title as string;
        _resolvedTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _resolvedTitle.SetEmphasized(subject is null ? labels.Resolved : $"{labels.Resolved} · {subject}", subject, FontWeight.Normal);
        _resolvedMeta.Text = $"{labels.CommentCount(total)} · {labels.PeopleCount(participants.Count)}";
        _resolvedStack.SetPeople(participants);
        _reopen.Content = labels.Reopen;
        AutomationProperties.SetName(_reopen, labels.Reopen);

        if (!_rendered)
        {
            _faces.Show(IsResolved ? _resolvedFace : _threadFace, animate: false);
            // Keep the other face mounted (hidden) so its state survives.
            if (!_faces.Children.Contains(_threadFace))
            {
                _faces.Children.Insert(0, _threadFace);
                _threadFace.IsVisible = false;
            }
            if (!_faces.Children.Contains(_resolvedFace))
            {
                _faces.Children.Add(_resolvedFace);
                _resolvedFace.IsVisible = false;
            }
            PseudoClasses.Set(ResolvedPseudoClass, IsResolved);
        }
        _rendered = _autoHeight is not null;
    }

    /// <summary>Shows <paramref name="comments"/> in <paramref name="panel"/>, reusing views by id, then their replies.</summary>
    private void Sync(Panel panel, CommentThreadItem? parent, IReadOnlyList<ThreadComment> comments, int depth, bool animate, HashSet<string> seen)
    {
        var position = 0;
        foreach (var comment in comments)
        {
            if (!seen.Add(comment.Id))
            {
                continue;
            }
            var fresh = false;
            if (!_items.TryGetValue(comment.Id, out var item))
            {
                item = new CommentThreadItem(this, comment.Id);
                item.SetNarrow(_narrow);
                _items[comment.Id] = item;
                fresh = true;
            }
            item.ParentItem = parent;
            if (!ReferenceEquals(item.Parent, panel))
            {
                (item.Parent as Panel)?.Children.Remove(item);
                panel.Children.Insert(Math.Min(position, panel.Children.Count), item);
            }
            else
            {
                var at = panel.Children.IndexOf(item);
                if (at != position && position < panel.Children.Count)
                {
                    panel.Children.Move(at, position);
                }
            }
            if (fresh)
            {
                item.SetPresent(!animate);
            }
            item.Update(comment, depth, ModeOf(comment.Id), _collapsed.Contains(comment.Id), animate && !fresh);
            if (fresh && animate)
            {
                item.Enter(animate: true);
            }
            Sync(item.RepliesPanel, item, comment.Replies, depth + 1, animate, seen);
            position = panel.Children.IndexOf(item) + 1;
        }
    }

    private CommentThreadItemMode ModeOf(string id) =>
        _editing == id ? CommentThreadItemMode.Edit
        : _confirming == id ? CommentThreadItemMode.Confirm
        : _picker == id ? CommentThreadItemMode.Picker
        : CommentThreadItemMode.View;

    /// <summary>Re-applies the per-comment UI state (editing, confirming, picker, collapsed) without a data change.</summary>
    private void RefreshUi(params string?[] ids)
    {
        foreach (var id in ids.Distinct())
        {
            if (id is not null && _items.TryGetValue(id, out var item) && item.Comment is { } comment)
            {
                item.Update(comment, item.Depth, ModeOf(id), _collapsed.Contains(id), animate: true);
            }
        }
    }

    private void UpdateReplying(bool animate)
    {
        var labels = Labels;
        if (_replyTo is { } reply)
        {
            _replyingText.SetEmphasized(labels.ReplyingTo(reply.Name), reply.Name, FontWeight.Medium);
            _replying.Enter(animate);
            _composer.CancelAction = CancelReply;
        }
        else
        {
            _replying.Leave(animate);
            _composer.CancelAction = null;
        }
    }

    private void ApplyResolved(bool animate, bool moveFocus)
    {
        _autoHeight?.ArmMorph();
        _faces.Show(IsResolved ? _resolvedFace : _threadFace, animate);
        if (moveFocus)
        {
            var target = IsResolved ? (Control)_reopen : _resolve;
            var method = FocusMethod;
            Dispatcher.UIThread.Post(() => target.Focus(method), DispatcherPriority.Input);
        }
    }

    #endregion

    #region Changes

    private void SetResolved(bool resolved)
    {
        _editing = null;
        _confirming = null;
        var picker = _picker;
        _picker = null;
        _replyTo = null;
        UpdateReplying(animate: false);
        SetCurrentValue(IsResolvedProperty, resolved);
        RaiseEvent(new CommentThreadResolvedChangedEventArgs(ResolvedChangedEvent, resolved));
        RefreshUi(picker);
        ApplyResolved(animate: true, moveFocus: true);
    }

    private void Commit(IReadOnlyList<ThreadComment> next, CommentThreadChange change)
    {
        _committing = true;
        try
        {
            SetCurrentValue(CommentsProperty, next);
        }
        finally
        {
            _committing = false;
        }
        RaiseEvent(new CommentsChangedEventArgs(CommentsChangedEvent, next, change));
        // A handler may have set Comments back (controlled): show whatever the property holds now.
        Refresh(animate: true);
    }

    private void AddReply(string body)
    {
        var comment = new ThreadComment
        {
            Id = CommentThreadTree.NewId(),
            Author = CurrentUser,
            Body = body,
            CreatedAt = NowLabel
        };
        string? parentId = null;
        var list = Comments;
        if (_replyTo is { } reply)
        {
            var path = CommentThreadTree.PathTo(list, reply.Id) ?? [];
            var at = Math.Min(path.Count, MaxDepth) - 1;
            parentId = at >= 0 ? path[at] : null;
        }
        IReadOnlyList<ThreadComment> next = parentId is null
            ? [.. list, comment]
            : CommentThreadTree.Map(list, parentId, parent => parent with { Replies = [.. parent.Replies, comment] });
        if (parentId is not null)
        {
            _collapsed.Remove(parentId);
        }
        _replyTo = null;
        Commit(next, CommentThreadChange.Reply(comment, parentId));
    }

    internal void BeginEdit(string id)
    {
        var previous = (_editing, _confirming, _picker);
        _editing = id;
        _confirming = null;
        _picker = null;
        RefreshUi(previous._editing, previous._confirming, previous._picker, id);
    }

    internal void CancelEdit(string id)
    {
        if (_editing == id)
        {
            _editing = null;
            RefreshUi(id);
        }
    }

    internal void SaveEdit(string id, string body)
    {
        _editing = null;
        Commit(CommentThreadTree.Map(Comments, id, comment => comment with { Body = body, IsEdited = true }), CommentThreadChange.Edit(id, body));
    }

    internal void BeginConfirm(string id)
    {
        var previous = (_editing, _confirming, _picker);
        _confirming = id;
        _editing = null;
        _picker = null;
        RefreshUi(previous._editing, previous._confirming, previous._picker, id);
    }

    internal void CancelConfirm(string id)
    {
        if (_confirming == id)
        {
            _confirming = null;
            RefreshUi(id);
        }
    }

    internal void Remove(string id)
    {
        _confirming = null;
        if (_replyTo?.Id == id)
        {
            _replyTo = null;
        }
        var next = CommentThreadTree.Map(Comments, id, comment => comment.Replies.Count > 0
            ? comment with { IsDeleted = true, Body = string.Empty, Reactions = Array.Empty<CommentReaction>() }
            : null);
        Commit(next, CommentThreadChange.Delete(id));
    }

    internal void OpenPicker(string id)
    {
        var previous = (_confirming, _picker);
        _picker = id;
        _confirming = null;
        RefreshUi(previous._confirming, previous._picker, id);
    }

    internal void ClosePicker(string id)
    {
        if (_picker == id)
        {
            _picker = null;
            RefreshUi(id);
        }
    }

    internal void React(string id, string emoji)
    {
        if (CommentThreadTree.Find(Comments, id) is not { } target)
        {
            return;
        }
        var me = CurrentUser.Id;
        var reactions = target.Reactions;
        var existing = reactions.FirstOrDefault(reaction => reaction.Emoji == emoji);
        var added = existing is null || !existing.UserIds.Contains(me);
        IReadOnlyList<CommentReaction> next = existing is not null
            ? reactions.Select(reaction => reaction.Emoji != emoji
                           ? reaction
                           : reaction with { UserIds = added ? [.. reaction.UserIds, me] : reaction.UserIds.Where(user => user != me).ToArray() })
                       .Where(reaction => reaction.UserIds.Count > 0).ToArray()
            : [.. reactions, new CommentReaction(emoji, [me])];
        _picker = null;
        Commit(CommentThreadTree.Map(Comments, id, comment => comment with { Reactions = next }), CommentThreadChange.React(id, emoji, added));
    }

    internal void ToggleReplies(string id)
    {
        if (!_collapsed.Remove(id))
        {
            _collapsed.Add(id);
        }
        RefreshUi(id);
    }

    internal void NoteTouch()
    {
        if (UsesTouch)
        {
            return;
        }
        UsesTouch = true;
        foreach (var item in _items.Values)
        {
            item.UpdateReveal();
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new CommentThreadAutomationPeer(this);

    /// <summary>Arc's <c>section aria-label</c>: a named region.</summary>
    private sealed class CommentThreadAutomationPeer(CommentThread owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
