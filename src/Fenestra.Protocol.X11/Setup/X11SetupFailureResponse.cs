namespace Fenestra.Protocol.X11.Setup;

public sealed class X11SetupFailureResponse
{
    public X11SetupFailureResponse(
        ByteOrder byteOrder,
        ushort protocolMajorVersion,
        ushort protocolMinorVersion,
        string reason)
    {
        ByteOrder = byteOrder;
        ProtocolMajorVersion = protocolMajorVersion;
        ProtocolMinorVersion = protocolMinorVersion;
        Reason = string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Failure reason is required.", nameof(reason))
            : reason;
    }

    public ByteOrder ByteOrder { get; }

    public ushort ProtocolMajorVersion { get; }

    public ushort ProtocolMinorVersion { get; }

    public string Reason { get; }

    public byte[] GetReasonBytes()
    {
        return System.Text.Encoding.ASCII.GetBytes(Reason);
    }
}
