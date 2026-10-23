using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The "Memory updated" pill of the agent memory card: it shows the note of a memory the assistant has just taken and
/// files it when pressed (<c>Click</c>). A 34px bordered pill on the surface: bookmark-plus, the title (medium), the note
/// (muted, trimmed with an ellipsis when the pill is too narrow) and an arrow-down. With <see cref="SavedText"/> set it
/// shows the saved state instead (a green check and the text, no arrow); the change crossfades (old out in 100ms, then
/// the new one in over 160ms rising 2px; Reduced: one 120ms fade). The pill is as wide as the widest text it may show
/// (an invisible sizing layer holds the note row, the saved row and one row per <see cref="SizingTexts"/> entry), so
/// its width does not move between the states. Hover tints it (160ms); the keyboard focus ring sits 2px outside.
/// With <see cref="IsPaused"/> the pill is replaced at once by the paused notice: a circle-pause glyph and
/// <see cref="PausedText"/>, wrapped, without the pill's frame; it is then not focusable and does not click.
/// The pill takes all its words from its owner.
/// </summary>
[TemplatePart(ContentPart, typeof(ArcContentSwap))]
[TemplatePart(SizerPart, typeof(Panel))]
[PseudoClasses(SavedPseudoClass, PausedPseudoClass, HoverPseudoClass)]
internal class MemoryCapturedPill : AvaloniaButton
{
    public const string ContentPart = "PART_Content";
    public const string SizerPart = "PART_Sizer";

    public const string SavedPseudoClass = ":saved";
    public const string PausedPseudoClass = ":paused";
    public const string HoverPseudoClass = ":hover";

    #region Properties

    /// <summary>The bold title ("Memory updated").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MemoryCapturedPill, string?>(nameof(Title));

    /// <summary>The assistant's note ("Noted: you run builds with the dotnet CLI"); trimmed with an ellipsis when the pill is too narrow.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<MemoryCapturedPill, string?>(nameof(Note));

    /// <summary>Non-empty shows the saved state: a check and this text ("Saved to Preferences"); null or empty returns to the note.</summary>
    public static readonly StyledProperty<string?> SavedTextProperty =
        AvaloniaProperty.Register<MemoryCapturedPill, string?>(nameof(SavedText));

    /// <summary>
    /// More texts the pill may show in the saved state (each measured as a check and the text, like
    /// <see cref="SavedText"/>); the pill is as wide as the widest of them, the current note row and the current saved
    /// row, so its width never moves when the state changes.
    /// </summary>
    public static readonly StyledProperty<IList<string>?> SizingTextsProperty =
        AvaloniaProperty.Register<MemoryCapturedPill, IList<string>?>(nameof(SizingTexts));

    /// <summary>The accessible name of the pill ("Memory updated. Noted: … File under Preferences").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<MemoryCapturedPill, string?>(nameof(AccessibleName));

    /// <summary>True replaces the pill by the paused notice: no frame, not focusable, no click.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<MemoryCapturedPill, bool>(nameof(IsPaused));

    /// <summary>The sentence of the paused notice (also its accessible name).</summary>
    public static readonly StyledProperty<string?> PausedTextProperty =
        AvaloniaProperty.Register<MemoryCapturedPill, string?>(nameof(PausedText));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="SavedTextProperty"/>
    public string? SavedText { get => GetValue(SavedTextProperty); set => SetValue(SavedTextProperty, value); }

    /// <inheritdoc cref="SizingTextsProperty"/>
    public IList<string>? SizingTexts { get => GetValue(SizingTextsProperty); set => SetValue(SizingTextsProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="PausedTextProperty"/>
    public string? PausedText { get => GetValue(PausedTextProperty); set => SetValue(PausedTextProperty, value); }

    #endregion

    private ArcContentSwap? _contentSwap;
    private Panel? _sizer;
    private MemoryCapturedPillContent? _content;
    private MemoryCapturedPillContent? _sizerNote;
    private MemoryCapturedPillContent? _sizerSaved;
    private INotifyCollectionChanged? _observedTexts;
    private bool _attached;

    protected override Type StyleKeyOverride => typeof(MemoryCapturedPill);

    /// <summary>True while the saved state is shown (<see cref="SavedText"/> is not empty).</summary>
    internal bool IsSaved => !string.IsNullOrEmpty(SavedText);

    /// <summary>The content layer on screen (note or saved state).</summary>
    internal MemoryCapturedPillContent? CurrentContent => _content;

    /// <summary>The invisible rows that size the pill.</summary>
    internal Panel? Sizer => _sizer;

    #region Content

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _contentSwap = e.NameScope.Find<ArcContentSwap>(ContentPart);
        _sizer = e.NameScope.Find<Panel>(SizerPart);
        // New parts start with the current state (their first content never animates).
        _content = null;
        _sizerNote = null;
        _sizerSaved = null;
        RebuildSizer();
        RefreshContent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TitleProperty || property == NoteProperty || property == SavedTextProperty)
        {
            PseudoClasses.Set(SavedPseudoClass, IsSaved);
            UpdateSizerRows();
            RefreshContent();
        }
        else if (property == SizingTextsProperty)
        {
            ObserveTexts();
            RebuildSizer();
        }
        else if (property == IsPausedProperty)
        {
            var paused = change.GetNewValue<bool>();
            PseudoClasses.Set(PausedPseudoClass, paused);
            if (paused)
            {
                PseudoClasses.Set(HoverPseudoClass, false);
            }
            RefreshName();
        }
        else if (property == AccessibleNameProperty || property == PausedTextProperty)
        {
            RefreshName();
        }
        else if (property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A pill disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    private void RefreshName() => AutomationProperties.SetName(this, IsPaused ? PausedText : AccessibleName);

    /// <summary>A new content layer (and so a crossfade) only when the words on screen change.</summary>
    private void RefreshContent()
    {
        if (_contentSwap is null)
        {
            return;
        }
        var saved = IsSaved;
        if (_content is not null && _content.IsSaved == saved &&
            (saved
                ? _content.SavedText == SavedText
                : _content.Title == Title && _content.Note == Note))
        {
            return;
        }
        _content = saved
            ? new MemoryCapturedPillContent { IsSaved = true, SavedText = SavedText }
            : new MemoryCapturedPillContent { Title = Title, Note = Note };
        _contentSwap.Content = _content;
    }

    /// <summary>The sizing rows: the note row, the saved row and one saved-style row per <see cref="SizingTexts"/> entry.</summary>
    private void RebuildSizer()
    {
        if (_sizer is null)
        {
            return;
        }
        _sizer.Children.Clear();
        _sizerNote = new MemoryCapturedPillContent();
        _sizerSaved = new MemoryCapturedPillContent { IsSaved = true };
        _sizer.Children.Add(_sizerNote);
        _sizer.Children.Add(_sizerSaved);
        if (SizingTexts is { } texts)
        {
            foreach (var text in texts)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    _sizer.Children.Add(new MemoryCapturedPillContent { IsSaved = true, SavedText = text });
                }
            }
        }
        UpdateSizerRows();
    }

    private void UpdateSizerRows()
    {
        if (_sizerNote is not null)
        {
            _sizerNote.Title = Title;
            _sizerNote.Note = Note;
            _sizerNote.IsVisible = !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Note);
        }
        if (_sizerSaved is not null)
        {
            _sizerSaved.SavedText = SavedText;
            _sizerSaved.IsVisible = IsSaved;
        }
    }

    #endregion

    #region Data observation

    // The texts list is the owner's and can outlive the pill: it is followed only while the pill is in a window.

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveTexts();
        RebuildSizer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveTexts();
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    private void ObserveTexts()
    {
        var next = _attached ? SizingTexts as INotifyCollectionChanged : null;
        if (ReferenceEquals(next, _observedTexts))
        {
            return;
        }
        if (_observedTexts is not null)
        {
            _observedTexts.CollectionChanged -= OnTextsChanged;
        }
        _observedTexts = next;
        if (next is not null)
        {
            next.CollectionChanged += OnTextsChanged;
        }
    }

    private void OnTextsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildSizer();

    #endregion

    #region Input

    /// <summary>Clicks like a press would (the automation Invoke); the paused notice does nothing.</summary>
    internal void Activate()
    {
        if (IsPaused || !IsEffectivelyEnabled)
        {
            return;
        }
        OnClick();
    }

    protected override void OnClick()
    {
        if (IsPaused)
        {
            return;
        }
        base.OnClick();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // The paused notice is text: no pressed state, no click on release.
        if (IsPaused)
        {
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsPaused)
        {
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, !IsPaused && e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new PillAutomationPeer(this);

    /// <summary>
    /// A button named <see cref="AccessibleName"/> (the visible title, note and arrow are not read separately); while
    /// paused, a text element named <see cref="PausedText"/> whose Invoke does nothing.
    /// </summary>
    private sealed class PillAutomationPeer(MemoryCapturedPill owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        private MemoryCapturedPill Pill => (MemoryCapturedPill)Owner;

        public void Invoke()
        {
            EnsureEnabled();
            Pill.Activate();
        }

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            Pill.IsPaused ? AutomationControlType.Text : AutomationControlType.Button;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// One state of a <see cref="MemoryCapturedPill"/>, 7px between the parts: the note row (bookmark-plus, title, note,
/// arrow-down) or, with <see cref="IsSaved"/>, the saved row (a green check and the text). A new one is made for every
/// change so the pill can crossfade the old one out; the same type sizes the pill in its invisible layer.
/// </summary>
[PseudoClasses(SavedPseudoClass)]
internal sealed class MemoryCapturedPillContent : TemplatedControl
{
    public const string SavedPseudoClass = ":saved";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MemoryCapturedPillContent, string?>(nameof(Title));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<MemoryCapturedPillContent, string?>(nameof(Note));

    public static readonly StyledProperty<string?> SavedTextProperty =
        AvaloniaProperty.Register<MemoryCapturedPillContent, string?>(nameof(SavedText));

    public static readonly StyledProperty<bool> IsSavedProperty =
        AvaloniaProperty.Register<MemoryCapturedPillContent, bool>(nameof(IsSaved));

    static MemoryCapturedPillContent()
    {
        FocusableProperty.OverrideDefaultValue<MemoryCapturedPillContent>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<MemoryCapturedPillContent>(false);
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    public string? SavedText { get => GetValue(SavedTextProperty); set => SetValue(SavedTextProperty, value); }

    public bool IsSaved { get => GetValue(IsSavedProperty); set => SetValue(IsSavedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSavedProperty)
        {
            PseudoClasses.Set(SavedPseudoClass, change.GetNewValue<bool>());
        }
    }
}

/// <summary>Lucide glyphs the captured pill needs that <see cref="ArcIcons"/> does not include.</summary>
internal static class MemoryCapturedPillGlyphs
{
    /// <summary>Lucide <c>bookmark-plus</c>: in front of the pill's title.</summary>
    public static Geometry BookmarkPlus =>
        ArcIcons.Parse("M12 7v6 M15 10H9 M17 3a2 2 0 0 1 2 2v15a1 1 0 0 1-1.496.868l-4.512-2.578a2 2 0 0 0-1.984 0l-4.512 2.578A1 1 0 0 1 5 20V5a2 2 0 0 1 2-2z");

    /// <summary>Lucide <c>circle-pause</c>: in front of the paused notice.</summary>
    public static Geometry CirclePause =>
        ArcIcons.Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z M10 15L10 9 M14 15L14 9");
}
