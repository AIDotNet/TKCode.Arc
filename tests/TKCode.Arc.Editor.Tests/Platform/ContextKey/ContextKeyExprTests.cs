// Ported from vscode (src/vs/platform/contextkey/test/common/contextkey.test.ts, commit b57d5c9e), MIT license.
// All 21 upstream tests. createContext's object becomes a dictionary (a missing key reads as null, JavaScript's
// undefined); arrays are object?[] and records Dictionary<string, object?>. The `evaluate` test compares against the
// answers JavaScript gives (written out, since the comparison is the point). The Windows-only file URI cases run where
// upstream runs them (OperatingSystem.IsWindows()). The tests after the upstream ones cover the regex mapping (JsRegExp)
// and RawContextKey.

using Shouldly;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ContextKeyExprTests
{
    private sealed class TestContext(Dictionary<string, object?> ctx) : IContext
    {
        public object? GetValue(string key) => ctx.TryGetValue(key, out var value) ? value : null;
    }

    private static IContext CreateContext(Dictionary<string, object?> ctx) => new TestContext(ctx);

    private static Dictionary<string, object?> Record(params (string Key, object? Value)[] entries)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in entries)
        {
            result[key] = value;
        }
        return result;
    }

    [Fact]
    public void ContextKeyExpr_equals()
    {
        var a = ContextKeyExpr.And(
            ContextKeyExpr.Has("a1"),
            ContextKeyExpr.And(ContextKeyExpr.Has("and.a")),
            ContextKeyExpr.Has("a2"),
            ContextKeyExpr.Regex("d3", new Platform.JsRegExp("d.*")),
            ContextKeyExpr.Regex("d4", new Platform.JsRegExp("\\*\\*3*")),
            ContextKeyExpr.Equals("b1", "bb1"),
            ContextKeyExpr.Equals("b2", "bb2"),
            ContextKeyExpr.NotEquals("c1", "cc1"),
            ContextKeyExpr.NotEquals("c2", "cc2"),
            ContextKeyExpr.Not("d1"),
            ContextKeyExpr.Not("d2"))!;
        var b = ContextKeyExpr.And(
            ContextKeyExpr.Equals("b2", "bb2"),
            ContextKeyExpr.NotEquals("c1", "cc1"),
            ContextKeyExpr.Not("d1"),
            ContextKeyExpr.Regex("d4", new Platform.JsRegExp("\\*\\*3*")),
            ContextKeyExpr.NotEquals("c2", "cc2"),
            ContextKeyExpr.Has("a2"),
            ContextKeyExpr.Equals("b1", "bb1"),
            ContextKeyExpr.Regex("d3", new Platform.JsRegExp("d.*")),
            ContextKeyExpr.Has("a1"),
            ContextKeyExpr.And(ContextKeyExpr.Equals("and.a", true)),
            ContextKeyExpr.Not("d2"))!;
        a.Equals(b).ShouldBeTrue("expressions should be equal");
    }

    [Fact]
    public void Issue134942_Equals_in_comparator_expressions()
    {
        static void TestEquals(ContextKeyExpression? expr, string str)
        {
            var deserialized = ContextKeyExpr.Deserialize(str);
            expr.ShouldNotBeNull();
            deserialized.ShouldNotBeNull();
            expr.Equals(deserialized).ShouldBeTrue(str);
        }

        TestEquals(ContextKeyExpr.Greater("value", 0), "value > 0");
        TestEquals(ContextKeyExpr.GreaterEquals("value", 0), "value >= 0");
        TestEquals(ContextKeyExpr.Smaller("value", 0), "value < 0");
        TestEquals(ContextKeyExpr.SmallerEquals("value", 0), "value <= 0");
    }

    [Fact]
    public void Normalize()
    {
        var key1IsTrue = ContextKeyExpr.Equals("key1", true);
        var key1IsNotFalse = ContextKeyExpr.NotEquals("key1", false);
        var key1IsFalse = ContextKeyExpr.Equals("key1", false);
        var key1IsNotTrue = ContextKeyExpr.NotEquals("key1", true);

        key1IsTrue.Equals(ContextKeyExpr.Has("key1")).ShouldBeTrue();
        key1IsNotFalse.Equals(ContextKeyExpr.Has("key1")).ShouldBeTrue();
        key1IsFalse.Equals(ContextKeyExpr.Not("key1")).ShouldBeTrue();
        key1IsNotTrue.Equals(ContextKeyExpr.Not("key1")).ShouldBeTrue();
    }

    [Fact]
    public void Evaluate()
    {
        var context = CreateContext(Record(
            ("a", true),
            ("b", false),
            ("c", "5"),
            ("d", "d")));

        void TestExpression(string expr, bool expected)
        {
            var rules = ContextKeyExpr.Deserialize(expr);
            rules!.Evaluate(context).ShouldBe(expected, expr);
        }

        // upstream computes the expected values with JavaScript's operators: !!value, value == '5', value != '5',
        // /d.*/.test(value), /D/i.test(value) (test() converts undefined to "undefined", which contains a d)
        void TestBatch(string expr, bool truthy, bool equals5, bool matchesDDotStar, bool matchesDIgnoreCase)
        {
            TestExpression(expr, truthy);
            TestExpression(expr + " == true", truthy);
            TestExpression(expr + " != true", !truthy);
            TestExpression(expr + " == false", !truthy);
            TestExpression(expr + " != false", truthy);
            TestExpression(expr + " == 5", equals5);
            TestExpression(expr + " != 5", !equals5);
            TestExpression("!" + expr, !truthy);
            TestExpression(expr + " =~ /d.*/", matchesDDotStar);
            TestExpression(expr + " =~ /D/i", matchesDIgnoreCase);
        }

        TestBatch("a", truthy: true, equals5: false, matchesDDotStar: false, matchesDIgnoreCase: false);
        TestBatch("b", truthy: false, equals5: false, matchesDDotStar: false, matchesDIgnoreCase: false);
        TestBatch("c", truthy: true, equals5: true, matchesDDotStar: false, matchesDIgnoreCase: false);
        TestBatch("d", truthy: true, equals5: false, matchesDDotStar: true, matchesDIgnoreCase: true);
        TestBatch("z", truthy: false, equals5: false, matchesDDotStar: true, matchesDIgnoreCase: true);

        TestExpression("true", true);
        TestExpression("false", false);
        TestExpression("a && !b", true && !false);
        TestExpression("a && b", true && false);
        TestExpression("a && !b && c == 5", true && !false && "5" == "5");
        TestExpression("d =~ /e.*/", false);

        // precedence test: false && true || true === true because && is evaluated first
        TestExpression("b && a || a", true);

        TestExpression("a || b", true);
        TestExpression("b || b", false);
        TestExpression("b && a || a && b", false);
    }

    [Fact]
    public void Negate()
    {
        static void TestNegate(string expr, string expected)
        {
            var actual = ContextKeyExpr.Deserialize(expr)!.Negate().Serialize();
            actual.ShouldBe(expected);
        }

        TestNegate("true", "false");
        TestNegate("false", "true");
        TestNegate("a", "!a");
        TestNegate("a && b || c", "!a && !c || !b && !c");
        TestNegate("a && b || c || d", "!a && !c && !d || !b && !c && !d");
        TestNegate("!a && !b || !c && !d", "a && c || a && d || b && c || b && d");
        TestNegate("!a && !b || !c && !d || !e && !f", "a && c && e || a && c && f || a && d && e || a && d && f || b && c && e || b && c && f || b && d && e || b && d && f");
    }

    [Fact]
    public void False_true()
    {
        static void TestNormalize(string expr, string expected)
        {
            var actual = ContextKeyExpr.Deserialize(expr)!.Serialize();
            actual.ShouldBe(expected);
        }

        TestNormalize("true", "true");
        TestNormalize("!true", "false");
        TestNormalize("false", "false");
        TestNormalize("!false", "true");
        TestNormalize("a && true", "a");
        TestNormalize("a && false", "false");
        TestNormalize("a || true", "true");
        TestNormalize("a || false", "a");
        TestNormalize("isMac", OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() ? "true" : "false");
        TestNormalize("isLinux", OperatingSystem.IsLinux() ? "true" : "false");
        TestNormalize("isWindows", OperatingSystem.IsWindows() ? "true" : "false");
        TestNormalize("isChromeOS", "false");
    }

    [Fact]
    public void Issue101015_distribute_OR()
    {
        static void T(string expr1, string expr2, string? expected)
        {
            var e1 = ContextKeyExpr.Deserialize(expr1);
            var e2 = ContextKeyExpr.Deserialize(expr2);
            var actual = ContextKeyExpr.And(e1, e2)?.Serialize();
            actual.ShouldBe(expected);
        }

        T("a", "b", "a && b");
        T("a || b", "c", "a && c || b && c");
        T("a || b", "c || d", "a && c || a && d || b && c || b && d");
        T("a || b", "c && d", "a && c && d || b && c && d");
        T("a || b", "c && d || e", "a && e || b && e || a && c && d || b && c && d");
    }

    [Fact]
    public void ContextKeyInExpr()
    {
        var ainb = ContextKeyExpr.Deserialize("a in b")!;
        ainb.Evaluate(CreateContext(Record(("a", 3), ("b", new object?[] { 3, 2, 1 })))).ShouldBeTrue();
        ainb.Evaluate(CreateContext(Record(("a", 3), ("b", new object?[] { 1, 2, 3 })))).ShouldBeTrue();
        ainb.Evaluate(CreateContext(Record(("a", 3), ("b", new object?[] { 1, 2 })))).ShouldBeFalse();
        ainb.Evaluate(CreateContext(Record(("a", 3)))).ShouldBeFalse();
        ainb.Evaluate(CreateContext(Record(("a", 3), ("b", null)))).ShouldBeFalse();
        ainb.Evaluate(CreateContext(Record(("a", "x"), ("b", new object?[] { "x" })))).ShouldBeTrue();
        ainb.Evaluate(CreateContext(Record(("a", "x"), ("b", new object?[] { "y" })))).ShouldBeFalse();
        ainb.Evaluate(CreateContext(Record(("a", "x"), ("b", Record())))).ShouldBeFalse();
        ainb.Evaluate(CreateContext(Record(("a", "x"), ("b", Record(("x", false)))))).ShouldBeTrue();
        ainb.Evaluate(CreateContext(Record(("a", "x"), ("b", Record(("x", true)))))).ShouldBeTrue();
        ainb.Evaluate(CreateContext(Record(("a", "prototype"), ("b", Record())))).ShouldBeFalse();

        // file URI case-insensitive comparison on Windows
        if (OperatingSystem.IsWindows())
        {
            // Array source: file URIs with different casing should match on Windows
            ainb.Evaluate(CreateContext(Record(("a", "file:///c%3A/Users/path/file.ts"), ("b", new object?[] { "file:///c%3A/users/path/file.ts" })))).ShouldBeTrue();
            ainb.Evaluate(CreateContext(Record(("a", "file:///c%3A/users/path/file.ts"), ("b", new object?[] { "file:///c%3A/Users/path/file.ts" })))).ShouldBeTrue();
            // Object source: file URIs with different casing should match on Windows
            ainb.Evaluate(CreateContext(Record(("a", "file:///c%3A/Users/path/file.ts"), ("b", Record(("file:///c%3A/users/path/file.ts", true)))))).ShouldBeTrue();
            // Non-file URIs should still be case-sensitive
            ainb.Evaluate(CreateContext(Record(("a", "git:/path/File.ts"), ("b", new object?[] { "git:/path/file.ts" })))).ShouldBeFalse();
            // Exact match still works
            ainb.Evaluate(CreateContext(Record(("a", "file:///c%3A/Users/path/file.ts"), ("b", new object?[] { "file:///c%3A/Users/path/file.ts" })))).ShouldBeTrue();
        }
    }

    [Fact]
    public void ContextKeyNotInExpr()
    {
        var aNotInB = ContextKeyExpr.Deserialize("a not in b")!;
        aNotInB.Evaluate(CreateContext(Record(("a", 3), ("b", new object?[] { 3, 2, 1 })))).ShouldBeFalse();
        aNotInB.Evaluate(CreateContext(Record(("a", 3), ("b", new object?[] { 1, 2, 3 })))).ShouldBeFalse();
        aNotInB.Evaluate(CreateContext(Record(("a", 3), ("b", new object?[] { 1, 2 })))).ShouldBeTrue();
        aNotInB.Evaluate(CreateContext(Record(("a", 3)))).ShouldBeTrue();
        aNotInB.Evaluate(CreateContext(Record(("a", 3), ("b", null)))).ShouldBeTrue();
        aNotInB.Evaluate(CreateContext(Record(("a", "x"), ("b", new object?[] { "x" })))).ShouldBeFalse();
        aNotInB.Evaluate(CreateContext(Record(("a", "x"), ("b", new object?[] { "y" })))).ShouldBeTrue();
        aNotInB.Evaluate(CreateContext(Record(("a", "x"), ("b", Record())))).ShouldBeTrue();
        aNotInB.Evaluate(CreateContext(Record(("a", "x"), ("b", Record(("x", false)))))).ShouldBeFalse();
        aNotInB.Evaluate(CreateContext(Record(("a", "x"), ("b", Record(("x", true)))))).ShouldBeFalse();
        aNotInB.Evaluate(CreateContext(Record(("a", "prototype"), ("b", Record())))).ShouldBeTrue();

        // file URI case-insensitive comparison on Windows
        if (OperatingSystem.IsWindows())
        {
            aNotInB.Evaluate(CreateContext(Record(("a", "file:///c%3A/Users/path/file.ts"), ("b", new object?[] { "file:///c%3A/users/path/file.ts" })))).ShouldBeFalse();
            aNotInB.Evaluate(CreateContext(Record(("a", "file:///c%3A/users/path/file.ts"), ("b", new object?[] { "file:///c%3A/Users/path/file.ts" })))).ShouldBeFalse();
            aNotInB.Evaluate(CreateContext(Record(("a", "git:/path/File.ts"), ("b", new object?[] { "git:/path/file.ts" })))).ShouldBeTrue();
        }
    }

    [Fact]
    public void Issue338834_distributing_an_OR_that_collapses_to_a_single_AND_produces_a_flat_AND()
    {
        // `b && !b` is false, so `b && (!b || a && c)` collapses to `a && b && c`
        var actual = ContextKeyExpr.And(
            ContextKeyExpr.Has("a"),
            ContextKeyExpr.Has("b"),
            ContextKeyExpr.Or(
                ContextKeyExpr.Not("b"),
                ContextKeyExpr.And(ContextKeyExpr.Has("a"), ContextKeyExpr.Has("c"))))!;
        actual.Serialize().ShouldBe("a && b && c");
        actual.Equals(ContextKeyExpr.And(ContextKeyExpr.Has("a"), ContextKeyExpr.Has("b"), ContextKeyExpr.Has("c"))!).ShouldBeTrue();
        actual.Equals(ContextKeyExpr.Deserialize(actual.Serialize())!).ShouldBeTrue();
    }

    [Fact]
    public void Distributing_AND_over_OR_is_not_affected_by_in_and_comparison_terms()
    {
        new[]
        {
            ContextKeyExpr.And(ContextKeyExpr.Or(ContextKeyExpr.Has("a"), ContextKeyExpr.Has("b")), ContextKeyExpr.Greater("c", 1))!.Serialize(),
            ContextKeyExpr.And(ContextKeyExpr.Or(ContextKeyExpr.Has("a"), ContextKeyExpr.Has("b")), ContextKeyExpr.In("c", "d"))!.Serialize(),
            ContextKeyExpr.And(ContextKeyExpr.Has("a"), ContextKeyExpr.Has("b"), ContextKeyExpr.Or(ContextKeyExpr.Not("b"), ContextKeyExpr.And(ContextKeyExpr.Not("b"), ContextKeyExpr.Has("c"))))!.Serialize(),
        }.ShouldBe(new[]
        {
            "a && c > 1 || b && c > 1",
            "a && c in 'd' || b && c in 'd'",
            "false",
        });
    }

    [Fact]
    public void Issue106524_distributing_AND_should_normalize()
    {
        var actual = ContextKeyExpr.And(
            ContextKeyExpr.Or(
                ContextKeyExpr.Has("a"),
                ContextKeyExpr.Has("b")),
            ContextKeyExpr.Has("c"));
        var expected = ContextKeyExpr.Or(
            ContextKeyExpr.And(
                ContextKeyExpr.Has("a"),
                ContextKeyExpr.Has("c")),
            ContextKeyExpr.And(
                ContextKeyExpr.Has("b"),
                ContextKeyExpr.Has("c")));
        actual!.Equals(expected!).ShouldBeTrue();
    }

    [Fact]
    public void Issue129625_Removes_duplicated_terms_in_OR_expressions()
    {
        var expr = ContextKeyExpr.Or(
            ContextKeyExpr.Has("A"),
            ContextKeyExpr.Has("B"),
            ContextKeyExpr.Has("A"))!;
        expr.Serialize().ShouldBe("A || B");
    }

    [Fact]
    public void Resolves_true_constant_OR_expressions()
    {
        var expr = ContextKeyExpr.Or(
            ContextKeyExpr.Has("A"),
            ContextKeyExpr.Not("A"))!;
        expr.Serialize().ShouldBe("true");
    }

    [Fact]
    public void Resolves_false_constant_AND_expressions()
    {
        var expr = ContextKeyExpr.And(
            ContextKeyExpr.Has("A"),
            ContextKeyExpr.Not("A"))!;
        expr.Serialize().ShouldBe("false");
    }

    [Fact]
    public void Issue129625_Removes_duplicated_terms_in_AND_expressions()
    {
        var expr = ContextKeyExpr.And(
            ContextKeyExpr.Has("A"),
            ContextKeyExpr.Has("B"),
            ContextKeyExpr.Has("A"))!;
        expr.Serialize().ShouldBe("A && B");
    }

    [Fact]
    public void Issue129625_Remove_duplicated_terms_when_negating()
    {
        var expr = ContextKeyExpr.And(
            ContextKeyExpr.Has("A"),
            ContextKeyExpr.Or(
                ContextKeyExpr.Has("B1"),
                ContextKeyExpr.Has("B2")))!;
        expr.Serialize().ShouldBe("A && B1 || A && B2");
        expr.Negate().Serialize().ShouldBe("!A || !A && !B1 || !A && !B2 || !B1 && !B2");
        expr.Negate().Negate().Serialize().ShouldBe("A && B1 || A && B2");
        expr.Negate().Negate().Negate().Serialize().ShouldBe("!A || !A && !B1 || !A && !B2 || !B1 && !B2");
    }

    private static bool StrImplies(string p0, string q0)
    {
        var p = ContextKeyExpr.Deserialize(p0)!;
        var q = ContextKeyExpr.Deserialize(q0)!;
        return ContextKeyExpressions.Implies(p, q);
    }

    [Fact]
    public void Issue129625_remove_redundant_terms_in_OR_expressions()
    {
        StrImplies("a && b", "a").ShouldBeTrue();
        StrImplies("a", "a && b").ShouldBeFalse();
    }

    [Fact]
    public void Implies()
    {
        StrImplies("a", "a").ShouldBeTrue();
        StrImplies("a", "a || b").ShouldBeTrue();
        StrImplies("a", "a && b").ShouldBeFalse();
        StrImplies("a", "a && b || a && c").ShouldBeFalse();
        StrImplies("a && b", "a").ShouldBeTrue();
        StrImplies("a && b", "b").ShouldBeTrue();
        StrImplies("a && b", "a && b || c").ShouldBeTrue();
        StrImplies("a || b", "a || c").ShouldBeFalse();
        StrImplies("a || b", "a || b").ShouldBeTrue();
        StrImplies("a && b", "a && b").ShouldBeTrue();
        StrImplies("a || b", "a || b || c").ShouldBeTrue();
        StrImplies("c && a && b", "c && a").ShouldBeTrue();
    }

    [Fact]
    public void Greater_GreaterEquals_Smaller_SmallerEquals_evaluate()
    {
        static void CheckEvaluate(string expr, Dictionary<string, object?> ctx, bool expected)
        {
            var parsed = ContextKeyExpr.Deserialize(expr)!;
            parsed.Evaluate(CreateContext(ctx)).ShouldBe(expected, expr);
        }

        CheckEvaluate("a > 1", Record(), false);
        CheckEvaluate("a > 1", Record(("a", 0)), false);
        CheckEvaluate("a > 1", Record(("a", 1)), false);
        CheckEvaluate("a > 1", Record(("a", 2)), true);
        CheckEvaluate("a > 1", Record(("a", "0")), false);
        CheckEvaluate("a > 1", Record(("a", "1")), false);
        CheckEvaluate("a > 1", Record(("a", "2")), true);
        CheckEvaluate("a > 1", Record(("a", "a")), false);

        CheckEvaluate("a > 10", Record(("a", 2)), false);
        CheckEvaluate("a > 10", Record(("a", 11)), true);
        CheckEvaluate("a > 10", Record(("a", "11")), true);
        CheckEvaluate("a > 10", Record(("a", "2")), false);
        CheckEvaluate("a > 10", Record(("a", "11")), true);

        CheckEvaluate("a > 1.1", Record(("a", 1)), false);
        CheckEvaluate("a > 1.1", Record(("a", 2)), true);
        CheckEvaluate("a > 1.1", Record(("a", 11)), true);
        CheckEvaluate("a > 1.1", Record(("a", "1.1")), false);
        CheckEvaluate("a > 1.1", Record(("a", "2")), true);
        CheckEvaluate("a > 1.1", Record(("a", "11")), true);

        CheckEvaluate("a > b", Record(("a", "b")), false);
        CheckEvaluate("a > b", Record(("a", "c")), false);
        CheckEvaluate("a > b", Record(("a", 1000)), false);

        CheckEvaluate("a >= 2", Record(("a", "1")), false);
        CheckEvaluate("a >= 2", Record(("a", "2")), true);
        CheckEvaluate("a >= 2", Record(("a", "3")), true);

        CheckEvaluate("a < 2", Record(("a", "1")), true);
        CheckEvaluate("a < 2", Record(("a", "2")), false);
        CheckEvaluate("a < 2", Record(("a", "3")), false);

        CheckEvaluate("a <= 2", Record(("a", "1")), true);
        CheckEvaluate("a <= 2", Record(("a", "2")), true);
        CheckEvaluate("a <= 2", Record(("a", "3")), false);
    }

    [Fact]
    public void Greater_GreaterEquals_Smaller_SmallerEquals_negate()
    {
        static void CheckNegate(string expr, string expected)
        {
            var a = ContextKeyExpr.Deserialize(expr)!;
            var b = a.Negate();
            b.Serialize().ShouldBe(expected);
        }

        CheckNegate("a > 1", "a <= 1");
        CheckNegate("a > 1.1", "a <= 1.1");
        CheckNegate("a > b", "a <= b");

        CheckNegate("a >= 1", "a < 1");
        CheckNegate("a >= 1.1", "a < 1.1");
        CheckNegate("a >= b", "a < b");

        CheckNegate("a < 1", "a >= 1");
        CheckNegate("a < 1.1", "a >= 1.1");
        CheckNegate("a < b", "a >= b");

        CheckNegate("a <= 1", "a > 1");
        CheckNegate("a <= 1.1", "a > 1.1");
        CheckNegate("a <= b", "a > b");
    }

    [Fact]
    public void Issue111899_context_keys_can_use_lt_or_gt()
    {
        var actual = ContextKeyExpr.Deserialize("editorTextFocus && vim.active && vim.use<C-r>")!;
        actual.Equals(
            ContextKeyExpr.And(
                ContextKeyExpr.Has("editorTextFocus"),
                ContextKeyExpr.Has("vim.active"),
                ContextKeyExpr.Has("vim.use<C-r>"))!).ShouldBeTrue();
    }

    // ---- not upstream: the regex mapping and RawContextKey ----

    [Fact]
    public void Regex_flags_source_and_serialization_follow_JavaScript()
    {
        var regexp = new Platform.JsRegExp("a/b", "mig");
        regexp.Source.ShouldBe("a\\/b");
        regexp.Flags.ShouldBe("gim");
        new Platform.JsRegExp("").Source.ShouldBe("(?:)");
        new Platform.JsRegExp("[/]").Source.ShouldBe("[/]");
        Should.Throw<ArgumentException>(() => new Platform.JsRegExp("a", "ii"));
        Should.Throw<ArgumentException>(() => new Platform.JsRegExp("a", "x"));
        Should.Throw<ArgumentException>(() => new Platform.JsRegExp("("));

        ContextKeyExpr.Deserialize("foo =~ /a\\/b/i")!.Serialize().ShouldBe("foo =~ /a\\/b/i");
        ContextKeyExpr.Deserialize("foo =~ /(/").ShouldBeNull();
    }

    [Fact]
    public void Regex_evaluates_with_JavaScript_semantics()
    {
        var context = CreateContext(Record(("name", "Hello\nWorld"), ("n", 42), ("list", new object?[] { "x", "y" })));

        ContextKeyExpr.Deserialize("name =~ /^World$/")!.Evaluate(context).ShouldBeFalse();
        ContextKeyExpr.Deserialize("name =~ /^World$/m")!.Evaluate(context).ShouldBeTrue();
        ContextKeyExpr.Deserialize("name =~ /hello/")!.Evaluate(context).ShouldBeFalse();
        ContextKeyExpr.Deserialize("name =~ /hello/i")!.Evaluate(context).ShouldBeTrue();
        ContextKeyExpr.Deserialize("name =~ /o.W/")!.Evaluate(context).ShouldBeFalse();
        ContextKeyExpr.Deserialize("name =~ /o.W/s")!.Evaluate(context).ShouldBeTrue();
        // $ without m does not match before a final newline, as in JavaScript (.NET's $ would)
        ContextKeyExpr.Deserialize("name =~ /d\\n$/")!.Evaluate(CreateContext(Record(("name", "World\n")))).ShouldBeTrue();
        ContextKeyExpr.Deserialize("name =~ /d$/")!.Evaluate(CreateContext(Record(("name", "World\n")))).ShouldBeFalse();
        // test() converts the value with String(value)
        ContextKeyExpr.Deserialize("n =~ /^42$/")!.Evaluate(context).ShouldBeTrue();
        ContextKeyExpr.Deserialize("list =~ /^x,y$/")!.Evaluate(context).ShouldBeTrue();
        ContextKeyExpr.Deserialize("missing =~ /^undefined$/")!.Evaluate(context).ShouldBeTrue();
        // the old quoted form, accepted only with regex error recovery (Deserialize has none)
        ContextKeyExpr.Deserialize("name =~ '/WORLD/i'").ShouldBeNull();
        new Parser().Parse("name =~ '/WORLD/i'")!.Evaluate(context).ShouldBeTrue();
        // negation
        ContextKeyExpr.Deserialize("name =~ /hello/")!.Negate().Serialize().ShouldBe("!(name =~ /hello/)");
        ContextKeyExpr.Deserialize("name =~ /hello/")!.Negate().Evaluate(context).ShouldBeTrue();
    }

    [Fact]
    public void Loose_equality_matches_JavaScript()
    {
        var context = CreateContext(Record(("n", 5), ("t", true), ("s", "1"), ("arr", new object?[] { 1, 2 })));

        ContextKeyExpr.Equals("n", "5").Evaluate(context).ShouldBeTrue(); // 5 == '5'
        ContextKeyExpr.Equals("n", 5.0).Evaluate(context).ShouldBeTrue(); // int and double are both numbers
        ContextKeyExpr.Equals("t", "1").Evaluate(context).ShouldBeTrue(); // true == '1'
        ContextKeyExpr.Equals("s", 1).Evaluate(context).ShouldBeTrue(); // '1' == 1
        ContextKeyExpr.Equals("arr", "1,2").Evaluate(context).ShouldBeTrue(); // [1, 2] == '1,2'
        ContextKeyExpr.Equals("missing", "undefined").Evaluate(context).ShouldBeFalse(); // undefined == 'undefined'
        ContextKeyExpr.NotEquals("n", "6").Evaluate(context).ShouldBeTrue();
    }

    [Fact]
    public void Keys_map_and_constant_substitution()
    {
        var expr = ContextKeyExpr.Deserialize("a && b == 'x' || c in d")!;
        expr.Keys().ShouldBe(new[] { "a", "b", "c", "d" });

        var renamed = expr.Map(new PrefixMapper("p."));
        // Map keeps the terms in place, it does not normalize again (as upstream)
        renamed.Serialize().ShouldBe("p.a && p.b == 'x' || p.c in 'p.d'");

        var isWindows = OperatingSystem.IsWindows();
        ContextKeyExpressions.ExpressionsAreEqualWithConstantSubstitution(
            ContextKeyExpr.Deserialize("isWindows && a"),
            isWindows ? ContextKeyExpr.Has("a") : ContextKeyExpr.False()).ShouldBeTrue();
        ContextKeyExpressions.ExpressionsAreEqualWithConstantSubstitution(null, null).ShouldBeTrue();
        ContextKeyExpressions.ExpressionsAreEqualWithConstantSubstitution(ContextKeyExpr.Has("a"), null).ShouldBeFalse();
    }

    [Fact]
    public void ValidateWhenClauses_reports_errors_per_clause()
    {
        var result = ContextKeyExpressions.ValidateWhenClauses(new[] { "a && b", "foo && 'bar", "!!foo", "" });
        result.Count.ShouldBe(4);
        result[0].ShouldBeEmpty();
        result[1].ShouldBe(new[] { new WhenClauseError("Unexpected token. Hint: " + Scanner.HintDidYouForgetToOpenOrCloseQuote, 7, 4) });
        result[2].ShouldBe(new[] { new WhenClauseError("Expected: KEY | true | false | '(' expression ')'\nReceived: '!'.", 1, 1) });
        // as upstream, an empty clause adds its error to the parser's previous parsing errors (Parse returns early
        // without clearing them), so the clause before it is reported again
        result[3].ShouldBe(new[]
        {
            new WhenClauseError("Expected: KEY | true | false | '(' expression ')'\nReceived: '!'.", 1, 1),
            new WhenClauseError(Parser.ErrorEmptyString + ". " + Parser.HintEmptyString, 0, 0),
        });
    }

    [Fact]
    public void RawContextKey_binds_reads_and_builds_expressions()
    {
        var key = new RawContextKey<int>("count", 3, "How many");
        key.Info.ShouldBe(new ContextKeyInfo("count", "number", "How many"));
        new RawContextKey<bool>("hidden", false, hide: true).Info.ShouldBeNull();
        new RawContextKey<bool?>("undef", null, "Undefined by default").Info.ShouldBe(new ContextKeyInfo("undef", null, "Undefined by default"));

        using var service = new ContextKeyService();
        var bound = key.BindTo(service);
        bound.Get().ShouldBe(3);
        key.GetValue(service).ShouldBe(3);
        service.ContextMatchesRules(key).ShouldBeTrue();
        service.ContextMatchesRules(key.Greater(2)).ShouldBeTrue();
        service.ContextMatchesRules(key.IsEqualTo(3)).ShouldBeTrue();
        service.ContextMatchesRules(key.NotEqualsTo(3)).ShouldBeFalse();
        bound.Set(0);
        service.ContextMatchesRules(key.ToNegated()).ShouldBeTrue();
        bound.Reset();
        key.GetValue(service).ShouldBe(3);
    }

    [Fact]
    public void EditorContextKeys_are_described()
    {
        EditorContextKeys.EditorTextFocus.Key.ShouldBe("editorTextFocus");
        EditorContextKeys.Writable.Serialize().ShouldBe("!editorReadonly");
        EditorContextKeys.HasSingleSelection.Serialize().ShouldBe("!editorHasMultipleSelections");
        EditorContextKeys.All.ShouldContain(new ContextKeyInfo("editorReadonly", "boolean", "Whether the editor is read-only"));
        EditorContextKeys.All.ShouldContain(new ContextKeyInfo("editorLangId", "string", "The language identifier of the editor"));
        EditorContextKeys.All.ShouldNotContain(info => info.Key == "canUndo");
        EditorContextKeys.All.Select(info => info.Key).ShouldBeUnique();
    }

    private sealed class PrefixMapper(string prefix) : IContextKeyExprMapper
    {
        public ContextKeyExpression MapDefined(string key) => ContextKeyDefinedExpr.Create(prefix + key);
        public ContextKeyExpression MapNot(string key) => ContextKeyNotExpr.Create(prefix + key);
        public ContextKeyExpression MapEquals(string key, object? value) => ContextKeyEqualsExpr.Create(prefix + key, value);
        public ContextKeyExpression MapNotEquals(string key, object? value) => ContextKeyNotEqualsExpr.Create(prefix + key, value);
        public ContextKeyExpression MapGreater(string key, object? value) => ContextKeyGreaterExpr.Create(prefix + key, value);
        public ContextKeyExpression MapGreaterEquals(string key, object? value) => ContextKeyGreaterEqualsExpr.Create(prefix + key, value);
        public ContextKeyExpression MapSmaller(string key, object? value) => ContextKeySmallerExpr.Create(prefix + key, value);
        public ContextKeyExpression MapSmallerEquals(string key, object? value) => ContextKeySmallerEqualsExpr.Create(prefix + key, value);
        public ContextKeyRegexExpr MapRegex(string key, Platform.JsRegExp? regexp) => ContextKeyRegexExpr.Create(prefix + key, regexp);
        public ContextKeyInExpr MapIn(string key, string valueKey) => Platform.ContextKeyInExpr.Create(prefix + key, prefix + valueKey);
        public ContextKeyNotInExpr MapNotIn(string key, string valueKey) => Platform.ContextKeyNotInExpr.Create(prefix + key, prefix + valueKey);
    }
}
