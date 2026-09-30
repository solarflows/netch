using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Netch.Models;
using Netch.Utils;

namespace Netch.Services;

public static class DnsService
{
    private static Socket? _listener;
    private static CancellationTokenSource? _cts;
    private static readonly HashSet<string> ChinaDomainSet = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DnsCacheItem> Cache = new();

    private static IPEndPoint _chinaDnsEndPoint = new(IPAddress.Parse("223.5.5.5"), 53);
    private static IPEndPoint _otherDnsEndPoint = new(IPAddress.Parse("1.1.1.1"), 53);

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

    public static bool IsRunning => _listener != null;

    public static async Task StartAsync()
    {
        if (IsRunning)
            return;

        LoadChinaDomainRules();

        var config = Global.Settings.AioDNS;
        _chinaDnsEndPoint = ParseDnsEndPoint(config.ChinaDNS, "223.5.5.5", 53);
        _otherDnsEndPoint = ParseDnsEndPoint(config.OtherDNS, "1.1.1.1", 53);

        _cts = new CancellationTokenSource();
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _listener.Bind(new IPEndPoint(IPAddress.Loopback, config.ListenPort));

        Log.Information("Native DnsService listening on 127.0.0.1:{Port}", config.ListenPort);

        _ = Task.Run(() => ListenLoopAsync(_listener, _cts.Token));
        await Task.CompletedTask;
    }

    public static async Task StopAsync()
    {
        try
        {
            if (_cts != null)
                await _cts.CancelAsync();
            _listener?.Close();
            _listener?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Stop DnsService warning");
        }
        finally
        {
            _listener = null;
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

    private static IPEndPoint ParseDnsEndPoint(string dnsStr, string defaultIp, int defaultPort)
    {
        try
        {
            var raw = dnsStr.Replace("tcp://", "").Replace("udp://", "").Replace("tls://", "");
            var parts = raw.Split(':');
            var ip = IPAddress.Parse(parts[0]);
            var port = parts.Length > 1 ? int.Parse(parts[1]) : defaultPort;
            return new IPEndPoint(ip, port);
        }
        catch
        {
            return new IPEndPoint(IPAddress.Parse(defaultIp), defaultPort);
        }
    }

    private static async Task ListenLoopAsync(Socket listener, CancellationToken token)
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

                _ = Task.Run(() => HandleQueryAsync(listener, clientEp, queryPacket, token), token);
            }
            catch when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Log.Verbose(ex, "DnsService receive error");
            }
        }
    }

    private static async Task HandleQueryAsync(Socket listener, EndPoint clientEp, byte[] queryPacket, CancellationToken token)
    {
        try
        {
            ushort queryId = (ushort)((queryPacket[0] << 8) | queryPacket[1]);
            var (domain, qtype) = ParseQuery(queryPacket);

            string cacheKey = $"{domain?.ToLowerInvariant()}#{qtype}";

            // 1. 尝试从本地 TTL 内存缓存中直出
            if (!string.IsNullOrEmpty(domain) && Cache.TryGetValue(cacheKey, out var cacheItem) && !cacheItem.IsExpired)
            {
                var cachedResp = (byte[])cacheItem.Response.Clone();
                // 覆盖匹配原请求的 Transaction ID
                cachedResp[0] = (byte)(queryId >> 8);
                cachedResp[1] = (byte)(queryId & 0xFF);

                Log.Debug("DNS Cache Hit: [{QType}] {Domain} (0ms response to {Client})", qtype, domain, clientEp);
                await listener.SendToAsync(cachedResp, SocketFlags.None, clientEp);
                return;
            }

            // 2. 判断国内外分流目标
            bool isChina = IsChinaDomain(domain);
            var targetUpstream = isChina ? _chinaDnsEndPoint : _otherDnsEndPoint;
            Log.Debug("DNS Query: [{QType}] {Domain} -> Upstream {Target} ({Route})", qtype, domain, targetUpstream, isChina ? "China" : "Foreign");

            // 3. 向上游发起查询
            using var upstreamClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            upstreamClient.ReceiveTimeout = 3000;
            upstreamClient.SendTimeout = 3000;

            await upstreamClient.SendToAsync(queryPacket, SocketFlags.None, targetUpstream);

            var respBuffer = new byte[1500];
            EndPoint upstreamEp = new IPEndPoint(IPAddress.Any, 0);
            var respResult = await upstreamClient.ReceiveFromAsync(respBuffer, SocketFlags.None, upstreamEp);

            if (respResult.ReceivedBytes >= 12)
            {
                var respPacket = new byte[respResult.ReceivedBytes];
                Buffer.BlockCopy(respBuffer, 0, respPacket, 0, respResult.ReceivedBytes);

                // 写入缓存 (提取 TTL)
                if (!string.IsNullOrEmpty(domain))
                {
                    int ttl = ExtractMinTtl(respPacket);
                    Cache[cacheKey] = new DnsCacheItem(respPacket, ttl);
                }

                // 回填给客户端
                await listener.SendToAsync(respPacket, SocketFlags.None, clientEp);
            }
        }
        catch (Exception ex)
        {
            Log.Verbose(ex, "DNS query forward failed");
        }
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
            // 简单扫描回答区的最小 TTL，默认为 60s
            int minTtl = 60;
            int offset = 12;

            // 跳过 Question 域
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

            offset += 4; // 跳过 QTYPE + QCLASS

            // 扫描 Answer 域
            if (offset + 10 <= packet.Length)
            {
                if ((packet[offset] & 0xC0) == 0xC0)
                    offset += 2;

                offset += 4; // TYPE + CLASS
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
