using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ArcButton = TKCode.Arc.Button;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;
using TKCode.Arc.Motion;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>What <see cref="CodeReviewThreadView"/> needs from the card.</summary>
internal interface ICodeReviewHost
{
    CodeReviewLabels Labels { get; }

    CodeReviewPerson CurrentViewer { get; }

    CodeReviewPalette Palette { get; }

    bool IsCompactLayout { get; }

    string LanguageOf(string fileId);

    IReadOnlyList<CodeReviewDiffLine> LinesOf(string fileId);

    void ApplySuggestion(CodeReviewThread thread, CodeReviewThreadView view);

    void SetResolved(CodeReviewThread thread, bool resolved);

    void PostReply(CodeReviewThread thread, string text);

    CodeReviewThread? PostComment(string fileId, string lineId, string label, string text, string? suggestion);

    void CloseComposer(string fileId, string lineId, bool focusLine);
}

/// <summary>
/// The content of a thread band (Arc's thread in the flow of the diff): a folded one-line summary for a resolved
/// conversation, the open conversation (comments, a suggested change, Apply / Reply / Resolve or a reply box), or the box
/// for a new comment on a line. Changing between them swaps the content; the band around springs to the new height.
/// Ctrl or Cmd + Enter posts what is typed, Escape closes the box.
/// </summary>
internal sealed class CodeReviewThreadView : Border
{
    private readonly ICodeReviewHost _host;
    private readonly string _fileId;
    private readonly string _lineId;
    private string _label;
    private CodeReviewThread? _thread;
    private bool _unfolded;
    private bool _replying;
    private bool _applying;
    private Textarea? _input;
    private Textarea? _suggestionInput;
    private ArcButton? _apply;
    private readonly string? _lineText;

    private CodeReviewThreadView(ICodeReviewHost host, string fileId, string lineId, string label, CodeReviewThread? thread, string? lineText)
    {
        _host = host;
        _fileId = fileId;
        _lineId = lineId;
        _label = label;
        _thread = thread;
        _lineText = lineText;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        BorderThickness = new Thickness(0, 1);
        UpdatePadding();
        Rebuild();
    }

    /// <summary>The view of an existing thread.</summary>
    public static CodeReviewThreadView ForThread(ICodeReviewHost host, CodeReviewThread thread) =>
        new(host, thread.FileId, thread.AnchorLineId, thread.Label, thread, null);

    /// <summary>The box for a new comment under a line.</summary>
    public static CodeReviewThreadView Composer(ICodeReviewHost host, string fileId, string lineId, string label, string lineText) =>
        new(host, fileId, lineId, label, null, lineText);

    /// <summary>The thread shown, or null for a new comment box.</summary>
    public CodeReviewThread? Thread => _thread;

    /// <summary>The line the band hangs under.</summary>
    public string LineId => _lineId;

    public bool IsComposer => _thread is null;

    /// <summary>The comment or reply box, when one is open (tests).</summary>
    internal Textarea? Input => _input;

    /// <summary>Focuses the comment or reply box.</summary>
    public void FocusInput()
    {
        if (_input is { } input)
        {
            Dispatcher.UIThread.Post(() => input.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
        }
    }

    /// <summary>Re-reads the thread (resolved, applied, new comments) and the layout width.</summary>
    public void Refresh()
    {
        UpdatePadding();
        Rebuild();
    }

    /// <summary>The thread's suggestion was applied: the button morphs to Applied, the thread folds a beat later.</summary>
    public async void OnApplied()
    {
        _applying = true;
        if (_apply is { } apply)
        {
            apply.Content = CodeReviewButtons.CheckLabel(_host.Labels.Applied);
            apply.Variant = ButtonVariant.Secondary;
            ApplyApprovedLook(apply);
        }
        if (IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            await Task.Delay(CodeReviewMotion.FoldAfterApply);
        }
        _applying = false;
        _unfolded = false;
        Rebuild();
    }

    internal static void ApplyApprovedLook(ArcButton button)
    {
        CodeReviewUi.Bind(button, TemplatedControl.BackgroundProperty, CodeReviewTokenKind.ApprovedFill);
        CodeReviewUi.Bind(button, TemplatedControl.ForegroundProperty, CodeReviewTokenKind.ApprovedText);
        button.BorderThickness = new Thickness(0);
    }

    private void UpdatePadding() =>
        CodeReviewUi.Bind(this, PaddingProperty, _host.IsCompactLayout ? CodeReviewTokenKind.CompactThreadPadding : CodeReviewTokenKind.ThreadPadding);

    private void Rebuild()
    {
        if (_applying)
        {
            return;
        }
        _input = null;
        _suggestionInput = null;
        _apply = null;
        Control content;
        if (_thread is null)
        {
            content = BuildComposer();
        }
        else if (_thread.IsResolved && !_unfolded)
        {
            content = BuildFolded(_thread);
        }
        else
        {
            content = BuildOpen(_thread);
        }
        Child = content;
    }

    // ---- Folded ----

    private Control BuildFolded(CodeReviewThread thread)
    {
        var labels = _host.Labels;
        var who = thread.Comments.Count > 0 ? thread.Comments[0].Author.Name : string.Empty;
        var text = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(text, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        text.Inlines = new InlineCollection
        {
            new Run(thread.IsSuggestionApplied ? labels.SuggestionApplied : labels.Resolved),
            Muted($" · {thread.Label}" + (who.Length > 0 ? $" · {who}" : string.Empty))
        };

        var faces = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var seen = new HashSet<string>();
        foreach (var comment in thread.Comments)
        {
            if (!seen.Add(comment.Author.Name) || seen.Count > 3)
            {
                continue;
            }
            var face = new CodeReviewAvatar(comment.Author.Name, 20) { Margin = new Thickness(faces.Children.Count == 0 ? 0 : -6, 0, 0, 0) };
            if (faces.Children.Count > 0)
            {
                face.Ring = ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
            }
            faces.Children.Add(face);
        }

        var row = new DockPanel { LastChildFill = true };
        var check = CodeReviewUi.Glyph(ArcIcons.CircleCheck, 16, ArcDesignTokenKind.Accent);
        check.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(check, Dock.Left);
        var chevron = CodeReviewUi.Glyph(ArcIcons.ChevronRight, 16, ArcDesignTokenKind.TextMuted);
        chevron.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(chevron, Dock.Right);
        DockPanel.SetDock(faces, Dock.Right);
        faces.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(check);
        row.Children.Add(chevron);
        row.Children.Add(faces);
        row.Children.Add(text);

        var button = new CodeReviewPressable
        {
            Height = 36,
            Padding = new Thickness(8, 0),
            Margin = new Thickness(-8, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = row
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        AutomationProperties.SetName(button, $"{text.Inlines.Text}");
        AutomationProperties.SetItemStatus(button, "collapsed");
        button.Click += () =>
        {
            _unfolded = true;
            Rebuild();
        };
        return Wrap(button, fold: true);
    }

    private static Run Muted(string text)
    {
        var run = new Run(text) { FontWeight = FontWeight.Normal };
        run.Bind(TextElement.ForegroundProperty, run.GetResourceObservable(ArcDesignTokenKind.TextMuted));
        return run;
    }

    // ---- Open ----

    private Control BuildOpen(CodeReviewThread thread)
    {
        var labels = _host.Labels;
        var compact = _host.IsCompactLayout;
        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(0, 6) };

        var head = new DockPanel { Height = 20 };
        var label = CodeReviewUi.Text(thread.Label, 12, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 16.8);
        var headRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headRow.Children.Add(label);
        if (thread.IsPending)
        {
            headRow.Children.Add(CodeReviewUi.Pill(labels.Pending));
        }
        if (thread.IsResolved)
        {
            var hide = new CodeReviewPressable
            {
                HoverFill = null,
                Padding = new Thickness(4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = CodeReviewUi.Text(labels.Hide, 12, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 16.8)
            }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
            AutomationProperties.SetName(hide, labels.Hide);
            AutomationProperties.SetItemStatus(hide, "expanded");
            hide.Click += () =>
            {
                _unfolded = false;
                _replying = false;
                Rebuild();
            };
            DockPanel.SetDock(hide, Dock.Right);
            head.Children.Add(hide);
        }
        head.Children.Add(headRow);
        stack.Children.Add(head);

        var comments = new StackPanel();
        for (var i = 0; i < thread.Comments.Count; i++)
        {
            var item = CommentItem(thread.Comments[i]);
            if (i > 0)
            {
                item.Margin = new Thickness(0, 12, 0, 0);
            }
            comments.Children.Add(item);
        }
        stack.Children.Add(comments);

        if (thread.Suggestion is { } suggestion && !thread.IsSuggestionApplied)
        {
            var old = CodeReviewDiff.OldText(_host.LinesOf(thread.FileId), suggestion);
            var snippet = new CodeReviewSnippet(_host.Palette, labels.SuggestedChange, old, suggestion.NewLines.ToList(),
                _host.LanguageOf(thread.FileId))
            {
                Margin = new Thickness(compact ? 0 : 34, 0, 0, 0)
            };
            stack.Children.Add(snippet);
        }

        if (_replying)
        {
            stack.Children.Add(BuildBox(isReply: true));
        }
        else
        {
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(compact ? 0 : 30, 0, 0, 0)
            };
            if (thread.Suggestion is not null && !thread.IsResolved)
            {
                _apply = CodeReviewButtons.Small(labels.ApplySuggestion, ButtonVariant.Primary);
                _apply.Click += (_, _) =>
                {
                    if (!_applying && _thread is { IsSuggestionApplied: false } t)
                    {
                        _host.ApplySuggestion(t, this);
                    }
                };
                actions.Children.Add(_apply);
            }
            var reply = CodeReviewButtons.Small(labels.Reply, ButtonVariant.Ghost);
            reply.Click += (_, _) =>
            {
                _replying = true;
                Rebuild();
                FocusInput();
            };
            actions.Children.Add(reply);
            var resolve = CodeReviewButtons.Small(thread.IsResolved ? labels.Reopen : labels.Resolve, ButtonVariant.Ghost);
            resolve.Click += (_, _) =>
            {
                if (_thread is { } t)
                {
                    var resolved = !t.IsResolved;
                    _unfolded = false;
                    _host.SetResolved(t, resolved);
                }
            };
            actions.Children.Add(resolve);
            stack.Children.Add(actions);
        }
        return Wrap(stack, fold: false);
    }

    private Control CommentItem(CodeReviewComment comment)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("24,10,*") };
        grid.Children.Add(new CodeReviewAvatar(comment.Author.Name, 24));
        var main = new StackPanel { Spacing = 2 };
        Grid.SetColumn(main, 2);
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        meta.Children.Add(CodeReviewUi.Text(comment.Author.Name, 13, ArcDesignTokenKind.Foreground, FontWeight.Medium, 18.2));
        var age = DateTimeOffset.Now - comment.CreatedAt;
        meta.Children.Add(CodeReviewUi.Text(age.TotalMinutes < 1 ? _host.Labels.JustNow : _host.Labels.Age(age), 12,
            ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
        main.Children.Add(meta);
        var body = CodeReviewUi.Text(comment.Text, 13, ArcDesignTokenKind.TextSecondary, lineHeight: 18.2);
        body.TextWrapping = TextWrapping.Wrap;
        main.Children.Add(body);
        grid.Children.Add(main);
        return grid;
    }

    // ---- Boxes ----

    private Control BuildComposer()
    {
        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(0, 6) };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Height = 20 };
        head.Children.Add(CodeReviewUi.Text(_label, 12, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 16.8));
        stack.Children.Add(head);
        stack.Children.Add(BuildBox(isReply: false));
        return Wrap(stack, fold: false);
    }

    private Control BuildBox(bool isReply)
    {
        var labels = _host.Labels;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("24,10,*") };
        grid.Children.Add(new CodeReviewAvatar(_host.CurrentViewer.Name, 24));
        // No panel spacing: the folded suggestion band must not leave a gap, so each part carries its own 8 above it.
        var main = new StackPanel();
        Grid.SetColumn(main, 2);

        var input = new Textarea
        {
            IsResizable = false,
            FrameMinHeight = 64,
            PlaceholderText = labels.LeaveComment,
            FontSize = 13
        };
        AutomationProperties.SetName(input, isReply ? labels.ReplyFieldName(_label) : labels.CommentFieldName(_label));
        _input = input;
        main.Children.Add(input);

        var post = CodeReviewButtons.Small(isReply ? labels.Reply : labels.Comment, ButtonVariant.Primary);
        post.IsEnabled = false;
        input.TextChanged += (_, _) => post.IsEnabled = !string.IsNullOrWhiteSpace(input.Text);

        CodeReviewBand? suggestionBand = null;
        if (!isReply && _lineText is not null)
        {
            var field = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 2) };
            field.Children.Add(CodeReviewUi.Text(labels.SuggestedChange, 12, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 16.8));
            var code = new Textarea
            {
                IsResizable = false,
                FrameMinHeight = 44,
                Text = _lineText,
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap
            };
            ArcTokens.Bind(code, TextBox.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
            AutomationProperties.SetName(code, labels.SuggestedChange);
            _suggestionInput = code;
            field.Children.Add(code);
            suggestionBand = new CodeReviewBand { Child = field, IsExpanded = false };
            main.Children.Add(suggestionBand);

            var toggle = new CodeReviewToggle(labels.SuggestChange)
            {
                Height = 28,
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(2, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            }.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
            AutomationProperties.SetName(toggle, labels.SuggestChange);
            toggle.Toggled += on => suggestionBand.IsExpanded = on;
            main.Children.Add(toggle);
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var cancel = CodeReviewButtons.Small(labels.Cancel, ButtonVariant.Ghost);
        cancel.Click += (_, _) => Close(isReply);
        actions.Children.Add(cancel);
        post.Click += (_, _) => Post(isReply, suggestionBand);
        actions.Children.Add(post);
        main.Children.Add(actions);
        grid.Children.Add(main);

        input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            {
                e.Handled = true;
                Post(isReply, suggestionBand);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close(isReply);
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        return grid;
    }

    private void Close(bool isReply)
    {
        if (isReply)
        {
            _replying = false;
            Rebuild();
            return;
        }
        _host.CloseComposer(_fileId, _lineId, focusLine: true);
    }

    private void Post(bool isReply, CodeReviewBand? suggestionBand)
    {
        var text = _input?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (isReply)
        {
            if (_thread is { } thread)
            {
                _replying = false;
                _host.PostReply(thread, text);
                Rebuild();
            }
            return;
        }
        var suggestion = suggestionBand is { IsExpanded: true } ? _suggestionInput?.Text : null;
        var created = _host.PostComment(_fileId, _lineId, _label, text, suggestion);
        if (created is not null)
        {
            _thread = created;
            _label = created.Label;
            Rebuild();
        }
    }

    /// <summary>
    /// The content column of the band: as wide as the band allows, at most 640 (648 for the folded line, which reaches 8
    /// further left), kept at the left edge.
    /// </summary>
    private Control Wrap(Control content, bool fold)
    {
        var maxWidth = CodeReviewUi.Number(this, CodeReviewTokenKind.ThreadMaxWidth, 640);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MaxWidth = fold ? maxWidth + 8 : maxWidth });
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0, GridUnitType.Pixel)));
        grid.Children.Add(content);
        return grid;
    }
}
