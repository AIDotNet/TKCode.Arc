using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// A reply streamed into a real <see cref="Markdown"/> control, with the raw text it has received so far beside it. It
/// starts when it first comes into view and runs once; under reduced motion the reply appears whole. No timer runs while
/// the stage is out of the visual tree.
/// </summary>
internal sealed class GalleryStreamStage : GalleryTwoPane
{
    private static readonly TimeSpan s_tick = TimeSpan.FromMilliseconds(40);

    private readonly Markdown _message = new() { MinHeight = 300 };
    private readonly TextBlock _chars = GalleryUi.Mono("0", ArcDesignTokenKind.Foreground, 18);
    private readonly TextBlock _raw = GalleryUi.Mono(string.Empty, ArcDesignTokenKind.TextSecondary, 11.5);
    private readonly GallerySegmented _speed = new(GalleryShellLangResourceKind.StreamSlow, GalleryShellLangResourceKind.StreamNormal, GalleryShellLangResourceKind.StreamFast);
    private readonly GalleryFirstSight _sight;
    private DispatcherTimer? _timer;
    private string _reply = string.Empty;
    private string? _language;
    private int _position;
    private int _chunk = 6;
    private Random _random = new(7);

    public GalleryStreamStage()
    {
        _raw.TextWrapping = TextWrapping.Wrap;
        _raw.Height = 120;
        _raw.ClipToBounds = true;
        _speed.SelectedIndex = 1;
        _speed.SelectionChanged += index => _chunk = index switch { 0 => 2, 1 => 6, _ => 24 };

        Children.Add(GallerySite.Panel(_message));
        Children.Add(GallerySite.Panel(BuildSide(), ArcDesignTokenKind.SurfaceMuted, 16));

        _sight = new GalleryFirstSight(this, Start);
        DetachedFromVisualTree += (_, _) => _timer?.Stop();
        AttachedToVisualTree += (_, _) =>
        {
            if (_position > 0 && _position < _reply.Length)
            {
                _timer?.Start();
            }
        };
        // The text belongs to the language: a new language plays the reply again, if it has played. Coming back to the page
        // with the same language keeps what is there.
        GalleryText.Track(this, () =>
        {
            if (_sight.IsSeen && _language != GalleryText.CurrentLanguage)
            {
                Start();
            }
        });
    }

    /// <summary>The Markdown control the reply is streamed into.</summary>
    public Markdown Message => _message;

    /// <summary>The whole reply in the current language.</summary>
    public static string BuildReply()
    {
        string T(GalleryShellLangResourceKind kind) => GalleryText.Get(kind);
        return $$"""
            {{T(GalleryShellLangResourceKind.StreamReplyIntro)}}

            {{T(GalleryShellLangResourceKind.StreamReplyBody)}}

            ```cs
            markdown.Append(chunk);   // as tokens arrive
            markdown.Complete();      // when the reply ends
            ```

            | {{T(GalleryShellLangResourceKind.StreamReplyTableStep)}} | {{T(GalleryShellLangResourceKind.StreamReplyTableCall)}} |
            |------|------|
            | {{T(GalleryShellLangResourceKind.StreamReplyStart)}} | `Append("...")` |
            | {{T(GalleryShellLangResourceKind.StreamReplyFinish)}} | `Complete()` |

            {{T(GalleryShellLangResourceKind.StreamReplyOutro)}}
            """;
    }

    private Control BuildSide()
    {
        var count = new StackPanel { Spacing = 2 };
        count.Children.Add(_chars);
        count.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.StreamChars, ArcDesignTokenKind.TextMuted, 12));

        var raw = new StackPanel { Spacing = 6 };
        raw.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.StreamRaw, ArcDesignTokenKind.TextMuted, 12));
        raw.Children.Add(_raw);

        var replay = GallerySite.Action(GalleryShellLangResourceKind.StreamReplay, ButtonVariant.Secondary, Start, ArcIcons.RefreshCw, ControlSize.Small);
        var skip = GallerySite.Action(GalleryShellLangResourceKind.StreamSkip, ButtonVariant.Ghost, Finish, null, ControlSize.Small);

        var side = new StackPanel { Spacing = 14 };
        side.Children.Add(count);
        side.Children.Add(raw);
        side.Children.Add(_speed);
        side.Children.Add(GallerySite.Actions(replay, skip));
        return side;
    }

    /// <summary>Plays the reply from the start.</summary>
    public void Start()
    {
        _timer?.Stop();
        _reply = BuildReply();
        _language = GalleryText.CurrentLanguage;
        _position = 0;
        _random = new Random(7);
        _message.Text = string.Empty;
        _raw.Text = string.Empty;
        _chars.Text = "0";
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            Finish();
            return;
        }
        _timer ??= new DispatcherTimer(s_tick, DispatcherPriority.Normal, (_, _) => Step());
        _timer.Start();
    }

    /// <summary>Shows the rest of the reply at once.</summary>
    public void Finish()
    {
        _timer?.Stop();
        if (_reply.Length == 0)
        {
            _reply = BuildReply();
        }
        if (_position < _reply.Length)
        {
            Push(_reply.Length - _position);
        }
        _message.Complete();
    }

    private void Step()
    {
        if (_position >= _reply.Length)
        {
            _timer?.Stop();
            _message.Complete();
            return;
        }
        Push(Math.Min(_reply.Length - _position, _random.Next(_chunk, _chunk * 2)));
    }

    private void Push(int length)
    {
        _message.Append(_reply.Substring(_position, length));
        _position += length;
        _chars.Text = _position.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _raw.Text = _reply[Math.Max(0, _position - 170).._position] + "▍";
    }
}
