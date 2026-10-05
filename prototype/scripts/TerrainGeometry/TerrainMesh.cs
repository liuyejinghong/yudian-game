using System;
using System.Collections;
using System.Collections.Generic;

namespace Yudian.Terrain;

/// <summary>Owned, immutable vertex and triangle buffers; no world state or renderer resources.</summary>
public sealed class TerrainMesh
{
    public int GridRows { get; }
    public int GridColumns { get; }
    public IReadOnlyList<TerrainVertex> Vertices { get; }
    public IReadOnlyList<int> Indices { get; }

    public TerrainMesh(int gridRows, int gridColumns, TerrainVertex[] vertices, int[] indices)
    {
        if (gridRows < 2 || gridRows > 513) throw new ArgumentException("gridRows must be 2..513", nameof(gridRows));
        if (gridColumns < 2 || gridColumns > 513) throw new ArgumentException("gridColumns must be 2..513", nameof(gridColumns));
        if (vertices is null || vertices.Length != gridRows * gridColumns)
            throw new ArgumentException("vertices length does not match grid layout", nameof(vertices));
        if (indices is null || indices.Length != (gridRows - 1) * (gridColumns - 1) * 6)
            throw new ArgumentException("indices length does not match grid cells", nameof(indices));
        var ownedVertices = (TerrainVertex[])vertices.Clone();
        var ownedIndices = (int[])indices.Clone();
        foreach (var vertex in ownedVertices)
            if (!double.IsFinite(vertex.X) || !double.IsFinite(vertex.Y) || !double.IsFinite(vertex.Z))
                throw new ArgumentException("vertices must be finite", nameof(vertices));
        foreach (var index in ownedIndices)
            if (index < 0 || index >= ownedVertices.Length)
                throw new ArgumentException("indices must reference vertices", nameof(indices));
        GridRows = gridRows;
        GridColumns = gridColumns;
        Vertices = new Buffer<TerrainVertex>(ownedVertices);
        Indices = new Buffer<int>(ownedIndices);
    }

    // ICollection.SyncRoot can expose storage even when its collection is read-only.
    private sealed class Buffer<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;
        public Buffer(T[] items) => _items = items;
        public int Count => _items.Length;
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _items.Length)
                    throw new ArgumentException("index is out of range", nameof(index));
                return _items[index];
            }
        }
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
