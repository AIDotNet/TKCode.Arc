// Ported from vscode (src/vs/editor/test/common/model/textModel.test.ts, commit b57d5c9e), MIT license.
// This file: the 'TextModel.mightContainRTL' suite.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelMightContainRTLTests
{
    [Fact]
    public void Nope()
    {
        var model = TestTextModel.Create("hello world!");
        model.MightContainRTL().ShouldBeFalse();
        model.Dispose();
    }

    [Fact]
    public void Yes()
    {
        var model = TestTextModel.Create("Hello,\nזוהי עובדה מבוססת שדעתו");
        model.MightContainRTL().ShouldBeTrue();
        model.Dispose();
    }

    [Fact]
    public void SetValue_Resets_1()
    {
        var model = TestTextModel.Create("hello world!");
        model.MightContainRTL().ShouldBeFalse();
        model.SetValue("Hello,\nזוהי עובדה מבוססת שדעתו");
        model.MightContainRTL().ShouldBeTrue();
        model.Dispose();
    }

    [Fact]
    public void SetValue_Resets_2()
    {
        var model = TestTextModel.Create("Hello,\nهناك حقيقة مثبتة منذ زمن طويل");
        model.MightContainRTL().ShouldBeTrue();
        model.SetValue("hello world!");
        model.MightContainRTL().ShouldBeFalse();
        model.Dispose();
    }
}
