using System.Diagnostics;
using System.Globalization;
using System.Text;
using Shouldly;

namespace TKCode.Arc.Tests.Video;

/// <summary>
/// WebVTT / SRT parser snapshots (design/components/video/video.md section 16): each input in <c>tests/Baselines/Video/</c>
/// has a reviewed <c>.txt</c> dump next to it. Set <c>ARC_UPDATE_BASELINES=1</c> to rewrite the dumps, then review the diff.
/// </summary>
public class CaptionsParserTests
{
    [Theory]
    [InlineData("vtt-basic.vtt")]
    [InlineData("vtt-bad.vtt")]
    [InlineData("vtt-bom-crlf.vtt")]
    [InlineData("vtt-header-only.vtt")]
    [InlineData("srt-basic.srt")]
    [InlineData("srt-empty-payload.srt")]
    public void Snapshot(string file)
    {
        var folder = BaselineFolder();
        var text = VideoCaptionParser.Decode(File.ReadAllBytes(Path.Combine(folder, file)), CultureInfo.GetCultureInfo("en-US"));
        var dump = Dump(VideoCaptionParser.Parse(text));
        var expectedPath = Path.Combine(folder, Path.ChangeExtension(file, ".txt"));
        if (Environment.GetEnvironmentVariable("ARC_UPDATE_BASELINES") == "1")
        {
            File.WriteAllText(expectedPath, dump, new UTF8Encoding(false));
            return;
        }
        File.Exists(expectedPath).ShouldBeTrue($"{expectedPath} is missing; run with ARC_UPDATE_BASELINES=1 and review it");
        dump.ShouldBe(File.ReadAllText(expectedPath).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Webvtt_detection_needs_the_header_word_alone()
    {
        // Not WebVTT, so read as SRT: the header is a stray block and the character reference stays literal.
        var srt = VideoCaptionParser.Parse("WEBVTTX\n\n00:01.000 --> 00:02.000\n&amp;");
        srt.Cues.Single().Text.ShouldBe("&amp;");
        srt.Warnings.ShouldBe(["line 1: expected a cue number or a timing line, block skipped"]);
        VideoCaptionParser.Parse("WEBVTT\n\n00:01.000 --> 00:02.000\n&amp;").Cues.Single().Text.ShouldBe("&");
        VideoCaptionParser.Parse("WEBVTT\n\n00:01.000 --> 00:02.000\nA").Cues.Single().Text.ShouldBe("A");
        VideoCaptionParser.Parse("﻿WEBVTT\tfoo\n\n00:01.000 --> 00:02.000\nA").Cues.Single().Text.ShouldBe("A");
    }

    [Fact]
    public void Plain_cue_has_one_plain_run()
    {
        var cue = VideoCaptionParser.Parse("WEBVTT\n\n00:01.000 --> 00:02.000\nJust text").Cues.Single();
        cue.Runs.ShouldBe([new VideoCueRun("Just text")]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("﻿")]
    [InlineData("WEBVTT")]
    [InlineData("-->")]
    [InlineData("WEBVTT\n\n-->\n-->")]
    [InlineData("1\n-->\n")]
    [InlineData("WEBVTT\n\n00:01.000 --> 00:02.000 line:-%,start position:% size:%% align:\n<<<>>>&#;&#x;&#xFFFFFFFFFF;&#0;</></b></v")]
    [InlineData("00:00:01,000 --> 00:00:02,000\n{\\an8\n<b")]
    [InlineData("WEBVTT\n\n99999999999:00:00.000 --> 99999999999:00:01.000\nhuge")]
    public void Never_throws_on_garbage(string text)
    {
        Should.NotThrow(() => VideoCaptionParser.Parse(text));
    }

    [Fact]
    public void Ten_thousand_cues_parse_quickly()
    {
        var builder = new StringBuilder("WEBVTT\n\n");
        for (var i = 0; i < 10_000; i++)
        {
            var start = TimeSpan.FromMilliseconds(i * 1500L);
            builder.Append(i).Append('\n')
                   .Append(CaptionTimestamps.Format(start)).Append(" --> ").Append(CaptionTimestamps.Format(start + TimeSpan.FromSeconds(1.2)))
                   .Append(" line:-1 align:center\n")
                   .Append("Cue number ").Append(i).Append(" with <b>bold</b> and <i>italic</i> &amp; text\nsecond line\n\n");
        }
        var text = builder.ToString();
        VideoCaptionParser.Parse(text).Cues.Count.ShouldBe(10_000);
        // Best of five: the first runs are JIT warm-up and the machine may be busy; the parser itself is far below the limit.
        var best = TimeSpan.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var watch = Stopwatch.StartNew();
            var result = VideoCaptionParser.Parse(text);
            watch.Stop();
            result.Cues.Count.ShouldBe(10_000);
            result.Warnings.ShouldBeEmpty();
            best = watch.Elapsed < best ? watch.Elapsed : best;
        }
        // A loose bound that still catches a quadratic slip (seconds at this size) without failing on a loaded CI machine;
        // unloaded it takes a few tens of milliseconds.
        best.TotalMilliseconds.ShouldBeLessThan(500);
    }

    [Theory]
    [InlineData("zh-CN", 54936)]
    [InlineData("zh-SG", 54936)]
    [InlineData("zh-Hans", 54936)]
    [InlineData("zh-TW", 950)]
    [InlineData("zh-HK", 950)]
    [InlineData("zh-Hant-TW", 950)]
    [InlineData("ja-JP", 932)]
    [InlineData("ko-KR", 949)]
    [InlineData("en-US", 1252)]
    [InlineData("fr-FR", 1252)]
    public void Legacy_code_page_follows_the_culture(string culture, int codePage)
    {
        VideoCaptionParser.LegacyCodePage(CultureInfo.GetCultureInfo(culture)).ShouldBe(codePage);
        VideoCaptionParser.LegacyEncoding(CultureInfo.GetCultureInfo(culture)).CodePage.ShouldBe(codePage);
    }

    [Theory]
    [InlineData("zh-CN", 54936, "1\n00:00:01,000 --> 00:00:02,000\n你好，世界！字幕\n")]
    [InlineData("zh-TW", 950, "1\n00:00:01,000 --> 00:00:02,000\n你好，世界！繁體字幕\n")]
    [InlineData("ja-JP", 932, "1\n00:00:01,000 --> 00:00:02,000\nこんにちは、字幕\n")]
    [InlineData("en-US", 1252, "1\n00:00:01,000 --> 00:00:02,000\nCafé déjà vu – “quoted”\n")]
    public void Legacy_bytes_decode_with_the_current_ui_culture(string culture, int codePage, string text)
    {
        var bytes = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(codePage)!.GetBytes(text);
        Should.Throw<DecoderFallbackException>(() => new UTF8Encoding(false, true).GetString(bytes));
        var saved = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            VideoCaptionParser.Decode(bytes).ShouldBe(text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = saved;
        }
        VideoCaptionParser.Parse(text).Cues.Single().Text.ShouldBe(text.Split('\n')[2]);
    }

    [Fact]
    public void Bom_decides_the_encoding()
    {
        const string text = "WEBVTT\n\n00:01.000 --> 00:02.000\n中文 ✓";
        var en = CultureInfo.GetCultureInfo("en-US");
        VideoCaptionParser.Decode([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)], en).ShouldBe(text);
        VideoCaptionParser.Decode([0xFF, 0xFE, .. Encoding.Unicode.GetBytes(text)], en).ShouldBe(text);
        VideoCaptionParser.Decode([0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(text)], en).ShouldBe(text);
        VideoCaptionParser.Decode(Encoding.UTF8.GetBytes(text), CultureInfo.GetCultureInfo("zh-CN")).ShouldBe(text);
        VideoCaptionParser.Decode([], en).ShouldBe(string.Empty);
    }

    [Fact]
    public void Chapters_lookup()
    {
        VideoCue[] chapters =
        [
            Cue(0, 10, "Intro"),
            Cue(10, 30, "Setup"),
            Cue(35, 60, "Demo"),
        ];
        VideoChapters.At(chapters, TimeSpan.FromSeconds(-1)).ShouldBeNull();
        VideoChapters.At(chapters, TimeSpan.Zero)!.Text.ShouldBe("Intro");
        VideoChapters.At(chapters, TimeSpan.FromSeconds(10))!.Text.ShouldBe("Setup");
        VideoChapters.At(chapters, TimeSpan.FromSeconds(32)).ShouldBeNull();
        VideoChapters.At(chapters, TimeSpan.FromSeconds(59.9))!.Text.ShouldBe("Demo");
        VideoChapters.At(chapters, TimeSpan.FromSeconds(60)).ShouldBeNull();
        VideoChapters.At([], TimeSpan.Zero).ShouldBeNull();

        VideoChapters.NextStart(chapters, TimeSpan.FromSeconds(-5)).ShouldBe(TimeSpan.Zero);
        VideoChapters.NextStart(chapters, TimeSpan.Zero).ShouldBe(TimeSpan.FromSeconds(10));
        VideoChapters.NextStart(chapters, TimeSpan.FromSeconds(12)).ShouldBe(TimeSpan.FromSeconds(35));
        VideoChapters.NextStart(chapters, TimeSpan.FromSeconds(35)).ShouldBeNull();

        VideoChapters.PreviousStart(chapters, TimeSpan.FromSeconds(-5)).ShouldBeNull();
        VideoChapters.PreviousStart(chapters, TimeSpan.FromSeconds(5)).ShouldBe(TimeSpan.Zero);
        VideoChapters.PreviousStart(chapters, TimeSpan.FromSeconds(0.5)).ShouldBe(TimeSpan.Zero);
        VideoChapters.PreviousStart(chapters, TimeSpan.FromSeconds(20)).ShouldBe(TimeSpan.FromSeconds(10));
        VideoChapters.PreviousStart(chapters, TimeSpan.FromSeconds(10.5)).ShouldBe(TimeSpan.Zero);
        VideoChapters.PreviousStart(chapters, TimeSpan.FromSeconds(32)).ShouldBe(TimeSpan.FromSeconds(10));
        VideoChapters.PreviousStart(chapters, TimeSpan.FromSeconds(35.2)).ShouldBe(TimeSpan.FromSeconds(10));
    }

    internal static VideoCue Cue(double start, double end, string text) =>
        new() { Start = TimeSpan.FromSeconds(start), End = TimeSpan.FromSeconds(end), Text = text, Runs = [new VideoCueRun(text)] };

    private static string Dump(VideoCaptionParseResult result)
    {
        var b = new StringBuilder();
        b.Append("# warnings (").Append(result.Warnings.Count).Append(")\n");
        foreach (var warning in result.Warnings)
        {
            b.Append(warning).Append('\n');
        }
        b.Append("# cues (").Append(result.Cues.Count).Append(")\n");
        foreach (var cue in result.Cues)
        {
            b.Append(CaptionTimestamps.Format(cue.Start)).Append(" --> ").Append(CaptionTimestamps.Format(cue.End));
            if (cue.Id is not null)
            {
                b.Append(" id=").Append(Quote(cue.Id));
            }
            if (cue.Line is { } line)
            {
                b.Append(" line=").Append(line.ToString(CultureInfo.InvariantCulture)).Append(cue.LineIsPercent ? "%" : string.Empty);
            }
            if (cue.Position is { } position)
            {
                b.Append(" position=").Append(position.ToString(CultureInfo.InvariantCulture)).Append('%');
            }
            if (cue.Size != 100)
            {
                b.Append(" size=").Append(cue.Size.ToString(CultureInfo.InvariantCulture)).Append('%');
            }
            if (cue.Align != VideoCueAlign.Center)
            {
                b.Append(" align=").Append(cue.Align);
            }
            b.Append('\n');
            b.Append("  text ").Append(Quote(cue.Text)).Append('\n');
            foreach (var run in cue.Runs)
            {
                b.Append("  run  ").Append(Quote(run.Text));
                if (run.IsBold)
                {
                    b.Append(" bold");
                }
                if (run.IsItalic)
                {
                    b.Append(" italic");
                }
                if (run.IsUnderline)
                {
                    b.Append(" underline");
                }
                if (run.Voice is not null)
                {
                    b.Append(" voice=").Append(Quote(run.Voice));
                }
                b.Append('\n');
            }
        }
        return b.ToString();
    }

    /// <summary>Quotes text with invisible characters escaped, so the dump shows them.</summary>
    private static string Quote(string text)
    {
        var b = new StringBuilder("\"");
        foreach (var c in text)
        {
            switch (c)
            {
                case '\n':
                    b.Append("\\n");
                    break;
                case '"':
                    b.Append("\\\"");
                    break;
                case '\\':
                    b.Append("\\\\");
                    break;
                case ' ' or '‎' or '‏' or '﻿' or '�':
                    b.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    break;
                default:
                    b.Append(c);
                    break;
            }
        }
        return b.Append('"').ToString();
    }

    internal static string BaselineFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TKCode.Arc.slnx")))
            {
                return Path.Combine(folder.FullName, "tests", "Baselines", "Video");
            }
        }
        throw new InvalidOperationException("TKCode.Arc.slnx was not found above the test folder");
    }
}
