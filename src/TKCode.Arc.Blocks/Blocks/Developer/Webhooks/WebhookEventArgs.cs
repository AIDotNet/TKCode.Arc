using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>Webhooks.EndpointSelectionChanged</c>.</summary>
public class WebhookEndpointSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookEndpointSelectionChangedEventArgs(RoutedEvent routedEvent, object source, WebhookEndpoint? oldEndpoint,
        WebhookEndpoint? newEndpoint, WebhookChangeSource changeSource)
        : base(routedEvent, source)
    {
        OldEndpoint = oldEndpoint;
        NewEndpoint = newEndpoint;
        ChangeSource = changeSource;
    }

    /// <summary>The endpoint selected before.</summary>
    public WebhookEndpoint? OldEndpoint { get; }

    /// <summary>The endpoint selected now.</summary>
    public WebhookEndpoint? NewEndpoint { get; }

    /// <summary>What changed the selection.</summary>
    public WebhookChangeSource ChangeSource { get; }
}

/// <summary>Arguments of <c>Webhooks.DeliverySelectionChanged</c>.</summary>
public class WebhookDeliverySelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookDeliverySelectionChangedEventArgs(RoutedEvent routedEvent, object source, WebhookDelivery? oldDelivery,
        WebhookDelivery? newDelivery, WebhookChangeSource changeSource)
        : base(routedEvent, source)
    {
        OldDelivery = oldDelivery;
        NewDelivery = newDelivery;
        ChangeSource = changeSource;
    }

    /// <summary>The delivery inspected before.</summary>
    public WebhookDelivery? OldDelivery { get; }

    /// <summary>The delivery inspected now.</summary>
    public WebhookDelivery? NewDelivery { get; }

    /// <summary>What changed the selection.</summary>
    public WebhookChangeSource ChangeSource { get; }
}

/// <summary>Arguments of <c>Webhooks.RetryStarted</c> and <c>Webhooks.TestEventSent</c>: a delivery and its endpoint.</summary>
public class WebhookDeliveryEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookDeliveryEventArgs(RoutedEvent routedEvent, object source, WebhookEndpoint endpoint, WebhookDelivery delivery)
        : base(routedEvent, source)
    {
        Endpoint = endpoint;
        Delivery = delivery;
    }

    /// <summary>The endpoint.</summary>
    public WebhookEndpoint Endpoint { get; }

    /// <summary>The delivery (the one being retried, or the test delivery that was sent).</summary>
    public WebhookDelivery Delivery { get; }
}

/// <summary>Arguments of <c>Webhooks.RetryCompleted</c>: a retry went through.</summary>
public class WebhookRetryCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookRetryCompletedEventArgs(RoutedEvent routedEvent, object source, WebhookEndpoint endpoint,
        WebhookDelivery original, WebhookDelivery result, double? oldRate, double? newRate)
        : base(routedEvent, source)
    {
        Endpoint = endpoint;
        Original = original;
        Result = result;
        OldRate = oldRate;
        NewRate = newRate;
    }

    /// <summary>The endpoint.</summary>
    public WebhookEndpoint Endpoint { get; }

    /// <summary>The failed delivery that was retried (now resolved).</summary>
    public WebhookDelivery Original { get; }

    /// <summary>The new attempt, added at the top of the list.</summary>
    public WebhookDelivery Result { get; }

    /// <summary>The endpoint's delivered rate before the retry.</summary>
    public double? OldRate { get; }

    /// <summary>The endpoint's delivered rate after it.</summary>
    public double? NewRate { get; }
}

/// <summary>Arguments of <c>Webhooks.ActionFailed</c>: a retry, a new endpoint or a test event did not go through.</summary>
public class WebhookActionFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookActionFailedEventArgs(RoutedEvent routedEvent, object source, WebhookActionKind action,
        WebhookEndpoint? endpoint, WebhookDelivery? delivery, Exception? exception)
        : base(routedEvent, source)
    {
        Action = action;
        Endpoint = endpoint;
        Delivery = delivery;
        Exception = exception;
    }

    /// <summary>Which action failed.</summary>
    public WebhookActionKind Action { get; }

    /// <summary>The endpoint (null for a new endpoint that was not added).</summary>
    public WebhookEndpoint? Endpoint { get; }

    /// <summary>The failed attempt that was added to the list (a retry or a test), or null.</summary>
    public WebhookDelivery? Delivery { get; }

    /// <summary>What the action threw; null when it returned a failed delivery instead.</summary>
    public Exception? Exception { get; }
}

/// <summary>Arguments of <c>Webhooks.EndpointCreated</c>.</summary>
public class WebhookEndpointEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookEndpointEventArgs(RoutedEvent routedEvent, object source, WebhookEndpoint endpoint)
        : base(routedEvent, source)
    {
        Endpoint = endpoint;
    }

    /// <summary>The endpoint that was added.</summary>
    public WebhookEndpoint Endpoint { get; }
}

/// <summary>Arguments of <c>Webhooks.ContentCopied</c>.</summary>
public class WebhookContentCopiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookContentCopiedEventArgs(RoutedEvent routedEvent, object source, WebhookCopiedContent content, string text)
        : base(routedEvent, source)
    {
        Content = content;
        Text = text;
    }

    /// <summary>Which tab was copied.</summary>
    public WebhookCopiedContent Content { get; }

    /// <summary>The copied text.</summary>
    public string Text { get; }
}

/// <summary>Arguments of <c>Webhooks.AddPanelClosed</c>.</summary>
public class WebhookAddPanelClosedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WebhookAddPanelClosedEventArgs(RoutedEvent routedEvent, object source, WebhookAddCloseReason reason)
        : base(routedEvent, source)
    {
        Reason = reason;
    }

    /// <summary>What closed the panel.</summary>
    public WebhookAddCloseReason Reason { get; }
}
