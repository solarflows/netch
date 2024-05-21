
using Netch.Models;
using Netch.Properties;
using Netch.Utils;

namespace Netch.Servers;

public static class XrayConfigUtils
{
    public static async Task<XrayConfig> GenerateClientConfigAsync(Server server)
    {
        var xrayConfig = new XrayConfig
        {
            log = new {
                access = "",
                error = "",
                loglevel = "warning"
            },

            inbounds = new object[]
            {
                new
                {
                    tag = "scoks",
                    port = Global.Settings.Socks5LocalPort,
                    listen = Global.Settings.LocalAddress,
                    protocol = "socks",
                    sniffing = new {
                        enabled = true,
                        destOverride = new object[]
                        {
                            "http",
                            "tls"
                        },
                        routeOnly = false
                    },
                    settings = new
                    {
                        auth = "noauth",
                        udp = true,
                        allowTransparent = false
                    }
                }
            }
        };

        xrayConfig.outbounds = await outbound(server);
        xrayConfig.dns = await dnss(server);
        xrayConfig.routing = await routes(server);

        return xrayConfig;
    }

    private static async Task<object> outbound(Server server)
    {
        var outBound = new List<object>() { };

        switch (server)
        {
            case Socks5Server socks:
                break;
            case VLESSServer vless:
                var vlessOut = new
                {
                    tag = "proxy",
                    protocol = "vless",
                    settings = new
                    {
                        vnext = new object[]
                        {
                            new
                            {
                                address = await server.AutoResolveHostnameAsync(),
                                port = server.Port,

                                users = new object[]
                                {
                                    new
                                    {
                                        id = getUUID(vless.UserID),
                                        alterId = 0,
                                        security  = "auto",
                                        encryption = vless.EncryptMethod,
                                        flow = vless.Flow
                                    }
                                }
                            }
                        }
                    },
                    streamSettings = boundStreamSettings(vless),
                    mux = new
                    {
                        enabled = true,
                        concurrency = -1
                    }
                };

                outBound.Add(
                    vlessOut
                );
                break;
            case VMessServer vmess:
                var vmessOut = new
                {
                    tag = "proxy",
                    protocol = "vmess",
                    settings = new
                    {
                        vnext = new object[]
                        {
                            new
                            {
                                address = await server.AutoResolveHostnameAsync(),
                                port = server.Port,

                                users = new object[]
                                {
                                    new
                                    {
                                        id = getUUID(vmess.UserID),
                                        alterId = 0,
                                        security  = "auto",
                                        encryption = vmess.EncryptMethod
                                    }
                                }
                            }
                        }
                    },
                    streamSettings = boundStreamSettings(vmess),
                    mux = new
                    {
                        enabled = true,
                        concurrency = -1
                    }
                };

                outBound.Add(
                    vmessOut
                );
                break;
            case ShadowsocksServer ss:

                break;
            case ShadowsocksRServer ssr:

                break;
            case TrojanServer trojan:
                var trojanOut = new
                {
                    tag = "proxy",
                    protocol = "vmess",
                    settings = new
                    {
                        servers = new object[]
                        {
                            new
                            {
                                address = await server.AutoResolveHostnameAsync(),
                                port = server.Port,
                                password =trojan.Password

                            }
                        }
                    },
                    streamSettings = boundStreamSettings(trojan),
                    mux = new
                    {
                        enabled = true,
                        concurrency = -1
                    }
                };

                outBound.Add(
                    trojanOut
                );
                break;
            case WireGuardServer wg:

                break;
        }

        outBound.Add(
            new
            {
                tag = "direct",
                protocol = "freedom",
                settings = new { }
            }
        );
        outBound.Add(
            new
            {
                tag = "block",
                protocol = "blackhole",
                settings = new {
                    response = new
                    {
                        type = "http"
                    }
                }
            }
        );

        return outBound;
    }

    private static Object boundStreamSettings(Server server)
    {
        // https://xtls.github.io/Xray-docs-next/config/transport.html
        var streamSettings = new Dictionary<string, object>();

        switch (server)
        {
            case VLESSServer vless:
                streamSettings.Add("network", vless.TransferProtocol);
                streamSettings.Add("security", vless.TLSSecureType);

                if (vless.TLSSecureType != "none")
                {
                    var tlsSettings = new
                    {
                        allowInsecure = Global.Settings.V2RayConfig.AllowInsecure,
                        serverName = vless.ServerName == "" ? vless.Hostname : vless.ServerName,
                        show = false
                    };

                    switch (vless.TLSSecureType)
                    {
                        case "tls":
                            streamSettings.Add("tlsSettings", tlsSettings);
                            break;
                        case "xtls":
                            streamSettings.Add("xtlsSettings", tlsSettings);
                            break;
                    }
                }
                break;
            case VMessServer vmess:
                streamSettings.Add("network", vmess.TransferProtocol);
                streamSettings.Add("security", vmess.TLSSecureType);

                if (vmess.TLSSecureType != "none")
                {
                    var tlsSettings = new
                    {
                        allowInsecure = Global.Settings.V2RayConfig.AllowInsecure,
                        serverName = vmess.ServerName == "" ? vmess.Hostname : vmess.ServerName,
                        show = false
                    };

                    switch (vmess.TLSSecureType)
                    {
                        case "tls":
                            streamSettings.Add("tlsSettings", tlsSettings);
                            break;
                        case "xtls":
                            streamSettings.Add("xtlsSettings", tlsSettings);
                            break;
                    }
                }
                break;
        }

        if (Global.Settings.V2RayConfig.TCPFastOpen)
        {
            streamSettings.Add("sockopt", new Sockopt
            {
                tcpFastOpen = true
            });
        }

        return streamSettings;
    }

    
    private static async Task<X_DNS> dnss(Server server)
    {
        var tmp = new X_DNS
        {
            servers = new List<object> () { }
        };

        if (Global.Settings.TUNTAP.DNS.Length != 0)
        {
            tmp.servers.Add(Global.Settings.TUNTAP.DNS);
        }

        return tmp;
    }

    private static async Task<X_Route> routes(Server server)
    {
        // https://xtls.github.io/Xray-docs-next/config/routing.html#routingobject
        var routes = new X_Route() {
            domainStrategy = "AsIs",
            rules = new List<object>() {
                new
                {
                    type = "field",
                    inboundTag = new object[]
                    {
                        "api"
                    },
                    outboundTag = "api"
                }
            }
        };

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