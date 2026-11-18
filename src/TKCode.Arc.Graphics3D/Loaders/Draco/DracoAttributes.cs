using System;
using System.Buffers.Binary;

namespace TKCode.Arc.Graphics3D;

/// <summary>One attribute while it is being decoded.</summary>
internal sealed class DracoAttributeData
{
    public const int GenericDecoder = 0;
    public const int IntegerDecoder = 1;
    public const int QuantizationDecoder = 2;
    public const int NormalDecoder = 3;

    public int UniqueId;
    public DracoAttributeType Type;
    public DracoDataType DataType;
    public int NumComponents;
    public int ComponentSize;
    public bool Normalized;

    /// <summary>How the values are stored: raw, as integers, as quantized floats or as octahedral normals.</summary>
    public int DecoderKind;

    /// <summary>Number of distinct values; points refer to them through <see cref="PointToValue"/>.</summary>
    public int NumEntries;

    /// <summary>The values in the attribute's own data type, little endian.</summary>
    public byte[]? Values;

    /// <summary>The values as stored integers (quantized), <see cref="PortableComponents"/> per entry.</summary>
    public int[]? Portable;

    public int PortableComponents;

    /// <summary>Value index of each point; null when point i uses value i.</summary>
    public int[]? PointToValue;

    public float[]? QuantizationMin;
    public float QuantizationRange;
    public int QuantizationBits;
    public int OctahedronBits = -1;

    public int ByteStride => NumComponents * ComponentSize;
}

/// <summary>
/// Decodes a group of attributes that share one ordering of their values on the mesh
/// (the format's "sequential attribute decoders controller").
/// </summary>
internal sealed class DracoAttributesDecoder
{
    private const int PredictionNone = -2;

    private readonly DracoMeshDecoder _mesh;
    private readonly DracoBuffer _buffer;
    private DracoAttributeData[] _attributes = Array.Empty<DracoAttributeData>();
    private int[]? _entryToPoint;
    private int _numEntries;

    public DracoAttributesDecoder(DracoMeshDecoder mesh)
    {
        _mesh = mesh;
        _buffer = mesh.Buffer;
    }

    /// <summary>Connectivity the values are ordered along; null when they are stored in point order.</summary>
    public DracoCornerTable? Table { get; set; }

    public DracoEncodingData? Encoding { get; set; }

    public bool MaxPredictionDegree { get; set; }

    /// <summary>Reads what the attributes are (type, components, id) and how each one is stored.</summary>
    public void DecodeDescriptors()
    {
        DracoBuffer buffer = _buffer;
        uint count = buffer.ReadCount(DracoVersion.V2_0);
        if (count == 0 || count > 5L * buffer.Remaining)
        {
            throw DracoError.Invalid("the number of attributes is wrong.");
        }

        _attributes = new DracoAttributeData[count];
        for (int i = 0; i < _attributes.Length; i++)
        {
            byte type = buffer.ReadByte();
            byte dataType = buffer.ReadByte();
            byte numComponents = buffer.ReadByte();
            byte normalized = buffer.ReadByte();
            if (type > (int)DracoAttributeType.Generic || dataType == 0 || dataType > 11 || numComponents == 0)
            {
                throw DracoError.Invalid("an attribute description is wrong.");
            }

            DracoAttributeData attribute = new()
            {
                Type = (DracoAttributeType)type,
                DataType = ToDataType(dataType, out int componentSize),
                ComponentSize = componentSize,
                NumComponents = numComponents,
                Normalized = normalized > 0,
                UniqueId = unchecked((int)(buffer.Version < DracoVersion.V1_3 ? buffer.ReadUInt16() : buffer.ReadVarUInt32())),
            };
            _attributes[i] = attribute;
            _mesh.Attributes.Add(attribute);
        }

        foreach (DracoAttributeData attribute in _attributes)
        {
            int kind = buffer.ReadByte();
            if (kind > DracoAttributeData.NormalDecoder)
            {
                throw DracoError.Invalid("unknown attribute decoder.");
            }

            bool isFloat = attribute.DataType == DracoDataType.Float32;
            if ((kind == DracoAttributeData.QuantizationDecoder && !isFloat)
                || (kind == DracoAttributeData.NormalDecoder && (!isFloat || attribute.NumComponents != 3)))
            {
                throw DracoError.Invalid("an attribute does not fit its decoder.");
            }

            attribute.DecoderKind = kind;
        }
    }

    public void DecodeAttributes()
    {
        int[]? pointToValue = null;
        if (Table != null && Encoding != null)
        {
            DracoLimits limits = _buffer.Limits;
            _entryToPoint = DracoTraversal.GenerateSequence(Table, _mesh.Faces, Encoding, MaxPredictionDegree, limits, out _numEntries);
            pointToValue = DracoTraversal.MapPointsToValues(Table, _mesh.Faces, Encoding, _mesh.NumPoints, limits);
        }
        else
        {
            _numEntries = _mesh.NumPoints;
        }

        foreach (DracoAttributeData attribute in _attributes)
        {
            attribute.NumEntries = _numEntries;
            attribute.PointToValue = pointToValue;
        }

        // The stream holds three passes over the attributes: stored values, transform parameters, final values.
        bool legacy = _buffer.Version < DracoVersion.V2_0;
        foreach (DracoAttributeData attribute in _attributes)
        {
            DecodeStoredValues(attribute, legacy);
        }

        if (legacy)
        {
            return;
        }

        foreach (DracoAttributeData attribute in _attributes)
        {
            DecodeTransformParameters(attribute);
        }

        foreach (DracoAttributeData attribute in _attributes)
        {
            if (attribute.DecoderKind != DracoAttributeData.GenericDecoder)
            {
                StoreValues(attribute);
            }
        }
    }

    private static DracoDataType ToDataType(byte code, out int size)
    {
        switch (code)
        {
            case 1:
                size = 1;
                return DracoDataType.Int8;
            case 2:
                size = 1;
                return DracoDataType.UInt8;
            case 3:
                size = 2;
                return DracoDataType.Int16;
            case 4:
                size = 2;
                return DracoDataType.UInt16;
            case 5:
                size = 4;
                return DracoDataType.Int32;
            case 6:
                size = 4;
                return DracoDataType.UInt32;
            case 9:
                size = 4;
                return DracoDataType.Float32;
            default:
                throw DracoError.Unsupported("64-bit and boolean attributes are not supported.");
        }
    }

    private void DecodeStoredValues(DracoAttributeData attribute, bool legacy)
    {
        DracoBuffer buffer = _buffer;
        if (attribute.DecoderKind == DracoAttributeData.GenericDecoder)
        {
            DracoTrace.Add("attribute.generic");
            long bytes = (long)_numEntries * attribute.ByteStride;
            if (bytes > buffer.Remaining)
            {
                throw DracoError.Invalid("attribute values are longer than the file.");
            }

            byte[] raw = buffer.Limits.Bytes(bytes);
            buffer.Take((int)bytes).CopyTo(raw);
            attribute.Values = raw;
            return;
        }

        int numComponents = attribute.DecoderKind == DracoAttributeData.NormalDecoder ? 2 : attribute.NumComponents;

        // The stored integers, the final values and their copy per point all have to fit.
        long valueCount = (long)_numEntries * numComponents;
        buffer.Limits.CheckDeclared(Math.Max(valueCount, (long)_numEntries * attribute.NumComponents), sizeof(int));
        int numValues = (int)valueCount;

        int method = buffer.ReadSByte();
        if (method < PredictionNone || method > 6)
        {
            throw DracoError.Invalid("unknown prediction scheme.");
        }

        DracoPredictionScheme? scheme = null;
        if (method != PredictionNone)
        {
            int transformType = buffer.ReadSByte();
            if (transformType < -1 || transformType > 3)
            {
                throw DracoError.Invalid("unknown prediction transform.");
            }

            scheme = CreateScheme(attribute, method, transformType);
        }
        else
        {
            DracoTrace.Add("prediction.none");
        }

        if (scheme != null && scheme.NeedsPositions)
        {
            scheme.Positions = FindPositions(legacy);
        }

        if (legacy)
        {
            // Before 2.0 the transform parameters come before the values.
            DracoTrace.Add("attribute.legacyOrder");
            DecodeTransformParameters(attribute);
        }

        int[] values;
        if (buffer.ReadByte() > 0)
        {
            values = DracoSymbols.Decode(buffer, numValues, numComponents);
        }
        else
        {
            DracoTrace.Add("attribute.uncompressedIntegers");
            values = ReadUncompressedIntegers(buffer, numValues);
        }

        if (numValues > 0 && (scheme == null || !scheme.AreCorrectionsPositive))
        {
            DracoSymbols.ToSigned(values);
        }

        if (scheme != null)
        {
            scheme.DecodePredictionData(buffer, numValues);
            if (numValues > 0)
            {
                scheme.ComputeOriginalValues(values, numComponents, _entryToPoint);
            }
        }

        attribute.Portable = values;
        attribute.PortableComponents = numComponents;
        if (legacy)
        {
            StoreValues(attribute);
        }
    }

    private static int[] ReadUncompressedIntegers(DracoBuffer buffer, int numValues)
    {
        int numBytes = buffer.ReadByte();
        if (numValues == 0)
        {
            return Array.Empty<int>();
        }

        // An encoder writes one to four bytes per value. Zero would create values out of nothing.
        if (numBytes < 1 || numBytes > 4 || (long)numBytes * numValues > buffer.Remaining)
        {
            throw DracoError.Invalid("uncompressed attribute values are stored incorrectly.");
        }

        int[] values = buffer.Limits.Ints(numValues);
        ReadOnlySpan<byte> source = buffer.Take(numBytes * numValues);
        for (int i = 0; i < numValues; i++)
        {
            int value = 0;
            int offset = i * numBytes;
            for (int b = 0; b < numBytes; b++)
            {
                value |= source[offset + b] << (8 * b);
            }

            values[i] = value;
        }

        return values;
    }

    private DracoPredictionScheme? CreateScheme(DracoAttributeData attribute, int method, int transformType)
    {
        DracoPredictionTransform transform;
        if (attribute.DecoderKind == DracoAttributeData.NormalDecoder)
        {
            if (transformType == 2)
            {
                DracoTrace.Add("transform.octahedron");
                transform = new DracoOctahedronTransform();
            }
            else if (transformType == 3)
            {
                DracoTrace.Add("transform.octahedronCanonicalized");
                transform = new DracoCanonicalizedOctahedronTransform();
            }
            else
            {
                // As the reference decoder: an unusable transform means the values are stored unpredicted.
                DracoTrace.Add("prediction.none");
                return null;
            }
        }
        else
        {
            if (transformType != 1)
            {
                DracoTrace.Add("prediction.none");
                return null;
            }

            DracoTrace.Add("transform.wrap");
            transform = new DracoWrapTransform();
        }

        DracoCornerTable? table = Table;
        DracoEncodingData? encoding = Encoding;
        if (table != null && encoding != null)
        {
            if (transform is DracoOctahedronTransform octahedron)
            {
                if (method == 6)
                {
                    DracoTrace.Add("prediction.geometricNormal");
                    if (_buffer.Version < DracoVersion.V2_2)
                    {
                        DracoTrace.Add("prediction.geometricNormal.legacy");
                    }

                    return new DracoGeometricNormalPrediction(octahedron, table, encoding);
                }
            }
            else
            {
                switch (method)
                {
                    case 1:
                        DracoTrace.Add("prediction.parallelogram");
                        return new DracoParallelogramPrediction(transform, table, encoding);
                    case 2:
                        DracoTrace.Add("prediction.multiParallelogram");
                        return new DracoMultiParallelogramPrediction(transform, table, encoding);
                    case 3:
                        DracoTrace.Add("prediction.texCoordsDeprecated");
                        return new DracoTexCoordsPrediction(transform, table, encoding, _buffer.Version);
                    case 4:
                        DracoTrace.Add("prediction.constrainedMultiParallelogram");
                        if (_buffer.Version < DracoVersion.V2_2)
                        {
                            DracoTrace.Add("prediction.constrainedMultiParallelogram.legacy");
                        }

                        return new DracoConstrainedMultiParallelogramPrediction(transform, table, encoding);
                    case 5:
                        DracoTrace.Add("prediction.texCoordsPortable");
                        return new DracoTexCoordsPortablePrediction(transform, table, encoding);
                    case 6:
                        throw DracoError.Invalid("normal prediction is used for an attribute that is not a normal.");
                    default:
                        break;
                }
            }
        }

        // Every other combination, and every mesh without edgebreaker connectivity, predicts from the previous value.
        DracoTrace.Add(transform is DracoOctahedronTransform ? "prediction.delta.octahedron" : "prediction.delta");
        return new DracoDeltaPrediction(transform);
    }

    private DracoPositionSource FindPositions(bool legacy)
    {
        DracoAttributeData? position = null;
        foreach (DracoAttributeData candidate in _mesh.Attributes)
        {
            if (candidate.Type == DracoAttributeType.Position)
            {
                position = candidate;
                break;
            }
        }

        if (position == null)
        {
            throw DracoError.Invalid("a prediction needs positions, and the mesh has none.");
        }

        if (legacy)
        {
            if (position.NumComponents != 3 || position.Values == null)
            {
                throw DracoError.Invalid("a prediction needs decoded three-component positions.");
            }

            DracoTrace.Add("prediction.positionsFromFinalValues");
            return new DracoPositionSource(position, portable: false);
        }

        if (position.Portable == null || position.PortableComponents != 3)
        {
            throw DracoError.Invalid("a prediction needs decoded three-component positions.");
        }

        return new DracoPositionSource(position, portable: true);
    }

    private void DecodeTransformParameters(DracoAttributeData attribute)
    {
        DracoBuffer buffer = _buffer;
        if (attribute.DecoderKind == DracoAttributeData.QuantizationDecoder)
        {
            float[] min = new float[attribute.NumComponents];
            for (int i = 0; i < min.Length; i++)
            {
                min[i] = buffer.ReadSingle();
            }

            attribute.QuantizationMin = min;
            attribute.QuantizationRange = buffer.ReadSingle();
            attribute.QuantizationBits = buffer.ReadByte();
            if (attribute.QuantizationBits < 1 || attribute.QuantizationBits > 30)
            {
                throw DracoError.Invalid("the number of quantization bits is wrong.");
            }
        }
        else if (attribute.DecoderKind == DracoAttributeData.NormalDecoder)
        {
            attribute.OctahedronBits = buffer.ReadByte();
        }
    }

    /// <summary>Converts the stored integers to the attribute's own data type.</summary>
    private void StoreValues(DracoAttributeData attribute)
    {
        int[] portable = attribute.Portable!;
        int numEntries = attribute.NumEntries;
        byte[] values = _buffer.Limits.Bytes((long)numEntries * attribute.ByteStride);
        Span<byte> destination = values;
        switch (attribute.DecoderKind)
        {
            case DracoAttributeData.QuantizationDecoder:
            {
                DracoTrace.Add("attribute.quantization");
                float[] min = attribute.QuantizationMin!;
                float delta = attribute.QuantizationRange / ((1 << attribute.QuantizationBits) - 1);
                int numComponents = attribute.NumComponents;
                int index = 0;
                for (int i = 0; i < numEntries; i++)
                {
                    for (int c = 0; c < numComponents; c++)
                    {
                        float value = (portable[index] * delta) + min[c];
                        BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(index * 4), value);
                        index++;
                    }
                }

                break;
            }

            case DracoAttributeData.NormalDecoder:
            {
                DracoTrace.Add("attribute.normal");
                DracoOctahedron octahedron = new();
                if (!octahedron.SetQuantizationBits(attribute.OctahedronBits))
                {
                    throw DracoError.Invalid("the quantization of normals is wrong.");
                }

                for (int i = 0; i < numEntries; i++)
                {
                    octahedron.CoordsToUnitVector(portable[2 * i], portable[(2 * i) + 1], out float x, out float y, out float z);
                    Span<byte> entry = destination.Slice(i * 12, 12);
                    BinaryPrimitives.WriteSingleLittleEndian(entry, x);
                    BinaryPrimitives.WriteSingleLittleEndian(entry.Slice(4), y);
                    BinaryPrimitives.WriteSingleLittleEndian(entry.Slice(8), z);
                }

                break;
            }

            default:
                DracoTrace.Add("attribute.integer");
                StoreIntegers(attribute.DataType, portable, destination);
                break;
        }

        attribute.Values = values;
    }

    private static void StoreIntegers(DracoDataType dataType, int[] portable, Span<byte> destination)
    {
        // The stored integers are truncated to the attribute's type, as a cast does.
        switch (dataType)
        {
            case DracoDataType.Int8:
            case DracoDataType.UInt8:
                for (int i = 0; i < portable.Length; i++)
                {
                    destination[i] = unchecked((byte)portable[i]);
                }

                break;
            case DracoDataType.Int16:
            case DracoDataType.UInt16:
                for (int i = 0; i < portable.Length; i++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(i * 2), unchecked((ushort)portable[i]));
                }

                break;
            case DracoDataType.Int32:
            case DracoDataType.UInt32:
                for (int i = 0; i < portable.Length; i++)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(i * 4), portable[i]);
                }

                break;
            default:
                throw DracoError.Invalid("a floating point attribute is stored as integers.");
        }
    }
}
