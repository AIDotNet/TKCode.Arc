using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>DictationButton with an app recognizer, an app model label, a real send task and an event log.</summary>
public partial class DictationButtonYourRecognizerDemo : UserControl
{
    private readonly List<string> _log = new();

    public DictationButtonYourRecognizerDemo()
    {
        InitializeComponent();
        Composer.Recognizer = new StandInRecognizer();
        Composer.Model = new DictationModel(AiModelCatalog.ClaudeHaiku55.Name, ArcIcons.Brain);
        Composer.StatusChanged += (_, e) => Write(e.Error is { } error ? $"Status: {e.Status} ({error})" : $"Status: {e.Status}");
        Composer.Committed += (_, e) => Write($"Inserted {e.WordCount} words");
        Composer.SendRequested += (_, e) =>
        {
            // Your request goes here; the send button spins until it completes and the field clears after.
            e.Completion = Task.Delay(1200);
            Write($"Sending \"{e.Text}\"");
        };
        Write("Press the mic, or tab to it and press Enter.");
    }

    private void Write(string line)
    {
        _log.Add(line);
        if (_log.Count > 5)
        {
            _log.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _log);
    }

    /// <summary>
    /// A stand-in for a platform recognizer or a speech service: it reports levels and a growing guess, then settles.
    /// A real adapter opens the microphone in StartAsync and releases it when the token is cancelled or StopAsync runs.
    /// </summary>
    private sealed class StandInRecognizer : IDictationRecognizer
    {
        private static readonly string[] s_words = "Can you check whether the refund for this order went out".Split(' ');

        public async Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken)
        {
            await Task.Delay(300, cancellationToken);
            var session = new Session(handlers, cancellationToken);
            _ = session.RunAsync();
            return session;
        }

        private sealed class Session(DictationHandlers handlers, CancellationToken token) : IDictationSession
        {
            private int _heard;
            private bool _stopped;

            public async Task RunAsync()
            {
                var random = new Random(7);
                try
                {
                    while (_heard < s_words.Length && !_stopped)
                    {
                        for (var i = 0; i < 6; i++)
                        {
                            handlers.OnLevel(0.25 + random.NextDouble() * 0.6);
                            await Task.Delay(40, token);
                        }
                        _heard++;
                        handlers.OnTranscript(string.Empty, string.Join(' ', s_words, 0, _heard));
                    }
                    handlers.OnLevel(0.03);
                    handlers.OnSpeechEnd();
                }
                catch (OperationCanceledException)
                {
                }
            }

            public async Task<string> StopAsync()
            {
                _stopped = true;
                await Task.Delay(400);
                return string.Join(' ', s_words, 0, _heard);
            }
        }
    }
}
