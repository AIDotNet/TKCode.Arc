using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="TagInput.ValueChanged"/>: the old and new lists and what was added or removed.</summary>
public sealed class TagInputValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TagInputValueChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> oldValue, IReadOnlyList<string> newValue,
                                         IReadOnlyList<string> added, IReadOnlyList<string> removed)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Added = added;
        Removed = removed;
    }

    /// <summary>The tags before the change.</summary>
    public IReadOnlyList<string> OldValue { get; }

    /// <summary>The tags after the change (Arc's <c>onValueChange</c> argument).</summary>
    public IReadOnlyList<string> NewValue { get; }

    /// <summary>Tags present in <see cref="NewValue"/> but not in <see cref="OldValue"/>.</summary>
    public IReadOnlyList<string> Added { get; }

    /// <summary>Tags present in <see cref="OldValue"/> but not in <see cref="NewValue"/>.</summary>
    public IReadOnlyList<string> Removed { get; }
}

/// <summary>
/// Arc tag input: turns short typed values into removable tags. Enter or a comma adds the trimmed draft (a duplicate,
/// compared case-insensitively, pulses the tag that exists instead); Backspace at the start of the field picks the last
/// tag and the next Backspace removes it; the arrow keys move the pick and a ring glides to it; leaving the field
/// commits the draft. New tags blur in where they were typed, removed tags leave their slot and the rest glide in, and
/// the shell follows wrapped rows on a spring; the field never scrolls sideways.
/// </summary>
/// <remarks>
/// Extensions beyond Arc: pasting text with commas, newlines or tabs adds each piece as a tag (the last piece without a
/// trailing separator stays in the draft), and <see cref="MaxTags"/> refuses adds past a limit with a shake and an
/// announcement. Tags are plain strings: validate or normalize them in <see cref="ValueChanged"/>. Suited to tens of tags.
/// </remarks>
[TemplatePart(FieldPart, typeof(Field))]
[TemplatePart(FramePart, typeof(FieldFrame))]
[TemplatePart(PanelPart, typeof(TagInputPanel))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(InvalidPseudoClass)]
public class TagInput : TemplatedControl
{
    public const string FieldPart = "PART_Field";
    public const string FramePart = "PART_Frame";
    public const string PanelPart = "PART_Panel";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsInvalid"/> is true.</summary>
    public const string InvalidPseudoClass = ":invalid";

    private static readonly char[] s_separators = [',', '\n', '\r', '\t'];

    /// <summary>The tags (two-way). Never null: setting null reads back as an empty list.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ValueProperty =
        AvaloniaProperty.Register<TagInput, IReadOnlyList<string>>(nameof(Value), Array.Empty<string>(),
            defaultBindingMode: BindingMode.TwoWay, coerce: CoerceTags);

    /// <summary>The visible label (Arc: required). Also the accessible name of the editor.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TagInput, string?>(nameof(Label));

    /// <summary>Helper copy under the field; it describes the editor for assistive technology.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<TagInput, string?>(nameof(Description));

    /// <summary>Error copy under the field; non-empty makes the field invalid.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<TagInput, string?>(nameof(ErrorMessage));

    /// <summary>Shown while there are no tags and no draft (Arc default "Add a tag").</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TagInput, string?>(nameof(Placeholder), "Add a tag");

    /// <summary>When false the label is hidden visually but still names the editor.</summary>
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        AvaloniaProperty.Register<TagInput, bool>(nameof(IsLabelVisible), true);

    /// <summary>Invalid state (danger border). Always true while <see cref="ErrorMessage"/> is not empty.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<TagInput, bool>(nameof(IsInvalid), coerce: CoerceIsInvalid);

    /// <summary>The most tags a user can add (extension; null = unlimited, as Arc). Negative values read as 0.</summary>
    public static readonly StyledProperty<int?> MaxTagsProperty =
        AvaloniaProperty.Register<TagInput, int?>(nameof(MaxTags), coerce: CoerceMaxTags);

    /// <summary>Accessible name of a tag's remove button; <c>{0}</c> is the tag.</summary>
    public static readonly StyledProperty<string> RemoveLabelFormatProperty =
        AvaloniaProperty.Register<TagInput, string>(nameof(RemoveLabelFormat), "Remove {0}");

    /// <summary>Announced after an add; <c>{0}</c> is the tag (or the tags, comma separated, after a paste).</summary>
    public static readonly StyledProperty<string> AddedAnnouncementFormatProperty =
        AvaloniaProperty.Register<TagInput, string>(nameof(AddedAnnouncementFormat), "Added {0}");

    /// <summary>Announced after a removal; <c>{0}</c> is the tag.</summary>
    public static readonly StyledProperty<string> RemovedAnnouncementFormatProperty =
        AvaloniaProperty.Register<TagInput, string>(nameof(RemovedAnnouncementFormat), "Removed {0}");

    /// <summary>Announced when the draft repeats a tag; <c>{0}</c> is the existing tag.</summary>
    public static readonly StyledProperty<string> DuplicateAnnouncementFormatProperty =
        AvaloniaProperty.Register<TagInput, string>(nameof(DuplicateAnnouncementFormat), "{0} is already added");

    /// <summary>Announced when a tag is picked; <c>{0}</c> is the tag.</summary>
    public static readonly StyledProperty<string> PickedAnnouncementFormatProperty =
        AvaloniaProperty.Register<TagInput, string>(nameof(PickedAnnouncementFormat), "{0} selected. Press Backspace to remove it.");

    /// <summary>Announced when <see cref="MaxTags"/> refuses an add; <c>{0}</c> is the limit.</summary>
    public static readonly StyledProperty<string> LimitAnnouncementFormatProperty =
        AvaloniaProperty.Register<TagInput, string>(nameof(LimitAnnouncementFormat), "You can add up to {0} tags");

    /// <summary>Raised (bubbling) whenever <see cref="Value"/> changes, by the user or in code.</summary>
    public static readonly RoutedEvent<TagInputValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<TagInput, TagInputValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    private Field? _field;
    private TextBlock? _label;
    private TagInputPanel? _panel;
    private SpecialInputsLiveRegion? _announcer;
    private string? _picked;
    private bool _chipPress;

    public TagInput()
    {
        // Tunnel: knows about a press on a tag before focus could move, so the editor does not commit on that blur.
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>The tags (two-way, never null).</summary>
    public IReadOnlyList<string> Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The visible label.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Helper copy under the field.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Error copy under the field.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>Placeholder shown while the field is empty.</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>Whether the label is shown.</summary>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <summary>Invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>The most tags a user can add; null for no limit.</summary>
    public int? MaxTags { get => GetValue(MaxTagsProperty); set => SetValue(MaxTagsProperty, value); }

    /// <summary>Accessible name format of the remove buttons.</summary>
    public string RemoveLabelFormat { get => GetValue(RemoveLabelFormatProperty); set => SetValue(RemoveLabelFormatProperty, value); }

    /// <summary>Announcement format after an add.</summary>
    public string AddedAnnouncementFormat { get => GetValue(AddedAnnouncementFormatProperty); set => SetValue(AddedAnnouncementFormatProperty, value); }

    /// <summary>Announcement format after a removal.</summary>
    public string RemovedAnnouncementFormat { get => GetValue(RemovedAnnouncementFormatProperty); set => SetValue(RemovedAnnouncementFormatProperty, value); }

    /// <summary>Announcement format for a duplicate.</summary>
    public string DuplicateAnnouncementFormat { get => GetValue(DuplicateAnnouncementFormatProperty); set => SetValue(DuplicateAnnouncementFormatProperty, value); }

    /// <summary>Announcement format for a pick.</summary>
    public string PickedAnnouncementFormat { get => GetValue(PickedAnnouncementFormatProperty); set => SetValue(PickedAnnouncementFormatProperty, value); }

    /// <summary>Announcement format when the limit refuses an add.</summary>
    public string LimitAnnouncementFormat { get => GetValue(LimitAnnouncementFormatProperty); set => SetValue(LimitAnnouncementFormatProperty, value); }

    /// <summary>Raised whenever <see cref="Value"/> changes, with the old and new lists and the difference.</summary>
    public event EventHandler<TagInputValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>The picked tag (Backspace removes it), or null.</summary>
    public string? PickedTag => ActiveTag;

    /// <summary>The text typed but not yet added.</summary>
    public string Draft => _panel?.Editor.Text ?? string.Empty;

    internal TagInputPanel? Panel => _panel;

    internal string? LastAnnouncement => _announcer?.Message;

    private string? ActiveTag => _picked is not null && IndexOf(Value, _picked) >= 0 ? _picked : null;

    // ---------------------------------------------------------------- public methods

    /// <summary>
    /// Adds <paramref name="tag"/> (trimmed) in code. Returns false for an empty tag, a duplicate (case-insensitive) or
    /// when <see cref="MaxTags"/> is reached. Code changes are not announced.
    /// </summary>
    public bool AddTag(string tag)
    {
        var trimmed = tag?.Trim() ?? string.Empty;
        var tags = Value;
        if (trimmed.Length == 0 || FindExisting(tags, trimmed) is not null || IsAtLimit(tags.Count))
        {
            return false;
        }
        SetCurrentValue(ValueProperty, Append(tags, trimmed));
        return true;
    }

    /// <summary>Removes <paramref name="tag"/> (exact match first, then case-insensitive). Returns false if it is not there.</summary>
    public bool RemoveTag(string tag)
    {
        var tags = Value;
        var target = IndexOf(tags, tag) >= 0 ? tag : FindExisting(tags, tag ?? string.Empty);
        if (target is null)
        {
            return false;
        }
        SetCurrentValue(ValueProperty, Without(tags, target));
        return true;
    }

    /// <summary>Moves keyboard focus to the field's editor.</summary>
    public bool FocusEditor(NavigationMethod method = NavigationMethod.Unspecified) => _panel?.Editor.Focus(method) == true;

    // ---------------------------------------------------------------- template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            _panel.Owner = null;
            _panel.Editor.Owner = null;
        }
        if (_field is not null)
        {
            _field.RemoveHandler(PointerPressedEvent, OnFieldPointerPressed);
            _field.TemplateApplied -= OnFieldTemplateApplied;
        }
        _label = null;
        _field = e.NameScope.Find<Field>(FieldPart);
        _panel = e.NameScope.Find<TagInputPanel>(PanelPart);
        _announcer = e.NameScope.Find<SpecialInputsLiveRegion>(AnnouncerPart);
        if (_field is not null)
        {
            _field.AddHandler(PointerPressedEvent, OnFieldPointerPressed);
            _field.TemplateApplied += OnFieldTemplateApplied;
        }
        if (_panel is not null)
        {
            _panel.Owner = this;
            _panel.Editor.Owner = this;
            _panel.SetPlaceholderText(Placeholder);
            _panel.Sync(Value, animate: false);
            UpdatePick();
            UpdatePlaceholder(animate: false);
            UpdateEditorAutomation();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            OnValueChanged(change.GetOldValue<IReadOnlyList<string>>() ?? Array.Empty<string>(),
                change.GetNewValue<IReadOnlyList<string>>() ?? Array.Empty<string>());
        }
        else if (change.Property == ErrorMessageProperty)
        {
            CoerceValue(IsInvalidProperty);
            UpdateEditorAutomation();
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == LabelProperty || change.Property == DescriptionProperty)
        {
            UpdateEditorAutomation();
        }
        else if (change.Property == PlaceholderProperty)
        {
            _panel?.SetPlaceholderText(Placeholder);
            UpdatePlaceholder(animate: false);
        }
        else if (change.Property == RemoveLabelFormatProperty)
        {
            _panel?.UpdateRemoveLabels();
        }
    }

    private void OnValueChanged(IReadOnlyList<string> oldValue, IReadOnlyList<string> newValue)
    {
        _panel?.Sync(newValue, animate: true);
        UpdatePick();
        UpdatePlaceholder(animate: true);
        if (oldValue.SequenceEqual(newValue, StringComparer.Ordinal))
        {
            return;
        }
        RaiseEvent(new TagInputValueChangedEventArgs(ValueChangedEvent, oldValue, newValue,
            Difference(newValue, oldValue), Difference(oldValue, newValue)));
    }

    private void UpdateEditorAutomation()
    {
        if (_panel is null)
        {
            return;
        }
        var editor = _panel.Editor;
        AutomationProperties.SetName(editor, Label ?? string.Empty);
        AutomationProperties.SetHelpText(editor, FieldText.HelpText(Description, ErrorMessage));
    }

    private void UpdatePlaceholder(bool animate)
    {
        if (_panel is null)
        {
            return;
        }
        var empty = Value.Count == 0;
        // The visible placeholder is the overlay; the editor keeps the text (never drawn) for assistive technology.
        _panel.Editor.PlaceholderText = empty ? Placeholder : null;
        _panel.SetPlaceholderShown(empty && string.IsNullOrEmpty(_panel.Editor.Text), animate);
    }

    // ---------------------------------------------------------------- editor callbacks

    internal bool HandleEditorKey(KeyEventArgs e)
    {
        if (_panel is null)
        {
            return false;
        }
        var editor = _panel.Editor;
        var tags = Value;
        var active = ActiveTag;
        var atStart = editor.SelectionStart == 0 && editor.SelectionEnd == 0;
        var index = active is null ? tags.Count : IndexOf(tags, active);
        switch (e.Key)
        {
            case Key.Enter:
                AddDraft();
                return true;
            case Key.Back or Key.Delete when active is not null:
                RemoveFromUser(active);
                return true;
            case Key.Back when atStart && tags.Count > 0:
                Pick(tags[^1]);
                return true;
            case Key.Left when (atStart || active is not null) && index > 0:
                Pick(tags[index - 1]);
                return true;
            case Key.Right when active is not null:
                Pick(index + 1 < tags.Count ? tags[index + 1] : null);
                return true;
            case Key.Escape when active is not null:
                Pick(null);
                return true;
            default:
                return false;
        }
    }

    internal bool HandleEditorTextInput(string text)
    {
        if (text == ",")
        {
            AddDraft();
            return true;
        }
        return text.IndexOfAny(s_separators) >= 0 && HandlePaste(text);
    }

    internal void OnDraftChanged()
    {
        // Typing clears the pick.
        Pick(null);
        UpdatePlaceholder(animate: true);
    }

    internal void OnEditorLostFocus()
    {
        if (_chipPress)
        {
            return;
        }
        AddDraft();
        Pick(null);
    }

    /// <summary>
    /// Extension: text with commas, newlines or tabs becomes tags. Each piece is trimmed; empty pieces and duplicates are
    /// skipped (an existing duplicate pulses); the last piece stays in the draft. Returns false for plain text.
    /// </summary>
    internal bool HandlePaste(string text)
    {
        if (_panel is null || text.IndexOfAny(s_separators) < 0)
        {
            return false;
        }
        var editor = _panel.Editor;
        var draft = editor.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(editor.SelectionStart, editor.SelectionEnd), 0, draft.Length);
        var end = Math.Clamp(Math.Max(editor.SelectionStart, editor.SelectionEnd), start, draft.Length);
        var after = draft[end..];
        var pieces = (draft[..start] + text + after).Split(s_separators);
        var tags = new List<string>(Value);
        var added = new List<string>();
        var refused = new List<string>();
        var duplicates = new List<string>();
        for (var i = 0; i < pieces.Length - 1; i++)
        {
            var tag = pieces[i].Trim();
            if (tag.Length == 0)
            {
                continue;
            }
            if (FindExisting(tags, tag) is { } existing)
            {
                if (IndexOf(added, existing) < 0 && IndexOf(duplicates, existing) < 0)
                {
                    duplicates.Add(existing);
                }
                continue;
            }
            if (IsAtLimit(tags.Count))
            {
                refused.Add(tag);
                continue;
            }
            tags.Add(tag);
            added.Add(tag);
        }
        var last = pieces[^1];
        var rest = last.TrimStart();
        var caret = Math.Max(0, rest.Length - after.Length);
        if (refused.Count > 0)
        {
            var prefix = string.Join(", ", refused) + (rest.Length > 0 ? ", " : string.Empty);
            rest = prefix + rest;
            caret += prefix.Length;
        }
        if (added.Count > 0)
        {
            SetCurrentValue(ValueProperty, tags.ToArray());
        }
        editor.Text = rest;
        editor.CaretIndex = Math.Min(caret, rest.Length);
        Pick(null);
        foreach (var duplicate in duplicates)
        {
            _panel.FindChip(duplicate)?.Pulse();
        }
        var messages = new List<string>(2);
        if (added.Count > 0)
        {
            messages.Add(Format(AddedAnnouncementFormat, string.Join(", ", added)));
        }
        if (refused.Count > 0)
        {
            _panel.Shake();
            messages.Add(Format(LimitAnnouncementFormat, MaxTags ?? 0));
        }
        if (messages.Count == 0 && duplicates.Count > 0)
        {
            messages.Add(Format(DuplicateAnnouncementFormat, duplicates[0]));
        }
        if (messages.Count > 0)
        {
            Announce(string.Join(". ", messages));
        }
        return true;
    }

    /// <summary>Arc <c>add()</c>: the trimmed draft becomes a tag, or a duplicate pulses, or the limit refuses it.</summary>
    private void AddDraft()
    {
        if (_panel is null)
        {
            return;
        }
        var tag = (_panel.Editor.Text ?? string.Empty).Trim();
        if (tag.Length == 0)
        {
            return;
        }
        var tags = Value;
        if (FindExisting(tags, tag) is { } existing)
        {
            // A duplicate pulses the tag that already exists, so the ignored Enter still gets an answer.
            _panel.FindChip(existing)?.Pulse();
            Announce(Format(DuplicateAnnouncementFormat, existing));
            return;
        }
        if (IsAtLimit(tags.Count))
        {
            _panel.Shake();
            Announce(Format(LimitAnnouncementFormat, MaxTags ?? 0));
            return;
        }
        SetCurrentValue(ValueProperty, Append(tags, tag));
        _panel.Editor.Text = string.Empty;
        Pick(null);
        Announce(Format(AddedAnnouncementFormat, tag));
    }

    /// <summary>Arc <c>remove()</c>: from the button or Backspace / Delete on a pick; focus returns to the editor.</summary>
    internal void RemoveFromUser(string tag)
    {
        SetCurrentValue(ValueProperty, Without(Value, tag));
        Pick(null);
        Announce(Format(RemovedAnnouncementFormat, tag));
        _panel?.Editor.Focus(NavigationMethod.Unspecified);
    }

    internal void OnChipClicked(string tag)
    {
        _chipPress = false;
        Pick(ActiveTag == tag ? null : tag);
        _panel?.Editor.Focus(NavigationMethod.Pointer);
    }

    internal void OnChipPressCancelled() => _chipPress = false;

    private void Pick(string? tag)
    {
        _picked = tag;
        UpdatePick();
        if (tag is not null)
        {
            Announce(Format(PickedAnnouncementFormat, tag));
        }
    }

    private void UpdatePick()
    {
        if (_picked is not null && IndexOf(Value, _picked) < 0)
        {
            _picked = null;
        }
        _panel?.SetPicked(_picked is null ? null : _panel.FindChip(_picked));
    }

    private void Announce(string message) => _announcer?.Announce(message);

    internal string FormatRemoveLabel(string tag) => Format(RemoveLabelFormat, tag);

    // ---------------------------------------------------------------- pointer

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<TagInputChip>(includeSelf: true) is { IsLeaving: false } chip &&
            !chip.IsInRemoveButton(source) && e.GetCurrentPoint(chip).Properties.IsLeftButtonPressed)
        {
            _chipPress = true;
        }
    }

    private void OnFieldTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // Arc's <label> is a block: its whole row takes the press, not only the glyphs.
        _label = e.NameScope.Find<TextBlock>(Field.LabelPart);
        _label?.SetCurrentValue(TextBlock.BackgroundProperty, Avalonia.Media.Brushes.Transparent);
    }

    private void OnFieldPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // <label for>: a press on the label focuses the editor.
        if (e.Handled || _label is null || !ReferenceEquals(e.Source, _label) || _panel is null)
        {
            return;
        }
        if (_panel.Editor.Focus(NavigationMethod.Pointer))
        {
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------- helpers

    private bool IsAtLimit(int count) => MaxTags is { } max && count >= max;

    private static string? FindExisting(IReadOnlyList<string> tags, string tag)
    {
        // Arc compares with toLowerCase().
        var lower = tag.ToLowerInvariant();
        foreach (var item in tags)
        {
            if (string.Equals(item.ToLowerInvariant(), lower, StringComparison.Ordinal))
            {
                return item;
            }
        }
        return null;
    }

    private static int IndexOf(IReadOnlyList<string> tags, string tag)
    {
        for (var i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static string[] Append(IReadOnlyList<string> tags, string tag)
    {
        var next = new string[tags.Count + 1];
        for (var i = 0; i < tags.Count; i++)
        {
            next[i] = tags[i];
        }
        next[^1] = tag;
        return next;
    }

    private static string[] Without(IReadOnlyList<string> tags, string tag)
    {
        var next = new List<string>(tags.Count);
        foreach (var item in tags)
        {
            if (!string.Equals(item, tag, StringComparison.Ordinal))
            {
                next.Add(item);
            }
        }
        return next.ToArray();
    }

    /// <summary>Items of <paramref name="a"/> not matched in <paramref name="b"/> (multiset difference, ordinal).</summary>
    private static IReadOnlyList<string> Difference(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in b)
        {
            counts[item] = counts.GetValueOrDefault(item) + 1;
        }
        var result = new List<string>();
        foreach (var item in a)
        {
            if (counts.TryGetValue(item, out var n) && n > 0)
            {
                counts[item] = n - 1;
            }
            else
            {
                result.Add(item);
            }
        }
        return result.Count == 0 ? Array.Empty<string>() : result.ToArray();
    }

    private static string Format(string? format, object argument)
    {
        if (string.IsNullOrEmpty(format))
        {
            return Convert.ToString(argument, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, argument);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private static IReadOnlyList<string> CoerceTags(AvaloniaObject target, IReadOnlyList<string> value) =>
        value ?? Array.Empty<string>();

    private static bool CoerceIsInvalid(AvaloniaObject target, bool value) =>
        value || !string.IsNullOrEmpty(((TagInput)target).ErrorMessage);

    private static int? CoerceMaxTags(AvaloniaObject target, int? value) => value is < 0 ? 0 : value;
}
