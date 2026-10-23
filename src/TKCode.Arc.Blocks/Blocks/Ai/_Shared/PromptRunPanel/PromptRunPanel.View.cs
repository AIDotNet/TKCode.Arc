using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

internal partial class PromptRunPanel
{
    private static readonly Easing s_linear = new LinearEasing();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_replySwap is not null)
        {
            _replySwap.Content = null;
        }
        if (_holdHooked && _size is not null)
        {
            _size.LayoutUpdated -= OnHeldLayout;
            _holdHooked = false;
        }
        if (_idleText is not null)
        {
            _idleText.Inlines?.Clear();
        }
        _size = e.NameScope.Find<SpringSizeDecorator>(SizePart);
        _idle = e.NameScope.Find<Control>(IdlePart);
        _idleText = e.NameScope.Find<TextBlock>(IdleTextPart);
        _run = e.NameScope.Find<Control>(RunPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _replySwap = e.NameScope.Find<ArcContentSwap>(ReplyPart);
        _error = e.NameScope.Find<Control>(ErrorPart);
        _retry = e.NameScope.Find<Button>(RetryPart);
        _footer = e.NameScope.Find<ArcContentSwap>(FooterPart);
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_idleText is not null)
        {
            _idleText.Inlines ??= new InlineCollection();
            _idleText.Inlines.Add(_idleBefore);
            _idleText.Inlines.Add(_idleCase);
            _idleText.Inlines.Add(_idleAfter);
        }
        if (_replySwap is not null)
        {
            _replySwap.Content = _reply;
        }
        if (_holding)
        {
            Hold();
        }
        // A new template shows the current state at once.
        _statusKey = null;
        _footerShown = null;
        UpdateIdleText();
        UpdateView(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            var state = change.GetNewValue<PromptRunState>();
            PseudoClasses.Set(IdlePseudoClass, state == PromptRunState.Idle);
            PseudoClasses.Set(ThinkingPseudoClass, state == PromptRunState.Thinking);
            PseudoClasses.Set(StreamingPseudoClass, state == PromptRunState.Streaming);
            PseudoClasses.Set(DonePseudoClass, state == PromptRunState.Done);
            PseudoClasses.Set(StoppedPseudoClass, state == PromptRunState.Stopped);
            PseudoClasses.Set(FailedPseudoClass, state == PromptRunState.Failed);
            if (state is PromptRunState.Idle or PromptRunState.Done or PromptRunState.Stopped or PromptRunState.Failed)
            {
                // The run ended (or never was): its own height counts now.
                Release();
            }
            UpdateView(animate: true, previous: change.GetOldValue<PromptRunState>());
        }
        else if (change.Property == StatusTextProperty)
        {
            UpdateStatus(animate: true);
        }
        else if (change.Property == CaseTextProperty)
        {
            UpdateIdleText();
            UpdateStatus(animate: true);
        }
        else if (change.Property == IdleTextProperty)
        {
            UpdateIdleText();
        }
        else if (change.Property == FooterTextProperty)
        {
            UpdateFooter(animate: true);
        }
        else if (change.Property == TextProperty)
        {
            if (_writingText == 0)
            {
                ShowWholeText(change.GetNewValue<string>() ?? string.Empty);
            }
        }
        else if (change.Property == AccessibleNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            // The shimmer is only drawn at full motion; reduced and off show the plain status.
            UpdateStatus(animate: false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The motion level is inherited: a panel built before it joined a window may have chosen the wrong status look.
        UpdateStatus(animate: false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    #region View

    /// <summary>Shows the idle line or the run view and brings the head, the error box, the copy button and the footer in line with <see cref="State"/>.</summary>
    private void UpdateView(bool animate, PromptRunState? previous = null)
    {
        var state = State;
        var idle = state == PromptRunState.Idle;
        animate &= IsLoaded;
        if (_idle is not null && _idle.IsVisible != idle)
        {
            _idle.IsVisible = idle;
            if (idle && animate)
            {
                FadeIn(_idle);
            }
        }
        if (_run is not null && _run.IsVisible == idle)
        {
            _run.IsVisible = !idle;
            if (!idle && animate)
            {
                FadeIn(_run);
            }
        }
        // Entering the run view from idle: the head and footer appear with the view, not swapped in again.
        var swap = animate && previous is not null && previous != PromptRunState.Idle;
        UpdateStatus(swap);
        UpdateFooter(swap);
        if (_error is not null)
        {
            var failed = state == PromptRunState.Failed;
            if (_error.IsVisible != failed)
            {
                _error.IsVisible = failed;
                if (failed && animate)
                {
                    FadeIn(_error);
                }
            }
        }
        UpdateCopy();
    }

    /// <summary>The run view (or the idle line, or the error box) fades in: 160 ms on the enter curve, 120 ms linear when reduced.</summary>
    private void FadeIn(Control control)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            control.Opacity = 1;
            return;
        }
        // Code-driven: the part appears because the run state changed, and the state change also hides the part it
        // replaces in the same layout pass, which a ControlTheme transition (fading a part that is already shown) cannot do.
        control.Opacity = 0;
        var fade = new MotionValue<double>(control, 0, MotionVectors.Double);
        fade.Changed += v => control.Opacity = v;
        _ = Run();

        async Task Run()
        {
            await fade.AnimateTo(1, level == ArcMotionLevel.Full
                ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter)
                : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
            control.Opacity = 1;
            fade.Dispose();
        }
    }

    private void UpdateIdleText()
    {
        var text = IdleText ?? string.Empty;
        var label = CaseText;
        var at = string.IsNullOrEmpty(label) ? -1 : text.IndexOf(label, StringComparison.Ordinal);
        if (at < 0)
        {
            _idleBefore.Text = text;
            _idleCase.Text = string.Empty;
            _idleAfter.Text = string.Empty;
        }
        else
        {
            _idleBefore.Text = text[..at];
            _idleCase.Text = label;
            _idleAfter.Text = text[(at + label!.Length)..];
        }
        if (_idleText is not null)
        {
            AutomationProperties.SetName(_idleText, text);
        }
    }

    /// <summary>
    /// The head's status row for the state, swapped in Arc's label mode when it changes: Thinking and Writing as a
    /// shimmer (plain secondary text below full motion), Reply with a green check, Stopped with a filled square, Run
    /// failed with a red alert circle; then "· case" in muted text.
    /// </summary>
    private void UpdateStatus(bool animate)
    {
        if (_status is null)
        {
            return;
        }
        var state = State;
        var busy = state is PromptRunState.Thinking or PromptRunState.Streaming;
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        // Thinking and Writing look alike but for their words: the state alone changing (the owner sets the state, then
        // the words) does not swap the row in again.
        var key = (busy ? PromptRunState.Thinking : state, StatusText, CaseText, full && busy);
        if (_statusKey == key)
        {
            return;
        }
        _statusKey = key;
        AutomationProperties.SetName(_status, string.IsNullOrEmpty(CaseText) ? StatusText : $"{StatusText} · {CaseText}");
        SetSwapContent(_status, state == PromptRunState.Idle ? null : BuildStatus(state, key.Item4), animate);
    }

    private Control BuildStatus(PromptRunState state, bool shimmer)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        switch (state)
        {
            case PromptRunState.Done:
            {
                var check = new ArcGlyph { Data = ArcIcons.Check, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
                row.Children.Add(check);
                break;
            }
            case PromptRunState.Stopped:
            {
                var square = new PromptRunGlyph { Data = ArcIcons.Square, Width = 11, Height = 11, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(square, PromptRunGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                row.Children.Add(square);
                break;
            }
            case PromptRunState.Failed:
            {
                var alert = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(alert, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
                row.Children.Add(alert);
                break;
            }
        }
        if (shimmer)
        {
            row.Children.Add(new TextShimmer { Text = StatusText ?? string.Empty, IsActive = true, VerticalAlignment = VerticalAlignment.Center });
        }
        else
        {
            // Natural line boxes, like the shimmer's: centered in the 36px head, the baselines line up.
            var text = new TextBlock
            {
                Text = StatusText,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            row.Children.Add(text);
        }
        if (!string.IsNullOrEmpty(CaseText))
        {
            var label = new TextBlock
            {
                Text = "· " + CaseText,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            row.Children.Add(label);
        }
        return row;
    }

    /// <summary>The footer line in tabular figures, swapped in label mode when its words change.</summary>
    private void UpdateFooter(bool animate)
    {
        if (_footer is null)
        {
            return;
        }
        var text = State == PromptRunState.Idle || string.IsNullOrEmpty(FooterText) ? null : FooterText;
        PseudoClasses.Set(HasFooterPseudoClass, text is not null);
        _footer.IsVisible = text is not null;
        if (string.Equals(_footerShown, text, StringComparison.Ordinal) && _footer.Content is not null == text is not null)
        {
            return;
        }
        _footerShown = text;
        AutomationProperties.SetName(_footer, text);
        Control? line = null;
        if (text is not null)
        {
            var block = new TextBlock
            {
                Text = text,
                FontSize = 12,
                LineHeight = 16.8,
                LetterSpacing = 0,
                FontFeatures = s_tabular,
                TextWrapping = TextWrapping.Wrap
            };
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            line = block;
        }
        SetSwapContent(_footer, line, animate && _footer.Content is not null && line is not null);
    }

    private static readonly FontFeatureCollection s_tabular = [FontFeature.Parse("tnum")];

    /// <summary>Copy reply shows only for a reply that is done or stopped and has text.</summary>
    private void UpdateCopy()
    {
        var canCopy = State is PromptRunState.Done or PromptRunState.Stopped && _reply.HasText;
        PseudoClasses.Set(CanCopyPseudoClass, canCopy);
        if (_copy is not null)
        {
            _copy.Value = canCopy ? _reply.Text : null;
        }
    }

    /// <summary>Sets a swap's content; without <paramref name="animate"/> the change is put in place without motion.</summary>
    private static void SetSwapContent(ArcContentSwap swap, Control? content, bool animate)
    {
        if (animate)
        {
            swap.Content = content;
            return;
        }
        // SetCurrentValue keeps the theme's Mode the value a style can still change.
        var mode = swap.Mode;
        swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
        swap.Content = content;
        swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
    }

    private void OnCopied(object? sender, EventArgs e) => RaiseEvent(new RoutedEventArgs(CopyRequestedEvent, this));

    private void OnRetryClick(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RetryRequestedEvent, this));

    #endregion

    /// <summary>A group named by <see cref="AccessibleName"/>; its status, reply, error and buttons are read as they are.</summary>
    private sealed class PanelPeer(PromptRunPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
