using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class ImageGeneration
{
    private Textarea? _prompt;
    private StackPanel? _models;
    private StackPanel? _aspects;
    private AvaloniaButton? _generate;
    private ImageGenerationGrid? _grid;
    private ArcContentSwap? _status;
    private AvaloniaButton? _upscale;
    private AvaloniaButton? _vary;
    private AvaloniaButton? _download;
    private ExplainerAnnouncer? _announcer;
    private ImageGenerationFaces? _generateFaces;
    private ImageGenerationFaces? _upscaleFaces;
    private ImageGenerationFaces? _varyFaces;
    private ImageGenerationFaces? _downloadFaces;
    private StackPanel? _suggestions;
    private AvaloniaButton? _close;
    private ViewportVisibility? _viewport;
    private DispatcherTimer? _savedTimer;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _upscaleCts;
    private ImageGenerationAspect? _batchAspect;
    private ImageGenerationModel? _batchModel;
    private bool _hasBatch;
    private bool _stopped;
    private bool _simulated;
    private bool _autoBatch;
    private bool _autoStarted;
    private bool _touched;
    private bool _upscaling;
    private bool _syncingPrompt;
    private int _selected = -1;
    private int _attempt;
    private string? _shownStatus;

    static ImageGeneration()
    {
        SpecialFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<ImageGeneration>(false);
    }

    /// <summary>Creates the card; with no generator it plays its <see cref="Simulation"/>, if any.</summary>
    public ImageGeneration()
    {
        AddHandler(PointerPressedEvent, (_, _) => _touched = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>The models on offer: <see cref="Models"/>, else the simulation's, else one unnamed model.</summary>
    internal IReadOnlyList<ImageGenerationModel> EffectiveModels =>
        Models is { Count: > 0 } models ? models : Simulation?.Models is { Count: > 0 } simulated ? simulated : s_noModels;

    /// <summary>The shapes on offer: <see cref="AspectRatios"/>, or square, portrait and landscape.</summary>
    internal IReadOnlyList<ImageGenerationAspect> EffectiveAspects =>
        AspectRatios is { Count: > 0 } aspects ? aspects : s_defaultAspects;

    internal IReadOnlyList<string> EffectiveSuggestions => Suggestions ?? Simulation?.Suggestions ?? [];

    private static readonly IReadOnlyList<ImageGenerationModel> s_noModels = [new() { Id = "model", Name = string.Empty, Mark = ArcIcons.Sparkles }];

    private static readonly IReadOnlyList<ImageGenerationAspect> s_defaultAspects =
    [
        new() { Id = "1:1", Label = "1:1", Width = 1024, Height = 1024 },
        new() { Id = "4:5", Label = "4:5", Width = 896, Height = 1120 },
        new() { Id = "16:9", Label = "16:9", Width = 1344, Height = 756 },
    ];

    /// <summary>The chosen model.</summary>
    internal ImageGenerationModel EffectiveModel => Find(EffectiveModels, SelectedModelId, m => m.Id);

    /// <summary>The chosen shape.</summary>
    internal ImageGenerationAspect EffectiveAspect => Find(EffectiveAspects, SelectedAspectId, a => a.Id);

    /// <summary>The picked tile, or −1.</summary>
    internal int SelectedIndex => _selected;

    internal IReadOnlyList<ImageGenerationTile> Tiles => _grid?.Tiles ?? [];

    internal ImageGenerationGrid? GridPartControl => _grid;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal string? StatusText => _shownStatus;

    internal StackPanel? SuggestionsPanel => _suggestions;

    internal ImageGenerationFaces? GenerateFaces => _generateFaces;

    internal ImageGenerationFaces? UpscaleFaces => _upscaleFaces;

    internal ImageGenerationFaces? DownloadFaces => _downloadFaces;

    internal AvaloniaButton? CloseButtonControl => _close;

    /// <summary>Whether the built-in simulation makes the images.</summary>
    internal bool UsesSimulation => Generator is null && Simulation is not null;

    /// <summary>Whether Generate has something to call: your generator, or the simulation.</summary>
    internal bool CanGenerate => Generator is not null || Simulation is not null;

    private bool AutoGenerates => CanGenerate && (AutoGenerate ?? UsesSimulation);

    private static T Find<T>(IReadOnlyList<T> items, string? id, Func<T, string> key)
    {
        if (id is not null)
        {
            foreach (var item in items)
            {
                if (key(item) == id)
                {
                    return item;
                }
            }
        }
        return items[0];
    }

    #region Template

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _prompt = e.NameScope.Find<Textarea>(PromptPart);
        _models = e.NameScope.Find<StackPanel>(ModelsPart);
        _aspects = e.NameScope.Find<StackPanel>(AspectsPart);
        _generate = e.NameScope.Find<AvaloniaButton>(GeneratePart);
        _grid = e.NameScope.Find<ImageGenerationGrid>(GridPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _upscale = e.NameScope.Find<AvaloniaButton>(UpscalePart);
        _vary = e.NameScope.Find<AvaloniaButton>(VaryPart);
        _download = e.NameScope.Find<AvaloniaButton>(DownloadPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        _shownStatus = null;

        if (_prompt is not null)
        {
            _prompt.TextChanged += OnPromptTextChanged;
            _prompt.AddHandler(KeyDownEvent, OnPromptKeyDown, RoutingStrategies.Tunnel);
        }
        if (_generate is not null)
        {
            _generateFaces = new ImageGenerationFaces().Add(ArcIcons.Sparkles, string.Empty).Add(ArcIcons.Square, string.Empty);
            _generate.Content = _generateFaces;
            _generate.Click += OnGenerateClick;
        }
        if (_upscale is not null)
        {
            _upscaleFaces = new ImageGenerationFaces().Add(ArcIcons.Maximize2, string.Empty).Add(ArcIcons.Maximize2, string.Empty)
                .Add(ArcIcons.Check, string.Empty);
            _upscale.Content = _upscaleFaces;
            _upscale.Click += OnUpscaleClick;
        }
        if (_vary is not null)
        {
            _varyFaces = new ImageGenerationFaces().Add(ArcIcons.RefreshCw, string.Empty);
            _vary.Content = _varyFaces;
            _vary.Click += OnVaryClick;
        }
        if (_download is not null)
        {
            _downloadFaces = new ImageGenerationFaces().Add(ArcIcons.Download, string.Empty).Add(ArcIcons.Check, string.Empty);
            _download.Content = _downloadFaces;
            _download.Click += OnDownloadClick;
        }
        if (_grid is not null)
        {
            BuildGrid(_grid);
        }
        RebuildChips();
        ApplyLabels();
        SyncPrompt();
        UpdateAll();
    }

    private void Unhook()
    {
        if (_prompt is not null)
        {
            _prompt.TextChanged -= OnPromptTextChanged;
            _prompt.RemoveHandler(KeyDownEvent, OnPromptKeyDown);
        }
        if (_generate is not null)
        {
            _generate.Click -= OnGenerateClick;
        }
        if (_upscale is not null)
        {
            _upscale.Click -= OnUpscaleClick;
        }
        if (_vary is not null)
        {
            _vary.Click -= OnVaryClick;
        }
        if (_download is not null)
        {
            _download.Click -= OnDownloadClick;
        }
        if (_grid is not null)
        {
            _grid.Collapsed -= OnGridCollapsed;
        }
    }

    private void BuildGrid(ImageGenerationGrid grid)
    {
        grid.Collapsed += OnGridCollapsed;
        if (grid.Tiles.Count == 0)
        {
            for (var i = 0; i < BatchSize; i++)
            {
                var tile = new ImageGenerationTile { Index = i };
                tile.Click += OnTileClick;
                grid.AddTile(tile);
            }
        }
        _suggestions = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16),
        };
        grid.Overlay = _suggestions;
        _close = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Padding = new Thickness(8, 0) };
        _close.Content = new ArcGlyph { Data = ArcIcons.Minimize2, Size = 16 };
        _close.Click += (_, _) => Close();
        grid.CloseButton = _close;
        var aspect = EffectiveAspect;
        grid.SetRatio((double)aspect.Height / aspect.Width, animate: false);
        foreach (var tile in grid.Tiles)
        {
            tile.Reset((double)aspect.Height / aspect.Width);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PromptProperty)
        {
            SyncPrompt();
        }
        else if (change.Property == ModelsProperty || change.Property == AspectRatiosProperty)
        {
            RebuildChips();
            UpdateAll();
        }
        else if (change.Property == SimulationProperty)
        {
            // Another simulation (or none): its run stops, and the prompt, models and suggestions follow it.
            if (change.GetOldValue<ImageGenerationSimulation?>() is { } old && Generator is null)
            {
                Stop();
                if (Prompt == old.Prompt)
                {
                    SetCurrentValue(PromptProperty, null);
                }
            }
            if (Prompt is null && Simulation?.Prompt is { } prompt)
            {
                SetCurrentValue(PromptProperty, prompt);
            }
            RebuildChips();
            ApplyLabels();
            UpdateAll();
        }
        else if (change.Property == SelectedModelIdProperty)
        {
            SyncChips();
            UpdateAll();
        }
        else if (change.Property == SelectedAspectIdProperty)
        {
            SyncChips();
            if (!_hasBatch && _grid is not null)
            {
                // Before the first batch the empty grid takes the chosen shape; later the grid keeps its batch's shape.
                var aspect = EffectiveAspect;
                var ratio = (double)aspect.Height / aspect.Width;
                _grid.SetRatio(ratio, animate: true);
                foreach (var tile in _grid.Tiles)
                {
                    tile.Reset(ratio);
                }
            }
            UpdateAll();
        }
        else if (change.Property == LabelsProperty || change.Property == PlaceholderProperty || change.Property == SuggestionsProperty)
        {
            ApplyLabels();
            UpdateAll();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_viewport is null && !_autoStarted)
        {
            _viewport = new ViewportVisibility(this, 0.25, once: true);
            _viewport.VisibilityChanged += OnViewportVisibility;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportVisibility;
            _viewport.Dispose();
            _viewport = null;
        }
    }

    private void OnViewportVisibility(object? sender, bool visible)
    {
        if (!visible || _autoStarted)
        {
            return;
        }
        _autoStarted = true;
        if (AutoGenerates && !_hasBatch && !_touched)
        {
            StartBatch(null, auto: true);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var width = e.NewSize.Width;
        PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
        PseudoClasses.Set(StackedPseudoClass, width < StackWidth);
    }

    #endregion

    #region Prompt and chips

    private void SyncPrompt()
    {
        if (_prompt is null || _prompt.Text == Prompt)
        {
            return;
        }
        _syncingPrompt = true;
        try
        {
            _prompt.Text = Prompt;
        }
        finally
        {
            _syncingPrompt = false;
        }
    }

    private void OnPromptTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_syncingPrompt && _prompt is not null && _prompt.Text != Prompt)
        {
            SetCurrentValue(PromptProperty, _prompt.Text);
            UpdateAll();
        }
    }

    /// <summary>Cmd/Ctrl + Enter generates from the prompt field.</summary>
    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            e.Handled = true;
            if (!IsGenerating)
            {
                Generate();
            }
        }
    }

    private void OnAnyKeyDown(object? sender, KeyEventArgs e)
    {
        _touched = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsExpanded)
        {
            e.Handled = true;
            Close();
        }
    }

    private void RebuildChips()
    {
        if (_models is not null)
        {
            _models.Children.Clear();
            foreach (var model in EffectiveModels)
            {
                var chip = new ImageGenerationChip
                {
                    Id = model.Id,
                    Text = model.Name,
                    Mark = model.Mark,
                    Monogram = model.Name.Length > 0 ? model.Name[..1].ToUpperInvariant() : "?",
                    Collapses = true,
                };
                AutomationProperties.SetName(chip, model.Name);
                Tooltip.SetTip(chip, model.Name);
                chip.Picked += OnModelPicked;
                _models.Children.Add(chip);
            }
        }
        if (_aspects is not null)
        {
            _aspects.Children.Clear();
            foreach (var aspect in EffectiveAspects)
            {
                var chip = new ImageGenerationChip
                {
                    Id = aspect.Id,
                    Text = aspect.Label,
                    ShapeAspect = (double)Math.Max(1, aspect.Height) / Math.Max(1, aspect.Width),
                };
                AutomationProperties.SetName(chip, aspect.Label);
                chip.Picked += OnAspectPicked;
                _aspects.Children.Add(chip);
            }
        }
        SyncChips();
    }

    private void SyncChips()
    {
        var model = EffectiveModel.Id;
        var aspect = EffectiveAspect.Id;
        if (_models is not null)
        {
            foreach (var child in _models.Children)
            {
                if (child is ImageGenerationChip chip)
                {
                    chip.IsChecked = chip.Id == model;
                    chip.IsTabStop = chip.Id == model;
                }
            }
        }
        if (_aspects is not null)
        {
            foreach (var child in _aspects.Children)
            {
                if (child is ImageGenerationChip chip)
                {
                    chip.IsChecked = chip.Id == aspect;
                    chip.IsTabStop = chip.Id == aspect;
                }
            }
        }
    }

    private void OnModelPicked(object? sender, RoutedEventArgs e)
    {
        if (sender is ImageGenerationChip chip)
        {
            SetCurrentValue(SelectedModelIdProperty, chip.Id);
            SyncChips();
        }
    }

    private void OnAspectPicked(object? sender, RoutedEventArgs e)
    {
        if (sender is ImageGenerationChip chip)
        {
            SetCurrentValue(SelectedAspectIdProperty, chip.Id);
            SyncChips();
        }
    }

    #endregion

    #region Generation

    /// <summary>Starts a batch of four from the prompt with the chosen model and shape (stopping one that runs).</summary>
    public void Generate() => StartBatch(null, auto: false);

    /// <summary>Stops the running batch: its unfinished tiles read Stopped and can be retried.</summary>
    public void Stop()
    {
        if (!IsGenerating)
        {
            return;
        }
        _cts?.Cancel();
        foreach (var tile in Tiles)
        {
            if (tile.State == ImageGenerationTileState.Running)
            {
                tile.Halt();
            }
        }
        _stopped = true;
        IsGenerating = false;
        Announce(Labels.StoppedStatus(Count(ImageGenerationTileState.Done), BatchSize));
        UpdateAll();
    }

    /// <summary>Closes the expanded image (cancelling an upscale that runs) and returns to the grid.</summary>
    public void Close()
    {
        if (!IsExpanded || _grid is null)
        {
            return;
        }
        _upscaleCts?.Cancel();
        _grid.Collapse();
        IsExpanded = false;
        UpdateAll();
        if (_selected >= 0 && _selected < Tiles.Count)
        {
            // Closed with a click: the tile takes focus without the keyboard ring.
            Tiles[_selected].Focus(SpecialFocusModality.For(this));
        }
    }

    private void StartBatch(ImageGenerationImage? variationOf, bool auto)
    {
        if (_grid is null || !CanGenerate)
        {
            return;
        }
        var prompt = (Prompt ?? string.Empty).Trim();
        if (prompt.Length == 0)
        {
            _prompt?.Focus();
            return;
        }
        _cts?.Cancel();
        _upscaleCts?.Cancel();
        if (IsExpanded)
        {
            _grid.Collapse();
            IsExpanded = false;
        }
        Select(-1);
        _cts = new CancellationTokenSource();
        var aspect = EffectiveAspect;
        var model = EffectiveModel;
        _batchAspect = aspect;
        _batchModel = model;
        _hasBatch = true;
        _stopped = false;
        _simulated = UsesSimulation;
        _autoBatch = auto;
        var ratio = (double)Math.Max(1, aspect.Height) / Math.Max(1, aspect.Width);
        _grid.SetRatio(ratio, animate: true);
        IsGenerating = true;
        var token = _cts.Token;
        foreach (var tile in _grid.Tiles)
        {
            tile.Reset(ratio);
            var request = new ImageGenerationRequest
            {
                Prompt = prompt,
                Model = model,
                Aspect = aspect,
                Index = tile.Index,
                VariationOf = variationOf,
            };
            tile.Begin(++_attempt, request);
        }
        Announce(Labels.Generating(BatchSize, model.Name));
        UpdateAll();
        foreach (var tile in _grid.Tiles)
        {
            _ = RunTile(tile, tile.Request!, tile.Run, token);
        }
    }

    /// <summary>Runs one tile: reports drive its mosaic; the result, a throw or a cancel settle it.</summary>
    private async Task RunTile(ImageGenerationTile tile, ImageGenerationRequest request, int attempt, CancellationToken token)
    {
        var generator = Generator ?? (Simulation is { } simulation ? simulation.Generate : static (_, _) => Task.FromException<ImageGenerationImage>(new InvalidOperationException()));
        var context = new ImageGenerationContext(token, (progress, preview) =>
        {
            if (tile.Run == attempt)
            {
                tile.Report(progress, preview);
            }
        });
        try
        {
            var image = await generator(request, context);
            if (tile.Run != attempt || tile.State != ImageGenerationTileState.Running)
            {
                return;
            }
            if (token.IsCancellationRequested)
            {
                tile.Halt();
            }
            else
            {
                tile.Finish(image);
            }
        }
        catch (OperationCanceledException)
        {
            if (tile.Run == attempt)
            {
                tile.Halt();
            }
        }
        catch (Exception)
        {
            if (tile.Run == attempt && tile.State == ImageGenerationTileState.Running)
            {
                tile.Fail();
                Announce(Labels.FailedStatus(tile.Index + 1));
            }
        }
        if (tile.Run == attempt)
        {
            OnTileSettled();
        }
    }

    private void OnTileSettled()
    {
        if (!IsGenerating || Count(ImageGenerationTileState.Running) > 0)
        {
            UpdateAll();
            return;
        }
        IsGenerating = false;
        Announce(Labels.Ready(Count(ImageGenerationTileState.Done), BatchSize));
        if (_autoBatch && !_touched && _selected < 0 && Tiles.Count > 0 && Tiles[0].State == ImageGenerationTileState.Done)
        {
            Select(0);
        }
        UpdateAll();
    }

    private void Retry(ImageGenerationTile tile)
    {
        if (tile.Request is not { } request)
        {
            return;
        }
        if (_cts is null || _cts.IsCancellationRequested)
        {
            _cts = new CancellationTokenSource();
        }
        _stopped = false;
        tile.Begin(++_attempt, request);
        IsGenerating = true;
        UpdateAll();
        _ = RunTile(tile, request, tile.Run, _cts.Token);
    }

    private int Count(ImageGenerationTileState state)
    {
        var count = 0;
        foreach (var tile in Tiles)
        {
            if (tile.State == state)
            {
                count++;
            }
        }
        return count;
    }

    #endregion

    #region Selection and actions

    private void OnTileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ImageGenerationTile tile)
        {
            return;
        }
        _touched = true;
        switch (tile.State)
        {
            case ImageGenerationTileState.Failed:
            case ImageGenerationTileState.Stopped:
                Retry(tile);
                break;
            case ImageGenerationTileState.Done when !IsExpanded:
                Select(_selected == tile.Index ? -1 : tile.Index);
                break;
        }
    }

    /// <summary>Picks a tile (−1 clears) and says so.</summary>
    private void Select(int index)
    {
        if (index >= 0 && (index >= Tiles.Count || Tiles[index].State != ImageGenerationTileState.Done))
        {
            index = -1;
        }
        var changed = index != _selected;
        _selected = index;
        for (var i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].IsChecked = i == index;
        }
        var image = index >= 0 ? Tiles[index].Image : null;
        if (changed || !ReferenceEquals(image, SelectedImage))
        {
            SelectedImage = image;
            RaiseEvent(new ImageGenerationSelectionChangedEventArgs(SelectionChangedEvent, image, index));
        }
        UpdateAll();
    }

    private void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (IsGenerating)
        {
            Stop();
        }
        else
        {
            Generate();
        }
    }

    private async void OnUpscaleClick(object? sender, RoutedEventArgs e)
    {
        if (_grid is null || _selected < 0 || _upscaling)
        {
            return;
        }
        var tile = Tiles[_selected];
        if (tile.Image is not { } image)
        {
            return;
        }
        _grid.Expand(_selected);
        IsExpanded = true;
        tile.Focus(SpecialFocusModality.For(this));
        if (tile.IsUpscaled)
        {
            UpdateAll();
            return;
        }
        _upscaleCts?.Cancel();
        var cts = _upscaleCts = new CancellationTokenSource();
        _upscaling = true;
        var number = tile.Index + 1;
        tile.BeginUpscale();
        Announce(Labels.UpscalingStatus(number));
        UpdateAll();
        var upscaler = Upscaler ?? (Simulation is { } simulation ? simulation.Upscale : static (image, _) => Task.FromResult(image));
        var attempt = tile.Run;
        var context = new ImageGenerationContext(cts.Token, (progress, _) =>
        {
            if (tile.Run == attempt && !cts.IsCancellationRequested)
            {
                tile.ReportUpscale(progress);
            }
        });
        try
        {
            var result = await upscaler(image, context);
            if (cts.IsCancellationRequested || tile.Run != attempt)
            {
                tile.CancelUpscale();
            }
            else
            {
                tile.FinishUpscale(result);
                Announce(Labels.UpscaledStatus(number, result.Width, result.Height));
                if (_selected == tile.Index)
                {
                    SelectedImage = result;
                    RaiseEvent(new ImageGenerationSelectionChangedEventArgs(SelectionChangedEvent, result, tile.Index));
                }
            }
        }
        catch (OperationCanceledException)
        {
            tile.CancelUpscale();
        }
        catch (Exception)
        {
            tile.CancelUpscale();
            Announce(Labels.UpscaleFailed(number));
        }
        finally
        {
            if (ReferenceEquals(_upscaleCts, cts))
            {
                _upscaling = false;
            }
            UpdateAll();
        }
    }

    private void OnVaryClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedImage is { } image && !IsGenerating)
        {
            StartBatch(image, auto: false);
        }
    }

    private void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        if (_selected < 0 || Tiles[_selected].Image is not { } image)
        {
            return;
        }
        var name = FileName(Prompt, _selected + 1, image.Width, image.Height);
        RaiseEvent(new ImageGenerationDownloadEventArgs(DownloadRequestedEvent, image, _selected, name));
        Announce(Labels.Downloaded(_selected + 1));
        _downloadFaces?.Show(1);
        UpdateActionNames();
        _savedTimer ??= new DispatcherTimer { Interval = SavedDuration };
        _savedTimer.Stop();
        _savedTimer.Tick -= OnSavedElapsed;
        _savedTimer.Tick += OnSavedElapsed;
        _savedTimer.Start();
    }

    private void OnSavedElapsed(object? sender, EventArgs e)
    {
        _savedTimer?.Stop();
        _downloadFaces?.Show(0);
        UpdateActionNames();
    }

    private void OnGridCollapsed(object? sender, EventArgs e) => UpdateAll();

    /// <summary>A download's file name: the prompt's first words, the image's number and its size.</summary>
    internal static string FileName(string? prompt, int number, int width, int height)
    {
        var slug = new StringBuilder();
        var words = 0;
        var gap = false;
        foreach (var c in (prompt ?? string.Empty).ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (gap && slug.Length > 0)
                {
                    if (++words >= 6)
                    {
                        break;
                    }
                    slug.Append('-');
                }
                gap = false;
                slug.Append(c);
            }
            else
            {
                gap = true;
            }
        }
        var stem = slug.Length > 0 ? slug.ToString() : "image";
        return $"{stem}-{number}-{width}x{height}.png";
    }

    #endregion

    #region State to parts

    private void ApplyLabels()
    {
        var labels = Labels;
        if (_prompt is not null)
        {
            _prompt.Placeholder = Placeholder ?? labels.Placeholder;
            AutomationProperties.SetName(_prompt, labels.Prompt);
        }
        if (_models is not null)
        {
            AutomationProperties.SetName(_models, labels.Models);
        }
        if (_aspects is not null)
        {
            AutomationProperties.SetName(_aspects, labels.AspectRatios);
        }
        _generateFaces?.SetText(0, labels.Generate);
        _generateFaces?.SetText(1, labels.Stop);
        _upscaleFaces?.SetText(0, labels.Upscale);
        _upscaleFaces?.SetText(1, labels.Upscaling);
        _upscaleFaces?.SetText(2, labels.Upscaled);
        _varyFaces?.SetText(0, labels.Vary);
        _downloadFaces?.SetText(0, labels.Download);
        _downloadFaces?.SetText(1, labels.Saved);
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Close);
            Tooltip.SetTip(_close, labels.Close);
        }
        foreach (var tile in Tiles)
        {
            tile.Labels = labels;
        }
        if (_suggestions is not null)
        {
            _suggestions.Children.Clear();
            var heading = new TextBlock
            {
                Text = labels.Suggestions,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12,
            };
            ArcTokens.Bind(heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            _suggestions.Children.Add(heading);
            foreach (var suggestion in EffectiveSuggestions)
            {
                var text = suggestion;
                var button = new ImageGenerationSuggestion { Text = text };
                AutomationProperties.SetName(button, text);
                button.Click += (_, _) =>
                {
                    _touched = true;
                    SetCurrentValue(PromptProperty, text);
                    StartBatch(null, auto: false);
                };
                _suggestions.Children.Add(button);
            }
        }
    }

    /// <summary>Brings the buttons, the footer, the tiles' focus and the suggestions in line with the state.</summary>
    private void UpdateAll()
    {
        PseudoClasses.Set(GeneratingPseudoClass, IsGenerating);
        PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
        var picked = _selected >= 0 && _selected < Tiles.Count ? Tiles[_selected] : null;
        var hasImage = picked?.Image is not null;
        _generateFaces?.Show(IsGenerating ? 1 : 0);
        if (_generate is not null)
        {
            _generate.IsEnabled = IsGenerating || CanGenerate && !string.IsNullOrWhiteSpace(Prompt);
        }
        _upscaleFaces?.Show(_upscaling ? 1 : picked?.IsUpscaled == true ? 2 : 0);
        if (_upscale is not null)
        {
            _upscale.IsEnabled = hasImage && !_upscaling && !(IsExpanded && picked?.IsUpscaled == true);
        }
        if (_vary is not null)
        {
            _vary.IsEnabled = hasImage && !IsGenerating && !_upscaling;
        }
        if (_download is not null)
        {
            _download.IsEnabled = hasImage && !_upscaling;
        }
        UpdateActionNames();
        if (_suggestions is not null)
        {
            _suggestions.IsVisible = !_hasBatch;
        }
        // One tab stop in the grid: the picked tile, else the first one that has something to show.
        var stop = _selected;
        if (stop < 0)
        {
            for (var i = 0; i < Tiles.Count; i++)
            {
                if (Tiles[i].State != ImageGenerationTileState.Idle)
                {
                    stop = i;
                    break;
                }
            }
        }
        var expanded = _grid?.ExpandedIndex ?? -1;
        for (var i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];
            var active = tile.State != ImageGenerationTileState.Idle;
            tile.IsHitTestVisible = active && (expanded < 0 || i == expanded);
            tile.Focusable = active && (expanded < 0 || i == expanded);
            tile.IsTabStop = expanded >= 0 ? i == expanded : i == stop;
        }
        UpdateStatus();
    }

    private void UpdateActionNames()
    {
        if (_generate is not null && _generateFaces is not null)
        {
            AutomationProperties.SetName(_generate, _generateFaces.ShownText);
        }
        if (_upscale is not null && _upscaleFaces is not null)
        {
            AutomationProperties.SetName(_upscale, _upscaleFaces.ShownText);
        }
        if (_vary is not null && _varyFaces is not null)
        {
            AutomationProperties.SetName(_vary, _varyFaces.ShownText);
        }
        if (_download is not null && _downloadFaces is not null)
        {
            AutomationProperties.SetName(_download, _downloadFaces.ShownText);
        }
    }

    /// <summary>The footer's line for the state; it crossfades only when the words change.</summary>
    internal string ComposeStatus()
    {
        var labels = Labels;
        var picked = _selected >= 0 && _selected < Tiles.Count ? Tiles[_selected] : null;
        if (_upscaling && picked is not null)
        {
            return labels.UpscalingStatus(picked.Index + 1);
        }
        if (picked?.Image is { } image)
        {
            return picked.IsUpscaled
                ? labels.UpscaledStatus(picked.Index + 1, image.Width, image.Height)
                : labels.Selected(picked.Index + 1, image.Width, image.Height);
        }
        if (IsGenerating)
        {
            return labels.Generating(BatchSize, (_batchModel ?? EffectiveModel).Name);
        }
        if (!_hasBatch)
        {
            return labels.Empty;
        }
        var done = Count(ImageGenerationTileState.Done);
        return _stopped ? labels.StoppedStatus(done, BatchSize) : labels.Ready(done, BatchSize);
    }

    private void UpdateStatus()
    {
        var text = ComposeStatus();
        if (text == _shownStatus)
        {
            return;
        }
        _shownStatus = text;
        if (_status is not null)
        {
            _status.Content = text;
        }
    }

    /// <summary>Says something in the polite live region, noting simulated results.</summary>
    private void Announce(string text)
    {
        _announcer?.Announce(_simulated ? text + ". " + Labels.Simulated : text);
    }

    #endregion
}
