using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Netch.Interfaces;
using Netch.Models;
using Netch.Utils;

namespace Netch.Servers;

public class WireGuardUtil : IServerUtil
{
    public ushort Priority { get; } = 4;

    public string TypeName { get; } = "WireGuard";

    public string FullName { get; } = "WireGuard";

    public string ShortName { get; } = "WG";

    public string[] UriScheme { get; } = { "wireguard" };

    public Type ServerType { get; } = typeof(WireGuardServer);

    public void Edit(Server s)
    {
        new WireGuardForm((WireGuardServer)s).ShowDialog();
    }

    public void Create()
    {
        new WireGuardForm().ShowDialog();
    }

    public string GetShareLink(Server s)
    {
        var wg = (WireGuardServer)s;
        var query = new Dictionary<string, string>
        {
            { "publickey", wg.PeerPublicKey },
            { "address", wg.LocalAddresses },
            { "mtu", wg.MTU.ToString() }
        };

        if (!string.IsNullOrWhiteSpace(wg.PreSharedKey))
            query["presharedkey"] = wg.PreSharedKey;

        if (!string.IsNullOrWhiteSpace(wg.Reserved))
            query["reserved"] = wg.Reserved;

        if (!string.IsNullOrWhiteSpace(wg.AllowIPs))
            query["allowedips"] = wg.AllowIPs;

        var queryStr = string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var remarkStr = !string.IsNullOrWhiteSpace(wg.Remark) ? $"#{Uri.EscapeDataString(wg.Remark)}" : "";
        return $"wireguard://{Uri.EscapeDataString(wg.PrivateKey)}@{wg.Hostname}:{wg.Port}?{queryStr}{remarkStr}";
    }

    public IServerController GetController()
    {
        return new V2rayController();
    }

    public IEnumerable<Server> ParseUri(string text)
    {
        if (!text.StartsWith("wireguard://", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Invalid WireGuard URI");

        var server = new WireGuardServer();
        if (text.Contains('#'))
        {
            server.Remark = Uri.UnescapeDataString(text.Split('#')[1]);
            text = text.Split('#')[0];
        }

        if (text.Contains('?'))
        {
            var query = HttpUtility.ParseQueryString(text.Split('?')[1]);
            text = text[..text.IndexOf('?')];

            server.PeerPublicKey = query.Get("publickey") ?? "";
            server.LocalAddresses = query.Get("address") ?? server.LocalAddresses;
            server.PreSharedKey = query.Get("presharedkey");
            server.Reserved = query.Get("reserved") ?? server.Reserved;
            server.AllowIPs = query.Get("allowedips") ?? server.AllowIPs;

            if (int.TryParse(query.Get("mtu"), out var mtu))
                server.MTU = mtu;
        }

        text = text["wireguard://".Length..];
        var atIndex = text.IndexOf('@');
        if (atIndex > 0)
        {
            server.PrivateKey = Uri.UnescapeDataString(text[..atIndex]);
            text = text[(atIndex + 1)..];
        }

        var lastColon = text.LastIndexOf(':');
        if (lastColon > 0)
        {
            server.Hostname = text[..lastColon];
            server.Port = ushort.Parse(text[(lastColon + 1)..]);
        }
        else
        {
            server.Hostname = text;
        }

        return new[] { server };
    }

    public bool CheckServer(Server s)
    {
        return true;
    }
}