using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Encoding;
using Fenestra.Protocol.X11.Parsing;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Server;

internal sealed class X11RequestDispatcher
{
    private readonly X11DisplayState _displayState;
    private readonly Func<CancellationToken, Task> _rootPresenter;

    public X11RequestDispatcher(
        X11DisplayState displayState,
        Func<CancellationToken, Task>? rootPresenter = null)
    {
        _displayState = displayState ?? throw new ArgumentNullException(nameof(displayState));
        _rootPresenter = rootPresenter ?? (_ => Task.CompletedTask);
    }

    public async Task<byte[]> DispatchAsync(
        X11ClientState clientState,
        ushort sequenceNumber,
        X11RequestHeader header,
        ReadOnlyMemory<byte> requestBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientState);

        return header.MajorOpcode switch
        {
            1 => HandleCreateWindow(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            4 => HandleDestroyWindow(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            7 => HandleReparentWindow(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            8 => HandleMapWindow(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            10 => HandleUnmapWindow(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            12 => HandleConfigureWindow(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            42 => HandleSetInputFocus(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            43 => HandleGetInputFocus(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            14 => HandleGetGeometry(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            15 => HandleQueryTree(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            16 => HandleInternAtom(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            18 => HandleChangeProperty(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            19 => HandleDeleteProperty(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            20 => HandleGetProperty(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            46 => HandleSelectInput(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            53 => HandleCreatePixmap(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            54 => HandleFreePixmap(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            55 => HandleCreateGraphicsContext(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            60 => HandleFreeGraphicsContext(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            72 => await HandlePutImageAsync(clientState, _displayState, header, requestBytes, sequenceNumber, _rootPresenter, cancellationToken).ConfigureAwait(false),
            73 => HandleGetImage(clientState, _displayState, header, requestBytes.Span, sequenceNumber),
            _ => X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Request,
                sequenceNumber,
                badValue: 0,
                header.MajorOpcode)
        };
    }

    private static byte[] HandleCreateWindow(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length < 24)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var depth = header.MinorOpcode;
        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var parentId = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);
        var x = X11RequestParser.ReadInt16(requestBytes[12..14], clientState.ByteOrder);
        var y = X11RequestParser.ReadInt16(requestBytes[14..16], clientState.ByteOrder);
        var width = X11RequestParser.ReadUInt16(requestBytes[16..18], clientState.ByteOrder);
        var height = X11RequestParser.ReadUInt16(requestBytes[18..20], clientState.ByteOrder);
        var borderWidth = X11RequestParser.ReadUInt16(requestBytes[20..22], clientState.ByteOrder);

        if (!displayState.TryCreateWindow(
                clientState.ClientId,
                windowId,
                parentId,
                x,
                y,
                width,
                height,
                borderWidth,
                depth == 0 ? displayState.RootDepth : depth))
        {
            var errorCode = displayState.TryGetWindow(parentId, out _)
                ? X11ErrorCode.Window
                : X11ErrorCode.Match;
            var badValue = errorCode == X11ErrorCode.Window ? windowId : parentId;
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                errorCode,
                sequenceNumber,
                badValue,
                header.MajorOpcode);
        }

        return Array.Empty<byte>();
    }

    private static byte[] HandleDestroyWindow(
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
        if (!displayState.TryDestroyWindow(clientState.ClientId, windowId, out var destroyedWindowIds))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        foreach (var destroyedId in destroyedWindowIds)
        {
            displayState.BroadcastDestroyNotify(sequenceNumber, destroyedId);
        }

        return Array.Empty<byte>();
    }

    private static byte[] HandleReparentWindow(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 12)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var parentId = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);
        var x = X11RequestParser.ReadInt16(requestBytes[12..14], clientState.ByteOrder);
        var y = X11RequestParser.ReadInt16(requestBytes[14..16], clientState.ByteOrder);

        if (!displayState.TryReparentWindow(clientState.ClientId, windowId, parentId, x, y, out var window) || window is null)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        displayState.BroadcastReparentNotify(sequenceNumber, window.Id, parentId, x, y);
        return Array.Empty<byte>();
    }

    private static byte[] HandleMapWindow(
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
        if (!displayState.TryMapWindow(clientState.ClientId, windowId, out var window) || window is null)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        displayState.BroadcastMapNotify(sequenceNumber, window.Id);
        return Array.Empty<byte>();
    }

    private static byte[] HandleUnmapWindow(
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
        if (!displayState.TryUnmapWindow(clientState.ClientId, windowId, out var window) || window is null)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        displayState.BroadcastUnmapNotify(sequenceNumber, window.Id);
        return Array.Empty<byte>();
    }

    private static byte[] HandleConfigureWindow(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length < 12)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var valueMask = X11RequestParser.ReadUInt16(requestBytes[8..10], clientState.ByteOrder);
        var values = ReadConfigureValues(clientState.ByteOrder, valueMask, requestBytes[12..]);

        if (!displayState.TryConfigureWindow(
                clientState.ClientId,
                windowId,
                values.X,
                values.Y,
                values.Width,
                values.Height,
                values.BorderWidth,
                out var window) || window is null)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        displayState.BroadcastConfigureNotify(
            sequenceNumber,
            window.Id,
            window.X,
            window.Y,
            window.Width,
            window.Height,
            window.BorderWidth);
        return Array.Empty<byte>();
    }

    private static byte[] HandleChangeProperty(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length < 24)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var propertyAtom = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);
        var format = requestBytes[16];
        var length = X11RequestParser.ReadUInt32(requestBytes[20..24], clientState.ByteOrder);
        var valueBytes = requestBytes[24..].ToArray();

        var expectedLength = format switch
        {
            8 => (int)length,
            16 => checked((int)length * 2),
            32 => checked((int)length * 4),
            _ => -1
        };

        if (expectedLength < 0 || valueBytes.Length < expectedLength)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        if (valueBytes.Length != X11RequestParser.PadToFourBytes(expectedLength))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        if (!displayState.SetProperty(windowId, propertyAtom, format, valueBytes[..expectedLength]))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        displayState.BroadcastPropertyNotify(sequenceNumber, windowId, propertyAtom);
        return Array.Empty<byte>();
    }

    private static byte[] HandleDeleteProperty(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 12)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var propertyAtom = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);
        if (!displayState.DeleteProperty(windowId, propertyAtom))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        displayState.BroadcastPropertyNotify(sequenceNumber, windowId, propertyAtom);
        return Array.Empty<byte>();
    }

    private static byte[] HandleGetProperty(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length < 24)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var propertyAtom = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);

        if (!displayState.TryGetProperty(windowId, propertyAtom, out var propertyValue))
        {
            return X11CoreReplyEncoder.EncodeGetPropertyReply(
                clientState.ByteOrder,
                sequenceNumber,
                0,
                0,
                0,
                []);
        }

        return X11CoreReplyEncoder.EncodeGetPropertyReply(
            clientState.ByteOrder,
            sequenceNumber,
            propertyValue.Format,
            propertyValue.AtomId,
            0,
            propertyValue.Value);
    }

    private readonly record struct ConfigureValues(
        int? X,
        int? Y,
        uint? Width,
        uint? Height,
        uint? BorderWidth);

    private static ConfigureValues ReadConfigureValues(
        ByteOrder byteOrder,
        ushort valueMask,
        ReadOnlySpan<byte> valueBytes)
    {
        int? x = null;
        int? y = null;
        uint? width = null;
        uint? height = null;
        uint? borderWidth = null;
        var offset = 0;

        for (var bit = 0; bit < 7 && offset + 4 <= valueBytes.Length; bit++)
        {
            if ((valueMask & (1 << bit)) == 0)
            {
                continue;
            }

            var value = X11RequestParser.ReadUInt32(valueBytes[offset..(offset + 4)], byteOrder);
            offset += 4;

            switch (bit)
            {
                case 0: x = unchecked((int)value); break;
                case 1: y = unchecked((int)value); break;
                case 2: width = value; break;
                case 3: height = value; break;
                case 4: borderWidth = value; break;
            }
        }

        return new ConfigureValues(x, y, width, height, borderWidth);
    }

    private static byte[] HandleSetInputFocus(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 12)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var focusWindowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        if (!displayState.TryGetWindow(focusWindowId, out _))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                focusWindowId,
                header.MajorOpcode);
        }

        displayState.SetInputFocus(focusWindowId);
        return Array.Empty<byte>();
    }

    private static byte[] HandleGetInputFocus(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 4)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        return X11CoreReplyEncoder.EncodeGetInputFocusReply(
            clientState.ByteOrder,
            sequenceNumber,
            0,
            displayState.FocusWindowId);
    }

    private static byte[] HandleSelectInput(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 12)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var windowId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var eventMask = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);
        if (!displayState.SelectInput(clientState.ClientId, windowId, eventMask))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Window,
                sequenceNumber,
                windowId,
                header.MajorOpcode);
        }

        return Array.Empty<byte>();
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
            window.ChildWindowIds);
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

    private static byte[] HandleCreatePixmap(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 16)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var depth = header.MinorOpcode;
        var pixmapId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var drawableId = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);
        var width = X11RequestParser.ReadUInt16(requestBytes[12..14], clientState.ByteOrder);
        var height = X11RequestParser.ReadUInt16(requestBytes[14..16], clientState.ByteOrder);

        if (!displayState.TryGetDrawable(drawableId, out _))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Drawable,
                sequenceNumber,
                drawableId,
                header.MajorOpcode);
        }

        if (depth != displayState.RootDepth)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Match,
                sequenceNumber,
                depth,
                header.MajorOpcode);
        }

        if (!displayState.CreatePixmap(pixmapId, width, height, depth))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Pixmap,
                sequenceNumber,
                pixmapId,
                header.MajorOpcode);
        }

        return Array.Empty<byte>();
    }

    private static byte[] HandleFreePixmap(
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

        var pixmapId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        if (!displayState.FreePixmap(pixmapId))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Pixmap,
                sequenceNumber,
                pixmapId,
                header.MajorOpcode);
        }

        return Array.Empty<byte>();
    }

    private static byte[] HandleCreateGraphicsContext(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length < 12)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var graphicsContextId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var drawableId = X11RequestParser.ReadUInt32(requestBytes[8..12], clientState.ByteOrder);

        if (!displayState.CreateGraphicsContext(graphicsContextId, drawableId))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Drawable,
                sequenceNumber,
                drawableId,
                header.MajorOpcode);
        }

        return Array.Empty<byte>();
    }

    private static byte[] HandleFreeGraphicsContext(
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

        var graphicsContextId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        if (!displayState.FreeGraphicsContext(graphicsContextId))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.GContext,
                sequenceNumber,
                graphicsContextId,
                header.MajorOpcode);
        }

        return Array.Empty<byte>();
    }

    private static async Task<byte[]> HandlePutImageAsync(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlyMemory<byte> requestBytes,
        ushort sequenceNumber,
        Func<CancellationToken, Task> rootPresenter,
        CancellationToken cancellationToken)
    {
        if (requestBytes.Length < 24)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var requestSpan = requestBytes.Span;
        var format = header.MinorOpcode;
        var drawableId = X11RequestParser.ReadUInt32(requestSpan[4..8], clientState.ByteOrder);
        var graphicsContextId = X11RequestParser.ReadUInt32(requestSpan[8..12], clientState.ByteOrder);
        var width = X11RequestParser.ReadUInt16(requestSpan[12..14], clientState.ByteOrder);
        var height = X11RequestParser.ReadUInt16(requestSpan[14..16], clientState.ByteOrder);
        var dstX = (short)X11RequestParser.ReadUInt16(requestSpan[16..18], clientState.ByteOrder);
        var dstY = (short)X11RequestParser.ReadUInt16(requestSpan[18..20], clientState.ByteOrder);
        var leftPad = requestSpan[20];
        var depth = requestSpan[21];

        if (format != 2)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Match,
                sequenceNumber,
                format,
                header.MajorOpcode);
        }

        var imageBytes = requestBytes[24..].Span;
        if (!displayState.PutImage(drawableId, graphicsContextId, width, height, dstX, dstY, leftPad, depth, imageBytes))
        {
            var errorCode = displayState.TryGetDrawable(drawableId, out _)
                ? X11ErrorCode.Match
                : X11ErrorCode.Drawable;
            var badValue = errorCode == X11ErrorCode.Drawable ? drawableId : graphicsContextId;

            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                errorCode,
                sequenceNumber,
                badValue,
                header.MajorOpcode);
        }

        if (drawableId == displayState.RootWindowId)
        {
            await rootPresenter(cancellationToken).ConfigureAwait(false);
        }

        return Array.Empty<byte>();
    }

    private static byte[] HandleGetImage(
        X11ClientState clientState,
        X11DisplayState displayState,
        X11RequestHeader header,
        ReadOnlySpan<byte> requestBytes,
        ushort sequenceNumber)
    {
        if (requestBytes.Length != 20)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Length,
                sequenceNumber,
                (uint)requestBytes.Length,
                header.MajorOpcode);
        }

        var format = header.MinorOpcode;
        var drawableId = X11RequestParser.ReadUInt32(requestBytes[4..8], clientState.ByteOrder);
        var x = (short)X11RequestParser.ReadUInt16(requestBytes[8..10], clientState.ByteOrder);
        var y = (short)X11RequestParser.ReadUInt16(requestBytes[10..12], clientState.ByteOrder);
        var width = X11RequestParser.ReadUInt16(requestBytes[12..14], clientState.ByteOrder);
        var height = X11RequestParser.ReadUInt16(requestBytes[14..16], clientState.ByteOrder);

        if (format != 2)
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Match,
                sequenceNumber,
                format,
                header.MajorOpcode);
        }

        if (!displayState.TryGetImage(drawableId, x, y, width, height, out var result))
        {
            return X11ErrorEncoder.Encode(
                clientState.ByteOrder,
                X11ErrorCode.Drawable,
                sequenceNumber,
                drawableId,
                header.MajorOpcode);
        }

        return X11CoreReplyEncoder.EncodeGetImageReply(
            clientState.ByteOrder,
            sequenceNumber,
            result.Depth,
            result.VisualId,
            result.ImageBytes);
    }
}
