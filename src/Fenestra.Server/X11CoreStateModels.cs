namespace Fenestra.Server;

public sealed record X11PropertyValue(
    uint AtomId,
    byte Format,
    byte[] Value);

public sealed record X11ColormapDefinition(
    uint ColormapId,
    uint VisualId);

public sealed record X11CursorDefinition(
    uint CursorId);

public enum X11MapState : byte
{
    Unmapped = 0,
    Unviewable = 1,
    Viewable = 2
}

public sealed class X11WindowDefinition
{
    public X11WindowDefinition(
        uint id,
        uint? parentId,
        short x,
        short y,
        ushort width,
        ushort height,
        ushort borderWidth,
        byte depth,
        X11MapState mapState = X11MapState.Unmapped)
    {
        Id = id;
        ParentId = parentId;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        BorderWidth = borderWidth;
        Depth = depth;
        MapState = mapState;
    }

    public uint Id { get; }

    public uint? ParentId { get; set; }

    public short X { get; set; }

    public short Y { get; set; }

    public ushort Width { get; set; }

    public ushort Height { get; set; }

    public ushort BorderWidth { get; set; }

    public byte Depth { get; }

    public X11MapState MapState { get; set; }

    public List<uint> ChildWindowIds { get; } = [];
}
