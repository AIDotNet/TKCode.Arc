using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.A2UI;

/// <summary>Information for a placeholder: which component is missing or broken, and where.</summary>
public sealed record A2UIFallbackInfo(string ComponentId, string? ParentType, A2UINodeState State, string? ComponentType);

/// <summary>
/// The slot of one node in the view tree. It keeps its place in the parent while the node changes: a pending node
/// becomes its component, a component whose type changed is rebuilt, values are applied in place.
/// </summary>
internal sealed class A2UINodePresenter : Decorator
{
    private readonly A2UIBuildContext _context;
    private IA2UIComponentView? _view;
    private bool _released;
    private MotionValue<double>? _enter;
    private bool _enterPending;

    internal A2UINodePresenter(A2UIBuildContext context, A2UINode node)
    {
        _context = context;
        Node = node;
        node.Changed += OnNodeChanged;
        Rebuild();
    }

    /// <summary>The node shown.</summary>
    public A2UINode Node { get; }

    /// <summary>The component view, or null for a placeholder.</summary>
    public IA2UIComponentView? View => _view;

    private void OnNodeChanged(object? sender, A2UINodeChangedEventArgs e)
    {
        if (_released)
        {
            return;
        }

        if (e.TypeChanged || e.StateChanged || _view is null)
        {
            Rebuild();
            return;
        }

        try
        {
            _view.Update(Node, e);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportViewError(ex);
        }

        ApplyCommon();
    }

    private void Rebuild()
    {
        var wasShown = _view is not null;
        _view?.Detach();
        _view = null;
        Child = null;
        if (Node.State == A2UINodeState.Resolved)
        {
            var view = _context.Registry.Create(Node);
            if (view is null)
            {
                Child = Fallback(_context.View.ErrorTemplate, A2UINodeState.UnknownType);
                ApplyCommon();
                return;
            }

            try
            {
                view.Attach(Node, _context);
                _view = view;
                Child = view.Control;
                // A component that arrives while the answer streams (not with the first tree, not a type change) eases in.
                if (!wasShown && _context.IsStreaming)
                {
                    PlayEnter();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                ReportViewError(ex);
                Child = null;
            }
        }
        else if (Node.State == A2UINodeState.Pending)
        {
            Child = Fallback(_context.View.PendingTemplate, Node.State);
        }
        else
        {
            Child = _context.View.ShowErrorPlaceholders || _context.View.ErrorTemplate is not null
                ? Fallback(_context.View.ErrorTemplate, Node.State)
                : null;
        }

        ApplyCommon();
    }

    /// <summary>
    /// Fade in and rise by <c>EnterOffset</c> (160 ms, Arc's enter easing): the full motion level moves and fades, reduced
    /// only fades, off shows the component at once. Code-driven with <see cref="MotionValue{T}"/> because node slots
    /// hold views of every type built in code; there is no ControlTheme to declare a transition on.
    /// </summary>
    internal void PlayEnter()
    {
        // A slot is built before it joins the tree; the motion level and the frame clock are known only once it has.
        if (VisualRoot is null)
        {
            _enterPending = true;
            return;
        }

        _enterPending = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            return;
        }

        var offset = level == ArcMotionLevel.Full ? A2UIBasicViews.Token(this, A2UISurfaceViewTokenKind.EnterOffset, 4) : 0;
        var shift = new TranslateTransform(0, offset);
        RenderTransform = offset > 0 ? shift : null;
        Opacity = 0;
        _enter?.Dispose();
        var progress = _enter = new MotionValue<double>(this, 0, MotionVectors.Double);
        progress.Changed += t =>
        {
            Opacity = t;
            shift.Y = offset * (1 - t);
        };
        _ = progress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(160), ArcEasings.Enter)).ContinueWith(
            _ =>
            {
                if (ReferenceEquals(_enter, progress))
                {
                    Opacity = 1;
                    RenderTransform = null;
                    _enter = null;
                    progress.Dispose();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private Control? Fallback(IDataTemplate? template, A2UINodeState state)
    {
        var info = new A2UIFallbackInfo(Node.ComponentId, Node.Parent?.Type, state, Node.Component?.Type);
        if (template is not null)
        {
            return template.Build(info);
        }

        return state == A2UINodeState.Pending ? null : _context.View.ShowErrorPlaceholders ? A2UIBasicViews.ErrorStrip(info) : null;
    }

    // Weight (flex grow), accessibility and visibility of the slot, from the node.
    private void ApplyCommon()
    {
        A2UIFlexPanel.SetWeight(this, Node.GetNumber("weight") ?? 0);
        IsVisible = Child is not null;
        var target = (Control?)Child ?? this;
        var a11y = Node.Accessibility;
        if (a11y.Label is { } label)
        {
            AutomationProperties.SetName(target, label);
        }

        if (a11y.Description is { } description)
        {
            AutomationProperties.SetHelpText(target, description);
        }

        AutomationProperties.SetLiveSetting(target, a11y.Live switch
        {
            "polite" => AutomationLiveSetting.Polite,
            "assertive" => AutomationLiveSetting.Assertive,
            _ => AutomationLiveSetting.Off
        });
        if (a11y.Hidden)
        {
            AutomationProperties.SetAccessibilityView(target, AccessibilityView.Raw);
        }
    }

    private void ReportViewError(Exception ex)
        => _context.Renderer.ReportError(new A2UIException(A2UIErrorCategory.Expression, A2UIErrorCodes.ExecutionError,
                                                           $"The view of '{Node.ComponentId}' ({Node.Type}) failed: {ex.Message}", Node.Surface.Id),
                                         Node.Surface.Id);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_enterPending)
        {
            PlayEnter();
        }
    }

    internal void Release()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        _enter?.Dispose();
        _enter = null;
        Node.Changed -= OnNodeChanged;
        _view?.Detach();
        _view = null;
        Child = null;
    }
}
