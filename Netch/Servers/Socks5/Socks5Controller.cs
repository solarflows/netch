using Netch.Models;

namespace Netch.Servers;

public class Socks5Controller : V2rayController
{
    public override string Name { get; } = "Socks5";

    public override Task<Socks5Server> StartAsync(Server s)
    {
        return base.StartAsync(s);
    }
}