using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Theme colors and type the stage hands to its parts.</summary>
internal sealed record WalletStackInks(
    IBrush Foreground, IBrush Secondary, IBrush Muted, IBrush Border, IBrush SurfaceMuted, IBrush Success, IBrush FocusRing,
    FontFamily Display, FontFamily Body, double SmallSize, double ExtraSmallSize, double BalanceSize);

/// <summary>
/// The open card's activity (Arc Wallet stack, design reference): the balance (label and amount, counting up) over a
/// rule, then the three latest transactions. A new body fades in over 300ms; its rows fade in on Smooth (700ms) and
/// rise 10px, staggered 80 / 136 / 192ms. Switching cards fades the old body out over 180ms first.
/// </summary>
/// <remarks>
/// Code-driven because the body is rebuilt per card and each row starts on its own delay; the balance count-up is a
/// spring whose value is formatted every frame. Under reduced motion the fades remain and nothing moves; the balance
/// shows its final value at once.
/// </remarks>
internal sealed class WalletStackDetail : Control
{
    public const int RowCount = 3;

    /// <summary>Row delays: 80ms, then every 56ms.</summary>
    public static TimeSpan RowDelay(int row) => TimeSpan.FromMilliseconds(80 + 56 * row);

    private static readonly Easing s_bodyEase = new SplineEasing(0.25, 0.1, 0.35, 1);
    private static readonly Easing s_exitEase = new SplineEasing(0, 0, 0.58, 1);
    private static readonly Easing s_rowEase = new ArcSpringEasing(ArcMotionPresets.Smooth);

    private readonly ScrollViewer _scroller;
    private readonly MotionValue<double> _bodyOpacity;
    private readonly MotionValue<double> _balance;
    private readonly List<Row> _rows = new();
    private StackPanel? _body;
    private TextBlock? _money;
    private WalletStackFormat? _format;
    private int _generation;

    public WalletStackDetail()
    {
        _scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Focusable = false,
        };
        VisualChildren.Add(_scroller);
        LogicalChildren.Add(_scroller);
        _bodyOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _bodyOpacity.Changed += value =>
        {
            if (_body is not null)
            {
                _body.Opacity = value;
            }
        };
        _balance = new MotionValue<double>(this, 0, MotionVectors.Double);
        _balance.Changed += ShowBalance;
    }

    /// <summary>The card whose activity is shown, or null.</summary>
    public WalletStackCard? Card { get; private set; }

    /// <summary>The balance text as shown now (tests read it).</summary>
    public string? BalanceText => _money?.Text;

    /// <summary>The rows' opacities and offsets (tests read them).</summary>
    public IReadOnlyList<Control> RowControls => _rows.ConvertAll(r => (Control)r.Host);

    /// <summary>
    /// Shows <paramref name="card"/>'s activity. When another card is shown, it fades out first and the balance counts
    /// from its amount; otherwise from zero.
    /// </summary>
    public async void Show(WalletStackCard card, WalletStackFormat format, WalletStackLabels labels, WalletStackInks inks)
    {
        var generation = ++_generation;
        var previous = Card;
        Card = card;
        _format = format;
        if (previous is not null && _body is not null && !ReferenceEquals(previous, card))
        {
            var finished = await _bodyOpacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), s_exitEase));
            if (generation != _generation || (!finished && _bodyOpacity.Value > 0.001 && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off))
            {
                return;
            }
        }
        Build(card, labels, inks);
        _bodyOpacity.Jump(0);
        _ = _bodyOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(300), s_bodyEase));
        var from = previous is not null && !ReferenceEquals(previous, card) ? (double)previous.Balance : 0;
        _balance.Jump(from);
        ShowBalance(from);
        CountUp(card, generation);
        var full = ArcMotion.IsFull(this);
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            row.Opacity.Jump(0);
            row.Offset.Jump(full ? 10 : 0);
            _ = row.Opacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(700), s_rowEase, RowDelay(i)));
            if (full)
            {
                _ = row.Offset.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, RowDelay(i)));
            }
        }
    }

    // The balance counts on Smooth and lands on the exact amount (the spring rests a hair short of it).
    private async void CountUp(WalletStackCard card, int generation)
    {
        await _balance.AnimateTo((double)card.Balance, MotionSpec.Spring(ArcSpring.Smooth));
        if (generation == _generation && _money is not null && _format is not null)
        {
            _money.Text = _format.Money(card.Balance);
        }
    }

    /// <summary>Forgets the shown card (after the stack closed) so the next open counts up from zero.</summary>
    public void Reset()
    {
        _generation++;
        Card = null;
    }

    /// <summary>Re-applies colors and type after a theme change.</summary>
    public void Restyle(WalletStackInks inks)
    {
        if (_body is null)
        {
            return;
        }
        foreach (var block in _body.GetLogicalDescendants().OfType<TextBlock>())
        {
            if (block.Tag is string role)
            {
                block.Foreground = role switch
                {
                    "secondary" => inks.Secondary,
                    "muted" => inks.Muted,
                    "success" => inks.Success,
                    _ => inks.Foreground,
                };
            }
        }
        foreach (var border in _body.GetLogicalDescendants().OfType<Border>())
        {
            if (border.Tag is "rule")
            {
                border.BorderBrush = inks.Border;
            }
        }
    }

    private void ShowBalance(double value)
    {
        if (_money is null || _format is null || Card is null)
        {
            return;
        }
        _money.Text = _format.Money((decimal)Math.Round(value, 2));
    }

    private void Build(WalletStackCard card, WalletStackLabels labels, WalletStackInks inks)
    {
        foreach (var row in _rows)
        {
            row.Dispose();
        }
        _rows.Clear();
        var body = new StackPanel { Spacing = 4 };
        AutomationProperties.SetName(this, labels.ActivityName(card));

        var label = new TextBlock
        {
            Text = card.BalanceLabel, Tag = "secondary", Foreground = inks.Secondary, FontFamily = inks.Body,
            FontSize = inks.SmallSize, LineHeight = inks.SmallSize * 1.4, VerticalAlignment = VerticalAlignment.Bottom,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        _money = new TextBlock
        {
            Tag = "foreground", Foreground = inks.Foreground, FontFamily = inks.Display, FontSize = inks.BalanceSize,
            LetterSpacing = -0.03 * inks.BalanceSize, LineHeight = inks.BalanceSize * 1.1, FontFeatures = Tabular(),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
        };
        var balanceRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        balanceRow.Children.Add(label);
        Grid.SetColumn(_money, 1);
        balanceRow.Children.Add(_money);
        body.Children.Add(new Border
        {
            Tag = "rule", Child = balanceRow, Padding = new Thickness(2, 0, 2, 8), BorderBrush = inks.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
        });

        var list = new StackPanel();
        AutomationProperties.SetName(list, labels.TransactionsName(card));
        var count = Math.Min(RowCount, card.Transactions.Count);
        for (var i = 0; i < count; i++)
        {
            var row = new Row(this, card.Transactions[i], _format!, inks);
            _rows.Add(row);
            list.Children.Add(row.Host);
        }
        body.Children.Add(list);
        _body = body;
        _scroller.Content = body;
        _scroller.Offset = default;
    }

    private static FontFeatureCollection Tabular() => new() { FontFeature.Parse("tnum") };

    protected override Size MeasureOverride(Size availableSize)
    {
        _scroller.Measure(availableSize);
        return _scroller.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _scroller.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>One transaction: badge, merchant over detail, signed amount (money in uses the success color).</summary>
    private sealed class Row : IDisposable
    {
        private readonly TranslateTransform _shift = new();

        public Row(Visual owner, WalletStackTransaction transaction, WalletStackFormat format, WalletStackInks inks)
        {
            var badge = new WalletStackBadge { VerticalAlignment = VerticalAlignment.Center };
            badge.Set(transaction, inks.SurfaceMuted, inks.Secondary, new Typeface(inks.Body));
            var merchant = new TextBlock
            {
                Text = transaction.Merchant, Tag = "foreground", Foreground = inks.Foreground, FontFamily = inks.Body,
                FontSize = inks.SmallSize, LineHeight = inks.SmallSize * 1.3, TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var detail = new TextBlock
            {
                Text = transaction.Detail, Tag = "muted", Foreground = inks.Muted, FontFamily = inks.Body,
                FontSize = inks.ExtraSmallSize, LineHeight = inks.ExtraSmallSize * 1.3, TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { merchant, detail } };
            var positive = transaction.Amount > 0;
            var amount = new TextBlock
            {
                Text = format.Signed(transaction.Amount), Tag = positive ? "success" : "foreground",
                Foreground = positive ? inks.Success : inks.Foreground, FontFamily = inks.Body, FontSize = inks.SmallSize,
                LineHeight = inks.SmallSize * 1.4, FontFeatures = Tabular(), VerticalAlignment = VerticalAlignment.Center,
            };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*,Auto"), ColumnSpacing = 10 };
            grid.Children.Add(badge);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            Grid.SetColumn(amount, 2);
            grid.Children.Add(amount);
            Host = new Border { Child = grid, Padding = new Thickness(2, 4), RenderTransform = _shift };
            AutomationProperties.SetName(Host, $"{transaction.Merchant}, {transaction.Detail}, {amount.Text}");
            Opacity = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Offset = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Opacity.Changed += value => Host.Opacity = value;
            Offset.Changed += value => _shift.Y = value;
            Host.Opacity = 0;
        }

        public Border Host { get; }

        public MotionValue<double> Opacity { get; }

        public MotionValue<double> Offset { get; }

        public void Dispose()
        {
            Opacity.Dispose();
            Offset.Dispose();
        }
    }
}
