using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace PoC.Engine;

/// LAN duel link: one TCP connection, one JSON message per line. Lockstep: only answers (Commands) travel;
/// both PCs run the same Duel from the same DuelSetup (host = seat 0, guest = seat 1).
/// Messages (T): hello (profile + build), deck (ready with this deck), wait (not ready), setup (host: seed and
/// who goes first), cmd (an answer), hash (state fingerprint at a turn start), bye.
public sealed class LanLink : IDisposable
{
    public const int Port = 47800, BeaconPort = 47801, Proto = 1;

    public sealed class Msg
    {
        public string T = "";
        public string? Name, Mat, Back, Frame, Build, List;
        public int Pic, P, Turn, First;
        public int[]? Main, Extra, Picks;
        public ulong Seed, Hash;
    }

    /// Same engine build and card data on both PCs, or the duels could drift apart.
    public static string Build => $"{Proto}/{typeof(Duel).Assembly.ManifestModule.ModuleVersionId}/{CardDb.Shared.ByIdx.Count}";

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    readonly TcpClient tcp;
    readonly StreamWriter w;
    readonly Channel<Msg> inbox = Channel.CreateUnbounded<Msg>();
    public readonly bool IsHost;
    public string Peer => (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address.MapToIPv4().ToString() ?? "";
    /// The other side hung up (or the connection broke).
    public bool Closed { get; private set; }

    LanLink(TcpClient c, bool host)
    {
        tcp = c; IsHost = host;
        tcp.NoDelay = true;
        var s = tcp.GetStream();
        w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        _ = Task.Run(async () =>
        {
            try
            {
                using var r = new StreamReader(s, Encoding.UTF8);
                while (await r.ReadLineAsync() is { } line)
                    if (JsonSerializer.Deserialize<Msg>(line, Json) is { } m) inbox.Writer.TryWrite(m);
            }
            catch (Exception) { }
            Closed = true;
            inbox.Writer.TryComplete();
        });
    }

    /// Wait for one guest on `port`.
    public static async Task<LanLink> Host(CancellationToken ct, int port = Port)
    {
        var l = new TcpListener(IPAddress.Any, port);
        l.Start();
        try { return new LanLink(await l.AcceptTcpClientAsync(ct), true); }
        finally { l.Stop(); }
    }

    public static async Task<LanLink> Join(string host, CancellationToken ct, int port = Port)
    {
        var c = new TcpClient();
        try { await c.ConnectAsync(host, port, ct); }
        catch { c.Dispose(); throw; }
        return new LanLink(c, false);
    }

    public void Send(Msg m)
    {
        if (Closed) return;
        try { lock (w) w.WriteLine(JsonSerializer.Serialize(m, Json)); }
        catch (Exception) { Closed = true; }
    }

    public bool TryRead(out Msg m) => inbox.Reader.TryRead(out m!);

    /// The next message; null once the link is closed.
    public async Task<Msg?> Read(CancellationToken ct = default)
    {
        try { return await inbox.Reader.ReadAsync(ct); }
        catch (ChannelClosedException) { return null; }
    }

    public void Dispose()
    {
        Send(new Msg { T = "bye" });
        Closed = true;
        tcp.Dispose();
    }
}

/// LAN discovery: a waiting host shouts "POCR|proto|name|port" by UDP broadcast every second; the join list
/// collects whoever it hears. Networks that block broadcast still work by typing the host's IP.
public static class Beacon
{
    const string Tag = "POCR";

    public static async Task Announce(string name, CancellationToken ct, int port = LanLink.Port)
    {
        using var u = new UdpClient { EnableBroadcast = true };
        var b = Encoding.UTF8.GetBytes($"{Tag}|{LanLink.Proto}|{name.Replace('|', ' ')}|{port}");
        // 255.255.255.255 only leaves the default adapter; each adapter's own broadcast reaches virtual LANs
        // (Hamachi, Radmin VPN) too.
        var to = Broadcasts().Select(a => new IPEndPoint(a, LanLink.BeaconPort)).ToList();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var t in to) try { await u.SendAsync(b, t, ct); } catch (SocketException) { }
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }   // no network: nobody can hear us anyway
    }

    static IEnumerable<IPAddress> Broadcasts()
    {
        yield return IPAddress.Broadcast;
        foreach (var n in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
            foreach (var a in n.GetIPProperties().UnicastAddresses)
            {
                if (a.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a.Address) || a.IPv4Mask == null) continue;
                var ip = a.Address.GetAddressBytes(); var m = a.IPv4Mask.GetAddressBytes();
                for (int i = 0; i < 4; i++) ip[i] |= (byte)~m[i];
                yield return new IPAddress(ip);
            }
        }
    }

    /// Calls `found(name, address)` for every announcement heard (repeats included).
    public static async Task Listen(Action<string, string> found, CancellationToken ct)
    {
        using var u = new UdpClient { ExclusiveAddressUse = false };
        u.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);   // two copies on one PC
        u.Client.Bind(new IPEndPoint(IPAddress.Any, LanLink.BeaconPort));
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var r = await u.ReceiveAsync(ct);
                var p = Encoding.UTF8.GetString(r.Buffer).Split('|');
                if (p.Length == 4 && p[0] == Tag && p[1] == LanLink.Proto.ToString())
                    found(p[2], $"{r.RemoteEndPoint.Address}:{p[3]}");
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
    }
}
