namespace Fenestra.Server;

public enum X11ResourceType
{
    Window,
    Pixmap,
    GraphicsContext,
    Colormap,
    Cursor
}

public sealed record X11ResourceRecord(
    uint ResourceId,
    X11ResourceType ResourceType,
    uint OwnerClientId);

public sealed class X11ResourceRegistry
{
    private readonly object _syncLock = new();
    private readonly Dictionary<uint, X11ResourceRecord> _resourcesById = new();

    public bool TryRegister(uint resourceId, X11ResourceType resourceType, uint ownerClientId)
    {
        lock (_syncLock)
        {
            if (_resourcesById.ContainsKey(resourceId))
            {
                return false;
            }

            _resourcesById[resourceId] = new X11ResourceRecord(resourceId, resourceType, ownerClientId);
            return true;
        }
    }

    public bool TryGet(uint resourceId, out X11ResourceRecord? resource)
    {
        lock (_syncLock)
        {
            return _resourcesById.TryGetValue(resourceId, out resource);
        }
    }

    public bool IsOwnedBy(uint resourceId, uint ownerClientId)
    {
        lock (_syncLock)
        {
            return _resourcesById.TryGetValue(resourceId, out var resource)
                && resource.OwnerClientId == ownerClientId;
        }
    }

    public IReadOnlyList<X11ResourceRecord> GetOwnedResources(uint ownerClientId)
    {
        lock (_syncLock)
        {
            return _resourcesById.Values
                .Where(resource => resource.OwnerClientId == ownerClientId)
                .OrderBy(resource => resource.ResourceId)
                .ToArray();
        }
    }

    public bool TryRemove(uint resourceId, uint ownerClientId)
    {
        lock (_syncLock)
        {
            if (!_resourcesById.TryGetValue(resourceId, out var resource))
            {
                return false;
            }

            if (resource.OwnerClientId != ownerClientId)
            {
                return false;
            }

            return _resourcesById.Remove(resourceId);
        }
    }

    public bool TryRemoveServerResource(uint resourceId)
    {
        lock (_syncLock)
        {
            if (!_resourcesById.TryGetValue(resourceId, out var resource))
            {
                return false;
            }

            if (resource.OwnerClientId != 0)
            {
                return false;
            }

            return _resourcesById.Remove(resourceId);
        }
    }
}
