using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>The single-line text field of <see cref="AiPanelBox"/>: no frame of its own, 14 / 19.6, no wrapping.</summary>
internal sealed class AiPanelInput : TextBox
{
    protected override Type StyleKeyOverride => typeof(AiPanelInput);
}

/// <summary>Carries the text the person sends from <see cref="AiPanelBox"/>.</summary>
internal sealed class AiPanelSendEventArgs(RoutedEvent routedEvent, object source, string text) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The text, as typed.</summary>
    public string Text { get; } = text;
}

/// <summary>
/// The side panel's box (spec 3.8, 9.3): one line, 44 high, radius 18, a 1px Border that turns BorderStrong while the focus
/// is inside (160ms standard), padding 0 6 0 14 on Surface; the text field (14 / 19.6, placeholder in TextMuted) and the
/// 32px send button (the chat's solid one: SurfaceMuted with a muted arrow when there is nothing to send, the accent when
/// there is, the accent with a stop square while <see cref="IsBusy"/>). Enter sends; while busy Enter does nothing (the
/// text stays) and Escape stops. A line break never goes in. Every word comes from the owner.
/// </summary>
[TemplatePart(InputPart, typeof(AiPanelInput))]
[TemplatePart(SendPart, typeof(AiSendButton))]
internal sealed class AiPanelBox : TemplatedControl
{
    public const string InputPart = "PART_Input";
    public const string SendPart = "PART_Send";

    /// <summary>The text (two-way).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiPanelBox, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The placeholder.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<AiPanelBox, string?>(nameof(Placeholder));

    /// <summary>Whether an answer is being read or written: the button stops it.</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<AiPanelBox, bool>(nameof(IsBusy));

    /// <summary>Whether a question can be sent at all (there is someone to answer).</summary>
    public static readonly StyledProperty<bool> CanSendProperty =
        AvaloniaProperty.Register<AiPanelBox, bool>(nameof(CanSend), true);

    /// <summary>The accessible name of the text field.</summary>
    public static readonly StyledProperty<string?> InputNameProperty =
        AvaloniaProperty.Register<AiPanelBox, string?>(nameof(InputName));

    /// <summary>The accessible name of the send button.</summary>
    public static readonly StyledProperty<string?> SendNameProperty =
        AvaloniaProperty.Register<AiPanelBox, string?>(nameof(SendName));

    /// <summary>The accessible name of the button while busy.</summary>
    public static readonly StyledProperty<string?> StopNameProperty =
        AvaloniaProperty.Register<AiPanelBox, string?>(nameof(StopName));

    /// <summary>Raised (bubbling) when the person sends the text.</summary>
    public static readonly RoutedEvent<AiPanelSendEventArgs> SendRequestedEvent =
        RoutedEvent.Register<AiPanelBox, AiPanelSendEventArgs>(nameof(SendRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person stops the answer (the button or Escape while busy).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<AiPanelBox, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    private AiPanelInput? _input;
    private AiSendButton? _send;

    static AiPanelBox()
    {
        FocusableProperty.OverrideDefaultValue<AiPanelBox>(false);
    }

    public AiPanelBox()
    {
        // Before the text field: Enter would otherwise reach it first.
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <inheritdoc cref="CanSendProperty"/>
    public bool CanSend { get => GetValue(CanSendProperty); set => SetValue(CanSendProperty, value); }

    /// <inheritdoc cref="InputNameProperty"/>
    public string? InputName { get => GetValue(InputNameProperty); set => SetValue(InputNameProperty, value); }

    /// <inheritdoc cref="SendNameProperty"/>
    public string? SendName { get => GetValue(SendNameProperty); set => SetValue(SendNameProperty, value); }

    /// <inheritdoc cref="StopNameProperty"/>
    public string? StopName { get => GetValue(StopNameProperty); set => SetValue(StopNameProperty, value); }

    /// <inheritdoc cref="SendRequestedEvent"/>
    public event EventHandler<AiPanelSendEventArgs>? SendRequested
    {
        add => AddHandler(SendRequestedEvent, value);
        remove => RemoveHandler(SendRequestedEvent, value);
    }

    /// <inheritdoc cref="StopRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? StopRequested
    {
        add => AddHandler(StopRequestedEvent, value);
        remove => RemoveHandler(StopRequestedEvent, value);
    }

    /// <summary>The text field (once the template is applied).</summary>
    internal AiPanelInput? Input => _input;

    /// <summary>The send / stop button (once the template is applied).</summary>
    internal AiSendButton? SendButton => _send;

    /// <summary>Whether there is something to send.</summary>
    internal bool HasText => !string.IsNullOrWhiteSpace(Text);

    /// <summary>Puts the keyboard focus in the text field, the caret at the end.</summary>
    public bool FocusInput(NavigationMethod method = NavigationMethod.Pointer)
    {
        if (_input is null)
        {
            ApplyTemplate();
        }
        if (_input is null)
        {
            return false;
        }
        _input.CaretIndex = _input.Text?.Length ?? 0;
        return _input.Focus(method);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        _input = e.NameScope.Find<AiPanelInput>(InputPart);
        _send = e.NameScope.Find<AiSendButton>(SendPart);
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        RefreshNames();
        RefreshSend();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IsBusyProperty || change.Property == CanSendProperty)
        {
            RefreshSend();
        }
        else if (change.Property == InputNameProperty || change.Property == SendNameProperty || change.Property == StopNameProperty)
        {
            RefreshNames();
        }
    }

    private void RefreshNames()
    {
        if (_input is not null)
        {
            AutomationProperties.SetName(_input, InputName);
        }
        if (_send is not null)
        {
            _send.SendName = SendName;
            _send.StopName = StopName;
        }
    }

    private void RefreshSend()
    {
        if (_send is null)
        {
            return;
        }
        _send.State = IsBusy ? AiSendState.Generating : HasText && CanSend ? AiSendState.Ready : AiSendState.Empty;
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _input is null || e.Source is not Visual source ||
            !(ReferenceEquals(source, _input) || _input.IsVisualAncestorOf(source)))
        {
            return;
        }
        if (e.Key == Key.Enter && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            // One line: Enter sends (never a line break); busy, it does nothing and the text stays.
            e.Handled = true;
            if (!IsBusy)
            {
                Send();
            }
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && IsBusy)
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(StopRequestedEvent, this));
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy)
        {
            RaiseEvent(new RoutedEventArgs(StopRequestedEvent, this));
        }
        else
        {
            Send();
        }
    }

    private void Send()
    {
        if (!HasText || !CanSend)
        {
            return;
        }
        RaiseEvent(new AiPanelSendEventArgs(SendRequestedEvent, this, Text!.Trim()));
    }
}
