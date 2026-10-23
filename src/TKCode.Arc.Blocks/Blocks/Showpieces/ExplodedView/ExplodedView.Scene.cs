using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class ExplodedView
{
    private Border? _frame;
    private ExplodedViewScrollViewer? _scroll;
    private ExplodedViewTrack? _track;
    private ExplodedViewLayoutPanel? _layout;
    private Control? _intro;
    private TextBlock? _title;
    private TextBlock? _description;
    private ExplodedViewStage? _stage;
    private ExplodedViewCaptions? _captions;
    private ExplodedViewStops? _stops;
    private ShowpieceLiveRegion? _liveRegion;
    private Panel? _layerList;

    // The follow spring runs in per-mille of progress: MotionValue settles within 0.5 units for moves larger than 5
    // units, which is the measured ≈0.0005 progress snap; in plain progress units it would snap at 0.005 (a visible jump).
    private const double SpringUnits = 1000;

    /// <summary>
    /// How long the layers stay cached after the spring rests: the dimming (480 ms) and outline (240 ms) transitions of a
    /// stop change often run past the spring, and a scroll that resumes soon keeps its bitmaps.
    /// </summary>
    internal static readonly TimeSpan CacheHold = ShowpieceRenderer.CacheHold;

    private MotionValue<double>? _follow;
    private ShowpieceCacheHold? _cacheHold;
    private bool _gpu;
    private bool? _layerCacheOverride;
    private ExplodedViewMetrics _metrics = ExplodedViewLayout.Metrics(AutoWidth);
    private bool _metricsApplied;
    private ArcMotionLevel _level = ArcMotionLevel.Full;
    private bool _levelKnown;
    private bool _stopKnown;
    private readonly List<ExplodedLayer> _observedLayers = [];
    private ExplodedCaption? _observedStart;
    private ExplodedCaption? _observedEnd;

    /// <summary>The internal stage (tests and the snapshot harness).</summary>
    internal ExplodedViewStage? Stage => _stage;

    internal ExplodedViewCaptions? Captions => _captions;

    internal ExplodedViewStops? Stops => _stops;

    internal ExplodedViewScrollViewer? ScrollRegion => _scroll;

    internal ExplodedViewTrack? Track => _track;

    internal ExplodedViewLayoutPanel? LayoutPanel => _layout;

    internal ShowpieceLiveRegion? LiveRegion => _liveRegion;

    internal Panel? LayerList => _layerList;

    internal ExplodedViewMetrics Metrics => _metrics;

    internal bool IsFollowing => _follow?.IsAnimating == true;

    internal ArcMotionLevel EffectiveMotionLevel => _level;

    /// <summary>Tests and snapshots: draw the continuous pose even when motion is reduced or off.</summary>
    internal bool AlwaysContinuous { get; set; }

    /// <summary>Tests and measurements: true or false forces the layer bitmaps on or off; null follows the renderer (GPU only).</summary>
    internal bool? LayerCacheOverride
    {
        get => _layerCacheOverride;
        set
        {
            _layerCacheOverride = value;
            ApplyCacheAllowance();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachScroll();
        if (_stops is not null)
        {
            foreach (var child in _stops.Children)
            {
                if (child is ExplodedViewStopButton button)
                {
                    button.Click -= OnStopClick;
                }
            }
        }
        if (_stage is not null)
        {
            _stage.TallChanged -= OnTallChanged;
            _stage.SetLayers([]);
        }
        _frame = e.NameScope.Find<Border>(FramePart);
        _scroll = e.NameScope.Find<ExplodedViewScrollViewer>(ScrollPart);
        _track = e.NameScope.Find<ExplodedViewTrack>(TrackPart);
        _layout = e.NameScope.Find<ExplodedViewLayoutPanel>(LayoutPart);
        _intro = e.NameScope.Find<Control>(IntroPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _stage = e.NameScope.Find<ExplodedViewStage>(StagePart);
        _captions = e.NameScope.Find<ExplodedViewCaptions>(CaptionsPart);
        _stops = e.NameScope.Find<ExplodedViewStops>(StopsPart);
        _liveRegion = e.NameScope.Find<ShowpieceLiveRegion>(LiveRegionPart);
        _layerList = e.NameScope.Find<Panel>(LayerListPart);
        if (_stage is not null)
        {
            _stage.TallChanged += OnTallChanged;
        }
        ApplyCacheAllowance();
        _metricsApplied = false;
        _stopKnown = false;
        AttachScroll();
        ApplyStageSettings();
        RebuildLayers();
        UpdateIntro();
        UpdateAccessibleNames();
        RefreshMotionLevel();
        UpdateScene();
    }

    // Layers --------------------------------------------------------------------------------------------------------------

    private void OnLayersChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildLayers();

    private void RebuildLayers()
    {
        foreach (var layer in _observedLayers)
        {
            layer.PropertyChanged -= OnLayerPropertyChanged;
        }
        _observedLayers.Clear();
        _observedLayers.AddRange(Layers);
        foreach (var layer in _observedLayers)
        {
            layer.PropertyChanged += OnLayerPropertyChanged;
        }
        var count = Layers.Count;
        StopCount = ExplodedViewProgress.StopCount(count);
        _stage?.SetLayers(_observedLayers);
        if (_layout is not null)
        {
            _layout.StopCount = StopCount;
        }
        RebuildStops();
        RefreshTexts();
        _stopKnown = false;
        UpdateScene();
    }

    private void OnLayerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not ExplodedLayer layer)
        {
            return;
        }
        var index = _observedLayers.IndexOf(layer);
        if (e.Property == ExplodedLayer.ContentProperty || e.Property == ExplodedLayer.ContentTemplateProperty ||
            e.Property == ExplodedLayer.KindProperty || e.Property == ExplodedLayer.AnchorFromBottomProperty ||
            e.Property == ExplodedLayer.NameProperty || e.Property == ExplodedLayer.NoteProperty)
        {
            _stage?.RefreshLayer(index);
        }
        RefreshTexts();
    }

    private void OnCaptionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => RefreshTexts();

    private void ObserveCaptions()
    {
        if (!ReferenceEquals(_observedStart, StartCaption))
        {
            if (_observedStart is not null)
            {
                _observedStart.PropertyChanged -= OnCaptionPropertyChanged;
            }
            _observedStart = StartCaption;
            if (_observedStart is not null)
            {
                _observedStart.PropertyChanged += OnCaptionPropertyChanged;
            }
        }
        if (!ReferenceEquals(_observedEnd, EndCaption))
        {
            if (_observedEnd is not null)
            {
                _observedEnd.PropertyChanged -= OnCaptionPropertyChanged;
            }
            _observedEnd = EndCaption;
            if (_observedEnd is not null)
            {
                _observedEnd.PropertyChanged += OnCaptionPropertyChanged;
            }
        }
    }

    private void RebuildStops()
    {
        if (_stops is null)
        {
            return;
        }
        foreach (var child in _stops.Children)
        {
            if (child is ExplodedViewStopButton old)
            {
                old.Click -= OnStopClick;
            }
        }
        _stops.Children.Clear();
        var count = StopCount;
        for (var stop = 0; stop < count; stop++)
        {
            var button = new ExplodedViewStopButton { Stop = stop, IsEnd = stop == 0 || stop == count - 1 };
            AutomationProperties.SetPositionInSet(button, stop + 1);
            AutomationProperties.SetSizeOfSet(button, count);
            button.Click += OnStopClick;
            _stops.Children.Add(button);
        }
    }

    /// <summary>Captions, stop names, the layer list and the live text follow the layer and caption texts.</summary>
    private void RefreshTexts()
    {
        ObserveCaptions();
        var count = StopCount;
        if (_captions is not null)
        {
            var captions = new (string?, string?)[count];
            for (var stop = 0; stop < count; stop++)
            {
                captions[stop] = GetStopCaption(stop);
            }
            _captions.SetCaptions(captions);
            _captions.Show(-1, animate: false);
            _captions.Show(_currentStop, animate: false);
        }
        if (_stops is not null)
        {
            for (var i = 0; i < _stops.Children.Count; i++)
            {
                AutomationProperties.SetName(_stops.Children[i], GetStopName(i));
            }
            UpdateStopButtons();
        }
        if (_layerList is not null)
        {
            _layerList.Children.Clear();
            foreach (var layer in Layers)
            {
                var text = string.IsNullOrEmpty(layer.Body) ? layer.Name ?? string.Empty : $"{layer.Name}: {layer.Body}";
                _layerList.Children.Add(new TextBlock { Text = text });
            }
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ExplodedViewStopButton button)
        {
            GoToStop(button.Stop);
            e.Handled = true;
        }
    }

    // Settings ------------------------------------------------------------------------------------------------------------

    private void ApplyStageSettings()
    {
        if (_stage is null)
        {
            return;
        }
        _stage.LayerSize = LayerSize;
        _stage.Perspective = Perspective;
        _stage.LayerCornerRadius = LayerCornerRadius;
        _stage.DimmedOpacity = DimmedLayerOpacity;
        _stage.IsCompact = _metrics.IsCompact;
        _stage.SetCompositions(new ExplodedComposition(MaxTilt, MaxTurn, LayerGap),
            new ExplodedComposition(TallMaxTilt, TallMaxTurn, TallLayerGap));
    }

    private void UpdateIntro()
    {
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
        if (_intro is not null)
        {
            _intro.IsVisible = !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Description);
        }
    }

    private void UpdateAccessibleNames()
    {
        if (_scroll is not null)
        {
            AutomationProperties.SetName(_scroll, string.Format(CultureInfo.CurrentCulture, RegionLabelFormat, Title ?? string.Empty).Trim());
        }
        if (_stops is not null)
        {
            AutomationProperties.SetName(_stops, StopsLabel);
        }
        if (_layerList is not null)
        {
            AutomationProperties.SetName(_layerList, LayerListLabel);
        }
    }

    /// <summary>Breakpoint, title size and pseudo-classes for a control width (a container query, applied while measuring).</summary>
    private void ApplyMetrics(double width)
    {
        var metrics = ExplodedViewLayout.Metrics(width);
        if (_metricsApplied && metrics == _metrics)
        {
            return;
        }
        _metricsApplied = true;
        _metrics = metrics;
        PseudoClasses.Set(WidePseudoClass, metrics.IsWide);
        PseudoClasses.Set(CompactPseudoClass, metrics.IsCompact);
        if (!metrics.IsCompact)
        {
            PseudoClasses.Set(TallStagePseudoClass, false);
        }
        if (_title is not null)
        {
            _title.FontSize = metrics.TitleSize;
            _title.LineHeight = metrics.TitleSize * 1.1;
            _title.LetterSpacing = -0.03 * metrics.TitleSize;
        }
        if (_layout is not null)
        {
            _layout.Metrics = metrics;
        }
        if (_stage is not null)
        {
            _stage.IsCompact = metrics.IsCompact;
        }
    }

    private void OnTallChanged(bool tall)
    {
        PseudoClasses.Set(TallStagePseudoClass, tall);
    }

    // Motion --------------------------------------------------------------------------------------------------------------

    private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshMotionLevel();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DetectRenderer();
        AttachPageScroll();
        RefreshMotionLevel();
        // The first frame is always the resting pose of the current progress (no spring from zero).
        JumpFollow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _follow?.Stop();
        _cacheHold?.Cancel();
        if (_stage is not null)
        {
            _stage.IsMoving = false;
        }
        StopScrollMotion();
        DetachPageScroll();
    }

    private void ApplyCacheAllowance()
    {
        if (_stage is not null)
        {
            _stage.AllowCache = _layerCacheOverride ?? _gpu;
        }
    }

    /// <summary>The layer bitmaps are used only when the compositor renders on a GPU (<see cref="ShowpieceRenderer"/>).</summary>
    private void DetectRenderer() => ShowpieceRenderer.Detect(this, ApplyRenderer);

    private void ApplyRenderer(bool gpu)
    {
        _gpu = gpu;
        ApplyCacheAllowance();
    }

    private void RefreshMotionLevel()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_levelKnown && level == _level)
        {
            return;
        }
        _levelKnown = true;
        _level = level;
        if (level != ArcMotionLevel.Full)
        {
            JumpFollow();
            StopScrollMotion();
        }
        UpdateScene();
    }

    private MotionValue<double> Follow
    {
        get
        {
            if (_follow is null)
            {
                // Starts from what is shown, not from the new target.
                _follow = new MotionValue<double>(this, SmoothedProgress * SpringUnits, MotionVectors.Double);
                _follow.Changed += OnFollowChanged;
            }
            return _follow;
        }
    }

    private void OnFollowChanged(double value)
    {
        SmoothedProgress = Math.Clamp(value / SpringUnits, 0, 1);
        UpdateScene();
        // The spring's last step (and every jump) lands exactly on the target.
        SetMoving(_follow is { } follow && !value.Equals(follow.Target));
    }

    /// <summary>
    /// The layers draw from bitmaps while the scene follows the spring and as vectors at rest (<see cref="ExplodedViewStage"/>
    /// explains why). Going back to vectors waits <see cref="CacheHold"/>; code-driven because it follows the spring's state.
    /// </summary>
    private void SetMoving(bool moving)
    {
        if (_stage is null)
        {
            return;
        }
        if (moving)
        {
            _cacheHold?.Cancel();
            _stage.IsMoving = true;
            return;
        }
        if (!_stage.IsMoving)
        {
            return;
        }
        // Nothing to keep without bitmaps (software rendering), or when nothing will animate.
        if (!_stage.AllowCache || _level != ArcMotionLevel.Full || !IsAttachedToVisualTree())
        {
            _cacheHold?.Cancel();
            _stage.IsMoving = false;
            return;
        }
        _cacheHold ??= new ShowpieceCacheHold(this, OnCacheHoldElapsed);
        _cacheHold.Start();
    }

    private void OnCacheHoldElapsed()
    {
        if (_stage is not null)
        {
            _stage.IsMoving = false;
        }
    }

    private void JumpFollow()
    {
        if (_follow is null)
        {
            SmoothedProgress = Progress;
            UpdateScene();
            return;
        }
        _follow.Jump(Progress * SpringUnits);
    }

    /// <summary>
    /// The scene follows the raw progress through an overdamped spring (330 / 60, ζ ≈ 1.6). Code-driven because the geometry
    /// is a function of a scroll-following spring (several smoothsteps and a projection per frame), which no ControlTheme
    /// transition can express. Retargeting keeps the velocity, so reversing mid-way is continuous.
    /// </summary>
    private void FollowProgress()
    {
        if (_level == ArcMotionLevel.Full && IsAttachedToVisualTree())
        {
            _ = Follow.AnimateTo(Progress * SpringUnits, MotionSpec.Spring(ShowpieceSprings.ScrollFollow));
        }
        else
        {
            JumpFollow();
        }
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    /// <summary>Writes one frame: pose, stop, focus. No layout.</summary>
    private void UpdateScene()
    {
        var count = Layers.Count;
        var discrete = _level != ArcMotionLevel.Full && !AlwaysContinuous;
        // Reduced motion decides by the raw progress (the switch happens on the scroll itself, with no spring).
        var progress = discrete ? Progress : SmoothedProgress;
        var (tilt, spread) = ExplodedViewProgress.Pose(progress, count, discrete);
        _stage?.SetPose(tilt, spread);
        PseudoClasses.Set(ExplodedPseudoClass, spread > 0);
        SetStop(ExplodedViewProgress.StopAt(progress, count));
    }

    private void SetStop(int stop)
    {
        var count = Layers.Count;
        var changed = stop != _currentStop;
        if (!changed && _stopKnown)
        {
            return;
        }
        var previous = _currentStop;
        var announce = _stopKnown && changed;
        _stopKnown = true;
        CurrentStop = stop;
        var layerIndex = ExplodedViewProgress.LayerAt(stop, count);
        _stage?.SetActive(layerIndex);
        UpdateStopButtons();
        _captions?.Show(stop, animate: announce && _level == ArcMotionLevel.Full);
        if (announce)
        {
            var (title, body) = GetStopCaption(stop);
            _liveRegion?.Announce(string.IsNullOrEmpty(body) ? title : $"{title}. {body}");
            RaiseEvent(new ExplodedStopChangedEventArgs(StopChangedEvent, stop, previous,
                layerIndex >= 0 ? Layers[layerIndex] : null, layerIndex));
        }
    }

    private void UpdateStopButtons()
    {
        if (_stops is null)
        {
            return;
        }
        foreach (var child in _stops.Children)
        {
            if (child is ExplodedViewStopButton button)
            {
                var current = button.Stop == _currentStop;
                button.IsCurrent = current;
                AutomationProperties.SetItemStatus(button, current ? CurrentStopText : null);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ProgressProperty)
        {
            OnProgressChanged();
        }
        else if (property == TitleProperty)
        {
            UpdateIntro();
            UpdateAccessibleNames();
        }
        else if (property == DescriptionProperty)
        {
            UpdateIntro();
        }
        else if (property == StartCaptionProperty || property == EndCaptionProperty || property == AssembledTextProperty ||
                 property == ReassembledTextProperty)
        {
            RefreshTexts();
        }
        else if (property == RegionLabelFormatProperty || property == StopsLabelProperty || property == LayerListLabelProperty)
        {
            UpdateAccessibleNames();
        }
        else if (property == CurrentStopTextProperty)
        {
            UpdateStopButtons();
        }
        else if (property == LayerSizeProperty || property == PerspectiveProperty || property == LayerCornerRadiusProperty ||
                 property == DimmedLayerOpacityProperty || property == MaxTiltProperty || property == MaxTurnProperty ||
                 property == LayerGapProperty || property == TallMaxTiltProperty || property == TallMaxTurnProperty ||
                 property == TallLayerGapProperty)
        {
            ApplyStageSettings();
            UpdateScene();
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (property == ScrollModeProperty)
        {
            DetachScroll();
            AttachScroll();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            RefreshMotionLevel();
        }
    }
}
