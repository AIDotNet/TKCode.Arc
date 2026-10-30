using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Builders for the code-built parts of <see cref="EnvVariables"/> (every color and type size bound to tokens).</summary>
internal static class EnvUi
{
    /// <summary>Lucide <c>file-up</c>.</summary>
    public static readonly Geometry FileUp = ArcIcons.Parse(
        "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M12 12v6 M15 15l-3-3-3 3");

    /// <summary>Lucide <c>arrow-left-right</c>.</summary>
    public static readonly Geometry ArrowLeftRight = ArcIcons.Parse("M8 3L4 7l4 4 M4 7h16 M16 21l4-4-4-4 M20 17H4");

    public static TextBlock Text(string? text, ArcDesignTokenKind color, ArcDesignTokenKind size = ArcDesignTokenKind.TypeXS,
        bool mono = false, FontWeight weight = FontWeight.Normal, double lineHeight = 16.8)
    {
        var block = new TextBlock { Text = text, FontWeight = weight, LineHeight = lineHeight };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        if (mono)
        {
            ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        }
        return block;
    }

    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind? color = null)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center };
        if (color is { } kind)
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, kind);
        }
        return glyph;
    }

    public static StackPanel IconLabel(Geometry icon, string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    public static Button Button(ButtonVariant variant, object content, string? name = null)
    {
        var button = new Button { Variant = variant, Content = content };
        AutomationProperties.SetName(button, name ?? content as string);
        return button;
    }

    public static Ellipse Dot(ArcDesignTokenKind color, double size = 6)
    {
        var dot = new Ellipse { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(dot, Shape.FillProperty, color);
        return dot;
    }

    public static T Edge<T>(T border, ArcDesignTokenKind color, Thickness thickness) where T : Border
    {
        border.BorderThickness = thickness;
        ArcTokens.Bind(border, Border.BorderBrushProperty, color);
        return border;
    }

    /// <summary>The block's mono 12 font on a text field (Arc sets value fields in monospace).</summary>
    public static T Mono<T>(T control) where T : TemplatedControl
    {
        ArcTokens.Bind(control, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(control, TemplatedControl.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        return control;
    }

    public static Thickness Inset(bool narrow) => new(narrow ? 16 : 24, 0);
}

/// <summary>A border whose background and edge change on a 240ms ease (the editing row, the redeploy banner).</summary>
internal class EnvTintBorder : Border
{
    protected override Type StyleKeyOverride => typeof(EnvTintBorder);
}

/// <summary>An empty state: a title, a line and one button, centered under a top line (search without match, environments in sync).</summary>
internal sealed class EnvEmptyState : Border
{
    private readonly TextBlock _title;
    private readonly TextBlock _detail;
    private readonly Button _button;

    public EnvEmptyState()
    {
        EnvUi.Edge(this, ArcDesignTokenKind.Border, new Thickness(0, 1, 0, 0));
        Padding = new Thickness(24, 48, 24, 56);
        var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        _title = EnvUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, weight: FontWeight.Medium, lineHeight: 19.6);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.TextAlignment = TextAlignment.Center;
        _title.TextWrapping = TextWrapping.Wrap;
        _detail = EnvUi.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM, lineHeight: 19.6);
        _detail.HorizontalAlignment = HorizontalAlignment.Center;
        _detail.TextAlignment = TextAlignment.Center;
        _detail.TextWrapping = TextWrapping.Wrap;
        _detail.Margin = new Thickness(0, 0, 0, 12);
        _button = new Button { Variant = ButtonVariant.Secondary, HorizontalAlignment = HorizontalAlignment.Center };
        _button.Click += (_, _) => Action?.Invoke();
        stack.Children.Add(_title);
        stack.Children.Add(_detail);
        stack.Children.Add(_button);
        Child = stack;
    }

    public Action? Action { get; set; }

    public Button ActionButton => _button;

    public void Set(string title, string detail, string button, bool narrow)
    {
        _title.Text = title;
        _detail.Text = detail;
        _button.Content = button;
        AutomationProperties.SetName(_button, button);
        Padding = new Thickness(narrow ? 16 : 24, 48, narrow ? 16 : 24, 56);
    }
}

/// <summary>The variables view: the column header, the rows and the empty search state.</summary>
internal sealed class EnvVariablesPane : StackPanel
{
    private readonly EnvVariables _owner;
    private readonly Border _headerFrame;
    private readonly EnvRowPanel _header;
    private readonly List<TextBlock> _counts = new();
    private readonly DeveloperListPanel _list;
    private readonly EnvEmptyState _empty;
    private bool _compact;
    private bool _narrow;

    public EnvVariablesPane(EnvVariables owner)
    {
        _owner = owner;
        _header = new EnvRowPanel { MinLineHeight = 0, Margin = new Thickness(24, 12, 24, 10) };
        _header.Children.Add(HeaderText(owner.Labels.KeyColumn));
        _header.Children.Add(HeaderText(owner.Labels.ValueColumn));
        foreach (var env in owner.EffectiveEnvironments)
        {
            var cell = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
            var name = HeaderText(owner.EnvironmentName(env.Key));
            name.HorizontalAlignment = HorizontalAlignment.Center;
            var count = EnvUi.Text(null, ArcDesignTokenKind.TextMuted);
            count.HorizontalAlignment = HorizontalAlignment.Center;
            count.FontFeatures = TabularNumbers;
            _counts.Add(count);
            cell.Children.Add(name);
            cell.Children.Add(count);
            _header.Children.Add(cell);
        }
        _header.Children.Add(new Border());
        AutomationProperties.SetAccessibilityView(_header, AccessibilityView.Raw);
        _headerFrame = EnvUi.Edge(new Border { Child = _header }, ArcDesignTokenKind.Border, new Thickness(0, 1, 0, 1));
        _list = new DeveloperListPanel
        {
            Motion = DeveloperListMotion.Disclosure,
            ItemTemplate = new FuncDataTemplate<EnvVariable>((variable, _) =>
                variable is null ? new Panel() : new EnvVariableRow(owner, variable, _compact, _narrow)),
        };
        AutomationProperties.SetName(_list, owner.Labels.VariablesListName);
        _empty = new EnvEmptyState { IsVisible = false, Action = owner.ClearSearch };
        Children.Add(_headerFrame);
        Children.Add(_list);
        Children.Add(_empty);
    }

    private static readonly FontFeatureCollection TabularNumbers = new() { new FontFeature { Tag = "tnum", Value = 1 } };

    private static TextBlock HeaderText(string text)
    {
        var block = EnvUi.Text(text, ArcDesignTokenKind.TextSecondary);
        block.VerticalAlignment = VerticalAlignment.Bottom;
        return block;
    }

    public DeveloperListPanel List => _list;

    public EnvEmptyState Empty => _empty;

    public EnvVariableRow? RowFor(EnvVariable variable) => _list.ContainerFor(variable) as EnvVariableRow;

    public IEnumerable<EnvVariableRow> Rows => _list.LiveItems.Select(i => _list.ContainerFor(i)).OfType<EnvVariableRow>();

    public void Update(bool animate)
    {
        var visible = _owner.VisibleVariables;
        var noMatch = visible.Count == 0 && !string.IsNullOrWhiteSpace(_owner.SearchText);
        _empty.IsVisible = noMatch;
        if (noMatch)
        {
            _empty.Set(_owner.Labels.NoMatchTitle(_owner.SearchText.Trim()), _owner.Labels.NoMatchDetail,
                _owner.Labels.ClearSearchButton, _narrow);
        }
        // Arc swaps the whole view between the list and the empty state, so that switch is instant; row changes animate.
        var wasEmpty = !_list.IsVisible;
        _list.IsVisible = !noMatch;
        if (!animate || wasEmpty || noMatch)
        {
            _list.Change(DeveloperListMotion.Instant, () => _list.Items = visible);
        }
        else
        {
            _list.Items = visible;
        }
        RefreshRows();
        UpdateHeaderCounts();
    }

    public void RefreshRows()
    {
        var index = 0;
        foreach (var item in _list.LiveItems)
        {
            if (_list.ContainerFor(item) is EnvVariableRow row)
            {
                row.ShowTopLine = index > 0;
                row.Refresh();
            }
            index++;
        }
    }

    public void UpdateHeaderCounts()
    {
        var envs = _owner.EffectiveEnvironments;
        for (var i = 0; i < envs.Count && i < _counts.Count; i++)
        {
            _counts[i].Text = _owner.Labels.SetCount(EnvVariableRules.CountSet(_owner.SortedVariables, envs[i].Key));
        }
    }

    public EnvVariable? NeighbourOf(EnvVariable variable)
    {
        var live = _list.LiveItems;
        var index = -1;
        for (var i = 0; i < live.Count; i++)
        {
            if (ReferenceEquals(live[i], variable))
            {
                index = i;
            }
        }
        if (index < 0)
        {
            return null;
        }
        if (index + 1 < live.Count)
        {
            return live[index + 1] as EnvVariable;
        }
        return index > 0 ? live[index - 1] as EnvVariable : null;
    }

    public void Glow(EnvVariable variable) => _list.Flash(variable, DeveloperFlash.Glow);

    public void SetLayout(bool compact, bool narrow)
    {
        _compact = compact;
        _narrow = narrow;
        var inset = narrow ? 16 : 24;
        _header.IsVisible = !compact;
        _headerFrame.BorderThickness = new Thickness(0, 1, 0, compact ? 0 : 1);
        _header.Margin = new Thickness(inset, 12, inset, 10);
        foreach (var row in Rows)
        {
            row.SetLayout(compact, narrow);
        }
        if (_empty.IsVisible)
        {
            Update(animate: false);
        }
    }

    public void StopTimers()
    {
        foreach (var row in Rows)
        {
            row.StopTimers();
        }
    }
}

/// <summary>
/// One row of the variables view: the key and its second line, the masked value, a switch per environment and the
/// edit button, with the conflict note and the inline editor opening under it. The flash of the list panel shows
/// through its transparent root.
/// </summary>
internal sealed class EnvVariableRow : Border
{
    private readonly EnvVariables _owner;
    private readonly EnvTintBorder _body;
    private readonly EnvRowPanel _main;
    private readonly TextBlock _key;
    private readonly Ellipse _dot;
    private readonly TextBlock _updated;
    private readonly TextBlock _pending;
    private readonly DeveloperSecretField _value;
    private readonly List<EnvScopeToggle> _toggles = new();
    private readonly DeveloperIconToggle _edit;
    private readonly EnvDisclosure _conflictHost;
    private readonly EnvDisclosure _editorHost;
    private EnvVariableEditor? _editor;
    private DispatcherTimer? _conflictTimer;
    private bool _syncing;
    private bool _narrow;
    private bool _editing;

    public EnvVariableRow(EnvVariables owner, EnvVariable variable, bool compact, bool narrow)
    {
        _owner = owner;
        Variable = variable;
        EnvUi.Edge(this, ArcDesignTokenKind.BorderSubtle, default);

        var keyLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _key = EnvUi.Text(variable.Key, ArcDesignTokenKind.Foreground, mono: true, weight: FontWeight.Medium);
        _key.TextTrimming = TextTrimming.CharacterEllipsis;
        _dot = EnvUi.Dot(ArcDesignTokenKind.Accent);
        keyLine.Children.Add(_key);
        keyLine.Children.Add(_dot);
        _updated = EnvUi.Text(null, ArcDesignTokenKind.TextMuted);
        _updated.TextTrimming = TextTrimming.CharacterEllipsis;
        _pending = EnvUi.Text(owner.Labels.PendingRow, ArcDesignTokenKind.Accent);
        _pending.TextTrimming = TextTrimming.CharacterEllipsis;
        var keyCell = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        keyCell.Children.Add(keyLine);
        keyCell.Children.Add(_updated);
        keyCell.Children.Add(_pending);

        _value = new DeveloperSecretField
        {
            Mode = DeveloperSecretMode.Masked,
            VerticalAlignment = VerticalAlignment.Center,
            RevealNameFormat = owner.Labels.RevealNameFormat,
            CopyNameFormat = owner.Labels.CopyNameFormat,
            HiddenText = owner.Labels.HiddenValue,
            SensitiveText = owner.Labels.Sensitive,
            SensitiveName = owner.Labels.SensitiveValue,
        };
        // The value is read only at the moment it is revealed (or copied), and the host can load it just before.
        _value.SecretProvider = () =>
        {
            owner.OnValueRevealed(Variable, true);
            return Variable.IsSensitive ? null : Variable.Value;
        };
        _value.RevealToggled += (_, on) =>
        {
            if (!on)
            {
                owner.OnValueRevealed(Variable, false);
            }
        };

        _main = new EnvRowPanel { ScopeWidth = 92, EditWidth = 52 };
        _main.Children.Add(keyCell);
        _main.Children.Add(_value);
        foreach (var env in owner.EffectiveEnvironments)
        {
            var toggle = new EnvScopeToggle
            {
                Environment = env.Key,
                Label = owner.EnvironmentName(env.Key),
                VerticalAlignment = VerticalAlignment.Center,
                ToggleRequested = t => owner.RequestToggle(Variable, t.Environment),
            };
            _toggles.Add(toggle);
            _main.Children.Add(toggle);
        }
        _edit = new DeveloperIconToggle
        {
            Content = new ArcGlyph { Data = ArcIcons.Pencil, Size = 16 },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _edit.IsCheckedChanged += OnEditToggled;
        _main.Children.Add(_edit);

        _conflictHost = new EnvDisclosure();
        _editorHost = new EnvDisclosure();
        var stack = new StackPanel();
        stack.Children.Add(_main);
        stack.Children.Add(_conflictHost);
        stack.Children.Add(_editorHost);
        _body = new EnvTintBorder { Background = Brushes.Transparent, Child = stack };
        Child = _body;
        SetLayout(compact, narrow);
        Refresh();
    }

    public EnvVariable Variable { get; }

    public DeveloperSecretField ValueField => _value;

    public IReadOnlyList<EnvScopeToggle> Toggles => _toggles;

    public DeveloperIconToggle EditButton => _edit;

    public EnvVariableEditor? Editor => _editor;

    public bool IsEditing => _editing;

    public bool IsConflictShown => _conflictHost.IsOpen;

    public Control? ConflictNote => _conflictHost.Child;

    public bool ShowTopLine
    {
        set => BorderThickness = new Thickness(0, value ? 1 : 0, 0, 0);
    }

    public void SetLayout(bool compact, bool narrow)
    {
        _narrow = narrow;
        var inset = narrow ? 16 : 24;
        _main.Layout = compact ? EnvRowLayout.Stacked : EnvRowLayout.Matrix;
        _main.Margin = compact ? new Thickness(inset, 12) : new Thickness(inset, 10);
        foreach (var toggle in _toggles)
        {
            toggle.IsPill = compact;
        }
        _editor?.SetNarrow(narrow);
        if (_conflictHost.Child is { } note)
        {
            note.Margin = new Thickness(inset, 0, inset, 12);
        }
    }

    public void Refresh()
    {
        var v = Variable;
        var labels = _owner.Labels;
        _key.Text = v.Key;
        _dot.IsVisible = v.IsPendingDeploy;
        _pending.IsVisible = v.IsPendingDeploy;
        var updated = labels.Updated(v.UpdatedAt, v.UpdatedBy);
        _updated.Text = updated;
        _updated.IsVisible = !v.IsPendingDeploy && updated.Length > 0;
        _value.Subject = v.Key;
        if (_value.IsSensitive != v.IsSensitive)
        {
            _value.IsSensitive = v.IsSensitive;
        }
        var readOnly = _owner.IsReadOnly;
        AutomationProperties.SetName(_main, labels.ScopeGroupName(v.Key));
        foreach (var toggle in _toggles)
        {
            toggle.IsChecked = v.Environments.Contains(toggle.Environment);
            toggle.IsHitTestVisible = !readOnly;
            toggle.Focusable = !readOnly;
            AutomationProperties.SetName(toggle, labels.ScopeName(_owner.EnvironmentName(toggle.Environment), v.Key));
        }
        _edit.IsVisible = !readOnly;
        AutomationProperties.SetName(_edit, labels.EditName(v.Key));
    }

    private void OnEditToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        if (_edit.IsChecked == true)
        {
            _owner.BeginEdit(Variable);
            if (!_editing)
            {
                SyncEdit(false);
            }
        }
        else if (_editing)
        {
            _owner.CloseEditor(restoreFocus: true);
        }
    }

    private void SyncEdit(bool on)
    {
        _syncing = true;
        _edit.IsChecked = on;
        _syncing = false;
    }

    public void OpenEditor()
    {
        _editing = true;
        SyncEdit(true);
        _editor = new EnvVariableEditor(_owner, Variable, _narrow);
        _editorHost.Open(_editor);
        UpdateTint();
        var editor = _editor;
        Dispatcher.UIThread.Post(() => editor.FocusInput(), DispatcherPriority.Loaded);
    }

    public void CloseEditor(bool restoreFocus)
    {
        if (!_editing)
        {
            return;
        }
        _editing = false;
        SyncEdit(false);
        var hadFocus = _editor?.IsKeyboardFocusWithin == true;
        _editorHost.Close();
        _editor = null;
        UpdateTint();
        if (restoreFocus || hadFocus)
        {
            // Save or Cancel clicked with the mouse hands focus back without the keyboard ring.
            _edit.Focus(BlockFocusModality.For(this));
        }
    }

    public void FocusEdit() => _edit.Focus(BlockFocusModality.For(this));

    public void ShowConflict(string text)
    {
        var inset = _narrow ? 16 : 24;
        var note = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            Margin = new Thickness(inset, 0, inset, 12),
        };
        var icon = EnvUi.Glyph(ArcIcons.TriangleAlert, 14, ArcDesignTokenKind.Warning);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 0, 0);
        var message = EnvUi.Text(text, ArcDesignTokenKind.TextSecondary);
        message.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(message, 1);
        note.Children.Add(icon);
        note.Children.Add(message);
        AutomationProperties.SetName(note, text);
        AutomationProperties.SetLiveSetting(note, AutomationLiveSetting.Assertive);
        _conflictHost.Open(note);
        _conflictTimer?.Stop();
        _conflictTimer = new DispatcherTimer { Interval = DeveloperSprings.ConflictDismiss };
        _conflictTimer.Tick += (_, _) =>
        {
            _conflictTimer?.Stop();
            _conflictHost.Close();
        };
        _conflictTimer.Start();
    }

    /// <summary>Closes the conflict note now (its timer ended).</summary>
    internal void DismissConflict()
    {
        _conflictTimer?.Stop();
        _conflictHost.Close();
    }

    public void StopTimers() => _conflictTimer?.Stop();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        UpdateBrushes();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        StopTimers();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateBrushes();

    private void UpdateBrushes()
    {
        if (VisualRoot is null)
        {
            return;
        }
        var palette = DeveloperPalette.For(this);
        _edit.HoverBrush = palette.IconHover;
        _edit.OnBrush = palette.IconOn;
        UpdateTint();
    }

    private void UpdateTint()
    {
        if (VisualRoot is null)
        {
            _body.Background = Brushes.Transparent;
            return;
        }
        _body.Background = _editing ? EnvPalette.For(this).EditingRow : Brushes.Transparent;
    }
}

/// <summary>The inline editor card under a row: the value, the sensitive option, Remove (two steps), Cancel and Save.</summary>
internal sealed class EnvVariableEditor : Border
{
    private readonly EnvVariables _owner;
    private readonly EnvVariable _variable;
    private readonly Input _input;
    private readonly Checkbox _sensitive;
    private readonly Button _remove;
    private readonly Button _cancel;
    private readonly Button _save;
    private readonly Grid _actions;
    private readonly string _original;
    private readonly bool _wasSensitive;
    private bool _confirming;

    public EnvVariableEditor(EnvVariables owner, EnvVariable variable, bool narrow)
    {
        _owner = owner;
        _variable = variable;
        var labels = owner.Labels;
        _wasSensitive = variable.IsSensitive;
        _original = _wasSensitive ? string.Empty : variable.Value ?? string.Empty;
        Padding = new Thickness(16);
        EnvUi.Edge(this, ArcDesignTokenKind.Border, new Thickness(1));
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);

        _input = EnvUi.Mono(new Input
        {
            Label = labels.EditorLabel(owner.JoinEnvironmentNames(variable.Environments)),
            Text = _original,
            Placeholder = _wasSensitive ? labels.ReplacePlaceholder : null,
        });
        _input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                if (_input.IsInvalid && !string.IsNullOrEmpty(_input.Text))
                {
                    _input.IsInvalid = false;
                    _input.ErrorMessage = null;
                }
                UpdateSave();
            }
        };
        // Tunnel: a text box may take Enter for itself.
        _input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Submit();
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _sensitive = new Checkbox
        {
            Content = labels.SensitiveOption,
            Description = _wasSensitive ? labels.SensitiveLockedHint : labels.SensitiveEditorHint,
            IsChecked = _wasSensitive,
            IsEnabled = !_wasSensitive,
        };
        _sensitive.IsCheckedChanged += (_, _) => UpdateSave();

        _remove = EnvUi.Button(ButtonVariant.Ghost, labels.RemoveButton);
        _remove.Click += (_, _) => OnRemove();
        _remove.LostFocus += (_, _) => ResetRemove();
        _cancel = EnvUi.Button(ButtonVariant.Secondary, labels.CancelButton);
        _cancel.Click += (_, _) => owner.CloseEditor(restoreFocus: true);
        _save = EnvUi.Button(ButtonVariant.Primary, labels.SaveValueButton);
        _save.Click += (_, _) => Submit();
        _actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        _actions.Children.Add(_remove);
        _actions.Children.Add(_cancel);
        _actions.Children.Add(_save);

        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(_input);
        stack.Children.Add(_sensitive);
        stack.Children.Add(_actions);
        Child = stack;
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                owner.CloseEditor(restoreFocus: true);
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        SetNarrow(narrow);
        UpdateSave();
    }

    public Input ValueInput => _input;

    public Checkbox SensitiveBox => _sensitive;

    public Button RemoveButton => _remove;

    public Button SaveButton => _save;

    public Button CancelButton => _cancel;

    public bool IsConfirmingRemove => _confirming;

    public void SetNarrow(bool narrow)
    {
        var inset = narrow ? 16 : 24;
        Margin = new Thickness(inset, 0, inset, 16);
        _actions.ColumnDefinitions.Clear();
        _actions.RowDefinitions.Clear();
        if (narrow)
        {
            // Cancel and Save share the first line; Remove takes the second.
            _actions.ColumnDefinitions = new ColumnDefinitions("*,*");
            _actions.RowDefinitions = new RowDefinitions("Auto,Auto");
            Place(_cancel, 0, 0, 1);
            Place(_save, 0, 1, 1);
            Place(_remove, 1, 0, 2);
            _remove.HorizontalAlignment = HorizontalAlignment.Stretch;
            _cancel.HorizontalAlignment = HorizontalAlignment.Stretch;
            _save.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            _actions.ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto");
            Place(_remove, 0, 0, 1);
            Place(_cancel, 0, 1, 1);
            Place(_save, 0, 2, 1);
            _remove.HorizontalAlignment = HorizontalAlignment.Left;
            _cancel.HorizontalAlignment = HorizontalAlignment.Right;
            _save.HorizontalAlignment = HorizontalAlignment.Right;
        }
    }

    private static void Place(Control control, int row, int column, int span)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
    }

    public void FocusInput()
    {
        _input.Focus(NavigationMethod.Tab);
        _input.CaretIndex = _input.Text?.Length ?? 0;
    }

    private bool HasChange =>
        !string.IsNullOrEmpty(_input.Text)
        && (!string.Equals(_input.Text, _original, StringComparison.Ordinal) || (_sensitive.IsChecked == true) != _wasSensitive);

    private void UpdateSave() => _save.IsEnabled = HasChange;

    /// <summary>Enter or Save: an empty value shows the error; an unchanged one does nothing.</summary>
    public void Submit()
    {
        if (string.IsNullOrEmpty(_input.Text))
        {
            _input.ErrorMessage = _owner.Labels.EmptyValueError;
            _input.IsInvalid = true;
            _input.Focus(NavigationMethod.Tab);
            return;
        }
        if (!HasChange)
        {
            return;
        }
        _owner.SaveValue(_variable, _input.Text ?? string.Empty, _sensitive.IsChecked == true);
    }

    private void OnRemove()
    {
        if (!_confirming)
        {
            _confirming = true;
            _remove.Variant = ButtonVariant.Danger;
            _remove.Content = _owner.Labels.ConfirmRemoveButton;
            AutomationProperties.SetName(_remove, _owner.Labels.ConfirmRemoveButton);
            return;
        }
        _owner.RemoveVariable(_variable);
    }

    private void ResetRemove()
    {
        if (!_confirming)
        {
            return;
        }
        _confirming = false;
        _remove.Variant = ButtonVariant.Ghost;
        _remove.Content = _owner.Labels.RemoveButton;
        AutomationProperties.SetName(_remove, _owner.Labels.RemoveButton);
    }
}

/// <summary>The add panel: one variable (key, value, environments, sensitive) or a pasted .env file with a live preview.</summary>
internal sealed class EnvAddPanel : Border
{
    private readonly EnvVariables _owner;
    private readonly SegmentedControl _modeSwitch;
    private readonly StackPanel _single;
    private readonly StackPanel _dotenv;
    private readonly Grid _fields;
    private readonly Input _key;
    private readonly Input _value;
    private readonly WrapPanel _singlePills;
    private readonly TextBlock _envError;
    private readonly Checkbox _sensitive;
    private readonly Grid _singleActions;
    private readonly Button _singleCancel;
    private readonly Button _save;
    private readonly Textarea _text;
    private readonly WrapPanel _importPills;
    private readonly TextBlock _summary;
    private readonly TextBlock _emptyHint;
    private readonly Border _preview;
    private readonly StackPanel _previewLines;
    private readonly Grid _dotenvActions;
    private readonly Button _dotenvCancel;
    private readonly Button _import;
    private readonly EnvSplitPanel _options;
    private EnvAddMode _mode;
    private bool _submitted;
    private bool _syncing;
    private bool _narrow;
    private EnvDotenvResult _parsed = EnvDotenvResult.Empty;

    public EnvAddPanel(EnvVariables owner)
    {
        _owner = owner;
        var labels = owner.Labels;
        Padding = new Thickness(16);
        EnvUi.Edge(this, ArcDesignTokenKind.Border, new Thickness(1));
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);

        _modeSwitch = new SegmentedControl
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = new[]
            {
                new SegmentedOption(EnvAddMode.Single, labels.SingleMode),
                new SegmentedOption(EnvAddMode.Dotenv, labels.DotenvMode),
            },
            SelectedIndex = 0,
        };
        AutomationProperties.SetName(_modeSwitch, labels.AddModeName);
        _modeSwitch.SelectionChanged += (_, _) =>
        {
            if (!_syncing)
            {
                SetMode(_modeSwitch.SelectedIndex == 1 ? EnvAddMode.Dotenv : EnvAddMode.Single, focus: false);
            }
        };

        // ---- One variable ----
        _key = EnvUi.Mono(new Input { Label = labels.KeyLabel, Placeholder = labels.KeyPlaceholder, Description = labels.KeyHint });
        _key.AddHandler(TextBox.PastingFromClipboardEvent, OnKeyPasting);
        _value = EnvUi.Mono(new Input { Label = labels.ValueLabel });
        _fields = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _fields.Children.Add(_key);
        _fields.Children.Add(_value);
        foreach (var input in new[] { _key, _value })
        {
            input.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty && _submitted)
                {
                    ValidateSingle(focusError: false);
                }
            };
            input.AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    SubmitSingle();
                    e.Handled = true;
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }
        _singlePills = Pills();
        _envError = EnvUi.Text(labels.EnvironmentsError, ArcDesignTokenKind.Danger);
        _envError.IsVisible = false;
        var envGroup = new StackPanel { Spacing = 8 };
        envGroup.Children.Add(Legend(labels.EnvironmentsLegend));
        envGroup.Children.Add(_singlePills);
        envGroup.Children.Add(_envError);
        AutomationProperties.SetName(envGroup, labels.EnvironmentsLegend);
        _sensitive = new Checkbox { Content = labels.SensitiveOption, Description = labels.SensitiveAddHint, VerticalAlignment = VerticalAlignment.Bottom };
        _options = new EnvSplitPanel { ColumnGap = 32, RowGap = 16, LineAlignment = VerticalAlignment.Bottom };
        _options.Children.Add(envGroup);
        _options.Children.Add(_sensitive);
        _singleCancel = EnvUi.Button(ButtonVariant.Secondary, labels.CancelButton);
        _singleCancel.Click += (_, _) => owner.CloseAddPanel(restoreFocus: true);
        _save = EnvUi.Button(ButtonVariant.Primary, labels.SaveVariableButton);
        _save.Click += (_, _) => SubmitSingle();
        _singleActions = new Grid { ColumnSpacing = 8 };
        _singleActions.Children.Add(_singleCancel);
        _singleActions.Children.Add(_save);
        _single = new StackPanel { Spacing = 16 };
        _single.Children.Add(_fields);
        _single.Children.Add(_options);
        _single.Children.Add(_singleActions);

        // ---- Paste .env ----
        _text = new Textarea { Label = labels.DotenvLabel, IsResizable = true, FrameHeight = 168.7, AcceptsReturn = true };
        EnvUi.Mono(_text);
        _text.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Reparse();
            }
        };
        _importPills = Pills();
        var importGroup = new StackPanel { Spacing = 8 };
        importGroup.Children.Add(Legend(labels.ImportIntoLegend));
        importGroup.Children.Add(_importPills);
        AutomationProperties.SetName(importGroup, labels.ImportIntoLegend);
        _summary = EnvUi.Text(null, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetLiveSetting(_summary, AutomationLiveSetting.Polite);
        _emptyHint = EnvUi.Text(labels.DotenvEmptyHint, ArcDesignTokenKind.TextSecondary);
        _emptyHint.TextWrapping = TextWrapping.Wrap;
        _previewLines = new StackPanel();
        AutomationProperties.SetName(_previewLines, labels.DotenvPreviewName);
        _preview = EnvUi.Edge(new Border { Child = _previewLines, ClipToBounds = true }, ArcDesignTokenKind.Border, new Thickness(1));
        _preview.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(_preview, BackgroundProperty, ArcDesignTokenKind.Surface);
        var parseArea = new StackPanel { Spacing = 8 };
        parseArea.Children.Add(_summary);
        parseArea.Children.Add(_emptyHint);
        parseArea.Children.Add(_preview);
        _dotenvCancel = EnvUi.Button(ButtonVariant.Secondary, labels.CancelButton);
        _dotenvCancel.Click += (_, _) => owner.CloseAddPanel(restoreFocus: true);
        _import = EnvUi.Button(ButtonVariant.Primary, labels.ImportCountButton(0));
        _import.Click += (_, _) => SubmitImport();
        _dotenvActions = new Grid { ColumnSpacing = 8 };
        _dotenvActions.Children.Add(_dotenvCancel);
        _dotenvActions.Children.Add(_import);
        _dotenv = new StackPanel { Spacing = 16, IsVisible = false };
        _dotenv.Children.Add(_text);
        _dotenv.Children.Add(importGroup);
        _dotenv.Children.Add(parseArea);
        _dotenv.Children.Add(_dotenvActions);

        var root = new StackPanel { Spacing = 16 };
        root.Children.Add(_modeSwitch);
        root.Children.Add(_single);
        root.Children.Add(_dotenv);
        Child = root;
        SetNarrow(false);
    }

    public EnvAddMode Mode => _mode;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        OnThemeChanged(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
    }

    // SurfaceMuted 55% into Surface (Arc mixes it; no token holds it).
    private void OnThemeChanged(object? sender, EventArgs e) => Background = EnvPalette.For(this).PanelFill;

    public Input KeyInput => _key;

    public Input ValueInput => _value;

    public Textarea DotenvText => _text;

    public Button SaveButton => _save;

    public Button ImportButton => _import;

    public TextBlock EnvironmentError => _envError;

    public Checkbox SensitiveBox => _sensitive;

    public EnvDotenvResult Parsed => _parsed;

    public IEnumerable<EnvScopeToggle> SinglePills => _singlePills.Children.OfType<EnvScopeToggle>();

    public IEnumerable<EnvScopeToggle> ImportPills => _importPills.Children.OfType<EnvScopeToggle>();

    public int PreviewLineCount => _previewLines.Children.Count;

    private static TextBlock Legend(string text) =>
        EnvUi.Text(text, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, weight: FontWeight.Medium, lineHeight: 19.6);

    private WrapPanel Pills()
    {
        var wrap = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        foreach (var env in _owner.EffectiveEnvironments)
        {
            var pill = new EnvScopeToggle
            {
                Environment = env.Key,
                Label = _owner.EnvironmentName(env.Key),
                IsPill = true,
                Padding = new Thickness(10, 0, 14, 0),
            };
            AutomationProperties.SetName(pill, _owner.EnvironmentName(env.Key));
            pill.IsCheckedChanged += (_, _) => OnPillsChanged();
            wrap.Children.Add(pill);
        }
        return wrap;
    }

    private static IReadOnlyList<string> Chosen(WrapPanel pills) =>
        pills.Children.OfType<EnvScopeToggle>().Where(p => p.IsChecked == true).Select(p => p.Environment).ToList();

    private static void Choose(WrapPanel pills, IReadOnlyCollection<string> environments)
    {
        foreach (var pill in pills.Children.OfType<EnvScopeToggle>())
        {
            pill.IsChecked = environments.Contains(pill.Environment);
        }
    }

    private void OnPillsChanged()
    {
        if (_syncing)
        {
            return;
        }
        if (_mode == EnvAddMode.Single && _submitted)
        {
            ValidateSingle(focusError: false);
        }
        if (_mode == EnvAddMode.Dotenv)
        {
            Reparse();
        }
    }

    /// <summary>Starts over on <paramref name="mode"/> (Arc always resets the panel when it opens).</summary>
    public void Reset(EnvAddMode mode, IReadOnlyCollection<string> environments, string dotenvText)
    {
        _syncing = true;
        _submitted = false;
        _key.Text = string.Empty;
        _value.Text = string.Empty;
        ClearError(_key);
        ClearError(_value);
        _envError.IsVisible = false;
        _sensitive.IsChecked = false;
        Choose(_singlePills, environments);
        Choose(_importPills, environments);
        _text.Text = dotenvText;
        _syncing = false;
        SetMode(mode, focus: false);
        Reparse();
    }

    public void SetMode(EnvAddMode mode, bool focus)
    {
        _mode = mode;
        _syncing = true;
        _modeSwitch.SelectedIndex = mode == EnvAddMode.Dotenv ? 1 : 0;
        _syncing = false;
        // Arc swaps the two forms and the height at once (no animation).
        _single.IsVisible = mode == EnvAddMode.Single;
        _dotenv.IsVisible = mode == EnvAddMode.Dotenv;
        AutomationProperties.SetName(this, mode == EnvAddMode.Dotenv ? _owner.Labels.ImportPanelName : _owner.Labels.AddPanelName);
        if (mode == EnvAddMode.Dotenv)
        {
            Reparse();
        }
        if (focus)
        {
            FocusFirst();
        }
    }

    public void FocusFirst()
    {
        if (_mode == EnvAddMode.Dotenv)
        {
            _text.Focus(NavigationMethod.Tab);
        }
        else
        {
            _key.Focus(NavigationMethod.Tab);
        }
    }

    public void SetNarrow(bool narrow)
    {
        _narrow = narrow;
        var inset = narrow ? 16 : 24;
        Margin = new Thickness(inset, 0, inset, 16);
        Grid.SetColumn(_value, narrow ? 0 : 1);
        Grid.SetRow(_value, narrow ? 1 : 0);
        _fields.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "5*,7*");
        _fields.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
        _options.IsStacked = narrow;
        LayoutActions(_singleActions, _singleCancel, _save, narrow);
        LayoutActions(_dotenvActions, _dotenvCancel, _import, narrow);
        RebuildPreview();
    }

    private static void LayoutActions(Grid grid, Button cancel, Button primary, bool narrow)
    {
        grid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*,*" : "*,Auto,Auto");
        Grid.SetColumn(cancel, narrow ? 0 : 1);
        Grid.SetColumn(primary, narrow ? 1 : 2);
        cancel.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        primary.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
    }

    private static void ClearError(Input input)
    {
        input.IsInvalid = false;
        input.ErrorMessage = null;
    }

    private static void SetError(Input input, string? error)
    {
        input.ErrorMessage = error;
        input.IsInvalid = error is not null;
    }

    private (string? Key, string? Value, string? Environments) ValidateSingle(bool focusError)
    {
        var errors = _owner.ValidateNew(_key.Text?.Trim() ?? string.Empty, _value.Text ?? string.Empty, Chosen(_singlePills));
        SetError(_key, errors.Key);
        SetError(_value, errors.Value);
        _envError.Text = errors.Environments;
        _envError.IsVisible = errors.Environments is not null;
        if (focusError)
        {
            if (errors.Key is not null)
            {
                _key.Focus(NavigationMethod.Tab);
            }
            else if (errors.Value is not null)
            {
                _value.Focus(NavigationMethod.Tab);
            }
            else if (errors.Environments is not null)
            {
                _singlePills.Children.OfType<EnvScopeToggle>().FirstOrDefault()?.Focus(BlockFocusModality.For(this));
            }
        }
        return errors;
    }

    public void SubmitSingle()
    {
        _submitted = true;
        var errors = ValidateSingle(focusError: true);
        if (errors.Key is not null || errors.Value is not null || errors.Environments is not null)
        {
            return;
        }
        _owner.AddVariable(_key.Text!.Trim(), _value.Text ?? string.Empty, Chosen(_singlePills), _sensitive.IsChecked == true);
    }

    private void Reparse()
    {
        if (_syncing)
        {
            return;
        }
        var envs = Chosen(_importPills);
        _parsed = EnvVariableDotenvParser.Parse(_text.Text, envs, _owner.SortedVariables);
        _summary.Text = _owner.Labels.DotenvSummary(_parsed.ImportCount, _parsed.SkippedCount);
        var empty = string.IsNullOrWhiteSpace(_text.Text);
        _summary.IsVisible = !empty;
        _emptyHint.IsVisible = empty;
        _preview.IsVisible = _parsed.Lines.Count > 0;
        var label = _owner.Labels.ImportCountButton(_parsed.ImportCount);
        if (!Equals(_import.Content, label))
        {
            _import.Content = label;
            AutomationProperties.SetName(_import, label);
        }
        _import.IsEnabled = _parsed.ImportCount > 0 && envs.Count > 0;
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        _previewLines.Children.Clear();
        var first = true;
        foreach (var line in _parsed.Lines)
        {
            _previewLines.Children.Add(PreviewLine(line, first));
            first = false;
        }
    }

    private Control PreviewLine(EnvDotenvLine line, bool first)
    {
        var labels = _owner.Labels;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(_narrow ? "24,*" : "24,10*,64,13*"),
            RowDefinitions = new RowDefinitions(_narrow ? "Auto,Auto" : "Auto"),
            ColumnSpacing = 12,
            RowSpacing = 2,
            Margin = new Thickness(12, 8),
            MinHeight = 24,
        };
        var number = EnvUi.Text(line.LineNumber.ToString(System.Globalization.CultureInfo.CurrentCulture), ArcDesignTokenKind.TextMuted);
        number.VerticalAlignment = VerticalAlignment.Center;
        number.HorizontalAlignment = HorizontalAlignment.Right;
        var key = EnvUi.Text(line.Key, line.IsSkipped ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.Foreground, mono: true);
        key.TextTrimming = TextTrimming.CharacterEllipsis;
        key.VerticalAlignment = VerticalAlignment.Center;
        if (line.IsSkipped)
        {
            key.TextDecorations = TextDecorations.Strikethrough;
        }
        Grid.SetColumn(key, 1);
        grid.Children.Add(number);
        grid.Children.Add(key);
        if (!_narrow && line.Value is { Length: > 0 })
        {
            // Eight bullets whatever the value: the preview never shows it.
            var dots = EnvUi.Text(DeveloperText.Bullets(8), ArcDesignTokenKind.TextMuted);
            dots.LetterSpacing = 0.72;
            dots.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetAccessibilityView(dots, AccessibilityView.Raw);
            Grid.SetColumn(dots, 2);
            grid.Children.Add(dots);
        }
        var (icon, color, text) = line.Status switch
        {
            EnvDotenvStatus.New => (ArcIcons.Plus, ArcDesignTokenKind.TextMuted, labels.DotenvNew),
            EnvDotenvStatus.Replaces => (ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning,
                labels.DotenvReplaces(_owner.JoinEnvironmentNames(line.ReplacedEnvironments))),
            EnvDotenvStatus.Duplicate => (ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, labels.DotenvDuplicate(line.SetAgainOn)),
            EnvDotenvStatus.StartsWithDigit => (ArcIcons.CircleAlert, ArcDesignTokenKind.Danger, labels.DotenvDigit),
            EnvDotenvStatus.InvalidCharacters => (ArcIcons.CircleAlert, ArcDesignTokenKind.Danger, labels.DotenvCharacters),
            _ => (ArcIcons.CircleAlert, ArcDesignTokenKind.Danger, labels.DotenvMissingEquals),
        };
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        status.Children.Add(EnvUi.Glyph(icon, 14, color));
        var statusText = EnvUi.Text(text, ArcDesignTokenKind.TextSecondary);
        statusText.TextTrimming = TextTrimming.CharacterEllipsis;
        status.Children.Add(statusText);
        Grid.SetColumn(status, _narrow ? 1 : 3);
        Grid.SetRow(status, _narrow ? 1 : 0);
        grid.Children.Add(status);
        var row = EnvUi.Edge(new Border { Child = grid }, ArcDesignTokenKind.BorderSubtle, new Thickness(0, first ? 0 : 1, 0, 0));
        AutomationProperties.SetName(row, $"{line.LineNumber} {line.Key}: {text}");
        return row;
    }

    public void SubmitImport()
    {
        var envs = Chosen(_importPills);
        if (_parsed.ImportCount == 0 || envs.Count == 0)
        {
            return;
        }
        _owner.Import(_parsed, envs);
    }

    private async void OnKeyPasting(object? sender, RoutedEventArgs e)
    {
        // A single-line box keeps only the first line of a paste: read the clipboard first, and switch to the .env form
        // when it holds several lines.
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        e.Handled = true;
        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception)
        {
            text = null;
        }
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (text.Contains('\n'))
        {
            _text.Text = text.Replace("\r\n", "\n");
            SetMode(EnvAddMode.Dotenv, focus: true);
            _text.CaretIndex = _text.Text?.Length ?? 0;
            return;
        }
        PasteInto(_key, text.Trim());
    }

    private static void PasteInto(TextBox box, string text)
    {
        var current = box.Text ?? string.Empty;
        var start = Math.Min(box.SelectionStart, box.SelectionEnd);
        var end = Math.Max(box.SelectionStart, box.SelectionEnd);
        start = Math.Clamp(start, 0, current.Length);
        end = Math.Clamp(end, 0, current.Length);
        box.Text = current[..start] + text + current[end..];
        box.CaretIndex = start + text.Length;
    }

    /// <summary>Switches to the .env form with <paramref name="text"/> (what a multi-line paste into the key does).</summary>
    internal void PasteDotenv(string text)
    {
        _text.Text = text;
        SetMode(EnvAddMode.Dotenv, focus: true);
    }
}

/// <summary>
/// The redeploy banner: an icon (Info, then Check on success, CircleAlert on failure), the title and the line (swapped
/// with a short exit then a rising entry; the line 40ms later), and the Redeploy button (spinner while running, Retry
/// after a failure, gone after success). Accent, Success or Danger tint, eased over 240ms.
/// </summary>
internal sealed class EnvRedeployBanner : EnvTintBorder
{
    private readonly EnvVariables _owner;
    private readonly Grid _grid;
    private readonly ArcContentSwap _icon;
    private readonly ArcContentSwap _title;
    private readonly ArcContentSwap _detail;
    private readonly ArcContentSwap _actionSlot;
    private Button? _action;
    private EnvRedeployState _state = (EnvRedeployState)(-1);
    private string? _titleText;
    private string? _detailText;
    private bool _narrow;

    public EnvRedeployBanner(EnvVariables owner)
    {
        _owner = owner;
        Padding = new Thickness(16, 12, 12, 12);
        BorderThickness = new Thickness(1);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        _icon = new ArcContentSwap { Mode = ContentSwapMode.Pop, Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) };
        _title = new ArcContentSwap
        {
            Mode = ContentSwapMode.Label,
            ExitDuration = ArcMotionPresets.Instant,
            EnterDuration = ArcMotionPresets.Standard,
            EnterOffset = 4,
        };
        _detail = new ArcContentSwap
        {
            Mode = ContentSwapMode.Label,
            ExitDuration = ArcMotionPresets.Instant,
            EnterDuration = ArcMotionPresets.Standard,
            EnterDelay = TimeSpan.FromMilliseconds(40),
            EnterOffset = 4,
        };
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_title);
        texts.Children.Add(_detail);
        _actionSlot = new ArcContentSwap { Mode = ContentSwapMode.Fade, VerticalAlignment = VerticalAlignment.Center };
        _grid = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*,Auto"), ColumnSpacing = 12, RowSpacing = 4 };
        Grid.SetColumn(texts, 1);
        Grid.SetColumn(_actionSlot, 2);
        _grid.Children.Add(_icon);
        _grid.Children.Add(texts);
        _grid.Children.Add(_actionSlot);
        Child = _grid;
    }

    public Button? ActionButton => _action;

    public string? TitleText => _titleText;

    public string? DetailText => _detailText;

    public void Update(EnvRedeployState state, int changes, string environments, int environmentCount, string? failure)
    {
        var labels = _owner.Labels;
        var (title, detail) = state switch
        {
            EnvRedeployState.Done => (labels.DoneTitle(environments), labels.DoneDetail),
            EnvRedeployState.Failed => (labels.FailedTitle, failure ?? labels.FailedDetail),
            _ => (labels.BannerTitle(changes), labels.BannerDetail(environments, environmentCount)),
        };
        if (title != _titleText)
        {
            _titleText = title;
            var block = EnvUi.Text(title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, weight: FontWeight.Medium, lineHeight: 19.6);
            block.TextWrapping = TextWrapping.Wrap;
            _title.Content = block;
        }
        if (detail != _detailText)
        {
            _detailText = detail;
            var block = EnvUi.Text(detail, ArcDesignTokenKind.TextSecondary);
            block.TextWrapping = TextWrapping.Wrap;
            _detail.Content = block;
        }
        AutomationProperties.SetName(this, title + ". " + detail);
        var previous = _state;
        _state = state;
        var iconKind = state switch
        {
            EnvRedeployState.Done => 1,
            EnvRedeployState.Failed => 2,
            _ => 0,
        };
        var previousKind = previous switch
        {
            EnvRedeployState.Done => 1,
            EnvRedeployState.Failed => 2,
            (EnvRedeployState)(-1) => -1,
            _ => 0,
        };
        if (iconKind != previousKind)
        {
            _icon.Content = iconKind switch
            {
                1 => EnvUi.Glyph(ArcIcons.Check, 16, ArcDesignTokenKind.Success),
                2 => EnvUi.Glyph(ArcIcons.CircleAlert, 16, ArcDesignTokenKind.Danger),
                _ => EnvUi.Glyph(ArcIcons.Info, 16, ArcDesignTokenKind.Accent),
            };
            if (_icon.Content is Control glyph)
            {
                glyph.HorizontalAlignment = HorizontalAlignment.Center;
                glyph.VerticalAlignment = VerticalAlignment.Center;
            }
        }
        if (state == EnvRedeployState.Done)
        {
            if (_action is not null && _action.IsKeyboardFocusWithin)
            {
                _owner.FocusSearch();
            }
            _action = null;
            _actionSlot.Content = null;
        }
        else
        {
            if (_action is null)
            {
                _action = new Button { Variant = ButtonVariant.Secondary };
                _action.Click += async (_, _) => await _owner.RedeployAsync();
                _actionSlot.Content = _action;
            }
            var text = state == EnvRedeployState.Failed ? labels.RetryButton : labels.RedeployButton;
            if (_action.Content is not StackPanel { Children: [_, TextBlock label] } || label.Text != text)
            {
                _action.Content = EnvUi.IconLabel(ArcIcons.RefreshCw, text);
            }
            AutomationProperties.SetName(_action, text);
            _action.IsLoading = state == EnvRedeployState.Running;
            _action.HorizontalAlignment = _narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        }
        UpdateBrushes();
    }

    // Posted: the button leaves its loading state (and becomes focusable again) in the same pass.
    public void FocusAction()
    {
        // Redeploy clicked with the mouse: the retry takes focus without the keyboard ring.
        var method = BlockFocusModality.For(this);
        Dispatcher.UIThread.Post(() => _action?.Focus(method), DispatcherPriority.Loaded);
    }

    public void SetNarrow(bool narrow)
    {
        _narrow = narrow;
        Margin = new Thickness(narrow ? 16 : 24, 0, narrow ? 16 : 24, 16);
        // Below 560 the button takes a second line across the banner.
        Grid.SetRow(_actionSlot, narrow ? 1 : 0);
        Grid.SetColumn(_actionSlot, narrow ? 0 : 2);
        Grid.SetColumnSpan(_actionSlot, narrow ? 3 : 1);
        _grid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
        _actionSlot.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _actionSlot.HorizontalContentAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _actionSlot.Margin = narrow ? new Thickness(0, 8, 0, 0) : default;
        if (_action is not null)
        {
            _action.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        UpdateBrushes();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateBrushes();

    private void UpdateBrushes()
    {
        if (VisualRoot is null)
        {
            return;
        }
        var palette = EnvPalette.For(this);
        (Background, BorderBrush) = _state switch
        {
            EnvRedeployState.Done => (palette.SuccessFill, palette.SuccessEdge),
            EnvRedeployState.Failed => (palette.DangerFill, palette.DangerEdge),
            _ => (palette.AccentFill, palette.AccentEdge),
        };
    }
}

/// <summary>One key of the compare view (kept by key so its row survives recomputation).</summary>
internal sealed class EnvCompareEntry(string key)
{
    public string Key { get; } = key;

    public EnvCompareRow Row { get; set; }

    public override string ToString() => Key;
}

/// <summary>The compare view: the two selects and the swap, the counts and Show matching, the table and its empty state.</summary>
internal sealed class EnvComparePane : StackPanel
{
    private readonly EnvVariables _owner;
    private readonly EnvSplitPanel _controls;
    private readonly Grid _pickers;
    private readonly Select _left;
    private readonly Select _right;
    private readonly Button _swap;
    private readonly StackPanel _status;
    private readonly WrapPanel _counts;
    private readonly TextBlock _missingText;
    private readonly TextBlock _differentText;
    private readonly TextBlock _matchingText;
    private readonly Switch _showMatching;
    private readonly Border _headFrame;
    private readonly Grid _head;
    private readonly TextBlock _headLeft;
    private readonly TextBlock _headRight;
    private readonly DeveloperListPanel _list;
    private readonly EnvEmptyState _empty;
    private readonly Dictionary<string, EnvCompareEntry> _entries = new(StringComparer.Ordinal);
    private bool _syncing;
    private bool _compact;
    private bool _narrow;

    public EnvComparePane(EnvVariables owner)
    {
        _owner = owner;
        var labels = owner.Labels;
        _left = new Select { Label = labels.CompareLeftLabel };
        _right = new Select { Label = labels.CompareRightLabel };
        _left.SelectionChanged += (_, _) => OnPicked(_left, CompareLeftSide: true);
        _right.SelectionChanged += (_, _) => OnPicked(_right, CompareLeftSide: false);
        _swap = new Button
        {
            Variant = ButtonVariant.Secondary,
            Content = EnvUi.Glyph(EnvUi.ArrowLeftRight, 16, ArcDesignTokenKind.TextSecondary),
            Width = 44,
            Padding = default,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        AutomationProperties.SetName(_swap, labels.SwapName);
        _swap.Click += (_, _) => Swap();
        _pickers = new Grid { ColumnSpacing = 8, ColumnDefinitions = new ColumnDefinitions("230,44,230") };
        Grid.SetColumn(_swap, 1);
        Grid.SetColumn(_right, 2);
        _pickers.Children.Add(_left);
        _pickers.Children.Add(_swap);
        _pickers.Children.Add(_right);

        _missingText = EnvUi.Text(null, ArcDesignTokenKind.TextSecondary);
        _differentText = EnvUi.Text(null, ArcDesignTokenKind.TextSecondary);
        _matchingText = EnvUi.Text(null, ArcDesignTokenKind.TextSecondary);
        _counts = new WrapPanel { ItemSpacing = 16, LineSpacing = 4, VerticalAlignment = VerticalAlignment.Center };
        _counts.Children.Add(Count(ArcDesignTokenKind.Warning, _missingText));
        _counts.Children.Add(Count(ArcDesignTokenKind.Accent, _differentText));
        _counts.Children.Add(Count(ArcDesignTokenKind.BorderStrong, _matchingText));
        AutomationProperties.SetLiveSetting(_counts, AutomationLiveSetting.Polite);
        _showMatching = new Switch { Content = labels.ShowMatching, VerticalAlignment = VerticalAlignment.Center };
        _showMatching.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing)
            {
                owner.SetCurrentValue(EnvVariables.ShowMatchingProperty, _showMatching.IsChecked == true);
            }
        };
        _status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
        _status.Children.Add(_counts);
        _status.Children.Add(_showMatching);
        _controls = new EnvSplitPanel { ColumnGap = 24, RowGap = 16, LineAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 4, 24, 16) };
        _controls.Children.Add(_pickers);
        _controls.Children.Add(_status);

        _head = new Grid { ColumnDefinitions = new ColumnDefinitions("1.1*,1*,1*,1.5*"), ColumnSpacing = 12, Margin = new Thickness(24, 12, 24, 10) };
        _headLeft = EnvUi.Text(null, ArcDesignTokenKind.TextSecondary);
        _headRight = EnvUi.Text(null, ArcDesignTokenKind.TextSecondary);
        var headKey = EnvUi.Text(labels.KeyColumn, ArcDesignTokenKind.TextSecondary);
        var headResult = EnvUi.Text(labels.ResultColumn, ArcDesignTokenKind.TextSecondary);
        Grid.SetColumn(_headLeft, 1);
        Grid.SetColumn(_headRight, 2);
        Grid.SetColumn(headResult, 3);
        _head.Children.Add(headKey);
        _head.Children.Add(_headLeft);
        _head.Children.Add(_headRight);
        _head.Children.Add(headResult);
        AutomationProperties.SetAccessibilityView(_head, AccessibilityView.Raw);
        _headFrame = EnvUi.Edge(new Border { Child = _head }, ArcDesignTokenKind.Border, new Thickness(0, 1, 0, 1));

        _list = new DeveloperListPanel
        {
            Motion = DeveloperListMotion.Disclosure,
            ItemTemplate = new FuncDataTemplate<EnvCompareEntry>((entry, _) =>
                entry is null ? new Panel() : new EnvCompareRowView(owner, this, entry, _compact, _narrow)),
        };
        _empty = new EnvEmptyState { IsVisible = false };
        Children.Add(_controls);
        Children.Add(_headFrame);
        Children.Add(_list);
        Children.Add(_empty);
    }

    public DeveloperListPanel List => _list;

    public Select LeftSelect => _left;

    public Select RightSelect => _right;

    public Button SwapButton => _swap;

    public Switch ShowMatchingSwitch => _showMatching;

    public EnvEmptyState Empty => _empty;

    public string CountsText => $"{_missingText.Text} · {_differentText.Text} · {_matchingText.Text}";

    public IEnumerable<EnvCompareRowView> Rows => _list.LiveItems.Select(i => _list.ContainerFor(i)).OfType<EnvCompareRowView>();

    private static Control Count(ArcDesignTokenKind color, TextBlock text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        panel.Children.Add(EnvUi.Dot(color));
        panel.Children.Add(text);
        return panel;
    }

    private void OnPicked(Select select, bool CompareLeftSide)
    {
        if (_syncing || select.SelectedItem is not SelectOption { Value: string key })
        {
            return;
        }
        var (left, right) = _owner.CompareSides;
        if (CompareLeftSide)
        {
            _owner.SetCurrentValue(EnvVariables.CompareLeftProperty, key);
            if (key == right)
            {
                _owner.SetCurrentValue(EnvVariables.CompareRightProperty, left);
            }
        }
        else
        {
            _owner.SetCurrentValue(EnvVariables.CompareRightProperty, key);
            if (key == left)
            {
                _owner.SetCurrentValue(EnvVariables.CompareLeftProperty, right);
            }
        }
    }

    public void Swap()
    {
        var (left, right) = _owner.CompareSides;
        _owner.SetCurrentValue(EnvVariables.CompareLeftProperty, right);
        _owner.SetCurrentValue(EnvVariables.CompareRightProperty, left);
    }

    private void SyncPickers(string left, string right)
    {
        _syncing = true;
        var envs = _owner.EffectiveEnvironments;
        _left.ItemsSource = envs.Select(e => new SelectOption(e.Key, _owner.EnvironmentName(e.Key), e.Key == right)).ToList();
        _right.ItemsSource = envs.Select(e => new SelectOption(e.Key, _owner.EnvironmentName(e.Key), e.Key == left)).ToList();
        _left.SelectedIndex = IndexOf(envs, left);
        _right.SelectedIndex = IndexOf(envs, right);
        _showMatching.IsChecked = _owner.ShowMatching;
        _syncing = false;
    }

    private static int IndexOf(IReadOnlyList<EnvEnvironment> envs, string key)
    {
        for (var i = 0; i < envs.Count; i++)
        {
            if (envs[i].Key == key)
            {
                return i;
            }
        }
        return -1;
    }

    private (string Left, string Right) _synced;

    public void Update(bool animate)
    {
        var (left, right) = _owner.CompareSides;
        if (_synced != (left, right) || _left.ItemsSource is null || _showMatching.IsChecked != _owner.ShowMatching)
        {
            var sidesChanged = _synced != (left, right);
            _synced = (left, right);
            SyncPickers(left, right);
            // Arc replaces the table in place on a swap: no row motion.
            if (sidesChanged)
            {
                animate = false;
            }
        }
        var labels = _owner.Labels;
        var leftName = _owner.EnvironmentName(left);
        var rightName = _owner.EnvironmentName(right);
        _headLeft.Text = leftName;
        _headRight.Text = rightName;
        AutomationProperties.SetName(_list, labels.CompareListName(leftName, rightName));
        var rows = EnvVariableRules.Compare(_owner.SortedVariables, left, right, _owner.SearchText, _owner.ShowMatching);
        var (missing, different, matching) = EnvVariableRules.CompareCounts(_owner.SortedVariables, left, right, _owner.SearchText);
        _missingText.Text = labels.MissingCount(missing);
        _differentText.Text = labels.DifferentCount(different);
        _matchingText.Text = labels.MatchingCount(matching);
        AutomationProperties.SetName(_counts, CountsText);
        var items = new List<EnvCompareEntry>(rows.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!_entries.TryGetValue(row.Key, out var entry))
            {
                entry = new EnvCompareEntry(row.Key);
                _entries[row.Key] = entry;
            }
            entry.Row = row;
            items.Add(entry);
            seen.Add(row.Key);
        }
        foreach (var key in _entries.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            if (_list.ContainerFor(_entries[key]) is null)
            {
                _entries.Remove(key);
            }
        }
        var empty = rows.Count == 0;
        if (empty)
        {
            if (!string.IsNullOrWhiteSpace(_owner.SearchText))
            {
                _empty.Action = _owner.ClearSearch;
                _empty.Set(labels.NoMatchTitle(_owner.SearchText.Trim()), labels.NoMatchDetail, labels.ClearSearchButton, _narrow);
            }
            else
            {
                _empty.Action = () =>
                {
                    _owner.SetCurrentValue(EnvVariables.ShowMatchingProperty, true);
                    _showMatching.Focus(BlockFocusModality.For(this));
                };
                _empty.Set(labels.InSyncTitle(leftName, rightName), labels.InSyncDetail, labels.ShowMatchingButton, _narrow);
            }
        }
        _empty.IsVisible = empty;
        var wasEmpty = !_list.IsVisible;
        _list.IsVisible = !empty;
        _headFrame.IsVisible = !empty || !_compact;
        if (!animate || wasEmpty || empty)
        {
            _list.Change(DeveloperListMotion.Instant, () => _list.Items = items);
        }
        else
        {
            _list.Items = items;
        }
        var index = 0;
        foreach (var view in Rows)
        {
            view.ShowTopLine = index++ > 0;
            view.Refresh();
        }
    }

    /// <summary>Copies the row's value across and moves the focus to the next Copy button (or Show matching).</summary>
    internal void CopyAcross(EnvCompareEntry entry)
    {
        var (left, right) = _owner.CompareSides;
        var row = entry.Row;
        var (from, to) = row.Result == EnvCompareResult.MissingLeft ? (right, left) : (left, right);
        var live = _list.LiveItems.OfType<EnvCompareEntry>().ToList();
        var at = live.IndexOf(entry);
        var next = live.Skip(at + 1).Concat(live.Take(Math.Max(0, at)))
            .FirstOrDefault(e => e.Row.Result is EnvCompareResult.MissingLeft or EnvCompareResult.MissingRight);
        if (_owner.CopyAcross(row.Key, from, to) is null)
        {
            return;
        }
        Update(animate: true);
        // Copy clicked with the mouse moves focus on without the keyboard ring.
        var method = BlockFocusModality.For(this);
        if (next is not null && _list.ContainerFor(next) is EnvCompareRowView view && view.CopyButton is { } button)
        {
            button.Focus(method);
        }
        else
        {
            _showMatching.Focus(method);
        }
    }

    public void SetLayout(bool compact, bool narrow)
    {
        _compact = compact;
        _narrow = narrow;
        var inset = narrow ? 16 : 24;
        _controls.Margin = new Thickness(inset, 4, inset, 16);
        _controls.IsStacked = narrow;
        _pickers.ColumnDefinitions = new ColumnDefinitions(narrow ? "*,44,*" : "230,44,230");
        _head.IsVisible = !compact;
        _headFrame.BorderThickness = new Thickness(0, 1, 0, compact ? 0 : 1);
        _head.Margin = new Thickness(inset, 12, inset, 10);
        foreach (var view in Rows)
        {
            view.SetLayout(compact, narrow);
        }
    }
}

/// <summary>One key of the compare view: the key, a fingerprint cell per side, the result and a Copy button when a side lacks it.</summary>
internal sealed class EnvCompareRowView : Border
{
    private readonly EnvVariables _owner;
    private readonly EnvComparePane _pane;
    private readonly EnvCompareEntry _entry;
    private bool _compact;
    private bool _narrow;

    public EnvCompareRowView(EnvVariables owner, EnvComparePane pane, EnvCompareEntry entry, bool compact, bool narrow)
    {
        _owner = owner;
        _pane = pane;
        _entry = entry;
        _compact = compact;
        _narrow = narrow;
        EnvUi.Edge(this, ArcDesignTokenKind.BorderSubtle, default);
        Refresh();
    }

    public EnvCompareEntry Entry => _entry;

    public Button? CopyButton { get; private set; }

    public bool ShowTopLine
    {
        set => BorderThickness = new Thickness(0, value ? 1 : 0, 0, 0);
    }

    public void SetLayout(bool compact, bool narrow)
    {
        _compact = compact;
        _narrow = narrow;
        Refresh();
    }

    public void Refresh()
    {
        var labels = _owner.Labels;
        var row = _entry.Row;
        var (leftKey, rightKey) = _owner.CompareSides;
        var leftName = _owner.EnvironmentName(leftKey);
        var rightName = _owner.EnvironmentName(rightKey);
        var key = EnvUi.Text(row.Key, ArcDesignTokenKind.Foreground, mono: true);
        key.TextTrimming = TextTrimming.CharacterEllipsis;
        key.VerticalAlignment = VerticalAlignment.Center;
        var leftCell = Cell(row.Left, leftName);
        var rightCell = Cell(row.Right, rightName);
        var (icon, color, text, textColor) = row.Result switch
        {
            EnvCompareResult.Same => (ArcIcons.Check, ArcDesignTokenKind.TextMuted, labels.SameResult, ArcDesignTokenKind.TextSecondary),
            EnvCompareResult.Different => (EnvUi.ArrowLeftRight, ArcDesignTokenKind.Accent, labels.DifferentResult, ArcDesignTokenKind.Foreground),
            EnvCompareResult.MissingLeft => (ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, labels.MissingResult(leftName), ArcDesignTokenKind.Foreground),
            _ => (ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, labels.MissingResult(rightName), ArcDesignTokenKind.Foreground),
        };
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        status.Children.Add(EnvUi.Glyph(icon, 14, color));
        status.Children.Add(EnvUi.Text(text, textColor));
        CopyButton = null;
        if (!_owner.IsReadOnly && row.Result is EnvCompareResult.MissingLeft or EnvCompareResult.MissingRight)
        {
            var target = row.Result == EnvCompareResult.MissingLeft ? leftName : rightName;
            CopyButton = EnvUi.Button(ButtonVariant.Secondary, labels.CopyTo(target));
            CopyButton.Click += (_, _) => _pane.CopyAcross(_entry);
        }
        AutomationProperties.SetName(this, $"{row.Key}: {text}");
        if (!_compact)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1.1*,1*,1*,1.5*"),
                ColumnSpacing = 12,
                MinHeight = 44,
                Margin = new Thickness(_narrow ? 16 : 24, 10),
            };
            var result = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            result.Children.Add(status);
            if (CopyButton is not null)
            {
                Grid.SetColumn(CopyButton, 1);
                CopyButton.VerticalAlignment = VerticalAlignment.Center;
                result.Children.Add(CopyButton);
            }
            Grid.SetColumn(leftCell, 1);
            Grid.SetColumn(rightCell, 2);
            Grid.SetColumn(result, 3);
            grid.Children.Add(key);
            grid.Children.Add(leftCell);
            grid.Children.Add(rightCell);
            grid.Children.Add(result);
            Child = grid;
            return;
        }
        // Stacked card: the key, both sides with their names, then the result (and the Copy button, across when narrow).
        var stack = new StackPanel { Spacing = 10, Margin = new Thickness(_narrow ? 16 : 24, 12) };
        stack.Children.Add(key);
        var sides = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
        var leftBox = Labelled(leftName, leftCell);
        var rightBox = Labelled(rightName, rightCell);
        Grid.SetColumn(rightBox, 1);
        sides.Children.Add(leftBox);
        sides.Children.Add(rightBox);
        stack.Children.Add(sides);
        if (CopyButton is not null && !_narrow)
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            Grid.SetColumn(CopyButton, 1);
            line.Children.Add(status);
            line.Children.Add(CopyButton);
            stack.Children.Add(line);
        }
        else
        {
            stack.Children.Add(status);
            if (CopyButton is not null)
            {
                CopyButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                stack.Children.Add(CopyButton);
            }
        }
        Child = stack;
    }

    private static Control Labelled(string name, Control cell)
    {
        var box = new StackPanel { Spacing = 4 };
        box.Children.Add(EnvUi.Text(name, ArcDesignTokenKind.TextMuted));
        box.Children.Add(cell);
        return box;
    }

    private Control Cell(EnvVariable? variable, string environment)
    {
        var labels = _owner.Labels;
        if (variable is null)
        {
            var notSet = EnvUi.Text(labels.NotSet, ArcDesignTokenKind.TextMuted);
            notSet.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(notSet, $"{environment}: {labels.NotSet}");
            return notSet;
        }
        var fingerprint = variable.EffectiveFingerprint ?? string.Empty;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var dots = EnvUi.Text(DeveloperText.Bullets(4), ArcDesignTokenKind.TextMuted);
        dots.LetterSpacing = 0.96;
        AutomationProperties.SetAccessibilityView(dots, AccessibilityView.Raw);
        panel.Children.Add(dots);
        panel.Children.Add(EnvUi.Text(fingerprint, ArcDesignTokenKind.Accent, mono: true));
        if (variable.IsSensitive)
        {
            panel.Children.Add(EnvUi.Glyph(ArcIcons.Lock, 14, ArcDesignTokenKind.TextMuted));
        }
        AutomationProperties.SetName(panel, $"{environment} {labels.FingerprintName} {fingerprint}"
            + (variable.IsSensitive ? ", " + labels.Sensitive : string.Empty));
        return panel;
    }
}
