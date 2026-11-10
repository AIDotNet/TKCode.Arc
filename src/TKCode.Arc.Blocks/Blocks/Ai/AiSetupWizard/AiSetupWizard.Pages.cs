using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The five pages. Each is built once from Arc controls and kept, so going back shows what was entered.
public partial class AiSetupWizard
{
    private readonly Control?[] _pages = new Control?[5];
    private RadioCards? _providerCards;
    private Select? _modelSelect;
    private PasswordField? _keyField;
    private TextBlock? _providerError;
    private Button? _testButton;
    private TextBlock? _testMessage;
    private readonly List<AiSetupWizardCheckRow> _checkRows = new();

    internal RadioCards? ProviderCards => _providerCards;

    internal Select? ModelSelect => _modelSelect;

    internal PasswordField? KeyField => _keyField;

    internal TextBlock? ProviderError => _providerError;

    internal IReadOnlyList<AiSetupWizardCheckRow> CheckRows => _checkRows;

    internal Button? TestButton => _testButton;

    private AiSetupProvider? SelectedProvider
        => (Providers ?? []).FirstOrDefault(p => p.Id == Configuration.ProviderId);

    private void BuildPages()
    {
        if (_page is null)
        {
            return;
        }
        _page.Content = null;
        _pages[0] = BuildProviderPage();
        _pages[1] = BuildDataPage();
        _pages[2] = BuildPrivacyPage();
        _pages[3] = BuildTestPage();
        _pages[4] = BuildDonePage();
    }

    private void ShowPage(bool animate, int direction = 1)
    {
        if (_page is null)
        {
            return;
        }
        if (CurrentStep == AiSetupStep.Done && !IsCompleted)
        {
            // The summary always reflects the latest choices.
            _pages[4] = BuildDonePage();
        }
        _page.Mode = animate ? ContentSwapMode.Slide : ContentSwapMode.None;
        _page.Direction = direction;
        _page.Content = _pages[(int)CurrentStep];
    }

    private bool Validate(AiSetupStep step)
    {
        if (step != AiSetupStep.Provider)
        {
            return true;
        }
        SyncKey();
        var labels = Labels;
        var provider = SelectedProvider;
        var ok = true;
        if (_providerError is not null)
        {
            _providerError.Text = provider is null ? labels.ProviderRequired : null;
            _providerError.IsVisible = provider is null;
        }
        if (provider is null)
        {
            ok = false;
            _providerCards?.Focus(NavigationMethod.Tab);
        }
        else if (provider.RequiresKey && string.IsNullOrWhiteSpace(Configuration.ApiKey))
        {
            ok = false;
            if (_keyField is not null)
            {
                _keyField.ErrorMessage = labels.KeyRequired;
                _keyField.Focus(NavigationMethod.Tab);
            }
        }
        if (!ok)
        {
            _announcer?.Announce(provider is null ? labels.ProviderRequired : labels.KeyRequired);
        }
        return ok;
    }

    // The key field reports its text after the keystroke; read what it holds now.
    private void SyncKey()
    {
        if (_keyField is { IsVisible: true } keyField && (keyField.Text ?? "") != Configuration.ApiKey)
        {
            Change(Configuration with { ApiKey = keyField.Text ?? "" });
        }
    }

    // A change to anything the test covers asks for a new test.
    private void Change(AiSetupConfiguration configuration)
    {
        Configuration = configuration;
        if (TestResult is not null || _checkRows.Any(r => r.State != AiSetupWizardCheckState.Pending))
        {
            _testCts?.Cancel();
            TestResult = null;
            IsTesting = false;
            foreach (var row in _checkRows)
            {
                row.State = AiSetupWizardCheckState.Pending;
            }
            if (_testMessage is not null)
            {
                _testMessage.Text = null;
            }
            if (_testButton is not null)
            {
                _testButton.Content = Labels.RunTest;
            }
            if (_furthest > AiSetupStep.Test)
            {
                _furthest = AiSetupStep.Test;
            }
            UpdateChrome();
        }
    }

    private static StackPanel PageStack() => new() { Spacing = 16 };

    private static TextBlock Text(string? text, ArcDesignTokenKind color = ArcDesignTokenKind.TextSecondary, bool small = false)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, small ? ArcDesignTokenKind.TypeXS : ArcDesignTokenKind.TypeSM);
        return block;
    }

    private static TextBlock Heading(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeBase);
        ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetHeadingLevel(block, 3);
        return block;
    }

    private Control BuildProviderPage()
    {
        var labels = Labels;
        var stack = PageStack();
        stack.Children.Add(Heading(StepName(AiSetupStep.Provider)));
        _providerCards = new RadioCards { Layout = RadioCardsLayout.List };
        AutomationProperties.SetName(_providerCards, StepName(AiSetupStep.Provider));
        foreach (var provider in Providers ?? [])
        {
            _providerCards.Items.Add(new RadioCardOption(provider.Id, provider.Name, provider.Description));
        }
        _providerCards.SelectedValue = Configuration.ProviderId;
        _providerCards.SelectionChanged += (_, _) =>
        {
            var id = _providerCards.SelectedValue as string;
            if (id == Configuration.ProviderId)
            {
                return;
            }
            var provider = (Providers ?? []).FirstOrDefault(p => p.Id == id);
            Change(Configuration with { ProviderId = id, Model = provider?.Models.FirstOrDefault(), ApiKey = "" });
            if (_providerError is not null)
            {
                _providerError.IsVisible = false;
            }
            UpdateProviderFields();
        };
        stack.Children.Add(_providerCards);
        _providerError = Text(null, ArcDesignTokenKind.Danger, small: true);
        _providerError.IsVisible = false;
        stack.Children.Add(_providerError);

        _modelSelect = new Select { Label = labels.Model };
        _modelSelect.SelectionChanged += (_, _) =>
        {
            if (_modelSelect.SelectedItem is string model && model != Configuration.Model)
            {
                Change(Configuration with { Model = model });
            }
        };
        stack.Children.Add(_modelSelect);

        _keyField = new PasswordField { Label = labels.ApiKey };
        _keyField.TextChanged += (_, _) =>
        {
            var key = _keyField.Text ?? "";
            if (key != Configuration.ApiKey)
            {
                _keyField.ErrorMessage = null;
                Change(Configuration with { ApiKey = key });
            }
        };
        stack.Children.Add(_keyField);
        UpdateProviderFields();
        return stack;
    }

    private void UpdateProviderFields()
    {
        var provider = SelectedProvider;
        if (_modelSelect is not null)
        {
            _modelSelect.IsVisible = provider is not null;
            _modelSelect.ItemsSource = provider?.Models;
            _modelSelect.SelectedItem = Configuration.Model;
        }
        if (_keyField is not null)
        {
            _keyField.IsVisible = provider is { RequiresKey: true };
            _keyField.Description = provider?.KeyHint;
            if (_keyField.Text != Configuration.ApiKey)
            {
                _keyField.Text = Configuration.ApiKey;
            }
            _keyField.ErrorMessage = null;
        }
    }

    private Control BuildDataPage()
    {
        var stack = PageStack();
        stack.Children.Add(Heading(StepName(AiSetupStep.DataAccess)));
        stack.Children.Add(Text(Labels.DataIntro));
        var list = new StackPanel { Spacing = 12 };
        foreach (var source in DataSources ?? [])
        {
            var box = new Checkbox
            {
                Content = source.Name,
                Description = source.Description,
                IsChecked = Configuration.DataSourceIds.Contains(source.Id),
            };
            box.IsCheckedChanged += (_, _) =>
            {
                var ids = Configuration.DataSourceIds.Where(id => id != source.Id).ToList();
                if (box.IsChecked == true)
                {
                    ids.Add(source.Id);
                }
                // Keep the order of DataSources.
                var ordered = (DataSources ?? []).Select(s => s.Id).Where(ids.Contains).ToArray();
                Change(Configuration with { DataSourceIds = ordered });
            };
            list.Children.Add(box);
        }
        stack.Children.Add(list);
        return stack;
    }

    private Control BuildPrivacyPage()
    {
        var labels = Labels;
        var stack = PageStack();
        stack.Children.Add(Heading(StepName(AiSetupStep.Privacy)));
        stack.Children.Add(SwitchRow(labels.ExcludeFromTraining, labels.ExcludeFromTrainingDetail, Configuration.ExcludeFromTraining,
            on => Change(Configuration with { ExcludeFromTraining = on })));
        stack.Children.Add(SwitchRow(labels.MaskPersonalInfo, labels.MaskPersonalInfoDetail, Configuration.MaskPersonalInfo,
            on => Change(Configuration with { MaskPersonalInfo = on })));
        var choices = HistoryChoices ?? [];
        if (choices.Count > 0)
        {
            var history = new StackPanel { Spacing = 8 };
            var label = Text(labels.History, ArcDesignTokenKind.Foreground);
            ArcTokens.Bind(label, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            history.Children.Add(label);
            var segmented = new SegmentedControl { HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(segmented, labels.History);
            foreach (var days in choices)
            {
                segmented.Items.Add(new SegmentedOption(days, labels.HistoryOption(days)));
            }
            segmented.SelectedValue = Configuration.HistoryDays;
            segmented.SelectionChanged += (_, _) =>
            {
                if (segmented.SelectedValue is int days && days != Configuration.HistoryDays)
                {
                    Change(Configuration with { HistoryDays = days });
                }
            };
            history.Children.Add(segmented);
            stack.Children.Add(history);
        }
        return stack;
    }

    private static Control SwitchRow(string title, string detail, bool value, Action<bool> changed)
    {
        var toggle = new Switch { IsChecked = value, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(toggle, title);
        AutomationProperties.SetHelpText(toggle, detail);
        toggle.IsCheckedChanged += (_, _) => changed(toggle.IsChecked == true);
        var name = Text(title, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(name, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        var copy = new StackPanel { Spacing = 2, Children = { name, Text(detail, ArcDesignTokenKind.TextMuted, small: true) } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(copy);
        Grid.SetColumn(toggle, 1);
        toggle.Margin = new Thickness(16, 0, 0, 0);
        grid.Children.Add(toggle);
        return grid;
    }

    private Control BuildTestPage()
    {
        var labels = Labels;
        var stack = PageStack();
        stack.Children.Add(Heading(StepName(AiSetupStep.Test)));
        stack.Children.Add(Text(labels.TestIntro));
        var list = new StackPanel { Spacing = 10 };
        _checkRows.Clear();
        foreach (var check in labels.TestChecks)
        {
            var row = new AiSetupWizardCheckRow { Text = check };
            _checkRows.Add(row);
            list.Children.Add(row);
        }
        stack.Children.Add(list);
        _testMessage = Text(null, small: true);
        AutomationProperties.SetLiveSetting(_testMessage, AutomationLiveSetting.Polite);
        stack.Children.Add(_testMessage);
        _testButton = new Button
        {
            Content = TestResult is null ? labels.RunTest : labels.RunAgain,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _testButton.Click += (_, _) => _ = RunTestAsync();
        stack.Children.Add(_testButton);
        return stack;
    }

    /// <summary>Runs the connection test now (what the test button does) and returns its result.</summary>
    public async Task<AiSetupTestResult?> RunTestAsync()
    {
        if (IsTesting)
        {
            return null;
        }
        SyncKey();
        _testCts?.Cancel();
        var cts = new CancellationTokenSource();
        _testCts = cts;
        var labels = Labels;
        TestResult = null;
        IsTesting = true;
        foreach (var row in _checkRows)
        {
            row.State = AiSetupWizardCheckState.Pending;
        }
        if (_checkRows.Count > 0)
        {
            _checkRows[0].State = AiSetupWizardCheckState.Running;
        }
        if (_testMessage is not null)
        {
            _testMessage.Text = null;
        }
        if (_testButton is not null)
        {
            _testButton.IsLoading = true;
        }
        UpdateChrome();
        var progress = new AiSetupWizardProgress(passed =>
        {
            for (var i = 0; i < _checkRows.Count; i++)
            {
                _checkRows[i].State = i < passed ? AiSetupWizardCheckState.Passed
                    : i == passed ? AiSetupWizardCheckState.Running : AiSetupWizardCheckState.Pending;
            }
        });
        AiSetupTestResult result;
        try
        {
            result = TestRunner is { } runner
                ? await runner(Configuration, progress, cts.Token)
                : await AiSetupWizardSamples.TestAsync(Configuration, progress, Delay, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            result = new AiSetupTestResult(false, ex.Message);
        }
        if (cts.IsCancellationRequested)
        {
            return null;
        }
        IsTesting = false;
        TestResult = result;
        foreach (var row in _checkRows)
        {
            if (result.IsSuccess)
            {
                row.State = AiSetupWizardCheckState.Passed;
            }
            else if (row.State == AiSetupWizardCheckState.Running)
            {
                row.State = AiSetupWizardCheckState.Failed;
            }
        }
        if (_testMessage is not null)
        {
            _testMessage.Text = result.Message;
            ArcTokens.Bind(_testMessage, TextBlock.ForegroundProperty, result.IsSuccess ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Danger);
        }
        if (_testButton is not null)
        {
            _testButton.IsLoading = false;
            _testButton.Content = labels.RunAgain;
        }
        UpdateChrome();
        _announcer?.Announce(result.Message);
        return result;
    }

    private Control BuildDonePage()
    {
        var labels = Labels;
        var configuration = Configuration;
        var stack = PageStack();
        var glyph = new ArcGlyph { Data = ArcIcons.CircleCheck, Size = 28, HorizontalAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        stack.Children.Add(glyph);
        stack.Children.Add(Heading(IsCompleted ? labels.CompletedTitle : labels.DoneTitle));
        stack.Children.Add(Text(labels.DoneText));
        var provider = SelectedProvider;
        var dataNames = (DataSources ?? []).Where(s => configuration.DataSourceIds.Contains(s.Id)).Select(s => s.Name).ToArray();
        string[] values =
        [
            provider?.Name ?? "–",
            configuration.Model ?? "–",
            dataNames.Length == 0 ? labels.NoData : string.Join(", ", dataNames),
            configuration.ExcludeFromTraining ? labels.Off : labels.On,
            configuration.MaskPersonalInfo ? labels.On : labels.Off,
            labels.HistoryOption(configuration.HistoryDays),
        ];
        var summary = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 8, ColumnSpacing = 24 };
        for (var i = 0; i < values.Length; i++)
        {
            summary.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var name = Text(i < labels.SummaryRows.Count ? labels.SummaryRows[i] : "", ArcDesignTokenKind.TextMuted);
            var value = Text(values[i], ArcDesignTokenKind.Foreground);
            Grid.SetRow(name, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            summary.Children.Add(name);
            summary.Children.Add(value);
        }
        var frame = new Border
        {
            Child = summary,
            Padding = new Thickness(16),
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        stack.Children.Add(frame);
        return stack;
    }
}

/// <summary>Reports test progress at once on the calling thread (a <see cref="Progress{T}"/> would post it later).</summary>
internal sealed class AiSetupWizardProgress(Action<int> report) : IProgress<int>
{
    public void Report(int value)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            report(value);
        }
        else
        {
            Dispatcher.UIThread.Post(() => report(value));
        }
    }
}

/// <summary>Where one check of the test stands.</summary>
internal enum AiSetupWizardCheckState
{
    Pending,
    Running,
    Passed,
    Failed
}

/// <summary>One check of the test: a status glyph (circle, spinner, check or cross) and its name.</summary>
internal sealed class AiSetupWizardCheckRow : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiSetupWizardCheckRow, string?>(nameof(Text));

    public static readonly StyledProperty<AiSetupWizardCheckState> StateProperty =
        AvaloniaProperty.Register<AiSetupWizardCheckRow, AiSetupWizardCheckState>(nameof(State));

    public AiSetupWizardCheckRow()
    {
        PseudoClasses.Set(":pending", true);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public AiSetupWizardCheckState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty || change.Property == TextProperty)
        {
            PseudoClasses.Set(":pending", State == AiSetupWizardCheckState.Pending);
            PseudoClasses.Set(":running", State == AiSetupWizardCheckState.Running);
            PseudoClasses.Set(":passed", State == AiSetupWizardCheckState.Passed);
            PseudoClasses.Set(":failed", State == AiSetupWizardCheckState.Failed);
            AutomationProperties.SetName(this, Text);
            AutomationProperties.SetItemStatus(this, State.ToString());
        }
    }
}
