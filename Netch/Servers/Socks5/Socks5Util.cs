using System.Text;
using Netch.Interfaces;
using Netch.Models;

namespace Netch.Servers;

public class Socks5Util : IServerUtil
{
    public ushort Priority { get; } = 0;

    public string TypeName { get; } = "SOCKS";

    public string FullName { get; } = "SOCKS5";

    public string ShortName { get; } = "SOCKS5";

    public string[] UriScheme { get; } = { "socks5", "socks" };

    public Type ServerType { get; } = typeof(Socks5Server);

    public void Edit(Server s)
    {
        new Socks5Form((Socks5Server)s).ShowDialog();
    }

    public void Create()
    {
        new Socks5Form().ShowDialog();
    }

    public string GetShareLink(Server s)
    {
        var server = (Socks5Server)s;
        var auth = server.Auth()
            ? $"{Uri.EscapeDataString(server.Username!)}:{Uri.EscapeDataString(server.Password!)}@"
            : "";
        var host = server.Hostname.Contains(':') && !server.Hostname.StartsWith('[') ? $"[{server.Hostname}]" : server.Hostname;
        var remark = !string.IsNullOrWhiteSpace(server.Remark) ? $"#{Uri.EscapeDataString(server.Remark)}" : "";
        return $"socks5://{auth}{host}:{server.Port}{remark}";
    }

    public IServerController GetController()
    {
        return new Socks5Controller();
    }

    public IEnumerable<Server> ParseUri(string text)
    {
        if (text.StartsWith("tg://socks?", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://t.me/socks?", StringComparison.OrdinalIgnoreCase))
        {
            var query = text.Contains('?') ? text[(text.IndexOf('?') + 1)..] : "";
            var dict = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(str => str.Split('=', 2))
                .Where(s => s.Length == 2)
                .ToDictionary(s => s[0].ToLowerInvariant(), s => Uri.UnescapeDataString(s[1]));

            if (!dict.TryGetValue("server", out var server) || !dict.TryGetValue("port", out var portStr) || !ushort.TryParse(portStr, out var port))
                throw new FormatException("Invalid Telegram Socks URL");

            var data = new Socks5Server
            {
                Hostname = server,
                Port = port
            };

            if (dict.TryGetValue("user", out var user) && !string.IsNullOrWhiteSpace(user))
                data.Username = user;

            if (dict.TryGetValue("pass", out var pass) && !string.IsNullOrWhiteSpace(pass))
                data.Password = pass;

            return new[] { data };
        }

        var remark = string.Empty;
        var hashIndex = text.IndexOf('#');
        if (hashIndex != -1)
        {
            remark = Uri.UnescapeDataString(text[(hashIndex + 1)..]);
            text = text[..hashIndex];
        }

        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd == -1)
            throw new FormatException("Invalid Socks URI");

        var content = text[(schemeEnd + 3)..];
        string? username = null;
        string? password = null;
        string hostPort;

        var atIndex = content.LastIndexOf('@');
        if (atIndex != -1)
        {
            var userInfo = content[..atIndex];
            hostPort = content[(atIndex + 1)..];

            if (userInfo.Contains(':'))
            {
                var parts = userInfo.Split(':', 2);
                username = Uri.UnescapeDataString(parts[0]);
                password = Uri.UnescapeDataString(parts[1]);
            }
            else
            {
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(userInfo));
                    if (decoded.Contains(':'))
                    {
                        var parts = decoded.Split(':', 2);
                        username = parts[0];
                        password = parts[1];
                    }
                    else
                    {
                        username = decoded;
                    }
                }
                catch
                {
                    username = Uri.UnescapeDataString(userInfo);
                }
            }
        }
        else
        {
            hostPort = content;
        }

        var queryIndex = hostPort.IndexOf('?');
        if (queryIndex != -1)
        {
            var query = hostPort[(queryIndex + 1)..];
            hostPort = hostPort[..queryIndex];

            var queryDict = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(str => str.Split('=', 2))
                .Where(s => s.Length == 2)
                .ToDictionary(s => s[0].ToLowerInvariant(), s => Uri.UnescapeDataString(s[1]));

            if (queryDict.TryGetValue("user", out var qUser) || queryDict.TryGetValue("username", out qUser))
                username ??= qUser;
            if (queryDict.TryGetValue("pass", out var qPass) || queryDict.TryGetValue("password", out qPass))
                password ??= qPass;
        }

        string hostname;
        ushort portNumber;

        if (hostPort.StartsWith('['))
        {
            var closeBracket = hostPort.IndexOf(']');
            if (closeBracket == -1)
                throw new FormatException("Invalid IPv6 address in Socks URI");

            hostname = hostPort[1..closeBracket];
            var colonIndex = hostPort.IndexOf(':', closeBracket);
            if (colonIndex == -1 || !ushort.TryParse(hostPort[(colonIndex + 1)..], out portNumber))
                throw new FormatException("Invalid port in Socks URI");
        }
        else
        {
            var colonIndex = hostPort.LastIndexOf(':');
            if (colonIndex == -1 || !ushort.TryParse(hostPort[(colonIndex + 1)..], out portNumber))
                throw new FormatException("Invalid host:port in Socks URI");

            hostname = hostPort[..colonIndex];
        }

        var serverObj = new Socks5Server
        {
            Hostname = hostname,
            Port = portNumber,
            Username = username,
            Password = password,
            Remark = remark
        };

        return new[] { serverObj };
    }

    public bool CheckServer(Server s)
    {
        return true;
    }
}