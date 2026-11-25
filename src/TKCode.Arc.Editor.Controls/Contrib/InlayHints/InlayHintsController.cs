// Ported from vscode (src/vs/editor/contrib/inlayHints/browser/inlayHintsController.ts and inlayHintsHover.ts, commit
// b57d5c9e), MIT license.
//
// Differences: the decorations are built by the engine's InlayHintsDecorations; the hints are not cached across
// models (an editor has one model); Ctrl+Alt is read from the editor's key events (upstream: a window-wide modifier
// key emitter); the tooltip of the label part under the pointer (or of the hint) is shown in an Arc Tooltip bubble as
// a content widget (upstream: a hover participant of the content hover); Ctrl/Cmd+click runs a part's command and a
// double click applies the hint's text edits; going to a part's location and its context menu are not ported. The
// debounce is the fixed minimum, 25 ms (upstream: the language feature debounce service). Settings are properties of
// the controller (upstream: editor.inlayHints.enabled / maximumLength / padding).

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.InlayHints;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

/// <summary>Asks the inlay hint providers and shows the hints as injected text (upstream's InlayHintsController).</summary>
internal sealed class InlayHintsController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.InlayHints";

    public const int MinDebounceDelay = 25;

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new InlayHintsController(editor), EditorContributionInstantiation.AfterFirstRender);

    private readonly CodeEditor _editor;
    private readonly Dictionary<string, InlayHintDecoration> _decorationsMetadata = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _styles = new();
    private readonly List<(IInlayHintsProvider Provider, Action Handler)> _watchedProviders = new();
    private readonly TooltipWidget _tooltip = new();
    private InlayHintsEnabled _enabled = InlayHintsEnabled.On;
    private bool _visible = true;
    private bool _sessionActive;
    private IDisposable? _timer;
    private IDisposable? _cursorTimer;
    private CancellationTokenSource? _cts;
    private RenderedInlayHintLabelPart? _activePart;
    private RenderedInlayHintLabelPart? _hoveredPart;
    private bool _disposed;

    private InlayHintsController(CodeEditor editor)
    {
        _editor = editor;
        // upstream's _fillInColors: editorInlayHint foreground / background (the parameter and type colors default to them)
        _styles.Add(editor.DecorationStyles.Register(InlayHintsDecorations.ClassName, new CodeEditorDecorationStyle
        {
            Foreground = CodeEditorThemeColor.InlayHintForeground,
            InlineBackground = CodeEditorThemeColor.InlayHintBackground,
            InlineCornerRadius = 3,
        }));
        _styles.Add(editor.DecorationStyles.Register(InlayHintsDecorations.ActiveLinkClassName, new CodeEditorDecorationStyle
        {
            Foreground = CodeEditorThemeColor.Link,
            FontStyle = TKCode.Arc.Editor.Tokens.FontStyle.Underline,
        }));

        editor.LanguageFeatures.InlayHintsProvider.DidChange += OnRegistryChanged;
        editor.DidChangeModelLanguage += OnLanguageChanged;
        editor.DidScrollChange += OnScroll;
        editor.DidChangeModelContent += OnContentChanged;
        editor.MouseMove += OnMouseMove;
        editor.MouseDown += OnMouseDown;
        editor.MouseLeave += OnMouseLeave;
        editor.AddHandler(InputElement.KeyDownEvent, OnModifierKey, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        editor.AddHandler(InputElement.KeyUpEvent, OnModifierKey, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Update();
    }

    public static InlayHintsController? Get(CodeEditor editor) => editor.GetContribution<InlayHintsController>(Id);

    /// <summary>Upstream's editor.inlayHints.enabled.</summary>
    public InlayHintsEnabled Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                Update();
            }
        }
    }

    /// <summary>Upstream's editor.inlayHints.maximumLength (0: no limit).</summary>
    public int MaximumLength { get; set; } = 43;

    /// <summary>The hints shown now, by decoration id (for tests).</summary>
    internal IReadOnlyDictionary<string, InlayHintDecoration> Decorations => _decorationsMetadata;

    /// <summary>Whether the hints are drawn now (Ctrl+Alt flips it in the "unless pressed" modes).</summary>
    internal bool IsVisible => _visible;

    /// <summary>The tooltip widget (for tests).</summary>
    internal ICodeEditorContentWidget Tooltip => _tooltip;

    // ---- the session (upstream's _update)

    private void Update()
    {
        EndSession();
        RemoveAllDecorations();
        if (_enabled == InlayHintsEnabled.Off || _disposed)
        {
            return;
        }

        var model = _editor.Model;
        if (!_editor.LanguageFeatures.InlayHintsProvider.Has(model))
        {
            return;
        }

        _visible = _enabled is InlayHintsEnabled.On or InlayHintsEnabled.OnUnlessPressed;
        _sessionActive = true;
        Schedule(0);
    }

    private void EndSession()
    {
        _sessionActive = false;
        _timer?.Dispose();
        _timer = null;
        _cursorTimer?.Dispose();
        _cursorTimer = null;
        _cts?.Cancel();
        _cts = null;
        UnwatchProviders();
    }

    private void Schedule(int delay = MinDebounceDelay)
    {
        if (!_sessionActive)
        {
            return;
        }

        _timer?.Dispose();
        _timer = CodeEditor.IdleScheduler.RunAfter(TimeSpan.FromMilliseconds(delay), () =>
        {
            _timer = null;
            _ = RefreshNow();
        });
    }

    /// <summary>Asks the providers for the visible ranges now and shows the answer (the scheduler's run).</summary>
    internal async Task RefreshNow()
    {
        if (!_sessionActive || _editor.ViewModel is not { } viewModel)
        {
            return;
        }

        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var model = _editor.Model;
        try
        {
            var ranges = InlayHintsDecorations.GetHintsRanges(model, viewModel.GetVisibleRangesPlusViewportAboveBelow());
            var inlayHints = await InlayHintsFragments.Create(_editor.LanguageFeatures.InlayHintsProvider, model, ranges, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested || !_sessionActive)
            {
                inlayHints.Dispose();
                return;
            }

            // listen to provider changes
            foreach (var provider in inlayHints.Providers)
            {
                if (_watchedProviders.All(w => !ReferenceEquals(w.Provider, provider)))
                {
                    Action handler = () =>
                    {
                        // ignore event when request is already scheduled
                        if (_timer is null)
                        {
                            Schedule();
                        }
                    };
                    provider.DidChangeInlayHints += handler;
                    _watchedProviders.Add((provider, handler));
                }
            }

            UpdateHintsDecorators(inlayHints.Ranges, inlayHints.Items);
            inlayHints.Dispose();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception err)
        {
            UnexpectedErrors.Report(err);
        }
    }

    private void UnwatchProviders()
    {
        foreach (var (provider, handler) in _watchedProviders)
        {
            provider.DidChangeInlayHints -= handler;
        }

        _watchedProviders.Clear();
    }

    private void UpdateHintsDecorators(IReadOnlyList<TextRange> ranges, IReadOnlyList<InlayHintItem> items)
    {
        var model = _editor.Model;
        var newDecorationsData = InlayHintsDecorations.Create(items, _visible, MaximumLength, _activePart);

        // collect all decoration ids that are affected by the ranges and only update those decorations
        var decorationIdsToReplace = new List<string>();
        foreach (var (id, _) in _decorationsMetadata.ToList())
        {
            if (model.GetDecorationRange(id) is { } range && ranges.Any(r => r.ContainsRange(range)))
            {
                decorationIdsToReplace.Add(id);
                _decorationsMetadata.Remove(id);
            }
        }

        var newDecorationIds = model.DeltaDecorations(decorationIdsToReplace, newDecorationsData.Select(static d => d.Decoration).ToArray());
        for (var i = 0; i < newDecorationIds.Length; i++)
        {
            _decorationsMetadata[newDecorationIds[i]] = newDecorationsData[i];
        }
    }

    // the hints with an anchor that reflects the edits made since they came (upstream's _copyInlayHintsWithCurrentAnchor)
    private List<InlayHintItem> CopyInlayHintsWithCurrentAnchor()
    {
        var items = new Dictionary<InlayHintItem, InlayHintItem>(ReferenceEqualityComparer.Instance);
        foreach (var (id, data) in _decorationsMetadata)
        {
            if (items.ContainsKey(data.Item))
            {
                // an inlay item can be rendered as multiple decorations but they will all uses the same range
                continue;
            }

            if (_editor.Model.GetDecorationRange(id) is { } range)
            {
                items[data.Item] = data.Item.With(new InlayHintAnchor(range, data.Item.Anchor.Direction));
            }
        }

        return [.. items.Values.OrderBy(static i => i.Anchor.Range.Start)];
    }

    private void RemoveAllDecorations()
    {
        if (!_editor.Model.IsDisposed())
        {
            _editor.Model.DeltaDecorations(_decorationsMetadata.Keys.ToArray(), []);
        }

        _decorationsMetadata.Clear();
    }

    // ---- events

    private void OnRegistryChanged(int _) => Update();

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => Update();

    private void OnScroll(TKCode.Arc.Editor.ViewModel.ScrollChangedEvent e)
    {
        // uses scrollTopChanged as weak heuristic to differentiate between scrolling due to typing or due to "actual" scrolling
        if (e.ScrollTopChanged || _timer is null)
        {
            Schedule();
        }
    }

    private void OnContentChanged(ModelContentChangedEvent e)
    {
        _cts?.Cancel();
        if (!_sessionActive)
        {
            return;
        }

        // the whole can be updated/redrawn after 800 ms at the latest
        _cursorTimer?.Dispose();
        _cursorTimer = CodeEditor.IdleScheduler.RunAfter(TimeSpan.FromMilliseconds(800), () =>
        {
            _cursorTimer = null;
            Schedule(0);
        });
        Schedule();
    }

    private void OnModifierKey(object? sender, KeyEventArgs e)
    {
        if (!_sessionActive || _enabled is InlayHintsEnabled.On or InlayHintsEnabled.Off)
        {
            return;
        }

        var modifiers = e.KeyModifiers;
        var pressed = modifiers.HasFlag(KeyModifiers.Control) && modifiers.HasFlag(KeyModifiers.Alt) && !modifiers.HasFlag(KeyModifiers.Shift) && !modifiers.HasFlag(KeyModifiers.Meta);
        SetModifierPressed(pressed);
    }

    /// <summary>Ctrl+Alt went down or up (upstream's ModifierKeyEmitter event).</summary>
    internal void SetModifierPressed(bool pressed)
    {
        var defaultVisible = _enabled == InlayHintsEnabled.OnUnlessPressed;
        var visible = pressed ? !defaultVisible : defaultVisible;
        if (visible != _visible)
        {
            _visible = visible;
            var copies = CopyInlayHintsWithCurrentAnchor();
            UpdateHintsDecorators([_editor.Model.GetFullModelRange()], copies);
            Schedule(0);
        }
    }

    /// <summary>The label part drawn at a point of the view, if any (upstream's _getInlayHintLabelPart).</summary>
    internal RenderedInlayHintLabelPart? GetLabelPartAt(Avalonia.Point point)
    {
        if (_editor.View is not { } view || _editor.ViewModel is not { } viewModel || point.X < view.GutterWidth)
        {
            return null;
        }

        var viewLine = viewModel.ViewLayout.GetLineNumberAtVerticalOffset(viewModel.ViewLayout.GetCurrentScrollTop() + point.Y);
        if (viewLine < 1 || viewLine > viewModel.GetLineCount())
        {
            return null;
        }

        var column = view.GetColumnAtX(viewLine, point.X);
        if (view.GetX(viewLine, column) > point.X && column > 1)
        {
            // the character the point is on starts a column before
            column--;
        }

        var maxColumn = viewModel.GetLineMaxColumn(viewLine);
        if (column >= maxColumn)
        {
            return null;
        }

        return viewModel.GetInjectedTextAt(new TextPosition(viewLine, column))?.Options.AttachedData as RenderedInlayHintLabelPart;
    }

    private void OnMouseMove(CodeEditorMouseEvent e)
    {
        var part = GetLabelPartAt(e.Event.GetPosition(_editor.View));
        SetActivePart(part);
    }

    private void OnMouseLeave(PointerEventArgs e) => SetActivePart(null);

    /// <summary>The label part under the pointer changed: tooltip and link underline follow it.</summary>
    internal void SetActivePart(RenderedInlayHintLabelPart? part)
    {
        if (Equals(part, _hoveredPart))
        {
            return;
        }

        _hoveredPart = part;
        var previous = _activePart;
        _activePart = part?.Part.Command is not null || part?.Part.Location is not null ? part : null;
        var lines = new HashSet<int>();
        foreach (var p in new[] { previous, _activePart })
        {
            if (p is not null)
            {
                lines.Add(_editor.Model.ValidatePosition(p.Item.Hint.Position).LineNumber);
            }
        }

        foreach (var line in Equals(previous, _activePart) ? [] : lines)
        {
            var range = new TextRange(line, 1, line, _editor.Model.GetLineMaxColumn(line));
            var lineHints = CopyInlayHintsWithCurrentAnchor().Where(i => range.ContainsRange(i.Anchor.Range)).ToList();
            UpdateHintsDecorators([range], lineHints);
        }

        _ = ShowTooltip(part);
    }

    private async Task ShowTooltip(RenderedInlayHintLabelPart? part)
    {
        if (part is null)
        {
            _tooltip.Hide(_editor);
            return;
        }

        // resolve the item
        await part.Item.Resolve(CancellationToken.None).ConfigureAwait(true);
        var tooltip = part.Part.Tooltip ?? part.Item.Hint.Tooltip;
        if (tooltip is not { } text || text.Value.Length == 0)
        {
            _tooltip.Hide(_editor);
            return;
        }

        _tooltip.Show(_editor, text.Value, _editor.Model.ValidatePosition(part.Item.Hint.Position));
    }

    private void OnMouseDown(CodeEditorMouseEvent e)
    {
        var part = GetLabelPartAt(e.Event.GetPosition(_editor.View));
        if (part is null)
        {
            return;
        }

        if (e.Event is PointerPressedEventArgs { ClickCount: 2 } && part.Item.Hint.TextEdits is { Count: > 0 } edits)
        {
            // double click: apply the hint's edits
            e.Handled = true;
            _editor.ExecuteEdits("inlayHint.default", edits.Select(static edit => new SingleEditOperation(edit.Range, edit.Text)).ToList());
            Schedule(0);
            return;
        }

        var modifiers = e.Event.KeyModifiers;
        var triggerModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (modifiers.HasFlag(triggerModifier) && part.Part.Command is { } command)
        {
            e.Handled = true;
            _ = _editor.ExecuteCommand(command.Id, command.Arguments is { Count: > 0 } args ? args[0] : null);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        EndSession();
        RemoveAllDecorations();
        _tooltip.Hide(_editor);
        _editor.LanguageFeatures.InlayHintsProvider.DidChange -= OnRegistryChanged;
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.DidScrollChange -= OnScroll;
        _editor.DidChangeModelContent -= OnContentChanged;
        _editor.MouseMove -= OnMouseMove;
        _editor.MouseDown -= OnMouseDown;
        _editor.MouseLeave -= OnMouseLeave;
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnModifierKey);
        _editor.RemoveHandler(InputElement.KeyUpEvent, OnModifierKey);
        foreach (var style in _styles)
        {
            style.Dispose();
        }

        _styles.Clear();
    }

    /// <summary>An Arc tooltip bubble above the hint (upstream: the hint's hover).</summary>
    private sealed class TooltipWidget : ICodeEditorContentWidget
    {
        private readonly Tooltip _bubble = new() { IsHitTestVisible = false };
        private TextPosition? _position;
        private bool _added;

        public string Id => "editor.contrib.inlayHintsTooltip";

        public Control Control => _bubble;

        public string? Text => _position is null ? null : _bubble.Content as string;

        public ContentWidgetPosition? GetPosition() =>
            _position is { } p ? new ContentWidgetPosition(p, [ContentWidgetPositionPreference.Above, ContentWidgetPositionPreference.Below]) : null;

        public void Show(CodeEditor editor, string text, TextPosition position)
        {
            _bubble.Content = text;
            _position = position;
            if (!_added)
            {
                editor.AddContentWidget(this);
                _added = true;
            }

            editor.LayoutContentWidget(this);
        }

        public void Hide(CodeEditor editor)
        {
            _position = null;
            if (_added)
            {
                editor.RemoveContentWidget(this);
                _added = false;
            }
        }
    }
}
