using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
/// demo's files (its .axaml and code-behind). Switching keeps the view and its state.
/// </summary>
public sealed class GalleryDemoView : StackPanel
{
    private const double HeroHeight = 420;

    private readonly GalleryDemo? _demo;
    private readonly Tabs _tabs;
    private readonly TextBlock _live = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 13);
    private readonly Panel _body = new();
    private readonly Border _stage;
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
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        bar.Children.Add(_tabs);
        bar.Children.Add(_live);

        // The content keeps its own width rules (rows center, the rest stretches) and sits in the middle of a tall stage.
        var content = new Border { VerticalAlignment = VerticalAlignment.Center, Child = CreateContent() };
        _stage = GalleryUi.Stage(content);
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
    }

    /// <summary>The demo's <c>Component.Name</c>.</summary>
    public string DemoId { get; }

    /// <summary>The page's preview: the demo above the first section, on a taller stage.</summary>
    public bool IsHero { get; }

    /// <summary>The demo shown (null when no demo has <see cref="DemoId"/>).</summary>
    public GalleryDemo? Demo => _demo;

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
            _stage.IsVisible = !code;
            if (_code is not null)
            {
                _code.IsVisible = code;
            }
            _live.IsVisible = !code;
        }
        var index = IsShowingCode ? 1 : 0;
        if (_tabs.SelectedIndex != index)
        {
            _tabs.SelectedIndex = index;
        }
    }

    /// <summary>Whether <paramref name="demo"/> belongs to a TKCode.Arc.Mobile component (<c>Demos/Mobile/...</c>).</summary>
    public static bool IsMobileDemo(GalleryDemo demo)
        => demo.Files.Length > 0 && demo.Files[0].Path.Replace('\\', '/').StartsWith($"Demos/{MobileCatalog.Folder}/", StringComparison.Ordinal);

    private Control CreateContent()
    {
        if (_demo is not null)
        {
            var view = _demo.Create();
            // A UserControl clips to its bounds, which cut off whatever a control draws outside its own box (the
            // inline edit's frame and the focus rings sit a few pixels past it); the stage has room around the view.
            view.ClipToBounds = false;
            // The demos of TKCode.Arc.Mobile run on a phone's screen, not on the stage.
            return IsMobileDemo(_demo) ? new PhoneFrame { Screen = view } : view;
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
                Code = file.Source,
                Language = file.Language,
                FileName = file.Path,
                ShowLineNumbers = true,
                IsCollapsible = true,
                MaxLines = 16
            };
            GalleryText.Bind(code, CodeBlock.CopyLabelProperty, GalleryShellLangResourceKind.CopyCode);
            GalleryText.Bind(code, CodeBlock.ShowAllLinesFormatProperty, GalleryShellLangResourceKind.ShowAllLines);
            GalleryText.Bind(code, CodeBlock.ShowFewerLinesLabelProperty, GalleryShellLangResourceKind.ShowFewerLines);
            files.Children.Add(code);
        }
        return files;
    }
}
