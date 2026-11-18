using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>Checks that hold for every well-formed triangle geometry.</summary>
internal static class GeometryAssert
{
    // Welds positions that differ only by rounding (a closed tube ends on the points it started from).
    public static (long, long, long) Key(Vector3 p) => ((long)MathF.Round(p.X * 2e3f), (long)MathF.Round(p.Y * 2e3f), (long)MathF.Round(p.Z * 2e3f));

    public static List<(Vector3 A, Vector3 B, Vector3 C, int Ia, int Ib, int Ic)> Triangles(BufferGeometry g)
    {
        var position = g.GetAttribute(BufferGeometry.Position)!;
        var list = new List<(Vector3, Vector3, Vector3, int, int, int)>();
        for (var i = 0; i + 2 < g.ElementCount; i += 3)
        {
            int a = g.GetIndex(i), b = g.GetIndex(i + 1), c = g.GetIndex(i + 2);
            list.Add((position.GetVector3(a), position.GetVector3(b), position.GetVector3(c), a, b, c));
        }

        return list;
    }

    /// <summary>Attributes present and consistent, indices in range, unit normals, finite values.</summary>
    public static void WellFormed(BufferGeometry g, string name, bool expectUv = true)
    {
        var position = g.GetAttribute(BufferGeometry.Position);
        position.ShouldNotBeNull(name);
        var normal = g.GetAttribute(BufferGeometry.Normal);
        normal.ShouldNotBeNull(name);
        normal!.Count.ShouldBe(position!.Count, name);
        if (expectUv)
        {
            var uv = g.GetAttribute(BufferGeometry.Uv);
            uv.ShouldNotBeNull(name);
            uv!.Count.ShouldBe(position.Count, name);
            foreach (var f in uv.FloatData)
            {
                float.IsFinite(f).ShouldBeTrue(name);
            }
        }

        g.Index.ShouldNotBeNull(name);
        (g.IndexCount % 3).ShouldBe(0, name);
        for (var i = 0; i < g.IndexCount; i++)
        {
            g.GetIndex(i).ShouldBeInRange(0, position.Count - 1, name);
        }

        for (var i = 0; i < position.Count; i++)
        {
            var p = position.GetVector3(i);
            (float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)).ShouldBeTrue(name);
            normal.GetVector3(i).Length().ShouldBe(1f, 1e-3f, $"{name}: normal {i}");
        }
    }

    /// <summary>Every triangle's geometric normal agrees with the normals of its vertices: the winding is
    /// counter-clockwise seen from where the normals point.</summary>
    public static void WindingMatchesNormals(BufferGeometry g, string name)
    {
        var normal = g.GetAttribute(BufferGeometry.Normal)!;
        var checkedCount = 0;
        foreach (var (a, b, c, ia, ib, ic) in Triangles(g))
        {
            var face = Vector3.Cross(b - a, c - a);
            if (face.Length() < 1e-9f)
            {
                continue;
            }

            var average = normal.GetVector3(ia) + normal.GetVector3(ib) + normal.GetVector3(ic);
            Vector3.Dot(Vector3.Normalize(face), Vector3.Normalize(average)).ShouldBeGreaterThan(0.05f, $"{name}: triangle {ia},{ib},{ic} faces away from its normals");
            checkedCount++;
        }

        checkedCount.ShouldBeGreaterThan(0, name);
    }

    /// <summary>Every edge is used by exactly two triangles, in opposite directions (positions are welded first), and
    /// the enclosed volume is positive (faces point outward).</summary>
    public static void Watertight(BufferGeometry g, string name)
    {
        var edges = new Dictionary<((long, long, long), (long, long, long)), int>();
        double volume = 0;
        foreach (var (a, b, c, _, _, _) in Triangles(g))
        {
            var (ka, kb, kc) = (Key(a), Key(b), Key(c));
            if (ka == kb || kb == kc || kc == ka)
            {
                continue;
            }

            volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            foreach (var edge in new[] { (ka, kb), (kb, kc), (kc, ka) })
            {
                edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }

        edges.Count.ShouldBeGreaterThan(0, name);
        foreach (var (edge, count) in edges)
        {
            count.ShouldBe(1, $"{name}: edge {edge} is used {count} times in the same direction");
            edges.GetValueOrDefault((edge.Item2, edge.Item1)).ShouldBe(1, $"{name}: edge {edge} has no opposite edge (a hole in the surface)");
        }

        volume.ShouldBeGreaterThan(0.0, $"{name}: faces point inward");
    }

    public static void ShouldBeNear(this Vector3 actual, Vector3 expected, float tolerance = 1e-4f, string? message = null)
    {
        if (Vector3.Distance(actual, expected) > tolerance)
        {
            throw new ShouldAssertException($"{message} expected {expected} but was {actual} (tolerance {tolerance})".Trim());
        }
    }

    public static void ShouldBeNear(this Vector2 actual, Vector2 expected, float tolerance = 1e-4f, string? message = null)
    {
        if (Vector2.Distance(actual, expected) > tolerance)
        {
            throw new ShouldAssertException($"{message} expected {expected} but was {actual} (tolerance {tolerance})".Trim());
        }
    }

    public static float Area(BufferGeometry g)
    {
        var sum = 0f;
        foreach (var (a, b, c, _, _, _) in Triangles(g))
        {
            sum += Vector3.Cross(b - a, c - a).Length() * 0.5f;
        }

        return sum;
    }

    public static double Volume(BufferGeometry g)
    {
        double volume = 0;
        foreach (var (a, b, c, _, _, _) in Triangles(g))
        {
            volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
        }

        return volume;
    }
}
