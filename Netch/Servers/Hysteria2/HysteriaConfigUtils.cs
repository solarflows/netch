using Netch.Models;
using Netch.Utils;
using System;
using System.Formats.Asn1;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

#pragma warning disable VSTHRD200

namespace Netch.Servers;

public static class HysteriaConfigUtils
{
    public static async Task<SingboxConfig> GenerateClientConfigAsync(Hysteria2Server server)
    {
        var hysteriaConfig = new SingboxConfig
        {
            inbounds = new object[]
            {
                new
                {
                    type = "mixed",
                    tag = "mixed-in",
                    listen = Global.Settings.LocalAddress,
                    listen_port = Global.Settings.Socks5LocalPort,
                    tcp_fast_open = true,
                    udp_fragment = true,
                    sniff = true,
                    set_system_proxy = true
                }
            }
        };

        hysteriaConfig.outbounds = await outbound(server);
        hysteriaConfig.dns = await dnss(server);
        hysteriaConfig.route = await routes(server);
        hysteriaConfig.experimental = new
        {
            cache_file = new
            {
                enabled = true,
            }
        };
        return hysteriaConfig;
    }

    private static async Task<object> outbound(Hysteria2Server server)
    {
        var outbound = new List<object>()
        {
            new SB_Outbound
            {
                type = "hysteria2",
                tag = "proxy",
                server = await server.AutoResolveHostnameAsync(),
                server_port = server.Port,
                password = server.Password,
                obfs = new SB_OBFS
                {
                    type = "salamander",
                    password = server.OBFSParam
                },
                tls = new SB_TLS
                {
                    enabled = true,
                    server_name = server.SNI,
                    insecure = true
                }
            },
            new
            {
                type = "direct",
                tag = "direct"
            },
            new
            {
                type = "block",
                tag = "block"
            },
            new
            {
                type = "dns",
                tag = "dns-out"
            }
        };
        return outbound;
    }

    // sing-box 1.8.0以上
    private static async Task<SB_DNS> dnss(Hysteria2Server server)
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
    private static async Task<SB_ROUTE> routes(Hysteria2Server server)
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
                new
                {
                    domain_suffix = new object[]
                    {
                        ".superego"
                    },
                    outbound = "direct"
                },
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

}