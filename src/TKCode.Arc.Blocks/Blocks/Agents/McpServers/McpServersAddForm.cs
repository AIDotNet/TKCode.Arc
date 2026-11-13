using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The Add server form of <see cref="McpServers"/>: a name, a URL / Command switch, the address or the command and its
/// arguments, optional headers or environment variables (one <c>KEY=value</c> per line), and Cancel and Add and connect.
/// The form checks itself on submit and then again as you type; Enter in a one-line field submits, Escape cancels.
/// </summary>
internal sealed class McpServersAddForm : Border
{
    private readonly McpServers _owner;
    private readonly TextBlock _title;
    private readonly Input _name;
    private readonly SegmentedControl _transport;
    private readonly SegmentedOption _httpOption;
    private readonly SegmentedOption _commandOption;
    private readonly Input _url;
    private readonly Grid _commandFields;
    private readonly Input _command;
    private readonly Input _arguments;
    private readonly Textarea _variables;
    private readonly Button _cancel;
    private readonly Button _submit;
    private bool _submitted;

    public McpServersAddForm(McpServers owner)
    {
        _owner = owner;
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        this.Bind(PaddingProperty, owner.GetObservable(McpServers.FormPaddingProperty));

        var panel = new StackPanel { Spacing = 16 };
        _title = McpServersUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, medium: true, lineHeight: 19.6);
        AutomationProperties.SetHeadingLevel(_title, 3);
        panel.Children.Add(_title);

        _name = new Input();
        panel.Children.Add(_name);

        _httpOption = new SegmentedOption(McpTransport.Http, null);
        _commandOption = new SegmentedOption(McpTransport.Command, null);
        _transport = new SegmentedControl
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = new[] { _httpOption, _commandOption },
            SelectedIndex = 0,
        };
        _transport.SelectionChanged += (_, _) => SyncTransport();
        panel.Children.Add(_transport);

        _url = new Input();
        ArcTokens.Bind(_url, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        panel.Children.Add(_url);

        _commandFields = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,3*"), ColumnSpacing = 12 };
        _command = new Input();
        _arguments = new Input();
        ArcTokens.Bind(_command, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(_arguments, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        Grid.SetColumn(_arguments, 1);
        _commandFields.Children.Add(_command);
        _commandFields.Children.Add(_arguments);
        panel.Children.Add(_commandFields);

        _variables = new Textarea { FrameMinHeight = 76, IsResizable = false, AcceptsReturn = true };
        ArcTokens.Bind(_variables, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        panel.Children.Add(_variables);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        _cancel = new Button { Variant = ButtonVariant.Ghost };
        _submit = new Button { Variant = ButtonVariant.Primary };
        _cancel.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.CloseAddForm(returnFocus: true);
        };
        _submit.Click += (_, e) =>
        {
            e.Handled = true;
            Submit();
        };
        buttons.Children.Add(_cancel);
        buttons.Children.Add(_submit);
        panel.Children.Add(buttons);

        foreach (var input in new TextBox[] { _name, _url, _command, _arguments, _variables })
        {
            input.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty && _submitted)
                {
                    Validate();
                }
            };
        }
        foreach (var input in new TextBox[] { _name, _url, _command, _arguments })
        {
            // Tunnel: the text box must not see the Enter first.
            input.AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
                {
                    e.Handled = true;
                    Submit();
                }
            }, RoutingStrategies.Tunnel);
        }
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _owner.CloseAddForm(returnFocus: true);
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        Child = panel;
        SyncLabels();
        SyncTransport();
    }

    public Input NameField => _name;

    public SegmentedControl TransportSwitch => _transport;

    public Input UrlField => _url;

    public Input CommandField => _command;

    public Input ArgumentsField => _arguments;

    public Textarea VariablesField => _variables;

    public Button CancelButton => _cancel;

    public Button SubmitButton => _submit;

    public McpTransport Transport
    {
        get => _transport.SelectedIndex == 1 ? McpTransport.Command : McpTransport.Http;
        set => _transport.SelectedIndex = value == McpTransport.Command ? 1 : 0;
    }

    /// <summary>Re-reads the words (labels changed).</summary>
    public void SyncLabels()
    {
        var labels = _owner.Labels;
        _title.Text = labels.FormTitle;
        _name.Label = labels.NameLabel;
        _name.Placeholder = labels.NamePlaceholder;
        _httpOption.Label = labels.HttpOption;
        _commandOption.Label = labels.CommandOption;
        AutomationProperties.SetName(_transport, labels.TransportLabel);
        _url.Label = labels.UrlLabel;
        _url.Placeholder = labels.UrlPlaceholder;
        _command.Label = labels.CommandLabel;
        _command.Placeholder = labels.CommandPlaceholder;
        _arguments.Label = labels.ArgumentsLabel;
        _arguments.Placeholder = labels.ArgumentsPlaceholder;
        _variables.Description = labels.VariablesHint;
        _variables.Placeholder = labels.VariablesPlaceholder;
        McpServersUi.SetText(_cancel, labels.Cancel);
        McpServersUi.SetText(_submit, labels.Submit);
        SyncTransport();
        if (_submitted)
        {
            Validate();
        }
    }

    /// <summary>Empties the form, back to URL, with no errors.</summary>
    public void Reset()
    {
        _submitted = false;
        _name.Text = string.Empty;
        _url.Text = string.Empty;
        _command.Text = string.Empty;
        _arguments.Text = string.Empty;
        _variables.Text = string.Empty;
        Transport = McpTransport.Http;
        ClearErrors();
    }

    /// <summary>Focuses the name field.</summary>
    public bool FocusFirst(NavigationMethod method) => _name.Focus(method);

    /// <summary>Checks the form; when it is valid, hands the draft to the owner (which adds and connects the server).</summary>
    public McpServer? Submit()
    {
        _submitted = true;
        var errors = Validate();
        if (!errors.IsValid)
        {
            FocusFirstError(errors);
            return null;
        }
        McpServersDraftCheck.TryParseVariables(_variables.Text, out var variables, out _);
        var http = Transport == McpTransport.Http;
        var draft = new McpServerDraft(
            (_name.Text ?? string.Empty).Trim(),
            Transport,
            http ? _url.Text?.Trim() : null,
            http ? null : _command.Text?.Trim(),
            http || string.IsNullOrWhiteSpace(_arguments.Text) ? null : _arguments.Text.Trim(),
            variables);
        return _owner.SubmitDraft(draft);
    }

    private McpServersDraftErrors Validate()
    {
        var errors = McpServersDraftCheck.Validate(_name.Text, Transport, _url.Text, _command.Text, _variables.Text,
            _owner.CurrentServers, _owner.Labels);
        _name.ErrorMessage = errors.Name;
        _url.ErrorMessage = errors.Url;
        _command.ErrorMessage = errors.Command;
        _variables.ErrorMessage = errors.Variables;
        return errors;
    }

    private void ClearErrors()
    {
        _name.ErrorMessage = null;
        _url.ErrorMessage = null;
        _command.ErrorMessage = null;
        _variables.ErrorMessage = null;
    }

    private void FocusFirstError(McpServersDraftErrors errors)
    {
        TextBox? target = errors.Name is not null ? _name
            : errors.Url is not null ? _url
            : errors.Command is not null ? _command
            : _variables;
        target.Focus(NavigationMethod.Tab);
    }

    private void SyncTransport()
    {
        var http = Transport == McpTransport.Http;
        _url.IsVisible = http;
        _commandFields.IsVisible = !http;
        var labels = _owner.Labels;
        _variables.Label = http ? labels.HeadersLabel : labels.EnvironmentLabel;
        if (_submitted)
        {
            Validate();
        }
    }
}
