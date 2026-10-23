using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class PromptStudio
{
    /// <summary>The last promotion, while it can be undone: the version promoted and what production was before.</summary>
    private sealed record Promotion(PromptVersion Version, PromptVersion? PreviousValue, PromptVersion? Previous);

    private Promotion? _promotion;
    private bool _shownCompare;
    private PromptVersion? _shownCompareVersion;
    private string? _hintKey;

    #region Public methods

    /// <summary>
    /// Puts <paramref name="version"/> in production at once and raises <see cref="Promoted"/>; when the handler sets
    /// <c>Cancel</c> the previous version is put back. Promoting the version already in production does nothing.
    /// </summary>
    /// <param name="version">The version to promote.</param>
    public void Promote(PromptVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var previousValue = ProductionVersion;
        var previous = EffectiveProductionVersion;
        if (ReferenceEquals(version, previous))
        {
            return;
        }
        _promotion = null;
        SetCurrentValue(ProductionVersionProperty, version);
        var args = new PromptPromotedEventArgs(PromotedEvent, this, version, previous, isUndo: false);
        RaiseEvent(args);
        if (args.Cancel)
        {
            SetCurrentValue(ProductionVersionProperty, previousValue);
            return;
        }
        _promotion = new Promotion(version, previousValue, previous);
        Announce(Labels.AnnouncePromoted(version.Label));
    }

    /// <summary>
    /// Undoes the last <see cref="Promote"/>: the version it replaced is in production again and <see cref="Promoted"/> is
    /// raised with the arguments swapped and <c>IsUndo</c> set (<c>Cancel</c> keeps the promotion). Does nothing when there
    /// is nothing to undo or production has moved since.
    /// </summary>
    public void UndoPromote()
    {
        if (_promotion is not { } promotion || !ReferenceEquals(EffectiveProductionVersion, promotion.Version))
        {
            return;
        }
        _promotion = null;
        SetCurrentValue(ProductionVersionProperty, promotion.PreviousValue);
        if (EffectiveProductionVersion is not { } restored)
        {
            return;
        }
        var args = new PromptPromotedEventArgs(PromotedEvent, this, restored, promotion.Version, isUndo: true);
        RaiseEvent(args);
        if (args.Cancel)
        {
            SetCurrentValue(ProductionVersionProperty, promotion.Version);
            _promotion = promotion;
            return;
        }
        Announce(Labels.AnnouncePromotionUndone(restored.Label));
    }

    /// <summary>Shows the word diff of the draft against <paramref name="version"/> in place of the editor.</summary>
    /// <param name="version">The version to compare with.</param>
    public void Compare(PromptVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        SetCurrentValue(CompareVersionProperty, version);
        SetCurrentValue(IsCompareModeProperty, true);
    }

    /// <summary>Leaves the compare view (Escape and Back to editing do the same).</summary>
    public void ExitCompare() => LeaveCompare(focusTab: false);

    #endregion

    #region Header

    /// <summary>The version button: the open version's label, "● Production" or "Latest"; hidden without versions.</summary>
    private void UpdateHeader()
    {
        var open = EffectiveOpenVersion;
        var production = EffectiveProductionVersion;
        if (_versionPill is not null)
        {
            _versionPill.IsVisible = open is not null;
            _versionPill.Label = open?.Label;
            _versionPill.IsProduction = open is not null && ReferenceEquals(open, production);
        }
        if (_versionMenu is not null)
        {
            _versionMenu.OpenVersion = open;
            _versionMenu.ProductionVersion = production;
            _versionMenu.Now = Now;
            if (open is null && _versionMenu.IsOpen)
            {
                _versionMenu.Close();
            }
        }
    }

    /// <summary>A header button asks for its menu: it opens, or closes when open.</summary>
    private void OnPillOpenRequested(object? sender, PromptPillOpenRequestedEventArgs e)
    {
        if (ReferenceEquals(e.Source, _versionPill))
        {
            e.Handled = true;
            _versionMenu?.Toggle(e.Focus);
        }
        else if (ReferenceEquals(e.Source, _modelPill))
        {
            e.Handled = true;
            _modelMenu?.Toggle(e.Focus);
        }
    }

    private void OnVersionMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PromptPopoverMenu.IsOpenProperty && _versionPill is not null && _versionMenu is not null)
        {
            _versionPill.IsOpen = _versionMenu.IsOpen;
        }
    }

    private void OnCompareRequested(object? sender, PromptVersionEventArgs e)
    {
        e.Handled = true;
        Compare(e.Version);
    }

    private void OnPromoteRequested(object? sender, PromptVersionEventArgs e)
    {
        e.Handled = true;
        Promote(e.Version);
    }

    private void OnUndoPromoteRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        UndoPromote();
    }

    #endregion

    #region Compare view

    /// <summary>
    /// The version the draft is compared with while <see cref="IsCompareMode"/>: <see cref="CompareVersion"/>, or the one after
    /// the open version (the one before it when the open version is the last).
    /// </summary>
    internal PromptVersion? EffectiveCompareVersion
    {
        get
        {
            if (!IsCompareMode)
            {
                return null;
            }
            if (CompareVersion is { } chosen)
            {
                return chosen;
            }
            var index = EffectiveOpenVersion is { } open ? Versions.IndexOf(open) : -1;
            if (index < 0)
            {
                return null;
            }
            return index + 1 < Versions.Count ? Versions[index + 1] : index > 0 ? Versions[index - 1] : null;
        }
    }

    /// <summary>
    /// Shows the compare view or the editor: the panes crossfade (the diff in, the editor out), the token estimate gives
    /// way to the word counts and Back to editing, the hint line to the key, and a tab whose part changed gets its dot.
    /// </summary>
    private void UpdateCompare(bool announce)
    {
        var compare = EffectiveCompareVersion;
        if (IsCompareMode && compare is null)
        {
            // Nothing to compare with.
            SetCurrentValue(IsCompareModeProperty, false);
            return;
        }
        var on = compare is not null;
        PseudoClasses.Set(ComparePseudoClass, on);
        if (!on && _compareCorner is { IsKeyboardFocusWithin: true })
        {
            // Back to editing goes with the corner: the tab of the shown part takes the focus.
            _tabs?.FocusActive(FocusMethod);
        }
        UpdatePanes();
        _diffFade?.SetShown(on);
        _tokensFade?.SetShown(!on);
        _compareFade?.SetShown(on);
        if (on)
        {
            UpdateCompareDiff();
        }
        UpdateTabDots();
        UpdateHint();
        if (announce && (on != _shownCompare || on && !ReferenceEquals(compare, _shownCompareVersion)))
        {
            Announce(on
                ? Labels.AnnounceCompare(compare!.Label, EffectiveOpenVersion?.Label ?? string.Empty)
                : Labels.AnnounceBackToEditing);
        }
        _shownCompare = on;
        _shownCompareVersion = compare;
    }

    /// <summary>The diff of the shown part: the compared version on the left, the draft on the right.</summary>
    private void UpdateCompareDiff()
    {
        if (_diff is null || EffectiveCompareVersion is not { } compare)
        {
            return;
        }
        var part = ActivePart;
        _diff.Before = (part == PromptPart.System ? compare.System : compare.User) ?? string.Empty;
        _diff.After = TextOf(part);
        UpdateCompareTexts();
    }

    /// <summary>The word counts ("+17 −1") and the names screen readers hear for them and for the diff.</summary>
    private void UpdateCompareTexts()
    {
        if (_diff is null)
        {
            return;
        }
        var labels = Labels;
        var added = _diff.InsertedWords;
        var removed = _diff.DeletedWords;
        if (_added is not null)
        {
            _added.Text = labels.DiffAdded(added);
        }
        if (_removed is not null)
        {
            _removed.Text = labels.DiffRemoved(removed);
        }
        var summary = labels.DiffSummaryName(added, removed);
        _diff.SummaryName = summary;
        if (_diffStats is not null)
        {
            AutomationProperties.SetName(_diffStats, summary);
        }
        if (EffectiveCompareVersion is { } compare)
        {
            AutomationProperties.SetName(_diff, labels.DiffName(compare.Label, EffectiveOpenVersion?.Label ?? string.Empty, ActivePart));
        }
    }

    private void OnDiffPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiDiffText.InsertedWordsProperty || e.Property == AiDiffText.DeletedWordsProperty)
        {
            UpdateCompareTexts();
        }
    }

    /// <summary>Comparing, a tab whose part differs from the compared version shows its dot.</summary>
    private void UpdateTabDots()
    {
        if (_tabs is null)
        {
            return;
        }
        var compare = EffectiveCompareVersion;
        _tabs.SystemChanged = compare is not null && !string.Equals(compare.System ?? string.Empty, SystemText, StringComparison.Ordinal);
        _tabs.UserChanged = compare is not null && !string.Equals(compare.User ?? string.Empty, UserText, StringComparison.Ordinal);
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        LeaveCompare(focusTab: true);
    }

    /// <summary>Back to the editor; with <paramref name="focusTab"/> (Back to editing, Escape) the shown part's tab takes the focus.</summary>
    private void LeaveCompare(bool focusTab)
    {
        if (!IsCompareMode)
        {
            return;
        }
        var method = FocusMethod;
        SetCurrentValue(IsCompareModeProperty, false);
        if (focusTab)
        {
            _tabs?.FocusActive(method);
        }
    }

    #endregion
}
