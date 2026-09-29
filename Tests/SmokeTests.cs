using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netch;
using Netch.Models;
using Netch.Servers;
using Netch.Servers.Singbox;
using Netch.Utils;

namespace Tests;

[TestClass]
public class SmokeTests
{
    [TestInitialize]
    public void Setup()
    {
        // Ensure default settings are initialized
        Netch.Global.Settings.Socks5LocalPort = 2801;
        Netch.Global.Settings.LocalAddress = "127.0.0.1";
        Netch.Global.Settings.CoreType = "sing-box";
    }

    #region Subscription Parsing Smoke Tests

    [TestMethod]
    public void Smoke_ClashYaml_Detection()
    {
        var validYaml = "proxies:\n  - name: test\n    type: socks5\n    server: 127.0.0.1\n    port: 1080";
        Assert.IsTrue(ClashSubParser.IsClashYaml(validYaml));

        var invalidYaml = "vmess://eyJhZGQiOiIxMjcuMC4wLjEiLCJwb3J0IjoxMDg...=";
        Assert.IsFalse(ClashSubParser.IsClashYaml(invalidYaml));
    }

    [TestMethod]
    public void Smoke_ClashYaml_AllProtocols()
    {
        var yaml = """
            proxies:
              - name: "SS-Node"
                type: ss
                server: 1.2.3.4
                port: 8388
                cipher: aes-256-gcm
                password: "password123"
              - name: "VMess-Node"
                type: vmess
                server: 2.3.4.5
                port: 443
                uuid: 11111111-1111-1111-1111-111111111111
                alterId: 0
                cipher: auto
                tls: true
                network: ws
                ws-opts:
                  path: /ws-path
                  headers:
                    Host: vmess.example.com
              - name: "VLESS-Reality"
                type: vless
                server: 3.4.5.6
                port: 443
                uuid: 22222222-2222-2222-2222-222222222222
                flow: xtls-rprx-vision
                network: tcp
                reality-opts:
                  public-key: "AbCdEf123456"
                  short-id: "abcd"
                client-fingerprint: chrome
                servername: reality.example.com
              - name: "Trojan-Node"
                type: trojan
                server: 4.5.6.7
                port: 443
                password: "trojanpassword"
                sni: trojan.example.com
              - name: "Socks5-Node"
                type: socks5
                server: 5.6.7.8
                port: 1080
                username: "socksuser"
                password: "sockspassword"
              - name: "WG-Node"
                type: wireguard
                server: 6.7.8.9
                port: 51820
                ip: 10.0.0.2
                public-key: "pubkey=="
                private-key: "privkey=="
                mtu: 1420
            """;

        var servers = ClashSubParser.ParseYaml(yaml);
        Assert.AreEqual(6, servers.Count);

        // SS
        var ss = servers[0] as ShadowsocksServer;
        Assert.IsNotNull(ss);
        Assert.AreEqual("SS-Node", ss.Remark);
        Assert.AreEqual("1.2.3.4", ss.Hostname);
        Assert.AreEqual(8388, ss.Port);
        Assert.AreEqual("aes-256-gcm", ss.EncryptMethod);
        Assert.AreEqual("password123", ss.Password);

        // VMess
        var vmess = servers[1] as VMessServer;
        Assert.IsNotNull(vmess);
        Assert.AreEqual("VMess-Node", vmess.Remark);
        Assert.AreEqual("11111111-1111-1111-1111-111111111111", vmess.UserID);
        Assert.AreEqual("ws", vmess.TransferProtocol);
        Assert.AreEqual("/ws-path", vmess.Path);
        Assert.AreEqual("vmess.example.com", vmess.Host);

        // VLESS
        var vless = servers[2] as VisionServer;
        Assert.IsNotNull(vless);
        Assert.AreEqual("VLESS-Reality", vless.Remark);
        Assert.AreEqual("22222222-2222-2222-2222-222222222222", vless.UserID);
        Assert.AreEqual("xtls-rprx-vision", vless.Flow);
        Assert.AreEqual("reality", vless.TLSSecureType);
        Assert.AreEqual("AbCdEf123456", vless.PublicKey);
        Assert.AreEqual("abcd", vless.ShortId);
        Assert.AreEqual("chrome", vless.Fingerprint);

        // Trojan
        var trojan = servers[3] as TrojanServer;
        Assert.IsNotNull(trojan);
        Assert.AreEqual("Trojan-Node", trojan.Remark);
        Assert.AreEqual("trojanpassword", trojan.Password);
        Assert.AreEqual("trojan.example.com", trojan.Host);

        // Socks5
        var socks = servers[4] as Socks5Server;
        Assert.IsNotNull(socks);
        Assert.AreEqual("Socks5-Node", socks.Remark);
        Assert.AreEqual("socksuser", socks.Username);
        Assert.AreEqual("sockspassword", socks.Password);

        // WireGuard
        var wg = servers[5] as WireGuardServer;
        Assert.IsNotNull(wg);
        Assert.AreEqual("WG-Node", wg.Remark);
        Assert.AreEqual("10.0.0.2", wg.LocalAddresses);
        Assert.AreEqual("pubkey==", wg.PeerPublicKey);
        Assert.AreEqual("privkey==", wg.PrivateKey);
        Assert.AreEqual(1420, wg.MTU);
    }

    [TestMethod]
    public void Smoke_ClashYaml_FlowStyle()
    {
        var yaml = """
            proxies:
              - { name: "Flow-SS", type: ss, server: 1.1.1.1, port: 8388, cipher: chacha20-ietf-poly1305, password: "pw" }
              - { name: "Flow-Socks", type: socks5, server: 2.2.2.2, port: 1080 }
            """;

        var servers = ClashSubParser.ParseYaml(yaml);
        Assert.AreEqual(2, servers.Count);
        Assert.IsInstanceOfType(servers[0], typeof(ShadowsocksServer));
        Assert.IsInstanceOfType(servers[1], typeof(Socks5Server));
        Assert.AreEqual("Flow-SS", servers[0].Remark);
        Assert.AreEqual("Flow-Socks", servers[1].Remark);
    }

    [TestMethod]
    public void Smoke_SingboxJson_OutboundsParsing()
    {
        var json = """
            {
              "outbounds": [
                {
                  "type": "vless",
                  "tag": "sb-vless",
                  "server": "1.2.3.4",
                  "server_port": 443,
                  "uuid": "33333333-3333-3333-3333-333333333333",
                  "flow": "xtls-rprx-vision",
                  "tls": {
                    "enabled": true,
                    "server_name": "sb.example.com",
                    "reality": {
                      "enabled": true,
                      "public_key": "reality_pub_key"
                    }
                  }
                },
                {
                  "type": "shadowsocks",
                  "tag": "sb-ss",
                  "server": "5.6.7.8",
                  "server_port": 8388,
                  "method": "aes-128-gcm",
                  "password": "sspassword"
                }
              ]
            }
            """;

        var servers = ShareLink.ParseText(json);
        Assert.AreEqual(2, servers.Count);
        Assert.IsInstanceOfType(servers[0], typeof(VisionServer));
        Assert.IsInstanceOfType(servers[1], typeof(ShadowsocksServer));

        var vision = (VisionServer)servers[0];
        Assert.AreEqual("sb-vless", vision.Remark);
        Assert.AreEqual("33333333-3333-3333-3333-333333333333", vision.UserID);
        Assert.AreEqual("reality", vision.TLSSecureType);
        Assert.AreEqual("reality_pub_key", vision.PublicKey);
    }

    #endregion

    #region Socks5 Link & ServerHelper Smoke Tests

    [TestMethod]
    public void Smoke_Socks5_ServerHelperDiscovery()
    {
        var utilBySocks = ServerHelper.GetUtilByTypeName("SOCKS");
        var utilBySocks5 = ServerHelper.GetUtilByTypeName("Socks5");
        Assert.IsNotNull(utilBySocks);
        Assert.IsNotNull(utilBySocks5);
        Assert.AreSame(utilBySocks, utilBySocks5);
        Assert.IsInstanceOfType(utilBySocks, typeof(Socks5Util));

        var utilBySchemeSocks5 = ServerHelper.GetUtilByUriScheme("socks5");
        var utilBySchemeSocks = ServerHelper.GetUtilByUriScheme("socks");
        Assert.IsNotNull(utilBySchemeSocks5);
        Assert.IsNotNull(utilBySchemeSocks);
        Assert.AreSame(utilBySchemeSocks5, utilBySchemeSocks);
    }

    [TestMethod]
    public void Smoke_Socks5_StandardRfcLink()
    {
        var link = "socks5://user:pass@127.0.0.1:1080#LocalNode";
        var servers = ShareLink.ParseText(link);
        Assert.AreEqual(1, servers.Count);

        var s5 = servers[0] as Socks5Server;
        Assert.IsNotNull(s5);
        Assert.AreEqual("127.0.0.1", s5.Hostname);
        Assert.AreEqual(1080, s5.Port);
        Assert.AreEqual("user", s5.Username);
        Assert.AreEqual("pass", s5.Password);
        Assert.AreEqual("LocalNode", s5.Remark);
    }

    [TestMethod]
    public void Smoke_Socks5_Base64AuthLink()
    {
        // user:pass in Base64 is dXNlcjpwYXNz
        var link = "socks5://dXNlcjpwYXNz@127.0.0.1:1080#Base64Node";
        var servers = ShareLink.ParseText(link);
        Assert.AreEqual(1, servers.Count);

        var s5 = servers[0] as Socks5Server;
        Assert.IsNotNull(s5);
        Assert.AreEqual("user", s5.Username);
        Assert.AreEqual("pass", s5.Password);
        Assert.AreEqual("Base64Node", s5.Remark);
    }

    [TestMethod]
    public void Smoke_Socks5_QueryParamsLink()
    {
        var link = "socks5://127.0.0.1:1080?user=paramuser&pass=parampass#QueryNode";
        var servers = ShareLink.ParseText(link);
        Assert.AreEqual(1, servers.Count);

        var s5 = servers[0] as Socks5Server;
        Assert.IsNotNull(s5);
        Assert.AreEqual("paramuser", s5.Username);
        Assert.AreEqual("parampass", s5.Password);
        Assert.AreEqual("QueryNode", s5.Remark);
    }

    [TestMethod]
    public void Smoke_Socks5_IPv6Link()
    {
        var link = "socks5://[::1]:1080#IPv6Node";
        var servers = ShareLink.ParseText(link);
        Assert.AreEqual(1, servers.Count);

        var s5 = servers[0] as Socks5Server;
        Assert.IsNotNull(s5);
        Assert.AreEqual("::1", s5.Hostname);
        Assert.AreEqual(1080, s5.Port);
        Assert.AreEqual("IPv6Node", s5.Remark);
    }

    [TestMethod]
    public void Smoke_Socks5_RoundTrip()
    {
        var original = new Socks5Server
        {
            Hostname = "127.0.0.1",
            Port = 1088,
            Username = "rt_user",
            Password = "rt_password",
            Remark = "RoundTripNode"
        };

        var shareLink = ShareLink.GetShareLink(original);
        Assert.IsTrue(shareLink.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase));

        var parsedServers = ShareLink.ParseText(shareLink);
        Assert.AreEqual(1, parsedServers.Count);
        var restored = parsedServers[0] as Socks5Server;
        Assert.IsNotNull(restored);
        Assert.AreEqual(original.Hostname, restored.Hostname);
        Assert.AreEqual(original.Port, restored.Port);
        Assert.AreEqual(original.Username, restored.Username);
        Assert.AreEqual(original.Password, restored.Password);
        Assert.AreEqual(original.Remark, restored.Remark);
    }

    #endregion

    #region Proxy Core Config Generation Smoke Tests

    [TestMethod]
    public async Task Smoke_SingboxConfig_GenerationAsync()
    {
        // 1. Socks5
        var s5 = new Socks5Server { Hostname = "127.0.0.1", Port = 1080, Username = "u", Password = "p" };
        var s5Config = await SingboxConfigUtils.GenerateClientConfigAsync(s5);
        ValidateSingboxConfig(s5Config, "socks");

        // 2. Shadowsocks
        var ss = new ShadowsocksServer { Hostname = "127.0.0.1", Port = 8388, EncryptMethod = "aes-256-gcm", Password = "p" };
        var ssConfig = await SingboxConfigUtils.GenerateClientConfigAsync(ss);
        ValidateSingboxConfig(ssConfig, "shadowsocks");

        // 3. Vision / VLESS Reality
        var vless = new VisionServer
        {
            Hostname = "127.0.0.1",
            Port = 443,
            UserID = "00000000-0000-0000-0000-000000000001",
            Flow = "xtls-rprx-vision",
            TLSSecureType = "reality",
            PublicKey = "pub123",
            ShortId = "1234"
        };
        var vlessConfig = await SingboxConfigUtils.GenerateClientConfigAsync(vless);
        ValidateSingboxConfig(vlessConfig, "vless");

        // 4. Trojan
        var trojan = new TrojanServer { Hostname = "127.0.0.1", Port = 443, Password = "p" };
        var trojanConfig = await SingboxConfigUtils.GenerateClientConfigAsync(trojan);
        ValidateSingboxConfig(trojanConfig, "trojan");

        // 5. VMess
        var vmess = new VMessServer
        {
            Hostname = "127.0.0.1",
            Port = 443,
            UserID = "00000000-0000-0000-0000-000000000002",
            TransferProtocol = "tcp"
        };
        var vmessConfig = await SingboxConfigUtils.GenerateClientConfigAsync(vmess);
        ValidateSingboxConfig(vmessConfig, "vmess");
    }

    private static void ValidateSingboxConfig(Dictionary<string, object> config, string expectedOutboundType)
    {
        Assert.IsTrue(config.ContainsKey("log"));
        Assert.IsTrue(config.ContainsKey("inbounds"));
        Assert.IsTrue(config.ContainsKey("outbounds"));
        Assert.IsTrue(config.ContainsKey("route"));

        var outbounds = config["outbounds"] as List<object>;
        Assert.IsNotNull(outbounds);
        Assert.IsTrue(outbounds.Count >= 3); // node outbound + direct + block

        var primaryOutbound = outbounds[0] as Dictionary<string, object>;
        Assert.IsNotNull(primaryOutbound);
        Assert.AreEqual(expectedOutboundType, primaryOutbound["type"]);

        // Verify JSON serialization doesn't throw
        var json = JsonSerializer.Serialize(config);
        Assert.IsFalse(string.IsNullOrWhiteSpace(json));
    }

    [TestMethod]
    public async Task Smoke_XrayConfig_GenerationAsync()
    {
        // 1. Socks5
        var s5 = new Socks5Server { Hostname = "127.0.0.1", Port = 1080 };
        var s5Config = await CoreConfig.GenerateClientConfigAsync(s5);
        Assert.IsNotNull(s5Config.inbounds);
        Assert.IsNotNull(s5Config.outbounds);
        Assert.AreEqual("socks", s5Config.outbounds[0].protocol);

        // 2. Vision
        var vision = new VisionServer
        {
            Hostname = "127.0.0.1",
            Port = 443,
            UserID = "00000000-0000-0000-0000-000000000001",
            Flow = "xtls-rprx-vision",
            TLSSecureType = "reality",
            PublicKey = "pub123"
        };
        var visionConfig = await CoreConfig.GenerateClientConfigAsync(vision);
        Assert.IsNotNull(visionConfig.outbounds);
        Assert.AreEqual("vless", visionConfig.outbounds[0].protocol);

        // 3. Trojan
        var trojan = new TrojanServer { Hostname = "127.0.0.1", Port = 443, Password = "p" };
        var trojanConfig = await CoreConfig.GenerateClientConfigAsync(trojan);
        Assert.IsNotNull(trojanConfig.outbounds);
        Assert.AreEqual("trojan", trojanConfig.outbounds[0].protocol);

        var json = JsonSerializer.Serialize(trojanConfig);
        Assert.IsFalse(string.IsNullOrWhiteSpace(json));
    }

    #endregion

    #region Architectural Constraints Smoke Tests

    [TestMethod]
    public void Smoke_Architecture_LegacyControllersAreRemoved()
    {
        var netchAssembly = typeof(Netch.Global).Assembly;
        var typeNames = netchAssembly.GetTypes().Select(t => t.Name).ToList();

        // Ensure legacy controllers do NOT exist in the assembly
        Assert.IsFalse(typeNames.Contains("ShadowsocksController"), "ShadowsocksController should be removed");
        Assert.IsFalse(typeNames.Contains("ShadowsocksRController"), "ShadowsocksRController should be removed");
        Assert.IsFalse(typeNames.Contains("TrojanController"), "TrojanController should be removed");

        // Ensure modern controllers DO exist
        Assert.IsTrue(typeNames.Contains("SingboxController"), "SingboxController must exist");
        Assert.IsTrue(typeNames.Contains("V2rayController"), "V2rayController must exist");
    }

    [TestMethod]
    public void Smoke_Settings_SingboxAndThemeProperties()
    {
        var setting = new Netch.Models.Setting();
        Assert.AreEqual("Xray", setting.CoreType);
        Assert.AreEqual("System", setting.Theme);
        Assert.IsNotNull(setting.SingboxConfig);
        Assert.IsTrue(setting.SingboxConfig.Sniffing);
    }

    #endregion
}
