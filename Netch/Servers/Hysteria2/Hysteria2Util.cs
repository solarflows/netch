
using System.Text.RegularExpressions;
using System.Web;
using Netch.Interfaces;
using Netch.Models;
using Netch.Servers;
using Netch.Utils;

namespace Netch.Servers;

public class Hysteria2Util : IServerUtil
{
    public ushort Priority { get; } = 3;

    public string TypeName { get; } = "Hysteria2";

    public string FullName { get; } = "Hysteria2";

    public string ShortName { get; } = "H2";

    public string[] UriScheme { get; } = { "hysteria2" };

    public Type ServerType { get; } = typeof(Hysteria2Server);

    public void Edit(Server s)
    {
        new Hysteria2Form((Hysteria2Server)s).ShowDialog();
    }

    public void Create()
    {
        new Hysteria2Form().ShowDialog();
    }

    public string GetShareLink(Server s)
    {
        if (Global.Settings.HysteriaConfig.V2rayNShareLink)
        {
            var server = (Hysteria2Server)s;

            return $"hysteria2://{HttpUtility.UrlEncode(server.Password)}@{server.Hostname}:{server.Port}?sni={server.Host}#{server.Remark}";
        }

        return V2rayUtils.GetVShareLink(s);
    }

    public IServerController GetController()
    {
        return new HysteriaController();
    }

    public IEnumerable<Server> ParseUri(string text)
    {
        var data = new Hysteria2Server();

        text = text.Replace("/?", "?");
        

        if (text.Contains("#"))
        {
            data.Remark = HttpUtility.UrlDecode(text.Split('#')[1]);
            text = text.Split('#')[0];
        }

        var sni = "";
        if (text.Contains("?"))
        {
            var reg = new Regex(@"^(?<data>.+?)\?(.+)$");
            var regmatch = reg.Match(text);

            if (!regmatch.Success)
                throw new FormatException();

            sni = HttpUtility.UrlDecode(HttpUtility.ParseQueryString(new Uri(text).Query).Get("sni"));
            
            var obfs = HttpUtility.UrlDecode(HttpUtility.ParseQueryString(new Uri(text).Query).Get("obfs"));
            if (obfs != null)
            {
                data.OBFS = "salamander";
                data.OBFSParam = obfs;
            }

            text = regmatch.Groups["data"].Value;
        }

        var finder = new Regex(@"^hysteria2://(?<psk>.+?)@(?<server>.+):(?<port>\d+)");
        var match = finder.Match(text);
        if (!match.Success)
            throw new FormatException();

        data.Password = HttpUtility.UrlDecode(match.Groups["psk"].Value);
        data.Hostname = match.Groups["server"].Value;
        data.Port = ushort.Parse(match.Groups["port"].Value);

        if (sni != null && sni.Length != 0)
        {
            data.SNI = sni;
        }
        else
        {
            if (Utils.Utils.IsDomain(data.Hostname))
                data.SNI = data.Hostname;
        }

        return new[] { data };
    }

    public bool CheckServer(Server s)
    {
        return true;
    }
}