// Ported from vscode (src/vs/editor/test/common/viewModel/lineBreakData.test.ts, commit b57d5c9e), MIT license.
// The nested suites 'Injected Text 1/2/3' are prefixes of the test names.

using Shouldly;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class LineBreakDataTests
{
    [Fact]
    public void Basic()
    {
        var data = new ModelLineProjectionData([], [], [100], [0], 10);

        data.TranslateToInputOffset(0, 50).ShouldBe(50);
        data.TranslateToInputOffset(1, 60).ShouldBe(150);
    }

    private static int[] Sequence(int length, int start = 0)
    {
        var result = new int[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = i + start;
        }

        return result;
    }

    private static void TestInverse(ModelLineProjectionData data)
    {
        for (var i = 0; i < 100; i++)
        {
            var output = data.TranslateToOutputPosition(i);
            data.TranslateToInputOffset(output.OutputLineIndex, output.OutputOffset).ShouldBe(i);
        }
    }

    private static int[] GetInputOffsets(ModelLineProjectionData data, int outputLineIdx) =>
        Sequence(20).Select(i => data.TranslateToInputOffset(outputLineIdx, i)).ToArray();

    private static string[] GetOutputOffsets(ModelLineProjectionData data, PositionAffinity affinity) =>
        Sequence(25).Select(i => data.TranslateToOutputPosition(i, affinity).ToString()).ToArray();

    private static ModelDecorationInjectedTextOptions[] MapTextToInjectedTextOptions(string[] arr) =>
        arr.Select(e => new ModelDecorationInjectedTextOptions(e)).ToArray();

    // --- Injected Text 1

    private static ModelLineProjectionData Data1() =>
        new([2, 3, 10], MapTextToInjectedTextOptions(["1", "22", "333"]), [10, 100], [], 10);

    [Fact]
    public void InjectedText1_GetInputOffsetOfOutputPosition()
    {
        var data = Data1();
        // For every view model position, what is the model position?
        GetInputOffsets(data, 0).ShouldBe([0, 1, 2, 2, 3, 3, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10, 10, 11, 12, 13]);
        GetInputOffsets(data, 1).ShouldBe([7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 8, 9, 10, 10, 10, 10, 11, 12, 13]);
    }

    [Fact]
    public void InjectedText1_GetOutputPositionOfInputOffset()
    {
        var data = Data1();
        data.TranslateToOutputPosition(20);
        GetOutputOffsets(data, PositionAffinity.None).ShouldBe(
        [
            "0:0",
            "0:1",
            "0:2",
            "0:4",
            "0:7",
            "0:8",
            "0:9",
            "1:10",
            "1:11",
            "1:12",
            "1:13",
            "1:17",
            "1:18",
            "1:19",
            "1:20",
            "1:21",
            "1:22",
            "1:23",
            "1:24",
            "1:25",
            "1:26",
            "1:27",
            "1:28",
            "1:29",
            "1:30",
        ]);

        GetOutputOffsets(data, PositionAffinity.Left).ShouldBe(
        [
            "0:0",
            "0:1",
            "0:2",
            "0:4",
            "0:7",
            "0:8",
            "0:9",
            "0:10",
            "1:11",
            "1:12",
            "1:13",
            "1:17",
            "1:18",
            "1:19",
            "1:20",
            "1:21",
            "1:22",
            "1:23",
            "1:24",
            "1:25",
            "1:26",
            "1:27",
            "1:28",
            "1:29",
            "1:30",
        ]);

        GetOutputOffsets(data, PositionAffinity.Right).ShouldBe(
        [
            "0:0",
            "0:1",
            "0:3",
            "0:6",
            "0:7",
            "0:8",
            "0:9",
            "1:10",
            "1:11",
            "1:12",
            "1:16",
            "1:17",
            "1:18",
            "1:19",
            "1:20",
            "1:21",
            "1:22",
            "1:23",
            "1:24",
            "1:25",
            "1:26",
            "1:27",
            "1:28",
            "1:29",
            "1:30",
        ]);
    }

    [Fact]
    public void InjectedText1_GetInputOffsetOfOutputPosition_Is_Inverse_Of_GetOutputPositionOfInputOffset()
    {
        TestInverse(Data1());
    }

    [Fact]
    public void InjectedText1_Normalization()
    {
        var data = Data1();
        Sequence(25)
            .Select(v => data.NormalizeOutputPosition(1, v, PositionAffinity.Right))
            .Select(s => s.ToString())
            .ToArray()
            .ShouldBe(
            [
                "1:0",
                "1:1",
                "1:2",
                "1:3",
                "1:4",
                "1:5",
                "1:6",
                "1:7",
                "1:8",
                "1:9",
                "1:10",
                "1:11",
                "1:12",
                "1:16",
                "1:16",
                "1:16",
                "1:16",
                "1:17",
                "1:18",
                "1:19",
                "1:20",
                "1:21",
                "1:22",
                "1:23",
                "1:24",
            ]);
    }

    // --- Injected Text 2

    private static ModelLineProjectionData Data2() =>
        new([2, 2, 6], MapTextToInjectedTextOptions(["1", "22", "333"]), [10, 100], [], 0);

    [Fact]
    public void InjectedText2_GetInputOffsetOfOutputPosition()
    {
        var data = Data2();
        GetInputOffsets(data, 0).ShouldBe([0, 1, 2, 2, 2, 2, 3, 4, 5, 6, 6, 6, 6, 7, 8, 9, 10, 11, 12, 13]);
        GetInputOffsets(data, 1).ShouldBe([6, 6, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23]);
    }

    [Fact]
    public void InjectedText2_GetInputOffsetOfOutputPosition_Is_Inverse_Of_GetOutputPositionOfInputOffset()
    {
        TestInverse(Data2());
    }

    // --- Injected Text 3

    private static ModelLineProjectionData Data3() =>
        new([2, 2, 7], MapTextToInjectedTextOptions(["1", "22", "333"]), [10, 100], [], 0);

    [Fact]
    public void InjectedText3_GetInputOffsetOfOutputPosition()
    {
        var data = Data3();
        GetInputOffsets(data, 0).ShouldBe([0, 1, 2, 2, 2, 2, 3, 4, 5, 6, 7, 7, 7, 7, 8, 9, 10, 11, 12, 13]);
        GetInputOffsets(data, 1).ShouldBe([7, 7, 7, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23]);
    }

    [Fact]
    public void InjectedText3_GetInputOffsetOfOutputPosition_Is_Inverse_Of_GetOutputPositionOfInputOffset()
    {
        TestInverse(Data3());
    }
}
