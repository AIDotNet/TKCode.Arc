using System;
using System.Collections.Generic;

namespace TKCode.Arc.Graphics3D;

/// <summary>
/// Edgebreaker connectivity: the mesh is rebuilt face by face from a sequence of five symbols
/// (C, S, L, R, E), read in the reverse of the order the encoder wrote them.
/// </summary>
internal sealed class DracoEdgebreakerDecoder
{
    private const int TopologyC = 0;
    private const int TopologyS = 1;
    private const int TopologyL = 3;
    private const int TopologyR = 5;
    private const int TopologyE = 7;

    private const int StandardTraversal = 0;
    private const int PredictiveTraversal = 1;
    private const int ValenceTraversal = 2;

    private const int MinValence = 2;
    private const int MaxValence = 7;

    private static readonly int[] SymbolToTopology = { TopologyC, TopologyS, TopologyL, TopologyR, TopologyE };

    private readonly DracoBuffer _buffer;
    private readonly DracoLimits _limits;
    private readonly int _version;
    private readonly int _traversalType;
    private readonly DracoCornerTable _table = new();
    private readonly List<TopologySplit> _splits = new();
    private readonly DracoEncodingData _positionEncoding = new();
    private SeamData[] _seams = Array.Empty<SeamData>();
    private int _positionDecoderId = -1;
    private long _maxVertices;

    // Vertices that turned out to be interior; everything else is treated as lying on a hole.
    private bool[] _interior = Array.Empty<bool>();

    // Symbol sources.
    private DracoBitReader? _symbolBits;
    private DracoBitReader? _startFaceBits;
    private DracoRansBitDecoder? _startFaceDecoder;
    private DracoRansBitDecoder[] _seamDecoders = Array.Empty<DracoRansBitDecoder>();

    // Valence traversal: the symbols are split into one stream per valence of the vertex being left.
    private int[] _valences = Array.Empty<int>();
    private int[][] _contextSymbols = Array.Empty<int[]>();
    private int[] _contextCounters = Array.Empty<int>();
    private int _lastSymbol = -1;
    private int _activeContext = -1;

    public DracoEdgebreakerDecoder(DracoBuffer buffer)
    {
        _buffer = buffer;
        _limits = buffer.Limits;
        _version = buffer.Version;
        _traversalType = buffer.ReadByte();
        if (_traversalType == PredictiveTraversal)
        {
            throw DracoError.Unsupported(
                $"the predictive edgebreaker traversal (deprecated, bitstream {_version >> 8}.{_version & 0xFF}) is not supported.");
        }

        if (_traversalType != StandardTraversal && _traversalType != ValenceTraversal)
        {
            throw DracoError.Invalid("unknown edgebreaker traversal.");
        }

        DracoTrace.Add(_traversalType == StandardTraversal ? "edgebreaker.standard" : "edgebreaker.valence");
    }

    /// <summary>The point of every corner, three per face.</summary>
    public int[] Faces { get; private set; } = Array.Empty<int>();

    public int NumPoints { get; private set; }

    /// <summary>Reads the header of one attributes decoder and says how its values are ordered on the mesh.</summary>
    public void ConfigureAttributesDecoder(int decoderId, DracoAttributesDecoder decoder)
    {
        int seamId = _buffer.ReadSByte();
        byte elementType = _buffer.ReadByte();
        if (seamId >= 0)
        {
            if (seamId >= _seams.Length || _seams[seamId].DecoderId >= 0)
            {
                throw DracoError.Invalid("attribute connectivity is referenced incorrectly.");
            }

            _seams[seamId].DecoderId = decoderId;
        }
        else
        {
            if (_positionDecoderId >= 0)
            {
                throw DracoError.Invalid("two attribute decoders use the mesh connectivity.");
            }

            _positionDecoderId = decoderId;
        }

        int traversalMethod = 0;
        if (_version >= DracoVersion.V1_2)
        {
            traversalMethod = _buffer.ReadByte();
            if (traversalMethod > 1)
            {
                throw DracoError.Invalid("unknown mesh traversal method.");
            }
        }

        if (elementType == 0)
        {
            // Per-vertex attribute: one value per mesh vertex, even when it was given seam data.
            decoder.Table = _table;
            decoder.Encoding = seamId < 0 ? _positionEncoding : _seams[seamId].Encoding;
            decoder.MaxPredictionDegree = traversalMethod == 1;
            DracoTrace.Add(traversalMethod == 1 ? "traversal.maxPredictionDegree" : "traversal.depthFirst");
        }
        else
        {
            // Per-corner attribute: values are split along the attribute's seams.
            if (traversalMethod != 0 || seamId < 0)
            {
                throw DracoError.Invalid("a per-corner attribute is set up incorrectly.");
            }

            decoder.Table = _seams[seamId].Connectivity;
            decoder.Encoding = _seams[seamId].Encoding;
            decoder.MaxPredictionDegree = false;
            DracoTrace.Add("traversal.depthFirst.attributeSeams");
        }
    }

    public void DecodeConnectivity()
    {
        DracoBuffer buffer = _buffer;
        if (_version < DracoVersion.V2_2)
        {
            // Number of vertices the encoder added to split non-manifold ones; not needed for decoding.
            buffer.ReadCount(DracoVersion.V2_0);
        }

        uint numEncodedVertices = buffer.ReadCount(DracoVersion.V2_0);
        uint numFacesDeclared = buffer.ReadCount(DracoVersion.V2_0);

        // Per face: two corner tables, the corners' points and the final indices.
        _limits.CheckDeclared(numFacesDeclared, 12 * sizeof(int));
        int numFaces = (int)numFacesDeclared;
        if (numEncodedVertices > (uint)numFaces * 3)
        {
            throw DracoError.Invalid("more vertices than corners.");
        }

        // A closed surface needs enough vertex pairs for its edges.
        ulong vertices = numEncodedVertices;
        if (unchecked(vertices * (vertices - 1)) / 2 < (ulong)(3L * numFaces / 2))
        {
            throw DracoError.Invalid("too few vertices for the number of faces.");
        }

        int numSeams = buffer.ReadByte();
        uint numSymbolsDeclared = buffer.ReadCount(DracoVersion.V2_0);
        if (numSymbolsDeclared > (uint)numFaces || (uint)numFaces > numSymbolsDeclared + (numSymbolsDeclared / 3))
        {
            throw DracoError.Invalid("the symbol count does not match the face count.");
        }

        int numSymbols = (int)numSymbolsDeclared;
        uint numSplitSymbols = buffer.ReadCount(DracoVersion.V2_0);
        if (numSplitSymbols > numSymbolsDeclared)
        {
            throw DracoError.Invalid("more split symbols than symbols.");
        }

        _table.NumFaces = numFaces;
        _maxVertices = (long)numEncodedVertices + numSplitSymbols;
        _seams = new SeamData[numSeams];
        for (int i = 0; i < numSeams; i++)
        {
            _seams[i] = new SeamData();
        }

        int eventBytes = -1;
        if (_version < DracoVersion.V2_2)
        {
            // Older streams store the split events after the symbols; read them first, then come back.
            uint connectivitySize = buffer.ReadCount(DracoVersion.V2_0);
            if (connectivitySize == 0 || connectivitySize > (uint)buffer.Remaining)
            {
                throw DracoError.Invalid("the connectivity size is wrong.");
            }

            int symbolsStart = buffer.Position;
            int eventsStart = symbolsStart + (int)connectivitySize;
            buffer.Seek(eventsStart);
            DecodeSplitEvents(numFaces);
            eventBytes = buffer.Position - eventsStart;
            buffer.Seek(symbolsStart);
        }
        else
        {
            DecodeSplitEvents(numFaces);
        }

        StartTraversal(numFaces);
        int numConnectivityVertices = DecodeSymbols(numSymbols);
        if (eventBytes >= 0)
        {
            buffer.Skip(eventBytes);
        }

        if (numSeams > 0)
        {
            DecodeSeams();
            DracoTrace.Add("edgebreaker.attributeSeams");
        }

        _positionEncoding.VertexToData = _limits.Ints(_table.NumVertices);
        for (int i = 0; i < numSeams; i++)
        {
            SeamData seam = _seams[i];
            RecomputeSeamVertices(seam);
            seam.Encoding.VertexToData = _limits.Ints(Math.Max(seam.Connectivity.NumVertices, _table.NumVertices));
        }

        AssignPointsToCorners(numConnectivityVertices);
    }

    private void DecodeSplitEvents(int numFaces)
    {
        DracoBuffer buffer = _buffer;
        uint numSplits = buffer.ReadCount(DracoVersion.V2_0);
        if (numSplits > 0)
        {
            DracoTrace.Add("edgebreaker.topologySplits");
            if (numSplits > (uint)numFaces)
            {
                throw DracoError.Invalid("more topology splits than faces.");
            }

            if (_version < DracoVersion.V1_2)
            {
                for (uint i = 0; i < numSplits; i++)
                {
                    uint split = buffer.ReadUInt32();
                    uint source = buffer.ReadUInt32();
                    _splits.Add(new TopologySplit(source, split, (buffer.ReadByte() & 1) != 0));
                }
            }
            else
            {
                // Sources are stored as deltas to the previous one, splits as distances back from their source.
                uint lastSource = 0;
                for (uint i = 0; i < numSplits; i++)
                {
                    uint source = unchecked(buffer.ReadVarUInt32() + lastSource);
                    uint delta = buffer.ReadVarUInt32();
                    if (delta > source)
                    {
                        throw DracoError.Invalid("a topology split points before the first symbol.");
                    }

                    _splits.Add(new TopologySplit(source, source - delta, false));
                    lastSource = source;
                }

                DracoBitReader bits = buffer.StartBits();
                int width = _version < DracoVersion.V2_2 ? 2 : 1;
                for (int i = 0; i < _splits.Count; i++)
                {
                    TopologySplit split = _splits[i];
                    _splits[i] = new TopologySplit(split.Source, split.Split, (bits.ReadBits(width) & 1) != 0);
                }

                buffer.Skip(bits.BytesConsumed);
            }
        }

        // Hole events were written by old encoders only and never used for decoding; skip them.
        uint numHoleEvents = 0;
        if (_version < DracoVersion.V2_0)
        {
            numHoleEvents = buffer.ReadUInt32();
        }
        else if (_version < DracoVersion.V2_1)
        {
            numHoleEvents = buffer.ReadVarUInt32();
        }

        if (numHoleEvents > 0)
        {
            DracoTrace.Add("edgebreaker.holeEvents");
            if (_version < DracoVersion.V1_2)
            {
                buffer.Skip(4L * numHoleEvents);
            }
            else
            {
                for (uint i = 0; i < numHoleEvents; i++)
                {
                    buffer.ReadVarUInt32();
                }
            }
        }
    }

    private void StartTraversal(int numFaces)
    {
        DracoBuffer buffer = _buffer;
        bool legacy = _version < DracoVersion.V2_2;
        if (_traversalType == StandardTraversal || legacy)
        {
            // Symbols as plain bits: C is "0", the others are three bits.
            ulong size = legacy ? buffer.ReadUInt64() : buffer.ReadVarUInt64();
            _symbolBits = buffer.StartBits();
            if (size > (ulong)buffer.Remaining)
            {
                throw DracoError.Invalid("the symbol data is longer than the file.");
            }

            buffer.Skip((long)size);
        }

        if (legacy)
        {
            ulong size = buffer.ReadUInt64();
            _startFaceBits = buffer.StartBits();
            if (size > (ulong)buffer.Remaining)
            {
                throw DracoError.Invalid("the start face data is longer than the file.");
            }

            buffer.Skip((long)size);
        }
        else
        {
            _startFaceDecoder = DracoRansBitDecoder.Start(buffer);
        }

        _seamDecoders = new DracoRansBitDecoder[_seams.Length];
        for (int i = 0; i < _seamDecoders.Length; i++)
        {
            _seamDecoders[i] = DracoRansBitDecoder.Start(buffer);
        }

        if (_traversalType != ValenceTraversal)
        {
            return;
        }

        if (legacy)
        {
            DracoTrace.Add("edgebreaker.valence.legacy");
            uint numSplitSymbols = buffer.ReadCount(DracoVersion.V2_0);
            if (numSplitSymbols >= _maxVertices)
            {
                throw DracoError.Invalid("too many split symbols.");
            }

            if (buffer.ReadSByte() != 0)
            {
                throw DracoError.Invalid("unknown valence coding mode.");
            }
        }

        int contexts = MaxValence - MinValence + 1;
        _contextSymbols = new int[contexts][];
        _contextCounters = new int[contexts];
        for (int i = 0; i < contexts; i++)
        {
            uint count = buffer.ReadVarUInt32();
            if (count > (uint)numFaces)
            {
                throw DracoError.Invalid("more symbols in a valence context than faces.");
            }

            _contextSymbols[i] = DracoSymbols.Decode(buffer, (int)count, 1);
            _contextCounters[i] = (int)count;
        }
    }

    private int DecodeSymbol()
    {
        if (_traversalType == ValenceTraversal)
        {
            if (_activeContext >= 0)
            {
                int counter = --_contextCounters[_activeContext];
                if (counter < 0)
                {
                    throw DracoError.Invalid("a valence context ran out of symbols.");
                }

                uint id = unchecked((uint)_contextSymbols[_activeContext][counter]);
                if (id > 4)
                {
                    throw DracoError.Invalid("unknown edgebreaker symbol.");
                }

                _lastSymbol = SymbolToTopology[id];
            }
            else if (_symbolBits != null)
            {
                _lastSymbol = DecodeSymbolBits(_symbolBits);
            }
            else
            {
                // Without a known valence the traversal can only be starting a new component.
                _lastSymbol = TopologyE;
            }

            return _lastSymbol;
        }

        return DecodeSymbolBits(_symbolBits!);
    }

    private static int DecodeSymbolBits(DracoBitReader bits)
    {
        uint symbol = bits.ReadBits(1);
        if (symbol == TopologyC)
        {
            return TopologyC;
        }

        return (int)(symbol | (bits.ReadBits(2) << 1));
    }

    private bool DecodeStartFaceConfiguration()
    {
        if (_startFaceBits != null)
        {
            return _startFaceBits.ReadBits(1) != 0;
        }

        return _startFaceDecoder!.DecodeBit();
    }

    // Keeps the valence of every vertex up to date and picks the symbol stream for the next symbol.
    private void OnActiveCorner(int corner)
    {
        if (_traversalType != ValenceTraversal)
        {
            return;
        }

        DracoCornerTable table = _table;
        int[] valences = _valences;
        int next = table.CornerToVertex[DracoCornerTable.Next(corner)];
        int previous = table.CornerToVertex[DracoCornerTable.Previous(corner)];
        switch (_lastSymbol)
        {
            case TopologyC:
            case TopologyS:
                valences[next] += 1;
                valences[previous] += 1;
                break;
            case TopologyR:
                valences[table.CornerToVertex[corner]] += 1;
                valences[next] += 1;
                valences[previous] += 2;
                break;
            case TopologyL:
                valences[table.CornerToVertex[corner]] += 1;
                valences[next] += 2;
                valences[previous] += 1;
                break;
            case TopologyE:
                valences[table.CornerToVertex[corner]] += 2;
                valences[next] += 2;
                valences[previous] += 2;
                break;
            default:
                break;
        }

        _activeContext = Math.Clamp(valences[next], MinValence, MaxValence) - MinValence;
    }

    private int AddVertex()
    {
        DracoCornerTable table = _table;
        int vertex = table.AddVertex(_limits);
        if (table.NumVertices > _maxVertices)
        {
            throw DracoError.Invalid("more vertices decoded than declared.");
        }

        // The per-vertex arrays follow the vertex table, so they too grow with what is really decoded.
        int capacity = table.VertexCorners.Length;
        if (_interior.Length < capacity)
        {
            _limits.Charge(capacity);
            Array.Resize(ref _interior, capacity);
            if (_traversalType == ValenceTraversal)
            {
                _limits.Charge((long)sizeof(int) * capacity);
                Array.Resize(ref _valences, capacity);
            }
        }

        return vertex;
    }

    private int DecodeSymbols(int numSymbols)
    {
        DracoCornerTable table = _table;
        List<int> activeCorners = new();
        Dictionary<int, int> splitActiveCorners = new();
        List<int> invalidVertices = new();
        bool removeInvalidVertices = _seams.Length == 0;
        int numFaces = 0;
        for (int symbolId = 0; symbolId < numSymbols; symbolId++)
        {
            int face = numFaces++;
            table.EnsureFaceCapacity(numFaces, _limits);
            int[] cornerToVertex = table.CornerToVertex;
            int[] opposite = table.Opposite;
            int corner = 3 * face;
            bool checkTopologySplit = false;
            int symbol = DecodeSymbol();
            if (symbol == TopologyC)
            {
                // The new face closes the fan around the vertex next to the active edge.
                if (activeCorners.Count == 0)
                {
                    throw Inconsistent();
                }

                int cornerA = activeCorners[activeCorners.Count - 1];
                int vertexX = cornerToVertex[DracoCornerTable.Next(cornerA)];
                int cornerB = DracoCornerTable.Next(table.VertexCorners[vertexX]);
                if (cornerB < 0 || cornerA == cornerB || opposite[cornerA] >= 0 || opposite[cornerB] >= 0)
                {
                    throw Inconsistent();
                }

                table.SetOpposite(cornerA, corner + 1);
                table.SetOpposite(cornerB, corner + 2);
                int vertexAPrevious = cornerToVertex[DracoCornerTable.Previous(cornerA)];
                int vertexBNext = cornerToVertex[DracoCornerTable.Next(cornerB)];
                if (vertexX == vertexAPrevious || vertexX == vertexBNext)
                {
                    throw Inconsistent();
                }

                cornerToVertex[corner] = vertexX;
                cornerToVertex[corner + 1] = vertexBNext;
                cornerToVertex[corner + 2] = vertexAPrevious;
                table.VertexCorners[vertexAPrevious] = corner + 2;
                _interior[vertexX] = true;
                activeCorners[activeCorners.Count - 1] = corner;
            }
            else if (symbol == TopologyR || symbol == TopologyL)
            {
                // The new face hangs on the active edge and brings one new vertex.
                if (activeCorners.Count == 0)
                {
                    throw Inconsistent();
                }

                int cornerA = activeCorners[activeCorners.Count - 1];
                if (opposite[cornerA] >= 0)
                {
                    throw Inconsistent();
                }

                int oppositeCorner;
                int cornerL;
                int cornerR;
                if (symbol == TopologyR)
                {
                    oppositeCorner = corner + 2;
                    cornerL = corner + 1;
                    cornerR = corner;
                }
                else
                {
                    oppositeCorner = corner + 1;
                    cornerL = corner;
                    cornerR = corner + 2;
                }

                table.SetOpposite(oppositeCorner, cornerA);
                int newVertex = AddVertex();
                cornerToVertex[oppositeCorner] = newVertex;
                table.VertexCorners[newVertex] = oppositeCorner;
                int vertexR = cornerToVertex[DracoCornerTable.Previous(cornerA)];
                cornerToVertex[cornerR] = vertexR;
                table.VertexCorners[vertexR] = cornerR;
                cornerToVertex[cornerL] = cornerToVertex[DracoCornerTable.Next(cornerA)];
                activeCorners[activeCorners.Count - 1] = corner;
                checkTopologySplit = true;
            }
            else if (symbol == TopologyS)
            {
                // The new face joins two separately built parts of the surface.
                if (activeCorners.Count == 0)
                {
                    throw Inconsistent();
                }

                int cornerB = activeCorners[activeCorners.Count - 1];
                activeCorners.RemoveAt(activeCorners.Count - 1);
                if (splitActiveCorners.TryGetValue(symbolId, out int splitCorner))
                {
                    activeCorners.Add(splitCorner);
                }

                if (activeCorners.Count == 0)
                {
                    throw Inconsistent();
                }

                int cornerA = activeCorners[activeCorners.Count - 1];
                if (cornerA == cornerB || opposite[cornerA] >= 0 || opposite[cornerB] >= 0)
                {
                    throw Inconsistent();
                }

                table.SetOpposite(cornerA, corner + 2);
                table.SetOpposite(cornerB, corner + 1);
                int vertexP = cornerToVertex[DracoCornerTable.Previous(cornerA)];
                cornerToVertex[corner] = vertexP;
                cornerToVertex[corner + 1] = cornerToVertex[DracoCornerTable.Next(cornerA)];
                int vertexBPrevious = cornerToVertex[DracoCornerTable.Previous(cornerB)];
                cornerToVertex[corner + 2] = vertexBPrevious;
                table.VertexCorners[vertexBPrevious] = corner + 2;

                // Both parts had their own copy of the shared vertex; keep one.
                int cornerN = DracoCornerTable.Next(cornerB);
                int vertexN = cornerToVertex[cornerN];
                if (_traversalType == ValenceTraversal)
                {
                    _valences[vertexP] += _valences[vertexN];
                }

                table.VertexCorners[vertexP] = table.VertexCorners[vertexN];
                int firstCorner = cornerN;
                while (cornerN >= 0)
                {
                    cornerToVertex[cornerN] = vertexP;
                    cornerN = table.SwingLeft(cornerN);
                    if (cornerN == firstCorner)
                    {
                        throw Inconsistent();
                    }
                }

                table.VertexCorners[vertexN] = -1;
                if (removeInvalidVertices)
                {
                    invalidVertices.Add(vertexN);
                }

                activeCorners[activeCorners.Count - 1] = corner;
            }
            else if (symbol == TopologyE)
            {
                // A face with three new vertices starts a new part.
                int first = AddVertex();
                int second = AddVertex();
                int third = AddVertex();
                cornerToVertex[corner] = first;
                cornerToVertex[corner + 1] = second;
                cornerToVertex[corner + 2] = third;
                table.VertexCorners[first] = corner;
                table.VertexCorners[second] = corner + 1;
                table.VertexCorners[third] = corner + 2;
                activeCorners.Add(corner);
                checkTopologySplit = true;
            }
            else
            {
                throw DracoError.Invalid("unknown edgebreaker symbol.");
            }

            OnActiveCorner(activeCorners[activeCorners.Count - 1]);
            if (checkTopologySplit)
            {
                // The encoder numbered symbols in the opposite direction.
                int encoderSymbolId = numSymbols - symbolId - 1;
                while (_splits.Count > 0)
                {
                    TopologySplit split = _splits[_splits.Count - 1];
                    if (split.Source > (uint)encoderSymbolId)
                    {
                        throw DracoError.Invalid("a topology split is out of order.");
                    }

                    if (split.Source != (uint)encoderSymbolId)
                    {
                        break;
                    }

                    if (split.Split > int.MaxValue)
                    {
                        throw DracoError.Invalid("a topology split is out of range.");
                    }

                    _splits.RemoveAt(_splits.Count - 1);
                    int top = activeCorners[activeCorners.Count - 1];
                    int newActiveCorner = split.RightEdge ? DracoCornerTable.Next(top) : DracoCornerTable.Previous(top);
                    splitActiveCorners[numSymbols - (int)split.Split - 1] = newActiveCorner;
                }
            }
        }

        // Every part that is still open ends in a start face: either it was a hole, or one more face closes it.
        while (activeCorners.Count > 0)
        {
            int corner = activeCorners[activeCorners.Count - 1];
            activeCorners.RemoveAt(activeCorners.Count - 1);
            if (!DecodeStartFaceConfiguration())
            {
                DracoTrace.Add("edgebreaker.boundary");
                continue;
            }

            DracoTrace.Add("edgebreaker.closedPart");

            if (numFaces >= table.NumFaces)
            {
                throw DracoError.Invalid("more faces decoded than declared.");
            }

            table.EnsureFaceCapacity(numFaces + 1, _limits);
            int[] cornerToVertex = table.CornerToVertex;
            int[] opposite = table.Opposite;
            int vertexN = cornerToVertex[DracoCornerTable.Next(corner)];
            int cornerB = DracoCornerTable.Next(table.VertexCorners[vertexN]);
            if (cornerB < 0)
            {
                throw Inconsistent();
            }

            int vertexX = cornerToVertex[DracoCornerTable.Next(cornerB)];
            int cornerC = DracoCornerTable.Next(table.VertexCorners[vertexX]);
            if (cornerC < 0 || corner == cornerB || corner == cornerC || cornerB == cornerC)
            {
                throw Inconsistent();
            }

            if (opposite[corner] >= 0 || opposite[cornerB] >= 0 || opposite[cornerC] >= 0)
            {
                throw Inconsistent();
            }

            int vertexP = cornerToVertex[DracoCornerTable.Next(cornerC)];
            int newCorner = 3 * numFaces++;
            table.SetOpposite(newCorner, corner);
            table.SetOpposite(newCorner + 1, cornerB);
            table.SetOpposite(newCorner + 2, cornerC);
            cornerToVertex[newCorner] = vertexX;
            cornerToVertex[newCorner + 1] = vertexP;
            cornerToVertex[newCorner + 2] = vertexN;
            _interior[vertexX] = true;
            _interior[vertexP] = true;
            _interior[vertexN] = true;
        }

        if (numFaces != table.NumFaces)
        {
            throw DracoError.Invalid("fewer faces decoded than declared.");
        }

        // Merged vertices left gaps in the numbering; fill each gap with the last vertex.
        int numVertices = table.NumVertices;
        if (invalidVertices.Count > 0)
        {
            DracoTrace.Add("edgebreaker.mergedVertices");
        }

        foreach (int invalidVertex in invalidVertices)
        {
            int source = numVertices - 1;
            while (source >= 0 && table.VertexCorners[source] < 0)
            {
                source = --numVertices - 1;
            }

            if (source < invalidVertex)
            {
                continue;
            }

            // Walk all corners of the source vertex: left around the fan, then right from the start.
            int start = table.VertexCorners[source];
            int current = start;
            bool leftTraversal = true;
            int guard = table.NumCorners;
            while (current >= 0)
            {
                if (table.CornerToVertex[current] != source || guard-- < 0)
                {
                    throw Inconsistent();
                }

                table.CornerToVertex[current] = invalidVertex;
                if (leftTraversal)
                {
                    current = table.SwingLeft(current);
                    if (current < 0)
                    {
                        current = table.SwingRight(start);
                        leftTraversal = false;
                    }
                    else if (current == start)
                    {
                        current = -1;
                    }
                }
                else
                {
                    current = table.SwingRight(current);
                }
            }

            table.VertexCorners[invalidVertex] = table.VertexCorners[source];
            table.VertexCorners[source] = -1;
            _interior[invalidVertex] = _interior[source];
            _interior[source] = true;
            numVertices--;
        }

        return numVertices;
    }

    // One bit per interior edge and attribute tells whether the attribute is discontinuous across it.
    private void DecodeSeams()
    {
        DracoCornerTable table = _table;
        int numCorners = table.NumCorners;
        SeamData[] seams = _seams;
        foreach (SeamData seam in seams)
        {
            int[] opposite = _limits.Ints(numCorners);
            table.Opposite.AsSpan(0, numCorners).CopyTo(opposite);
            seam.Connectivity = new DracoCornerTable { NumFaces = table.NumFaces, Opposite = opposite };
            seam.IsVertexOnSeam = _limits.Bools(table.NumVertices);
        }

        // Before 2.1 every edge was written from both of its faces.
        bool legacy = _version < DracoVersion.V2_1;
        for (int corner = 0; corner < numCorners; corner++)
        {
            int oppositeCorner = table.Opposite[corner];
            if (oppositeCorner < 0)
            {
                foreach (SeamData seam in seams)
                {
                    AddSeamEdge(seam, corner);
                }

                continue;
            }

            if (!legacy && oppositeCorner / 3 < corner / 3)
            {
                continue;
            }

            for (int i = 0; i < seams.Length; i++)
            {
                if (_seamDecoders[i].DecodeBit())
                {
                    AddSeamEdge(seams[i], corner);
                }
            }
        }
    }

    private void AddSeamEdge(SeamData seam, int corner)
    {
        DracoCornerTable table = _table;
        seam.Connectivity.Opposite[corner] = -1;
        seam.IsVertexOnSeam[table.CornerToVertex[DracoCornerTable.Next(corner)]] = true;
        seam.IsVertexOnSeam[table.CornerToVertex[DracoCornerTable.Previous(corner)]] = true;
        int oppositeCorner = table.Opposite[corner];
        if (oppositeCorner >= 0)
        {
            seam.Connectivity.Opposite[oppositeCorner] = -1;
            seam.IsVertexOnSeam[table.CornerToVertex[DracoCornerTable.Next(oppositeCorner)]] = true;
            seam.IsVertexOnSeam[table.CornerToVertex[DracoCornerTable.Previous(oppositeCorner)]] = true;
        }
    }

    // Splits every mesh vertex into one attribute vertex per run of corners between two seams.
    private void RecomputeSeamVertices(SeamData seam)
    {
        DracoCornerTable table = _table;
        DracoCornerTable attribute = seam.Connectivity;
        int numCorners = table.NumCorners;
        int[] cornerToVertex = _limits.Ints(numCorners);
        cornerToVertex.AsSpan().Fill(-1);

        // At most one attribute vertex per corner; charged as such, the list itself grows as needed.
        _limits.Charge((long)sizeof(int) * numCorners);
        List<int> leftMostCorners = new();
        for (int vertex = 0; vertex < table.NumVertices; vertex++)
        {
            int corner = table.VertexCorners[vertex];
            if (corner < 0)
            {
                continue;
            }

            int attributeVertex = leftMostCorners.Count;
            int firstCorner = corner;
            if (seam.IsVertexOnSeam[vertex])
            {
                // Start at the seam so that each run is walked from its beginning.
                int current = attribute.SwingLeft(firstCorner);
                while (current >= 0)
                {
                    firstCorner = current;
                    current = attribute.SwingLeft(current);
                    if (current == corner)
                    {
                        throw Inconsistent();
                    }
                }
            }

            cornerToVertex[firstCorner] = attributeVertex;
            leftMostCorners.Add(firstCorner);
            int guard = numCorners;
            int next = table.SwingRight(firstCorner);
            while (next >= 0 && next != firstCorner)
            {
                if (guard-- < 0)
                {
                    throw Inconsistent();
                }

                if (attribute.Opposite[DracoCornerTable.Next(next)] < 0)
                {
                    attributeVertex = leftMostCorners.Count;
                    leftMostCorners.Add(next);
                }

                cornerToVertex[next] = attributeVertex;
                next = table.SwingRight(next);
            }
        }

        attribute.CornerToVertex = cornerToVertex;
        attribute.VertexCorners = leftMostCorners.ToArray();
        attribute.NumVertices = leftMostCorners.Count;
    }

    // A point is a vertex together with one consistent set of attribute values: corners around a vertex
    // share a point as long as no attribute has a seam between them.
    private void AssignPointsToCorners(int numConnectivityVertices)
    {
        DracoCornerTable table = _table;
        int numCorners = table.NumCorners;
        int[] faces = _limits.Ints(numCorners);
        SeamData[] seams = _seams;
        if (seams.Length == 0)
        {
            table.CornerToVertex.AsSpan(0, numCorners).CopyTo(faces);
            Faces = faces;
            NumPoints = numConnectivityVertices;
            return;
        }

        int numPoints = 0;
        for (int vertex = 0; vertex < table.NumVertices; vertex++)
        {
            int corner = table.VertexCorners[vertex];
            if (corner < 0)
            {
                continue;
            }

            int firstCorner = corner;
            if (_interior[vertex])
            {
                // An interior vertex has no natural first corner: start at a seam if there is one, so that
                // the corners on both sides of the start do not end up in different points by accident.
                foreach (SeamData seam in seams)
                {
                    if (!seam.IsVertexOnSeam[table.CornerToVertex[corner]])
                    {
                        continue;
                    }

                    int[] attributeVertices = seam.Connectivity.CornerToVertex;
                    int attributeVertex = attributeVertices[corner];
                    int current = table.SwingRight(corner);
                    bool seamFound = false;
                    while (current != corner)
                    {
                        if (current < 0)
                        {
                            throw Inconsistent();
                        }

                        if (attributeVertices[current] != attributeVertex)
                        {
                            firstCorner = current;
                            seamFound = true;
                            break;
                        }

                        current = table.SwingRight(current);
                    }

                    if (seamFound)
                    {
                        break;
                    }
                }
            }

            int point = numPoints++;
            faces[firstCorner] = point;
            int previous = firstCorner;
            int next = table.SwingRight(firstCorner);
            while (next >= 0 && next != firstCorner)
            {
                foreach (SeamData seam in seams)
                {
                    int[] attributeVertices = seam.Connectivity.CornerToVertex;
                    if (attributeVertices[next] != attributeVertices[previous])
                    {
                        point = numPoints++;
                        break;
                    }
                }

                faces[next] = point;
                previous = next;
                next = table.SwingRight(next);
            }
        }

        Faces = faces;
        NumPoints = numPoints;
    }

    private static Exception Inconsistent()
    {
        return DracoError.Invalid("the connectivity symbols do not describe a mesh.");
    }

    /// <summary>Connectivity of one attribute that may be discontinuous across edges.</summary>
    private sealed class SeamData
    {
        public int DecoderId = -1;
        public DracoCornerTable Connectivity = null!;
        public bool[] IsVertexOnSeam = Array.Empty<bool>();
        public DracoEncodingData Encoding = new();
    }

    private readonly struct TopologySplit
    {
        public TopologySplit(uint source, uint split, bool rightEdge)
        {
            Source = source;
            Split = split;
            RightEdge = rightEdge;
        }

        public uint Source { get; }

        public uint Split { get; }

        public bool RightEdge { get; }
    }
}
