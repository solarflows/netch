namespace Netch.Models;

public class HysteriaConfig
{
    public bool AllowInsecure { get; set; } = false;

    public KcpConfig KcpConfig { get; set; } = new();

    public bool UseMux { get; set; } = false;

    public bool V2rayNShareLink { get; set; } = true;

    public bool HysteriaCone { get; set; } = true;

    public bool OBFSOpen { get; set; } = false;
}