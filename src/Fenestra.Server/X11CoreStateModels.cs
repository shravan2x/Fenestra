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
