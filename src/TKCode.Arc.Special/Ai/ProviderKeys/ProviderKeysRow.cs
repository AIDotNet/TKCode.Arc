using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Where a row's inline form is.</summary>
internal enum ProviderKeysFormPhase
{
    /// <summary>The form is closed.</summary>
    Closed,

    /// <summary>The person types; the format is checked on every change.</summary>
    Editing,

    /// <summary>Save was pressed: Testing connection.</summary>
    Testing,

    /// <summary>The key passed: Connected shows for a beat before the row updates and the form closes.</summary>
    Connected
}

/// <summary>
/// One provider row of <see cref="ProviderKeys"/>: the mark, the name over the masked key, the status over when the key
/// was last used, and the actions (reveal with a countdown ring, copy, rotate, remove), or Add key on an empty row and
/// Undo after a removal. Add key and Rotate open the inline form under the row. The row keeps its own passing states
/// (revealed, retesting, the form, the undo window); the card owns the keys.
/// </summary>
[TemplatePart(MarkPart, typeof(Border))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(KeyPart, typeof(ArcContentSwap))]
[TemplatePart(StatusPart, typeof(ProviderKeysStatusButton))]
[TemplatePart(StatusSwapPart, typeof(ArcContentSwap))]
[TemplatePart(MetaPart, typeof(ArcContentSwap))]
[TemplatePart(KeyActionsPart, typeof(Panel))]
[TemplatePart(RevealPart, typeof(ProviderKeysRevealButton))]
[TemplatePart(RevealRingPart, typeof(ArcRing))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(RotatePart, typeof(ProviderKeysDisclosureButton))]
[TemplatePart(RemovePart, typeof(ProviderKeysIconButton))]
[TemplatePart(AddPart, typeof(ProviderKeysDisclosureButton))]
[TemplatePart(UndoPart, typeof(AvaloniaButton))]
[TemplatePart(FormBoxPart, typeof(AiHeightBox))]
[TemplatePart(InputPart, typeof(Input))]
[TemplatePart(CancelPart, typeof(AvaloniaButton))]
[TemplatePart(SavePart, typeof(Button))]
[TemplatePart(HelperPart, typeof(ArcContentSwap))]
[PseudoClasses(NarrowPseudoClass, FirstPseudoClass, PlaceholderPseudoClass, RevealedPseudoClass)]
internal sealed class ProviderKeysRow : TemplatedControl
{
    internal const string MarkPart = "PART_Mark";
    internal const string MarkGlyphPart = "PART_MarkGlyph";
    internal const string MonogramPart = "PART_Monogram";
    internal const string NamePart = "PART_Name";
    internal const string KeyPart = "PART_Key";
    internal const string StatusPart = "PART_Status";
    internal const string StatusSwapPart = "PART_StatusSwap";
    internal const string MetaPart = "PART_Meta";
    internal const string KeyActionsPart = "PART_KeyActions";
    internal const string RevealPart = "PART_Reveal";
    internal const string RevealGlyphPart = "PART_RevealGlyph";
    internal const string RevealRingPart = "PART_RevealRing";
    internal const string CopyPart = "PART_Copy";
    internal const string RotatePart = "PART_Rotate";
    internal const string RemovePart = "PART_Remove";
    internal const string AddPart = "PART_Add";
    internal const string AddTextPart = "PART_AddText";
    internal const string UndoPart = "PART_Undo";
    internal const string UndoRingPart = "PART_UndoRing";
    internal const string UndoTextPart = "PART_UndoText";
    internal const string FormBoxPart = "PART_FormBox";
    internal const string InputPart = "PART_Input";
    internal const string CancelPart = "PART_Cancel";
    internal const string SavePart = "PART_Save";
    internal const string HelperPart = "PART_Helper";

    internal const string NarrowPseudoClass = ":narrow";
    internal const string FirstPseudoClass = ":first";
    internal const string PlaceholderPseudoClass = ":placeholder";
    internal const string RevealedPseudoClass = ":revealed";

    /// <summary>How long Connected shows in the form before the row takes the new key.</summary>
    internal static readonly TimeSpan ConnectedHold = TimeSpan.FromMilliseconds(650);

    private readonly ProviderKeys _owner;
    private Border? _mark;
    private ArcGlyph? _markGlyph;
    private TextBlock? _monogram;
    private TextBlock? _name;
    private ArcContentSwap? _key;
    private ProviderKeysStatusButton? _status;
    private ArcContentSwap? _statusSwap;
    private ArcContentSwap? _meta;
    private Panel? _keyActions;
    private ProviderKeysRevealButton? _reveal;
    private ProviderKeysSwapGlyph? _revealGlyph;
    private ArcRing? _revealRing;
    private CopyButton? _copy;
    private ProviderKeysDisclosureButton? _rotate;
    private ProviderKeysIconButton? _remove;
    private ProviderKeysDisclosureButton? _add;
    private TextBlock? _addText;
    private AvaloniaButton? _undo;
    private ArcRing? _undoRing;
    private TextBlock? _undoText;
    private AiHeightBox? _formBox;
    private Input? _input;
    private AvaloniaButton? _cancel;
    private Button? _save;
    private ArcContentSwap? _helper;

    private string? _shownKey;
    private string? _shownStatus;
    private string? _shownMeta;
    private string? _shownHelper;

    private MotionTimeline? _revealTimeline;
    private MotionTimeline? _undoTimeline;
    private CancellationTokenSource? _retestCts;
    private CancellationTokenSource? _formCts;
    private ProviderKeySaveMode _formMode;
    private string? _formError;
    private string? _formDetail;
    private bool _formErrorIsRejection;
    private string? _errorFor;

    public ProviderKeysRow(ProviderKeys owner, ProviderKeyProvider provider)
    {
        SpecialFocusModality.Watch();
        _owner = owner;
        Provider = provider;
    }

    /// <summary>The provider this row shows.</summary>
    public ProviderKeyProvider Provider { get; }

    /// <summary>The stored key, or null for an empty row.</summary>
    internal ProviderKey? Key { get; private set; }

    /// <summary>Whether the full key is on screen.</summary>
    internal bool IsRevealed { get; private set; }

    /// <summary>Whether the stored key is being tested again.</summary>
    internal bool IsRetesting { get; private set; }

    /// <summary>The key removed a moment ago, while Undo is on screen.</summary>
    internal ProviderKey? PendingRemoval { get; private set; }

    /// <summary>Where the inline form is.</summary>
    internal ProviderKeysFormPhase FormPhase { get; private set; }

    /// <summary>Whether the form adds a key or rotates one (while it is open).</summary>
    internal ProviderKeySaveMode FormMode => _formMode;

    /// <summary>Whether the row shows placeholder bars while keys load.</summary>
    internal bool IsPlaceholder { get; private set; }

    /// <summary>The texts on screen, for tests.</summary>
    internal string? ShownKey => _shownKey;

    internal string? ShownStatus => _shownStatus;

    internal string? ShownMeta => _shownMeta;

    internal string? ShownHelper => _shownHelper;

    internal ArcRing? RevealRing => _revealRing;

    internal ArcRing? UndoRing => _undoRing;

    internal AiHeightBox? FormBox => _formBox;

    private ProviderKeysLabels Labels => _owner.Labels;

    private string ProviderName => Provider.Name;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _mark = e.NameScope.Find<Border>(MarkPart);
        _markGlyph = e.NameScope.Find<ArcGlyph>(MarkGlyphPart);
        _monogram = e.NameScope.Find<TextBlock>(MonogramPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _key = e.NameScope.Find<ArcContentSwap>(KeyPart);
        _status = e.NameScope.Find<ProviderKeysStatusButton>(StatusPart);
        _statusSwap = e.NameScope.Find<ArcContentSwap>(StatusSwapPart);
        _meta = e.NameScope.Find<ArcContentSwap>(MetaPart);
        _keyActions = e.NameScope.Find<Panel>(KeyActionsPart);
        _reveal = e.NameScope.Find<ProviderKeysRevealButton>(RevealPart);
        _revealGlyph = e.NameScope.Find<ProviderKeysSwapGlyph>(RevealGlyphPart);
        _revealRing = e.NameScope.Find<ArcRing>(RevealRingPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _rotate = e.NameScope.Find<ProviderKeysDisclosureButton>(RotatePart);
        _remove = e.NameScope.Find<ProviderKeysIconButton>(RemovePart);
        _add = e.NameScope.Find<ProviderKeysDisclosureButton>(AddPart);
        _addText = e.NameScope.Find<TextBlock>(AddTextPart);
        _undo = e.NameScope.Find<AvaloniaButton>(UndoPart);
        _undoRing = e.NameScope.Find<ArcRing>(UndoRingPart);
        _undoText = e.NameScope.Find<TextBlock>(UndoTextPart);
        _formBox = e.NameScope.Find<AiHeightBox>(FormBoxPart);
        if (_formBox is not null)
        {
            _formBox.HeightSpring = AiHeightBox.PanelSpring;
        }
        _input = e.NameScope.Find<Input>(InputPart);
        _cancel = e.NameScope.Find<AvaloniaButton>(CancelPart);
        _save = e.NameScope.Find<Button>(SavePart);
        _helper = e.NameScope.Find<ArcContentSwap>(HelperPart);

        if (_status is not null)
        {
            _status.Click += OnStatusClick;
        }
        if (_reveal is not null)
        {
            _reveal.IsCheckedChanged += OnRevealChanged;
        }
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
        }
        if (_rotate is not null)
        {
            _rotate.Click += OnRotateClick;
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        if (_undo is not null)
        {
            _undo.Click += OnUndoClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }
        if (_input is not null)
        {
            _input.PropertyChanged += OnInputChanged;
            _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        }
        _shownKey = _shownStatus = _shownMeta = _shownHelper = null;
        if (_formBox is not null && FormPhase != ProviderKeysFormPhase.Closed)
        {
            _formBox.IsExpanded = true;
        }
        Refresh();
    }

    private void Unhook()
    {
        if (_status is not null)
        {
            _status.Click -= OnStatusClick;
        }
        if (_reveal is not null)
        {
            _reveal.IsCheckedChanged -= OnRevealChanged;
        }
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        if (_rotate is not null)
        {
            _rotate.Click -= OnRotateClick;
        }
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        if (_undo is not null)
        {
            _undo.Click -= OnUndoClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }
        if (_input is not null)
        {
            _input.PropertyChanged -= OnInputChanged;
            _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        }
    }

    /// <summary>Sets the stored key (null for an empty row) and redraws what changed.</summary>
    internal void SetKey(ProviderKey? key)
    {
        if (key is not null && key.Status == ProviderKeyStatus.Unset)
        {
            key = null;
        }
        var lostValue = IsRevealed && key?.Value is null;
        Key = key;
        if (key is null || lostValue)
        {
            Hide(announce: false);
        }
        if (key is null && FormPhase != ProviderKeysFormPhase.Closed && _formMode == ProviderKeySaveMode.Rotate)
        {
            CloseForm(returnFocus: false);
        }
        Refresh();
    }

    internal void SetNarrow(bool narrow) => PseudoClasses.Set(NarrowPseudoClass, narrow);

    internal void SetFirst(bool first) => PseudoClasses.Set(FirstPseudoClass, first);

    internal void SetPlaceholder(bool placeholder)
    {
        if (IsPlaceholder == placeholder)
        {
            return;
        }
        IsPlaceholder = placeholder;
        PseudoClasses.Set(PlaceholderPseudoClass, placeholder);
        if (placeholder)
        {
            Hide(announce: false);
            CloseForm(returnFocus: false);
        }
        Refresh();
    }

    /// <summary>The 30-second clock: only the last-used line can change.</summary>
    internal void RefreshTimes() => Refresh();

    /// <summary>Brings every part in line with the state; a text that already shows is left alone, so only changes swap.</summary>
    internal void Refresh()
    {
        if (_name is null)
        {
            return;
        }
        var labels = Labels;
        var key = Key;
        _name.Text = ProviderName;
        UpdateMark();

        // Key line.
        string keyText;
        if (IsPlaceholder)
        {
            keyText = "\u0000placeholder";
        }
        else if (PendingRemoval is not null)
        {
            keyText = labels.KeyRemoved;
        }
        else if (key is null)
        {
            keyText = labels.NoKey;
        }
        else if (IsRevealed && key.Value is { } value)
        {
            keyText = value;
        }
        else
        {
            keyText = Mask(key, Provider);
        }
        if (_key is not null && _shownKey != keyText)
        {
            _shownKey = keyText;
            _key.Content = IsPlaceholder ? Bar(156) : MakeText(keyText, mono: key is not null && PendingRemoval is null, strong: IsRevealed);
        }

        // Status.
        var (statusKind, statusText) = StatusOf(labels);
        var statusId = IsPlaceholder ? "\u0000placeholder" : statusKind + ":" + statusText;
        if (_statusSwap is not null && _shownStatus != statusId)
        {
            _shownStatus = statusId;
            _statusSwap.Content = IsPlaceholder ? Bar(72) : MakeStatus(statusKind, statusText);
        }
        if (_status is not null)
        {
            var retestable = !IsPlaceholder && key is not null && !IsRetesting && PendingRemoval is null;
            _status.Focusable = retestable;
            _status.IsHitTestVisible = retestable;
            _status.Classes.Set("static", !retestable);
            AutomationProperties.SetName(_status, key is null
                ? ProviderName + ": " + statusText
                : labels.RetestName(ProviderName, statusText, key.Status == ProviderKeyStatus.Invalid ? key.Error : null));
        }

        // Last used.
        var metaText = IsPlaceholder ? "\u0000placeholder" : MetaOf(labels);
        if (_meta is not null && _shownMeta != metaText)
        {
            _shownMeta = metaText;
            _meta.Content = IsPlaceholder ? Bar(96) : MakeText(metaText, mono: false);
        }

        // Actions.
        var hasKey = key is not null && !IsPlaceholder;
        if (_keyActions is not null)
        {
            _keyActions.IsVisible = hasKey;
        }
        if (_add is not null)
        {
            _add.IsVisible = !hasKey && PendingRemoval is null && !IsPlaceholder;
            _add.IsExpanded = FormPhase != ProviderKeysFormPhase.Closed && _formMode == ProviderKeySaveMode.Add;
            AutomationProperties.SetName(_add, labels.AddName(ProviderName));
        }
        if (_addText is not null)
        {
            _addText.Text = labels.AddKey;
        }
        if (_undo is not null)
        {
            _undo.IsVisible = PendingRemoval is not null && !IsPlaceholder;
            AutomationProperties.SetName(_undo, labels.UndoName(ProviderName));
        }
        if (_undoText is not null)
        {
            _undoText.Text = labels.Undo;
        }
        var hasValue = key?.Value is not null;
        if (_reveal is not null)
        {
            _reveal.IsEnabled = hasValue;
            if (_reveal.IsChecked != IsRevealed)
            {
                _reveal.IsChecked = IsRevealed;
            }
            AutomationProperties.SetName(_reveal, labels.RevealName(ProviderName));
            Tooltip.SetTip(_reveal, hasValue ? null : labels.ValueHidden);
        }
        if (_revealGlyph is not null)
        {
            _revealGlyph.Data = IsRevealed ? ArcIcons.EyeOff : ArcIcons.Eye;
        }
        if (_copy is not null)
        {
            _copy.IsEnabled = hasValue;
            if (_copy.Value != key?.Value)
            {
                _copy.Value = key?.Value;
            }
            AutomationProperties.SetName(_copy, labels.CopyName(ProviderName));
        }
        if (_rotate is not null)
        {
            _rotate.IsExpanded = FormPhase != ProviderKeysFormPhase.Closed && _formMode == ProviderKeySaveMode.Rotate;
            AutomationProperties.SetName(_rotate, labels.RotateName(ProviderName));
        }
        if (_remove is not null)
        {
            AutomationProperties.SetName(_remove, labels.RemoveName(ProviderName));
        }
        PseudoClasses.Set(RevealedPseudoClass, IsRevealed);
        RefreshForm();
    }

    private void UpdateMark()
    {
        if (_markGlyph is not null)
        {
            _markGlyph.Data = Provider.Mark;
            _markGlyph.IsVisible = Provider.Mark is not null;
            if (Provider.MarkBrush is { } brush)
            {
                _markGlyph.Foreground = brush;
            }
        }
        if (_monogram is not null)
        {
            _monogram.IsVisible = Provider.Mark is null;
            _monogram.Text = Provider.Monogram ?? (ProviderName.Length > 0 ? ProviderName[..1].ToUpperInvariant() : string.Empty);
            if (Provider.MarkBrush is { } brush)
            {
                _monogram.Foreground = brush;
            }
        }
    }

    private (string Kind, string Text) StatusOf(ProviderKeysLabels labels)
    {
        if (IsRetesting)
        {
            return ("testing", labels.Testing);
        }
        return Key?.Status switch
        {
            ProviderKeyStatus.Connected => ("connected", labels.Connected),
            ProviderKeyStatus.Invalid => ("invalid", labels.Invalid),
            _ => ("unset", labels.NotSet),
        };
    }

    private string MetaOf(ProviderKeysLabels labels)
    {
        var key = Key;
        if (key is null)
        {
            return string.Empty;
        }
        if (key.Status == ProviderKeyStatus.Invalid)
        {
            return string.IsNullOrEmpty(key.Error) ? labels.Rejected : key.Error!;
        }
        if (key.LastUsedAt is not { } used)
        {
            return labels.NeverUsed;
        }
        var age = _owner.Now() - used;
        return labels.LastUsed(age < TimeSpan.Zero ? TimeSpan.Zero : age);
    }

    /// <summary>The masked key: the prefix, four dots and the last four characters ("sk-••••4f2a").</summary>
    internal static string Mask(ProviderKey key, ProviderKeyProvider provider)
    {
        var prefix = provider.KeyPrefix ?? string.Empty;
        if (key.Value is { } value && prefix.Length > 0 && !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            prefix = value.Length > 8 ? value[..3] : string.Empty;
        }
        return prefix + "••••" + key.DisplayLast4;
    }

    private static TextBlock MakeText(string text, bool mono, bool strong = false)
    {
        var block = new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (mono)
        {
            ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        }
        if (strong)
        {
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        }
        return block;
    }

    /// <summary>The color of a status or helper icon and its words.</summary>
    private static (ArcDesignTokenKind Icon, ArcDesignTokenKind Text) InksOf(string kind) => kind switch
    {
        "connected" => (ArcDesignTokenKind.Success, ArcDesignTokenKind.Foreground),
        "valid" => (ArcDesignTokenKind.Success, ArcDesignTokenKind.TextMuted),
        "invalid" => (ArcDesignTokenKind.Danger, ArcDesignTokenKind.Danger),
        "testing" => (ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TextSecondary),
        _ => (ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TextMuted),
    };

    /// <summary>Status as icon plus label, never color alone.</summary>
    private static Control MakeStatus(string kind, string text)
    {
        var (iconInk, textInk) = InksOf(kind);
        Control icon;
        if (kind == "testing")
        {
            var spinner = new ProviderKeysSpinner { VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(spinner, ProviderKeysSpinner.ForegroundProperty, iconInk);
            icon = spinner;
        }
        else
        {
            var glyph = new ArcGlyph
            {
                Data = kind switch
                {
                    "connected" => ArcIcons.CircleCheck,
                    "invalid" => ArcIcons.CircleAlert,
                    _ => ProviderKeysIcons.CircleDashed,
                },
                Size = 14,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, iconInk);
            icon = glyph;
        }
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, textInk);
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { icon, label } };
    }

    private static Border Bar(double width)
    {
        var bar = new Border
        {
            Width = width,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep5);
        ArcTokens.Bind(bar, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        return bar;
    }

    // ---------------------------------------------------------------- reveal

    private void OnRevealChanged(object? sender, RoutedEventArgs e)
    {
        var want = _reveal?.IsChecked == true;
        if (want == IsRevealed)
        {
            return;
        }
        if (want)
        {
            Reveal();
        }
        else
        {
            Hide(announce: true);
        }
    }

    /// <summary>Shows the full key for <see cref="ProviderKeys.RevealDuration"/>, with a hairline ring that empties linearly.</summary>
    internal void Reveal()
    {
        if (Key?.Value is null || IsRevealed)
        {
            if (_reveal is not null && _reveal.IsChecked != IsRevealed)
            {
                _reveal.IsChecked = IsRevealed;
            }
            return;
        }
        IsRevealed = true;
        var duration = _owner.RevealDuration;
        if (_revealTimeline is null)
        {
            _revealTimeline = new MotionTimeline(this);
            _revealTimeline.Changed += value =>
            {
                if (_revealRing is not null)
                {
                    _revealRing.Value = 1 - value;
                }
            };
            _revealTimeline.Completed += () => Hide(announce: true);
        }
        if (_revealRing is not null)
        {
            // Reduced motion keeps the reveal and its timer but drops the countdown ring.
            _revealRing.IsVisible = ArcMotion.IsFull(this);
            _revealRing.Value = 1;
        }
        _revealTimeline.Start(duration);
        _owner.Announce(Labels.RevealedAnnouncement(ProviderName, (int)Math.Round(duration.TotalSeconds)), simulated: false);
        Refresh();
    }

    /// <summary>Masks the key again (the countdown ended, the toggle was pressed, or the key went away).</summary>
    internal void Hide(bool announce)
    {
        if (!IsRevealed)
        {
            return;
        }
        IsRevealed = false;
        _revealTimeline?.Stop();
        if (_revealRing is not null)
        {
            _revealRing.IsVisible = false;
            _revealRing.Value = 0;
        }
        if (announce)
        {
            _owner.Announce(Labels.HiddenAnnouncement(ProviderName), simulated: false);
        }
        Refresh();
    }

    /// <summary>Ends the reveal as if its time had run out (tests).</summary>
    internal void ExpireReveal()
    {
        if (IsRevealed)
        {
            Hide(announce: true);
        }
    }

    // ---------------------------------------------------------------- copy

    private void OnCopied(object? sender, EventArgs e) => _owner.OnCopied(this);

    // ---------------------------------------------------------------- retest

    private void OnStatusClick(object? sender, RoutedEventArgs e) => _ = RetestAsync();

    /// <summary>Tests the stored key again: Testing, then the result replaces the row's status.</summary>
    internal async Task RetestAsync()
    {
        if (Key is not { } key || IsRetesting || IsPlaceholder)
        {
            return;
        }
        _retestCts?.Cancel();
        var cts = _retestCts = new CancellationTokenSource();
        IsRetesting = true;
        Refresh();
        ProviderKeyTestResult result;
        try
        {
            result = await _owner.RunTestAsync(Provider, key, null, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_retestCts, cts))
            {
                _retestCts = null;
                IsRetesting = false;
            }
        }
        if (cts.IsCancellationRequested || !ReferenceEquals(Key, key))
        {
            Refresh();
            return;
        }
        var updated = key.Copy();
        updated.Status = result.IsOk ? ProviderKeyStatus.Connected : ProviderKeyStatus.Invalid;
        updated.Error = result.IsOk ? null : ErrorOf(result);
        updated.CheckedAt = _owner.Now();
        _owner.Replace(this, updated, ProviderKeysChange.Retested);
        _owner.Announce(result.IsOk ? Labels.PassedAnnouncement(ProviderName) : Labels.FailedAnnouncement(ProviderName, ErrorOf(result)), simulated: true);
    }

    private string ErrorOf(ProviderKeyTestResult result) =>
        string.IsNullOrEmpty(result.Error) ? Labels.UnknownError : result.Error!;

    // ---------------------------------------------------------------- remove and undo

    private void OnRemoveClick(object? sender, RoutedEventArgs e) => Remove();

    /// <summary>Removes the key at once (the card's keys change) and shows Undo for <see cref="ProviderKeys.UndoDuration"/>.</summary>
    internal void Remove()
    {
        if (Key is not { } key || IsPlaceholder)
        {
            return;
        }
        CommitPendingRemoval();
        Hide(announce: false);
        CloseForm(returnFocus: false);
        _retestCts?.Cancel();
        PendingRemoval = key;
        _owner.Replace(this, null, ProviderKeysChange.Removed);
        var duration = _owner.UndoDuration;
        if (_undoTimeline is null)
        {
            _undoTimeline = new MotionTimeline(this);
            _undoTimeline.Changed += value =>
            {
                if (_undoRing is not null)
                {
                    _undoRing.Value = 1 - value;
                }
            };
            _undoTimeline.Completed += CommitPendingRemoval;
        }
        if (_undoRing is not null)
        {
            _undoRing.IsVisible = ArcMotion.IsFull(this);
            _undoRing.Value = 1;
        }
        _undoTimeline.Start(duration);
        _owner.Announce(Labels.RemovedAnnouncement(ProviderName, (int)Math.Round(duration.TotalSeconds)), simulated: false);
        Refresh();
        FocusLater(() => _undo);
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Undo();

    /// <summary>Puts the removed key back and returns focus to Remove.</summary>
    internal void Undo()
    {
        if (PendingRemoval is not { } key)
        {
            return;
        }
        _undoTimeline?.Stop();
        PendingRemoval = null;
        _owner.Replace(this, key, ProviderKeysChange.Restored);
        _owner.Announce(Labels.RestoredAnnouncement(ProviderName), simulated: false);
        Refresh();
        FocusLater(() => _remove);
    }

    /// <summary>The undo window closed: the removal sticks and reaches the app.</summary>
    internal void CommitPendingRemoval()
    {
        if (PendingRemoval is null)
        {
            return;
        }
        var focusWasOnUndo = _undo?.IsKeyboardFocusWithin == true;
        _undoTimeline?.Stop();
        PendingRemoval = null;
        Refresh();
        _owner.OnRemoved(this);
        if (focusWasOnUndo)
        {
            FocusLater(() => _add);
        }
    }

    // ---------------------------------------------------------------- the inline form

    private void OnRotateClick(object? sender, RoutedEventArgs e) => ToggleForm(ProviderKeySaveMode.Rotate);

    private void OnAddClick(object? sender, RoutedEventArgs e) => ToggleForm(ProviderKeySaveMode.Add);

    private void ToggleForm(ProviderKeySaveMode mode)
    {
        if (FormPhase != ProviderKeysFormPhase.Closed && _formMode == mode)
        {
            CloseForm(returnFocus: true);
        }
        else
        {
            OpenForm(mode);
        }
    }

    /// <summary>Opens the form under the row and focuses its input.</summary>
    internal void OpenForm(ProviderKeySaveMode mode)
    {
        if (IsPlaceholder)
        {
            return;
        }
        _formCts?.Cancel();
        _formCts = null;
        _formMode = mode;
        FormPhase = ProviderKeysFormPhase.Editing;
        _formError = null;
        _formDetail = null;
        _formErrorIsRejection = false;
        if (_input is not null)
        {
            _input.Text = string.Empty;
        }
        if (_formBox is not null)
        {
            _formBox.IsExpanded = true;
        }
        Refresh();
        FocusLater(() => _input);
    }

    /// <summary>Closes the form, aborting a test in flight; returns focus to the button that opened it when asked.</summary>
    internal void CloseForm(bool returnFocus)
    {
        if (FormPhase == ProviderKeysFormPhase.Closed)
        {
            return;
        }
        _formCts?.Cancel();
        _formCts = null;
        var mode = _formMode;
        FormPhase = ProviderKeysFormPhase.Closed;
        if (_formBox is not null)
        {
            _formBox.IsExpanded = false;
        }
        Refresh();
        if (returnFocus)
        {
            FocusLater(() => mode == ProviderKeySaveMode.Rotate || Key is not null ? _rotate : _add);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => CloseForm(returnFocus: true);

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SubmitAsync();

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            _ = SubmitAsync();
        }
        else if (e.Key == Avalonia.Input.Key.Escape)
        {
            e.Handled = true;
            CloseForm(returnFocus: true);
        }
    }

    /// <summary>Checks the format on every change of the text (the property, so the check is never a frame late).</summary>
    private void OnInputChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || FormPhase != ProviderKeysFormPhase.Editing)
        {
            return;
        }
        if (_formError is not null && string.Equals((_input?.Text ?? string.Empty).Trim(), _errorFor, StringComparison.Ordinal))
        {
            return;
        }
        _formError = null;
        _formErrorIsRejection = false;
        RefreshForm();
    }

    /// <summary>The format error for what is typed, or null.</summary>
    private string? FormatError(ProviderKeyFormat format) => format switch
    {
        ProviderKeyFormat.WrongPrefix => Labels.WrongPrefix(ProviderName, Provider.KeyPrefix),
        ProviderKeyFormat.BadCharacters => Labels.BadCharacters,
        _ => null,
    };

    /// <summary>Checks the format, then tests the key: Testing connection, Connected, then the row takes the key.</summary>
    internal async Task SubmitAsync()
    {
        if (FormPhase != ProviderKeysFormPhase.Editing || _input is null)
        {
            return;
        }
        var text = (_input.Text ?? string.Empty).Trim();
        var format = Provider.CheckFormat(text);
        if (format != ProviderKeyFormat.Valid)
        {
            _formError = format switch
            {
                ProviderKeyFormat.Empty => Labels.FormHint(ProviderName, Provider.KeyPrefix),
                ProviderKeyFormat.TooShort => Labels.TooShort,
                _ => FormatError(format),
            };
            _formErrorIsRejection = false;
            _errorFor = text;
            RefreshForm();
            _owner.Announce(_formError, simulated: false);
            return;
        }
        var mode = _formMode;
        var cts = _formCts = new CancellationTokenSource();
        FormPhase = ProviderKeysFormPhase.Testing;
        _formError = null;
        RefreshForm();
        ProviderKeyTestResult result;
        try
        {
            result = await _owner.RunTestAsync(Provider, Key, text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cts.IsCancellationRequested || FormPhase != ProviderKeysFormPhase.Testing)
        {
            return;
        }
        if (!result.IsOk)
        {
            FormPhase = ProviderKeysFormPhase.Editing;
            _formError = Labels.RejectedDetail(ErrorOf(result));
            _formErrorIsRejection = true;
            _errorFor = text;
            RefreshForm();
            _owner.Announce(Labels.FailedAnnouncement(ProviderName, ErrorOf(result)), simulated: true);
            FocusLater(() => _input);
            return;
        }
        FormPhase = ProviderKeysFormPhase.Connected;
        _formDetail = result.Detail;
        RefreshForm();
        try
        {
            await _owner.DelayAsync(ConnectedHold, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cts.IsCancellationRequested || FormPhase != ProviderKeysFormPhase.Connected)
        {
            return;
        }
        // Rotation keeps the old key until here: only now does the row swap to the new one.
        var now = _owner.Now();
        var saved = new ProviderKey
        {
            Provider = Provider.Id,
            Status = ProviderKeyStatus.Connected,
            Value = text,
            CheckedAt = now,
        };
        _formCts = null;
        FormPhase = ProviderKeysFormPhase.Closed;
        if (_formBox is not null)
        {
            _formBox.IsExpanded = false;
        }
        _owner.Replace(this, saved, mode == ProviderKeySaveMode.Add ? ProviderKeysChange.Added : ProviderKeysChange.Rotated);
        _owner.OnSaved(this, text, mode);
        _owner.Announce(Labels.SavedAnnouncement(ProviderName, mode), simulated: true);
        Refresh();
        FocusLater(() => _rotate);
    }

    private void RefreshForm()
    {
        if (_input is null)
        {
            return;
        }
        var labels = Labels;
        _input.Label = labels.InputLabel(ProviderName);
        _input.Placeholder = Provider.KeyPrefix.Length > 0 ? Provider.KeyPrefix + "…" : null;
        var phase = FormPhase;
        _input.IsReadOnly = phase is ProviderKeysFormPhase.Testing or ProviderKeysFormPhase.Connected;
        if (_cancel is not null)
        {
            _cancel.Content = labels.Cancel;
        }
        if (_save is not null)
        {
            _save.Content = labels.Save;
            _save.IsLoading = phase == ProviderKeysFormPhase.Testing;
        }

        string kind;
        string text;
        var invalid = false;
        switch (phase)
        {
            case ProviderKeysFormPhase.Testing:
                kind = "testing";
                text = labels.TestingConnection;
                break;
            case ProviderKeysFormPhase.Connected:
                kind = "connected";
                text = labels.ConnectedDetail(_formDetail);
                break;
            default:
                if (_formError is not null)
                {
                    kind = "invalid";
                    text = _formError;
                    invalid = true;
                    break;
                }
                var format = Provider.CheckFormat(_input.Text);
                switch (format)
                {
                    case ProviderKeyFormat.Valid:
                        kind = "valid";
                        text = labels.LooksRight;
                        break;
                    case ProviderKeyFormat.WrongPrefix or ProviderKeyFormat.BadCharacters:
                        kind = "invalid";
                        text = FormatError(format)!;
                        invalid = true;
                        break;
                    default:
                        kind = "hint";
                        text = labels.FormHint(ProviderName, Provider.KeyPrefix);
                        break;
                }
                break;
        }
        _input.IsInvalid = invalid;
        var id = kind + ":" + text;
        if (_helper is not null && _shownHelper != id)
        {
            _shownHelper = id;
            _helper.Content = MakeHelper(kind, text);
        }
        // The input points at its helper line (Arc's aria-describedby).
        AutomationProperties.SetHelpText(_input, text);
    }

    private static Control MakeHelper(string kind, string text)
    {
        var (iconInk, textInk) = InksOf(kind);
        Control? icon = null;
        if (kind == "testing")
        {
            var spinner = new ProviderKeysSpinner();
            ArcTokens.Bind(spinner, ProviderKeysSpinner.ForegroundProperty, iconInk);
            icon = spinner;
        }
        else if (kind is "connected" or "valid" or "invalid")
        {
            var glyph = new ArcGlyph { Data = kind == "invalid" ? ArcIcons.CircleAlert : ArcIcons.CircleCheck, Size = 14 };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, iconInk);
            icon = glyph;
        }
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, textInk);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        if (icon is not null)
        {
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 2, 6, 0);
            grid.Children.Add(icon);
        }
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        return grid;
    }

    /// <summary>The form's error or rejection on screen (tests).</summary>
    internal string? FormError => _formError;

    internal bool FormErrorIsRejection => _formErrorIsRejection;

    internal Input? FormInput => _input;

    // ---------------------------------------------------------------- focus and teardown

    private void FocusLater(Func<Control?> target)
    {
        // Buttons take focus with the keyboard ring only when a key led here; the key field keeps its Tab focus (its
        // look does not use the ring).
        var method = SpecialFocusModality.For(this);
        Dispatcher.UIThread.Post(() =>
        {
            if (target() is { IsEffectivelyVisible: true } control)
            {
                control.Focus(control is Input ? NavigationMethod.Tab : method);
            }
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Timers and tests in flight end with the row; a removal that is still pending sticks.
        _retestCts?.Cancel();
        _retestCts = null;
        IsRetesting = false;
        _formCts?.Cancel();
        _formCts = null;
        if (FormPhase is ProviderKeysFormPhase.Testing or ProviderKeysFormPhase.Connected)
        {
            FormPhase = ProviderKeysFormPhase.Editing;
        }
        if (IsRevealed)
        {
            IsRevealed = false;
            _revealTimeline?.Stop();
        }
        CommitPendingRemoval();
    }
}

/// <summary>Lucide glyphs <see cref="ArcIcons"/> does not include.</summary>
internal static class ProviderKeysIcons
{
    /// <summary>Lucide <c>circle-dashed</c>: the status of a row without a key.</summary>
    public static Geometry CircleDashed => ArcIcons.Parse(
        "M10.1 2.182a10 10 0 0 1 3.8 0 M13.9 21.818a10 10 0 0 1-3.8 0 M17.609 3.721a10 10 0 0 1 2.69 2.7 " +
        "M2.182 13.9a10 10 0 0 1 0-3.8 M20.279 17.609a10 10 0 0 1-2.7 2.69 M21.818 10.1a10 10 0 0 1 0 3.8 " +
        "M3.721 6.391a10 10 0 0 1 2.7-2.69 M6.391 20.279a10 10 0 0 1-2.69-2.7");
}
