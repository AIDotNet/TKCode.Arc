namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Invented search results for the source cards demos: made-up sites on the reserved <c>.example</c> domain with made-up
/// passages. None of the pages exist.
/// </summary>
internal static class SourceCardsDemoData
{
    // The simulated search of the Preview ------------------------------------------------------------------------------

    /// <summary>The query the simulated search runs.</summary>
    public const string SimulatedQuery = "webhook signature verification";

    /// <summary>The query of the simulated empty search.</summary>
    public const string SimulatedEmptyQuery = "webhook retries on the lunar billing api";

    /// <summary>Why a search fails (the simulated one and the reader demo's).</summary>
    public const string ErrorText = "The search provider timed out after 8s.";

    /// <summary>The search time the simulated search reports.</summary>
    public static readonly TimeSpan SimulatedDuration = TimeSpan.FromMilliseconds(2100);

    /// <summary>The answer the simulated search streams; [n] cites source n.</summary>
    public const string SimulatedAnswer =
        "Read the raw body with request.text() before parsing it [1] [2]. " +
        "Compute an HMAC-SHA256 of the timestamp and body with your endpoint secret, then compare it with the header in constant time [3] [6]. " +
        "Most mismatches come from a framework parsing the JSON first [4]; on edge runtimes, turn off body parsing for that route [5].";

    /// <summary>Six pages about verifying webhook signatures, in citation order.</summary>
    public static readonly IReadOnlyList<SourceCardsSource> SimulatedSources =
    [
        new("Verify webhook signatures", "https://docs.ledgerpay.example/webhooks/signatures")
        {
            Site = "Ledgerpay Docs",
            Published = "Oct 9, 2026",
            Snippet = "Compute an HMAC with SHA-256 over the timestamp, a period and the raw request body, using the endpoint's signing secret, and compare it with the signature in the Ledgerpay-Signature header."
        },
        new("Request: text() method", "https://webref.example/api/request/text")
        {
            Site = "WebRef",
            Published = "Aug 30, 2026",
            Snippet = "The text() method of the Request interface reads the request stream to completion and returns a promise that resolves with the body as a string, exactly as it was sent."
        },
        new("Constant-time comparison for HMAC digests", "https://cryptonotes.example/posts/constant-time-compare")
        {
            Site = "Crypto Notes",
            Published = "Jun 2, 2026",
            Snippet = "Comparing digests with == stops at the first different byte, which leaks timing. Use a comparison whose time does not depend on where the inputs differ."
        },
        new("Why does my webhook signature never match?", "https://devanswers.example/q/48213")
        {
            Site = "DevAnswers",
            Published = "Mar 14, 2026",
            Snippet = "Your framework parses the JSON before your handler runs, so you sign a re-serialized body with different spacing. Read the raw bytes for this route and verify those."
        },
        new("Reading the raw body in edge functions", "https://edgehost.example/docs/functions/raw-body")
        {
            Site = "Edgehost Docs",
            Published = "Sep 21, 2026",
            Snippet = "Edge functions receive a standard Request. Call request.text() once, verify the signature against that string, then JSON.parse it yourself."
        },
        new("HMAC", "https://wikiref.example/wiki/HMAC")
        {
            Site = "WikiRef",
            Published = "Jul 7, 2026",
            Snippet = "A hash-based message authentication code combines a cryptographic hash function with a secret key to verify both the integrity and the authenticity of a message."
        }
    ];

    // The app-driven demos ---------------------------------------------------------------------------------------------

    public const string Query = "postgres connection pool size for serverless";

    /// <summary>Seven results in citation order (a fresh copy each call).</summary>
    public static SourceCardsSource[] Sources() =>
    [
        new("Connection pooling in serverless functions", "https://docs.edgehost.example/guides/postgres-pooling")
        {
            Site = "Edgehost Docs",
            Published = "Sep 30, 2026",
            Snippet = "Every function instance opens its own connections. Put a pooler between your functions and the database and keep each instance to one or two connections."
        },
        new("max_connections and memory", "https://pgnotes.example/config/max-connections")
        {
            Site = "PG Notes",
            Published = "Jul 12, 2026",
            Snippet = "Each connection costs a backend process and its working memory. Raising max_connections past a few hundred usually slows the server down instead of serving more clients."
        },
        new("Transaction mode versus session mode", "https://poolkeeper.example/docs/modes")
        {
            Site = "Poolkeeper",
            Published = "Aug 3, 2026",
            Snippet = "In transaction mode a server connection returns to the pool when each transaction ends, so a few dozen connections can serve thousands of short-lived clients."
        },
        new("Why do my lambdas exhaust the database?", "https://devanswers.example/q/51870")
        {
            Site = "DevAnswers",
            Published = "May 18, 2026",
            Snippet = "A burst of cold starts opens a burst of connections. Without a pooler the database runs out of slots long before it runs out of CPU."
        },
        new("Sizing a pool: a worked example", "https://blog.querylane.example/pool-sizing")
        {
            Site = "Querylane Blog",
            Published = "Oct 2, 2026",
            Snippet = "Start from cores × 2 plus the number of disks, measure, then adjust. More connections than the server can run at once only add queueing inside the database."
        },
        new("Prepared statements behind a pooler", "https://poolkeeper.example/docs/prepared-statements")
        {
            Site = "Poolkeeper",
            Published = "Aug 3, 2026",
            Snippet = "Named prepared statements belong to one server connection. In transaction mode, turn them off in the driver or use a pooler that tracks them."
        },
        new("Idle timeouts", "https://pgnotes.example/config/idle-timeouts")
        {
            Site = "PG Notes",
            Published = "Jul 12, 2026",
            Snippet = "Set idle_in_transaction_session_timeout so a client that forgot to commit cannot hold a connection forever."
        }
    ];
}
