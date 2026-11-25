// Ported from vscode (src/vs/editor/contrib/links/browser/links.ts: LinkDetector, LinkOccurrence, the decorations
// and OpenLinkAction; the gesture of src/vs/editor/contrib/gotoSymbol/browser/link/clickLinkGesture.ts:
// ClickLinkGesture; commit b57d5c9e), MIT license. The links come from the engine (LinksComputer.GetLinks).
// Differences:
//  - the editor worker's link computation is a ComputedLinkProvider this contribution registers for every language
//    in the editor's registry (upstream: registered once by the editor worker service);
//  - opening goes through CodeEditor.LinkOpening, then the platform launcher (TopLevel.Launcher) instead of the opener
//    service; relative file links (file://./x) and command: links are not special;
//  - the debounce is a fixed 1000 ms after an edit (upstream learns it per model, 1000 to 4000 ms);
//  - the multi-cursor modifier is upstream's default (Alt), so the trigger modifier is Ctrl (Cmd on macOS); the middle
//    click opens no link (upstream's mouseMiddleClickAction default).

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Links;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using TokenFontStyle = TKCode.Arc.Editor.Tokens.FontStyle;

namespace TKCode.Arc;

/// <summary>Finds the links in the text, underlines them and opens one on Ctrl+click (upstream's LinkDetector).</summary>
internal sealed class LinkDetector : ICodeEditorContribution
{
    public const string Id = "editor.linkDetector";

    /// <summary>The class name of a link.</summary>
    public const string LinkClassName = "detected-link";

    /// <summary>The class name of the link under the pointer while the trigger modifier is down.</summary>
    public const string ActiveLinkClassName = "detected-link-active";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new LinkDetector(editor), EditorContributionInstantiation.AfterFirstRender);

    private static readonly ModelDecorationOptions s_general = new("detected-link")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        CollapseOnReplaceEdit = true,
        InlineClassName = LinkClassName,
    };

    private static readonly ModelDecorationOptions s_active = new("detected-link-active")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        CollapseOnReplaceEdit = true,
        InlineClassName = ActiveLinkClassName,
    };

    private static readonly Cursor s_handCursor = new(StandardCursorType.Hand);

    private readonly CodeEditor _editor;
    private readonly List<IDisposable> _registrations = [];
    private readonly Dictionary<string, ProviderLink> _currentOccurrences = new(StringComparer.Ordinal);
    private IDisposable? _computeLinks;
    private CancellationTokenSource? _computePromise;
    private ProviderLinksList? _activeLinksList;
    private string? _activeLinkDecorationId;
    private CodeEditorMouseTarget? _lastMouseMoveTarget;
    private KeyModifiers _lastMouseMoveModifiers;
    private TextPosition? _mouseDownPosition;
    private bool _mouseDownWithModifier;
    private bool _disposed;

    private LinkDetector(CodeEditor editor)
    {
        _editor = editor;
        _registrations.Add(editor.LanguageFeatures.LinkProvider.Register("*", new ComputedLinkProvider()));
        _registrations.Add(editor.DecorationStyles.Register(LinkClassName, new CodeEditorDecorationStyle { FontStyle = TokenFontStyle.Underline }));
        _registrations.Add(editor.DecorationStyles.Register(ActiveLinkClassName, new CodeEditorDecorationStyle { Foreground = CodeEditorThemeColor.Link, FontStyle = TokenFontStyle.Underline }));

        editor.DidChangeModelContent += OnModelContent;
        editor.DidChangeModelLanguage += OnModelLanguage;
        editor.LanguageFeatures.LinkProvider.DidChange += OnProvidersChanged;
        editor.MouseMove += OnEditorMouseMove;
        editor.MouseDown += OnEditorMouseDown;
        editor.MouseUp += OnEditorMouseUp;
        editor.MouseLeave += OnEditorMouseLeave;
        editor.EditorKeyDown += OnEditorKeyDown;
        editor.AddHandler(InputElement.KeyUpEvent, OnEditorKeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

        Schedule(TimeSpan.Zero);
    }

    /// <summary>Whether links are detected (upstream's links option, default true).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>The links found, in order (tests).</summary>
    internal IReadOnlyList<ProviderLink> Links => _activeLinksList?.Links ?? [];

    /// <summary>The range of the active (Ctrl-hovered) link, or null (tests).</summary>
    internal TextRange? ActiveLinkRange => _activeLinkDecorationId is { } id ? _editor.Model.GetDecorationRange(id) : null;

    /// <summary>Raised after the links were computed (tests).</summary>
    internal event Action? LinksComputed;

    public static LinkDetector? Get(CodeEditor editor) => editor.GetContribution<LinkDetector>(Id);

    private static bool HasTriggerModifier(KeyModifiers modifiers) =>
        OperatingSystem.IsMacOS() ? modifiers.HasFlag(KeyModifiers.Meta) : modifiers.HasFlag(KeyModifiers.Control);

    private static bool IsTriggerKey(Key key) =>
        OperatingSystem.IsMacOS() ? key is Key.LWin or Key.RWin : key is Key.LeftCtrl or Key.RightCtrl;

    private void Schedule(TimeSpan delay)
    {
        _computeLinks?.Dispose();
        _computeLinks = CodeEditorMultiCursorAdapter.RunAfter(delay, () =>
        {
            _computeLinks = null;
            _ = ComputeLinksNow();
        });
    }

    private void OnModelContent(ModelContentChangedEvent e) => Schedule(TimeSpan.FromMilliseconds(1000));

    private void OnModelLanguage(ModelLanguageChangedEvent e)
    {
        Stop();
        Schedule(TimeSpan.Zero);
    }

    private void OnProvidersChanged(int e)
    {
        Stop();
        Schedule(TimeSpan.Zero);
    }

    /// <summary>Computes the links now (upstream's computeLinksNow).</summary>
    internal async Task ComputeLinksNow()
    {
        var model = _editor.Model;
        if (_disposed || !IsEnabled || model.IsTooLargeForSyncing() || !_editor.LanguageFeatures.LinkProvider.Has(model))
        {
            return;
        }

        _activeLinksList?.Dispose();
        _activeLinksList = null;

        _computePromise?.Cancel();
        var cancellation = new CancellationTokenSource();
        _computePromise = cancellation;
        try
        {
            var list = await LinksComputer.GetLinks(_editor.LanguageFeatures.LinkProvider, model, cancellation.Token);
            if (cancellation.IsCancellationRequested || _disposed || model.IsDisposed())
            {
                list.Dispose();
                return;
            }

            _activeLinksList = list;
            UpdateDecorations(list.Links);
            LinksComputed?.Invoke();
        }
        finally
        {
            if (ReferenceEquals(_computePromise, cancellation))
            {
                _computePromise = null;
            }
        }
    }

    private void UpdateDecorations(IReadOnlyList<ProviderLink> links)
    {
        var oldDecorations = _currentOccurrences.Keys.ToList();
        var newDecorations = links.Select(static link => new ModelDeltaDecoration(link.Range, s_general)).ToList();
        var decorations = _editor.Model.DeltaDecorations(oldDecorations, newDecorations);

        _currentOccurrences.Clear();
        _activeLinkDecorationId = null;
        for (var i = 0; i < decorations.Length; i++)
        {
            _currentOccurrences[decorations[i]] = links[i];
        }
    }

    // ---- ClickLinkGesture

    private void OnEditorMouseMove(CodeEditorMouseEvent e)
    {
        _lastMouseMoveTarget = e.Target;
        _lastMouseMoveModifiers = e.Event.KeyModifiers;
        OnMouseMoveOrRelevantKeyDown(e.Target, HasTriggerModifier(e.Event.KeyModifiers));
    }

    private void OnEditorMouseLeave(PointerEventArgs e)
    {
        _lastMouseMoveTarget = null;
        CleanUpActiveLinkDecoration();
    }

    private void OnEditorKeyDown(KeyEventArgs e)
    {
        if (IsTriggerKey(e.Key) && _lastMouseMoveTarget is { } target)
        {
            OnMouseMoveOrRelevantKeyDown(target, true);
        }
    }

    private void OnEditorKeyUp(object? sender, KeyEventArgs e)
    {
        if (IsTriggerKey(e.Key))
        {
            CleanUpActiveLinkDecoration();
        }
    }

    private void OnEditorMouseDown(CodeEditorMouseEvent e)
    {
        _mouseDownWithModifier = HasTriggerModifier(e.Event.KeyModifiers);
        _mouseDownPosition = e.Target.Position;
    }

    private void OnEditorMouseUp(CodeEditorMouseEvent e)
    {
        var withModifier = _mouseDownWithModifier && HasTriggerModifier(e.Event.KeyModifiers);
        var samePosition = _mouseDownPosition is { } down && e.Target.Position is { } up && down == up;
        _mouseDownPosition = null;
        _mouseDownWithModifier = false;
        if (withModifier && samePosition)
        {
            OnExecute(e.Target);
        }
    }

    private void OnMouseMoveOrRelevantKeyDown(CodeEditorMouseTarget target, bool hasTriggerModifier)
    {
        if (IsEnabledAt(target, hasTriggerModifier))
        {
            CleanUpActiveLinkDecoration(); // always remove previous link decoration as their can only be one
            if (GetLinkOccurrence(target.Position) is { } occurrence)
            {
                _editor.Model.ChangeDecorations(accessor =>
                {
                    accessor.ChangeDecorationOptions(occurrence.DecorationId, s_active);
                    return true;
                });
                _activeLinkDecorationId = occurrence.DecorationId;
                SetCursor(s_handCursor);
            }
        }
        else
        {
            CleanUpActiveLinkDecoration();
        }
    }

    private void CleanUpActiveLinkDecoration()
    {
        if (_activeLinkDecorationId is { } id)
        {
            if (_currentOccurrences.ContainsKey(id))
            {
                _editor.Model.ChangeDecorations(accessor =>
                {
                    accessor.ChangeDecorationOptions(id, s_general);
                    return true;
                });
            }

            _activeLinkDecorationId = null;
            SetCursor(null);
        }
    }

    private void SetCursor(Cursor? cursor)
    {
        if (_editor.View is { } view)
        {
            view.Cursor = cursor ?? new Cursor(StandardCursorType.Ibeam);
        }
    }

    private void OnExecute(CodeEditorMouseTarget target)
    {
        if (!IsEnabledAt(target, true) || GetLinkOccurrence(target.Position) is not { } occurrence)
        {
            return;
        }

        _ = OpenLinkOccurrence(occurrence.Link, fromUserGesture: true);
    }

    private static bool IsEnabledAt(CodeEditorMouseTarget target, bool hasTriggerModifier) =>
        target.Type == CodeEditorMouseTargetType.ContentText && hasTriggerModifier;

    /// <summary>The link at <paramref name="position"/> and its decoration (upstream's getLinkOccurrence).</summary>
    internal (string DecorationId, ProviderLink Link)? GetLinkOccurrence(TextPosition? position)
    {
        if (position is not { } p)
        {
            return null;
        }

        var decorations = _editor.Model.GetDecorationsInRange(new TextRange(p.LineNumber, p.Column, p.LineNumber, p.Column), 0, true);
        foreach (var decoration in decorations)
        {
            if (_currentOccurrences.TryGetValue(decoration.Id, out var link))
            {
                return (decoration.Id, link);
            }
        }

        return null;
    }

    /// <summary>Resolves a link and opens it (upstream's openLinkOccurrence).</summary>
    internal async Task OpenLinkOccurrence(ProviderLink link, bool fromUserGesture)
    {
        LinkTarget target;
        try
        {
            target = await link.Resolve(CancellationToken.None);
        }
        catch (LinkResolveException)
        {
            // upstream warns through the notification service; the editor has none
            return;
        }

        await _editor.OpenLinkAsync(target.Uri, target.ToString(), fromUserGesture);
    }

    private void Stop()
    {
        _computeLinks?.Dispose();
        _computeLinks = null;
        _activeLinksList?.Dispose();
        _activeLinksList = null;
        _computePromise?.Cancel();
        _computePromise = null;
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
        if (!_editor.Model.IsDisposed() && _currentOccurrences.Count > 0)
        {
            _editor.Model.DeltaDecorations(_currentOccurrences.Keys.ToList(), []);
        }

        _currentOccurrences.Clear();
        _editor.DidChangeModelContent -= OnModelContent;
        _editor.DidChangeModelLanguage -= OnModelLanguage;
        _editor.LanguageFeatures.LinkProvider.DidChange -= OnProvidersChanged;
        _editor.MouseMove -= OnEditorMouseMove;
        _editor.MouseDown -= OnEditorMouseDown;
        _editor.MouseUp -= OnEditorMouseUp;
        _editor.MouseLeave -= OnEditorMouseLeave;
        _editor.EditorKeyDown -= OnEditorKeyDown;
        _editor.RemoveHandler(InputElement.KeyUpEvent, OnEditorKeyUp);
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }
    }

    /// <summary>Opens the links at the ends of the selections (upstream's OpenLinkAction).</summary>
    public static readonly EditorAction OpenLinkAction = EditorAction.Create(
        "editor.action.openLink",
        "Open Link",
        null,
        async (editor, _) =>
        {
            if (Get(editor) is not { } linkDetector)
            {
                return;
            }

            foreach (var selection in editor.GetSelections())
            {
                if (linkDetector.GetLinkOccurrence(selection.Range.End) is { } occurrence)
                {
                    await linkDetector.OpenLinkOccurrence(occurrence.Link, fromUserGesture: false);
                }
            }
        });
}
