using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What a comment shows besides its data: the thread's per-comment UI state (Arc's <c>ui</c>).</summary>
internal enum CommentThreadItemMode
{
    View,
    Edit,
    Confirm,
    Picker
}

/// <summary>
/// One comment and its replies (Arc's <c>renderComment</c>): portrait, name and time, body with highlighted mentions,
/// reactions and actions, the reply toggle, and the replies hanging from a rail. Views are kept by comment id across
/// tree updates, so counts roll, heights spring and focus survives. The whole item grows in when it is new and shrinks
/// away when it is removed (<see cref="CommentThreadGrow"/>).
/// </summary>
internal sealed class CommentThreadItem : CommentThreadGrow
{
    private readonly CommentThread _owner;
    private readonly CommentThreadArticle _article;
    private readonly Avatar _avatar;
    private readonly CommentThreadDeletedDot _deletedDot;
    private readonly CommentThreadText _name;
    private readonly CommentThreadText _time;
    private readonly CommentThreadInlinePanel _meta;
    private readonly CommentThreadAutoHeight _autoHeight;
    private readonly CommentThreadFaces _faces;
    private readonly StackPanel _view;
    private readonly CommentThreadBody _body;
    private readonly ActivityReactionBar _reactions;
    private readonly CommentThreadReveal _reveal;
    private readonly CommentThreadSequence _sequence;
    private readonly CommentThreadStack _actionsRow;
    private readonly CommentThreadButton _addReaction;
    private readonly CommentThreadButton _reply;
    private readonly CommentThreadButton _edit;
    private readonly CommentThreadButton _delete;
    private readonly CommentThreadText _deletedText;
    private readonly CommentThreadReplyToggle _toggle;
    private readonly CommentThreadGrow _repliesGrow;
    private readonly CommentThreadRail _rail;
    private CommentThreadStack? _confirmRow;
    private CommentThreadButton? _confirmDelete;
    private CommentThreadStack? _pickerRow;
    private readonly List<(CommentThreadButton Button, MotionValue<double>? Pop)> _emoji = new();
    private CommentThreadComposer? _editor;
    private CommentThreadItemMode _mode = CommentThreadItemMode.View;
    private Control? _restoreFocus;
    private string _pickerEmojiKey = string.Empty;

    public CommentThreadItem(CommentThread owner, string id)
    {
        _owner = owner;
        Id = id;

        _avatar = ActivityAvatar.Create(28);
        _deletedDot = new CommentThreadDeletedDot { IsVisible = false };
        var portrait = new Panel { VerticalAlignment = VerticalAlignment.Top, Children = { _avatar, _deletedDot } };

        _name = new CommentThreadText { Role = CommentThreadTextRole.Name };
        _time = new CommentThreadText { Role = CommentThreadTextRole.Small };
        _meta = new CommentThreadInlinePanel { Children = { _name, _time } };

        _body = new CommentThreadBody();
        _reactions = new ActivityReactionBar { Variant = ActivityReactionChipVariant.Comment, VerticalAlignment = VerticalAlignment.Center };
        _reactions.Toggled += (_, e) => _owner.React(Id, e.Emoji);

        _addReaction = Button(CommentThreadButtonKind.Icon, glyph: ArcIcons.SmilePlus, glyphSize: 15);
        _addReaction.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.OpenPicker(Id);
        };
        _reply = Button(CommentThreadButtonKind.Action);
        _reply.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.ReplyTo(Id);
        };
        _edit = Button(CommentThreadButtonKind.Action);
        _edit.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.BeginEdit(Id);
        };
        _delete = Button(CommentThreadButtonKind.Action);
        _delete.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.BeginConfirm(Id);
        };
        _actionsRow = new CommentThreadStack
        {
            Role = CommentThreadStackRole.Actions,
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _addReaction, _reply, _edit, _delete }
        };
        _sequence = new CommentThreadSequence();
        _sequence.Entering += OnFaceEntering;
        _sequence.Show(_actionsRow, 0, animate: false);
        _reveal = new CommentThreadReveal { Child = _sequence, VerticalAlignment = VerticalAlignment.Center };

        var footer = new CommentThreadFooter { Children = { _reactions, _reveal } };
        _view = new StackPanel { Orientation = Orientation.Vertical, Children = { _body, footer } };
        _faces = new CommentThreadFaces { EnterDuration = ArcMotionPresets.Fast, ExitDuration = ArcMotionPresets.Instant, ExitEasing = ArcEasings.Standard };
        _faces.Show(_view, animate: false);
        _autoHeight = new CommentThreadAutoHeight { Child = _faces, ClipMargin = 4 };

        _deletedText = new CommentThreadText { Role = CommentThreadTextRole.Deleted, IsVisible = false, TextWrapping = TextWrapping.Wrap };

        _toggle = new CommentThreadReplyToggle { IsVisible = false };
        _toggle.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.ToggleReplies(Id);
        };

        var main = new CommentThreadStack
        {
            Role = CommentThreadStackRole.Main,
            Orientation = Orientation.Vertical,
            Children = { _meta, _autoHeight, _deletedText, _toggle }
        };
        _article = new CommentThreadArticle
        {
            Child = new CommentThreadRow { Role = CommentThreadRowRole.Comment, Children = { portrait, main } }
        };
        _article.PropertyChanged += OnArticlePropertyChanged;
        _article.AddHandler(PointerPressedEvent, OnArticlePressed, RoutingStrategies.Tunnel);

        RepliesPanel = new StackPanel { Orientation = Orientation.Vertical };
        _rail = new CommentThreadRail { Child = RepliesPanel };
        _repliesGrow = new CommentThreadGrow { Child = _rail };
        _repliesGrow.SetPresent(false);

        Child = new StackPanel { Orientation = Orientation.Vertical, Children = { _article, _repliesGrow } };
    }

    /// <summary>The comment id.</summary>
    public string Id { get; }

    /// <summary>The comment shown.</summary>
    public ThreadComment? Comment { get; private set; }

    /// <summary>Nesting depth (0 for top-level comments).</summary>
    public int Depth { get; private set; }

    /// <summary>The item this one is a reply of, or null.</summary>
    public CommentThreadItem? ParentItem { get; set; }

    /// <summary>Where the replies go.</summary>
    public StackPanel RepliesPanel { get; }

    // Parts exposed for tests and the thread.
    internal CommentThreadArticle Article => _article;
    internal CommentThreadBody Body => _body;
    internal ActivityReactionBar ReactionBar => _reactions;
    internal CommentThreadReveal Reveal => _reveal;
    internal CommentThreadReplyToggle Toggle => _toggle;
    internal CommentThreadGrow Replies => _repliesGrow;
    internal CommentThreadAutoHeight AutoHeight => _autoHeight;
    internal CommentThreadComposer? Editor => _editor;
    internal CommentThreadButton AddReactionButton => _addReaction;
    internal CommentThreadButton ReplyButton => _reply;
    internal CommentThreadButton EditButton => _edit;
    internal CommentThreadButton DeleteButton => _delete;
    internal CommentThreadButton? ConfirmDeleteButton => _confirmDelete;
    internal IReadOnlyList<CommentThreadButton> EmojiButtons => _emoji.Select(e => e.Button).ToList();
    internal CommentThreadStack? PickerRow => _pickerRow;
    internal CommentThreadStack? ConfirmRow => _confirmRow;
    internal CommentThreadSequence Sequence => _sequence;
    internal CommentThreadItemMode Mode => _mode;
    internal CommentThreadText TimeText => _time;
    internal CommentThreadText NameText => _name;
    internal CommentThreadText DeletedText => _deletedText;
    internal Avatar Portrait => _avatar;

    private static CommentThreadButton Button(CommentThreadButtonKind kind, Geometry? glyph = null, double glyphSize = 14) =>
        new() { Kind = kind, Glyph = glyph, GlyphSize = glyphSize, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Shows <paramref name="comment"/> at <paramref name="depth"/> in the thread's current state.</summary>
    public void Update(ThreadComment comment, int depth, CommentThreadItemMode mode, bool collapsed, bool animate)
    {
        Comment = comment;
        Depth = depth;
        var labels = _owner.Labels;
        var mine = comment.Author.Id == _owner.CurrentUser.Id;
        var deleted = comment.IsDeleted;

        AutomationProperties.SetName(_article, deleted ? labels.DeletedComment : $"{comment.Author.Name}, {comment.CreatedAt}");
        _avatar.IsVisible = !deleted;
        _deletedDot.IsVisible = deleted;
        _meta.IsVisible = !deleted;
        _autoHeight.IsVisible = !deleted;
        _deletedText.IsVisible = deleted;
        _deletedText.Text = labels.DeletedText;
        if (!deleted)
        {
            ActivityAvatar.Update(_avatar, comment.Author.Name, comment.Author.Avatar);
            _name.Text = comment.Author.Name;
            _time.Text = comment.IsEdited ? $"{comment.CreatedAt} · {labels.Edited}" : comment.CreatedAt;
            _body.SetBody(comment.Body, _owner.MentionPattern);
            _reactions.NameFormatter = r => labels.ReactionCount(r.Emoji, r.Count, r.Mine);
            var me = _owner.CurrentUser.Id;
            var reactions = new List<ActivityReaction>(comment.Reactions.Count);
            foreach (var reaction in comment.Reactions)
            {
                if (reaction.UserIds.Count > 0)
                {
                    reactions.Add(new ActivityReaction(reaction.Emoji, reaction.UserIds.Count, reaction.UserIds.Contains(me)));
                }
            }
            _reactions.SetReactions(reactions, animate);
            _reactions.IsVisible = reactions.Count > 0 || _reactions.Children.Count > 0;
        }

        SetWords(_addReaction, null, labels.AddReaction);
        SetWords(_reply, labels.Reply, labels.Reply);
        SetWords(_edit, labels.Edit, labels.Edit);
        SetWords(_delete, labels.Delete, labels.Delete);
        _edit.IsVisible = mine;
        _delete.IsVisible = mine;

        // Replies toggle and the replies themselves.
        var replies = comment.Replies;
        _toggle.IsVisible = replies.Count > 0;
        _toggle.IsExpanded = !collapsed;
        _toggle.Label = collapsed ? labels.ShowReplies(CommentThreadTree.Count(replies)) : labels.HideReplies;
        if (collapsed)
        {
            _toggle.Stack.SetPeople(CommentThreadTree.Authors(replies));
        }
        var showReplies = replies.Count > 0 && !collapsed;
        if (showReplies)
        {
            _repliesGrow.Enter(animate);
        }
        else
        {
            _repliesGrow.Leave(animate);
        }

        SetMode(deleted ? CommentThreadItemMode.View : mode, animate);
        UpdatePicker(comment);
        UpdateReveal();
    }

    private static void SetWords(CommentThreadButton button, string? text, string name)
    {
        if (text is not null)
        {
            button.Content = text;
        }
        AutomationProperties.SetName(button, name);
    }

    /// <summary>Applies the narrow layout (12px reply indent).</summary>
    public void SetNarrow(bool narrow)
    {
        _rail.Classes.Set("narrow", narrow);
    }

    private void SetMode(CommentThreadItemMode mode, bool animate)
    {
        if (mode == _mode)
        {
            return;
        }
        var previous = _mode;
        _mode = mode;
        var hadFocus = _article.IsKeyboardFocusWithin;
        // The comment's height springs between its views (Arc's AutoHeight morphKey).
        _autoHeight.ArmMorph();
        if (mode == CommentThreadItemMode.Edit)
        {
            var editor = EnsureEditor();
            var labels = _owner.Labels;
            editor.SetWords(labels.EditComment, labels.Save, labels.Cancel, labels.Mentions);
            editor.SetPeople(_owner.Mentionable);
            editor.Initial = Comment?.Body ?? string.Empty;
            editor.Text = Comment?.Body ?? string.Empty;
            _faces.Show(editor, animate);
            editor.FocusField();
            return;
        }
        if (previous == CommentThreadItemMode.Edit)
        {
            _faces.Show(_view, animate);
            if (hadFocus)
            {
                _restoreFocus = _edit;
            }
        }
        switch (mode)
        {
            case CommentThreadItemMode.Confirm:
                _sequence.Show(EnsureConfirm(), 6, animate);
                break;
            case CommentThreadItemMode.Picker:
                _sequence.Show(EnsurePicker(), -6, animate);
                break;
            default:
                if (hadFocus && previous == CommentThreadItemMode.Picker)
                {
                    _restoreFocus = _addReaction;
                }
                else if (hadFocus && previous == CommentThreadItemMode.Confirm)
                {
                    _restoreFocus = _delete;
                }
                _sequence.Show(_actionsRow, 0, animate);
                if (_sequence.Displayed == _actionsRow)
                {
                    RestoreFocus();
                }
                break;
        }
    }

    private void RestoreFocus()
    {
        if (_restoreFocus is { } target)
        {
            _restoreFocus = null;
            if (target.IsVisible)
            {
                target.Focus(_owner.FocusMethod);
            }
        }
    }

    private CommentThreadComposer EnsureEditor()
    {
        if (_editor is null)
        {
            _editor = new CommentThreadComposer(compact: true)
            {
                Submit = body => _owner.SaveEdit(Id, body),
                CancelAction = () => _owner.CancelEdit(Id)
            };
        }
        return _editor;
    }

    private CommentThreadStack EnsureConfirm()
    {
        if (_confirmRow is not null)
        {
            UpdateConfirmWords();
            return _confirmRow;
        }
        var prompt = new CommentThreadText { Role = CommentThreadTextRole.Prompt, VerticalAlignment = VerticalAlignment.Center };
        var cancel = Button(CommentThreadButtonKind.Action);
        cancel.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.CancelConfirm(Id);
        };
        _confirmDelete = Button(CommentThreadButtonKind.Danger);
        _confirmDelete.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.Remove(Id);
        };
        _confirmRow = new CommentThreadStack
        {
            Role = CommentThreadStackRole.Actions,
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { prompt, cancel, _confirmDelete }
        };
        _confirmRow.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _owner.CancelConfirm(Id);
            }
        });
        UpdateConfirmWords();
        return _confirmRow;
    }

    private void UpdateConfirmWords()
    {
        var labels = _owner.Labels;
        ((CommentThreadText)_confirmRow!.Children[0]).Text = labels.DeleteConfirm;
        SetWords((CommentThreadButton)_confirmRow.Children[1], labels.Cancel, labels.Cancel);
        SetWords(_confirmDelete!, labels.Delete, labels.Delete);
    }

    private CommentThreadStack EnsurePicker()
    {
        var emojis = _owner.ReactionChoices;
        var key = string.Join("\u0001", emojis);
        if (_pickerRow is not null && key == _pickerEmojiKey)
        {
            AutomationProperties.SetName(_pickerRow, _owner.Labels.Reactions);
            return _pickerRow;
        }
        _pickerEmojiKey = key;
        _pickerRow ??= new CommentThreadStack
        {
            Role = CommentThreadStackRole.Actions,
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (_pickerRow.GetValue(AutomationProperties.ControlTypeOverrideProperty) is null)
        {
            AutomationProperties.SetControlTypeOverride(_pickerRow, Avalonia.Automation.Peers.AutomationControlType.Group);
            _pickerRow.AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    _owner.ClosePicker(Id);
                }
            });
        }
        AutomationProperties.SetName(_pickerRow, _owner.Labels.Reactions);
        foreach (var (_, pop) in _emoji)
        {
            pop?.Dispose();
        }
        _emoji.Clear();
        _pickerRow.Children.Clear();
        foreach (var emoji in emojis)
        {
            var button = new CommentThreadButton { Kind = CommentThreadButtonKind.Emoji, Content = emoji, VerticalAlignment = VerticalAlignment.Center };
            button.Click += (_, e) =>
            {
                e.Handled = true;
                _owner.React(Id, emoji);
            };
            button.RenderTransform = new ScaleTransform();
            _emoji.Add((button, null));
            _pickerRow.Children.Add(button);
        }
        var close = Button(CommentThreadButtonKind.Icon, ArcIcons.X, 14);
        close.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.ClosePicker(Id);
        };
        _pickerRow.Children.Add(close);
        UpdatePicker(Comment);
        return _pickerRow;
    }

    private void UpdatePicker(ThreadComment? comment)
    {
        if (_pickerRow is null || comment is null)
        {
            return;
        }
        var labels = _owner.Labels;
        var me = _owner.CurrentUser.Id;
        foreach (var (button, _) in _emoji)
        {
            var emoji = (string)button.Content!;
            AutomationProperties.SetName(button, labels.ReactWith(emoji));
            button.IsOn = comment.Reactions.Any(r => r.Emoji == emoji && r.UserIds.Contains(me));
        }
        if (_pickerRow.Children.Count > 0 && _pickerRow.Children[^1] is CommentThreadButton close)
        {
            AutomationProperties.SetName(close, labels.CloseReactions);
        }
    }

    /// <summary>A face of the action row starts entering: the picker staggers its emoji in and takes focus.</summary>
    private void OnFaceEntering(Control face, bool animated)
    {
        if (ReferenceEquals(face, _pickerRow))
        {
            for (var i = 0; i < _emoji.Count; i++)
            {
                var (button, pop) = _emoji[i];
                if (pop is null)
                {
                    pop = new MotionValue<double>(button, 1, MotionVectors.Double);
                    var target = button;
                    pop.Changed += p => ApplyPop(target, p);
                    _emoji[i] = (button, pop);
                }
                if (animated && ArcMotion.IsFull(this))
                {
                    // Arc: scale .6 → 1 and opacity on the snappy spring, staggered by stagger.item (35ms).
                    pop.Jump(0);
                    _ = pop.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, TimeSpan.FromTicks(ArcMotionPresets.StaggerItem.Ticks * i)));
                }
                else
                {
                    pop.Jump(1);
                }
                ApplyPop(button, pop.Value);
            }
            if (_emoji.Count > 0)
            {
                _emoji[0].Button.Focus(_owner.FocusMethod);
            }
        }
        else if (ReferenceEquals(face, _confirmRow))
        {
            _confirmDelete?.Focus(_owner.FocusMethod);
        }
        else if (ReferenceEquals(face, _actionsRow))
        {
            RestoreFocus();
        }
    }

    private static void ApplyPop(CommentThreadButton button, double p)
    {
        var scale = 0.6 + 0.4 * p;
        if (button.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
        button.Opacity = Math.Clamp(p, 0, 1);
    }

    private void OnArticlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty || e.Property == IsKeyboardFocusWithinProperty)
        {
            UpdateReveal();
        }
    }

    private void OnArticlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            _owner.NoteTouch();
        }
    }

    /// <summary>Actions come forward on hover or focus, always while a prompt or the picker is open, and on touch.</summary>
    public void UpdateReveal()
    {
        _reveal.IsRevealed = _mode is CommentThreadItemMode.Confirm or CommentThreadItemMode.Picker
                             || _article.IsPointerOver || _article.IsKeyboardFocusWithin || _owner.UsesTouch;
    }
}
