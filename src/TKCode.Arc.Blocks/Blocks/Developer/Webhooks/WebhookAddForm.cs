using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The "Add endpoint" form (Arc Webhooks, design reference; 16 between parts): the URL and the optional description
/// side by side (3fr : 2fr, 16 apart; one column at ≤ 560), "Events" with a checkbox per event type in three columns
/// (two at ≤ 800, one at ≤ 560; nudged 13px left so the boxes line up with the fields), the ticked count at the left
/// of Cancel (ghost) and Add endpoint (primary). Errors show on submit (empty or non-public URL, no event) and clear
/// as the field changes; the focus goes to the first wrong field. While the request runs the fields are read-only and
/// the submit button shows a spinner and "Saving" but keeps the focus.
/// </summary>
internal sealed class WebhookAddForm : StackPanel
{
    private readonly Webhooks _owner;
    private readonly Grid _fields;
    private readonly Input _url;
    private readonly Input _description;
    private readonly Grid _events;
    private readonly List<(WebhookEventType Type, Checkbox Box)> _boxes = new();
    private readonly TextBlock _eventsError;
    private readonly TextBlock _createError;
    private readonly Grid _actions;
    private readonly TextBlock _count;
    private readonly Button _cancel;
    private readonly Button _submit;
    private bool? _singleColumn;
    private int _eventColumns;
    private bool _busy;

    public WebhookAddForm(Webhooks owner)
    {
        _owner = owner;
        Spacing = 16;
        var labels = owner.Labels;
        _url = new Input { Label = labels.UrlLabel, Placeholder = labels.UrlPlaceholder };
        _url.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _url.ErrorMessage = null;
            }
        };
        _description = new Input { Label = labels.DescriptionLabel, Placeholder = labels.DescriptionPlaceholder, Description = labels.Optional };
        _fields = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _fields.Children.Add(_url);
        _fields.Children.Add(_description);

        var legend = WebhookUi.Text(labels.EventsLegend, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 19.6);
        legend.Margin = new Thickness(0, 0, 0, 2);
        _events = new Grid { ColumnSpacing = 16, Margin = new Thickness(-13, 0, 0, 0) };
        _eventsError = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Danger, lineHeight: 16.8);
        _eventsError.IsVisible = false;
        var fieldset = new StackPanel();
        fieldset.Children.Add(legend);
        fieldset.Children.Add(_events);
        fieldset.Children.Add(_eventsError);
        AutomationProperties.SetName(fieldset, labels.EventsLegend);

        _createError = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Danger, lineHeight: 16.8);
        _createError.TextWrapping = TextWrapping.Wrap;
        _createError.IsVisible = false;

        _count = WebhookUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        _count.VerticalAlignment = VerticalAlignment.Center;
        _cancel = new Button { Variant = ButtonVariant.Ghost, Content = labels.Cancel };
        _cancel.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.CancelFromForm();
        };
        _submit = new Button { Variant = ButtonVariant.Primary, Content = labels.AddEndpoint };
        _submit.Click += (_, e) =>
        {
            e.Handled = true;
            if (!_busy)
            {
                _ = _owner.SubmitFromFormAsync();
            }
        };
        _actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        _actions.Children.Add(_count);
        _actions.Children.Add(_cancel);
        _actions.Children.Add(_submit);

        Children.Add(_fields);
        Children.Add(fieldset);
        Children.Add(_createError);
        Children.Add(_actions);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        ApplyWidth(double.PositiveInfinity);
    }

    /// <summary>The URL field.</summary>
    public Input UrlInput => _url;

    /// <summary>The description field.</summary>
    public Input DescriptionInput => _description;

    /// <summary>The submit button.</summary>
    public Button SubmitButton => _submit;

    /// <summary>The cancel button.</summary>
    public Button CancelButton => _cancel;

    /// <summary>The event checkboxes, in order.</summary>
    public IReadOnlyList<Checkbox> EventBoxes => _boxes.Select(b => b.Box).ToList();

    /// <summary>The error under the events (tests).</summary>
    public TextBlock EventsError => _eventsError;

    /// <summary>The form's error when the endpoint could not be added (tests).</summary>
    public TextBlock CreateError => _createError;

    /// <summary>The ticked count (tests).</summary>
    public TextBlock CountText => _count;

    /// <summary>How many columns the events take now.</summary>
    public int EventColumns => _eventColumns;

    /// <summary>Whether the fields take one column.</summary>
    public bool IsSingleColumn => _singleColumn == true;

    /// <summary>Picks the columns from the block's width (Arc: events 3 → 2 at ≤ 800 → 1 at ≤ 560; fields 1 at ≤ 560).</summary>
    public void ApplyWidth(double width)
    {
        var single = width <= _owner.FormSingleColumnWidth;
        var columns = single ? 1 : width <= _owner.EventsTwoColumnWidth ? 2 : 3;
        if (_singleColumn != single)
        {
            _singleColumn = single;
            _fields.ColumnDefinitions = single ? new ColumnDefinitions("*") : new ColumnDefinitions("3*,2*");
            _fields.RowDefinitions = single ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            Grid.SetColumn(_description, single ? 0 : 1);
            Grid.SetRow(_description, single ? 1 : 0);

            _actions.ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto");
            _actions.RowDefinitions = single ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            Grid.SetColumnSpan(_count, single ? 3 : 1);
            Grid.SetRow(_cancel, single ? 1 : 0);
            Grid.SetRow(_submit, single ? 1 : 0);
            Grid.SetColumn(_cancel, 1);
            Grid.SetColumn(_submit, 2);
        }
        if (_eventColumns != columns)
        {
            _eventColumns = columns;
            LayoutEvents();
        }
    }

    private void LayoutEvents()
    {
        var columns = Math.Max(1, _eventColumns);
        _events.ColumnDefinitions.Clear();
        _events.RowDefinitions.Clear();
        for (var c = 0; c < columns; c++)
        {
            _events.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        }
        var rows = (_boxes.Count + columns - 1) / columns;
        for (var r = 0; r < rows; r++)
        {
            _events.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        for (var i = 0; i < _boxes.Count; i++)
        {
            Grid.SetColumn(_boxes[i].Box, i % columns);
            Grid.SetRow(_boxes[i].Box, i / columns);
        }
    }

    /// <summary>Builds the checkboxes from <see cref="Webhooks.EventTypes"/> and resets everything (Arc resets on every open).</summary>
    public void Reset(IEnumerable<WebhookEventType> types)
    {
        _events.Children.Clear();
        _boxes.Clear();
        var list = types.ToList();
        var anyDefault = list.Any(t => t.IsDefaultSelected);
        for (var i = 0; i < list.Count; i++)
        {
            var type = list[i];
            var name = WebhookUi.Text(type.Name, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, FontWeight.Medium, mono: true, lineHeight: 16.8);
            var box = new Checkbox
            {
                Content = name,
                Description = type.Description,
                IsChecked = anyDefault ? type.IsDefaultSelected : i == 0,
                VerticalAlignment = VerticalAlignment.Top,
            };
            AutomationProperties.SetName(box, type.Name);
            box.IsCheckedChanged += (_, _) =>
            {
                _eventsError.IsVisible = false;
                UpdateCount();
            };
            _boxes.Add((type, box));
            _events.Children.Add(box);
        }
        LayoutEvents();
        _url.Text = string.Empty;
        _description.Text = string.Empty;
        _url.ErrorMessage = null;
        _eventsError.IsVisible = false;
        ShowCreateError(null);
        SetBusy(false);
        UpdateCount();
    }

    private int Ticked => _boxes.Count(b => b.Box.IsChecked == true);

    private void UpdateCount() => _count.Text = _owner.Labels.EventsSelected(Ticked);

    /// <summary>Checks the form; on errors shows them, focuses the first wrong field and returns null.</summary>
    public WebhookEndpointDraft? Validate()
    {
        var labels = _owner.Labels;
        var url = (_url.Text ?? string.Empty).Trim();
        var urlError = _owner.UrlValidator is { } custom
            ? custom(url)
            : WebhookMath.ValidateUrl(url, labels.UrlRequired, labels.UrlInvalid);
        _url.ErrorMessage = urlError;
        var events = _boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Type.Name).ToList();
        var eventsError = events.Count == 0 && _boxes.Count > 0 ? labels.EventsRequired : null;
        _eventsError.Text = eventsError;
        _eventsError.IsVisible = eventsError is not null;
        if (urlError is not null)
        {
            _url.Focus(NavigationMethod.Tab);
            _owner.Announce(urlError);
            return null;
        }
        if (eventsError is not null)
        {
            _boxes[0].Box.Focus(NavigationMethod.Tab);
            _owner.Announce(eventsError);
            return null;
        }
        var description = (_description.Text ?? string.Empty).Trim();
        return new WebhookEndpointDraft(url, description.Length == 0 ? null : description, events);
    }

    /// <summary>Read-only fields and a busy submit button while the request runs.</summary>
    public void SetBusy(bool busy)
    {
        _busy = busy;
        var labels = _owner.Labels;
        _url.IsReadOnly = busy;
        _description.IsReadOnly = busy;
        foreach (var (_, box) in _boxes)
        {
            box.IsHitTestVisible = !busy;
        }
        _submit.Content = busy ? WebhookUi.ButtonLabel(WebhookUi.Spinner(), labels.Saving) : labels.AddEndpoint;
        AutomationProperties.SetName(_submit, busy ? labels.Saving : labels.AddEndpoint);
    }

    /// <summary>Whether the request runs.</summary>
    public bool IsBusy => _busy;

    /// <summary>Shows (or hides with null) the form's error when the endpoint could not be added.</summary>
    public void ShowCreateError(string? text)
    {
        _createError.Text = text;
        _createError.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.Source is TextBox && !_busy)
        {
            e.Handled = true;
            _ = _owner.SubmitFromFormAsync();
        }
    }
}

/// <summary>
/// What replaces the form once the endpoint was added: the one-time signing secret (label, mono value with Copy, the
/// warning line) and Done at the right.
/// </summary>
internal sealed class WebhookReveal : StackPanel
{
    public WebhookReveal(Webhooks owner)
    {
        Spacing = 16;
        var labels = owner.Labels;
        Secret = new DeveloperSecretField
        {
            Label = labels.SigningSecret,
            LabelSpacing = 8,
            Notice = labels.SecretNotice,
            CopyText = labels.Copy,
            CopiedText = labels.Copied,
        };
        Done = new Button { Variant = ButtonVariant.Primary, Content = labels.Done, HorizontalAlignment = HorizontalAlignment.Right };
        Done.Click += (_, e) =>
        {
            e.Handled = true;
            owner.DoneFromReveal();
        };
        Children.Add(Secret);
        Children.Add(Done);
    }

    /// <summary>The one-time secret.</summary>
    public DeveloperSecretField Secret { get; }

    /// <summary>The Done button.</summary>
    public Button Done { get; }
}
