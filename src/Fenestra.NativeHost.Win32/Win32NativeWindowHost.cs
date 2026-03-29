using Fenestra.NativeHost.Abstractions;

namespace Fenestra.NativeHost.Win32;

public sealed class Win32NativeWindowHost : INativeWindowHost
{
    public string PlatformName => "Win32";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Win32 native window host initialized in stub mode.");
        return Task.CompletedTask;
    }

    public Task ShowWindowAsync(WindowDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // This will eventually create HWND-backed windows with native chrome on Windows.
        Console.WriteLine(
            $"Stub native window: '{descriptor.Title}' at ({descriptor.X}, {descriptor.Y}) {descriptor.Width}x{descriptor.Height}.");
        return Task.CompletedTask;
    }
}
