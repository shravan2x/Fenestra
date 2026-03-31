using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Encoding;
using Fenestra.Protocol.X11.Parsing;

namespace Fenestra.Server;

internal sealed class X11RequestDispatcher
{
    private readonly X11DisplayState _displayState;

    public X11RequestDispatcher(X11DisplayState displayState)
    {
        _displayState = displayState ?? throw new ArgumentNullException(nameof(displayState));
    }

    public byte[] Dispatch(
        X11ClientState clientState,
        ushort sequenceNumber,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes)
    {
        ArgumentNullException.ThrowIfNull(clientState);

        return header.MajorOpcode switch
        {
            14 => HandleGetGeometry(clientState, _displayState, header, requestBytes, sequenceNumber),
            15 => HandleQueryTree(clientState, _displayState, header, requestBytes, sequenceNumber),
            16 => HandleInternAtom(clientState, _displayState, header, requestBytes, sequenceNumber),
            _ => X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Request,
                sequenceNumber,
                badValue: 0,
                header.MajorOpcode)
        };
    }

    private static byte[] HandleGetGeometry(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 8)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var drawableId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        if (!displayState.TryGetWindow(drawableId, out var window) || window is null)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Drawable,
                sequenceNumber,
                drawableId,
                header.MajorOpcode);
        }

        return X11CoreReplyEncoder.EncodeGetGeometryReply(
            clientState.ByteOrder,
            sequenceNumber,
            window.Depth,
            displayState.RootWindowId,
            window.X,
            window.Y,
            window.Width,
            window.Height,
            window.BorderWidth);
    }

    private static byte[] HandleQueryTree(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 8)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        if (!displayState.TryGetWindow(windowId, out var window) || window is null)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        var parentWindowId = window.ParentId ?? 0u;

        return X11CoreReplyEncoder.EncodeQueryTreeReply(
            clientState.ByteOrder,
            sequenceNumber,
            displayState.RootWindowId,
            parentWindowId,
            []);
    }

    private static byte[] HandleInternAtom(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length < 8)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var onlyIfExists = requestBytes[1] != 0;
        var nameLength = X11RequestParser.ReadUInt16(requestBytes[4..6], clientState.ByteOrder);
        var paddedNameLength = X11RequestParser.PadToFourBytes(nameLength);
        if (requestBytes.Length != 8 + paddedNameLength)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var atomName = X11RequestParser.ReadPaddedAsciiString(requestBytes, 8, nameLength);
        var atomId = displayState.InternAtom(atomName, onlyIfExists);

        return X11CoreReplyEncoder.EncodeInternAtomReply(
            clientState.ByteOrder,
            sequenceNumber,
            atomId);
    }
}
