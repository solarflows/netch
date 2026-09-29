using System.Net;
using System.Text.Json;
using Netch.Interfaces;
using Netch.Models;
using Netch.Servers;
using Netch.Servers.Singbox;

namespace Netch.Controllers;

public class SingboxController : Guard, IServerController
{
    public SingboxController() : base("sing-box.exe")
    {
    }

    protected override IEnumerable<string> StartedKeywords => new[] { "sing-box started", "started" };

    protected override IEnumerable<string> FailedKeywords => new[] { "FATAL", "panic" };

    public override string Name => "sing-box";

    public ushort? Socks5LocalPort { get; set; }

    public string? LocalAddress { get; set; }

    public virtual async Task<Socks5Server> StartAsync(Server s)
    {
        await using (var fileStream = new FileStream(Constants.SingboxConfig, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            var config = await SingboxConfigUtils.GenerateClientConfigAsync(s);
            await JsonSerializer.SerializeAsync(fileStream, config, Global.NewCustomJsonSerializerOptions());
        }

        await StartGuardAsync("run -c ..\\data\\singbox.json");
        return new Socks5Server(IPAddress.Loopback.ToString(), this.Socks5LocalPort(), s.Hostname);
    }
}
