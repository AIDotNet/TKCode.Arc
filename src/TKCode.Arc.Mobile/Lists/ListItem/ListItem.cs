using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TKCode.Arc.Mobile.DesignTokens;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// One row of a <see cref="ListGroup"/>: an optional <see cref="Icon"/> or <see cref="Leading"/> content, the title (the
/// content), an optional <see cref="Subtitle"/>, optional <see cref="Trailing"/> content and an optional chevron. It is a
/// button: give it a <c>Command</c> or handle <c>Click</c> and a finger on it shades the row. It has no hover state. For a
/// row that does not respond to a press (text, a QR code, a custom button), use <see cref="ListCell"/>.
/// </summary>
[PseudoClasses(LeadingPseudoClass)]
public class ListItem : AvaloniaButton
{
    /// <summary>Set while <see cref="Leading"/> holds content: the leading content replaces the icon.</summary>
    public const string LeadingPseudoClass = ":leading";

    /// <summary>The glyph geometry shown before the title, for example a Lucide icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ListItem, Geometry?>(nameof(Icon));

    /// <summary>
    /// Any content before the title, for example an avatar. It takes the first column, the one <see cref="Icon"/> uses, and
    /// replaces the icon when both are set; the title and <see cref="Subtitle"/> stay together beside it. It keeps its own
    /// size (the avatar sizes are the <c>AvatarSize*</c> tokens of <c>ListItemToken</c>).
    /// </summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<ListItem, object?>(nameof(Leading));

    /// <summary>A second, smaller line under the title.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ListItem, string?>(nameof(Subtitle));

    /// <summary>The content at the end of the row: a value, a badge or a control such as a <see cref="Switch"/>.</summary>
    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<ListItem, object?>(nameof(Trailing));

    /// <summary>Whether a chevron at the end of the row shows that it opens another screen.</summary>
    public static readonly StyledProperty<bool> ShowChevronProperty =
        AvaloniaProperty.Register<ListItem, bool>(nameof(ShowChevron));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="LeadingProperty"/>
    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <inheritdoc cref="TrailingProperty"/>
    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    /// <inheritdoc cref="ShowChevronProperty"/>
    public bool ShowChevron
    {
        get => GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ListItem);

    private Grid? _row;
    private ContentPresenter? _leading;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_leading is not null)
        {
            _leading.Content = null;
            (_leading.Parent as Panel)?.Children.Remove(_leading);
            _leading = null;
        }
        _row = e.NameScope.Find<Grid>(RowPart);
        UpdateLeading();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LeadingProperty)
        {
            PseudoClasses.Set(LeadingPseudoClass, change.NewValue is not null);
            UpdateLeading();
        }
    }

    /// <summary>The template's row grid; the leading content goes into its first column.</summary>
    internal const string RowPart = "PART_Row";

    /// <summary>
    /// The presenter of <see cref="Leading"/> (PART_Leading) is built in the row's first column the first time there is
    /// leading content, so the many rows without it carry no extra node.
    /// </summary>
    private void UpdateLeading()
    {
        var leading = Leading;
        if (leading is null)
        {
            if (_leading is not null)
            {
                _leading.Content = null;
                _leading.IsVisible = false;
            }
            return;
        }
        if (_leading is null)
        {
            if (_row is null)
            {
                return;
            }
            var presenter = new ContentPresenter
            {
                Name = "PART_Leading",
                VerticalAlignment = VerticalAlignment.Center,
            };
            presenter.Bind(MarginProperty, presenter.GetResourceObservable(ListItemTokens.Key(ListItemTokenKind.LeadingMargin)));
            Grid.SetColumn(presenter, 0);
            _row.Children.Insert(0, presenter);
            _leading = presenter;
        }
        _leading.Content = leading;
        _leading.IsVisible = true;
    }
}
