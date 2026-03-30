namespace Fenestra.Transport;

public interface IX11TransportListener
{
    string DisplayName { get; }

    Task RunAsync(
        Func<X11TransportConnection, CancellationToken, Task> connectionHandler,
        CancellationToken cancellationToken = default);
}
