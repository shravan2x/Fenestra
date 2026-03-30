using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Fenestra.NativeHost.Abstractions;
using Fenestra.Protocol.X11.Encoding;
using Fenestra.Protocol.X11.Parsing;
using Fenestra.Protocol.X11.Setup;
using Fenestra.Server;
using Fenestra.Transport;

namespace Fenestra.Tests;

public sealed class X11HandshakeTests
{
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
        var response = X11ServerHandshakeConfiguration.CreateDefault().CreateSuccessResponse(ByteOrder.LittleEndian);

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

        var responseBuffer = new byte[80];
        var bytesRead = await ReadExactLengthAsync(stream, responseBuffer, 80);

        Assert.Equal(80, bytesRead);
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

    private static async Task<int> ReadExactLengthAsync(NetworkStream stream, byte[] buffer, int expectedLength)
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

        return totalRead;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        return byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer)
            : BinaryPrimitives.ReadUInt16BigEndian(buffer);
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

        public Task ShowWindowAsync(WindowDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
