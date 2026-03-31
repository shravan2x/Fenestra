namespace Fenestra.NativeHost.Abstractions;

public interface INativeWindowHost
{
    string PlatformName { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<NativeWindowReference> CreateWindowAsync(
        WindowDescriptor descriptor,
        CancellationToken cancellationToken = default);

    Task UpdateWindowAsync(
        NativeWindowReference handle,
        WindowDescriptor descriptor,
        CancellationToken cancellationToken = default);

    Task ShowWindowAsync(
        NativeWindowReference handle,
        CancellationToken cancellationToken = default);

    Task HideWindowAsync(
        NativeWindowReference handle,
        CancellationToken cancellationToken = default);

    Task DestroyWindowAsync(
        NativeWindowReference handle,
        CancellationToken cancellationToken = default);
}
