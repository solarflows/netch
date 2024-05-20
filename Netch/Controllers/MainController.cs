using System.Diagnostics;
using Microsoft.VisualStudio.Threading;
using Netch.Interfaces;
using Netch.Models;
using Netch.Models.Modes;
using Netch.Servers;
using Netch.Services;
using Netch.Utils;
using static System.Net.Mime.MediaTypeNames;

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

        Log.Information("Start MainController: {Server} {Mode}", $"{server.Type}", $"[{(int)mode.Type}]{mode.i18NRemark}");

        if (await DnsUtils.LookupAsync(server.Hostname) == null)
            throw new MessageException(i18N.Translate("Lookup Server hostname failed"));

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

            if (Server is Socks5Server socks5 && (!socks5.Auth() || ModeController.Features.HasFlag(ModeFeature.SupportSocks5Auth)))
            {
                Socks5Server = socks5;
            }
            else
            {
                // Start Server Controller to get a local socks5 server
                Log.Debug("Server Information: {Data}", $"{server.Type} {server.MaskedData()}");
                if (Server.Type == "Socks5")
                {
                    ServerController = new Socks5Controller();
                }
                else if (Server.Type == "SS")
                {
                    if (Global.Settings.Core.SSCore == "SS")
                    {
                        ServerController = new XrayController();
                    } 
                    else if (Global.Settings.Core.SSCore == "SingBox")
                    {
                        ServerController = new SingBoxController();
                    }
                    else
                    {
                        ServerController = new V2rayController();
                    }
                }
                else if (Server.Type == "SSR")
                {
                    if (Global.Settings.Core.SSRCore == "SSR")
                    {
                        ServerController = new XrayController();
                    }
                    else if (Global.Settings.Core.SSRCore == "SingBox")
                    {
                        ServerController = new SingBoxController();
                    }
                    else
                    {
                        ServerController = new V2rayController();
                    }
                }
                if (Server.Type == "VLESS")
                {
                    if (Global.Settings.Core.VLESSCore == "Xray")
                    {
                        ServerController = new XrayController();
                    }
                    else if (Global.Settings.Core.VLESSCore == "SingBox")
                    {
                        ServerController = new SingBoxController();
                    }
                    else
                    {
                        ServerController = new V2rayController();
                    }
                }
                else if (Server.Type == "Hysteria2")
                {
                    if (Global.Settings.Core.Hysteria2Core == "Hysteria")
                    {
                        ServerController = new HysteriaController();
                    }
                    else if (Global.Settings.Core.Hysteria2Core == "SingBox")
                    {
                        ServerController = new SingBoxController();
                    }
                    else
                    {
                        ServerController = new V2rayController();
                    }
                }
                else if (Server.Type == "Trojan")
                {
                    if (Global.Settings.Core.TrojanCore == "Trojan")
                    {
                        ServerController = new TrojanController();
                    }
                    else if (Global.Settings.Core.TrojanCore == "SingBox")
                    {
                        ServerController = new SingBoxController();
                    }
                    else if (Global.Settings.Core.TrojanCore == "V2ray")
                    {
                        ServerController = new V2rayController();
                    }
                    else if (Global.Settings.Core.TrojanCore == "Xray")
                    {
                        ServerController = new XrayController();
                    }
                    else
                    {
                        ServerController = new V2rayController();
                    }
                }
                else if (Server.Type == "VMess")
                {
                    if (Global.Settings.Core.VMESSCore == "V2ray")
                    {
                        ServerController = new V2rayController();
                    }
                    else if (Global.Settings.Core.VMESSCore == "Xray")
                    {
                        ServerController = new XrayController();
                    }
                    else if (Global.Settings.Core.VMESSCore == "SingBox")
                    {
                        ServerController = new SingBoxController();
                    }
                    else
                    {
                        ServerController = new V2rayController();
                    }
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

            await ModeController.StartAsync(Socks5Server, mode);
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

        Log.Information("Stop Main Controller");
        StatusPortInfoText.Reset();

        var tasks = new[]
        {
            ServerController?.StopAsync() ?? Task.CompletedTask,
            ModeController?.StopAsync() ?? Task.CompletedTask
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            Log.Error(e, "MainController Stop Error");
        }

        ServerController = null;
        ModeController = null;
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

    public static Task<NatTypeTestResult> DiscoveryNatTypeAsync(CancellationToken ctx = default)
    {
        Debug.Assert(Socks5Server != null, nameof(Socks5Server) + " != null");
        return Socks5ServerTestUtils.DiscoveryNatTypeAsync(Socks5Server, ctx);
    }

    public static Task<int?> HttpConnectAsync(CancellationToken ctx = default)
    {
        Debug.Assert(Socks5Server != null, nameof(Socks5Server) + " != null");
        try
        {
            return Socks5ServerTestUtils.HttpConnectAsync(Socks5Server, ctx);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception e)
        {
            Log.Warning(e, "Unhandled Socks5ServerTestUtils.HttpConnectAsync Exception");
        }

        return Task.FromResult<int?>(null);
    }
}