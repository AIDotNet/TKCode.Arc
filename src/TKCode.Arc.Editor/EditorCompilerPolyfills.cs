#if NET9_0_OR_GREATER
global using SyncLock = System.Threading.Lock;
#else
global using SyncLock = object;
#endif

#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    internal sealed class UnionAttribute : Attribute;

    internal interface IUnion
    {
        object? Value { get; }
    }
}
#endif
