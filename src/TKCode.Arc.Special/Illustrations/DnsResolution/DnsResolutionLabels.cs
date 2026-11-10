using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The chapters of a <see cref="DnsResolution"/> story.</summary>
public enum DnsResolutionStep
{
    /// <summary>Nothing has happened yet.</summary>
    Ready,

    /// <summary>The browser asks its resolver, which finds nothing in its cache.</summary>
    Query,

    /// <summary>A root server refers the resolver to the top-level domain's servers.</summary>
    Root,

    /// <summary>The top-level domain's server refers the resolver to the domain's own name servers.</summary>
    Tld,

    /// <summary>The domain's authoritative server answers with the address.</summary>
    Authoritative,

    /// <summary>The resolver caches the answer and hands it to the browser.</summary>
    Answer,

    /// <summary>A second lookup is answered from the resolver's cache.</summary>
    Cached,

    /// <summary>The story has ended.</summary>
    Done
}

/// <summary>The parts of the <see cref="DnsResolution"/> diagram.</summary>
public enum DnsResolutionNode
{
    /// <summary>The browser that wants the address.</summary>
    Client,

    /// <summary>The recursive resolver that does the asking (with its cache).</summary>
    Resolver,

    /// <summary>A root name server.</summary>
    Root,

    /// <summary>A name server of the top-level domain.</summary>
    Tld,

    /// <summary>A name server that is authoritative for the domain.</summary>
    Authoritative
}

/// <summary>Arguments of <see cref="DnsResolution.StepChanged"/>.</summary>
public sealed class DnsResolutionStepChangedEventArgs : RoutedEventArgs
{
    internal DnsResolutionStepChangedEventArgs(RoutedEvent routedEvent, object source, DnsResolutionStep step)
        : base(routedEvent, source)
    {
        Step = step;
    }

    /// <summary>The chapter the story entered.</summary>
    public DnsResolutionStep Step { get; }
}

/// <summary>
/// Every word a <see cref="DnsResolution"/> shows or announces, in English by default (the playback buttons are named by
/// <see cref="Explainer.PlaybackLabels"/>). Replace the instance to translate or reword; texts with names or numbers are
/// functions so the word order can change.
/// </summary>
public sealed class DnsResolutionLabels
{
    /// <summary>The status before the story starts.</summary>
    public string Ready { get; set; } = "Ready";

    /// <summary>The status while the story plays or steps.</summary>
    public string Resolving { get; set; } = "Resolving";

    /// <summary>The status while paused.</summary>
    public string Paused { get; set; } = "Paused";

    /// <summary>The status once the story has ended.</summary>
    public string Resolved { get; set; } = "Resolved";

    /// <summary>The words before the speed control, also its accessible name.</summary>
    public string Speed { get; set; } = "Speed";

    /// <summary>The browser's name in the diagram.</summary>
    public string Client { get; set; } = "Browser";

    /// <summary>The recursive resolver's name.</summary>
    public string Resolver { get; set; } = "Resolver";

    /// <summary>The note under the resolver's name.</summary>
    public string ResolverNote { get; set; } = "recursive";

    /// <summary>The root server's name.</summary>
    public string Root { get; set; } = "Root server";

    /// <summary>The top-level domain server's name.</summary>
    public string Tld { get; set; } = "TLD server";

    /// <summary>The authoritative server's name.</summary>
    public string Authoritative { get; set; } = "Authoritative";

    /// <summary>The cache box's heading.</summary>
    public string Cache { get; set; } = "Cache";

    /// <summary>The cache box while it holds nothing for the domain.</summary>
    public string CacheEmpty { get; set; } = "Empty";

    /// <summary>The readout of round trips the resolver made.</summary>
    public string RoundTrips { get; set; } = "Round trips";

    /// <summary>The readout of the time the lookup took.</summary>
    public string LookupTime { get; set; } = "Lookup time";

    /// <summary>The question every query asks; the argument is the domain.</summary>
    public Func<string, string> Ask { get; set; } = domain => $"Where is {domain}?";

    /// <summary>The resolver's note when its cache has nothing.</summary>
    public string NotCached { get; set; } = "Not in my cache";

    /// <summary>The resolver's note when its cache has the answer.</summary>
    public string FoundInCache { get; set; } = "Found in my cache";

    /// <summary>The root server's referral; the argument is the top-level domain with its dot (".dev").</summary>
    public Func<string, string> ReferToTld { get; set; } = tld => $"Ask the {tld} servers";

    /// <summary>The top-level domain server's referral; the argument is the domain's name server.</summary>
    public Func<string, string> ReferToZone { get; set; } = server => $"Ask {server}";

    /// <summary>The authoritative answer; the arguments are the address and the time to live in seconds.</summary>
    public Func<string, int, string> Answer { get; set; } = (address, ttl) => $"{address}, keep it {ttl} s";

    /// <summary>The caption before the story starts; the argument is the domain.</summary>
    public Func<string, string> ReadyCaption { get; set; } =
        domain => $"How a browser finds the address of {domain}, one name server at a time.";

    /// <summary>The caption of the first chapter; the argument is the domain.</summary>
    public Func<string, string> QueryCaption { get; set; } =
        domain => $"The browser asks its recursive resolver for {domain}. Its cache is empty, so the resolver starts at the top.";

    /// <summary>The caption of the root chapter; the argument is the top-level domain with its dot.</summary>
    public Func<string, string> RootCaption { get; set; } =
        tld => $"A root server does not know the address, but it knows who runs {tld} and sends the resolver there.";

    /// <summary>The caption of the top-level domain chapter; the arguments are the top-level domain and the domain's zone.</summary>
    public Func<string, string, string> TldCaption { get; set; } =
        (tld, zone) => $"The {tld} server does not know it either. It names the servers that hold the records for {zone}.";

    /// <summary>The caption of the authoritative chapter; the arguments are the domain and the address.</summary>
    public Func<string, string, string> AuthoritativeCaption { get; set; } =
        (domain, address) => $"The authoritative server owns the record: {domain} is at {address}.";

    /// <summary>The caption of the answer chapter; the arguments are the address and the time to live in seconds.</summary>
    public Func<string, int, string> AnswerCaption { get; set; } =
        (address, ttl) => $"The resolver keeps the answer for {ttl} seconds and hands {address} to the browser.";

    /// <summary>The caption of the cached chapter.</summary>
    public string CachedCaption { get; set; } = "Asked again, the resolver answers from its cache, with no trip to the name servers.";

    /// <summary>The caption at the end; the arguments are the first lookup's and the cached lookup's time in milliseconds.</summary>
    public Func<int, int, string> DoneCaption { get; set; } =
        (first, cached) => cached > 0
            ? $"The first lookup took {first} ms over four round trips. From the cache it took {cached} ms."
            : $"The first lookup took {first} ms over four round trips. Until the answer expires, the cache answers at once.";
}
