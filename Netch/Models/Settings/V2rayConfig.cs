namespace Netch.Models;

public class V2rayConfig
{
    /// <summary>
    ///     使用Xray核心
    /// </summary>
    public bool XrayCore { get; set; } = true;

    public bool AllowInsecure { get; set; } = false;

    public KcpConfig KcpConfig { get; set; } = new();

    public bool UseMux { get; set; } = false;

    public bool V2rayNShareLink { get; set; } = true;

    public bool XrayCone { get; set; } = true;

    public bool TCPFastOpen { get; set; } = false;
}