using Netch.Models;

namespace Netch.Servers;

public class WireGuardServer : Server
{
    public override string Type { get; } = "WireGuard";

    public override string MaskedData()
    {
        return $"{LocalAddresses} + {MTU}";
    }

    /// <summary>
    ///     本地地址
    /// </summary>
    public string LocalAddresses { get; set; } = "172.16.0.2/32";

    /// <summary>
    ///     节点公钥
    /// </summary>
    public string PeerPublicKey { get; set; } = string.Empty;

    /// <summary>
    ///     私钥
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    ///     节点预共享密钥
    /// </summary>
    public string? PreSharedKey { get; set; }

    /// <summary>
    ///     MTU
    /// </summary>
    public int MTU { get; set; } = 1420;

    /// <summary>
    ///     Reserved
    /// </summary>
    public string Reserved { get; set; } = "0,0,0";

    /// <summary>
    ///     EDomainStrategy
    /// </summary>
    public enum EDomainStrategy
    {
        ForceIPv6v4,
        ForceIPv6,
        ForceIPv4v6,
        ForceIPv4,
        ForceIP
    }

    /// <summary>
    ///     DomainStrategy
    /// </summary>
    public string DomainStrategy { get; set; } = EDomainStrategy.ForceIP.ToString();

    /// <summary>
    ///     GetDomainStrategyList
    /// </summary>
    public List<string> GetDomainStrategyList()
    {
        return Enum.GetValues<EDomainStrategy>().OfType<EDomainStrategy>().Select(x => x.ToString()).ToList();
    }

    /// <summary>
    ///     KeepAlive
    /// </summary>
    public int KeepAlive { get; set; } = 25;

    /// <summary>
    ///     AllowIPs
    /// </summary>
    public string AllowIPs { get; set; } = "0.0.0.0/0,127.0.0.1/32";

    /// <summary>
    ///     Workers
    /// </summary>
    public int Workers { get; set; } = 2;
}
