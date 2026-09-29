using Netch.Interfaces;
using Netch.Services;

namespace Netch.Controllers;

public class DNSController : IController
{
    public string Name => "DNS Service";

    public static async Task StartAsync()
    {
        await DnsService.StartAsync();
    }

    public Task StopAsync()
    {
        return DnsService.StopAsync();
    }
}
