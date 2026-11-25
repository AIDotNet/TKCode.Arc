using BenchmarkDotNet.Attributes;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Benchmarks;

/// <summary>Loading a document: chunks to piece tree to text model.</summary>
[MemoryDiagnoser]
public class LoadBenchmarks
{
    private List<string> _chunks = null!;

    [Params(1, 20, 100)]
    public int Megabytes { get; set; }

    [GlobalSetup]
    public void Setup() => _chunks = Documents.Chunk(Documents.Create(Megabytes));

    [Benchmark]
    public int Load()
    {
        using var model = Documents.Model(_chunks);
        return model.GetLineCount();
    }
}

/// <summary>Reading a document line by line, and through a snapshot.</summary>
[MemoryDiagnoser]
public class ReadBenchmarks
{
    private TextModel _model = null!;
    private TextModel _edited = null!;

    [Params(20)]
    public int Megabytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var chunks = Documents.Chunk(Documents.Create(Megabytes));
        _model = Documents.Model(chunks);
        _edited = Documents.Model(chunks);
        var random = new Random(1);
        for (var i = 0; i < 10_000; i++)
        {
            var line = random.Next(1, _edited.GetLineCount());
            _edited.ApplyEdits([EditOperation.Insert(new TextPosition(line, 1), "// edit " + i + "\n")]);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _model.Dispose();
        _edited.Dispose();
    }

    [Benchmark(Baseline = true)]
    public long GetLineContent() => ReadAll(_model);

    [Benchmark]
    public long GetLineContentSpan() => ReadAllSpans(_model);

    [Benchmark]
    public long GetLineContent_After10kEdits() => ReadAll(_edited);

    [Benchmark]
    public long GetLineContentSpan_After10kEdits() => ReadAllSpans(_edited);

    /// <summary>Taking a random-access snapshot: O(pieces), no text copied.</summary>
    [Benchmark]
    public int CreateTextSnapshot_After10kEdits() => _edited.CreateTextSnapshot().LineCount;

    /// <summary>Reading every line of a random-access snapshot, as a background tokenizer would.</summary>
    [Benchmark]
    public long TextSnapshot_ReadAllSpans_After10kEdits()
    {
        var snapshot = _edited.CreateTextSnapshot();
        var cursor = default(TextSnapshot.LineCursor);
        long total = 0;
        for (var lineNumber = 1; lineNumber <= snapshot.LineCount; lineNumber++)
        {
            total += snapshot.GetLineContentSpan(lineNumber, ref cursor).Length;
        }

        return total;
    }

    [Benchmark]
    public long Snapshot_After10kEdits()
    {
        var snapshot = _edited.CreateSnapshot();
        long total = 0;
        string? chunk;
        while ((chunk = snapshot.Read()) is not null)
        {
            total += chunk.Length;
        }

        return total;
    }

    private static long ReadAll(TextModel model)
    {
        long total = 0;
        var lineCount = model.GetLineCount();
        for (var lineNumber = 1; lineNumber <= lineCount; lineNumber++)
        {
            total += model.GetLineContent(lineNumber).Length;
        }

        return total;
    }

    private static long ReadAllSpans(TextModel model)
    {
        long total = 0;
        var lineCount = model.GetLineCount();
        for (var lineNumber = 1; lineNumber <= lineCount; lineNumber++)
        {
            total += model.GetLineContentSpan(lineNumber).Length;
        }

        return total;
    }
}

/// <summary>
/// The keystroke path of the model: one character typed, with its undo step, as a cursor command would push it. The
/// time per operation is the model's share of the 4 ms budget per keystroke (ADR 0056).
/// </summary>
[MemoryDiagnoser]
public class EditBenchmarks
{
    private List<string> _chunks = null!;
    private TextModel _model = null!;
    private int _line;
    private int _column;

    [Params(1, 20)]
    public int Megabytes { get; set; }

    [GlobalSetup]
    public void Setup() => _chunks = Documents.Chunk(Documents.Create(Megabytes));

    [IterationSetup]
    public void IterationSetup()
    {
        _model?.Dispose();
        _model = Documents.Model(_chunks);
        _line = _model.GetLineCount() / 2;
        _column = 1;
        _model.DeltaDecorations(null, [new ModelDeltaDecoration(new TextRange(_line, 1, _line + 5, 1), new ModelDecorationOptions("bench"))]);
    }

    [IterationCleanup]
    public void IterationCleanup() => _model.Dispose();

    /// <summary>1000 characters typed in the middle of the document, each its own edit, in one undo step.</summary>
    [Benchmark(OperationsPerInvoke = 1000)]
    public void Type1000Characters()
    {
        for (var i = 0; i < 1000; i++)
        {
            var position = new TextPosition(_line, _column);
            _model.PushEditOperations(
                [new TextSelection(position)],
                [EditOperation.Insert(position, "x")],
                static _ => null);
            _column++;
        }
    }

    /// <summary>1000 single-character inserts at random lines, without undo.</summary>
    [Benchmark(OperationsPerInvoke = 1000)]
    public void RandomInserts()
    {
        var random = new Random(7);
        var lineCount = _model.GetLineCount();
        for (var i = 0; i < 1000; i++)
        {
            _model.ApplyEdits([EditOperation.Insert(new TextPosition(random.Next(1, lineCount), 1), "x")]);
        }
    }
}

/// <summary>Find over a whole document.</summary>
[MemoryDiagnoser]
public class FindBenchmarks
{
    private TextModel _model = null!;

    [Params(20)]
    public int Megabytes { get; set; }

    [GlobalSetup]
    public void Setup() => _model = Documents.Model(Documents.Chunk(Documents.Create(Megabytes)));

    [GlobalCleanup]
    public void Cleanup() => _model.Dispose();

    [Benchmark]
    public int PlainText_NoMatch() => _model.FindMatches("not in the text", false, false, true, null, false, int.MaxValue).Count;

    [Benchmark]
    public int PlainText_ManyMatches() => _model.FindMatches("value", false, false, true, null, false, int.MaxValue).Count;

    [Benchmark]
    public int Regex_DotStar() => _model.FindMatches("Try.*out", false, true, true, null, false, int.MaxValue).Count;

    [Benchmark]
    public int WholeWord() => _model.FindMatches("key", false, false, true, "`~!@#$%^&*()-=+[{]}\\|;:'\",.<>/?", false, int.MaxValue).Count;
}
