using TKCode.Arc.Motion;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The detail of a tool call, under the chips of an AI reply (Arc chat): 8px below them, a panel with a 1px BorderSubtle
/// edge, radius 16, 12px above and below and 14px at the sides, 10px between its parts. What it holds follows
/// <see cref="Tool"/>:
/// <list type="bullet">
/// <item>a search: the search terms (a 13px Search glyph and the term, 4px apart) and the sources (one link row each; the
/// domain hides when <see cref="IsNarrow"/>);</item>
/// <item>a query: the statement in a monospace block that scrolls sideways, then the result row (label and value);</item>
/// <item>a failed query: the statement only;</item>
/// <item>anything else: <see cref="DetailTemplate"/> with the tool as its data.</item>
/// </list>
/// It opens and closes like a reply's reasoning (<see cref="AiCollapsible"/>: a <see cref="ArcSpring.Settle"/> height with
/// the content fading in 160ms and out in 100ms) and is unmounted while closed. Words come from the owner:
/// <see cref="SearchesName"/>, <see cref="SourcesName"/>, <see cref="ResultText"/> and <see cref="SourceLinkName"/>.
/// </summary>
[TemplatePart(BodyPart, typeof(AiCollapsible))]
[TemplatePart(QueriesPart, typeof(ItemsControl))]
[TemplatePart(SourcesPart, typeof(ItemsControl))]
[TemplatePart(InputPart, typeof(Control))]
[TemplatePart(ResultPart, typeof(Control))]
[TemplatePart(CustomPart, typeof(ContentPresenter))]
[TemplatePart(InputTextPart, typeof(TextBlock))]
[TemplatePart(OutputTextPart, typeof(TextBlock))]
[PseudoClasses(SearchPseudoClass, QueryPseudoClass, CustomPseudoClass, ErrorPseudoClass, NarrowPseudoClass)]
internal class AiToolDetail : TemplatedControl
{
    public const string BodyPart = "PART_Body";
    public const string QueriesPart = "PART_Queries";
    public const string SourcesPart = "PART_Sources";
    public const string InputPart = "PART_InputScroll";
    public const string ResultPart = "PART_ResultRow";
    public const string CustomPart = "PART_Custom";
    public const string InputTextPart = "PART_Input";
    public const string OutputTextPart = "PART_Output";

    public const string SearchPseudoClass = ":search";
    public const string QueryPseudoClass = ":query";
    public const string CustomPseudoClass = ":custom";
    public const string ErrorPseudoClass = ":error";
    public const string NarrowPseudoClass = ":narrow";

    #region Properties

    /// <summary>The tool call whose detail is shown; the panel follows its changes while it is in a window.</summary>
    public static readonly StyledProperty<AiChatTool?> ToolProperty =
        AvaloniaProperty.Register<AiToolDetail, AiChatTool?>(nameof(Tool));

    /// <summary>Whether the panel is open (the owner ties it to the chip's <see cref="AiToolChip.IsExpanded"/>).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiToolDetail, bool>(nameof(IsExpanded), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>A narrow chat (480 or less): source rows hide their domain. Inherited by the rows.</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<AiToolDetail, bool>(nameof(IsNarrow), inherits: true);

    /// <summary>Shows the detail of a <see cref="AiChatToolKind.Custom"/> tool; the data is the <see cref="AiChatTool"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> DetailTemplateProperty =
        AvaloniaProperty.Register<AiToolDetail, IDataTemplate?>(nameof(DetailTemplate));

    /// <summary>The accessible name of the list of search terms ("Searches").</summary>
    public static readonly StyledProperty<string?> SearchesNameProperty =
        AvaloniaProperty.Register<AiToolDetail, string?>(nameof(SearchesName));

    /// <summary>The accessible name of the list of sources ("Sources").</summary>
    public static readonly StyledProperty<string?> SourcesNameProperty =
        AvaloniaProperty.Register<AiToolDetail, string?>(nameof(SourcesName));

    /// <summary>The label before a query's result when the tool brings none (<see cref="AiChatTool.OutputLabel"/>): "Result".</summary>
    public static readonly StyledProperty<string?> ResultTextProperty =
        AvaloniaProperty.Register<AiToolDetail, string?>(nameof(ResultText));

    /// <summary>Names a source row from its number, title and domain ("Source 1: title, domain"). Inherited by the rows.</summary>
    public static readonly StyledProperty<Func<int, string, string?, string>?> SourceLinkNameProperty =
        AvaloniaProperty.Register<AiToolDetail, Func<int, string, string?, string>?>(nameof(SourceLinkName), inherits: true);

    /// <summary>The result label as shown: the tool's own, else <see cref="ResultText"/> (read-only, for the template).</summary>
    public static readonly DirectProperty<AiToolDetail, string?> ResultLabelProperty =
        AvaloniaProperty.RegisterDirect<AiToolDetail, string?>(nameof(ResultLabel), o => o.ResultLabel);

    /// <summary>Raised (bubbling) when the person opens a source row.</summary>
    public static readonly RoutedEvent<AiChatSourceEventArgs> SourceInvokedEvent =
        RoutedEvent.Register<AiToolDetail, AiChatSourceEventArgs>(nameof(SourceInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ToolProperty"/>
    public AiChatTool? Tool { get => GetValue(ToolProperty); set => SetValue(ToolProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="DetailTemplateProperty"/>
    public IDataTemplate? DetailTemplate { get => GetValue(DetailTemplateProperty); set => SetValue(DetailTemplateProperty, value); }

    /// <inheritdoc cref="SearchesNameProperty"/>
    public string? SearchesName { get => GetValue(SearchesNameProperty); set => SetValue(SearchesNameProperty, value); }

    /// <inheritdoc cref="SourcesNameProperty"/>
    public string? SourcesName { get => GetValue(SourcesNameProperty); set => SetValue(SourcesNameProperty, value); }

    /// <inheritdoc cref="ResultTextProperty"/>
    public string? ResultText { get => GetValue(ResultTextProperty); set => SetValue(ResultTextProperty, value); }

    /// <inheritdoc cref="SourceLinkNameProperty"/>
    public Func<int, string, string?, string>? SourceLinkName { get => GetValue(SourceLinkNameProperty); set => SetValue(SourceLinkNameProperty, value); }

    /// <inheritdoc cref="ResultLabelProperty"/>
    public string? ResultLabel { get => _resultLabel; private set => SetAndRaise(ResultLabelProperty, ref _resultLabel, value); }

    /// <inheritdoc cref="SourceInvokedEvent"/>
    public event EventHandler<AiChatSourceEventArgs>? SourceInvoked
    {
        add => AddHandler(SourceInvokedEvent, value);
        remove => RemoveHandler(SourceInvokedEvent, value);
    }

    #endregion

    private string? _resultLabel;
    private AiCollapsible? _body;
    private ItemsControl? _queries;
    private ItemsControl? _sources;
    private Control? _input;
    private Control? _result;
    private ContentPresenter? _custom;
    private TextBlock? _inputText;
    private TextBlock? _outputText;
    private AiChatTool? _watched;

    /// <summary>The collapsible body.</summary>
    internal AiCollapsible? Body => _body;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _body = e.NameScope.Find<AiCollapsible>(BodyPart);
        _queries = e.NameScope.Find<ItemsControl>(QueriesPart);
        _sources = e.NameScope.Find<ItemsControl>(SourcesPart);
        _input = e.NameScope.Find<Control>(InputPart);
        _result = e.NameScope.Find<Control>(ResultPart);
        _custom = e.NameScope.Find<ContentPresenter>(CustomPart);
        _inputText = e.NameScope.Find<TextBlock>(InputTextPart);
        _outputText = e.NameScope.Find<TextBlock>(OutputTextPart);
        if (_body is not null)
        {
            _body.IsExpanded = IsExpanded;
        }
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToolProperty)
        {
            Watch(this.IsAttachedToVisualTree() ? change.GetNewValue<AiChatTool?>() : null);
            Refresh();
        }
        else if (change.Property == IsExpandedProperty)
        {
            if (_body is not null)
            {
                _body.IsExpanded = change.GetNewValue<bool>();
            }
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == DetailTemplateProperty || change.Property == ResultTextProperty
                 || change.Property == SearchesNameProperty || change.Property == SourcesNameProperty)
        {
            Refresh();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Tool);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    private void Watch(AiChatTool? tool)
    {
        if (ReferenceEquals(_watched, tool))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnToolChanged;
            _watched.Queries.CollectionChanged -= OnListChanged;
            _watched.Sources.CollectionChanged -= OnListChanged;
        }
        _watched = tool;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnToolChanged;
            _watched.Queries.CollectionChanged += OnListChanged;
            _watched.Sources.CollectionChanged += OnListChanged;
        }
    }

    private void OnToolChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    /// <summary>Shows the parts the tool's kind and state call for; the lists themselves track the tool's collections.</summary>
    private void Refresh()
    {
        var tool = Tool;
        var kind = tool?.Kind ?? AiChatToolKind.Custom;
        var failed = tool?.Status == AiChatToolStatus.Error;
        PseudoClasses.Set(SearchPseudoClass, tool is not null && kind == AiChatToolKind.Search);
        PseudoClasses.Set(QueryPseudoClass, tool is not null && kind == AiChatToolKind.Query);
        PseudoClasses.Set(CustomPseudoClass, tool is not null && kind == AiChatToolKind.Custom);
        PseudoClasses.Set(ErrorPseudoClass, failed);
        ResultLabel = string.IsNullOrEmpty(tool?.OutputLabel) ? ResultText : tool.OutputLabel;

        var search = tool is not null && kind == AiChatToolKind.Search;
        if (_queries is not null)
        {
            _queries.ItemsSource = search ? tool!.Queries : null;
            _queries.IsVisible = search && tool!.Queries.Count > 0;
            AutomationProperties.SetName(_queries, SearchesName ?? string.Empty);
        }
        if (_sources is not null)
        {
            _sources.ItemsSource = search ? tool!.Sources : null;
            _sources.IsVisible = search && tool!.Sources.Count > 0;
            AutomationProperties.SetName(_sources, SourcesName ?? string.Empty);
        }
        var query = tool is not null && kind == AiChatToolKind.Query;
        if (_inputText is not null)
        {
            _inputText.Text = query ? tool!.Input : null;
        }
        if (_outputText is not null)
        {
            _outputText.Text = query ? tool!.Output : null;
        }
        if (_input is not null)
        {
            _input.IsVisible = query && !string.IsNullOrEmpty(tool!.Input);
        }
        if (_result is not null)
        {
            // A failed query shows only what it ran.
            _result.IsVisible = query && !failed && !string.IsNullOrEmpty(tool!.Output);
        }
        if (_custom is not null)
        {
            var custom = tool is not null && kind == AiChatToolKind.Custom && DetailTemplate is not null;
            // Content first, then the template; clearing drops the template first (a template is never built with null).
            if (custom)
            {
                _custom.Content = tool;
                _custom.ContentTemplate = DetailTemplate;
            }
            else
            {
                _custom.ContentTemplate = null;
                _custom.Content = null;
            }
            _custom.IsVisible = custom;
        }
    }
}
