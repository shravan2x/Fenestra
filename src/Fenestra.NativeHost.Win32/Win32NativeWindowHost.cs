using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Fenestra.NativeHost.Abstractions;

namespace Fenestra.NativeHost.Win32;

public sealed class Win32NativeWindowHost : INativeWindowHost
{
    private readonly ConcurrentDictionary<uint, NativeWindowReference> _windows = new();
    private readonly object _windowClassLock = new();
    private ushort _windowClassAtom;
    private string? _windowClassName;

    public string PlatformName => "Win32";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Win32 native window host initialized in non-Windows stub mode.");
            return Task.CompletedTask;
        }

        EnsureWindowClassRegistered();
        Console.WriteLine("Win32 native window host initialized.");
        return Task.CompletedTask;
    }

    public Task<NativeWindowReference> CreateWindowAsync(
        WindowDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!OperatingSystem.IsWindows())
        {
            var stubReference = new NativeWindowReference(
                WindowId: descriptor.WindowId,
                HostHandle: 0,
                PlatformName: PlatformName,
                IsStub: true);
            _windows[descriptor.WindowId] = stubReference;

            Console.WriteLine(
                $"Stub native window created: '{descriptor.Title}' [{descriptor.WindowId}] at ({descriptor.X}, {descriptor.Y}) {descriptor.Width}x{descriptor.Height}.");
            return Task.FromResult(stubReference);
        }

        EnsureWindowClassRegistered();
        var style = NativeMethods.WS_OVERLAPPEDWINDOW | (descriptor.IsVisible ? NativeMethods.WS_VISIBLE : 0u);
        var windowHandle = NativeMethods.CreateWindowExW(
            0,
            _windowClassName!,
            descriptor.Title,
            style,
            descriptor.X,
            descriptor.Y,
            descriptor.Width,
            descriptor.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create a Win32 native window.");
        }

        var reference = new NativeWindowReference(
            WindowId: descriptor.WindowId,
            HostHandle: windowHandle,
            PlatformName: PlatformName,
            IsStub: false);
        _windows[descriptor.WindowId] = reference;
        return Task.FromResult(reference);
    }

    public Task UpdateWindowAsync(
        NativeWindowReference handle,
        WindowDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!OperatingSystem.IsWindows() || handle.IsStub)
        {
            Console.WriteLine(
                $"Stub native window updated: '{descriptor.Title}' [{descriptor.WindowId}] at ({descriptor.X}, {descriptor.Y}) {descriptor.Width}x{descriptor.Height}.");
            return Task.CompletedTask;
        }

        NativeMethods.SetWindowTextW((nint)handle.HostHandle, descriptor.Title);
        NativeMethods.SetWindowPos(
            (nint)handle.HostHandle,
            IntPtr.Zero,
            descriptor.X,
            descriptor.Y,
            descriptor.Width,
            descriptor.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        return Task.CompletedTask;
    }

    public Task ShowWindowAsync(
        NativeWindowReference handle,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || handle.IsStub)
        {
            Console.WriteLine($"Stub native window shown: [{handle.WindowId}].");
            return Task.CompletedTask;
        }

        NativeMethods.ShowWindow((nint)handle.HostHandle, NativeMethods.SW_SHOW);
        return Task.CompletedTask;
    }

    public Task HideWindowAsync(
        NativeWindowReference handle,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || handle.IsStub)
        {
            Console.WriteLine($"Stub native window hidden: [{handle.WindowId}].");
            return Task.CompletedTask;
        }

        NativeMethods.ShowWindow((nint)handle.HostHandle, NativeMethods.SW_HIDE);
        return Task.CompletedTask;
    }

    public Task DestroyWindowAsync(
        NativeWindowReference handle,
        CancellationToken cancellationToken = default)
    {
        _windows.TryRemove(handle.WindowId, out _);

        if (!OperatingSystem.IsWindows() || handle.IsStub)
        {
            Console.WriteLine($"Stub native window destroyed: [{handle.WindowId}].");
            return Task.CompletedTask;
        }

        NativeMethods.DestroyWindow((nint)handle.HostHandle);
        return Task.CompletedTask;
    }

    private void EnsureWindowClassRegistered()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (_windowClassAtom != 0)
        {
            return;
        }

        lock (_windowClassLock)
        {
            if (_windowClassAtom != 0)
            {
                return;
            }

            _windowClassName = $"FenestraWindowHost_{Environment.ProcessId}";
            var windowClass = new NativeMethods.WNDCLASSW
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(NativeMethods.WindowProcDelegateInstance),
                lpszClassName = _windowClassName
            };

            _windowClassAtom = NativeMethods.RegisterClassW(ref windowClass);
            if (_windowClassAtom == 0)
            {
                throw new InvalidOperationException("Failed to register the Win32 native window class.");
            }
        }
    }

    private static class NativeMethods
    {
        public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        public const uint WS_VISIBLE = 0x10000000;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        public delegate nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

        public static readonly WindowProc WindowProcDelegateInstance = WindowProcedure;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSW
        {
            public uint style;
            public nint lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public string? lpszMenuName;
            public string? lpszClassName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint CreateWindowExW(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int X,
            int Y,
            int nWidth,
            int nHeight,
            nint hWndParent,
            nint hMenu,
            nint hInstance,
            nint lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool DestroyWindow(nint hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetWindowTextW(nint hWnd, string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            nint hWnd,
            nint hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

        private static nint WindowProcedure(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
}
