// Ported from vscode (src/vs/editor/contrib/colorPicker/browser/colorPickerContribution.ts (ColorContribution: a click
// on a color decoration opens the picker), hoverColorPicker/hoverColorPickerParticipant.ts (renderHoverParts: the
// model's color and presentation changes write the presentation into the document), colorPickerParticipantUtils.ts
// (updateEditorModel) and standaloneColorPicker/standaloneColorPickerActions.ts (the show action); commit b57d5c9e),
// MIT license.
// Differences:
//  - the picker is a content widget below the color (upstream shows it in the hover); it opens on a click on the
//    swatch (colorDecoratorsActivatedOn 'click'), not on hover;
//  - the edited range is followed with a model decoration (upstream: _setTrackedRange with GrowsOnlyWhenTypingAfter);
//  - each picked color is written at once as one undo step (upstream: on onColorFlushed and on every change);
//  - Escape, a click elsewhere in the text or an edit of the document by something else closes the widget.

using Avalonia.Input;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.ColorPicker;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using AvaloniaColor = Avalonia.Media.Color;

namespace TKCode.Arc;

/// <summary>Opens a color picker on a click on a color swatch and writes the picked color back (upstream's ColorContribution and hover participant).</summary>
internal sealed class ColorPickerController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.colorContribution";

    /// <summary>Whether the color picker is shown (upstream's standaloneColorPickerVisible).</summary>
    public static readonly EditorContributionDescription Description =
        new(Id, editor => new ColorPickerController(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    /// <summary>Shows the color picker of the color at the cursor (upstream's editor.action.showOrFocusStandaloneColorPicker).</summary>
    public static readonly EditorAction ShowColorPickerAction = EditorAction.Create(
        "editor.action.showOrFocusStandaloneColorPicker",
        "Show or Focus Standalone Color Picker",
        null,
        static (editor, _) => Get(editor)?.ShowAtPosition() ?? Task.CompletedTask);

    /// <summary>Closes the color picker (Escape).</summary>
    public static readonly EditorCommand HideColorPickerCommand = EditorCommand.BindToContribution<ColorPickerController>(
        Id,
        "editor.action.hideColorPicker",
        EditorContextKeys.StandaloneColorPickerVisible,
        static (controller, _) => controller.Hide(),
        new EditorKeybinding((int)KeyCode.Escape) { When = EditorContextKeys.Focus, Weight = KeybindingWeight.EditorContrib });

    private static readonly ModelDecorationOptions TrackedRange = new("color-picker-range")
    {
        Stickiness = TrackedRangeStickiness.GrowsOnlyWhenTypingAfter,
    };

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _visible;
    private CodeEditorColorPickerWidget? _widget;
    private ColorPickerModel? _model;
    private IDocumentColorProvider? _provider;
    private string[] _range = [];
    private bool _applying;

    private ColorPickerController(CodeEditor editor)
    {
        _editor = editor;
        _visible = editor.CreateContextKey(EditorContextKeys.StandaloneColorPickerVisible);
        editor.MouseDown += OnMouseDown;
    }

    public static ColorPickerController? Get(CodeEditor editor) => editor.GetContribution<ColorPickerController>(Id);

    /// <summary>The widget while it is shown (for tests).</summary>
    internal CodeEditorColorPickerWidget? Widget => _widget;

    /// <summary>The picker model while the widget is shown (for tests).</summary>
    internal ColorPickerModel? Model => _model;

    /// <summary>The range of the color being edited.</summary>
    internal TextRange? Range => _range.Length > 0 ? _editor.Model.GetDecorationRange(_range[0]) : null;

    public void Dispose()
    {
        _editor.MouseDown -= OnMouseDown;
        Hide();
    }

    // upstream's ColorContribution.onMouseDown: a click on a swatch's injected text
    private void OnMouseDown(CodeEditorMouseEvent e)
    {
        if (!e.Event.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (ColorDetector.Get(_editor) is not { } detector || _editor.View is not { } view)
        {
            Hide();
            return;
        }

        var point = e.Event.GetPosition(view);
        foreach (var (range, data) in detector.GetColorRanges())
        {
            if (GetSwatchBounds(view, range) is { } bounds && bounds.Contains(point))
            {
                e.Handled = true;
                _ = Show(range, data);
                return;
            }
        }

        Hide();
    }

    /// <summary>Where the swatch of a color is drawn (the injected text before its range), relative to the view.</summary>
    internal Avalonia.Rect? GetSwatchBounds(CodeEditorView view, TextRange range)
    {
        if (_editor.ViewModel is not { } viewModel)
        {
            return null;
        }

        var converter = viewModel.CoordinatesConverter;
        var before = converter.ConvertModelPositionToViewPosition(range.Start, PositionAffinity.Left);
        var after = converter.ConvertModelPositionToViewPosition(range.Start, PositionAffinity.Right);
        if (before.LineNumber != after.LineNumber || after.Column <= before.Column)
        {
            return null;
        }

        var viewport = viewModel.ViewLayout.GetLinesViewportData();
        if (before.LineNumber < viewport.StartLineNumber || before.LineNumber > viewport.EndLineNumber)
        {
            return null;
        }

        var x1 = view.GetX(before.LineNumber, before.Column);
        var x2 = view.GetX(after.LineNumber, after.Column);
        return new Avalonia.Rect(x1, view.GetY(before.LineNumber), x2 - x1, view.LineHeight);
    }

    /// <summary>Opens the picker for the color at the cursor, if there is one.</summary>
    public Task ShowAtPosition()
    {
        if (_editor.GetPosition() is not { } position || ColorDetector.Get(_editor) is not { } detector)
        {
            return Task.CompletedTask;
        }

        foreach (var (range, data) in detector.GetColorRanges())
        {
            if (range.ContainsPosition(position))
            {
                return Show(range, data);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Opens the picker for a color of the document (upstream's createColorHover and renderHoverParts).</summary>
    public async Task Show(TextRange range, ColorData data)
    {
        Hide();
        var colorInfo = data.ColorInfo with { Range = range };
        var model = await ColorPickerModel.Create(_editor.Model, colorInfo, data.Provider, CancellationToken.None).ConfigureAwait(true);
        _model = model;
        _provider = data.Provider;
        _range = _editor.Model.DeltaDecorations(_range, [new ModelDeltaDecoration(range, TrackedRange)]);

        var widget = _widget = new CodeEditorColorPickerWidget { Anchor = range.Start };
        widget.SetOriginalColor(ToAvalonia(model.OriginalColor));
        widget.Color = ToAvalonia(model.Color);
        widget.PresentationLabel = model.Presentation?.Label;
        widget.ColorPicked += OnColorPicked;
        widget.PresentationClicked += OnPresentationClicked;
        widget.OriginalClicked += OnOriginalClicked;
        model.DidChangePresentation += OnPresentationChanged;
        model.ColorFlushed += OnColorFlushed;
        _editor.AddContentWidget(widget);
        _editor.DidChangeModelContent += OnContentChanged;
        _visible.Set(true);
    }

    /// <summary>Closes the picker.</summary>
    public void Hide()
    {
        if (_widget is { } widget)
        {
            widget.ColorPicked -= OnColorPicked;
            widget.PresentationClicked -= OnPresentationClicked;
            widget.OriginalClicked -= OnOriginalClicked;
            if (widget.Picker is { } picker)
            {
                picker.IsDropDownOpen = false;
            }

            _editor.RemoveContentWidget(widget);
            _widget = null;
            _editor.DidChangeModelContent -= OnContentChanged;
        }

        if (_model is { } model)
        {
            model.DidChangePresentation -= OnPresentationChanged;
            model.ColorFlushed -= OnColorFlushed;
            _model = null;
        }

        if (_range.Length > 0 && !_editor.Model.IsDisposed())
        {
            _range = _editor.Model.DeltaDecorations(_range, []);
        }

        _range = [];
        _provider = null;
        _visible.Set(false);
    }

    private void OnContentChanged(ModelContentChangedEvent e)
    {
        if (!_applying)
        {
            Hide();
        }
    }

    private void OnPresentationChanged(ColorPresentation? presentation)
    {
        if (_widget is { } widget)
        {
            widget.PresentationLabel = presentation?.Label;
        }
    }

    private void OnPresentationClicked() => _model?.SelectNextColorPresentation();

    private void OnOriginalClicked()
    {
        if (_model is { } model)
        {
            _ = ApplyColor(model.OriginalColor);
            if (_widget is { } widget)
            {
                widget.Color = ToAvalonia(model.OriginalColor);
            }
        }
    }

    private void OnColorPicked(AvaloniaColor color) =>
        _ = ApplyColor(new CssRgba(color.R, color.G, color.B, color.A / 255.0));

    private void OnColorFlushed(CssRgba color) => _ = ApplyColor(color);

    /// <summary>Writes <paramref name="color"/> into the document in the current presentation (upstream's updateColorPresentations and updateEditorModel).</summary>
    internal async Task ApplyColor(CssRgba color)
    {
        if (_model is not { } model || _provider is not { } provider || Range is not { } range)
        {
            return;
        }

        model.Color = color;
        await model.UpdateColorPresentations(_editor.Model, color, range, provider, CancellationToken.None).ConfigureAwait(true);
        if (!ReferenceEquals(model, _model) || model.Presentation is not { } presentation)
        {
            return;
        }

        var edit = presentation.TextEdit ?? new TextEdit(range, presentation.Label);
        var edits = new List<SingleEditOperation>();
        if (presentation.AdditionalTextEdits is { } additional)
        {
            edits.AddRange(additional.Select(TextEdit.AsEditOperation));
        }

        edits.Add(EditOperation.Replace(edit.Range, edit.Text));
        _range = _editor.Model.DeltaDecorations(_range, [new ModelDeltaDecoration(edit.Range, TrackedRange)]);
        _applying = true;
        try
        {
            _editor.ExecuteEdits("colorpicker", edits);
            _editor.PushUndoStop();
        }
        finally
        {
            _applying = false;
        }
    }

    private static AvaloniaColor ToAvalonia(CssRgba color) =>
        AvaloniaColor.FromArgb((byte)Math.Round(color.A * 255), (byte)color.R, (byte)color.G, (byte)color.B);
}
