#nullable disable
// ReSharper disable InconsistentNaming

namespace Netch.Servers;

public struct HysteriaConfig
{
    public object[] inbounds { get; set; }

    public HyOutbound[] outbounds { get; set; }

    public DNS_Item[] dns { get; set; }
}

public class HyOutbound
{
    public string type { get; set; }

    public string server { get; set; }

    public int server_port { get; set; }

    public string password { get; set; }

    public int up_mbps { get; set; }

    public int down_mbps { get; set; }

    public TLS_Item tls { get; set; }

    public OBFS_Item obfs { get; set; }
}

public class TLS_Item
{
    public bool enabled { get; set; }

    public string server_name { get; set; }

    public bool insecure { get; set; }
}

public class OBFS_Item
{
    public string type { get; set; }

    public string password { get; set; }
}

public class DNS_Item
{
    public List<Rule_Item> rules { get; set; }
}

public class Rule_Item
{

}