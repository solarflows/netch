#nullable disable
// ReSharper disable InconsistentNaming

namespace Netch.Servers;

public struct SingboxConfig
{
    public object[] inbounds { get; set; }

    public SB_Outbound[] outbounds { get; set; }

    public SB_DNS dns { get; set; }
    public SB_ROUTE route { get; set; }
}

public class SB_Outbound
{
    public string type { get; set; }

    public string server { get; set; }

    public int server_port { get; set; }

    public string password { get; set; }

    public int up_mbps { get; set; }

    public int down_mbps { get; set; }

    public SB_TLS tls { get; set; }

    public SB_OBFS obfs { get; set; }
}

public class SB_TLS
{
    public bool enabled { get; set; }

    public string server_name { get; set; }

    public bool insecure { get; set; }
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
    //public List<SB_Rule> rules { get; set; }
    public object[] servers { get; set; }

    public SB_DNS_Rule[] rules { get; set; }
}

public class SB_ROUTE
{
    public SB_ROUTE_Rule[] rules { get; set; }
}

public class SB_ROUTE_Rule
{
    public object[] geosite { get; set; }
    public object[] rule_set { get; set; }
}