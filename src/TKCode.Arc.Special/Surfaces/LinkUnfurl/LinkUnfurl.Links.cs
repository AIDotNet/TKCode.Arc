using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public sealed partial class LinkUnfurl
{
    /// <summary>The chip width tween (340ms, enter), the title roll (340ms) and the favicon pop (272ms = 0.8 × 340).</summary>
    internal static readonly TimeSpan ChipMorph = TimeSpan.FromMilliseconds(340);

    internal static readonly TimeSpan FaviconPop = TimeSpan.FromMilliseconds(272);

    /// <summary>The link's leave after the remove button (opacity, 0.9 scale; 180ms, exit).</summary>
    internal static readonly TimeSpan LinkExit = TimeSpan.FromMilliseconds(180);

    #region Public methods

    /// <summary>
    /// Pastes <paramref name="address"/> as a link at the caret (at the last caret position when the message is not
    /// focused) and starts fetching its preview. Returns the link, or null when the text is not an address.
    /// </summary>
    public LinkUnfurlLink? Paste(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _interacted = true;
        _autoPlay?.Cancel();
        return AddLink(address.Trim(), atEnd: false)?.Link;
    }

    /// <summary>Opens the card of the link <paramref name="id"/> (it must be resolved).</summary>
    public void ExpandPreview(string id)
    {
        if (EntryFor(id) is { } entry)
        {
            Expand(entry, announce: true);
        }
    }

    /// <summary>Folds the card of the link <paramref name="id"/> back into the link.</summary>
    public void CollapsePreview(string id)
    {
        if (EntryFor(id) is { } entry)
        {
            Collapse(entry, announce: true);
        }
    }

    /// <summary>Opens or folds the card of the link <paramref name="id"/>.</summary>
    public void TogglePreview(string id)
    {
        if (EntryFor(id) is not { } entry)
        {
            return;
        }
        if (entry.Link.IsExpanded)
        {
            Collapse(entry, announce: true);
        }
        else
        {
            Expand(entry, announce: true);
        }
    }

    /// <summary>Fetches the preview of the link <paramref name="id"/> again (after a failure).</summary>
    public void Retry(string id)
    {
        if (EntryFor(id) is { Link.Status: LinkUnfurlStatus.Failed } entry)
        {
            Resolve(entry, announce: true);
        }
    }

    /// <summary>
    /// Removes the link <paramref name="id"/> from the message: its card folds back into the link, then the link leaves
    /// the text (the card's remove button).
    /// </summary>
    public async void RemoveLink(string id)
    {
        if (EntryFor(id) is not { } entry || entry.IsLeaving)
        {
            return;
        }
        entry.IsLeaving = true;
        entry.Fetch?.Cancel();
        var title = entry.Link.Preview?.Title;
        var focusBack = entry.Card?.IsKeyboardFocusWithin == true;
        if (focusBack)
        {
            FocusEditor();
        }
        if (entry.Link.IsExpanded || entry.Progress.Value > 0)
        {
            entry.Link.IsExpanded = false;
            AnimateHot(entry, 0);
            await FoldTo(entry, 0, velocity: null);
        }
        if (!_entries.Contains(entry))
        {
            return;
        }
        if (!IsInstant)
        {
            await entry.Exit.AnimateTo(0, MotionSpec.Tween(LinkExit, ArcEasings.Exit));
        }
        else
        {
            entry.Exit.Jump(0);
        }
        if (!_entries.Contains(entry))
        {
            return;
        }
        DeleteLinkText(entry);
        Forget(entry);
        Announce(Labels.AnnounceRemoved(title));
        RaiseEvent(new LinkUnfurlLinkEventArgs(LinkRemovedEvent, entry.Link));
    }

    /// <summary>Empties the message and removes every link and card (pending fetches are cancelled).</summary>
    public void Clear()
    {
        ClearAll(removeEvents: true);
        _converting = true;
        try
        {
            _editor?.Clear();
            SetCurrentValue(TextProperty, string.Empty);
        }
        finally
        {
            _converting = false;
        }
    }

    #endregion

    #region Adding links

    /// <summary>Inserts <paramref name="address"/> as a link (at the caret, or at the end) and starts its fetch.</summary>
    private LinkUnfurlEntry? AddLink(string address, bool atEnd)
    {
        if (_editor is not { } editor || !LinkUnfurlAddress.TryParse(address, out var url) || IsSending)
        {
            return null;
        }
        var entry = CreateEntry(address, url);
        var focusedSample = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as LinkUnfurlSampleButton;
        var sampleFromKeyboard = focusedSample?.Classes.Contains(":focus-visible") == true;
        _converting = true;
        TokenSpan? token;
        try
        {
            if (atEnd)
            {
                var end = editor.Text.Length;
                editor.Select(end, end);
            }
            token = editor.InsertTokenAtCaret(LinkUnfurlChipRenderer.Kind, entry.Id, address);
        }
        finally
        {
            _converting = false;
        }
        if (token is null)
        {
            Forget(entry);
            return null;
        }
        SyncFromEditor();
        if (focusedSample is not null)
        {
            // The sample keeps its focus, shown as a ring only when it was pressed from the keyboard.
            focusedSample.Focus(sampleFromKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        OnLinkAdded(entry);
        return entry;
    }

    private LinkUnfurlEntry CreateEntry(string address, string url)
    {
        var link = new LinkUnfurlLink("link-" + (++_counter).ToString(System.Globalization.CultureInfo.InvariantCulture), url, address);
        var entry = new LinkUnfurlEntry(this, link);
        entry.Label = LinkUnfurlAddress.ShortForm(url);
        entry.Width.Changed += value =>
        {
            entry.ShownWidth = value;
            _renderer.Remeasure();
        };
        entry.Reveal.Changed += _ => _renderer.Repaint();
        entry.Pop.Changed += _ => _renderer.Repaint();
        entry.Hot.Changed += _ => _renderer.Repaint();
        entry.Exit.Changed += _ => _renderer.Repaint();
        entry.Progress.Changed += _ => _lane?.InvalidateMeasure();
        _entries.Add(entry);
        _links.Add(link);
        return entry;
    }

    private void OnLinkAdded(LinkUnfurlEntry entry)
    {
        RaiseEvent(new LinkUnfurlLinkEventArgs(LinkAddedEvent, entry.Link));
        Resolve(entry, announce: true);
    }

    private LinkUnfurlEntry? FindByAddress(string url)
    {
        foreach (var entry in _entries)
        {
            if (!entry.IsLeaving && LinkUnfurlAddress.SameAddress(entry.Link.Url, url))
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>
    /// The text changed: links that left the text fold away, links brought back (undo) fetch again, and a pasted address
    /// or an address followed by a typed space becomes a link (after the change settles).
    /// </summary>
    private void OnEditorTextChanged(object? sender, TokenTextChangedEventArgs e)
    {
        if (!string.Equals(Text, e.Text, StringComparison.Ordinal))
        {
            SetCurrentValue(TextProperty, e.Text);
        }
        if (SendError is not null && !_converting)
        {
            SendError = null;
        }
        SyncFromEditor();
        if (_converting)
        {
            return;
        }
        if (FindConversion(e.OldText, e.Text, e.Tokens) is { } conversion)
        {
            Dispatcher.UIThread.Post(() => Convert(conversion.Start, conversion.Address, conversion.Typed));
        }
    }

    /// <summary>What a text change turns into a link: an address pasted whole, or the word before a typed space.</summary>
    internal static (int Start, string Address, bool Typed)? FindConversion(string oldText, string text, IReadOnlyList<TokenSpan> tokens)
    {
        var prefix = 0;
        var max = Math.Min(oldText.Length, text.Length);
        while (prefix < max && oldText[prefix] == text[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < max - prefix && oldText[oldText.Length - 1 - suffix] == text[text.Length - 1 - suffix])
        {
            suffix++;
        }
        var insertedLength = text.Length - prefix - suffix;
        if (insertedLength <= 0)
        {
            return null;
        }
        var inserted = text.Substring(prefix, insertedLength);
        if (insertedLength == 1 && char.IsWhiteSpace(inserted[0]))
        {
            // A typed space (or line break) after an address.
            var start = prefix;
            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }
            var word = text[start..prefix];
            if (word.Length > 0 && !Covered(tokens, start, prefix) && LinkUnfurlAddress.TryParse(word, out _))
            {
                return (start, word, true);
            }
            return null;
        }
        if (insertedLength >= 2)
        {
            var trimmed = inserted.Trim();
            if (trimmed.Length == 0 || !LinkUnfurlAddress.TryParse(trimmed, out _))
            {
                return null;
            }
            var start = prefix + inserted.IndexOf(trimmed, StringComparison.Ordinal);
            if (Covered(tokens, start, start + trimmed.Length))
            {
                return null;
            }
            return (start, trimmed, false);
        }
        return null;
    }

    private static bool Covered(IReadOnlyList<TokenSpan> tokens, int start, int end)
    {
        foreach (var token in tokens)
        {
            if (token.Start < end && token.End > start)
            {
                return true;
            }
        }
        return false;
    }

    private void Convert(int start, string address, bool typed)
    {
        if (_editor is not { } editor || IsSending || !LinkUnfurlAddress.TryParse(address, out var url))
        {
            return;
        }
        var text = editor.Text;
        if (start < 0 || start + address.Length > text.Length || string.CompareOrdinal(text, start, address, 0, address.Length) != 0 ||
            Covered(editor.Tokens, start, start + address.Length))
        {
            return;
        }
        var entry = CreateEntry(address, url);
        _converting = true;
        TokenSpan? token;
        try
        {
            editor.Select(start, start + address.Length);
            token = editor.InsertTokenAtCaret(LinkUnfurlChipRenderer.Kind, entry.Id, address);
            if (token is not null && typed)
            {
                // The typed space stays after the link; the caret goes after it.
                var after = Math.Min(editor.Text.Length, token.End + 1);
                editor.Select(after, after);
            }
        }
        finally
        {
            _converting = false;
        }
        if (token is null)
        {
            Forget(entry);
            return;
        }
        SyncFromEditor();
        OnLinkAdded(entry);
    }

    /// <summary>Matches the links to the editor's tokens: order, removals by editing, and links restored by undo.</summary>
    private void SyncFromEditor()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        var tokens = editor.Tokens;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            var present = false;
            foreach (var token in tokens)
            {
                if (token.Id == entry.Id)
                {
                    present = true;
                    break;
                }
            }
            if (!present && !entry.IsLeaving && !_converting)
            {
                RemovedByEditing(entry);
            }
        }
        if (!_converting)
        {
            foreach (var token in tokens)
            {
                if (token.Kind == LinkUnfurlChipRenderer.Kind && EntryFor(token.Id) is null && LinkUnfurlAddress.TryParse(token.Label, out var url))
                {
                    // Brought back by undo: a new link with the same address.
                    var entry = CreateEntry(token.Label, url);
                    _converting = true;
                    try
                    {
                        var list = new List<TokenSpan>(tokens.Count);
                        foreach (var t in tokens)
                        {
                            list.Add(t.Id == token.Id ? t with { Id = entry.Id } : t);
                        }
                        var caret = editor.CaretIndex;
                        editor.SetTextWithTokens(editor.Text, list);
                        editor.Select(caret, caret);
                    }
                    finally
                    {
                        _converting = false;
                    }
                    OnLinkAdded(entry);
                    SyncFromEditor();
                    return;
                }
            }
        }
        // Order the links (and their cards) as the text does.
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            order[token.Id] = token.Start;
        }
        _entries.Sort((a, b) => Position(a).CompareTo(Position(b)));
        _links.Clear();
        foreach (var entry in _entries)
        {
            _links.Add(entry.Link);
        }
        if (_lane is { } lane)
        {
            var index = 0;
            foreach (var entry in _entries)
            {
                if (entry.Card is { } card && lane.Children.IndexOf(card) is var at && at >= 0)
                {
                    if (at != index && index < lane.Children.Count)
                    {
                        lane.Children.Move(at, index);
                    }
                    index++;
                }
            }
        }
        UpdateEditorStatus();
        UpdateSampleButtons();

        int Position(LinkUnfurlEntry entry) => order.TryGetValue(entry.Id, out var start) ? start : int.MaxValue;
    }

    /// <summary>Backspace, Delete, a cut or a typed-over selection took the link out: the card shrinks in place and goes.</summary>
    private async void RemovedByEditing(LinkUnfurlEntry entry)
    {
        entry.IsRemoved = true;
        entry.Fetch?.Cancel();
        _entries.Remove(entry);
        _links.Remove(entry.Link);
        UpdateSweep();
        Announce(Labels.AnnounceRemoved(null));
        RaiseEvent(new LinkUnfurlLinkEventArgs(LinkRemovedEvent, entry.Link));
        if (entry.Card is { IsVisible: true } && entry.Progress.Value > 0.001)
        {
            entry.FoldsInPlace = true;
            entry.Link.IsExpanded = false;
            if (entry.Card.IsKeyboardFocusWithin)
            {
                FocusEditor();
            }
            await FoldTo(entry, 0, velocity: null);
        }
        DropCard(entry);
        entry.Dispose();
    }

    /// <summary>Takes a link out of the lists and its card out of the lane.</summary>
    private void Forget(LinkUnfurlEntry entry)
    {
        entry.IsRemoved = true;
        _entries.Remove(entry);
        _links.Remove(entry.Link);
        DropCard(entry);
        entry.Dispose();
        UpdateSweep();
        UpdateEditorStatus();
        UpdateSampleButtons();
    }

    private void DropCard(LinkUnfurlEntry entry)
    {
        if (entry.Card is { } card)
        {
            if (card.IsKeyboardFocusWithin)
            {
                FocusEditor();
            }
            _lane?.Children.Remove(card);
            entry.Card = null;
        }
    }

    /// <summary>Deletes a link's characters (and one space after them) from the text, keeping the other links.</summary>
    private void DeleteLinkText(LinkUnfurlEntry entry)
    {
        if (_editor is not { } editor)
        {
            return;
        }
        TokenSpan? found = null;
        foreach (var token in editor.Tokens)
        {
            if (token.Id == entry.Id)
            {
                found = token;
                break;
            }
        }
        if (found is null)
        {
            return;
        }
        var text = editor.Text;
        var end = found.End < text.Length && text[found.End] == ' ' ? found.End + 1 : found.End;
        var removed = end - found.Start;
        var next = string.Concat(text.AsSpan(0, found.Start), text.AsSpan(end));
        var tokens = new List<TokenSpan>();
        foreach (var token in editor.Tokens)
        {
            if (token.Id == entry.Id)
            {
                continue;
            }
            tokens.Add(token.Start >= end ? token with { Start = token.Start - removed, End = token.End - removed } : token);
        }
        var caret = editor.CaretIndex;
        _converting = true;
        try
        {
            editor.SetTextWithTokens(next, tokens);
            var at = caret >= end ? caret - removed : caret > found.Start ? found.Start : caret;
            editor.Select(at, at);
        }
        finally
        {
            _converting = false;
        }
    }

    #endregion

    #region Fetching

    private async void Resolve(LinkUnfurlEntry entry, bool announce)
    {
        entry.Fetch?.Cancel();
        var cts = new CancellationTokenSource();
        entry.Fetch = cts;
        entry.Link.Status = LinkUnfurlStatus.Loading;
        entry.Link.Preview = null;
        SetLabel(entry, LinkUnfurlAddress.ShortForm(entry.Link.Url), animate: true);
        UpdateSweep();
        UpdateEditorStatus();
        UpdateSampleButtons();
        _renderer.Repaint();
        if (announce)
        {
            Announce(Labels.AnnounceAdded(LinkUnfurlAddress.Host(entry.Link.Url)));
        }
        LinkUnfurlPreview? preview;
        try
        {
            var resolver = Resolver ?? DefaultResolve;
            preview = await resolver(entry.Link.Url, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            preview = null;
        }
        if (cts.IsCancellationRequested || entry.IsRemoved || !_entries.Contains(entry))
        {
            return;
        }
        entry.Fetch = null;
        cts.Dispose();
        if (preview is null)
        {
            entry.Link.Status = LinkUnfurlStatus.Failed;
            SetLabel(entry, LinkUnfurlAddress.ShortForm(entry.Link.Url), animate: true);
            UpdateSweep();
            UpdateEditorStatus();
            UpdateSampleButtons();
            _renderer.Repaint();
            Announce(Labels.AnnounceFailed(LinkUnfurlAddress.Host(entry.Link.Url)));
            RaiseEvent(new LinkUnfurlLinkEventArgs(LinkResolvedEvent, entry.Link));
            return;
        }
        entry.Link.Preview = preview;
        entry.Link.Status = LinkUnfurlStatus.Ready;
        SetLabel(entry, preview.Title, animate: true);
        if (IsInstant)
        {
            entry.Reveal.Jump(1);
            entry.Pop.Jump(1);
        }
        else
        {
            entry.Reveal.Jump(0);
            entry.Pop.Jump(0);
            _ = entry.Reveal.AnimateTo(1, MotionSpec.Tween(Scaled(ChipMorph), ArcEasings.Enter));
            _ = entry.Pop.AnimateTo(1, MotionSpec.Tween(Scaled(FaviconPop), ArcEasings.Enter));
        }
        EnsureCard(entry, preview);
        UpdateSweep();
        UpdateEditorStatus();
        RaiseEvent(new LinkUnfurlLinkEventArgs(LinkResolvedEvent, entry.Link));
        if (AutoExpand)
        {
            Expand(entry, announce: true);
        }
        else
        {
            Announce(Labels.AnnounceReady(preview.Site, preview.Title));
            UpdateSampleButtons();
        }
    }

    /// <summary>The built-in resolver: a sample by address, otherwise a plain preview, after the simulated fetch.</summary>
    private async Task<LinkUnfurlPreview?> DefaultResolve(string url, CancellationToken cancellationToken)
    {
        LinkUnfurlPreview? sample = null;
        if (Samples is { } samples)
        {
            foreach (var candidate in samples)
            {
                if (LinkUnfurlAddress.SameAddress(candidate.Url, url))
                {
                    sample = candidate;
                    break;
                }
            }
        }
        var wait = SimulatedFetch + (sample?.Image is not null ? ImageDecode : TimeSpan.Zero);
        await _scheduler.Delay(TimeSpan.FromTicks((long)(wait.Ticks / Speed)), cancellationToken);
        return sample ?? LinkUnfurlAddress.PlainPreview(url);
    }

    private TimeSpan Scaled(TimeSpan duration) => TimeSpan.FromTicks((long)(duration.Ticks / Speed));

    /// <summary>Sets a chip's label and eases its width to fit (340ms, enter; instant when not at full motion).</summary>
    private void SetLabel(LinkUnfurlEntry entry, string label, bool animate)
    {
        entry.Label = label;
        var target = _renderer.NaturalWidth(label);
        if (double.IsNaN(target))
        {
            entry.ShownWidth = double.NaN;
            entry.TargetWidth = double.NaN;
            _renderer.Remeasure();
            return;
        }
        entry.TargetWidth = target;
        if (double.IsNaN(entry.ShownWidth) || !animate || IsInstant)
        {
            entry.Width.Jump(target);
            return;
        }
        entry.Width.Jump(entry.ShownWidth);
        _ = entry.Width.AnimateTo(target, MotionSpec.Tween(Scaled(ChipMorph), ArcEasings.Enter));
    }

    /// <summary>The editor's typeface became known or changed: every chip takes its label's width again.</summary>
    internal void OnChipTypefaceChanged()
    {
        foreach (var entry in _entries)
        {
            entry.ShownWidth = double.NaN;
            entry.TargetWidth = double.NaN;
            entry.ClearLayouts();
        }
    }

    #endregion

    #region Cards

    private void EnsureCard(LinkUnfurlEntry entry, LinkUnfurlPreview preview)
    {
        if (entry.Card is null)
        {
            entry.Card = new LinkUnfurlCardHost(this, entry) { IsVisible = false };
            if (_lane is { } lane)
            {
                var index = 0;
                foreach (var other in _entries)
                {
                    if (other == entry)
                    {
                        break;
                    }
                    if (other.Card is not null && lane.Children.Contains(other.Card))
                    {
                        index++;
                    }
                }
                lane.Children.Insert(Math.Min(index, lane.Children.Count), entry.Card);
            }
        }
        entry.Card.SetPreview(preview, Labels);
    }

    private void Expand(LinkUnfurlEntry entry, bool announce)
    {
        if (entry.Link.Status != LinkUnfurlStatus.Ready || entry.Link.Preview is not { } preview || entry.Card is not { } card ||
            entry.IsLeaving || entry.IsRemoved || entry.Link.IsExpanded)
        {
            return;
        }
        entry.Link.IsExpanded = true;
        card.SetShown(true);
        AnimateHot(entry, 1);
        _ = FoldTo(entry, 1, velocity: null);
        UpdateEditorStatus();
        UpdateSampleButtons();
        if (announce)
        {
            Announce(Labels.AnnounceShown(preview.Site, preview.Title));
        }
        RaiseEvent(new LinkUnfurlLinkEventArgs(PreviewToggledEvent, entry.Link));
    }

    private void Collapse(LinkUnfurlEntry entry, bool announce, double? velocity = null)
    {
        if (!entry.Link.IsExpanded || entry.IsLeaving)
        {
            return;
        }
        entry.Link.IsExpanded = false;
        AnimateHot(entry, 0);
        if (entry.Card?.IsKeyboardFocusWithin == true)
        {
            FocusEditor();
        }
        _ = FoldTo(entry, 0, velocity);
        UpdateEditorStatus();
        UpdateSampleButtons();
        if (announce && entry.Link.Preview is { } preview)
        {
            Announce(Labels.AnnounceCollapsed(preview.Title));
        }
        RaiseEvent(new LinkUnfurlLinkEventArgs(PreviewToggledEvent, entry.Link));
    }

    /// <summary>
    /// Springs a card's progress to 0 or 1 (open k 110 c 23, fold k 224 c 34, both time-scaled by <see cref="Speed"/>),
    /// keeping the current velocity; instant when paused or not at full motion. A card that ends folded hides.
    /// </summary>
    private async Task FoldTo(LinkUnfurlEntry entry, double target, double? velocity)
    {
        bool reached;
        if (IsInstant)
        {
            entry.Progress.Jump(target);
            reached = true;
        }
        else
        {
            var spec = LinkUnfurlMorph.Scaled(target > 0.5 ? LinkUnfurlMorph.Open : LinkUnfurlMorph.Fold, Speed);
            reached = await entry.Progress.AnimateTo(target, MotionSpec.Spring(spec), velocity);
        }
        if (reached && target <= 0 && !entry.Link.IsExpanded && !entry.IsDragging)
        {
            entry.Card?.SetShown(false);
        }
        _lane?.InvalidateMeasure();
    }

    /// <summary>The link's accent tint follows its card in 160ms (standard); a color change, so reduced motion keeps it.</summary>
    private void AnimateHot(LinkUnfurlEntry entry, double target) =>
        _ = entry.Hot.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));

    /// <summary>The lane placed a card in <paramref name="slot"/>: put it on its link for the current progress.</summary>
    internal void PlaceCard(LinkUnfurlCardHost host, Rect slot, Visual lane)
    {
        var entry = host.Entry;
        var p = entry.Progress.Value;
        Rect chip;
        if (!entry.FoldsInPlace && _editor is { } editor && TokenStart(entry) is { } start && editor.TryGetTokenBounds(start, lane, out var box))
        {
            chip = LinkUnfurlChipRenderer.Pill(box);
        }
        else
        {
            // The link left the text: the card folds into a pill at its own top left.
            chip = new Rect(slot.X, slot.Y, 132, LinkUnfurlMorph.LinkHeight);
        }
        var frame = LinkUnfurlMorph.Compute(chip, slot, p);
        var resting = p >= 0.9999 && !entry.IsDragging && !entry.Progress.IsAnimating;
        host.Apply(frame, slot, resting);
    }

    private int? TokenStart(LinkUnfurlEntry entry)
    {
        if (_editor is null)
        {
            return null;
        }
        foreach (var token in _editor.Tokens)
        {
            if (token.Id == entry.Id)
            {
                return token.Start;
            }
        }
        return null;
    }

    /// <summary>The card title was pressed: open the address with the platform launcher.</summary>
    internal void OpenLink(LinkUnfurlEntry entry)
    {
        if (Uri.TryCreate(entry.Link.Url, UriKind.Absolute, out var uri) && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    #endregion

    #region Dragging

    internal void OnCardDragStarted(LinkUnfurlEntry entry, DragGestureEventArgs e)
    {
        _interacted = true;
        entry.IsDragging = true;
        entry.Progress.Stop();
        _dragStart = entry.Progress.Value;
        _dragHeight = entry.Card?.Bounds.Height ?? 300;
        PseudoClasses.Set(DraggingPseudoClass, true);
    }

    internal void OnCardDragMoved(LinkUnfurlEntry entry, DragGestureEventArgs e)
    {
        if (!entry.IsDragging)
        {
            return;
        }
        entry.Progress.Jump(LinkUnfurlDragFold.Progress(_dragStart, e.Offset.Y, _dragHeight));
    }

    internal void OnCardDragEnded(LinkUnfurlEntry entry, DragGestureEventArgs e, bool cancelled)
    {
        if (!entry.IsDragging)
        {
            return;
        }
        entry.IsDragging = false;
        PseudoClasses.Set(DraggingPseudoClass, false);
        var velocity = cancelled ? 0 : e.Velocity.Y;
        var progressVelocity = LinkUnfurlDragFold.ProgressVelocity(velocity, _dragHeight);
        if (!cancelled && LinkUnfurlDragFold.ShouldFold(entry.Progress.Value, velocity))
        {
            Collapse(entry, announce: true, velocity: progressVelocity);
        }
        else
        {
            _ = FoldTo(entry, 1, progressVelocity);
        }
    }

    #endregion

    #region Samples and names

    private void RebuildSamples()
    {
        if (_samples is not { } panel)
        {
            return;
        }
        for (var i = panel.Children.Count - 1; i >= 0; i--)
        {
            if (panel.Children[i] is LinkUnfurlSampleButton button)
            {
                button.Click -= OnSampleClick;
                panel.Children.RemoveAt(i);
            }
        }
        var samples = Samples;
        var any = samples is { Count: > 0 };
        panel.IsVisible = any;
        if (samples is not null)
        {
            foreach (var sample in samples)
            {
                var button = new LinkUnfurlSampleButton(sample);
                button.Click += OnSampleClick;
                panel.Children.Add(button);
            }
        }
        UpdateSampleButtons();
    }

    private void OnSampleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not LinkUnfurlSampleButton button)
        {
            return;
        }
        e.Handled = true;
        _interacted = true;
        _autoPlay?.Cancel();
        if (FindByAddress(button.Preview.Url) is { } entry)
        {
            Activate(entry);
            return;
        }
        AddLink(button.Preview.Url, atEnd: false);
    }

    /// <summary>Names and marks the sample buttons after their links' states.</summary>
    private void UpdateSampleButtons()
    {
        if (_samples is null)
        {
            return;
        }
        foreach (var child in _samples.Children)
        {
            if (child is not LinkUnfurlSampleButton button)
            {
                continue;
            }
            var site = button.Preview.Site;
            var entry = FindByAddress(button.Preview.Url);
            button.IsActive = entry is not null;
            var name = entry switch
            {
                null => Labels.SamplePaste(site),
                { Link.Status: LinkUnfurlStatus.Loading } => Labels.SampleLoading(site),
                { Link.IsExpanded: true } => Labels.SampleCollapse(site),
                { Link.Status: LinkUnfurlStatus.Ready } => Labels.SampleShow(site),
                _ => Labels.SamplePaste(site)
            };
            AutomationProperties.SetName(button, name);
        }
    }

    /// <summary>The message field's status for assistive technology: the links' names (address while loading, title once ready).</summary>
    private void UpdateEditorStatus()
    {
        if (_editor is null)
        {
            return;
        }
        if (_entries.Count == 0)
        {
            AutomationProperties.SetItemStatus(_editor, null);
            return;
        }
        var parts = new List<string>(_entries.Count);
        foreach (var entry in _entries)
        {
            parts.Add(LinkName(entry));
        }
        AutomationProperties.SetItemStatus(_editor, string.Join("; ", parts));
    }

    /// <summary>A link's accessible name.</summary>
    internal string LinkName(LinkUnfurlEntry entry) => entry.Link switch
    {
        { Status: LinkUnfurlStatus.Ready, Preview: { } preview } => Labels.LinkReady(preview.Title, preview.Site),
        { Status: LinkUnfurlStatus.Failed } => Labels.LinkFailed(LinkUnfurlAddress.ShortForm(entry.Link.Url)),
        _ => Labels.LinkLoading(LinkUnfurlAddress.ShortForm(entry.Link.Url))
    };

    private void Announce(string text) => _announcer?.Announce(text);

    #endregion
}
