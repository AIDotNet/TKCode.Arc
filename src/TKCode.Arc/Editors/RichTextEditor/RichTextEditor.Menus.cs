using TKCode.Arc.Primitives;
using System.Collections.Immutable;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class RichTextEditor
{
    /* ----------------------------------------------------------------------------------------------------------
     * Slash menu
     * -------------------------------------------------------------------------------------------------------- */

    private void SyncSlash()
    {
        if (_slash is not { } slash)
        {
            return;
        }
        var selection = _engine.Selection;
        var blocks = _engine.Blocks;
        var valid = selection.IsCollapsed && slash.Block < blocks.Count && selection.Focus.Block == slash.Block
                    && selection.Focus.Offset > slash.Offset && slash.Offset < blocks[slash.Block].Length
                    && blocks[slash.Block].Text[slash.Offset] == '/';
        var query = valid ? blocks[slash.Block].Text[(slash.Offset + 1)..selection.Focus.Offset] : string.Empty;
        if (!valid || query.Length > 24 || (query.Length > 0 && char.IsWhiteSpace(query[0])) || HasDoubleWhitespace(query))
        {
            CloseSlash();
        }
        else if (query != slash.Query)
        {
            _slash = slash with { Query = query };
        }
    }

    private static bool HasDoubleWhitespace(string query)
    {
        for (var i = 1; i < query.Length; i++)
        {
            if (char.IsWhiteSpace(query[i]) && char.IsWhiteSpace(query[i - 1]))
            {
                return true;
            }
        }
        return false;
    }

    private void CloseSlash()
    {
        if (_slash is null)
        {
            return;
        }
        _slash = null;
        _slashMenu?.Dismiss();
    }

    private void ChooseSlash(RichTextEditorSlashItem item)
    {
        if (_slash is not { } slash)
        {
            return;
        }
        var selection = _engine.Selection;
        var caret = selection.Focus.Block == slash.Block ? selection.Focus.Offset : slash.Offset + 1 + slash.Query.Length;
        _slash = null;
        _slashMenu?.Dismiss();
        _engine.ApplySlashChoice(slash.Block, slash.Offset, caret, item.Type);
        Announce($"{item.Label} added");
        if (item.Type == RichTextBlockType.Image)
        {
            _ = PickImageAsync();
        }
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Toolbar and links
     * -------------------------------------------------------------------------------------------------------- */

    private void EnsureOverlay()
    {
        if (_overlay is not null)
        {
            return;
        }
        _toolbar = new RichTextEditorToolbar();
        _slashMenu = new RichTextEditorSlashMenu();
        _overlay = new RichTextEditorOverlay { Children = { _toolbar, _slashMenu } };
        AdornerLayer.SetIsClipEnabled(_overlay, true);
        _toolbar.BlockTools.IsVisible = Bounds.Width > 330;
        _toolbar.Bold.Click += (_, _) => _engine.ToggleMark(RichTextMark.Bold);
        _toolbar.Italic.Click += (_, _) => _engine.ToggleMark(RichTextMark.Italic);
        _toolbar.Strike.Click += (_, _) => _engine.ToggleMark(RichTextMark.Strikethrough);
        _toolbar.Code.Click += (_, _) => _engine.ToggleMark(RichTextMark.Code);
        _toolbar.Link.Click += (_, _) => OpenLink();
        _toolbar.SetDocs(_engine.Docs);
        _toolbar.Underline.Click += (_, _) => _engine.ToggleMark(RichTextMark.Underline);
        _toolbar.Superscript.Click += (_, _) => _engine.ToggleMark(RichTextMark.Superscript);
        _toolbar.Subscript.Click += (_, _) => _engine.ToggleMark(RichTextMark.Subscript);
        _toolbar.Todo.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.TodoList);
        _toolbar.Palette.Click += (_, _) => OpenColors();
        _toolbar.ColorBack.Click += (_, _) => _toolbar?.SetColorFace(false);
        _toolbar.TextSize.Click += (_, _) =>
        {
            if (!IsReadOnly)
            {
                _toolbar?.SetSizeFace(true, _engine.GetFormats().Size);
            }
        };
        _toolbar.SizeBack.Click += (_, _) => _toolbar?.SetSizeFace(false);
        _toolbar.SizeChosen += size =>
        {
            SetTextSize(size);
            _toolbar?.SetSizeFace(false);
        };
        _toolbar.SwatchChosen += (isHighlight, color) =>
        {
            if (isHighlight)
            {
                SetHighlight(color);
            }
            else
            {
                SetTextColor(color);
            }
            _toolbar?.SetColorFace(false);
        };
        _toolbar.Heading1.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.Heading1);
        _toolbar.Heading2.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.Heading2);
        _toolbar.Quote.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.Quote);
        _toolbar.Back.Click += (_, _) => CloseLink(focusEditor: true);
        _toolbar.Unlink.Click += (_, _) => ApplyLink(remove: true);
        _toolbar.Apply.Click += (_, _) => ApplyLink(remove: false);
        _toolbar.LinkBox.KeyDown += OnLinkBoxKeyDown;
        _toolbar.LinkBox.TextChanged += (_, _) => UpdateApplyReady();
        _toolbar.KeyDown += OnToolbarKeyDown;
        _slashMenu.Hovered += index =>
        {
            if (index >= 0 && index != _slashActive)
            {
                _slashActive = index;
                ScheduleChromeSync();
            }
        };
        _slashMenu.Chosen += ChooseSlash;
        _blockHandle = new RichTextEditorBlockHandle(this, _overlay);
        AdornerLayer.SetAdorner(this, _overlay);
    }

    /// <summary>The languages offered by the code block's language list, each a label and the id that is stored and highlighted. A "Custom…" row always lets the person type any other name.</summary>
    public static readonly StyledProperty<IReadOnlyList<RichTextCodeLanguage>?> CodeLanguagesProperty =
        AvaloniaProperty.Register<RichTextEditor, IReadOnlyList<RichTextCodeLanguage>?>(nameof(CodeLanguages));

    /// <inheritdoc cref="CodeLanguagesProperty"/>
    public IReadOnlyList<RichTextCodeLanguage>? CodeLanguages { get => GetValue(CodeLanguagesProperty); set => SetValue(CodeLanguagesProperty, value); }

    internal MenuSurface? LanguageMenu => _languageMenu;

    internal Avalonia.Controls.Primitives.Popup? CustomLanguagePopup => _customLanguagePopup;

    internal TextBox? CustomLanguageBox => _customLanguageBox;

    private const string CustomLanguageTag = "\u0001custom";

    private MenuSurface? _languageMenu;
    private Border? _languageAnchor;
    private Avalonia.Controls.Primitives.Popup? _customLanguagePopup;
    private TextBox? _customLanguageBox;

    /// <summary>A press on a code block's language label: opens the language list under it.</summary>
    internal void OnCodeLanguagePressed(int block, Rect labelRect)
    {
        if (IsReadOnly || !_engine.Docs || _surface is not { } surface)
        {
            return;
        }
        EnsureOverlay();
        CloseSlash();
        _engine.SetSelection(RichTextEditorSelection.Caret(block, Math.Min(_engine.Selection.Focus.Block == block ? _engine.Selection.Focus.Offset : 0, _engine.Blocks[block].Length)));
        var current = _engine.Blocks[block].Language;
        if (_languageMenu is null)
        {
            _languageAnchor = new Border { Background = Brushes.Transparent, IsHitTestVisible = false };
            _overlay!.Children.Add(_languageAnchor);
            _languageMenu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Start, AnchorGap = 4 };
            AutomationProperties.SetName(_languageMenu, "Code language");
            _languageMenu.ItemClicked += (_, args) =>
            {
                if (args.Entry.Tag is CustomLanguageMarker)
                {
                    Dispatcher.UIThread.Post(OpenCustomLanguage, DispatcherPriority.Loaded);
                    return;
                }
                _engine.SetCodeLanguage(args.Entry.Tag as string);
                FocusEditor();
            };
        }
        // Rebuilt on every open: the list can change, and a language set from code (or typed) shows as the checked row.
        _languageMenu.Items.Clear();
        var languages = CodeLanguages ?? RichTextCodeLanguage.Defaults;
        var known = languages.Any(l => string.Equals(l.Id, current, StringComparison.Ordinal));
        if (current is not null && !known)
        {
            _languageMenu.Items.Add(new MenuEntry { Header = current, Tag = current, ToggleType = MenuEntryToggleType.Radio, GroupName = "language", IsChecked = true });
        }
        foreach (var language in languages)
        {
            _languageMenu.Items.Add(new MenuEntry
            {
                Header = language.Label, Tag = language.Id, ToggleType = MenuEntryToggleType.Radio, GroupName = "language",
                IsChecked = string.Equals(language.Id, current, StringComparison.Ordinal),
                HasSeparatorBefore = language.Id is not null && ReferenceEquals(language, languages.FirstOrDefault(l => l.Id is not null))
            });
        }
        _languageMenu.Items.Add(new MenuEntry { Header = "Custom…", Tag = CustomLanguageMarker.Instance, HasSeparatorBefore = true });
        var origin = surface.TranslatePoint(labelRect.TopLeft, this) ?? default;
        _languageAnchor!.Width = labelRect.Width;
        _languageAnchor.Height = labelRect.Height;
        _languageAnchor.RenderTransform = new TranslateTransform(origin.X, origin.Y);
        _languageMenu.Open(_languageAnchor);
    }

    private sealed class CustomLanguageMarker
    {
        public static readonly CustomLanguageMarker Instance = new();
    }

    /// <summary>Opens a small field under the language label for a name the list does not have.</summary>
    internal void OpenCustomLanguage()
    {
        if (_languageAnchor is null)
        {
            return;
        }
        if (_customLanguagePopup is null)
        {
            _customLanguageBox = SelectInputBox.Create();
            _customLanguageBox.Width = 200;
            _customLanguageBox.Height = 32;
            _customLanguageBox.Padding = new Thickness(8, 0);
            _customLanguageBox.PlaceholderText = "Language, such as csharp";
            AutomationProperties.SetName(_customLanguageBox, "Custom language");
            _customLanguageBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplyCustomLanguage();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _customLanguagePopup!.IsOpen = false;
                    FocusEditor();
                    e.Handled = true;
                }
            };
            var frame = new Border { Padding = new Thickness(6), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Child = _customLanguageBox };
            ArcTokens.Bind(frame, Border.BackgroundProperty, DesignTokens.ArcDesignTokenKind.SurfaceRaised);
            ArcTokens.Bind(frame, Border.BorderBrushProperty, DesignTokens.ArcDesignTokenKind.Border);
            _customLanguagePopup = new Avalonia.Controls.Primitives.Popup
            {
                Child = frame, IsLightDismissEnabled = true, Placement = PlacementMode.BottomEdgeAlignedLeft, PlacementTarget = _languageAnchor
            };
            _overlay!.Children.Add(_customLanguagePopup);
        }
        _customLanguageBox!.Text = _engine.Blocks[_engine.Selection.Focus.Block].Language ?? string.Empty;
        _customLanguagePopup.IsOpen = true;
        Dispatcher.UIThread.Post(() =>
        {
            _customLanguageBox.Focus();
            _customLanguageBox.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    private void ApplyCustomLanguage()
    {
        var name = _customLanguageBox?.Text?.Trim();
        if (_customLanguagePopup is not null)
        {
            _customLanguagePopup.IsOpen = false;
        }
        _engine.SetCodeLanguage(string.IsNullOrEmpty(name) ? null : name);
        FocusEditor();
    }

    private void OpenColors()
    {
        if (IsReadOnly || _surface is not { } surface || _toolbar is not { } toolbar)
        {
            return;
        }
        toolbar.SetPalette(surface.TextBrushOf, surface.FillBrushOf);
        toolbar.SetColorFace(true);
    }

    private void UpdateApplyReady()
    {
        if (_toolbar is { } toolbar)
        {
            toolbar.Apply.Classes.Set("ready", !string.IsNullOrWhiteSpace(toolbar.LinkBox.Text));
        }
    }

    private void OnToolbarKeyDown(object? sender, KeyEventArgs e)
    {
        if (_toolbar is not { } toolbar || toolbar.IsLinkFace)
        {
            return;
        }
        var buttons = toolbar.FormatButtons.ToList();
        var at = buttons.FindIndex(b => b.IsKeyboardFocusWithin);
        if (e.Key is Key.Right or Key.Left)
        {
            var next = (at + (e.Key == Key.Right ? 1 : -1) + buttons.Count) % buttons.Count;
            buttons[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _surface?.Focus();
            e.Handled = true;
        }
    }

    private void OnLinkBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyLink(remove: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseLink(focusEditor: true);
            e.Handled = true;
        }
    }

    private void OpenLink()
    {
        if (IsReadOnly)
        {
            return;
        }
        var range = _engine.LinkRange(out var href);
        if (range is null)
        {
            return;
        }
        EnsureOverlay();
        CloseSlash();
        _engine.SetSelection(range.Value);
        _savedLinkRange = range;
        var toolbar = _toolbar!;
        toolbar.Unlink.IsVisible = href is not null;
        toolbar.LinkBox.Text = href ?? string.Empty;
        UpdateApplyReady();
        // Open the bar first if the selection had none yet (a link edited from a caret).
        SyncChromeNow();
        toolbar.SetLinkFace(true);
        StartLinkWatch();
        Dispatcher.UIThread.Post(() =>
        {
            toolbar.LinkBox.Focus();
            toolbar.LinkBox.CaretIndex = toolbar.LinkBox.Text?.Length ?? 0;
        }, DispatcherPriority.Loaded);
    }

    private void CloseLink(bool focusEditor)
    {
        var range = _savedLinkRange;
        _savedLinkRange = null;
        StopLinkWatch();
        _toolbar?.SetLinkFace(false);
        if (focusEditor)
        {
            _surface?.Focus();
            if (range is { } saved)
            {
                _engine.SetSelection(saved);
            }
        }
        ScheduleChromeSync();
    }

    private void ApplyLink(bool remove)
    {
        var range = _savedLinkRange;
        if (range is null || _toolbar is not { } toolbar)
        {
            CloseLink(focusEditor: true);
            return;
        }
        var url = remove ? string.Empty : RichTextEditorMarkdown.NormalizeUrl(toolbar.LinkBox.Text);
        _savedLinkRange = null;
        StopLinkWatch();
        _surface?.Focus();
        _engine.SetSelection(range.Value);
        _engine.SetLink(range.Value, url);
        toolbar.SetLinkFace(false, animate: false);
        toolbar.Dismiss();
        Announce(url.Length == 0 ? "Link removed" : "Link added");
    }

    /// <summary>While the link field is open, a press anywhere outside the toolbar closes it (Arc's document pointerdown).</summary>
    private void StartLinkWatch()
    {
        StopLinkWatch();
        _linkWatchRoot = TopLevel.GetTopLevel(this);
        _linkWatchRoot?.AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void StopLinkWatch()
    {
        _linkWatchRoot?.RemoveHandler(PointerPressedEvent, OnAnyPointerPressed);
        _linkWatchRoot = null;
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_toolbar is not { IsLinkFace: true } toolbar)
        {
            StopLinkWatch();
            return;
        }
        for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, toolbar))
            {
                return;
            }
        }
        _savedLinkRange = null;
        StopLinkWatch();
        toolbar.SetLinkFace(false, animate: false);
        toolbar.Dismiss();
    }

    private void ScheduleChromeSync()
    {
        if (_chromePending)
        {
            return;
        }
        _chromePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _chromePending = false;
            SyncChromeNow();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Arc's selection driven chrome: the slash menu at the slash, the toolbar above (or below) the selection.</summary>
    private void SyncChromeNow()
    {
        var surface = _surface;
        if (surface is null || !IsAttachedToVisualTree())
        {
            return;
        }
        var selection = _engine.Selection;
        var focused = IsFocusWithinEditor();
        var linkFace = _toolbar is { IsLinkFace: true };
        var wantsBar = !IsReadOnly && !selection.IsCollapsed && !surface.IsDragging && !IsSlashOpen && (focused || linkFace) && !IsFindFocused
                       && !_engine.Blocks[selection.Start.Block].IsCode;
        if (!wantsBar && !IsSlashOpen && _overlay is null)
        {
            return;
        }
        EnsureOverlay();
        var toolbar = _toolbar!;
        var menu = _slashMenu!;
        UpdateToolbarState(toolbar);
        var offset = surface.TranslatePoint(default, this) ?? default;
        var band = ClipBand();
        PlaceFindBar();

        if (IsSlashOpen && focused && _slash is { } slash)
        {
            var results = RichTextEditorSlashItem.Filter(slash.Query, _engine.Docs);
            _slashActive = Math.Min(_slashActive, results.Count - 1);
            var start = surface.CaretRect(new RichTextEditorPosition(slash.Block, slash.Offset)).Translate(offset);
            var top = start.Top;
            var bottom = start.Bottom;
            var above = band.Bottom - bottom < 330 && top - band.Top > band.Bottom - bottom;
            var room = above ? top - band.Top - 12 : band.Bottom - bottom - 12;
            var x = Math.Max(0, Math.Min(start.Left - 8, Bounds.Width - 244));
            var y = above ? top - 6 : bottom + 6;
            menu.Present(new Point(x, y), above, Math.Max(150, double.IsFinite(room) ? room : 320), Bounds.Width - 8, results, _slashActive);
        }
        else
        {
            menu.Dismiss();
        }

        if (linkFace)
        {
            return;
        }
        if (!wantsBar)
        {
            toolbar.Dismiss();
            return;
        }
        var bounds = surface.SelectionBounds(selection.Start, selection.End);
        var caretLine = surface.LineRect(selection.Start);
        var first = (bounds?.First ?? caretLine).Translate(offset);
        var whole = (bounds?.Whole ?? caretLine).Translate(offset);
        var below = first.Top - band.Top < 60;
        toolbar.Present(whole.Center.X, below ? whole.Bottom + 8 : first.Top - 8, below, Bounds.Width);
    }

    private void UpdateToolbarState(RichTextEditorToolbar toolbar)
    {
        var formats = Formats;
        toolbar.Bold.IsChecked = formats.IsBold;
        toolbar.Italic.IsChecked = formats.IsItalic;
        toolbar.Strike.IsChecked = formats.IsStrikethrough;
        toolbar.Code.IsChecked = formats.IsCode;
        toolbar.Link.IsChecked = formats.Link is not null;
        var linkLabel = formats.Link is null ? "Link" : "Edit link";
        AutomationProperties.SetName(toolbar.Link, linkLabel);
        Tooltip.SetTip(toolbar.Link, linkLabel + " (⌘K)");
        toolbar.SetDocs(_engine.Docs);
        toolbar.Underline.IsChecked = formats.IsUnderline;
        toolbar.Superscript.IsChecked = formats.IsSuperscript;
        toolbar.Subscript.IsChecked = formats.IsSubscript;
        toolbar.Todo.IsChecked = formats.BlockType == RichTextBlockType.TodoList;
        toolbar.Heading1.IsChecked = formats.BlockType == RichTextBlockType.Heading1;
        toolbar.Heading2.IsChecked = formats.BlockType == RichTextBlockType.Heading2;
        toolbar.Quote.IsChecked = formats.BlockType == RichTextBlockType.Quote;
        ArcMotion.SetLevel(toolbar, ArcMotion.GetLevel(this));
        ArcMotion.SetLevel(_slashMenu!, ArcMotion.GetLevel(this));
    }

    /// <summary>The visible band of the nearest clipping ancestors, in editor coordinates, so floating layers flip before they get cut off.</summary>
    private Rect ClipBand()
    {
        var top = double.NegativeInfinity;
        var bottom = double.PositiveInfinity;
        for (var visual = this.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (!visual.ClipToBounds && visual is not TopLevel)
            {
                continue;
            }
            var transform = visual.TransformToVisual(this);
            if (transform is null)
            {
                continue;
            }
            var rect = new Rect(visual.Bounds.Size).TransformToAABB(transform.Value);
            top = Math.Max(top, rect.Top);
            bottom = Math.Min(bottom, rect.Bottom);
        }
        return new Rect(0, double.IsFinite(top) ? top : -10000, Bounds.Width, double.IsFinite(top) && double.IsFinite(bottom) ? Math.Max(0, bottom - top) : 20000);
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;
}
