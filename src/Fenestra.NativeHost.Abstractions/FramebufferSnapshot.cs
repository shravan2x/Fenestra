namespace Fenestra.NativeHost.Abstractions;

public sealed record FramebufferSnapshot(
    int Width,
    int Height,
    int Stride,
    byte Depth,
    ReadOnlyMemory<byte> Pixels);
