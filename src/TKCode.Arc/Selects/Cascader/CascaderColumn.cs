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
    private TranslateTransform? _revealShift;
    private bool _revealSlides;
    private byte[]? _branchFlags;
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
    // widths: label widths already shaped for this font (a width only depends on the text and the font), shared by the
    // panel's columns. Columns are recreated on every open and expand; shaping 500 labels each time is a visible hitch.
    public double MeasureContentWidth(double min, double max, double fontSize, Typeface typeface, Thickness rowPadding, Dictionary<string, double> widths)
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
            if (!widths.TryGetValue(text, out var width))
            {
                using var layout = new TextLayout(text, typeface, fontSize, null);
                width = layout.WidthIncludingTrailingWhitespace;
                widths[text] = width;
            }
            widest = Math.Max(widest, width);
        }
        _contentWidth = Math.Clamp(widest + rowPadding.Left + rowPadding.Right, min, Math.Max(min, max));
        return _contentWidth;
    }

    /// <summary>Redraws the rows after the path or the selection changed.</summary>
    public void Refresh()
    {
        // The owner's data may have changed along with the path; the flags are read again on the next draw.
        if (_branchFlags is not null)
        {
            Array.Clear(_branchFlags);
        }
        Options.InvalidateRows();
    }

    /// <summary>
    /// Fades the column in (and slides it 8px in from the left at full motion). Why code: the column is created
    /// while the menu is open, so a ControlTheme has no state change to hang a transition on.
    /// </summary>
    public void PlayReveal(bool slide)
    {
        if (_reveal is null)
        {
            _reveal = new MotionValue<double>(this, 0, MotionVectors.Double);
            _revealShift = new TranslateTransform();
            _reveal.Changed += value =>
            {
                Opacity = value;
                if (_revealSlides)
                {
                    _revealShift!.X = (value - 1) * 8;
                }
            };
        }
        _revealSlides = slide;
        RenderTransform = slide ? _revealShift : null;
        if (slide)
        {
            // Where the first Changed would have put it: nothing is drawn (opacity 0) until the delayed tween starts.
            _revealShift!.X = (_reveal.Value - 1) * 8;
        }
        Opacity = 0;
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(slide ? ArcMotionPresets.Standard : TimeSpan.FromMilliseconds(120),
            ArcEasings.Enter, slide ? TimeSpan.FromMilliseconds(40) : default));
    }

    bool ISelectOptionHost.IsEntrySelected(int entryIndex) =>
        entryIndex >= 0 && entryIndex < Nodes.Count && _owner.IsSelectedNode(Nodes[entryIndex]);

    // Rows ask on every draw (each frame of the gliding highlight): 0 = not read yet, 1 = branch, 2 = leaf.
    bool ISelectOptionBranchHost.IsBranch(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= Nodes.Count)
        {
            return false;
        }
        var flags = _branchFlags ??= new byte[Nodes.Count];
        if (flags[entryIndex] == 0)
        {
            flags[entryIndex] = _owner.IsBranchNode(Nodes[entryIndex]) ? (byte)1 : (byte)2;
        }
        return flags[entryIndex] == 1;
    }

    bool ISelectOptionBranchHost.IsOnPath(int entryIndex) =>
        entryIndex >= 0 && entryIndex < Nodes.Count && _owner.IsPathNode(Level, Nodes[entryIndex]);

    void ISelectOptionHost.OnOptionHovered(int position) => _owner.OnColumnHovered(this, position);

    void ISelectOptionHost.OnOptionInvoked(int position) => _owner.OnColumnInvoked(this, position);

    void ISelectOptionHost.OnOptionsPointerExited() => _owner.OnColumnPointerExited(this);
}
