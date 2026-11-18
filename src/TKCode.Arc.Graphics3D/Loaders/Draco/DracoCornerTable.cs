using System;
using System.Collections.Generic;

namespace TKCode.Arc.Graphics3D;

/// <summary>
/// Triangle connectivity as a corner table: corner <c>3f + k</c> is the k-th corner of face f, each corner
/// knows its vertex and the corner opposite to it across the facing edge (-1 on a boundary).
/// The same class describes an attribute's connectivity, where edges on an attribute seam count as boundaries.
/// </summary>
internal sealed class DracoCornerTable
{
    public int[] CornerToVertex = Array.Empty<int>();
    public int[] Opposite = Array.Empty<int>();

    /// <summary>For each vertex its left-most corner (the first one of its fan), or -1 when isolated.</summary>
    public int[] VertexCorners = Array.Empty<int>();

    public int NumVertices;
    public int NumFaces;

    public int NumCorners => NumFaces * 3;

    public static int Next(int corner)
    {
        if (corner < 0)
        {
            return corner;
        }

        return corner % 3 == 2 ? corner - 2 : corner + 1;
    }

    public static int Previous(int corner)
    {
        if (corner < 0)
        {
            return corner;
        }

        return corner % 3 == 0 ? corner + 2 : corner - 1;
    }

    public int Vertex(int corner)
    {
        return corner < 0 ? -1 : CornerToVertex[corner];
    }

    public int Opp(int corner)
    {
        return corner < 0 ? -1 : Opposite[corner];
    }

    public int SwingRight(int corner)
    {
        return Previous(Opp(Previous(corner)));
    }

    public int SwingLeft(int corner)
    {
        return Next(Opp(Next(corner)));
    }

    public int RightCorner(int corner)
    {
        return Opp(Next(corner));
    }

    public int LeftCorner(int corner)
    {
        return Opp(Previous(corner));
    }

    public bool IsOnBoundary(int vertex)
    {
        return SwingLeft(VertexCorners[vertex]) < 0;
    }

    /// <summary>
    /// Makes room for the corners of <paramref name="faces"/> faces. The arrays grow with the faces that
    /// are really decoded instead of being sized from the declared face count.
    /// </summary>
    public void EnsureFaceCapacity(int faces, DracoLimits limits)
    {
        int needed = faces * 3;
        int current = CornerToVertex.Length;
        if (needed <= current)
        {
            return;
        }

        int length = (int)Math.Min(NumFaces * 3L, Math.Max(current * 2L, 3 * 1024));
        length = Math.Max(length, needed);
        limits.Charge(2L * sizeof(int) * length);
        Array.Resize(ref CornerToVertex, length);
        Array.Resize(ref Opposite, length);
        CornerToVertex.AsSpan(current).Fill(-1);
        Opposite.AsSpan(current).Fill(-1);
    }

    public int AddVertex(DracoLimits limits)
    {
        if (NumVertices == VertexCorners.Length)
        {
            int length = Math.Max(1024, NumVertices * 2);
            limits.Charge((long)sizeof(int) * length);
            Array.Resize(ref VertexCorners, length);
        }

        VertexCorners[NumVertices] = -1;
        return NumVertices++;
    }

    public void SetOpposite(int a, int b)
    {
        Opposite[a] = b;
        Opposite[b] = a;
    }
}

/// <summary>What a traversal recorded: the order attribute values are stored in, and where each one sits on the mesh.</summary>
internal sealed class DracoEncodingData
{
    /// <summary>For each stored value, the corner it was first reached through.</summary>
    public int[] DataToCorner = Array.Empty<int>();

    /// <summary>For each vertex of the traversed table, the index of its stored value.</summary>
    public int[] VertexToData = Array.Empty<int>();

    public int NumValues;
}

/// <summary>
/// Walks the mesh in the order the encoder did, which is the order attribute values are stored in.
/// </summary>
internal sealed class DracoTraversal
{
    private const int MaxPriority = 3;

    private readonly DracoCornerTable _table;
    private readonly int[] _faces;
    private readonly DracoEncodingData _encoding;
    private readonly bool[] _faceVisited;
    private readonly bool[] _vertexVisited;
    private readonly int[] _pointIds;
    private readonly List<int> _stack = new();
    private List<int>[]? _priorityStacks;
    private int[]? _predictionDegree;
    private int _bestPriority;

    private DracoTraversal(DracoCornerTable table, int[] faces, DracoEncodingData encoding, DracoLimits limits)
    {
        _table = table;
        _faces = faces;
        _encoding = encoding;
        _faceVisited = limits.Bools(table.NumFaces);
        _vertexVisited = limits.Bools(table.NumVertices);
        _pointIds = limits.Ints(table.NumVertices);
        encoding.DataToCorner = limits.Ints(table.NumVertices);
        encoding.NumValues = 0;
        if (encoding.VertexToData.Length < table.NumVertices)
        {
            encoding.VertexToData = limits.Ints(table.NumVertices);
        }
    }

    /// <summary>
    /// Returns the point of each stored value, in storage order, and fills <paramref name="encoding"/>.
    /// <paramref name="faces"/> holds the point of every corner.
    /// </summary>
    public static int[] GenerateSequence(
        DracoCornerTable table,
        int[] faces,
        DracoEncodingData encoding,
        bool maxPredictionDegree,
        DracoLimits limits,
        out int count)
    {
        DracoTraversal traversal = new(table, faces, encoding, limits);
        if (maxPredictionDegree)
        {
            traversal._priorityStacks = new List<int>[MaxPriority];
            for (int i = 0; i < MaxPriority; i++)
            {
                traversal._priorityStacks[i] = new List<int>();
            }

            traversal._predictionDegree = limits.Ints(table.NumVertices);
        }

        for (int face = 0; face < table.NumFaces; face++)
        {
            if (maxPredictionDegree)
            {
                traversal.TraversePredictionDegree(3 * face);
            }
            else
            {
                traversal.TraverseDepthFirst(3 * face);
            }
        }

        count = encoding.NumValues;
        return traversal._pointIds;
    }

    /// <summary>The value index of each point: every corner's point gets the value of the corner's vertex.</summary>
    public static int[] MapPointsToValues(DracoCornerTable table, int[] faces, DracoEncodingData encoding, int numPoints, DracoLimits limits)
    {
        int[] map = limits.Ints(numPoints);
        int[] vertexToData = encoding.VertexToData;
        int numCorners = table.NumCorners;
        for (int corner = 0; corner < numCorners; corner++)
        {
            int vertex = table.CornerToVertex[corner];
            if ((uint)vertex >= (uint)vertexToData.Length)
            {
                throw DracoError.Invalid("a corner has no attribute vertex.");
            }

            int point = faces[corner];
            int entry = vertexToData[vertex];
            if ((uint)point >= (uint)numPoints || (uint)entry >= (uint)numPoints)
            {
                throw DracoError.Invalid("a point index is out of range.");
            }

            map[point] = entry;
        }

        return map;
    }

    private void Visit(int vertex, int corner)
    {
        _vertexVisited[vertex] = true;
        int index = _encoding.NumValues++;
        _pointIds[index] = _faces[corner];
        _encoding.DataToCorner[index] = corner;
        _encoding.VertexToData[vertex] = index;
    }

    private int VertexOf(int corner)
    {
        int vertex = _table.Vertex(corner);
        if ((uint)vertex >= (uint)_vertexVisited.Length)
        {
            throw DracoError.Invalid("a corner has no vertex.");
        }

        return vertex;
    }

    private bool IsFaceVisited(int face)
    {
        return face < 0 || _faceVisited[face];
    }

    private void TraverseDepthFirst(int corner)
    {
        if (_faceVisited[corner / 3])
        {
            return;
        }

        DracoCornerTable table = _table;
        List<int> stack = _stack;
        stack.Clear();
        stack.Add(corner);
        int nextCorner = DracoCornerTable.Next(corner);
        int previousCorner = DracoCornerTable.Previous(corner);
        int nextVertex = VertexOf(nextCorner);
        int previousVertex = VertexOf(previousCorner);
        if (!_vertexVisited[nextVertex])
        {
            Visit(nextVertex, nextCorner);
        }

        if (!_vertexVisited[previousVertex])
        {
            Visit(previousVertex, previousCorner);
        }

        while (stack.Count > 0)
        {
            corner = stack[stack.Count - 1];
            if (corner < 0 || _faceVisited[corner / 3])
            {
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            int face = corner / 3;
            while (true)
            {
                _faceVisited[face] = true;
                int vertex = VertexOf(corner);
                if (!_vertexVisited[vertex])
                {
                    bool onBoundary = table.IsOnBoundary(vertex);
                    Visit(vertex, corner);
                    if (!onBoundary)
                    {
                        // An interior vertex was reached for the first time: keep turning around it.
                        corner = table.RightCorner(corner);
                        if (corner < 0)
                        {
                            throw DracoError.Invalid("the connectivity is inconsistent.");
                        }

                        face = corner / 3;
                        continue;
                    }
                }

                int right = table.RightCorner(corner);
                int left = table.LeftCorner(corner);
                int rightFace = right < 0 ? -1 : right / 3;
                int leftFace = left < 0 ? -1 : left / 3;
                if (IsFaceVisited(rightFace))
                {
                    if (IsFaceVisited(leftFace))
                    {
                        stack.RemoveAt(stack.Count - 1);
                        break;
                    }

                    corner = left;
                    face = leftFace;
                }
                else if (IsFaceVisited(leftFace))
                {
                    corner = right;
                    face = rightFace;
                }
                else
                {
                    // Both neighbours are new: go right first and come back for the left one.
                    stack[stack.Count - 1] = left;
                    stack.Add(right);
                    break;
                }
            }
        }
    }

    private void TraversePredictionDegree(int corner)
    {
        DracoCornerTable table = _table;
        List<int>[] stacks = _priorityStacks!;
        if (_predictionDegree!.Length == 0)
        {
            return;
        }

        stacks[0].Add(corner);
        _bestPriority = 0;
        int nextCorner = DracoCornerTable.Next(corner);
        int previousCorner = DracoCornerTable.Previous(corner);
        int nextVertex = VertexOf(nextCorner);
        int previousVertex = VertexOf(previousCorner);
        int tipVertex = VertexOf(corner);
        if (!_vertexVisited[nextVertex])
        {
            Visit(nextVertex, nextCorner);
        }

        if (!_vertexVisited[previousVertex])
        {
            Visit(previousVertex, previousCorner);
        }

        if (!_vertexVisited[tipVertex])
        {
            Visit(tipVertex, corner);
        }

        while ((corner = PopNextCorner()) >= 0)
        {
            if (_faceVisited[corner / 3])
            {
                continue;
            }

            while (true)
            {
                _faceVisited[corner / 3] = true;
                int vertex = VertexOf(corner);
                if (!_vertexVisited[vertex])
                {
                    Visit(vertex, corner);
                }

                int right = table.RightCorner(corner);
                int left = table.LeftCorner(corner);
                bool rightVisited = right < 0 || _faceVisited[right / 3];
                bool leftVisited = left < 0 || _faceVisited[left / 3];
                if (!leftVisited)
                {
                    int priority = ComputePriority(left);
                    if (rightVisited && priority <= _bestPriority)
                    {
                        corner = left;
                        continue;
                    }

                    AddCorner(left, priority);
                }

                if (!rightVisited)
                {
                    int priority = ComputePriority(right);
                    if (priority <= _bestPriority)
                    {
                        corner = right;
                        continue;
                    }

                    AddCorner(right, priority);
                }

                break;
            }
        }
    }

    private int PopNextCorner()
    {
        List<int>[] stacks = _priorityStacks!;
        for (int i = _bestPriority; i < MaxPriority; i++)
        {
            List<int> stack = stacks[i];
            if (stack.Count > 0)
            {
                int corner = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                _bestPriority = i;
                return corner;
            }
        }

        return -1;
    }

    private void AddCorner(int corner, int priority)
    {
        _priorityStacks![priority].Add(corner);
        if (priority < _bestPriority)
        {
            _bestPriority = priority;
        }
    }

    // Faces whose tip vertex is already decoded come first, then the ones whose tip can be predicted from
    // two or more decoded neighbours.
    private int ComputePriority(int corner)
    {
        int tip = VertexOf(corner);
        if (_vertexVisited[tip])
        {
            return 0;
        }

        int degree = ++_predictionDegree![tip];
        return degree > 1 ? 1 : 2;
    }
}
