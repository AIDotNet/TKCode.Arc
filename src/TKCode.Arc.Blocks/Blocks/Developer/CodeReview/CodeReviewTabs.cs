using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One file tab: a file icon (a check once the file is viewed), the file name and, while it has open conversations, a
/// message glyph with their count that fades out and narrows to nothing when the last one is resolved. 32 tall, radius
/// 12; the name is the foreground when selected or hovered, the secondary text otherwise and muted once viewed.
/// </summary>
internal sealed class CodeReviewTab : CodeReviewPressable
{
    private readonly ArcContentSwap _icon;
    private readonly TextBlock _name;
    private readonly TextBlock _count;
    private readonly PresenceHost _countHost;
    private readonly Geometry _fileIcon;
    private bool _selected;
    private bool _viewed;
    private bool _over;

    public CodeReviewTab(CodeReviewFile file)
    {
        File = file;
        HoverFill = null;
        Height = 32;
        Padding = new Thickness(9, 0);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.TabItem);
        // Arc's tab carries the full path as its title.
        Tooltip.SetTip(this, file.Path);
        _fileIcon = file.Language.Equals("md", StringComparison.OrdinalIgnoreCase) || file.Language.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            ? ArcIcons.FileText
            : ArcIcons.FileCode2;
        _icon = new ArcContentSwap
        {
            Mode = ContentSwapMode.Pop,
            Width = 14,
            Height = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Content = CodeReviewUi.Glyph(_fileIcon, 14, ArcDesignTokenKind.TextMuted)
        };
        var slash = file.Path.LastIndexOf('/');
        _name = CodeReviewUi.Text(slash >= 0 ? file.Path[(slash + 1)..] : file.Path, 13, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 18.2);
        _count = CodeReviewUi.Text("0", 12, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        _count.FontFeatures = CodeReviewUi.TabularNumbers;
        var countRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Margin = new Thickness(2, 0, 0, 0) };
        countRow.Children.Add(CodeReviewUi.Glyph(ArcIcons.MessageSquare, 12, ArcDesignTokenKind.TextMuted));
        countRow.Children.Add(_count);
        _countHost = new PresenceHost
        {
            Mode = PresenceMode.Fade,
            Collapse = PresenceCollapse.Width,
            Spring = ArcSpring.Settle,
            IsPresent = false,
            VerticalAlignment = VerticalAlignment.Center,
            Child = countRow
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_icon);
        row.Children.Add(_name);
        row.Children.Add(_countHost);
        Child = row;
    }

    public CodeReviewFile File { get; }

    /// <summary>How many open conversations the tab shows (the count stays mounted at 0 so it can fade).</summary>
    public int OpenCount { get; private set; }

    public void Set(bool selected, bool viewed, int open, CodeReviewLabels labels)
    {
        if (viewed != _viewed)
        {
            _icon.Content = viewed
                ? CodeReviewUi.Glyph(ArcIcons.Check, 14, ArcDesignTokenKind.TextMuted, 2.25)
                : CodeReviewUi.Glyph(_fileIcon, 14, ArcDesignTokenKind.TextMuted);
        }
        _selected = selected;
        _viewed = viewed;
        if (open > 0)
        {
            _count.Text = open.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }
        OpenCount = open;
        _countHost.IsPresent = open > 0;
        UpdateName();
        IsTabStop = selected;
        AutomationProperties.SetName(this, labels.TabName(File.Path, viewed, open));
        AutomationProperties.SetItemStatus(this, selected ? "selected" : string.Empty);
    }

    private void UpdateName() => ArcTokens.Bind(_name, TextBlock.ForegroundProperty,
        _selected || _over ? ArcDesignTokenKind.Foreground : _viewed ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.TextSecondary);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _over = e.Pointer.Type != PointerType.Touch;
        UpdateName();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _over = false;
        UpdateName();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    private sealed class TabPeer(CodeReviewTab owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        public bool IsSelected => owner._selected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select() => owner.PerformClick();

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    }

}

/// <summary>
/// The file tabs (a tab list with roving focus: Left / Right / Home / End select, as Arc's tabs do) over a highlight that
/// slides between them on the block's spring; the row scrolls sideways when the tabs do not fit and keeps the selected
/// tab in view.
/// </summary>
internal sealed class CodeReviewTabStrip : ScrollViewer
{
    private readonly GlidingIndicator _highlight;
    private readonly StackPanel _row;
    private readonly List<CodeReviewTab> _tabs = new();
    private int _selected = -1;

    public CodeReviewTabStrip()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        // Arc's tablist has 4px of padding and -4px of margin, so focus rings are not clipped.
        Padding = new Thickness(4);
        Margin = new Thickness(-4);
        _highlight = new GlidingIndicator { IsHitTestVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(_highlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.SurfaceMuted);
        _row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var layer = new Panel();
        layer.Children.Add(_highlight);
        layer.Children.Add(_row);
        Content = layer;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Tab);
        _row.LayoutUpdated += (_, _) => PlaceHighlight(animate: false, onlyIfMoved: true);
    }

    /// <summary>Raised when a tab is chosen by click or key, with its index.</summary>
    public event Action<int>? Chosen;

    public IReadOnlyList<CodeReviewTab> Tabs => _tabs;

    public void SetFiles(IReadOnlyList<CodeReviewFile> files)
    {
        _row.Children.Clear();
        _tabs.Clear();
        for (var i = 0; i < files.Count; i++)
        {
            var tab = new CodeReviewTab(files[i]);
            var index = i;
            tab.Click += () => Chosen?.Invoke(index);
            tab.KeyDown += OnTabKey;
            _tabs.Add(tab);
            _row.Children.Add(tab);
        }
        _selected = -1;
    }

    /// <summary>Marks the selected tab and slides the highlight to it.</summary>
    public void Select(int index, bool animate)
    {
        _selected = index;
        PlaceHighlight(animate, onlyIfMoved: false);
        if (index >= 0 && index < _tabs.Count)
        {
            var tab = _tabs[index];
            Dispatcher.UIThread.Post(() => tab.BringIntoView(), DispatcherPriority.Background);
        }
    }

    private Rect _placed;

    private void PlaceHighlight(bool animate, bool onlyIfMoved)
    {
        if (_selected < 0 || _selected >= _tabs.Count)
        {
            return;
        }
        var rect = _tabs[_selected].Bounds;
        if (rect.Width <= 0 || onlyIfMoved && rect == _placed)
        {
            return;
        }
        _placed = rect;
        // A tab that changes width (its count fading out) re-aims the running glide; the spring keeps its velocity.
        _highlight.MoveTo(rect, CodeReviewMotion.Spring, (animate || onlyIfMoved) && _highlight.HasTarget && IsLoaded);
    }

    private void OnTabKey(object? sender, KeyEventArgs e)
    {
        if (sender is not CodeReviewTab tab || _tabs.Count == 0)
        {
            return;
        }
        var index = _tabs.IndexOf(tab);
        var next = e.Key switch
        {
            Key.Left => Math.Max(0, index - 1),
            Key.Right => Math.Min(_tabs.Count - 1, index + 1),
            Key.Home => 0,
            Key.End => _tabs.Count - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        Chosen?.Invoke(next);
        _tabs[next].Focus(NavigationMethod.Directional);
    }
}

/// <summary>
/// "0/5 viewed" with a rolling count and a 4px track whose fill springs to the viewed share. Reads as one group with a
/// name that also gives the additions and deletions.
/// </summary>
internal sealed class CodeReviewProgress : DockPanel
{
    private readonly RollingDigits _count;
    private readonly TextBlock _of;
    private readonly Border _track;
    private readonly Border _fill;
    private readonly ScaleTransform _scale = new(0, 1);
    private MotionValue<double>? _progress;
    private double _value;

    public CodeReviewProgress()
    {
        VerticalAlignment = VerticalAlignment.Center;
        _count = new RollingDigits { FontSize = 12, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_count, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_count, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        _of = CodeReviewUi.Text(string.Empty, 12, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8);
        _of.Margin = new Thickness(1, 0, 0, 0);
        _of.FontFeatures = CodeReviewUi.TabularNumbers;
        var text = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_count);
        text.Children.Add(_of);
        DockPanel.SetDock(text, Dock.Left);

        _fill = new Border { RenderTransform = _scale, RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative) }
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(_fill, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
        _track = new Border
        {
            Height = 4,
            Width = 56,
            Margin = new Thickness(8, 0, 0, 0),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = _fill
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(_track, Border.BackgroundProperty, ArcDesignTokenKind.ControlTrack);
        Children.Add(text);
        Children.Add(_track);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
    }

    /// <summary>The compact layout: the track takes up to 160 at the right end of the row.</summary>
    public bool IsCompact
    {
        set
        {
            _track.Width = CodeReviewUi.Number(this, value ? CodeReviewTokenKind.CompactProgressWidth : CodeReviewTokenKind.ProgressWidth,
                value ? 160 : 56);
            _track.HorizontalAlignment = HorizontalAlignment.Right;
            _track.Margin = new Thickness(8, 0, 0, 0);
        }
    }

    /// <summary>The track's fill share (tests).</summary>
    internal double FillScale => _scale.ScaleX;

    internal int Count => (int)_count.Value;

    public void Set(int viewed, int total, int added, int deleted, CodeReviewLabels labels, bool animate)
    {
        _of.Text = labels.ViewedOf(total);
        _count.IsInstant = !animate;
        _count.Value = viewed;
        AutomationProperties.SetName(this, labels.ProgressName(viewed, total, added, deleted));
        var target = total == 0 ? 0 : (double)viewed / total;
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _progress?.Jump(target);
            _value = target;
            _scale.ScaleX = target;
            return;
        }
        if (_progress is null)
        {
            _progress = new MotionValue<double>(this, _value, MotionVectors.Double);
            _progress.Changed += v =>
            {
                _value = v;
                _scale.ScaleX = Math.Max(0, v);
            };
        }
        _ = _progress.AnimateTo(target, MotionSpec.Spring(CodeReviewMotion.Spring));
    }
}
