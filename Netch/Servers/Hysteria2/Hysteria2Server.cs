using Netch.Models;

namespace Netch.Servers;

public class Hysteria2Server : Server
{
    public override string Type { get; } = "Hysteria2";

    public override string MaskedData()
    {
        return "";
    }

    /// <summary>
    ///     密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    public string? ServerName { get; set; } = string.Empty;

    public string? Host { get; set; } = string.Empty;

    public string? OBFS { get; set; } = HysteriaGlobal.OBFSs[0];

    /// <summary>
    ///     混淆密码
    /// </summary>
    public string? OBFSParam { get; set; }

    public string? SNI { get; set; } = string.Empty;

}

public class HysteriaGlobal
{

    /// <summary>
    ///     Hysteria 混淆列表
    /// </summary>
    public static readonly List<string> OBFSs = new()
    {
        "none",
        "salamander"
    };

}