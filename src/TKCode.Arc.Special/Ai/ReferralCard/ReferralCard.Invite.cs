using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>What the hint under the email field says.</summary>
internal enum ReferralCardHintKind
{
    Idle,
    Ready,
    Sending,
    Sent,
    Error
}

// The invite-by-email field: chips, validation, the hint and sending.
public partial class ReferralCard
{
    private readonly List<ReferralCardChip> _chips = [];
    private ReferralCardHintKind _hintKind;
    private string? _hintText;
    private string? _shownHintKey;
    private bool _sending;
    private CancellationTokenSource? _sendCts;
    private int _localIds;

    /// <summary>Waits between beats of a simulation (tests replace it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (delay, token) => Task.Delay(delay, token);

    /// <summary>The addresses in the field, in order (tests).</summary>
    internal IReadOnlyList<string> ChipEmails => _chips.Select(c => c.Email).ToList();

    /// <summary>The chips (tests).</summary>
    internal IReadOnlyList<ReferralCardChip> Chips => _chips;

    /// <summary>The email editor (tests).</summary>
    internal ReferralCardEmailBox? Editor => _editor;

    /// <summary>The Send button (tests).</summary>
    internal Button? SendButton => _send;

    /// <summary>What the hint says now.</summary>
    internal (ReferralCardHintKind Kind, string Text) Hint => (_hintKind, HintText());

    /// <summary>True while invites are going out.</summary>
    internal bool IsSending => _sending;

    // ------------------------------------------------------------------ editor callbacks

    /// <summary>Enter, Tab and Backspace in the email field. Returns true when the key was used.</summary>
    internal bool HandleEmailKey(Key key, KeyModifiers modifiers)
    {
        if (_editor is null || _sending)
        {
            return key == Key.Enter && _sending;
        }
        var draft = _editor.Text ?? string.Empty;
        switch (key)
        {
            case Key.Enter:
                if (draft.Trim().Length > 0)
                {
                    CommitDraft();
                }
                else
                {
                    _ = SendAsync();
                }
                return true;
            case Key.Tab when draft.Trim().Length > 0 && (modifiers & KeyModifiers.Shift) == 0:
                CommitDraft();
                return true;
            case Key.Back when draft.Length == 0 && _editor.SelectionStart == 0 && _chips.Count > 0:
                RemoveChip(_chips[^1], focus: false);
                return true;
            default:
                return false;
        }
    }

    /// <summary>A comma, semicolon or space ends an address; text with separators (an IME commit) is added like a paste.</summary>
    internal bool HandleEmailText(string text)
    {
        if (_sending)
        {
            return true;
        }
        if (text is "," or ";" or " ")
        {
            CommitDraft();
            return true;
        }
        return text.IndexOfAny(ReferralCardFormat.Separators) >= 0 && HandleEmailPaste(text);
    }

    /// <summary>Pasted addresses separated by commas, spaces or new lines all become chips. Returns false for a single word.</summary>
    internal bool HandleEmailPaste(string text)
    {
        if (_editor is null || _sending || text.Trim().IndexOfAny(ReferralCardFormat.Separators) < 0)
        {
            return false;
        }
        var draft = _editor.Text ?? string.Empty;
        _editor.Text = string.Empty;
        AddAddresses(ReferralCardFormat.Split(draft + " " + text));
        return true;
    }

    internal void OnEmailLostFocus()
    {
        if (!_sending && !string.IsNullOrWhiteSpace(_editor?.Text))
        {
            CommitDraft();
        }
    }

    /// <summary>The typed text becomes chips (several when it holds separators).</summary>
    internal void CommitDraft()
    {
        if (_editor is null)
        {
            return;
        }
        var draft = _editor.Text ?? string.Empty;
        _editor.Text = string.Empty;
        AddAddresses(ReferralCardFormat.Split(draft));
    }

    /// <summary>
    /// Adds addresses as chips. A duplicate, an address already in the list of people and anything past
    /// <see cref="MaxInvites"/> is refused with a note; an address that is not valid becomes a marked chip and the hint says
    /// why. Returns how many chips were added.
    /// </summary>
    internal int AddAddresses(IReadOnlyList<string> addresses)
    {
        var labels = Labels;
        string? error = null;
        var added = 0;
        foreach (var address in addresses)
        {
            if (_chips.Any(c => string.Equals(c.Email, address, StringComparison.OrdinalIgnoreCase)))
            {
                error ??= labels.ErrorDuplicate(address);
                continue;
            }
            if (Items.Any(r => string.Equals(r.Email, address, StringComparison.OrdinalIgnoreCase)))
            {
                error ??= labels.ErrorAlreadyInvited(address);
                continue;
            }
            if (_chips.Count >= MaxInvites)
            {
                error = labels.ErrorLimit(MaxInvites);
                break;
            }
            var valid = ReferralCardFormat.IsEmail(address);
            AddChip(address, valid);
            added++;
            if (!valid)
            {
                error ??= labels.ErrorInvalid(address);
            }
        }
        if (error is not null)
        {
            SetHint(ReferralCardHintKind.Error, error);
        }
        else if (added > 0 || _hintKind != ReferralCardHintKind.Error)
        {
            SetHint(_chips.Count > 0 ? ReferralCardHintKind.Ready : ReferralCardHintKind.Idle, null);
        }
        if (added > 0)
        {
            ScrollChipsToEnd();
        }
        return added;
    }

    private void AddChip(string email, bool valid)
    {
        var chip = new ReferralCardChip(email, valid, Labels);
        chip.Remove.Click += (_, _) => RemoveChip(chip, focus: true);
        _chips.Add(chip);
        _chipPanel?.Children.Insert(Math.Max(0, _chipPanel.Children.Count - 1), chip);
    }

    /// <summary>Removes a chip (its button, or Backspace in the empty field).</summary>
    internal void RemoveChip(ReferralCardChip chip, bool focus)
    {
        if (_sending || !_chips.Remove(chip))
        {
            return;
        }
        _chipPanel?.Children.Remove(chip);
        var invalid = _chips.FirstOrDefault(c => !c.IsValid);
        if (invalid is not null)
        {
            SetHint(ReferralCardHintKind.Error, Labels.ErrorInvalid(invalid.Email));
        }
        else
        {
            SetHint(_chips.Count > 0 ? ReferralCardHintKind.Ready : ReferralCardHintKind.Idle, null);
        }
        if (focus)
        {
            _editor?.Focus(NavigationMethod.Unspecified);
        }
    }

    private void ScrollChipsToEnd()
    {
        var scroller = _chipScroller;
        if (scroller is null)
        {
            return;
        }
        Dispatcher.UIThread.Post(() => scroller.Offset = new Vector(Math.Max(0, scroller.Extent.Width - scroller.Viewport.Width), 0),
            DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------ sending

    /// <summary>
    /// Sends the chips: the typed text first becomes chips; nothing to send or an invalid chip refuses with a note. While it
    /// runs the field turns read only (focus stays) and Send shows its spinner. On success the addresses join the top of the
    /// list as Invited; a failure keeps the chips and shows the reason.
    /// </summary>
    internal async Task SendAsync()
    {
        if (_sending || _editor is null)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(_editor.Text))
        {
            CommitDraft();
        }
        var labels = Labels;
        if (_chips.Count == 0)
        {
            if (_hintKind != ReferralCardHintKind.Error)
            {
                SetHint(ReferralCardHintKind.Error, labels.ErrorEmpty);
            }
            return;
        }
        if (_chips.Any(c => !c.IsValid))
        {
            SetHint(ReferralCardHintKind.Error, labels.ErrorFixChips);
            return;
        }
        var emails = _chips.Select(c => c.Email).ToList();
        var handler = InviteHandler;
        SetSending(true);
        SetHint(ReferralCardHintKind.Sending, labels.HintSending(emails.Count));
        var cts = new CancellationTokenSource();
        _sendCts = cts;
        try
        {
            if (handler is not null)
            {
                await handler(emails, cts.Token);
            }
            else
            {
                await Delay(SimulatedSendTime, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Finish(cts);
            SetHint(_chips.Count > 0 ? ReferralCardHintKind.Ready : ReferralCardHintKind.Idle, null);
            return;
        }
        catch (Exception exception)
        {
            Finish(cts);
            SetHint(ReferralCardHintKind.Error, string.IsNullOrWhiteSpace(exception.Message) ? labels.ErrorSend : exception.Message);
            return;
        }
        Finish(cts);

        foreach (var chip in _chips)
        {
            _chipPanel?.Children.Remove(chip);
        }
        _chips.Clear();
        var now = DateTimeOffset.Now;
        var fresh = new List<Referral>(emails.Count);
        foreach (var email in emails)
        {
            fresh.Add(new Referral { Id = "local-" + ++_localIds, Email = email, Stage = ReferralStage.Invited, InvitedAt = now });
        }
        EnsureItems();
        _items.InsertRange(0, fresh);
        var simulated = handler is null;
        SetHint(ReferralCardHintKind.Sent, simulated ? labels.HintSimulated(emails.Count) : labels.HintSent(emails.Count));
        Refresh(animate: true);
        _listScroller?.ScrollToHome();
        RaiseEvent(new ReferralInvitesEventArgs(InvitesSentEvent, emails, simulated));
        if (simulated && Simulates)
        {
            _ = AdvanceAsync(fresh[0].Id, CurrentProgramToken());
        }
    }

    private void Finish(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_sendCts, cts))
        {
            _sendCts = null;
        }
        cts.Dispose();
        SetSending(false);
    }

    private void SetSending(bool sending)
    {
        _sending = sending;
        PseudoClasses.Set(SendingPseudoClass, sending);
        if (_editor is not null)
        {
            _editor.IsReadOnly = sending;
        }
        if (_send is not null)
        {
            _send.IsLoading = sending;
        }
        foreach (var chip in _chips)
        {
            chip.Remove.IsEnabled = !sending;
        }
    }

    // ------------------------------------------------------------------ hint

    private void SetHint(ReferralCardHintKind kind, string? text)
    {
        _hintKind = kind;
        _hintText = text;
        RefreshHint(animate: true);
        if (kind is ReferralCardHintKind.Error or ReferralCardHintKind.Sent or ReferralCardHintKind.Sending)
        {
            Announce(HintText());
        }
    }

    private string HintText()
    {
        var labels = Labels;
        return _hintKind switch
        {
            ReferralCardHintKind.Ready => labels.HintReady(_chips.Count),
            ReferralCardHintKind.Idle => labels.HintIdle,
            _ => _hintText ?? string.Empty
        };
    }

    /// <summary>The hint crossfades once per change: an alert glyph plus the words for an error, never color alone.</summary>
    private void RefreshHint(bool animate)
    {
        if (_hint is null)
        {
            return;
        }
        var text = HintText();
        var error = _hintKind == ReferralCardHintKind.Error;
        _emailFrame!.IsInvalid = error;
        AutomationProperties.SetHelpText(_editor!, text);
        var key = _hintKind + "\u0001" + text;
        if (_shownHintKey == key)
        {
            return;
        }
        _shownHintKey = key;
        var words = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.LabelFontSize)
            .ReferralArc(TextBlock.ForegroundProperty, error ? ArcDesignTokenKind.Danger
                : _hintKind == ReferralCardHintKind.Sent ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.TextMuted);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (error || _hintKind == ReferralCardHintKind.Sent)
        {
            row.Children.Add(new ArcGlyph { Data = error ? ArcIcons.CircleAlert : ArcIcons.CircleCheck, VerticalAlignment = VerticalAlignment.Center }
                .Tok(ArcGlyph.SizeProperty, ReferralCardTokenKind.HintGlyphSize)
                .ReferralArc(ArcGlyph.ForegroundProperty, error ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Success));
        }
        row.Children.Add(words);
        AutomationProperties.SetName(row, text);
        _hint.Swap(row, animate && IsLoaded);
    }
}
