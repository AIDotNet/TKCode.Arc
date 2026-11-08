using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The shop half of the block: header, cart button, product grid, flights and the badge's landing feedback.</summary>
public partial class CartDrawer
{
    /// <summary>The Lucide "shopping-bag" glyph.</summary>
    internal static readonly Geometry ShoppingBagGlyph = ArcIcons.Parse(
        "M16 10a4 4 0 0 1-8 0 M3.103 6.034h17.794 M3.4 5.467a2 2 0 0 0-.4 1.2V20a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6.667a2 2 0 0 0-.4-1.2l-2-2.667A2 2 0 0 0 17 2H7a2 2 0 0 0-1.6.8z");

    /// <summary>The Lucide "truck" glyph.</summary>
    internal static readonly Geometry TruckGlyph = ArcIcons.Parse(
        "M14 18V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v11a1 1 0 0 0 1 1h2 M15 18H9 M19 18h2a1 1 0 0 0 1-1v-3.65a1 1 0 0 0-.22-.624l-3.48-4.35A1 1 0 0 0 17.52 8H14 M15 18a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M5 18a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");

    /// <summary>How long the add button says "Added" (measured 1.40 s).</summary>
    internal static readonly TimeSpan AddedHold = TimeSpan.FromMilliseconds(1400);

    private sealed class Card
    {
        public required CartDrawerProduct Product { get; init; }
        public required Control Root { get; init; }
        public required Border ImageFrame { get; init; }
        public required Grid NameRow { get; init; }
        public required TextBlock Price { get; init; }
        public required Button AddButton { get; init; }
        public required CartDrawerSwap Label { get; init; }
        public required Control AddLabel { get; init; }
        public required Control AddedLabel { get; init; }
        public DispatcherTimer? Timer { get; set; }
    }

    private readonly List<Card> _cards = new();
    private readonly ScaleTransform _bump = new();
    private readonly ScaleTransform _rippleScale = new() { ScaleX = 1.45, ScaleY = 1.45 };
    private Panel? _root;
    private ScrollViewer? _shopScroller;
    private TextBlock? _title;
    private Border? _ripple;
    private CartDrawerCartButton? _cartButton;
    private CartDrawerGridPanel? _grid;
    private Border? _scrim;
    private Panel? _drawerHost;
    private Canvas? _flightLayer;
    private TextBlock? _live;
    private ArcGlyph? _cartGlyph;
    private TextBlock? _cartText;
    private RollingDigits? _badgeDigits;
    private MotionValue<double>? _bumpValue;
    private MotionValue<double>? _rippleValue;
    private int _pendingFlights;
    private int _activeFlights;
    private bool _narrow;
    private bool _compact;

    /// <summary>The ripple's overdamped spring (fitted ω 32, ζ 1.5): a strong ease-out of about 590ms.</summary>
    private static readonly Avalonia.Animation.Easings.LinearEasing s_linear = new();

    private static readonly SpringSpec s_rippleSpring = SpringSpec.FromPhysical(32 * 32, 2 * 1.5 * 32);

    /// <summary>The count shown on the badge: added items whose flight has not landed yet are not counted.</summary>
    internal int DisplayedCount => Math.Max(0, ItemCount - _pendingFlights);

    /// <summary>Flights in the air (tests).</summary>
    internal int ActiveFlights => _activeFlights;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_cartButton is not null)
        {
            _cartButton.Click -= OnCartButtonClick;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
        _root = e.NameScope.Find<Panel>(RootPart);
        _shopScroller = e.NameScope.Find<ScrollViewer>(ShopScrollerPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _ripple = e.NameScope.Find<Border>(RipplePart);
        _cartButton = e.NameScope.Find<CartDrawerCartButton>(CartButtonPart);
        _grid = e.NameScope.Find<CartDrawerGridPanel>(GridPart);
        _scrim = e.NameScope.Find<Border>(ScrimPart);
        _drawerHost = e.NameScope.Find<Panel>(DrawerHostPart);
        _flightLayer = e.NameScope.Find<Canvas>(FlightLayerPart);
        _live = e.NameScope.Find<TextBlock>(LivePart);
        _drawer = null;
        _drawerX = null;
        _drawerFade = null;
        _scrimFade = null;
        _fades.Clear();
        _bumpValue = null;
        _rippleValue = null;

        if (_title is not null)
        {
            _title.Text = Title;
        }
        if (_ripple is not null)
        {
            _ripple.RenderTransform = _rippleScale;
        }
        if (_cartButton is not null)
        {
            _cartButton.RenderTransform = _bump;
            _cartButton.Content = BuildCartButtonContent();
            _cartButton.Click += OnCartButtonClick;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        RebuildGrid();
        SeedInitialQuantities();
        Recompute(animate: false);
        UpdateBreakpoints();
        if (IsOpen)
        {
            OnIsOpenChanged(true);
        }
    }

    private Control BuildCartButtonContent()
    {
        _cartGlyph = CartDrawerBuild.Glyph(ShoppingBagGlyph, 18, ArcDesignTokenKind.Foreground);
        _cartText = CartDrawerBuild.Text(Labels.Cart, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, medium: true);
        _cartText.Margin = new Thickness(0, 0, 4, 0);
        _cartText.TextTrimming = TextTrimming.None;
        _badgeDigits = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.AccentForeground, medium: true);
        _badgeDigits.HorizontalAlignment = HorizontalAlignment.Center;
        var badge = new Border
        {
            MinWidth = 22,
            Height = 22,
            Padding = new Thickness(6, 0),
            Margin = new Thickness(-2, 0, 0, 0),
            Child = _badgeDigits
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(badge, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _cartGlyph, _cartText, badge }
        };
    }

    private void OnCartButtonClick(object? sender, RoutedEventArgs e) => Open();

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (State != CartDrawerState.Placing)
        {
            Close();
        }
    }

    // ---- Grid ----

    private void RebuildGrid()
    {
        if (_grid is null)
        {
            return;
        }
        foreach (var card in _cards)
        {
            card.Timer?.Stop();
        }
        _cards.Clear();
        _grid.Children.Clear();
        foreach (var product in EffectiveProducts)
        {
            var card = BuildCard(product);
            _cards.Add(card);
            _grid.Children.Add(card.Root);
        }
        ApplyCardLayout();
        UpdateGridButtons();
    }

    private Card BuildCard(CartDrawerProduct product)
    {
        var image = new Image { Source = product.Image, Stretch = Stretch.UniformToFill };
        var frame = new Border { Child = image, ClipToBounds = true }.BindCornerRadius(ArcDesignTokenKind.RadiusStep20);
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        var aspect = new CartDrawerAspectBox { Child = frame, Margin = new Thickness(0, 0, 0, 8) };

        var name = CartDrawerBuild.Text(product.Name, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, medium: true, lineHeight: 19.6);
        var price = CartDrawerBuild.Text(Money(product.Price), ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, lineHeight: 19.6);
        price.TextTrimming = TextTrimming.None;
        price.Margin = new Thickness(12, 0, 0, 0);
        var nameRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        nameRow.Children.Add(name);
        nameRow.Children.Add(price);
        Grid.SetColumn(price, 1);

        var detail = CartDrawerBuild.Text(product.Detail, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8);
        detail.IsVisible = !string.IsNullOrEmpty(product.Detail);

        var addLabel = AddButtonLabel(ArcIcons.Plus, Labels.AddToCart);
        var addedLabel = AddButtonLabel(ArcIcons.Check, Labels.Added);
        var swap = new CartDrawerSwap
        {
            ExitDuration = TimeSpan.FromMilliseconds(100),
            ExitOffset = 0,
            EnterDuration = ArcMotionPresets.Standard,
            EnterOffset = 2
        };
        swap.Show(addLabel, animate: false);
        var button = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            Content = new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Spring = ArcSpring.Smooth, Child = swap }
        };
        AutomationProperties.SetName(button, Labels.AddToCartName(product.Name));
        button.Click += (_, e) =>
        {
            e.Handled = true;
            Add(product);
        };
        var root = new StackPanel { Spacing = 4, Children = { aspect, nameRow, detail, button } };
        return new Card
        {
            Product = product,
            Root = root,
            ImageFrame = frame,
            NameRow = nameRow,
            Price = price,
            AddButton = button,
            Label = swap,
            AddLabel = addLabel,
            AddedLabel = addedLabel
        };
    }

    private static Control AddButtonLabel(Geometry glyph, string text)
    {
        var icon = new ArcGlyph { Data = glyph, Size = 15, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { icon, label }
        };
    }

    private void ApplyCardLayout()
    {
        foreach (var card in _cards)
        {
            // Narrow blocks put the price on its own line under the name.
            Grid.SetRow(card.Price, _narrow ? 1 : 0);
            Grid.SetColumn(card.Price, _narrow ? 0 : 1);
            card.Price.Margin = _narrow ? default : new Thickness(12, 0, 0, 0);
            card.Price.HorizontalAlignment = _narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }
    }

    private void UpdateGridButtons()
    {
        foreach (var card in _cards)
        {
            var quantity = Find(card.Product)?.Quantity ?? 0;
            // At the row's cap the button rests (Arc keeps flying and announcing without adding anything).
            card.AddButton.IsEnabled = quantity < card.Product.MaxQuantity;
        }
    }

    private void ShowAdded(CartDrawerProduct product)
    {
        var card = _cards.Find(c => ReferenceEquals(c.Product, product));
        if (card is null)
        {
            return;
        }
        card.Label.Show(card.AddedLabel);
        card.Timer?.Stop();
        var timer = new DispatcherTimer { Interval = AddedHold };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            card.Timer = null;
            card.Label.Show(card.AddLabel);
        };
        card.Timer = timer;
        timer.Start();
    }

    private void StopAddedTimers()
    {
        foreach (var card in _cards)
        {
            card.Timer?.Stop();
            card.Timer = null;
            card.Label.Show(card.AddLabel, animate: false);
        }
    }

    // ---- Badge ----

    private void UpdateBadge(bool animate)
    {
        if (_badgeDigits is null || _cartButton is null)
        {
            return;
        }
        var shown = DisplayedCount;
        _badgeDigits.IsInstant = !animate;
        _badgeDigits.CultureName = Labels.Culture.Name;
        _badgeDigits.Value = shown;
        AutomationProperties.SetName(_badgeDigits, shown.ToString(Labels.Culture));
        AutomationProperties.SetName(_cartButton, Labels.CartButtonName(shown));
    }

    // ---- Fly to cart ----

    /// <summary>
    /// Sends a clone of the product's picture along a quadratic Bézier to the cart glyph (780ms, one progress value drives
    /// center, size, radius, rotation, opacity and shadow); the badge counts the item when it lands.
    /// </summary>
    /// <remarks>Code-driven: the path, the shrink and the lift all derive from one eased progress and measured endpoints.</remarks>
    private bool StartFlight(CartDrawerProduct product)
    {
        if (!IsFlightEnabled || _flightLayer is null || _cartGlyph is null || VisualRoot is null || !ArcMotion.IsFull(this))
        {
            return false;
        }
        var card = _cards.Find(c => ReferenceEquals(c.Product, product));
        if (card is null || card.ImageFrame.Bounds.Width <= 0 || !card.ImageFrame.IsEffectivelyVisible)
        {
            return false;
        }
        var origin = card.ImageFrame.TranslatePoint(default, _flightLayer);
        var target = _cartGlyph.TranslatePoint(new Point(_cartGlyph.Bounds.Width / 2, _cartGlyph.Bounds.Height / 2), _flightLayer);
        if (origin is not { } topLeft || target is not { } end)
        {
            return false;
        }
        var startSize = card.ImageFrame.Bounds.Size;
        var start = new Point(topLeft.X + startSize.Width / 2, topLeft.Y + startSize.Height / 2);
        var control = CartDrawerMath.FlightControl(start, end);
        var rotate = new RotateTransform();
        var clone = new Border
        {
            ClipToBounds = true,
            IsHitTestVisible = false,
            RenderTransform = rotate,
            Child = new Image { Source = product.Image, Stretch = Stretch.UniformToFill }
        };
        ArcTokens.Bind(clone, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        var shadow = new BoxShadow();
        void Apply(double p)
        {
            var center = CartDrawerMath.FlightPoint(start, control, end, p);
            var size = CartDrawerMath.FlightSize(startSize, p);
            clone.Width = size.Width;
            clone.Height = size.Height;
            Canvas.SetLeft(clone, center.X - size.Width / 2);
            Canvas.SetTop(clone, center.Y - size.Height / 2);
            clone.CornerRadius = new CornerRadius(CartDrawerMath.FlightRadius(p));
            rotate.Angle = CartDrawerMath.FlightRotation(p);
            clone.Opacity = CartDrawerMath.FlightOpacity(p);
            var (offset, blur, alpha) = CartDrawerMath.FlightShadow(p);
            shadow.OffsetY = offset;
            shadow.Blur = blur;
            shadow.Color = Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0);
            clone.BoxShadow = new BoxShadows(shadow);
        }
        Apply(0);
        _flightLayer.Children.Add(clone);
        _pendingFlights++;
        _activeFlights++;
        var progress = new MotionValue<double>(clone, 0, MotionVectors.Double);
        progress.Changed += Apply;
        _ = Fly(clone, progress);
        return true;
    }

    private async Task Fly(Border clone, MotionValue<double> progress)
    {
        await progress.AnimateTo(1, MotionSpec.Tween(CartDrawerMath.FlightDuration, CartDrawerMath.FlightEasing));
        progress.Dispose();
        _flightLayer?.Children.Remove(clone);
        _activeFlights = Math.Max(0, _activeFlights - 1);
        if (_pendingFlights > 0)
        {
            _pendingFlights--;
            Land();
        }
    }

    /// <summary>The landing: the badge rolls up by one, the button bumps (1 → 1.09 → 0.98 → 1) and a ring ripples out.</summary>
    private void Land()
    {
        UpdateBadge(animate: true);
        if (!ArcMotion.IsFull(this) || _cartButton is null)
        {
            return;
        }
        _bumpValue ??= CreateBumpValue();
        _bumpValue.Jump(0);
        _ = _bumpValue.AnimateTo(1, MotionSpec.Tween(CartDrawerMath.BumpDuration, s_linear));
        if (_ripple is not null)
        {
            _rippleValue ??= CreateRippleValue();
            _rippleValue.Jump(0);
            _ = RunRipple(_rippleValue);
        }
    }

    private MotionValue<double> CreateBumpValue()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += t =>
        {
            var s = CartDrawerMath.BumpScale(t);
            _bump.ScaleX = s;
            _bump.ScaleY = s;
        };
        return value;
    }

    private MotionValue<double> CreateRippleValue()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += t =>
        {
            var (scale, opacity) = CartDrawerMath.Ripple(t);
            _rippleScale.ScaleX = scale;
            _rippleScale.ScaleY = scale;
            if (_ripple is not null)
            {
                _ripple.Opacity = opacity;
            }
        };
        return value;
    }

    private async Task RunRipple(MotionValue<double> value)
    {
        if (await value.AnimateTo(1, MotionSpec.Spring(s_rippleSpring)) && _ripple is not null)
        {
            _ripple.Opacity = 0;
        }
    }

    // ---- Breakpoints ----

    private void UpdateBreakpoints()
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        var compact = width <= TokenDouble(CartDrawerTokenKind.CompactWidth, 640);
        var narrow = width <= TokenDouble(CartDrawerTokenKind.NarrowWidth, 380);
        var changed = compact != _compact || narrow != _narrow;
        _compact = compact;
        _narrow = narrow;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (_cartText is not null)
        {
            _cartText.IsVisible = !narrow;
        }
        if (changed)
        {
            ApplyCardLayout();
        }
        LayoutDrawer();
    }

    private double TokenDouble(CartDrawerTokenKind kind, double fallback)
        => this.TryFindResource(CartDrawerTokens.Key(kind), ActualThemeVariant, out var value) && value is double d ? d : fallback;
}
