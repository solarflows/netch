using System.Diagnostics;
using Microsoft.VisualStudio.Threading;
using Netch.Interfaces;
using Netch.Models;
using Netch.Models.Modes;
using Netch.Servers;
using Netch.Servers.Singbox;
using Netch.Services;
using Netch.Utils;

namespace Netch.Controllers;

public static class MainController
{
    public static Socks5Server? Socks5Server { get; private set; }

    public static Server? Server { get; private set; }

    public static Mode? Mode { get; private set; }

    public static IServerController? ServerController { get; private set; }

    public static IModeController? ModeController { get; private set; }

    private static readonly AsyncSemaphore Lock = new(1);

    public static async Task StartAsync(Server server, Mode mode)
    {
        using var releaser = await Lock.EnterAsync();

        Log.Information("Starting MainController: Server=[{Type}] {Server} ({Hostname}:{Port}), Mode=[{ModeType}] {Mode}",
            server.Type, server.Remark, server.Hostname, server.Port, (int)mode.Type, mode.i18NRemark);

        var destination = await DnsUtils.LookupAsync(server.Hostname);
        if (destination == null)
        {
            Log.Error("Failed to resolve hostname for server: {Hostname}", server.Hostname);
            throw new MessageException(i18N.Translate("Lookup Server hostname failed"));
        }

        Log.Information("Server hostname {Hostname} resolved to {Address}", server.Hostname, destination);

        // TODO Disable NAT Type Test setting
        // cache STUN Server ip to prevent "Wrong STUN Server"
        DnsUtils.LookupAsync(Global.Settings.STUN_Server).Forget();

        Server = server;
        Mode = mode;

        await Task.WhenAll(Task.Run(NativeMethods.RefreshDNSCache), Task.Run(Firewall.AddNetchFwRules));

        try
        {
            ModeController = ModeService.GetModeControllerByType(mode.Type, out var modePort, out var portName);

            if (modePort != null)
                TryReleaseTcpPort((ushort)modePort, portName);

            if (Server is Socks5Server s5 && (!s5.Auth() || ModeController.Features.HasFlag(ModeFeature.SupportSocks5Auth)) && !Global.Settings.ShareLan && !Global.Settings.V2RayConfig.AllowHttp)
            {
                // 直连直通远端裸节点模式：不启动任何本地代理核心，实现零额外内存与零CPU占用
                Log.Information("Using direct Socks5 server mode (zero local core overhead): {Hostname}:{Port}", s5.Hostname, s5.Port);
                Socks5Server = s5;
                ServerController = null;
                StatusPortInfoText.Reset();
            }
            else
            {
                // Start Server Controller to get a local socks5 server
                Log.Debug("Server Information: {Data}", $"{server.Type} {server.MaskedData()}");

                if (Global.Settings.CoreType.Equals("sing-box", StringComparison.OrdinalIgnoreCase) && SingboxConfigUtils.IsSupported(server))
                {
                    ServerController = new SingboxController();
                }
                else
                {
                    ServerController = new V2rayController();
                }

                Global.MainForm.StatusText(i18N.TranslateFormat("Starting {0}", ServerController.Name));

                TryReleaseTcpPort(ServerController.Socks5LocalPort(), "Socks5");
                Socks5Server = await ServerController.StartAsync(server);

                StatusPortInfoText.Socks5Port = Socks5Server.Port;
                StatusPortInfoText.UpdateShareLan();
            }

            // Start Mode Controller
            Global.MainForm.StatusText(i18N.TranslateFormat("Starting {0}", ModeController.Name));
            Log.Information("Starting mode controller: {ModeName}", ModeController.Name);

            await ModeController.StartAsync(Socks5Server, mode);
            Log.Information("MainController started successfully. Core: {Core}, Mode: {Mode}", ServerController?.Name ?? "Direct", ModeController.Name);
        }
        catch (Exception e)
        {
            releaser.Dispose();
            await StopAsync();

            switch (e)
            {
                case DllNotFoundException:
                case FileNotFoundException:
                    throw new Exception(e.Message + "\n\n" + i18N.Translate("Missing File or runtime components"));
                case MessageException:
                    throw;
                default:
                    Log.Error(e, "Unhandled Exception When Start MainController");
                    Utils.Utils.Open(Constants.LogFile);
                    throw new MessageException($"{i18N.Translate("Unhandled Exception")}\n{e.Message}");
            }
        }
    }

    public static async Task StopAsync()
    {
        if (Lock.CurrentCount == 0)
        {
            (await Lock.EnterAsync()).Dispose();
            if (ServerController == null && ModeController == null)
                // stopped
                return;

            // else begin stop
        }

        using var _ = await Lock.EnterAsync();

        if (ServerController == null && ModeController == null)
            return;

        Log.Information("Stopping MainController: Core={Core}, Mode={Mode}", ServerController?.Name ?? "Direct", ModeController?.Name ?? "None");
        StatusPortInfoText.Reset();

        var tasks = new[]
        {
            ServerController?.StopAsync() ?? Task.CompletedTask,
            ModeController?.StopAsync() ?? Task.CompletedTask
        };

        try
        {
            await Task.WhenAll(tasks);
            Log.Information("MainController stopped successfully.");
        }
        catch (Exception e)
        {
            Log.Error(e, "MainController Stop Error");
        }

        ServerController = null;
        ModeController = null;
    }

    /// <summary>
    ///     热切换节点（若处于已启动状态，保持驱动/网卡/路由完全不重启，仅毫秒级重载代理后端总线）
    /// </summary>
    public static async Task HotSwitchServerAsync(Server newServer)
    {
        using var releaser = await Lock.EnterAsync();

        if (Global.MainForm.State != State.Started || ModeController == null)
        {
            Server = newServer;
            Global.Settings.ServerComboBoxSelectedIndex = Global.Settings.Server.IndexOf(newServer);
            return;
        }

        Log.Information("Hot-switching server: [{OldType}] {OldRemark} -> [{NewType}] {NewRemark}",
            Server?.Type, Server?.Remark, newServer.Type, newServer.Remark);

        Global.MainForm.StatusText(i18N.TranslateFormat("Switching to {0}", newServer.Remark));

        try
        {
            var destination = await DnsUtils.LookupAsync(newServer.Hostname);
            if (destination == null)
            {
                throw new MessageException(i18N.Translate("Lookup Server hostname failed"));
            }

            bool oldIsDirectSocks = ServerController == null && Server is Socks5Server;
            bool newIsDirectSocks = newServer is Socks5Server s5 && (!s5.Auth() || ModeController.Features.HasFlag(ModeFeature.SupportSocks5Auth)) && !Global.Settings.ShareLan && !Global.Settings.V2RayConfig.AllowHttp;

            if (oldIsDirectSocks != newIsDirectSocks)
            {
                // 裸 Socks5 直连模式与微内核模式跨模式切换，需要驱动重新绑定目标，执行平滑重启
                releaser.Dispose();
                var currentMode = Mode;
                await StopAsync();
                await StartAsync(newServer, currentMode!);
                return;
            }

            if (newIsDirectSocks)
            {
                Server = newServer;
                Socks5Server = (Socks5Server)newServer;
                await Task.Run(NativeMethods.RefreshDNSCache);
                Log.Information("Direct Socks5 hot-switched to {Hostname}:{Port}", newServer.Hostname, newServer.Port);
            }
            else
            {
                // 保持驱动层 (nfdriver / wintun / 路由表) 完全不动，仅重启代理微内核
                if (ServerController != null)
                {
                    await ServerController.StopAsync();
                }

                if (Global.Settings.CoreType.Equals("sing-box", StringComparison.OrdinalIgnoreCase) && SingboxConfigUtils.IsSupported(newServer))
                {
                    ServerController = new SingboxController();
                }
                else
                {
                    ServerController = new V2rayController();
                }

                TryReleaseTcpPort(ServerController.Socks5LocalPort(), "Socks5");
                Socks5Server = await ServerController.StartAsync(newServer);
                Server = newServer;

                StatusPortInfoText.Socks5Port = Socks5Server.Port;
                StatusPortInfoText.UpdateShareLan();

                await Task.Run(NativeMethods.RefreshDNSCache);
                Log.Information("Proxy core hot-switched successfully to [{Type}] {Remark}", newServer.Type, newServer.Remark);
            }

            Global.Settings.ServerComboBoxSelectedIndex = Global.Settings.Server.IndexOf(newServer);
            Global.MainForm.OnServerHotSwitched(newServer);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Hot-switching server failed");
            Global.MainForm.StatusText(i18N.Translate("Started"));
            throw;
        }
    }

    public static void PortCheck(ushort port, string portName, PortType portType = PortType.Both)
    {
        try
        {
            PortHelper.CheckPort(port, portType);
        }
        catch (PortInUseException)
        {
            throw new MessageException(i18N.TranslateFormat("The {0} port is in use.", $"{portName} ({port})"));
        }
        catch (PortReservedException)
        {
            throw new MessageException(i18N.TranslateFormat("The {0} port is reserved by system.", $"{portName} ({port})"));
        }
    }

    public static void TryReleaseTcpPort(ushort port, string portName)
    {
        foreach (var p in PortHelper.GetProcessByUsedTcpPort(port))
        {
            var fileName = p.MainModule?.FileName;
            if (fileName == null)
                continue;

            if (fileName.StartsWith(Global.NetchDir))
            {
                p.Kill();
                p.WaitForExit();
            }
            else
            {
                throw new MessageException(i18N.TranslateFormat("The {0} port is used by {1}.", $"{portName} ({port})", $"({p.Id}){fileName}"));
            }
        }

        PortCheck(port, portName, PortType.TCP);
    }

    public static async Task<NatTypeTestResult> DiscoveryNatTypeAsync(CancellationToken ctx = default)
    {
        if (Socks5Server == null)
            return new NatTypeTestResult { Result = "Error: Socks5Server is null" };

        Log.Information("Starting NAT type discovery via STUN server {StunServer}:{Port} through {SocksServer}...",
            Global.Settings.STUN_Server, Global.Settings.STUN_Server_Port, Socks5Server.Hostname);
        var result = await Socks5ServerTestUtils.DiscoveryNatTypeAsync(Socks5Server, ctx);
        Log.Information("NAT type discovery result: {Result}, LocalEnd: {Local}, PublicEnd: {Public}",
            result.Result, result.LocalEnd, result.PublicEnd);
        return result;
    }

    public static async Task<int?> HttpConnectAsync(CancellationToken ctx = default)
    {
        if (Socks5Server == null)
            return null;

        try
        {
            Log.Information("Testing HTTP connectivity through {SocksServer}...", Socks5Server.Hostname);
            var result = await Socks5ServerTestUtils.HttpConnectAsync(Socks5Server, ctx);
            Log.Information("HTTP connectivity test result: {Latency}ms", result);
            return result;
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception e)
        {
            Log.Warning(e, "Unhandled Socks5ServerTestUtils.HttpConnectAsync Exception");
        }

        return null;
    }
}