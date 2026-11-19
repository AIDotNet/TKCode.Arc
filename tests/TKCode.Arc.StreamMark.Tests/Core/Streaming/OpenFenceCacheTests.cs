using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Streaming;

/// <summary>
/// 未闭合代码块的增量组装：每一步都必须与不带缓存的组装（新的 composer 一次处理整段前缀）完全一致；
/// 覆盖缩进围栏、CRLF、末尾空格、空行、波浪线围栏与围栏闭合后的下一个代码块。
/// （与整篇解析的一致性由 <see cref="StreamingEquivalenceTests"/> 保证。）
/// </summary>
public sealed class OpenFenceCacheTests
{
    public static TheoryData<string> Documents() => new()
    {
        "Intro\n\n```csharp\nint a = 1;\n    int b = 2;\n\n\nreturn a + b;  \nx \n```\n\nAfter\n",
        "   ```py\n   def f():\n     return 1\n  two\n x\nnone\n   ```\n",
        "```js\r\nconst a = 1;\r\n\r\nconst b = 2; \r\n```\r\n",
        "~~~\nline one \nline two  \n \n  \n~~~\n\n```\nsecond block\nmore\n",
        "- item\n\n  ```\n  nested code\n    deeper\n  ```\n",
        "```\n",
        "```\n \n \n",
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryPrefix_MatchesUncachedAssembly(string text)
    {
        foreach (var chunk in new[] { 1, 2, 3, 5 })
        {
            var composer = new DocumentComposer();
            var version = 0L;
            for (var length = Math.Min(chunk, text.Length); ; length = Math.Min(length + chunk, text.Length))
            {
                var prefix = text[..length];
                var from = composer.FrozenEnd;
                var snapshot = composer.Update(prefix[from..], from, ++version);

                var fresh = new DocumentComposer().Update(prefix, 0, 1);
                var actual = snapshot.Blocks.ToList();
                Assert.Equal(fresh.Blocks.Count(), actual.Count);
                for (var i = 0; i < actual.Count; i++)
                {
                    var context = $"chunk={chunk} length={length} block={i}";
                    Assert.True(fresh[i].ComputeFingerprint() == actual[i].ComputeFingerprint(), $"{context}: differs from uncached\n{fresh[i]}\n---\n{actual[i]}");
                    Assert.Equal(fresh[i].Range, actual[i].Range);
                }
                if (length == text.Length)
                {
                    break;
                }
            }
        }
    }

    [Fact]
    public void LongOpenBlock_KeepsCodeExact()
    {
        var lines = Enumerable.Range(0, 400).Select(i => $"    var x{i} = {i}; // line {i}").ToArray();
        var text = "```csharp\n" + string.Join("\n", lines);
        var composer = new DocumentComposer();
        var version = 0L;
        for (var length = 7; length <= text.Length; length += 7)
        {
            var from = composer.FrozenEnd;
            composer.Update(text[from..length], from, ++version);
        }
        var last = composer.Update(text[composer.FrozenEnd..], composer.FrozenEnd, ++version);
        var code = Assert.IsType<CodeBlock>(Assert.Single(last.Blocks));
        Assert.True(code.IsOpen);
        Assert.Equal(string.Join("\n", lines), code.Code);
    }
}
