using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Turns a document into typed data and shows where each value came from. A quiet viewer of the document sits on the left and
/// an editable form on the right; while a model reads, the fields fill in one by one and the text each value came from is
/// tinted. Pointing at a field or at its text lights both and draws a curved link between them; values the model is unsure of
/// carry its reason and a "Looks right" button; "Accept all" hands the typed result to the app.
/// <code>
/// &lt;arc:DocumentExtraction Samples="{Binding Samples}" Extractor="{Binding Extractor}" /&gt;
/// </code>
/// </summary>
public partial class DocumentExtraction : TemplatedControl
{
    #region Properties

    /// <summary>The document to read; when null the selected sample's document is used.</summary>
    public static readonly StyledProperty<ExtractionDocument?> DocumentProperty =
        AvaloniaProperty.Register<DocumentExtraction, ExtractionDocument?>(nameof(Document));

    /// <summary>What to extract; when null the selected sample's schema is used.</summary>
    public static readonly StyledProperty<ExtractionSchema?> SchemaProperty =
        AvaloniaProperty.Register<DocumentExtraction, ExtractionSchema?>(nameof(Schema));

    /// <summary>The sample chosen on the file menu (two-way); choosing one clears the values.</summary>
    public static readonly StyledProperty<ExtractionSample?> SelectedSampleProperty =
        AvaloniaProperty.Register<DocumentExtraction, ExtractionSample?>(nameof(SelectedSample), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Reads the document with a model; null disables Extract.</summary>
    public static readonly StyledProperty<IDocumentExtractor?> ExtractorProperty =
        AvaloniaProperty.Register<DocumentExtraction, IDocumentExtractor?>(nameof(Extractor));

    /// <summary>The model's name and prices, for the footer and the cost.</summary>
    public static readonly StyledProperty<ExtractionModelInfo?> ModelProperty =
        AvaloniaProperty.Register<DocumentExtraction, ExtractionModelInfo?>(nameof(Model));

    /// <summary>The mark shown before the model's name in the footer (a geometry in a 24 × 24 box or an image).</summary>
    public static readonly StyledProperty<object?> ModelMarkProperty =
        AvaloniaProperty.Register<DocumentExtraction, object?>(nameof(ModelMark));

    /// <summary>Values below this confidence are marked for a check (0.8).</summary>
    public static readonly StyledProperty<double> ReviewThresholdProperty =
        AvaloniaProperty.Register<DocumentExtraction, double>(nameof(ReviewThreshold), 0.8);

    /// <summary>The path of the field the document and the form are linked on (two-way); Escape clears it.</summary>
    public static readonly StyledProperty<string?> SelectedPathProperty =
        AvaloniaProperty.Register<DocumentExtraction, string?>(nameof(SelectedPath), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The tab of the form pane (two-way).</summary>
    public static readonly StyledProperty<ExtractionTab> SelectedTabProperty =
        AvaloniaProperty.Register<DocumentExtraction, ExtractionTab>(nameof(SelectedTab), ExtractionTab.Fields, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The zoom of the page, 0.4 to 2 (two-way); while <see cref="IsFitWidth"/> it follows the canvas width.</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<DocumentExtraction, double>(nameof(Zoom), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the page is fitted to the canvas width (two-way); zooming by hand turns it off.</summary>
    public static readonly StyledProperty<bool> IsFitWidthProperty =
        AvaloniaProperty.Register<DocumentExtraction, bool>(nameof(IsFitWidth), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The zoom steps of the buttons and the keys.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>> ZoomStepsProperty =
        AvaloniaProperty.Register<DocumentExtraction, IReadOnlyList<double>>(nameof(ZoomSteps),
            new[] { 0.4, 0.5, 0.67, 0.75, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2 });

    /// <summary>Run by Extract and Retry.</summary>
    public static readonly StyledProperty<ICommand?> ExtractCommandProperty =
        AvaloniaProperty.Register<DocumentExtraction, ICommand?>(nameof(ExtractCommand));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<DocumentExtractionLabels> LabelsProperty =
        AvaloniaProperty.Register<DocumentExtraction, DocumentExtractionLabels>(nameof(Labels), new DocumentExtractionLabels());

    /// <summary>Where the extraction is (read-only).</summary>
    public static readonly DirectProperty<DocumentExtraction, ExtractionState> StateProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, ExtractionState>(nameof(State), o => o.State);

    /// <summary>What went wrong when the run failed (read-only).</summary>
    public static readonly DirectProperty<DocumentExtraction, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>How many fields have a value (read-only; a table counts its cells).</summary>
    public static readonly DirectProperty<DocumentExtraction, int> FoundCountProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, int>(nameof(FoundCount), o => o.FoundCount);

    /// <summary>How many values there are to find (read-only): every scalar field and every table cell so far; an empty table counts as one.</summary>
    public static readonly DirectProperty<DocumentExtraction, int> FieldCountProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, int>(nameof(FieldCount), o => o.FieldCount);

    /// <summary>How many values need a check (read-only).</summary>
    public static readonly DirectProperty<DocumentExtraction, int> CheckCountProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, int>(nameof(CheckCount), o => o.CheckCount);

    /// <summary>The mean confidence of every value, table cells included (read-only).</summary>
    public static readonly DirectProperty<DocumentExtraction, double> AverageConfidenceProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, double>(nameof(AverageConfidence), o => o.AverageConfidence);

    /// <summary>The cost of the run so far, in dollars (read-only).</summary>
    public static readonly DirectProperty<DocumentExtraction, decimal> CostProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, decimal>(nameof(Cost), o => o.Cost);

    /// <summary>The time of the run (read-only).</summary>
    public static readonly DirectProperty<DocumentExtraction, TimeSpan> ElapsedProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, TimeSpan>(nameof(Elapsed), o => o.Elapsed);

    /// <summary>The typed output as it is now (read-only).</summary>
    public static readonly DirectProperty<DocumentExtraction, ExtractionOutput> OutputProperty =
        AvaloniaProperty.RegisterDirect<DocumentExtraction, ExtractionOutput>(nameof(Output), o => o.Output);

    #endregion

    #region Events

    /// <summary>Raised when a run starts.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ExtractionStartedEvent =
        RoutedEvent.Register<DocumentExtraction, RoutedEventArgs>(nameof(ExtractionStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when a run ends: done, stopped or failed.</summary>
    public static readonly RoutedEvent<ExtractionCompletedEventArgs> ExtractionCompletedEvent =
        RoutedEvent.Register<DocumentExtraction, ExtractionCompletedEventArgs>(nameof(ExtractionCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when a value changes, by the model or by the person.</summary>
    public static readonly RoutedEvent<ExtractionValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<DocumentExtraction, ExtractionValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person marks a low-confidence value "Looks right".</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> ValueCheckedEvent =
        RoutedEvent.Register<DocumentExtraction, ExtractionPathEventArgs>(nameof(ValueChecked), RoutingStrategies.Bubble);

    /// <summary>Raised by Accept all so the app can save the output; throw or set <c>Cancel</c> to undo it.</summary>
    public static readonly RoutedEvent<ExtractionAcceptingEventArgs> AcceptingEvent =
        RoutedEvent.Register<DocumentExtraction, ExtractionAcceptingEventArgs>(nameof(Accepting), RoutingStrategies.Bubble);

    /// <summary>Raised after the accept was saved.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> AcceptedEvent =
        RoutedEvent.Register<DocumentExtraction, RoutedEventArgs>(nameof(Accepted), RoutingStrategies.Bubble);

    /// <summary>Raised when the linked field changes.</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> SelectedPathChangedEvent =
        RoutedEvent.Register<DocumentExtraction, ExtractionPathEventArgs>(nameof(SelectedPathChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a run starts.</summary>
    public event EventHandler<RoutedEventArgs>? ExtractionStarted { add => AddHandler(ExtractionStartedEvent, value); remove => RemoveHandler(ExtractionStartedEvent, value); }

    /// <summary>Raised when a run ends.</summary>
    public event EventHandler<ExtractionCompletedEventArgs>? ExtractionCompleted { add => AddHandler(ExtractionCompletedEvent, value); remove => RemoveHandler(ExtractionCompletedEvent, value); }

    /// <summary>Raised when a value changes.</summary>
    public event EventHandler<ExtractionValueChangedEventArgs>? ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }

    /// <summary>Raised when the person marks a value checked.</summary>
    public event EventHandler<ExtractionPathEventArgs>? ValueChecked { add => AddHandler(ValueCheckedEvent, value); remove => RemoveHandler(ValueCheckedEvent, value); }

    /// <summary>Raised by Accept all so the app can save.</summary>
    public event EventHandler<ExtractionAcceptingEventArgs>? Accepting { add => AddHandler(AcceptingEvent, value); remove => RemoveHandler(AcceptingEvent, value); }

    /// <summary>Raised after the accept was saved.</summary>
    public event EventHandler<RoutedEventArgs>? Accepted { add => AddHandler(AcceptedEvent, value); remove => RemoveHandler(AcceptedEvent, value); }

    /// <summary>Raised when the linked field changes.</summary>
    public event EventHandler<ExtractionPathEventArgs>? SelectedPathChanged { add => AddHandler(SelectedPathChangedEvent, value); remove => RemoveHandler(SelectedPathChangedEvent, value); }

    #endregion

    private ExtractionState _state;
    private string? _errorMessage;
    private int _foundCount;
    private int _fieldCount;
    private int _checkCount;
    private double _averageConfidence;
    private decimal _cost;
    private TimeSpan _elapsed;
    private ExtractionOutput _output = new([]);

    /// <summary>The documents of the file menu; with two or more the file button opens the menu.</summary>
    public AvaloniaList<ExtractionSample> Samples { get; } = new();

    /// <summary>The values the model found and the person edited (read-only view; the control creates and updates them).</summary>
    public IReadOnlyList<ExtractionValue> Values => _values;

    private readonly List<ExtractionValue> _values = new();

    /// <inheritdoc cref="DocumentProperty"/>
    public ExtractionDocument? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

    /// <inheritdoc cref="SchemaProperty"/>
    public ExtractionSchema? Schema { get => GetValue(SchemaProperty); set => SetValue(SchemaProperty, value); }

    /// <inheritdoc cref="SelectedSampleProperty"/>
    public ExtractionSample? SelectedSample { get => GetValue(SelectedSampleProperty); set => SetValue(SelectedSampleProperty, value); }

    /// <inheritdoc cref="ExtractorProperty"/>
    public IDocumentExtractor? Extractor { get => GetValue(ExtractorProperty); set => SetValue(ExtractorProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public ExtractionModelInfo? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public object? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="ReviewThresholdProperty"/>
    public double ReviewThreshold { get => GetValue(ReviewThresholdProperty); set => SetValue(ReviewThresholdProperty, value); }

    /// <inheritdoc cref="SelectedPathProperty"/>
    public string? SelectedPath { get => GetValue(SelectedPathProperty); set => SetValue(SelectedPathProperty, value); }

    /// <inheritdoc cref="SelectedTabProperty"/>
    public ExtractionTab SelectedTab { get => GetValue(SelectedTabProperty); set => SetValue(SelectedTabProperty, value); }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    /// <inheritdoc cref="IsFitWidthProperty"/>
    public bool IsFitWidth { get => GetValue(IsFitWidthProperty); set => SetValue(IsFitWidthProperty, value); }

    /// <inheritdoc cref="ZoomStepsProperty"/>
    public IReadOnlyList<double> ZoomSteps { get => GetValue(ZoomStepsProperty); set => SetValue(ZoomStepsProperty, value); }

    /// <inheritdoc cref="ExtractCommandProperty"/>
    public ICommand? ExtractCommand { get => GetValue(ExtractCommandProperty); set => SetValue(ExtractCommandProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DocumentExtractionLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public ExtractionState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => _errorMessage; private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value); }

    /// <inheritdoc cref="FoundCountProperty"/>
    public int FoundCount { get => _foundCount; private set => SetAndRaise(FoundCountProperty, ref _foundCount, value); }

    /// <inheritdoc cref="FieldCountProperty"/>
    public int FieldCount { get => _fieldCount; private set => SetAndRaise(FieldCountProperty, ref _fieldCount, value); }

    /// <inheritdoc cref="CheckCountProperty"/>
    public int CheckCount { get => _checkCount; private set => SetAndRaise(CheckCountProperty, ref _checkCount, value); }

    /// <inheritdoc cref="AverageConfidenceProperty"/>
    public double AverageConfidence { get => _averageConfidence; private set => SetAndRaise(AverageConfidenceProperty, ref _averageConfidence, value); }

    /// <inheritdoc cref="CostProperty"/>
    public decimal Cost { get => _cost; private set => SetAndRaise(CostProperty, ref _cost, value); }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan Elapsed { get => _elapsed; private set => SetAndRaise(ElapsedProperty, ref _elapsed, value); }

    /// <inheritdoc cref="OutputProperty"/>
    public ExtractionOutput Output { get => _output; private set => SetAndRaise(OutputProperty, ref _output, value); }
}
