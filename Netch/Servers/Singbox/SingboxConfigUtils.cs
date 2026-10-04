using System.Text.Json;
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

            int nodeIndex = 0;
            foreach (var remark in urlTest.Outbounds)
            {
                var candidate = Global.Settings.Server.FirstOrDefault(s => s.Remark == remark);
                if (candidate != null && candidate is not UrlTestServer)
                {
                    nodeIndex++;
                    var tag = $"node-{nodeIndex}";
                    candidateTags.Add(tag);
                    var nodeOutbound = await GenerateOutboundAsync(candidate, tag);
                    candidateOutbounds.Add(nodeOutbound);
                }
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

        var config = new Dictionary<string, object>
        {
            {
                "log", new Dictionary<string, object>
                {
                    { "level", "info" },
                    { "timestamp", true }
                }
            },
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

        var resolvedAddress = await server.AutoResolveHostnameAsync();
        var sboxCfg = Global.Settings.SingboxConfig;

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
                    { "insecure", sboxCfg.AllowInsecure }
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
                        { "insecure", sboxCfg.AllowInsecure }
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

                AttachTransport(outbound, vless);
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
                        { "insecure", sboxCfg.AllowInsecure }
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

                AttachTransport(outbound, vmess);
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
                    { "insecure", sboxCfg.AllowInsecure }
                };

                var serverName = trojan.Host.ValueOrDefault() ?? trojan.Hostname;
                if (!string.IsNullOrWhiteSpace(serverName))
                    tls["server_name"] = serverName;

                outbound["tls"] = tls;

                if (sboxCfg.UseMux)
                {
                    ApplyMultiplex(outbound);
                }

                if (trojan.Mode?.Equals("grpc", StringComparison.OrdinalIgnoreCase) == true)
                {
                    outbound["transport"] = new Dictionary<string, object>
                    {
                        { "type", "grpc" },
                        { "service_name", trojan.ServiceName ?? "" }
                    };
                }

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

    private static void AttachTransport(Dictionary<string, object> outbound, VMessServer server)
    {
        switch (server.TransferProtocol)
        {
            case "ws":
            {
                var transport = new Dictionary<string, object>
                {
                    { "type", "ws" },
                    { "path", server.Path.ValueOrDefault() ?? "/" }
                };

                if (!server.Host.IsNullOrWhiteSpace())
                {
                    transport["headers"] = new Dictionary<string, string>
                    {
                        { "Host", server.Host! }
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
                    { "service_name", server.Path ?? "" }
                };
                break;
            }

            case "http":
            case "h2":
            {
                outbound["transport"] = new Dictionary<string, object>
                {
                    { "type", "http" },
                    { "path", server.Path.ValueOrDefault() ?? "/" },
                    { "host", server.Host.SplitOrDefault() ?? Array.Empty<string>() }
                };
                break;
            }
        }
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
            UrlTestServer => true,
            _ => false
        };
    }
}
