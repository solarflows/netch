using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Netch.Models;
using Netch.Servers;
using Socks5.Models;
using STUN.Client;
using STUN.Enums;
using STUN.Proxy;
using STUN.StunResult;

namespace Netch.Utils;

public static class Socks5ServerTestUtils
{
    public static async Task<NatTypeTestResult> DiscoveryNatTypeAsync(Socks5Server socks5, CancellationToken ctx = default)
    {
        var stunServer = Global.Settings.STUN_Server;
        var port = (ushort)Global.Settings.STUN_Server_Port;
        var local = new IPEndPoint(IPAddress.Any, 0);

        var serverIp = await DnsUtils.LookupAsync(socks5.Hostname);
        if (serverIp == null)
        {
            return new NatTypeTestResult { Result = "Resolve Server Failed!" };
        }

        var socks5Option = new Socks5CreateOption
        {
            Address = serverIp,
            Port = socks5.Port,
            UsernamePassword = socks5.Auth()
                ? new UsernamePassword
                {
                    UserName = socks5.Username,
                    Password = socks5.Password
                }
                : null
        };

        var ip = await DnsUtils.LookupAsync(stunServer);
        if (ip == null)
        {
            return new NatTypeTestResult { Result = "Wrong STUN Server!" };
        }

        var bindAddress = IPAddress.IsLoopback(serverIp)
            ? IPAddress.Loopback
            : (serverIp.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any);

        using IUdpProxy proxy = ProxyFactory.CreateProxy(ProxyType.Socks5, new IPEndPoint(bindAddress, 0), socks5Option);
        using var client = new StunClient5389UDP(new IPEndPoint(ip, port), local, proxy);

        await client.ConnectProxyAsync(ctx);
        try
        {
            await client.QueryAsync(ctx);
        }
        finally
        {
            await client.CloseProxyAsync(ctx);
        }

        var res = client.State;
        var result = GetSimpleResult(res);
        var mapping = res.MappingBehavior.ToString();
        var filtering = res.FilteringBehavior.ToString();
        string classic = (res.BindingTestResult, res.MappingBehavior, res.FilteringBehavior) switch
        {
            (BindingTestResult.Fail, _, _) => "UdpBlocked",
            (not BindingTestResult.Success, _, _) => res.BindingTestResult.ToString(),
            (_, MappingBehavior.Direct or MappingBehavior.EndpointIndependent, FilteringBehavior.EndpointIndependent) => "Full Cone",
            (_, MappingBehavior.Direct or MappingBehavior.EndpointIndependent, FilteringBehavior.AddressDependent) => "Restricted Cone",
            (_, MappingBehavior.Direct or MappingBehavior.EndpointIndependent, FilteringBehavior.AddressAndPortDependent) => "Port Restricted Cone",
            (_, MappingBehavior.AddressDependent or MappingBehavior.AddressAndPortDependent, _) => "Symmetric",
            _ => res.FilteringBehavior.ToString()
        };

        return new NatTypeTestResult
        {
            Result = result,
            LocalEnd = res.LocalEndPoint?.ToString(),
            PublicEnd = res.PublicEndPoint?.ToString(),
            MappingBehavior = mapping,
            FilteringBehavior = filtering,
            ClassicNatType = classic
        };
    }

    private static string GetSimpleResult(StunResult5389 res)
    {
        return (res.BindingTestResult, res.MappingBehavior, res.FilteringBehavior)
switch
        {
            (BindingTestResult.Fail, _, _) => "NoUDP",
            (not BindingTestResult.Success, _, _) => res.BindingTestResult.ToString(),
            (_, MappingBehavior.Direct or MappingBehavior.EndpointIndependent, FilteringBehavior.EndpointIndependent) => "1",
            (_, MappingBehavior.Direct or MappingBehavior.EndpointIndependent, _) => "2",
            (_, MappingBehavior.AddressDependent or MappingBehavior.AddressAndPortDependent, _) => "3",
            (_, MappingBehavior.Fail, _) => MappingBehavior.Fail.ToString(),
            _ => res.FilteringBehavior.ToString(),
        };
    }

    public static async Task<int?> HttpConnectAsync(Socks5Server socks5, CancellationToken ctx)
    {
        var serverIp = await DnsUtils.LookupAsync(socks5.Hostname);
        if (serverIp == null)
            return null;

        var socks5Option = new Socks5CreateOption
        {
            Address = serverIp,
            Port = socks5.Port,
            UsernamePassword = socks5.Auth()
                ? new UsernamePassword
                {
                    UserName = socks5.Username,
                    Password = socks5.Password
                }
                : null
        };

        var stopwatch = Stopwatch.StartNew();

        var result = await Socks5.Utils.Socks5TestUtils.Socks5ConnectAsync(socks5Option, token: ctx);

        stopwatch.Stop();
        if (result)
            return Convert.ToInt32(stopwatch.Elapsed.TotalMilliseconds);

        return null;
    }
}