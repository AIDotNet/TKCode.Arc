using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A searchable emoji picker: a search field with a skin tone choice, a strip of categories, a scrolling grid of every
/// Unicode emoji (recently used first) and a preview line with the name of the emoji under the pointer or the keyboard.
/// Typing filters by name and keyword; the arrow keys, Home, End and Page keys move through the grid, Enter picks.
/// The grid draws only the rows in view. Picking raises <see cref="EmojiSelected"/> with the emoji in the chosen skin
/// tone, runs <see cref="Command"/> and moves the emoji to the front of <see cref="Recent"/>.
/// </summary>
/// <remarks>
/// Emoji data: Unicode emoji-test.txt (16.0) and the English CLDR annotations, Unicode License v3. The glyphs come from
/// the platform's emoji font; <see cref="MaxEmojiVersion"/> leaves out emoji newer than the fonts you target.
/// </remarks>
[TemplatePart(SearchPart, typeof(SearchField))]
[TemplatePart(TonesPart, typeof(EmojiPickerTones))]
[TemplatePart(TabsPart, typeof(EmojiPickerTabs))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(GridPart, typeof(EmojiPickerGrid))]
[TemplatePart(PreviewGlyphPart, typeof(TextBlock))]
[TemplatePart(PreviewNamePart, typeof(TextBlock))]
[TemplatePart(EmptyPart, typeof(TextBlock))]
[PseudoClasses(SearchingPseudoClass, NoResultsPseudoClass, TonesOpenPseudoClass)]
public class EmojiPicker : TemplatedControl
{
    public const string SearchPart = "PART_Search";
    public const string TonesPart = "PART_Tones";
    public const string TabsPart = "PART_Tabs";
    public const string ScrollPart = "PART_Scroll";
    public const string GridPart = "PART_Grid";
    public const string PreviewGlyphPart = "PART_PreviewGlyph";
    public const string PreviewNamePart = "PART_PreviewName";
    public const string EmptyPart = "PART_Empty";
    public const string SearchingPseudoClass = ":searching";
    public const string NoResultsPseudoClass = ":no-results";
    public const string TonesOpenPseudoClass = ":tones-open";

    private static readonly EmojiCategory[] s_groups =
    [
        EmojiCategory.Smileys, EmojiCategory.People, EmojiCategory.Nature, EmojiCategory.Food, EmojiCategory.Travel,
        EmojiCategory.Activities, EmojiCategory.Objects, EmojiCategory.Symbols, EmojiCategory.Flags
    ];

    #region Properties

    /// <summary>The skin tone applied to people and hands that have tones. Two-way.</summary>
    public static readonly StyledProperty<EmojiSkinTone> SkinToneProperty =
        AvaloniaProperty.Register<EmojiPicker, EmojiSkinTone>(nameof(SkinTone), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Emoji newer than this Unicode emoji version are left out (15.0 by default), so fonts that lack them show no empty boxes.</summary>
    public static readonly StyledProperty<double> MaxEmojiVersionProperty =
        AvaloniaProperty.Register<EmojiPicker, double>(nameof(MaxEmojiVersion), 15.0);

    /// <summary>How many emoji <see cref="Recent"/> keeps (24 by default; 0 hides the section).</summary>
    public static readonly StyledProperty<int> MaxRecentProperty =
        AvaloniaProperty.Register<EmojiPicker, int>(nameof(MaxRecent), 24);

    /// <summary>The search text. Two-way.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<EmojiPicker, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the search field and the skin tone choice are shown (default true).</summary>
    public static readonly StyledProperty<bool> IsSearchVisibleProperty =
        AvaloniaProperty.Register<EmojiPicker, bool>(nameof(IsSearchVisible), true);

    /// <summary>Whether the preview line under the grid is shown (default true).</summary>
    public static readonly StyledProperty<bool> IsPreviewVisibleProperty =
        AvaloniaProperty.Register<EmojiPicker, bool>(nameof(IsPreviewVisible), true);

    /// <summary>Runs when an emoji is picked; its parameter is the emoji text (with the skin tone).</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<EmojiPicker, ICommand?>(nameof(Command));

    /// <summary>Every word the picker shows or says (English by default).</summary>
    public static readonly StyledProperty<EmojiPickerLabels> LabelsProperty =
        AvaloniaProperty.Register<EmojiPicker, EmojiPickerLabels>(nameof(Labels), EmojiPickerLabels.Default);

    /// <summary>Raised when an emoji is picked.</summary>
    public static readonly RoutedEvent<EmojiSelectedEventArgs> EmojiSelectedEvent =
        RoutedEvent.Register<EmojiPicker, EmojiSelectedEventArgs>(nameof(EmojiSelected), RoutingStrategies.Bubble);

    /// <summary>The skin tone applied to people and hands that have tones. Two-way.</summary>
    public EmojiSkinTone SkinTone { get => GetValue(SkinToneProperty); set => SetValue(SkinToneProperty, value); }

    /// <summary>Emoji newer than this Unicode emoji version are left out (15.0 by default), so fonts that lack them show no empty boxes.</summary>
    public double MaxEmojiVersion { get => GetValue(MaxEmojiVersionProperty); set => SetValue(MaxEmojiVersionProperty, value); }

    /// <summary>How many emoji <see cref="Recent"/> keeps (24 by default; 0 hides the section).</summary>
    public int MaxRecent { get => GetValue(MaxRecentProperty); set => SetValue(MaxRecentProperty, value); }

    /// <summary>The search text. Two-way.</summary>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <summary>Whether the search field and the skin tone choice are shown (default true).</summary>
    public bool IsSearchVisible { get => GetValue(IsSearchVisibleProperty); set => SetValue(IsSearchVisibleProperty, value); }

    /// <summary>Whether the preview line under the grid is shown (default true).</summary>
    public bool IsPreviewVisible { get => GetValue(IsPreviewVisibleProperty); set => SetValue(IsPreviewVisibleProperty, value); }

    /// <summary>Runs when an emoji is picked; its parameter is the emoji text (with the skin tone).</summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>Every word the picker shows or says (English by default).</summary>
    public EmojiPickerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>
    /// The recently picked emoji, most recent first, without skin tones. Fill it to restore a saved list and watch it to
    /// save one; the section shows the list as it was when the picker was last shown, so it does not move while in use.
    /// </summary>
    public ObservableCollection<string> Recent { get; } = new();

    /// <summary>Raised when an emoji is picked.</summary>
    public event EventHandler<EmojiSelectedEventArgs>? EmojiSelected
    {
        add => AddHandler(EmojiSelectedEvent, value);
        remove => RemoveHandler(EmojiSelectedEvent, value);
    }

    #endregion

    private SearchField? _search;
    private EmojiPickerTones? _tones;
    private EmojiPickerTabs? _tabs;
    private ScrollViewer? _scroll;
    private EmojiPickerGrid? _grid;
    private TextBlock? _previewGlyph;
    private TextBlock? _previewName;
    private TextBlock? _empty;
    private IReadOnlyList<EmojiPickerSection>? _browseSections;
    private double _builtVersion = double.NaN;
    private bool _recentDirty = true;
    private bool _ownRecentChange;
    private bool _scrollingToSection;
    private MotionValue<double>? _scrollMotion;

    static EmojiPicker()
    {
        FocusableProperty.OverrideDefaultValue<EmojiPicker>(false);
        SpecialFocusModality.Watch();
    }

    public EmojiPicker()
    {
        Recent.CollectionChanged += OnRecentChanged;
    }

    internal EmojiPickerGrid? Grid => _grid;

    internal EmojiPickerTabs? Tabs => _tabs;

    internal EmojiPickerTones? Tones => _tones;

    internal SearchField? Search => _search;

    /// <summary>Moves keyboard focus to the search field (or the grid when search is hidden).</summary>
    public void FocusSearch()
    {
        if (IsSearchVisible && _search is not null)
        {
            _search.Focus(SpecialFocusModality.For(this));
        }
        else
        {
            _grid?.Focus(SpecialFocusModality.For(this));
        }
    }

    /// <summary>Scrolls the grid to a category and makes its first emoji active (clears the search first).</summary>
    public void ScrollToCategory(EmojiCategory category)
    {
        if (!string.IsNullOrEmpty(SearchText))
        {
            SetCurrentValue(SearchTextProperty, string.Empty);
        }
        if (_grid is null || _scroll is null)
        {
            return;
        }
        var index = IndexOfSection(category);
        if (index < 0)
        {
            return;
        }
        _scroll.UpdateLayout();
        _grid.ActivateSection(index);
        _tabs?.SetActive(category);
        ScrollTo(_grid.SectionTop(index));
    }

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _search = e.NameScope.Find<SearchField>(SearchPart);
        _tones = e.NameScope.Find<EmojiPickerTones>(TonesPart);
        _tabs = e.NameScope.Find<EmojiPickerTabs>(TabsPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _grid = e.NameScope.Find<EmojiPickerGrid>(GridPart);
        _previewGlyph = e.NameScope.Find<TextBlock>(PreviewGlyphPart);
        _previewName = e.NameScope.Find<TextBlock>(PreviewNamePart);
        _empty = e.NameScope.Find<TextBlock>(EmptyPart);
        Hook();
        _scrollMotion = null;
        _browseSections = null;
        _recentDirty = true;
        ApplyLabels();
        _grid?.SetDisplay(SkinTone, MaxEmojiVersion);
        _tones?.SetTone(SkinTone);
        Rebuild();
    }

    private void Hook()
    {
        if (_search is not null)
        {
            _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
            _search.TextChanged += OnSearchTextChanged;
            _search.Text = SearchText;
        }
        if (_tones is not null)
        {
            _tones.ToneChosen += OnToneChosen;
            _tones.ExpandedChanged += OnTonesExpanded;
        }
        if (_tabs is not null)
        {
            _tabs.Invoked += OnTabInvoked;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
            _scroll.AddHandler(PointerWheelChangedEvent, OnScrollWheel, RoutingStrategies.Tunnel);
        }
        if (_grid is not null)
        {
            _grid.Picked += OnPicked;
            _grid.ActiveChanged += OnActiveChanged;
            _grid.TextInput += OnGridTextInput;
        }
    }

    private void Unhook()
    {
        if (_search is not null)
        {
            _search.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
            _search.TextChanged -= OnSearchTextChanged;
        }
        if (_tones is not null)
        {
            _tones.ToneChosen -= OnToneChosen;
            _tones.ExpandedChanged -= OnTonesExpanded;
        }
        if (_tabs is not null)
        {
            _tabs.Invoked -= OnTabInvoked;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll.RemoveHandler(PointerWheelChangedEvent, OnScrollWheel);
        }
        if (_grid is not null)
        {
            _grid.Picked -= OnPicked;
            _grid.ActiveChanged -= OnActiveChanged;
            _grid.TextInput -= OnGridTextInput;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SkinToneProperty)
        {
            _grid?.SetDisplay(SkinTone, MaxEmojiVersion);
            _tones?.SetTone(SkinTone);
            OnActiveChanged(_grid?.ActiveEmoji);
        }
        else if (change.Property == MaxEmojiVersionProperty)
        {
            _grid?.SetDisplay(SkinTone, MaxEmojiVersion);
            _browseSections = null;
            Rebuild();
        }
        else if (change.Property == SearchTextProperty)
        {
            if (_search is not null && _search.Text != SearchText)
            {
                _search.Text = SearchText;
            }
            Rebuild();
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            _browseSections = null;
            Rebuild();
        }
        else if (change.Property == MaxRecentProperty)
        {
            TrimRecent();
            _recentDirty = true;
            Rebuild();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Picks made while it was shown move into the Recent section the next time it is shown.
        if (_recentDirty && _grid is not null)
        {
            Rebuild();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tones?.SetExpanded(false);
    }

    private void ApplyLabels()
    {
        var labels = Labels ?? EmojiPickerLabels.Default;
        if (_search is not null)
        {
            _search.Placeholder = labels.Search;
            AutomationProperties.SetName(_search, labels.Search);
        }
        _tones?.SetLabels(labels.SkinTone, labels.Tone);
        if (_tabs is not null)
        {
            AutomationProperties.SetHelpText(_tabs, labels.Categories);
        }
        if (_empty is not null)
        {
            _empty.Text = labels.NoResults;
        }
        if (_grid is not null)
        {
            AutomationProperties.SetHelpText(_grid, labels.Grid);
        }
    }

    #endregion

    #region Sections

    private bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    private void Rebuild()
    {
        if (_grid is null)
        {
            return;
        }
        var labels = Labels ?? EmojiPickerLabels.Default;
        var browse = BrowseSections(labels);
        if (IsSearching)
        {
            var hits = EmojiPickerData.Search(SearchText!, EmojiPickerData.All, MaxEmojiVersion);
            _grid.SetSections(hits.Count > 0 ? [new EmojiPickerSection(EmojiCategory.Recent, labels.SearchResults, hits)] : []);
            PseudoClasses.Set(SearchingPseudoClass, true);
            PseudoClasses.Set(NoResultsPseudoClass, hits.Count == 0);
            if (_search is not null)
            {
                AutomationProperties.SetHelpText(_search, hits.Count == 0 ? labels.NoResults : labels.ResultCount(hits.Count));
            }
        }
        else
        {
            _grid.SetSections(browse);
            PseudoClasses.Set(SearchingPseudoClass, false);
            PseudoClasses.Set(NoResultsPseudoClass, false);
            if (_search is not null)
            {
                AutomationProperties.SetHelpText(_search, string.Empty);
            }
        }
        _tabs?.SetCategories(browse.Select(s => s.Category).ToArray(), labels.Category);
        _scrollMotion?.Stop();
        if (_scroll is not null)
        {
            _scroll.Offset = default;
        }
        if (!IsSearching && browse.Count > 0)
        {
            _tabs?.SetActive(browse[0].Category);
        }
    }

    /// <summary>The sections shown without a search: Recent (when there is any), then the groups; cached per version.</summary>
    private IReadOnlyList<EmojiPickerSection> BrowseSections(EmojiPickerLabels labels)
    {
        if (_browseSections is not null && !_recentDirty && _builtVersion.Equals(MaxEmojiVersion))
        {
            return _browseSections;
        }
        var max = MaxEmojiVersion;
        var groups = new List<EmojiPickerEmoji>[s_groups.Length];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = new List<EmojiPickerEmoji>();
        }
        foreach (var emoji in EmojiPickerData.All)
        {
            if (emoji.Version <= max)
            {
                groups[(int)emoji.Category - 1].Add(emoji);
            }
        }
        var sections = new List<EmojiPickerSection>(s_groups.Length + 1);
        var recent = new List<EmojiPickerEmoji>();
        foreach (var text in Recent)
        {
            if (EmojiPickerData.Find(text) is { } emoji && emoji.Version <= max && !recent.Contains(emoji))
            {
                recent.Add(emoji);
            }
        }
        if (recent.Count > 0 && MaxRecent > 0)
        {
            sections.Add(new EmojiPickerSection(EmojiCategory.Recent, labels.Category(EmojiCategory.Recent), recent));
        }
        for (var i = 0; i < s_groups.Length; i++)
        {
            if (groups[i].Count > 0)
            {
                sections.Add(new EmojiPickerSection(s_groups[i], labels.Category(s_groups[i]), groups[i]));
            }
        }
        _browseSections = sections;
        _builtVersion = max;
        _recentDirty = false;
        return sections;
    }

    private int IndexOfSection(EmojiCategory category)
    {
        if (_grid is null)
        {
            return -1;
        }
        var sections = _grid.Sections;
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i].Category == category)
            {
                return i;
            }
        }
        return -1;
    }

    private void OnRecentChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _recentDirty = true;
        if (!_ownRecentChange && !IsKeyboardFocusWithin)
        {
            // Changed by the app (a restored list): show it now.
            Rebuild();
        }
    }

    private void TrimRecent()
    {
        _ownRecentChange = true;
        try
        {
            while (Recent.Count > Math.Max(0, MaxRecent))
            {
                Recent.RemoveAt(Recent.Count - 1);
            }
        }
        finally
        {
            _ownRecentChange = false;
        }
    }

    #endregion

    #region Picking and preview

    private void OnPicked(EmojiPickerEmoji emoji)
    {
        var text = emoji.Display(SkinTone, MaxEmojiVersion);
        if (MaxRecent > 0)
        {
            _ownRecentChange = true;
            try
            {
                var existing = Recent.IndexOf(emoji.Emoji);
                if (existing > 0)
                {
                    Recent.Move(existing, 0);
                }
                else if (existing < 0)
                {
                    Recent.Insert(0, emoji.Emoji);
                }
            }
            finally
            {
                _ownRecentChange = false;
            }
            TrimRecent();
        }
        RaiseEvent(new EmojiSelectedEventArgs(EmojiSelectedEvent, text, emoji.Emoji, emoji.Name, emoji.Category));
        if (Command is { } command && command.CanExecute(text))
        {
            command.Execute(text);
        }
    }

    private void OnActiveChanged(EmojiPickerEmoji? emoji)
    {
        var labels = Labels ?? EmojiPickerLabels.Default;
        if (_previewGlyph is not null)
        {
            _previewGlyph.Text = emoji is null ? string.Empty : emoji.Display(SkinTone, MaxEmojiVersion);
        }
        if (_previewName is not null)
        {
            _previewName.Text = emoji is null ? labels.PreviewHint : Capitalize(emoji.Name);
        }
        if (_grid is not null && emoji is not null)
        {
            var (section, index) = _grid.Active;
            if (section >= 0 && section < _grid.Sections.Count)
            {
                var s = _grid.Sections[section];
                AutomationProperties.SetHelpText(_grid, s.Title + ", " + labels.Position(index + 1, s.Items.Count));
            }
        }
    }

    private static string Capitalize(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    #endregion

    #region Input

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_search is not null && _search.Text != SearchText)
        {
            SetCurrentValue(SearchTextProperty, _search.Text);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_grid is null)
        {
            return;
        }
        if (e.Key == Key.Down)
        {
            e.Handled = true;
            _grid.Focus(NavigationMethod.Tab);
        }
        else if (e.Key == Key.Enter && IsSearching)
        {
            // Enter in the search picks the best match.
            e.Handled = _grid.PickActive();
        }
    }

    private void OnGridTextInput(object? sender, TextInputEventArgs e)
    {
        // Typing in the grid goes on in the search field.
        if (_search is null || !IsSearchVisible || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]) || e.Text == " ")
        {
            return;
        }
        e.Handled = true;
        _search.Focus(NavigationMethod.Tab);
        _search.Text = (_search.Text ?? string.Empty) + e.Text;
        _search.CaretIndex = _search.Text.Length;
    }

    private void OnToneChosen(EmojiSkinTone tone) => SetCurrentValue(SkinToneProperty, tone);

    private void OnTonesExpanded(bool expanded) => PseudoClasses.Set(TonesOpenPseudoClass, expanded);

    private void OnTabInvoked(EmojiCategory category) => ScrollToCategory(category);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollingToSection || IsSearching || _grid is null || _tabs is null || _scroll is null)
        {
            return;
        }
        var section = _grid.SectionAt(_scroll.Offset.Y + 1);
        if (section >= 0 && section < _grid.Sections.Count)
        {
            _tabs.SetActive(_grid.Sections[section].Category);
        }
    }

    private void ScrollTo(double y)
    {
        if (_scroll is null)
        {
            return;
        }
        var target = Math.Clamp(y, 0, Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height));
        if (!ArcMotion.IsFull(this) || !IsLoaded)
        {
            _scroll.Offset = new Vector(0, target);
            return;
        }
        if (_scrollMotion is null)
        {
            // Code-driven: a ScrollViewer's offset is not a styled property a transition can animate.
            _scrollMotion = new MotionValue<double>(this, _scroll.Offset.Y, MotionVectors.Double);
            _scrollMotion.Changed += v =>
            {
                if (_scroll is not null)
                {
                    _scroll.Offset = new Vector(0, v);
                }
            };
        }
        if (!_scrollMotion.IsAnimating)
        {
            _scrollMotion.Jump(_scroll.Offset.Y);
        }
        _scrollingToSection = true;
        _ = AwaitScroll(_scrollMotion.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth)));
    }

    private async Task AwaitScroll(Task<bool> motion)
    {
        try
        {
            await motion;
        }
        finally
        {
            _scrollingToSection = false;
        }
    }

    // A wheel takes over from a category jump.
    private void OnScrollWheel(object? sender, PointerWheelEventArgs e) => _scrollMotion?.Stop();

    #endregion
}
