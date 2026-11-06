using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A tab group: its <see cref="Panes"/> share one card, one tab each, and <see cref="ActivePane"/> is the one shown.
/// A group whose last pane leaves is removed from the layout.
/// </summary>
public sealed class DockGroup : DockNode
{
    /// <summary>The pane whose tab is selected and whose content is shown. Two-way; always one of <see cref="Panes"/> (or null when empty).</summary>
    public static readonly StyledProperty<DockPane?> ActivePaneProperty =
        AvaloniaProperty.Register<DockGroup, DockPane?>(nameof(ActivePane), defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceActivePane);

    /// <summary>True when the group is inside the <see cref="DockDocumentArea"/> (its tabs then look like document tabs).</summary>
    public static readonly DirectProperty<DockGroup, bool> IsDocumentGroupProperty =
        AvaloniaProperty.RegisterDirect<DockGroup, bool>(nameof(IsDocumentGroup), o => o.IsDocumentGroup);

    private bool _isDocumentGroup;

    public DockGroup()
    {
        Panes = new AvaloniaList<DockPane> { ResetBehavior = ResetBehavior.Remove };
        Panes.Validate = ValidatePane;
        Panes.CollectionChanged += OnPanesChanged;
    }

    /// <summary>Creates a group with the given panes; the first one is active.</summary>
    public DockGroup(params DockPane[] panes) : this()
    {
        Panes.AddRange(panes);
    }

    /// <summary>The panes of the group, in tab order.</summary>
    [Content]
    public AvaloniaList<DockPane> Panes { get; }

    /// <inheritdoc cref="ActivePaneProperty"/>
    public DockPane? ActivePane { get => GetValue(ActivePaneProperty); set => SetValue(ActivePaneProperty, value); }

    /// <inheritdoc cref="IsDocumentGroupProperty"/>
    public bool IsDocumentGroup
    {
        get => _isDocumentGroup;
        private set => SetAndRaise(IsDocumentGroupProperty, ref _isDocumentGroup, value);
    }

    internal override IEnumerable<DockNode> ChildNodes => [];

    /// <summary>Recomputes <see cref="IsDocumentGroup"/> from where the group is now.</summary>
    internal void RefreshPlacement() => IsDocumentGroup = IsInDocumentArea;

    private static DockPane? CoerceActivePane(AvaloniaObject sender, DockPane? value)
    {
        var group = (DockGroup)sender;
        if (value is not null && group.Panes.Contains(value))
        {
            return value;
        }
        return group.Panes.Count > 0 ? group.Panes[0] : null;
    }

    private void ValidatePane(DockPane pane)
    {
        if (pane.Container is not null)
        {
            throw new InvalidOperationException(
                $"The pane '{pane.Id}' is already in a group or on an auto-hide strip; remove it first, or use DockLayout.Move.");
        }
        Workspace?.ValidateIncoming(pane);
    }

    private void OnPanesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var removal = false;
        var removedIndex = -1;
        var activeRemoved = false;
        if (e.OldItems is { } old)
        {
            foreach (DockPane pane in old)
            {
                if (ReferenceEquals(pane.Container, this) && !Panes.Contains(pane))
                {
                    pane.SetContainer(null, null);
                    removal = true;
                    if (ReferenceEquals(pane, ActivePane))
                    {
                        activeRemoved = true;
                        removedIndex = e.OldStartingIndex;
                    }
                }
            }
        }
        if (e.NewItems is { } added)
        {
            foreach (DockPane pane in added)
            {
                pane.SetContainer(this, null);
            }
        }
        if (activeRemoved && Panes.Count > 0)
        {
            // The pane to the right of the one that left, or the last one when it was the last.
            var index = Math.Clamp(removedIndex < 0 ? 0 : removedIndex, 0, Panes.Count - 1);
            SetCurrentValue(ActivePaneProperty, Panes[index]);
        }
        else if (activeRemoved)
        {
            // Empty now: drop the pane itself, not only its coerced value (the property would keep the closed pane alive).
            SetCurrentValue(ActivePaneProperty, null);
        }
        else
        {
            CoerceValue(ActivePaneProperty);
        }
        NotifyStructure(removal);
    }
}
