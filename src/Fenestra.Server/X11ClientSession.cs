using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Encoding;
using Fenestra.Protocol.X11.Parsing;
using Fenestra.Protocol.X11.Setup;
using Fenestra.Transport;

namespace Fenestra.Server;

internal sealed class X11ClientSession
{
    private readonly X11DisplayState _displayState;
    private readonly X11ServerHandshakeConfiguration _handshakeConfiguration;
    private readonly X11RequestDispatcher _requestDispatcher;

    public X11ClientSession(
        X11DisplayState displayState,
        X11ServerHandshakeConfiguration handshakeConfiguration)
    {
        _displayState = displayState ?? throw new ArgumentNullException(nameof(displayState));
        _handshakeConfiguration = handshakeConfiguration ?? throw new ArgumentNullException(nameof(handshakeConfiguration));
        _requestDispatcher = new X11RequestDispatcher(_displayState);
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

        var clientState = _displayState.CreateClientState(request.ByteOrder);
        var responseBytes = CreateResponseBytes(request, clientState);
        await connection.Stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
        await connection.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine(
            $"Accepted X11 client {clientState.ClientId} from {connection.RemoteEndpoint} using byte order {(char)request.ByteOrder} and protocol {request.ProtocolMajorVersion}.{request.ProtocolMinorVersion}.");

        await RunRequestLoopAsync(connection.Stream, clientState, cancellationToken).ConfigureAwait(false);
    }

    private byte[] CreateResponseBytes(X11SetupRequest request, X11ClientState clientState)
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

        return X11SetupResponseEncoder.EncodeSuccess(
            _handshakeConfiguration.CreateSuccessResponse(request.ByteOrder, _displayState, clientState));
    }

    private async Task RunRequestLoopAsync(
        Stream stream,
        X11ClientState clientState,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            byte[] headerBytes;

            try
            {
                headerBytes = await ReadExactAsync(stream, X11RequestHeader.Size, cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                break;
            }

            X11RequestHeader header;
            try
            {
                header = X11RequestHeader.Parse(headerBytes, clientState.ByteOrder);
            }
            catch (InvalidOperationException)
            {
                var errorSequenceNumber = clientState.AdvanceSequenceNumber();
                var errorBytes = X11ErrorEncoder.Encode(
                    clientState.ByteOrder,
                    X11ErrorCode.Length,
                    errorSequenceNumber,
                    badValue: 0,
                    majorOpcode: headerBytes[0]);
                await stream.WriteAsync(errorBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var requestBytes = new byte[header.LengthInBytes];
            headerBytes.CopyTo(requestBytes, 0);

            if (header.LengthInBytes > headerBytes.Length)
            {
                var payloadBytes = await ReadExactAsync(
                    stream,
                    header.LengthInBytes - headerBytes.Length,
                    cancellationToken).ConfigureAwait(false);
                payloadBytes.CopyTo(requestBytes, headerBytes.Length);
            }

            var sequenceNumber = clientState.AdvanceSequenceNumber();
            var dispatchResult = _requestDispatcher.Dispatch(clientState, sequenceNumber, header, requestBytes);

            await stream.WriteAsync(dispatchResult, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
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
