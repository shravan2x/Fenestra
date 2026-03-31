using Fenestra.NativeHost.Abstractions;

namespace Fenestra.Server;

public sealed class NativeWindowCoordinator
{
    private readonly INativeWindowHost _nativeWindowHost;
    private readonly Dictionary<uint, NativeWindowReference> _nativeWindowsById = new();
    private Func<NativeInputEvent, CancellationToken, Task>? _inputSink;

    public NativeWindowCoordinator(INativeWindowHost nativeWindowHost)
    {
        _nativeWindowHost = nativeWindowHost ?? throw new ArgumentNullException(nameof(nativeWindowHost));
    }

    public IReadOnlyDictionary<uint, NativeWindowReference> NativeWindowsById => _nativeWindowsById;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _nativeWindowHost.InitializeAsync(cancellationToken);
    }

    public Task RegisterInputSinkAsync(
        Func<NativeInputEvent, CancellationToken, Task> inputSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputSink);

        _inputSink = inputSink;
        return _nativeWindowHost.RegisterInputSinkAsync(DispatchInputAsync, cancellationToken);
    }

    public async Task<NativeWindowReference> CreateOrUpdateTopLevelWindowAsync(
        uint windowId,
        WindowDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (_nativeWindowsById.TryGetValue(windowId, out var existingWindow))
        {
            await _nativeWindowHost.UpdateWindowAsync(existingWindow, descriptor, cancellationToken).ConfigureAwait(false);
            return existingWindow;
        }

        var nativeWindow = await _nativeWindowHost.CreateWindowAsync(descriptor, cancellationToken).ConfigureAwait(false);
        _nativeWindowsById[windowId] = nativeWindow;
        return nativeWindow;
    }

    public async Task<NativeWindowReference> ShowOrCreateAsync(
        uint windowId,
        WindowDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        var nativeWindow = await CreateOrUpdateTopLevelWindowAsync(windowId, descriptor, cancellationToken).ConfigureAwait(false);

        if (descriptor.IsVisible)
        {
            await _nativeWindowHost.ShowWindowAsync(nativeWindow, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _nativeWindowHost.HideWindowAsync(nativeWindow, cancellationToken).ConfigureAwait(false);
        }

        return nativeWindow;
    }

    public async Task DestroyTopLevelWindowAsync(uint windowId, CancellationToken cancellationToken = default)
    {
        if (!_nativeWindowsById.Remove(windowId, out var nativeWindow))
        {
            return;
        }

        await _nativeWindowHost.DestroyWindowAsync(nativeWindow, cancellationToken).ConfigureAwait(false);
    }

    public async Task PresentWindowAsync(
        uint windowId,
        FramebufferSnapshot framebuffer,
        CancellationToken cancellationToken = default)
    {
        if (!_nativeWindowsById.TryGetValue(windowId, out var nativeWindow))
        {
            return;
        }

        await _nativeWindowHost.PresentFrameAsync(
            nativeWindow,
            framebuffer,
            cancellationToken).ConfigureAwait(false);
    }

    private Task DispatchInputAsync(NativeInputEvent inputEvent, CancellationToken cancellationToken)
    {
        if (_inputSink is null)
        {
            return Task.CompletedTask;
        }

        return _inputSink(inputEvent, cancellationToken);
    }
}
