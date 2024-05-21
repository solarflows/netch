using System.Net;
using System.Text.Json;
using Netch.Controllers;
using Netch.Interfaces;
using Netch.Models;

namespace Netch.Servers;

public class HysteriaController : Guard, IServerController
{
    public HysteriaController() : base(Global.Settings.Core.HysteriaBin)
    {
    }

    protected override IEnumerable<string> StartedKeywords => new[] { "SOCKS5 server listening", "update available" };

    protected override IEnumerable<string> FailedKeywords => new[] { "failed to" };

    public override string Name => "Hysteria";

    public ushort? Socks5LocalPort { get; set; }

    public string? LocalAddress { get; set; }

    public virtual async Task<Socks5Server> StartAsync(Server s)
    {
        var server = (Hysteria2Server)s;

        await using (var fileStream = new FileStream(Constants.TempConfig, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await JsonSerializer.SerializeAsync(fileStream, await HysteriaConfigUtils.GenerateClientConfigAsync(server), Global.NewCustomJsonSerializerOptions());
        }

        await StartGuardAsync("client -c ..\\data\\last.json");

        return new Socks5Server(IPAddress.Loopback.ToString(), this.Socks5LocalPort(), s.Hostname);
    }
}