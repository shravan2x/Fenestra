using System.Net;
using System.Net.Sockets;

namespace Fenestra.Transport;

public sealed class TcpDisplayEndpoint : IX11TransportListener
{
    public TcpDisplayEndpoint(string host, int port, int displayNumber)
    {
        Host = host;
        Port = port;
        DisplayNumber = displayNumber;
    }

    public string Host { get; }

    public int Port { get; }

    public int DisplayNumber { get; }

    public int BoundPort { get; private set; }

    public string DisplayName => $":{DisplayNumber}";

    public async Task RunAsync(
        Func<X11TransportConnection, CancellationToken, Task> connectionHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionHandler);

        var listener = new TcpListener(ResolveAddress(Host), Port);
        var connectionTasks = new List<Task>();

        listener.Start();
        BoundPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        Console.WriteLine($"Transport listener active on {Host}:{BoundPort} ({DisplayName}).");

        using var cancellationRegistration = cancellationToken.Register(static state =>
        {
            ((TcpListener)state!).Stop();
        }, listener);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                client.NoDelay = true;

                connectionTasks.Add(ProcessConnectionAsync(
                    new X11TransportConnection(client),
                    connectionHandler,
                    cancellationToken));

                connectionTasks.RemoveAll(static task => task.IsCompleted);
            }
        }
        finally
        {
            listener.Stop();

            if (connectionTasks.Count > 0)
            {
                await Task.WhenAll(connectionTasks).ConfigureAwait(false);
            }
        }
    }

    public override string ToString()
    {
        var port = BoundPort == 0 ? Port : BoundPort;
        return $"{Host}:{port} (display {DisplayName})";
    }

    private static async Task ProcessConnectionAsync(
        X11TransportConnection connection,
        Func<X11TransportConnection, CancellationToken, Task> connectionHandler,
        CancellationToken cancellationToken)
    {
        await using var ownedConnection = connection;

        try
        {
            await connectionHandler(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException ioException)
        {
            Console.WriteLine(
                $"Transport connection {connection.RemoteEndpoint} closed with I/O error: {ioException.Message}");
        }
        catch (SocketException socketException)
        {
            Console.WriteLine(
                $"Transport connection {connection.RemoteEndpoint} closed with socket error: {socketException.Message}");
        }
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return address;
        }

        return host.ToLowerInvariant() switch
        {
            "localhost" => IPAddress.Loopback,
            _ => throw new ArgumentException($"Unsupported listen address '{host}'.", nameof(host))
        };
    }
}
