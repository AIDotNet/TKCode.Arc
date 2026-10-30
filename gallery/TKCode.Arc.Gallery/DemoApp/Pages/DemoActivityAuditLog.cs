using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev's Audit log block, which has no port in the library, built for this page: a search, a date range and a Live
/// switch over category chips with counts, a list of events (8 at a time) that open into the changed fields and the
/// event's facts, an Export CSV button that pretends to export, and a Live mode that adds an event every few seconds and
/// ages the others. Below 640px of its own width the IP column folds into the meta line.
/// </summary>
internal sealed class DemoAuditLog : UserControl
{
    private const int PageSize = 8;
    private const double NarrowWidth = 640;
    private const double TightWidth = 440;

    private static readonly int[] s_liveDelays = [900, 3600, 4400, 3200, 5000, 3800];

    private readonly List<AuditEvent> _events = DemoActivityData.Events().ToList();
    private readonly IReadOnlyList<AuditEvent> _templates = DemoActivityData.LiveTemplates();
    private readonly Dictionary<string, AuditRow> _rows = new();

    private readonly SearchField _search;
    private readonly SegmentedControl _range;
    private readonly SegmentedControl _chips;
    private readonly Dictionary<string, AnimatedCounter> _chipCounts = new();
    private readonly Switch _live;
    private readonly Button _export;
    private readonly StackPanel _list = new();
    private readonly Control _listHeader;
    private readonly Border _empty = new();
    private readonly TextBlock _footerNote;
    private readonly AnimatedCounter _shownCount = Counter();
    private readonly AnimatedCounter _totalCount = Counter();
    private readonly TextBlock _eventsWord = DemoUi.Text(" events", ArcDesignTokenKind.TextMuted, 12);
    private readonly StackPanel _showing;
    private readonly Button _more = DemoUi.Button("Show more", ButtonVariant.Ghost);
    private readonly List<Border> _padded = new();

    private readonly Grid _toolbar;
    private readonly Control _toolbarEnd;
    private double _clock;
    private int _shown = PageSize;
    private string? _openId;
    private int _liveCount;
    private int _liveStep;
    private DispatcherTimer? _liveTimer;
    private bool _narrow;
    private bool _tight;
    private string? _freshId;
    private int _exportRun;

    public DemoAuditLog()
    {
        // Header: the title and the export button.
        _export = DemoUi.Button("Export CSV", ButtonVariant.Secondary, ArcIcons.Download);
        _export.Click += (_, _) => Export();
        var header = Pad(DemoUi.Head("Audit log", "Northwind workspace · kept for 400 days", _export), new Thickness(16, 16, 16, 0));

        // Toolbar: the search, then the date range and Live at the end.
        _search = new SearchField
        {
            Label = "Search events",
            IsLabelVisible = false,
            Placeholder = "Search people, actions, IPs",
            Size = ControlSize.Small,
            MinWidth = 160
        };
        _search.TextChanged += (_, _) => FiltersChanged();
        _range = new SegmentedControl { VerticalAlignment = VerticalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(_range, "Date range");
        _range.Items.Add(new SegmentedOption { Value = "24h", Label = "24 hours" });
        _range.Items.Add(new SegmentedOption { Value = "7d", Label = "7 days" });
        _range.Items.Add(new SegmentedOption { Value = "30d", Label = "30 days" });
        _range.SelectedValue = "7d";
        _range.SelectionChanged += (_, _) => FiltersChanged();
        _live = new Switch { Content = "Live", VerticalAlignment = VerticalAlignment.Center };
        _live.IsCheckedChanged += (_, _) => SetLive(_live.IsChecked == true);
        var end = DemoUi.Actions(_range, _live);
        end.Spacing = 12;
        // The search takes the room the end controls leave; on a narrow log they go under it.
        _toolbar = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        _toolbarEnd = end;
        _toolbar.Children.Add(_search);
        _toolbar.Children.Add(end);
        LayOutToolbar(stacked: false);
        var toolbarHost = Pad(_toolbar, new Thickness(16, 12, 16, 0));

        // Category chips with their counts; the highlight slides between them.
        _chips = new SegmentedControl { HorizontalAlignment = HorizontalAlignment.Left };
        Avalonia.Automation.AutomationProperties.SetName(_chips, "Category");
        AddChip("all", "All");
        foreach (var category in Enum.GetValues<AuditCategory>())
        {
            AddChip(category.ToString(), DemoActivityData.Label(category));
        }
        _chips.SelectedValue = "all";
        _chips.SelectionChanged += (_, _) => FiltersChanged();
        var chipsHost = Pad(new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _chips
        }, new Thickness(16, 12, 16, 12));

        // The column heads.
        var heads = Columns(narrow: false);
        heads.Children.Add(At(Caption("Event"), 1));
        heads.Children.Add(At(Caption("IP address"), 2));
        heads.Children.Add(At(Caption("Time"), 3));
        var headBorder = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 8), Child = heads };
        ArcTokens.Bind(headBorder, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        _padded.Add(headBorder);
        _listHeader = headBorder;

        var listTop = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = _list };
        // The list header's hairline is the subtle one; without the header (narrow) it is the only line above the rows.
        ArcTokens.Bind(listTop, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        _list.Spacing = 0;

        // Footer: the count and Show more / fewer.
        _showing = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _showing.Children.Add(DemoUi.Text("Showing ", ArcDesignTokenKind.TextMuted, 12));
        _showing.Children.Add(_shownCount);
        _showing.Children.Add(DemoUi.Text(" of ", ArcDesignTokenKind.TextMuted, 12));
        _showing.Children.Add(_totalCount);
        _showing.Children.Add(_eventsWord);
        foreach (var child in _showing.Children)
        {
            child.VerticalAlignment = VerticalAlignment.Center;
        }
        _footerNote = DemoUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 12);
        _footerNote.IsVisible = false;
        _footerNote.VerticalAlignment = VerticalAlignment.Center;
        _more.Click += (_, _) =>
        {
            var total = Matches().Count;
            _shown = _shown >= total ? PageSize : _shown + PageSize;
            Render();
        };
        var footerText = new Panel { VerticalAlignment = VerticalAlignment.Center, Children = { _showing, _footerNote } };
        var footer = new DockPanel { MinHeight = 32 };
        DockPanel.SetDock(_more, Dock.Right);
        footer.Children.Add(_more);
        footer.Children.Add(footerText);
        var footerHost = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 10), Child = footer };
        ArcTokens.Bind(footerHost, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        _padded.Add(footerHost);

        var column = new StackPanel();
        column.Children.Add(header);
        column.Children.Add(toolbarHost);
        column.Children.Add(chipsHost);
        column.Children.Add(headBorder);
        column.Children.Add(listTop);
        column.Children.Add(_empty);
        column.Children.Add(footerHost);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(DemoUi.CardRadius),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = column
        };
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        Content = frame;

        SizeChanged += (_, e) => SetWidth(e.NewSize.Width);
        DetachedFromVisualTree += (_, _) => StopLive();
        AttachedToVisualTree += (_, _) =>
        {
            if (_live.IsChecked == true)
            {
                SetLive(true);
            }
        };
        Render();
    }

    private void AddChip(string value, string label)
    {
        var count = Counter();
        _chipCounts[value] = count;
        _chips.Items.Add(new SegmentedOption { Value = value, Label = label, Accessory = count });
    }

    private static AnimatedCounter Counter() => new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    private Border Pad(Control child, Thickness padding)
    {
        var border = new Border { Padding = padding, Child = child };
        _padded.Add(border);
        return border;
    }

    private static TextBlock Caption(string text) => DemoUi.Line(text, ArcDesignTokenKind.TextMuted, 12);

    private static Control At(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

    /// <summary>The five columns (avatar, event, IP, time, chevron), or four when narrow (no IP).</summary>
    internal static Grid Columns(bool narrow) => new()
    {
        ColumnDefinitions = narrow ? new ColumnDefinitions("28,*,0,Auto,16") : new ColumnDefinitions("28,*,120,88,16"),
        ColumnSpacing = 12
    };

    private void SetWidth(double width)
    {
        var narrow = width <= NarrowWidth;
        var tight = width <= TightWidth;
        if (tight != _tight)
        {
            _tight = tight;
            foreach (var border in _padded)
            {
                var p = border.Padding;
                border.Padding = new Thickness(tight ? 12 : 16, p.Top, tight ? 12 : 16, p.Bottom);
            }
            foreach (var row in _rows.Values)
            {
                row.SetTight(tight);
            }
        }
        if (narrow != _narrow)
        {
            _narrow = narrow;
            LayOutToolbar(narrow);
            _listHeader.IsVisible = !narrow;
            foreach (var row in _rows.Values)
            {
                row.SetNarrow(narrow);
            }
        }
    }

    private void LayOutToolbar(bool stacked)
    {
        _toolbar.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("*,Auto");
        _toolbar.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
        Grid.SetColumn(_toolbarEnd, stacked ? 0 : 1);
        Grid.SetRow(_toolbarEnd, stacked ? 1 : 0);
        _toolbarEnd.HorizontalAlignment = HorizontalAlignment.Left;
    }

    // ---- Filtering ---------------------------------------------------------------------------------------------------

    private double RangeSeconds => (_range.SelectedValue as string) switch
    {
        "24h" => 86400,
        "30d" => 2592000,
        _ => 604800
    };

    private string RangePhrase => (_range.SelectedValue as string) switch
    {
        "24h" => "the last 24 hours",
        "30d" => "the last 30 days",
        _ => "the last 7 days"
    };

    private AuditCategory? Category => Enum.TryParse<AuditCategory>(_chips.SelectedValue as string, out var category) ? category : null;

    private string Query => (_search.Text ?? string.Empty).Trim();

    /// <summary>Events in range that match the search, newest first (the category filter is not applied).</summary>
    private List<AuditEvent> InRange()
    {
        var query = Query;
        var range = RangeSeconds;
        return _events
            .Where(item => _clock - item.At <= range && (query.Length == 0 || Matches(item, query)))
            .OrderByDescending(item => item.At)
            .ToList();
    }

    private List<AuditEvent> Matches()
    {
        var category = Category;
        return InRange().Where(item => category is null || item.Category == category).ToList();
    }

    private static bool Matches(AuditEvent item, string query)
    {
        bool Has(string? text) => text is not null && text.Contains(query, StringComparison.OrdinalIgnoreCase);
        return Has(item.Actor.Name) || Has(item.Action) || Has(item.Target) || Has(item.Actor.Ip) || Has(item.Actor.Location)
               || Has(DemoActivityData.Label(item.Category))
               || item.Changes.Any(change => Has(change.Field) || Has(change.Before) || Has(change.After));
    }

    private void FiltersChanged()
    {
        _shown = PageSize;
        Render();
    }

    private void ClearFilters()
    {
        _chips.SelectedValue = "all";
        _search.Text = string.Empty;
        Render();
        _search.Focus();
    }

    // ---- Rendering ---------------------------------------------------------------------------------------------------

    private void Render()
    {
        var inRange = InRange();
        _chipCounts["all"].Value = inRange.Count;
        foreach (var category in Enum.GetValues<AuditCategory>())
        {
            _chipCounts[category.ToString()].Value = inRange.Count(item => item.Category == category);
        }

        var matches = Matches();
        var visible = matches.Take(_shown).ToList();
        if (_openId is not null && visible.All(item => item.Id != _openId))
        {
            _openId = null;
        }

        // Keep the rows that stay (their open state and focus), drop the others, add the new ones in order.
        var keep = visible.Select(item => item.Id).ToHashSet();
        foreach (var id in _rows.Keys.Where(id => !keep.Contains(id)).ToList())
        {
            _rows.Remove(id);
        }
        _list.Children.Clear();
        for (var i = 0; i < visible.Count; i++)
        {
            var item = visible[i];
            if (!_rows.TryGetValue(item.Id, out var row))
            {
                row = new AuditRow(this, item, _narrow, _tight, entering: item.Id == _freshId);
                _rows[item.Id] = row;
            }
            row.SetAge(_clock - item.At);
            row.SetOpen(item.Id == _openId, animate: false);
            row.SetLast(i == visible.Count - 1);
            _list.Children.Add(row);
        }
        _freshId = null;

        // Footer.
        _shownCount.Value = visible.Count;
        _totalCount.Value = matches.Count;
        _eventsWord.Text = matches.Count == 1 ? " event" : " events";
        var remaining = matches.Count - visible.Count;
        _more.IsVisible = matches.Count > PageSize;
        SetButtonText(_more, remaining > 0 ? $"Show {Math.Min(PageSize, remaining)} more" : "Show fewer");
        _export.IsEnabled = matches.Count > 0 || _exportRun != 0;
        Avalonia.Automation.AutomationProperties.SetName(_export, $"Export {Plural(matches.Count)} as CSV");

        // Empty state in place of the rows.
        _empty.IsVisible = matches.Count == 0;
        if (matches.Count == 0)
        {
            _empty.Child = BuildEmpty();
        }
    }

    private Control BuildEmpty()
    {
        var category = Category;
        var filtered = category is not null || Query.Length > 0;
        var title = Query.Length > 0
            ? $"No events match “{Query}”"
            : $"No {(category is { } c ? DemoActivityData.Label(c).ToLowerInvariant() + " " : string.Empty)}events in {RangePhrase}";
        var body = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        var head = DemoUi.Text(title, ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium);
        head.TextAlignment = TextAlignment.Center;
        var hint = DemoUi.Text(filtered ? "Clear the filters or widen the date range." : "Try a wider date range.", ArcDesignTokenKind.TextMuted, 12);
        hint.TextAlignment = TextAlignment.Center;
        body.Children.Add(head);
        body.Children.Add(hint);
        if (filtered)
        {
            var clear = DemoUi.Button("Clear filters");
            clear.HorizontalAlignment = HorizontalAlignment.Center;
            clear.Margin = new Thickness(0, 8, 0, 0);
            clear.Click += (_, _) => ClearFilters();
            body.Children.Add(clear);
        }
        return new Border { Padding = new Thickness(16, 40), Child = body };
    }

    private static string Plural(int count) => count == 1 ? "1 event" : $"{count} events";

    private static void SetButtonText(Button button, string text) => button.Content = text;

    /// <summary>A button's glyph and label (as <see cref="DemoUi.Button"/> lays them out).</summary>
    private static Control ButtonContent(string label, Geometry icon)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new ArcGlyph { Data = icon, Size = 14, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    // ---- Rows --------------------------------------------------------------------------------------------------------

    /// <summary>Opens one row and closes the one that was open.</summary>
    private void Toggle(AuditRow row)
    {
        var opening = _openId != row.Item.Id;
        if (_openId is not null && _rows.TryGetValue(_openId, out var open))
        {
            open.SetOpen(false, animate: true);
        }
        _openId = opening ? row.Item.Id : null;
        row.SetOpen(opening, animate: true);
    }

    /// <summary>Arrow keys, Home and End move between the rows.</summary>
    private void MoveFocus(AuditRow from, Key key)
    {
        var rows = _list.Children.OfType<AuditRow>().ToList();
        var index = rows.IndexOf(from);
        var target = key switch
        {
            Key.Up => Math.Max(0, index - 1),
            Key.Down => Math.Min(rows.Count - 1, index + 1),
            Key.Home => 0,
            _ => rows.Count - 1
        };
        rows[target].FocusTrigger();
    }

    // ---- Export ------------------------------------------------------------------------------------------------------

    // Nothing is written: the button spins for 1.1 s, says "Exported" until 3.4 s, and the footer names the file until 5.2 s.
    private void Export()
    {
        var count = Matches().Count;
        if (_exportRun != 0 || count == 0)
        {
            return;
        }
        var run = ++_exportRun;
        _export.IsLoading = true;
        DispatcherTimer.RunOnce(() =>
        {
            _export.IsLoading = false;
            _export.Content = ButtonContent("Exported", ArcIcons.Check);
            Avalonia.Automation.AutomationProperties.SetName(_export, $"Exported {Plural(count)}");
            _footerNote.Text = $"✓ {Plural(count)} exported to audit-log-2026-09-22.csv";
            _footerNote.IsVisible = true;
            _showing.IsVisible = false;
        }, TimeSpan.FromMilliseconds(1100));
        DispatcherTimer.RunOnce(() =>
        {
            _export.Content = ButtonContent("Export CSV", ArcIcons.Download);
            _exportRun = run == _exportRun ? 0 : _exportRun;
            Render();
        }, TimeSpan.FromMilliseconds(3400));
        DispatcherTimer.RunOnce(() =>
        {
            _footerNote.IsVisible = false;
            _showing.IsVisible = true;
        }, TimeSpan.FromMilliseconds(5200));
    }

    // ---- Live --------------------------------------------------------------------------------------------------------

    private void SetLive(bool on)
    {
        StopLive();
        if (!on)
        {
            return;
        }
        _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(s_liveDelays[_liveStep % s_liveDelays.Length]) };
        _liveTimer.Tick += (_, _) => LiveTick();
        _liveTimer.Start();
    }

    private void StopLive()
    {
        _liveTimer?.Stop();
        _liveTimer = null;
    }

    // The next template lands "now"; the clock moves on by the delay, so every other row ages by it.
    private void LiveTick()
    {
        var delay = s_liveDelays[_liveStep % s_liveDelays.Length] / 1000.0;
        var template = _templates[_liveCount % _templates.Count];
        _liveCount++;
        _liveStep++;
        var item = template with { Id = $"live-{_liveCount}", At = _clock + delay };
        _clock += delay;
        _events.Add(item);
        _freshId = item.Id;
        Render();
        if (_liveTimer is not null)
        {
            _liveTimer.Interval = TimeSpan.FromMilliseconds(s_liveDelays[_liveStep % s_liveDelays.Length]);
        }
    }

    /// <summary>One event: a full-width trigger over a panel that opens with the changed fields and the facts.</summary>
    private sealed class AuditRow : StackPanel
    {
        private readonly DemoAuditLog _owner;
        private readonly Border _trigger;
        private readonly Border _hover = new() { Opacity = 0, IsHitTestVisible = false };
        private readonly Grid _columns;
        private readonly TextBlock _ip;
        private readonly TextBlock _time;
        private readonly TextBlock _metaIp;
        private readonly RotateTransform _chevronTurn = new();
        private readonly DemoReveal _panel;
        private readonly Border _panelPad;
        private readonly Border _rule = new() { Height = 1 };
        private bool _open;
        private bool _pointerOver;

        public AuditRow(DemoAuditLog owner, AuditEvent item, bool narrow, bool tight, bool entering)
        {
            _owner = owner;
            Item = item;

            var avatar = new Avatar { DisplayName = item.Actor.Name, Source = DemoPhotos.For(item.Actor.Name), Size = AvatarSize.Small, VerticalAlignment = VerticalAlignment.Center };

            var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = DemoUi.BodySize };
            ArcTokens.Bind(summary, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
            summary.Inlines!.Add(new Run(item.Actor.Name) { FontWeight = FontWeight.Medium });
            summary.Inlines.Add(new Run($" {item.Action} "));
            summary.Inlines.Add(new Run(item.Target) { FontWeight = FontWeight.Medium });

            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var glyph = DemoUi.Glyph(DemoActivityData.Icon(item.Category), 14);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            meta.Children.Add(glyph);
            meta.Children.Add(DemoUi.Text(DemoActivityData.Label(item.Category), ArcDesignTokenKind.TextMuted, 12));
            _metaIp = DemoUi.Text($"· {item.Actor.Ip}", ArcDesignTokenKind.TextMuted, 12);
            meta.Children.Add(_metaIp);
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { summary, meta } };

            _ip = DemoUi.Line(item.Actor.Ip, ArcDesignTokenKind.TextMuted, 12);
            _ip.VerticalAlignment = VerticalAlignment.Center;
            _time = DemoUi.Line(string.Empty, ArcDesignTokenKind.Foreground, 12);
            _time.VerticalAlignment = VerticalAlignment.Center;
            Tooltip.SetTip(_time, DemoActivityData.AbsoluteLabel(item));
            var chevron = DemoUi.Glyph(ArcIcons.ChevronDown, 16);
            chevron.VerticalAlignment = VerticalAlignment.Center;
            chevron.RenderTransform = _chevronTurn;

            _columns = Columns(narrow);
            _columns.Children.Add(At(avatar, 0));
            _columns.Children.Add(At(text, 1));
            _columns.Children.Add(At(_ip, 2));
            _columns.Children.Add(At(_time, 3));
            _columns.Children.Add(At(chevron, 4));

            ArcTokens.Bind(_hover, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            _trigger = new Border
            {
                MinHeight = 52,
                Padding = new Thickness(tight ? 12 : 16, 8),
                Background = Brushes.Transparent,
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = _columns
            };
            var triggerHost = new Panel { Children = { _hover, _trigger } };
            _trigger.PointerEntered += (_, _) => SetHover(true);
            _trigger.PointerExited += (_, _) => SetHover(false);
            _trigger.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                {
                    _trigger.Focus();
                    _owner.Toggle(this);
                }
            };
            _trigger.KeyDown += OnKeyDown;
            _trigger.GotFocus += (_, _) => UpdateFocusRing();
            _trigger.LostFocus += (_, _) => UpdateFocusRing();
            Avalonia.Automation.AutomationProperties.SetName(_trigger, $"{item.Actor.Name} {item.Action} {item.Target}");

            _panelPad = new Border { Padding = new Thickness(tight ? 12 : 16, 0, tight ? 12 : 16, 16), Child = BuildPanel(item) };
            _panel = new DemoReveal(_panelPad, 0);

            ArcTokens.Bind(_rule, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);

            var body = new StackPanel { Children = { triggerHost, _panel, _rule } };
            if (entering)
            {
                // A new live event slides in with an accent bar and a wash that fades about 2.2 s after it lands.
                var wash = new Border { BorderThickness = new Thickness(2, 0, 0, 0), IsHitTestVisible = false };
                ArcTokens.Bind(wash, Border.BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
                ArcTokens.Bind(wash, Border.BorderBrushProperty, ArcDesignTokenKind.Accent);
                var holder = new Panel { Children = { wash, body } };
                var reveal = new DemoReveal(holder, 0);
                Children.Add(reveal);
                reveal.Show(true, animate: true);
                var fade = new MotionValue<double>(wash, 1, MotionVectors.Double);
                fade.Changed += value => wash.Opacity = value;
                _ = fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(1400), ArcEasings.Standard, TimeSpan.FromMilliseconds(2200)));
            }
            else
            {
                Children.Add(body);
            }
            SetNarrow(narrow);
        }

        public AuditEvent Item { get; }

        public void FocusTrigger() => _trigger.Focus(NavigationMethod.Directional);

        public void SetAge(double seconds) => _time.Text = DemoActivityData.Relative(seconds);

        public void SetLast(bool last) => _rule.IsVisible = !last;

        public void SetNarrow(bool narrow)
        {
            _columns.ColumnDefinitions = narrow ? new ColumnDefinitions("28,*,0,Auto,16") : new ColumnDefinitions("28,*,120,88,16");
            _ip.IsVisible = !narrow;
            _metaIp.IsVisible = narrow;
            foreach (var child in _columns.Children)
            {
                child.VerticalAlignment = narrow ? VerticalAlignment.Top : VerticalAlignment.Center;
            }
        }

        public void SetTight(bool tight)
        {
            var side = tight ? 12 : 16;
            _trigger.Padding = new Thickness(side, 8);
            _panelPad.Padding = new Thickness(side, 0, side, 16);
        }

        public void SetOpen(bool open, bool animate)
        {
            if (_open == open && animate)
            {
                return;
            }
            _open = open;
            _panel.Show(open, animate);
            _chevronTurn.Angle = open ? 180 : 0;
            UpdateHover();
        }

        private void SetHover(bool over)
        {
            _pointerOver = over;
            UpdateHover();
        }

        // An open row keeps the muted wash at 55%; hover shows it at 40%.
        private void UpdateHover() => _hover.Opacity = _open ? 0.55 : _pointerOver ? 0.4 : 0;

        private void UpdateFocusRing()
        {
            var focused = _trigger.IsFocused && _trigger.IsKeyboardFocusWithin;
            _trigger.BorderThickness = focused ? new Thickness(2) : default;
            if (focused)
            {
                ArcTokens.Bind(_trigger, Border.BorderBrushProperty, ArcDesignTokenKind.FocusRing);
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                case Key.Space:
                    _owner.Toggle(this);
                    e.Handled = true;
                    break;
                case Key.Up:
                case Key.Down:
                case Key.Home:
                case Key.End:
                    _owner.MoveFocus(this, e.Key);
                    e.Handled = true;
                    break;
            }
        }

        private static Control BuildPanel(AuditEvent item)
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(item.Changes.Count == 0
                ? DemoUi.Text("No fields changed. This event records access only.", ArcDesignTokenKind.TextMuted, 12)
                : BuildDiff(item.Changes));
            panel.Children.Add(BuildFacts(item));
            return panel;
        }

        // Field | Before | After, the first column 32% wide; before is struck through on a danger tint, after on a success tint.
        private static Control BuildDiff(IReadOnlyList<AuditChange> changes)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32*,34*,34*"), ColumnSpacing = 12, RowSpacing = 0 };
            Avalonia.Automation.AutomationProperties.SetName(grid, "Changed fields");
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Children.Add(Cell(Caption("Field"), 0, 0));
            grid.Children.Add(Cell(Caption("Before"), 0, 1));
            grid.Children.Add(Cell(Caption("After"), 0, 2));
            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top };
                ArcTokens.Bind(rule, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
                Grid.SetRow(rule, i + 1);
                Grid.SetColumnSpan(rule, 3);
                grid.Children.Add(rule);
                grid.Children.Add(Cell(DemoUi.Text(change.Field, ArcDesignTokenKind.TextSecondary, 12), i + 1, 0));
                grid.Children.Add(Cell(Value(change.Before, after: false), i + 1, 1));
                grid.Children.Add(Cell(Value(change.After, after: true), i + 1, 2));
            }
            var frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 4), Child = grid };
            ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            return frame;
        }

        private static Control Cell(Control content, int row, int column)
        {
            var cell = new Border { Padding = new Thickness(0, 6), Child = content };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            return cell;
        }

        private static Control Value(string? value, bool after)
        {
            if (value is null)
            {
                return DemoUi.Text("Not set", ArcDesignTokenKind.TextMuted, 12);
            }
            var text = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextDecorations = after ? null : TextDecorations.Strikethrough
            };
            ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
            text.Inlines!.Add(new Run(after ? "+ " : "− "));
            text.Inlines.Add(new Run(value));
            var tint = new Border { CornerRadius = new CornerRadius(5), Opacity = after ? 0.11 : 0.09 };
            ArcTokens.Bind(tint, Border.BackgroundProperty, after ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Danger);
            return new Panel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Children = { tint, new Border { Padding = new Thickness(6, 1), Child = text } }
            };
        }

        // Time, Client and Event ID, in as many columns of at least 190px as fit.
        private static Control BuildFacts(AuditEvent item)
        {
            var id = DemoActivityData.EventId(item.Id);
            var idText = DemoUi.Text(id, ArcDesignTokenKind.Foreground, 12);
            ArcTokens.Bind(idText, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
            idText.VerticalAlignment = VerticalAlignment.Center;
            var copy = new CopyButton { Value = id, Label = "Copy event ID", IsIconOnly = true, Variant = CopyButtonVariant.Plain, VerticalAlignment = VerticalAlignment.Center };
            var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { idText, copy } };

            var facts = new DemoAutoFitPanel { MinColumnWidth = 190, Gap = 12, RowGap = 10 };
            facts.Children.Add(Fact("Time", DemoUi.Text(DemoActivityData.AbsoluteLabel(item), ArcDesignTokenKind.Foreground, 12)));
            facts.Children.Add(Fact("Client", DemoUi.Text($"{item.Actor.Client} · {item.Actor.Location}", ArcDesignTokenKind.Foreground, 12)));
            facts.Children.Add(Fact("Event ID", idRow));
            return facts;
        }

        private static Control Fact(string label, Control value) => new StackPanel
        {
            Spacing = 2,
            Children = { Caption(label), value }
        };
    }
}

/// <summary>
/// Shows its child at a share of its height (0 hidden, 1 whole) and springs between the two, so a panel opens and closes
/// by its height. Code-driven because the height to reach is the content's measured height, which a ControlTheme setter
/// cannot name.
/// </summary>
internal sealed class DemoReveal : Decorator
{
    private readonly MotionValue<double> _progress;

    public DemoReveal(Control child, double initial)
    {
        Child = child;
        ClipToBounds = true;
        _progress = new MotionValue<double>(this, initial, MotionVectors.Double);
        _progress.Changed += value =>
        {
            child.Opacity = Math.Clamp(value * 1.4 - 0.1, 0, 1);
            InvalidateMeasure();
        };
        child.Opacity = initial;
        IsVisible = initial > 0;
    }

    public void Show(bool open, bool animate)
    {
        IsVisible = true;
        if (!animate)
        {
            _progress.Jump(open ? 1 : 0);
            IsVisible = open;
            return;
        }
        _ = _progress.AnimateTo(open ? 1 : 0, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(_ =>
        {
            if (_progress.Value <= 0.001)
            {
                IsVisible = false;
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width, size.Height * Math.Clamp(_progress.Value, 0, 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// CSS grid's <c>repeat(auto-fit, minmax(MinColumnWidth, 1fr))</c>: as many equal columns of at least
/// <see cref="MinColumnWidth"/> as fit (never more than there are children), filled row by row.
/// </summary>
internal sealed class DemoAutoFitPanel : Panel
{
    public double MinColumnWidth { get; init; } = 190;

    public double Gap { get; init; } = 12;

    public double RowGap { get; init; } = 12;

    private (int Columns, double Width) Fit(double width)
    {
        var count = Math.Max(1, Children.Count);
        if (double.IsInfinity(width))
        {
            return (count, MinColumnWidth);
        }
        var columns = Math.Clamp((int)((width + Gap) / (MinColumnWidth + Gap)), 1, count);
        return (columns, Math.Max(0, (width - Gap * (columns - 1)) / columns));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, width) = Fit(availableSize.Width);
        double height = 0;
        double line = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(width, double.PositiveInfinity));
            line = Math.Max(line, Children[i].DesiredSize.Height);
            if (i % columns == columns - 1 || i == Children.Count - 1)
            {
                height += line + (height > 0 ? RowGap : 0);
                line = 0;
            }
        }
        return new Size(double.IsInfinity(availableSize.Width) ? columns * width + Gap * (columns - 1) : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, width) = Fit(finalSize.Width);
        double y = 0;
        for (var start = 0; start < Children.Count; start += columns)
        {
            var end = Math.Min(Children.Count, start + columns);
            double line = 0;
            for (var i = start; i < end; i++)
            {
                line = Math.Max(line, Children[i].DesiredSize.Height);
            }
            for (var i = start; i < end; i++)
            {
                Children[i].Arrange(new Rect((i - start) * (width + Gap), y, width, line));
            }
            y += line + RowGap;
        }
        return finalSize;
    }
}
