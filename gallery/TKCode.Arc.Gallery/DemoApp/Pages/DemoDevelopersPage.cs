using Avalonia.Controls;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/developers: two stacked rows, the ApiKeys block ("keys") over the Webhooks block ("webhooks"), with
/// uiarc.dev's Halden API datasets (DemoDevelopersData). Every server call is simulated with uiarc.dev's timings: a key
/// is generated in 0.72 s, an endpoint added in 0.76 s, a retry answers after 1.1 s and a test event after 0.9 s.
/// </summary>
public sealed class DemoDevelopersPage : DemoPageView
{
    public DemoDevelopersPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        AddRow(DemoRowLayout.Stack, "keys", BuildKeys());
        AddRow(DemoRowLayout.Stack, "webhooks", BuildWebhooks());
    }

    private static ApiKeys BuildKeys()
    {
        var keys = new ApiKeys
        {
            Title = "API keys",
            Description = DemoDevelopersData.KeysDescription,
            CurrentUserName = DemoDevelopersData.CurrentUser,
            ReferenceTime = DemoData.Now,
            SimulatedPrefixFormat = "sk_{0}_",
            EmptyActionText = "Restore sample keys",
            Labels = DemoDevelopersData.KeyLabels,
            GenerateAction = DemoDevelopersData.GenerateKeyAsync
        };
        keys.Scopes.AddRange(DemoDevelopersData.Scopes());
        keys.Keys.AddRange(DemoDevelopersData.Keys());
        // The empty state's button puts the five sample keys back instead of opening the form.
        keys.EmptyActionInvoked += (_, e) =>
        {
            e.Handled = true;
            keys.Keys.AddRange(DemoDevelopersData.Keys());
        };
        return keys;
    }

    private static Border BuildWebhooks()
    {
        var hooks = new Webhooks
        {
            Title = "Webhooks",
            Description = DemoDevelopersData.WebhooksDescription,
            CurrentUser = DemoDevelopersData.CurrentUser,
            Labels = DemoDevelopersData.WebhookLabels,
            UrlValidator = DemoDevelopersData.ValidateUrl,
            RetryAction = DemoDevelopersData.RetryAsync,
            SendTestAction = DemoDevelopersData.SendTestAsync,
            CreateEndpointAction = DemoDevelopersData.CreateEndpointAsync
        };
        hooks.EventTypes.AddRange(DemoDevelopersData.EventTypes());
        hooks.Endpoints.AddRange(DemoDevelopersData.Endpoints());

        // The block has no frame of its own: the demo's 16px card around it.
        var card = new Border
        {
            CornerRadius = new Avalonia.CornerRadius(DemoUi.CardRadius),
            BorderThickness = new Avalonia.Thickness(1),
            ClipToBounds = true,
            Child = hooks
        };
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return card;
    }
}
