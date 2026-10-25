using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class PhoneInput
{
    private const int Page = 8;
    private const double PanelMax = 340;
    private const double ListMaxHeight = 280;
    private const double ScrollInset = 6;
    private const double FaceInset = 6;

    #region 内部属性定义（浮层材质）

    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<PhoneInput, IBrush?>("SurfaceBrush");

    internal static readonly StyledProperty<IBrush?> SurfaceRaisedBrushProperty =
        AvaloniaProperty.Register<PhoneInput, IBrush?>("SurfaceRaisedBrush");

    internal static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<PhoneInput, IBrush?>("EdgeBrush");

    internal static readonly StyledProperty<BoxShadows> FloatingShadowProperty =
        AvaloniaProperty.Register<PhoneInput, BoxShadows>("FloatingShadow");

    /// <summary>The gliding row highlight: foreground at 6.5% (set by the theme from the PhoneInput token).</summary>
    internal static readonly StyledProperty<IBrush?> OptionHighlightProperty =
        AvaloniaProperty.Register<PhoneInput, IBrush?>(nameof(OptionHighlight));

    /// <summary>Clear button fill and close-target hover: foreground at 8%.</summary>
    internal static readonly StyledProperty<IBrush?> ControlFillProperty =
        AvaloniaProperty.Register<PhoneInput, IBrush?>(nameof(ControlFill));

    internal IBrush? OptionHighlight { get => GetValue(OptionHighlightProperty); set => SetValue(OptionHighlightProperty, value); }

    internal IBrush? ControlFill { get => GetValue(ControlFillProperty); set => SetValue(ControlFillProperty, value); }

    #endregion

    private PhoneInputSurface? _surface;
    private Panel? _lid;
    private Border? _lidFill;
    private PhoneInputFace? _lidFace;
    private RotateTransform? _chevronTurn;
    private DockPanel? _searchRow;
    private PhoneInputSearchBox? _search;
    private PhoneInputPresence? _clearHost;
    private AvaloniaButton? _clear;
    private AvaloniaButton? _closeHit;
    private Border? _faceBorder;
    private ScrollViewer? _scroll;
    private PhoneInputCountryList? _list;
    private MotionValue<double>? _chevronAngle;
    private MotionValue<double>? _searchFade;
    private MotionValue<double>? _lidFaceFade;
    private MotionValue<double>? _lidFillFade;
    private string _query = string.Empty;
    private string? _activeKey;
    private bool _scrollIntent;
    private bool _syncingQuery;
    private int _closeGeneration;
    private Dictionary<PhoneCountry, int>? _order;
    private PhoneCountry[]? _sorted;
    private TopLevel? _pressHost;
    private bool _open;

    /// <summary>Opens the country list on the current country.</summary>
    public void OpenPicker() => OpenPicker(string.Empty);

    /// <summary>Closes the country list (focus stays where it is).</summary>
    public void ClosePicker() => ClosePicker(PhoneInputFocusTarget.None);

    internal PhoneInputSurface? Surface => _surface;

    internal PhoneInputCountryList? CountryList => _list;

    internal PhoneInputSearchBox? SearchBox => _search;

    internal string Query => _query;

    /// <summary>The active option as assistive technology hears it (the search field's activedescendant).</summary>
    internal string? ActiveOptionText =>
        _open && _list?.CountryAt(ActiveIndex()) is { } country ? $"{country.Name} +{country.Dial}" : null;

    private double LidHeight => Frame is { Bounds.Height: > 2 } frame ? frame.Bounds.Height - 2 : 42;

    // ---------------------------------------------------------------- popup

    private void ConfigurePopup(PhoneInputPopup popup)
    {
        popup.Closed += OnPopupClosed;
        popup.PlacementTarget = _anchor;
    }

    private static readonly Thickness s_shadowRoom = new(56, 40, 56, 80);

    /// <summary>
    /// Room for the floating shadow around the shape, trimmed where the window ends so the popup never has to slide
    /// (a slid popup would no longer start exactly over the trigger). One pixel stays for the open step-out.
    /// </summary>
    private Thickness ShadowRoomFor(Size capacity)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || _anchor is null || _anchor.TranslatePoint(default, topLevel) is not { } origin)
        {
            return s_shadowRoom;
        }
        var client = topLevel.ClientSize;
        static double Fit(double wanted, double space) => Math.Clamp(Math.Min(wanted, Math.Floor(space)), Math.Min(1, wanted), wanted);
        return new Thickness(
            Fit(s_shadowRoom.Left, origin.X),
            Fit(s_shadowRoom.Top, origin.Y),
            Fit(s_shadowRoom.Right, client.Width - (origin.X + capacity.Width)),
            Fit(s_shadowRoom.Bottom, client.Height - (origin.Y + capacity.Height)));
    }

    private void EnsureSurface()
    {
        if (_surface is not null || _popup is null)
        {
            return;
        }
        ArcTokens.Bind(this, SurfaceBrushProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, SurfaceRaisedBrushProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(this, EdgeBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, FloatingShadowProperty, ArcDesignTokenKind.ShadowFloating);

        // Lid: the trigger row as it appears on the surface, then the search row that takes its place.
        _lidFill = new Border { Margin = new Thickness(3), Opacity = 0, IsHitTestVisible = false };
        _lidFill.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        ArcTokens.Bind(_lidFill, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _lidFace = new PhoneInputFace
        {
            Margin = new Thickness(12, 0, 36, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(_lidFace, PhoneInputFace.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_lidFace, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_lidFace, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        TextElement.SetFontWeight(_lidFace, FontWeight.Medium);
        _chevronTurn = new RotateTransform();
        var chevron = new ArcGlyph
        {
            Data = ArcIcons.ChevronDown,
            Size = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            RenderTransform = _chevronTurn,
            RenderTransformOrigin = RelativePoint.Center,
            IsHitTestVisible = false
        };
        ArcTokens.Bind(chevron, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _searchRow = BuildSearchRow();
        _lid = new Panel
        {
            Height = LidHeight,
            Background = Brushes.Transparent,
            Children = { _lidFill, _lidFace, _searchRow, chevron }
        };
        _lid.PointerPressed += (_, e) => e.Handled = true;

        // Face: the country list hanging under the lid, with a hairline at its top.
        _list = new PhoneInputCountryList();
        ArcTokens.Bind(_list, PhoneInputCountryList.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_list, PhoneInputCountryList.EmphasisForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_list, PhoneInputCountryList.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_list, PhoneInputCountryList.SmallFontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_list, PhoneInputCountryList.LineHeightFactorProperty, ArcDesignTokenKind.LeadingBody);
        ArcTokens.Bind(_list, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_list, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        _list.Bind(PhoneInputCountryList.HighlightBrushProperty, this.GetObservable(OptionHighlightProperty));
        _list.RowHovered += row => Activate(row, scroll: false);
        _list.RowInvoked += Pick;
        _list.SelectedCountry = _country;
        _scroll = new ScrollViewer
        {
            Padding = new Thickness(0, ScrollInset, 0, 0),
            MaxHeight = ListMaxHeight + ScrollInset,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _list
        };
        var hairline = new Border { Height = 1, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        ArcTokens.Bind(hairline, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
        _faceBorder = new Border
        {
            Padding = new Thickness(FaceInset, 0, FaceInset, FaceInset),
            Child = new Panel { Children = { _scroll, hairline } }
        };

        _surface = new PhoneInputSurface(_lid, _faceBorder) { LidHeight = LidHeight };
        _surface.OutsidePressed += (_, _) => ClosePicker(PhoneInputFocusTarget.None, keyboard: false);
        _popup.Child = _surface;
        UpdatePickerTexts();
        ApplySurfaceStyle();
    }

    private DockPanel BuildSearchRow()
    {
        var glyph = new ArcGlyph { Data = ArcIcons.Search, Size = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        DockPanel.SetDock(glyph, Dock.Left);

        _search = new PhoneInputSearchBox { Field = this, Margin = new Thickness(0, 0, 8, 0) };
        _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        _search.PropertyChanged += OnSearchPropertyChanged;

        _clear = CreateRoundButton(ArcIcons.X, 14, 24, filled: true);
        _clear.Click += (_, _) =>
        {
            OnQuery(string.Empty, syncInput: true);
            _search?.Focus(NavigationMethod.Pointer);
        };
        // The clear button pops on the glide spring from 0.6 and leaves back to 0.6 in 0.1s (reduced: at once).
        _clearHost = new PhoneInputPresence
        {
            EnterSpring = PhoneInputFace.Glide,
            EnterScale = 0.6,
            ExitScale = 0.6,
            ExitDuration = TimeSpan.FromMilliseconds(100),
            ReducedDuration = TimeSpan.Zero,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _clear
        };
        DockPanel.SetDock(_clearHost, Dock.Right);
        // An invisible 36px target over the shared chevron: the chevron closes the list.
        _closeHit = CreateRoundButton(null, 16, 36, filled: false);
        _closeHit.Click += (_, _) => ClosePicker(PhoneInputFocusTarget.Trigger, keyboard: false);
        DockPanel.SetDock(_closeHit, Dock.Right);
        var row = new DockPanel
        {
            Margin = new Thickness(13, 0, 2, 0),
            IsVisible = false,
            Opacity = 0,
            Children = { glyph, _closeHit, _clearHost, _search }
        };
        row.PointerPressed += (_, e) => e.Handled = true;
        row.PointerReleased += (_, e) => e.Handled = true;
        return row;
    }

    private AvaloniaButton CreateRoundButton(Geometry? glyph, double glyphSize, double size, bool filled)
    {
        var button = new AvaloniaButton
        {
            Width = size,
            Height = size,
            Padding = default,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
            Template = new Avalonia.Controls.Templates.FuncControlTemplate<AvaloniaButton>((b, _) =>
            {
                var face = new Border().BindCornerRadius(ArcDesignTokenKind.RadiusPill);
                face.Bind(Border.BackgroundProperty, b.GetObservable(BackgroundProperty));
                if (glyph is not null)
                {
                    var mark = new ArcGlyph
                    {
                        Data = glyph,
                        Size = glyphSize,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    mark.Bind(ArcGlyph.ForegroundProperty, b.GetObservable(ForegroundProperty));
                    face.Child = mark;
                }
                return face;
            })
        };
        ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        if (filled)
        {
            button.Bind(BackgroundProperty, this.GetObservable(ControlFillProperty));
            button.PointerEntered += (_, e) =>
            {
                if (e.Pointer.Type != PointerType.Touch)
                {
                    ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.Foreground);
                }
            };
            button.PointerExited += (_, _) => ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        }
        else
        {
            button.PointerEntered += (_, e) =>
            {
                if (e.Pointer.Type != PointerType.Touch)
                {
                    button.Background = ControlFill;
                }
            };
            button.PointerExited += (_, _) => button.Background = Brushes.Transparent;
        }
        return button;
    }

    private void UpdatePickerTexts()
    {
        if (_search is not null)
        {
            _search.PlaceholderText = SearchPlaceholder;
            AutomationProperties.SetName(_search, SearchLabel);
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, ListLabel);
        }
        if (_clear is not null)
        {
            AutomationProperties.SetName(_clear, ClearSearchLabel);
        }
        if (_closeHit is not null)
        {
            AutomationProperties.SetName(_closeHit, CloseListLabel);
        }
    }

    private void ApplySurfaceStyle()
    {
        if (_surface is null)
        {
            return;
        }
        static Color C(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        _surface.RestFill = C(GetValue(SurfaceBrushProperty));
        _surface.OpenFill = C(GetValue(SurfaceRaisedBrushProperty));
        _surface.EdgePen = new Pen(GetValue(EdgeBrushProperty), 1);
        _surface.OpenShadow = GetValue(FloatingShadowProperty);
        _surface.InvalidateVisual();
    }

    // ---------------------------------------------------------------- sections

    private PhoneCountry[] SortedPool()
    {
        if (_sorted is null || _order is null)
        {
            _sorted = _pool.OrderBy(c => c.Name, StringComparer.InvariantCulture).ToArray();
            _order = new Dictionary<PhoneCountry, int>();
            for (var i = 0; i < _sorted.Length; i++)
            {
                _order[_sorted[i]] = i;
            }
        }
        return _sorted;
    }

    private void RebuildSections()
    {
        if (_list is null)
        {
            return;
        }
        var needle = _query.Trim().ToLowerInvariant();
        var sorted = SortedPool();
        var sections = new List<PhoneInputCountryList.Section>(2);
        if (needle.Length > 0 && needle != "+")
        {
            IEnumerable<PhoneCountry> hits = sorted.Where(c => PhoneNumbers.MatchesQuery(c, needle));
            var digits = PhoneNumbers.OnlyDigits(needle);
            if (digits.Length > 0)
            {
                // An exact calling code first, then shorter codes (a stable sort, as Arc's).
                hits = hits.OrderByDescending(c => c.Dial == digits ? 1 : 0).ThenBy(c => c.Dial.Length);
            }
            sections.Add(new PhoneInputCountryList.Section("r", null, hits.ToArray()));
        }
        else
        {
            var preferred = new List<PhoneCountry>();
            foreach (var code in PreferredCountries)
            {
                var entry = _pool.FirstOrDefault(c => string.Equals(c.Iso, code, StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    preferred.Add(entry);
                }
            }
            if (preferred.Count > 0)
            {
                sections.Add(new PhoneInputCountryList.Section("p", SuggestedLabel, preferred));
            }
            sections.Add(new PhoneInputCountryList.Section("a", preferred.Count > 0 ? AllCountriesLabel : null, sorted));
        }
        var empty = sections.Sum(s => s.Countries.Count) == 0
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, NoMatchesTextFormat, _query.Trim())
            : null;
        _list.SetContent(sections, empty);
        _list.SelectedCountry = _country;
    }

    private void RefreshListIfOpen()
    {
        if (!_open)
        {
            return;
        }
        RebuildSections();
        ApplyActive();
        ResizeOpen();
    }

    /// <summary>The active row: the one picked by key if it is still listed, else the first (Arc's activeKey).</summary>
    private int ActiveIndex()
    {
        if (_list is null)
        {
            return -1;
        }
        var index = _list.IndexOfKey(_activeKey);
        return index >= 0 ? index : _list.RowCount > 0 ? 0 : -1;
    }

    /// <summary>Arc's highlight effect: glide to the active row; keep it in view when the keyboard moved it.</summary>
    private void ApplyActive()
    {
        if (_list is null)
        {
            return;
        }
        if (!_open)
        {
            _list.SetActive(-1, instantHide: true);
            return;
        }
        var index = ActiveIndex();
        _list.SetActive(index);
        if (_scrollIntent && index >= 0)
        {
            _scrollIntent = false;
            _list.ScrollIntoView(index, _scroll);
        }
    }

    private void Activate(int row, bool scroll)
    {
        if (_list is null || row < 0 || row >= _list.RowCount)
        {
            return;
        }
        _scrollIntent = scroll;
        _activeKey = _list.KeyAt(row);
        ApplyActive();
    }

    private double FaceHeight() => ScrollInset + Math.Min(ListMaxHeight, _list?.Extent ?? 0) + FaceInset;

    // ---------------------------------------------------------------- open / close

    internal void OnTriggerClicked()
    {
        if (_open)
        {
            ClosePicker(PhoneInputFocusTarget.Trigger, keyboard: false);
        }
        else
        {
            OpenPicker(string.Empty);
        }
    }

    /// <summary>Arc's openList: <paramref name="seed"/> starts a search; without one the current country is active.</summary>
    internal void OpenPicker(string seed)
    {
        if (!IsEffectivelyEnabled || _open || _popup is null || _anchor is null)
        {
            return;
        }
        EnsureSurface();
        var surface = _surface!;
        _closeGeneration++;
        IsPickerOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        SetQuery(seed, syncInput: true);
        RebuildSections();
        _activeKey = seed.Length == 0 && _list!.IndexOfKey("a-" + _country.Iso) >= 0 ? "a-" + _country.Iso : null;
        _scrollIntent = true;
        SetAnnouncement(string.Empty);

        var lidHeight = LidHeight;
        var triggerWidth = _anchor.TargetWidth;
        var shellWidth = Frame?.Bounds.Width ?? triggerWidth;
        var panel = Math.Min(PanelMax, shellWidth);
        var openWidth = Math.Max(triggerWidth, panel);
        var openHeight = lidHeight + 2 + FaceHeight();
        var capacityHeight = lidHeight + 2 + ScrollInset + ListMaxHeight + FaceInset;
        _faceBorder!.Width = panel;
        _lid!.Height = lidHeight;
        surface.LidHeight = lidHeight;
        surface.FaceWidth = panel;
        surface.Capacity = new Size(openWidth, Math.Max(capacityHeight, openHeight));
        if (!_popup.IsOpen)
        {
            surface.ShadowRoom = ShadowRoomFor(surface.Capacity);
            _popup.ShadowRoom = surface.ShadowRoom;
            _lidFace!.Show(_country, _roll, animate: false);
            surface.Jump(new Size(triggerWidth, lidHeight), open: false);
            // The lid continues the trigger's hover / press / focus fill, then lets it go (fast).
            JumpLidFill(TriggerFilled ? 1 : 0);
            _popup.IsOpen = true;
        }
        surface.InvalidateMeasure();
        var target = new Size(openWidth, openHeight);
        if (TopLevel.GetTopLevel(surface) is null)
        {
            // The popup attaches its content a moment later; a motion value without a top level would jump.
            var generation = _closeGeneration;
            void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
            {
                surface.AttachedToVisualTree -= OnAttached;
                if (_open && generation == _closeGeneration)
                {
                    _ = surface.MorphTo(target, open: true);
                }
            }
            surface.AttachedToVisualTree += OnAttached;
        }
        else
        {
            _ = surface.MorphTo(target, open: true);
        }
        TurnChevron(open: true);
        ShowSearch(true);
        FadeLidFace(visible: false);
        FadeLidFill(0);
        ApplyActive();
        AttachOutsidePress();
        _trigger?.RaiseExpandChanged(true);
        Dispatcher.UIThread.Post(() =>
        {
            if (!_open || _list is null)
            {
                return;
            }
            // After layout: the scroller knows its viewport now.
            _scrollIntent = true;
            ApplyActive();
            if (_search is not null)
            {
                _search.Focus(NavigationMethod.Unspecified);
                var end = _search.Text?.Length ?? 0;
                _search.SelectionStart = end;
                _search.SelectionEnd = end;
                _search.CaretIndex = end;
            }
        }, DispatcherPriority.Loaded);
    }

    private bool TriggerFilled => _trigger is { } trigger &&
        (trigger.Classes.Contains(PhoneInputTrigger.HoverPseudoClass) || trigger.IsPressed || trigger.Classes.Contains(":focus-visible"));

    /// <summary>Closes the list and folds the surface back into the trigger; focus goes to <paramref name="target"/>.</summary>
    internal void ClosePicker(PhoneInputFocusTarget target, bool instant = false, bool keyboard = true)
    {
        if (!_open)
        {
            if (instant)
            {
                FinishClose();
            }
            return;
        }
        IsPickerOpen = false;
        PseudoClasses.Set(OpenPseudoClass, false);
        var generation = ++_closeGeneration;
        DetachOutsidePress();
        TurnChevron(open: false);
        ShowSearch(false);
        FadeLidFace(visible: true);
        ApplyActive();
        switch (target)
        {
            case PhoneInputFocusTarget.Trigger:
                _trigger?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
                FadeLidFill(keyboard ? 1 : 0);
                break;
            case PhoneInputFocusTarget.Number:
                FocusNumber();
                break;
        }
        _trigger?.RaiseExpandChanged(false);
        if (instant || _surface is null || !IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            FinishClose();
            return;
        }
        RunCloseMorph(generation);
    }

    private async void RunCloseMorph(int generation)
    {
        if (_surface is null || _anchor is null)
        {
            FinishClose();
            return;
        }
        var reached = await _surface.MorphTo(new Size(_anchor.TargetWidth, LidHeight), open: false);
        if (generation != _closeGeneration || _open)
        {
            return;
        }
        if (!reached && _surface.IsMoving)
        {
            // Retargeted (the trigger changed width while folding back): the newer morph finishes the close.
            return;
        }
        FinishClose();
    }

    private void FinishClose()
    {
        if (_popup is { IsOpen: true })
        {
            // The surface already sits exactly over the trigger: closing needs no fade of its own.
            _popup.IsMotionEnabled = false;
            _popup.IsOpen = false;
        }
        _lidFillFade?.Jump(0);
        if (!_open && _anchor is not null)
        {
            // Rest closed: the next open starts from the trigger again.
            _surface?.Jump(new Size(_anchor.TargetWidth, LidHeight), open: false);
        }
    }

    private void OnAnchorTargetChanged(object? sender, EventArgs e)
    {
        if (_popup is not { IsOpen: true } || _surface is null)
        {
            return;
        }
        if (_open)
        {
            ResizeOpen();
        }
        else
        {
            RunCloseMorph(_closeGeneration);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_open)
        {
            ClosePicker(PhoneInputFocusTarget.None, instant: true);
        }
    }

    /// <summary>The list changed height while open (search): the surface springs to it on the grow spring.</summary>
    private void ResizeOpen()
    {
        if (_surface is null || _anchor is null || !_open)
        {
            return;
        }
        var height = LidHeight + 2 + FaceHeight();
        var width = Math.Max(_anchor.TargetWidth, _surface.FaceWidth);
        if (height > _surface.Capacity.Height || width > _surface.Capacity.Width)
        {
            _surface.Capacity = new Size(Math.Max(width, _surface.Capacity.Width), Math.Max(height, _surface.Capacity.Height));
        }
        _ = _surface.MorphTo(new Size(width, height), open: true);
    }

    private void AttachOutsidePress()
    {
        DetachOutsidePress();
        _pressHost = TopLevel.GetTopLevel(this);
        _pressHost?.AddHandler(PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachOutsidePress()
    {
        _pressHost?.RemoveHandler(PointerPressedEvent, OnHostPointerPressed);
        _pressHost = null;
    }

    /// <summary>Arc: a press outside the field closes the list (focus goes where the press goes).</summary>
    private void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_open || e.Source is not Visual visual)
        {
            return;
        }
        if (ReferenceEquals(visual, this) || this.IsVisualAncestorOf(visual) ||
            (_surface is not null && (ReferenceEquals(visual, _surface) || _surface.IsVisualAncestorOf(visual))))
        {
            return;
        }
        ClosePicker(PhoneInputFocusTarget.None, keyboard: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _closeGeneration++;
        ClosePicker(PhoneInputFocusTarget.None, instant: true);
        DetachOutsidePress();
    }

    // ---------------------------------------------------------------- lid motion

    private void TurnChevron(bool open)
    {
        if (_chevronTurn is null)
        {
            return;
        }
        if (_chevronAngle is null)
        {
            _chevronAngle = new MotionValue<double>(this, 0, MotionVectors.Double);
            _chevronAngle.Changed += v =>
            {
                if (_chevronTurn is not null)
                {
                    _chevronTurn.Angle = v;
                }
            };
        }
        _ = _chevronAngle.AnimateTo(open ? 180 : 0, MotionSpec.Spring(PhoneInputFace.Glide));
    }

    /// <summary>The search row takes the lid's place: it fades in and unblurs (0.18s, 50ms late); out in 0.1s.</summary>
    private void ShowSearch(bool show)
    {
        if (_searchRow is null)
        {
            return;
        }
        if (_searchFade is null)
        {
            _searchFade = new MotionValue<double>(this, 0, MotionVectors.Double);
            _searchFade.Changed += v =>
            {
                if (_searchRow is null)
                {
                    return;
                }
                var t = Math.Clamp(v, 0, 1);
                _searchRow.Opacity = t;
                var radius = ArcMotion.IsFull(this) ? 2 * (1 - t) : 0;
                if (radius < 0.05)
                {
                    _searchRow.Effect = null;
                }
                else
                {
                    _searchRow.Effect = ArcBlur.Get(radius);
                }
            };
        }
        _searchRow.IsHitTestVisible = show;
        if (show)
        {
            _searchRow.IsVisible = true;
            var delay = ArcMotion.IsFull(this) ? TimeSpan.FromMilliseconds(50) : TimeSpan.Zero;
            _ = _searchFade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Enter, delay));
            return;
        }
        _searchFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard)).ContinueWith(
            t => Dispatcher.UIThread.Post(() =>
            {
                if (t.Result && !_open && _searchRow is not null)
                {
                    _searchRow.IsVisible = false;
                }
            }), TaskScheduler.Default);
    }

    /// <summary>The trigger's flag and code hide while the list is open (Arc: instant duration; reduced: none).</summary>
    private void FadeLidFace(bool visible)
    {
        if (_lidFace is null)
        {
            return;
        }
        if (_lidFaceFade is null)
        {
            _lidFaceFade = new MotionValue<double>(this, 1, MotionVectors.Double);
            _lidFaceFade.Changed += v =>
            {
                if (_lidFace is not null)
                {
                    _lidFace.Opacity = Math.Clamp(v, 0, 1);
                }
            };
        }
        if (ArcMotion.IsFull(this))
        {
            _ = _lidFaceFade.AnimateTo(visible ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            _lidFaceFade.Jump(visible ? 1 : 0);
        }
    }

    private MotionValue<double> LidFillFade()
    {
        if (_lidFillFade is null)
        {
            _lidFillFade = new MotionValue<double>(this, 0, MotionVectors.Double);
            _lidFillFade.Changed += v =>
            {
                if (_lidFill is not null)
                {
                    _lidFill.Opacity = Math.Clamp(v, 0, 1);
                }
            };
        }
        return _lidFillFade;
    }

    private void JumpLidFill(double value) => LidFillFade().Jump(value);

    private void FadeLidFill(double target) =>
        _ = LidFillFade().AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));

    // ---------------------------------------------------------------- search

    private void SetQuery(string query, bool syncInput)
    {
        _query = query;
        if (syncInput && _search is not null && _search.Text != query)
        {
            _syncingQuery = true;
            _search.Text = query;
            _search.CaretIndex = query.Length;
            _syncingQuery = false;
        }
        if (_clearHost is not null)
        {
            _clearHost.IsPresent = query.Length > 0;
        }
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncingQuery || _search is null || !_open)
        {
            return;
        }
        OnQuery(_search.Text ?? string.Empty, syncInput: false);
    }

    /// <summary>Arc's onQuery: the first result becomes active, the list scrolls to the top, the count is announced.</summary>
    private void OnQuery(string next, bool syncInput)
    {
        SetQuery(next, syncInput);
        _activeKey = null;
        _scrollIntent = false;
        _scroll?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
        var text = next.Trim().ToLowerInvariant();
        var count = text.Length > 0 && text != "+" ? _pool.Count(c => PhoneNumbers.MatchesQuery(c, text)) : _pool.Count;
        SetAnnouncement(text.Length == 0
            ? string.Empty
            : count > 0 ? $"{count} {(count == 1 ? "country" : "countries")}" : "No matches");
        RebuildSections();
        ApplyActive();
        ResizeOpen();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_open || _list is null)
        {
            return;
        }
        var rows = _list.RowCount;
        var at = ActiveIndex();
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                Activate(Math.Min(rows - 1, at + 1), scroll: true);
                return;
            case Key.Up:
                e.Handled = true;
                Activate(Math.Max(0, at - 1), scroll: true);
                return;
            case Key.PageDown:
                e.Handled = true;
                Activate(Math.Min(rows - 1, at + Page), scroll: true);
                return;
            case Key.PageUp:
                e.Handled = true;
                Activate(Math.Max(0, at - Page), scroll: true);
                return;
            case Key.Home when _query.Length == 0:
                e.Handled = true;
                Activate(0, scroll: true);
                return;
            case Key.End when _query.Length == 0:
                e.Handled = true;
                Activate(rows - 1, scroll: true);
                return;
            case Key.Enter:
                e.Handled = true;
                Pick(at);
                return;
            case Key.Escape:
                e.Handled = true;
                ClosePicker(PhoneInputFocusTarget.Trigger);
                return;
            case Key.Tab:
                // Arc lets Tab close the list and move on: forward to the number, backward to the country button.
                e.Handled = true;
                ClosePicker(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? PhoneInputFocusTarget.Trigger : PhoneInputFocusTarget.Number);
                return;
        }
    }

    /// <summary>Arc's pick: keeps the digits (capped for the new country), announces it, closes onto the number.</summary>
    private void Pick(int row)
    {
        if (_list?.CountryAt(row) is not { } entry)
        {
            return;
        }
        if (!ReferenceEquals(entry, _country))
        {
            SortedPool();
            var order = _order!;
            var delta = order.GetValueOrDefault(entry) - order.GetValueOrDefault(_country);
            _roll = delta == 0 ? 1 : Math.Sign(delta);
            var nextDigits = PhoneNumbers.CapDigits(entry, _digits);
            SetAnnouncement($"{entry.Name}, +{entry.Dial}", update: false);
            Commit(entry, nextDigits, user: true);
        }
        ClosePicker(PhoneInputFocusTarget.Number);
    }
}
