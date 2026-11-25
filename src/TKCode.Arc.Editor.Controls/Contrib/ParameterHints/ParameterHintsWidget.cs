// Ported from vscode (src/vs/editor/contrib/parameterHints/browser/parameterHintsWidget.ts, commit b57d5c9e), MIT
// license; the DOM is a behavior reference only.
// Differences from upstream:
//  - the widget is a CodeEditorHoverSurface: the overload controls (up, "1/3", down) on the left when there are several
//    signatures, the signature in the code font with the active parameter in bold, underlined and in the highlight
//    colour, and the documentation (the active parameter's, then the signature's) below a separator;
//  - the Markdown documentation is rendered by HoverMarkdownRenderer;
//  - the screen reader announcement (aria.alert) is the surface's automation name.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.ParameterHints;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>The parameter hints widget (upstream's ParameterHintsWidget).</summary>
internal sealed class ParameterHintsWidget : ICodeEditorContentWidget, IDisposable
{
    public const string WidgetId = "editor.widget.parameterHintsWidget";

    private readonly CodeEditor _editor;
    private readonly ParameterHintsModel _model;
    private readonly IContextKey<bool> _keyVisible;
    private readonly IContextKey<bool> _keyMultipleSignatures;
    private readonly HoverMarkdownRenderer _markdown;
    private readonly StackPanel _controls;
    private readonly TextBlock _overloads;
    private readonly TextBlock _signature;
    private readonly StackPanel _docs;
    private readonly Border _docsBorder;
    private readonly ScrollViewer _scroll;
    private readonly Border _padded;
    private bool _visible;
    private bool _added;
    private string? _announcedLabel;

    public ParameterHintsWidget(CodeEditor editor, ParameterHintsModel model, IContextKey<bool> keyMultipleSignatures)
    {
        _editor = editor;
        _model = model;
        _keyVisible = editor.CreateContextKey(CodeEditorContextKeys.ParameterHintsVisible);
        _keyMultipleSignatures = keyMultipleSignatures;
        Surface = new CodeEditorHoverSurface { Classes = { "parameter-hints" } };
        _markdown = new HoverMarkdownRenderer(Surface, editor, (_, _) => false);

        _overloads = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Name = "PART_Overloads" };
        Surface.BindTo(_overloads, TextBlock.ForegroundProperty, CodeEditorHoverSurface.MutedForegroundProperty);
        _controls = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
        _controls.Children.Add(CreateButton("↑", "Previous", Previous, "PART_Previous"));
        _controls.Children.Add(_overloads);
        _controls.Children.Add(CreateButton("↓", "Next", Next, "PART_Next"));

        _signature = new TextBlock { TextWrapping = TextWrapping.Wrap, Name = "PART_Signature" };
        Surface.BindTo(_signature, TextBlock.FontFamilyProperty, CodeEditorHoverSurface.CodeFontFamilyProperty);

        _docs = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        _docsBorder = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(0, 4, 0, 0), Child = _docs };
        Surface.BindTo(_docsBorder, Border.BorderBrushProperty, CodeEditorHoverSurface.SeparatorBrushProperty);

        var body = new StackPanel { Orientation = Orientation.Vertical, Children = { _signature, _docsBorder } };
        _scroll = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(_controls, Dock.Left);
        layout.Children.Add(_controls);
        layout.Children.Add(_scroll);
        var padded = new Border { Child = layout };
        _padded = padded;
        Surface.BindTo(padded, Decorator.PaddingProperty, CodeEditorHoverSurface.RowPaddingProperty);
        Surface.Content = padded;
    }

    /// <summary>The card the hints are drawn on.</summary>
    public CodeEditorHoverSurface Surface { get; }

    public string Id => WidgetId;

    public Control Control => Surface;

    /// <summary>Whether the hints show.</summary>
    public bool IsVisible => _visible;

    /// <summary>The signature text shown, the active parameter's range in it and the overload counter (for tests).</summary>
    internal (string Signature, string? ActiveParameter, string? Overloads, bool HasDocs) Rendered
    {
        get
        {
            var text = string.Concat(_signature.Inlines?.OfType<Run>().Select(r => r.Text) ?? []);
            var active = _signature.Inlines?.OfType<Run>().FirstOrDefault(r => r.Classes.Contains("active"))?.Text;
            return (text, active, _controls.IsVisible ? _overloads.Text : null, _docsBorder.IsVisible);
        }
    }

    /// <summary>The documentation controls (for tests).</summary>
    internal Panel Docs => _docs;

    public ContentWidgetPosition? GetPosition() =>
        _visible
            ? new ContentWidgetPosition(_editor.GetPosition(), [ContentWidgetPositionPreference.Above, ContentWidgetPositionPreference.Below])
            : null;

    /// <summary>Shows the widget (upstream's show).</summary>
    public void Show()
    {
        if (_visible)
        {
            return;
        }

        _keyVisible.Set(true);
        _visible = true;
        UpdateMaxHeight();
        if (!_added)
        {
            _editor.AddContentWidget(this);
            _added = true;
        }
        else
        {
            _editor.LayoutContentWidget(this);
        }
    }

    /// <summary>Hides the widget (upstream's hide).</summary>
    public void Hide()
    {
        if (!_visible)
        {
            return;
        }

        _keyVisible.Reset();
        _visible = false;
        _announcedLabel = null;
        _editor.LayoutContentWidget(this);
    }

    /// <summary>Shows the active signature of <paramref name="hints"/> (upstream's render).</summary>
    public void Render(SignatureHelp hints)
    {
        var multiple = hints.Signatures.Count > 1;
        _controls.IsVisible = multiple;
        _keyMultipleSignatures.Set(multiple);
        _signature.Inlines!.Clear();
        _docs.Children.Clear();

        if (hints.ActiveSignature < 0 || hints.ActiveSignature >= hints.Signatures.Count)
        {
            return;
        }

        var signature = hints.Signatures[hints.ActiveSignature];
        var activeParameterIndex = ParameterHintsLabel.GetActiveParameterIndex(hints, signature);
        if (signature.Parameters.Count == 0)
        {
            _signature.Inlines.Add(new Run(signature.Label));
        }
        else
        {
            RenderParameters(signature, activeParameterIndex);
        }

        var activeParameter = activeParameterIndex >= 0 && activeParameterIndex < signature.Parameters.Count ? signature.Parameters[activeParameterIndex] : null;
        if (activeParameter?.Documentation is { } parameterDoc)
        {
            _docs.Children.Add(RenderDocumentation(parameterDoc));
        }

        if (signature.Documentation is { } signatureDoc)
        {
            _docs.Children.Add(RenderDocumentation(signatureDoc));
        }

        _docsBorder.IsVisible = ParameterHintsLabel.HasDocs(signature, activeParameter);
        _overloads.Text = ParameterHintsLabel.FormatOverloads(hints);

        if (ParameterHintsLabel.GetLabelToAnnounce(signature, activeParameterIndex) is { } labelToAnnounce && _announcedLabel != labelToAnnounce)
        {
            Avalonia.Automation.AutomationProperties.SetName(Surface, labelToAnnounce + ", hint");
            _announcedLabel = labelToAnnounce;
        }

        UpdateMaxHeight();
        _editor.LayoutContentWidget(this);
    }

    private void RenderParameters(SignatureInformation signature, int activeParameterIndex)
    {
        var (start, end) = ParameterHintsLabel.GetParameterLabelOffsets(signature, activeParameterIndex);
        var label = signature.Label;
        start = Math.Clamp(start, 0, label.Length);
        end = Math.Clamp(end, start, label.Length);
        _signature.Inlines!.Add(new Run(label[..start]));
        var parameter = new Run(label[start..end]) { FontWeight = FontWeight.Bold, TextDecorations = TextDecorations.Underline };
        parameter.Classes.Add("active");
        Surface.BindTo(parameter, TextElement.ForegroundProperty, CodeEditorHoverSurface.HighlightForegroundProperty);
        _signature.Inlines.Add(parameter);
        _signature.Inlines.Add(new Run(label[end..]));
    }

    private Control RenderDocumentation(StringOrMarkdown documentation) =>
        documentation.Markdown is { } markdown
            ? _markdown.Render(markdown)
            : new TextBlock { Text = documentation.Text, TextWrapping = TextWrapping.Wrap };

    /// <summary>Shows the next signature (upstream's next).</summary>
    public void Next()
    {
        _editor.Focus();
        _model.Next();
    }

    /// <summary>Shows the previous signature (upstream's previous).</summary>
    public void Previous()
    {
        _editor.Focus();
        _model.Previous();
    }

    // upstream's updateMaxHeight: a quarter of the editor, at least 250 pixels
    // (and as wide as the text area at most)
    private void UpdateMaxHeight()
    {
        _scroll.MaxHeight = Math.Max(_editor.GetLayoutSize().Height / 4, 250);
        _padded.MaxWidth = CodeEditorWidgetSizing.AvailableWidth(_editor);
    }

    private TextBlock CreateButton(string glyph, string label, Action run, string name)
    {
        var button = new TextBlock
        {
            Text = glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Name = name,
        };
        ToolTip.SetTip(button, label);
        button.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            run();
        };
        return button;
    }

    public void Dispose()
    {
        Hide();
        if (_added)
        {
            _editor.RemoveContentWidget(this);
            _added = false;
        }
    }
}
