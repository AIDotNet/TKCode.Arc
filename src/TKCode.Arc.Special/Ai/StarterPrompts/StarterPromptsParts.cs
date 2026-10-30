using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The outer surface of a <see cref="StarterPrompts"/>. It reports its width to the control, which steps the layout down
/// (Shuffle as an icon, smaller padding and radius) by width alone, so the steps never depend on content.
/// </summary>
internal sealed class StarterPromptsSurface : Border
{
    /// <summary>Under this width Shuffle shows only its icon.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<StarterPromptsSurface, double>(nameof(CompactWidth), 440);

    /// <summary>Under this width the padding and radius step down.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<StarterPromptsSurface, double>(nameof(NarrowWidth), 480);

    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        (TemplatedParent as StarterPrompts)?.OnSurfaceWidth(e.NewSize.Width, CompactWidth, NarrowWidth);
    }
}

/// <summary>
/// The grid of starter cards: two columns from <see cref="TwoColumnMinWidth"/> on, one below it. Every row is as tall as
/// the tallest card, so a swap never moves the cards, and the slots are fixed, so the grid never changes height when a tab
/// holds fewer starters than slots.
/// </summary>
internal sealed class StarterPromptsGrid : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<StarterPromptsGrid, double>(nameof(Gap), 10);

    public static readonly StyledProperty<double> TwoColumnMinWidthProperty =
        AvaloniaProperty.Register<StarterPromptsGrid, double>(nameof(TwoColumnMinWidth), 560);

    static StarterPromptsGrid()
    {
        AffectsMeasure<StarterPromptsGrid>(GapProperty, TwoColumnMinWidthProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double TwoColumnMinWidth { get => GetValue(TwoColumnMinWidthProperty); set => SetValue(TwoColumnMinWidthProperty, value); }

    /// <summary>The number of columns at the last measure (tests).</summary>
    internal int Columns { get; private set; } = 1;

    internal int ColumnsFor(double width) =>
        Children.Count > 1 && (double.IsInfinity(width) || width >= TwoColumnMinWidth) ? 2 : 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = ColumnsFor(availableSize.Width);
        Columns = columns;
        var gap = Gap;
        var columnWidth = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, (availableSize.Width - gap * (columns - 1)) / columns);
        double widest = 0, tallest = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            widest = Math.Max(widest, child.DesiredSize.Width);
            tallest = Math.Max(tallest, child.DesiredSize.Height);
        }
        var rows = (Children.Count + columns - 1) / columns;
        var height = rows == 0 ? 0 : rows * tallest + gap * (rows - 1);
        var width = double.IsInfinity(availableSize.Width) ? widest * columns + gap * (columns - 1) : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width);
        Columns = columns;
        var gap = Gap;
        var columnWidth = Math.Max(0, (finalSize.Width - gap * (columns - 1)) / columns);
        double tallest = 0;
        foreach (var child in Children)
        {
            tallest = Math.Max(tallest, child.DesiredSize.Height);
        }
        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            Children[i].Arrange(new Rect(column * (columnWidth + gap), row * (tallest + gap), columnWidth, tallest));
        }
        return finalSize;
    }
}

/// <summary>
/// One starter card: a fixed frame (border, hover fill, focus ring) whose content (the mark, the app's name, the title and
/// the description) crossfades in place when the card shows another starter. While the starters load it shows placeholder
/// bars of the same size; a slot with no starter keeps its space and is hidden.
/// </summary>
[TemplatePart(ContentPart, typeof(Control))]
[PseudoClasses(LoadingPseudoClass, EmptyPseudoClass, AppPseudoClass, LetterPseudoClass)]
internal sealed class StarterPromptCard : AvaloniaButton
{
    internal const string ContentPart = "PART_Content";
    internal const string LoadingPseudoClass = ":loading";
    internal const string EmptyPseudoClass = ":empty";
    internal const string AppPseudoClass = ":app";
    internal const string LetterPseudoClass = ":letter";

    /// <summary>The fade out of the old content (Arc: 100ms).</summary>
    internal static readonly TimeSpan OutDuration = TimeSpan.FromMilliseconds(100);

    /// <summary>The fade in of the new content (Arc: 160ms) and how far it rises (2px).</summary>
    internal static readonly TimeSpan InDuration = TimeSpan.FromMilliseconds(160);

    internal const double Rise = 2;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StarterPromptCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<StarterPromptCard, string?>(nameof(Description));

    public static readonly StyledProperty<string?> AppNameProperty =
        AvaloniaProperty.Register<StarterPromptCard, string?>(nameof(AppName));

    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<StarterPromptCard, Geometry?>(nameof(Mark));

    public static readonly StyledProperty<string?> MarkLetterProperty =
        AvaloniaProperty.Register<StarterPromptCard, string?>(nameof(MarkLetter));

    public static readonly StyledProperty<bool> HasDescriptionProperty =
        AvaloniaProperty.Register<StarterPromptCard, bool>(nameof(HasDescription));

    private readonly TranslateTransform _rise = new();
    private Control? _content;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _offset;
    private int _swap;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public string? AppName { get => GetValue(AppNameProperty); set => SetValue(AppNameProperty, value); }

    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public string? MarkLetter { get => GetValue(MarkLetterProperty); set => SetValue(MarkLetterProperty, value); }

    public bool HasDescription { get => GetValue(HasDescriptionProperty); set => SetValue(HasDescriptionProperty, value); }

    protected override Type StyleKeyOverride => typeof(StarterPromptCard);

    /// <summary>The starter the card shows once its swap has turned over, or null for an empty slot.</summary>
    internal StarterPrompt? Prompt { get; private set; }

    /// <summary>The starter the card is moving to (the one a swap in flight will show).</summary>
    internal StarterPrompt? Target { get; private set; }

    /// <summary>Whether a swap is running (tests).</summary>
    internal bool IsSwapping => _fade?.IsAnimating == true || _offset?.IsAnimating == true;

    /// <summary>The content's opacity (tests).</summary>
    internal double ContentOpacity => _content?.Opacity ?? 1;

    /// <summary>The content's rise (tests).</summary>
    internal double ContentOffset => _rise.Y;

    private StarterPromptCategory? _category;
    private StarterPromptsLabels _labels = StarterPromptsLabels.Default;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _content = e.NameScope.Find<Control>(ContentPart);
        if (_content is not null)
        {
            _content.RenderTransform = _rise;
            _content.Opacity = _fade?.Value ?? 1;
        }
    }

    /// <summary>Shows the placeholder bars (or the content again).</summary>
    internal void SetLoading(bool loading)
    {
        PseudoClasses.Set(LoadingPseudoClass, loading);
        UpdateInteractive();
    }

    /// <summary>
    /// Moves the card to <paramref name="prompt"/>. Animated (full motion only): after <paramref name="delay"/> the old
    /// content fades out over 100ms, then the new one fades in over 160ms rising 2px on the enter curve. Code-driven because
    /// each card's swap starts on its own stagger delay and the content changes between the two halves, which a
    /// ControlTheme transition cannot sequence. Reduced motion and Off: the new content shows at once.
    /// </summary>
    internal void SwapTo(StarterPrompt? prompt, StarterPromptCategory? category, StarterPromptsLabels labels, TimeSpan delay, bool animate)
    {
        var version = ++_swap;
        Target = prompt;
        var same = ReferenceEquals(prompt, Prompt) && ReferenceEquals(category, _category) && ReferenceEquals(labels, _labels);
        if (same && !IsSwapping)
        {
            return;
        }
        if (!animate || _content is null || !IsEffectivelyVisible || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _fade?.Jump(1);
            _offset?.Jump(0);
            if (_content is not null)
            {
                _content.Opacity = 1;
            }
            _rise.Y = 0;
            Apply(prompt, category, labels);
            return;
        }
        _ = Run(version, prompt, category, labels, delay);
    }

    private async Task Run(int version, StarterPrompt? prompt, StarterPromptCategory? category, StarterPromptsLabels labels, TimeSpan delay)
    {
        var fade = _fade ??= CreateFade();
        var offset = _offset ??= CreateOffset();
        var wait = delay;
        if (Prompt is not null && fade.Value > 0.001)
        {
            if (!await fade.AnimateTo(0, MotionSpec.Tween(OutDuration, ArcEasings.Standard, delay)) || version != _swap)
            {
                return;
            }
            wait = TimeSpan.Zero;
        }
        else
        {
            fade.Jump(0);
        }
        Apply(prompt, category, labels);
        offset.Jump(Rise);
        _ = offset.AnimateTo(0, MotionSpec.Tween(InDuration, ArcEasings.Enter, wait));
        await fade.AnimateTo(1, MotionSpec.Tween(InDuration, ArcEasings.Enter, wait));
    }

    private MotionValue<double> CreateFade()
    {
        var value = new MotionValue<double>(this, _content?.Opacity ?? 1, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_content is not null)
            {
                _content.Opacity = v;
            }
        };
        return value;
    }

    private MotionValue<double> CreateOffset()
    {
        var value = new MotionValue<double>(this, _rise.Y, MotionVectors.Double);
        value.Changed += v => _rise.Y = v;
        return value;
    }

    private void Apply(StarterPrompt? prompt, StarterPromptCategory? category, StarterPromptsLabels labels)
    {
        Prompt = prompt;
        _category = category;
        _labels = labels;
        PseudoClasses.Set(EmptyPseudoClass, prompt is null);
        var app = prompt?.App;
        Title = prompt?.Title;
        Description = prompt?.Description;
        HasDescription = !string.IsNullOrEmpty(prompt?.Description);
        AppName = app?.Name;
        PseudoClasses.Set(AppPseudoClass, app is not null);
        var mark = app is not null ? app.Mark : category?.Icon ?? ArcIcons.Sparkles;
        Mark = mark;
        var letter = mark is null && !string.IsNullOrEmpty(app?.Name) ? app!.Name.Substring(0, 1).ToUpperInvariant() : null;
        MarkLetter = letter;
        PseudoClasses.Set(LetterPseudoClass, letter is not null);
        Avalonia.Automation.AutomationProperties.SetName(this,
            prompt is null ? null : labels.CardName(prompt.Title, app?.Name, category?.Label));
        UpdateInteractive();
    }

    private void UpdateInteractive()
    {
        var live = Prompt is not null && !PseudoClasses.Contains(LoadingPseudoClass);
        Focusable = live;
        IsTabStop = live;
        IsHitTestVisible = live;
    }
}

/// <summary>
/// The composer's text box: Enter sends and Shift+Enter adds a line (Arc's chat composer keys). A bare text area with no
/// field frame; the composer's border is the frame.
/// </summary>
internal sealed class StarterPromptsInput : AvaloniaTextBox
{
    /// <summary>Raised when Enter is pressed without Shift.</summary>
    internal event EventHandler? SendRequested;

    protected override Type StyleKeyOverride => typeof(StarterPromptsInput);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Shift) == 0 && (e.KeyModifiers & KeyModifiers.Alt) == 0)
        {
            e.Handled = true;
            SendRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>The round send button: an up arrow that crossfades to a stop square while a message is in flight.</summary>
[PseudoClasses(SendingPseudoClass)]
internal sealed class StarterPromptsSendButton : AvaloniaButton
{
    internal const string SendingPseudoClass = ":sending";

    public static readonly StyledProperty<bool> IsSendingProperty =
        AvaloniaProperty.Register<StarterPromptsSendButton, bool>(nameof(IsSending));

    public bool IsSending { get => GetValue(IsSendingProperty); set => SetValue(IsSendingProperty, value); }

    protected override Type StyleKeyOverride => typeof(StarterPromptsSendButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSendingProperty)
        {
            PseudoClasses.Set(SendingPseudoClass, IsSending);
        }
    }
}

/// <summary>Lucide glyphs the starters need that Core <see cref="ArcIcons"/> does not ship (24×24, stroked).</summary>
internal static class StarterPromptsIcons
{
    // Lucide shuffle, ISC (each shape starts with an absolute move).
    private const string ShuffleData =
        "M18 14l4 4-4 4 M18 2l4 4-4 4 M2 18h1.973a4 4 0 0 0 3.3-1.7l5.454-7.6a4 4 0 0 1 3.3-1.7H22 " +
        "M2 6h1.972a4 4 0 0 1 3.256 1.677 M22 18h-6.041a4 4 0 0 1-3.3-1.8l-.359-.45";

    /// <summary>Two crossing arrows: shuffle.</summary>
    public static Geometry Shuffle => ArcIcons.Parse(ShuffleData);
}

/// <summary>A tab's header: the category's icon (if any) and its label.</summary>
internal static class StarterPromptsTabHeader
{
    public static Control Create(string label, Geometry? icon, double iconSize, double gap)
    {
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        if (icon is null)
        {
            return text;
        }
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
        panel.Children.Add(new ArcGlyph { Data = icon, Size = iconSize, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(text);
        return panel;
    }
}
