#nullable disable
namespace Netch.Servers;

public class HysteriaConfig
{
    /// <summary>
    ///     监听地址
    /// </summary>
    public string local_addr { get; set; } = "127.0.0.1";

    /// <summary>
    ///     监听端口
    /// </summary>
    public int local_port { get; set; } = 2801;

    /// <summary>
    ///     日志级别
    /// </summary>
    public int log_level { get; set; } = 1;

    /// <summary>
    ///     密码
    /// </summary>
    public List<string> password { get; set; }

    /// <summary>
    ///     远端地址
    /// </summary>
    public string remote_addr { get; set; }

    /// <summary>
    ///     远端端口
    /// </summary>
    public int remote_port { get; set; }

    /// <summary>
    ///     启动类型
    /// </summary>
    public string run_type { get; set; } = "client";

    public string server { get; set; }

    public string auth { get; set; }

    public TLS_Item tls { get; set; }

    public OBFS_Item obfs { get; set; }

    public SOCKS5_Item socks5 { get; set; }

    public HTTP_Item http { get; set; }

}


public class TLS_Item
{
    public string sni { get; set; }
    public bool insecure { get; set; }
}

public class OBFS_Item
{
    public string type { get; set; }
    public Salamander_Item salamander { get; set; }
}

public class Salamander_Item
{
    public string password { get; set; }
}

public class SOCKS5_Item
{
    public string listen { get; set; }
}

public class HTTP_Item
{
    public string listen { get; set; }
}