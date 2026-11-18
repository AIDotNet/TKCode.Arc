namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>Meshopt index data written by the meshoptimizer library itself (0.22), for the two cases the public sample
/// models do not contain: the index sequence codec (used for lines and points) and version 0 of the triangle codec.</summary>
public class MeshoptDecoderTests
{
    private static readonly uint[] s_sequence = [0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13,14,14,15,15,16,16,17,17,18,18,19,19,20,20,21,21,22,22,23,23,24,24,25,25,26,26,27,27,28,28,29,29,30,30,31,31,32,32,33,33,34,34,35,35,36,36,37,37,38,38,39,39,40,28677,99,49899,271,51325,243,2586,97,1600,253,51315,121,45331,126,13865,120,22178,146,65570,36,50504,263,15788,104,42749,184,42961,192,4257,230,53213,34,30530,212,28806,155,63907,144,55953,208,3000000,2999999,0,4000000000];

    private static readonly uint[] s_triangles = [0,1,7,1,8,7,1,2,8,2,9,8,2,3,9,3,10,9,3,4,10,4,11,10,4,5,11,5,12,11,5,6,12,6,13,12,7,8,14,8,15,14,8,9,15,9,16,15,9,10,16,10,17,16,10,11,17,11,18,17,11,12,18,12,19,18,12,13,19,13,20,19,14,15,21,15,22,21,15,16,22,16,23,22,16,17,23,17,24,23,17,18,24,18,25,24,18,19,25,19,26,25,19,20,26,20,27,26,21,22,28,22,29,28,22,23,29,23,30,29,23,24,30,24,31,30,24,25,31,25,32,31,25,26,32,26,33,32,26,27,33,27,34,33,28,29,35,29,36,35,29,30,36,30,37,36,30,31,37,31,38,37,31,32,38,32,39,38,32,33,39,33,40,39,33,34,40,34,41,40,35,36,42,36,43,42,36,37,43,37,44,43,37,38,44,38,45,44,38,39,45,39,46,45,39,40,46,40,47,46,40,41,47,41,48,47,100000,5,70000];

    [Theory]
    [InlineData("0AAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAElYAH7AGZlwWwBcksbovzC8YE5x7wBM2RDI4E/7oBFKfXBxblgwJogcwKtgPn1gOMB++8CPoExcoGwALRBiC/uQmYAfH5C44G68QFyAXvNeIB9cgIKsf4AYACveHOBQO+Bvv1qbgEAAAAAA==")]
    [InlineData("0QAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAElYAH7AGZlwWwBcksbovzC8YE5x7wBM2RDI4E/7oBFKfXBxblgwJogcwKtgPn1gOMB++8CPoExcoGwALRBiC/uQmYAfH5C44G68QFyAXvNeIB9cgIKsf4AYACveHOBQO+Bvv1qbgEAAAAAA==")]
    public void Index_Sequences_Decode_To_What_Was_Encoded(string base64)
    {
        var decoded = MeshoptDecoder.Decode(Convert.FromBase64String(base64), s_sequence.Length, 4, MeshoptMode.Indices);
        System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(decoded).ToArray().ShouldBe(s_sequence);
    }

    [Fact]
    public void Sixteen_Bit_Index_Sequences_Keep_The_Low_Half()
    {
        uint[] small = [.. s_sequence[..80]];
        var decoded = MeshoptDecoder.Decode(Convert.FromBase64String("0QAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAEAAQABAAElYAH7AGZlwWwBcksbovzC8YE5x7wBM2RDI4E/7oBFKfXBxblgwJogcwKtgPn1gOMB++8CPoExcoGwALRBiC/uQmYAfH5C44G68QFyAXvNeIB9cgIKsf4AYACveHOBQO+Bvv1qbgEAAAAAA=="), s_sequence.Length, 2, MeshoptMode.Indices);
        var shorts = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(decoded);
        for (var i = 0; i < small.Length; i++)
        {
            shorts[i].ShouldBe((ushort)small[i]);
        }
    }

    [Fact]
    public void Version_0_Triangle_Lists_Decode_To_What_Was_Encoded()
    {
        var decoded = MeshoptDecoder.Decode(Convert.FromBase64String("4P4fEA8QDxAPEA8QD/4fGw8aDxkPGA8XD/8fFw8XDxcPFw8XD/8fFw8XDxcPFw8XD/8fFw8XDxcPFw8XD/8fFw8XDxcPFw8XD/8PDgICAgICAr8CAgICAgICbwsOAgICAgICbwsOAgICAgICbwsOAgICAgICbwsOAgICAgIC/+CZDLWaDNbFCAB2h1ZneKmGZYlomAFpAAA="), s_triangles.Length, 4, MeshoptMode.Triangles);
        var indices = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(decoded);

        // The codec keeps each triangle and its winding but may start it at another corner.
        static (uint, uint, uint) Rotated(uint a, uint b, uint c) => a <= b && a <= c ? (a, b, c) : b <= a && b <= c ? (b, c, a) : (c, a, b);
        for (var i = 0; i < s_triangles.Length; i += 3)
        {
            Rotated(indices[i], indices[i + 1], indices[i + 2]).ShouldBe(Rotated(s_triangles[i], s_triangles[i + 1], s_triangles[i + 2]));
        }
    }
}
