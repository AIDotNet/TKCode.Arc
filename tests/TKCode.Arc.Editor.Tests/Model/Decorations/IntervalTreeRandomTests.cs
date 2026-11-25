// Ported from vscode's src/vs/editor/test/common/model/intervalTree.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// The generated/random suite. Upstream uses Math.random; here a seeded System.Random makes runs reproducible.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;
using Xunit.Sdk;

namespace TKCode.Arc.Editor.Tests;

public sealed class IntervalTreeRandomTests
{
    private const int TestCount = 1000;
    private const int MinIntervalStart = 1;
    private const int MaxIntervalEnd = 100;
    private const int MinInserts = 1;
    private const int MaxInserts = 30;
    private const int MinChangeCnt = 10;
    private const int MaxChangeCnt = 20;

    [Fact]
    public void Generated_Random_Operations_Match_Oracle()
    {
        // Upstream runs this suite only when GENERATE_TESTS is on and seeds with Math.random. A fixed seed keeps
        // the port reproducible; the generated scripts still cover the same operations.
        Random random = new Random(20241124);
        for (int i = 0; i < TestCount; i++)
        {
            AutoTest test = new AutoTest(random);
            try
            {
                test.Run();
            }
            catch (Exception exception)
            {
                throw new XunitException($"iteration {i}: {exception.Message}{Environment.NewLine}{test.OperationsText()}");
            }
        }
    }

    private static int GetRandomInt(Random random, int min, int max)
    {
        return random.Next(min, max + 1);
    }

    private static (int Begin, int End) GetRandomRange(Random random, int min, int max)
    {
        int begin = GetRandomInt(random, min, max);
        int length;
        if (GetRandomInt(random, 1, 10) <= 2)
        {
            // large range
            length = GetRandomInt(random, 0, max - begin);
        }
        else
        {
            // small range
            length = GetRandomInt(random, 0, Math.Min(max - begin, 10));
        }
        return (begin, begin + length);
    }

    private sealed class AutoTest
    {
        private readonly Random _random;
        private readonly List<TestOperation> _ops = new List<TestOperation>();
        private readonly IntervalTreeTestState _state = new IntervalTreeTestState();
        private int _insertCnt;
        private int _deleteCnt;
        private int _changeCnt;

        internal AutoTest(Random random)
        {
            _random = random;
            _insertCnt = GetRandomInt(random, MinInserts, MaxInserts);
            _changeCnt = GetRandomInt(random, MinChangeCnt, MaxChangeCnt);
            _deleteCnt = 0;
        }

        private void DoRandomInsert()
        {
            (int begin, int end) = GetRandomRange(_random, MinIntervalStart, MaxIntervalEnd);
            Run(TestOperation.Insert(begin, end));
        }

        private void DoRandomDelete()
        {
            int idx = GetRandomInt(_random, _deleteCnt / 2, _deleteCnt - 1);
            Run(TestOperation.Delete(_state.GetExistingNodeId(idx)));
        }

        private void DoRandomChange()
        {
            int idx = GetRandomInt(_random, 0, _deleteCnt - 1);
            (int begin, int end) = GetRandomRange(_random, MinIntervalStart, MaxIntervalEnd);
            Run(TestOperation.Change(_state.GetExistingNodeId(idx), begin, end));
        }

        internal void Run()
        {
            while (_insertCnt > 0 || _deleteCnt > 0 || _changeCnt > 0)
            {
                if (_insertCnt > 0)
                {
                    DoRandomInsert();
                    _insertCnt--;
                    _deleteCnt++;
                }
                else if (_changeCnt > 0)
                {
                    DoRandomChange();
                    _changeCnt--;
                }
                else
                {
                    DoRandomDelete();
                    _deleteCnt--;
                }

                // Let's also search for something...
                (int begin, int end) = GetRandomRange(_random, MinIntervalStart, MaxIntervalEnd);
                Run(TestOperation.Search(begin, end));
            }
        }

        private void Run(TestOperation op)
        {
            _ops.Add(op);
            _state.AcceptOp(op);
        }

        internal string OperationsText()
        {
            return string.Join(Environment.NewLine, _ops);
        }
    }
}
