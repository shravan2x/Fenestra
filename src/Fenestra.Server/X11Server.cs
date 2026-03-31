using Fenestra.NativeHost.Abstractions;
using Fenestra.Transport;

namespace Fenestra.Server;

public sealed class X11Server
{
    private readonly IX11TransportListener _listener;
    private readonly INativeWindowHost _windowHost;
    private readonly X11ServerHandshakeConfiguration _handshakeConfiguration;
    private readonly X11DisplayState _displayState;

    public X11Server(
        IX11TransportListener listener,
        INativeWindowHost windowHost)
        : this(listener, windowHost, X11ServerHandshakeConfiguration.CreateDefault())
    {
    }

    public X11Server(
        IX11TransportListener listener,
        INativeWindowHost windowHost,
        X11ServerHandshakeConfiguration handshakeConfiguration)
    {
        _listener = listener;
        _windowHost = windowHost;
        _handshakeConfiguration = handshakeConfiguration;
        _displayState = X11DisplayState.CreateDefault();
    }

    public async Task StartAsync(X11ServerOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Starting Fenestra display :{options.DisplayNumber} on port {options.Port}.");

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

        Console.WriteLine("Waiting for X11 clients.");

        await _listener.RunAsync(
            (connection, connectionCancellationToken) =>
            {
                var session = new X11ClientSession(_displayState, _handshakeConfiguration);
                return session.HandleAsync(connection, connectionCancellationToken);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
