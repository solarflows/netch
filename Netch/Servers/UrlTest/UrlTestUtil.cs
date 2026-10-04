using Netch.Controllers;
using Netch.Forms;
using Netch.Interfaces;
using Netch.Models;

namespace Netch.Servers;

public class UrlTestUtil : IServerUtil
{
    public ushort Priority { get; } = 5;

    public string TypeName { get; } = "URLTest";

    public string FullName { get; } = "sing-box URLTest";

    public string ShortName { get; } = "URLTest";

    public string[] UriScheme { get; } = { "urltest" };

    public Type ServerType { get; } = typeof(UrlTestServer);

    public void Edit(Server s)
    {
        new UrlTestServerForm((UrlTestServer)s).ShowDialog();
    }

    public void Create()
    {
        new UrlTestServerForm().ShowDialog();
    }

    public string GetShareLink(Server s)
    {
        return string.Empty;
    }

    public IServerController GetController()
    {
        return new SingboxController();
    }

    public IEnumerable<Server> ParseUri(string text)
    {
        return Enumerable.Empty<Server>();
    }

    public bool CheckServer(Server s)
    {
        return s is UrlTestServer urlTest && urlTest.Outbounds.Count > 0;
    }
}
