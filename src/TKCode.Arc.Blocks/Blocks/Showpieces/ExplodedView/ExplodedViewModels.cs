using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How an <see cref="ExplodedLayer"/> is drawn while the stack is apart.</summary>
public enum ExplodedLayerKind
{
    /// <summary>A transparent sheet: only its content shows, with a dashed outline while the stack is apart (solid accent when focused).</summary>
    Sheet,

    /// <summary>
    /// An opaque base: the page background with a 1px border and the layer's corner radius; the border turns accent when the
    /// layer is focused. It has no dashed outline. Usually the first (bottom) layer.
    /// </summary>
    Plate,

    /// <summary>A sheet whose content fades in with the separation (a layout grid that exists only in the exploded view).</summary>
    Grid
}

/// <summary>Where the progress of an <see cref="ExplodedView"/> comes from.</summary>
public enum ExplodedScrollMode
{
    /// <summary>The control scrolls inside its own frame: an empty track <see cref="ExplodedView.Length"/> frames tall under a pinned stage.</summary>
    Container,

    /// <summary>
    /// The page scrolls: the control is a track <see cref="ExplodedView.Length"/> viewports tall inside an ancestor
    /// <c>ScrollViewer</c>, and its stage stays pinned to the viewport while the track passes.
    /// </summary>
    Page,

    /// <summary>Nothing scrolls: the host drives <see cref="ExplodedView.Progress"/> (a slider, a timeline, its own scroll).</summary>
    External
}

/// <summary>
/// One layer of an <see cref="ExplodedView"/>, from the bottom up: its picture (<see cref="Content"/>, drawn at
/// <see cref="ExplodedView.LayerSize"/>), the label beside it (<see cref="Name"/> and <see cref="Note"/>) and the caption shown
/// while it is focused (<see cref="Name"/> and <see cref="Body"/>).
/// </summary>
public class ExplodedLayer : AvaloniaObject
{
    /// <summary>The layer's name: the first line of its label, the caption title and the name of its stop button.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<ExplodedLayer, string?>(nameof(Name));

    /// <summary>A short note under the name in the label ("4 columns, 8pt steps"); hidden below 560 px.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<ExplodedLayer, string?>(nameof(Note));

    /// <summary>One or two sentences shown in the caption while the layer is focused.</summary>
    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<ExplodedLayer, string?>(nameof(Body));

    /// <summary>The layer's picture: any control, laid out at the view's <see cref="ExplodedView.LayerSize"/>.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<ExplodedLayer, object?>(nameof(Content));

    /// <summary>Template for a <see cref="Content"/> that is data rather than a control.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<ExplodedLayer, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>How the layer is drawn: a transparent sheet (default), an opaque plate, or a grid that fades in as the stack opens.</summary>
    public static readonly StyledProperty<ExplodedLayerKind> KindProperty =
        AvaloniaProperty.Register<ExplodedLayer, ExplodedLayerKind>(nameof(Kind));

    /// <summary>Height of the label's anchor above the layer's bottom edge, on its right edge (default 44).</summary>
    public static readonly StyledProperty<double> AnchorFromBottomProperty =
        AvaloniaProperty.Register<ExplodedLayer, double>(nameof(AnchorFromBottom), 44);

    /// <inheritdoc cref="NameProperty"/>
    public string? Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <inheritdoc cref="BodyProperty"/>
    public string? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <inheritdoc cref="KindProperty"/>
    public ExplodedLayerKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc cref="AnchorFromBottomProperty"/>
    public double AnchorFromBottom
    {
        get => GetValue(AnchorFromBottomProperty);
        set => SetValue(AnchorFromBottomProperty, value);
    }
}

/// <summary>The caption of an end of an <see cref="ExplodedView"/>: before the stack opens or after it closes.</summary>
public class ExplodedCaption : AvaloniaObject
{
    /// <summary>The caption's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ExplodedCaption, string?>(nameof(Title));

    /// <summary>One or two sentences under the title.</summary>
    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<ExplodedCaption, string?>(nameof(Body));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="BodyProperty"/>
    public string? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
}

/// <summary>Arguments of <see cref="ExplodedView.StopChanged"/>.</summary>
public sealed class ExplodedStopChangedEventArgs : RoutedEventArgs
{
    internal ExplodedStopChangedEventArgs(RoutedEvent routedEvent, int stop, int previousStop, ExplodedLayer? layer, int layerIndex)
        : base(routedEvent)
    {
        Stop = stop;
        PreviousStop = previousStop;
        Layer = layer;
        LayerIndex = layerIndex;
    }

    /// <summary>The new stop: 0 is the whole screen, 1..N the layers from the bottom up, N + 1 the screen put back together.</summary>
    public int Stop { get; }

    /// <summary>The stop before the change.</summary>
    public int PreviousStop { get; }

    /// <summary>The focused layer, or null at the two ends.</summary>
    public ExplodedLayer? Layer { get; }

    /// <summary>Index of <see cref="Layer"/> in <see cref="ExplodedView.Layers"/>, or -1 at the two ends.</summary>
    public int LayerIndex { get; }
}
