namespace Fenestra.Protocol.X11.Core;

[Flags]
public enum X11EventMask : uint
{
    None = 0,
    KeyPress = 1u << 0,
    KeyRelease = 1u << 1,
    ButtonPress = 1u << 2,
    ButtonRelease = 1u << 3,
    EnterWindow = 1u << 4,
    LeaveWindow = 1u << 5,
    PointerMotion = 1u << 6,
    Exposure = 1u << 15,
    FocusChange = 1u << 21
}

public enum X11EventKind : byte
{
    KeyPress = 2,
    KeyRelease = 3,
    ButtonPress = 4,
    ButtonRelease = 5,
    MotionNotify = 6,
    FocusIn = 9,
    FocusOut = 10,
    Expose = 12
}

public readonly record struct QueuedX11Event(
    X11EventKind Kind,
    X11EventMask RequiredMask,
    ushort SequenceNumber,
    byte Detail,
    uint RootWindowId,
    uint EventWindowId,
    short RootX,
    short RootY,
    short EventX,
    short EventY,
    ushort State,
    ushort Width,
    ushort Height,
    uint Time);
