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
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="PromptTemplateEditor.ChipInvoked"/> and <see cref="PromptTemplateEditor.VariableAdded"/>.</summary>
internal sealed class PromptChipEventArgs : RoutedEventArgs
{
    public PromptChipEventArgs(RoutedEvent routedEvent, string name)
        : base(routedEvent)
    {
        Name = name;
    }

    /// <summary>The variable's name.</summary>
    public string Name { get; }
}

/// <summary>
/// The template editor of the prompt studio (Arc's prompt editor): plain text in which every <c>{{name}}</c> is drawn
/// as one colored chip showing only the name, in the variable's tone (<see cref="PromptTone"/>). Chips are atomic: the
/// caret never lands inside one, Backspace and Delete remove one whole, Alt+Backspace counts one as a word. Typing
/// "{{" opens a list of the variables at the caret (filtered as the name is typed, with "+ Add name" for a new one);
/// typing <c>name}}</c> or pasting <c>{{ name }}</c> makes a chip too, and a new name raises
/// <see cref="VariableAdded"/>. Copy writes <c>{{name}}</c>. Undo and redo (Ctrl/Cmd+Z, with Shift to redo) group
/// typing in steps of about a second. Ctrl/Cmd+Enter runs <see cref="SubmitCommand"/>.
/// </summary>
/// <remarks>
/// Built on the main package's <see cref="TokenTextEditor"/>: the text is the model (<see cref="Text"/> keeps every
/// brace), tokens are parsed from it (<see cref="TokenTextEditor.TokenParser"/>), and a <see cref="TokenRenderer"/>
/// lays each chip out as one inline object, so caret movement, hit testing, wrapping and selection treat it as a unit
/// and an IME composition can only happen outside it. The editor grows with its text up to <see cref="MaxLines"/>.
/// All text it shows comes from the owner: <see cref="Placeholder"/>, <see cref="AccessibleName"/>,
/// <see cref="ListName"/>, <see cref="AddVariableText"/> and <see cref="AnnounceCount"/>.
/// </remarks>
[TemplatePart(EditorPart, typeof(TokenTextEditor))]
internal class PromptTemplateEditor : TemplatedControl
{
    public const string EditorPart = "PART_Editor";

    /// <summary>The template text, with <c>{{name}}</c> markers (two-way; inner spaces are removed).</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => value ?? string.Empty);

    /// <summary>The names the "{{" list offers, in the order of their tones.</summary>
    public static readonly StyledProperty<IList<string>?> VariablesProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, IList<string>?>(nameof(Variables));

    /// <summary>The tone (1 to 4) of a name; null: its place in <see cref="Variables"/> (names only in the text follow).</summary>
    public static readonly StyledProperty<Func<string, int>?> ToneSelectorProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, Func<string, int>?>(nameof(ToneSelector));

    /// <summary>Names whose value is empty: their chips are drawn with a dashed outline.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> MissingVariablesProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, IEnumerable<string>?>(nameof(MissingVariables));

    /// <summary>The name whose chips are highlighted (its field has focus), or null.</summary>
    public static readonly StyledProperty<string?> ActiveVariableProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, string?>(nameof(ActiveVariable));

    /// <summary>Muted text shown while the editor is empty.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, string?>(nameof(Placeholder));

    /// <summary>The text can be selected and copied but not changed.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, bool>(nameof(IsReadOnly));

    /// <summary>Runs on Ctrl/Cmd+Enter.</summary>
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, ICommand?>(nameof(SubmitCommand));

    /// <summary>The editor's accessible name ("System prompt").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, string?>(nameof(AccessibleName));

    /// <summary>The text of the list's last row for a new name ("Add order_id"); the name inside it is drawn medium.</summary>
    public static readonly StyledProperty<Func<string, string>?> AddVariableTextProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, Func<string, string>?>(nameof(AddVariableText));

    /// <summary>The accessible name of the "{{" list ("Variables").</summary>
    public static readonly StyledProperty<string?> ListNameProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, string?>(nameof(ListName));

    /// <summary>The longest pause between keys that still joins one undo step (default 1 s; Arc 0.8 to 1.1 s).</summary>
    public static readonly StyledProperty<TimeSpan> UndoGroupDelayProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, TimeSpan>(nameof(UndoGroupDelay), TimeSpan.FromSeconds(1));

    /// <summary>Lines the editor shows at least (default 1).</summary>
    public static readonly StyledProperty<int> MinLinesProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, int>(nameof(MinLines), 1);

    /// <summary>The editor grows to this many lines, then scrolls (default 16).</summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<PromptTemplateEditor, int>(nameof(MaxLines), 16);

    /// <summary>Series colors from the theme (tones 1 to 4).</summary>
    internal static readonly StyledProperty<IBrush?> Series1Property = AvaloniaProperty.Register<PromptTemplateEditor, IBrush?>("Series1");

    internal static readonly StyledProperty<IBrush?> Series2Property = AvaloniaProperty.Register<PromptTemplateEditor, IBrush?>("Series2");

    internal static readonly StyledProperty<IBrush?> Series3Property = AvaloniaProperty.Register<PromptTemplateEditor, IBrush?>("Series3");

    internal static readonly StyledProperty<IBrush?> Series4Property = AvaloniaProperty.Register<PromptTemplateEditor, IBrush?>("Series4");

    /// <summary>Raised when a chip is clicked; carries its name.</summary>
    public static readonly RoutedEvent<PromptChipEventArgs> ChipInvokedEvent =
        RoutedEvent.Register<PromptTemplateEditor, PromptChipEventArgs>(nameof(ChipInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a new name appears through "+ Add", typing <c>name}}</c> or a paste; carries the name.</summary>
    public static readonly RoutedEvent<PromptChipEventArgs> VariableAddedEvent =
        RoutedEvent.Register<PromptTemplateEditor, PromptChipEventArgs>(nameof(VariableAdded), RoutingStrategies.Bubble);

    private readonly PromptChipRenderer _renderer;
    private readonly PromptVariablePop _pop;
    private readonly PromptVariablePopup _popup;
    private TokenTextEditor? _editor;
    private TopLevel? _topLevel;
    private INotifyCollectionChanged? _watchedVariables;
    private INotifyCollectionChanged? _watchedMissing;
    private List<string> _textNames = new();
    private List<string>? _order;
    private PromptEditKind? _pendingKind;
    private string? _textBeforeEdit;
    private TokenSpan? _pressedChip;
    private bool _applying;
    private bool _syncingText;
    private bool _afterEditPosted;
    private bool _popupParented;

    static PromptTemplateEditor()
    {
        FocusableProperty.OverrideDefaultValue<PromptTemplateEditor>(true);
        // Focus() lands on the text; Tab goes straight to it.
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<PromptTemplateEditor>(false);
    }

    public PromptTemplateEditor()
    {
        _renderer = new PromptChipRenderer(this);
        _pop = new PromptVariablePop { Owner = this };
        _pop.Accepted += OnRowAccepted;
        _pop.ActiveChanged += (_, _) => UpdateItemStatus();
        _popup = new PromptVariablePopup { Child = _pop };
        _popup.Closed += (_, _) => _pop.OnHidden();
    }

    #region Properties

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="VariablesProperty"/>
    public IList<string>? Variables { get => GetValue(VariablesProperty); set => SetValue(VariablesProperty, value); }

    /// <inheritdoc cref="ToneSelectorProperty"/>
    public Func<string, int>? ToneSelector { get => GetValue(ToneSelectorProperty); set => SetValue(ToneSelectorProperty, value); }

    /// <inheritdoc cref="MissingVariablesProperty"/>
    public IEnumerable<string>? MissingVariables { get => GetValue(MissingVariablesProperty); set => SetValue(MissingVariablesProperty, value); }

    /// <inheritdoc cref="ActiveVariableProperty"/>
    public string? ActiveVariable { get => GetValue(ActiveVariableProperty); set => SetValue(ActiveVariableProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="SubmitCommandProperty"/>
    public ICommand? SubmitCommand { get => GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="AddVariableTextProperty"/>
    public Func<string, string>? AddVariableText { get => GetValue(AddVariableTextProperty); set => SetValue(AddVariableTextProperty, value); }

    /// <inheritdoc cref="ListNameProperty"/>
    public string? ListName { get => GetValue(ListNameProperty); set => SetValue(ListNameProperty, value); }

    /// <inheritdoc cref="UndoGroupDelayProperty"/>
    public TimeSpan UndoGroupDelay { get => GetValue(UndoGroupDelayProperty); set => SetValue(UndoGroupDelayProperty, value); }

    /// <inheritdoc cref="MinLinesProperty"/>
    public int MinLines { get => GetValue(MinLinesProperty); set => SetValue(MinLinesProperty, value); }

    /// <inheritdoc cref="MaxLinesProperty"/>
    public int MaxLines { get => GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    internal IBrush? Series1 { get => GetValue(Series1Property); set => SetValue(Series1Property, value); }

    internal IBrush? Series2 { get => GetValue(Series2Property); set => SetValue(Series2Property, value); }

    internal IBrush? Series3 { get => GetValue(Series3Property); set => SetValue(Series3Property, value); }

    internal IBrush? Series4 { get => GetValue(Series4Property); set => SetValue(Series4Property, value); }

    /// <summary>
    /// Turns the number of rows the "{{" list shows into a polite announcement ("3 variables"): the owner says it in its
    /// status region and returns it. Null announces nothing.
    /// </summary>
    public Func<int, string?>? AnnounceCount { get => _pop.AnnounceCount; set => _pop.AnnounceCount = value; }

    /// <summary>Raised when a chip is clicked.</summary>
    public event EventHandler<PromptChipEventArgs>? ChipInvoked
    {
        add => AddHandler(ChipInvokedEvent, value);
        remove => RemoveHandler(ChipInvokedEvent, value);
    }

    /// <summary>Raised when a new variable name is added from the editor.</summary>
    public event EventHandler<PromptChipEventArgs>? VariableAdded
    {
        add => AddHandler(VariableAddedEvent, value);
        remove => RemoveHandler(VariableAddedEvent, value);
    }

    /// <summary>The undo steps (tests reach the clock through it).</summary>
    internal PromptUndoHistory History { get; } = new();

    internal TokenTextEditor? Editor => _editor;

    internal PromptChipRenderer Renderer => _renderer;

    internal PromptVariablePop Pop => _pop;

    internal PromptVariablePopup Popup => _popup;

    /// <summary>Whether the "{{" list is showing.</summary>
    internal bool IsListOpen => _popup.IsOpen;

    /// <summary>The window's render scaling (chips snap their boxes to device pixels).</summary>
    internal double RenderScaling { get; private set; } = 1;

    #endregion

    #region Public methods

    /// <summary>Pulses the chips of <paramref name="name"/> once (each call; full motion only).</summary>
    public void FlashVariable(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _renderer.Flash(name);
    }

    /// <summary>Undoes the last step; false when there is none.</summary>
    public bool Undo() => Restore(History.Undo(CurrentText));

    /// <summary>Redoes the last undone step; false when there is none.</summary>
    public bool Redo() => Restore(History.Redo(CurrentText));

    /// <summary>Selects from <paramref name="anchor"/> to <paramref name="active"/> (equal: the caret; never inside a chip).</summary>
    public void Select(int anchor, int active) => _editor?.Select(anchor, active);

    #endregion

    #region Tones and colors

    /// <summary>The tone (1 to 4) of <paramref name="name"/>.</summary>
    internal int ToneOf(string name)
    {
        if (ToneSelector is { } selector)
        {
            return Math.Clamp(selector(name), 1, 4);
        }
        return PromptTone.ToneOf(name, EffectiveOrder());
    }

    /// <summary>
    /// The variables, then the names that are only in the text in the order they appear (Arc's effective variables);
    /// cached until either changes, since chips ask for it while they paint.
    /// </summary>
    private List<string> EffectiveOrder()
    {
        if (_order is not null)
        {
            return _order;
        }
        var order = new List<string>(Variables ?? Array.Empty<string>());
        foreach (var textName in _textNames)
        {
            if (!order.Contains(textName))
            {
                order.Add(textName);
            }
        }
        _order = order;
        return order;
    }

    /// <summary>The series color of <paramref name="tone"/> in the current theme.</summary>
    internal Color SeriesColorOf(int tone)
    {
        var brush = tone switch
        {
            1 => GetValue(Series1Property),
            2 => GetValue(Series2Property),
            3 => GetValue(Series3Property),
            _ => GetValue(Series4Property)
        };
        return brush is ISolidColorBrush solid ? solid.Color : PromptTone.SeriesColor(this, tone);
    }

    /// <summary>A chip's text color: its series color mixed 68% into Foreground.</summary>
    internal Color ChipTextColor(string name)
    {
        var foreground = Foreground is ISolidColorBrush solid ? solid.Color : Colors.Black;
        return PromptTone.Mix(foreground, SeriesColorOf(ToneOf(name)), PromptChipColors.TextMix);
    }

    private void InvalidateColors()
    {
        _order = null;
        _renderer.InvalidateColors();
        _pop.InvalidateRows();
    }

    #endregion

    #region Template and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_editor is not null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.TriggerChanged -= OnTriggerChanged;
            _editor.RemoveHandler(KeyDownEvent, OnEditorKeyDown);
            _editor.RemoveHandler(TextInputEvent, OnEditorTextInput);
            _editor.RemoveHandler(TextBox.PastingFromClipboardEvent, OnEditorPasting);
            _editor.RemoveHandler(PointerPressedEvent, OnEditorPointerPressed);
            _editor.RemoveHandler(PointerReleasedEvent, OnEditorPointerReleased);
            _editor.TokenRenderer = null;
            _editor.TokenParser = null;
        }
        CloseList();
        _editor = e.NameScope.Find<TokenTextEditor>(EditorPart);
        if (_editor is null)
        {
            return;
        }
        _editor.TokenParser = PromptTemplateText.Parse;
        _editor.TokenRenderer = _renderer;
        // Arc's editor slot follows its content on the smooth spring (AutoHeight).
        _editor.HeightMotion = MotionSpec.Spring(ArcSpring.Smooth);
        _editor.Triggers.Clear();
        _editor.Triggers.Add(new TokenTrigger('{', PromptTemplateText.VariableKind) { Position = TokenTriggerPosition.Anywhere });
        _editor.Text = Text;
        _editor.TextChanged += OnEditorTextChanged;
        _editor.TriggerChanged += OnTriggerChanged;
        // Tunnel: before the TextBox acts on the key (and after the editor's own tunnel handler refreshed its query).
        _editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.AddHandler(TextInputEvent, OnEditorTextInput, RoutingStrategies.Tunnel);
        _editor.AddHandler(TextBox.PastingFromClipboardEvent, OnEditorPasting, RoutingStrategies.Bubble, handledEventsToo: true);
        // The TextBox handles presses itself; a chip click is watched alongside.
        _editor.AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _editor.AddHandler(PointerReleasedEvent, OnEditorPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        UpdateAccessibleName();
        History.Clear();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this) && _editor is { } editor)
        {
            editor.Focus(e.NavigationMethod, e.KeyModifiers);
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (!_popupParented)
        {
            // Styles, resources and the motion level reach the list through its logical parent.
            ((ISetLogicalParent)_popup).SetParent(this);
            _popupParented = true;
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        CloseList();
        if (_popupParented)
        {
            ((ISetLogicalParent)_popup).SetParent(null);
            _popupParented = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _topLevel = topLevel;
            topLevel.ScalingChanged += OnScalingChanged;
            OnScalingChanged(topLevel, EventArgs.Empty);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }
        CloseList();
        _renderer.Settle();
    }

    private void OnScalingChanged(object? sender, EventArgs e)
    {
        var scaling = _topLevel?.RenderScaling ?? 1;
        if (Math.Abs(scaling - RenderScaling) > 0.001)
        {
            RenderScaling = scaling;
            _renderer.InvalidateColors();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            if (!_syncingText)
            {
                SetTextFromOwner(change.GetNewValue<string>() ?? string.Empty);
            }
        }
        else if (change.Property == VariablesProperty)
        {
            Watch(ref _watchedVariables, change.GetNewValue<IList<string>?>() as INotifyCollectionChanged, OnVariablesChanged);
            OnVariablesChanged(null, null);
        }
        else if (change.Property == ToneSelectorProperty || change.Property == ForegroundProperty ||
                 change.Property == Series1Property || change.Property == Series2Property ||
                 change.Property == Series3Property || change.Property == Series4Property)
        {
            InvalidateColors();
        }
        else if (change.Property == MissingVariablesProperty)
        {
            Watch(ref _watchedMissing, change.GetNewValue<IEnumerable<string>?>() as INotifyCollectionChanged, OnMissingChanged);
            OnMissingChanged(null, null);
        }
        else if (change.Property == ActiveVariableProperty)
        {
            _renderer.SetActive(change.GetNewValue<string?>());
        }
        else if (change.Property == AccessibleNameProperty || change.Property == AutomationProperties.HelpTextProperty)
        {
            UpdateAccessibleName();
        }
        else if (change.Property == ListNameProperty)
        {
            AutomationProperties.SetName(_pop, change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == AddVariableTextProperty)
        {
            _pop.InvalidateRows();
        }
        else if (change.Property == UndoGroupDelayProperty)
        {
            History.GroupDelay = change.GetNewValue<TimeSpan>();
        }
        else if (change.Property == IsReadOnlyProperty && change.GetNewValue<bool>())
        {
            CloseList();
        }
    }

    private static void Watch(ref INotifyCollectionChanged? watched, INotifyCollectionChanged? next, NotifyCollectionChangedEventHandler handler)
    {
        if (watched is not null)
        {
            watched.CollectionChanged -= handler;
        }
        watched = next;
        if (watched is not null)
        {
            watched.CollectionChanged += handler;
        }
    }

    private void OnVariablesChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        InvalidateColors();
        if (_editor?.TriggerState is { } state)
        {
            UpdateList(state);
        }
    }

    private void OnMissingChanged(object? sender, NotifyCollectionChangedEventArgs? e) => _renderer.SetMissing(MissingVariables);

    private void UpdateAccessibleName()
    {
        if (_editor is null)
        {
            return;
        }
        if (string.IsNullOrEmpty(AccessibleName))
        {
            _editor.ClearValue(AutomationProperties.NameProperty);
        }
        else
        {
            AutomationProperties.SetName(_editor, AccessibleName);
        }
        // The hint under the editor (Arc's aria-describedby), set on this control, reaches the text box too.
        if (AutomationProperties.GetHelpText(this) is { Length: > 0 } help)
        {
            AutomationProperties.SetHelpText(_editor, help);
        }
        else
        {
            _editor.ClearValue(AutomationProperties.HelpTextProperty);
        }
    }

    #endregion

    #region Text

    private string CurrentText => _editor?.Text ?? Text;

    /// <summary>The owner set <see cref="Text"/>: markers are normalized and the undo steps start over.</summary>
    private void SetTextFromOwner(string text)
    {
        var normalized = PromptTemplateText.Normalize(text);
        if (!ReferenceEquals(normalized, text))
        {
            SyncText(normalized);
        }
        _textNames = PromptTemplateText.Names(normalized);
        _order = null;
        if (_editor is null || string.Equals(_editor.Text, normalized, StringComparison.Ordinal))
        {
            return;
        }
        CloseList();
        _applying = true;
        try
        {
            _editor.Text = normalized;
        }
        finally
        {
            _applying = false;
        }
        History.Clear();
        InvalidateColors();
    }

    private void SyncText(string text)
    {
        _syncingText = true;
        try
        {
            SetCurrentValue(TextProperty, text);
        }
        finally
        {
            _syncingText = false;
        }
    }

    /// <summary>The editor's text changed: an undo step (unless this editor made the change), then the value.</summary>
    private void OnEditorTextChanged(object? sender, TokenTextChangedEventArgs e)
    {
        if (string.Equals(e.OldText, e.Text, StringComparison.Ordinal))
        {
            return;
        }
        if (!_applying)
        {
            History.Record(e.OldText, e.Text, _pendingKind ?? PromptEditKind.Other);
            _pendingKind = null;
            _textBeforeEdit ??= e.OldText;
            PostAfterEdit();
        }
        var names = PromptTemplateText.Names(e.Text);
        if (!SameNames(names, _textNames))
        {
            _textNames = names;
            InvalidateColors();
        }
        SyncText(e.Text);
    }

    private static bool SameNames(List<string> a, List<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// After the TextBox finished the edit (its caret is final): <c>{{ name }}</c> becomes <c>{{name}}</c> within the same
    /// undo step, and names that were not in the text or the variables before are announced through
    /// <see cref="VariableAdded"/>.
    /// </summary>
    private void PostAfterEdit()
    {
        if (_afterEditPosted)
        {
            return;
        }
        _afterEditPosted = true;
        Dispatcher.UIThread.Post(AfterEdit, DispatcherPriority.Normal);
    }

    private void AfterEdit()
    {
        _afterEditPosted = false;
        var before = _textBeforeEdit;
        _textBeforeEdit = null;
        if (_editor is not { } editor)
        {
            return;
        }
        var text = editor.Text;
        var normalized = PromptTemplateText.Normalize(text, editor.CaretIndex, out var caret);
        if (!ReferenceEquals(normalized, text))
        {
            Apply(normalized, caret);
            text = normalized;
        }
        if (before is null)
        {
            return;
        }
        var known = PromptTemplateText.Names(before);
        foreach (var name in PromptTemplateText.Names(text))
        {
            if (!known.Contains(name) && Variables?.Contains(name) != true)
            {
                RaiseEvent(new PromptChipEventArgs(VariableAddedEvent, name));
            }
        }
    }

    /// <summary>Puts <paramref name="text"/> in the editor with the caret at <paramref name="caret"/>, outside the undo steps.</summary>
    private void Apply(string text, int caret)
    {
        if (_editor is not { } editor)
        {
            return;
        }
        _applying = true;
        try
        {
            editor.Text = text;
            editor.Select(caret, caret);
        }
        finally
        {
            _applying = false;
        }
    }

    private bool Restore(string? text)
    {
        if (text is null || _editor is null)
        {
            return false;
        }
        CloseList();
        Apply(text, PromptTemplateText.CaretAfterChange(_editor.Text, text));
        return true;
    }

    #endregion

    #region Keys and pointer

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is not { } editor || editor.IsComposing || e.Handled)
        {
            return;
        }
        var modifiers = e.KeyModifiers;
        if (IsListOpen)
        {
            if (e.Key == Key.Escape && modifiers == KeyModifiers.None)
            {
                // Arc keeps the typed "{{name": only the list goes.
                editor.CancelTrigger();
                CloseList();
                e.Handled = true;
                return;
            }
            if (_pop.TryHandleKey(e.Key, modifiers))
            {
                e.Handled = true;
                return;
            }
        }
        var command = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (e.Key == Key.Enter && command)
        {
            // Never a line break; the owner runs the prompt.
            e.Handled = true;
            if (SubmitCommand is { } submit && submit.CanExecute(null))
            {
                submit.Execute(null);
            }
            return;
        }
        if (IsRedo(e))
        {
            e.Handled = true;
            if (!IsReadOnly)
            {
                Redo();
            }
            return;
        }
        if (IsUndo(e))
        {
            e.Handled = true;
            if (!IsReadOnly)
            {
                Undo();
            }
            return;
        }
        if (IsReadOnly)
        {
            return;
        }
        var collapsed = editor.SelectionStart == editor.SelectionEnd;
        if (e.Key is Key.Back or Key.Delete)
        {
            var wordKey = Application.Current?.PlatformSettings?.HotkeyConfiguration.WholeWordTextActionModifiers ?? KeyModifiers.Control;
            var byWord = collapsed && (modifiers.HasFlag(KeyModifiers.Alt) || (wordKey != KeyModifiers.None && modifiers.HasFlag(wordKey)));
            if (byWord)
            {
                // A chip is one word: select the word (the TextBox then deletes the selection in one edit).
                var text = editor.Text;
                var caret = editor.CaretIndex;
                var tokens = editor.Tokens;
                var other = e.Key == Key.Back ? PromptTemplateText.WordDeleteStart(text, tokens, caret) : WordDeleteEnd(text, tokens, caret);
                if (other != caret)
                {
                    editor.Select(caret, other);
                }
                _pendingKind = PromptEditKind.Other;
            }
            else
            {
                _pendingKind = collapsed ? PromptEditKind.Deleting : PromptEditKind.Other;
            }
            return;
        }
        _pendingKind = PromptEditKind.Other;
    }

    /// <summary>Where Alt+Delete at <paramref name="caret"/> deletes to: blanks, then a chip or a word.</summary>
    private static int WordDeleteEnd(string text, IReadOnlyList<TokenSpan> chips, int caret)
    {
        var at = Math.Clamp(caret, 0, text.Length);
        if (at < text.Length && text[at] == '\n')
        {
            return at + 1;
        }
        while (at < text.Length && text[at] is ' ' or '\t')
        {
            at++;
        }
        foreach (var chip in chips)
        {
            if (chip.Start == at)
            {
                return chip.End;
            }
        }
        if (at >= text.Length || text[at] == '\n')
        {
            return at;
        }
        var word = PromptTemplateText.IsNameChar(text[at]);
        while (at < text.Length)
        {
            var c = text[at];
            if (char.IsWhiteSpace(c) || PromptTemplateText.IsNameChar(c) != word || StartsChip(chips, at))
            {
                break;
            }
            at++;
        }
        return at;
    }

    private static bool StartsChip(IReadOnlyList<TokenSpan> chips, int index)
    {
        foreach (var chip in chips)
        {
            if (chip.Start == index)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsUndo(KeyEventArgs e) =>
        Matches(Application.Current?.PlatformSettings?.HotkeyConfiguration.Undo, e) ||
        (e.Key == Key.Z && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && !e.KeyModifiers.HasFlag(KeyModifiers.Shift));

    private static bool IsRedo(KeyEventArgs e) =>
        Matches(Application.Current?.PlatformSettings?.HotkeyConfiguration.Redo, e) ||
        (e.Key == Key.Z && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.KeyModifiers.HasFlag(KeyModifiers.Shift));

    private static bool Matches(List<KeyGesture>? gestures, KeyEventArgs e)
    {
        if (gestures is null)
        {
            return false;
        }
        foreach (var gesture in gestures)
        {
            if (gesture.Matches(e))
            {
                return true;
            }
        }
        return false;
    }

    private void OnEditorTextInput(object? sender, TextInputEventArgs e)
    {
        var collapsed = _editor is null || _editor.SelectionStart == _editor.SelectionEnd;
        _pendingKind = collapsed && e.Text is { Length: 1 } ? PromptEditKind.Typing : PromptEditKind.Other;
    }

    private void OnEditorPasting(object? sender, RoutedEventArgs e) => _pendingKind = PromptEditKind.Other;

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedChip = null;
        if (_editor is { } editor && e.GetCurrentPoint(editor).Properties.IsLeftButtonPressed &&
            editor.HitTestToken(e.GetPosition(editor)) is { Kind: PromptTemplateText.VariableKind } chip)
        {
            _pressedChip = chip;
        }
    }

    private void OnEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressedChip;
        _pressedChip = null;
        if (pressed is not null && _editor is { } editor && e.InitialPressMouseButton == MouseButton.Left &&
            editor.HitTestToken(e.GetPosition(editor)) is { } chip && chip.Id == pressed.Id)
        {
            RaiseEvent(new PromptChipEventArgs(ChipInvokedEvent, chip.Id));
        }
    }

    #endregion

    #region The "{{" list

    private void OnTriggerChanged(object? sender, TokenTriggerChangedEventArgs e)
    {
        if (e.State is { } state)
        {
            UpdateList(state);
        }
        else
        {
            CloseList();
        }
    }

    private void UpdateList(TokenTriggerState state)
    {
        if (_editor is not { } editor || IsReadOnly || !PromptTemplateText.IsVariableQuery(editor.Text, state.Start))
        {
            CloseList();
            return;
        }
        var rows = PromptTemplateText.Rows(Variables ?? (IEnumerable<string>)Array.Empty<string>(), state.Query);
        if (!_pop.Show(rows))
        {
            CloseList();
            return;
        }
        _popup.PlacementTarget = editor;
        _popup.Place(state.CaretRect);
        if (!_popup.IsOpen)
        {
            _popup.IsOpen = true;
        }
        editor.SuggestionsExpanded = true;
        UpdateItemStatus();
    }

    private void CloseList()
    {
        _popup.CloseNow();
        _pop.Show(Array.Empty<PromptVariableRow>());
        if (_editor is { } editor)
        {
            editor.SuggestionsExpanded = false;
            editor.ClearValue(AutomationProperties.ItemStatusProperty);
        }
    }

    private void UpdateItemStatus()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        if (IsListOpen && _pop.ActiveRowName is { } name)
        {
            AutomationProperties.SetItemStatus(editor, name);
        }
        else
        {
            editor.ClearValue(AutomationProperties.ItemStatusProperty);
        }
    }

    /// <summary>A row was chosen: "{{query" becomes the chip (a new name also becomes a variable through <see cref="VariableAdded"/>).</summary>
    private void OnRowAccepted(object? sender, PromptVariableRow row)
    {
        if (_editor is not { } editor || editor.TriggerState is not { } state)
        {
            return;
        }
        var from = state.Start - 1;
        var to = Math.Min(editor.Text.Length, state.Start + 1 + state.Query.Length);
        CloseList();
        _pendingKind = PromptEditKind.Other;
        editor.Select(from, to);
        editor.InsertText("{{" + row.Name + "}}");
    }

    #endregion
}
