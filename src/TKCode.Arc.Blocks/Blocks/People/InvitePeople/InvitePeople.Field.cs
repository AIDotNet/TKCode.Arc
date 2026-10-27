using TKCode.Arc.DesignTokens;
using Avalonia.Input.Platform;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class InvitePeople
{
    private readonly Dictionary<InvitePeopleEntry, InvitePeopleChip> _chipMap = new();
    private InvitePeopleRoster? _roster;
    private bool _settingText;
    private InvitePeopleChip? _armed;

    /// <summary>The roster the parser and the suggestions check against (rebuilt when the data changes).</summary>
    internal InvitePeopleRoster Roster =>
        _roster ??= new InvitePeopleRoster(EffectiveDirectory, Members, PendingInvites.Select(i => i.Person));

    /// <summary>The chip of <paramref name="entry"/>.</summary>
    internal InvitePeopleChip? ChipOf(InvitePeopleEntry entry) => _chipMap.TryGetValue(entry, out var chip) ? chip : null;

    /// <summary>The chip marked by the first Backspace, or null.</summary>
    internal InvitePeopleChip? ArmedChip => _armed;

    #region Public methods

    /// <summary>
    /// Adds the people in <paramref name="text"/> as chips, as a paste does: split at commas, semicolons and line breaks,
    /// duplicates skipped, entries that cannot be invited added as invalid chips. Returns the parse summary that is also
    /// announced and shown under the field.
    /// </summary>
    public string AddText(string text) => AddParsed(text, isPaste: true);

    /// <summary>Removes every chip.</summary>
    public void ClearEntries()
    {
        if (_entries.Count == 0)
        {
            return;
        }
        _chips?.ArmLayout();
        foreach (var entry in _entries.ToList())
        {
            RemoveChip(entry);
        }
        _entries.Clear();
        AfterEntriesChanged();
    }

    /// <summary>Puts the keyboard focus in the field.</summary>
    public void FocusField() => _input?.Focus(NavigationMethod.Tab);

    #endregion

    #region Field and chips

    private void BuildField()
    {
        if (_chips is null)
        {
            return;
        }
        UnwireInput();
        _input ??= new InvitePeopleInput();
        if (_input.Parent is Panel old)
        {
            old.Children.Remove(_input);
        }
        _chips.Children.Clear();
        _chipMap.Clear();
        _chips.Children.Add(_input);
        _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        _input.AddHandler(TextInputEvent, OnInputTextInput, RoutingStrategies.Tunnel);
        _input.AddHandler(TextBox.PastingFromClipboardEvent, OnInputPasting, RoutingStrategies.Bubble);
        _input.TextChanged += OnInputTextChanged;
        _input.GotFocus += OnInputGotFocus;
        _input.LostFocus += OnInputLostFocus;
    }

    private void UnwireInput()
    {
        if (_input is null)
        {
            return;
        }
        _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        _input.RemoveHandler(TextInputEvent, OnInputTextInput);
        _input.RemoveHandler(TextBox.PastingFromClipboardEvent, OnInputPasting);
        _input.TextChanged -= OnInputTextChanged;
        _input.GotFocus -= OnInputGotFocus;
        _input.LostFocus -= OnInputLostFocus;
    }

    private void RebuildChips()
    {
        if (_chips is null || _input is null)
        {
            return;
        }
        foreach (var chip in _chipMap.Values)
        {
            _chips.Children.Remove(chip);
        }
        _chipMap.Clear();
        foreach (var entry in _entries)
        {
            var chip = CreateChip(entry);
            _chipMap[entry] = chip;
            _chips.Children.Insert(_chips.Children.Count - 1, chip);
        }
        UpdatePlaceholder();
    }

    private void UpdatePlaceholder()
    {
        if (_input is not null)
        {
            _input.PlaceholderText = _entries.Count == 0 ? Labels.Placeholder : Labels.MorePlaceholder;
        }
    }

    private void SetInputText(string text)
    {
        if (_input is null)
        {
            return;
        }
        _settingText = true;
        try
        {
            _input.Text = text;
            _input.CaretIndex = text.Length;
            _input.ClearSelection();
        }
        finally
        {
            _settingText = false;
        }
    }

    private HashSet<string> SelectedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            keys.Add(new InvitePeopleParsedEntry(entry.Person, entry.Text, entry.Error).Key);
        }
        return keys;
    }

    private HashSet<string> SelectedEmails()
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _entries)
        {
            if (entry.Person?.Email is { Length: > 0 } email)
            {
                emails.Add(email.Trim());
            }
        }
        return emails;
    }

    /// <summary>Adds chips for <paramref name="parsed"/> at the end; <paramref name="from"/> gives flight sources by index.</summary>
    private List<InvitePeopleEntry> AddEntries(IReadOnlyList<InvitePeopleParsedEntry> parsed, IReadOnlyDictionary<int, Rect?>? from = null)
    {
        Disarm();
        var added = new List<InvitePeopleEntry>();
        if (parsed.Count == 0)
        {
            return added;
        }
        _chips?.ArmLayout();
        for (var i = 0; i < parsed.Count; i++)
        {
            var item = parsed[i];
            var entry = new InvitePeopleEntry(item.Person, item.Text, item.Error, item.IsValid ? DefaultRole : null);
            _entries.Add(entry);
            added.Add(entry);
            if (_chips is null || _input is null)
            {
                continue;
            }
            var chip = CreateChip(entry);
            _chipMap[entry] = chip;
            _chips.Children.Insert(_chips.Children.Count - 1, chip);
            _chips.Enter(chip);
            if (!entry.IsValid)
            {
                InvitePeopleShake.Run(chip.Face, -1);
            }
            if (from is not null && from.TryGetValue(i, out var source) && chip.Avatar is not null)
            {
                _flights?.Fly(source, chip.Avatar, entry.Person);
            }
        }
        AfterEntriesChanged();
        return added;
    }

    private void RemoveChip(InvitePeopleEntry entry)
    {
        if (_chipMap.Remove(entry, out var chip))
        {
            if (ReferenceEquals(_armed, chip))
            {
                _armed = null;
            }
            if (ReferenceEquals(_menuChip, chip))
            {
                CloseRoleMenu(returnFocus: false);
            }
            _chips?.Remove(chip);
        }
    }

    /// <summary>Removes one chip (its × button, the second Backspace).</summary>
    internal void RemoveEntry(InvitePeopleEntry entry, bool announce = true)
    {
        if (!_entries.Contains(entry))
        {
            return;
        }
        var hadFocus = _fieldHost?.IsKeyboardFocusWithin == true;
        _chips?.ArmLayout();
        RemoveChip(entry);
        _entries.Remove(entry);
        AfterEntriesChanged();
        if (announce)
        {
            Announce(Labels.Removed(entry.Text));
        }
        if (hadFocus)
        {
            _input?.Focus(NavigationMethod.Tab);
        }
    }

    private void AfterEntriesChanged()
    {
        _roster = null;
        UpdatePlaceholder();
        UpdateSeats();
        UpdateErrors();
        RaiseEntriesChanged();
        if (_popup?.IsOpen == true)
        {
            RefreshSuggestions();
        }
    }

    private string AddParsed(string text, bool isPaste)
    {
        var result = InvitePeopleParser.Parse(text, Roster, SelectedKeys());
        AddEntries(result.Entries);
        var labels = Labels;
        string summary;
        if (!isPaste && result.Entries.Count == 1 && result.Duplicates == 0 && result.Entries[0].IsValid)
        {
            summary = labels.Added(result.Entries[0].Text);
        }
        else
        {
            var parts = new List<string>();
            if (result.ValidCount > 0)
            {
                parts.Add(labels.AddedCount(result.ValidCount));
            }
            if (result.InvalidCount > 0)
            {
                parts.Add(labels.FixCount(result.InvalidCount));
            }
            if (result.Duplicates > 0)
            {
                parts.Add(labels.DuplicateCount(result.Duplicates));
            }
            summary = parts.Count > 0 ? string.Join(", ", parts) : labels.NothingAdded;
        }
        Announce(summary);
        if (isPaste)
        {
            ShowSummary(summary);
        }
        return summary;
    }

    private InvitePeopleChip CreateChip(InvitePeopleEntry entry)
    {
        var chip = new InvitePeopleChip(entry);
        var labels = Labels;
        var face = chip.Face;
        face.BorderThickness = new Thickness(1);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var remove = new InvitePeopleButton
        {
            Width = 24,
            Height = 24,
            Content = InvitePeopleText.Glyph(ArcIcons.X, 14)
        };
        remove.Classes.Add("icon");
        AutomationProperties.SetName(remove, labels.RemoveName(entry.Text));
        remove.Click += (_, _) => RemoveEntry(entry);
        chip.RemoveButton = remove;
        if (entry.IsValid)
        {
            face.Padding = new Thickness(5, 0, 3, 0);
            var avatar = InvitePeopleAvatars.Create(22, entry.Person);
            chip.Avatar = avatar;
            var name = InvitePeopleText.Create(13, ArcDesignTokenKind.Foreground, FontWeight.Medium);
            name.Text = entry.Text;
            row.Children.Add(avatar);
            row.Children.Add(name);
            if (Roles.Count > 0)
            {
                var roleText = new ArcContentSwap
                {
                    Mode = ContentSwapMode.BlurSwap,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = entry.Role?.Name
                };
                var roleButton = new InvitePeopleButton
                {
                    Margin = new Thickness(4, 0, 0, 0),
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 2,
                        Children =
                        {
                            new SpringSizeDecorator { Axis = SpringSizeAxis.Width, CrossClipOverflow = 12, Child = roleText },
                            InvitePeopleText.Glyph(ArcIcons.ChevronDown, 12)
                        }
                    }
                };
                roleButton.Classes.Add("chip-role");
                AutomationProperties.SetName(roleButton, labels.RoleButtonName(entry.Text, entry.Role?.Name ?? string.Empty));
                roleButton.Click += (_, _) => ToggleRoleMenu(chip, keyboard: false);
                roleButton.AddHandler(KeyDownEvent, (_, e) =>
                {
                    if (e.Key == Key.Down)
                    {
                        e.Handled = true;
                        OpenRoleMenu(chip, keyboard: true);
                    }
                }, RoutingStrategies.Tunnel);
                chip.RoleButton = roleButton;
                chip.RoleText = roleText;
                row.Children.Add(roleButton);
            }
            row.Children.Add(remove);
        }
        else
        {
            face.Padding = new Thickness(0, 0, 3, 0);
            row.Spacing = 0;
            var text = new TextBlock { Text = entry.Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var glyph = InvitePeopleText.Glyph(ArcIcons.CircleAlert, 14);
            var edit = new InvitePeopleButton
            {
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { glyph, text } }
            };
            edit.Classes.Add("chip-edit");
            AutomationProperties.SetName(edit, labels.EditName(entry.Text, labels.Reason(entry, ExampleDomainText())));
            edit.Click += (_, _) => EditEntry(entry);
            chip.EditButton = edit;
            remove.Classes.Add("danger");
            row.Children.Add(edit);
            row.Children.Add(remove);
        }
        face.Child = row;
        ApplyChipLook(chip);
        return chip;
    }

    private void ApplyChipLook(InvitePeopleChip chip)
    {
        var face = chip.Face;
        foreach (var binding in chip.LookBindings)
        {
            binding.Dispose();
        }
        chip.LookBindings.Clear();
        if (chip.Entry.IsValid)
        {
            chip.LookBindings.Add(chip.IsArmed
                ? face.Bind(Border.BackgroundProperty, this.GetObservable(ArmedBrushProperty))
                : ArcTokens.Bind(face, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted));
            chip.LookBindings.Add(ArcTokens.Bind(face, Border.BorderBrushProperty,
                chip.IsArmed ? ArcDesignTokenKind.BorderStrong : ArcDesignTokenKind.BorderSubtle));
        }
        else
        {
            chip.LookBindings.Add(face.Bind(Border.BackgroundProperty, this.GetObservable(ErrorFillBrushProperty)));
            chip.LookBindings.Add(chip.IsArmed
                ? ArcTokens.Bind(face, Border.BorderBrushProperty, ArcDesignTokenKind.Danger)
                : face.Bind(Border.BorderBrushProperty, this.GetObservable(ErrorStrokeBrushProperty)));
        }
    }

    private void Arm(InvitePeopleChip chip)
    {
        Disarm();
        _armed = chip;
        chip.IsArmed = true;
        ApplyChipLook(chip);
        Announce(Labels.Armed(chip.Entry.Text));
    }

    private void Disarm()
    {
        if (_armed is not { } chip)
        {
            return;
        }
        _armed = null;
        chip.IsArmed = false;
        ApplyChipLook(chip);
    }

    /// <summary>An invalid chip goes back into the field: removed, its text in the text box (replacing what was there).</summary>
    internal void EditEntry(InvitePeopleEntry entry)
    {
        if (!_entries.Contains(entry))
        {
            return;
        }
        _chips?.ArmLayout();
        RemoveChip(entry);
        _entries.Remove(entry);
        AfterEntriesChanged();
        SetInputText(entry.Text);
        _input?.Focus(NavigationMethod.Pointer);
        Announce(Labels.Editing(entry.Text));
    }

    private void OnRemoveInvalidClick(object? sender, RoutedEventArgs e) => RemoveInvalidEntries();

    /// <summary>Removes every chip that cannot be invited, as the error panel's button does.</summary>
    public void RemoveInvalidEntries()
    {
        var invalid = _entries.Where(x => !x.IsValid).ToList();
        if (invalid.Count == 0)
        {
            return;
        }
        _chips?.ArmLayout();
        foreach (var entry in invalid)
        {
            RemoveChip(entry);
            _entries.Remove(entry);
        }
        AfterEntriesChanged();
        Announce(Labels.RemovedInvalid(invalid.Count));
    }

    private void OnPasteSampleClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(SampleText))
        {
            AddParsed(SampleText!, isPaste: true);
        }
    }

    private void OnFieldHostPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_input is null || e.Source is Visual source && source.FindAncestorOfType<AvaloniaButton>(includeSelf: true) is not null)
        {
            return;
        }
        if (!_input.IsFocused)
        {
            e.Handled = true;
            _input.Focus(NavigationMethod.Pointer);
        }
        if (_popup?.IsOpen != true)
        {
            OpenSuggestions();
        }
    }

    #endregion

    #region Text box input

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.NavigationMethod == NavigationMethod.Pointer)
        {
            OpenSuggestions();
        }
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_input?.IsFocused != true)
            {
                CloseSuggestions();
                Disarm();
            }
        });
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_settingText)
        {
            return;
        }
        Disarm();
        if (_input?.IsFocused == true)
        {
            OpenSuggestions();
        }
    }

    private void OnInputTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { Length: > 0 } typed)
        {
            return;
        }
        Disarm();
        if (typed is "," or ";")
        {
            e.Handled = true;
            CommitText();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var text = _input?.Text ?? string.Empty;
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                if (_popup?.IsOpen == true)
                {
                    MoveHighlight(+1);
                }
                else
                {
                    OpenSuggestions();
                }
                break;
            case Key.Up:
                if (_popup?.IsOpen == true)
                {
                    e.Handled = true;
                    MoveHighlight(-1);
                }
                break;
            case Key.Enter:
                e.Handled = true;
                if (_popup?.IsOpen == true && HighlightedSuggestion is { IsBlocked: false } suggestion)
                {
                    CommitSuggestion(suggestion);
                }
                else
                {
                    CommitText();
                }
                break;
            case Key.Escape:
                if (_popup?.IsOpen == true)
                {
                    e.Handled = true;
                    CloseSuggestions();
                }
                else if (text.Length > 0)
                {
                    e.Handled = true;
                    SetInputText(string.Empty);
                }
                break;
            case Key.Back:
                if (text.Length == 0 && _entries.Count > 0)
                {
                    e.Handled = true;
                    var last = ChipOf(_entries[^1]);
                    if (last is not null && ReferenceEquals(_armed, last))
                    {
                        RemoveEntry(last.Entry);
                    }
                    else if (last is not null)
                    {
                        Arm(last);
                    }
                }
                break;
            case Key.Tab:
                CloseSuggestions();
                break;
        }
    }

    private async void OnInputPasting(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard || _input is null)
        {
            return;
        }
        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception)
        {
            text = null;
        }
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        PasteText(text);
    }

    /// <summary>A paste into the field: a list becomes chips; plain text goes into the text box at the caret.</summary>
    internal void PasteText(string text)
    {
        if (_input is null)
        {
            return;
        }
        if (InvitePeopleParser.HasSeparator(text))
        {
            AddParsed(text, isPaste: true);
            return;
        }
        var current = _input.Text ?? string.Empty;
        var start = Math.Min(_input.SelectionStart, _input.SelectionEnd);
        var end = Math.Max(_input.SelectionStart, _input.SelectionEnd);
        start = Math.Clamp(start, 0, current.Length);
        end = Math.Clamp(end, start, current.Length);
        if (start == end)
        {
            start = end = Math.Clamp(_input.CaretIndex, 0, current.Length);
        }
        var next = current[..start] + text + current[end..];
        _input.Text = next;
        _input.CaretIndex = start + text.Length;
        _input.ClearSelection();
    }

    /// <summary>Enter without a suggestion, a comma or a semicolon: the text becomes a chip (or several, or an invalid one).</summary>
    private void CommitText()
    {
        var text = _input?.Text ?? string.Empty;
        if (text.Trim().Length == 0)
        {
            return;
        }
        SetInputText(string.Empty);
        AddParsed(text, isPaste: false);
    }

    #endregion

    #region Suggestions

    private ArcPopup? _popup;
    private Border? _suggestSurface;
    private Panel? _suggestHost;
    private StackPanel? _suggestList;
    private GlidingIndicator? _indicator;
    private readonly List<InvitePeopleSuggestionRow> _suggestRows = new();
    private InvitePeopleSuggestionList _suggestions = InvitePeopleSuggestionList.Empty;
    private int _highlight = -1;
    private double _suggestHeader;
    private TopLevel? _hooked;

    /// <summary>The list on screen (tests read it).</summary>
    internal InvitePeopleSuggestionList Suggestions => _suggestions;

    /// <summary>The index of the highlighted row, or −1.</summary>
    internal int HighlightIndex => _highlight;

    /// <summary>True while the suggestion list is open.</summary>
    internal bool IsSuggestionsOpen => _popup?.IsOpen == true;

    /// <summary>The suggestion rows (tests read them).</summary>
    internal IReadOnlyList<InvitePeopleSuggestionRow> SuggestionRows => _suggestRows;

    private InvitePeopleSuggestion? HighlightedSuggestion =>
        _highlight >= 0 && _highlight < _suggestions.Rows.Count ? _suggestions.Rows[_highlight] : null;

    private void EnsurePopup()
    {
        if (_popup is not null || _fieldHost is null)
        {
            return;
        }
        _indicator = new GlidingIndicator { IsHitTestVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusStep20);
        _indicator.Bind(GlidingIndicator.FillProperty, this.GetObservable(HoverBrushProperty));
        _suggestList = new StackPanel { Spacing = 2 };
        _suggestHost = new Panel { Children = { _indicator, _suggestList } };
        _suggestSurface = new Border
        {
            Padding = new Thickness(6),
            BorderThickness = new Thickness(1),
            Child = _suggestHost
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
        ArcTokens.Bind(_suggestSurface, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(_suggestSurface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        AutomationProperties.SetName(_suggestSurface, Labels.SuggestionsName);
        _popup = new ArcPopup
        {
            Child = _suggestSurface,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            MarginToAnchor = 6,
            EnterOffset = 4,
            EnterScale = 0.98,
            IsLightDismissEnabled = false,
            WindowManagerAddShadowHint = false,
            PlacementTarget = _fieldHost,
            OverlayInputPassThroughElement = _fieldHost
        };
        LogicalChildren.Add(_popup);
    }

    private void OpenSuggestions()
    {
        if (_input is null || !IsEffectivelyEnabled || VisualRoot is null)
        {
            return;
        }
        EnsurePopup();
        if (_popup is null)
        {
            return;
        }
        var wasOpen = _popup.IsOpen;
        RefreshSuggestions(keepHighlight: false, animate: false);
        if (!wasOpen)
        {
            _popup.IsOpen = true;
            Hook();
        }
    }

    private void CloseSuggestions()
    {
        if (_popup is null || !_popup.IsOpen)
        {
            return;
        }
        _popup.IsOpen = false;
        _highlight = -1;
        Unhook();
    }

    private void Hook()
    {
        if (_hooked is not null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }
        _hooked = top;
        top.AddHandler(PointerPressedEvent, OnTopLevelPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void Unhook()
    {
        _hooked?.RemoveHandler(PointerPressedEvent, OnTopLevelPressed);
        _hooked = null;
    }

    private void OnTopLevelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return;
        }
        if (_fieldHost is not null && (ReferenceEquals(source, _fieldHost) || _fieldHost.IsVisualAncestorOf(source)))
        {
            return;
        }
        if (_suggestSurface is not null && (ReferenceEquals(source, _suggestSurface) || _suggestSurface.IsVisualAncestorOf(source)))
        {
            return;
        }
        CloseSuggestions();
    }

    private void RefreshSuggestions() => RefreshSuggestions(keepHighlight: true, animate: true);

    private void RefreshSuggestions(bool keepHighlight, bool animate)
    {
        if (_suggestList is null || _input is null)
        {
            return;
        }
        var previous = HighlightedSuggestion;
        _suggestions = InvitePeopleSuggestions.Build(_input.Text, Roster, SelectedEmails(), Math.Max(1, MaxSuggestions),
            Math.Max(0, SuggestedCount));
        BuildSuggestionRows();
        var index = -1;
        if (keepHighlight && previous is not null)
        {
            index = IndexOf(previous);
        }
        if (index < 0)
        {
            index = FirstAvailable();
        }
        SetHighlight(index, animate && keepHighlight);
        if (_fieldHost is not null && _suggestSurface is not null)
        {
            _suggestSurface.Width = Math.Max(0, _fieldHost.Bounds.Width);
        }
    }

    private int IndexOf(InvitePeopleSuggestion suggestion)
    {
        for (var i = 0; i < _suggestions.Rows.Count; i++)
        {
            var row = _suggestions.Rows[i];
            if (row.Kind == suggestion.Kind && ReferenceEquals(row.Person, suggestion.Person) &&
                string.Equals(row.Email, suggestion.Email, StringComparison.OrdinalIgnoreCase) && !row.IsBlocked)
            {
                return i;
            }
        }
        return -1;
    }

    private int FirstAvailable()
    {
        for (var i = 0; i < _suggestions.Rows.Count; i++)
        {
            if (!_suggestions.Rows[i].IsBlocked)
            {
                return i;
            }
        }
        return -1;
    }

    private void BuildSuggestionRows()
    {
        if (_suggestList is null)
        {
            return;
        }
        var labels = Labels;
        _suggestList.Children.Clear();
        _suggestRows.Clear();
        _suggestHeader = 0;
        if (_suggestions.IsSuggested && _suggestions.Rows.Count > 0)
        {
            var heading = InvitePeopleText.Create(12, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
            heading.Text = labels.Suggested;
            heading.Margin = new Thickness(10, 6, 10, 4);
            _suggestList.Children.Add(heading);
            _suggestHeader = 6 + 16.8 + 4 + _suggestList.Spacing;
        }
        if (_suggestions.Rows.Count == 0)
        {
            if (_suggestions.IsSuggested)
            {
                CloseSuggestionsLater();
                return;
            }
            var none = InvitePeopleText.Create(13, ArcDesignTokenKind.TextSecondary, lineHeight: 18.2);
            none.TextWrapping = TextWrapping.Wrap;
            none.TextTrimming = TextTrimming.None;
            none.Text = labels.NoMatch(_input?.Text ?? string.Empty);
            none.Margin = new Thickness(10, 6);
            _suggestList.Children.Add(none);
            return;
        }
        for (var i = 0; i < _suggestions.Rows.Count; i++)
        {
            var row = CreateSuggestionRow(_suggestions.Rows[i], i);
            _suggestRows.Add(row);
            _suggestList.Children.Add(row);
        }
    }

    private void CloseSuggestionsLater() => Dispatcher.UIThread.Post(() =>
    {
        if (_suggestions.Rows.Count == 0 && _suggestions.IsSuggested)
        {
            CloseSuggestions();
        }
    });

    private InvitePeopleSuggestionRow CreateSuggestionRow(InvitePeopleSuggestion suggestion, int index)
    {
        var labels = Labels;
        var row = new InvitePeopleSuggestionRow(suggestion, index)
        {
            Height = 49,
            Padding = new Thickness(8, 7, 10, 7),
            Background = Brushes.Transparent,
            Cursor = suggestion.IsBlocked ? Cursor.Default : new Cursor(StandardCursorType.Hand)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,Auto") };
        Control avatar;
        string first;
        string second;
        if (suggestion.Kind == InvitePeopleSuggestionKind.Email)
        {
            var glyph = InvitePeopleText.Glyph(ArcIcons.Mail, 15);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            var circle = new Border
            {
                Width = 30,
                Height = 30,
                BorderThickness = new Thickness(1),
                Child = glyph,
                VerticalAlignment = VerticalAlignment.Center
            }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(circle, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            avatar = circle;
            first = suggestion.Email ?? string.Empty;
            second = labels.InviteByEmail;
        }
        else
        {
            avatar = InvitePeopleAvatars.Create(30, suggestion.Person);
            first = suggestion.Person?.Name ?? suggestion.Email ?? string.Empty;
            second = suggestion.Person?.Email ?? string.Empty;
        }
        if (suggestion.IsBlocked)
        {
            avatar.Opacity = 0.55;
        }
        row.Avatar = avatar;
        var lines = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var name = InvitePeopleText.Create(13, suggestion.IsBlocked ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground,
            FontWeight.Medium, 17);
        name.Text = first;
        var detail = InvitePeopleText.Create(12, ArcDesignTokenKind.TextSecondary, lineHeight: 16);
        detail.Text = second;
        lines.Children.Add(name);
        lines.Children.Add(detail);
        Grid.SetColumn(lines, 1);
        grid.Children.Add(avatar);
        grid.Children.Add(lines);
        var noteText = suggestion.Block switch
        {
            InvitePeopleEntryError.AlreadyMember => labels.OnTeam,
            InvitePeopleEntryError.AlreadyInvited => labels.Invited,
            _ => suggestion.Person?.Title
        };
        if (!string.IsNullOrEmpty(noteText) && !_isNarrow)
        {
            var note = InvitePeopleText.Create(12, ArcDesignTokenKind.TextMuted);
            note.Text = noteText;
            note.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(note, 2);
            grid.Children.Add(note);
            row.Note = note;
        }
        row.Child = grid;
        AutomationProperties.SetName(row, string.IsNullOrEmpty(second) ? first : first + ", " + second);
        if (suggestion.IsBlocked)
        {
            AutomationProperties.SetHelpText(row, noteText);
            row.IsEnabled = true;
        }
        row.PointerEntered += (_, _) =>
        {
            if (!suggestion.IsBlocked)
            {
                SetHighlight(index, animate: true);
            }
        };
        row.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (!suggestion.IsBlocked)
            {
                CommitSuggestion(suggestion);
            }
        };
        return row;
    }

    private void MoveHighlight(int step)
    {
        var count = _suggestions.Rows.Count;
        if (count == 0)
        {
            return;
        }
        var start = _highlight < 0 ? (step > 0 ? -1 : count) : _highlight;
        for (var n = 1; n <= count; n++)
        {
            var index = ((start + step * n) % count + count) % count;
            if (!_suggestions.Rows[index].IsBlocked)
            {
                SetHighlight(index, animate: true);
                return;
            }
        }
    }

    private void SetHighlight(int index, bool animate)
    {
        _highlight = index;
        if (_indicator is null)
        {
            return;
        }
        if (index < 0 || index >= _suggestRows.Count)
        {
            _indicator.Hide(animate: false);
            return;
        }
        var width = _suggestHost?.Bounds.Width is > 0 and var w ? w : Math.Max(0, (_fieldHost?.Bounds.Width ?? 0) - 14);
        var rect = new Rect(0, _suggestHeader + index * (49 + 2), width, 49);
        if (animate && ArcMotion.IsFull(this) && _indicator.CurrentOpacity > 0)
        {
            _indicator.MoveTo(rect, InvitePeopleMotion.Highlight);
        }
        else
        {
            _indicator.MoveTo(rect, animate: false);
        }
    }

    /// <summary>A suggestion becomes a chip; its avatar flies from the row into the chip and the list stays open.</summary>
    internal void CommitSuggestion(InvitePeopleSuggestion suggestion)
    {
        if (suggestion.IsBlocked)
        {
            return;
        }
        Rect? from = null;
        var row = _suggestRows.FirstOrDefault(r => ReferenceEquals(r.Suggestion, suggestion));
        if (row?.Avatar is { } avatar && suggestion.Kind == InvitePeopleSuggestionKind.Person)
        {
            from = _flights?.BoxOf(avatar);
        }
        var person = suggestion.Person ?? new InvitePeoplePerson(null, suggestion.Email);
        var parsed = new InvitePeopleParsedEntry(person, person.DisplayName, InvitePeopleEntryError.None);
        SetInputText(string.Empty);
        AddEntries([parsed], new Dictionary<int, Rect?> { [0] = from });
        Announce(Labels.Added(person.DisplayName));
        _input?.Focus(NavigationMethod.Pointer);
        if (_popup?.IsOpen == true)
        {
            RefreshSuggestions(keepHighlight: false, animate: false);
        }
    }

    #endregion

    #region Role menu

    private ArcPopup? _menuPopup;
    private Border? _menuSurface;
    private StackPanel? _menuList;
    private InvitePeopleChip? _menuChip;
    private bool _menuByKeyboard;

    /// <summary>True while a role menu is open.</summary>
    internal bool IsRoleMenuOpen => _menuPopup?.IsOpen == true;

    /// <summary>The role menu's popup (tests read its offset).</summary>
    internal ArcPopup? RoleMenuPopup => _menuPopup;

    /// <summary>The role menu's lines.</summary>
    internal IEnumerable<InvitePeopleMenuItem> RoleMenuItems => _menuList?.Children.OfType<InvitePeopleMenuItem>() ?? [];

    private void ToggleRoleMenu(InvitePeopleChip chip, bool keyboard)
    {
        if (IsRoleMenuOpen && ReferenceEquals(_menuChip, chip))
        {
            CloseRoleMenu(returnFocus: true);
            return;
        }
        OpenRoleMenu(chip, keyboard);
    }

    /// <summary>Opens the role menu under a chip's role button (focus on the current role).</summary>
    internal void OpenRoleMenu(InvitePeopleChip chip, bool keyboard)
    {
        if (chip.RoleButton is not { } button || Roles.Count == 0 || VisualRoot is null)
        {
            return;
        }
        CloseSuggestions();
        if (IsRoleMenuOpen)
        {
            CloseRoleMenu(returnFocus: false);
        }
        EnsureMenu();
        _menuChip = chip;
        _menuByKeyboard = keyboard;
        _menuList!.Children.Clear();
        InvitePeopleMenuItem? current = null;
        foreach (var role in Roles)
        {
            var item = new InvitePeopleMenuItem(role) { IsChecked = ReferenceEquals(role, chip.Entry.Role) };
            item.Click += (_, _) => PickRole(role);
            _menuList.Children.Add(item);
            if (item.IsChecked)
            {
                current = item;
            }
        }
        AutomationProperties.SetName(_menuSurface!, Labels.RoleButtonName(chip.Entry.Text, chip.Entry.Role?.Name ?? string.Empty));
        _menuPopup!.PlacementTarget = button;
        _menuPopup.HorizontalOffset = RoleMenuOffset(button);
        button.SetState(InvitePeopleButton.OpenPseudoClass, true);
        _menuPopup.IsOpen = true;
        var focus = current ?? _menuList.Children.OfType<InvitePeopleMenuItem>().FirstOrDefault();
        Dispatcher.UIThread.Post(() => focus?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The menu's horizontal offset from the button: 8 to the left of it, kept 8 inside the block's card on both sides
    /// (Arc clamps to the block, not the window).
    /// </summary>
    internal double RoleMenuOffset(Control button)
    {
        var bounds = _card ?? (Control)this;
        var origin = button.TranslatePoint(default, bounds);
        if (origin is null)
        {
            return -8;
        }
        const double menuWidth = 232;
        var left = origin.Value.X - 8;
        var max = bounds.Bounds.Width - 8 - menuWidth;
        left = Math.Min(left, max);
        left = Math.Max(left, 8);
        return left - origin.Value.X;
    }

    private void EnsureMenu()
    {
        if (_menuPopup is not null)
        {
            return;
        }
        _menuList = new StackPanel { Spacing = 2 };
        _menuSurface = new Border
        {
            Width = 232,
            Padding = new Thickness(6),
            BorderThickness = new Thickness(1),
            Child = _menuList
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
        ArcTokens.Bind(_menuSurface, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(_menuSurface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        _menuSurface.AddHandler(KeyDownEvent, OnMenuKeyDown, RoutingStrategies.Tunnel);
        _menuPopup = new ArcPopup
        {
            Child = _menuSurface,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            MarginToAnchor = 6,
            EnterOffset = 4,
            EnterScale = 0.97,
            IsLightDismissEnabled = true,
            WindowManagerAddShadowHint = false
        };
        _menuPopup.Closed += (_, _) => OnMenuClosed();
        LogicalChildren.Add(_menuPopup);
    }

    private void OnMenuClosed()
    {
        _menuChip?.RoleButton?.SetState(InvitePeopleButton.OpenPseudoClass, false);
        if (_menuSurface?.IsKeyboardFocusWithin == true || FocusManagerOf()?.GetFocusedElement() is null)
        {
            _menuChip?.RoleButton?.Focus(_menuByKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        _menuChip = null;
    }

    private IFocusManager? FocusManagerOf() => TopLevel.GetTopLevel(this)?.FocusManager;

    /// <summary>Closes the role menu; <paramref name="returnFocus"/> puts the focus back on the role button.</summary>
    internal void CloseRoleMenu(bool returnFocus)
    {
        if (_menuPopup is null || !_menuPopup.IsOpen)
        {
            return;
        }
        var chip = _menuChip;
        if (returnFocus)
        {
            chip?.RoleButton?.Focus(_menuByKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        _menuPopup.IsOpen = false;
    }

    private void OnMenuKeyDown(object? sender, KeyEventArgs e)
    {
        var items = RoleMenuItems.ToList();
        if (items.Count == 0)
        {
            return;
        }
        var index = items.FindIndex(i => i.IsFocused);
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                items[(index + 1 + items.Count) % items.Count].Focus(NavigationMethod.Tab);
                break;
            case Key.Up:
                e.Handled = true;
                items[(index - 1 + items.Count) % items.Count].Focus(NavigationMethod.Tab);
                break;
            case Key.Home:
                e.Handled = true;
                items[0].Focus(NavigationMethod.Tab);
                break;
            case Key.End:
                e.Handled = true;
                items[^1].Focus(NavigationMethod.Tab);
                break;
            case Key.Escape:
                e.Handled = true;
                _menuByKeyboard = true;
                CloseRoleMenu(returnFocus: true);
                break;
            case Key.Tab:
                e.Handled = true;
                CloseRoleMenu(returnFocus: true);
                break;
        }
    }

    private void PickRole(InvitePeopleRole role)
    {
        if (_menuChip is not { } chip)
        {
            return;
        }
        var entry = chip.Entry;
        var old = entry.Role;
        CloseRoleMenu(returnFocus: true);
        if (ReferenceEquals(old, role))
        {
            return;
        }
        entry.Role = role;
        if (chip.RoleText is not null)
        {
            chip.RoleText.Content = role.Name;
        }
        if (chip.RoleButton is not null)
        {
            AutomationProperties.SetName(chip.RoleButton, Labels.RoleButtonName(entry.Text, role.Name ?? string.Empty));
        }
        _chips?.ArmLayout();
        Announce(Labels.RoleChanged(entry.Text, role.Name ?? string.Empty));
        RaiseEvent(new InvitePeopleRoleChangedEventArgs(RoleChangedEvent, entry, old, role));
        RaiseEntriesChanged();
    }

    #endregion
}
