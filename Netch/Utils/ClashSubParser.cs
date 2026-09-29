using System.Text.Json;
using Netch.Models;
using Netch.Servers;

namespace Netch.Utils;

public static class ClashSubParser
{
    public static bool IsClashYaml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var line in text.GetLines())
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
                continue;

            if (trimmed.StartsWith("proxies:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("proxy:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static List<Server> ParseYaml(string yamlText)
    {
        var servers = new List<Server>();
        var lines = yamlText.GetLines().ToList();

        var inProxies = false;
        var currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string currentParentPrefix = "";

        void FlushCurrent()
        {
            if (currentDict.Count > 0)
            {
                var s = ConvertDictToServer(currentDict);
                if (s != null)
                    servers.Add(s);

                currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                currentParentPrefix = "";
            }
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (!inProxies)
            {
                if (trimmed.StartsWith("proxies:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("proxy:", StringComparison.OrdinalIgnoreCase))
                {
                    inProxies = true;
                }
                continue;
            }

            // If we hit another root key (no leading whitespace), proxies section has ended
            if (!char.IsWhiteSpace(line[0]))
            {
                FlushCurrent();
                break;
            }

            // Check if this line starts a new proxy item (- ...)
            if (trimmed.StartsWith("- ") || trimmed.Equals("-"))
            {
                FlushCurrent();

                var content = trimmed[2..].Trim();
                if (content.StartsWith('{') && content.EndsWith('}'))
                {
                    // Flow style inline dictionary: - { name: "...", type: ss, ... }
                    ParseInlineDict(content, currentDict);
                    FlushCurrent();
                    continue;
                }

                if (!string.IsNullOrEmpty(content) && content.Contains(':'))
                {
                    ParseKeyValue(content, currentParentPrefix, currentDict);
                }
                continue;
            }

            // Continuation of current proxy in block style
            if (trimmed.Contains(':'))
            {
                var colonIndex = trimmed.IndexOf(':');
                var key = trimmed[..colonIndex].Trim();
                var val = trimmed[(colonIndex + 1)..].Trim();

                if (string.IsNullOrEmpty(val))
                {
                    // Sub-object header (e.g. ws-opts:, reality-opts:, plugin-opts:)
                    currentParentPrefix = key + "-";
                }
                else
                {
                    var fullKey = currentParentPrefix + key;
                    currentDict[fullKey] = Unquote(val);
                    if (!string.IsNullOrEmpty(currentParentPrefix) && !currentDict.ContainsKey(key))
                    {
                        currentDict[key] = Unquote(val);
                    }
                }
            }
        }

        FlushCurrent();
        return servers;
    }

    private static void ParseInlineDict(string inline, Dictionary<string, string> dict)
    {
        var content = inline.Trim('{', '}').Trim();
        var parts = SplitFlowStyle(content);
        foreach (var part in parts)
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = part[..colonIndex].Trim();
                var val = part[(colonIndex + 1)..].Trim();
                dict[key] = Unquote(val);
            }
        }
    }

    private static List<string> SplitFlowStyle(string content)
    {
        var list = new List<string>();
        var inQuotes = false;
        var quoteChar = '\0';
        int start = 0;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (inQuotes)
            {
                if (c == quoteChar)
                    inQuotes = false;
            }
            else
            {
                if (c == '"' || c == '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (c == ',')
                {
                    list.Add(content[start..i].Trim());
                    start = i + 1;
                }
            }
        }

        if (start < content.Length)
        {
            list.Add(content[start..].Trim());
        }

        return list;
    }

    private static void ParseKeyValue(string text, string prefix, Dictionary<string, string> dict)
    {
        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0)
        {
            var key = text[..colonIndex].Trim();
            var val = text[(colonIndex + 1)..].Trim();
            var fullKey = prefix + key;
            dict[fullKey] = Unquote(val);
            if (!string.IsNullOrEmpty(prefix) && !dict.ContainsKey(key))
            {
                dict[key] = Unquote(val);
            }
        }
    }

    private static string Unquote(string val)
    {
        val = val.Trim();
        if ((val.StartsWith('"') && val.EndsWith('"')) || (val.StartsWith('\'') && val.EndsWith('\'')))
        {
            if (val.Length >= 2)
                val = val[1..^1];
        }
        return val.Trim();
    }

    private static Server? ConvertDictToServer(Dictionary<string, string> dict)
    {
        var type = dict.GetValueOrDefault("type")?.ToLowerInvariant();
        var hostname = dict.GetValueOrDefault("server") ?? dict.GetValueOrDefault("hostname");
        var portStr = dict.GetValueOrDefault("port");
        var remark = dict.GetValueOrDefault("name") ?? dict.GetValueOrDefault("remark") ?? "";

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(hostname) || !ushort.TryParse(portStr, out var port))
            return null;

        switch (type)
        {
            case "ss":
            case "shadowsocks":
            {
                return new ShadowsocksServer
                {
                    Hostname = hostname,
                    Port = port,
                    EncryptMethod = dict.GetValueOrDefault("cipher") ?? dict.GetValueOrDefault("method") ?? "chacha20-ietf-poly1305",
                    Password = dict.GetValueOrDefault("password") ?? "",
                    Plugin = dict.GetValueOrDefault("plugin"),
                    PluginOption = dict.GetValueOrDefault("plugin-opts") ?? dict.GetValueOrDefault("plugin_opts"),
                    Remark = remark
                };
            }

            case "ssr":
            case "shadowsocksr":
            {
                return new ShadowsocksRServer
                {
                    Hostname = hostname,
                    Port = port,
                    EncryptMethod = dict.GetValueOrDefault("cipher") ?? dict.GetValueOrDefault("method") ?? "none",
                    Password = dict.GetValueOrDefault("password") ?? "",
                    Protocol = dict.GetValueOrDefault("protocol") ?? "origin",
                    ProtocolParam = dict.GetValueOrDefault("protocol-param") ?? "",
                    OBFS = dict.GetValueOrDefault("obfs") ?? "plain",
                    OBFSParam = dict.GetValueOrDefault("obfs-param") ?? "",
                    Remark = remark
                };
            }

            case "vmess":
            {
                var tls = dict.GetValueOrDefault("tls")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                return new VMessServer
                {
                    Hostname = hostname,
                    Port = port,
                    UserID = dict.GetValueOrDefault("uuid") ?? "",
                    AlterID = int.TryParse(dict.GetValueOrDefault("alterId"), out var aid) ? aid : 0,
                    EncryptMethod = dict.GetValueOrDefault("cipher") ?? "auto",
                    TransferProtocol = dict.GetValueOrDefault("network") ?? "tcp",
                    TLSSecureType = tls ? "tls" : "none",
                    Host = dict.GetValueOrDefault("servername") ?? dict.GetValueOrDefault("sni") ?? dict.GetValueOrDefault("host") ?? "",
                    Path = dict.GetValueOrDefault("ws-path") ?? dict.GetValueOrDefault("path") ?? dict.GetValueOrDefault("serviceName") ?? "/",
                    Remark = remark
                };
            }

            case "vless":
            {
                var flow = dict.GetValueOrDefault("flow") ?? "";
                var isReality = dict.ContainsKey("public-key") ||
                                dict.ContainsKey("reality-opts-public-key") ||
                                dict.GetValueOrDefault("reality")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

                if (flow.Contains("vision", StringComparison.OrdinalIgnoreCase) || isReality)
                {
                    return new VisionServer
                    {
                        Hostname = hostname,
                        Port = port,
                        UserID = dict.GetValueOrDefault("uuid") ?? "",
                        Flow = flow,
                        TLSSecureType = isReality ? "reality" : (dict.GetValueOrDefault("tls")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ? "tls" : "none"),
                        PublicKey = dict.GetValueOrDefault("public-key") ?? dict.GetValueOrDefault("reality-opts-public-key"),
                        ShortId = dict.GetValueOrDefault("short-id") ?? dict.GetValueOrDefault("reality-opts-short-id"),
                        ServerName = dict.GetValueOrDefault("servername") ?? dict.GetValueOrDefault("sni"),
                        Remark = remark
                    };
                }

                return new VLESSServer
                {
                    Hostname = hostname,
                    Port = port,
                    UserID = dict.GetValueOrDefault("uuid") ?? "",
                    FlowControl = flow,
                    TLSSecureType = dict.GetValueOrDefault("tls")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ? "tls" : "none",
                    ServerName = dict.GetValueOrDefault("servername") ?? dict.GetValueOrDefault("sni"),
                    TransferProtocol = dict.GetValueOrDefault("network") ?? "tcp",
                    Remark = remark
                };
            }

            case "trojan":
            {
                return new TrojanServer
                {
                    Hostname = hostname,
                    Port = port,
                    Password = dict.GetValueOrDefault("password") ?? "",
                    Host = dict.GetValueOrDefault("sni") ?? dict.GetValueOrDefault("servername") ?? "",
                    TLSSecureType = "tls",
                    Remark = remark
                };
            }

            case "socks5":
            case "socks":
            {
                return new Socks5Server
                {
                    Hostname = hostname,
                    Port = port,
                    Username = dict.GetValueOrDefault("username") ?? dict.GetValueOrDefault("user"),
                    Password = dict.GetValueOrDefault("password") ?? dict.GetValueOrDefault("pass"),
                    Remark = remark
                };
            }

            case "wireguard":
            {
                return new WireGuardServer
                {
                    Hostname = hostname,
                    Port = port,
                    LocalAddresses = dict.GetValueOrDefault("ip") ?? "172.16.0.2/32",
                    PeerPublicKey = dict.GetValueOrDefault("public-key") ?? "",
                    PrivateKey = dict.GetValueOrDefault("private-key") ?? "",
                    Remark = remark
                };
            }

            default:
                return null;
        }
    }

    public static List<Server> ParseJsonConfig(string text)
    {
        var list = new List<Server>();

        // 1. Try SSD JSON format: [{ "server": ..., "server_port": ... }]
        try
        {
            var ssdList = JsonSerializer.Deserialize<List<ShadowsocksConfig>>(text);
            if (ssdList != null && ssdList.Any())
            {
                list.AddRange(ssdList.Select(server => new ShadowsocksServer
                {
                    Hostname = server.server,
                    Port = server.server_port,
                    EncryptMethod = server.method,
                    Password = server.password,
                    Remark = server.remarks,
                    Plugin = server.plugin,
                    PluginOption = server.plugin_opts
                }));
                return list;
            }
        }
        catch
        {
            // Not SSD JSON
        }

        // 2. Try sing-box JSON configuration with "outbounds"
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("outbounds", out var outbounds) && outbounds.ValueKind == JsonValueKind.Array)
            {
                foreach (var outbound in outbounds.EnumerateArray())
                {
                    var server = ParseSingboxOutbound(outbound);
                    if (server != null)
                        list.Add(server);
                }
            }
        }
        catch
        {
            // Not sing-box JSON
        }

        return list;
    }

    private static Server? ParseSingboxOutbound(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("type", out var typeElem))
            return null;

        var type = typeElem.GetString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type) || type is "direct" or "block" or "dns")
            return null;

        if (!outbound.TryGetProperty("server", out var serverElem) || !outbound.TryGetProperty("server_port", out var portElem))
            return null;

        var hostname = serverElem.GetString();
        var port = (ushort)portElem.GetInt32();
        var tag = outbound.TryGetProperty("tag", out var tagElem) ? tagElem.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(hostname) || port == 0)
            return null;

        switch (type)
        {
            case "shadowsocks":
            {
                return new ShadowsocksServer
                {
                    Hostname = hostname,
                    Port = port,
                    EncryptMethod = outbound.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "",
                    Password = outbound.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "",
                    Plugin = outbound.TryGetProperty("plugin", out var pl) ? pl.GetString() : null,
                    PluginOption = outbound.TryGetProperty("plugin_opts", out var po) ? po.GetString() : null,
                    Remark = tag
                };
            }

            case "trojan":
            {
                var serverName = "";
                if (outbound.TryGetProperty("tls", out var tls) && tls.TryGetProperty("server_name", out var sn))
                    serverName = sn.GetString() ?? "";

                return new TrojanServer
                {
                    Hostname = hostname,
                    Port = port,
                    Password = outbound.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "",
                    Host = serverName,
                    TLSSecureType = "tls",
                    Remark = tag
                };
            }

            case "socks":
            {
                return new Socks5Server
                {
                    Hostname = hostname,
                    Port = port,
                    Username = outbound.TryGetProperty("username", out var u) ? u.GetString() : null,
                    Password = outbound.TryGetProperty("password", out var p) ? p.GetString() : null,
                    Remark = tag
                };
            }

            case "vless":
            {
                var uuid = outbound.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";
                var flow = outbound.TryGetProperty("flow", out var f) ? f.GetString() ?? "" : "";
                var isReality = false;
                var pubKey = "";
                var shortId = "";
                var serverName = "";

                if (outbound.TryGetProperty("tls", out var tls))
                {
                    if (tls.TryGetProperty("server_name", out var sn))
                        serverName = sn.GetString() ?? "";

                    if (tls.TryGetProperty("reality", out var reality))
                    {
                        isReality = true;
                        pubKey = reality.TryGetProperty("public_key", out var pk) ? pk.GetString() ?? "" : "";
                        shortId = reality.TryGetProperty("short_id", out var sid) ? sid.GetString() ?? "" : "";
                    }
                }

                if (flow.Contains("vision", StringComparison.OrdinalIgnoreCase) || isReality)
                {
                    return new VisionServer
                    {
                        Hostname = hostname,
                        Port = port,
                        UserID = uuid,
                        Flow = flow,
                        TLSSecureType = isReality ? "reality" : "tls",
                        PublicKey = pubKey,
                        ShortId = shortId,
                        ServerName = serverName,
                        Remark = tag
                    };
                }

                return new VLESSServer
                {
                    Hostname = hostname,
                    Port = port,
                    UserID = uuid,
                    FlowControl = flow,
                    TLSSecureType = !string.IsNullOrEmpty(serverName) ? "tls" : "none",
                    ServerName = serverName,
                    Remark = tag
                };
            }

            case "vmess":
            {
                return new VMessServer
                {
                    Hostname = hostname,
                    Port = port,
                    UserID = outbound.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "",
                    AlterID = outbound.TryGetProperty("alter_id", out var a) ? a.GetInt32() : 0,
                    EncryptMethod = outbound.TryGetProperty("security", out var s) ? s.GetString() ?? "auto" : "auto",
                    Remark = tag
                };
            }

            default:
                return null;
        }
    }
}
