using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>What one record row shows in its status column.</summary>
internal enum DomainRowState
{
    NotChecked,
    Checking,
    Valid,
    NotFound,
    Invalid,
}

/// <summary>Builders for the code-made parts of <see cref="DomainVerify"/> (text, glyphs), bound to ArcDesign tokens.</summary>
internal static class DomainVerifyUi
{
    /// <summary>A text block with token-bound size, color and (optionally) the mono font.</summary>
    public static TextBlock Text(string? text, ArcDesignTokenKind size, ArcDesignTokenKind color,
        FontWeight weight = FontWeight.Normal, bool mono = false, double lineHeight = double.NaN)
    {
        var block = new TextBlock { Text = text, FontWeight = weight, LineHeight = lineHeight };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, mono ? ArcDesignTokenKind.MonoFontFamily : ArcDesignTokenKind.BodyFontFamily);
        return block;
    }

    /// <summary>A Lucide glyph in a token color.</summary>
    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color, double stroke = 1.75)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>A leading visual and a text on one line (icon plus short label, never color alone).</summary>
    public static StackPanel IconLabel(Control icon, string text, ArcDesignTokenKind color)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(icon);
        row.Children.Add(Text(text, ArcDesignTokenKind.TypeXS, color, FontWeight.Medium, lineHeight: 16.8));
        return row;
    }

    /// <summary>A 14px spinner for a checking row.</summary>
    public static Control Spinner() => new ArcSpinner { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
}

/// <summary>
/// One DNS record of a <see cref="DomainVerify"/>: its type, name, value with a copy button, a status (icon plus word)
/// and, for a conflicting record, a fix hint that grows under it. Wide rows put everything on one line; below the compact
/// width the type, name and status share the first line, the value with its copy button takes the second and the purpose
/// the third. The row owns no theme: it is built in code from token-bound parts because it is made per record and the
/// list is a handful of rows.
/// </summary>
internal sealed class DomainVerifyRow : Border
{
    private readonly DomainRecord _record;
    private readonly DomainVerifyLabels _labels;
    private readonly Grid _grid;
    private readonly TextBlock _type;
    private readonly TextBlock _name;
    private readonly TextBlock? _purpose;
    private readonly TextBlock _value;
    private readonly Grid _valueCell;
    private readonly CopyButton _copy;
    private readonly ArcContentSwap _status;
    private readonly SpringSizeDecorator _fixHost;
    private readonly Border _fix;
    private readonly TextBlock _found;
    private readonly TextBlock _fixText;
    private MotionValue<double>? _entrance;
    private DomainRowState _state = (DomainRowState)(-1);
    private bool _compact;

    public DomainVerifyRow(DomainRecord record, DomainVerifyLabels labels)
    {
        _record = record;
        _labels = labels;
        _type = DomainVerifyUi.Text(record.Type, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary,
            FontWeight.Medium, mono: true, lineHeight: 16.8);
        _name = DomainVerifyUi.Text(record.Name, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, mono: true, lineHeight: 16.8);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        ToolTip.SetTip(_name, record.Name);
        if (!string.IsNullOrEmpty(record.Purpose))
        {
            _purpose = DomainVerifyUi.Text(record.Purpose, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
            _purpose.TextTrimming = TextTrimming.CharacterEllipsis;
        }
        _value = DomainVerifyUi.Text(record.Value, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, mono: true, lineHeight: 16.8);
        _value.TextTrimming = TextTrimming.CharacterEllipsis;
        _value.VerticalAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(_value, record.Value);
        _copy = new CopyButton
        {
            Value = record.Value,
            IsIconOnly = true,
            Variant = CopyButtonVariant.Plain,
            VerticalAlignment = VerticalAlignment.Center,
            Label = labels.CopyName(record.Type),
            CopiedLabel = labels.Copied,
        };
        AutomationProperties.SetName(_copy, labels.CopyName(record.Type));
        _valueCell = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
        _valueCell.Children.Add(_value);
        Grid.SetColumn(_copy, 1);
        _valueCell.Children.Add(_copy);

        _status = new ArcContentSwap
        {
            Mode = ContentSwapMode.Label,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetAccessibilityView(_status, AccessibilityView.Raw);

        _found = DomainVerifyUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, mono: true, lineHeight: 16.8);
        _found.TextWrapping = TextWrapping.Wrap;
        _fixText = DomainVerifyUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, lineHeight: 16.8);
        _fixText.TextWrapping = TextWrapping.Wrap;
        _fix = new Border { Padding = new Thickness(12, 8), Margin = new Thickness(0, 8, 0, 0), Child = new StackPanel { Spacing = 2, Children = { _found, _fixText } } }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        ArcTokens.Bind(_fix, BackgroundProperty, ArcDesignTokenKind.DangerSubtle);
        _fixHost = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, IsExpanded = false, Child = _fix, ClipOverflow = 0 };

        _grid = new Grid { ColumnSpacing = 16, RowSpacing = 0 };
        _grid.ColumnDefinitions.Add(new ColumnDefinition(72, GridUnitType.Pixel));
        _grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        _grid.ColumnDefinitions.Add(new ColumnDefinition(1.6, GridUnitType.Star));
        _grid.ColumnDefinitions.Add(new ColumnDefinition(128, GridUnitType.Pixel));
        for (var i = 0; i < 4; i++)
        {
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        foreach (var child in new Control?[] { _type, _name, _purpose, _valueCell, _status, _fixHost })
        {
            if (child is not null)
            {
                _grid.Children.Add(child);
            }
        }
        Child = _grid;
        ClipToBounds = false;

        AutomationProperties.SetControlTypeOverride(this, Avalonia.Automation.Peers.AutomationControlType.ListItem);
        SetState(DomainRowState.NotChecked, null, announce: false);
        Arrange(compact: false);
    }

    public DomainRecord Record => _record;

    public DomainRowState State => _state;

    public CopyButton CopyButton => _copy;

    public Control FixHint => _fixHost;

    public bool IsFixShown => _fixHost.IsExpanded;

    public string StatusText { get; private set; } = string.Empty;

    /// <summary>Switches between the one-line layout and the stacked layout.</summary>
    public void SetCompact(bool compact)
    {
        if (compact != _compact)
        {
            Arrange(compact);
        }
    }

    private void Arrange(bool compact)
    {
        _compact = compact;
        _grid.ColumnDefinitions[2].Width = compact ? new GridLength(0) : new GridLength(1.6, GridUnitType.Star);
        _grid.ColumnDefinitions[0].Width = compact ? GridLength.Auto : new GridLength(72, GridUnitType.Pixel);
        Place(_type, 0, 0, 1);
        Place(_name, 0, 1, compact ? 1 : 1);
        Place(_status, 0, 3, 1);
        if (compact)
        {
            Place(_valueCell, 1, 0, 4);
            _valueCell.Margin = new Thickness(0, 4, 0, 0);
            if (_purpose is not null)
            {
                Place(_purpose, 2, 0, 4);
                _purpose.Margin = new Thickness(0, 2, 0, 0);
            }
            Place(_fixHost, 3, 0, 4);
        }
        else
        {
            Place(_valueCell, 0, 2, 1);
            _valueCell.Margin = default;
            if (_purpose is not null)
            {
                Place(_purpose, 1, 1, 3);
                _purpose.Margin = new Thickness(0, 2, 0, 0);
            }
            Place(_fixHost, 2, 1, 3);
        }
    }

    private static void Place(Control control, int row, int column, int span)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
    }

    /// <summary>Shows a state in the status column and, for an invalid record, the found value and the fix.</summary>
    public void SetState(DomainRowState state, DomainRecordCheck? check, bool announce = true)
    {
        var showFix = state == DomainRowState.Invalid && (!string.IsNullOrEmpty(check?.Found) || !string.IsNullOrEmpty(check?.Fix));
        _found.Text = string.IsNullOrEmpty(check?.Found) ? null : $"{_labels.FoundLabel}: {check!.Found}";
        _found.IsVisible = !string.IsNullOrEmpty(_found.Text);
        _fixText.Text = string.IsNullOrEmpty(check?.Fix) ? check?.Detail : $"{_labels.FixLabel}: {check!.Fix}";
        _fixText.IsVisible = !string.IsNullOrEmpty(_fixText.Text);
        _fixHost.IsExpanded = showFix;
        if (state == _state)
        {
            return;
        }
        _state = state;
        var (icon, text, color) = state switch
        {
            DomainRowState.Checking => (DomainVerifyUi.Spinner(), _labels.Checking, ArcDesignTokenKind.TextSecondary),
            DomainRowState.Valid => (DomainVerifyUi.Glyph(ArcIcons.CircleCheck, 14, ArcDesignTokenKind.Success), _labels.Valid, ArcDesignTokenKind.Success),
            DomainRowState.NotFound => (DomainVerifyUi.Glyph(ArcIcons.Clock, 14, ArcDesignTokenKind.Warning), _labels.NotFound, ArcDesignTokenKind.Warning),
            DomainRowState.Invalid => (DomainVerifyUi.Glyph(ArcIcons.TriangleAlert, 14, ArcDesignTokenKind.Danger), _labels.Misconfigured, ArcDesignTokenKind.Danger),
            _ => (DomainVerifyUi.Glyph(ArcIcons.Circle, 14, ArcDesignTokenKind.TextMuted), _labels.NotChecked, ArcDesignTokenKind.TextMuted),
        };
        StatusText = text;
        _status.Content = DomainVerifyUi.IconLabel(icon, text, color);
        AutomationProperties.SetName(this, $"{_record.Type} {_record.Name}, {text}");
    }

    /// <summary>
    /// Fades the row in, each one 40ms after the one before. Code-driven because a ControlTheme cannot delay by the row's
    /// index. Full and Reduced fade; Off shows the row at once.
    /// </summary>
    public void PlayEntrance(ArcMotionLevel level, int index)
    {
        if (level == ArcMotionLevel.Off)
        {
            Opacity = 1;
            return;
        }
        if (_entrance is null)
        {
            _entrance = new MotionValue<double>(this, 0, MotionVectors.Double);
            _entrance.Changed += _ => Opacity = _entrance.Value;
        }
        Opacity = 0;
        var delay = level == ArcMotionLevel.Full ? TimeSpan.FromMilliseconds(40 * index) : TimeSpan.Zero;
        _ = _entrance.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter, delay));
    }

    /// <summary>A dimmed, inert row of blocks that stands in for a record before the domain is added.</summary>
    public static Control CreatePlaceholder(double width, Thickness padding)
    {
        Border Block(double w)
        {
            var block = new Border { Width = w, Height = 10, HorizontalAlignment = HorizontalAlignment.Left }
                .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(block, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            return block;
        }

        var grid = new Grid { ColumnSpacing = 16, IsHitTestVisible = false, Focusable = false, Margin = padding };
        grid.ColumnDefinitions.Add(new ColumnDefinition(72, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1.6, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(128, GridUnitType.Pixel));
        var cells = new[] { Block(36), Block(width), Block(width * 1.4), Block(64) };
        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }
        AutomationProperties.SetAccessibilityView(grid, AccessibilityView.Raw);
        return grid;
    }
}
