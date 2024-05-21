#nullable disable
// ReSharper disable InconsistentNaming

using System.Text.Json.Serialization;

namespace Netch.Servers;

public struct HysteriaConfig
{
    public string server { get; set; }

    public string auth { get; set; }

    public string password { get; set; }

    public TLS_Item tls { get; set; }

    public OBFS_Item obfs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object transport { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object quic { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object bandwidth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool fastOpen { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool lazy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object socks5 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object http { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object tcpForwarding { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object udpForwarding { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object tcpTProxy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object udpTProxy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object tcpRedirect { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object tun { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object acl { get; set; }
}

public class TLS_Item
{
    public string sni { get; set; }

    public bool insecure { get; set; }


    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string pinSHA256 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string ca { get; set; }
}

public class OBFS_Item
{
    public string type { get; set; }

    public object salamander { get; set; }
}

public class DNS_Item
{
    public List<Rule_Item> rules { get; set; }
}

public class Rule_Item
{

}