using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Netch.Models;
using Netch.Utils;

namespace Netch.Services;

public static class DnsService
{
    private static Socket? _udpListener;
    private static Socket? _tcpListener;
    private static CancellationTokenSource? _cts;
    private static readonly HashSet<string> ChinaDomainSet = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DnsCacheItem> Cache = new();
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private static DnsEndpointConfig _chinaDnsConfig = new("223.5.5.5", 53, DnsProtocol.Udp);
    private static DnsEndpointConfig _otherDnsConfig = new("1.1.1.1", 53, DnsProtocol.Udp);

    public enum DnsProtocol
    {
        Udp,
        Tcp,
        Doh
    }

    public class DnsEndpointConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public DnsProtocol Protocol { get; set; }
        public IPEndPoint? IpEndPoint { get; set; }
        public string? DohUrl { get; set; }

        public DnsEndpointConfig(string host, int port, DnsProtocol protocol)
        {
            Host = host;
            Port = port;
            Protocol = protocol;
            if (IPAddress.TryParse(host, out var ip))
                IpEndPoint = new IPEndPoint(ip, port);
        }

        public override string ToString()
        {
            return Protocol switch
            {
                DnsProtocol.Doh => DohUrl ?? Host,
                DnsProtocol.Tcp => $"tcp://{Host}:{Port}",
                _ => $"udp://{Host}:{Port}"
            };
        }
    }

    private class DnsCacheItem
    {
        public byte[] Response { get; }
        public DateTime ExpireAt { get; }

        public DnsCacheItem(byte[] response, int ttlSeconds)
        {
            Response = response;
            ExpireAt = DateTime.UtcNow.AddSeconds(Math.Clamp(ttlSeconds, 5, 300));
        }

        public bool IsExpired => DateTime.UtcNow >= ExpireAt;
    }

    public static bool IsRunning => _udpListener != null;

    public static async Task StartAsync()
    {
        if (IsRunning)
            return;

        LoadChinaDomainRules();

        var config = Global.Settings.AioDNS;
        _chinaDnsConfig = ParseDnsConfig(config.ChinaDNS, "223.5.5.5", 53);
        _otherDnsConfig = ParseDnsConfig(config.OtherDNS, "1.1.1.1", 53);

        _cts = new CancellationTokenSource();

        // 1. 本地 UDP 53 监听
        _udpListener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _udpListener.Bind(new IPEndPoint(IPAddress.Loopback, config.ListenPort));

        // 2. 本地 TCP 53 监听 (RFC 7766 完整规范标准兼容)
        try
        {
            _tcpListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpListener.Bind(new IPEndPoint(IPAddress.Loopback, config.ListenPort));
            _tcpListener.Listen(128);
            _ = Task.Run(() => ListenTcpLoopAsync(_tcpListener, _cts.Token));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to bind TCP 53 listener for DnsService, continuing with UDP only");
            _tcpListener = null;
        }

        Log.Information("Native DnsService listening on 127.0.0.1:{Port} (Dual-Stack UDP/TCP). China: {China}, Other: {Other}",
            config.ListenPort, _chinaDnsConfig, _otherDnsConfig);

        _ = Task.Run(() => ListenUdpLoopAsync(_udpListener, _cts.Token));
        await Task.CompletedTask;
    }

    public static async Task StopAsync()
    {
        try
        {
            if (_cts != null)
                await _cts.CancelAsync();

            _udpListener?.Close();
            _udpListener?.Dispose();

            _tcpListener?.Close();
            _tcpListener?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Stop DnsService warning");
        }
        finally
        {
            _udpListener = null;
            _tcpListener = null;
            _cts = null;
        }
    }

    private static void LoadChinaDomainRules()
    {
        ChinaDomainSet.Clear();
        var rulePath = Path.GetFullPath(Constants.AioDnsRuleFile);
        if (!File.Exists(rulePath))
            return;

        try
        {
            foreach (var line in File.ReadLines(rulePath))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith('#'))
                {
                    ChinaDomainSet.Add(trimmed.TrimEnd('.'));
                }
            }
            Log.Information("Loaded {Count} China domain rules for DnsService", ChinaDomainSet.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load China domain rules");
        }
    }

    public static DnsEndpointConfig ParseDnsConfig(string dnsStr, string defaultIp, int defaultPort)
    {
        if (string.IsNullOrWhiteSpace(dnsStr))
            return new DnsEndpointConfig(defaultIp, defaultPort, DnsProtocol.Udp);

        dnsStr = dnsStr.Trim();

        // 1. DoH (DNS-over-HTTPS)
        if (dnsStr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new DnsEndpointConfig(dnsStr, 443, DnsProtocol.Doh)
            {
                DohUrl = dnsStr
            };
        }

        // 2. TCP DNS
        if (dnsStr.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            var clean = dnsStr["tcp://".Length..];
            var parts = clean.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 53;
            return new DnsEndpointConfig(host, port, DnsProtocol.Tcp);
        }

        // 3. UDP DNS (兼容 udp:// 或纯 IP:Port)
        var cleanUdp = dnsStr.StartsWith("udp://", StringComparison.OrdinalIgnoreCase)
            ? dnsStr["udp://".Length..]
            : dnsStr;

        var udpParts = cleanUdp.Split(':');
        var udpHost = udpParts[0];
        var udpPort = udpParts.Length > 1 && int.TryParse(udpParts[1], out var up) ? up : defaultPort;
        return new DnsEndpointConfig(udpHost, udpPort, DnsProtocol.Udp);
    }

    private static async Task ListenUdpLoopAsync(Socket listener, CancellationToken token)
    {
        var buffer = new byte[1500];
        EndPoint remoteClient = new IPEndPoint(IPAddress.Any, 0);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await listener.ReceiveFromAsync(buffer, SocketFlags.None, remoteClient);
                var clientEp = result.RemoteEndPoint;
                var packetLen = result.ReceivedBytes;

                if (packetLen < 12)
                    continue;

                var queryPacket = new byte[packetLen];
                Buffer.BlockCopy(buffer, 0, queryPacket, 0, packetLen);

                _ = Task.Run(() => HandleUdpQueryAsync(listener, clientEp, queryPacket, token), token);
            }
            catch when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Log.Verbose(ex, "DnsService UDP receive error");
            }
        }
    }

    private static async Task ListenTcpLoopAsync(Socket listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await listener.AcceptAsync(token);
                _ = Task.Run(() => HandleTcpClientAsync(clientSocket, token), token);
            }
            catch when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Log.Verbose(ex, "DnsService TCP accept error");
            }
        }
    }

    private static async Task HandleTcpClientAsync(Socket client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                var lenBuf = new byte[2];
                int read = await client.ReceiveAsync(lenBuf, SocketFlags.None, token);
                if (read < 2) return;

                int packetLen = (lenBuf[0] << 8) | lenBuf[1];
                if (packetLen <= 0 || packetLen > 4096) return;

                var packet = new byte[packetLen];
                int totalRead = 0;
                while (totalRead < packetLen)
                {
                    int r = await client.ReceiveAsync(packet.AsMemory(totalRead, packetLen - totalRead), SocketFlags.None, token);
                    if (r <= 0) break;
                    totalRead += r;
                }

                if (totalRead >= 12)
                {
                    var respPacket = await ResolveQueryPacketAsync(packet, token);
                    if (respPacket != null)
                    {
                        var outLenBuf = new byte[2] { (byte)(respPacket.Length >> 8), (byte)(respPacket.Length & 0xFF) };
                        await client.SendAsync(outLenBuf, SocketFlags.None, token);
                        await client.SendAsync(respPacket, SocketFlags.None, token);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Verbose(ex, "DnsService TCP client handle error");
            }
        }
    }

    private static async Task HandleUdpQueryAsync(Socket listener, EndPoint clientEp, byte[] queryPacket, CancellationToken token)
    {
        try
        {
            var respPacket = await ResolveQueryPacketAsync(queryPacket, token);
            if (respPacket != null)
            {
                await listener.SendToAsync(respPacket, SocketFlags.None, clientEp);
            }
        }
        catch (Exception ex)
        {
            Log.Verbose(ex, "DnsService UDP query forward failed");
        }
    }

    private static async Task<byte[]?> ResolveQueryPacketAsync(byte[] queryPacket, CancellationToken token)
    {
        ushort queryId = (ushort)((queryPacket[0] << 8) | queryPacket[1]);
        var (domain, qtype) = ParseQuery(queryPacket);

        string cacheKey = $"{domain?.ToLowerInvariant()}#{qtype}";

        // 1. 尝试从本地 TTL 内存缓存中直出
        if (!string.IsNullOrEmpty(domain) && Cache.TryGetValue(cacheKey, out var cacheItem) && !cacheItem.IsExpired)
        {
            var cachedResp = (byte[])cacheItem.Response.Clone();
            cachedResp[0] = (byte)(queryId >> 8);
            cachedResp[1] = (byte)(queryId & 0xFF);
            return cachedResp;
        }

        // 2. 判断国内外分流目标
        bool isChina = IsChinaDomain(domain);
        var targetConfig = isChina ? _chinaDnsConfig : _otherDnsConfig;

        // 3. 按照真实协议向上游发起带超时的安全查询 (真正3秒强行熔断防假死)
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        byte[]? responsePacket = null;
        try
        {
            responsePacket = targetConfig.Protocol switch
            {
                DnsProtocol.Doh => await QueryDohAsync(targetConfig.DohUrl!, queryPacket, timeoutCts.Token),
                DnsProtocol.Tcp => await QueryTcpAsync(targetConfig, queryPacket, timeoutCts.Token),
                _ => await QueryUdpAsync(targetConfig, queryPacket, timeoutCts.Token)
            };
        }
        catch (Exception ex)
        {
            Log.Verbose(ex, "DNS query to {Target} failed", targetConfig);
        }

        if (responsePacket != null && responsePacket.Length >= 12)
        {
            // 确保回填 ID 匹配
            responsePacket[0] = (byte)(queryId >> 8);
            responsePacket[1] = (byte)(queryId & 0xFF);

            // 写入本地高速缓存
            if (!string.IsNullOrEmpty(domain))
            {
                int ttl = ExtractMinTtl(responsePacket);
                Cache[cacheKey] = new DnsCacheItem(responsePacket, ttl);
            }

            return responsePacket;
        }

        return null;
    }

    private static async Task<byte[]?> QueryUdpAsync(DnsEndpointConfig config, byte[] queryPacket, CancellationToken token)
    {
        var targetEp = config.IpEndPoint ?? (await DnsUtils.LookupAsync(config.Host) is { } ip ? new IPEndPoint(ip, config.Port) : null);
        if (targetEp == null) return null;

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        await client.SendToAsync(queryPacket, SocketFlags.None, targetEp);

        var respBuffer = new byte[1500];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        var result = await client.ReceiveFromAsync(respBuffer, SocketFlags.None, remote, token);
        if (result.ReceivedBytes >= 12)
        {
            var res = new byte[result.ReceivedBytes];
            Buffer.BlockCopy(respBuffer, 0, res, 0, result.ReceivedBytes);
            return res;
        }

        return null;
    }

    private static async Task<byte[]?> QueryTcpAsync(DnsEndpointConfig config, byte[] queryPacket, CancellationToken token)
    {
        var targetEp = config.IpEndPoint ?? (await DnsUtils.LookupAsync(config.Host) is { } ip ? new IPEndPoint(ip, config.Port) : null);
        if (targetEp == null) return null;

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(targetEp, token);

        // RFC 7766: TCP DNS 报文前带有 2 字节长的大端报文长度
        var lenBuf = new byte[2] { (byte)(queryPacket.Length >> 8), (byte)(queryPacket.Length & 0xFF) };
        await client.SendAsync(lenBuf, SocketFlags.None, token);
        await client.SendAsync(queryPacket, SocketFlags.None, token);

        var respLenBuf = new byte[2];
        int r = await client.ReceiveAsync(respLenBuf, SocketFlags.None, token);
        if (r < 2) return null;

        int respLen = (respLenBuf[0] << 8) | respLenBuf[1];
        if (respLen <= 0 || respLen > 4096) return null;

        var respBuf = new byte[respLen];
        int totalRead = 0;
        while (totalRead < respLen)
        {
            int bytesRead = await client.ReceiveAsync(respBuf.AsMemory(totalRead, respLen - totalRead), SocketFlags.None, token);
            if (bytesRead <= 0) break;
            totalRead += bytesRead;
        }

        return totalRead >= 12 ? respBuf : null;
    }

    private static async Task<byte[]?> QueryDohAsync(string dohUrl, byte[] queryPacket, CancellationToken token)
    {
        using var content = new ByteArrayContent(queryPacket);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/dns-message");

        using var request = new HttpRequestMessage(HttpMethod.Post, dohUrl) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-message"));

        var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsByteArrayAsync(token);
        }

        return null;
    }

    private static (string? domain, ushort qtype) ParseQuery(byte[] packet)
    {
        try
        {
            int offset = 12;
            var sb = new StringBuilder();

            while (offset < packet.Length)
            {
                int len = packet[offset++];
                if (len == 0)
                    break;

                if (sb.Length > 0)
                    sb.Append('.');

                sb.Append(Encoding.ASCII.GetString(packet, offset, len));
                offset += len;
            }

            ushort qtype = 0;
            if (offset + 2 <= packet.Length)
            {
                qtype = (ushort)((packet[offset] << 8) | packet[offset + 1]);
            }

            return (sb.ToString(), qtype);
        }
        catch
        {
            return (null, 0);
        }
    }

    private static int ExtractMinTtl(byte[] packet)
    {
        try
        {
            int minTtl = 60;
            int offset = 12;

            while (offset < packet.Length && packet[offset] != 0)
            {
                if ((packet[offset] & 0xC0) == 0xC0)
                {
                    offset += 2;
                    break;
                }
                offset += packet[offset] + 1;
            }
            if (offset < packet.Length && packet[offset] == 0)
                offset++;

            offset += 4;

            if (offset + 10 <= packet.Length)
            {
                if ((packet[offset] & 0xC0) == 0xC0)
                    offset += 2;

                offset += 4;
                if (offset + 4 <= packet.Length)
                {
                    int ttl = (packet[offset] << 24) | (packet[offset + 1] << 16) | (packet[offset + 2] << 8) | packet[offset + 3];
                    if (ttl > 0)
                        minTtl = Math.Min(minTtl, ttl);
                }
            }

            return minTtl;
        }
        catch
        {
            return 60;
        }
    }

    private static bool IsChinaDomain(string? domain)
    {
        if (string.IsNullOrEmpty(domain))
            return false;

        var parts = domain.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            var sub = string.Join(".", parts.Skip(i));
            if (ChinaDomainSet.Contains(sub))
                return true;
        }

        return false;
    }
}
