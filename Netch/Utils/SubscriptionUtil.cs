using System.Net;
using Netch.Models;

namespace Netch.Utils;

public static class SubscriptionUtil
{
    private static readonly object ServerLock = new();

    public static Task UpdateServersAsync(bool? blProxy = false)
    {
        return Task.WhenAll(Global.Settings.Subscription.Select(item => UpdateServerCoreAsync(item, blProxy)));
    }

    private static async Task UpdateServerCoreAsync(Subscription item, bool? blProxy)
    {
        try
        {
            if (!item.Enable)
                return;

            var request = WebUtil.CreateRequest(item.Link);

            if (!string.IsNullOrEmpty(item.UserAgent))
                request.UserAgent = item.UserAgent;

            // Ìí¼Óproxy
            if (blProxy ?? true)
                request.Proxy = new WebProxy(Global.Settings.LocalAddress, Global.Settings.Socks5LocalPort);

            List<Server> servers;

            var (code, result) = await WebUtil.DownloadStringAsync(request);
            if (code == HttpStatusCode.OK)
                servers = ShareLink.ParseText(result);
            else
                throw new Exception($"{item.Remark} Response Status Code: {code}");

            foreach (var server in servers)
                server.Group = item.Remark;

            lock (ServerLock)
            {
                Global.Settings.Server.RemoveAll(server => server.Group.Equals(item.Remark));
                Global.Settings.Server.AddRange(servers);
            }

            Global.MainForm.NotifyTip(i18N.TranslateFormat("Update {1} server(s) from {0}", item.Remark, servers.Count));
        }
        catch (Exception e)
        {
            Global.MainForm.NotifyTip($"{i18N.TranslateFormat("Update servers failed from {0}", item.Remark)}\n{e.Message}", info: false);
            Log.Warning(e, "Update servers failed");
        }
    }
}