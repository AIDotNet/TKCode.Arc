using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What a <see cref="CascaderColumn"/> asks of its <see cref="Cascader"/>.</summary>
internal interface ICascaderColumnOwner
{
    bool IsBranchNode(object node);

    bool IsPathNode(int level, object node);

    bool IsSelectedNode(object node);

    void OnColumnHovered(CascaderColumn column, int position);

    void OnColumnInvoked(CascaderColumn column, int position);

    void OnColumnPointerExited(CascaderColumn column);
}

/// <summary>
/// One column of the cascader menu: the nodes of one level in a scrolling <see cref="SelectOptionView"/>. The rows
/// are the same ones the select menus use; branch rows end in a chevron and the rows on the open path carry a fill.
/// The nodes are projected into option entries through an unattached <see cref="ItemsControl"/>, so a column of
/// thousands of nodes still costs a few text layouts.
/// </summary>
internal sealed class CascaderColumn : Decorator, ISelectOptionHost, ISelectOptionBranchHost
{
    private readonly ICascaderColumnOwner _owner;
    private readonly ItemsControl _source = new();
    private readonly List<int> _all = new();
    private readonly SelectEntrySource _entries;
    private MotionValue<double>? _reveal;
    private double _contentWidth = -1;

    public CascaderColumn(ICascaderColumnOwner owner, int level, object? parent, IReadOnlyList<object> nodes,
        Func<object?, string?> text, Func<object?, bool>? disabled)
    {
        _owner = owner;
        Level = level;
        ParentNode = parent;
        Nodes = nodes;
        _source.ItemsSource = nodes;
        _entries = new SelectEntrySource(_source, () => text, () => disabled);
        Options = new SelectOptionView
        {
            Host = this,
            CheckMode = SelectCheckMode.Static,
            GlideHighlight = true,
        };
        Scroll = new ScrollViewer
        {
            Padding = new Thickness(0, 2),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            Content = Options,
        };
        Child = Scroll;
        for (var i = 0; i < nodes.Count; i++)
        {
            _all.Add(i);
        }
        Options.SetOptions(_all);
    }

    /// <summary>Depth of this column, 0 for the roots.</summary>
    public int Level { get; }

    /// <summary>The node whose children this column shows; null for the root column.</summary>
    public object? ParentNode { get; }

    public IReadOnlyList<object> Nodes { get; }

    public SelectOptionView Options { get; }

    public ScrollViewer Scroll { get; }

    public int Count => Nodes.Count;

    SelectEntrySource ISelectOptionHost.Entries => _entries;

    public SelectEntrySource Entries => _entries;

    /// <summary>Index of <paramref name="node"/> in this column, or -1.</summary>
    public int IndexOf(object node)
    {
        for (var i = 0; i < Nodes.Count; i++)
        {
            if (ReferenceEquals(Nodes[i], node) || Equals(Nodes[i], node))
            {
                return i;
            }
        }
        return -1;
    }

    public bool IsEnabledAt(int position) => _entries.IsEnabledAt(position);

    /// <summary>The widest label plus the row padding, clamped to the column limits. Measured once per column.</summary>
    public double MeasureContentWidth(double min, double max, double fontSize, Typeface typeface, Thickness rowPadding)
    {
        if (_contentWidth >= 0)
        {
            return _contentWidth;
        }
        var widest = 0.0;
        var limit = Math.Min(Nodes.Count, 500);
        for (var i = 0; i < limit; i++)
        {
            var text = _entries.TextAt(i);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }
            var layout = new TextLayout(text, typeface, fontSize, null);
            widest = Math.Max(widest, layout.WidthIncludingTrailingWhitespace);
        }
        _contentWidth = Math.Clamp(widest + rowPadding.Left + rowPadding.Right, min, Math.Max(min, max));
        return _contentWidth;
    }

    /// <summary>Redraws the rows after the path or the selection changed.</summary>
    public void Refresh() => Options.InvalidateRows();

    /// <summary>
    /// Fades the column in (and slides it 8px in from the left at full motion). Why code: the column is created
    /// while the menu is open, so a ControlTheme has no state change to hang a transition on.
    /// </summary>
    public void PlayReveal(bool slide)
    {
        _reveal ??= new MotionValue<double>(this, 0, MotionVectors.Double);
        _reveal.Changed += value =>
        {
            Opacity = value;
            RenderTransform = slide ? new TranslateTransform((value - 1) * 8, 0) : null;
        };
        Opacity = 0;
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(slide ? ArcMotionPresets.Standard : TimeSpan.FromMilliseconds(120),
            ArcEasings.Enter, slide ? TimeSpan.FromMilliseconds(40) : default));
    }

    bool ISelectOptionHost.IsEntrySelected(int entryIndex) =>
        entryIndex >= 0 && entryIndex < Nodes.Count && _owner.IsSelectedNode(Nodes[entryIndex]);

    bool ISelectOptionBranchHost.IsBranch(int entryIndex) =>
        entryIndex >= 0 && entryIndex < Nodes.Count && _owner.IsBranchNode(Nodes[entryIndex]);

    bool ISelectOptionBranchHost.IsOnPath(int entryIndex) =>
        entryIndex >= 0 && entryIndex < Nodes.Count && _owner.IsPathNode(Level, Nodes[entryIndex]);

    void ISelectOptionHost.OnOptionHovered(int position) => _owner.OnColumnHovered(this, position);

    void ISelectOptionHost.OnOptionInvoked(int position) => _owner.OnColumnInvoked(this, position);

    void ISelectOptionHost.OnOptionsPointerExited() => _owner.OnColumnPointerExited(this);
}
