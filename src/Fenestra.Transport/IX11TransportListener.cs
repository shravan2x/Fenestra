namespace Fenestra.Transport;

public interface IX11TransportListener
{
    string DisplayName { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
}
