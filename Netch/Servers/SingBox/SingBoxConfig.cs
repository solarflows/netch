#nullable disable
// ReSharper disable InconsistentNaming

using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Netch.Servers;

public struct SingboxConfig
{
    public object[] inbounds { get; set; }

    public object outbounds { get; set; }

    public SB_DNS dns { get; set; }

    public SB_Route route { get; set; }

    public Object experimental { get; set; }
}

public class SB_Outbound
{
    public string type { get; set; }

    public string tag { get; set; }

    public string server { get; set; }

    public int server_port { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string uuid { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string password { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string security { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int alter_id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string flow { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string network { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int up_mbps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int down_mbps { get; set; }

    public SB_TLS tls { get; set; }

    public SB_OBFS obfs { get; set; }

    public List<object> users { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string packet_encoding { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<string, object> multiplex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<string, object> transport { get; set; }
}

public class SB_Experimental
{

}

public class SB_TLS

// https://sing-box.sagernet.org/zh/configuration/shared/tls/#_1

{
    public bool enabled { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string server_name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool insecure { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string fingerprint { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string disable_sni { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> alpn { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string min_version { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string max_version { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> cipher_suites { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> certificate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string certificate_path { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object ech { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public SB_Reality reality { get; set; }

}

public class SB_OBFS
{
    public string type { get; set; }

    public string password { get; set; }
}

public class SB_DNS_Rule
{
    public object[] geosite { get; set; }
    public object[] rule_set { get; set; }
}

public class SB_DNS
{
    public List<object> servers { get; set; }

    public List<object> rules { get; set; }
}

public class SB_Route
{
    public List<object> rules { get; set; }

    public List<object> rule_set { get; set; }

    public bool auto_detect_interface { get; set; }

    public string final { get; set; }
}

public class SB_Reality
{
    public bool enabled { get; set; }

    public string public_key { get; set; }

    public string short_id { get; set;}
}