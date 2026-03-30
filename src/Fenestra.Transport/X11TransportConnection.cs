using System.Net;
using System.Net.Sockets;

namespace Fenestra.Transport;

public sealed class X11TransportConnection : IAsyncDisposable, IDisposable
{
    private readonly TcpClient _client;

    public X11TransportConnection(TcpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Stream = client.GetStream();
    }

    public Stream Stream { get; }

    public EndPoint? LocalEndpoint => _client.Client.LocalEndPoint;

    public EndPoint? RemoteEndpoint => _client.Client.RemoteEndPoint;

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
