using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Netch.Models;
using Netch.Servers;
using Netch.Utils;

namespace Netch.Servers.Singbox;

public static class SingboxConfigUtils
{
    public static async Task<Dictionary<string, object>> GenerateClientConfigAsync(Server server)
    {
        var localPort = Global.Settings.Socks5LocalPort;
        var localAddress = Global.Settings.LocalAddress;
        var sboxCfg = Global.Settings.SingboxConfig;

        var inbounds = new List<object>
        {
            new Dictionary<string, object>
            {
                { "type", "mixed" },
                { "tag", "mixed-in" },
                { "listen", localAddress },
                { "listen_port", localPort },
                { "sniff", sboxCfg.Sniffing }
            }
        };

        if (Global.Settings.V2RayConfig.AllowHttp)
        {
            inbounds.Add(new Dictionary<string, object>
            {
                { "type", "http" },
                { "tag", "http-in" },
                { "listen", localAddress },
                { "listen_port", localPort + 1 }
            });
        }

        List<object> outbounds;
        if (server is UrlTestServer urlTest)
        {
            var candidateTags = new List<string>();
            var candidateOutbounds = new List<object>();

            IEnumerable<Server> candidates;
            if (urlTest.UseCustomFilter)
            {
                candidates = FilterCandidatesByRules(urlTest);
            }
            else
            {
                candidates = urlTest.Outbounds
                    .Select(r => Global.Settings.Server.FirstOrDefault(s => s.Remark == r))
                    .Where(s => s != null && s is not UrlTestServer)!;
            }

            int nodeIndex = 0;
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate is UrlTestServer)
                    continue;

                nodeIndex++;
                var tag = $"node-{nodeIndex}";
                candidateTags.Add(tag);
                var nodeOutbound = await GenerateOutboundAsync(candidate, tag);
                candidateOutbounds.Add(nodeOutbound);
            }

            var urltestOutbound = new Dictionary<string, object>
            {
                { "type", "urltest" },
                { "tag", "proxy" },
                { "outbounds", candidateTags },
                { "url", urlTest.Url },
                { "interval", urlTest.Interval },
                { "tolerance", urlTest.Tolerance },
                { "idle_timeout", urlTest.IdleTimeout },
                { "interrupt_exist_connections", urlTest.InterruptExistConnections }
            };

            outbounds = new List<object>
            {
                urltestOutbound
            };
            outbounds.AddRange(candidateOutbounds);
            outbounds.Add(new Dictionary<string, object>
            {
                { "type", "direct" },
                { "tag", "direct" }
            });
            outbounds.Add(new Dictionary<string, object>
            {
                { "type", "block" },
                { "tag", "block" }
            });
        }
        else
        {
            outbounds = new List<object>
            {
                await GenerateOutboundAsync(server, "proxy"),
                new Dictionary<string, object>
                {
                    { "type", "direct" },
                    { "tag", "direct" }
                },
                new Dictionary<string, object>
                {
                    { "type", "block" },
                    { "tag", "block" }
                }
            };
        }

        var dns = new Dictionary<string, object>
        {
            {
                "servers", new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "tag", "dns-remote" },
                        { "address", "tcp://1.1.1.1" },
                        { "detour", "proxy" }
                    },
                    new Dictionary<string, object>
                    {
                        { "tag", "dns-local" },
                        { "address", "local" },
                        { "detour", "direct" }
                    }
                }
            },
            {
                "rules", new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "outbound", "any" },
                        { "server", "dns-local" }
                    }
                }
            },
            { "strategy", "prefer_ipv4" }
        };

        var config = new Dictionary<string, object>
        {
            {
                "log", new Dictionary<string, object>
                {
                    { "level", "info" },
                    { "timestamp", true }
                }
            },
            { "dns", dns },
            { "inbounds", inbounds },
            { "outbounds", outbounds },
            {
                "route", new Dictionary<string, object>
                {
                    { "auto_detect_interface", true },
                    { "final", "proxy" }
                }
            }
        };

        return config;
    }

    private static async Task<Dictionary<string, object>> GenerateOutboundAsync(Server server, string tag = "proxy")
    {
        var outbound = new Dictionary<string, object>
        {
            { "tag", tag }
        };

        string resolvedAddress = server.Hostname;
        try
        {
            var res = await server.AutoResolveHostnameAsync();
            if (!string.IsNullOrWhiteSpace(res))
            {
                resolvedAddress = res;
            }
        }
        catch
        {
            resolvedAddress = server.Hostname;
        }

        // sing-box 的 server 字段若是纯 IPv6 地址，必须是不带方括号的纯地址
        resolvedAddress = resolvedAddress.Trim('[', ']');

        var sboxCfg = Global.Settings.SingboxConfig;
        bool allowInsecure = sboxCfg.AllowInsecure || server.AllowInsecure == true;

        if (sboxCfg.TCPFastOpen)
        {
            outbound["tcp_fast_open"] = true;
        }

        switch (server)
        {
            case VisionServer vision:
            {
                outbound["type"] = "vless";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = vision.Port;
                outbound["uuid"] = CoreConfig.GetUUID(vision.UserID);
                outbound["flow"] = !string.IsNullOrWhiteSpace(vision.Flow) ? vision.Flow : "xtls-rprx-vision";
                outbound["packet_encoding"] = vision.PacketEncoding != "none" ? vision.PacketEncoding : "xudp";

                var tls = new Dictionary<string, object>
                {
                    { "enabled", vision.TLSSecureType != "none" },
                    { "insecure", allowInsecure }
                };

                var serverName = vision.ServerName.ValueOrDefault() ?? vision.Host.SplitOrDefault()?[0] ?? vision.Hostname;
                if (!string.IsNullOrWhiteSpace(serverName))
                    tls["server_name"] = serverName;

                var fp = !string.IsNullOrWhiteSpace(vision.Fingerprint) ? vision.Fingerprint : "chrome";
                tls["utls"] = new Dictionary<string, object>
                {
                    { "enabled", true },
                    { "fingerprint", fp }
                };

                if (vision.TLSSecureType == "reality")
                {
                    tls["reality"] = new Dictionary<string, object>
                    {
                        { "enabled", true },
                        { "public_key", vision.PublicKey ?? "" },
                        { "short_id", vision.ShortId ?? "" }
                    };
                }

                outbound["tls"] = tls;
                break;
            }

            case VLESSServer vless:
            {
                outbound["type"] = "vless";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = vless.Port;
                outbound["uuid"] = CoreConfig.GetUUID(vless.UserID);
                if (!string.IsNullOrWhiteSpace(vless.FlowControl))
                    outbound["flow"] = vless.FlowControl;
                outbound["packet_encoding"] = vless.PacketEncoding != "none" ? vless.PacketEncoding : "xudp";

                if (vless.TLSSecureType != "none")
                {
                    var tls = new Dictionary<string, object>
                    {
                        { "enabled", true },
                        { "insecure", allowInsecure }
                    };

                    var serverName = vless.ServerName.ValueOrDefault() ?? vless.Host.SplitOrDefault()?[0] ?? vless.Hostname;
                    if (!string.IsNullOrWhiteSpace(serverName))
                        tls["server_name"] = serverName;

                    outbound["tls"] = tls;
                }

                if (sboxCfg.UseMux && string.IsNullOrWhiteSpace(vless.FlowControl))
                {
                    ApplyMultiplex(outbound);
                }

                AttachTransport(outbound, vless.TransferProtocol, vless.Path, vless.Host, vless.Hostname);
                break;
            }

            case VMessServer vmess:
            {
                outbound["type"] = "vmess";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = vmess.Port;
                outbound["uuid"] = CoreConfig.GetUUID(vmess.UserID);
                outbound["alter_id"] = vmess.AlterID;
                outbound["security"] = vmess.EncryptMethod == "auto" ? "auto" : vmess.EncryptMethod;
                outbound["packet_encoding"] = vmess.PacketEncoding != "none" ? vmess.PacketEncoding : "xudp";

                if (vmess.TLSSecureType != "none")
                {
                    var tls = new Dictionary<string, object>
                    {
                        { "enabled", true },
                        { "insecure", allowInsecure }
                    };

                    var serverName = vmess.ServerName.ValueOrDefault() ?? vmess.Host.SplitOrDefault()?[0] ?? vmess.Hostname;
                    if (!string.IsNullOrWhiteSpace(serverName))
                        tls["server_name"] = serverName;

                    outbound["tls"] = tls;
                }

                if (sboxCfg.UseMux)
                {
                    ApplyMultiplex(outbound);
                }

                AttachTransport(outbound, vmess.TransferProtocol, vmess.Path, vmess.Host, vmess.Hostname);
                break;
            }

            case TrojanServer trojan:
            {
                outbound["type"] = "trojan";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = trojan.Port;
                outbound["password"] = trojan.Password;

                var tls = new Dictionary<string, object>
                {
                    { "enabled", trojan.TLSSecureType != "none" },
                    { "insecure", allowInsecure }
                };

                var serverName = trojan.Host.ValueOrDefault() ?? trojan.Hostname;
                if (!string.IsNullOrWhiteSpace(serverName))
                    tls["server_name"] = serverName;

                outbound["tls"] = tls;

                if (sboxCfg.UseMux)
                {
                    ApplyMultiplex(outbound);
                }

                AttachTransport(outbound, trojan.TransferProtocol, trojan.Path, trojan.Host, trojan.Hostname, trojan.ServiceName);
                break;
            }

            case ShadowsocksServer ss:
            {
                outbound["type"] = "shadowsocks";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = ss.Port;
                outbound["method"] = ss.EncryptMethod;
                outbound["password"] = ss.Password;

                if (!string.IsNullOrWhiteSpace(ss.Plugin))
                {
                    outbound["plugin"] = ss.Plugin;
                    outbound["plugin_opts"] = ss.PluginOption ?? "";
                }

                if (sboxCfg.UseMux)
                {
                    ApplyMultiplex(outbound);
                }

                break;
            }

            case WireGuardServer wg:
            {
                outbound["type"] = "wireguard";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = wg.Port;
                outbound["system_interface"] = false;
                outbound["local_address"] = wg.LocalAddresses.SplitOrDefault() ?? new[] { "172.16.0.2/32" };
                outbound["private_key"] = wg.PrivateKey;
                outbound["peer_public_key"] = wg.PeerPublicKey;
                outbound["mtu"] = wg.MTU;

                if (!string.IsNullOrWhiteSpace(wg.PreSharedKey))
                    outbound["pre_shared_key"] = wg.PreSharedKey;

                if (!string.IsNullOrWhiteSpace(wg.Reserved))
                {
                    var parts = wg.Reserved.Split(',');
                    if (parts.Length == 3 && parts.All(p => int.TryParse(p.Trim(), out _)))
                    {
                        outbound["reserved"] = parts.Select(p => int.Parse(p.Trim())).ToArray();
                    }
                }

                break;
            }

            case Socks5Server socks:
            {
                outbound["type"] = "socks";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = socks.Port;
                outbound["version"] = socks.Version switch { "4" => "4", "4a" => "4a", _ => "5" };
                if (socks.Auth())
                {
                    outbound["username"] = socks.Username ?? "";
                    outbound["password"] = socks.Password ?? "";
                }

                break;
            }

            case SSHServer ssh:
            {
                outbound["type"] = "ssh";
                outbound["server"] = resolvedAddress;
                outbound["server_port"] = ssh.Port > 0 ? ssh.Port : 22;
                outbound["user"] = !string.IsNullOrWhiteSpace(ssh.User) ? ssh.User : "root";
                if (!string.IsNullOrWhiteSpace(ssh.Password))
                {
                    outbound["password"] = ssh.Password;
                }
                if (!string.IsNullOrWhiteSpace(ssh.PrivateKey))
                {
                    outbound["private_key"] = ssh.PrivateKey;
                }
                if (!string.IsNullOrWhiteSpace(ssh.PublicKey))
                {
                    outbound["host_key"] = new[] { ssh.PublicKey };
                }
                break;
            }

            default:
                throw new NotSupportedException($"Server type {server.Type} is not supported by sing-box.");
        }

        return outbound;
    }

    private static void ApplyMultiplex(Dictionary<string, object> outbound)
    {
        outbound["multiplex"] = new Dictionary<string, object>
        {
            { "enabled", true },
            { "protocol", "smux" },
            { "max_connections", 4 }
        };
    }

    /// <summary>
    ///     格式化 HTTP / WebSocket 请求头中的 Host 字段。
    ///     根据 RFC 3986 规范，若目标为 IPv6 地址，必须包裹方括号 [IPv6]，杜绝 Nginx 404
    /// </summary>
    private static string FormatHostHeader(string? host, string fallbackHostname)
    {
        var raw = !string.IsNullOrWhiteSpace(host) ? host.Trim() : fallbackHostname.Trim();

        // 已包含方括号则直接返回
        if (raw.StartsWith('[') && raw.EndsWith(']'))
            return raw;

        // 若为纯 IPv6 地址，包裹为 [IPv6]
        if (IPAddress.TryParse(raw, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return $"[{ip}]";
        }

        return raw;
    }

    private static void AttachTransport(Dictionary<string, object> outbound, string? protocol, string? path, string? host, string hostname, string? serviceName = null)
    {
        var proto = protocol?.ToLowerInvariant() ?? "tcp";

        switch (proto)
        {
            case "ws":
            case "websocket":
            {
                var transport = new Dictionary<string, object>
                {
                    { "type", "ws" },
                    { "path", !string.IsNullOrWhiteSpace(path) ? path : "/" }
                };

                var hostHeader = FormatHostHeader(host, hostname);
                if (!string.IsNullOrWhiteSpace(hostHeader))
                {
                    transport["headers"] = new Dictionary<string, string>
                    {
                        { "Host", hostHeader }
                    };
                }

                outbound["transport"] = transport;
                break;
            }

            case "grpc":
            {
                outbound["transport"] = new Dictionary<string, object>
                {
                    { "type", "grpc" },
                    { "service_name", serviceName ?? path ?? "" }
                };
                break;
            }

            case "http":
            case "h2":
            {
                var hostList = !string.IsNullOrWhiteSpace(host)
                    ? host.SplitOrDefault() ?? new[] { FormatHostHeader(host, hostname) }
                    : new[] { FormatHostHeader(host, hostname) };

                outbound["transport"] = new Dictionary<string, object>
                {
                    { "type", "http" },
                    { "path", !string.IsNullOrWhiteSpace(path) ? path : "/" },
                    { "host", hostList }
                };
                break;
            }
        }
    }

    public static List<Server> FilterCandidatesByRules(UrlTestServer urlTest)
    {
        var query = Global.Settings.Server.Where(s => s is not UrlTestServer);

        if (!string.IsNullOrWhiteSpace(urlTest.MatchGroup) && !urlTest.MatchGroup.Equals("全部", StringComparison.OrdinalIgnoreCase) && !urlTest.MatchGroup.Equals("All Groups", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Group.Equals(urlTest.MatchGroup, StringComparison.OrdinalIgnoreCase));
        }

        Regex? incRegex = null;
        if (!string.IsNullOrWhiteSpace(urlTest.IncludePattern))
        {
            try { incRegex = new Regex(urlTest.IncludePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
            catch { /* fallback */ }
        }

        Regex? excRegex = null;
        if (!string.IsNullOrWhiteSpace(urlTest.ExcludePattern))
        {
            try { excRegex = new Regex(urlTest.ExcludePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
            catch { /* fallback */ }
        }

        return query.Where(s =>
        {
            if (excRegex != null && excRegex.IsMatch(s.Remark))
                return false;

            if (incRegex != null && !incRegex.IsMatch(s.Remark))
                return false;

            return true;
        }).ToList();
    }

    public static bool IsSupported(Server server)
    {
        return server switch
        {
            VisionServer => true,
            VLESSServer => true,
            VMessServer => true,
            TrojanServer => true,
            ShadowsocksServer => true,
            WireGuardServer => true,
            Socks5Server => true,
            SSHServer => true,
            UrlTestServer => true,
            _ => false
        };
    }
}
