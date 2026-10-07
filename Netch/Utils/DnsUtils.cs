using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.Threading;

namespace Netch.Utils;

public static class DnsUtils
{
    // 提升 DNS 并发信号量至 8，彻底消除单锁导致的全局串行堵塞
    private static readonly AsyncSemaphore Lock = new(8);

    /// <summary>
    ///     线程安全高速缓存 (零锁直读)
    /// </summary>
    private static readonly ConcurrentDictionary<string, IPAddress> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, IPAddress> Cache6 = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IPAddress?> LookupAsync(string hostname, AddressFamily inet = AddressFamily.Unspecified, int timeout = 3000)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return null;

        var cleanHost = hostname.Trim('[', ']');

        // 1. 若本身已是有效 IP 地址，0ms 直接短路返回，无需查缓存或进入信号量
        if (IPAddress.TryParse(cleanHost, out var directIp))
        {
            if (inet == AddressFamily.Unspecified || directIp.AddressFamily == inet)
                return directIp;

            return null;
        }

        // 2. 线程安全缓存直读 (零锁)
        IPAddress? cached = inet switch
        {
            AddressFamily.Unspecified => Cache.TryGetValue(cleanHost, out var v4) ? v4 : (Cache6.TryGetValue(cleanHost, out var v6) ? v6 : null),
            AddressFamily.InterNetwork => Cache.TryGetValue(cleanHost, out var v4) ? v4 : null,
            AddressFamily.InterNetworkV6 => Cache6.TryGetValue(cleanHost, out var v6) ? v6 : null,
            _ => throw new ArgumentOutOfRangeException(nameof(inet))
        };

        if (cached != null)
            return cached;

        // 3. 限制并发数进行异步 DNS 解析
        using var _ = await Lock.EnterAsync();

        // 二次双重检查
        cached = inet switch
        {
            AddressFamily.Unspecified => Cache.TryGetValue(cleanHost, out var v4) ? v4 : (Cache6.TryGetValue(cleanHost, out var v6) ? v6 : null),
            AddressFamily.InterNetwork => Cache.TryGetValue(cleanHost, out var v4) ? v4 : null,
            AddressFamily.InterNetworkV6 => Cache6.TryGetValue(cleanHost, out var v6) ? v6 : null,
            _ => null
        };
        if (cached != null)
            return cached;

        try
        {
            return await LookupNoCacheAsync(cleanHost, inet, timeout);
        }
        catch (Exception e)
        {
            Log.Verbose(e, "Lookup hostname {Hostname} failed", cleanHost);
            return null;
        }
    }

    private static async Task<IPAddress?> LookupNoCacheAsync(string hostname, AddressFamily inet = AddressFamily.Unspecified, int timeout = 3000)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostname, cts.Token);

            var result = addresses.FirstOrDefault(i => inet == AddressFamily.Unspecified || inet == i.AddressFamily);
            if (result == null)
                return null;

            if (result.AddressFamily == AddressFamily.InterNetwork)
                Cache[hostname] = result;
            else if (result.AddressFamily == AddressFamily.InterNetworkV6)
                Cache6[hostname] = result;

            return result;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static void ClearCache()
    {
        Cache.Clear();
        Cache6.Clear();
    }

    public static string AppendPort(string host, ushort port = 53)
    {
        if (!host.Contains(':'))
            return host + $":{port}";

        return host;
    }
}
