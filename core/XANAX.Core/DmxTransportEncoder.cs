using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;

namespace Xanax.Core;

public interface IDmxUniverseFrame
{
    byte this[int universe, int channel] { get; }
}

public sealed record ArtNetDmxPacket(
    int Universe,
    byte Sequence,
    IReadOnlyList<byte> Bytes)
{
    public const int HeaderLength = 18;
    public const int DataLength = 512;
}

public sealed record DmxOutputPlan(
    IReadOnlyList<ArtNetDmxPacket> Packets,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Encodes the canonical patched output into the Art-Net ArtDMX wire format.
/// This is a pure encoder; it never opens a socket or sends data.
/// </summary>
public static class ArtNetEncoder
{
    private static readonly byte[] ArtNetId = Encoding.ASCII.GetBytes("Art-Net\0");
    private const ushort ArtDmxOpcode = 0x5000;
    private const ushort ProtocolVersion = 14;

    public static ArtNetDmxPacket Encode(
        IDmxUniverseFrame frame,
        int universe,
        byte sequence = 0,
        byte physical = 0)
    {
        if (universe is < 1 or > 32768)
            throw new ArgumentOutOfRangeException(nameof(universe), "Patched universes must map into the Art-Net 0..32767 universe field.");

        var packet = new byte[ArtNetDmxPacket.HeaderLength + ArtNetDmxPacket.DataLength];
        ArtNetId.CopyTo(packet, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8, 2), ArtDmxOpcode);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(10, 2), ProtocolVersion);
        packet[12] = sequence;
        packet[13] = physical;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(14, 2), (ushort)(universe - 1));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(16, 2), ArtNetDmxPacket.DataLength);
        for (var channel = 1; channel <= ArtNetDmxPacket.DataLength; channel++)
            packet[ArtNetDmxPacket.HeaderLength + channel - 1] = frame[universe, channel];
        return new(universe, sequence, new ReadOnlyCollection<byte>(packet));
    }

    public static DmxOutputPlan Compile(
        IDmxUniverseFrame frame,
        IEnumerable<int> universes,
        byte sequence = 0,
        byte physical = 0)
    {
        var errors = new List<string>();
        var packets = new List<ArtNetDmxPacket>();
        foreach (var universe in universes.Distinct().OrderBy(value => value))
        {
            if (universe < 1)
            {
                errors.Add($"invalid_artnet_universe:{universe}");
                continue;
            }
            packets.Add(Encode(frame, universe, sequence, physical));
        }
        return new(packets, errors);
    }
}
