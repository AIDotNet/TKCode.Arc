using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

public partial class AgentApprovals
{
    private TextBlock? _title;
    private TextBlock? _count;
    private TextBlock? _emptyText;
    private ArcGlyph? _detailGlyph;
    private TextBlock? _detailTitle;
    private TextBlock? _asked;
    private TextBlock? _riskText;
    private TextBlock? _riskNote;
    private TextBlock? _whyHeading;
    private TextBlock? _context;
    private TextBlock? _detailsHeading;
    private Grid? _details;
    private TextBlock? _status;

    /// <summary>The line under the buttons (template use).</summary>
    internal TextBlock? StatusLine => _status;

    private void FindDetailParts(INameScope scope)
    {
        _title = scope.Find<TextBlock>("PART_Title");
        _count = scope.Find<TextBlock>("PART_Count");
        _emptyText = scope.Find<TextBlock>("PART_EmptyText");
        _detailGlyph = scope.Find<ArcGlyph>("PART_DetailGlyph");
        _detailTitle = scope.Find<TextBlock>("PART_DetailTitle");
        _asked = scope.Find<TextBlock>("PART_Asked");
        _riskText = scope.Find<TextBlock>("PART_RiskText");
        _riskNote = scope.Find<TextBlock>("PART_RiskNote");
        _whyHeading = scope.Find<TextBlock>("PART_WhyHeading");
        _context = scope.Find<TextBlock>("PART_Context");
        _detailsHeading = scope.Find<TextBlock>("PART_DetailsHeading");
        _details = scope.Find<Grid>("PART_Details");
        _status = scope.Find<TextBlock>("PART_Status");
    }

    private void UpdateHeader()
    {
        var labels = Labels;
        Set(_title, labels.Title);
        Set(_count, labels.Count(PendingCount));
        Set(_emptyText, labels.Empty);
    }

    private void ShowDetail()
    {
        var labels = Labels;
        var request = SelectedRequest;
        PseudoClasses.Set(HighRiskPseudoClass, request?.Risk == AgentApprovalRisk.High);
        PseudoClasses.Set(":medium-risk", request?.Risk == AgentApprovalRisk.Medium);
        if (request is null)
        {
            UpdateButtons();
            return;
        }
        if (_detailGlyph is not null)
        {
            _detailGlyph.Data = GlyphOf(request.Kind);
        }
        Set(_detailTitle, request.Title);
        var time = request.Requested is { } requested ? labels.Time(requested, Clock()) : null;
        Set(_asked, labels.Asked(request.Agent, time));
        Set(_riskText, labels.Risk(request.Risk));
        Set(_riskNote, request.RiskNote);
        Set(_whyHeading, labels.Why);
        Set(_context, request.Context);
        Set(_detailsHeading, labels.Details);
        if (_context is not null)
        {
            _context.IsVisible = !string.IsNullOrWhiteSpace(request.Context);
        }
        if (_whyHeading is not null)
        {
            _whyHeading.IsVisible = !string.IsNullOrWhiteSpace(request.Context);
        }
        if (_details is not null)
        {
            _details.Children.Clear();
            _details.RowDefinitions.Clear();
            var details = request.Details ?? [];
            for (var i = 0; i < details.Count; i++)
            {
                _details.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var label = new TextBlock { Text = details[i].Label, Margin = new Thickness(0, 0, 16, 6) };
                ArcTokens.Bind(label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
                ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                var value = new SelectableTextBlock { Text = details[i].Value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
                ArcTokens.Bind(value, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
                ArcTokens.Bind(value, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
                AutomationProperties.SetName(value, $"{details[i].Label}: {details[i].Value}");
                Grid.SetRow(label, i);
                Grid.SetRow(value, i);
                Grid.SetColumn(value, 1);
                label.VerticalAlignment = VerticalAlignment.Top;
                label.Margin = new Thickness(0, 2, 16, 6);
                _details.Children.Add(label);
                _details.Children.Add(value);
            }
            if (_detailsHeading is not null)
            {
                _detailsHeading.IsVisible = details.Count > 0;
            }
        }
        if (_reason is FieldTextBox field)
        {
            field.Placeholder = labels.ReasonPlaceholder;
        }
        if (_reason is not null)
        {
            AutomationProperties.SetName(_reason, labels.ReasonPlaceholder);
        }
        if (_confirm is not null)
        {
            _confirm.Content = labels.Confirm;
        }
        if (_approve is not null)
        {
            _approve.Content = labels.Approve;
        }
        if (_deny is not null)
        {
            _deny.Content = labels.Deny;
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var request = SelectedRequest;
        if (_approve is not null)
        {
            _approve.IsEnabled = request is not null && (request.Risk != AgentApprovalRisk.High || _confirm?.IsChecked == true);
        }
        if (_deny is not null)
        {
            _deny.IsEnabled = request is not null && !_busy;
        }
    }

    private void SetStatus(string? text, bool error)
    {
        if (_status is null)
        {
            return;
        }
        _status.Text = text;
        _status.IsVisible = !string.IsNullOrEmpty(text);
        PseudoClasses.Set(":status-error", error);
    }

    private static void Set(TextBlock? block, string? text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }
}
