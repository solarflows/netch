using Netch.Models;
using Netch.Utils;

namespace Netch.Servers;

public class SingBoxServer : Server
{
    public new int Delay { get; private set; } = -1;

    public override string Type { get; } = "SingBox";

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

    public string? OBFS { get; set; } = SingBoxGlobal.OBFSs[0];

    /// <summary>
    ///     混淆密码
    /// </summary>
    public string? OBFSParam { get; set; }

    public string? SNI { get; set; } = string.Empty;

    public int up_mbps { get; set; }

    public int down_mbps { get; set; }

    public async new Task<int> PingAsync()
    {
        Console.WriteLine(string.Format("SingBoxServer, PingAsync"));
        try
        {
            var destination = await DnsUtils.LookupAsync(Hostname);
            if (destination == null)
                return Delay = -2;

            var list = new Task<int>[3];
            for (var i = 0; i < 3; i++)
            {
                Task<int> PingCoreAsync()
                {
                    try
                    {
                        return Global.Settings.ServerTCPing ? Utils.Utils.TCPingAsync(destination, Port) : Utils.Utils.ICMPingAsync(destination);
                    }
                    catch (Exception)
                    {
                        return Task.FromResult(-4);
                    }
                }

                list[i] = PingCoreAsync();
            }

            var resTask = await Task.WhenAny(list[0], list[1], list[2]);

            return Delay = await resTask;
        }
        catch (Exception)
        {
            return Delay = -4;
        }
    }

}

public class SingBoxGlobal
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