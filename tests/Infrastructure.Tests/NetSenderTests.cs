using System.Net;
using System.Net.Sockets;
using System.Text;
using MidiToEverything.Infrastructure.Net;

namespace MidiToEverything.Infrastructure.Tests;

public class NetSenderTests
{
    [Fact]
    public void OscEncode_LaysOutAddressTypeTagAndArgs()
    {
        var packet = OscSender.Encode("/test", "1 2.5 hi");

        // address: "/test" + null, padded to 8 bytes
        Assert.Equal("/test", ReadOscString(packet, 0, out var next));
        // type tag: ",ifs" (int, float, string)
        Assert.Equal(",ifs", ReadOscString(packet, next, out _));
    }

    [Fact]
    public async Task OscSender_SendsUdpDatagram_DecodableByReceiver()
    {
        using var receiver = new UdpClient(0); // OS-assigned free port
        var port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;

        new OscSender().Send($"127.0.0.1:{port}", "/x", "42");

        var receive = receiver.ReceiveAsync();
        var done = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(receive, done); // datagram arrived
        Assert.Equal("/x", ReadOscString(receive.Result.Buffer, 0, out _));
    }

    [Fact]
    public async Task HttpSender_PerformsTheRequest()
    {
        using var listener = new HttpListener();
        var port = FreeTcpPort();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        var received = listener.GetContextAsync();
        new HttpSender().Send($"http://localhost:{port}/hook", "POST", "hello");

        var done = await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received, done);

        var ctx = received.Result;
        Assert.Equal("POST", ctx.Request.HttpMethod);
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        Assert.Equal("hello", await reader.ReadToEndAsync());
        ctx.Response.Close();
        listener.Stop();
    }

    private static string ReadOscString(byte[] data, int offset, out int next)
    {
        var end = offset;
        while (end < data.Length && data[end] != 0)
        {
            end++;
        }

        var s = Encoding.ASCII.GetString(data, offset, end - offset);
        var len = end - offset + 1;            // include null
        next = offset + len + ((4 - (len % 4)) % 4); // 4-byte aligned
        return s;
    }

    private static int FreeTcpPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
