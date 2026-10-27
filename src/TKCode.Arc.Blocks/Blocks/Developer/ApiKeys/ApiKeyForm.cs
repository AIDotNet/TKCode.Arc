using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The "Create key" form inside the <see cref="ApiKeys"/> panel: name, environment, scopes, a hint, Cancel and Generate.
/// Two columns of fields and scopes while the panel is wider than 560; one column (and the hint on its own line) at or
/// below. The block resets it on every open and reads it on submit.
/// </summary>
[TemplatePart(NamePart, typeof(Input))]
[TemplatePart(EnvironmentPart, typeof(SegmentedControl))]
[TemplatePart(ScopeGridPart, typeof(ApiKeyScopeGrid))]
[TemplatePart(ScopeErrorHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(ErrorHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(HintPart, typeof(ArcContentSwap))]
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(GeneratePart, typeof(Button))]
[PseudoClasses(StackedPseudoClass, BusyPseudoClass)]
internal sealed class ApiKeyForm : TemplatedControl
{
    internal const string NamePart = "PART_Name";
    internal const string EnvironmentLabelPart = "PART_EnvironmentLabel";
    internal const string EnvironmentPart = "PART_Environment";
    internal const string EnvironmentFieldPart = "PART_EnvironmentField";
    internal const string FieldsPart = "PART_Fields";
    internal const string ScopesLabelPart = "PART_ScopesLabel";
    internal const string ScopeGridPart = "PART_ScopeGrid";
    internal const string ScopeErrorHostPart = "PART_ScopeErrorHost";
    internal const string ScopeErrorPart = "PART_ScopeError";
    internal const string ErrorHostPart = "PART_ErrorHost";
    internal const string ErrorPart = "PART_Error";
    internal const string ActionsPart = "PART_Actions";
    internal const string HintPart = "PART_Hint";
    internal const string CancelPart = "PART_Cancel";
    internal const string GeneratePart = "PART_Generate";
    internal const string StackedPseudoClass = ":stacked";
    internal const string BusyPseudoClass = ":busy";

    private Input? _name;
    private TextBlock? _environmentLabel;
    private SegmentedControl? _environment;
    private Control? _environmentField;
    private Grid? _fields;
    private TextBlock? _scopesLabel;
    private ApiKeyScopeGrid? _scopeGrid;
    private SpringSizeDecorator? _scopeErrorHost;
    private TextBlock? _scopeError;
    private SpringSizeDecorator? _errorHost;
    private TextBlock? _error;
    private Grid? _actions;
    private ArcContentSwap? _hint;
    private Button? _cancel;
    private Button? _generate;

    private ApiKeyLabels _labels = new();
    private readonly List<(ApiKeyScope Scope, Checkbox Box)> _scopes = new();
    private IReadOnlyList<ApiKeyScope> _pendingScopes = [];
    private ApiKeyEnvironment _environmentValue;
    private int _maxLength = 40;
    private bool _stacked;
    private bool _busy;
    private bool _syncing;

    /// <summary>Raised when the person submits (Generate, or Enter in the name field).</summary>
    public event EventHandler? Submitted;

    /// <summary>Raised when Cancel is pressed.</summary>
    public event EventHandler? Cancelled;

    /// <summary>The name field (focused when the panel opens).</summary>
    public Input? NameInput => _name;

    /// <summary>The submit button.</summary>
    public Button? GenerateButton => _generate;

    /// <summary>The cancel button.</summary>
    public Button? CancelButton => _cancel;

    /// <summary>The environment switch.</summary>
    public SegmentedControl? EnvironmentSwitch => _environment;

    /// <summary>The scope checkboxes, in order.</summary>
    public IReadOnlyList<Checkbox> ScopeBoxes => _scopes.Select(s => s.Box).ToList();

    /// <summary>The name as typed (not trimmed).</summary>
    public string NameText => _name?.Text ?? string.Empty;

    /// <summary>The chosen environment.</summary>
    public ApiKeyEnvironment Environment => _environmentValue;

    /// <summary>The ticked scopes' names, in order.</summary>
    public IReadOnlyList<string> CheckedScopes =>
        _scopes.Where(s => s.Box.IsChecked == true).Select(s => s.Scope.Name).ToList();

    /// <summary>Whether the name error shows.</summary>
    public bool HasNameError => !string.IsNullOrEmpty(_name?.ErrorMessage);

    /// <summary>Whether the scope error shows.</summary>
    public bool HasScopeError => _scopeErrorHost?.IsExpanded == true;

    /// <summary>The generate error shown above the actions; null while none.</summary>
    public string? GenerateError { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_name is not null)
        {
            _name.RemoveHandler(KeyDownEvent, OnNameKeyDown);
            _name.TextChanged -= OnNameChanged;
        }
        if (_environment is not null)
        {
            _environment.SelectionChanged -= OnEnvironmentChanged;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_generate is not null)
        {
            _generate.Click -= OnGenerateClick;
        }

        _name = e.NameScope.Find<Input>(NamePart);
        _environmentLabel = e.NameScope.Find<TextBlock>(EnvironmentLabelPart);
        _environment = e.NameScope.Find<SegmentedControl>(EnvironmentPart);
        _environmentField = e.NameScope.Find<Control>(EnvironmentFieldPart);
        _fields = e.NameScope.Find<Grid>(FieldsPart);
        _scopesLabel = e.NameScope.Find<TextBlock>(ScopesLabelPart);
        _scopeGrid = e.NameScope.Find<ApiKeyScopeGrid>(ScopeGridPart);
        _scopeErrorHost = e.NameScope.Find<SpringSizeDecorator>(ScopeErrorHostPart);
        _scopeError = e.NameScope.Find<TextBlock>(ScopeErrorPart);
        _errorHost = e.NameScope.Find<SpringSizeDecorator>(ErrorHostPart);
        _error = e.NameScope.Find<TextBlock>(ErrorPart);
        _actions = e.NameScope.Find<Grid>(ActionsPart);
        _hint = e.NameScope.Find<ArcContentSwap>(HintPart);
        _cancel = e.NameScope.Find<Button>(CancelPart);
        _generate = e.NameScope.Find<Button>(GeneratePart);

        if (_name is not null)
        {
            // Tunnel: the text box would otherwise see Enter first; nothing in it needs Enter.
            _name.AddHandler(KeyDownEvent, OnNameKeyDown, RoutingStrategies.Tunnel);
            _name.TextChanged += OnNameChanged;
        }
        if (_environment is not null)
        {
            _environment.SelectionChanged += OnEnvironmentChanged;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_generate is not null)
        {
            _generate.Click += OnGenerateClick;
        }
        _scopes.Clear();
        ApplyLabels();
        BuildScopes(_pendingScopes, keepChecks: false);
        ApplyEnvironment();
        ApplyStacked();
        ApplyBusy();
        ApplyGenerateError(animate: false);
    }

    /// <summary>Sets every text from <paramref name="labels"/> and the name limit.</summary>
    public void Configure(ApiKeyLabels labels, int maxLength)
    {
        _labels = labels;
        _maxLength = maxLength;
        ApplyLabels();
    }

    /// <summary>Puts the form back to its first state: no name, <paramref name="environment"/>, the default scopes, no errors.</summary>
    public void Reset(ApiKeyEnvironment environment, IReadOnlyList<ApiKeyScope> scopes)
    {
        _pendingScopes = scopes;
        _environmentValue = environment;
        _busy = false;
        GenerateError = null;
        if (_name is not null)
        {
            _syncing = true;
            _name.Text = string.Empty;
            _syncing = false;
            _name.ErrorMessage = null;
            _name.IsInvalid = false;
        }
        BuildScopes(scopes, keepChecks: false);
        SetScopeError(false, animate: false);
        ApplyEnvironment(animateHint: false);
        ApplyBusy();
        ApplyGenerateError(animate: false);
    }

    /// <summary>Shows the empty-name error and focuses the field.</summary>
    public void ShowNameError(NavigationMethod method)
    {
        if (_name is null)
        {
            return;
        }
        _name.ErrorMessage = _labels.NameRequired;
        _name.IsInvalid = true;
        _name.Focus(method);
    }

    /// <summary>Shows the no-scope error and focuses the first scope.</summary>
    public void ShowScopeError(NavigationMethod method)
    {
        SetScopeError(true, animate: true);
        _scopes.FirstOrDefault().Box?.Focus(method);
    }

    /// <summary>Shows (or clears with null) the error of a failed generate.</summary>
    public void SetGenerateError(string? error)
    {
        GenerateError = error;
        ApplyGenerateError(animate: true);
    }

    /// <summary>Locks the fields while the key is being generated; Generate shows a spinner and keeps the focus.</summary>
    public void SetBusy(bool busy)
    {
        _busy = busy;
        ApplyBusy();
    }

    /// <summary>Whether the form uses one column (the panel is 560 wide or less).</summary>
    public bool IsStacked
    {
        get => _stacked;
        set
        {
            if (_stacked == value)
            {
                return;
            }
            _stacked = value;
            ApplyStacked();
        }
    }

    private void ApplyLabels()
    {
        if (_name is not null)
        {
            _name.Label = _labels.NameLabel;
            _name.Placeholder = _labels.NamePlaceholder;
            _name.MaxLength = _maxLength;
            AutomationProperties.SetName(_name, _labels.NameLabel);
        }
        if (_environmentLabel is not null)
        {
            _environmentLabel.Text = _labels.EnvironmentLabel;
        }
        if (_environment is not null)
        {
            var index = _environment.SelectedIndex;
            _syncing = true;
            _environment.ItemsSource = new[]
            {
                new SegmentedOption { Value = ApiKeyEnvironment.Test, Label = _labels.Test },
                new SegmentedOption { Value = ApiKeyEnvironment.Live, Label = _labels.Live },
            };
            _environment.SelectedIndex = index < 0 ? (int)_environmentValue : index;
            _syncing = false;
            AutomationProperties.SetName(_environment, _labels.EnvironmentLabel);
        }
        if (_scopesLabel is not null)
        {
            _scopesLabel.Text = _labels.ScopesLabel;
        }
        if (_scopeGrid is not null)
        {
            AutomationProperties.SetName(_scopeGrid, _labels.ScopesLabel);
        }
        if (_scopeError is not null)
        {
            _scopeError.Text = _labels.ScopeRequired;
        }
        if (_cancel is not null)
        {
            _cancel.Content = _labels.Cancel;
        }
        if (_generate is not null)
        {
            _generate.Content = _labels.Generate;
        }
        if (_name?.IsInvalid == true)
        {
            _name.ErrorMessage = _labels.NameRequired;
        }
        ApplyHint(animate: false);
    }

    private void BuildScopes(IReadOnlyList<ApiKeyScope> scopes, bool keepChecks)
    {
        if (_scopeGrid is null)
        {
            return;
        }
        var previous = keepChecks ? CheckedScopes.ToHashSet() : null;
        foreach (var (_, box) in _scopes)
        {
            box.IsCheckedChanged -= OnScopeChecked;
        }
        _scopes.Clear();
        _scopeGrid.Children.Clear();
        foreach (var scope in scopes)
        {
            var name = new TextBlock
            {
                Text = scope.Name,
                FontSize = 12,
                LineHeight = 16.8,
                FontWeight = Avalonia.Media.FontWeight.Medium,
            };
            ArcTokens.Bind(name, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
            var box = new Checkbox
            {
                Content = name,
                Description = scope.Description,
                IsChecked = previous?.Contains(scope.Name) ?? scope.IsDefault,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(box, scope.Name);
            if (!string.IsNullOrEmpty(scope.Description))
            {
                AutomationProperties.SetHelpText(box, scope.Description);
            }
            box.IsCheckedChanged += OnScopeChecked;
            _scopes.Add((scope, box));
            _scopeGrid.Children.Add(box);
        }
    }

    private void OnScopeChecked(object? sender, RoutedEventArgs e)
    {
        if (HasScopeError && CheckedScopes.Count > 0)
        {
            SetScopeError(false, animate: true);
        }
    }

    private void SetScopeError(bool on, bool animate)
    {
        if (_scopeErrorHost is null)
        {
            return;
        }
        // The line opens and closes on Smooth; it is an alert while it shows.
        _scopeErrorHost.IsExpanded = on;
        if (_scopeError is not null)
        {
            _scopeError.IsVisible = on || animate;
            AutomationProperties.SetLiveSetting(_scopeError, on ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Off);
        }
    }

    private void ApplyGenerateError(bool animate)
    {
        if (_errorHost is null)
        {
            return;
        }
        if (_error is not null && GenerateError is not null)
        {
            _error.Text = GenerateError;
        }
        _errorHost.IsExpanded = GenerateError is not null;
        if (!animate && GenerateError is null && _error is not null)
        {
            _error.Text = string.Empty;
        }
    }

    private void OnNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _name is null)
        {
            return;
        }
        // Typing clears the name error at once.
        if (_name.IsInvalid)
        {
            _name.ErrorMessage = null;
            _name.IsInvalid = false;
        }
    }

    private void OnNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.Handled)
        {
            e.Handled = true;
            if (!_busy)
            {
                Submitted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnEnvironmentChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _environment is null || _environment.SelectedIndex < 0)
        {
            return;
        }
        _environmentValue = _environment.SelectedIndex == 1 ? ApiKeyEnvironment.Live : ApiKeyEnvironment.Test;
        ApplyHint(animate: true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (!_busy)
        {
            Submitted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyEnvironment(bool animateHint = false)
    {
        if (_environment is not null)
        {
            _syncing = true;
            _environment.SelectedIndex = (int)_environmentValue;
            _syncing = false;
        }
        ApplyHint(animateHint);
    }

    private void ApplyHint(bool animate)
    {
        if (_hint is null)
        {
            return;
        }
        var text = _environmentValue == ApiKeyEnvironment.Live ? _labels.LiveHint : _labels.TestHint;
        if (Equals(_hint.Content, text))
        {
            return;
        }
        if (!animate)
        {
            // A reset happens while the form is out of sight: swap without the blur.
            var mode = _hint.Mode;
            _hint.Mode = ContentSwapMode.None;
            _hint.Content = text;
            _hint.Mode = mode;
            return;
        }
        _hint.Content = text;
    }

    private void ApplyStacked()
    {
        PseudoClasses.Set(StackedPseudoClass, _stacked);
        if (_fields is not null && _environmentField is not null)
        {
            _fields.ColumnDefinitions = _stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("*,Auto");
            // Only as many rows as are used: an empty row would still add the row spacing.
            _fields.RowDefinitions = _stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            Grid.SetColumn(_environmentField, _stacked ? 0 : 1);
            Grid.SetRow(_environmentField, _stacked ? 1 : 0);
        }
        if (_scopeGrid is not null)
        {
            _scopeGrid.Columns = _stacked ? 1 : 2;
        }
        if (_actions is not null && _hint is not null && _cancel is not null && _generate is not null)
        {
            // Stacked: the hint takes the first line, the buttons the second at the end.
            _actions.RowDefinitions = _stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            Grid.SetColumnSpan(_hint, _stacked ? 3 : 1);
            Grid.SetRow(_cancel, _stacked ? 1 : 0);
            Grid.SetRow(_generate, _stacked ? 1 : 0);
            _hint.Margin = _stacked ? default : new Thickness(0, 0, 8, 0);
        }
    }

    private void ApplyBusy()
    {
        PseudoClasses.Set(BusyPseudoClass, _busy);
        if (_generate is not null)
        {
            _generate.IsLoading = _busy;
        }
        if (_name is not null)
        {
            // Read-only rather than disabled: Enter in the field submits, and the focus must not drop.
            _name.IsReadOnly = _busy;
        }
        if (_environment is not null)
        {
            _environment.IsEnabled = !_busy;
        }
        foreach (var (_, box) in _scopes)
        {
            box.IsEnabled = !_busy;
        }
        if (_cancel is not null)
        {
            _cancel.IsEnabled = !_busy;
        }
    }
}

/// <summary>The one-time reveal inside the <see cref="ApiKeys"/> panel: the secret with a copy button, a warning and Done.</summary>
[TemplatePart(SecretPart, typeof(DeveloperSecretField))]
[TemplatePart(DonePart, typeof(Button))]
internal sealed class ApiKeyReveal : TemplatedControl
{
    internal const string SecretPart = "PART_Secret";
    internal const string DonePart = "PART_Done";

    private DeveloperSecretField? _secret;
    private Button? _done;
    private ApiKeyLabels _labels = new();
    private string? _pending;

    /// <summary>Raised when Done is pressed.</summary>
    public event EventHandler? DoneClicked;

    /// <summary>Raised when the secret was copied.</summary>
    public event EventHandler? Copied;

    /// <summary>The secret field.</summary>
    public DeveloperSecretField? SecretField => _secret;

    /// <summary>The Done button.</summary>
    public Button? DoneButton => _done;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_secret is not null)
        {
            _secret.Copied -= OnCopied;
        }
        if (_done is not null)
        {
            _done.Click -= OnDone;
        }
        _secret = e.NameScope.Find<DeveloperSecretField>(SecretPart);
        _done = e.NameScope.Find<Button>(DonePart);
        if (_secret is not null)
        {
            _secret.Copied += OnCopied;
        }
        if (_done is not null)
        {
            _done.Click += OnDone;
        }
        ApplyLabels();
        if (_pending is not null)
        {
            _secret?.SetSecret(_pending);
            _pending = null;
        }
    }

    /// <summary>Sets every text from <paramref name="labels"/>.</summary>
    public void Configure(ApiKeyLabels labels)
    {
        _labels = labels;
        ApplyLabels();
    }

    /// <summary>Shows <paramref name="secret"/> (the reveal plays).</summary>
    public void Show(string secret)
    {
        if (_secret is null)
        {
            _pending = secret;
            return;
        }
        _secret.SetSecret(secret);
    }

    /// <summary>Drops the secret: the text and every reference to it.</summary>
    public void Clear()
    {
        _pending = null;
        _secret?.ClearSecret();
    }

    private void ApplyLabels()
    {
        if (_secret is not null)
        {
            _secret.Label = _labels.SecretLabel;
            _secret.Notice = _labels.SecretOnce;
            _secret.CopyText = _labels.Copy;
            _secret.CopiedText = _labels.Copied;
        }
        if (_done is not null)
        {
            _done.Content = _labels.Done;
        }
    }

    private void OnCopied(object? sender, EventArgs e) => Copied?.Invoke(this, EventArgs.Empty);

    private void OnDone(object? sender, RoutedEventArgs e) => DoneClicked?.Invoke(this, EventArgs.Empty);
}
