using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The model arena's reveal rule for one slot (a pane or a narrow tab), applied before anything that names the model is
/// set: hiding takes effect at once; showing a model, or another model, waits for the end of the current dispatcher turn.
/// An owner that changes the slot's model for a new round and turns the reveal off in the same turn (in either order)
/// therefore never shows or names the new model, not even in an accessible name. Off screen it applies at once.
/// </summary>
internal sealed class ArenaRevealGate
{
    private readonly Control _owner;
    private readonly Func<AiModel?> _wanted;
    private readonly Action<AiModel?> _apply;
    private bool _pending;

    /// <param name="owner">The control whose window decides whether to wait.</param>
    /// <param name="wanted">The model to show now: the slot's model when revealed, otherwise null.</param>
    /// <param name="apply">Shows a model, or hides it (null).</param>
    public ArenaRevealGate(Control owner, Func<AiModel?> wanted, Action<AiModel?> apply)
    {
        _owner = owner;
        _wanted = wanted;
        _apply = apply;
    }

    /// <summary>The model shown now; null while hidden.</summary>
    public AiModel? Shown { get; private set; }

    /// <summary>
    /// Brings the slot in line with <c>wanted</c>: a hide, or a change around the model already shown (its texts), applies
    /// at once; a model to show is applied at the end of the dispatcher turn.
    /// </summary>
    public void Update()
    {
        var model = _wanted();
        if (model is null || ReferenceEquals(model, Shown) || TopLevel.GetTopLevel(_owner) is null)
        {
            Apply(model);
            return;
        }
        if (_pending)
        {
            return;
        }
        _pending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _pending = false;
            Apply(_wanted());
        }, DispatcherPriority.Normal);
    }

    private void Apply(AiModel? model)
    {
        Shown = model;
        _apply(model);
    }
}

/// <summary>A named group of a pane (the metrics row: "Model A metrics"); the parts inside are read as its children.</summary>
internal sealed class ArenaPaneGroup : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer(ArenaPaneGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>The arena's slot letters ("A", "B": the argument of the labels' formats).</summary>
internal static class ArenaSlots
{
    public static string Letter(ArenaSlot slot) => slot == ArenaSlot.B ? "B" : "A";
}
