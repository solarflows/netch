using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Netch.Controllers;
using Netch.Interfaces;
using Netch.Models;
using Netch.Utils;

namespace Netch.Servers;

public class HysteriaController : Guard, IServerController
{
    public HysteriaController() : base("hysteria.exe")
    {
    }

    protected override IEnumerable<string> StartedKeywords => new[] { "HTTP proxy server listening", "no update available" };

    protected override IEnumerable<string> FailedKeywords => new[] { "failed to read client config", "failed to initialize client", "failed to" };

    public override string Name => "Hysteria";

    public ushort? Socks5LocalPort { get; set; }

    public string? LocalAddress { get; set; }

    public virtual async Task<Socks5Server> StartAsync(Server s)
    {
        var server = (Hysteria2Server)s;
     
        var hysteriaConfig = new HysteriaConfig
        {
            server = await server.AutoResolveHostnameAsync() + ":" + server.Port.ToString(),
            auth = server.Password,
            tls = new TLS_Item
            {
                sni = server.SNI,
                insecure = true
            },
            obfs = new OBFS_Item
            {
                type = "salamander",
                salamander = new Salamander_Item
                {
                    password = server.OBFSParam
                }
            },
            //socks5 = new SOCKS5_Item
            //{
            //    listen = this.LocalAddress() + ";" + this.Socks5LocalPort().ToString()
            //},
            http = new HTTP_Item
            {
                listen = this.LocalAddress() + ':' + (this.Socks5LocalPort() + 1).ToString()
            }

        };

        if (server.OBFS != "None" && server.OBFS.Length != 0)
        {
            hysteriaConfig.obfs = new OBFS_Item
            {
                type = "salamander",
                salamander = new Salamander_Item
                {
                    password = server.OBFSParam
                }
            };
        }

        await using (var fileStream = new FileStream(Constants.TempConfig, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await JsonSerializer.SerializeAsync(fileStream, hysteriaConfig, Global.NewCustomJsonSerializerOptions());
        }

        await StartGuardAsync("client -c ..\\data\\last.json -l debug");

        return new Socks5Server(IPAddress.Loopback.ToString(), this.Socks5LocalPort(), s.Hostname);
    }
}