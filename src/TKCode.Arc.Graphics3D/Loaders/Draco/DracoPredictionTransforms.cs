using System;
using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Octahedral encoding of unit vectors: a normal is a point (s, t) on an unfolded octahedron.</summary>
internal sealed class DracoOctahedron
{
    private int _maxQuantizedValue;
    private int _maxValue;
    private int _centerValue;
    private float _dequantizationScale;

    public int QuantizationBits { get; private set; } = -1;

    public int CenterValue => _centerValue;

    public bool SetQuantizationBits(int bits)
    {
        if (bits < 2 || bits > 30)
        {
            return false;
        }

        QuantizationBits = bits;
        _maxQuantizedValue = (1 << bits) - 1;
        _maxValue = _maxQuantizedValue - 1;
        _dequantizationScale = 2f / _maxValue;
        _centerValue = _maxValue / 2;
        return true;
    }

    /// <summary>Maps an integer vector whose absolute values add up to the center value to (s, t).</summary>
    public void IntegerVectorToCoords(int x, int y, int z, out int s, out int t)
    {
        unchecked
        {
            if (x >= 0)
            {
                s = y + _centerValue;
                t = z + _centerValue;
            }
            else
            {
                s = y < 0 ? Abs(z) : _maxValue - Abs(z);
                t = z < 0 ? Abs(y) : _maxValue - Abs(y);
            }

            // Several (s, t) on the border of the square mean the same direction; pick one of them.
            if ((s == 0 && t == 0) || (s == 0 && t == _maxValue) || (s == _maxValue && t == 0))
            {
                s = _maxValue;
                t = _maxValue;
            }
            else if (s == 0 && t > _centerValue)
            {
                t = _centerValue - (t - _centerValue);
            }
            else if (s == _maxValue && t < _centerValue)
            {
                t = _centerValue + (_centerValue - t);
            }
            else if (t == _maxValue && s < _centerValue)
            {
                s = _centerValue + (_centerValue - s);
            }
            else if (t == 0 && s > _centerValue)
            {
                s = _centerValue - (s - _centerValue);
            }
        }
    }

    /// <summary>Scales an integer vector so that its absolute values add up to the center value.</summary>
    public void CanonicalizeIntegerVector(ref int x, ref int y, ref int z)
    {
        long absSum = Math.Abs((long)x) + Math.Abs((long)y) + Math.Abs((long)z);
        unchecked
        {
            if (absSum == 0)
            {
                x = _centerValue;
                return;
            }

            x = (int)((long)x * _centerValue / absSum);
            y = (int)((long)y * _centerValue / absSum);
            int rest = _centerValue - Abs(x) - Abs(y);
            z = z >= 0 ? rest : -rest;
        }
    }

    public void CoordsToUnitVector(int s, int t, out float x, out float y, out float z)
    {
        y = (s * _dequantizationScale) - 1f;
        z = (t * _dequantizationScale) - 1f;
        x = 1f - MathF.Abs(y) - MathF.Abs(z);
        float offset = -x;
        if (offset < 0)
        {
            offset = 0;
        }

        y += y < 0 ? offset : -offset;
        z += z < 0 ? offset : -offset;
        float normSquared = (x * x) + (y * y) + (z * z);
        if (normSquared < 1e-6)
        {
            x = 0;
            y = 0;
            z = 0;
            return;
        }

        float scale = 1f / MathF.Sqrt(normSquared);
        x *= scale;
        y *= scale;
        z *= scale;
    }

    public bool IsInDiamond(int s, int t)
    {
        unchecked
        {
            return (uint)Abs(s) + (uint)Abs(t) <= (uint)_centerValue;
        }
    }

    /// <summary>Mirrors a point between the inner diamond and the four outer triangles of the square.</summary>
    public void InvertDiamond(ref int s, ref int t)
    {
        unchecked
        {
            int signS;
            int signT;
            if (s >= 0 && t >= 0)
            {
                signS = 1;
                signT = 1;
            }
            else if (s <= 0 && t <= 0)
            {
                signS = -1;
                signT = -1;
            }
            else
            {
                signS = s > 0 ? 1 : -1;
                signT = t > 0 ? 1 : -1;
            }

            uint cornerS = (uint)(signS * _centerValue);
            uint cornerT = (uint)(signT * _centerValue);
            uint us = (uint)s;
            uint ut = (uint)t;
            us = us + us - cornerS;
            ut = ut + ut - cornerT;
            if (signS * signT >= 0)
            {
                uint temp = us;
                us = 0u - ut;
                ut = 0u - temp;
            }
            else
            {
                uint temp = us;
                us = ut;
                ut = temp;
            }

            us += cornerS;
            ut += cornerT;
            s = (int)us / 2;
            t = (int)ut / 2;
        }
    }

    public int ModMax(int value)
    {
        unchecked
        {
            if (value > _centerValue)
            {
                return value - _maxQuantizedValue;
            }

            if (value < -_centerValue)
            {
                return value + _maxQuantizedValue;
            }

            return value;
        }
    }

    // Math.Abs throws for int.MinValue; damaged data must not get that far.
    private static int Abs(int value)
    {
        return value < 0 ? unchecked(-value) : value;
    }
}

/// <summary>Turns a predicted value and a stored correction into the original value.</summary>
internal abstract class DracoPredictionTransform
{
    /// <summary>When true the stored corrections are used as they are; otherwise they are sign-folded symbols.</summary>
    public abstract bool AreCorrectionsPositive { get; }

    public abstract void DecodeTransformData(DracoBuffer buffer);

    /// <summary>
    /// Replaces the corrections at <paramref name="offset"/> of <paramref name="data"/> with the original values.
    /// </summary>
    public abstract void ComputeOriginalValue(int[] prediction, int predictionOffset, int[] data, int offset, int numComponents);
}

/// <summary>Corrections wrap around the value range, which halves the range they need.</summary>
internal sealed class DracoWrapTransform : DracoPredictionTransform
{
    private int _min;
    private int _max;
    private int _range;

    public override bool AreCorrectionsPositive => false;

    public override void DecodeTransformData(DracoBuffer buffer)
    {
        _min = buffer.ReadInt32();
        _max = buffer.ReadInt32();
        long difference = (long)_max - _min;
        if (difference < 0 || difference >= int.MaxValue)
        {
            throw DracoError.Invalid("the value range of a prediction is wrong.");
        }

        _range = 1 + (int)difference;
    }

    public override void ComputeOriginalValue(int[] prediction, int predictionOffset, int[] data, int offset, int numComponents)
    {
        for (int i = 0; i < numComponents; i++)
        {
            int predicted = prediction[predictionOffset + i];
            if (predicted > _max)
            {
                predicted = _max;
            }
            else if (predicted < _min)
            {
                predicted = _min;
            }

            long value = (long)predicted + data[offset + i];
            if (value > _max)
            {
                value -= _range;
            }
            else if (value < _min)
            {
                value += _range;
            }

            data[offset + i] = unchecked((int)value);
        }
    }
}

/// <summary>Corrections of octahedral normal coordinates, taken in a frame where the prediction is in the inner diamond.</summary>
internal class DracoOctahedronTransform : DracoPredictionTransform
{
    protected readonly DracoOctahedron Octahedron = new();

    public override bool AreCorrectionsPositive => true;

    public int QuantizationBits => Octahedron.QuantizationBits;

    public override void DecodeTransformData(DracoBuffer buffer)
    {
        int maxQuantizedValue = buffer.ReadInt32();
        if (buffer.Version < DracoVersion.V2_2)
        {
            // The center value; it follows from the maximum.
            buffer.ReadInt32();
        }

        SetMaxQuantizedValue(maxQuantizedValue);
    }

    public override void ComputeOriginalValue(int[] prediction, int predictionOffset, int[] data, int offset, int numComponents)
    {
        unchecked
        {
            DracoOctahedron octahedron = Octahedron;
            int center = octahedron.CenterValue;
            int s = prediction[predictionOffset] - center;
            int t = prediction[predictionOffset + 1] - center;
            bool inDiamond = octahedron.IsInDiamond(s, t);
            if (!inDiamond)
            {
                octahedron.InvertDiamond(ref s, ref t);
            }

            s = octahedron.ModMax(s + data[offset]);
            t = octahedron.ModMax(t + data[offset + 1]);
            if (!inDiamond)
            {
                octahedron.InvertDiamond(ref s, ref t);
            }

            data[offset] = s + center;
            data[offset + 1] = t + center;
        }
    }

    protected void SetMaxQuantizedValue(int maxQuantizedValue)
    {
        if (maxQuantizedValue <= 0
            || maxQuantizedValue % 2 == 0
            || !Octahedron.SetQuantizationBits(BitOperations.Log2((uint)maxQuantizedValue) + 1))
        {
            throw DracoError.Invalid("the quantization of normals is wrong.");
        }
    }
}

/// <summary>As <see cref="DracoOctahedronTransform"/>, and the prediction is also rotated into the bottom-left quadrant.</summary>
internal sealed class DracoCanonicalizedOctahedronTransform : DracoOctahedronTransform
{
    public override void DecodeTransformData(DracoBuffer buffer)
    {
        int maxQuantizedValue = buffer.ReadInt32();
        buffer.ReadInt32();
        SetMaxQuantizedValue(maxQuantizedValue);
    }

    public override void ComputeOriginalValue(int[] prediction, int predictionOffset, int[] data, int offset, int numComponents)
    {
        unchecked
        {
            DracoOctahedron octahedron = Octahedron;
            int center = octahedron.CenterValue;
            int s = prediction[predictionOffset] - center;
            int t = prediction[predictionOffset + 1] - center;
            bool inDiamond = octahedron.IsInDiamond(s, t);
            if (!inDiamond)
            {
                octahedron.InvertDiamond(ref s, ref t);
            }

            bool inBottomLeft = (s == 0 && t == 0) || (s < 0 && t <= 0);
            int rotations = RotationCount(s, t);
            if (!inBottomLeft)
            {
                Rotate(ref s, ref t, rotations);
            }

            s = octahedron.ModMax(s + data[offset]);
            t = octahedron.ModMax(t + data[offset + 1]);
            if (!inBottomLeft)
            {
                Rotate(ref s, ref t, (4 - rotations) % 4);
            }

            if (!inDiamond)
            {
                octahedron.InvertDiamond(ref s, ref t);
            }

            data[offset] = s + center;
            data[offset + 1] = t + center;
        }
    }

    // Quarter turns that bring the point into the bottom-left quadrant.
    private static int RotationCount(int s, int t)
    {
        if (s == 0)
        {
            if (t == 0)
            {
                return 0;
            }

            return t > 0 ? 3 : 1;
        }

        if (s > 0)
        {
            return t >= 0 ? 2 : 1;
        }

        return t <= 0 ? 0 : 3;
    }

    private static void Rotate(ref int s, ref int t, int rotations)
    {
        unchecked
        {
            int oldS = s;
            switch (rotations)
            {
                case 1:
                    s = t;
                    t = -oldS;
                    break;
                case 2:
                    s = -s;
                    t = -t;
                    break;
                case 3:
                    s = -t;
                    t = oldS;
                    break;
                default:
                    break;
            }
        }
    }
}
