using Fenestra.NativeHost.Win32;
using Fenestra.Server;
using Fenestra.Transport;

var options = new X11ServerOptions(
    DisplayNumber: 0,
    ListenAddress: "127.0.0.1",
    Port: 6000);

var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
var nativeWindowHost = new Win32NativeWindowHost();
var server = new X11Server(transport, nativeWindowHost);

Console.WriteLine("Fenestra bootstrap starting.");
Console.WriteLine($"Display :{options.DisplayNumber} on {options.ListenAddress}:{options.Port}");
Console.WriteLine("Native host: Win32");
Console.WriteLine();
Console.WriteLine("This starter host does not yet accept X11 clients.");
Console.WriteLine("See docs/architecture.md and docs/roadmap.md for the implementation plan.");

await server.StartAsync(options);
