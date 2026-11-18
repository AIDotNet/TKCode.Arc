namespace TKCode.Arc.Graphics3D;

/// <summary>What <see cref="GltfLoader"/> produced from one glTF file: scene objects ready to add to a
/// <see cref="Scene"/>, and the resources they share. Dispose the asset to free those resources on the GPU.</summary>
public sealed class GltfAsset : IDisposable
{
    private bool _disposed;

    internal GltfAsset(
        Group scene, IReadOnlyList<Group> scenes, IReadOnlyList<AnimationClip> animations, IReadOnlyList<Camera> cameras,
        IReadOnlyList<Light> lights, IReadOnlyList<Material> materials, IReadOnlyList<Texture> textures,
        IReadOnlyList<BufferGeometry> geometries, IReadOnlyList<string> warnings)
    {
        Scene = scene;
        Scenes = scenes;
        Animations = animations;
        Cameras = cameras;
        Lights = lights;
        Materials = materials;
        Textures = textures;
        Geometries = geometries;
        Warnings = warnings;
    }

    /// <summary>The root of the file's default scene.</summary>
    public Group Scene { get; }

    /// <summary>The roots of all scenes of the file.</summary>
    public IReadOnlyList<Group> Scenes { get; }

    /// <summary>The animations, bound to the loaded objects.</summary>
    public IReadOnlyList<AnimationClip> Animations { get; }

    /// <summary>The cameras, each a child of the node that carries it.</summary>
    public IReadOnlyList<Camera> Cameras { get; }

    /// <summary>The lights of <c>KHR_lights_punctual</c>, each a child of the node that carries it.</summary>
    public IReadOnlyList<Light> Lights { get; }

    /// <summary>Every material that was created (one glTF material can give several, for example with and without
    /// vertex colors).</summary>
    public IReadOnlyList<Material> Materials { get; }

    /// <summary>Every texture that was created.</summary>
    public IReadOnlyList<Texture> Textures { get; }

    /// <summary>Every geometry that was created.</summary>
    public IReadOnlyList<BufferGeometry> Geometries { get; }

    /// <summary>Things in the file that were skipped because the engine does not support them.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Disposes the geometries, materials and textures of the asset.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var geometry in Geometries)
        {
            geometry.Dispose();
        }

        foreach (var material in Materials)
        {
            material.Dispose();
        }

        foreach (var texture in Textures)
        {
            texture.Dispose();
        }
    }
}

/// <summary>Loads glTF 2.0 files: <c>.gltf</c> (JSON with external or embedded buffers) and <c>.glb</c> (binary).
/// Loading only builds scene objects; nothing touches the GPU until the result is rendered, so it can run on any
/// thread.</summary>
public static class GltfLoader
{
    /// <summary>Loads a <c>.gltf</c> or <c>.glb</c> file. Buffers and images referenced by relative URI are read from
    /// the file's folder.</summary>
    public static GltfAsset Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Load(File.ReadAllBytes(path), FileResolver(path));
    }

    /// <summary>Loads glTF or GLB data.</summary>
    /// <param name="data">The contents of a <c>.gltf</c> or <c>.glb</c> file.</param>
    /// <param name="resolveUri">Returns the bytes of an external buffer or image by its URI (unescaped), or null when
    /// it does not exist. Not needed for self-contained files.</param>
    public static GltfAsset Load(ReadOnlyMemory<byte> data, Func<string, byte[]?>? resolveUri = null)
    {
        using var reader = new GltfReader(data, resolveUri);
        return reader.Read();
    }

    /// <summary>Loads a file like <see cref="Load(string)"/>, reading and decoding on a worker thread.</summary>
    public static async Task<GltfAsset> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return await LoadAsync(data, FileResolver(path), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads glTF or GLB data like the synchronous overload, on a worker thread.</summary>
    public static Task<GltfAsset> LoadAsync(ReadOnlyMemory<byte> data, Func<string, byte[]?>? resolveUri = null, CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Load(data, resolveUri);
            },
            cancellationToken);

    private static Func<string, byte[]?> FileResolver(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        return uri =>
        {
            if (uri.Contains("://", StringComparison.Ordinal))
            {
                return null;
            }

            var file = Path.GetFullPath(Path.Combine(folder, uri));
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        };
    }
}
