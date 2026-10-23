using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The panel's name cell: a title and the line under it (swapped in as one).</summary>
internal sealed record LayoutMorphHeading(string Title, string? Subtitle);

/// <summary>The detail's main button content: Plus or Check and the text (swapped in on every toggle).</summary>
internal sealed record LayoutMorphActionContent(bool IsActive, string Text);

public partial class LayoutMorph
{
    private ArcContentSwap? _hint;
    private ShowpieceLiveRegion? _live;
    private ShowpieceHoverCard? _tag;
    private TextBlock? _tagTitle;
    private TextBlock? _tagLine;
    private LayoutMorphItem? _tagItem;
    private Point _tagAnchor;
    private Grid? _panelGrid;
    private Border? _who;
    private ArcContentSwap? _whoSwap;
    private readonly Border?[] _statCells = new Border?[3];
    private readonly ArcContentSwap?[] _statLabels = new ArcContentSwap?[3];
    private readonly ShowpieceNumber?[] _statNumbers = new ShowpieceNumber?[3];
    private readonly TextBlock?[] _statTexts = new TextBlock?[3];
    private LayoutMorphItem? _subject;
    private bool _panelNarrow;
    private bool _panelArranged;
    private LayoutMorphDialog? _dialog;
    private AvaloniaButton? _close;
    private ToggleButton? _action;
    private ArcContentSwap? _actionSwap;
    private MotionValue<double>? _dialogOpacity;
    private LayoutMorphItem? _dialogItem;

    /// <summary>The floating tag above a pointed-at card: centered on its top edge, 10px above it.</summary>
    internal static readonly ShowpieceCardPlacement TagPlacement = new()
    {
        Side = ShowpieceCardSide.Above,
        Gap = 10,
        CrossAlign = ShowpieceCardAlign.Center,
        EdgeMargin = 8
    };

    internal ShowpieceHoverCard? TagCard => _tag;

    internal LayoutMorphItem? TagItem => _tagItem;

    internal LayoutMorphItem? PanelSubject => _subject;

    internal LayoutMorphDialog? Dialog => _dialog;

    internal ArcContentSwap? HintSwap => _hint;

    internal ShowpieceLiveRegion? LiveRegion => _live;

    private void ApplyPanelTemplate(INameScope scope)
    {
        _hint = scope.Find<ArcContentSwap>("PART_Hint");
        _live = scope.Find<ShowpieceLiveRegion>("PART_Live");
        _tag = scope.Find<ShowpieceHoverCard>("PART_Tag");
        _tagTitle = scope.Find<TextBlock>("PART_TagTitle");
        _tagLine = scope.Find<TextBlock>("PART_TagLine");
        if (_tag is not null)
        {
            _tag.Placement = TagPlacement;
        }
        _panelGrid = scope.Find<Grid>("PART_PanelGrid");
        _who = scope.Find<Border>("PART_Who");
        _whoSwap = scope.Find<ArcContentSwap>("PART_WhoSwap");
        for (var i = 0; i < 3; i++)
        {
            _statCells[i] = scope.Find<Border>("PART_Stat" + i);
            _statLabels[i] = scope.Find<ArcContentSwap>("PART_StatLabel" + i);
            _statNumbers[i] = scope.Find<ShowpieceNumber>("PART_StatNumber" + i);
            _statTexts[i] = scope.Find<TextBlock>("PART_StatText" + i);
        }
        _panelArranged = false;
    }

    // Hint -----------------------------------------------------------------------------------------------------------------

    private void UpdateHint()
    {
        if (_hint is not null)
        {
            _hint.Content = HintOf(Layout);
        }
    }

    // Bottom panel ------------------------------------------------------------------------------------------------------------

    /// <summary>The stage's described card changed (pointed at, focused, in front, opened, or none).</summary>
    internal void OnSubjectChanged(LayoutMorphItem? item) => OnSubjectChanged(item, force: false);

    private void OnSubjectChanged(LayoutMorphItem? item, bool force)
    {
        if (!force && ReferenceEquals(item, _subject))
        {
            return;
        }
        _subject = item;
        if (item is null)
        {
            UpdateDefaultPanel();
            return;
        }
        SetHeading(new LayoutMorphHeading(item.Title, item.SecondLine));
        SetStats(item.Stats);
    }

    private void UpdateDefaultPanel()
    {
        if (_subject is not null)
        {
            return;
        }
        SetHeading(new LayoutMorphHeading(DefaultPanelTitle ?? Title ?? string.Empty, DefaultPanelSubtitle));
        SetStats(DefaultStats);
    }

    private void SetHeading(LayoutMorphHeading heading)
    {
        if (_whoSwap is not null && !Equals(_whoSwap.Content, heading))
        {
            _whoSwap.Content = heading;
        }
    }

    private void SetStats(IList<LayoutMorphFact>? stats)
    {
        for (var i = 0; i < 3; i++)
        {
            var fact = stats is not null && i < stats.Count ? stats[i] : null;
            if (_statLabels[i] is { } label)
            {
                var text = fact?.Label ?? string.Empty;
                // The same label does not replay its entrance.
                if (!Equals(label.Content as string, text))
                {
                    label.Content = text;
                }
            }
            var number = _statNumbers[i];
            var plain = _statTexts[i];
            if (fact is not null && TryReadNumber(fact, out var value, out var decimals, out var prefix, out var suffix))
            {
                if (number is not null)
                {
                    number.Prefix = prefix;
                    number.Suffix = suffix;
                    number.Decimals = decimals;
                    number.Value = value;
                    number.IsVisible = true;
                }
                if (plain is not null)
                {
                    plain.IsVisible = false;
                }
            }
            else
            {
                if (number is not null)
                {
                    number.IsVisible = false;
                }
                if (plain is not null)
                {
                    plain.Text = fact?.Value;
                    plain.IsVisible = true;
                }
            }
        }
    }

    /// <summary>
    /// A statistic as a rolling number: <see cref="LayoutMorphFact.NumericValue"/>, or the first number in
    /// <see cref="LayoutMorphFact.Value"/> ("$1,240" → "$" + 1240; "4.8" → 4.8 with one decimal; "2 days" → 2 + " days").
    /// </summary>
    internal static bool TryReadNumber(LayoutMorphFact fact, out double value, out int decimals, out string prefix, out string suffix)
    {
        value = 0;
        decimals = 0;
        prefix = string.Empty;
        suffix = string.Empty;
        var text = fact.Value ?? string.Empty;
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                start = i;
                break;
            }
        }
        if (start < 0)
        {
            if (fact.NumericValue is { } only)
            {
                value = only;
                decimals = Math.Max(0, fact.Decimals);
                return true;
            }
            return false;
        }
        if (start > 0 && text[start - 1] == '-')
        {
            start--;
        }
        var end = start;
        var seenDot = false;
        while (end < text.Length)
        {
            var c = text[end];
            if (char.IsAsciiDigit(c) || (c == '-' && end == start))
            {
                end++;
            }
            else if (c == ',' && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1]) && !seenDot)
            {
                end++;
            }
            else if (c == '.' && !seenDot && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1]))
            {
                seenDot = true;
                end++;
            }
            else
            {
                break;
            }
        }
        var core = text[start..end].Replace(",", string.Empty);
        if (!double.TryParse(core, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        var dot = core.IndexOf('.');
        decimals = dot >= 0 ? core.Length - dot - 1 : 0;
        prefix = text[..start];
        suffix = text[end..];
        if (fact.NumericValue is { } numeric)
        {
            value = numeric;
            decimals = Math.Max(0, fact.Decimals);
        }
        return true;
    }

    /// <summary>Wide: the name and three statistics in a row (1.5 : 1 : 1 : 1). Narrow: the name across the top, statistics under it.</summary>
    private void ArrangePanel(bool narrow)
    {
        if (_panelGrid is null || (_panelArranged && narrow == _panelNarrow))
        {
            return;
        }
        _panelArranged = true;
        _panelNarrow = narrow;
        _panelGrid.ColumnDefinitions.Clear();
        _panelGrid.RowDefinitions.Clear();
        if (narrow)
        {
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _panelGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _panelGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        else
        {
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition(1.5, GridUnitType.Star));
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _panelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _panelGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        var padding = narrow ? new Thickness(16, 12, 16, 13) : new Thickness(20, 14, 20, 15);
        if (_who is not null)
        {
            Grid.SetRow(_who, 0);
            Grid.SetColumn(_who, 0);
            Grid.SetColumnSpan(_who, narrow ? 3 : 1);
            _who.Padding = padding;
            _who.BorderThickness = narrow ? new Thickness(0, 0, 0, 1) : default;
        }
        for (var i = 0; i < 3; i++)
        {
            if (_statCells[i] is not { } cell)
            {
                continue;
            }
            Grid.SetRow(cell, narrow ? 1 : 0);
            Grid.SetColumn(cell, narrow ? i : i + 1);
            cell.Padding = padding;
            cell.BorderThickness = narrow && i == 0 ? default : new Thickness(1, 0, 0, 0);
        }
    }

    // Floating tag -------------------------------------------------------------------------------------------------------------

    /// <summary>Called every stage frame: the tagged card (Masonry, Stack, Helix) and its top-edge middle in stage px.</summary>
    internal void OnTagChanged(LayoutMorphItem? item, Point anchor)
    {
        if (_tag is null)
        {
            return;
        }
        if (item is null)
        {
            _tagItem = null;
            if (_tag.IsOpen)
            {
                _tag.IsOpen = false;
            }
            return;
        }
        if (!ReferenceEquals(item, _tagItem))
        {
            _tagItem = item;
            if (_tagTitle is not null)
            {
                _tagTitle.Text = item.Title;
            }
            if (_tagLine is not null)
            {
                _tagLine.Text = item.SecondLine;
                _tagLine.IsVisible = !string.IsNullOrEmpty(item.SecondLine);
            }
        }
        // Near the top the tag stays inside the stage, over the card, instead of flipping under its top edge.
        // Two 16px lines, padding 7 / 8 and the border, until the tag has been measured.
        var height = Math.Max(Math.Max(_tag.Bounds.Height, _tag.DesiredSize.Height), 49);
        anchor = new Point(anchor.X, Math.Max(anchor.Y, TagPlacement.EdgeMargin + TagPlacement.Gap + height));
        if (!_tag.IsOpen || Math.Abs(anchor.X - _tagAnchor.X) > 0.05 || Math.Abs(anchor.Y - _tagAnchor.Y) > 0.05)
        {
            _tagAnchor = anchor;
            _tag.ShowAt(anchor);
        }
    }

    // Detail dialog ------------------------------------------------------------------------------------------------------------

    private void ApplyDialogTemplate(INameScope scope)
    {
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        _dialog = scope.Find<LayoutMorphDialog>("PART_Dialog");
        _close = scope.Find<AvaloniaButton>("PART_Close");
        _action = scope.Find<ToggleButton>("PART_Action");
        _actionSwap = scope.Find<ArcContentSwap>("PART_ActionSwap");
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_dialog is not null)
        {
            _dialogOpacity = new MotionValue<double>(_dialog, 0, MotionVectors.Double);
            _dialogOpacity.Changed += o =>
            {
                if (_dialog is not null)
                {
                    _dialog.Opacity = o;
                }
            };
            _dialog.Opacity = 0;
            _dialog.IsVisible = false;
            _dialog.IsHitTestVisible = false;
        }
        if (OpenedItem is { } open)
        {
            ShowDialog(open);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (OpenedItem is not { } item)
        {
            return;
        }
        // The item's IsActive change updates the button (OnItemPropertyChanged).
        item.SetCurrentValue(LayoutMorphItem.IsActiveProperty, !item.IsActive);
        if (_action is not null)
        {
            _action.IsChecked = item.IsActive;
        }
        var format = item.IsActive ? ActionOnFormat : ActionOffFormat;
        Announce(string.Format(format, item.Title, item.IsActive ? item.ActiveActionText : item.ActionText));
        RaiseEvent(new LayoutMorphItemEventArgs(ItemActionInvokedEvent, item));
        if (ActionCommand is { } command && command.CanExecute(item))
        {
            command.Execute(item);
        }
    }

    /// <summary>
    /// Shows the dialog layer over the open card (its buttons): it fades in 260ms later, once the card has mostly morphed,
    /// over 240ms; focus moves to the close button and Tab stays inside.
    /// </summary>
    /// <remarks>
    /// Code-driven motion: the fade in is delayed and the fade out is immediate, and both follow the open state rather
    /// than a style class; a ControlTheme transition cannot give one property an entrance delay and a different exit.
    /// </remarks>
    private void ShowDialog(LayoutMorphItem item)
    {
        if (_dialog is null)
        {
            return;
        }
        _dialogItem = item;
        UpdateDialog(item);
        UpdateAction(item, animate: false);
        _dialog.IsVisible = true;
        _dialog.IsHitTestVisible = true;
        _dialogOpacity?.Jump(0);
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !IsLoaded)
        {
            _dialogOpacity?.Jump(1);
        }
        else
        {
            _ = _dialogOpacity?.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard,
                ArcMotion.IsFull(this) ? TimeSpan.FromMilliseconds(260) : TimeSpan.Zero));
        }
        // Focus moves into the dialog; the ring shows only when the card was opened from the keyboard.
        var method = _openMethod;
        _openMethod = NavigationMethod.Unspecified;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(OpenedItem, item))
            {
                _close?.Focus(method);
            }
        }, Avalonia.Threading.DispatcherPriority.Input);
    }

    /// <summary>The layer goes inert at once and fades out quickly (Arc: at 15% within 100ms).</summary>
    private void HideDialog(LayoutMorphItem? item)
    {
        if (_dialog is null || !_dialog.IsVisible)
        {
            return;
        }
        _dialogItem = null;
        _dialog.IsHitTestVisible = false;
        var dialog = _dialog;
        if (_dialogOpacity is null || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !IsLoaded)
        {
            _dialogOpacity?.Jump(0);
            dialog.IsVisible = false;
            return;
        }
        _ = FadeOutDialogAsync(dialog, _dialogOpacity);
    }

    private async Task FadeOutDialogAsync(LayoutMorphDialog dialog, MotionValue<double> opacity)
    {
        await opacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Exit));
        if (_dialogItem is null)
        {
            dialog.IsVisible = false;
        }
    }

    /// <summary>Sizes the layer to the open card and names it (title, description) and the close button.</summary>
    private void UpdateDialog(LayoutMorphItem item)
    {
        if (_dialog is null)
        {
            return;
        }
        var metrics = _stage.Metrics;
        var pose = LayoutMorphEngine.OpenPose(metrics, item.OpenVariant, out var sheet);
        _dialog.Width = pose.Width;
        _dialog.Height = pose.Height;
        _dialog.IsStacked = sheet.IsStacked;
        AutomationProperties.SetName(_dialog, item.Title);
        AutomationProperties.SetHelpText(_dialog, item.Description ?? item.Meta);
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, string.Format(CloseTextFormat, item.Title));
        }
    }

    private void UpdateAction(LayoutMorphItem item, bool animate)
    {
        if (_action is null)
        {
            return;
        }
        _action.IsChecked = item.IsActive;
        var text = item.IsActive ? item.ActiveActionText : item.ActionText;
        AutomationProperties.SetName(_action, text);
        if (_actionSwap is not null)
        {
            var content = new LayoutMorphActionContent(item.IsActive, text);
            if (!Equals(_actionSwap.Content, content))
            {
                _actionSwap.Content = content;
            }
        }
    }
}

/// <summary>
/// The open detail's layer over the card: transparent, the size of the card, holding the close and main buttons. It is a
/// dialog to screen readers (named by the title, described by the description) and keeps Tab inside while open.
/// </summary>
internal sealed class LayoutMorphDialog : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<LayoutMorphDialog, bool>(nameof(IsStacked));

    public LayoutMorphDialog()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>The narrow detail: the main button spans the card's width.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStackedProperty)
        {
            PseudoClasses.Set(":stacked", IsStacked);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LayoutMorphDialogAutomationPeer(this);
}

internal sealed class LayoutMorphDialogAutomationPeer : ControlAutomationPeer
{
    public LayoutMorphDialogAutomationPeer(LayoutMorphDialog owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "dialog";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
