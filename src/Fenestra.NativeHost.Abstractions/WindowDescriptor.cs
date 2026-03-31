namespace Fenestra.NativeHost.Abstractions;

public sealed record WindowDescriptor(
    uint WindowId,
    string Title,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsVisible = true);

public sealed record NativeWindowReference(
    uint WindowId,
    long HostHandle,
    string PlatformName,
    bool IsStub);

public enum NativeInputEventKind
{
    KeyDown,
    KeyUp,
    ButtonDown,
    ButtonUp,
    PointerMove,
    FocusIn,
    FocusOut
}

public sealed record NativeInputEvent(
    uint WindowId,
    NativeInputEventKind Kind,
    uint Detail,
    short X,
    short Y,
    ushort State);
