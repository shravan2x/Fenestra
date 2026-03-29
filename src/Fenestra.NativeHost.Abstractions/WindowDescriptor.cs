namespace Fenestra.NativeHost.Abstractions;

public sealed record WindowDescriptor(
    string Title,
    int X,
    int Y,
    int Width,
    int Height);
