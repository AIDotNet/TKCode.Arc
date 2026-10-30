using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A longer file in a capped code area.</summary>
public partial class CodeSuggestionLongFileDemo : UserControl
{
    public CodeSuggestionLongFileDemo()
    {
        InitializeComponent();
        Card.FileName = "settings.py";
        Card.FilePath = "app/core";
        Card.Instruction = "Read the timeouts from the environment";
        Card.Code = string.Join('\n',
        [
            "\"\"\"Runtime settings for the worker.\"\"\"",
            "",
            "import os",
            "",
            "QUEUE_URL = os.environ[\"QUEUE_URL\"]",
            "BATCH_SIZE = 50",
            "",
            "# Seconds before a job is retried.",
            "RETRY_AFTER = 30",
            "",
            "# Seconds before an HTTP call gives up.",
            "HTTP_TIMEOUT = 10",
            "",
            "LOG_LEVEL = \"info\"",
            "SENTRY_DSN = None",
            "",
            "def describe() -> str:",
            "    return f\"batch={BATCH_SIZE} retry={RETRY_AFTER}s timeout={HTTP_TIMEOUT}s\"",
        ]);
        Card.Hunks.Add(new CodeSuggestionHunk("retry", 8, 1, "RETRY_AFTER = int(os.environ.get(\"RETRY_AFTER\", \"30\"))")
        {
            Summary = "Read the retry delay",
        });
        Card.Hunks.Add(new CodeSuggestionHunk("timeout", 11, 1, "HTTP_TIMEOUT = float(os.environ.get(\"HTTP_TIMEOUT\", \"10\"))")
        {
            Summary = "Read the HTTP timeout",
        });
    }
}
