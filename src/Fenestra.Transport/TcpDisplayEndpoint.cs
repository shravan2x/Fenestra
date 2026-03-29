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

    public string DisplayName => $":{DisplayNumber}";

    public TcpDisplayEndpoint Endpoint => this;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Transport listener configured for {Host}:{Port} ({DisplayName}).");
        return Task.CompletedTask;
    }

    public override string ToString()
    {
        return $"{Host}:{Port} (display {DisplayName})";
    }
}
