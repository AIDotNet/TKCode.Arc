using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TKCode.Arc.Graphics3D.Rhi.OpenGL;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>
/// Every function <see cref="GlApi"/> calls through a function pointer has a delegate of the same shape in
/// <see cref="GlSignatures"/>: the WebAssembly build makes its interpreter-to-native trampolines from those delegates, and a
/// call through a shape without one ends the program in the browser.
/// </summary>
public class GlSignatureTests
{
    [Fact]
    public void Every_Function_Pointer_Shape_Has_A_Signature_Delegate()
    {
        var declared = typeof(GlSignatures).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Select(type =>
            {
                var invoke = type.GetMethod("Invoke")!;
                var shape = Letter(invoke.ReturnType) + string.Concat(invoke.GetParameters().Select(p => Letter(p.ParameterType)));
                // The name is the shape, so the list reads at a glance.
                type.Name.ShouldBe(shape);
                return shape;
            })
            .ToHashSet(StringComparer.Ordinal);

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in typeof(GlApi).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var type = field.FieldType;
            if (!type.IsFunctionPointer)
            {
                continue;
            }

            var shape = Letter(type.GetFunctionPointerReturnType()) + string.Concat(type.GetFunctionPointerParameterTypes().Select(Letter));
            used.Add(shape);
            if (!declared.Contains(shape))
            {
                missing.Add($"{shape} ({field.Name})");
            }
        }

        used.Count.ShouldBeGreaterThan(10);
        missing.ShouldBeEmpty();
        declared.Except(used).ShouldBeEmpty("a delegate no function uses any more");
    }

    [Fact]
    public void The_Constructor_Keeps_Every_Signature_Delegate_Through_Trimming()
    {
        // A trimmed application drops what nothing refers to; the constructor names each delegate's Invoke method.
        var constructor = typeof(GlApi).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ShouldHaveSingleItem();
        var kept = constructor.GetCustomAttributes<DynamicDependencyAttribute>()
            .Where(attribute => attribute.MemberSignature == "Invoke")
            .Select(attribute => attribute.Type)
            .ToHashSet();
        foreach (var type in typeof(GlSignatures).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            kept.ShouldContain(type, $"GlApi's constructor has no DynamicDependency for GlSignatures.{type.Name}");
        }
    }

    // What WebAssembly passes: 32-bit integers (pointers and nint are 32 bits there), floats and doubles.
    private static string Letter(Type type)
    {
        if (type == typeof(void))
        {
            return "V";
        }

        if (type == typeof(float))
        {
            return "F";
        }

        if (type == typeof(double))
        {
            return "D";
        }

        if (type == typeof(long) || type == typeof(ulong))
        {
            return "L";
        }

        (type.IsPointer || type.IsFunctionPointer || type == typeof(nint) || type == typeof(nuint) || type == typeof(int) || type == typeof(uint)
         || type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(bool))
            .ShouldBeTrue($"{type} has no WebAssembly shape in this test");
        return "I";
    }
}
