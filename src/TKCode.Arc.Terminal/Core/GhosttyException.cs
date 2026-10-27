using TKCode.Terminal.Core.Interop;

namespace TKCode.Terminal.Core;

/// <summary>libghostty-vt 返回了非成功的结果码（通常是绑定或调用顺序错误）。</summary>
public sealed class GhosttyException : Exception
{
    internal GhosttyException(GhosttyResult result, string expression)
        : base($"libghostty-vt returned {result} ({(int)result}) from {expression}")
    {
        Code = (int)result;
    }

    public int Code { get; }
}
