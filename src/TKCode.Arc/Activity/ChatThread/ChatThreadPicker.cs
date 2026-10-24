using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The reaction picker of a <see cref="ChatThread"/> (Arc <c>ReactionPicker</c>): one lazily created
/// <see cref="ArcPopup"/> reused for every message. A pill of emoji opens over the message's line (6px away, aligned to
/// the add-reaction button's end of the line), or under it when it would cross the top of the scroller; it pops on the
/// pop spring and its emoji rise in a 25ms stagger. The first emoji takes focus; ←/→ move and wrap (roving tab stop);
/// Escape closes and returns focus to the button; a pointer press outside closes it.
/// </summary>
internal sealed class ChatThreadPicker
{
    private static readonly SpringSpec s_pop = SpringSpec.FromVisual(0.3, 0.22);
    private static readonly TimeSpan s_stagger = TimeSpan.FromMilliseconds(25);

    private readonly ChatThread _thread;
    private ArcPopup? _popup;
    private ChatThreadPickerSurface? _surface;
    private StackPanel? _row;
    private readonly List<ChatThreadButton> _buttons = new();
    private readonly List<MotionValue<double>> _rises = new();
    private IReadOnlyList<string>? _builtFor;
    private int _active;

    public ChatThreadPicker(ChatThread thread)
    {
        _thread = thread;
    }

    /// <summary>The message the picker is open for.</summary>
    public ChatMessage? Message { get; private set; }

    /// <summary>The add-reaction button that opened it.</summary>
    public ChatThreadButton? Trigger { get; private set; }

    public bool IsOpen => _popup?.IsOpen == true && Message is not null;

    /// <summary>True when it opened under the line (no room above).</summary>
    public bool IsBelow { get; private set; }

    internal ArcPopup? Popup => _popup;

    internal IReadOnlyList<ChatThreadButton> Buttons => _buttons;

    public void Open(ChatThreadMessageView view, ChatMessage message, bool mine, bool below)
    {
        if (view.Line is not { } line)
        {
            return;
        }
        Ensure();
        Build();
        Message = message;
        Trigger = view.ReactButton;
        IsBelow = below;
        var popup = _popup!;
        popup.PlacementTarget = line;
        popup.Placement = below
            ? (mine ? PlacementMode.BottomEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedRight)
            : (mine ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.TopEdgeAlignedRight);
        popup.HorizontalOffset = mine ? -4 : 4;
        popup.MarginToAnchor = _thread.PickerOffset;
        _active = 0;
        UpdateTabStops();
        popup.IsOpen = true;
        AnimateIn();
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && _buttons.Count > 0)
            {
                _buttons[0].Focus(NavigationMethod.Tab);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    public void Close(bool restoreFocus)
    {
        var trigger = Trigger;
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
        var had = Message is not null;
        Message = null;
        Trigger = null;
        if (had)
        {
            _thread.OnPickerClosed();
        }
        if (restoreFocus && trigger is not null)
        {
            trigger.Focus(NavigationMethod.Tab);
        }
    }

    private void Ensure()
    {
        if (_popup is not null)
        {
            return;
        }
        _row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = _thread.PickerGap };
        _surface = new ChatThreadPickerSurface { Child = _row };
        AutomationProperties.SetName(_surface, _thread.Labels.Reactions);
        _surface.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _popup = new ArcPopup
        {
            Child = _surface,
            EnterScale = 0.85,
            EnterOffset = 6,
            IsLightDismissEnabled = true
        };
        ((ISetLogicalParent)_popup).SetParent(_thread);
        _popup.Closed += (_, _) =>
        {
            if (Message is not null)
            {
                Message = null;
                Trigger = null;
                _thread.OnPickerClosed();
            }
        };
    }

    private void Build()
    {
        var reactions = _thread.Reactions;
        if (ReferenceEquals(_builtFor, reactions) && _buttons.Count == reactions.Count)
        {
            RefreshNames();
            return;
        }
        _builtFor = reactions;
        foreach (var rise in _rises)
        {
            rise.Dispose();
        }
        _rises.Clear();
        _buttons.Clear();
        _row!.Children.Clear();
        for (var i = 0; i < reactions.Count; i++)
        {
            var emoji = reactions[i];
            var button = new ChatThreadButton
            {
                Role = ChatThreadButtonRole.Emoji,
                Content = new TextBlock { Text = emoji, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                Tag = emoji,
                RenderTransform = new TranslateTransform()
            };
            var index = i;
            button.Click += (_, e) =>
            {
                e.Handled = true;
                if (Message is { } message)
                {
                    _thread.React(message, emoji);
                }
            };
            button.GotFocus += (_, _) =>
            {
                _active = index;
                UpdateTabStops();
            };
            var rise = new MotionValue<double>(button, 1, MotionVectors.Double);
            rise.Changed += value =>
            {
                ((TranslateTransform)button.RenderTransform!).Y = 6 * (1 - value);
                button.Opacity = Math.Clamp(value, 0, 1);
            };
            _rises.Add(rise);
            _buttons.Add(button);
            _row.Children.Add(button);
        }
        RefreshNames();
    }

    private void RefreshNames()
    {
        var labels = _thread.Labels;
        if (_surface is not null)
        {
            AutomationProperties.SetName(_surface, labels.Reactions);
        }
        foreach (var button in _buttons)
        {
            AutomationProperties.SetName(button, labels.ReactWith((string)button.Tag!));
        }
    }

    private void AnimateIn()
    {
        var full = ArcMotion.IsFull(_thread);
        for (var i = 0; i < _rises.Count; i++)
        {
            if (!full)
            {
                _rises[i].Jump(1);
                continue;
            }
            _rises[i].Jump(0);
            _ = _rises[i].AnimateTo(1, MotionSpec.Spring(s_pop, TimeSpan.FromTicks(s_stagger.Ticks * i)));
        }
    }

    private void UpdateTabStops()
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            KeyboardNavigation.SetIsTabStop(_buttons[i], i == _active);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _buttons.Count == 0)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(restoreFocus: true);
            return;
        }
        var step = e.Key switch
        {
            Key.Right => 1,
            Key.Left => -1,
            _ => 0
        };
        if (step == 0)
        {
            return;
        }
        e.Handled = true;
        _active = (_active + step + _buttons.Count) % _buttons.Count;
        UpdateTabStops();
        _buttons[_active].Focus(NavigationMethod.Directional);
    }
}

/// <summary>The picker's pill surface (Arc <c>.picker</c>: 4px padding, border, raised surface, pill radius).</summary>
internal sealed class ChatThreadPickerSurface : Border
{
    protected override Type StyleKeyOverride => typeof(ChatThreadPickerSurface);

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new ChatThreadGroupPeer(this);
}

/// <summary>A named group (Arc <c>role="group"</c>).</summary>
internal sealed class ChatThreadGroupPeer(Control owner) : Avalonia.Automation.Peers.ControlAutomationPeer(owner)
{
    protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => Avalonia.Automation.Peers.AutomationControlType.Group;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
