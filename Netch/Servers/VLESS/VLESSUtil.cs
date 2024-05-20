using Netch.Interfaces;
using Netch.Models;
using Netch.Utils;
using System.Text.RegularExpressions;
using System.Web;

namespace Netch.Servers;

public class VLESSUtil : IServerUtil
{
    public ushort Priority { get; } = 2;

    public string TypeName { get; } = "VLESS";

    public string FullName { get; } = "VLESS";

    public string ShortName { get; } = "VL";

    public string[] UriScheme { get; } = { "vless" };

    public Type ServerType { get; } = typeof(VLESSServer);

    public void Edit(Server s)
    {
        new VLESSForm((VLESSServer)s).ShowDialog();
    }

    public void Create()
    {
        new VLESSForm().ShowDialog();
    }

    public string GetShareLink(Server s)
    {
        return V2rayUtils.GetVShareLink(s, "vless");
    }

    public IServerController GetController()
    {
        if (Global.Settings.Core.VLESSCore == "Xray")
            return new XrayController();
        else if (Global.Settings.Core.VLESSCore == "SingBox")
            return new SingBoxController();
        else 
            return new V2rayController();
    }

    public IEnumerable<Server> ParseUri(string text)
    {
        var scheme = ShareLink.GetUriScheme(text).ToLower();
        var server = new VLESSServer();
        if (text.Contains("#"))
        {
            server.Remark = Uri.UnescapeDataString(text.Split('#')[1]);
            text = text.Split('#')[0];
        }

        if (text.Contains("?"))
        {
            var parameter = HttpUtility.ParseQueryString(text.Split('?')[1]);
            text = text.Substring(0, text.IndexOf("?", StringComparison.Ordinal));
            server.TransferProtocol = parameter.Get("type") ?? "tcp";
            server.PacketEncoding = parameter.Get("packetEncoding") ?? "xudp";
            server.EncryptMethod = parameter.Get("encryption") ?? scheme switch { "vless" => "none", _ => "auto" };
            server.Flow = parameter.Get("flow");
            switch (server.TransferProtocol)
            {
                case "tcp":
                    break;
                case "kcp":
                    server.FakeType = parameter.Get("headerType") ?? "none";
                    server.Path = Uri.UnescapeDataString(parameter.Get("seed") ?? "");
                    break;
                case "ws":
                    server.Path = Uri.UnescapeDataString(parameter.Get("path") ?? "/");
                    server.Host = Uri.UnescapeDataString(parameter.Get("host") ?? "");
                    break;
                case "h2":
                    server.Path = Uri.UnescapeDataString(parameter.Get("path") ?? "/");
                    server.Host = Uri.UnescapeDataString(parameter.Get("host") ?? "");
                    break;
                case "quic":
                    server.QUICSecure = parameter.Get("quicSecurity") ?? "none";
                    server.QUICSecret = parameter.Get("key") ?? "";
                    server.FakeType = parameter.Get("headerType") ?? "none";
                    break;
                case "grpc":
                    server.FakeType = parameter.Get("mode") ?? "gun";
                    server.Path = parameter.Get("serviceName") ?? "";
                    break;
            }

            server.TLSSecureType = parameter.Get("security") ?? "none";
            if (server.TLSSecureType != "none")
            {
                server.ServerName = parameter.Get("sni") ?? "";
            }
        }

        var finder = new Regex(@$"^{scheme}://(?<guid>.+?)@(?<server>.+):(?<port>\d+)");
        var match = finder.Match(text.Split('?')[0]);
        if (!match.Success)
            throw new FormatException();

        server.UserID = match.Groups["guid"].Value;
        server.Hostname = match.Groups["server"].Value;
        server.Port = ushort.Parse(match.Groups["port"].Value);

        return new[] { server };
    }

    public bool CheckServer(Server s)
    {
        return true;
    }
}