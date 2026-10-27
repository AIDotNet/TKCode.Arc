using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class RolesPermissions
{
    #region Save

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SaveCore(fromButton: true);
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DiscardCore(focus: true);
    }

    private RolesPermissionsSaveRequest BuildRequest()
        => new(new HashSet<RolesPermissionsGrant>(_model.Current), new HashSet<RolesPermissionsGrant>(_model.Saved),
            _model.Changes(_roles, _permissionKeys), _roles.ToArray(), _roles.Where(r => r.IsNew).ToArray(), PendingChangeCount);

    private async Task<bool> SaveCore(bool fromButton)
    {
        if (SaveState == RolesPermissionsSaveState.Saving || PendingChangeCount == 0)
        {
            return false;
        }
        _menu?.Close(returnFocus: false);
        var request = BuildRequest();
        var starting = new RolesPermissionsSaveStartingEventArgs(SaveStartingEvent, this, request);
        RaiseEvent(starting);
        if (starting.Cancel || SaveState == RolesPermissionsSaveState.Saving)
        {
            return false;
        }
        var generation = ++_saveGeneration;
        var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        ErrorText = null;
        SetSaveState(RolesPermissionsSaveState.Saving);
        UpdateBar(animate: true);
        _announcer?.Announce(ProjectsText.Format(Labels.SavingFormat, Labels.CountText(request.ChangeCount)));
        try
        {
            if (SaveHandler is { } handler)
            {
                await handler(request, cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (generation == _saveGeneration)
            {
                SetSaveState(RolesPermissionsSaveState.Idle);
                UpdateBar(animate: true);
            }
            return false;
        }
        catch (Exception exception)
        {
            if (generation != _saveGeneration)
            {
                return false;
            }
            var text = ErrorFormatter?.Invoke(exception);
            ErrorText = string.IsNullOrWhiteSpace(text)
                ? string.IsNullOrWhiteSpace(exception.Message) ? Labels.FailedText : exception.Message
                : text;
            SetSaveState(RolesPermissionsSaveState.Failed);
            UpdateBar(animate: true);
            _alert?.Announce(ProjectsText.Format(Labels.FailedFormat, ErrorText));
            RaiseEvent(new RolesPermissionsSaveFailedEventArgs(SaveFailedEvent, this, request, exception));
            return false;
        }
        finally
        {
            if (ReferenceEquals(_saveCancellation, cancellation))
            {
                _saveCancellation = null;
            }
            cancellation.Dispose();
        }
        if (generation != _saveGeneration)
        {
            return false;
        }

        _model.Commit();
        foreach (var role in _roles)
        {
            role.IsNew = false;
        }
        _syncingGrants = true;
        try
        {
            SetCurrentValue(SavedGrantsProperty, new HashSet<RolesPermissionsGrant>(_model.Saved));
            SetCurrentValue(GrantsProperty, new HashSet<RolesPermissionsGrant>(_model.Current));
        }
        finally
        {
            _syncingGrants = false;
        }
        SetSaveState(RolesPermissionsSaveState.Saved);
        // Every dot leaves at once (no ripple) as the bar turns to "saved".
        Refresh(null, animate: true);
        _announcer?.Announce(ProjectsText.Format(Labels.SavedFormat, Labels.CountText(request.ChangeCount)));
        RaiseEvent(new RolesPermissionsSavedEventArgs(SavedEvent, this, request));
        _ = HoldThenClose(generation, SavedHoldDuration ?? SavedHold);
        return true;
    }

    private async Task HoldThenClose(int generation, TimeSpan hold)
    {
        if (hold > TimeSpan.Zero)
        {
            await Task.Delay(hold);
        }
        if (generation != _saveGeneration || SaveState != RolesPermissionsSaveState.Saved)
        {
            return;
        }
        SetSaveState(RolesPermissionsSaveState.Idle);
        UpdateBar(animate: true);
    }

    private void SetSaveState(RolesPermissionsSaveState state)
    {
        SaveState = state;
        PseudoClasses.Set(SavingPseudoClass, state == RolesPermissionsSaveState.Saving);
        PseudoClasses.Set(SavedPseudoClass, state == RolesPermissionsSaveState.Saved);
        PseudoClasses.Set(FailedPseudoClass, state == RolesPermissionsSaveState.Failed);
    }

    #endregion

    #region Review bar

    private void UpdateBar(bool animate)
    {
        var open = HasUnsavedChanges || SaveState is RolesPermissionsSaveState.Saving or RolesPermissionsSaveState.Saved;
        if (open)
        {
            UpdateBarContent(_barOpen && animate);
        }
        SetBarOpen(open, animate);
    }

    private void UpdateBarContent(bool animate)
    {
        var labels = Labels;
        var kind = SaveState switch
        {
            RolesPermissionsSaveState.Saved => BarTitleKind.Saved,
            RolesPermissionsSaveState.Failed => BarTitleKind.Failed,
            _ => BarTitleKind.Pending,
        };
        if (_barTitle is not null && (kind != _barTitleKind || (kind == BarTitleKind.Pending && _countDigits is null)))
        {
            _barTitleKind = kind;
            _barTitle.Content = kind switch
            {
                BarTitleKind.Saved => TitleWithGlyph(ArcIcons.Check, DesignTokens.ArcDesignTokenKind.Success, labels.SavedTitle),
                BarTitleKind.Failed => TitleWithGlyph(ArcIcons.CircleAlert, DesignTokens.ArcDesignTokenKind.Danger, labels.FailedTitle),
                _ => PendingTitle(),
            };
        }
        if (kind == BarTitleKind.Pending && _countDigits is not null)
        {
            var count = PendingChangeCount;
            if (count > 0)
            {
                _countDigits.Value = count;
                _changeWord!.Text = count == 1 ? labels.ChangeOne : labels.ChangeMany;
            }
            _pendingWord!.Text = labels.PendingWord;
            _pendingWord.IsVisible = !PseudoClasses.Contains(CompactPseudoClass);
        }
        var text = SaveState switch
        {
            RolesPermissionsSaveState.Saving => labels.Saving,
            RolesPermissionsSaveState.Saved => labels.SavedText,
            RolesPermissionsSaveState.Failed => ErrorText ?? labels.FailedText,
            _ => RolesPermissionsModel.Summary(ChangedRoles(), labels),
        };
        if (_barText is not null && !string.Equals(text, _barTextValue, StringComparison.Ordinal))
        {
            _barTextValue = text;
            _barText.Content = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, Classes = { "bar-text" } };
        }
        if (_save is not null)
        {
            object contentKind = SaveState switch
            {
                RolesPermissionsSaveState.Saved => labels.SavedButton + "✓",
                RolesPermissionsSaveState.Failed => labels.RetryButton,
                _ => labels.Save,
            };
            if (!Equals(contentKind, _saveContentKind))
            {
                _saveContentKind = contentKind;
                _save.Content = SaveState == RolesPermissionsSaveState.Saved ? SavedButtonContent(labels.SavedButton) : contentKind;
            }
            _save.IsLoading = SaveState == RolesPermissionsSaveState.Saving;
            AutomationProperties.SetName(_save, SaveState == RolesPermissionsSaveState.Saved ? labels.SavedButton : (string)contentKind);
        }
        if (_discard is not null)
        {
            _discard.Content = labels.Discard;
            _discard.IsEnabled = SaveState is RolesPermissionsSaveState.Idle or RolesPermissionsSaveState.Failed;
        }
    }

    private List<RolesPermissionsRole> ChangedRoles()
        => _roles.Where(r => _model.RoleHasChanges(r, _permissionKeys)).ToList();

    private Control PendingTitle()
    {
        var count = PendingChangeCount;
        _countDigits = new RollingDigits { Value = count, UseGrouping = true, VerticalAlignment = VerticalAlignment.Center, Classes = { "bar-count" } };
        _pendingWord = new TextBlock
        {
            Text = Labels.PendingWord,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = !PseudoClasses.Contains(CompactPseudoClass),
        };
        _changeWord = new TextMorph
        {
            Text = count == 1 ? Labels.ChangeOne : Labels.ChangeMany,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _changeWord.Bind(TextMorph.ForegroundProperty, _changeWord.GetResourceObservable(DesignTokens.ArcDesignTokenKind.Foreground));
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { _countDigits, _pendingWord, _changeWord } };
    }

    private Control TitleWithGlyph(Avalonia.Media.Geometry glyph, DesignTokens.ArcDesignTokenKind color, string text)
    {
        _countDigits = null;
        var icon = new ArcGlyph { Data = glyph, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        icon.Bind(ArcGlyph.ForegroundProperty, icon.GetResourceObservable(color));
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { icon, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
        };
    }

    private static Control SavedButtonContent(string text)
    {
        var icon = new ArcGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { icon, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
        };
    }

    /// <summary>
    /// Opens or closes the review bar. Code-driven because the slot's height (240ms tween), the bar's fade (160ms in,
    /// 240ms out) and its rise (smooth spring from 24 below, out to 16 below) run on different clocks, and the bar must
    /// stay in the tree until its exit finishes. Reduced and Off show and hide it at once, as Arc does.
    /// </summary>
    private void SetBarOpen(bool open, bool animate)
    {
        if (_bar is null || _barSlot is null)
        {
            _barOpen = open;
            return;
        }
        if (_slotHeight is null)
        {
            _slotHeight = new MotionValue<double>(this, 0, MotionVectors.Double);
            _slotHeight.Changed += v =>
            {
                if (_barSlot is not null)
                {
                    _barSlot.Height = Math.Max(0, v);
                }
            };
            _barOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
            _barOpacity.Changed += v =>
            {
                if (_bar is not null)
                {
                    _bar.Opacity = Math.Clamp(v, 0, 1);
                }
            };
            _barShift = new MotionValue<double>(this, 0, MotionVectors.Double);
            _barShift.Changed += v => _barTranslate.Y = v;
        }
        if (_barOpen == open)
        {
            return;
        }
        _barOpen = open;
        var full = animate && ArcMotion.IsFull(this) && (TopLevel.GetTopLevel(this) is not null);
        var slot = BarSlotHeight;
        if (open)
        {
            _bar.IsVisible = true;
            if (full)
            {
                _slotHeight.AnimateTo(slot, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
                _barOpacity!.Jump(0);
                _barOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
                _barShift!.Jump(24);
                _barShift.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _slotHeight.Jump(slot);
                _barOpacity!.Jump(1);
                _barShift!.Jump(0);
            }
            return;
        }
        var hadFocus = _bar.IsKeyboardFocusWithin;
        if (full)
        {
            _slotHeight.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            _barShift!.AnimateTo(16, MotionSpec.Spring(ArcSpring.Smooth));
            _ = _barOpacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard)).ContinueWith(_ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!_barOpen && _bar is not null)
                    {
                        _bar.IsVisible = false;
                    }
                }), TaskScheduler.Default);
        }
        else
        {
            _slotHeight.Jump(0);
            _barOpacity!.Jump(0);
            _barShift!.Jump(0);
            _bar.IsVisible = false;
        }
        if (hadFocus)
        {
            ReturnFocusToGrid();
        }
    }

    #endregion
}
