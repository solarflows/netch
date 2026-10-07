using System.Linq;
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

        // 缩进栈：维护多层 YAML 对象的复合前缀 (例如 ws-opts -> headers -> Host)
        var indentStack = new Stack<(int Indent, string Key)>();

        string GetCurrentPrefix()
        {
            if (indentStack.Count == 0)
                return "";
            return string.Join("-", indentStack.Reverse().Select(x => x.Key)) + "-";
        }

        void FlushCurrent()
        {
            if (currentDict.Count > 0)
            {
                var s = ConvertDictToServer(currentDict);
                if (s != null)
                    servers.Add(s);

                currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                indentStack.Clear();
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

            // 遇到非缩进的顶级键（无前导空格且非列表项），说明 proxies 区块已结束
            if (!char.IsWhiteSpace(line[0]) && !trimmed.StartsWith("-"))
            {
                FlushCurrent();
                break;
            }

            // 计算当前行的缩进空格数
            int lineIndent = line.TakeWhile(char.IsWhiteSpace).Count();

            // 检查是否开始新的节点 (- ...)
            if (trimmed.StartsWith("- ") || trimmed.Equals("-"))
            {
                FlushCurrent();

                var content = trimmed[2..].Trim();
                if (content.StartsWith('{') && content.EndsWith('}'))
                {
                    // Flow style 单行字典: - { name: "...", type: vmess, ... }
                    ParseFlowStyleDict(content, "", currentDict);
                    FlushCurrent();
                    continue;
                }

                if (!string.IsNullOrEmpty(content) && content.Contains(':'))
                {
                    ParseKeyValue(content, "", currentDict);
                }
                continue;
            }

            // 根据缩进深度管理前缀栈：当前行缩进小于或等于栈顶缩进时，回退退出深层对象
            while (indentStack.Count > 0 && lineIndent <= indentStack.Peek().Indent)
            {
                indentStack.Pop();
            }

            // 处理块级键值对
            if (trimmed.Contains(':'))
            {
                var colonIndex = trimmed.IndexOf(':');
                var key = trimmed[..colonIndex].Trim();
                var val = trimmed[(colonIndex + 1)..].Trim();

                if (string.IsNullOrEmpty(val))
                {
                    // 子字典对象头 (例如 ws-opts:, headers:, reality-opts:, grpc-opts:)
                    indentStack.Push((lineIndent, key));
                }
                else if (val.StartsWith('{') && val.EndsWith('}'))
                {
                    // 行内复合子字典 (例如 headers: { Host: "example.com" })
                    ParseFlowStyleDict(val, GetCurrentPrefix() + key + "-", currentDict);
                }
                else
                {
                    var prefix = GetCurrentPrefix();
                    StoreKeyValue(key, val, prefix, currentDict);
                }
            }
        }

        FlushCurrent();
        return servers;
    }

    private static void ParseFlowStyleDict(string inline, string prefix, Dictionary<string, string> dict)
    {
        var content = inline.Trim('{', '}').Trim();
        var parts = SplitFlowStyle(content);
        foreach (var part in parts)
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = part[..colonIndex].Trim();
            var val = part[(colonIndex + 1)..].Trim();

            if (val.StartsWith('{') && val.EndsWith('}'))
            {
                ParseFlowStyleDict(val, prefix + key + "-", dict);
            }
            else
            {
                StoreKeyValue(key, val, prefix, dict);
            }
        }
    }

    private static List<string> SplitFlowStyle(string content)
    {
        var list = new List<string>();
        var inQuotes = false;
        var quoteChar = '\0';
        int braceDepth = 0;
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
                if (c is '"' or '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (c is '{' or '[')
                {
                    braceDepth++;
                }
                else if (c is '}' or ']')
                {
                    if (braceDepth > 0)
                        braceDepth--;
                }
                else if (c == ',' && braceDepth == 0)
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
            StoreKeyValue(key, val, prefix, dict);
        }
    }

    private static void StoreKeyValue(string key, string rawVal, string prefix, Dictionary<string, string> dict)
    {
        var cleanVal = Unquote(rawVal);
        var fullKey = prefix + key;
        dict[fullKey] = cleanVal;

        // 建立关键字段别名，消除不同层级命名的差异
        if (!string.IsNullOrEmpty(prefix))
        {
            // 写入去除最外层前缀后的直接键
            if (!dict.ContainsKey(key))
                dict[key] = cleanVal;

            // 特别映射 Host / SNI / Path 常用别名
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                dict["ws-headers-host"] = cleanVal;
                dict["headers-host"] = cleanVal;
                dict["host"] = cleanVal;
            }
            else if (key.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                dict["ws-path"] = cleanVal;
                dict["path"] = cleanVal;
            }
            else if (key.Equals("grpc-service-name", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals("service-name", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals("serviceName", StringComparison.OrdinalIgnoreCase))
            {
                dict["serviceName"] = cleanVal;
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

        var skipCert = dict.GetValueOrDefault("skip-cert-verify");
        var allowInsecure = skipCert?.Equals("true", StringComparison.OrdinalIgnoreCase) == true || skipCert == "1";

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
                    Remark = remark,
                    AllowInsecure = allowInsecure
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
                var tls = dict.GetValueOrDefault("tls")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ||
                          dict.GetValueOrDefault("tls") == "1";

                var host = dict.GetValueOrDefault("ws-opts-headers-host") ??
                           dict.GetValueOrDefault("headers-host") ??
                           dict.GetValueOrDefault("ws-headers-host") ??
                           dict.GetValueOrDefault("ws-host") ??
                           dict.GetValueOrDefault("servername") ??
                           dict.GetValueOrDefault("sni") ??
                           dict.GetValueOrDefault("host") ?? "";

                var path = dict.GetValueOrDefault("ws-opts-path") ??
                           dict.GetValueOrDefault("ws-path") ??
                           dict.GetValueOrDefault("path") ??
                           dict.GetValueOrDefault("serviceName") ?? "/";

                return new VMessServer
                {
                    Hostname = hostname,
                    Port = port,
                    UserID = dict.GetValueOrDefault("uuid") ?? "",
                    AlterID = int.TryParse(dict.GetValueOrDefault("alterId"), out var aid) ? aid : 0,
                    EncryptMethod = dict.GetValueOrDefault("cipher") ?? "auto",
                    TransferProtocol = dict.GetValueOrDefault("network") ?? "tcp",
                    TLSSecureType = tls ? "tls" : "none",
                    Host = host,
                    Path = path,
                    ServerName = dict.GetValueOrDefault("servername") ?? dict.GetValueOrDefault("sni"),
                    Remark = remark,
                    AllowInsecure = allowInsecure
                };
            }

            case "vless":
            {
                var flow = dict.GetValueOrDefault("flow") ?? "";
                var isReality = dict.ContainsKey("public-key") ||
                                dict.ContainsKey("reality-opts-public-key") ||
                                dict.GetValueOrDefault("reality")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

                var isTls = isReality ||
                            dict.GetValueOrDefault("tls")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ||
                            dict.GetValueOrDefault("tls") == "1";

                var host = dict.GetValueOrDefault("ws-opts-headers-host") ??
                           dict.GetValueOrDefault("headers-host") ??
                           dict.GetValueOrDefault("ws-headers-host") ??
                           dict.GetValueOrDefault("ws-host") ??
                           dict.GetValueOrDefault("servername") ??
                           dict.GetValueOrDefault("sni") ??
                           dict.GetValueOrDefault("host") ?? "";

                var path = dict.GetValueOrDefault("ws-opts-path") ??
                           dict.GetValueOrDefault("ws-path") ??
                           dict.GetValueOrDefault("path") ??
                           dict.GetValueOrDefault("serviceName") ?? "/";

                if (flow.Contains("vision", StringComparison.OrdinalIgnoreCase) || isReality)
                {
                    return new VisionServer
                    {
                        Hostname = hostname,
                        Port = port,
                        UserID = dict.GetValueOrDefault("uuid") ?? "",
                        Flow = flow,
                        TLSSecureType = isReality ? "reality" : (isTls ? "tls" : "none"),
                        PublicKey = dict.GetValueOrDefault("public-key") ?? dict.GetValueOrDefault("reality-opts-public-key") ?? "",
                        ShortId = dict.GetValueOrDefault("short-id") ?? dict.GetValueOrDefault("reality-opts-short-id") ?? "",
                        ServerName = dict.GetValueOrDefault("servername") ?? dict.GetValueOrDefault("sni"),
                        Host = host,
                        Path = path,
                        Remark = remark,
                        AllowInsecure = allowInsecure
                    };
                }

                return new VLESSServer
                {
                    Hostname = hostname,
                    Port = port,
                    UserID = dict.GetValueOrDefault("uuid") ?? "",
                    FlowControl = flow,
                    TLSSecureType = isTls ? "tls" : "none",
                    ServerName = dict.GetValueOrDefault("servername") ?? dict.GetValueOrDefault("sni"),
                    TransferProtocol = dict.GetValueOrDefault("network") ?? "tcp",
                    Host = host,
                    Path = path,
                    Remark = remark,
                    AllowInsecure = allowInsecure
                };
            }

            case "trojan":
            {
                var net = dict.GetValueOrDefault("network") ?? "tcp";
                var host = dict.GetValueOrDefault("ws-opts-headers-host") ??
                           dict.GetValueOrDefault("headers-host") ??
                           dict.GetValueOrDefault("ws-headers-host") ??
                           dict.GetValueOrDefault("ws-host") ??
                           dict.GetValueOrDefault("sni") ??
                           dict.GetValueOrDefault("servername") ??
                           dict.GetValueOrDefault("host") ?? "";

                var path = dict.GetValueOrDefault("ws-opts-path") ??
                           dict.GetValueOrDefault("ws-path") ??
                           dict.GetValueOrDefault("path") ?? "/";

                var serviceName = dict.GetValueOrDefault("grpc-opts-grpc-service-name") ??
                                  dict.GetValueOrDefault("grpc-service-name") ??
                                  dict.GetValueOrDefault("serviceName");

                return new TrojanServer
                {
                    Hostname = hostname,
                    Port = port,
                    Password = dict.GetValueOrDefault("password") ?? "",
                    Host = host,
                    Path = path,
                    Mode = net,
                    ServiceName = serviceName,
                    TLSSecureType = "tls",
                    Remark = remark,
                    AllowInsecure = allowInsecure
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

        bool allowInsecure = false;
        string serverName = "";
        if (outbound.TryGetProperty("tls", out var tlsElem))
        {
            if (tlsElem.TryGetProperty("insecure", out var ins))
                allowInsecure = ins.GetBoolean();
            if (tlsElem.TryGetProperty("server_name", out var sn))
                serverName = sn.GetString() ?? "";
        }

        string transportType = "tcp";
        string wsPath = "/";
        string wsHost = "";
        string serviceName = "";

        if (outbound.TryGetProperty("transport", out var transElem))
        {
            if (transElem.TryGetProperty("type", out var tt))
                transportType = tt.GetString()?.ToLowerInvariant() ?? "tcp";

            if (transElem.TryGetProperty("path", out var p))
                wsPath = p.GetString() ?? "/";

            if (transElem.TryGetProperty("headers", out var headers) && headers.TryGetProperty("Host", out var h))
                wsHost = h.GetString() ?? "";

            if (transElem.TryGetProperty("service_name", out var snElem))
                serviceName = snElem.GetString() ?? "";
        }

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
                return new TrojanServer
                {
                    Hostname = hostname,
                    Port = port,
                    Password = outbound.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "",
                    Host = !string.IsNullOrEmpty(serverName) ? serverName : wsHost,
                    Path = wsPath,
                    Mode = transportType,
                    ServiceName = serviceName,
                    TLSSecureType = "tls",
                    Remark = tag,
                    AllowInsecure = allowInsecure
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

                if (outbound.TryGetProperty("tls", out var tls))
                {
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
                        Host = wsHost,
                        Path = wsPath,
                        Remark = tag,
                        AllowInsecure = allowInsecure
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
                    TransferProtocol = transportType,
                    Host = wsHost,
                    Path = wsPath,
                    Remark = tag,
                    AllowInsecure = allowInsecure
                };
            }

            case "vmess":
            {
                var isTls = !string.IsNullOrEmpty(serverName) || (outbound.TryGetProperty("tls", out var t) && t.TryGetProperty("enabled", out var en) && en.GetBoolean());
                return new VMessServer
                {
                    Hostname = hostname,
                    Port = port,
                    UserID = outbound.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "",
                    AlterID = outbound.TryGetProperty("alter_id", out var a) ? a.GetInt32() : 0,
                    EncryptMethod = outbound.TryGetProperty("security", out var s) ? s.GetString() ?? "auto" : "auto",
                    TransferProtocol = transportType,
                    TLSSecureType = isTls ? "tls" : "none",
                    ServerName = serverName,
                    Host = wsHost,
                    Path = wsPath,
                    Remark = tag,
                    AllowInsecure = allowInsecure
                };
            }

            default:
                return null;
        }
    }
}
