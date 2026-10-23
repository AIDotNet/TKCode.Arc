using TKCode.Arc.DesignTokens;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The card details drawer (internal), scoped to the board: a floating panel 8px from the board's edges, at most 400
/// wide (never wider than the board minus 16, so it covers a narrow board). It slides in from the right on the layout
/// spring while the board's scrim fades in; it is modal inside the board (Tab cycles in it, Escape closes it, focus
/// returns to the card). It edits the title, status, owner, notes and checklist of <see cref="Card"/> live.
/// </summary>
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(KeyPart, typeof(TextBlock))]
[TemplatePart(ClosePart, typeof(AvaloniaButton))]
[TemplatePart(TitlePart, typeof(TextBox))]
[TemplatePart(StatusPart, typeof(KanbanSegmented))]
[TemplatePart(OwnerPart, typeof(AvaloniaButton))]
[TemplatePart(OwnerAvatarPart, typeof(ProjectsAvatar))]
[TemplatePart(OwnerNamePart, typeof(TextBlock))]
[TemplatePart(NotesPart, typeof(TextBox))]
[TemplatePart(StepsPart, typeof(StackPanel))]
[TemplatePart(StepFieldPart, typeof(TextBox))]
[TemplatePart(AddStepPart, typeof(AvaloniaButton))]
[TemplatePart(ProgressPart, typeof(ProjectsProgressLine))]
internal sealed class KanbanDrawer : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string KeyPart = "PART_Key";
    public const string ClosePart = "PART_Close";
    public const string TitlePart = "PART_Title";
    public const string StatusPart = "PART_Status";
    public const string OwnerFieldPart = "PART_OwnerField";
    public const string OwnerPart = "PART_Owner";
    public const string OwnerAvatarPart = "PART_OwnerAvatar";
    public const string OwnerNamePart = "PART_OwnerName";
    public const string LabelFieldPart = "PART_LabelField";
    public const string LabelPart = "PART_Label";
    public const string DueFieldPart = "PART_DueField";
    public const string DuePart = "PART_Due";
    public const string EstimateFieldPart = "PART_EstimateField";
    public const string EstimatePart = "PART_Estimate";
    public const string NotesFieldPart = "PART_NotesField";
    public const string NotesPart = "PART_Notes";
    public const string StepsFieldPart = "PART_StepsField";
    public const string StepsCountPart = "PART_StepsCount";
    public const string ProgressPart = "PART_Progress";
    public const string StepsPart = "PART_Steps";
    public const string StepFieldPart = "PART_StepField";
    public const string AddStepPart = "PART_AddStep";

    private readonly MenuSurface _ownerMenu = new();
    private readonly List<(KanbanChecklistItem Item, KanbanStepBox Box, TextBlock Text)> _rows = new();
    private Border? _surface;
    private TextBlock? _key;
    private AvaloniaButton? _close;
    private TextBox? _title;
    private KanbanSegmented? _status;
    private AvaloniaButton? _owner;
    private ProjectsAvatar? _ownerAvatar;
    private TextBlock? _ownerName;
    private TextBlock? _ownerField;
    private TextBlock? _labelField;
    private TextBlock? _label;
    private TextBlock? _dueField;
    private TextBlock? _due;
    private TextBlock? _estimateField;
    private TextBlock? _estimate;
    private TextBlock? _notesField;
    private TextBox? _notes;
    private TextBlock? _stepsField;
    private TextBlock? _stepsCount;
    private ProjectsProgressLine? _progress;
    private StackPanel? _steps;
    private TextBox? _stepField;
    private AvaloniaButton? _addStep;
    private MotionValue<double>? _slide;
    private readonly TranslateTransform _translate = new();
    private bool _refreshing;
    private bool _isOpen;
    private int _token;
    private double _boardWidth;
    private KanbanCard? _watchedCard;

    public KanbanDrawer()
    {
        IsVisible = false;
        _ownerMenu.ItemClicked += OnOwnerChosen;
    }

    public KanbanBoard? Board { get; set; }

    /// <summary>The card shown (kept while the drawer slides out).</summary>
    public KanbanCard? Card { get; private set; }

    public bool IsOpen => _isOpen;

    public MenuSurface OwnerMenu => _ownerMenu;

    public AvaloniaButton? CloseButton => _close;

    public TextBox? TitleBox => _title;

    public TextBox? NotesBox => _notes;

    public TextBox? StepField => _stepField;

    public KanbanSegmented? StatusSwitch => _status;

    public AvaloniaButton? OwnerTrigger => _owner;

    public IReadOnlyList<KanbanStepBox> StepBoxes => _rows.Select(r => r.Box).ToList();

    /// <summary>The drawer's horizontal slide (0 = in place; its width + 16 = out of view).</summary>
    public double SlideOffset => _slide?.Value ?? 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _key = e.NameScope.Find<TextBlock>(KeyPart);
        _close = e.NameScope.Find<AvaloniaButton>(ClosePart);
        _title = e.NameScope.Find<TextBox>(TitlePart);
        _status = e.NameScope.Find<KanbanSegmented>(StatusPart);
        _owner = e.NameScope.Find<AvaloniaButton>(OwnerPart);
        _ownerAvatar = e.NameScope.Find<ProjectsAvatar>(OwnerAvatarPart);
        _ownerName = e.NameScope.Find<TextBlock>(OwnerNamePart);
        _ownerField = e.NameScope.Find<TextBlock>(OwnerFieldPart);
        _labelField = e.NameScope.Find<TextBlock>(LabelFieldPart);
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        _dueField = e.NameScope.Find<TextBlock>(DueFieldPart);
        _due = e.NameScope.Find<TextBlock>(DuePart);
        _estimateField = e.NameScope.Find<TextBlock>(EstimateFieldPart);
        _estimate = e.NameScope.Find<TextBlock>(EstimatePart);
        _notesField = e.NameScope.Find<TextBlock>(NotesFieldPart);
        _notes = e.NameScope.Find<TextBox>(NotesPart);
        _stepsField = e.NameScope.Find<TextBlock>(StepsFieldPart);
        _stepsCount = e.NameScope.Find<TextBlock>(StepsCountPart);
        _progress = e.NameScope.Find<ProjectsProgressLine>(ProgressPart);
        _steps = e.NameScope.Find<StackPanel>(StepsPart);
        _stepField = e.NameScope.Find<TextBox>(StepFieldPart);
        _addStep = e.NameScope.Find<AvaloniaButton>(AddStepPart);
        _rows.Clear();
        if (_surface is not null)
        {
            _surface.RenderTransform = _translate;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_title is not null)
        {
            _title.TextChanged += OnTitleChanged;
        }
        if (_notes is not null)
        {
            _notes.TextChanged += OnNotesChanged;
        }
        if (_status is not null)
        {
            _status.SelectionChanged += OnStatusChosen;
        }
        if (_owner is not null)
        {
            DropdownMenu.SetMenu(_owner, _ownerMenu);
        }
        if (_stepField is not null)
        {
            _stepField.TextChanged += OnStepFieldChanged;
            _stepField.AddHandler(KeyDownEvent, OnStepFieldKeyDown, RoutingStrategies.Tunnel);
        }
        if (_addStep is not null)
        {
            _addStep.Click += OnAddStepClick;
        }
        Refresh();
    }

    private void Unhook()
    {
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_title is not null)
        {
            _title.TextChanged -= OnTitleChanged;
        }
        if (_notes is not null)
        {
            _notes.TextChanged -= OnNotesChanged;
        }
        if (_status is not null)
        {
            _status.SelectionChanged -= OnStatusChosen;
        }
        if (_owner is not null)
        {
            DropdownMenu.SetMenu(_owner, null);
        }
        if (_stepField is not null)
        {
            _stepField.TextChanged -= OnStepFieldChanged;
            _stepField.RemoveHandler(KeyDownEvent, OnStepFieldKeyDown);
        }
        if (_addStep is not null)
        {
            _addStep.Click -= OnAddStepClick;
        }
    }

    // Open / close -----------------------------------------------------------------------------------------------------

    /// <summary>Slides in with <paramref name="card"/> and moves the focus to the close button.</summary>
    public void Show(KanbanCard card, NavigationMethod method)
    {
        var wasOpen = _isOpen && IsVisible;
        WatchCard(card);
        Card = card;
        _isOpen = true;
        _token++;
        IsVisible = true;
        IsHitTestVisible = true;
        ApplyTemplate();
        Refresh();
        EnsureSlide();
        if (!wasOpen)
        {
            _slide!.Jump(OutOfView);
        }
        // The panel slides in on the layout spring (Arc: +416 → 0, K195 / C28.7, about 570ms); reduced motion: in place.
        _ = _slide!.AnimateTo(0, KanbanMotion.Layout);
        // The content sits in a scroll viewer that attaches it on its first layout: focus the close button after it.
        var focusMethod = method == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab;
        var token = _token;
        if (_close?.Focus(focusMethod) != true)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_isOpen && token == _token)
                {
                    _close?.Focus(focusMethod);
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>Slides out; the drawer hides once it is out of view.</summary>
    public void Hide()
    {
        if (!_isOpen)
        {
            return;
        }
        _isOpen = false;
        _ownerMenu.Close(returnFocus: false);
        IsHitTestVisible = false;
        EnsureSlide();
        var token = ++_token;
        var task = _slide!.AnimateTo(OutOfView, KanbanMotion.Layout);
        if (task.IsCompleted)
        {
            Finish();
            return;
        }
        task.ContinueWith(_ =>
        {
            if (token == _token)
            {
                Finish();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        void Finish()
        {
            if (!_isOpen)
            {
                IsVisible = false;
                WatchCard(null);
                Card = null;
            }
        }
    }

    private double OutOfView => Math.Max(Width is { } w && double.IsFinite(w) ? w : Bounds.Width, 0) + 16;

    private void EnsureSlide()
    {
        if (_slide is not null)
        {
            return;
        }
        // Code-driven: the travel is the drawer's run-time width plus its 8px margins on both sides.
        _slide = new MotionValue<double>(this, 0, MotionVectors.Double);
        _slide.Changed += x => _translate.X = x;
    }

    /// <summary>The board's width changed: the drawer is min(400, width − 16) wide; under 440 the status options stack two by two.</summary>
    public void UpdateWidth(double boardWidth)
    {
        if (!(boardWidth > 0))
        {
            return;
        }
        _boardWidth = boardWidth;
        var max = Board?.DrawerMaxWidth ?? 400;
        Width = Math.Max(0, Math.Min(max, boardWidth - 16));
        if (_status is not null)
        {
            _status.Columns = boardWidth <= 440 ? 2 : 0;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && _isOpen)
        {
            // The owner menu handles its own Escape first (it closes only the menu); a second Escape closes the drawer.
            e.Handled = true;
            Board?.CloseDetails();
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Board?.CloseDetails();
    }

    // Content ----------------------------------------------------------------------------------------------------------

    private void WatchCard(KanbanCard? card)
    {
        if (_watchedCard is not null)
        {
            _watchedCard.PropertyChanged -= OnCardChanged;
            _watchedCard.Checklist.CollectionChanged -= OnChecklistChanged;
        }
        _watchedCard = card;
        if (card is not null)
        {
            card.PropertyChanged += OnCardChanged;
            card.Checklist.CollectionChanged += OnChecklistChanged;
        }
    }

    private void OnCardChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == KanbanCard.ChecklistProperty && _watchedCard is not null)
        {
            if (e.OldValue is Avalonia.Collections.AvaloniaList<KanbanChecklistItem> old)
            {
                old.CollectionChanged -= OnChecklistChanged;
            }
            _watchedCard.Checklist.CollectionChanged += OnChecklistChanged;
        }
        Refresh();
    }

    private void OnChecklistChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Refresh();

    /// <summary>Shows the card's current values (texts that are being typed are left alone).</summary>
    public void Refresh()
    {
        var board = Board;
        var card = Card;
        if (board is null || _refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            var strings = board.Strings;
            var readOnly = board.IsReadOnly;
            SetText(_ownerField, strings.OwnerField);
            SetText(_labelField, strings.LabelField);
            SetText(_dueField, strings.DueField);
            SetText(_estimateField, strings.EstimateField);
            SetText(_notesField, strings.NotesField);
            SetText(_stepsField, strings.ChecklistField);
            if (_close is not null)
            {
                AutomationProperties.SetName(_close, strings.CloseDetails);
            }
            if (_addStep is not null)
            {
                _addStep.Content = strings.AddStep;
                _addStep.IsVisible = !readOnly;
            }
            if (_stepField is not null)
            {
                _stepField.PlaceholderText = strings.NewStep;
                AutomationProperties.SetName(_stepField, strings.NewStep);
                _stepField.IsVisible = !readOnly;
            }
            if (_notes is not null)
            {
                _notes.PlaceholderText = strings.NotesPlaceholder;
                AutomationProperties.SetName(_notes, strings.NotesField);
                _notes.IsReadOnly = readOnly;
            }
            if (_title is not null)
            {
                AutomationProperties.SetName(_title, strings.TitleField);
                _title.IsReadOnly = readOnly;
            }
            if (card is null)
            {
                return;
            }
            AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(card.Title) ? strings.Untitled : card.Title);
            AutomationProperties.SetHelpText(this, card.Key);
            SetText(_key, card.Key);
            if (_title is not null && (_title.Text ?? string.Empty) != card.Title)
            {
                _title.Text = card.Title;
            }
            if (_notes is not null && (_notes.Text ?? string.Empty) != (card.Description ?? string.Empty))
            {
                _notes.Text = card.Description ?? string.Empty;
            }
            var columns = board.Columns;
            if (_status is not null)
            {
                _status.SetOptions(columns.Select(c => c.Name).ToList());
                _status.SelectedIndex = columns.ToList().FindIndex(c => c.Identity == card.ColumnKey);
                _status.IsEnabled = !readOnly;
                AutomationProperties.SetName(_status, strings.StatusField);
                if (_boardWidth > 0)
                {
                    _status.Columns = _boardWidth <= 440 ? 2 : 0;
                }
            }
            var owner = board.PersonFor(card.AssigneeKey);
            if (_ownerAvatar is not null)
            {
                _ownerAvatar.Person = owner;
                _ownerAvatar.IsVisible = owner is not null;
            }
            SetText(_ownerName, owner?.Name ?? strings.Unassigned);
            if (_owner is not null)
            {
                AutomationProperties.SetName(_owner, strings.OwnerTriggerName(owner?.Name ?? strings.Unassigned));
                _owner.IsEnabled = !readOnly;
            }
            RebuildOwnerMenu(board, owner);
            SetText(_label, board.LabelFor(card.LabelKey)?.Name ?? strings.NoLabel);
            SetText(_due, card.Due is { } due ? strings.Date(due) : strings.NoDate);
            SetText(_estimate, card.Points is { } points ? strings.Points(points) : strings.NoEstimate);
            var total = card.ChecklistTotal;
            if (_stepsCount is not null)
            {
                _stepsCount.Text = total > 0 ? string.Format(CultureInfo.InvariantCulture, "{0}/{1}", card.ChecklistDone, total) : null;
                _stepsCount.IsVisible = total > 0;
                AutomationProperties.SetName(_stepsCount, total > 0 ? strings.ChecklistProgress(card.ChecklistDone, total) : string.Empty);
            }
            if (_progress is not null)
            {
                _progress.IsVisible = total > 0;
                _progress.Maximum = Math.Max(1, total);
                _progress.Value = card.ChecklistDone;
                if (TopLevel.GetTopLevel(this) is not null)
                {
                    var fill = KanbanTokens.Color(this, card.IsChecklistComplete ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Accent);
                    _progress.FillBrush = new ImmutableSolidColorBrush(fill);
                    _progress.TrackBrush = new ImmutableSolidColorBrush(KanbanTokens.Color(this, ArcDesignTokenKind.SurfaceMuted));
                }
            }
            RebuildSteps(card, readOnly);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    private void RebuildOwnerMenu(KanbanBoard board, ProjectPerson? owner)
    {
        var wanted = board.People.Cast<ProjectPerson?>().ToList();
        if (board.AllowUnassigned)
        {
            wanted.Add(null);
        }
        var current = _ownerMenu.Items.OfType<MenuEntry>().Select(e => e.Tag as ProjectPerson).ToList();
        var same = current.Count == wanted.Count && current.Zip(wanted).All(p => ReferenceEquals(p.First, p.Second));
        if (!same)
        {
            _ownerMenu.Items.Clear();
            foreach (var person in wanted)
            {
                var entry = new MenuEntry
                {
                    Header = person?.Name ?? board.Strings.Unassigned,
                    Tag = person,
                    Icon = person is null
                        ? ArcIcons.User
                        : new ProjectsAvatar { Person = person, Width = 20, Height = 20, IsDecorative = true }
                };
                _ownerMenu.Items.Add(entry);
            }
        }
        foreach (var entry in _ownerMenu.Items.OfType<MenuEntry>())
        {
            entry.IsChecked = ReferenceEquals(entry.Tag, owner);
        }
    }

    private void RebuildSteps(KanbanCard card, bool readOnly)
    {
        if (_steps is null)
        {
            return;
        }
        var items = card.Checklist;
        var same = _rows.Count == items.Count && _rows.Select(r => r.Item).SequenceEqual(items);
        if (!same)
        {
            foreach (var row in _rows)
            {
                row.Box.IsCheckedChanged -= OnStepToggled;
            }
            _rows.Clear();
            _steps.Children.Clear();
            foreach (var item in items)
            {
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 19.6 };
                var box = new KanbanStepBox { Content = text, Tag = item };
                box.IsCheckedChanged += OnStepToggled;
                _rows.Add((item, box, text));
                _steps.Children.Add(box);
            }
        }
        var muted = TopLevel.GetTopLevel(this) is not null ? KanbanTokens.Color(this, ArcDesignTokenKind.TextMuted) : Colors.Gray;
        var strike = TopLevel.GetTopLevel(this) is not null ? KanbanTokens.Color(this, ArcDesignTokenKind.BorderStrong) : Colors.Gray;
        foreach (var (item, box, text) in _rows)
        {
            text.Text = item.Text;
            box.IsChecked = item.IsDone;
            box.IsEnabled = !readOnly;
            AutomationProperties.SetName(box, item.Text);
            if (item.IsDone)
            {
                text.Foreground = new ImmutableSolidColorBrush(muted);
                text.TextDecorations =
                [
                    new TextDecoration { Location = TextDecorationLocation.Strikethrough, Stroke = new ImmutableSolidColorBrush(strike) }
                ];
            }
            else
            {
                text.ClearValue(TextBlock.ForegroundProperty);
                text.TextDecorations = null;
            }
        }
    }

    // Edits ------------------------------------------------------------------------------------------------------------

    private void OnTitleChanged(object? sender, TextChangedEventArgs e)
    {
        if (_refreshing || _title is null || Card is not { } card || Board is not { } board)
        {
            return;
        }
        var text = _title.Text ?? string.Empty;
        if (text.Contains('\n') || text.Contains('\r'))
        {
            // A title has no line breaks: Enter (and pasted lines) become spaces, as Arc.
            var caret = _title.CaretIndex;
            var flat = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            _refreshing = true;
            try
            {
                _title.Text = flat;
                _title.CaretIndex = Math.Min(caret, flat.Length);
            }
            finally
            {
                _refreshing = false;
            }
            text = flat;
        }
        board.EditTitle(card, text);
    }

    private void OnNotesChanged(object? sender, TextChangedEventArgs e)
    {
        if (_refreshing || _notes is null || Card is not { } card)
        {
            return;
        }
        Board?.EditDescription(card, _notes.Text);
    }

    private void OnStatusChosen(int index)
    {
        if (_refreshing || Card is not { } card || Board is not { } board || index < 0 || index >= board.Columns.Count)
        {
            return;
        }
        board.ChangeStatus(card, board.Columns[index]);
    }

    private void OnOwnerChosen(object? sender, MenuEntryClickedEventArgs e)
    {
        if (Card is not { } card || Board is not { } board)
        {
            return;
        }
        board.ChangeOwner(card, e.Entry.Tag as ProjectPerson);
    }

    private void OnStepToggled(object? sender, RoutedEventArgs e)
    {
        if (_refreshing || sender is not KanbanStepBox { Tag: KanbanChecklistItem item } box || Card is not { } card)
        {
            return;
        }
        Board?.ToggleStep(card, item, box.IsChecked == true);
    }

    private void OnStepFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (_addStep is not null)
        {
            _addStep.IsEnabled = !string.IsNullOrWhiteSpace(_stepField?.Text);
        }
    }

    private void OnStepFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AddStep();
        }
    }

    private void OnAddStepClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AddStep();
        _stepField?.Focus(NavigationMethod.Tab);
    }

    private void AddStep()
    {
        if (_stepField is null || Card is not { } card || string.IsNullOrWhiteSpace(_stepField.Text))
        {
            return;
        }
        Board?.AddStep(card, _stepField.Text);
        _stepField.Text = string.Empty;
    }
}
