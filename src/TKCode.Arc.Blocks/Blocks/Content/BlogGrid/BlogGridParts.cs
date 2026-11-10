// Ported from Arc's free blog-grid block (MIT, Copyright (c) 2026 Elia Kuratli): the cards and layout of blog-grid.tsx and .module.css.
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Sizes itself by width and an aspect ratio and clips its child to its rounded corners (Arc's <c>aspect-ratio</c> image frames).</summary>
internal sealed class BlogAspectFrame : Border
{
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<BlogAspectFrame, double>(nameof(Ratio), 1.5);

    static BlogAspectFrame()
    {
        AffectsMeasure<BlogAspectFrame>(RatioProperty);
    }

    /// <summary>Width over height.</summary>
    public double Ratio { get => GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        var size = new Size(width, width / Ratio);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>Row heights follow the tallest card of the row, unlike a uniform grid (Arc's CSS grid of cards).</summary>
internal sealed class BlogCardGrid : Panel
{
    private double _columnGap = 24;
    private double _rowGap = 48;
    private int _columns = 3;

    public int ColumnCount => _columns;

    public void SetLayout(int columns, double columnGap, double rowGap)
    {
        _columns = Math.Max(1, columns);
        _columnGap = columnGap;
        _rowGap = rowGap;
        InvalidateMeasure();
    }

    private double CellWidth(double width) => Math.Max(0, (width - _columnGap * (_columns - 1)) / _columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 960 : availableSize.Width;
        var cell = new Size(CellWidth(width), double.PositiveInfinity);
        var height = 0.0;
        var rowHeight = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(cell);
            rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            if (i % _columns == _columns - 1 || i == Children.Count - 1)
            {
                height += rowHeight + (i == Children.Count - 1 ? 0 : _rowGap);
                rowHeight = 0;
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cellWidth = CellWidth(finalSize.Width);
        var y = 0.0;
        for (var start = 0; start < Children.Count; start += _columns)
        {
            var rowHeight = 0.0;
            var end = Math.Min(start + _columns, Children.Count);
            for (var i = start; i < end; i++)
            {
                rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            }
            for (var i = start; i < end; i++)
            {
                Children[i].Arrange(new Rect((i - start) * (cellWidth + _columnGap), y, cellWidth, rowHeight));
            }
            y += rowHeight + _rowGap;
        }
        return finalSize;
    }
}

/// <summary>Holds the index and the reader. Only the one in front sizes it; the other keeps its place, so the index is still there for the cover to fly back to.</summary>
internal sealed class BlogStage : Panel
{
    private bool _showsSecond;

    public bool ShowsSecond
    {
        get => _showsSecond;
        set
        {
            _showsSecond = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(availableSize);
            if ((i == 1) == _showsSecond)
            {
                size = Children[i].DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// One post as a single focusable link: cover, meta line, title, excerpt and byline. The featured card lays cover and text side
/// by side (1.35 to 1) until the block is small.
/// </summary>
[PseudoClasses(FeaturedPseudoClass, StackedPseudoClass, MediumPseudoClass)]
internal sealed class BlogCard : BlockButton
{
    public const string FeaturedPseudoClass = ":featured";
    public const string StackedPseudoClass = ":stacked";
    public const string MediumPseudoClass = ":medium";

    private Grid? _layout;
    private Control? _body;
    private bool _stacked;
    private bool _medium;

    public BlogCard(BlogPost post, bool featured, Func<int, string> minutes, double featuredTitleSize)
    {
        Post = post;
        IsFeaturedCard = featured;
        MinutesText = minutes;
        FeaturedTitleSize = featuredTitleSize;
        PseudoClasses.Set(FeaturedPseudoClass, featured);
    }

    public BlogPost Post { get; }

    public bool IsFeaturedCard { get; }

    private Func<int, string> MinutesText { get; }

    private double FeaturedTitleSize { get; set; }

    /// <summary>The cover's frame, the piece that flies into the reader.</summary>
    public BlogAspectFrame? Frame { get; private set; }

    /// <summary>The body text, for tests.</summary>
    public string? TitleText => Post.Title;

    protected override ArcDesignTokenKind RingRadius => ArcDesignTokenKind.RadiusStep22;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<Grid>("PART_Layout");
        Frame = e.NameScope.Find<BlogAspectFrame>("PART_Frame");
        _body = e.NameScope.Find<Control>("PART_Body");
        var imageHost = e.NameScope.Find<Decorator>("PART_ImageHost");
        var post = Post;
        if (Frame is not null)
        {
            Frame.IsVisible = post.HasCover;
            SharedElement.SetKey(Frame, BlogGrid.CoverKey(post));
            if (imageHost is not null && post.HasCover)
            {
                imageHost.Child = BlogGrid.CreateCover(post);
            }
        }
        Set(e, "PART_Category", post.Category);
        Set(e, "PART_Date", BlogGrid.FormatDate(post.Date));
        Set(e, "PART_Title", post.Title);
        Set(e, "PART_Excerpt", post.Excerpt);
        Set(e, "PART_Author", post.Author.Name);
        Set(e, "PART_ReadTime", MinutesText(post.ReadMinutes));
        var avatar = e.NameScope.Find<Border>("PART_Avatar");
        if (avatar is not null && post.Author.Avatar is { } face)
        {
            avatar.Child = new Image { Source = face, Stretch = Stretch.UniformToFill };
        }
        if (IsFeaturedCard)
        {
            e.NameScope.Find<TextBlock>("PART_Title")?.SetValue(TextBlock.FontSizeProperty, FeaturedTitleSize);
        }
        AutomationProperties.SetName(this, $"{post.Title}. {post.Category}, {BlogGrid.FormatDate(post.Date)}. {post.Author.Name}.");
        ApplyLayout();
    }

    private static void Set(TemplateAppliedEventArgs e, string part, string? text)
    {
        if (e.NameScope.Find<TextBlock>(part) is { } block)
        {
            block.Text = text;
        }
    }

    /// <summary>Follows the block's width: at 900 and less the featured card closes its gap, at 680 and less it stacks.</summary>
    public void SetBreakpoints(bool medium, bool stacked, double featuredTitleSize)
    {
        FeaturedTitleSize = featuredTitleSize;
        _medium = medium;
        _stacked = stacked;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(MediumPseudoClass, medium);
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_layout is null || Frame is null || _body is null)
        {
            return;
        }
        var sideBySide = IsFeaturedCard && !_stacked && Post.HasCover;
        _layout.ColumnDefinitions = sideBySide ? new ColumnDefinitions("1.35*,*") : new ColumnDefinitions("*");
        _layout.RowDefinitions = sideBySide ? new RowDefinitions("Auto") : new RowDefinitions("Auto,Auto");
        Grid.SetRow(Frame, 0);
        Grid.SetColumn(Frame, 0);
        Grid.SetRow(_body, sideBySide ? 0 : 1);
        Grid.SetColumn(_body, sideBySide ? 1 : 0);
        _layout.ColumnSpacing = sideBySide ? (_medium ? 24 : 40) : 0;
        _layout.RowSpacing = Post.HasCover ? 16 : 0;
        _body.VerticalAlignment = sideBySide ? VerticalAlignment.Center : VerticalAlignment.Top;
        Frame.Ratio = IsFeaturedCard && !_stacked ? 1.6 : 1.5;
        if (IsFeaturedCard && _layout.FindNameScope()?.Find<TextBlock>("PART_Title") is { } title)
        {
            title.FontSize = FeaturedTitleSize;
        }
    }
}

/// <summary>Previous or Next: a chevron and a label that goes away when the block is tiny.</summary>
internal sealed class BlogPageStepButton : BlockButton
{
    public BlogPageStepButton(string label, Geometry icon, bool iconFirst)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var glyph = new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center };
        Label = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(iconFirst ? glyph : Label);
        row.Children.Add(iconFirst ? Label : glyph);
        Content = row;
    }

    public TextBlock Label { get; }
}

/// <summary>The reader's way back: an arrow and "All posts".</summary>
internal sealed class BlogBackButton : BlockButton
{
}
