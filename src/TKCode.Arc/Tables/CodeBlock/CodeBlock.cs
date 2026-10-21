using System.Globalization;
using System.Text;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A source viewer with a file-name header, syntax highlighting (TextMate grammars painted in Arc's role palette), a
/// copy action and optional collapsing.
/// Long sources collapse to <see cref="MaxLines"/> lines with a toggle that expands the rest in place on a smooth
/// height spring; new code fades in with a short rise. Long lines scroll horizontally inside the block.
/// </summary>
[TemplatePart(TextPart, typeof(CodeBlockText))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(ExpandPart, typeof(CodeBlockExpandButton))]
[TemplatePart(ViewportPart, typeof(CodeBlockViewport))]
[PseudoClasses(CollapsiblePseudoClass, ExpandedPseudoClass, CompactPseudoClass, LineNumbersPseudoClass, CodeFocusPseudoClass)]
public class CodeBlock : TemplatedControl
{
    public const string TextPart = "PART_Text";
    public const string CopyPart = "PART_Copy";
    public const string ExpandPart = "PART_Expand";
    public const string ViewportPart = "PART_Viewport";

    public const string CollapsiblePseudoClass = ":collapsible";
    public const string ExpandedPseudoClass = ":expanded";
    public const string CompactPseudoClass = ":compact";
    public const string LineNumbersPseudoClass = ":line-numbers";
    public const string CodeFocusPseudoClass = ":code-focus-visible";

    #region Properties

    /// <summary>Source shown in the block and copied by the action.</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<CodeBlock, string?>(nameof(Code));

    /// <summary>
    /// Header label and highlighting language (Arc default "tsx"): a fence-style name or alias such as <c>cs</c>,
    /// <c>ts</c>, <c>json</c>, <c>xml</c>/<c>axaml</c>, <c>py</c>, <c>sh</c>. Languages without a TextMate grammar
    /// show plain text.
    /// </summary>
    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<CodeBlock, string?>(nameof(Language), "tsx");

    /// <summary>File name in the header; <see cref="SourceCodeLabel"/> when null.</summary>
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<CodeBlock, string?>(nameof(FileName));

    /// <summary>Shows a line-number gutter (an extension; Arc has none).</summary>
    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<CodeBlock, bool>(nameof(ShowLineNumbers));

    /// <summary>Collapses sources longer than <see cref="MaxLines"/> lines behind a "Show all" toggle (Arc <c>maxLines</c>).</summary>
    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<CodeBlock, bool>(nameof(IsCollapsible));

    /// <summary>Lines visible while collapsed.</summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<CodeBlock, int>(nameof(MaxLines), 12, coerce: (_, v) => Math.Max(1, v));

    /// <summary>Whether a collapsible source shows all of its lines.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CodeBlock, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Label of the copy button in the header (default "Copy code").</summary>
    public static readonly StyledProperty<string> CopyLabelProperty =
        AvaloniaProperty.Register<CodeBlock, string>(nameof(CopyLabel), "Copy code");

    /// <summary>Header text shown when <c>FileName</c> is empty (default "Source code").</summary>
    public static readonly StyledProperty<string> SourceCodeLabelProperty =
        AvaloniaProperty.Register<CodeBlock, string>(nameof(SourceCodeLabel), "Source code");

    /// <summary>Collapsed toggle label; <c>{0}</c> is the line count.</summary>
    public static readonly StyledProperty<string> ShowAllLinesFormatProperty =
        AvaloniaProperty.Register<CodeBlock, string>(nameof(ShowAllLinesFormat), "Show all {0} lines");

    /// <summary>Label of the toggle while a collapsed source is expanded (default "Show fewer lines").</summary>
    public static readonly StyledProperty<string> ShowFewerLinesLabelProperty =
        AvaloniaProperty.Register<CodeBlock, string>(nameof(ShowFewerLinesLabel), "Show fewer lines");

    public static readonly DirectProperty<CodeBlock, int> LineCountProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, int>(nameof(LineCount), o => o.LineCount);

    /// <summary>Raised after the copy action put the code on the clipboard.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CopiedEvent =
        RoutedEvent.Register<CodeBlock, RoutedEventArgs>(nameof(Copied), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<CodeBlock, string> DisplayFileNameProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, string>(nameof(DisplayFileName), o => o.DisplayFileName);

    internal static readonly DirectProperty<CodeBlock, string> DisplayLanguageProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, string>(nameof(DisplayLanguage), o => o.DisplayLanguage);

    internal static readonly DirectProperty<CodeBlock, string> ShowAllLabelProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, string>(nameof(ShowAllLabel), o => o.ShowAllLabel);

    internal static readonly DirectProperty<CodeBlock, string> LineNumbersTextProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, string>(nameof(LineNumbersText), o => o.LineNumbersText);

    internal static readonly DirectProperty<CodeBlock, bool> CanCollapseProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, bool>(nameof(CanCollapse), o => o.CanCollapse);

    internal static readonly DirectProperty<CodeBlock, bool> IsCollapsedProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, bool>(nameof(IsCollapsed), o => o.IsCollapsed);

    internal static readonly DirectProperty<CodeBlock, double> CollapsedHeightProperty =
        AvaloniaProperty.RegisterDirect<CodeBlock, double>(nameof(CollapsedHeight), o => o.CollapsedHeight);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<CodeBlock, double>(nameof(CompactWidth), 420);

    // Syntax colors, set by the theme from CodeBlockToken.
    internal static readonly StyledProperty<IBrush?> CommentBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(CommentBrush));
    internal static readonly StyledProperty<IBrush?> StringBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(StringBrush));
    internal static readonly StyledProperty<IBrush?> NumberBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(NumberBrush));
    internal static readonly StyledProperty<IBrush?> KeywordBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(KeywordBrush));
    internal static readonly StyledProperty<IBrush?> TypeBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(TypeBrush));
    internal static readonly StyledProperty<IBrush?> FunctionBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(FunctionBrush));
    internal static readonly StyledProperty<IBrush?> PropertyBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(PropertyBrush));
    internal static readonly StyledProperty<IBrush?> TagBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(TagBrush));
    internal static readonly StyledProperty<IBrush?> PunctuationBrushProperty =
        AvaloniaProperty.Register<CodeBlock, IBrush?>(nameof(PunctuationBrush));

    private static readonly AvaloniaProperty[] s_brushProperties =
    [
        CommentBrushProperty, StringBrushProperty, NumberBrushProperty, KeywordBrushProperty, TypeBrushProperty,
        FunctionBrushProperty, PropertyBrushProperty, TagBrushProperty, PunctuationBrushProperty
    ];

    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public string? Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }
    public string? FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    public bool ShowLineNumbers { get => GetValue(ShowLineNumbersProperty); set => SetValue(ShowLineNumbersProperty, value); }
    public bool IsCollapsible { get => GetValue(IsCollapsibleProperty); set => SetValue(IsCollapsibleProperty, value); }
    public int MaxLines { get => GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    /// <inheritdoc cref="CopyLabelProperty"/>
    public string CopyLabel { get => GetValue(CopyLabelProperty); set => SetValue(CopyLabelProperty, value); }
    /// <inheritdoc cref="SourceCodeLabelProperty"/>
    public string SourceCodeLabel { get => GetValue(SourceCodeLabelProperty); set => SetValue(SourceCodeLabelProperty, value); }
    public string ShowAllLinesFormat { get => GetValue(ShowAllLinesFormatProperty); set => SetValue(ShowAllLinesFormatProperty, value); }
    /// <inheritdoc cref="ShowFewerLinesLabelProperty"/>
    public string ShowFewerLinesLabel { get => GetValue(ShowFewerLinesLabelProperty); set => SetValue(ShowFewerLinesLabelProperty, value); }

    /// <summary>Raised after the copy action put the code on the clipboard.</summary>
    public event EventHandler<RoutedEventArgs>? Copied
    {
        add => AddHandler(CopiedEvent, value);
        remove => RemoveHandler(CopiedEvent, value);
    }

    private int _lineCount = 1;

    /// <summary>Number of lines in <see cref="Code"/>.</summary>
    public int LineCount { get => _lineCount; private set => SetAndRaise(LineCountProperty, ref _lineCount, value); }

    private string _displayFileName = "Source code";
    internal string DisplayFileName { get => _displayFileName; private set => SetAndRaise(DisplayFileNameProperty, ref _displayFileName, value); }

    private string _displayLanguage = "tsx";
    internal string DisplayLanguage { get => _displayLanguage; private set => SetAndRaise(DisplayLanguageProperty, ref _displayLanguage, value); }

    private string _showAllLabel = string.Empty;
    internal string ShowAllLabel { get => _showAllLabel; private set => SetAndRaise(ShowAllLabelProperty, ref _showAllLabel, value); }

    private string _lineNumbersText = "1";
    internal string LineNumbersText { get => _lineNumbersText; private set => SetAndRaise(LineNumbersTextProperty, ref _lineNumbersText, value); }

    private bool _canCollapse;
    internal bool CanCollapse { get => _canCollapse; private set => SetAndRaise(CanCollapseProperty, ref _canCollapse, value); }

    private bool _isCollapsed;
    internal bool IsCollapsed { get => _isCollapsed; private set => SetAndRaise(IsCollapsedProperty, ref _isCollapsed, value); }

    private double _collapsedHeight = double.NaN;
    internal double CollapsedHeight { get => _collapsedHeight; private set => SetAndRaise(CollapsedHeightProperty, ref _collapsedHeight, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal IBrush? CommentBrush { get => GetValue(CommentBrushProperty); set => SetValue(CommentBrushProperty, value); }
    internal IBrush? StringBrush { get => GetValue(StringBrushProperty); set => SetValue(StringBrushProperty, value); }
    internal IBrush? NumberBrush { get => GetValue(NumberBrushProperty); set => SetValue(NumberBrushProperty, value); }
    internal IBrush? KeywordBrush { get => GetValue(KeywordBrushProperty); set => SetValue(KeywordBrushProperty, value); }
    internal IBrush? TypeBrush { get => GetValue(TypeBrushProperty); set => SetValue(TypeBrushProperty, value); }
    internal IBrush? FunctionBrush { get => GetValue(FunctionBrushProperty); set => SetValue(FunctionBrushProperty, value); }
    internal IBrush? PropertyBrush { get => GetValue(PropertyBrushProperty); set => SetValue(PropertyBrushProperty, value); }
    internal IBrush? TagBrush { get => GetValue(TagBrushProperty); set => SetValue(TagBrushProperty, value); }
    internal IBrush? PunctuationBrush { get => GetValue(PunctuationBrushProperty); set => SetValue(PunctuationBrushProperty, value); }

    /// <summary>The source as displayed and tokenized (line breaks normalized, tabs as two spaces).</summary>
    internal string DisplayCode { get; private set; } = string.Empty;

    #endregion

    private readonly CodeBlockSyntaxPalette _palette = new();
    private CodeBlockText? _text;
    private CopyButton? _copy;
    private CodeBlockExpandButton? _expand;
    private MotionValue<double>? _enter;
    private readonly TranslateTransform _textShift = new();

    static CodeBlock()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<CodeBlock>(AutomationLandmarkType.Region);
    }

    public CodeBlock()
    {
        UpdateSource();
        UpdateLabels();
    }

    internal CodeBlockText? TextPresenter => _text;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_text is not null)
        {
            _text.Classes.CollectionChanged -= OnTextClassesChanged;
            _text.PropertyChanged -= OnTextPropertyChanged;
        }
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        if (_expand is not null)
        {
            _expand.Click -= OnExpandClick;
        }
        _text = e.NameScope.Find<CodeBlockText>(TextPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _expand = e.NameScope.Find<CodeBlockExpandButton>(ExpandPart);
        if (_text is not null)
        {
            _text.Palette = _palette;
            _text.RenderTransform = _textShift;
            _text.Classes.CollectionChanged += OnTextClassesChanged;
            _text.PropertyChanged += OnTextPropertyChanged;
            _text.SetSource(DisplayCode, DisplayLanguage);
        }
        if (_copy is not null)
        {
            _copy.Copied += OnCopied;
        }
        if (_expand is not null)
        {
            _expand.Click += OnExpandClick;
        }
        UpdatePalette();
        UpdateCollapse();
    }

    private void OnCopied(object? sender, EventArgs e) => RaiseEvent(new RoutedEventArgs(CopiedEvent, this));

    private void OnExpandClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsExpandedProperty, !IsExpanded);

    private void OnTextClassesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => PseudoClasses.Set(CodeFocusPseudoClass, _text?.Classes.Contains(":focus-visible") == true);

    private void OnTextPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBlock.LineHeightProperty || e.Property == TextBlock.PaddingProperty)
        {
            UpdateCollapse();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty || change.Property == LanguageProperty)
        {
            UpdateSource();
            UpdateLabels();
            _text?.SetSource(DisplayCode, DisplayLanguage);
            UpdateCollapse();
            if (change.Property == CodeProperty)
            {
                PlayCodeEnter();
            }
        }
        else if (change.Property == FileNameProperty || change.Property == SourceCodeLabelProperty
                 || change.Property == ShowAllLinesFormatProperty)
        {
            UpdateLabels();
        }
        else if (change.Property == IsCollapsibleProperty || change.Property == MaxLinesProperty || change.Property == IsExpandedProperty)
        {
            UpdateCollapse();
        }
        else if (change.Property == ShowLineNumbersProperty)
        {
            PseudoClasses.Set(LineNumbersPseudoClass, ShowLineNumbers);
            UpdateLineNumbers();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            var width = Bounds.Width;
            PseudoClasses.Set(CompactPseudoClass, width > 0 && width < CompactWidth);
        }
        else if (Array.IndexOf(s_brushProperties, change.Property) >= 0)
        {
            UpdatePalette();
        }
    }

    private void UpdateSource()
    {
        var code = Code ?? string.Empty;
        if (code.Contains('\r'))
        {
            code = code.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        }
        if (code.Contains('\t'))
        {
            // Arc: tab-size 2.
            code = code.Replace("\t", "  ", StringComparison.Ordinal);
        }
        DisplayCode = code;
        DisplayLanguage = CodeBlockHighlighter.NormalizeLanguage(Language);
        var lines = 1;
        foreach (var ch in code.AsSpan())
        {
            if (ch == '\n')
            {
                lines++;
            }
        }
        LineCount = lines;
        UpdateLineNumbers();
    }

    private void UpdateLineNumbers()
    {
        if (!ShowLineNumbers)
        {
            LineNumbersText = string.Empty;
            return;
        }
        var builder = new StringBuilder(LineCount * 4);
        for (var i = 1; i <= LineCount; i++)
        {
            if (i > 1)
            {
                builder.Append('\n');
            }
            builder.Append(i.ToString(CultureInfo.InvariantCulture));
        }
        LineNumbersText = builder.ToString();
    }

    private void UpdateLabels()
    {
        DisplayFileName = string.IsNullOrEmpty(FileName) ? SourceCodeLabel : FileName;
        ShowAllLabel = string.Format(CultureInfo.CurrentCulture, ShowAllLinesFormat, LineCount);
        AutomationProperties.SetName(this, string.IsNullOrEmpty(FileName) ? $"{DisplayLanguage} source code" : $"{FileName} source code");
    }

    private void UpdateCollapse()
    {
        CanCollapse = IsCollapsible && LineCount > MaxLines;
        IsCollapsed = CanCollapse && !IsExpanded;
        PseudoClasses.Set(CollapsiblePseudoClass, CanCollapse);
        PseudoClasses.Set(ExpandedPseudoClass, CanCollapse && IsExpanded);
        if (_text is not null)
        {
            var lineHeight = double.IsNaN(_text.LineHeight) ? _text.FontSize * 1.7 : _text.LineHeight;
            CollapsedHeight = Math.Round(MaxLines * lineHeight + _text.Padding.Top + _text.Padding.Bottom);
        }
    }

    private void UpdatePalette()
    {
        _palette[CodeBlockSyntaxKind.Comment] = GetValue(CommentBrushProperty);
        _palette[CodeBlockSyntaxKind.String] = GetValue(StringBrushProperty);
        _palette[CodeBlockSyntaxKind.Number] = GetValue(NumberBrushProperty);
        _palette[CodeBlockSyntaxKind.Keyword] = GetValue(KeywordBrushProperty);
        _palette[CodeBlockSyntaxKind.Type] = GetValue(TypeBrushProperty);
        _palette[CodeBlockSyntaxKind.Function] = GetValue(FunctionBrushProperty);
        _palette[CodeBlockSyntaxKind.Property] = GetValue(PropertyBrushProperty);
        _palette[CodeBlockSyntaxKind.Tag] = GetValue(TagBrushProperty);
        _palette[CodeBlockSyntaxKind.Punctuation] = GetValue(PunctuationBrushProperty);
        _text?.RefreshPalette();
    }

    /// <summary>
    /// New code rises 4px into place while fading in (P2, standard + enter). Code-driven because the motion must
    /// restart from its initial state on every change, which a ControlTheme transition cannot express.
    /// </summary>
    private void PlayCodeEnter()
    {
        if (_text is null || !IsLoaded || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        var text = _text;
        if (_enter is null)
        {
            _enter = new MotionValue<double>(this, 1, MotionVectors.Double);
            _enter.Changed += p =>
            {
                text.Opacity = p;
                _textShift.Y = 4 * (1 - p);
            };
        }
        _enter.Jump(0);
        _ = _enter.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _enter?.Jump(1);
    }
}
