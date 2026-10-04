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
