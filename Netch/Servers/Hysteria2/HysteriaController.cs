using System.Net;
using System.Text.Json;
using Netch.Controllers;
using Netch.Interfaces;
using Netch.Models;
using Netch.Utils;

namespace Netch.Servers;

public class HysteriaController : Guard, IServerController
{
    public HysteriaController() : base("sing-box.exe")
    {
    }

    protected override IEnumerable<string> StartedKeywords => new[] { "started" };

    protected override IEnumerable<string> FailedKeywords => new[] { "config file not readable", "failed to" };

    public override string Name => "Hysteria (SagerNet)";

    public ushort? Socks5LocalPort { get; set; }

    public string? LocalAddress { get; set; }

    public virtual async Task<Socks5Server> StartAsync(Server s)
    {
        var server = (Hysteria2Server)s;

        await using (var fileStream = new FileStream(Constants.TempConfig, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await JsonSerializer.SerializeAsync(fileStream, await HysteriaConfigUtils.GenerateClientConfigAsync(server), Global.NewCustomJsonSerializerOptions());
        }

        await StartGuardAsync("run -c ..\\data\\last.json");

        return new Socks5Server(IPAddress.Loopback.ToString(), this.Socks5LocalPort(), s.Hostname);
    }
}