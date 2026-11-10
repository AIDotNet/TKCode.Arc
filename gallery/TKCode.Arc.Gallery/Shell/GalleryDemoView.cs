using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Plugins;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Model;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The StreamMark plugin behind docs pages: a <c>&lt;demo name="Component.Name"&gt;&lt;/demo&gt;</c> block (what
/// <see cref="GalleryDocs"/> turns <c>&lt;Demo name="..." /&gt;</c> into) renders as a <see cref="GalleryDemoView"/>.
/// </summary>
public sealed class GalleryDemoPlugin : IMarkdownPlugin
{
    public const string Tag = "demo";

    /// <summary>Arc's Markdown services (highlighting, code block wording) with live demos and screenshots.</summary>
    public static MarkdownServices Services { get; } = new()
    {
        Plugins = [new MarkdownHighlightPlugin(), new GalleryDemoPlugin(), new GalleryShotPlugin()],
        CodeBlocks = Markdown.DefaultServices.CodeBlocks
    };

    public string Name => "arc-gallery-demo";

    public void Configure(MarkdownPluginBuilder builder) => builder.AddBlockComponent(Tag, static _ => new GalleryDemoBlock());
}

/// <summary>The block view StreamMark lays out in the document; it hosts the demo for the block's <c>name</c>.</summary>
internal sealed class GalleryDemoBlock : BlockView
{
    private GalleryDemoView? _view;

    public GalleryDemoBlock()
    {
        // The document selects text on a press it does not see handled and opens its Copy / Find menu on a right click;
        // inside a demo, presses and menus belong to the live controls.
        AddHandler(PointerPressedEvent, (_, e) => e.Handled = true);
        AddHandler(ContextRequestedEvent, (_, e) => e.Handled = true);
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment,
                                      bool contextChanged)
    {
        var attributes = ((ComponentBlock)block).Attributes;
        var id = attributes["name"] ?? string.Empty;
        var hero = string.Equals(attributes["hero"], "true", StringComparison.OrdinalIgnoreCase);
        if (_view is not null && _view.DemoId == id && _view.IsHero == hero)
        {
            return;
        }
        if (_view is not null)
        {
            RemoveChild(_view);
        }
        _view = new GalleryDemoView(id, hero);
        AddChild(_view);
    }
}

/// <summary>
/// A live example in a docs page, as on uiarc.dev: Preview and Code tabs over a stage with the running view, or over the
/// demo's files (its .axaml and code-behind). Switching keeps the view and its state. The page's Preview also has a
/// Fit / Mobile size switch (Mobile moves the same view onto a phone's screen, <see cref="GalleryPreviewPhone"/>) and a
/// Fullscreen button (<see cref="GalleryPreviewFullscreen"/>).
/// </summary>
public sealed class GalleryDemoView : StackPanel
{
    private const double HeroHeight = 420;
    // The room a demo that is not built yet keeps on its stage (plus the stage's padding), so the page does not shrink to
    // a column of empty frames before the visitor scrolls.
    private const double PendingHeight = 140;

    private readonly GalleryDemo? _demo;
    private readonly Tabs _tabs;
    private readonly TextBlock _live = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 13);
    private readonly Panel _body = new();
    private readonly Border _stage;
    private readonly Border _content;
    // The stage's child: the Fit content, the phone while it shows (or still fades away) and a fading snapshot.
    private readonly Grid _layers = new();
    private MotionValue<double>? _viewOpacity;
    private Control? _view;
    // Null until the demo is built (Realize).
    private Control? _movable;
    private readonly GallerySegmented? _size;
    private readonly Button? _expand;
    private GalleryPreviewPhone? _phone;
    private GalleryPreviewFullscreen? _fullscreen;
    private Size _fitSize;
    private int _sizeGeneration;
    private StackPanel? _code;

    public GalleryDemoView(string id, bool hero = false)
    {
        DemoId = id;
        IsHero = hero;
        _demo = GalleryDemos.Get(id);
        Spacing = 12;
        Margin = new Thickness(0, 8);

        _tabs = GalleryUi.TabStrip(GalleryUi.Tab(GalleryShellLangResourceKind.Preview, ArcIcons.Eye),
                                   GalleryUi.Tab(GalleryShellLangResourceKind.Code, GalleryIcons.Code));
        _tabs.SelectionChanged += (_, _) => ShowCode(_tabs.SelectedIndex == 1);
        GalleryText.Bind(_live, TextBlock.TextProperty, GalleryShellLangResourceKind.LivePreview);
        _live.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_live, 2);
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
        bar.Children.Add(_tabs);
        bar.Children.Add(_live);

        // The content keeps its own width rules (rows center, the rest stretches) and sits in the middle of a tall stage.
        _content = new Border { VerticalAlignment = VerticalAlignment.Center };
        _layers.Children.Add(_content);
        _stage = GalleryUi.Stage(_layers);

        // The page's Preview can show the demo on a phone (the TKCode.Arc.Mobile demos always run on one already) and
        // fill the window.
        if (hero && _demo is not null && !IsMobileDemo(_demo))
        {
            _size = GalleryPreviewTools.SizeSwitch();
            _size.Margin = new Thickness(12, 0, 0, 0);
            _size.SelectionChanged += index => ShowMobile(index == 1);
            Grid.SetColumn(_size, 3);
            bar.Children.Add(_size);
        }
        if (hero && _demo is not null)
        {
            _expand = GalleryPreviewTools.IconButton(ArcIcons.Maximize2, GalleryShellLangResourceKind.PreviewFullscreen);
            _expand.Margin = new Thickness(6, 0, 0, 0);
            _expand.Click += (_, _) => OpenFullscreen();
            Grid.SetColumn(_expand, 4);
            bar.Children.Add(_expand);
        }
        if (hero)
        {
            _stage.MinHeight = HeroHeight;
        }
        _body.Children.Add(_stage);

        Children.Add(bar);
        Children.Add(_body);
        if (_demo is null)
        {
            _tabs.Items.Cast<TabItem>().Last().IsEnabled = false;
        }

        // The page's preview is built with the page; every other demo when its stage first reaches the viewport, so a page
        // with many examples opens at the cost of its first screen, and examples below it do not run until they are seen.
        if (hero || _demo is null)
        {
            Realize();
        }
        else
        {
            _content.Child = new Panel { MinHeight = PendingHeight };
            _ = new GalleryFirstSight(_stage, Realize);
        }
    }

    /// <summary>Whether the running demo has been built (the page's preview at once, the others on first sight).</summary>
    public bool IsRealized => _movable is not null;

    /// <summary>Builds the running demo now if it has not been built yet (it is otherwise built when it first comes into view).</summary>
    public void Realize()
    {
        if (_movable is not null)
        {
            return;
        }
        _view = _demo?.Create();
        _movable = CreateContent();
        _content.Child = _movable;
    }


    /// <summary>The demo's <c>Component.Name</c>.</summary>
    public string DemoId { get; }

    /// <summary>The page's preview: the demo above the first section, on a taller stage.</summary>
    public bool IsHero { get; }

    /// <summary>The demo shown (null when no demo has <see cref="DemoId"/>).</summary>
    public GalleryDemo? Demo => _demo;

    /// <summary>Whether the Preview shows the demo on a phone (the Mobile size).</summary>
    public bool IsMobile { get; private set; }

    /// <summary>The Fit / Mobile switch (null when this demo has none: not the page's Preview, or a TKCode.Arc.Mobile demo).</summary>
    public GallerySegmented? SizeSwitch => _size;

    /// <summary>The phone the Mobile size shows the demo on (null until Mobile was first chosen).</summary>
    public GalleryPreviewPhone? Phone => _phone;

    /// <summary>The running demo (null when the demo is missing, or until it is built: see <see cref="Realize"/>).</summary>
    public Control? View => _view;

    /// <summary>The Fullscreen button (null when this demo is not the page's Preview).</summary>
    public Button? FullscreenButton => _expand;

    /// <summary>The open Fullscreen view (null while closed).</summary>
    public GalleryPreviewFullscreen? Fullscreen => _fullscreen;

    /// <summary>Whether the Code tab is showing.</summary>
    public bool IsShowingCode { get; private set; }

    /// <summary>The source viewers, one per file, once the Code tab has been opened.</summary>
    public IReadOnlyList<CodeBlock> Code => _code?.Children.OfType<CodeBlock>().ToList() ?? [];

    /// <summary>Switches between the live control and its source (Code is ignored when the demo is missing).</summary>
    public void ShowCode(bool code)
    {
        code &= _demo is not null;
        if (code != IsShowingCode)
        {
            IsShowingCode = code;
            if (code && _code is null)
            {
                _code = CreateCode(_demo!);
                _body.Children.Add(_code);
            }
            else if (code)
            {
                // A file that could not be read the last time is asked for again.
                foreach (var block in Code)
                {
                    if (block.Tag is GalleryDemoFile file)
                    {
                        Fill(block, file);
                    }
                }
            }
            _stage.IsVisible = !code;
            if (_code is not null)
            {
                _code.IsVisible = code;
            }
            _live.IsVisible = !code;
            if (_size is not null)
            {
                _size.IsVisible = !code;
            }
            if (_expand is not null)
            {
                _expand.IsVisible = !code;
            }
        }
        var index = IsShowingCode ? 1 : 0;
        if (_tabs.SelectedIndex != index)
        {
            _tabs.SelectedIndex = index;
        }
    }

    /// <summary>
    /// Shows the demo on the stage (Fit) or on a phone's screen (Mobile), moving the same view so its state stays. The
    /// outline morphs between the stage's content box and the phone; popups and modals opened from the demo close first,
    /// since they belong to the place the demo leaves. Ignored for a demo without the switch.
    /// </summary>
    public void ShowMobile(bool mobile)
    {
        if (_size is null || _view is null || _fullscreen is not null)
        {
            return;
        }
        if (_size.SelectedIndex != (mobile ? 1 : 0))
        {
            _size.SelectedIndex = mobile ? 1 : 0;
        }
        if (mobile == IsMobile)
        {
            return;
        }
        IsMobile = mobile;
        var generation = ++_sizeGeneration;
        if (mobile)
        {
            MoveToPhone();
        }
        else
        {
            _ = MoveToStageAsync(generation);
        }
    }

    /// <summary>
    /// Fit → Mobile, as on uiarc.dev: a snapshot of the stage's demo fades out where it stood (90ms) while the outline
    /// morphs from the content box to the phone, and the demo, now on the phone, fades in after 100ms.
    /// </summary>
    private void MoveToPhone()
    {
        var view = _view!;
        var focused = FocusedInView();
        CloseOverlays(view);
        _phone ??= new GalleryPreviewPhone();
        var phoneShown = _layers.Children.Contains(_phone);
        Size from;
        if (phoneShown)
        {
            // Mobile again while the phone was still fading away: it turns back from where it is.
            from = _phone.CurrentSize;
        }
        else
        {
            // A Fit content box that was never laid out (a hidden page) morphs from the phone itself.
            from = _content.Bounds.Size is { Width: > 0, Height: > 0 } box ? box : GalleryPreviewPhone.OutlineSize;
            _fitSize = from;
            ShowGhost(view);
            _layers.Children.Insert(1, _phone);
        }
        _content.Child = null;
        ReleaseScreenSnapshot(_phone);
        _phone.Screen = view;
        FadeView(TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(100));
        _ = _phone.MorphAsync(from, _phone.Outline, toPhone: true);
        Refocus(focused);
    }

    /// <summary>
    /// Mobile → Fit, as on uiarc.dev: the demo goes back to the stage at once and fades in (250ms) while the phone,
    /// holding a snapshot of what it showed, grows toward the content box and fades out; then the phone leaves.
    /// </summary>
    private async Task MoveToStageAsync(int generation)
    {
        var phone = _phone!;
        var view = _view!;
        CloseOverlays(view);
        var focused = FocusedInView();
        var snapshot = Snapshot(view);
        phone.Screen = snapshot;
        _content.Child = view;
        FadeView(TimeSpan.FromMilliseconds(250), TimeSpan.Zero);
        Refocus(focused);
        var to = _fitSize is { Width: > 0, Height: > 0 } ? _fitSize : phone.CurrentSize;
        await phone.MorphAsync(phone.CurrentSize, to, toPhone: false);
        if (generation != _sizeGeneration)
        {
            // Mobile was chosen again while the phone faded: it is showing the demo again.
            return;
        }
        ReleaseScreenSnapshot(phone);
        phone.Screen = null;
        phone.Settle();
        _layers.Children.Remove(phone);
    }

    /// <summary>
    /// Fades the demo in from transparent (jumps under no motion). A phone shown for the first time templates its scroller
    /// on the next layout, so the demo may not be in the visual tree yet: it stays transparent until it is, then fades.
    /// </summary>
    private void FadeView(TimeSpan duration, TimeSpan delay)
    {
        var view = _view!;
        if (_viewOpacity is null)
        {
            _viewOpacity = new MotionValue<double>(view, 1, MotionVectors.Double);
            _viewOpacity.Changed += value => view.Opacity = value;
        }
        var opacity = _viewOpacity;
        opacity.Jump(0);
        var spec = MotionSpec.Tween(duration, ArcEasings.Enter, delay);
        if (TopLevel.GetTopLevel(view) is not null)
        {
            _ = opacity.AnimateTo(1, spec);
            return;
        }
        void Start(object? sender, VisualTreeAttachmentEventArgs e)
        {
            view.AttachedToVisualTree -= Start;
            _ = opacity.AnimateTo(1, spec);
        }
        view.AttachedToVisualTree += Start;
    }

    /// <summary>Leaves a picture of the demo where it stands on the stage, fading out over 90ms, then removes it.</summary>
    private void ShowGhost(Control view)
    {
        if (Snapshot(view) is not { } picture || view.TranslatePoint(default, _layers) is not { } at)
        {
            return;
        }
        var ghost = new Border
        {
            Child = picture,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(at.X, at.Y, 0, 0),
            IsHitTestVisible = false
        };
        _layers.Children.Add(ghost);
        var opacity = new MotionValue<double>(ghost, 1, MotionVectors.Double);
        opacity.Changed += value => ghost.Opacity = value;
        _ = opacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(90), ArcEasings.Standard)).ContinueWith(_ =>
        {
            _layers.Children.Remove(ghost);
            (picture.Source as IDisposable)?.Dispose();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// A picture of <paramref name="view"/> at its size (null under no motion, where nothing fades, or before layout).
    /// </summary>
    private static Image? Snapshot(Control view)
    {
        if (ArcMotion.GetEffectiveLevel(view) == ArcMotionLevel.Off || TopLevel.GetTopLevel(view) is not { } topLevel)
        {
            return null;
        }
        var size = view.Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
        {
            return null;
        }
        var scaling = topLevel.RenderScaling;
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scaling)), Math.Max(1, (int)Math.Ceiling(size.Height * scaling)));
        var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(pixels, new Vector(96 * scaling, 96 * scaling));
        bitmap.Render(view);
        return new Image
        {
            Source = bitmap,
            Width = size.Width,
            Height = size.Height,
            Stretch = Avalonia.Media.Stretch.Fill,
            IsHitTestVisible = false
        };
    }

    /// <summary>Drops the snapshot a phone's screen holds while it fades away (the demo itself is never disposed).</summary>
    private void ReleaseScreenSnapshot(GalleryPreviewPhone phone)
    {
        if (phone.Screen is Image { Source: IDisposable bitmap } picture && !ReferenceEquals(picture, _view))
        {
            phone.Screen = null;
            bitmap.Dispose();
        }
    }

    private IInputElement? FocusedInView() => _view is null ? null : GalleryPreviewTools.FocusedIn(this, _view);

    private static void Refocus(IInputElement? element) => GalleryPreviewTools.Refocus(element);

    private static void CloseOverlays(Control view) => GalleryPreviewTools.CloseOverlays(view);

    /// <summary>
    /// Opens the Fullscreen view: the demo (with its state) moves into a panel that grows from the stage over the whole
    /// window, in the current size; it comes back when the panel closes. The stage keeps its height meanwhile, so the
    /// page does not jump. Ignored for a demo without the button or while it is open.
    /// </summary>
    public void OpenFullscreen()
    {
        if (_expand is null || _fullscreen is not null || _movable is null || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }
        var from = _stage.TransformToVisual(topLevel) is { } transform ? new Rect(_stage.Bounds.Size).TransformToAABB(transform) : default;
        CloseOverlays(_movable!);
        var focused = GalleryPreviewTools.FocusedIn(this, _movable!);
        if (_stage.Bounds.Height > 0)
        {
            _stage.Height = _stage.Bounds.Height;
        }
        TakeMovable();
        var title = GalleryCatalog.AllEntries.FirstOrDefault(e => e.ClassName == _demo!.Component)?.Title ?? _demo!.Component;
        _fullscreen = new GalleryPreviewFullscreen(title, _movable!, canMobile: _size is not null, IsMobile, _expand);
        _fullscreen.Closed += OnFullscreenClosed;
        _fullscreen.Open(from);
        Refocus(focused);
    }

    private void OnFullscreenClosed(object? sender, EventArgs e)
    {
        if (sender is not GalleryPreviewFullscreen fullscreen || !ReferenceEquals(fullscreen, _fullscreen))
        {
            return;
        }
        fullscreen.Closed -= OnFullscreenClosed;
        var focused = GalleryPreviewTools.FocusedIn(fullscreen, _movable!);
        fullscreen.Release();
        _fullscreen = null;
        _stage.ClearValue(HeightProperty);
        PutMovable();
        Refocus(focused);
    }

    /// <summary>Takes the demo off the stage or the phone, ending any size morph in progress.</summary>
    private void TakeMovable()
    {
        _sizeGeneration++;
        _viewOpacity?.Jump(1);
        if (_phone is not null)
        {
            ReleaseScreenSnapshot(_phone);
            if (ReferenceEquals(_phone.Screen, _movable))
            {
                _phone.Screen = null;
            }
            _phone.Settle();
            if (!IsMobile)
            {
                // A phone still fading away after Fit was chosen leaves now.
                _layers.Children.Remove(_phone);
            }
        }
        if (ReferenceEquals(_content.Child, _movable))
        {
            _content.Child = null;
        }
    }

    /// <summary>Puts the demo back where the current size shows it.</summary>
    private void PutMovable()
    {
        if (IsMobile && _phone is not null)
        {
            _phone.Settle();
            _phone.Screen = _movable;
            if (!_layers.Children.Contains(_phone))
            {
                _layers.Children.Add(_phone);
            }
        }
        else
        {
            _content.Child = _movable;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the page (a new route) closes the Fullscreen view with it.
        _fullscreen?.Close();
    }

    /// <summary>Whether <paramref name="demo"/> belongs to a TKCode.Arc.Mobile component (<c>Demos/Mobile/...</c>).</summary>
    public static bool IsMobileDemo(GalleryDemo demo)
        => demo.Files.Length > 0 && demo.Files[0].Path.Replace('\\', '/').StartsWith($"Demos/{MobileCatalog.Folder}/", StringComparison.Ordinal);

    private Control CreateContent()
    {
        if (_view is { } view)
        {
            // A UserControl clips to its bounds, which cut off whatever a control draws outside its own box (the
            // inline edit's frame and the focus rings sit a few pixels past it); the stage has room around the view.
            view.ClipToBounds = false;
            // The demos of TKCode.Arc.Mobile run on a phone's screen, not on the stage.
            return IsMobileDemo(_demo!) ? new PhoneFrame { Screen = view } : view;
        }
        // The generator rejects unknown demos (GDOC010), so this only shows for a page edited while the app runs.
        var missing = GalleryUi.Mono(DemoId, ArcDesignTokenKind.TextMuted);
        missing.HorizontalAlignment = HorizontalAlignment.Center;
        return missing;
    }

    private static StackPanel CreateCode(GalleryDemo demo)
    {
        var files = new StackPanel { Spacing = 12 };
        foreach (var file in demo.Files)
        {
            var code = new CodeBlock
            {
                Language = file.Language,
                FileName = file.Path,
                ShowLineNumbers = true,
                IsCollapsible = true,
                MaxLines = 16
            };
            GalleryText.Bind(code, CodeBlock.CopyLabelProperty, GalleryShellLangResourceKind.CopyCode);
            GalleryText.Bind(code, CodeBlock.ShowAllLinesFormatProperty, GalleryShellLangResourceKind.ShowAllLines);
            GalleryText.Bind(code, CodeBlock.ShowFewerLinesLabelProperty, GalleryShellLangResourceKind.ShowFewerLines);
            Fill(code, file);
            files.Children.Add(code);
        }
        return files;
    }

    /// <summary>
    /// Shows <paramref name="file"/> in <paramref name="code"/>: at once from the gallery's resources, when it arrives in
    /// the WebAssembly host. A file that cannot be read stays marked on the viewer, to be asked for again.
    /// </summary>
    private static async void Fill(CodeBlock code, GalleryDemoFile file)
    {
        code.Tag = null;
        var text = await file.ReadAsync();
        if (text is null)
        {
            code.Tag = file;
        }
        else
        {
            code.Code = text;
        }
    }
}
