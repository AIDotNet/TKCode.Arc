using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Maps cell text to a <see cref="DataGridTone"/> (ignoring case), for renderers that color by value.</summary>
internal sealed class DataGridToneMap
{
    private readonly Dictionary<string, DataGridTone> _tones = new(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, DataGridTone> Tones => _tones;

    public DataGridTone Default { get; set; } = DataGridTone.Neutral;

    /// <summary>Reads "Won=Success, Lost=Danger" (pairs split by commas or semicolons); unknown tones are ignored.</summary>
    public void Parse(string? map)
    {
        _tones.Clear();
        if (string.IsNullOrWhiteSpace(map))
        {
            return;
        }
        foreach (var pair in map.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var at = pair.LastIndexOf('=');
            if (at > 0 && Enum.TryParse<DataGridTone>(pair[(at + 1)..].Trim(), ignoreCase: true, out var tone))
            {
                _tones[pair[..at].Trim()] = tone;
            }
        }
    }

    public DataGridTone Of(object? value, string text) =>
        (value as string ?? text) is { Length: > 0 } key && _tones.TryGetValue(key, out var tone) ? tone : Default;
}

/// <summary>
/// Shows the cell as a small pill colored by its value, such as a stage or a status. Map values to tones with
/// <see cref="Tones"/> (or <see cref="ToneMap"/> in XAML); values without a mapping use <see cref="DefaultTone"/>.
/// </summary>
public sealed class BadgeRenderer : DataGridCellRenderer
{
    private const double Padding = 8;
    private const double MaxHeight = 22;
    private readonly DataGridToneMap _map = new();
    private string? _toneMap;

    /// <summary>Tone per value, matched ignoring case. Change it before the grid draws, or call it again after.</summary>
    public IDictionary<string, DataGridTone> Tones => _map.Tones;

    /// <summary>The tone of values that have no entry in <see cref="Tones"/> (default <see cref="DataGridTone.Neutral"/>).</summary>
    public DataGridTone DefaultTone
    {
        get => _map.Default;
        set
        {
            _map.Default = value;
            NotifyChanged();
        }
    }

    /// <summary>XAML shorthand for <see cref="Tones"/>: <c>Won=Success, At risk=Warning, Lost=Danger</c>.</summary>
    public string? ToneMap
    {
        get => _toneMap;
        set
        {
            _toneMap = value;
            _map.Parse(value);
            NotifyChanged();
        }
    }

    /// <inheritdoc />
    public override void Render(in DataGridCellRenderContext context)
    {
        var text = context.Text;
        if (text.Length == 0)
        {
            return;
        }
        var brushes = context.Palette.Tone(_map.Of(context.Value, text));
        var bounds = context.Bounds;
        var height = Math.Min(MaxHeight, Math.Max(0, bounds.Height - 8));
        var maxText = Math.Max(1, bounds.Width - Padding * 2);
        var textWidth = context.MeasureText(text, maxText, medium: true, small: true);
        var pill = new Rect(bounds.X, Math.Round(bounds.Y + (bounds.Height - height) / 2), Math.Min(bounds.Width, textWidth + Padding * 2), height);
        context.Drawing.DrawRectangle(brushes.Fill, brushes.Border, pill, height / 2, height / 2);
        context.DrawText(text, pill.X + Padding, maxText, brushes.Foreground, medium: true, small: true);
    }
}

/// <summary>
/// Shows a number as a horizontal bar with its text at the right edge. The value is read against
/// <see cref="Minimum"/> and <see cref="Maximum"/> (0 to 100 by default, which suits a percent column).
/// </summary>
public sealed class ProgressRenderer : DataGridCellRenderer
{
    private const double TextWidth = 48;
    private const double Gap = 8;
    private const double BarHeight = 6;
    private double _minimum;
    private double _maximum = 100;
    private bool _showText = true;
    private DataGridTone _tone = DataGridTone.Accent;

    /// <summary>The value that draws an empty bar (default 0).</summary>
    public double Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            NotifyChanged();
        }
    }

    /// <summary>The value that draws a full bar (default 100).</summary>
    public double Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            NotifyChanged();
        }
    }

    /// <summary>Shows the value's text to the right of the bar (default true).</summary>
    public bool ShowText
    {
        get => _showText;
        set
        {
            _showText = value;
            NotifyChanged();
        }
    }

    /// <summary>The color of the bar (default <see cref="DataGridTone.Accent"/>).</summary>
    public DataGridTone Tone
    {
        get => _tone;
        set
        {
            _tone = value;
            NotifyChanged();
        }
    }

    /// <inheritdoc />
    public override void Render(in DataGridCellRenderContext context)
    {
        if (context.Value is not decimal number || _maximum <= _minimum)
        {
            return;
        }
        var ratio = Math.Clamp(((double)number - _minimum) / (_maximum - _minimum), 0, 1);
        var bounds = context.Bounds;
        var reserved = _showText ? TextWidth + Gap : 0;
        var trackWidth = Math.Max(0, bounds.Width - reserved);
        var track = new Rect(bounds.X, Math.Round(bounds.Y + (bounds.Height - BarHeight) / 2), trackWidth, BarHeight);
        var brushes = context.Palette.Tone(_tone);
        context.Drawing.DrawRectangle(context.Palette.SurfaceMuted, null, track, BarHeight / 2, BarHeight / 2);
        if (ratio > 0 && trackWidth > 0)
        {
            var fill = track.WithWidth(Math.Max(BarHeight, track.Width * ratio));
            context.Drawing.DrawRectangle(_tone == DataGridTone.Neutral ? context.Palette.SecondaryForeground : brushes.Foreground, null, fill,
                BarHeight / 2, BarHeight / 2);
        }
        if (_showText && context.Text.Length > 0)
        {
            var width = context.MeasureText(context.Text, TextWidth, small: true);
            context.DrawText(context.Text, bounds.Right - width, TextWidth, context.Palette.SecondaryForeground, small: true);
        }
    }
}

/// <summary>
/// Shows the cell as an accent-colored link with an underline that strengthens on hover. A click on the text raises <see cref="DataGrid.CellInvoked"/> with
/// <see cref="Part"/>; the cell is not selected by that click.
/// </summary>
public sealed class LinkRenderer : DataGridCellRenderer
{
    private string? _part;

    /// <summary>Named in <see cref="DataGridCellInvokedEventArgs.Part"/>, to tell links apart (default null).</summary>
    public string? Part
    {
        get => _part;
        set
        {
            _part = value;
            NotifyChanged();
        }
    }

    /// <inheritdoc />
    public override void Render(in DataGridCellRenderContext context)
    {
        var text = context.Text;
        if (text.Length == 0)
        {
            return;
        }
        var bounds = context.Bounds;
        var width = context.MeasureText(text, bounds.Width);
        context.DrawText(text, bounds.X, bounds.Width, context.Palette.Accent);
        var hovered = context.Pointer is { } pointer && TextArea(bounds, width).Contains(pointer);
        var y = Math.Round(bounds.Y + bounds.Height / 2 + context.FontSize / 2 + 1) + 0.5;
        context.Drawing.DrawLine(hovered ? context.Palette.AccentLine : context.Palette.AccentLineSubtle, new Point(bounds.X, y),
            new Point(bounds.X + width, y));
    }

    /// <inheritdoc />
    public override bool IsInteractiveAt(in DataGridCellPointerContext context) =>
        context.Text.Length > 0 && TextArea(context.Bounds, context.MeasureText(context.Text, context.Bounds.Width)).Contains(context.Position);

    /// <inheritdoc />
    public override bool OnPointerPressed(in DataGridCellPointerContext context)
    {
        if (!IsInteractiveAt(context))
        {
            return false;
        }
        context.Invoke(_part);
        return true;
    }

    private static Rect TextArea(Rect bounds, double textWidth) => new(bounds.X, bounds.Y, textWidth, bounds.Height);
}

/// <summary>
/// Shows an icon chosen by the cell's value, with the text after it. Icons are 24 x 24 stroked geometries
/// (<see cref="ArcIcons"/>) drawn at 16 px; <see cref="Tones"/> can color them by value.
/// </summary>
public sealed class IconTextRenderer : DataGridCellRenderer
{
    private const double IconSize = 16;
    private const double Gap = 6;
    private readonly Dictionary<string, Geometry> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataGridToneMap _map = new();
    private Geometry? _defaultIcon;
    private bool _showText = true;

    /// <summary>Icon per value, matched ignoring case. Change it before the grid draws, or set another property after.</summary>
    public IDictionary<string, Geometry> Icons => _icons;

    /// <summary>The icon of values that have no entry in <see cref="Icons"/>; null draws no icon for them.</summary>
    public Geometry? DefaultIcon
    {
        get => _defaultIcon;
        set
        {
            _defaultIcon = value;
            NotifyChanged();
        }
    }

    /// <summary>Tone per value for the icon and text, matched ignoring case (the default tone is neutral).</summary>
    public IDictionary<string, DataGridTone> Tones => _map.Tones;

    /// <summary>Shows the text after the icon (default true); false draws the icon alone, with the text still read by screen readers.</summary>
    public bool ShowText
    {
        get => _showText;
        set
        {
            _showText = value;
            NotifyChanged();
        }
    }

    /// <inheritdoc />
    public override void Render(in DataGridCellRenderContext context)
    {
        var key = context.Value as string ?? context.Text;
        var icon = key.Length > 0 && _icons.TryGetValue(key, out var found) ? found : _defaultIcon;
        var bounds = context.Bounds;
        var brushes = context.Palette.Tone(_map.Of(context.Value, context.Text));
        var x = bounds.X;
        if (icon is not null)
        {
            var origin = new Point(x, Math.Round(bounds.Y + (bounds.Height - IconSize) / 2));
            var scale = IconSize / 24;
            using (context.Drawing.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                context.Drawing.DrawGeometry(null, brushes.Stroke, icon);
            }
            x += IconSize + Gap;
        }
        if (_showText && context.Text.Length > 0 && bounds.Right - x > 1)
        {
            context.DrawText(context.Text, x, bounds.Right - x, _map.Tones.Count > 0 ? brushes.Foreground : context.Palette.Foreground);
        }
    }
}

/// <summary>
/// Draws a yes or no value as a checkbox in the middle of the cell; a click on it flips the value (one undo step, and
/// <see cref="DataGrid.CellEditEnding"/> can refuse it). Boolean columns use it without setting <see cref="DataGridColumn.Renderer"/>;
/// set it on a column of another type to show a 0 or 1 that way. Values that are not a <see cref="bool"/> draw as text.
/// </summary>
public sealed class CheckboxRenderer : DataGridCellRenderer
{
    private const double Size = 16;
    private const double HitPadding = 6;

    /// <summary>The renderer Boolean columns use.</summary>
    internal static CheckboxRenderer Instance { get; } = new();

    /// <inheritdoc />
    public override void Render(in DataGridCellRenderContext context)
    {
        if (context.Value is not (null or bool))
        {
            context.DrawText(context.Text, context.Bounds.X, context.Bounds.Width, context.Palette.Foreground);
            return;
        }
        var box = Box(context.Bounds);
        using (context.IsEditable ? default : context.Drawing.PushOpacity(0.5))
        {
            DataGridDrawing.Checkbox(context.Drawing, box, context.Value is true, context.Palette.Inner);
        }
    }

    /// <inheritdoc />
    public override bool IsInteractiveAt(in DataGridCellPointerContext context) =>
        context.Value is null or bool && context.IsEditable && Box(context.Bounds).Inflate(HitPadding).Contains(context.Position);

    /// <inheritdoc />
    public override bool OnPointerPressed(in DataGridCellPointerContext context)
    {
        if (!IsInteractiveAt(context))
        {
            return false;
        }
        context.SetValue(context.Value is not true);
        return true;
    }

    private static Rect Box(Rect bounds) =>
        new(Math.Round(bounds.X + (bounds.Width - Size) / 2), Math.Round(bounds.Y + (bounds.Height - Size) / 2), Size, Size);
}

/// <summary>One icon button of an <see cref="ActionsRenderer"/>.</summary>
public sealed class DataGridCellAction : AvaloniaObject
{
    /// <summary>Names the action in <see cref="DataGridCellInvokedEventArgs.Part"/> and for screen readers.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DataGridCellAction, string>(nameof(Label), string.Empty);

    /// <summary>A 24 x 24 stroked geometry drawn at 16 px (such as <c>ArcIcons.Trash2</c>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DataGridCellAction, Geometry?>(nameof(Icon));

    /// <summary>Draws the icon in the danger color.</summary>
    public static readonly StyledProperty<bool> IsDangerProperty =
        AvaloniaProperty.Register<DataGridCellAction, bool>(nameof(IsDanger));

    /// <summary>Runs with the row object as its parameter; the button is disabled while <see cref="ICommand.CanExecute"/> is false.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<DataGridCellAction, ICommand?>(nameof(Command));

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IsDangerProperty"/>
    public bool IsDanger { get => GetValue(IsDangerProperty); set => SetValue(IsDangerProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
}

/// <summary>
/// Draws a row of icon buttons in the cell, such as Edit and Delete at the end of a row. A click runs the action's
/// <see cref="DataGridCellAction.Command"/> with the row object and raises <see cref="DataGrid.CellInvoked"/> with the
/// action's label as the part; the cell is not selected by that click.
/// </summary>
public sealed class ActionsRenderer : DataGridCellRenderer
{
    private const double ButtonSize = 28;
    private const double Gap = 2;
    private const double IconSize = 16;
    private readonly AvaloniaList<DataGridCellAction> _actions = new();

    /// <summary>Creates the renderer.</summary>
    public ActionsRenderer()
    {
        _actions.CollectionChanged += OnActionsChanged;
    }

    /// <summary>The buttons, left to right. Buttons that do not fit the column width are not drawn.</summary>
    [Content]
    public AvaloniaList<DataGridCellAction> Actions => _actions;

    private void OnActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DataGridCellAction action in e.OldItems)
            {
                action.PropertyChanged -= OnActionPropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (DataGridCellAction action in e.NewItems)
            {
                action.PropertyChanged += OnActionPropertyChanged;
            }
        }
        NotifyChanged();
    }

    private void OnActionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => NotifyChanged();

    /// <inheritdoc />
    public override string GetText(object? value, DataGridCellFormat format, CultureInfo culture) => string.Join(", ", _actions.Select(a => a.Label));

    /// <inheritdoc />
    public override void Render(in DataGridCellRenderContext context)
    {
        var palette = context.Palette;
        for (var i = 0; i < _actions.Count; i++)
        {
            var rect = ButtonRect(context.Bounds, i);
            if (rect.Right > context.Bounds.Right + 0.5)
            {
                break;
            }
            var action = _actions[i];
            var enabled = action.Command?.CanExecute(context.Row) ?? true;
            var hovered = enabled && context.Pointer is { } pointer && rect.Contains(pointer);
            using (enabled ? default : context.Drawing.PushOpacity(0.4))
            {
                if (hovered)
                {
                    context.Drawing.DrawRectangle(palette.SurfaceMuted, null, rect, 6, 6);
                }
                if (action.Icon is { } icon)
                {
                    var stroke = palette.Tone(action.IsDanger ? DataGridTone.Danger : DataGridTone.Neutral).Stroke;
                    var scale = IconSize / 24;
                    var origin = new Point(Math.Round(rect.X + (rect.Width - IconSize) / 2), Math.Round(rect.Y + (rect.Height - IconSize) / 2));
                    using (context.Drawing.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
                    {
                        context.Drawing.DrawGeometry(null, stroke, icon);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public override bool IsInteractiveAt(in DataGridCellPointerContext context) =>
        IndexAt(context.Bounds, context.Position) is >= 0 and var index && (_actions[index].Command?.CanExecute(context.Row) ?? true);

    /// <inheritdoc />
    public override bool OnPointerPressed(in DataGridCellPointerContext context)
    {
        var index = IndexAt(context.Bounds, context.Position);
        if (index < 0)
        {
            return false;
        }
        var action = _actions[index];
        if (action.Command is { } command)
        {
            if (!command.CanExecute(context.Row))
            {
                return true;
            }
            command.Execute(context.Row);
        }
        context.Invoke(action.Label.Length > 0 ? action.Label : null);
        return true;
    }

    private int IndexAt(Rect bounds, Point position)
    {
        for (var i = 0; i < _actions.Count; i++)
        {
            var rect = ButtonRect(bounds, i);
            if (rect.Right > bounds.Right + 0.5)
            {
                break;
            }
            if (rect.Contains(position))
            {
                return i;
            }
        }
        return -1;
    }

    private static Rect ButtonRect(Rect bounds, int index) =>
        new(bounds.X + index * (ButtonSize + Gap), Math.Round(bounds.Y + (bounds.Height - ButtonSize) / 2), ButtonSize, ButtonSize);
}
