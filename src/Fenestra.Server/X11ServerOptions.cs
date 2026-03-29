namespace Fenestra.Server;

public sealed record X11ServerOptions(
    int DisplayNumber,
    string ListenAddress,
    int Port,
    bool EnableNativeWindows = true);
