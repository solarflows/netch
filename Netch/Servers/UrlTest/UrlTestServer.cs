using Netch.Models;

namespace Netch.Servers;

public class UrlTestServer : Server
{
    public override string Type { get; } = "URLTest";

    public override string MaskedData()
    {
        return $"{Outbounds.Count} candidate nodes | {Url}";
    }

    public UrlTestServer()
    {
        Group = "sing-box";
        Hostname = "127.0.0.1";
        Port = 0;
    }

    /// <summary>
    ///     候选出站标签列表 (选中的服务器 Remark 列表)
    /// </summary>
    public List<string> Outbounds { get; set; } = new();

    /// <summary>
    ///     是否启用规则自动匹配模式 (类似于 PassWall 自动优选)
    /// </summary>
    public bool UseCustomFilter { get; set; } = true;

    /// <summary>
    ///     匹配的订阅分组名称 (空或全部表示不限)
    /// </summary>
    public string MatchGroup { get; set; } = "";

    /// <summary>
    ///     包含的关键字/正则表达式 (如: 香港|HK|日本|JP)
    /// </summary>
    public string IncludePattern { get; set; } = "";

    /// <summary>
    ///     排除的关键字/正则表达式 (如: 官网|到期|重置|剩余|流量|频道|公告)
    /// </summary>
    public string ExcludePattern { get; set; } = "官网|到期|重置|剩余|流量|频道|公告";

    /// <summary>
    ///     测速 URL
    /// </summary>
    public string Url { get; set; } = "https://www.gstatic.com/generate_204";

    /// <summary>
    ///     测速间隔
    /// </summary>
    public string Interval { get; set; } = "3m";

    /// <summary>
    ///     容差 (毫秒)
    /// </summary>
    public int Tolerance { get; set; } = 50;

    /// <summary>
    ///     空闲超时
    /// </summary>
    public string IdleTimeout { get; set; } = "30m";

    /// <summary>
    ///     节点切换时是否中断现有连接
    /// </summary>
    public bool InterruptExistConnections { get; set; } = false;
}
