#nullable disable
// ReSharper disable InconsistentNaming

using System.Text.Json.Serialization;

namespace Netch.Servers;

public struct XrayConfig
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object log { get; set; }

    public object[] inbounds { get; set; }

    public object outbounds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object api { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object policy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object stats { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public X_Route routing { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public X_DNS dns { get; set; }
}

public class X_DNS()
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> servers { get; set; }
}

public class X_Route ()
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string domainStrategy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> rules { get; set; }
}

public class X_TLS()
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string serverName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool rejectUnknownSni { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool allowInsecure { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> alpn { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string minVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string maxVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> servers { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string cipherSuites { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> certificates { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool disableSystemRoot { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool enableSessionResumption { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string fingerprint { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> pinnedPeerCertificateChainSha256 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string masterKeyLog { get; set; }
}

public class X_StreamSettings ()
// https://xtls.github.io/Xray-docs-next/config/transport.html#transportobject
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string network { get; set; }     // "tcp" | "ws" | "h2" | "grpc" | "quic" | "kcp" | "httpupgrade"

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string security { get; set; }     // "none" | "tls" | "reality"

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object tlsSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object tcpSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object kcpSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object wsSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object httpSettings { get; set; } 

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object quicSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object dsSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object grpcSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object httpupgradeSettings { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object sockopt { get; set; }     //
}

public class X_Reality ()
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool show { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string dest { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int xver { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object serverNames { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string privateKey { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string minClientVer { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int maxTimeDiff { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object shortIds { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object fingerprint { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object serverName { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object sockopt { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string publicKey { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string shortId { get; set; }     //

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string spiderX { get; set; }     //

}