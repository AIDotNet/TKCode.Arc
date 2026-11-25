using System.Numerics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Graphics3D;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Data of <see cref="ModelViewer.LoadFailed"/>.</summary>
public sealed class ModelLoadFailedEventArgs : EventArgs
{
    internal ModelLoadFailedEventArgs(Exception exception) => Exception = exception;

    /// <summary>What went wrong.</summary>
    public Exception Exception { get; }
}

/// <summary>
/// Shows one 3D model with everything a product view needs: studio lighting, a soft ground shadow, orbit / zoom / pan with
/// the pointer, auto-rotation and a reset button. Give it a glTF file (<see cref="Source"/>, <see cref="LoadAsync(Stream, CancellationToken)"/>)
/// or any object built in code (<see cref="Model"/>); it frames the model by itself.
/// </summary>
/// <remarks>
/// The viewer draws on demand: a still model costs nothing. It keeps drawing while the camera moves, while auto-rotation is on
/// (only at full motion: with reduced motion the model stands still) and while an animation plays. For a scene of your own with
/// several objects, cameras or custom lighting use <see cref="Viewport3D"/> directly.
/// </remarks>
[TemplatePart(ViewportPart, typeof(Viewport3D))]
[TemplatePart(ResetButtonPart, typeof(AvaloniaButton))]
[TemplatePart(AutoRotateButtonPart, typeof(AvaloniaButton))]
[PseudoClasses(LoadingPseudoClass, ErrorPseudoClass, EmptyPseudoClass, AutoRotatePseudoClass, ToolbarPseudoClass)]
public class ModelViewer : TemplatedControl
{
    public const string ViewportPart = "PART_Viewport";
    public const string ResetButtonPart = "PART_Reset";
    public const string AutoRotateButtonPart = "PART_AutoRotate";
    public const string LoadingPseudoClass = ":loading";
    public const string ErrorPseudoClass = ":error";
    public const string EmptyPseudoClass = ":empty";
    public const string AutoRotatePseudoClass = ":autorotate";
    public const string ToolbarPseudoClass = ":toolbar";

    /// <summary>Where the glTF 2.0 model (<c>.gltf</c> or <c>.glb</c>) to show comes from: a file path, an
    /// <c>avares://</c> resource or an <c>http(s)://</c> address. Loading runs off the UI thread.</summary>
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<ModelViewer, string?>(nameof(Source));

    /// <summary>An object to show instead of a file: anything built with the engine. The viewer does not dispose it.</summary>
    public static readonly StyledProperty<Object3D?> ModelProperty =
        AvaloniaProperty.Register<ModelViewer, Object3D?>(nameof(Model));

    /// <summary>True turns the model slowly about its vertical axis while nobody is dragging it. It stands still under
    /// reduced motion.</summary>
    public static readonly StyledProperty<bool> AutoRotateProperty =
        AvaloniaProperty.Register<ModelViewer, bool>(nameof(AutoRotate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Auto-rotation speed in degrees per second (default 12: one turn in 30 seconds).</summary>
    public static readonly StyledProperty<double> AutoRotateSpeedProperty =
        AvaloniaProperty.Register<ModelViewer, double>(nameof(AutoRotateSpeed), 12d);

    /// <summary>The surroundings that light the model and show in its reflections.</summary>
    public static readonly StyledProperty<ModelViewerEnvironment> EnvironmentProperty =
        AvaloniaProperty.Register<ModelViewer, ModelViewerEnvironment>(nameof(Environment));

    /// <summary>Multiplies the environment's light.</summary>
    public static readonly StyledProperty<double> EnvironmentIntensityProperty =
        AvaloniaProperty.Register<ModelViewer, double>(nameof(EnvironmentIntensity), 1d);

    /// <summary>Darkness of the shadow on the ground under the model, 0 (none) to 1.</summary>
    public static readonly StyledProperty<double> ShadowIntensityProperty =
        AvaloniaProperty.Register<ModelViewer, double>(nameof(ShadowIntensity), 0.35d);

    /// <summary>Exposure multiplier applied before tone mapping.</summary>
    public static readonly StyledProperty<double> ExposureProperty =
        AvaloniaProperty.Register<ModelViewer, double>(nameof(Exposure), 1d);

    /// <summary>How bright values are compressed into the displayable range.</summary>
    public static readonly StyledProperty<ToneMapping> ToneMappingProperty =
        AvaloniaProperty.Register<ModelViewer, ToneMapping>(nameof(ToneMapping), ToneMapping.Neutral);

    /// <summary>False fixes the camera: the pointer, wheel and arrow keys do nothing.</summary>
    public static readonly StyledProperty<bool> CameraControlsEnabledProperty =
        AvaloniaProperty.Register<ModelViewer, bool>(nameof(CameraControlsEnabled), true);

    /// <summary>True draws the model on a render thread where the viewport can (see
    /// <see cref="Viewport3D.UseRenderThread"/>); set it before the viewer joins a window.</summary>
    public static readonly StyledProperty<bool> UseRenderThreadProperty =
        Viewport3D.UseRenderThreadProperty.AddOwner<ModelViewer>();

    /// <summary>False hides the reset and auto-rotate buttons.</summary>
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<ModelViewer, bool>(nameof(ShowToolbar), true);

    /// <summary>Accessible name and tooltip of the reset button; null (default) uses the built-in text in the app's language.</summary>
    public static readonly StyledProperty<string?> ResetViewLabelProperty =
        AvaloniaProperty.Register<ModelViewer, string?>(nameof(ResetViewLabel));

    /// <summary>Accessible name and tooltip of the auto-rotate button; null (default) uses the built-in text in the app's language.</summary>
    public static readonly StyledProperty<string?> AutoRotateLabelProperty =
        AvaloniaProperty.Register<ModelViewer, string?>(nameof(AutoRotateLabel));

    /// <summary>True while a file is being read.</summary>
    public static readonly DirectProperty<ModelViewer, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<ModelViewer, bool>(nameof(IsLoading), static v => v.IsLoading);

    /// <summary>Why the last load failed, or null.</summary>
    public static readonly DirectProperty<ModelViewer, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<ModelViewer, string?>(nameof(ErrorMessage), static v => v.ErrorMessage);

    private readonly Scene _scene = new();
    private readonly PerspectiveCamera _camera = new(35f, 1f, 0.05f, 2000f) { Position = new Vector3(0f, 0.8f, 4f) };
    private readonly OrbitControls _controls;
    private readonly Group _modelRoot = new() { Name = "model" };
    private readonly DirectionalLight _key = new(2.2f) { Position = new Vector3(2.5f, 5f, 3.5f), CastShadow = true };
    private readonly HemisphereLight _fill = new(ColorF.FromHex(0xffffff), ColorF.FromHex(0x8a8580), 0.5f);
    private readonly Mesh _ground;
    private readonly ShadowMaterial _groundMaterial = new();
    private Viewport3D? _viewport;
    private AvaloniaButton? _resetButton;
    private AvaloniaButton? _autoRotateButton;
    private AnimationMixer? _mixer;
    private GltfAsset? _ownedAsset;
    private IReadOnlyList<AnimationClip> _animations = [];
    private CancellationTokenSource? _loading;
    private bool _isLoading;
    private string? _errorMessage;
    private bool _playing;
    private bool _ownsAutomationName;
    private ArcLanguageManager? _languages;

    /// <summary>Creates an empty viewer.</summary>
    public ModelViewer()
    {
        _controls = new OrbitControls(_camera) { EnableDamping = true };
        _key.Shadow.AutoFit = true;
        _key.Shadow.MapSize = 2048;
        _ground = new Mesh(new PlaneGeometry(1f, 1f), _groundMaterial) { ReceiveShadow = true, Name = "ground", Visible = false };
        _ground.RotateX(-MathF.PI / 2f);
        _scene.Add(_modelRoot, _key, _fill, _ground);
        _scene.Environment = ModelViewerEnvironments.Get(ModelViewerEnvironment.Studio);
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    /// <summary>Raised when a model was loaded or set and is on screen.</summary>
    public event EventHandler? ModelLoaded;

    /// <summary>Raised when loading <see cref="Source"/> or a stream failed; <see cref="ErrorMessage"/> has the text.</summary>
    public event EventHandler<ModelLoadFailedEventArgs>? LoadFailed;

    /// <inheritdoc cref="SourceProperty"/>
    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <inheritdoc cref="ModelProperty"/>
    public Object3D? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <inheritdoc cref="AutoRotateProperty"/>
    public bool AutoRotate
    {
        get => GetValue(AutoRotateProperty);
        set => SetValue(AutoRotateProperty, value);
    }

    /// <inheritdoc cref="AutoRotateSpeedProperty"/>
    public double AutoRotateSpeed
    {
        get => GetValue(AutoRotateSpeedProperty);
        set => SetValue(AutoRotateSpeedProperty, value);
    }

    /// <inheritdoc cref="EnvironmentProperty"/>
    public ModelViewerEnvironment Environment
    {
        get => GetValue(EnvironmentProperty);
        set => SetValue(EnvironmentProperty, value);
    }

    /// <inheritdoc cref="EnvironmentIntensityProperty"/>
    public double EnvironmentIntensity
    {
        get => GetValue(EnvironmentIntensityProperty);
        set => SetValue(EnvironmentIntensityProperty, value);
    }

    /// <inheritdoc cref="ShadowIntensityProperty"/>
    public double ShadowIntensity
    {
        get => GetValue(ShadowIntensityProperty);
        set => SetValue(ShadowIntensityProperty, value);
    }

    /// <inheritdoc cref="ExposureProperty"/>
    public double Exposure
    {
        get => GetValue(ExposureProperty);
        set => SetValue(ExposureProperty, value);
    }

    /// <inheritdoc cref="ToneMappingProperty"/>
    public ToneMapping ToneMapping
    {
        get => GetValue(ToneMappingProperty);
        set => SetValue(ToneMappingProperty, value);
    }

    /// <inheritdoc cref="CameraControlsEnabledProperty"/>
    public bool CameraControlsEnabled
    {
        get => GetValue(CameraControlsEnabledProperty);
        set => SetValue(CameraControlsEnabledProperty, value);
    }

    /// <inheritdoc cref="UseRenderThreadProperty"/>
    public bool UseRenderThread
    {
        get => GetValue(UseRenderThreadProperty);
        set => SetValue(UseRenderThreadProperty, value);
    }

    /// <inheritdoc cref="ShowToolbarProperty"/>
    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <inheritdoc cref="ResetViewLabelProperty"/>
    public string? ResetViewLabel
    {
        get => GetValue(ResetViewLabelProperty);
        set => SetValue(ResetViewLabelProperty, value);
    }

    /// <inheritdoc cref="AutoRotateLabelProperty"/>
    public string? AutoRotateLabel
    {
        get => GetValue(AutoRotateLabelProperty);
        set => SetValue(AutoRotateLabelProperty, value);
    }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetAndRaise(IsLoadingProperty, ref _isLoading, value))
            {
                PseudoClasses.Set(LoadingPseudoClass, value);
                ApplyLabels();
            }
        }
    }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetAndRaise(ErrorMessageProperty, ref _errorMessage, value))
            {
                PseudoClasses.Set(ErrorPseudoClass, value is not null);
            }
        }
    }

    /// <summary>The animations of the loaded file.</summary>
    public IReadOnlyList<AnimationClip> Animations => _animations;

    /// <summary>Effects applied to the finished picture (bloom, vignette, saturation, contrast, FXAA); null until the
    /// template is applied. Call <see cref="Viewport3D.RequestRender"/> on the viewport, or change any viewer property,
    /// after changing a setting.</summary>
    public PostProcessing? PostProcessing => _viewport?.PostProcessing;

    /// <summary>The viewer's scene: add lights or objects of your own next to the model.</summary>
    public Scene Scene => _scene;

    /// <summary>The viewer's camera.</summary>
    public PerspectiveCamera Camera => _camera;

    /// <summary>The viewer's camera controls (limits, speeds, damping).</summary>
    public OrbitControls CameraControls => _controls;

    /// <summary>Loads a glTF file from a stream and shows it. External resources of a <c>.gltf</c> cannot be resolved
    /// from a stream: use a <c>.glb</c> or data URIs.</summary>
    public async Task LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        await LoadCoreAsync(token => GltfLoader.LoadAsync(buffer.ToArray(), null, token), cancellationToken);
    }

    /// <summary>Puts the camera back where it framed the whole model.</summary>
    public void ResetView()
    {
        _controls.Reset();
        _viewport?.RequestRender();
    }

    /// <summary>Plays one of <see cref="Animations"/> in a loop (by index), stopping any other.</summary>
    public void PlayAnimation(int index = 0)
    {
        if (_mixer is null || index < 0 || index >= _animations.Count)
        {
            return;
        }

        _mixer.StopAllActions();
        _mixer.ClipAction(_animations[index]).Reset().Play();
        _playing = true;
        _viewport?.RequestRender();
    }

    /// <summary>Stops the playing animation; the model returns to its rest pose.</summary>
    public void StopAnimation()
    {
        _mixer?.StopAllActions();
        _playing = false;
        _viewport?.RequestRender();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _viewport?.FrameRendering -= OnFrameRendering;

        _resetButton?.Click -= OnResetClick;

        _autoRotateButton?.Click -= OnAutoRotateClick;

        _viewport = e.NameScope.Find<Viewport3D>(ViewportPart);
        _resetButton = e.NameScope.Find<AvaloniaButton>(ResetButtonPart);
        _autoRotateButton = e.NameScope.Find<AvaloniaButton>(AutoRotateButtonPart);
        if (_viewport is not null)
        {
            _viewport.Scene = _scene;
            _viewport.Camera = _camera;
            _viewport.FrameRendering += OnFrameRendering;
            ApplyViewportSettings();
        }

        _resetButton?.Click += OnResetClick;

        _autoRotateButton?.Click += OnAutoRotateClick;

        ApplyLabels();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            OnSourceChanged(change.GetNewValue<string?>());
        }
        else if (change.Property == ModelProperty)
        {
            if (change.GetNewValue<Object3D?>() is { } model)
            {
                CancelLoading();
                ShowModel(model, [], null);
            }
            else if (Source is null)
            {
                ClearModel();
            }
        }
        else if (change.Property == AutoRotateProperty)
        {
            PseudoClasses.Set(AutoRotatePseudoClass, AutoRotate);
            _viewport?.RequestRender();
        }
        else if (change.Property == EnvironmentProperty || change.Property == EnvironmentIntensityProperty
            || change.Property == ShadowIntensityProperty || change.Property == ExposureProperty || change.Property == ToneMappingProperty
            || change.Property == CameraControlsEnabledProperty || change.Property == AutoRotateSpeedProperty)
        {
            ApplyViewportSettings();
        }
        else if (change.Property == ResetViewLabelProperty || change.Property == AutoRotateLabelProperty)
        {
            ApplyLabels();
        }
        else if (change.Property == ShowToolbarProperty)
        {
            UpdateToolbar();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }

        CancelLoading();
    }

    // The toolbar shows when it is wanted and there is a model to act on.
    private void UpdateToolbar() => PseudoClasses.Set(ToolbarPseudoClass, ShowToolbar && _modelRoot.Children.Count > 0);

    private void ApplyLabels()
    {
        if (_resetButton is not null)
        {
            var label = ResetViewLabel ?? ModelViewerLangResources.Get(ModelViewerLangResourceKind.ResetView);
            AutomationProperties.SetName(_resetButton, label);
            ToolTip.SetTip(_resetButton, label);
        }

        if (_autoRotateButton is not null)
        {
            var label = AutoRotateLabel ?? ModelViewerLangResources.Get(ModelViewerLangResourceKind.AutoRotate);
            AutomationProperties.SetName(_autoRotateButton, label);
            ToolTip.SetTip(_autoRotateButton, label);
        }

        // The viewer's own accessible name, unless the app gave it one; while a file loads it says so.
        if (!IsSet(AutomationProperties.NameProperty) || _ownsAutomationName)
        {
            _ownsAutomationName = true;
            AutomationProperties.SetName(this, ModelViewerLangResources.Get(IsLoading ? ModelViewerLangResourceKind.Loading : ModelViewerLangResourceKind.ModelViewer));
        }
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) => ApplyLabels();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _languages = ArcLanguageManager.Current;
        _languages?.Changed += OnLanguageChanged;

        ApplyLabels();
    }

    private void ApplyViewportSettings()
    {
        _scene.Environment = ModelViewerEnvironments.Get(Environment);
        _scene.EnvironmentIntensity = (float)Math.Max(0d, EnvironmentIntensity);

        // Without an environment the two lights carry the scene alone.
        var lit = Environment == ModelViewerEnvironment.None;
        _key.Intensity = lit ? 3f : 1.2f;
        _fill.Intensity = lit ? 1.4f : 0.15f;
        var shadow = (float)Math.Clamp(ShadowIntensity, 0d, 1d);
        _groundMaterial.Opacity = shadow;
        _key.CastShadow = shadow > 0f;
        _controls.Enabled = CameraControlsEnabled;

        // OrbitControls counts in three.js units: 2 is one turn in 30 seconds, i.e. 12 degrees per second.
        _controls.AutoRotateSpeed = (float)(AutoRotateSpeed / 6d);
        if (_viewport is not null)
        {
            _viewport.CameraControls = _controls;
            _viewport.Exposure = Exposure;
            _viewport.ToneMapping = ToneMapping;
            _viewport.RequestRender();
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e) => ResetView();

    private void OnAutoRotateClick(object? sender, RoutedEventArgs e) => SetCurrentValue(AutoRotateProperty, !AutoRotate);

    private void OnFrameRendering(object? sender, Viewport3DFrameEventArgs e)
    {
        // Rotation is decoration: it runs only at full motion.
        _controls.AutoRotate = AutoRotate && ArcMotion.IsFull(this);
        if (_controls.AutoRotate)
        {
            e.RequestNextFrame = true;
        }

        if (_playing && _mixer is not null)
        {
            _mixer.Update((float)e.Elapsed.TotalSeconds);
            e.RequestNextFrame = true;
        }
    }

    private void CancelLoading()
    {
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
        IsLoading = false;
    }

    private async void OnSourceChanged(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            CancelLoading();
            if (Model is null)
            {
                ClearModel();
            }

            return;
        }

        Func<CancellationToken, Task<GltfAsset>> load;
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.IsFile)
        {
            var path = uri is { IsFile: true } ? uri.LocalPath : source;
            load = token => GltfLoader.LoadAsync(path, token);
        }
        else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            load = token => LoadFromWebAsync(uri, token);
        }
        else if (uri.Scheme == "avares")
        {
            load = token => LoadFromResourcesAsync(uri, token);
        }
        else
        {
            load = _ => Task.FromException<GltfAsset>(new NotSupportedException($"Models cannot be loaded from '{uri.Scheme}:' addresses."));
        }

        try
        {
            await LoadCoreAsync(load, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Another source took over.
        }
    }

    // One client for every viewer: connections are pooled per host.
    private static readonly Lazy<HttpClient> s_http = new(static () => new HttpClient { Timeout = TimeSpan.FromSeconds(100) });

    private static async Task<GltfAsset> LoadFromWebAsync(Uri uri, CancellationToken cancellationToken)
    {
        var data = await s_http.Value.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
        return await GltfLoader.LoadAsync(data, Resolve, cancellationToken).ConfigureAwait(false);

        // Buffers and images next to the model. The loader asks from its worker thread, so waiting here blocks no UI.
        // A file on the web may only pull in more of the web, never a local file.
        byte[]? Resolve(string relative)
        {
            if (!Uri.TryCreate(uri, relative, out var target) || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            using var response = s_http.Value.Send(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var content = response.Content.ReadAsStream(cancellationToken);
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            return buffer.ToArray();
        }
    }

    private static Task<GltfAsset> LoadFromResourcesAsync(Uri uri, CancellationToken cancellationToken)
    {
        byte[] data;
        try
        {
            data = ReadResource(uri);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Task.FromException<GltfAsset>(new FileNotFoundException($"The resource '{uri}' was not found.", ex));
        }

        return GltfLoader.LoadAsync(
            data,
            relative => Uri.TryCreate(uri, relative, out var target) && target.Scheme == "avares" && Avalonia.Platform.AssetLoader.Exists(target) ? ReadResource(target) : null,
            cancellationToken);
    }

    private static byte[] ReadResource(Uri uri)
    {
        using var stream = Avalonia.Platform.AssetLoader.Open(uri);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private async Task LoadCoreAsync(Func<CancellationToken, Task<GltfAsset>> load, CancellationToken cancellationToken)
    {
        CancelLoading();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // CancelLoading disposes cts when another source takes over, and a disposed source throws on its Token:
        // keep the token of this load for the checks after the await.
        var token = cts.Token;
        _loading = cts;
        ErrorMessage = null;
        IsLoading = true;
        GltfAsset asset;
        try
        {
            asset = await load(token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ReferenceEquals(_loading, cts))
            {
                _loading = null;
                cts.Dispose();
                IsLoading = false;
                ErrorMessage = ex.Message;
                LoadFailed?.Invoke(this, new ModelLoadFailedEventArgs(ex));
            }

            return;
        }

        if (!ReferenceEquals(_loading, cts) || token.IsCancellationRequested)
        {
            asset.Dispose();
            token.ThrowIfCancellationRequested();
            return;
        }

        _loading = null;
        cts.Dispose();
        IsLoading = false;
        ShowModel(asset.Scene, asset.Animations, asset);
    }

    private void ClearModel()
    {
        _mixer?.StopAllActions();
        _mixer = null;
        _playing = false;
        _animations = [];
        _modelRoot.Clear();
        _ownedAsset?.Dispose();
        _ownedAsset = null;
        _ground.Visible = false;
        PseudoClasses.Set(EmptyPseudoClass, true);
        UpdateToolbar();
        _viewport?.RequestRender();
    }

    private void ShowModel(Object3D model, IReadOnlyList<AnimationClip> animations, GltfAsset? owned)
    {
        ClearModel();
        _ownedAsset = owned;
        _animations = animations;
        _mixer = animations.Count > 0 ? new AnimationMixer() : null;
        ErrorMessage = null;
        model.Traverse(static o =>
        {
            if (o is Renderable)
            {
                o.CastShadow = true;
                o.ReceiveShadow = true;
            }
        });
        _modelRoot.Add(model);
        PseudoClasses.Set(EmptyPseudoClass, false);
        UpdateToolbar();
        FrameModel();
        ModelLoaded?.Invoke(this, EventArgs.Empty);
        _viewport?.RequestRender();
    }

    /// <summary>Puts the ground under the model, aims the light and frames the camera on it.</summary>
    private void FrameModel()
    {
        _ground.Visible = false;
        var box = _scene.ComputeBounds();
        if (box.IsEmpty)
        {
            return;
        }

        var size = box.Size;
        var extent = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        var center = box.Center;
        _ground.Position = new Vector3(center.X, box.Min.Y, center.Z);
        _ground.Scale = new Vector3(extent * 6f, extent * 6f, 1f);
        _ground.Visible = true;
        _key.Position = center + (new Vector3(0.45f, 0.9f, 0.6f) * extent * 3f);
        _key.Target = center;
        _key.Shadow.NormalBias = extent * 0.004f;
        _camera.Near = MathF.Max(extent * 0.01f, 1e-4f);
        _camera.Far = extent * 200f;
        _controls.MinDistance = extent * 0.2f;
        _controls.MaxDistance = extent * 20f;

        // A three-quarter view from slightly above, then back off until the whole box is in view.
        _controls.Target = center;
        _camera.Position = center + (Vector3.Normalize(new Vector3(0.55f, 0.35f, 1f)) * extent * 2f);
        _camera.LookAt(center);
        _controls.FitToBounds(box, 1.25f);
        _controls.SaveState();
    }
}
