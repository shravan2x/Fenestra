using Fenestra.NativeHost.Abstractions;
using Fenestra.Protocol.X11.Parsing;
using Fenestra.Transport;

namespace Fenestra.Server;

public sealed class X11Server
{
    private readonly IX11TransportListener _listener;
    private readonly INativeWindowHost _windowHost;

    public X11Server(
        IX11TransportListener listener,
        INativeWindowHost windowHost)
    {
        _listener = listener;
        _windowHost = windowHost;
    }

    public async Task StartAsync(X11ServerOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Starting Fenestra display :{options.DisplayNumber} on port {options.Port}.");
        Console.WriteLine("This bootstrap server currently models the startup architecture only.");

        await _listener.StartAsync(cancellationToken).ConfigureAwait(false);

        if (options.EnableNativeWindows)
        {
            await _windowHost.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _windowHost.ShowWindowAsync(
                new WindowDescriptor(
                    Title: "Fenestra bootstrap host",
                    X: 100,
                    Y: 100,
                    Width: 1024,
                    Height: 768),
                cancellationToken).ConfigureAwait(false);
        }

        var sampleSetupBytes = new byte[]
        {
            (byte)'l', 0, // little-endian
            11, 0,        // major version
            0, 0,         // minor version
            0, 0,         // auth protocol name length
            0, 0,         // auth data length
            0, 0          // padding
        };

        if (X11HandshakeParser.TryParseSetupRequest(sampleSetupBytes, out var request) && request is not null)
        {
            Console.WriteLine(
                $"Parsed sample X11 setup request for protocol {request.ProtocolMajorVersion}.{request.ProtocolMinorVersion}.");
        }
    }
}
