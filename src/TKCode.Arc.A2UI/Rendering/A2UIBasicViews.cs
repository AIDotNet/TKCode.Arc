using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Icons.Lucide;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.A2UI;

/// <summary>The basic catalog's components drawn with Arc controls.</summary>
internal static class A2UIBasicViews
{
    public static void RegisterAll(A2UIComponentRegistry registry)
    {
        var id = A2UICatalogs.BasicCatalogId;
        registry.Register(id, "Text", static () => new TextView())
                .Register(id, "Image", static () => new ImageView())
                .Register(id, "Icon", static () => new IconView())
                .Register(id, "Video", static () => new MediaView(video: true))
                .Register(id, "AudioPlayer", static () => new MediaView(video: false))
                .Register(id, "Row", static () => new FlexView(Orientation.Horizontal))
                .Register(id, "Column", static () => new FlexView(Orientation.Vertical))
                .Register(id, "List", static () => new ListView())
                .Register(id, "Card", static () => new CardView())
                .Register(id, "Tabs", static () => new TabsView())
                .Register(id, "Modal", static () => new ModalView())
                .Register(id, "Divider", static () => new DividerView())
                .Register(id, "Button", static () => new ButtonView())
                .Register(id, "TextField", static () => new TextFieldView())
                .Register(id, "CheckBox", static () => new CheckBoxView())
                .Register(id, "ChoicePicker", static () => new ChoicePickerView())
                .Register(id, "Slider", static () => new SliderView())
                .Register(id, "DateTimeInput", static () => new DateTimeView());
    }

    internal static double Token(Control control, A2UISurfaceViewTokenKind kind, double fallback)
        => control.TryFindResource(A2UISurfaceViewTokens.Key(kind), out var value) && value is double d ? d : fallback;

    internal static void BindToken(Control control, AvaloniaProperty property, A2UISurfaceViewTokenKind kind)
        => control.Bind(property, control.GetResourceObservable(A2UISurfaceViewTokens.Key(kind)));

    /// <summary>A small strip shown for unknown or cyclic components when error placeholders are on.</summary>
    public static Control ErrorStrip(A2UIFallbackInfo info)
    {
        var text = new TextBlock
        {
            Text = info.State == A2UINodeState.Cyclic
                ? $"'{info.ComponentId}' contains itself"
                : $"'{info.ComponentId}': unknown component {info.ComponentType}",
            TextWrapping = TextWrapping.Wrap
        };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        var border = new Border { Child = text, Padding = new Thickness(8, 4), BorderThickness = new Thickness(1) };
        ArcTokens.Bind(border, Border.BorderBrushProperty, ArcDesignTokenKind.Danger);
        ArcTokens.Bind(border, Border.BackgroundProperty, ArcDesignTokenKind.DangerSubtle);
        return border.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
    }

    /// <summary>The first failing check's message, or null.</summary>
    internal static string? CheckMessage(A2UINode node)
        => node.Validation.FirstOrDefault(v => !v.Valid) is { } failed ? failed.Message ?? "Invalid value." : null;

    // Keeps a panel's children equal to the presenters of the given nodes, reusing the ones that stay.
    internal static void SyncChildren(Panel panel, IReadOnlyList<Control> controls)
    {
        if (panel.Children.Count == controls.Count && panel.Children.SequenceEqual(controls))
        {
            return;
        }

        panel.Children.Clear();
        foreach (var control in controls)
        {
            if (control.Parent is Panel old && !ReferenceEquals(old, panel))
            {
                old.Children.Remove(control);
            }
            else if (control.Parent is Decorator decorator)
            {
                decorator.Child = null;
            }

            panel.Children.Add(control);
        }
    }

    internal static void Unparent(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, control):
                content.Content = null;
                break;
        }
    }

    // ---- Display ------------------------------------------------------------------------------------------------

    private sealed class TextView : A2UIComponentView<TextBlock>
    {
        // The color is inherited, never set: a Text inside a primary button must take the button's foreground. Captions
        // are told apart by size and opacity, which works on any background.
        protected override TextBlock Create() => new() { TextWrapping = TextWrapping.Wrap };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var caption = Node.GetString("variant") == "caption";
            ArcTokens.Bind(View, TextBlock.FontSizeProperty, caption ? ArcDesignTokenKind.TypeSM : ArcDesignTokenKind.TypeBase);
            View.Opacity = caption ? 0.72 : 1;
            A2UIInlineMarkdown.Apply(View, Node.GetString("text") ?? string.Empty);
        }
    }

    private sealed class ImageView : A2UIComponentView<Panel>
    {
        private ArcImage? _image;
        private Avatar? _avatar;

        protected override Panel Create() => new Panel();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var renderer = Context.Renderer;
            var uri = renderer.Options.UrlPolicy.ResolveMedia(Node.GetString("url"), renderer.Options.BaseUri);
            var variant = Node.GetString("variant") ?? "mediumFeature";
            var description = Node.GetString("description");
            if (variant == "avatar")
            {
                _avatar ??= new Avatar();
                _avatar.DisplayName = description;
                _avatar.Source = uri is null ? null : ImageSource.FromUri(uri);
                Show(_avatar);
                return;
            }

            _image ??= new ArcImage();
            _image.Source = uri is null ? null : ImageSource.FromUri(uri);
            _image.Stretch = Node.GetString("fit") switch
            {
                "contain" or "scaleDown" => Stretch.Uniform,
                "cover" => Stretch.UniformToFill,
                "none" => Stretch.None,
                _ => Stretch.Fill
            };
            _image.StretchDirection = Node.GetString("fit") == "scaleDown" ? StretchDirection.DownOnly : StretchDirection.Both;
            switch (variant)
            {
                case "icon":
                    _image.Width = _image.Height = Token(View, A2UISurfaceViewTokenKind.ImageIconSize, 24);
                    break;
                case "smallFeature":
                    _image.Width = _image.Height = Token(View, A2UISurfaceViewTokenKind.ImageSmallFeature, 64);
                    break;
                case "largeFeature":
                    _image.Width = double.NaN;
                    _image.Height = Token(View, A2UISurfaceViewTokenKind.ImageLargeFeature, 280);
                    break;
                case "header":
                    _image.Width = double.NaN;
                    _image.Height = Token(View, A2UISurfaceViewTokenKind.ImageHeader, 200);
                    break;
                default:
                    _image.Width = double.NaN;
                    _image.Height = Token(View, A2UISurfaceViewTokenKind.ImageMediumFeature, 160);
                    break;
            }

            if (description is not null)
            {
                Avalonia.Automation.AutomationProperties.SetName(_image, description);
            }

            Show(_image);
        }

        private void Show(Control control)
        {
            if (View.Children.Count != 1 || !ReferenceEquals(View.Children[0], control))
            {
                View.Children.Clear();
                View.Children.Add(control);
            }
        }
    }

    private sealed class IconView : A2UIComponentView<Panel>
    {
        private LucideIcon? _lucide;
        private PathIcon? _path;

        protected override Panel Create() => new Panel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var size = Token(View, A2UISurfaceViewTokenKind.IconSize, 20);
            var name = Node.Get("name");
            Control? shown = null;
            if (name is JsonObject obj && A2UIJson.AsString(obj["svgPath"]) is { Length: > 0 and <= 8192 } svg)
            {
                try
                {
                    _path ??= new PathIcon();
                    _path.Data = Geometry.Parse(svg);
                    _path.Width = _path.Height = size;
                    shown = _path;
                }
                catch (FormatException)
                {
                    shown = null;
                }
            }
            else if (A2UIJson.AsString(name) is { } text && A2UIIcons.TryMap(text, out var kind))
            {
                _lucide ??= new LucideIcon();
                _lucide.Kind = kind;
                _lucide.Width = _lucide.Height = size;
                shown = _lucide;
            }

            View.Children.Clear();
            if (shown is not null)
            {
                View.Children.Add(shown);
            }
        }
    }

    // Video and audio are shown as a card with a link: playback stays with the platform (ADR 0048 D4).
    private sealed class MediaView(bool video) : A2UIComponentView<Border>
    {
        private readonly TextBlock _title = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        private readonly Button _open = new() { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = "Open" };
        private ArcImage? _poster;
        private Uri? _uri;

        protected override Border Create()
        {
            var icon = new LucideIcon { Kind = video ? LucideIconKind.Play : LucideIconKind.Volume2, Width = 20, Height = 20 };
            ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            ArcTokens.Bind(_title, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
            _open.Click += (_, _) =>
            {
                if (_uri is not null)
                {
                    try
                    {
                        Context.Renderer.OpenUrl(_uri, Node.Surface);
                    }
                    catch (A2UIException ex)
                    {
                        Context.Renderer.ReportError(ex, Node.Surface.Id);
                    }
                }
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            Grid.SetColumn(_title, 1);
            Grid.SetColumn(_open, 2);
            row.Children.Add(icon);
            row.Children.Add(_title);
            row.Children.Add(_open);
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(row);
            var border = new Border { Child = stack, Padding = new Thickness(12), BorderThickness = new Thickness(1) };
            ArcTokens.Bind(border, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(border, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            return border.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var options = Context.Renderer.Options;
            _uri = options.UrlPolicy.ResolveMedia(Node.GetString("url"), options.BaseUri);
            _open.IsEnabled = _uri is { Scheme: "http" or "https" };
            _title.Text = Node.GetString("description") ?? _uri?.Host ?? (video ? "Video" : "Audio");
            var poster = video ? options.UrlPolicy.ResolveMedia(Node.GetString("posterUrl"), options.BaseUri) : null;
            var stack = (StackPanel)View.Child!;
            if (poster is not null)
            {
                _poster ??= new ArcImage { Height = 160, Stretch = Stretch.UniformToFill };
                _poster.Source = ImageSource.FromUri(poster);
                if (!stack.Children.Contains(_poster))
                {
                    stack.Children.Insert(0, _poster);
                }
            }
            else if (_poster is not null)
            {
                stack.Children.Remove(_poster);
            }
        }
    }

    // ---- Layout -------------------------------------------------------------------------------------------------

    private sealed class FlexView(Orientation orientation) : A2UIComponentView<A2UIFlexPanel>
    {
        protected override A2UIFlexPanel Create()
        {
            var panel = new A2UIFlexPanel { Orientation = orientation };
            BindToken(panel, A2UIFlexPanel.GapProperty, A2UISurfaceViewTokenKind.ContainerGap);
            return panel;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Justify = ParseJustify(Node.GetString("justify"));
            View.Align = ParseAlign(Node.GetString("align"));
            if (change.ChildrenChanged)
            {
                SyncChildren(View, Context.BuildChildren(Node.GetChildren("children")));
            }
        }
    }

    internal static A2UIJustify ParseJustify(string? value) => value switch
    {
        "center" => A2UIJustify.Center,
        "end" => A2UIJustify.End,
        "spaceBetween" => A2UIJustify.SpaceBetween,
        "spaceAround" => A2UIJustify.SpaceAround,
        "spaceEvenly" => A2UIJustify.SpaceEvenly,
        "stretch" => A2UIJustify.Stretch,
        _ => A2UIJustify.Start
    };

    internal static A2UIAlign ParseAlign(string? value) => value switch
    {
        "start" => A2UIAlign.Start,
        "center" => A2UIAlign.Center,
        "end" => A2UIAlign.End,
        _ => A2UIAlign.Stretch
    };

    private sealed class ListView : A2UIComponentView<Control>
    {
        private readonly A2UIFlexPanel _panel = new();
        private ScrollViewer? _scroll;

        protected override Control Create()
        {
            BindToken(_panel, A2UIFlexPanel.GapProperty, A2UISurfaceViewTokenKind.ContainerGap);
            return new Decorator { Child = _panel };
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var horizontal = Node.GetString("direction") == "horizontal";
            _panel.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
            _panel.Align = ParseAlign(Node.GetString("align"));
            var host = (Decorator)View;
            if (horizontal && _scroll is null)
            {
                host.Child = null;
                _scroll = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = _panel
                };
                host.Child = _scroll;
            }
            else if (!horizontal && _scroll is not null)
            {
                _scroll.Content = null;
                _scroll = null;
                host.Child = _panel;
            }

            if (change.ChildrenChanged)
            {
                SyncChildren(_panel, Context.BuildChildren(Node.GetChildren("children")));
            }
        }
    }

    private sealed class CardView : A2UIComponentView<Card>
    {
        // The card's content area hands its children the styling of a card's paragraph: the secondary text color and a
        // 1.4 × 14px line height. A2UI content is components, so the slot sets them back for everything inside (buttons
        // and inputs still apply their own); a large number in a 19.6px line would be clipped.
        private readonly Decorator _slot = new();

        protected override Card Create()
        {
            ArcTokens.Bind(_slot, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
            _slot.SetValue(TextBlock.LineHeightProperty, double.NaN);
            return new Card { Content = _slot };
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            if (!change.ChildrenChanged)
            {
                return;
            }

            var child = Node.GetChild("child") is { } node ? Context.BuildChild(node) : null;
            if (!ReferenceEquals(_slot.Child, child))
            {
                if (child is not null)
                {
                    Unparent(child);
                }

                _slot.Child = child;
            }
        }
    }

    private sealed class TabsView : A2UIComponentView<Tabs>
    {
        protected override Tabs Create()
        {
            var tabs = new Tabs();
            tabs.SelectionChanged += (_, _) => FillSelected();
            return tabs;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var specs = Node.Get("tabs") as JsonArray ?? new JsonArray();
            var items = View.Items;
            while (items.Count > specs.Count)
            {
                items.RemoveAt(items.Count - 1);
            }

            for (var i = 0; i < specs.Count; i++)
            {
                if (items.Count <= i)
                {
                    items.Add(new TabItem());
                }

                var item = (TabItem)items[i]!;
                item.Header = A2UIJson.ToDisplayString((specs[i] as JsonObject)?["title"]);
                if (change.ChildrenChanged && item.Content is Control old && !ReferenceEquals(old, ChildOf(i)))
                {
                    item.Content = null;
                }
            }

            if (View.SelectedIndex < 0 && specs.Count > 0)
            {
                View.SelectedIndex = 0;
            }

            FillSelected();
        }

        private Control? ChildOf(int index) => Node.GetChild($"tabs/{index}/child") is { } child ? Context.BuildChild(child) : null;

        // Only the selected tab's content is built; it stays built afterwards.
        private void FillSelected()
        {
            if (View.SelectedIndex >= 0 && View.SelectedIndex < View.Items.Count && View.Items[View.SelectedIndex] is TabItem item
                && item.Content is null && ChildOf(View.SelectedIndex) is { } content)
            {
                Unparent(content);
                item.Content = content;
            }
        }
    }

    private sealed class ModalView : A2UIComponentView<Panel>
    {
        private readonly Dialog _dialog = new();
        private readonly Decorator _trigger = new() { Cursor = new Cursor(StandardCursorType.Hand) };

        protected override Panel Create()
        {
            var panel = new Panel();
            panel.Children.Add(_trigger);
            panel.Children.Add(_dialog);
            _trigger.AddHandler(AvaloniaButton.ClickEvent, (_, _) => _dialog.IsOpen = true, RoutingStrategies.Bubble, handledEventsToo: true);
            _trigger.Tapped += (_, e) =>
            {
                if (!e.Handled)
                {
                    _dialog.IsOpen = true;
                }
            };
            return panel;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            if (!change.ChildrenChanged)
            {
                return;
            }

            var trigger = Node.GetChild("trigger") is { } t ? Context.BuildChild(t) : null;
            if (!ReferenceEquals(_trigger.Child, trigger))
            {
                if (trigger is not null)
                {
                    Unparent(trigger);
                }

                _trigger.Child = trigger;
            }

            var content = Node.GetChild("content") is { } c ? Context.BuildChild(c) : null;
            if (!ReferenceEquals(_dialog.Content, content))
            {
                if (content is not null)
                {
                    Unparent(content);
                }

                _dialog.Content = content;
            }
        }

        public override void Detach() => _dialog.IsOpen = false;
    }

    private sealed class DividerView : A2UIComponentView<Border>
    {
        protected override Border Create()
        {
            var line = new Border();
            ArcTokens.Bind(line, Border.BackgroundProperty, ArcDesignTokenKind.Border);
            return line;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var vertical = Node.GetString("axis") == "vertical";
            View.Width = vertical ? 1 : double.NaN;
            View.Height = vertical ? double.NaN : 1;
            View.Margin = vertical ? new Thickness(4, 0) : new Thickness(0, 4);
        }
    }

    // ---- Inputs -------------------------------------------------------------------------------------------------

    private sealed class ButtonView : A2UIComponentView<Button>
    {
        protected override Button Create()
        {
            var button = new Button();
            button.Click += (_, _) => Node.InvokeAction();
            return button;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Variant = Node.GetString("variant") switch
            {
                "primary" => ButtonVariant.Primary,
                "borderless" => ButtonVariant.Ghost,
                _ => ButtonVariant.Secondary
            };
            View.IsEnabled = Node.IsValid && Node.HasAction();
            if (change.ChildrenChanged)
            {
                var child = Node.GetChild("child") is { } node ? Context.BuildChild(node) : null;
                if (!ReferenceEquals(View.Content, child))
                {
                    if (child is not null)
                    {
                        Unparent(child);
                    }

                    View.Content = child;
                }
            }
        }
    }

    private sealed class TextFieldView : A2UIComponentView<FieldTextBox>
    {
        private string _variant = string.Empty;
        private bool _touched;

        protected override FieldTextBox Create() => Build(Node.GetString("variant") ?? "shortText");

        private FieldTextBox Build(string variant)
        {
            _variant = variant;
            FieldTextBox box = variant switch
            {
                "longText" => new Textarea(),
                "obscured" => new PasswordField(),
                "number" => new Input(),
                _ => new Input()
            };
            box.TextChanged += (_, _) =>
            {
                if (IsApplying)
                {
                    return;
                }

                _touched = true;
                var text = box.Text ?? string.Empty;
                JsonNode? value = _variant == "number" && double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)
                    ? A2UIJson.Number(number)
                    : JsonValue.Create(text);
                if (_variant == "number" && text.Length > 0 && value is JsonValue jv && A2UIJson.IsString(jv))
                {
                    return;
                }

                Node.Write("value", value);
            };
            box.LostFocus += (_, _) =>
            {
                _touched = true;
                ApplyValidation();
            };
            return box;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Label = Node.GetString("label");
            View.Placeholder = Node.GetString("placeholder");
            var text = Node.GetString("value") ?? string.Empty;
            if ((View.Text ?? string.Empty) != text)
            {
                View.Text = text;
            }

            ApplyValidation();
        }

        private void ApplyValidation()
        {
            var message = _touched ? CheckMessage(Node) : null;
            View.ErrorMessage = message;
            View.IsInvalid = message is not null;
        }
    }

    private sealed class CheckBoxView : A2UIComponentView<Checkbox>
    {
        protected override Checkbox Create()
        {
            var box = new Checkbox();
            box.IsCheckedChanged += (_, _) =>
            {
                if (!IsApplying)
                {
                    Node.Write("value", JsonValue.Create(box.IsChecked == true));
                }
            };
            return box;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Content = Node.GetString("label");
            var value = Node.GetBoolean("value") == true;
            if (View.IsChecked != value)
            {
                View.IsChecked = value;
            }

            View.IsInvalid = CheckMessage(Node) is not null && Node.Validation.Any(v => v.IsBlocking);
            View.Description = CheckMessage(Node);
        }
    }

    private sealed record Choice(string Value, string Label);

    private sealed class ChoicePickerView : A2UIComponentView<Decorator>
    {
        private string _mode = string.Empty;
        private Control? _control;
        private TextBlock? _chipLabel;

        protected override Decorator Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var options = (Node.Get("options") as JsonArray ?? new JsonArray())
                .OfType<JsonObject>()
                .Select(o => new Choice(A2UIJson.ToDisplayString(o["value"]), A2UIJson.ToDisplayString(o["label"])))
                .ToList();
            var selected = Node.GetStringList("value") ?? [];
            var multiple = Node.GetString("variant") == "multipleSelection";
            var chips = Node.GetString("displayStyle") == "chips";
            var filterable = Node.GetBoolean("filterable") == true;
            var mode = (multiple ? "m" : "s") + (filterable ? "f" : chips ? "c" : "b");
            var label = Node.GetString("label");
            if (mode != _mode || _control is null)
            {
                _mode = mode;
                _control = mode switch
                {
                    "sb" => new RadioGroup(),
                    "mb" => new StackPanel { Spacing = 6 },
                    "sc" or "mc" => new ChipGroup { IsMultiple = multiple },
                    "sf" => new Combobox(),
                    _ => new MultiSelect { IsSearchable = true }
                };
                Wire(_control);
                _chipLabel = null;
                View.Child = _control is ChipGroup ? new StackPanel { Spacing = 6, Children = { _control } } : _control;
            }

            switch (_control)
            {
                case RadioGroup radio:
                    radio.Header = label;
                    if (radio.Items.Count != options.Count || !options.Select((o, i) => ((RadioOption)radio.Items[i]!).Value as string == o.Value).All(b => b))
                    {
                        radio.Items.Clear();
                        foreach (var option in options)
                        {
                            radio.Items.Add(new RadioOption { Value = option.Value, Label = option.Label });
                        }
                    }

                    var first = selected.FirstOrDefault();
                    if (!Equals(radio.SelectedValue, first))
                    {
                        radio.SelectedValue = first;
                    }

                    break;
                case StackPanel stack:
                    stack.Children.Clear();
                    if (label is not null)
                    {
                        stack.Children.Add(FieldLabel(null, label));
                    }

                    foreach (var option in options)
                    {
                        var box = new Checkbox { Content = option.Label, IsChecked = selected.Contains(option.Value), Tag = option.Value };
                        box.IsCheckedChanged += (_, _) =>
                        {
                            if (!IsApplying)
                            {
                                WriteSelection(stack.Children.OfType<Checkbox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag!));
                            }
                        };
                        stack.Children.Add(box);
                    }

                    break;
                case ChipGroup chipGroup:
                    chipGroup.Label = label;
                    if (View.Child is StackPanel chipStack)
                    {
                        if (label is null && chipStack.Children.Count > 1)
                        {
                            chipStack.Children.RemoveAt(0);
                        }
                        else if (label is not null)
                        {
                            _chipLabel = FieldLabel(_chipLabel, label);
                            if (chipStack.Children.Count == 1)
                            {
                                chipStack.Children.Insert(0, _chipLabel);
                            }
                        }
                    }

                    chipGroup.TextSelector = static item => (item as Choice)?.Label;
                    chipGroup.ValueSelector = static item => (item as Choice)?.Value;
                    chipGroup.ItemsSource = options;
                    SelectIndexes(chipGroup.SelectedItems, options, selected);
                    break;
                case Combobox combo:
                    combo.Label = label;
                    combo.TextSelector = static item => (item as Choice)?.Label;
                    combo.ItemsSource = options;
                    combo.SelectedItem = options.FirstOrDefault(o => selected.Contains(o.Value));
                    break;
                case MultiSelect multi:
                    multi.Label = label;
                    multi.TextSelector = static item => (item as Choice)?.Label;
                    multi.ItemsSource = options;
                    SelectIndexes(multi.SelectedItems, options, selected);
                    break;
            }
        }

        private static void SelectIndexes(System.Collections.IList? current, List<Choice> options, IReadOnlyList<string> selected)
        {
            if (current is null)
            {
                return;
            }

            var wanted = options.Where(o => selected.Contains(o.Value)).ToList();
            if (current.Count == wanted.Count && current.OfType<Choice>().All(wanted.Contains))
            {
                return;
            }

            current.Clear();
            foreach (var choice in wanted)
            {
                current.Add(choice);
            }
        }

        private void Wire(Control control)
        {
            switch (control)
            {
                case RadioGroup radio:
                    radio.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == Avalonia.Controls.Primitives.SelectingItemsControl.SelectedValueProperty && !IsApplying)
                        {
                            WriteSelection(radio.SelectedValue is string v ? [v] : []);
                        }
                    };
                    break;
                case Avalonia.Controls.Primitives.SelectingItemsControl selecting:
                    selecting.SelectionChanged += (_, _) =>
                    {
                        if (!IsApplying)
                        {
                            var items = selecting switch
                            {
                                ChipGroup chip => chip.SelectedItems,
                                MultiSelect multi => multi.SelectedItems,
                                _ => selecting.SelectedItem is { } one ? new[] { one } : null
                            };
                            WriteSelection(items?.OfType<Choice>().Select(c => c.Value) ?? []);
                        }
                    };
                    break;
            }
        }

        private void WriteSelection(IEnumerable<string> values)
        {
            var array = new JsonArray();
            foreach (var value in values)
            {
                array.Add((JsonNode?)(JsonValue.Create(value)));
            }

            Node.Write("value", array);
        }
    }

    private sealed class SliderView : A2UIComponentView<Slider>
    {
        protected override Slider Create()
        {
            var slider = new Slider { IsValueVisible = true };
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && !IsApplying)
                {
                    Node.Write("value", A2UIJson.Number(slider.Value));
                }
            };
            return slider;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var min = Node.GetNumber("min") ?? 0;
            var max = Math.Max(min, Node.GetNumber("max") ?? 100);
            View.Minimum = min;
            View.Maximum = max;
            View.Label = Node.GetString("label");
            // Arc's Slider snaps to SmallChange.
            View.SmallChange = Node.GetNumber("steps") is { } steps && steps >= 1 ? (max - min) / steps : (max - min) / 100;

            var value = Math.Clamp(Node.GetNumber("value") ?? min, min, max);
            if (Math.Abs(View.Value - value) > double.Epsilon)
            {
                View.Value = value;
            }
        }
    }

    /// <summary>A field label that looks like the label of Arc's text fields (Input, Textarea).</summary>
    private static TextBlock FieldLabel(TextBlock? reuse, string text)
    {
        if (reuse is null)
        {
            reuse = new TextBlock { Margin = new Thickness(0, 0, 0, 2) };
            ArcTokens.Bind(reuse, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
            ArcTokens.Bind(reuse, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
            ArcTokens.Bind(reuse, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
            ArcTokens.Bind(reuse, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }

        reuse.Text = text;
        return reuse;
    }

    private sealed class DateTimeView : A2UIComponentView<StackPanel>
    {
        private DatePicker? _date;
        private TimePicker? _time;
        private TextBlock? _label;

        protected override StackPanel Create() => new() { Spacing = 6 };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var enableDate = Node.GetBoolean("enableDate") == true;
            var enableTime = Node.GetBoolean("enableTime") == true;
            if (!enableDate && !enableTime)
            {
                enableDate = true;
            }

            var label = Node.GetString("label");
            View.Children.Clear();
            if (label is not null)
            {
                _label = FieldLabel(_label, label);
                View.Children.Add(_label);
            }

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            A2UIDateFormat.TryParse(Node.Get("value"), out var current, out var original);
            var hasValue = original is not null || Node.Get("value") is not null && A2UIJson.IsNumber(Node.Get("value"));
            if (enableDate)
            {
                if (_date is null)
                {
                    _date = new DatePicker();
                    _date.SelectedDateChanged += (_, _) => WriteValue();
                }

                _date.MinDate = ParseDate(Node.GetString("min"));
                _date.MaxDate = ParseDate(Node.GetString("max"));
                var date = hasValue ? DateOnly.FromDateTime(current.DateTime) : (DateOnly?)null;
                if (_date.SelectedDate != date)
                {
                    _date.SelectedDate = date;
                }

                row.Children.Add(_date);
            }

            if (enableTime)
            {
                if (_time is null)
                {
                    _time = new TimePicker();
                    _time.SelectedTimeChanged += (_, _) => WriteValue();
                }

                var time = hasValue ? TimeOnly.FromDateTime(current.DateTime) : (TimeOnly?)null;
                if (_time.SelectedTime != time)
                {
                    _time.SelectedTime = time;
                }

                row.Children.Add(_time);
            }

            View.Children.Add(row);
        }

        private static DateOnly? ParseDate(string? text)
            => A2UIDateFormat.TryParse(text is null ? null : JsonValue.Create(text), out var d, out _) ? DateOnly.FromDateTime(d.DateTime) : null;

        private void WriteValue()
        {
            if (IsApplying)
            {
                return;
            }

            var date = _date?.SelectedDate;
            var time = _time?.SelectedTime;
            string? text = (date, time) switch
            {
                ({ } d, { } t) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T" + t.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                ({ } d, null) when _time is null => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                (null, { } t) when _date is null => t.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                _ => null
            };
            if (text is not null)
            {
                Node.Write("value", JsonValue.Create(text));
            }
        }
    }

}
