using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The button of a connected app: a check, "Connected" and a chevron that turns over while the access details are open.
/// Screen readers get its expanded state (Arc's <c>aria-expanded</c>).
/// </summary>
internal sealed class ConnectorSetupConnectedButton : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(ConnectorSetupConnectedButton);
}

/// <summary>
/// One app of a <see cref="ConnectorSetup"/> (an <c>li</c>): its mark, its name over one status line, one action, the
/// progress hairline along its bottom edge and, for a connected app, the access details with the inline disconnect
/// confirmation. The status line and the action each swap in a single cell (old out in 100ms, new in over 160ms rising 2px),
/// so the 60px row never reflows. The row only shows state; the card runs the flows through the callbacks.
/// </summary>
[TemplatePart(MarkPart, typeof(ConnectorMark))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(ActionPart, typeof(ArcContentSwap))]
[TemplatePart(HairlinePart, typeof(ConnectorSetupHairline))]
[TemplatePart(DetailsPart, typeof(AiHeightBox))]
[TemplatePart(ScopesHeadingPart, typeof(TextBlock))]
[TemplatePart(ScopesPart, typeof(StackPanel))]
[TemplatePart(ConfirmPart, typeof(ArcContentSwap))]
[PseudoClasses(BusyPseudoClass, ConnectedPseudoClass, FailedPseudoClass)]
internal sealed class ConnectorSetupRow : TemplatedControl
{
    internal const string MarkPart = "PART_Mark";
    internal const string NamePart = "PART_Name";
    internal const string StatusPart = "PART_Status";
    internal const string ActionPart = "PART_Action";
    internal const string HairlinePart = "PART_Hairline";
    internal const string DetailsPart = "PART_Details";
    internal const string ScopesHeadingPart = "PART_ScopesHeading";
    internal const string ScopesPart = "PART_Scopes";
    internal const string ConfirmPart = "PART_Confirm";

    internal const string BusyPseudoClass = ":busy";
    internal const string ConnectedPseudoClass = ":connected";
    internal const string FailedPseudoClass = ":failed";

    private ConnectorMark? _mark;
    private TextBlock? _name;
    private ArcContentSwap? _status;
    private ArcContentSwap? _action;
    private ConnectorSetupHairline? _hairline;
    private AiHeightBox? _details;
    private TextBlock? _scopesHeading;
    private StackPanel? _scopes;
    private ArcContentSwap? _confirm;

    private string? _statusKey;
    private string? _actionKey;
    private string? _confirmKey;
    private TextBlock? _countText;
    private TextBlock? _countReserve;
    private TextBlock? _nounText;
    private ConnectorSetupConnectedButton? _connectedButton;
    private Control? _actionControl;
    private Control? _confirmControl;
    private bool _confirmFocusPending;

    public ConnectorSetupRow(ConnectorSetup owner, ConnectorApp app)
    {
        Owner = owner;
        App = app;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
    }

    /// <summary>The card the row belongs to.</summary>
    public ConnectorSetup Owner { get; }

    /// <summary>The app.</summary>
    public ConnectorApp App { get; }

    /// <summary>Where the app is.</summary>
    public ConnectorPhase Phase { get; private set; }

    /// <summary>The items the first sync has read.</summary>
    public long Synced { get; private set; }

    /// <summary>The total the sync reads, when known.</summary>
    public long? Total { get; private set; }

    /// <summary>The account of the connection.</summary>
    public string? Account { get; private set; }

    /// <summary>The granted scopes of the connection.</summary>
    public IReadOnlyList<string> Scopes { get; private set; } = [];

    /// <summary>The reason of a failed connect.</summary>
    public string? Error { get; private set; }

    /// <summary>Whether the access details are open.</summary>
    public bool IsExpanded { get; private set; }

    /// <summary>Whether the disconnect confirmation shows.</summary>
    public bool IsConfirming { get; private set; }

    /// <summary>The running connect or disconnect; cancelled on Cancel or when the card leaves.</summary>
    internal CancellationTokenSource? Cts { get; set; }

    /// <summary>Bumped by every new flow, so a late answer of an older one is ignored.</summary>
    internal int Run { get; set; }

    /// <summary>The status line as text (tests, announcements).</summary>
    internal string StatusText { get; private set; } = string.Empty;

    /// <summary>The control the action cell shows now.</summary>
    internal Control? ActionControl => _actionControl;

    /// <summary>The control the confirmation cell shows now.</summary>
    internal Control? ConfirmControl => _confirmControl;

    internal ConnectorSetupHairline? Hairline => _hairline;

    internal AiHeightBox? Details => _details;

    internal StackPanel? ScopesPanel => _scopes;

    internal TextBlock? ScopesHeading => _scopesHeading;

    /// <summary>Whether a connect step runs.</summary>
    public bool IsBusy => Phase is ConnectorPhase.Redirecting or ConnectorPhase.Authorizing or ConnectorPhase.Syncing;

    private ConnectorSetupLabels Labels => Owner.Labels;

    // State ---------------------------------------------------------------------------------------------------------------

    /// <summary>Shows the app as not connected.</summary>
    public void SetIdle()
    {
        Phase = ConnectorPhase.Idle;
        Error = null;
        Synced = 0;
        Total = null;
        IsExpanded = false;
        IsConfirming = false;
        _hairline?.Finish(complete: false);
        Refresh();
    }

    /// <summary>Shows the app as connected (a connection made before, or one that just finished).</summary>
    public void SetConnected(string? account, IReadOnlyList<string> scopes, long? synced)
    {
        var wasBusy = IsBusy;
        Phase = ConnectorPhase.Connected;
        Error = null;
        Account = account;
        Scopes = scopes;
        Synced = synced ?? 0;
        Total = synced;
        IsConfirming = false;
        if (wasBusy)
        {
            _hairline?.Finish(complete: true);
        }
        Refresh();
    }

    /// <summary>Starts a connect: the first step.</summary>
    public void BeginConnect()
    {
        Phase = ConnectorPhase.Redirecting;
        Error = null;
        Synced = 0;
        Total = App.SyncCount;
        IsExpanded = false;
        IsConfirming = false;
        _hairline?.MoveTo(0.12, tracking: false);
        Refresh();
    }

    /// <summary>A step the connect handler reported.</summary>
    public void Report(ConnectorPhase phase, long? synced, long? total)
    {
        if (!IsBusy)
        {
            return;
        }
        if (phase == ConnectorPhase.Authorizing && Phase == ConnectorPhase.Redirecting)
        {
            Phase = ConnectorPhase.Authorizing;
            _hairline?.MoveTo(0.3, tracking: false);
            Refresh();
            return;
        }
        if (phase == ConnectorPhase.Syncing)
        {
            var entering = Phase != ConnectorPhase.Syncing;
            Phase = ConnectorPhase.Syncing;
            if (total is { } t && t > 0)
            {
                Total = t;
            }
            Synced = synced ?? Synced;
            _hairline?.MoveTo(SyncShare(Synced, Total), tracking: !entering);
            Refresh();
        }
    }

    /// <summary>The share of the line while syncing: 40% for the handshake, the rest for the count.</summary>
    internal static double SyncShare(long synced, long? total)
    {
        double part = total is { } t && t > 0
            ? Math.Clamp((double)synced / t, 0, 1)
            : 1 - Math.Exp(-synced / 400.0);
        return 0.4 + 0.6 * part;
    }

    /// <summary>Shows a failed connect with its reason and Retry.</summary>
    public void SetFailed(string reason)
    {
        Phase = ConnectorPhase.Failed;
        Error = reason;
        _hairline?.Finish(complete: false);
        Refresh();
    }

    /// <summary>Shows that access is being revoked.</summary>
    public void SetDisconnecting()
    {
        Phase = ConnectorPhase.Disconnecting;
        Refresh();
    }

    /// <summary>Opens or closes the access details.</summary>
    public void SetExpanded(bool expanded)
    {
        if (expanded && Phase is not (ConnectorPhase.Connected or ConnectorPhase.Disconnecting))
        {
            expanded = false;
        }
        IsExpanded = expanded;
        if (!expanded)
        {
            IsConfirming = false;
        }
        Refresh();
    }

    /// <summary>Shows or hides the disconnect confirmation; <paramref name="focus"/> moves focus into it (a keyboard press).</summary>
    public void SetConfirming(bool confirming, bool focus)
    {
        IsConfirming = confirming && Phase == ConnectorPhase.Connected && IsExpanded;
        _confirmFocusPending = focus;
        Refresh();
    }

    // Template ------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mark = e.NameScope.Find<ConnectorMark>(MarkPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _action = e.NameScope.Find<ArcContentSwap>(ActionPart);
        _hairline = e.NameScope.Find<ConnectorSetupHairline>(HairlinePart);
        _details = e.NameScope.Find<AiHeightBox>(DetailsPart);
        if (_details is not null)
        {
            _details.HeightSpring = AiHeightBox.PanelSpring;
        }
        _scopesHeading = e.NameScope.Find<TextBlock>(ScopesHeadingPart);
        _scopes = e.NameScope.Find<StackPanel>(ScopesPart);
        _confirm = e.NameScope.Find<ArcContentSwap>(ConfirmPart);
        _statusKey = null;
        _actionKey = null;
        _confirmKey = null;
        _connectedButton = null;
        if (_hairline is not null && IsBusy)
        {
            _hairline.MoveTo(Phase == ConnectorPhase.Syncing ? SyncShare(Synced, Total) : 0.12, tracking: false);
        }
        Refresh();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsConfirming)
        {
            SetConfirming(false, focus: false);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>Brings every part in line with the state; a cell that already shows the right thing is left alone.</summary>
    public void Refresh()
    {
        PseudoClasses.Set(BusyPseudoClass, IsBusy);
        PseudoClasses.Set(ConnectedPseudoClass, Phase is ConnectorPhase.Connected or ConnectorPhase.Disconnecting);
        PseudoClasses.Set(FailedPseudoClass, Phase == ConnectorPhase.Failed);
        AutomationProperties.SetName(this, App.Name);
        if (_mark is not null)
        {
            _mark.Kind = App.Kind;
            _mark.Glyph = App.Glyph;
            _mark.Monogram = App.Name;
        }
        if (_name is not null && _name.Text != App.Name)
        {
            _name.Text = App.Name;
        }
        RefreshStatus();
        RefreshAction();
        RefreshDetails();
    }

    private void RefreshStatus()
    {
        var labels = Labels;
        string key;
        string text;
        switch (Phase)
        {
            case ConnectorPhase.Redirecting:
                text = labels.Redirecting(string.IsNullOrEmpty(App.Host) ? App.Name : App.Host!);
                key = "redirect";
                break;
            case ConnectorPhase.Authorizing:
                text = labels.Authorizing(App.Name);
                key = "auth";
                break;
            case ConnectorPhase.Syncing:
                text = labels.Syncing + " " + labels.Count(Synced) + " " + Noun;
                key = "sync";
                break;
            case ConnectorPhase.Connected:
                text = ConnectedDetail();
                key = "connected:" + text;
                break;
            case ConnectorPhase.Disconnecting:
                text = labels.Disconnecting;
                key = "disconnecting";
                break;
            case ConnectorPhase.Failed:
                text = Error ?? labels.ConnectFailed;
                key = "failed:" + text;
                break;
            default:
                text = App.Description ?? string.Empty;
                key = "idle:" + text;
                break;
        }
        StatusText = text;
        if (_status is null)
        {
            return;
        }
        if (key == _statusKey)
        {
            if (Phase == ConnectorPhase.Syncing)
            {
                UpdateCount();
            }
            return;
        }
        _statusKey = key;
        _status.Content = Phase switch
        {
            ConnectorPhase.Syncing => BuildSyncStatus(),
            ConnectorPhase.Failed => BuildErrorStatus(text),
            _ => StatusLine(text),
        };
    }

    /// <summary>"acme.chat · 2,480 messages synced", or the parts that are known.</summary>
    internal string ConnectedDetail()
    {
        var labels = Labels;
        var synced = Total is { } total && total > 0 ? labels.Synced(labels.Count(total), Noun) : null;
        if (!string.IsNullOrEmpty(Account) && synced is not null)
        {
            return Account + " · " + synced;
        }
        return !string.IsNullOrEmpty(Account) ? Account! : synced ?? labels.ConnectedStatus;
    }

    private string Noun => string.IsNullOrEmpty(App.SyncNoun) ? Labels.DefaultNoun : App.SyncNoun!;

    private static TextBlock StatusLine(string text) => new()
    {
        Text = text,
        Classes = { "connector-status" },
        TextTrimming = TextTrimming.CharacterEllipsis,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private Control BuildErrorStatus(string text)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 4 };
        var glyph = new ArcGlyph { Data = ArcIcons.CircleAlert, Classes = { "connector-error-glyph" }, VerticalAlignment = VerticalAlignment.Center };
        var line = StatusLine(text);
        line.Classes.Add("connector-error");
        Grid.SetColumn(line, 1);
        grid.Children.Add(glyph);
        grid.Children.Add(line);
        return grid;
    }

    /// <summary>"Syncing", the count right-aligned in a cell as wide as its final value (tabular figures), and the noun.</summary>
    private Control BuildSyncStatus()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        var word = StatusLine(Labels.Syncing + " ");
        var cell = new Panel { HorizontalAlignment = HorizontalAlignment.Left };
        _countReserve = StatusLine(string.Empty);
        _countReserve.Classes.Add("connector-count");
        _countReserve.Opacity = 0;
        AutomationProperties.SetAccessibilityView(_countReserve, AccessibilityView.Raw);
        _countText = StatusLine(string.Empty);
        _countText.Classes.Add("connector-count");
        _countText.TextTrimming = TextTrimming.None;
        // Right-aligned in the reserved width, so the digits grow leftward like a counter and the noun never moves.
        _countText.HorizontalAlignment = HorizontalAlignment.Right;
        cell.Children.Add(_countReserve);
        cell.Children.Add(_countText);
        _nounText = StatusLine(string.Empty);
        Grid.SetColumn(cell, 1);
        Grid.SetColumn(_nounText, 2);
        grid.Children.Add(word);
        grid.Children.Add(cell);
        grid.Children.Add(_nounText);
        UpdateCount();
        return grid;
    }

    private void UpdateCount()
    {
        var labels = Labels;
        if (_countText is not null)
        {
            var count = labels.Count(Synced);
            if (_countText.Text != count)
            {
                _countText.Text = count;
            }
        }
        if (_countReserve is not null)
        {
            var reserve = labels.Count(Math.Max(Synced, Total ?? 0));
            if (_countReserve.Text != reserve)
            {
                _countReserve.Text = reserve;
            }
        }
        if (_nounText is not null)
        {
            var noun = " " + Noun;
            if (_nounText.Text != noun)
            {
                _nounText.Text = noun;
            }
        }
    }

    private void RefreshAction()
    {
        var key = Phase switch
        {
            ConnectorPhase.Redirecting or ConnectorPhase.Authorizing or ConnectorPhase.Syncing => "cancel",
            ConnectorPhase.Failed => "retry",
            ConnectorPhase.Connected or ConnectorPhase.Disconnecting => "connected",
            _ => "connect",
        };
        var labels = Labels;
        if (key != _actionKey && _action is not null)
        {
            var hadFocus = _actionControl?.IsKeyboardFocusWithin == true;
            var method = FocusMethodOf(_actionControl);
            Retire(_actionControl);
            _actionKey = key;
            _connectedButton = null;
            Control next;
            switch (key)
            {
                case "cancel":
                    next = ActionButton(labels.Cancel, ButtonVariant.Ghost, labels.CancelApp(App.Name), () => Owner.CancelConnect(App.Id));
                    break;
                case "retry":
                    next = ActionButton(labels.Retry, ButtonVariant.Secondary, labels.RetryApp(App.Name), () => Owner.StartConnect(App.Id));
                    break;
                case "connected":
                    _connectedButton = BuildConnectedButton();
                    next = _connectedButton;
                    break;
                default:
                    next = ActionButton(labels.Connect, ButtonVariant.Secondary, labels.ConnectApp(App.Name), () => Owner.StartConnect(App.Id));
                    break;
            }
            _actionControl = next;
            _action.Content = next;
            if (hadFocus)
            {
                Dispatcher.UIThread.Post(() => next.Focus(method), DispatcherPriority.Loaded);
            }
        }
        if (_connectedButton is not null)
        {
            _connectedButton.IsExpanded = IsExpanded;
            _connectedButton.IsEnabled = Phase == ConnectorPhase.Connected;
            AutomationProperties.SetName(_connectedButton, IsExpanded ? labels.HideAccess(App.Name) : labels.ShowAccess(App.Name));
        }
    }

    private static Button ActionButton(string text, ButtonVariant variant, string name, Action click)
    {
        var button = new Button
        {
            Content = text,
            Variant = variant,
            Size = ControlSize.Small,
            Classes = { "connector-action" },
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, e) =>
        {
            e.Handled = true;
            click();
        };
        return button;
    }

    private ConnectorSetupConnectedButton BuildConnectedButton()
    {
        var button = new ConnectorSetupConnectedButton
        {
            Content = Labels.Connected,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        button.Click += (_, e) =>
        {
            e.Handled = true;
            Owner.ToggleDetails(App.Id);
        };
        return button;
    }

    /// <summary>
    /// How focus moves on with a swap: a control focused from the keyboard (it shows its ring) hands on a keyboard focus
    /// with its ring, a pressed one a quiet focus.
    /// </summary>
    private static NavigationMethod FocusMethodOf(Control? control)
    {
        var focused = control is null ? null : control.IsFocused ? control : control.GetLogicalDescendantsOf<Control>().FirstOrDefault(c => c.IsFocused);
        return focused is not null && focused.Classes.Contains(":focus-visible") ? NavigationMethod.Tab : NavigationMethod.Pointer;
    }

    /// <summary>Content that is fading out leaves input, focus and the accessibility tree at once.</summary>
    private static void Retire(Control? control)
    {
        if (control is null)
        {
            return;
        }
        control.IsHitTestVisible = false;
        control.Focusable = false;
        if (control is InputElement input)
        {
            KeyboardNavigation.SetIsTabStop(input, false);
        }
        foreach (var child in control.GetLogicalDescendantsOf<InputElement>())
        {
            child.Focusable = false;
            KeyboardNavigation.SetIsTabStop(child, false);
        }
        AutomationProperties.SetAccessibilityView(control, AccessibilityView.Raw);
    }

    private void RefreshDetails()
    {
        var labels = Labels;
        if (_scopesHeading is not null)
        {
            var heading = labels.ScopesHeading(Owner.EffectiveAssistantName);
            if (_scopesHeading.Text != heading)
            {
                _scopesHeading.Text = heading;
            }
        }
        if (_scopes is not null && !ReferenceEquals(_scopes.Tag, Scopes))
        {
            _scopes.Tag = Scopes;
            _scopes.Children.Clear();
            if (Scopes.Count == 0)
            {
                _scopes.Children.Add(new TextBlock { Text = labels.NoScopes, Classes = { "connector-scope-text" }, TextWrapping = TextWrapping.Wrap });
            }
            foreach (var scope in Scopes)
            {
                var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
                line.Children.Add(new ArcGlyph { Data = ArcIcons.Check, Classes = { "connector-scope-glyph" }, VerticalAlignment = VerticalAlignment.Top });
                var text = new TextBlock { Text = scope, Classes = { "connector-scope-text" }, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(text, 1);
                line.Children.Add(text);
                _scopes.Children.Add(line);
            }
        }
        if (_confirm is not null)
        {
            var key = IsConfirming || Phase == ConnectorPhase.Disconnecting ? "confirm" : "disconnect";
            if (key != _confirmKey)
            {
                var hadFocus = _confirmControl?.IsKeyboardFocusWithin == true;
                var method = FocusMethodOf(_confirmControl);
                Retire(_confirmControl);
                _confirmKey = key;
                _confirmControl = key == "confirm" ? BuildConfirm() : BuildDisconnect();
                _confirm.Content = _confirmControl;
                var target = _confirmControl;
                if (key == "confirm" && _confirmFocusPending)
                {
                    // A deliberate press moves focus to the safe answer; the destructive one is only ever focused by the person.
                    _confirmFocusPending = false;
                    Dispatcher.UIThread.Post(() => target.GetLogicalDescendantsOf<Button>().FirstOrDefault(b => b.Classes.Contains("connector-cancel"))?.Focus(method),
                        DispatcherPriority.Loaded);
                }
                else if (hadFocus)
                {
                    Dispatcher.UIThread.Post(() => target.Focus(method), DispatcherPriority.Loaded);
                }
            }
            if (_confirmControl is Grid grid)
            {
                var question = grid.Children.OfType<TextBlock>().FirstOrDefault();
                if (question is not null)
                {
                    question.Text = labels.ConfirmDisconnect(App.Name, Owner.EffectiveAssistantName);
                }
                foreach (var button in grid.Children.OfType<Button>())
                {
                    button.IsLoading = Phase == ConnectorPhase.Disconnecting && button.Variant == ButtonVariant.Danger;
                }
            }
        }
        if (_details is not null)
        {
            _details.IsExpanded = IsExpanded;
        }
    }

    private Control BuildDisconnect()
    {
        var button = new Button
        {
            Content = Labels.Disconnect,
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Classes = { "connector-action", "connector-disconnect" },
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        button.Click += (_, e) =>
        {
            e.Handled = true;
            SetConfirming(true, focus: true);
        };
        return button;
    }

    private Control BuildConfirm()
    {
        var labels = Labels;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6 };
        var question = new TextBlock
        {
            Text = labels.ConfirmDisconnect(App.Name, Owner.EffectiveAssistantName),
            Classes = { "connector-question" },
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var cancel = new Button
        {
            Content = labels.Cancel,
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Classes = { "connector-action", "connector-cancel" },
        };
        cancel.Click += (_, e) =>
        {
            e.Handled = true;
            SetConfirming(false, focus: false);
        };
        var confirm = new Button
        {
            Content = labels.ConfirmDisconnectButton,
            Variant = ButtonVariant.Danger,
            Size = ControlSize.Small,
            Classes = { "connector-action", "connector-confirm" },
        };
        AutomationProperties.SetName(confirm, labels.ConfirmDisconnectButton + " " + App.Name);
        confirm.Click += (_, e) =>
        {
            e.Handled = true;
            if (Phase == ConnectorPhase.Connected)
            {
                Owner.Disconnect(App.Id);
            }
        };
        Grid.SetColumn(cancel, 1);
        Grid.SetColumn(confirm, 2);
        grid.Children.Add(question);
        grid.Children.Add(cancel);
        grid.Children.Add(confirm);
        return grid;
    }
}

/// <summary>Logical tree helpers for the row's code-built cells.</summary>
internal static class ConnectorSetupTree
{
    /// <summary>Every logical descendant of <paramref name="root"/> of type <typeparamref name="T"/>.</summary>
    public static IEnumerable<T> GetLogicalDescendantsOf<T>(this Control root) where T : class
    {
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is T match)
                {
                    yield return match;
                }
                foreach (var nested in child.GetLogicalDescendantsOf<T>())
                {
                    yield return nested;
                }
            }
        }
        else if (root is ContentControl { Content: Control content })
        {
            if (content is T match)
            {
                yield return match;
            }
            foreach (var nested in content.GetLogicalDescendantsOf<T>())
            {
                yield return nested;
            }
        }
    }
}
