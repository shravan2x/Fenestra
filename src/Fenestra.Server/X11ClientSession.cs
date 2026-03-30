using Fenestra.Protocol.X11.Encoding;
using Fenestra.Protocol.X11.Parsing;
using Fenestra.Protocol.X11.Setup;
using Fenestra.Transport;

namespace Fenestra.Server;

internal sealed class X11ClientSession
{
    private readonly X11ServerHandshakeConfiguration _handshakeConfiguration;

    public X11ClientSession(X11ServerHandshakeConfiguration handshakeConfiguration)
    {
        _handshakeConfiguration = handshakeConfiguration ?? throw new ArgumentNullException(nameof(handshakeConfiguration));
    }

    public async Task HandleAsync(X11TransportConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var header = await ReadExactAsync(
            connection.Stream,
            X11HandshakeParser.SetupRequestHeaderLength,
            cancellationToken).ConfigureAwait(false);

        var totalLength = X11HandshakeParser.GetSetupRequestLength(header);
        var requestBytes = new byte[totalLength];
        header.CopyTo(requestBytes, 0);

        if (totalLength > header.Length)
        {
            var payload = await ReadExactAsync(
                connection.Stream,
                totalLength - header.Length,
                cancellationToken).ConfigureAwait(false);
            payload.CopyTo(requestBytes, header.Length);
        }

        if (!X11HandshakeParser.TryParseSetupRequest(requestBytes, out var request) || request is null)
        {
            throw new InvalidOperationException("Received malformed X11 setup request.");
        }

        var responseBytes = CreateResponseBytes(request);
        await connection.Stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
        await connection.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine(
            $"Accepted X11 client {connection.RemoteEndpoint} using byte order {(char)request.ByteOrder} and protocol {request.ProtocolMajorVersion}.{request.ProtocolMinorVersion}.");
    }

    private byte[] CreateResponseBytes(X11SetupRequest request)
    {
        if (request.ProtocolMajorVersion != 11 || request.ProtocolMinorVersion != 0)
        {
            return X11SetupResponseEncoder.EncodeFailure(
                new X11SetupFailureResponse(
                    request.ByteOrder,
                    request.ProtocolMajorVersion,
                    request.ProtocolMinorVersion,
                    "Unsupported X11 protocol version."));
        }

        return X11SetupResponseEncoder.EncodeSuccess(_handshakeConfiguration.CreateSuccessResponse(request.ByteOrder));
    }

    private static async Task<byte[]> ReadExactAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                throw new EndOfStreamException("Client disconnected before the X11 setup request completed.");
            }

            offset += read;
        }

        return buffer;
    }
}
