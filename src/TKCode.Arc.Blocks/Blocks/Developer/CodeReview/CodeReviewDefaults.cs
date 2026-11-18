namespace TKCode.Arc;

/// <summary>
/// The sample review <see cref="CodeReview"/> shows when no data is set: a pull request that rate limits outgoing email
/// per workspace, with five files (a new one, two changed sources, their test and a doc), a resolved conversation and an
/// open one with a one-line suggestion.
/// </summary>
internal static class CodeReviewDefaults
{
    public static readonly CodeReviewPerson Author = new("Jonah Pierce");

    public static readonly CodeReviewPerson Viewer = new("Mia Laurent");

    private static readonly CodeReviewPerson s_sofia = new("Sofia Marin");
    private static readonly CodeReviewPerson s_ravi = new("Ravi Patel");

    public static CodeReviewPullRequest Pull() => new()
    {
        Title = "Rate limit outgoing email per workspace",
        Number = 3172,
        Author = Author,
        BaseBranch = "main",
        HeadBranch = "jonah/mail-rate-limit",
        CommitCount = 3
    };

    public static List<CodeReviewFile> Files() =>
    [
        new CodeReviewFile
        {
            Id = "limiter",
            Path = "src/mail/limiter.ts",
            Language = "ts",
            Status = CodeReviewFileStatus.Added,
            Lines = Lines("limiter",
                H(0, 1),
                A("export const WINDOW_MS = 60_000;"),
                A("const DEFAULT_LIMIT = 120;"),
                A(""),
                A("/**"),
                A(" * One token bucket per workspace, refilled every window, so a busy"),
                A(" * tenant can never starve the shared mail relay."),
                A(" */"),
                A("export function takeToken(bucket: Bucket, now = Date.now()) {"),
                A("  const refills = Math.floor((now - bucket.since) / WINDOW_MS);"),
                A("  return bucket.tokens + refills * DEFAULT_LIMIT > 0;"),
                A("}"))
        },
        new CodeReviewFile
        {
            Id = "send",
            Path = "src/mail/send.ts",
            Language = "ts",
            Status = CodeReviewFileStatus.Modified,
            Lines = Lines("send",
                H(1, 1),
                C("import { renderTemplate } from \"./templates\";"),
                A("import { takeToken, WINDOW_MS } from \"./limiter\";"),
                A("import { bucketFor } from \"./buckets\";"),
                C("import type { Message, Workspace } from \"./types\";"),
                C("import { relay } from \"../relay\";"),
                H(18, 20, "export async function send("),
                C("export async function send(message: Message, workspace: Workspace) {"),
                C("  const html = renderTemplate(message.template, message.data);"),
                A("  const bucket = await bucketFor(workspace.id);"),
                A(""),
                A("  if (!takeToken(bucket)) {"),
                A("    await relay.defer(message, { after: WINDOW_MS });"),
                A("    return { sent: false, deferred: true };"),
                A("  }"),
                C(""),
                C("  const result = await relay.post({"),
                C("    to: message.to,"),
                C("    subject: message.subject,"),
                C("    html,"),
                A("    headers: { \"x-workspace\": workspace.id },"),
                C("  });"),
                C(""),
                D("  if (result.status !== 202) {"),
                D("    return { sent: false };"),
                D("  }"),
                D("  return { sent: true };"),
                A("  return { sent: result.status === 202, deferred: false };"),
                C("}"))
        },
        new CodeReviewFile
        {
            Id = "test",
            Path = "src/mail/send.test.ts",
            Language = "ts",
            Status = CodeReviewFileStatus.Modified,
            Lines = Lines("test",
                H(12, 12, "describe(\"send\", () => {"),
                C("  it(\"posts the rendered message to the relay\", async () => {"),
                C("    mockRelay(202);"),
                D("    expect(await send(message, workspace)).toEqual({ sent: true });"),
                A("    expect(await send(message, workspace)).toEqual({ sent: true, deferred: false });"),
                C("  });"),
                A(""),
                A("  it(\"defers a message when the bucket is empty\", async () => {"),
                A("    emptyBucket(workspace.id);"),
                A("    const result = await send(message, workspace);"),
                A("    expect(result).toEqual({ sent: false, deferred: true });"),
                A("    expect(relay.defer).toHaveBeenCalledWith(message, { after: WINDOW_MS });"),
                A("  });"),
                A(""),
                A("  it(\"never posts while deferred\", async () => {"),
                A("    emptyBucket(workspace.id);"),
                A("    await send(message, workspace);"),
                A("    expect(relay.post).not.toHaveBeenCalled();"),
                A("  });"),
                C("});"))
        },
        new CodeReviewFile
        {
            Id = "limits",
            Path = "src/config/limits.ts",
            Language = "ts",
            Status = CodeReviewFileStatus.Modified,
            Lines = Lines("limits",
                H(6, 6, "export const limits = {"),
                C("export const limits = {"),
                D("  mail: { perMinute: 60, burst: 10 },"),
                A("  mail: { perMinute: 120, burst: 10, perWorkspace: true },"),
                C("  sms: { perMinute: 30, burst: 5 },"),
                C("  push: { perMinute: 600 },"),
                C("};"))
        },
        new CodeReviewFile
        {
            Id = "docs",
            Path = "docs/email.md",
            Language = "md",
            Status = CodeReviewFileStatus.Modified,
            Lines = Lines("docs",
                H(27, 27, "## Sending limits"),
                C("## Sending limits"),
                C(""),
                D("Every workspace shares one limit of 60 messages a minute."),
                A("Each workspace gets its own bucket of 120 messages a minute. When it"),
                A("runs out, messages wait for the next window instead of failing, and"),
                A("the workspace owner sees the delay in the activity log."))
        }
    ];

    public static List<CodeReviewThread> Threads()
    {
        var now = DateTimeOffset.Now;
        return
        [
            new CodeReviewThread
            {
                Id = "bucket-scope",
                FileId = "limiter",
                AnchorLineId = "limiter-7",
                Label = "Lines 4–7",
                IsResolved = true,
                Comments =
                [
                    new CodeReviewComment
                    {
                        Author = s_sofia,
                        Text = "Would a bucket per sender be safer than one per workspace?",
                        CreatedAt = now.AddMinutes(-52)
                    },
                    new CodeReviewComment
                    {
                        Author = Author,
                        Text = "Per workspace is enough for now. Sender limits arrive with the abuse rules in the next pull request.",
                        CreatedAt = now.AddMinutes(-41)
                    }
                ]
            },
            new CodeReviewThread
            {
                Id = "refill-cap",
                FileId = "limiter",
                AnchorLineId = "limiter-9",
                Label = "Line 9",
                Comments =
                [
                    new CodeReviewComment
                    {
                        Author = s_ravi,
                        Text = "A workspace that sends nothing overnight comes back with hundreds of windows of refills. Can we cap it at one window so a quiet tenant cannot fire a burst?",
                        CreatedAt = now.AddMinutes(-38)
                    }
                ],
                Suggestion = new CodeReviewSuggestion
                {
                    LineIds = ["limiter-9"],
                    NewLines = ["  const refills = Math.min(Math.floor((now - bucket.since) / WINDOW_MS), 1);"]
                }
            }
        ];
    }

    // Lines get ids "<file>-<n>" in order (hunk headers keep theirs), so threads can point at them.
    private static List<CodeReviewDiffLine> Lines(string file, params Func<string, CodeReviewDiffLine>[] makers)
    {
        var lines = new List<CodeReviewDiffLine>(makers.Length);
        int n = 0, hunks = 0;
        foreach (var make in makers)
        {
            var probe = make(file);
            lines.Add(probe.Kind == CodeReviewLineKind.Hunk ? make($"{file}-h{++hunks}") : make($"{file}-{++n}"));
        }
        return lines;
    }

    private static Func<string, CodeReviewDiffLine> H(int oldStart, int newStart, string? section = null) =>
        id => CodeReviewDiffLine.Hunk(id, oldStart, newStart, section);

    private static Func<string, CodeReviewDiffLine> A(string text) => id => CodeReviewDiffLine.Added(id, text);

    private static Func<string, CodeReviewDiffLine> C(string text) => id => CodeReviewDiffLine.Context(id, text);

    private static Func<string, CodeReviewDiffLine> D(string text) => id => CodeReviewDiffLine.Deleted(id, text);
}
