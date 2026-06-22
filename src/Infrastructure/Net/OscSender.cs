using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Infrastructure.Net;

/// <summary>
/// Minimal OSC 1.0 sender over UDP (docs/05 §5, Phase 3). Encodes an address + arguments
/// (int/float/string, auto-typed from the token) and sends one message to "host:port".
/// </summary>
public sealed class OscSender : IOscSender
{
    private readonly ILogger<OscSender> _logger;

    public OscSender(ILogger<OscSender>? logger = null)
        => _logger = logger ?? NullLogger<OscSender>.Instance;

    public void Send(string target, string address, string args)
    {
        try
        {
            var (host, port) = ParseTarget(target);
            var packet = Encode(address, args);
            using var udp = new UdpClient();
            udp.Send(packet, packet.Length, host, port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSC send to {Target} {Address} failed", target, address);
        }
    }

    private static (string Host, int Port) ParseTarget(string target)
    {
        var idx = target.LastIndexOf(':');
        if (idx <= 0 || !int.TryParse(target[(idx + 1)..], out var port))
        {
            throw new FormatException($"OSC target must be host:port, got '{target}'");
        }

        return (target[..idx], port);
    }

    /// <summary>Encode an OSC message (address + auto-typed args) to its UDP byte payload.</summary>
    public static byte[] Encode(string address, string args)
    {
        var tokens = string.IsNullOrWhiteSpace(args)
            ? Array.Empty<string>()
            : args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var typeTag = new StringBuilder(",");
        var body = new List<byte>();
        foreach (var token in tokens)
        {
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                typeTag.Append('i');
                body.AddRange(BigEndian(i));
            }
            else if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                typeTag.Append('f');
                body.AddRange(BigEndian(BitConverter.SingleToInt32Bits(f)));
            }
            else
            {
                typeTag.Append('s');
                body.AddRange(OscString(token));
            }
        }

        var packet = new List<byte>();
        packet.AddRange(OscString(address));
        packet.AddRange(OscString(typeTag.ToString()));
        packet.AddRange(body);
        return packet.ToArray();
    }

    private static byte[] OscString(string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        var len = bytes.Length + 1;          // include the null terminator
        var padded = len + ((4 - (len % 4)) % 4);
        var result = new byte[padded];
        Array.Copy(bytes, result, bytes.Length);
        return result;
    }

    private static byte[] BigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }
}
