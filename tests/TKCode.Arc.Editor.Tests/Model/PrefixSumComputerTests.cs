// Ported from vscode (src/vs/editor/test/common/viewModel/prefixSumComputer.test.ts, commit b57d5c9e), MIT license.
// Each test runs against both computers through the IPrefixSumComputer wrapper, as upstream's forBoth does.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class PrefixSumComputerTests
{
    private interface IPrefixSumComputer
    {
        long GetTotalSum();

        /// <summary>Returns sum of first <c>count</c> values: SUM(0 &lt;= j &lt; count, values[j]).</summary>
        long GetPrefixSum(int count);

        PrefixSumIndexOfResult GetIndexOf(int sum);

        void SetValue(int index, int value);

        void InsertValues(int insertIndex, int[] insertArr);

        void RemoveValues(int start, int deleteCount);
    }

    private sealed class Wrapped(PrefixSumComputer psc) : IPrefixSumComputer
    {
        public long GetTotalSum() => psc.GetTotalSum();

        public long GetPrefixSum(int count) => count == 0 ? 0 : psc.GetPrefixSum(count - 1);

        public PrefixSumIndexOfResult GetIndexOf(int sum) => psc.GetIndexOf(sum);

        public void SetValue(int index, int value) => psc.SetValue(index, value);

        public void InsertValues(int insertIndex, int[] insertArr) => psc.InsertValues(insertIndex, ToUint32Array(insertArr));

        public void RemoveValues(int start, int deleteCount) => psc.RemoveValues(start, deleteCount);
    }

    private sealed class WrappedConstantTime(ConstantTimePrefixSumComputer ct) : IPrefixSumComputer
    {
        public long GetTotalSum() => ct.GetTotalSum();

        public long GetPrefixSum(int count) => ct.GetPrefixSum(count);

        public PrefixSumIndexOfResult GetIndexOf(int sum) => ct.GetIndexOf(sum);

        public void SetValue(int index, int value) => ct.SetValue(index, value);

        public void InsertValues(int insertIndex, int[] insertArr) => ct.InsertValues(insertIndex, insertArr);

        public void RemoveValues(int start, int deleteCount) => ct.RemoveValues(start, deleteCount);
    }

    private static uint[] ToUint32Array(int[] arr)
    {
        var r = new uint[arr.Length];
        for (var i = 0; i < arr.Length; i++)
        {
            r[i] = (uint)Math.Max(0, arr[i]);
        }

        return r;
    }

    private static IPrefixSumComputer[] CreateBoth(int[] values) =>
    [
        new Wrapped(new PrefixSumComputer(ToUint32Array(values))),
        new WrappedConstantTime(new ConstantTimePrefixSumComputer([.. values])),
    ];

    private static void ForBoth(int[] values, Action<IPrefixSumComputer> callback)
    {
        foreach (var psc in CreateBoth(values))
        {
            callback(psc);
        }
    }

    [Fact]
    public void Comprehensive_SetValue_And_GetIndexOf()
    {
        ForBoth([1, 1, 2, 1, 3], psc => {
            psc.GetTotalSum().ShouldBe(8);
            psc.GetPrefixSum(0).ShouldBe(0);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(2);
            psc.GetPrefixSum(3).ShouldBe(4);
            psc.GetPrefixSum(4).ShouldBe(5);
            psc.GetPrefixSum(5).ShouldBe(8);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(2, 1));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(3, 0));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(4, 0));
            psc.GetIndexOf(6).ShouldBe(new PrefixSumIndexOfResult(4, 1));
            psc.GetIndexOf(7).ShouldBe(new PrefixSumIndexOfResult(4, 2));
            psc.GetIndexOf(8).ShouldBe(new PrefixSumIndexOfResult(4, 3));

            // [1, 2, 2, 1, 3]
            psc.SetValue(1, 2);
            psc.GetTotalSum().ShouldBe(9);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(3);
            psc.GetPrefixSum(3).ShouldBe(5);
            psc.GetPrefixSum(4).ShouldBe(6);
            psc.GetPrefixSum(5).ShouldBe(9);

            // [1, 0, 2, 1, 3]
            psc.SetValue(1, 0);
            psc.GetTotalSum().ShouldBe(7);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(1);
            psc.GetPrefixSum(3).ShouldBe(3);
            psc.GetPrefixSum(4).ShouldBe(4);
            psc.GetPrefixSum(5).ShouldBe(7);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(2, 1));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(3, 0));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(4, 0));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(4, 1));
            psc.GetIndexOf(6).ShouldBe(new PrefixSumIndexOfResult(4, 2));

            // [1, 0, 0, 1, 3]
            psc.SetValue(2, 0);
            psc.GetTotalSum().ShouldBe(5);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(1);
            psc.GetPrefixSum(3).ShouldBe(1);
            psc.GetPrefixSum(4).ShouldBe(2);
            psc.GetPrefixSum(5).ShouldBe(5);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(3, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(4, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(4, 1));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(4, 2));

            // [1, 0, 0, 0, 3]
            psc.SetValue(3, 0);
            psc.GetTotalSum().ShouldBe(4);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(1);
            psc.GetPrefixSum(3).ShouldBe(1);
            psc.GetPrefixSum(4).ShouldBe(1);
            psc.GetPrefixSum(5).ShouldBe(4);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(4, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(4, 1));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(4, 2));

            // [1, 1, 0, 1, 1]
            psc.SetValue(1, 1);
            psc.SetValue(3, 1);
            psc.SetValue(4, 1);
            psc.GetTotalSum().ShouldBe(4);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(2);
            psc.GetPrefixSum(3).ShouldBe(2);
            psc.GetPrefixSum(4).ShouldBe(3);
            psc.GetPrefixSum(5).ShouldBe(4);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(3, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(4, 0));
        });
    }

    // --- getTotalSum ---

    [Fact]
    public void GetTotalSum_With_Typical_Values()
    {
        ForBoth([1, 1, 2, 1, 3], psc => psc.GetTotalSum().ShouldBe(8));
        ForBoth([10], psc => psc.GetTotalSum().ShouldBe(10));
        ForBoth([5, 5, 5], psc => psc.GetTotalSum().ShouldBe(15));
    }

    [Fact]
    public void GetTotalSum_With_All_Zeroes()
    {
        ForBoth([0, 0, 0], psc => psc.GetTotalSum().ShouldBe(0));
        ForBoth([0], psc => psc.GetTotalSum().ShouldBe(0));
    }

    [Fact]
    public void GetTotalSum_With_Empty_Array()
    {
        ForBoth([], psc => psc.GetTotalSum().ShouldBe(0));
    }

    [Fact]
    public void GetTotalSum_With_Single_Element()
    {
        ForBoth([0], psc => psc.GetTotalSum().ShouldBe(0));
        ForBoth([1], psc => psc.GetTotalSum().ShouldBe(1));
        ForBoth([100], psc => psc.GetTotalSum().ShouldBe(100));
    }

    // --- getPrefixSum ---

    [Fact]
    public void GetPrefixSum_With_Typical_Values()
    {
        ForBoth([1, 1, 2, 1, 3], psc => {
            psc.GetPrefixSum(0).ShouldBe(0);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(2);
            psc.GetPrefixSum(3).ShouldBe(4);
            psc.GetPrefixSum(4).ShouldBe(5);
            psc.GetPrefixSum(5).ShouldBe(8);
        });
    }

    [Fact]
    public void GetPrefixSum_With_All_Zeroes()
    {
        ForBoth([0, 0, 0], psc => {
            psc.GetPrefixSum(0).ShouldBe(0);
            psc.GetPrefixSum(1).ShouldBe(0);
            psc.GetPrefixSum(2).ShouldBe(0);
            psc.GetPrefixSum(3).ShouldBe(0);
        });
    }

    [Fact]
    public void GetPrefixSum_With_Single_Element()
    {
        ForBoth([7], psc => {
            psc.GetPrefixSum(0).ShouldBe(0);
            psc.GetPrefixSum(1).ShouldBe(7);
        });
    }

    [Fact]
    public void GetPrefixSum_With_Empty_Array()
    {
        ForBoth([], psc => {
            psc.GetPrefixSum(0).ShouldBe(0);
        });
    }

    [Fact]
    public void GetPrefixSum_With_Leading_Trailing_Zeroes()
    {
        ForBoth([0, 0, 3, 0, 0], psc => {
            psc.GetPrefixSum(0).ShouldBe(0);
            psc.GetPrefixSum(1).ShouldBe(0);
            psc.GetPrefixSum(2).ShouldBe(0);
            psc.GetPrefixSum(3).ShouldBe(3);
            psc.GetPrefixSum(4).ShouldBe(3);
            psc.GetPrefixSum(5).ShouldBe(3);
        });
    }

    // --- getIndexOf ---

    [Fact]
    public void GetIndexOf_With_Typical_Values()
    {
        ForBoth([1, 1, 2, 1, 3], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(2, 1));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(3, 0));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(4, 0));
            psc.GetIndexOf(6).ShouldBe(new PrefixSumIndexOfResult(4, 1));
            psc.GetIndexOf(7).ShouldBe(new PrefixSumIndexOfResult(4, 2));
            psc.GetIndexOf(8).ShouldBe(new PrefixSumIndexOfResult(4, 3));
        });
    }

    [Fact]
    public void GetIndexOf_With_All_Zeroes()
    {
        ForBoth([0, 0, 0], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void GetIndexOf_With_Single_Zero()
    {
        ForBoth([0], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
        });
    }

    [Fact]
    public void GetIndexOf_With_Single_Element()
    {
        ForBoth([5], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(0, 1));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(0, 4));
        });
    }

    [Fact]
    public void GetIndexOf_With_Leading_Zeroes()
    {
        ForBoth([0, 0, 3], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(2, 1));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(2, 2));
        });
    }

    [Fact]
    public void GetIndexOf_With_Trailing_Zeroes()
    {
        ForBoth([3, 0, 0], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(0, 1));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(0, 2));
        });
    }

    [Fact]
    public void GetIndexOf_With_Interleaved_Zeroes()
    {
        ForBoth([0, 1, 0, 2, 0], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(3, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(3, 1));
        });
    }

    [Fact]
    public void GetIndexOf_With_All_Ones()
    {
        ForBoth([1, 1, 1, 1, 1], psc => {
            for (var i = 0; i < 5; i++)
            {
                psc.GetIndexOf(i).ShouldBe(new PrefixSumIndexOfResult(i, 0));
            }
        });
    }

    [Fact]
    public void GetIndexOf_With_Large_Value_In_Single_Element()
    {
        ForBoth([1000], psc => {
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(500).ShouldBe(new PrefixSumIndexOfResult(0, 500));
            psc.GetIndexOf(999).ShouldBe(new PrefixSumIndexOfResult(0, 999));
        });
    }

    // --- setValue ---

    [Fact]
    public void SetValue_No_Op_When_Value_Unchanged()
    {
        ForBoth([1, 2, 3], psc => {
            psc.GetTotalSum().ShouldBe(6);
            psc.SetValue(1, 2);
            psc.GetTotalSum().ShouldBe(6);
        });
    }

    [Fact]
    public void SetValue_Increase()
    {
        ForBoth([1, 2, 3], psc => {
            psc.SetValue(1, 5);
            psc.GetTotalSum().ShouldBe(9);
            psc.GetPrefixSum(2).ShouldBe(6);
            psc.GetPrefixSum(3).ShouldBe(9);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(1, 4));
            psc.GetIndexOf(6).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void SetValue_Decrease()
    {
        ForBoth([1, 5, 3], psc => {
            psc.SetValue(1, 2);
            psc.GetTotalSum().ShouldBe(6);
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(1, 1));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void SetValue_To_Zero()
    {
        ForBoth([1, 2, 3], psc => {
            psc.SetValue(1, 0);
            psc.GetTotalSum().ShouldBe(4);
            psc.GetPrefixSum(2).ShouldBe(1);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void SetValue_From_Zero()
    {
        ForBoth([0, 0, 0], psc => {
            psc.SetValue(1, 3);
            psc.GetTotalSum().ShouldBe(3);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(1, 2));
        });
    }

    [Fact]
    public void SetValue_On_First_Element()
    {
        ForBoth([1, 2, 3], psc => {
            psc.SetValue(0, 10);
            psc.GetTotalSum().ShouldBe(15);
            psc.GetPrefixSum(1).ShouldBe(10);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(9).ShouldBe(new PrefixSumIndexOfResult(0, 9));
            psc.GetIndexOf(10).ShouldBe(new PrefixSumIndexOfResult(1, 0));
        });
    }

    [Fact]
    public void SetValue_On_Last_Element()
    {
        ForBoth([1, 2, 3], psc => {
            psc.SetValue(2, 10);
            psc.GetTotalSum().ShouldBe(13);
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(12).ShouldBe(new PrefixSumIndexOfResult(2, 9));
        });
    }

    [Fact]
    public void Set_All_Values_To_Zero_Then_Restore()
    {
        ForBoth([1, 2, 3], psc => {
            psc.SetValue(0, 0);
            psc.SetValue(1, 0);
            psc.SetValue(2, 0);
            psc.GetTotalSum().ShouldBe(0);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(2, 0));

            psc.SetValue(0, 4);
            psc.GetTotalSum().ShouldBe(4);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(0, 3));
        });
    }

    [Fact]
    public void SetValue_Multiple_Times_On_Same_Index()
    {
        ForBoth([1, 1, 1], psc => {
            psc.SetValue(1, 5);
            psc.SetValue(1, 2);
            psc.SetValue(1, 10);
            psc.GetTotalSum().ShouldBe(12);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(10).ShouldBe(new PrefixSumIndexOfResult(1, 9));
            psc.GetIndexOf(11).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    // --- insertValues ---

    [Fact]
    public void InsertValues_At_Beginning()
    {
        ForBoth([3, 4], psc => {
            psc.InsertValues(0, [1, 2]);
            psc.GetTotalSum().ShouldBe(10);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(3);
            psc.GetPrefixSum(3).ShouldBe(6);
            psc.GetPrefixSum(4).ShouldBe(10);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void InsertValues_At_End()
    {
        ForBoth([1, 2], psc => {
            psc.InsertValues(2, [3, 4]);
            psc.GetTotalSum().ShouldBe(10);
            psc.GetPrefixSum(3).ShouldBe(6);
            psc.GetPrefixSum(4).ShouldBe(10);
        });
    }

    [Fact]
    public void InsertValues_In_The_Middle()
    {
        ForBoth([1, 4], psc => {
            psc.InsertValues(1, [2, 3]);
            psc.GetTotalSum().ShouldBe(10);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(3);
            psc.GetPrefixSum(3).ShouldBe(6);
            psc.GetPrefixSum(4).ShouldBe(10);
        });
    }

    [Fact]
    public void InsertValues_With_Zeroes()
    {
        ForBoth([1, 2], psc => {
            psc.InsertValues(1, [0, 0]);
            psc.GetTotalSum().ShouldBe(3);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(1);
            psc.GetPrefixSum(3).ShouldBe(1);
            psc.GetPrefixSum(4).ShouldBe(3);
        });
    }

    [Fact]
    public void InsertValues_Into_All_Zeroes()
    {
        ForBoth([0, 0, 0], psc => {
            psc.InsertValues(1, [2, 3]);
            psc.GetTotalSum().ShouldBe(5);
            psc.GetPrefixSum(1).ShouldBe(0);
            psc.GetPrefixSum(2).ShouldBe(2);
            psc.GetPrefixSum(3).ShouldBe(5);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(2, 2));
        });
    }

    [Fact]
    public void InsertValues_Into_Empty_Computer()
    {
        ForBoth([], psc => {
            psc.InsertValues(0, [5, 3]);
            psc.GetTotalSum().ShouldBe(8);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(0, 4));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(1, 0));
        });
    }

    // --- removeValues ---

    [Fact]
    public void RemoveValues_From_Beginning()
    {
        ForBoth([1, 2, 3, 4], psc => {
            psc.RemoveValues(0, 2);
            psc.GetTotalSum().ShouldBe(7);
            psc.GetPrefixSum(1).ShouldBe(3);
            psc.GetPrefixSum(2).ShouldBe(7);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(1, 0));
        });
    }

    [Fact]
    public void RemoveValues_From_End()
    {
        ForBoth([1, 2, 3, 4], psc => {
            psc.RemoveValues(2, 2);
            psc.GetTotalSum().ShouldBe(3);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(3);
        });
    }

    [Fact]
    public void RemoveValues_From_The_Middle()
    {
        ForBoth([1, 2, 3, 4], psc => {
            psc.RemoveValues(1, 2);
            psc.GetTotalSum().ShouldBe(5);
            psc.GetPrefixSum(1).ShouldBe(1);
            psc.GetPrefixSum(2).ShouldBe(5);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(1, 3));
        });
    }

    [Fact]
    public void RemoveValues_All()
    {
        ForBoth([1, 2, 3], psc => {
            psc.RemoveValues(0, 3);
            psc.GetTotalSum().ShouldBe(0);
        });
    }

    [Fact]
    public void RemoveValues_Single_Element()
    {
        ForBoth([5, 10, 15], psc => {
            psc.RemoveValues(1, 1);
            psc.GetTotalSum().ShouldBe(20);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(19).ShouldBe(new PrefixSumIndexOfResult(1, 14));
        });
    }

    [Fact]
    public void RemoveValues_Zero_Valued_Elements()
    {
        ForBoth([0, 0, 5, 0, 0], psc => {
            psc.RemoveValues(0, 2);
            psc.GetTotalSum().ShouldBe(5);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(0, 4));
        });
    }

    // --- combined operations ---

    [Fact]
    public void Insert_Then_Remove()
    {
        ForBoth([1, 2, 3], psc => {
            psc.InsertValues(1, [10, 20]);
            psc.GetTotalSum().ShouldBe(36);
            psc.RemoveValues(1, 2);
            psc.GetTotalSum().ShouldBe(6);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(3).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void Remove_Then_Insert_At_Same_Position()
    {
        ForBoth([1, 2, 3], psc => {
            psc.RemoveValues(1, 1);
            psc.InsertValues(1, [5]);
            psc.GetTotalSum().ShouldBe(9);
            psc.GetIndexOf(1).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(1, 4));
            psc.GetIndexOf(6).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void SetValue_Then_Insert_Then_Remove()
    {
        ForBoth([1, 1, 1], psc => {
            psc.SetValue(0, 5);
            psc.InsertValues(1, [10]);
            psc.RemoveValues(3, 1);
            // [5, 10, 1]
            psc.GetTotalSum().ShouldBe(16);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(4).ShouldBe(new PrefixSumIndexOfResult(0, 4));
            psc.GetIndexOf(5).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(14).ShouldBe(new PrefixSumIndexOfResult(1, 9));
            psc.GetIndexOf(15).ShouldBe(new PrefixSumIndexOfResult(2, 0));
        });
    }

    [Fact]
    public void Multiple_Queries_Between_Mutations_Are_Consistent()
    {
        ForBoth([2, 3, 5], psc => {
            psc.GetTotalSum().ShouldBe(10);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));

            psc.SetValue(1, 0);
            psc.GetTotalSum().ShouldBe(7);
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(2, 0));

            psc.SetValue(1, 3);
            psc.GetTotalSum().ShouldBe(10);
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(1, 0));
        });
    }

    // --- edge cases ---

    [Fact]
    public void Large_Values()
    {
        ForBoth([100, 200, 300], psc => {
            psc.GetTotalSum().ShouldBe(600);
            psc.GetPrefixSum(1).ShouldBe(100);
            psc.GetPrefixSum(2).ShouldBe(300);
            psc.GetPrefixSum(3).ShouldBe(600);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(0, 0));
            psc.GetIndexOf(99).ShouldBe(new PrefixSumIndexOfResult(0, 99));
            psc.GetIndexOf(100).ShouldBe(new PrefixSumIndexOfResult(1, 0));
            psc.GetIndexOf(299).ShouldBe(new PrefixSumIndexOfResult(1, 199));
            psc.GetIndexOf(300).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(599).ShouldBe(new PrefixSumIndexOfResult(2, 299));
        });
    }

    [Fact]
    public void Many_Elements()
    {
        ForBoth(Enumerable.Repeat(1, 100).ToArray(), psc => {
            psc.GetTotalSum().ShouldBe(100);
            psc.GetPrefixSum(50).ShouldBe(50);

            for (var i = 0; i < 100; i++)
            {
                psc.GetIndexOf(i).ShouldBe(new PrefixSumIndexOfResult(i, 0));
            }
        });
    }

    [Fact]
    public void Many_Elements_All_Zeroes()
    {
        ForBoth(Enumerable.Repeat(0, 100).ToArray(), psc => {
            psc.GetTotalSum().ShouldBe(0);
            for (var i = 0; i <= 100; i++)
            {
                psc.GetPrefixSum(i).ShouldBe(0);
            }
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(99, 0));
        });
    }

    [Fact]
    public void SetValue_Between_Queries_Re_Validates_Correctly()
    {
        ForBoth([1, 1, 1, 1, 1], psc => {
            psc.GetTotalSum().ShouldBe(5);

            psc.SetValue(2, 10);
            psc.GetTotalSum().ShouldBe(14);
            psc.GetPrefixSum(3).ShouldBe(12);
            psc.GetIndexOf(2).ShouldBe(new PrefixSumIndexOfResult(2, 0));
            psc.GetIndexOf(11).ShouldBe(new PrefixSumIndexOfResult(2, 9));
            psc.GetIndexOf(12).ShouldBe(new PrefixSumIndexOfResult(3, 0));
            psc.GetIndexOf(13).ShouldBe(new PrefixSumIndexOfResult(4, 0));

            psc.SetValue(0, 0);
            psc.GetTotalSum().ShouldBe(13);
            psc.GetIndexOf(0).ShouldBe(new PrefixSumIndexOfResult(1, 0));
        });
    }
}
