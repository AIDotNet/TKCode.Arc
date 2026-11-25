// Ported from vscode (src/vs/editor/contrib/colorPicker/browser/colorPickerWidget.ts and colorPickerParts/
// colorPickerHeader.ts; commit b57d5c9e), MIT license, as an Avalonia control.
// Differences: the body (saturation box, hue and opacity strips) is Arc's ColorPicker, whose value field and panel pick
// the color; the header shows the presentation (a click selects the next one) and the original color (a click restores
// it); there is no close button (Escape or a click elsewhere closes the widget). Colors come from the control theme
// (Themes/CodeEditorColorPickerWidgetTheme.axaml); the chip shows the original color itself.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaColor = Avalonia.Media.Color;

namespace TKCode.Arc;

/// <summary>The color picker card of the color decorators (upstream's ColorPickerWidget), a content widget below the color.</summary>
[TemplatePart(PartPresentation, typeof(Button))]
[TemplatePart(PartOriginal, typeof(Border))]
[TemplatePart(PartPicker, typeof(ColorPicker))]
internal sealed class CodeEditorColorPickerWidget : TemplatedControl, ICodeEditorContentWidget
{
    internal const string PartPresentation = "PART_Presentation";
    internal const string PartOriginal = "PART_Original";
    internal const string PartPicker = "PART_Picker";

    /// <summary>Defines the <see cref="Shadow"/> property.</summary>
    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<CodeEditorColorPickerWidget, BoxShadows>(nameof(Shadow));

    /// <summary>Defines the <see cref="PresentationLabel"/> property.</summary>
    public static readonly StyledProperty<string?> PresentationLabelProperty =
        AvaloniaProperty.Register<CodeEditorColorPickerWidget, string?>(nameof(PresentationLabel));

    /// <summary>Defines the <see cref="OriginalColorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> OriginalColorBrushProperty =
        AvaloniaProperty.Register<CodeEditorColorPickerWidget, IBrush?>(nameof(OriginalColorBrush));

    /// <summary>Defines the <see cref="ChipBorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ChipBorderBrushProperty =
        AvaloniaProperty.Register<CodeEditorColorPickerWidget, IBrush?>(nameof(ChipBorderBrush));

    private Button? _presentation;
    private Border? _original;
    private ColorPicker? _picker;
    private AvaloniaColor _color;
    private bool _settingColor;
    private TKCode.Arc.Editor.TextPosition? _anchor;

    /// <summary>The card's shadow.</summary>
    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>The text the color will be written as.</summary>
    public string? PresentationLabel { get => GetValue(PresentationLabelProperty); set => SetValue(PresentationLabelProperty, value); }

    /// <summary>The color before the picker changed it.</summary>
    public IBrush? OriginalColorBrush { get => GetValue(OriginalColorBrushProperty); set => SetValue(OriginalColorBrushProperty, value); }

    /// <summary>The border of the original color chip.</summary>
    public IBrush? ChipBorderBrush { get => GetValue(ChipBorderBrushProperty); set => SetValue(ChipBorderBrushProperty, value); }

    /// <summary>Raised when the user picked a color.</summary>
    public event Action<AvaloniaColor>? ColorPicked;

    /// <summary>Raised when the user clicked the presentation (upstream: next presentation).</summary>
    public event Action? PresentationClicked;

    /// <summary>Raised when the user clicked the original color (upstream: restore it).</summary>
    public event Action? OriginalClicked;

    /// <inheritdoc />
    public string Id => "editor.contrib.colorPickerWidget";

    /// <inheritdoc />
    Control ICodeEditorContentWidget.Control => this;

    /// <summary>Where the widget is anchored (the start of the color); null hides it.</summary>
    public TKCode.Arc.Editor.TextPosition? Anchor
    {
        get => _anchor;
        set => _anchor = value;
    }

    /// <summary>The picker inside (for tests).</summary>
    internal ColorPicker? Picker => _picker;

    /// <summary>The color the picker shows.</summary>
    public AvaloniaColor Color
    {
        get => _color;
        set
        {
            _color = value;
            if (_picker is not null && _picker.Color != value)
            {
                _settingColor = true;
                try
                {
                    _picker.Color = value;
                }
                finally
                {
                    _settingColor = false;
                }
            }
        }
    }

    /// <summary>Shows the original color in the chip.</summary>
    public void SetOriginalColor(AvaloniaColor color) => OriginalColorBrush = new ImmutableSolidColorBrush(color);

    /// <inheritdoc />
    public ContentWidgetPosition? GetPosition() => _anchor is { } anchor && IsVisible
        ? new ContentWidgetPosition(anchor, [ContentWidgetPositionPreference.Below, ContentWidgetPositionPreference.Above])
        : null;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _presentation?.Click -= OnPresentationClick;

        _original?.PointerPressed -= OnOriginalPressed;

        _picker?.ColorChanged -= OnPickerColorChanged;

        _presentation = e.NameScope.Find<Button>(PartPresentation);
        _original = e.NameScope.Find<Border>(PartOriginal);
        _picker = e.NameScope.Find<ColorPicker>(PartPicker);
        _presentation?.Click += OnPresentationClick;

        _original?.PointerPressed += OnOriginalPressed;

        if (_picker is not null)
        {
            _picker.Label = null;
            _settingColor = true;
            try
            {
                _picker.Color = _color;
            }
            finally
            {
                _settingColor = false;
            }

            _picker.ColorChanged += OnPickerColorChanged;
        }
    }

    private void OnPresentationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => PresentationClicked?.Invoke();

    private void OnOriginalPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        OriginalClicked?.Invoke();
    }

    private void OnPickerColorChanged(object? sender, ColorPickerColorChangedEventArgs e)
    {
        _color = e.NewColor;
        if (!_settingColor)
        {
            ColorPicked?.Invoke(e.NewColor);
        }
    }

    /// <summary>Raises <see cref="ColorPicked"/> as if the user picked <paramref name="color"/> (for tests and keyboard use).</summary>
    internal void Pick(AvaloniaColor color)
    {
        Color = color;
        ColorPicked?.Invoke(color);
    }

    /// <summary>Opens the picker's panel.</summary>
    internal void OpenPanel()
    {
        _picker?.IsDropDownOpen = true;
    }
}
