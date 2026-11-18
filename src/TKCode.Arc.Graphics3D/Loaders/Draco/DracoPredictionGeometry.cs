using System;

namespace TKCode.Arc.Graphics3D;

/// <summary>
/// Texture coordinates predicted from the positions: the triangle in UV space is assumed to have the
/// shape it has in 3D, and one stored bit says on which side of the known edge the new corner lies.
/// Integer arithmetic only, so every platform predicts the same.
/// </summary>
internal sealed class DracoTexCoordsPortablePrediction : DracoMeshPrediction
{
    private readonly int[] _predicted = new int[2];
    private bool[] _orientations = Array.Empty<bool>();
    private int _orientationsLeft;

    public DracoTexCoordsPortablePrediction(DracoPredictionTransform transform, DracoCornerTable table, DracoEncodingData encoding)
        : base(transform, table, encoding)
    {
    }

    public override bool NeedsPositions => true;

    public override void DecodePredictionData(DracoBuffer buffer, int numValues)
    {
        int count = buffer.ReadInt32();
        if (count < 0 || count > numValues)
        {
            throw DracoError.Invalid("the number of texture coordinate orientations is wrong.");
        }

        // A bit says whether the orientation is the same as the previous one.
        bool[] orientations = new bool[count];
        bool last = true;
        DracoRansBitDecoder decoder = DracoRansBitDecoder.Start(buffer);
        for (int i = 0; i < count; i++)
        {
            if (!decoder.DecodeBit())
            {
                last = !last;
            }

            orientations[i] = last;
        }

        _orientations = orientations;
        _orientationsLeft = count;
        base.DecodePredictionData(buffer, numValues);
    }

    public override void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint)
    {
        if (numComponents != 2)
        {
            throw DracoError.Invalid("texture coordinate prediction needs two components.");
        }

        SetEntryToPoint(entryToPoint);
        int count = CheckValueCount(data, numComponents);
        int[] dataToCorner = Encoding.DataToCorner;
        for (int p = 0; p < count; p++)
        {
            ComputePredictedValue(dataToCorner[p], data, p);
            Transform.ComputeOriginalValue(_predicted, 0, data, p * 2, 2);
        }
    }

    private void ComputePredictedValue(int corner, int[] data, int entry)
    {
        int next = DataId(DracoCornerTable.Next(corner));
        int previous = DataId(DracoCornerTable.Previous(corner));
        if (previous < entry && next < entry)
        {
            long nextU = data[next * 2];
            long nextV = data[(next * 2) + 1];
            long previousU = data[previous * 2];
            long previousV = data[(previous * 2) + 1];
            if (previousU == nextU && previousV == nextV)
            {
                _predicted[0] = (int)previousU;
                _predicted[1] = (int)previousV;
                return;
            }

            if (PredictFromPositions(entry, next, previous, nextU, nextV, previousU, previousV))
            {
                return;
            }
        }

        // Not enough decoded neighbours: fall back to one of them, or to the previous value.
        int offset;
        if (next < entry)
        {
            offset = next * 2;
        }
        else if (entry > 0)
        {
            offset = (entry - 1) * 2;
        }
        else
        {
            _predicted[0] = 0;
            _predicted[1] = 0;
            return;
        }

        _predicted[0] = data[offset];
        _predicted[1] = data[offset + 1];
    }

    private bool PredictFromPositions(int entry, int next, int previous, long nextU, long nextV, long previousU, long previousV)
    {
        DracoPositionSource positions = Positions!;
        positions.GetInt64(PointOfEntry(entry), out long tipX, out long tipY, out long tipZ);
        positions.GetInt64(PointOfEntry(next), out long nextX, out long nextY, out long nextZ);
        positions.GetInt64(PointOfEntry(previous), out long previousX, out long previousY, out long previousZ);
        unchecked
        {
            // P and N are the known corners, C is the new one, X is the foot of C on the line PN.
            long pnX = previousX - nextX;
            long pnY = previousY - nextY;
            long pnZ = previousZ - nextZ;
            ulong pnNormSquared = (ulong)((pnX * pnX) + (pnY * pnY) + (pnZ * pnZ));
            if (pnNormSquared == 0)
            {
                return false;
            }

            long cnX = tipX - nextX;
            long cnY = tipY - nextY;
            long cnZ = tipZ - nextZ;
            long cnDotPn = (pnX * cnX) + (pnY * cnY) + (pnZ * cnZ);
            long pnU = previousU - nextU;
            long pnV = previousV - nextV;

            // The products below must not overflow; an encoder never writes such data.
            long nextUvMax = Math.Max(Abs(nextU), Abs(nextV));
            if ((ulong)nextUvMax > (ulong)long.MaxValue / pnNormSquared)
            {
                throw Overflow();
            }

            long pnUvMax = Math.Max(Abs(pnU), Abs(pnV));
            long absDot = Abs(cnDotPn);
            if (absDot < 0 || pnUvMax <= 0 || absDot > long.MaxValue / pnUvMax)
            {
                throw Overflow();
            }

            long xU = (long)(((ulong)nextU * pnNormSquared) + ((ulong)pnU * (ulong)cnDotPn));
            long xV = (long)(((ulong)nextV * pnNormSquared) + ((ulong)pnV * (ulong)cnDotPn));
            long pnMax = Math.Max(Math.Max(Abs(pnX), Abs(pnY)), Abs(pnZ));
            if (pnMax <= 0 || absDot > long.MaxValue / pnMax)
            {
                throw Overflow();
            }

            long divisor = (long)pnNormSquared;
            long xX = nextX + Divide(cnDotPn * pnX, divisor);
            long xY = nextY + Divide(cnDotPn * pnY, divisor);
            long xZ = nextZ + Divide(cnDotPn * pnZ, divisor);
            long cxX = tipX - xX;
            long cxY = tipY - xY;
            long cxZ = tipZ - xZ;
            ulong cxNormSquared = (ulong)((cxX * cxX) + (cxY * cxY) + (cxZ * cxZ));

            // PN rotated by 90 degrees in UV space and scaled to the length of CX.
            long scale = (long)IntSqrt(cxNormSquared * pnNormSquared);
            long cxU = pnV * scale;
            long cxV = -pnU * scale;
            if (_orientationsLeft <= 0)
            {
                throw DracoError.Invalid("too few texture coordinate orientations.");
            }

            long u;
            long v;
            if (_orientations[--_orientationsLeft])
            {
                u = Divide((long)((ulong)xU + (ulong)cxU), divisor);
                v = Divide((long)((ulong)xV + (ulong)cxV), divisor);
            }
            else
            {
                u = Divide((long)((ulong)xU - (ulong)cxU), divisor);
                v = Divide((long)((ulong)xV - (ulong)cxV), divisor);
            }

            _predicted[0] = (int)u;
            _predicted[1] = (int)v;
            return true;
        }
    }

    private static long Abs(long value)
    {
        return value < 0 ? unchecked(-value) : value;
    }

    // long.MinValue / -1 throws; the wrapped result is what the format's arithmetic gives.
    private static long Divide(long value, long divisor)
    {
        return divisor == -1 ? unchecked(-value) : value / divisor;
    }

    private static ulong IntSqrt(ulong number)
    {
        if (number == 0)
        {
            return 0;
        }

        ulong remaining = number;
        ulong root = 1;
        while (remaining >= 2)
        {
            root *= 2;
            remaining /= 4;
        }

        // Newton steps from above; a damaged input cannot keep this going for long.
        for (int i = 0; i < 64; i++)
        {
            root = unchecked((root + (number / root)) / 2);
            if (root == 0 || unchecked(root * root) <= number)
            {
                break;
            }
        }

        return root;
    }

    private static Exception Overflow()
    {
        return DracoError.Invalid("texture coordinate prediction overflows.");
    }
}

/// <summary>
/// The texture coordinate prediction of streams before 2.0, in floating point. Kept as close to the
/// original arithmetic as possible, because the corrections only fit the exact same predictions.
/// </summary>
internal sealed class DracoTexCoordsPrediction : DracoMeshPrediction
{
    private readonly int[] _predicted = new int[2];
    private readonly int _version;
    private bool[] _orientations = Array.Empty<bool>();
    private int _orientationsLeft;

    public DracoTexCoordsPrediction(DracoPredictionTransform transform, DracoCornerTable table, DracoEncodingData encoding, int version)
        : base(transform, table, encoding)
    {
        _version = version;
    }

    public override bool NeedsPositions => true;

    public override void DecodePredictionData(DracoBuffer buffer, int numValues)
    {
        uint count = buffer.ReadCount(DracoVersion.V2_2);
        if (count == 0 || count > (uint)Table.NumCorners)
        {
            throw DracoError.Invalid("the number of texture coordinate orientations is wrong.");
        }

        bool[] orientations = new bool[count];
        bool last = true;
        DracoRansBitDecoder decoder = DracoRansBitDecoder.Start(buffer);
        for (int i = 0; i < orientations.Length; i++)
        {
            if (!decoder.DecodeBit())
            {
                last = !last;
            }

            orientations[i] = last;
        }

        _orientations = orientations;
        _orientationsLeft = orientations.Length;
        base.DecodePredictionData(buffer, numValues);
    }

    public override void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint)
    {
        if (numComponents != 2)
        {
            throw DracoError.Invalid("texture coordinate prediction needs two components.");
        }

        SetEntryToPoint(entryToPoint);
        int count = CheckValueCount(data, numComponents);
        int[] dataToCorner = Encoding.DataToCorner;
        for (int p = 0; p < count; p++)
        {
            ComputePredictedValue(dataToCorner[p], data, p);
            Transform.ComputeOriginalValue(_predicted, 0, data, p * 2, 2);
        }
    }

    private void ComputePredictedValue(int corner, int[] data, int entry)
    {
        int next = DataId(DracoCornerTable.Next(corner));
        int previous = DataId(DracoCornerTable.Previous(corner));
        if (previous < entry && next < entry)
        {
            float nextU = data[next * 2];
            float nextV = data[(next * 2) + 1];
            float previousU = data[previous * 2];
            float previousV = data[(previous * 2) + 1];
            if (previousU == nextU && previousV == nextV)
            {
                _predicted[0] = ToInt32(previousU);
                _predicted[1] = ToInt32(previousV);
                return;
            }

            DracoPositionSource positions = Positions!;
            positions.GetSingle(PointOfEntry(entry), out float tipX, out float tipY, out float tipZ);
            positions.GetSingle(PointOfEntry(next), out float nextX, out float nextY, out float nextZ);
            positions.GetSingle(PointOfEntry(previous), out float previousX, out float previousY, out float previousZ);
            float pnX = previousX - nextX;
            float pnY = previousY - nextY;
            float pnZ = previousZ - nextZ;
            float cnX = tipX - nextX;
            float cnY = tipY - nextY;
            float cnZ = tipZ - nextZ;
            float pnNormSquared = Dot(pnX, pnY, pnZ, pnX, pnY, pnZ);

            // s: position of the new corner along PN; t: its distance from PN, both in units of |PN|.
            float s = 0;
            float t = 0;
            if (_version < DracoVersion.V1_2 || pnNormSquared > 0)
            {
                s = Dot(pnX, pnY, pnZ, cnX, cnY, cnZ) / pnNormSquared;
                float dx = cnX - (pnX * s);
                float dy = cnY - (pnY * s);
                float dz = cnZ - (pnZ * s);
                t = MathF.Sqrt(Dot(dx, dy, dz, dx, dy, dz) / pnNormSquared);
            }

            float pnU = previousU - nextU;
            float pnV = previousV - nextV;
            float pnus = (pnU * s) + nextU;
            float pnut = pnU * t;
            float pnvs = (pnV * s) + nextV;
            float pnvt = pnV * t;
            if (_orientationsLeft <= 0)
            {
                throw DracoError.Invalid("too few texture coordinate orientations.");
            }

            float u;
            float v;
            if (_orientations[--_orientationsLeft])
            {
                u = pnus - pnvt;
                v = pnvs + pnut;
            }
            else
            {
                u = pnus + pnvt;
                v = pnvs - pnut;
            }

            _predicted[0] = ToInt32(Math.Floor(u + 0.5));
            _predicted[1] = ToInt32(Math.Floor(v + 0.5));
            return;
        }

        int offset;
        if (next < entry)
        {
            offset = next * 2;
        }
        else if (entry > 0)
        {
            offset = (entry - 1) * 2;
        }
        else
        {
            _predicted[0] = 0;
            _predicted[1] = 0;
            return;
        }

        _predicted[0] = data[offset];
        _predicted[1] = data[offset + 1];
    }

    // Accumulates in the order the original code does; float rounding makes the order matter.
    private static float Dot(float ax, float ay, float az, float bx, float by, float bz)
    {
        float sum = 0;
        sum += ax * bx;
        sum += ay * by;
        sum += az * bz;
        return sum;
    }

    private static int ToInt32(double value)
    {
        if (double.IsNaN(value) || value > int.MaxValue || value < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)value;
    }
}

/// <summary>
/// Normals predicted from the geometry: the sum of the (area weighted) face normals around the vertex,
/// computed from the positions, with one stored bit that says whether the result has to be flipped.
/// </summary>
internal sealed class DracoGeometricNormalPrediction : DracoMeshPrediction
{
    private readonly DracoOctahedron _octahedron = new();
    private readonly int[] _predicted = new int[2];
    private DracoRansBitDecoder? _flipDecoder;
    private bool _oneTriangle;

    public DracoGeometricNormalPrediction(DracoOctahedronTransform transform, DracoCornerTable table, DracoEncodingData encoding)
        : base(transform, table, encoding)
    {
    }

    public override bool NeedsPositions => true;

    public override void DecodePredictionData(DracoBuffer buffer, int numValues)
    {
        Transform.DecodeTransformData(buffer);
        if (buffer.Version < DracoVersion.V2_2)
        {
            byte mode = buffer.ReadByte();
            if (mode > 1)
            {
                throw DracoError.Invalid("unknown normal prediction mode.");
            }

            _oneTriangle = mode == 0;
        }

        _flipDecoder = DracoRansBitDecoder.Start(buffer);
    }

    public override void ComputeOriginalValues(int[] data, int numComponents, int[]? entryToPoint)
    {
        if (numComponents != 2 || !_octahedron.SetQuantizationBits(((DracoOctahedronTransform)Transform).QuantizationBits))
        {
            throw DracoError.Invalid("the quantization of normals is wrong.");
        }

        SetEntryToPoint(entryToPoint);
        int count = CheckValueCount(data, numComponents);
        int[] dataToCorner = Encoding.DataToCorner;
        DracoRansBitDecoder flipDecoder = _flipDecoder!;
        for (int p = 0; p < count; p++)
        {
            ComputePredictedNormal(dataToCorner[p], out int x, out int y, out int z);
            _octahedron.CanonicalizeIntegerVector(ref x, ref y, ref z);
            if (flipDecoder.DecodeBit())
            {
                unchecked
                {
                    x = -x;
                    y = -y;
                    z = -z;
                }
            }

            _octahedron.IntegerVectorToCoords(x, y, z, out _predicted[0], out _predicted[1]);
            Transform.ComputeOriginalValue(_predicted, 0, data, p * 2, 2);
        }
    }

    private void GetPosition(int corner, out long x, out long y, out long z)
    {
        Positions!.GetInt64(PointOfEntry(DataId(corner)), out x, out y, out z);
    }

    private void ComputePredictedNormal(int corner, out int x, out int y, out int z)
    {
        unchecked
        {
            DracoCornerTable table = Table;
            GetPosition(corner, out long centerX, out long centerY, out long centerZ);
            long normalX = 0;
            long normalY = 0;
            long normalZ = 0;

            // All corners of the vertex: left around its fan, and from the start to the right on a boundary.
            int current = corner;
            bool leftTraversal = true;
            int guard = table.NumCorners;
            while (current >= 0 && guard-- >= 0)
            {
                int source = _oneTriangle ? corner : current;
                GetPosition(DracoCornerTable.Next(source), out long nextX, out long nextY, out long nextZ);
                GetPosition(DracoCornerTable.Previous(source), out long previousX, out long previousY, out long previousZ);
                long ax = nextX - centerX;
                long ay = nextY - centerY;
                long az = nextZ - centerZ;
                long bx = previousX - centerX;
                long by = previousY - centerY;
                long bz = previousZ - centerZ;
                normalX += (ay * bz) - (az * by);
                normalY += (az * bx) - (ax * bz);
                normalZ += (ax * by) - (ay * bx);
                if (leftTraversal)
                {
                    current = table.SwingLeft(current);
                    if (current < 0)
                    {
                        current = table.SwingRight(corner);
                        leftTraversal = false;
                    }
                    else if (current == corner)
                    {
                        current = -1;
                    }
                }
                else
                {
                    current = table.SwingRight(current);
                }
            }

            // Keep the vector small enough for the 32-bit arithmetic that follows.
            const long UpperBound = 1 << 29;
            long absSum = AbsSum(normalX, normalY, normalZ);
            if (_oneTriangle)
            {
                absSum = (int)absSum;
            }

            if (absSum > UpperBound)
            {
                long quotient = absSum / UpperBound;
                normalX /= quotient;
                normalY /= quotient;
                normalZ /= quotient;
            }

            x = (int)normalX;
            y = (int)normalY;
            z = (int)normalZ;
        }
    }

    private static long AbsSum(long x, long y, long z)
    {
        ulong sum = AbsUnsigned(x) + AbsUnsigned(y);
        sum += AbsUnsigned(z);
        return sum > long.MaxValue ? long.MaxValue : (long)sum;
    }

    private static ulong AbsUnsigned(long value)
    {
        return value < 0 ? unchecked(0UL - (ulong)value) : (ulong)value;
    }
}
