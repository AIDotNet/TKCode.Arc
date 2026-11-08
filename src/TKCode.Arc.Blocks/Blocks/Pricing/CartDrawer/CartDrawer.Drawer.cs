using TKCode.Arc.DesignTokens;
using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The drawer half of the block: built on first open inside the block, it holds the header, the rows, the free-shipping
/// card, the promo code, the totals, checkout and the receipt.
/// </summary>
public partial class CartDrawer
{
    /// <summary>The checkout disc's spring (fitted ω ≈ 8.5, ζ ≈ 1: visual duration 0.6 s, no bounce).</summary>
    internal static readonly SpringSpec DiscSpring = SpringSpec.FromVisual(0.6, 0);

    private static readonly Easing s_checkEasing = new QuinticEaseOut();

    private Border? _drawer;
    private readonly TranslateTransform _drawerShift = new();
    private TextBlock? _drawerTitle;
    private StackPanel? _countPanel;
    private RollingDigits? _countDigits;
    private TextBlock? _countNoun;
    private CartDrawerPlainButton? _closeButton;
    private CartDrawerSwap? _bodySwap;
    private ScrollViewer? _listScroll;
    private Border? _shipCard;
    private ArcGlyph? _truck;
    private MotionValue<double>? _truckMix;
    private bool _truckUnlocked;
    private CartDrawerSwap? _shipText;
    private string? _shipTextValue;
    private CartDrawerMeter? _meter;
    private StackPanel? _rowsPanel;
    private StackPanel? _emptyView;
    private Button? _browseButton;
    private Control? _receiptView;
    private Border? _discSlot;
    private Canvas? _discLayer;
    private Border? _disc;
    private SpringSizeDecorator? _summary;
    private StackPanel? _summaryStack;
    private Control? _promoBlock;
    private SpringSizeDecorator? _promoSize;
    private CartDrawerSwap? _promoSwap;
    private StackPanel? _promoForm;
    private Border? _promoField;
    private readonly TranslateTransform _promoShake = new();
    private Input? _promoInput;
    private CartDrawerPlainButton? _applyButton;
    private CartDrawerSwap? _promoHint;
    private Control? _promoChip;
    private CartDrawerPlainButton? _chipRemove;
    private bool _promoInvalid;
    private RollingDigits? _subtotalDigits;
    private SpringSizeDecorator? _discountRow;
    private Control? _discountContent;
    private RollingDigits? _discountDigits;
    private CartDrawerSwap? _shippingSwap;
    private string? _shippingText;
    private RollingDigits? _totalDigits;
    private CartDrawerSwap? _checkoutSwap;
    private CartDrawerCheckoutButton? _checkout;
    private CartDrawerSwap? _checkoutContent;
    private Control? _checkoutIdle;
    private Control? _checkoutBusy;
    private RollingDigits? _checkoutAmount;
    private Button? _continueButton;
    private MotionValue<double>? _drawerX;
    private MotionValue<double>? _drawerFade;
    private MotionValue<double>? _scrimFade;
    private MotionValue<double>? _shakeValue;
    private readonly Dictionary<Control, MotionValue<double>> _fades = new();
    private bool _holdReceipt;
    private bool _summaryShown = true;

    // ---- Exposed for tests ----

    internal Border? DrawerPart => _drawer;
    internal CartDrawerPlainButton? CloseButtonPart => _closeButton;
    internal CartDrawerCheckoutButton? CheckoutButtonPart => _checkout;
    internal Button? ContinueButtonPart => _continueButton;
    internal Button? BrowseButtonPart => _browseButton;
    internal Input? PromoInputPart => _promoInput;
    internal CartDrawerPlainButton? PromoApplyPart => _applyButton;
    internal CartDrawerPlainButton? PromoRemovePart => _chipRemove;
    internal CartDrawerMeter? MeterPart => _meter;
    internal Border? ShippingCardPart => _shipCard;
    internal CartDrawerCartButton? CartButtonPartControl => _cartButton;
    internal Border? DiscPart => _disc;
    internal double DrawerOffset => _drawerShift.X;
    internal double ScrimOpacity => _scrim?.Opacity ?? 0;
    internal bool IsPromoInvalid => _promoInvalid;
    internal double PromoShakeOffset => _promoShake.X;
    internal Control? BodyView => _bodySwap?.Current;
    internal Control? EmptyViewPart => _emptyView;
    internal Control? ReceiptViewPart => _receiptView;
    internal string? ShippingCardText => _shipTextValue;
    internal string? ShippingValueText => _shippingText;
    internal bool IsDiscountRowShown => _discountRow?.IsExpanded ?? false;
    internal bool IsSummaryShown => _summaryShown;
    internal IReadOnlyList<CartDrawerRow> RowParts => _rowsPanel?.Children.OfType<CartDrawerRow>().ToList() ?? (IReadOnlyList<CartDrawerRow>)Array.Empty<CartDrawerRow>();
    internal CartDrawerRow? RowFor(CartDrawerProduct product) => Find(product)?.Row;

    // ---- Open / close ----

    private void OnIsOpenChanged(bool open)
    {
        if (_drawerHost is null || _scrim is null)
        {
            return;
        }
        if (open)
        {
            if (_holdReceipt)
            {
                _holdReceipt = false;
                ResetDrawerView();
            }
            EnsureDrawer();
            UpdateDrawer(animate: false);
            ShowDrawer();
        }
        else
        {
            if (State == CartDrawerState.Placed)
            {
                ResetAfterOrder();
            }
            HideDrawerAnimated();
        }
    }

    private void ShowDrawer()
    {
        if (_drawer is null || _scrim is null)
        {
            return;
        }
        _drawer.IsVisible = true;
        _drawer.IsHitTestVisible = true;
        _scrim.IsVisible = true;
        if (_shopScroller is not null)
        {
            // The shop behind is inert while the drawer is open.
            _shopScroller.IsHitTestVisible = false;
        }
        _drawerX ??= CreateValue(1.04, v => _drawerShift.X = v * DrawerTravelWidth());
        _drawerFade ??= CreateValue(1, v =>
        {
            if (_drawer is not null)
            {
                _drawer.Opacity = v;
            }
        });
        _scrimFade ??= CreateValue(0, v =>
        {
            if (_scrim is not null)
            {
                _scrim.Opacity = v;
            }
        });
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Full)
        {
            // Open on the smooth spring from 104% of the drawer's width; the scrim fades in 280ms.
            _drawerFade.Jump(1);
            _ = _drawerX.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            _ = _scrimFade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(280), ArcEasings.Standard));
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            // Reduced motion: the drawer fades in place (180ms), the scrim in 100ms.
            _drawerX.Jump(0);
            if (_drawerFade.Value >= 1 && !_drawerFade.IsAnimating)
            {
                _drawerFade.Jump(0);
            }
            _ = _drawerFade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Standard));
            _ = _scrimFade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
        }
        else
        {
            _drawerX.Jump(0);
            _drawerFade.Jump(1);
            _scrimFade.Jump(1);
        }
        var keyboard = _keyboardInput;
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && _closeButton is not null)
            {
                _closeButton.Focus(CartDrawerBuild.Method(keyboard));
            }
        }, DispatcherPriority.Loaded);
    }

    private async void HideDrawerAnimated()
    {
        if (_drawer is null || _scrim is null || _drawerX is null || _drawerFade is null || _scrimFade is null)
        {
            return;
        }
        // Focus goes back to the cart button before the drawer leaves.
        if (_drawer.IsKeyboardFocusWithin || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is null)
        {
            _cartButton?.Focus(CartDrawerBuild.Method(_keyboardInput));
        }
        _drawer.IsHitTestVisible = false;
        if (_shopScroller is not null)
        {
            _shopScroller.IsHitTestVisible = true;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        bool done;
        if (level == ArcMotionLevel.Full)
        {
            _ = _scrimFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(280), ArcEasings.Standard));
            done = await _drawerX.AnimateTo(1.04, MotionSpec.Spring(ArcSpring.Settle));
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _ = _scrimFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
            done = await _drawerFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Standard));
        }
        else
        {
            _scrimFade.Jump(0);
            _drawerX.Jump(1.04);
            done = true;
        }
        if (done && !IsOpen)
        {
            _drawer.IsVisible = false;
            _scrim.IsVisible = false;
            _drawerX.Jump(1.04);
            _drawerFade.Jump(1);
            if (_holdReceipt)
            {
                _holdReceipt = false;
                ResetDrawerView();
            }
        }
    }

    private double DrawerTravelWidth()
    {
        var width = _drawer?.Bounds.Width ?? 0;
        return width > 0 ? width : TokenDouble(CartDrawerTokenKind.DrawerWidth, 400);
    }

    private MotionValue<double> CreateValue(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += apply;
        apply(initial);
        return value;
    }

    /// <summary>Fades <paramref name="target"/> to <paramref name="to"/> (opacity) over <paramref name="duration"/>.</summary>
    private Task<bool> Fade(Control target, double to, TimeSpan duration, bool animate, TimeSpan delay = default)
    {
        if (!_fades.TryGetValue(target, out var value))
        {
            value = new MotionValue<double>(this, target.Opacity, MotionVectors.Double);
            value.Changed += v => target.Opacity = v;
            _fades[target] = value;
        }
        if (!animate)
        {
            value.Jump(to);
            return Task.FromResult(true);
        }
        return value.AnimateTo(to, MotionSpec.Tween(duration, ArcEasings.Standard, delay));
    }

    private void ResetAfterOrder()
    {
        // The drawer keeps showing the receipt while it slides away; the cart and the badge start over at once.
        _holdReceipt = true;
        foreach (var entry in _entries)
        {
            entry.Row = null;
        }
        _entries.Clear();
        AppliedPromo = null;
        Receipt = null;
        State = CartDrawerState.Shopping;
        _pendingFlights = 0;
        Recompute(animate: true);
    }

    private void ResetDrawerView()
    {
        if (_drawer is null)
        {
            return;
        }
        _discLayer?.Children.Clear();
        _disc = null;
        _receiptView = null;
        if (_drawerTitle is not null)
        {
            _drawerTitle.Text = Labels.DrawerTitle;
        }
        if (_countPanel is not null)
        {
            _countPanel.IsVisible = true;
        }
        LeavePlacing();
        _checkoutContent?.Show(_checkoutIdle, animate: false);
        _checkoutSwap?.Show(_checkout, animate: false);
        if (_bodySwap is not null)
        {
            _bodySwap.Show(null, animate: false);
        }
        RebuildRows();
        SyncPromoView(animate: false);
        UpdateDrawer(animate: false);
    }

    // ---- Building ----

    private void EnsureDrawer()
    {
        if (_drawer is not null || _drawerHost is null)
        {
            return;
        }
        var labels = Labels;

        // Header: title, rolling count, close.
        _drawerTitle = new TextBlock { Text = labels.DrawerTitle, VerticalAlignment = VerticalAlignment.Center, LetterSpacing = -0.18 };
        ArcTokens.Bind(_drawerTitle, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_drawerTitle, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeLG);
        ArcTokens.Bind(_drawerTitle, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_drawerTitle, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _countDigits = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextMuted);
        _countNoun = CartDrawerBuild.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextMuted);
        _countNoun.TextTrimming = TextTrimming.None;
        _countPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(8, 1, 0, 0), Children = { _countDigits, _countNoun } };
        _closeButton = CartDrawerBuild.IconButton(ArcIcons.X, 32, 17, labels.Close, ArcDesignTokenKind.TextSecondary);
        _closeButton.Click += (_, e) =>
        {
            e.Handled = true;
            if (State != CartDrawerState.Placing)
            {
                Close();
            }
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(20, 14, 12, 10), MinHeight = 32 };
        header.Children.Add(_drawerTitle);
        Grid.SetColumn(_countPanel, 1);
        header.Children.Add(_countPanel);
        Grid.SetColumn(_closeButton, 2);
        header.Children.Add(_closeButton);

        // Body: the list (shipping card + rows), the empty state, later the receipt.
        _truck = new ArcGlyph { Data = TruckGlyph, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        _truck.AttachedToVisualTree += (_, _) => PaintTruck();
        _truck.ActualThemeVariantChanged += (_, _) => PaintTruck();
        _shipText = new CartDrawerSwap { VerticalAlignment = VerticalAlignment.Center };
        _meter = new CartDrawerMeter();
        BindToken(_meter, CartDrawerMeter.TrackBrushProperty, CartDrawerTokenKind.TrackColor);
        ArcTokens.Bind(_meter, CartDrawerMeter.FillBrushProperty, ArcDesignTokenKind.Accent);
        BindToken(_meter, CartDrawerMeter.CompleteBrushProperty, CartDrawerTokenKind.SuccessInk);
        AutomationProperties.SetName(_meter, labels.FreeShippingProgress);
        var shipLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _truck, _shipText } };
        _shipCard = new Border
        {
            Padding = new Thickness(14, 12),
            Margin = new Thickness(8, 0),
            Child = new StackPanel { Spacing = 8, Children = { shipLine, _meter } }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(_shipCard, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _rowsPanel = new StackPanel();
        _listScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new StackPanel { Spacing = 12, Margin = new Thickness(12, 0, 12, 12), Children = { _shipCard, _rowsPanel } }
        };

        _emptyText = CartDrawerBuild.Text(labels.EmptyCart, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary);
        _emptyText.HorizontalAlignment = HorizontalAlignment.Center;
        _browseButton = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            Content = labels.BrowseProducts,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _browseButton.Click += (_, e) =>
        {
            e.Handled = true;
            Close();
        };
        var bag = CartDrawerBuild.Glyph(ShoppingBagGlyph, 22, ArcDesignTokenKind.TextMuted, 1.5);
        bag.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyView = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 32),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { bag, _emptyText, _browseButton }
        };
        _bodySwap = new CartDrawerSwap();

        // Footer: promo code, totals, checkout.
        BuildPromo(labels);
        var totals = BuildTotals(labels);
        _summaryStack = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 0, 14), Children = { _promoBlock!, totals } };
        _summary = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, Child = _summaryStack };
        BuildCheckout(labels);
        _checkoutSwap = new CartDrawerSwap { ExitDuration = TimeSpan.FromMilliseconds(1), ExitOffset = 0, EnterOffset = 0 };
        _checkoutSwap.Show(_checkout, animate: false);
        var footer = new Border
        {
            Padding = new Thickness(16, 12, 16, 16),
            Child = new StackPanel { Children = { _summary, _checkoutSwap } }
        };
        ArcTokens.Bind(footer, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(footer, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        layout.Children.Add(header);
        Grid.SetRow(_bodySwap, 1);
        layout.Children.Add(_bodySwap);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        _discLayer = new Canvas { IsHitTestVisible = false };
        Grid.SetRowSpan(_discLayer, 3);
        layout.Children.Add(_discLayer);

        var inner = new Border { ClipToBounds = true, Child = layout }.BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
        _drawer = new Border
        {
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            RenderTransform = _drawerShift,
            Child = inner
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
        ArcTokens.Bind(_drawer, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(_drawer, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_drawer, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(_drawer, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);
        AutomationProperties.SetName(_drawer, labels.DrawerTitle);
        // A real focus trap: Tab and Shift+Tab cycle inside the drawer while it is open.
        KeyboardNavigation.SetTabNavigation(_drawer, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetTabNavigation(_discLayer, KeyboardNavigationMode.None);
        _drawerHost.Children.Add(_drawer);
        _drawerX = null;
        _drawerFade = null;
        LayoutDrawer();
        RebuildRows();
        SyncPromoView(animate: false);
        _bodySwap.Show(_entries.Count > 0 ? _listScroll : _emptyView, animate: false);
        _summaryShown = true;
        SetSummaryVisible(_entries.Count > 0, animate: false);
    }

    private TextBlock? _emptyText;

    private void BuildPromo(CartDrawerLabels labels)
    {
        _applyButton = new CartDrawerPlainButton { Content = labels.PromoApply, Height = 32, VerticalAlignment = VerticalAlignment.Center };
        _applyButton.Classes.Add("text");
        ArcTokens.Bind(_applyButton, Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_applyButton, Avalonia.Controls.Primitives.TemplatedControl.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        AutomationProperties.SetName(_applyButton, labels.PromoApply);
        _applyButton.Click += (_, e) =>
        {
            e.Handled = true;
            SubmitPromo();
        };
        _promoInput = new Input { Placeholder = labels.PromoPlaceholder, Suffix = _applyButton };
        AutomationProperties.SetName(_promoInput, labels.PromoPlaceholder);
        _promoInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitPromo();
            }
        };
        _promoInput.TextChanged += (_, _) =>
        {
            // Any edit after a refusal clears the error (the refused text itself does not).
            if (_promoInvalid && !string.Equals(_promoInput?.Text?.Trim(), _rejectedCode, StringComparison.Ordinal))
            {
                SetPromoInvalid(null, animate: true);
            }
        };
        _promoField = new Border { Child = _promoInput, RenderTransform = _promoShake };
        _promoHint = new CartDrawerSwap { Margin = new Thickness(2, 0, 0, 0) };
        _promoForm = new StackPanel { Spacing = 6, Children = { _promoField, _promoHint } };
        _promoSwap = new CartDrawerSwap();
        _promoSize = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, Child = _promoSwap };
        _promoBlock = _promoSize;
        SetPromoInvalid(null, animate: false);
    }

    private Control BuildChip(CartDrawerPromo promo, CartDrawerLabels labels)
    {
        var tag = new ArcGlyph { Data = ArcIcons.Tag, Size = 15, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        BindToken(tag, ArcGlyph.ForegroundProperty, CartDrawerTokenKind.SuccessInk);
        var code = CartDrawerBuild.Text(promo.Code, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, medium: true);
        code.TextTrimming = TextTrimming.None;
        var percent = (promo.PercentOff * 100m).ToString("0.##", labels.Culture) + "%";
        var off = CartDrawerBuild.Text(promo.Label ?? labels.PromoPercentOff(percent), ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        _chipRemove = new CartDrawerPlainButton { Content = labels.PromoRemove, Height = 30, VerticalAlignment = VerticalAlignment.Center };
        _chipRemove.Classes.Add("text");
        _chipRemove.CornerRadius = new CornerRadius(12);
        ArcTokens.Bind(_chipRemove, Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetName(_chipRemove, labels.PromoRemoveName(promo.Code));
        _chipRemove.Click += (_, e) =>
        {
            e.Handled = true;
            RemovePromo();
        };
        var info = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { tag, code } };
        off.Margin = new Thickness(-4, 0, 0, 0);
        info.Children.Add(off);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(info);
        Grid.SetColumn(_chipRemove, 1);
        grid.Children.Add(_chipRemove);
        var chip = new Border { Height = 40, Padding = new Thickness(12, 0, 6, 0), Child = grid }.BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        BindToken(chip, Border.BackgroundProperty, CartDrawerTokenKind.SuccessFill);
        return chip;
    }

    private Control BuildTotals(CartDrawerLabels labels)
    {
        _subtotalDigits = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        _discountDigits = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        BindToken(_discountDigits, RollingDigits.ForegroundProperty, CartDrawerTokenKind.SuccessInk);
        _totalDigits = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeBase, ArcDesignTokenKind.Foreground, medium: true);
        _shippingSwap = new CartDrawerSwap { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        _shippingText = null;

        var subtotal = TotalRow(labels.Subtotal, _subtotalDigits, total: false);
        _discountContent = TotalRow(labels.Discount, _discountDigits, total: false);
        _discountRow = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, IsExpanded = false, Child = _discountContent };
        var shipping = TotalRow(labels.Shipping, _shippingSwap, total: false);
        var totalRow = TotalRow(labels.Total, _totalDigits, total: true);
        var totalFrame = new Border { Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(0, 8, 0, 0), Child = totalRow };
        ArcTokens.Bind(totalFrame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(totalFrame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        return new StackPanel { Children = { subtotal, _discountRow, shipping, totalFrame } };
    }

    private static Grid TotalRow(string label, Control value, bool total)
    {
        var text = total
            ? CartDrawerBuild.Text(label, ArcDesignTokenKind.TypeBase, ArcDesignTokenKind.Foreground, medium: true, lineHeight: 22.4)
            : CartDrawerBuild.Text(label, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = total ? default : new Thickness(0, 2) };
        grid.Children.Add(text);
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);
        return grid;
    }

    private void BuildCheckout(CartDrawerLabels labels)
    {
        _checkoutAmount = CartDrawerBuild.Digits(ArcDesignTokenKind.TypeBase, ArcDesignTokenKind.AccentForeground, medium: true);
        var label = new TextBlock { Text = labels.Checkout, VerticalAlignment = VerticalAlignment.Center };
        var idle = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        idle.Children.Add(label);
        Grid.SetColumn(_checkoutAmount, 1);
        idle.Children.Add(_checkoutAmount);
        _checkoutIdle = idle;
        var spinner = new ArcSpinner { Width = 16, Height = 16, Thickness = 2, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.AccentForeground);
        _checkoutBusy = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { spinner, new TextBlock { Text = labels.PlacingOrder, VerticalAlignment = VerticalAlignment.Center } }
        };
        _checkoutContent = new CartDrawerSwap { ExitDuration = TimeSpan.FromMilliseconds(100), ExitOffset = 0, EnterDuration = ArcMotionPresets.Standard, EnterOffset = 2 };
        _checkoutContent.Show(_checkoutIdle, animate: false);
        _checkout = new CartDrawerCheckoutButton { Content = _checkoutContent };
        _checkout.Click += (_, e) =>
        {
            e.Handled = true;
            _ = CheckoutAsync();
        };
        _continueButton = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Large,
            Content = labels.ContinueShopping,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        _continueButton.Click += (_, e) =>
        {
            e.Handled = true;
            Close();
        };
    }

    private void RebuildDrawerText()
    {
        if (_drawer is null || _drawerHost is null)
        {
            return;
        }
        var open = IsOpen && _drawer.IsVisible;
        _drawerHost.Children.Remove(_drawer);
        _drawer = null;
        _fades.Clear();
        foreach (var entry in _entries)
        {
            entry.Row = null;
        }
        if (open)
        {
            EnsureDrawer();
            _drawer!.IsVisible = true;
            _drawerX = CreateValue(0, v => _drawerShift.X = v * DrawerTravelWidth());
        }
    }

    private void LayoutDrawer()
    {
        if (_drawer is null)
        {
            return;
        }
        var width = Bounds.Width;
        var inset = _narrow ? TokenDouble(CartDrawerTokenKind.DrawerInsetNarrow, 4) : TokenDouble(CartDrawerTokenKind.DrawerInset, 8);
        _drawer.Margin = new Thickness(inset);
        var drawerWidth = TokenDouble(CartDrawerTokenKind.DrawerWidth, 400);
        if (_narrow || (width > 0 && width - 2 * inset < drawerWidth))
        {
            _drawer.Width = double.NaN;
            _drawer.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            _drawer.Width = drawerWidth;
            _drawer.HorizontalAlignment = HorizontalAlignment.Right;
        }
        var thumb = _narrow ? TokenDouble(CartDrawerTokenKind.ThumbnailSizeNarrow, 52) : TokenDouble(CartDrawerTokenKind.ThumbnailSize, 64);
        foreach (var row in RowParts)
        {
            row.SetThumbnailSize(thumb);
        }
    }

    private void BindToken(StyledElement target, AvaloniaProperty property, CartDrawerTokenKind kind)
        => target.Bind(property, target.GetResourceObservable(CartDrawerTokens.Key(kind)), BindingPriority.Template);

    // ---- Rows ----

    private void RebuildRows()
    {
        if (_rowsPanel is null)
        {
            return;
        }
        _rowsPanel.Children.Clear();
        foreach (var entry in _entries)
        {
            entry.Row = null;
        }
        SyncRows();
    }

    private void SyncRows()
    {
        if (_rowsPanel is null)
        {
            return;
        }
        var labels = Labels;
        var thumb = _narrow ? TokenDouble(CartDrawerTokenKind.ThumbnailSizeNarrow, 52) : TokenDouble(CartDrawerTokenKind.ThumbnailSize, 64);
        foreach (var entry in _entries)
        {
            if (entry.Row is null)
            {
                entry.Row = CreateRow(entry.Product);
                entry.Row.SetThumbnailSize(thumb);
                _rowsPanel.Children.Add(entry.Row);
            }
            entry.Row.Update(entry.Quantity, entry.Product.MaxQuantity, labels, Currency, AmountDecimals);
        }
    }

    private CartDrawerRow CreateRow(CartDrawerProduct product)
    {
        var row = new CartDrawerRow(product)
        {
            CanSwipe = () => IsSwipeToRemoveEnabled && State == CartDrawerState.Shopping && ArcMotion.IsFull(this)
        };
        BindToken(row, CartDrawerRow.RemoveFillProperty, CartDrawerTokenKind.RemoveFill);
        BindToken(row, CartDrawerRow.RemoveFillArmedProperty, CartDrawerTokenKind.RemoveFillArmed);
        BindToken(row.RemoveLabel, TextBlock.ForegroundProperty, CartDrawerTokenKind.RemoveInk);
        BindToken(row.RemoveGlyph, ArcGlyph.ForegroundProperty, CartDrawerTokenKind.RemoveInk);
        row.DecreaseRequested += (_, _) =>
        {
            var quantity = Find(product)?.Quantity ?? 0;
            if (quantity > 1)
            {
                SetQuantity(product, quantity - 1);
            }
            else
            {
                RemoveCore(product, swiped: false);
            }
        };
        row.IncreaseRequested += (_, _) => SetQuantity(product, (Find(product)?.Quantity ?? 0) + 1);
        row.RemoveRequested += (_, _) => RemoveCore(product, swiped: false);
        row.SwipeRemoved += (_, _) => RemoveCore(product, swiped: true);
        return row;
    }

    private async Task LeaveRow(CartDrawerRow row, bool swiped)
    {
        if (_rowsPanel is null || !_rowsPanel.Children.Contains(row))
        {
            return;
        }
        await row.LeaveAsync(swiped, row.ReleaseVelocity);
        _rowsPanel?.Children.Remove(row);
    }

    private void FocusAfterRemoval(int index, bool removeButton)
    {
        var keyboard = _keyboardInput;
        Control? target = null;
        var next = index < _entries.Count ? _entries[index] : index - 1 >= 0 && index - 1 < _entries.Count ? _entries[index - 1] : null;
        if (next?.Row is { } row)
        {
            target = removeButton ? row.RemoveButton : row.DecreaseButton;
        }
        else
        {
            target = _browseButton;
        }
        Dispatcher.UIThread.Post(() => target?.Focus(CartDrawerBuild.Method(keyboard)), DispatcherPriority.Loaded);
    }

    // ---- State to screen ----

    private void UpdateDrawer(bool animate)
    {
        if (_drawer is null)
        {
            return;
        }
        var labels = Labels;
        if (_countDigits is not null && _countNoun is not null)
        {
            _countDigits.IsInstant = !animate;
            _countDigits.CultureName = labels.Culture.Name;
            _countDigits.Value = ItemCount;
            _countNoun.Text = labels.ItemNoun(ItemCount);
            AutomationProperties.SetName(_countDigits, ItemCount.ToString(labels.Culture));
        }
        if (_holdReceipt || State == CartDrawerState.Placed)
        {
            return;
        }
        SyncRows();
        var hasItems = _entries.Count > 0;

        // Free shipping card (measured on the subtotal before the discount).
        var threshold = FreeShippingThreshold;
        var showShip = threshold > 0 && hasItems;
        if (_shipCard is not null && _shipText is not null && _meter is not null)
        {
            _shipCard.IsVisible = showShip;
            if (showShip)
            {
                var unlocked = Subtotal >= threshold;
                var text = unlocked ? labels.FreeShippingUnlocked : labels.FreeShippingRemaining(Money(CartDrawerMath.Remaining(Subtotal, threshold)));
                if (text != _shipTextValue)
                {
                    _shipTextValue = text;
                    var block = CartDrawerBuild.Text(text, ArcDesignTokenKind.TypeSM, unlocked ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
                    _shipText.Show(block, animate);
                }
                _meter.Set(CartDrawerMath.Progress(Subtotal, threshold), unlocked, animate);
                AutomationProperties.SetHelpText(_meter, $"{Money(Math.Min(Subtotal, threshold))} / {Money(threshold)}");
                SetTruck(unlocked, animate);
            }
            else
            {
                _shipTextValue = null;
            }
        }

        // Body: rows or the empty state.
        if (_bodySwap is not null && _listScroll is not null && _emptyView is not null)
        {
            var view = hasItems ? (Control)_listScroll : _emptyView;
            if (!ReferenceEquals(_bodySwap.Current, view))
            {
                _bodySwap.ExitDuration = ArcMotionPresets.Fast;
                _bodySwap.ExitOffset = 0;
                _bodySwap.EnterOffset = 0;
                _bodySwap.EnterDuration = ArcMotionPresets.Standard;
                _bodySwap.EnterDelay = hasItems ? TimeSpan.Zero : TimeSpan.FromMilliseconds(120);
                _bodySwap.Show(view, animate);
            }
        }

        SetSummaryVisible(hasItems, animate);
        if (_promoBlock is not null)
        {
            _promoBlock.IsVisible = IsPromoVisible;
        }
        var culture = labels.Culture;
        if (_subtotalDigits is not null)
        {
            SetAmount(_subtotalDigits, Subtotal, animate);
        }
        if (_discountDigits is not null && _discountRow is not null && _discountContent is not null)
        {
            if (Discount > 0 || AppliedPromo is null)
            {
                SetAmount(_discountDigits, Discount, animate, negative: true);
            }
            var show = AppliedPromo is not null;
            if (_discountRow.IsExpanded != show)
            {
                _discountRow.IsExpanded = show;
                _ = Fade(_discountContent, show ? 1 : 0, TimeSpan.FromMilliseconds(170), animate);
            }
        }
        if (_shippingSwap is not null)
        {
            var text = Shipping <= 0 && hasItems && (threshold <= 0 || Subtotal >= threshold) ? labels.Free : Money(Shipping);
            if (text != _shippingText)
            {
                _shippingText = text;
                _shippingSwap.Show(new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Right }.Also(t =>
                {
                    ArcTokens.Bind(t, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
                    ArcTokens.Bind(t, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
                    t.LineHeight = 19.6;
                }), animate);
            }
        }
        if (_totalDigits is not null)
        {
            SetAmount(_totalDigits, Total, animate);
        }
        if (_checkout is not null && _checkoutAmount is not null)
        {
            SetAmount(_checkoutAmount, Total, animate);
            _checkout.IsEnabled = hasItems;
            AutomationProperties.SetName(_checkout, labels.CheckoutName(Money(Total)));
        }
        _ = culture;
    }

    private void SetAmount(RollingDigits digits, decimal amount, bool animate, bool negative = false)
    {
        digits.IsInstant = !animate;
        CartDrawerFormat.Apply(digits, amount, Currency, Labels.Culture, AmountDecimals, negative);
        AutomationProperties.SetName(digits, (negative && amount > 0 ? CartDrawerFormat.Minus : string.Empty) + Money(amount));
    }

    private void SetTruck(bool unlocked, bool animate)
    {
        if (_truck is null)
        {
            return;
        }
        if (_truckMix is null)
        {
            _truckMix = new MotionValue<double>(this, unlocked ? 1 : 0, MotionVectors.Double);
            _truckMix.Changed += _ => PaintTruck();
            _truckUnlocked = unlocked;
        }
        if (unlocked != _truckUnlocked)
        {
            _truckUnlocked = unlocked;
            if (animate)
            {
                _ = _truckMix.AnimateTo(unlocked ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
            else
            {
                _truckMix.Jump(unlocked ? 1 : 0);
            }
        }
        PaintTruck();
    }

    private void PaintTruck()
    {
        if (_truck is null || TopLevel.GetTopLevel(_truck) is null)
        {
            return;
        }
        var muted = ArcTokens.GetColor(_truck, ArcDesignTokenKind.TextMuted);
        var green = this.TryFindResource(CartDrawerTokens.Key(CartDrawerTokenKind.SuccessInk), ActualThemeVariant, out var value) && value is ISolidColorBrush b
            ? b.Color
            : muted;
        var t = Math.Clamp(_truckMix?.Value ?? 0, 0, 1);
        _truck.Foreground = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb(
            (byte)Math.Round(muted.A + (green.A - muted.A) * t),
            (byte)Math.Round(muted.R + (green.R - muted.R) * t),
            (byte)Math.Round(muted.G + (green.G - muted.G) * t),
            (byte)Math.Round(muted.B + (green.B - muted.B) * t)));
    }

    private void SetSummaryVisible(bool show, bool animate)
    {
        if (_summary is null || _summaryStack is null || show == _summaryShown)
        {
            return;
        }
        _summaryShown = show;
        _summary.IsExpanded = show;
        _ = Fade(_summaryStack, show ? 1 : 0, ArcMotionPresets.Fast, animate);
    }

    // ---- Promo code ----

    private void SubmitPromo()
    {
        if (_promoInput is null || State != CartDrawerState.Shopping)
        {
            return;
        }
        _ = ApplyPromoAsync(_promoInput.Text ?? string.Empty);
    }

    private void SyncPromoView(bool animate)
    {
        if (_promoSwap is null || _promoForm is null)
        {
            return;
        }
        if (AppliedPromo is { } promo)
        {
            _promoChip = BuildChip(promo, Labels);
            _promoSwap.ExitDuration = TimeSpan.FromMilliseconds(170);
            _promoSwap.ExitOffset = 0;
            _promoSwap.ExitScale = 1;
            _promoSwap.EnterDuration = TimeSpan.FromMilliseconds(200);
            _promoSwap.EnterOffset = 0;
            _promoSwap.EnterScale = 0.96;
            _promoSwap.Show(_promoChip, animate);
        }
        else
        {
            if (_promoInput is not null)
            {
                _promoInput.Text = string.Empty;
            }
            SetPromoInvalid(null, animate: false);
            _promoSwap.ExitDuration = TimeSpan.FromMilliseconds(200);
            _promoSwap.ExitOffset = 0;
            _promoSwap.ExitScale = 0.96;
            _promoSwap.EnterDuration = TimeSpan.FromMilliseconds(200);
            _promoSwap.EnterOffset = 0;
            _promoSwap.EnterScale = 1;
            _promoSwap.Show(_promoForm, animate);
            _promoChip = null;
            _chipRemove = null;
        }
    }

    private string? _rejectedCode;

    private void OnPromoRejected(string code)
    {
        _rejectedCode = code;
        SetPromoInvalid(code.ToUpper(Labels.Culture), animate: true);
        if (ArcMotion.IsFull(this))
        {
            _shakeValue ??= CreateValue(1, t => _promoShake.X = CartDrawerMath.ShakeOffset(t));
            _shakeValue.Jump(0);
            _ = _shakeValue.AnimateTo(1, MotionSpec.Tween(CartDrawerMath.ShakeDuration, s_linear, TimeSpan.FromMilliseconds(50)));
        }
    }

    /// <summary>Shows the refusal for <paramref name="code"/> (upper case), or the hint again when null.</summary>
    private void SetPromoInvalid(string? code, bool animate)
    {
        if (_promoHint is null || _promoInput is null)
        {
            return;
        }
        _promoInvalid = code is not null;
        _promoInput.IsInvalid = _promoInvalid;
        var text = code is not null ? Labels.PromoInvalid(code) : Labels.PromoHint;
        if (string.IsNullOrEmpty(text))
        {
            _promoHint.Show(null, animate);
            return;
        }
        var line = CartDrawerBuild.Text(text, ArcDesignTokenKind.TypeXS, code is not null ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        line.HorizontalAlignment = HorizontalAlignment.Left;
        _promoHint.Show(line, animate);
    }

    private void FocusPromoRemove()
    {
        var keyboard = _keyboardInput;
        Dispatcher.UIThread.Post(() => _chipRemove?.Focus(CartDrawerBuild.Method(keyboard)), DispatcherPriority.Loaded);
    }

    private void FocusPromoInput()
    {
        var keyboard = _keyboardInput;
        Dispatcher.UIThread.Post(() => _promoInput?.Focus(CartDrawerBuild.Method(keyboard)), DispatcherPriority.Loaded);
    }

    // ---- Checkout ----

    private void EnterPlacing()
    {
        if (_checkout is null || _checkoutContent is null)
        {
            return;
        }
        _checkout.IsBusy = true;
        _checkoutContent.Show(_checkoutBusy);
        AutomationProperties.SetName(_checkout, Labels.PlacingOrder);
        SetInert(true);
    }

    private void LeavePlacing()
    {
        if (_checkout is null || _checkoutContent is null)
        {
            return;
        }
        _checkout.IsBusy = false;
        _checkoutContent.Show(_checkoutIdle);
        SetInert(false);
        UpdateDrawer(animate: false);
    }

    private void SetInert(bool inert)
    {
        foreach (var part in new Control?[] { _bodySwap, _summary })
        {
            if (part is null)
            {
                continue;
            }
            part.IsHitTestVisible = !inert;
            KeyboardNavigation.SetTabNavigation(part, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        }
    }

    private void ShowReceipt(CartDrawerReceipt receipt)
    {
        if (_drawer is null || _bodySwap is null || _checkout is null || _checkoutSwap is null || _discLayer is null)
        {
            return;
        }
        var labels = Labels;
        _checkout.IsBusy = false;
        if (_drawerTitle is not null)
        {
            _drawerTitle.Text = labels.OrderPlacedTitle;
        }
        if (_countPanel is not null)
        {
            _countPanel.IsVisible = false;
        }
        _receiptView = BuildReceipt(receipt, labels);
        _bodySwap.ExitDuration = TimeSpan.FromMilliseconds(150);
        _bodySwap.ExitOffset = 0;
        _bodySwap.EnterOffset = 0;
        _bodySwap.EnterDuration = TimeSpan.FromMilliseconds(220);
        _bodySwap.EnterDelay = TimeSpan.FromMilliseconds(130);
        _bodySwap.Show(_receiptView);
        _summaryShown = true;
        SetSummaryVisible(false, animate: true);

        var full = ArcMotion.IsFull(this) && _drawer.IsVisible;
        var start = _checkout.TranslatePoint(default, _discLayer) is { } p ? new Rect(p, _checkout.Bounds.Size) : default;
        _checkoutSwap.EnterDelay = full ? TimeSpan.FromMilliseconds(300) : TimeSpan.Zero;
        _checkoutSwap.EnterDuration = TimeSpan.FromMilliseconds(300);
        _checkoutSwap.Show(_continueButton);

        _disc = BuildDisc(out var check);
        if (full && start.Width > 0)
        {
            _ = MorphDisc(_disc, check, start);
        }
        else if (_discSlot is not null)
        {
            _discSlot.Child = _disc;
            check.Progress = 1;
        }
        var keyboard = _keyboardInput;
        Dispatcher.UIThread.Post(() => _continueButton?.Focus(CartDrawerBuild.Method(keyboard)), DispatcherPriority.Loaded);
    }

    private Border BuildDisc(out StrokeDrawGlyph check)
    {
        check = new StrokeDrawGlyph
        {
            Data = ArcIcons.Check,
            Size = 32,
            StrokeThickness = 2.2,
            Progress = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(check, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.AccentForeground);
        var disc = new Border { Child = check, Width = 64, Height = 64, CornerRadius = new CornerRadius(32) };
        ArcTokens.Bind(disc, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
        return disc;
    }

    /// <summary>
    /// The checkout button becomes the confirmation disc: its bounds and corner radius spring from the button (366 × 50,
    /// radius 18) to the 64px circle above the receipt (visual duration 0.6 s, no bounce), and the check draws itself after
    /// 400ms in 250ms (ease-out).
    /// </summary>
    /// <remarks>
    /// Code-driven: the target is the receipt's slot, which moves while the footer collapses, so it is read every frame;
    /// animating bounds instead of scale keeps the check unstretched.
    /// </remarks>
    private async Task MorphDisc(Border disc, StrokeDrawGlyph check, Rect start)
    {
        if (_discLayer is null)
        {
            return;
        }
        var radius = TokenRadius(ArcDesignTokenKind.RadiusControl, 18);
        var size = TokenDouble(CartDrawerTokenKind.DiscSize, 64);
        void Apply(double p)
        {
            var target = _discSlot is not null && _discSlot.TranslatePoint(default, _discLayer) is { } t && _discSlot.Bounds.Width > 0
                ? new Rect(t, new Size(size, size))
                : new Rect(start.Center.X - size / 2, start.Y - size, size, size);
            var x = start.X + (target.X - start.X) * p;
            var y = start.Y + (target.Y - start.Y) * p;
            var w = Math.Max(1, start.Width + (target.Width - start.Width) * p);
            var h = Math.Max(1, start.Height + (target.Height - start.Height) * p);
            Canvas.SetLeft(disc, x);
            Canvas.SetTop(disc, y);
            disc.Width = w;
            disc.Height = h;
            var r = Math.Min(Math.Min(w, h) / 2, radius + (size / 2 - radius) * Math.Clamp(p, 0, 1));
            disc.CornerRadius = new CornerRadius(r);
        }
        Apply(0);
        _discLayer.Children.Add(disc);
        var progress = new MotionValue<double>(disc, 0, MotionVectors.Double);
        progress.Changed += Apply;
        _ = check.Play(TimeSpan.FromMilliseconds(250), s_checkEasing, TimeSpan.FromMilliseconds(400));
        await progress.AnimateTo(1, MotionSpec.Spring(DiscSpring));
        progress.Dispose();
        if (!ReferenceEquals(_disc, disc) || _discSlot is null)
        {
            return;
        }
        _discLayer.Children.Remove(disc);
        disc.Width = size;
        disc.Height = size;
        disc.CornerRadius = new CornerRadius(size / 2);
        _discSlot.Child = disc;
    }

    private Control BuildReceipt(CartDrawerReceipt receipt, CartDrawerLabels labels)
    {
        _discSlot = new Border { Width = 64, Height = 64, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Center };
        var title = new TextBlock { Text = receipt.Title, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, LineHeight = 25.2, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(title, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeLG);
        ArcTokens.Bind(title, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(title, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var message = CartDrawerBuild.Text(receipt.Message, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);
        message.TextWrapping = TextWrapping.Wrap;
        message.TextTrimming = TextTrimming.None;
        message.TextAlignment = TextAlignment.Center;
        message.MaxWidth = 304;
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.IsVisible = !string.IsNullOrEmpty(receipt.Message);
        var rows = new StackPanel { Spacing = 6 };
        rows.Children.Add(ReceiptRow(labels.ReceiptItems, receipt.ItemCount.ToString(labels.Culture)));
        rows.Children.Add(ReceiptRow(labels.ReceiptPaid, Money(receipt.Paid)));
        if (!string.IsNullOrEmpty(receipt.Arrives))
        {
            rows.Children.Add(ReceiptRow(labels.ReceiptArrives, receipt.Arrives));
        }
        var list = new Border { Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(0, 12, 0, 0), MaxWidth = 320, Child = rows };
        ArcTokens.Bind(list, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(list, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        return new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(28, 24),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _discSlot, title, message, list }
        };
    }

    private static Grid ReceiptRow(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(CartDrawerBuild.Text(label, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6));
        var text = CartDrawerBuild.Text(value, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, lineHeight: 19.6);
        text.TextTrimming = TextTrimming.None;
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private double TokenRadius(ArcDesignTokenKind kind, double fallback)
        => ArcTokens.GetCornerRadius(this, kind, new CornerRadius(fallback)).TopLeft;
}

internal static class CartDrawerObjectExtensions
{
    /// <summary>Runs <paramref name="configure"/> on <paramref name="value"/> and returns it (for expression-bodied builders).</summary>
    public static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
