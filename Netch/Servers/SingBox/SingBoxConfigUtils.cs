using Netch.Models;
using Netch.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Runtime.Serialization;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;


namespace Netch.Servers;

public static class SingBoxConfigUtils
{
    public static async Task<SingboxConfig> GenerateClientConfigAsync(Server server)
    {
        var singBoxConfig = new SingboxConfig
        {
            inbounds = new object[]
            {
                new
                {
                    type = "socks",
                    tag = "mixed-in",
                    listen = Global.Settings.LocalAddress,
                    listen_port = Global.Settings.Socks5LocalPort,
                    tcp_fast_open = true,
                    udp_fragment = true,
                    sniff = true,
                    //sniff_override_destination = true
                }
            }
        };

        singBoxConfig.outbounds = await outbound(server);
        singBoxConfig.dns = await dnss(server);
        singBoxConfig.route = await routes(server);
        singBoxConfig.experimental = new
        {
            cache_file = new
            {
                enabled = true,
            }
        };
        return singBoxConfig;
    }

    private static async Task<object> outbound(Server server)
    {
        var outBound = new List<object>(){};

        switch (server)
        {
            case Socks5Server socks:

                break;
            case VLESSServer vless:
                var sbOut = new SB_Outbound
                {
                    type = "vless",
                    tag = "proxy",
                    server = await server.AutoResolveHostnameAsync(),
                    server_port = server.Port,
                    
                    uuid = getUUID(vless.UserID),
                    flow = vless.Flow,
                    network = vless.TransferProtocol,
                    tls = new SB_TLS
                    {
                        enabled = vless.TLSSecureType == "tls" ? true : false,
                        server_name = vless.ServerName == ""? vless.Hostname : vless.ServerName,
                        insecure = true
                    },
                    packet_encoding = "",
                    multiplex = new Dictionary<string, object>(),
                    transport = new Dictionary<string, object>()
                };
                switch (vless.TransferProtocol)
                {
                    case "ws":
                        sbOut.network = "tcp";
                        sbOut.transport.Add("type", "ws");
                        break;
                    case "grpc":
                        sbOut.network = "udp";
                        sbOut.transport.Add("type", "grpc");
                        break;
                }

                outBound.Add(
                    sbOut
                );
                break;
            case VMessServer vmess:
                sbOut = new SB_Outbound
                {
                    type = "vmess",
                    tag = "proxy",
                    server = await server.AutoResolveHostnameAsync(),
                    server_port = server.Port,

                    security = vmess.EncryptMethod,

                    alter_id = vmess.AlterID,
                    uuid = getUUID(vmess.UserID),
                    network = vmess.TransferProtocol,
                    tls = new SB_TLS
                    {
                        enabled = vmess.TLSSecureType == "tls" ? true : false,
                        server_name = vmess.ServerName == "" ? vmess.Hostname : vmess.ServerName
                    },
                    packet_encoding = "",
                    multiplex = new Dictionary<string, object>(),
                    transport = new Dictionary<string, object>()
                };

                switch (vmess.TransferProtocol)
                {
                    case "ws":
                        sbOut.network = "tcp";
                        sbOut.transport.Add("type", "ws");
                        break;
                    case "grpc":
                        sbOut.network = "udp";
                        sbOut.transport.Add("type", "grpc");
                        break;
                }

                outBound.Add(
                    sbOut
                );
                break;
            case ShadowsocksServer ss:
                
                break;
            case ShadowsocksRServer ssr:
                
                break;
            case TrojanServer trojan:
                sbOut = new SB_Outbound
                {
                    type = "trojan",
                    tag = "proxy",
                    server = await server.AutoResolveHostnameAsync(),
                    server_port = server.Port,

                    password = getUUID(trojan.Password),
                    tls = new SB_TLS
                    {
                        enabled = trojan.TLSSecureType == "tls" ? true : false,
                        server_name = trojan.Hostname,
                        insecure = true
                    }
                };
                outBound.Add(
                    sbOut
                );
                break;
            case WireGuardServer wg:
                
                break;
            case SSHServer ssh:
                break;
            case Hysteria2Server hy:
                outBound.Add(
                    new SB_Outbound
                    {
                        type = "hysteria2",
                        tag = "proxy",
                        server = await server.AutoResolveHostnameAsync(),
                        server_port = server.Port,
                        password = hy.Password,
                        up_mbps = 0,
                        down_mbps = 0,
                        obfs = new SB_OBFS
                        {
                            type = "salamander",
                            password = hy.OBFSParam
                        },
                        tls = new SB_TLS
                        {
                            enabled = true,
                            server_name = hy.SNI,
                            insecure = true
                        }
                    }
                );
                break;
        }
        outBound.Add(
            new
            {
                type = "direct",
                tag = "direct"
            }
        );
        outBound.Add(
            new
            {
                type = "block",
                tag = "block"
            }
        );
        outBound.Add(
            new
            {
                type = "dns",
                tag = "dns-out"
            }
        );

        return outBound;
    }

    // sing-box 1.8.0以上
    private static async Task<SB_DNS> dnss(Server server)
    {
        var dnss = new SB_DNS
        {
            servers = new List<object>()
            {
                new
                {
                    tag = "local",
                    address = "local",
                    detour = "direct"
                },
                new
                {
                    tag = "cloudflare",
                    address = "https://1.1.1.1/dns-query",
                    detour = "proxy"
                },
                new
                {
                    tag = "block",
                    address = "rcode://success"
                },

            },

            rules = new List<object>()
        };

        if (Global.Settings.TUNTAP.DNS.Length != 0)
        {
            var customDNS = new
            {
                tag = "custom",
                address = Global.Settings.TUNTAP.DNS,
                detour = "direct"
            };

            dnss.servers.Add(customDNS);
        }

        return dnss;
    }

    // sing-box 1.8.0以上
    private static async Task<SB_ROUTE> routes(Server server)
    {
        var routes = new SB_ROUTE
        {
            rules = new List<object>()
            {
                new {
                    inbound = new object[]
                    {
                        "mixed-in"
                    },
                    ip_cidr = new object[]
                    {
                        "10.0.0.0/8",
                        "172.16.0.0/12",
                        "192.168.0.0/24"
                    },
                    ip_is_private = true,
                    rule_set_ipcidr_match_source = false,
                    invert = false,
                    domain = new object[]
                    {
                    },
                    domain_suffix = new object[]
                    {
                        ".cn"
                    },
                    outbound = "direct"
                },
                /*new
                {
                    domain_suffix = new object[]
                    {
                       
                    },
                    outbound = "direct"
                },*/
                new
                {
                    ip_is_private = true,
                    outbound = "direct"
                },
                new {
                    rule_set = "geoip-cn",
                    outbound = "direct"
                },
                new {
                    rule_set = "geosite-cn",
                    outbound = "direct"
                },
                new {
                    rule_set = "geoip-us",
                    rule_set_ipcidr_match_source = true,
                    outbound = "proxy"
                }
            },
            rule_set = new List<object>()
            {
                new
                {
                    tag = "geoip-cn",
                    type = "remote",
                    format = "binary",
                    url = "https://raw.githubusercontent.com/SagerNet/sing-geoip/rule-set/geoip-cn.srs",
                    download_detour = "proxy"
                },
                new
                {
                    tag = "geosite-cn",
                    type = "remote",
                    format = "binary",
                    url = "https://raw.githubusercontent.com/SagerNet/sing-geosite/rule-set/geosite-cn.srs",
                    download_detour = "proxy"
                },
                new
                {
                    tag = "geoip-us",
                    type = "remote",
                    format = "binary",
                    url = "https://raw.githubusercontent.com/SagerNet/sing-geoip/rule-set/geoip-us.srs",
                    download_detour = "proxy"
                }
            },
            auto_detect_interface = true,
            final = "proxy"
        };

        if (Global.Settings.TUNTAP.BypassIPs.Count != 0)
        {
            routes.rules.Add(
                new
                {
                    ip_cidr = Global.Settings.TUNTAP.BypassIPs,
                    outbound = "direct"
                }
            );
        }

        return routes;
    }

    public static string getUUID(string uuid)
    {
        if (uuid.Length == 36 || uuid.Length == 32)
        {
            return uuid;
        }
        return uuid.GenerateUUIDv5();
    }
}

