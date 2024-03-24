#nullable disable
// ReSharper disable InconsistentNaming

namespace Netch.Servers;

public struct SingboxConfig
{
    public object[] inbounds { get; set; }

    public object outbounds { get; set; }

    public SB_DNS dns { get; set; }

    public SB_ROUTE route { get; set; }

    public Object experimental { get; set; }
}

public class SB_Outbound
{
    public string type { get; set; }

    public string tag { get; set; }

    public string server { get; set; }

    public int server_port { get; set; }

    public string password { get; set; }

    public int up_mbps { get; set; }

    public int down_mbps { get; set; }

    public SB_TLS tls { get; set; }

    public SB_OBFS obfs { get; set; }
}

public class SB_Experimental
{

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
    public List<object> servers { get; set; }

    public List<object> rules { get; set; }
}

public class SB_ROUTE
{
    public List<object> rules { get; set; }

    public List<object> rule_set { get; set; }

    public bool auto_detect_interface { get; set; }

    public string final { get; set; }
}
