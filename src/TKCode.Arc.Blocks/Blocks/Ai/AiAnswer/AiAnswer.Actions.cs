using System.Globalization;
using System.Text;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class AiAnswer
{
    #region Asking from the page

    private void OnComposerSent(object? sender, AiComposerSentEventArgs e)
    {
        // The box clears itself and keeps the focus (Arc: the person can ask the next one at once).
        e.Handled = true;
        _ = Ask(e.Message.Text, AiAnswerQuestionOrigin.Composer);
    }

    private void OnComposerStopRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Run(StopCommand, null);
        Stop();
    }

    /// <summary>A related question or a suggestion of the empty page: asked in place, the focus moves to the new question.</summary>
    private void OnQuestionInvoked(object? sender, AiRelatedQuestionEventArgs e)
    {
        e.Handled = true;
        var origin = ReferenceEquals(e.Source, _suggestions) ? AiAnswerQuestionOrigin.Suggestion : AiAnswerQuestionOrigin.Related;
        if (Ask(e.Question, origin) is not null)
        {
            FocusQuestion();
        }
    }

    private void OnNewThreadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Run(NewThreadCommand, null);
        NewThread();
    }

    #endregion

    #region Answer again, the note

    private void OnRegenerateChosen(object? sender, AiRegenerateChosenEventArgs e)
    {
        e.Handled = true;
        Run(RegenerateCommand, e.Mode == AiAnswerMode.SwitchModel ? e.Model : e.Mode);
        Regenerate(e.Mode, e.Model);
    }

    /// <summary>The note's Try again: a new version in the same mode as before it ran out (Arc: retry).</summary>
    private void OnNoteActionRequested(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _note))
        {
            return;
        }
        e.Handled = true;
        Run(RegenerateCommand, AiAnswerMode.Retry);
        Regenerate(AiAnswerMode.Retry);
    }

    #endregion

    #region Copy and share

    /// <summary>Copy answer: the answer as plain text, then its sources ("[n] Title - domain - url").</summary>
    private void OnCopyRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = CopyAnswerAsync(BuildCopyText());
    }

    private async Task CopyAnswerAsync(string text)
    {
        try
        {
            await CopyTextAsync(text);
        }
        catch (Exception)
        {
            // The clipboard refused: nothing was copied, nothing is announced.
            return;
        }
        RaiseEvent(new AiAnswerCopiedEventArgs(CopiedEvent, this, AiAnswerCopyKind.Answer, text));
        Announce(Labels.AnnounceCopied);
    }

    /// <summary>The shown answer as plain text with a line per source after a blank line.</summary>
    internal string BuildCopyText()
    {
        var builder = new StringBuilder(_document?.PlainText ?? CurrentVersion?.Markdown ?? string.Empty);
        if (CurrentVersion is { Sources.Count: > 0 } version)
        {
            builder.Append("\n\n");
            for (var i = 0; i < version.Sources.Count; i++)
            {
                var source = version.Sources[i];
                if (i > 0)
                {
                    builder.Append('\n');
                }
                builder.Append('[').Append(source.Index.ToString(CultureInfo.InvariantCulture)).Append("] ").Append(source.Title);
                if (!string.IsNullOrEmpty(source.Domain))
                {
                    builder.Append(" - ").Append(source.Domain);
                }
                if (source.Url is { } url)
                {
                    builder.Append(" - ").Append(url.ToString());
                }
            }
        }
        return builder.ToString();
    }

    /// <summary>The action bar's link button: the link to the shown answer (its version index), copied; then the check.</summary>
    private void OnLinkRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = CopyLinkAsync(Math.Clamp(CurrentVersionIndex, 0, Math.Max(0, Versions.Count - 1)));
    }

    private async Task CopyLinkAsync(int versionIndex)
    {
        if (ShareLinkProvider is not { } provider)
        {
            return;
        }
        var info = new AiAnswerShareInfo(Question ?? string.Empty, CurrentVersion?.Markdown ?? string.Empty, versionIndex);
        string link;
        try
        {
            link = await provider(info, CancellationToken.None);
            await CopyTextAsync(link);
        }
        catch (Exception)
        {
            // No link or no clipboard: the button keeps its glyph (no check for a copy that did not happen).
            _actions?.ClearLinkCopied();
            return;
        }
        _actions?.ShowLinkCopied();
        RaiseEvent(new AiAnswerCopiedEventArgs(CopiedEvent, this, AiAnswerCopyKind.ShareLink, link));
        Announce(Labels.AnnounceLinkCopied);
    }

    /// <summary>The top bar's Share: the link to the whole thread (no version), copied; the pill says "Copied" for 1.6s.</summary>
    private void OnShareClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = ShareThreadAsync();
    }

    private async Task ShareThreadAsync()
    {
        if (ShareLinkProvider is not { } provider || IsEmptyPage)
        {
            return;
        }
        _shareCancel?.Cancel();
        var cancel = _shareCancel = new CancellationTokenSource();
        var info = new AiAnswerShareInfo(Question ?? string.Empty, CurrentVersion?.Markdown ?? string.Empty, null);
        string link;
        try
        {
            link = await provider(info, cancel.Token);
            await CopyTextAsync(link);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            // The pill says "Failed" for a moment: the person learns the link is not on the clipboard.
            if (!cancel.IsCancellationRequested)
            {
                SetShareState(ShareState.Failed);
            }
            return;
        }
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        SetShareState(ShareState.Copied);
        RaiseEvent(new AiAnswerCopiedEventArgs(CopiedEvent, this, AiAnswerCopyKind.ThreadLink, link));
        Announce(Labels.AnnounceLinkCopied);
    }

    /// <summary>A code block's Copy put its code on the clipboard.</summary>
    private void OnCodeCopied(object? sender, AiAnswerCopiedEventArgs e)
    {
        if (e.RoutedEvent != AiAnswerDocument.CodeCopiedEvent)
        {
            return;
        }
        e.Handled = true;
        RaiseEvent(new AiAnswerCopiedEventArgs(CopiedEvent, this, AiAnswerCopyKind.Code, e.Text));
    }

    #endregion

    #region Ratings

    /// <summary>A final rating of the shown version (up, down with its reason, or taken back): kept on the version and reported.</summary>
    private void OnActionsFeedbackChanged(object? sender, AiAnswerActionsFeedbackEventArgs e)
    {
        e.Handled = true;
        if (CurrentVersion is not { } version)
        {
            return;
        }
        version.Feedback = e.Value;
        version.FeedbackReason = e.Value == AiAnswerFeedbackValue.Down ? e.Reason : null;
        var index = Math.Clamp(CurrentVersionIndex, 0, Versions.Count - 1);
        RaiseEvent(new AiAnswerFeedbackEventArgs(FeedbackChangedEvent, this, e.Value, version.FeedbackReason, Question ?? string.Empty, version, index));
        Announce(e.Value switch
        {
            AiAnswerFeedbackValue.Up => Labels.AnnounceFeedback(true),
            AiAnswerFeedbackValue.Down => Labels.AnnounceFeedback(false),
            _ => Labels.AnnounceFeedbackRemoved
        });
    }

    private void OnActionsVersionChanged(object? sender, AiVersionPagerEventArgs e)
    {
        e.Handled = true;
        ShowVersionAt(e.Index, announce: true);
    }

    #endregion

    #region Sources and lighting

    /// <summary>A citation is pointed at or focused (or none is any more): its source card lights.</summary>
    private void OnCitationActivated(object? sender, AiAnswerLightEventArgs e)
    {
        e.Handled = true;
        if (_sources is not null)
        {
            _sources.ActiveNumber = e.SourceNumber;
        }
    }

    /// <summary>A source card is pointed at or focused (or none is any more): every sentence citing it lights.</summary>
    private void OnCardActivated(object? sender, AiSourceCardEventArgs e)
    {
        e.Handled = true;
        if (_document is not null)
        {
            _document.LitSourceNumber = e.SourceNumber;
        }
    }

    private void OnCitationInvoked(object? sender, AiAnswerSourceEventArgs e)
    {
        if (e.RoutedEvent != AiAnswerDocument.CitationInvokedEvent)
        {
            return;
        }
        e.Handled = true;
        InvokeSource(e.Item, fromCitation: true);
    }

    private void OnCardInvoked(object? sender, AiSourceCardEventArgs e)
    {
        e.Handled = true;
        if (e.Item is { } source)
        {
            InvokeSource(source, fromCitation: false);
        }
    }

    /// <summary><see cref="SourceInvoked"/>; unless it is handled, the source's address opens with the system launcher.</summary>
    private void InvokeSource(AiSource source, bool fromCitation)
    {
        var args = new AiAnswerSourceEventArgs(SourceInvokedEvent, this, source, fromCitation);
        RaiseEvent(args);
        if (!args.Handled && source.Url is { IsAbsoluteUri: true } url && url.Scheme is "http" or "https" or "mailto")
        {
            OpenUri(url);
        }
    }

    #endregion
}
