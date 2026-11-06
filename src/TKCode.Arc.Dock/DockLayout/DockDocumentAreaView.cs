using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// The view of the <see cref="DockDocumentArea"/>: the view of its tree, or, with no document open, a card with
/// <see cref="DockLayout.EmptyDocumentContent"/> (by default an <see cref="EmptyState"/> that reads "No open documents").
/// </summary>
internal sealed class DockDocumentAreaView : Decorator, IDockView
{
    private Border? _emptyCard;

    public DockDocumentAreaView(DockLayout layout, DockDocumentArea area)
    {
        Layout = layout;
        Area = area;
        area.PropertyChanged += OnAreaPropertyChanged;
        Sync();
    }

    /// <inheritdoc />
    public void Release() => Area.PropertyChanged -= OnAreaPropertyChanged;

    public DockLayout Layout { get; }

    public DockDocumentArea Area { get; }

    /// <summary>The empty-state card while no document is open (tests).</summary>
    internal Border? EmptyCard => Area.Root is null ? _emptyCard : null;

    private void OnAreaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockDocumentArea.RootProperty)
        {
            Sync();
        }
    }

    /// <summary>Shows the area's tree or the empty card.</summary>
    internal void Sync()
    {
        if (Area.Root is { } root)
        {
            var view = Layout.ViewFor(root);
            if (!ReferenceEquals(Child, view))
            {
                Child = null;
                DockLayout.Adopt(view);
                Child = view;
            }
            return;
        }
        var card = _emptyCard ??= CreateEmptyCard();
        card.Child = Layout.EmptyDocumentContent switch
        {
            null => CreateEmptyState(),
            Control control => DetachedOrNull(control),
            var content => new ContentControl { Content = content, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        if (!ReferenceEquals(Child, card))
        {
            Child = card;
        }
    }

    private static Control? DetachedOrNull(Control control)
    {
        DockLayout.Adopt(control);
        return control;
    }

    private static Border CreateEmptyCard()
    {
        var card = new Border { ClipToBounds = true }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(card, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        return card;
    }

    private static EmptyState CreateEmptyState()
    {
        var empty = new EmptyState
        {
            Icon = ArcIcons.FileText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        empty.Bind(EmptyState.TitleProperty, empty.GetResourceObservable(DockLangResources.Key(DockLangResourceKind.NoOpenDocuments)));
        return empty;
    }
}
