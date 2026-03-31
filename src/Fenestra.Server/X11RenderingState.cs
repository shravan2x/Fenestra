using Fenestra.NativeHost.Abstractions;

namespace Fenestra.Server;

public sealed class X11RenderingState
{
    private readonly object _syncLock = new();
    private readonly Dictionary<uint, PixmapDefinition> _pixmaps = new();
    private readonly Dictionary<uint, GraphicsContextDefinition> _graphicsContexts = new();

    public X11RenderingState(
        uint rootDrawableId,
        ushort rootWidth,
        ushort rootHeight,
        byte rootDepth,
        byte bitsPerPixel)
    {
        RootFramebuffer = new SoftwareFramebuffer(
            rootDrawableId,
            rootWidth,
            rootHeight,
            rootDepth,
            bitsPerPixel);
    }

    public SoftwareFramebuffer RootFramebuffer { get; }

    public bool CreatePixmap(uint pixmapId, ushort width, ushort height, byte depth)
    {
        lock (_syncLock)
        {
            if (_pixmaps.ContainsKey(pixmapId))
            {
                return false;
            }

            _pixmaps[pixmapId] = new PixmapDefinition(
                pixmapId,
                width,
                height,
                depth,
                bitsPerPixel: 32);
            return true;
        }
    }

    public bool FreePixmap(uint pixmapId)
    {
        lock (_syncLock)
        {
            return _pixmaps.Remove(pixmapId);
        }
    }

    public bool TryGetPixmap(uint pixmapId, out PixmapDefinition? pixmap)
    {
        lock (_syncLock)
        {
            return _pixmaps.TryGetValue(pixmapId, out pixmap);
        }
    }

    public bool CreateGraphicsContext(uint graphicsContextId, uint drawableId)
    {
        lock (_syncLock)
        {
            if (_graphicsContexts.ContainsKey(graphicsContextId))
            {
                return false;
            }

            _graphicsContexts[graphicsContextId] = new GraphicsContextDefinition(graphicsContextId, drawableId);
            return true;
        }
    }

    public bool FreeGraphicsContext(uint graphicsContextId)
    {
        lock (_syncLock)
        {
            return _graphicsContexts.Remove(graphicsContextId);
        }
    }

    public bool TryGetGraphicsContext(uint graphicsContextId, out GraphicsContextDefinition? graphicsContext)
    {
        lock (_syncLock)
        {
            return _graphicsContexts.TryGetValue(graphicsContextId, out graphicsContext);
        }
    }
}

public sealed class SoftwareFramebuffer
{
    public SoftwareFramebuffer(
        uint drawableId,
        ushort width,
        ushort height,
        byte depth,
        byte bitsPerPixel)
    {
        DrawableId = drawableId;
        Width = width;
        Height = height;
        Depth = depth;
        BitsPerPixel = bitsPerPixel;
        StrideInBytes = width * (bitsPerPixel / 8);
        Pixels = new byte[StrideInBytes * height];
    }

    public uint DrawableId { get; }

    public ushort Width { get; }

    public ushort Height { get; }

    public byte Depth { get; }

    public byte BitsPerPixel { get; }

    public int StrideInBytes { get; }

    public byte[] Pixels { get; }

    public FramebufferSnapshot Snapshot()
    {
        return new FramebufferSnapshot(
            Width,
            Height,
            StrideInBytes,
            Depth,
            Pixels.ToArray());
    }
}

public sealed class PixmapDefinition
{
    public PixmapDefinition(
        uint pixmapId,
        ushort width,
        ushort height,
        byte depth,
        byte bitsPerPixel)
    {
        PixmapId = pixmapId;
        Width = width;
        Height = height;
        Depth = depth;
        BitsPerPixel = bitsPerPixel;
        StrideInBytes = width * (bitsPerPixel / 8);
        Pixels = new byte[StrideInBytes * height];
    }

    public uint PixmapId { get; }

    public ushort Width { get; }

    public ushort Height { get; }

    public byte Depth { get; }

    public byte BitsPerPixel { get; }

    public int StrideInBytes { get; }

    public byte[] Pixels { get; }
}

public sealed record GraphicsContextDefinition(
    uint GraphicsContextId,
    uint DrawableId);

public readonly record struct DrawableTarget(
    uint DrawableId,
    ushort Width,
    ushort Height,
    byte Depth,
    byte BitsPerPixel,
    int StrideInBytes,
    byte[] Pixels);

public readonly record struct GetImageResult(
    byte Depth,
    uint VisualId,
    byte[] ImageBytes);
