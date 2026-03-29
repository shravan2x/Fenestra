namespace Fenestra.NativeHost.Abstractions;

public interface INativeWindowHost
{
    string PlatformName { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task ShowWindowAsync(WindowDescriptor descriptor, CancellationToken cancellationToken = default);
}
