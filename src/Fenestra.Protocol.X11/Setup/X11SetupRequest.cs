namespace Fenestra.Protocol.X11.Setup;

public sealed record X11SetupRequest(
    ByteOrder ByteOrder,
    ushort ProtocolMajorVersion,
    ushort ProtocolMinorVersion,
    ushort AuthorizationProtocolNameLength,
    ushort AuthorizationProtocolDataLength,
    string AuthorizationProtocolName,
    byte[] AuthorizationProtocolData);
