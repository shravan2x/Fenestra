namespace Fenestra.Server;

public sealed class X11ClientState
{
    private readonly object _syncLock = new();
    private readonly uint _resourceIdMask;
    private uint _nextResourceIdOffset;

    public X11ClientState(uint clientId, uint resourceIdBase, uint resourceIdMask)
    {
        if (resourceIdMask == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resourceIdMask), "Resource ID mask must be non-zero.");
        }

        ClientId = clientId;
        ResourceIdBase = resourceIdBase;
        _resourceIdMask = resourceIdMask;
        _nextResourceIdOffset = 1;
    }

    public uint ClientId { get; }

    public uint ResourceIdBase { get; }

    public uint ResourceIdMask => _resourceIdMask;

    public ushort SequenceNumber { get; private set; }

    public uint AllocateXid()
    {
        lock (_syncLock)
        {
            while (_nextResourceIdOffset <= _resourceIdMask)
            {
                var resourceId = ResourceIdBase | _nextResourceIdOffset;
                _nextResourceIdOffset++;

                if ((resourceId & ~_resourceIdMask) == ResourceIdBase)
                {
                    return resourceId;
                }
            }
        }

        throw new InvalidOperationException("The client resource ID range has been exhausted.");
    }

    public ushort AdvanceSequenceNumber()
    {
        SequenceNumber++;
        return SequenceNumber;
    }
}
