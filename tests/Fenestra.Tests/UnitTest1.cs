using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Fenestra.NativeHost.Abstractions;
using Fenestra.Protocol.X11.Encoding;
using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Parsing;
using Fenestra.Protocol.X11.Setup;
using Fenestra.Server;
using Fenestra.Transport;

namespace Fenestra.Tests;

public sealed class X11StateAndHandshakeTests
{
    private static readonly X11ServerHandshakeConfiguration DefaultHandshakeConfiguration =
        X11ServerHandshakeConfiguration.CreateDefault();
    private static readonly X11DisplayState DefaultDisplayState =
        X11DisplayState.CreateDefault();

    [Fact]
    public void ParseSetupRequest_ReadsLittleEndianAuthorizationFields()
    {
        var requestBytes = BuildSetupRequest(
            ByteOrder.LittleEndian,
            protocolMajorVersion: 11,
            protocolMinorVersion: 0,
            authorizationName: "MIT-MAGIC-COOKIE-1",
            authorizationData: [0x01, 0x02, 0x03, 0x04]);

        var parsed = X11HandshakeParser.TryParseSetupRequest(requestBytes, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(ByteOrder.LittleEndian, request!.ByteOrder);
        Assert.Equal((ushort)11, request.ProtocolMajorVersion);
        Assert.Equal((ushort)0, request.ProtocolMinorVersion);
        Assert.Equal("MIT-MAGIC-COOKIE-1", request.AuthorizationProtocolName);
        Assert.Equal([0x01, 0x02, 0x03, 0x04], request.AuthorizationProtocolData);
    }

    [Fact]
    public void ParseSetupRequest_ReadsBigEndianAuthorizationFields()
    {
        var requestBytes = BuildSetupRequest(
            ByteOrder.BigEndian,
            protocolMajorVersion: 11,
            protocolMinorVersion: 0,
            authorizationName: "XDM-AUTHORIZATION-1",
            authorizationData: [0x10, 0x20, 0x30]);

        var parsed = X11HandshakeParser.TryParseSetupRequest(requestBytes, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(ByteOrder.BigEndian, request!.ByteOrder);
        Assert.Equal("XDM-AUTHORIZATION-1", request.AuthorizationProtocolName);
        Assert.Equal([0x10, 0x20, 0x30], request.AuthorizationProtocolData);
    }

    [Fact]
    public void ParseSetupRequest_RejectsMalformedPacketLength()
    {
        var malformed = BuildSetupRequest(
            ByteOrder.LittleEndian,
            protocolMajorVersion: 11,
            protocolMinorVersion: 0,
            authorizationName: "MIT",
            authorizationData: [0x01, 0x02])[..^1];

        var parsed = X11HandshakeParser.TryParseSetupRequest(malformed, out var request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void EncodeSuccess_WritesExpectedLittleEndianHeader()
    {
        var clientState = DefaultDisplayState.CreateClientState(ByteOrder.LittleEndian);
        var response = DefaultHandshakeConfiguration.CreateSuccessResponse(
            ByteOrder.LittleEndian,
            DefaultDisplayState,
            clientState);

        var bytes = X11SetupResponseEncoder.EncodeSuccess(response);

        Assert.Equal(1, bytes[0]);
        Assert.Equal(0, bytes[1]);
        Assert.Equal((ushort)11, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)));
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(24, 2)));
        Assert.Equal((ushort)65535, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2)));
        Assert.Equal((byte)1, bytes[28]);
        Assert.Equal((byte)1, bytes[29]);
        Assert.Equal((byte)'l', bytes[30]);
        Assert.Equal("Fenestra", Encoding.ASCII.GetString(bytes.AsSpan(40, 8)));
    }

    [Fact]
    public void DisplayState_ExposesFixedRootResources()
    {
        var displayState = DefaultDisplayState;

        Assert.Equal(0x0020_0000u, displayState.ResourceIdBase);
        Assert.Equal(0x001F_FFFFu, displayState.ResourceIdMask);
        Assert.Equal(1u, displayState.RootWindowId);
        Assert.Equal(1u, displayState.DefaultColormapId);
        Assert.Equal(33u, displayState.RootVisualId);
        Assert.Equal((ushort)1024, displayState.ScreenWidthInPixels);
        Assert.Equal((ushort)768, displayState.ScreenHeightInPixels);
    }

    [Fact]
    public void ClientState_AllocatesSequentialXidsWithinMask()
    {
        var displayState = DefaultDisplayState;
        var clientState = displayState.CreateClientState(ByteOrder.LittleEndian);

        var first = clientState.AllocateXid();
        var second = clientState.AllocateXid();
        var third = clientState.AllocateXid();

        Assert.Equal(displayState.ResourceIdBase | 1u, first);
        Assert.Equal(displayState.ResourceIdBase | 2u, second);
        Assert.Equal(displayState.ResourceIdBase | 3u, third);
        Assert.All(new[] { first, second, third }, xid =>
        {
            Assert.Equal(displayState.ResourceIdBase, xid & ~displayState.ResourceIdMask);
        });
    }

    [Fact]
    public void HandshakeConfiguration_UsesDisplayStateValues()
    {
        var configuration = new X11ServerHandshakeConfiguration(
            releaseNumber: 7,
            motionBufferSize: 4,
            vendor: "PhaseTwo",
            maximumRequestLength: 4096);
        var displayState = new X11DisplayState(
            resourceIdBase: 0x0040_0000,
            resourceIdMask: 0x000F_FFFF,
            screenWidthInPixels: 1440,
            screenHeightInPixels: 900,
            screenWidthInMillimeters: 310,
            screenHeightInMillimeters: 190,
            rootWindowId: 99,
            defaultColormapId: 77,
            rootVisualId: 123,
            whitePixel: 0x00FF_FFFF,
            blackPixel: 0x0000_0000,
            rootDepth: 24,
            pixmapFormats:
            [
                new X11PixmapFormatDefinition(Depth: 24, BitsPerPixel: 32, ScanlinePad: 32)
            ],
            allowedDepths:
            [
                new X11DepthDefinition(
                    Depth: 24,
                    Visuals:
                    [
                        new X11VisualDefinition(
                            VisualId: 123,
                            VisualClass: 4,
                            BitsPerRgbValue: 8,
                            ColormapEntries: 256,
                            RedMask: 0x00FF_0000,
                            GreenMask: 0x0000_FF00,
                            BlueMask: 0x0000_00FF)
                    ])
            ],
            atomTable: X11AtomTable.CreateDefault());
        var clientState = displayState.CreateClientState(ByteOrder.BigEndian);
        var response = configuration.CreateSuccessResponse(ByteOrder.BigEndian, displayState, clientState);

        Assert.Equal("PhaseTwo", response.Vendor);
        Assert.Equal(7u, response.ReleaseNumber);
        Assert.Equal(0x0040_0000u, response.ResourceIdBase);
        Assert.Equal(0x000F_FFFFu, response.ResourceIdMask);
        Assert.Equal(99u, response.Screens[0].RootWindowId);
        Assert.Equal(77u, response.Screens[0].DefaultColormapId);
        Assert.Equal(123u, response.Screens[0].RootVisualId);
    }

    [Fact]
    public async Task NativeWindowCoordinator_CreatesUpdatesAndDestroysWindow()
    {
        var host = new RecordingNativeWindowHost();
        var coordinator = new NativeWindowCoordinator(host);

        await coordinator.InitializeAsync();
        var created = await coordinator.CreateOrUpdateTopLevelWindowAsync(
            10,
            new WindowDescriptor(
                WindowId: 10,
                Title: "Initial",
                X: 10,
                Y: 20,
                Width: 640,
                Height: 480,
                IsVisible: false));

        await coordinator.ShowOrCreateAsync(
            10,
            new WindowDescriptor(
                WindowId: 10,
                Title: "Updated",
                X: 30,
                Y: 40,
                Width: 800,
                Height: 600,
                IsVisible: true));

        await coordinator.DestroyTopLevelWindowAsync(10);

        Assert.Equal(1, host.InitializeCalls);
        Assert.Single(host.CreatedWindows);
        Assert.Single(host.UpdatedWindows);
        Assert.Single(host.ShownWindows);
        Assert.Single(host.DestroyedWindows);
        Assert.Equal("Initial", host.CreatedWindows[0].Title);
        Assert.Equal("Updated", host.UpdatedWindows[0].Title);
        Assert.Equal((uint)10, created.WindowId);
        Assert.Empty(coordinator.NativeWindowsById);
    }

    [Fact]
    public async Task ServerStart_CreatesBootstrapNativeWindowThroughCoordinator()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var host = new RecordingNativeWindowHost();
        var transport = new BlockingTransportListener();
        var server = new X11Server(transport, host, X11ServerHandshakeConfiguration.CreateDefault());
        var options = new X11ServerOptions(
            DisplayNumber: 0,
            ListenAddress: "127.0.0.1",
            Port: 6000,
            EnableNativeWindows: true);

        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);
        await transport.WaitForRunAsync();
        cancellationTokenSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await serverTask);

        Assert.Equal(1, host.InitializeCalls);
        Assert.Single(host.CreatedWindows);
        Assert.Single(host.ShownWindows);
        Assert.Equal((uint)1, host.CreatedWindows[0].WindowId);
        Assert.Equal("Fenestra bootstrap host", host.CreatedWindows[0].Title);
        Assert.Equal(1024, host.CreatedWindows[0].Width);
        Assert.Equal(768, host.CreatedWindows[0].Height);
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public async Task ServerHandshake_CompletesOverTcp(ByteOrder byteOrder)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var options = new X11ServerOptions(
            DisplayNumber: 9,
            ListenAddress: "127.0.0.1",
            Port: 0,
            EnableNativeWindows: false);
        var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
        var server = new X11Server(
            transport,
            new FakeNativeWindowHost(),
            X11ServerHandshakeConfiguration.CreateDefault());
        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);

        await WaitForBoundPortAsync(transport);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.BoundPort);

        await using var stream = client.GetStream();
        var requestBytes = BuildSetupRequest(
            byteOrder,
            protocolMajorVersion: 11,
            protocolMinorVersion: 0,
            authorizationName: string.Empty,
            authorizationData: []);

        await stream.WriteAsync(requestBytes);
        await stream.FlushAsync();

        var responseBuffer = await ReadSetupResponseAsync(stream, byteOrder);

        Assert.Equal(1, responseBuffer[0]);
        Assert.Equal((ushort)11, ReadUInt16(responseBuffer.AsSpan(2, 2), byteOrder));
        Assert.Equal((ushort)0, ReadUInt16(responseBuffer.AsSpan(4, 2), byteOrder));
        Assert.Equal((ushort)8, ReadUInt16(responseBuffer.AsSpan(24, 2), byteOrder));
        Assert.Equal((ushort)65535, ReadUInt16(responseBuffer.AsSpan(26, 2), byteOrder));
        Assert.Equal((byte)1, responseBuffer[28]);
        Assert.Equal((byte)1, responseBuffer[29]);
        Assert.Equal((byte)byteOrder, responseBuffer[30]);
        Assert.Equal((byte)byteOrder, responseBuffer[31]);
        Assert.Equal("Fenestra", Encoding.ASCII.GetString(responseBuffer.AsSpan(40, 8)));
        var vendorLength = ReadUInt16(responseBuffer.AsSpan(24, 2), byteOrder);
        var screenOffset = 40 + PadToFourBytes(vendorLength) + (responseBuffer[29] * 8);
        Assert.Equal(1u, ReadUInt32(responseBuffer.AsSpan(screenOffset, 4), byteOrder));
        Assert.Equal(1u, ReadUInt32(responseBuffer.AsSpan(screenOffset + 4, 4), byteOrder));
        Assert.Equal(33u, ReadUInt32(responseBuffer.AsSpan(screenOffset + 32, 4), byteOrder));

        cancellationTokenSource.Cancel();
        await serverTask;
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public async Task RequestLoop_GetGeometry_ReturnsRootWindowGeometry(ByteOrder byteOrder)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var options = new X11ServerOptions(
            DisplayNumber: 10,
            ListenAddress: "127.0.0.1",
            Port: 0,
            EnableNativeWindows: false);
        var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
        var server = new X11Server(
            transport,
            new FakeNativeWindowHost(),
            X11ServerHandshakeConfiguration.CreateDefault());
        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);

        await WaitForBoundPortAsync(transport);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.BoundPort);

        await using var stream = client.GetStream();
        await CompleteHandshakeAsync(stream, byteOrder);

        var request = BuildSingleUInt32Request(byteOrder, majorOpcode: 14, data: 0, value: 1);
        await stream.WriteAsync(request);
        await stream.FlushAsync();

        var reply = await ReadReplyOrErrorAsync(stream, byteOrder);

        Assert.Equal(1, reply[0]);
        Assert.Equal(24, reply[1]);
        Assert.Equal((ushort)1, ReadUInt16(reply.AsSpan(2, 2), byteOrder));
        Assert.Equal(1u, ReadUInt32(reply.AsSpan(8, 4), byteOrder));
        Assert.Equal((ushort)1024, ReadUInt16(reply.AsSpan(16, 2), byteOrder));
        Assert.Equal((ushort)768, ReadUInt16(reply.AsSpan(18, 2), byteOrder));

        cancellationTokenSource.Cancel();
        await serverTask;
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public async Task RequestLoop_QueryTree_ReturnsRootWithoutChildren(ByteOrder byteOrder)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var options = new X11ServerOptions(
            DisplayNumber: 11,
            ListenAddress: "127.0.0.1",
            Port: 0,
            EnableNativeWindows: false);
        var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
        var server = new X11Server(
            transport,
            new FakeNativeWindowHost(),
            X11ServerHandshakeConfiguration.CreateDefault());
        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);

        await WaitForBoundPortAsync(transport);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.BoundPort);

        await using var stream = client.GetStream();
        await CompleteHandshakeAsync(stream, byteOrder);

        var request = BuildSingleUInt32Request(byteOrder, majorOpcode: 15, data: 0, value: 1);
        await stream.WriteAsync(request);
        await stream.FlushAsync();

        var reply = await ReadReplyOrErrorAsync(stream, byteOrder);

        Assert.Equal(1, reply[0]);
        Assert.Equal((ushort)1, ReadUInt16(reply.AsSpan(2, 2), byteOrder));
        Assert.Equal(1u, ReadUInt32(reply.AsSpan(8, 4), byteOrder));
        Assert.Equal(0u, ReadUInt32(reply.AsSpan(12, 4), byteOrder));
        Assert.Equal((ushort)0, ReadUInt16(reply.AsSpan(16, 2), byteOrder));

        cancellationTokenSource.Cancel();
        await serverTask;
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public async Task RequestLoop_InternAtom_CreatesAndFindsDynamicAtom(ByteOrder byteOrder)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var options = new X11ServerOptions(
            DisplayNumber: 12,
            ListenAddress: "127.0.0.1",
            Port: 0,
            EnableNativeWindows: false);
        var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
        var server = new X11Server(
            transport,
            new FakeNativeWindowHost(),
            X11ServerHandshakeConfiguration.CreateDefault());
        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);

        await WaitForBoundPortAsync(transport);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.BoundPort);

        await using var stream = client.GetStream();
        await CompleteHandshakeAsync(stream, byteOrder);

        var createRequest = BuildInternAtomRequest(byteOrder, onlyIfExists: false, atomName: "FENESTRA_TEST_ATOM");
        await stream.WriteAsync(createRequest);
        await stream.FlushAsync();
        var createReply = await ReadReplyOrErrorAsync(stream, byteOrder);
        var atomId = ReadUInt32(createReply.AsSpan(8, 4), byteOrder);

        Assert.Equal(1, createReply[0]);
        Assert.True(atomId > 33);

        var lookupRequest = BuildInternAtomRequest(byteOrder, onlyIfExists: true, atomName: "FENESTRA_TEST_ATOM");
        await stream.WriteAsync(lookupRequest);
        await stream.FlushAsync();
        var lookupReply = await ReadReplyOrErrorAsync(stream, byteOrder);

        Assert.Equal(1, lookupReply[0]);
        Assert.Equal((ushort)2, ReadUInt16(lookupReply.AsSpan(2, 2), byteOrder));
        Assert.Equal(atomId, ReadUInt32(lookupReply.AsSpan(8, 4), byteOrder));

        cancellationTokenSource.Cancel();
        await serverTask;
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public async Task RequestLoop_UnsupportedOpcode_ReturnsRequestError(ByteOrder byteOrder)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var options = new X11ServerOptions(
            DisplayNumber: 13,
            ListenAddress: "127.0.0.1",
            Port: 0,
            EnableNativeWindows: false);
        var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
        var server = new X11Server(
            transport,
            new FakeNativeWindowHost(),
            X11ServerHandshakeConfiguration.CreateDefault());
        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);

        await WaitForBoundPortAsync(transport);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.BoundPort);

        await using var stream = client.GetStream();
        await CompleteHandshakeAsync(stream, byteOrder);

        var request = BuildSingleUInt32Request(byteOrder, majorOpcode: 250, data: 0, value: 0);
        await stream.WriteAsync(request);
        await stream.FlushAsync();

        var error = await ReadReplyOrErrorAsync(stream, byteOrder);

        Assert.Equal(0, error[0]);
        Assert.Equal((byte)X11ErrorCode.Request, error[1]);
        Assert.Equal((ushort)1, ReadUInt16(error.AsSpan(2, 2), byteOrder));
        Assert.Equal(0u, ReadUInt32(error.AsSpan(4, 4), byteOrder));
        Assert.Equal((byte)250, error[10]);

        cancellationTokenSource.Cancel();
        await serverTask;
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public async Task RequestLoop_CreatePixmapPutImageGetImage_RoundTripsPixels(ByteOrder byteOrder)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var options = new X11ServerOptions(
            DisplayNumber: 14,
            ListenAddress: "127.0.0.1",
            Port: 0,
            EnableNativeWindows: false);
        var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
        var server = new X11Server(
            transport,
            new FakeNativeWindowHost(),
            X11ServerHandshakeConfiguration.CreateDefault());
        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);

        await WaitForBoundPortAsync(transport);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.BoundPort);

        await using var stream = client.GetStream();
        await CompleteHandshakeAsync(stream, byteOrder);

        await stream.WriteAsync(BuildCreatePixmapRequest(byteOrder, pixmapId: 0x0020_0001, drawableId: 1, width: 2, height: 2, depth: 24));
        await stream.FlushAsync();

        await stream.WriteAsync(BuildCreateGraphicsContextRequest(byteOrder, graphicsContextId: 0x0020_0002, drawableId: 0x0020_0001));
        await stream.FlushAsync();

        var imageBytes = new byte[]
        {
            0x10, 0x20, 0x30, 0x00,
            0x40, 0x50, 0x60, 0x00,
            0x70, 0x80, 0x90, 0x00,
            0xA0, 0xB0, 0xC0, 0x00
        };

        await stream.WriteAsync(BuildPutImageRequest(
            byteOrder,
            drawableId: 0x0020_0001,
            graphicsContextId: 0x0020_0002,
            width: 2,
            height: 2,
            dstX: 0,
            dstY: 0,
            depth: 24,
            imageBytes: imageBytes));
        await stream.FlushAsync();

        await stream.WriteAsync(BuildGetImageRequest(
            byteOrder,
            drawableId: 0x0020_0001,
            x: 0,
            y: 0,
            width: 2,
            height: 2));
        await stream.FlushAsync();

        var reply = await ReadReplyOrErrorAsync(stream, byteOrder);

        Assert.Equal(1, reply[0]);
        Assert.Equal(24, reply[1]);
        Assert.Equal((ushort)4, ReadUInt16(reply.AsSpan(2, 2), byteOrder));
        Assert.Equal(33u, ReadUInt32(reply.AsSpan(8, 4), byteOrder));
        Assert.Equal(imageBytes, reply.AsSpan(32).ToArray());

        cancellationTokenSource.Cancel();
        await serverTask;
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public async Task RequestLoop_PutImageToRoot_PresentsRootFramebuffer(ByteOrder byteOrder)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var host = new RecordingNativeWindowHost();
        var options = new X11ServerOptions(
            DisplayNumber: 15,
            ListenAddress: "127.0.0.1",
            Port: 0,
            EnableNativeWindows: true);
        var transport = new TcpDisplayEndpoint(options.ListenAddress, options.Port, options.DisplayNumber);
        var server = new X11Server(
            transport,
            host,
            X11ServerHandshakeConfiguration.CreateDefault());
        var serverTask = server.StartAsync(options, cancellationTokenSource.Token);

        await WaitForBoundPortAsync(transport);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.BoundPort);

        await using var stream = client.GetStream();
        await CompleteHandshakeAsync(stream, byteOrder);

        await stream.WriteAsync(BuildCreateGraphicsContextRequest(byteOrder, graphicsContextId: 0x0020_0004, drawableId: 1));
        await stream.FlushAsync();

        var imageBytes = new byte[]
        {
            0x01, 0x02, 0x03, 0x00,
            0x04, 0x05, 0x06, 0x00,
            0x07, 0x08, 0x09, 0x00,
            0x0A, 0x0B, 0x0C, 0x00
        };

        await stream.WriteAsync(BuildPutImageRequest(
            byteOrder,
            drawableId: 1,
            graphicsContextId: 0x0020_0004,
            width: 2,
            height: 2,
            dstX: 0,
            dstY: 0,
            depth: 24,
            imageBytes: imageBytes));
        await stream.FlushAsync();

        await Task.Delay(50);

        Assert.NotEmpty(host.PresentedFrames);
        Assert.Equal((uint)1, host.PresentedFrames[^1].Handle.WindowId);
        Assert.Equal(1024, host.PresentedFrames[^1].Framebuffer.Width);
        Assert.Equal(768, host.PresentedFrames[^1].Framebuffer.Height);

        var presentedPixels = host.PresentedFrames[^1].Framebuffer.Pixels;
        var stride = host.PresentedFrames[^1].Framebuffer.Stride;
        Assert.Equal(imageBytes.AsSpan(0, 8).ToArray(), presentedPixels.Slice(0, 8).ToArray());
        Assert.Equal(imageBytes.AsSpan(8, 8).ToArray(), presentedPixels.Slice(stride, 8).ToArray());

        cancellationTokenSource.Cancel();
        await serverTask;
    }

    private static async Task WaitForBoundPortAsync(TcpDisplayEndpoint transport)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (transport.BoundPort != 0)
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Timed out waiting for TCP listener to bind.");
    }

    private static async Task<byte[]> ReadSetupResponseAsync(NetworkStream stream, ByteOrder byteOrder)
    {
        var header = new byte[8];
        await ReadExactLengthAsync(stream, header, header.Length);

        var additionalLengthWords = ReadUInt16(header.AsSpan(6, 2), byteOrder);
        var response = new byte[8 + (additionalLengthWords * 4)];
        header.CopyTo(response, 0);

        if (response.Length > header.Length)
        {
            await ReadExactLengthAsync(
                stream,
                response.AsMemory(header.Length, response.Length - header.Length));
        }

        return response;
    }

    private static async Task CompleteHandshakeAsync(NetworkStream stream, ByteOrder byteOrder)
    {
        var requestBytes = BuildSetupRequest(
            byteOrder,
            protocolMajorVersion: 11,
            protocolMinorVersion: 0,
            authorizationName: string.Empty,
            authorizationData: []);

        await stream.WriteAsync(requestBytes);
        await stream.FlushAsync();
        _ = await ReadSetupResponseAsync(stream, byteOrder);
    }

    private static async Task<byte[]> ReadReplyOrErrorAsync(NetworkStream stream, ByteOrder byteOrder)
    {
        var prefix = new byte[8];
        await ReadExactLengthAsync(stream, prefix, prefix.Length);

        if (prefix[0] == 0)
        {
            var error = new byte[32];
            prefix.CopyTo(error, 0);
            await ReadExactLengthAsync(stream, error.AsMemory(8, 24));
            return error;
        }

        var additionalLengthWords = ReadUInt32(prefix.AsSpan(4, 4), byteOrder);
        var reply = new byte[32 + (additionalLengthWords * 4)];
        prefix.CopyTo(reply, 0);
        await ReadExactLengthAsync(stream, reply.AsMemory(8, reply.Length - 8));

        return reply;
    }

    private static async Task ReadExactLengthAsync(NetworkStream stream, byte[] buffer, int expectedLength)
    {
        var totalRead = 0;

        while (totalRead < expectedLength)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, expectedLength - totalRead));
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Connection closed before the X11 setup response completed.");
            }

            totalRead += bytesRead;
        }
    }

    private static async Task ReadExactLengthAsync(NetworkStream stream, Memory<byte> buffer)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer[totalRead..]);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Connection closed before the X11 setup response completed.");
            }

            totalRead += bytesRead;
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        return byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer)
            : BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        return byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(buffer)
            : BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private static byte[] BuildSetupRequest(
        ByteOrder byteOrder,
        ushort protocolMajorVersion,
        ushort protocolMinorVersion,
        string authorizationName,
        byte[] authorizationData)
    {
        var authorizationNameBytes = System.Text.Encoding.ASCII.GetBytes(authorizationName);
        var paddedNameLength = PadToFourBytes(authorizationNameBytes.Length);
        var paddedDataLength = PadToFourBytes(authorizationData.Length);
        var buffer = new byte[12 + paddedNameLength + paddedDataLength];

        buffer[0] = (byte)byteOrder;
        WriteUInt16(buffer.AsSpan(2, 2), protocolMajorVersion, byteOrder);
        WriteUInt16(buffer.AsSpan(4, 2), protocolMinorVersion, byteOrder);
        WriteUInt16(buffer.AsSpan(6, 2), (ushort)authorizationNameBytes.Length, byteOrder);
        WriteUInt16(buffer.AsSpan(8, 2), (ushort)authorizationData.Length, byteOrder);

        authorizationNameBytes.CopyTo(buffer.AsSpan(12));
        authorizationData.CopyTo(buffer.AsSpan(12 + paddedNameLength));

        return buffer;
    }

    private static byte[] BuildSingleUInt32Request(
        ByteOrder byteOrder,
        byte majorOpcode,
        byte data,
        uint value)
    {
        var buffer = new byte[8];
        buffer[0] = majorOpcode;
        buffer[1] = data;
        WriteUInt16(buffer.AsSpan(2, 2), 2, byteOrder);
        WriteUInt32(buffer.AsSpan(4, 4), value, byteOrder);
        return buffer;
    }

    private static byte[] BuildInternAtomRequest(ByteOrder byteOrder, bool onlyIfExists, string atomName)
    {
        var atomNameBytes = Encoding.ASCII.GetBytes(atomName);
        var paddedNameLength = PadToFourBytes(atomNameBytes.Length);
        var buffer = new byte[8 + paddedNameLength];
        buffer[0] = 16;
        buffer[1] = onlyIfExists ? (byte)1 : (byte)0;
        WriteUInt16(buffer.AsSpan(2, 2), (ushort)(buffer.Length / 4), byteOrder);
        WriteUInt16(buffer.AsSpan(4, 2), (ushort)atomNameBytes.Length, byteOrder);
        atomNameBytes.CopyTo(buffer.AsSpan(8));
        return buffer;
    }

    private static byte[] BuildCreatePixmapRequest(
        ByteOrder byteOrder,
        uint pixmapId,
        uint drawableId,
        ushort width,
        ushort height,
        byte depth)
    {
        var buffer = new byte[16];
        buffer[0] = 53;
        buffer[1] = depth;
        WriteUInt16(buffer.AsSpan(2, 2), 4, byteOrder);
        WriteUInt32(buffer.AsSpan(4, 4), pixmapId, byteOrder);
        WriteUInt32(buffer.AsSpan(8, 4), drawableId, byteOrder);
        WriteUInt16(buffer.AsSpan(12, 2), width, byteOrder);
        WriteUInt16(buffer.AsSpan(14, 2), height, byteOrder);
        return buffer;
    }

    private static byte[] BuildCreateGraphicsContextRequest(
        ByteOrder byteOrder,
        uint graphicsContextId,
        uint drawableId)
    {
        var buffer = new byte[12];
        buffer[0] = 55;
        buffer[1] = 0;
        WriteUInt16(buffer.AsSpan(2, 2), 3, byteOrder);
        WriteUInt32(buffer.AsSpan(4, 4), graphicsContextId, byteOrder);
        WriteUInt32(buffer.AsSpan(8, 4), drawableId, byteOrder);
        return buffer;
    }

    private static byte[] BuildPutImageRequest(
        ByteOrder byteOrder,
        uint drawableId,
        uint graphicsContextId,
        ushort width,
        ushort height,
        short dstX,
        short dstY,
        byte depth,
        byte[] imageBytes)
    {
        var buffer = new byte[24 + imageBytes.Length];
        buffer[0] = 72;
        buffer[1] = 2;
        WriteUInt16(buffer.AsSpan(2, 2), (ushort)(buffer.Length / 4), byteOrder);
        WriteUInt32(buffer.AsSpan(4, 4), drawableId, byteOrder);
        WriteUInt32(buffer.AsSpan(8, 4), graphicsContextId, byteOrder);
        WriteUInt16(buffer.AsSpan(12, 2), width, byteOrder);
        WriteUInt16(buffer.AsSpan(14, 2), height, byteOrder);
        WriteUInt16(buffer.AsSpan(16, 2), unchecked((ushort)dstX), byteOrder);
        WriteUInt16(buffer.AsSpan(18, 2), unchecked((ushort)dstY), byteOrder);
        buffer[20] = 0;
        buffer[21] = depth;
        imageBytes.CopyTo(buffer.AsSpan(24));
        return buffer;
    }

    private static byte[] BuildGetImageRequest(
        ByteOrder byteOrder,
        uint drawableId,
        short x,
        short y,
        ushort width,
        ushort height)
    {
        var buffer = new byte[20];
        buffer[0] = 73;
        buffer[1] = 2;
        WriteUInt16(buffer.AsSpan(2, 2), 5, byteOrder);
        WriteUInt32(buffer.AsSpan(4, 4), drawableId, byteOrder);
        WriteUInt16(buffer.AsSpan(8, 2), unchecked((ushort)x), byteOrder);
        WriteUInt16(buffer.AsSpan(10, 2), unchecked((ushort)y), byteOrder);
        WriteUInt16(buffer.AsSpan(12, 2), width, byteOrder);
        WriteUInt16(buffer.AsSpan(14, 2), height, byteOrder);
        WriteUInt32(buffer.AsSpan(16, 4), uint.MaxValue, byteOrder);
        return buffer;
    }

    private static void WriteUInt16(Span<byte> destination, ushort value, ByteOrder byteOrder)
    {
        if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        }
    }

    private static void WriteUInt32(Span<byte> destination, uint value, ByteOrder byteOrder)
    {
        if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        }
    }

    private static int PadToFourBytes(int length)
    {
        return (length + 3) & ~3;
    }

    private sealed class FakeNativeWindowHost : INativeWindowHost
    {
        public string PlatformName => "Fake";

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<NativeWindowReference> CreateWindowAsync(
            WindowDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NativeWindowReference(descriptor.WindowId, 0, PlatformName, true));
        }

        public Task UpdateWindowAsync(
            NativeWindowReference handle,
            WindowDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ShowWindowAsync(
            NativeWindowReference handle,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task HideWindowAsync(
            NativeWindowReference handle,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PresentFrameAsync(
            NativeWindowReference handle,
            FramebufferSnapshot framebuffer,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DestroyWindowAsync(
            NativeWindowReference handle,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNativeWindowHost : INativeWindowHost
    {
        public int InitializeCalls { get; private set; }

        public List<WindowDescriptor> CreatedWindows { get; } = [];

        public List<WindowDescriptor> UpdatedWindows { get; } = [];

        public List<NativeWindowReference> ShownWindows { get; } = [];

        public List<NativeWindowReference> HiddenWindows { get; } = [];

        public List<(NativeWindowReference Handle, FramebufferSnapshot Framebuffer)> PresentedFrames { get; } = [];

        public List<NativeWindowReference> DestroyedWindows { get; } = [];

        public string PlatformName => "Recording";

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task<NativeWindowReference> CreateWindowAsync(
            WindowDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            CreatedWindows.Add(descriptor);
            return Task.FromResult(new NativeWindowReference(descriptor.WindowId, descriptor.WindowId, PlatformName, true));
        }

        public Task UpdateWindowAsync(
            NativeWindowReference handle,
            WindowDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            UpdatedWindows.Add(descriptor);
            return Task.CompletedTask;
        }

        public Task ShowWindowAsync(
            NativeWindowReference handle,
            CancellationToken cancellationToken = default)
        {
            ShownWindows.Add(handle);
            return Task.CompletedTask;
        }

        public Task HideWindowAsync(
            NativeWindowReference handle,
            CancellationToken cancellationToken = default)
        {
            HiddenWindows.Add(handle);
            return Task.CompletedTask;
        }

        public Task PresentFrameAsync(
            NativeWindowReference handle,
            FramebufferSnapshot framebuffer,
            CancellationToken cancellationToken = default)
        {
            PresentedFrames.Add((handle, framebuffer));
            return Task.CompletedTask;
        }

        public Task DestroyWindowAsync(
            NativeWindowReference handle,
            CancellationToken cancellationToken = default)
        {
            DestroyedWindows.Add(handle);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingTransportListener : IX11TransportListener
    {
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string DisplayName => ":test";

        public async Task RunAsync(
            Func<X11TransportConnection, CancellationToken, Task> connectionHandler,
            CancellationToken cancellationToken = default)
        {
            _runStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public Task WaitForRunAsync()
        {
            return _runStarted.Task;
        }
    }
}
