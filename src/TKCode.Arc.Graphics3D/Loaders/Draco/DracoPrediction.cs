using System;
using System.Buffers.Binary;

namespace TKCode.Arc.Graphics3D;

/// <summary>
/// Reads the positions another attribute is predicted from. From 2.0 on these are the quantized integers
/// ("portable" values); older streams predict from the final values.
/// </summary>
internal sealed class DracoPositionSource
{
    private readonly DracoAttributeData _attribute;
    private readonly bool _portable;

    public DracoPositionSource(DracoAttributeData attribute, bool portable)
    {
        _attribute = attribute;
        _portable = portable;
    }

    public void GetInt64(int point, out long x, out long y, out long z)
    {
        int index = ValueIndex(point);
        if (_portable)
        {
            int[] values = _attribute.Portable!;
            x = values[index];
            y = values[index + 1];
            z = values[index + 2];
            return;
        }

        x = ToInt64(index, 0);
        y = ToInt64(index, 1);
        z = ToInt64(index, 2);
    }

    public void GetSingle(int point, out float x, out float y, out float z)
    {
        int index = ValueIndex(point);
        if (_portable)
        {
            int[] values = _attribute.Portable!;
            x = values[index];
            y = values[index + 1];
            z = values[index + 2];
            return;
        }

        x = ToSingle(index, 0);
        y = ToSingle(index, 1);
        z = ToSingle(index, 2);
    }

    // Offset of the point's value: in components for portable values, in entries otherwise.
    private int ValueIndex(int point)
    {
        int[]? map = _attribute.PointToValue;
        int entry = point;
        if (map != null)
        {
            if ((uint)point >= (uint)map.Length)
            {
                throw DracoError.Invalid("a point index is out of range.");
            }

            entry = map[point];
        }

        if ((uint)entry >= (uint)_attribute.NumEntries)
        {
            throw DracoError.Invalid("a position index is out of range.");
        }

        return _portable ? entry * 3 : entry;
    }

    private long ToInt64(int entry, int component)
    {
        ReadOnlySpan<byte> source = Component(entry, component);
        switch (_attribute.DataType)
        {
            case DracoDataType.Int8:
                return unchecked((sbyte)source[0]);
            case DracoDataType.UInt8:
                return source[0];
            case DracoDataType.Int16:
                return BinaryPrimitives.ReadInt16LittleEndian(source);
            case DracoDataType.UInt16:
                return BinaryPrimitives.ReadUInt16LittleEndian(source);
            case DracoDataType.Int32:
                return BinaryPrimitives.ReadInt32LittleEndian(source);
            case DracoDataType.UInt32:
                return BinaryPrimitives.ReadUInt32LittleEndian(source);
            default:
                float value = BinaryPrimitives.ReadSingleLittleEndian(source);
                if (float.IsNaN(value) || float.IsInfinity(value) || _attribute.Normalized
                    || value < -9.2233720368547758E+18f || value >= 9.2233720368547758E+18f)
                {
                    throw DracoError.Invalid("a position cannot be used for prediction.");
                }

                return (long)value;
        }
    }

    private float ToSingle(int entry, int component)
    {
        ReadOnlySpan<byte> source = Component(entry, component);
        bool normalized = _attribute.Normalized;
        switch (_attribute.DataType)
        {
            case DracoDataType.Int8:
                return normalized ? unchecked((sbyte)source[0]) / (float)sbyte.MaxValue : unchecked((sbyte)source[0]);
            case DracoDataType.UInt8:
                return normalized ? source[0] / (float)byte.MaxValue : source[0];
            case DracoDataType.Int16:
                short int16 = BinaryPrimitives.ReadInt16LittleEndian(source);
                return normalized ? int16 / (float)short.MaxValue : int16;
            case DracoDataType.UInt16:
                ushort uint16 = BinaryPrimitives.ReadUInt16LittleEndian(source);
                return normalized ? uint16 / (float)ushort.MaxValue : uint16;
            case DracoDataType.Int32:
                int int32 = BinaryPrimitives.ReadInt32LittleEndian(source);
                return normalized ? int32 / (float)int.MaxValue : int32;
            case DracoDataType.UInt32:
                uint uint32 = BinaryPrimitives.ReadUInt32LittleEndian(source);
                return normalized ? uint32 / (float)uint.MaxValue : uint32;
            default:
                return BinaryPrimitives.ReadSingleLittleEndian(source);
        }
    }

    private ReadOnlySpan<byte> Component(int entry, int component)
    {
        int size = _attribute.ComponentSize;
        return _attribute.Values.AsSpan((entry * _attribute.ByteStride) + (component * size), size);
    }
}

/// <summary>Undoes a prediction: stored values are corrections to what the decoded part of the mesh predicts.</summary>
internal abstract class DracoPredictionScheme
{
    protected readonly DracoPredictionTransform Transform;

    protected DracoPredictionScheme(DracoPredictionTransform transform)
    {
        Transform = transform;
    }

    public bool AreCorrectionsPositive => Transform.AreCorrectionsPositive;

    /// <summary>True when the scheme predicts from the position attribute.</summary>
    public virtual bool NeedsPositions => false;

    public DracoPositionSource? Positions { get; set; }

    public virtual void DecodePredictionData(DracoBuffer buffer, int numValues)
    {
        Transform.DecodeTransformData(buffer);
    }

    /// <summary>Replaces the corrections in <paramref name="data"/> with the original values, in storage order.</summary>
    public abstract void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint);
}

/// <summary>Each value is predicted by the previous one.</summary>
internal sealed class DracoDeltaPrediction : DracoPredictionScheme
{
    public DracoDeltaPrediction(DracoPredictionTransform transform)
        : base(transform)
    {
    }

    public override void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint)
    {
        int[] zero = new int[numComponents];
        Transform.ComputeOriginalValue(zero, 0, data, 0, numComponents);
        for (int i = numComponents; i < data.Length; i += numComponents)
        {
            Transform.ComputeOriginalValue(data, i - numComponents, data, i, numComponents);
        }
    }
}

/// <summary>Base of the schemes that predict from neighbouring values on the mesh.</summary>
internal abstract class DracoMeshPrediction : DracoPredictionScheme
{
    protected readonly DracoCornerTable Table;
    protected readonly DracoEncodingData Encoding;
    private int[]? _entryToPoint;

    protected DracoMeshPrediction(DracoPredictionTransform transform, DracoCornerTable table, DracoEncodingData encoding)
        : base(transform)
    {
        Table = table;
        Encoding = encoding;
    }

    /// <summary>Number of values that have a place on the mesh; it must fit in the decoded data.</summary>
    protected int CheckValueCount(int[] data, int numComponents)
    {
        int count = Encoding.NumValues;
        if ((long)count * numComponents > data.Length)
        {
            throw DracoError.Invalid("fewer attribute values than mesh vertices.");
        }

        return count;
    }

    /// <summary>Index of the value stored for the vertex at a corner.</summary>
    protected int DataId(int corner)
    {
        int vertex = Table.Vertex(corner);
        int[] vertexToData = Encoding.VertexToData;
        if ((uint)vertex >= (uint)vertexToData.Length)
        {
            throw DracoError.Invalid("a corner has no attribute vertex.");
        }

        return vertexToData[vertex];
    }

    protected void SetEntryToPoint(int[]? entryToPoint)
    {
        _entryToPoint = entryToPoint;
    }

    protected int PointOfEntry(int entry)
    {
        int[]? map = _entryToPoint;
        if (map == null)
        {
            return entry;
        }

        if ((uint)entry >= (uint)map.Length)
        {
            throw DracoError.Invalid("an attribute value index is out of range.");
        }

        return map[entry];
    }

    /// <summary>
    /// Predicts the value at <paramref name="corner"/> from the triangle across its opposite edge:
    /// the fourth corner of the parallelogram the three known values span.
    /// </summary>
    protected bool ComputeParallelogram(int entry, int corner, int[] data, int numComponents, int[] prediction)
    {
        int oppositeCorner = Table.Opp(corner);
        if (oppositeCorner < 0)
        {
            return false;
        }

        int opposite = DataId(oppositeCorner);
        int next = DataId(DracoCornerTable.Next(oppositeCorner));
        int previous = DataId(DracoCornerTable.Previous(oppositeCorner));
        if (opposite >= entry || next >= entry || previous >= entry)
        {
            return false;
        }

        opposite *= numComponents;
        next *= numComponents;
        previous *= numComponents;
        for (int c = 0; c < numComponents; c++)
        {
            prediction[c] = unchecked((int)((long)data[next + c] + data[previous + c] - data[opposite + c]));
        }

        return true;
    }
}

/// <summary>Parallelogram prediction from the one triangle the value was reached through.</summary>
internal sealed class DracoParallelogramPrediction : DracoMeshPrediction
{
    public DracoParallelogramPrediction(DracoPredictionTransform transform, DracoCornerTable table, DracoEncodingData encoding)
        : base(transform, table, encoding)
    {
    }

    public override void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint)
    {
        int[] prediction = new int[numComponents];
        Transform.ComputeOriginalValue(prediction, 0, data, 0, numComponents);
        int count = CheckValueCount(data, numComponents);
        int[] dataToCorner = Encoding.DataToCorner;
        for (int p = 1; p < count; p++)
        {
            int offset = p * numComponents;
            if (ComputeParallelogram(p, dataToCorner[p], data, numComponents, prediction))
            {
                Transform.ComputeOriginalValue(prediction, 0, data, offset, numComponents);
            }
            else
            {
                Transform.ComputeOriginalValue(data, offset - numComponents, data, offset, numComponents);
            }
        }
    }
}

/// <summary>Average of the parallelogram predictions of all triangles around the vertex.</summary>
internal sealed class DracoMultiParallelogramPrediction : DracoMeshPrediction
{
    public DracoMultiParallelogramPrediction(DracoPredictionTransform transform, DracoCornerTable table, DracoEncodingData encoding)
        : base(transform, table, encoding)
    {
    }

    public override void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint)
    {
        int[] prediction = new int[numComponents];
        int[] single = new int[numComponents];
        Transform.ComputeOriginalValue(prediction, 0, data, 0, numComponents);
        int count = CheckValueCount(data, numComponents);
        int[] dataToCorner = Encoding.DataToCorner;
        int numCorners = Table.NumCorners;
        for (int p = 1; p < count; p++)
        {
            int start = dataToCorner[p];
            int corner = start;
            int parallelograms = 0;
            int guard = numCorners;
            Array.Clear(prediction, 0, numComponents);
            while (corner >= 0 && guard-- > 0)
            {
                if (ComputeParallelogram(p, corner, data, numComponents, single))
                {
                    for (int c = 0; c < numComponents; c++)
                    {
                        prediction[c] = unchecked(prediction[c] + single[c]);
                    }

                    parallelograms++;
                }

                corner = Table.SwingRight(corner);
                if (corner == start)
                {
                    break;
                }
            }

            int offset = p * numComponents;
            if (parallelograms == 0)
            {
                Transform.ComputeOriginalValue(data, offset - numComponents, data, offset, numComponents);
            }
            else
            {
                for (int c = 0; c < numComponents; c++)
                {
                    prediction[c] /= parallelograms;
                }

                Transform.ComputeOriginalValue(prediction, 0, data, offset, numComponents);
            }
        }
    }
}

/// <summary>
/// As the multi parallelogram scheme, but the stream says for each parallelogram whether it lies across a
/// crease and must be left out of the average.
/// </summary>
internal sealed class DracoConstrainedMultiParallelogramPrediction : DracoMeshPrediction
{
    private const int MaxParallelograms = 4;

    private readonly bool[][] _isCrease = new bool[MaxParallelograms][];

    public DracoConstrainedMultiParallelogramPrediction(DracoPredictionTransform transform, DracoCornerTable table, DracoEncodingData encoding)
        : base(transform, table, encoding)
    {
    }

    public override void DecodePredictionData(DracoBuffer buffer, int numValues)
    {
        if (buffer.Version < DracoVersion.V2_2 && buffer.ReadByte() != 0)
        {
            throw DracoError.Invalid("unknown constrained multi parallelogram mode.");
        }

        // One list of flags per number of parallelograms available at a vertex.
        for (int i = 0; i < MaxParallelograms; i++)
        {
            uint numFlags = buffer.ReadVarUInt32();
            if (numFlags > (uint)Table.NumCorners)
            {
                throw DracoError.Invalid("more crease flags than corners.");
            }

            bool[] flags = new bool[numFlags];
            if (numFlags > 0)
            {
                DracoRansBitDecoder decoder = DracoRansBitDecoder.Start(buffer);
                for (int j = 0; j < flags.Length; j++)
                {
                    flags[j] = decoder.DecodeBit();
                }
            }

            _isCrease[i] = flags;
        }

        base.DecodePredictionData(buffer, numValues);
    }

    public override void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint)
    {
        int[][] predictions = new int[MaxParallelograms][];
        for (int i = 0; i < MaxParallelograms; i++)
        {
            predictions[i] = new int[numComponents];
        }

        int[] average = new int[numComponents];
        int[] flagPositions = new int[MaxParallelograms];
        Transform.ComputeOriginalValue(predictions[0], 0, data, 0, numComponents);
        int count = CheckValueCount(data, numComponents);
        int[] dataToCorner = Encoding.DataToCorner;
        int numCorners = Table.NumCorners;
        for (int p = 1; p < count; p++)
        {
            // Collect up to four parallelograms: first turning left around the vertex, then right.
            int start = dataToCorner[p];
            int corner = start;
            int parallelograms = 0;
            bool firstPass = true;
            int guard = 2 * numCorners;
            while (corner >= 0 && guard-- > 0)
            {
                if (ComputeParallelogram(p, corner, data, numComponents, predictions[parallelograms]))
                {
                    parallelograms++;
                    if (parallelograms == MaxParallelograms)
                    {
                        break;
                    }
                }

                corner = firstPass ? Table.SwingLeft(corner) : Table.SwingRight(corner);
                if (corner == start)
                {
                    break;
                }

                if (corner < 0 && firstPass)
                {
                    firstPass = false;
                    corner = Table.SwingRight(start);
                }
            }

            int used = 0;
            if (parallelograms > 0)
            {
                Array.Clear(average, 0, numComponents);
                bool[] flags = _isCrease[parallelograms - 1];
                for (int i = 0; i < parallelograms; i++)
                {
                    int position = flagPositions[parallelograms - 1]++;
                    if (position >= flags.Length)
                    {
                        throw DracoError.Invalid("too few crease flags.");
                    }

                    if (!flags[position])
                    {
                        used++;
                        int[] prediction = predictions[i];
                        for (int c = 0; c < numComponents; c++)
                        {
                            average[c] = unchecked(average[c] + prediction[c]);
                        }
                    }
                }
            }

            int offset = p * numComponents;
            if (used == 0)
            {
                Transform.ComputeOriginalValue(data, offset - numComponents, data, offset, numComponents);
            }
            else
            {
                for (int c = 0; c < numComponents; c++)
                {
                    average[c] /= used;
                }

                Transform.ComputeOriginalValue(average, 0, data, offset, numComponents);
            }
        }
    }
}
