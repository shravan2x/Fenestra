using Fenestra.NativeHost.Abstractions;
using Fenestra.Transport;

namespace Fenestra.Server;

public sealed class X11Server
{
    private readonly IX11TransportListener _listener;
    private readonly X11ServerHandshakeConfiguration _handshakeConfiguration;
    private readonly X11DisplayState _displayState;
    private readonly NativeWindowCoordinator _nativeWindowCoordinator;

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
        _handshakeConfiguration = handshakeConfiguration;
        _displayState = X11DisplayState.CreateDefault();
        _nativeWindowCoordinator = new NativeWindowCoordinator(
            windowHost ?? throw new ArgumentNullException(nameof(windowHost)));
    }

    public async Task StartAsync(X11ServerOptions options, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Starting Fenestra display :{options.DisplayNumber} on port {options.Port}.");

        if (options.EnableNativeWindows)
        {
            await _nativeWindowCoordinator.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _nativeWindowCoordinator.RegisterInputSinkAsync(
                HandleNativeInputAsync,
                cancellationToken).ConfigureAwait(false);
            await _nativeWindowCoordinator.ShowOrCreateAsync(
                windowId: _displayState.RootWindowId,
                new WindowDescriptor(
                    WindowId: _displayState.RootWindowId,
                    Title: "Fenestra bootstrap host",
                    X: 100,
                    Y: 100,
                    Width: _displayState.ScreenWidthInPixels,
                    Height: _displayState.ScreenHeightInPixels,
                    IsVisible: true),
                cancellationToken).ConfigureAwait(false);
        }

        Console.WriteLine("Waiting for X11 clients.");

        await _listener.RunAsync(
            (connection, connectionCancellationToken) =>
            {
                var session = new X11ClientSession(
                    _displayState,
                    _handshakeConfiguration,
                    PresentRootAsync);
                return session.HandleAsync(connection, connectionCancellationToken);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private Task PresentRootAsync(CancellationToken cancellationToken)
    {
        return _nativeWindowCoordinator.PresentWindowAsync(
            _displayState.RootWindowId,
            _displayState.RootFramebuffer.Snapshot(),
            cancellationToken);
    }

    private Task HandleNativeInputAsync(NativeInputEvent inputEvent, CancellationToken cancellationToken)
    {
        _displayState.EnqueueInputEvent(inputEvent);
        return Task.CompletedTask;
    }
}
