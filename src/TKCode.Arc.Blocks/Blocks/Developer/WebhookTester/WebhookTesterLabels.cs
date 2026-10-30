namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="WebhookTester"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with values are functions so that word order can change.
/// </summary>
public sealed class WebhookTesterLabels
{
    /// <summary>The title of the card.</summary>
    public string Title { get; init; } = "Webhook tester";

    /// <summary>The line under the title; the argument is the source's name.</summary>
    public Func<string, string> Subtitle { get; init; } = source => $"Signed test events from {source}";

    /// <summary>The badge at the right of the header.</summary>
    public string TestMode { get; init; } = "Test mode";

    /// <summary>The label of the endpoint field.</summary>
    public string Endpoint { get; init; } = "Endpoint";

    /// <summary>The error under an endpoint that is not https (or http on localhost).</summary>
    public string EndpointInvalid { get; init; } = "Use an https URL (http is allowed for localhost).";

    /// <summary>The label of the event list.</summary>
    public string Event { get; init; } = "Event";

    /// <summary>The label of the signing secret.</summary>
    public string Secret { get; init; } = "Secret";

    /// <summary>The reveal toggle's text while the secret is masked.</summary>
    public string Hidden { get; init; } = "Hidden";

    /// <summary>The reveal toggle's text while the secret shows.</summary>
    public string Shown { get; init; } = "Shown";

    /// <summary>The accessible name of the reveal toggle.</summary>
    public string RevealName { get; init; } = "Reveal signing secret";

    /// <summary>The accessible name of the secret's copy button.</summary>
    public string CopySecret { get; init; } = "Copy signing secret";

    /// <summary>The accessible name of the masked secret.</summary>
    public string SecretMasked { get; init; } = "Signing secret, hidden";

    /// <summary>The label of the payload editor; the argument is the event's type.</summary>
    public Func<string, string> Payload { get; init; } = type => $"Payload for {type}";

    /// <summary>The validity line under the editor; the argument is the byte count.</summary>
    public Func<int, string> ValidJson { get; init; } = bytes => $"Valid JSON · {bytes} bytes";

    /// <summary>The validity line while the payload does not parse; the arguments are the line and the parser's message.</summary>
    public Func<int, string, string> InvalidJson { get; init; } = (line, message) => $"Invalid JSON on line {line}: {message}";

    /// <summary>The validity line while the payload is empty.</summary>
    public string EmptyJson { get; init; } = "Payload is empty";

    /// <summary>The button that pretty prints the payload.</summary>
    public string Format { get; init; } = "Format";

    /// <summary>The name of the first key of the send hint on a Mac.</summary>
    public string SendHintCommand { get; init; } = "Command";

    /// <summary>The name of the first key of the send hint elsewhere.</summary>
    public string SendHintControl { get; init; } = "Ctrl";

    /// <summary>The name of the second key of the send hint.</summary>
    public string SendHintReturn { get; init; } = "Return";

    /// <summary>The words after the keys of the send hint.</summary>
    public string SendHintTail { get; init; } = "to send";

    /// <summary>The send button.</summary>
    public string Send { get; init; } = "Send test event";

    /// <summary>The send button while a request is in flight.</summary>
    public string Sending { get; init; } = "Sending";

    /// <summary>The first step of the timeline.</summary>
    public string StepSigned { get; init; } = "Signed";

    /// <summary>The detail of the first step.</summary>
    public string StepSignedDetail { get; init; } = "HMAC SHA-256";

    /// <summary>The second step of the timeline.</summary>
    public string StepSent { get; init; } = "Sent";

    /// <summary>The detail of the second step.</summary>
    public string StepSentDetail { get; init; } = "POST";

    /// <summary>The third step while nothing answered yet.</summary>
    public string StepResponse { get; init; } = "Response";

    /// <summary>The detail of the third step before an answer.</summary>
    public string StepResponseDetail { get; init; } = "Waiting";

    /// <summary>The third step's label for a status; the arguments are the status and its standard reason phrase.</summary>
    public Func<int, string, string> StepStatus { get; init; } = (status, reason) => $"{status} {reason}".Trim();

    /// <summary>The third step's label when nothing came back.</summary>
    public string StepNoAnswer { get; init; } = "No response";

    /// <summary>The third step's label when the endpoint timed out.</summary>
    public string StepTimedOut { get; init; } = "Timed out";

    /// <summary>The third step's detail; the argument is the duration in milliseconds.</summary>
    public Func<int, string> StepDuration { get; init; } = ms => $"{ms} ms";

    /// <summary>The tab with the response body.</summary>
    public string TabBody { get; init; } = "Body";

    /// <summary>The tab with the response headers.</summary>
    public string TabHeaders { get; init; } = "Headers";

    /// <summary>The tab with the request as it was sent.</summary>
    public string TabRequest { get; init; } = "Request";

    /// <summary>The accessible name of the tab list.</summary>
    public string TabsName { get; init; } = "Response details";

    /// <summary>The accessible name of the copy button over the response panel.</summary>
    public string CopyPanel { get; init; } = "Copy";

    /// <summary>The panel's text before anything was sent.</summary>
    public string NothingSent { get; init; } = "Send a test event to see what your endpoint answers.";

    /// <summary>The body tab's text when the response had no body.</summary>
    public string EmptyBody { get; init; } = "The response had no body.";

    /// <summary>The headers tab's text when the response had no headers.</summary>
    public string EmptyHeaders { get; init; } = "No response headers were returned.";

    /// <summary>The heading of the recent list.</summary>
    public string Recent { get; init; } = "Recent deliveries";

    /// <summary>The badge in the heading; the argument is how many deliveries failed. Not shown at 0.</summary>
    public Func<int, string> FailedCount { get; init; } = count => $"{count} failed";

    /// <summary>The list's text while there is no delivery.</summary>
    public string NoDeliveries { get; init; } = "No deliveries yet.";

    /// <summary>The retry button of a failed row.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The retry button while it runs.</summary>
    public string Retrying { get; init; } = "Retrying";

    /// <summary>The status chip of a delivery with no status and no timeout.</summary>
    public string ChipFailed { get; init; } = "Failed";

    /// <summary>The status chip of a delivery that timed out.</summary>
    public string ChipTimeout { get; init; } = "Timeout";

    /// <summary>The announcement while a send starts; the argument is the event's type.</summary>
    public Func<string, string> AnnounceSending { get; init; } = type => $"Sending {type}.";

    /// <summary>The announcement after a 2xx answer; the arguments are the event, the status and the duration.</summary>
    public Func<string, int, int, string> AnnounceSucceeded { get; init; } = (type, status, ms) => $"{type} delivered: {status} in {ms} ms.";

    /// <summary>The announcement after a failure; the arguments are the event and a short reason.</summary>
    public Func<string, string, string> AnnounceFailed { get; init; } = (type, reason) => $"{type} failed: {reason}.";

    /// <summary>The note added to announcements while <see cref="WebhookTester.SendAction"/> is null: no request left the app.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent.";

    /// <summary>The announcement when the payload cannot be sent because it does not parse.</summary>
    public string CannotSendInvalid { get; init; } = "The payload is not valid JSON.";

    /// <summary>The accessible name of a delivery row; the arguments are the event, the outcome and the time.</summary>
    public Func<string, string, string, string> DeliveryName { get; init; } = (type, outcome, time) => $"{type}, {outcome}, {time}";

    /// <summary>The accessible name of the list of deliveries.</summary>
    public string DeliveriesName { get; init; } = "Recent deliveries";
}
