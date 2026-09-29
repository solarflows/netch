namespace Netch.Models;

public class SingboxConfig
{
    public bool Sniffing { get; set; } = true;

    public bool UseMux { get; set; } = false;

    public bool TCPFastOpen { get; set; } = false;

    public bool AllowInsecure { get; set; } = false;
}
