using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;

namespace Xanax.Core;

public sealed record SacnDmxPacket(
    int Universe,
    byte Sequence,
    byte Priority,
    IReadOnlyList<byte> Bytes);

/// <summary>
/// Pure ANSI E1.31 data-packet encoder. It creates packets only; transmission,
/// source ownership and physical-output authorization remain outside this type.
/// </summary>
public static class SacnEncoder
{
    public const int PacketLength = 638;
    private static readonly byte[] AcnPacketIdentifier =
        { 0x41, 0x53, 0x43, 0x2D, 0x45, 0x31, 0x2E, 0x31, 0x37, 0x00, 0x00, 0x00 };

    public static SacnDmxPacket Encode(
        IDmxUniverseFrame frame,
        int universe,
        Guid cid,
        string sourceName,
        byte sequence = 0,
        byte priority = 100,
        bool previewData = false)
    {
        if (universe is < 1 or > 63999)
            throw new ArgumentOutOfRangeException(nameof(universe), "sACN universes are 1..63999.");
        if (priority is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(priority), "sACN priority is 1..200.");
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("sACN source name is required.", nameof(sourceName));

        var packet = new byte[PacketLength];
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(0, 2), 0x0010);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), 0x0000);
        AcnPacketIdentifier.CopyTo(packet, 4);
        WriteFlagsAndLength(packet, 16, PacketLength - 16);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(18, 4), 0x00000004);
        cid.TryWriteBytes(packet.AsSpan(22, 16));

        WriteFlagsAndLength(packet, 38, PacketLength - 38);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(40, 4), 0x00000002);
        WriteSourceName(packet.AsSpan(44, 64), sourceName);
        packet[108] = priority;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(109, 2), 0);
        packet[111] = sequence;
        packet[112] = previewData ? (byte)0x80 : (byte)0;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(113, 2), (ushort)universe);

        WriteFlagsAndLength(packet, 115, PacketLength - 115);
        packet[117] = 0x02;
        packet[118] = 0xA1;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(119, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(121, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(123, 2), 513);
        packet[125] = 0;
        for (var channel = 1; channel <= 512; channel++)
            packet[125 + channel] = frame[universe, channel];
        return new(universe, sequence, priority, new ReadOnlyCollection<byte>(packet));
    }

    private static void WriteFlagsAndLength(byte[] packet, int offset, int length)
    {
        if (length is < 0 or > 0x0FFF)
            throw new ArgumentOutOfRangeException(nameof(length));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset, 2), (ushort)(0x7000 | length));
    }

    private static void WriteSourceName(Span<byte> destination, string sourceName)
    {
        destination.Clear();
        var encoded = Encoding.UTF8.GetBytes(sourceName);
        encoded.AsSpan(0, Math.Min(encoded.Length, destination.Length - 1)).CopyTo(destination);
    }
}
