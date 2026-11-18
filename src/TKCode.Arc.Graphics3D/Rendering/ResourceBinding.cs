namespace TKCode.Arc.Graphics3D;

/// <summary>GPU-side state that one renderer keeps for one scene resource (geometry, texture, material, render target).
/// Stored in a short linked list on the resource itself, so lookups during drawing are a pointer compare instead of a
/// dictionary probe; with a single renderer the list has one entry.</summary>
internal abstract class ResourceBinding
{
    public ResourceBinding? Next;
    public Renderer Owner = null!;

    /// <summary>The frame the binding was last used in.</summary>
    public int LastUsedFrame;

    /// <summary>The resource whose list holds this binding.</summary>
    public object? Resource;

    /// <summary>Frees the GPU objects. Runs on the render thread with the context current.</summary>
    public abstract void Destroy();

    /// <summary>The binding of a renderer, or null.</summary>
    public static T? Find<T>(ResourceBinding? head, Renderer owner)
        where T : ResourceBinding
    {
        for (var b = head; b is not null; b = b.Next)
        {
            // A resource can carry several kinds of binding for one renderer (a texture used both as a map and as
            // the environment), so the type is part of the match.
            if (ReferenceEquals(b.Owner, owner) && b is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Takes this binding out of its resource's list (the renderer is going away; the resource lives on).</summary>
    public void Detach()
    {
        switch (Resource)
        {
            case BufferGeometry geometry:
                Unlink(ref geometry.Binding);
                break;
            case Texture texture:
                Unlink(ref texture.Binding);
                break;
            case Material material:
                Unlink(ref material.Binding);
                break;
            case RenderTarget target:
                Unlink(ref target.Binding);
                break;
            case InstancedMesh mesh:
                Unlink(ref mesh.InstanceBinding);
                break;
        }

        Resource = null;
    }

    private void Unlink(ref ResourceBinding? head)
    {
        ResourceBinding? previous = null;
        for (var b = head; b is not null; b = b.Next)
        {
            if (ReferenceEquals(b, this))
            {
                if (previous is null)
                {
                    head = b.Next;
                }
                else
                {
                    previous.Next = b.Next;
                }

                Next = null;
                return;
            }

            previous = b;
        }
    }

    /// <summary>Adds a binding to a resource's list.</summary>
    public static void Add(ref ResourceBinding? head, ResourceBinding binding, Renderer owner, object resource)
    {
        binding.Resource = resource;
        binding.Owner = owner;
        binding.Next = head;
        head = binding;
    }

    /// <summary>Hands every binding of a disposed resource to its renderer for destruction. Safe to call from any
    /// thread: the renderers free the GPU objects at the start of their next frame.</summary>
    public static void ReleaseAll(ref ResourceBinding? head)
    {
        var b = Interlocked.Exchange(ref head, null);
        while (b is not null)
        {
            var next = b.Next;
            b.Next = null;
            b.Owner.QueueDestroy(b);
            b = next;
        }
    }
}
