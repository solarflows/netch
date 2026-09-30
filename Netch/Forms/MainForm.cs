using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32;
using Netch.Controllers;
using Netch.Enums;
using Netch.Forms.ModeForms;
using Netch.Interfaces;
using Netch.Models;
using Netch.Models.Modes;
using Netch.Properties;
using Netch.Servers;
using Netch.Services;
using Netch.Utils;

namespace Netch.Forms;

[Fody.ConfigureAwait(true)]
public partial class MainForm : Form
{
    #region Start

    private readonly Dictionary<string, object> _mainFormText = new();

    private bool _textRecorded;

    public MainForm()
    {
        InitializeComponent();
        NotifyIcon.Icon = Icon = Resources.icon;

        AddAddServerToolStripMenuItems();

        #region i18N Translations

        if (Flags.NoSupport)
            _mainFormText.Add(Name, new[] { "{0} ({1})", "Netch", "No Support" });

        _mainFormText.Add(UninstallServiceToolStripMenuItem.Name, new[] { "Uninstall {0}", "NF Service" });

        #endregion

        // 监听电源事件
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    private void AddAddServerToolStripMenuItems()
    {
        var socks5Item = new ToolStripMenuItem
        {
            Name = "AddSocks5BareServerToolStripMenuItem",
            Size = new Size(259, 22),
            Text = i18N.Translate("Add [Socks5] Server")
        };
        _mainFormText[socks5Item.Name] = "Add [Socks5] Server";
        socks5Item.Click += (_, _) =>
        {
            Hide();
            new Socks5Form().ShowDialog();
            LoadServers();
            Utils.Configuration.SaveAsync().Forget();
            Show();
        };
        ServerToolStripMenuItem.DropDownItems.Add(socks5Item);

        var xrayItem = new ToolStripMenuItem
        {
            Name = "AddXrayServerToolStripMenuItem",
            Size = new Size(259, 22),
            Text = i18N.Translate("Add [Xray] Server")
        };
        _mainFormText[xrayItem.Name] = "Add [Xray] Server";
        xrayItem.Click += (_, _) =>
        {
            Hide();
            new XrayServerForm().ShowDialog();
            LoadServers();
            Utils.Configuration.SaveAsync().Forget();
            Show();
        };
        ServerToolStripMenuItem.DropDownItems.Add(xrayItem);

        var singboxItem = new ToolStripMenuItem
        {
            Name = "AddSingboxServerToolStripMenuItem",
            Size = new Size(259, 22),
            Text = i18N.Translate("Add [sing-box] Server")
        };
        _mainFormText[singboxItem.Name] = "Add [sing-box] Server";
        singboxItem.Click += (_, _) =>
        {
            Hide();
            new SingboxServerForm().ShowDialog();
            LoadServers();
            Utils.Configuration.SaveAsync().Forget();
            Show();
        };
        ServerToolStripMenuItem.DropDownItems.Add(singboxItem);
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        // 计算 ComboBox绘制 目标宽度
        RecordSize();

        LoadServers();
        SelectLastServer();
        DelayTestHelper.UpdateTick(true);

        ModeService.Instance.Load();

        // 加载翻译
        TranslateControls();

        // 隐藏 ConnectivityStatusLabel
        ConnectivityStatusVisible(false);

        // 加载快速配置
        LoadProfiles();

        // 检查更新
        if (Global.Settings.CheckUpdateWhenOpened)
            CheckUpdateAsync().Forget();

        // 检查订阅更新
        if (Global.Settings.UpdateServersWhenOpened)
            UpdateServersFromSubscriptionAsync().Forget();

        // 打开软件时启动加速，产生开始按钮点击事件
        if (Global.Settings.StartWhenOpened)
            ControlButton.PerformClick();

        // 应用界面主题
        ThemeService.Apply(this);

        Program.SingleInstance.StartListenServer();
    }

    private void RecordSize()
    {
        _configurationGroupBoxHeight = ConfigurationGroupBox.Height;
        _profileConfigurationHeight = ConfigurationGroupBox.Controls[0].Height / 3; // 因为 AutoSize, 所以得到的是Controls的总高度
        _profileGroupBoxPaddingHeight = ProfileGroupBox.Height - ProfileTable.Height;
        _profileTableHeight = ProfileTable.Height;
    }

    private void TranslateControls()
    {
        #region Record English

        if (!_textRecorded)
        {
            void RecordText(Component component)
            {
                try
                {
                    RecordControlText(component);
                }
                catch (ArgumentException)
                {
                    // Handle the exception appropriately, if needed.
                }
            }

            Utils.Utils.ComponentIterator(this, RecordText);
            Utils.Utils.ComponentIterator(NotifyMenu, RecordText);
            _textRecorded = true;
        }

        #endregion

        #region Translate

        void TranslateText(Component component)
        {
            TranslateControlText(component);
        }

        Utils.Utils.ComponentIterator(this, TranslateText);
        Utils.Utils.ComponentIterator(NotifyMenu, TranslateText);

        UsedBandwidthLabel.Text = $"{i18N.Translate("Used", ": ")}0 KB";
        State = State; // Consider removing this line if it's not necessary
        VersionLabel.Text = UpdateChecker.Version;
    }

    private void RecordControlText(Component component)
    {
        switch (component)
        {
            case TextBoxBase:
            case ListControl:
                break;
            case Control c:
                if (!string.IsNullOrEmpty(c.Name) && !_mainFormText.ContainsKey(c.Name))
                    _mainFormText[c.Name] = c.Text ?? string.Empty;
                break;
            case ToolStripItem c:
                if (!string.IsNullOrEmpty(c.Name) && !_mainFormText.ContainsKey(c.Name))
                    _mainFormText[c.Name] = c.Text ?? string.Empty;
                break;
        }
    }

    private void TranslateControlText(Component component)
    {
        switch (component)
        {
            case TextBoxBase:
            case ListControl:
                break;
            case Control c:
                if (!string.IsNullOrEmpty(c.Name) && _mainFormText.ContainsKey(c.Name))
                    c.Text = ControlText(c.Name);

                break;
            case ToolStripItem c:
                if (!string.IsNullOrEmpty(c.Name) && _mainFormText.ContainsKey(c.Name))
                    c.Text = ControlText(c.Name);

                break;
        }
    }

    private string ControlText(string name)
    {
        var value = _mainFormText[name];
        if (value.Equals(string.Empty))
            return string.Empty;

        if (value is object[] values)
            return i18N.TranslateFormat((string)values.First(), values.Skip(1).ToArray());

        return i18N.Translate(value);
    }

    #endregion

    #region Controls

    #region MenuStrip

    #region Server

    private async void ImportServersFromClipboardToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var texts = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(texts))
            return;

        var servers = ShareLink.ParseText(texts);
        foreach (var server in servers)
            server.Group = Constants.DefaultGroup;

        Global.Settings.Server.AddRange(servers);
        NotifyTip(i18N.TranslateFormat("Import {0} server(s) form Clipboard", servers.Count));

        LoadServers();
        await Utils.Configuration.SaveAsync();
    }

    private async void AddServerToolStripMenuItem_Click(object? sender, EventArgs? e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not IServerUtil util)
            return;

        Hide();
        util.Create();

        LoadServers();
        await Utils.Configuration.SaveAsync();
        Show();
    }

    #endregion

    #region Mode

    private void CreateProcessModeToolStripButton_Click(object sender, EventArgs e)
    {
        Hide();
        new ProcessForm().ShowDialog();
        Show();
    }

    private void CreateRouteTableModeToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Hide();
        new RouteForm().ShowDialog();
        Show();
    }

    private void ReloadModesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Enabled = false;
        try
        {
            ModeService.Instance.Load();
        }
        finally
        {
            Enabled = true;
        }
    }

    #endregion

    #region Subscription

    private void ManageSubscriptionLinksToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Hide();
        new SubscriptionForm().ShowDialog();
        LoadServers();
        Show();
    }

    private async void UpdateServersFromSubscriptionLinksToolStripMenuItem_Click(object sender, EventArgs e)
    {
        await UpdateServersFromSubscriptionAsync();
    }

    private async Task UpdateServersFromSubscriptionAsync()
    {
        void DisableItems(bool v)
        {
            MenuStrip.Enabled = ConfigurationGroupBox.Enabled = ProfileGroupBox.Enabled = ControlButton.Enabled = v;
        }

        if (Global.Settings.Subscription.Count <= 0)
        {
            MessageBoxX.Show(i18N.Translate("No subscription link"));
            return;
        }

        StatusText(i18N.Translate("Updating..."));
        DisableItems(false);

        try
        {
            await SubscriptionUtil.UpdateServersAsync();

            LoadServers();
            await Utils.Configuration.SaveAsync();
            StatusText(i18N.Translate("Servers updated"));
        }
        catch (Exception e)
        {
            NotifyTip(i18N.Translate("Unhandled update servers error") + "\n" + e.Message, info: false);
            Log.Error(e, "Unhandled Update servers error");
        }
        finally
        {
            DisableItems(true);
        }
    }

    private async void UpdateBypassIPsFromUrlLinksToolStripMenuItem_Click(object sender, EventArgs e)
    {
        await UpdateBypassIPsFromUrlAsync();
    }

    private async Task UpdateBypassIPsFromUrlAsync()
    {
        // 根据传入的布尔值 v 来禁用或启用一些 UI 元素
        void DisableItems(bool v)
        {
            // 更新 UI 元素的可用状态
            MenuStrip.Enabled = ConfigurationGroupBox.Enabled = ProfileGroupBox.Enabled = ControlButton.Enabled = v;
        }

        // 显示状态文本
        StatusText(i18N.Translate("Updating..."));
        DisableItems(false);

        try
        {
            // 获取远程绕过 IP 内容
            string displayName = Global.Settings.BILU_Server;
            if (!BiluServer.NamesAndUrls.TryGetValue(displayName, out string? url))
            {
                MessageBoxX.Show(i18N.Translate("Invalid update link"));
                return;
            }

#if DEBUG
            if (!string.IsNullOrEmpty(url))
            {
                // 使用 Url 执行相应的检查
                Console.WriteLine($"The URL for selected server ({displayName}) is: {url}");
            }
            else
            {
                // 处理未找到匹配项的情况
                Console.WriteLine($"No URL found for selected server ({displayName})");
            }
#endif

            string remoteContent = await DownloadStringAsync(url);

            // 获取本地文件内容
            string localFilePath = "mode\\Bypass LAN and China.txt";
            string localContent = File.Exists(localFilePath) ? File.ReadAllText(localFilePath) ?? string.Empty : string.Empty;

            // 跳过本地文件的前2行
            string[] localLines = localContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();
            localContent = string.Join("\n", localLines);

            // 比较本地和远程内容
            int updatedEntries = 0;
            if (localContent != remoteContent)
            {
                // 检查并更新本地文件及获取更新的条目数量
                updatedEntries = UpdateLocalFile(localFilePath, remoteContent);
                // 如果更新的条目数量为0
                if (updatedEntries == 0)
                {
                    // 本地内容与远程内容一致时的提示
                    StatusText(i18N.Translate("Bypass IPs are already up-to-date"));
                    return;
                }
            }
            else
            {
                // 本地内容与远程内容一致时的提示
                StatusText(i18N.Translate("Bypass IPs are already up-to-date"));
                return;
            }

            // 显示更新信息和行数
            StatusText(i18N.TranslateFormat("Bypass IPs updated {0} entries", updatedEntries));
        }
        catch (Exception e)
        {
            // 异常处理
            NotifyTip("Unhandled update bypass IPs error\n" + e.Message, info: false);
            Log.Error(e, "Unhandled Update bypass IPs error");
        }
        finally
        {
            // 恢复 UI 元素的可用状态
            DisableItems(true);
        }
    }

    private static int UpdateLocalFile(string filePath, string newContent, int skipLines = 2)
    {
        var newLines = (newContent ?? string.Empty)
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (File.Exists(filePath))
        {
            var oldLines = File.ReadAllLines(filePath)
                .Select(s => s.Trim())
                .ToList();

            var oldHeaders = oldLines.Take(Math.Min(skipLines, oldLines.Count)).ToList();
            var oldPayload = oldLines.Skip(skipLines).ToList();

            if (!oldPayload.SequenceEqual(newLines))
            {
                var updatedContent = new List<string>(oldHeaders);
                updatedContent.AddRange(newLines);
                File.WriteAllLines(filePath, updatedContent);
                return newLines.Count;
            }

            return 0;
        }
        else
        {
            File.WriteAllLines(filePath, newLines);
            return newLines.Count;
        }
    }

    private static async Task<string> DownloadStringAsync(string url)
    {
        using HttpClient httpClient = new();
        try
        {
            // 发送 GET 请求获取内容
            HttpResponseMessage response = await httpClient.GetAsync(url);

            // 确保请求成功
            response.EnsureSuccessStatusCode();

            // 读取并返回内容
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"HTTP request error: {ex.Message}");
        }
    }

    #endregion

    #region Options

    private async void CheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        void OnNewVersionNotFound(object? o, EventArgs? args)
        {
            NotifyTip(i18N.Translate("Already latest version"));
        }

        void OnNewVersionFoundFailed(object? o, EventArgs? args)
        {
            NotifyTip(i18N.Translate("Check for update failed"), info: false);
        }

        try
        {
            UpdateChecker.NewVersionNotFound += OnNewVersionNotFound;
            UpdateChecker.NewVersionFoundFailed += OnNewVersionFoundFailed;
            await CheckUpdateAsync();
        }
        finally
        {
            UpdateChecker.NewVersionNotFound -= OnNewVersionNotFound;
            UpdateChecker.NewVersionFoundFailed -= OnNewVersionFoundFailed;
        }
    }

    private void OpenDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Utils.Utils.Open(".\\");
    }

    private async void CleanDNSCacheToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            await Task.Run(() =>
            {
                // 捕获 RefreshDNSCache 的结果
                int result = (int)NativeMethods.RefreshDNSCache();

                // 检查结果并处理任何错误
                if (result != 0)
                {
                    // 处理错误，记录错误，或采取适当的行动
                    // 例如：throw new SomeException("DNS缓存清理失败");
                }

                // 继续执行其他代码
                DnsUtils.ClearCache();
            });

            NotifyTip(i18N.Translate("DNS cache cleanup succeeded"));
        }
        catch (Exception)
        {
            // ignored
        }
        finally
        {
            StatusText();
        }
    }

    private async void UninstallServiceToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Enabled = false;
        StatusText(i18N.TranslateFormat("Uninstalling {0}", "NF Service"));
        try
        {
            var task = Task.Run(NFController.UninstallDriver);

            if (await task)
            {
                NotifyTip(i18N.TranslateFormat("{0} has been uninstalled", "NF Service"));
            }
            else
            {
                NotifyTip(i18N.TranslateFormat("Failed to uninstall {0}", "NF Service"));
            }
        }
        finally
        {
            StatusText();
            Enabled = true;
        }
    }

    private void RemoveNetchFirewallRulesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Firewall.RemoveNetchFwRules(true);
    }

    private void ShowHideConsoleToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var windowStyles = (WINDOW_STYLE)PInvoke.GetWindowLong(new HWND(Program.ConsoleHwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var visible = windowStyles.HasFlag(WINDOW_STYLE.WS_VISIBLE);
        PInvoke.ShowWindow(Program.ConsoleHwnd, visible ? SHOW_WINDOW_CMD.SW_HIDE : SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
    }

    #endregion

    /// <summary>
    ///     菜单栏强制退出
    /// </summary>
    private void ForceExitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Exit(true);
    }

    private void VersionLabel_Click(object sender, EventArgs e)
    {
        Utils.Utils.Open($"https://github.com/{UpdateChecker.Owner}/{UpdateChecker.Repo}/releases");
    }

    private async void NewVersionLabel_Click(object sender, EventArgs e)
    {
        if (ModifierKeys == Keys.Control || !UpdateChecker.LatestRelease!.assets.Any())
        {
            Utils.Utils.Open(UpdateChecker.LatestVersionUrl!);
            return;
        }

        if (MessageBoxX.Show(i18N.Translate($"Download and install now?\n\n{UpdateChecker.GetLatestReleaseContent()}"), confirm: true) !=
            DialogResult.OK)
            return;

        NotifyTip(i18N.Translate("Start downloading new version"));
        NewVersionLabel.Enabled = false;
        NewVersionLabel.Text = "...";

        try
        {
            var progress = new Progress<int>();
            progress.ProgressChanged += (_, percentage) => { NewVersionLabel.Text = $"{percentage}%"; };

            string downloadDirectory = Path.Combine(Global.NetchDir, "data");

            var (updateFileName, sha256) = UpdateChecker.GetLatestUpdateFileNameAndHash();
            var updateFileUrl = UpdateChecker.LatestRelease.assets[0].browser_download_url!;

            var updateFileFullName = Path.Combine(downloadDirectory, updateFileName);
            var updater = new Updater(updateFileFullName, Global.NetchDir);

            var downloaded = false;
            if (File.Exists(updateFileFullName))
            {
                var fileHash = await Utils.Utils.Sha256CheckSumAsync(updateFileFullName);
                if (fileHash == sha256)
                    downloaded = true;
                else
                    File.Delete(updateFileFullName);
            }

            if (!downloaded)
            {
                try
                {
                    await WebUtil.DownloadFileAsync(updateFileUrl, updateFileFullName, progress);
                }
                catch (Exception e1)
                {
                    Log.Warning(e1, "Download Update File Failed");
                    throw new MessageException($"Download Update File Failed: {e1.Message}");
                }

                var fileHash = await Utils.Utils.Sha256CheckSumAsync(updateFileFullName);
                if (fileHash != sha256)
                    throw new MessageException(i18N.Translate("The downloaded file has the wrong hash"));
            }

            await StopAsync();
            await Utils.Configuration.SaveAsync();

            // Update
            await Task.Run(updater.ApplyUpdate);

            // release mutex, exit
            Program.SingleInstance.Dispose();
            Process.Start(Global.NetchExecutable);
            Environment.Exit(0);
        }
        catch (MessageException exception)
        {
            NotifyTip(exception.Message, info: false);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Unhandled Update error");
            NotifyTip(exception.Message, info: false);
        }
        finally
        {
            NewVersionLabel.Visible = false;
            NewVersionLabel.Enabled = true;
        }
    }

    private void AboutToolStripButton_Click(object sender, EventArgs e)
    {
        Hide();
        new AboutForm().ShowDialog();
        Show();
    }

    private void FAQToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Utils.Utils.Open("https://docs.netch.org");
    }

    #endregion

    #region ControlButton

    private async void ControlButton_Click(object? sender, EventArgs? e)
    {
        if (!IsWaiting())
        {
            await StopCoreAsync();
            return;
        }

        Utils.Configuration.SaveAsync().Forget();

        // 服务器、模式 需选择
        if (ServerComboBox.SelectedItem is not Server server)
        {
            MessageBoxX.Show(i18N.Translate("Please select a server first"));
            return;
        }

        if (ModeComboBox.SelectedItem is not Mode mode)
        {
            MessageBoxX.Show(i18N.Translate("Please select a mode first"));
            return;
        }

        Log.Information("Starting connection: Server=[{Type}] {Server} ({Hostname}:{Port}), Mode=[{ModeType}] {Mode}",
            server.Type, server.Remark, server.Hostname, server.Port, mode.Type, mode.Remark);

        State = State.Starting;

        try
        {
            await MainController.StartAsync(server, mode);
        }
        catch (Exception exception)
        {
            State = State.Stopped;
            StatusText(i18N.Translate("Start failed"));
            MessageBoxX.Show(exception.Message, LogLevel.ERROR);
            return;
        }

        State = State.Started;

        Task.Run(Bandwidth.NetTraffic).Forget();
        UpdateDnsStatusLabel();
        DiscoveryNatTypeAsync().Forget();
        HttpConnectAsync().Forget();

        if (Global.Settings.MinimizeWhenStarted)
            Minimize();

        // 自动检测延迟
        async Task StartedPingAsync()
        {
            while (State == State.Started)
            {
                if (Global.Settings.StartedPingInterval >= 0)
                {
                    await server.PingAsync();
                    ServerComboBox.Refresh();

                    await Task.Delay(Global.Settings.StartedPingInterval * 1000);
                }
                else
                {
                    await Task.Delay(5000);
                }
            }
        }

        StartedPingAsync().Forget();
    }

    #endregion

    #region SettingsButton

    private void SettingsButton_Click(object sender, EventArgs e)
    {
        Log.Information("Opening SettingsForm dialog");
        var oldSettings = Global.Settings.ShallowCopy();

        Hide();
        new SettingForm().ShowDialog();

        if (oldSettings.Language != Global.Settings.Language)
        {
            i18N.Load(Global.Settings.Language);
            TranslateControls();
            LoadModes();
            LoadProfiles();
        }

        if (oldSettings.DetectionTick != Global.Settings.DetectionTick)
            DelayTestHelper.UpdateTick(true);

        if (oldSettings.ProfileCount != Global.Settings.ProfileCount)
            LoadProfiles();

        if (oldSettings.Theme != Global.Settings.Theme)
            ThemeService.Apply(this);

        Log.Information("SettingsForm closed. Current: Core={Core}, Theme={Theme}, Lang={Lang}, NotifyOnMin={Notify}",
            Global.Settings.CoreType, Global.Settings.Theme, Global.Settings.Language, Global.Settings.NotifyOnMinimize);

        Show();
    }

    #endregion

    #region Server

    private void LoadServers()
    {
        ServerComboBox.Items.Clear();
        ServerComboBox.Items.AddRange(Global.Settings.Server.Cast<object>().ToArray());
        SelectLastServer();
    }

    private void SelectLastServer()
    {
        // 如果值合法，选中该位置
        if (Global.Settings.ServerComboBoxSelectedIndex > 0 && Global.Settings.ServerComboBoxSelectedIndex < ServerComboBox.Items.Count)
            ServerComboBox.SelectedIndex = Global.Settings.ServerComboBoxSelectedIndex;
        // 如果值非法，且当前 ServerComboBox 中有元素，选择第一个位置
        else if (ServerComboBox.Items.Count > 0)
            ServerComboBox.SelectedIndex = 0;

        // 如果当前 ServerComboBox 中没元素，不做处理
    }

    private void ServerComboBox_SelectionChangeCommitted(object sender, EventArgs o)
    {
        Global.Settings.ServerComboBoxSelectedIndex = ServerComboBox.SelectedIndex;
        if (ServerComboBox.SelectedItem is Server s)
        {
            Log.Information("Selected server changed: [{Type}] {Remark} ({Hostname}:{Port})", s.Type, s.Remark, s.Hostname, s.Port);
        }
    }

    private async void EditServerPictureBox_Click(object sender, EventArgs e)
    {
        // 当前ServerComboBox中至少有一项
        if (ServerComboBox.SelectedItem is not Server server)
        {
            MessageBoxX.Show(i18N.Translate("Please select a server first"));
            return;
        }

        Hide();
        if (server is Socks5Server s5 && s5.Group != "Xray" && s5.Group != "sing-box")
        {
            new Socks5Form(s5).ShowDialog();
        }
        else if (server.Group == "sing-box" || server is WireGuardServer)
        {
            new SingboxServerForm(server).ShowDialog();
        }
        else
        {
            new XrayServerForm(server).ShowDialog();
        }

        LoadServers();
        await Utils.Configuration.SaveAsync();
        Show();
    }

    private async void SpeedPictureBox_Click(object sender, EventArgs e)
    {
        void Enable()
        {
            ServerComboBox.Refresh();
            Enabled = true;
            StatusText();
        }

        Enabled = false;
        StatusText(i18N.Translate("Testing"));

        if (!IsWaiting() || ModifierKeys == Keys.Control)
        {
            (ServerComboBox.SelectedItem as Server)?.PingAsync();
            Enable();
        }
        else
        {
            await DelayTestHelper.PerformTestAsync(true);
            Enable();
        }
    }

    private void CopyLinkPictureBox_Click(object sender, EventArgs e)
    {
        // 当前ServerComboBox中至少有一项
        if (ServerComboBox.SelectedItem is not Server server)
        {
            MessageBoxX.Show(i18N.Translate("Please select a server first"));
            return;
        }

        try
        {
            //听说巨硬BUG经常会炸，所以Catch一下 :D
            string text;
            if (ModifierKeys == Keys.Control)
                text = ShareLink.GetNetchLink(server);
            else
                text = ShareLink.GetShareLink(server);

            Clipboard.SetText(text);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void DeleteServerPictureBox_Click(object sender, EventArgs e)
    {
        // 当前 ServerComboBox 中至少有一项
        if (ServerComboBox.SelectedItem is not Server server)
        {
            MessageBoxX.Show(i18N.Translate("Please select a server first"));
            return;
        }

        Global.Settings.Server.Remove(server);
        LoadServers();
    }

    #endregion

    #region Mode

    public void LoadModes()
    {
        if (InvokeRequired)
        {
            Invoke(LoadModes);
            return;
        }

        ModeComboBox.Items.Clear();
        ModeComboBox.Items.AddRange(Global.Modes.Cast<object>().ToArray());
        ModeComboBox.Tag = null;
        SelectLastMode();
    }

    private void SelectLastMode()
    {
        // 如果值合法，选中该位置
        if (Global.Settings.ModeComboBoxSelectedIndex > 0 && Global.Settings.ModeComboBoxSelectedIndex < ModeComboBox.Items.Count)
            ModeComboBox.SelectedIndex = Global.Settings.ModeComboBoxSelectedIndex;
        // 如果值非法，且当前 ModeComboBox 中有元素，选择第一个位置
        else if (ModeComboBox.Items.Count > 0)
            ModeComboBox.SelectedIndex = 0;

        // 如果当前 ModeComboBox 中没元素，不做处理
    }

    private void ModeComboBox_SelectionChangeCommitted(object sender, EventArgs o)
    {
        try
        {
            if (ModeComboBox.SelectedItem is Mode mode)
            {
                Global.Settings.ModeComboBoxSelectedIndex = Global.Modes.IndexOf(mode);
                Log.Information("Selected mode changed: [{Type}] {Remark} ({FullName})", mode.Type, mode.Remark, mode.FullName);
            }
        }
        catch
        {
            Global.Settings.ModeComboBoxSelectedIndex = 0;
        }
    }

    private void EditModePictureBox_Click(object sender, EventArgs e)
    {
        // 当前ModeComboBox中至少有一项
        if (ModeComboBox.SelectedIndex == -1 || ModeComboBox.SelectedItem is not Mode mode)
        {
            MessageBoxX.Show(i18N.Translate("Please select a mode first"));
            return;
        }

        if (ModifierKeys == Keys.Control)
        {
            Utils.Utils.Open(mode.FullName);
            return;
        }

        switch (mode.Type)
        {
            case ModeType.ProcessMode:
                Hide();
                new ProcessForm(mode).ShowDialog();
                Show();
                break;
            case ModeType.TunMode:
                Hide();
                new RouteForm(mode).ShowDialog();
                Show();
                break;
            case ModeType.ShareMode:
            // throw new NotImplementedException();
            default:
                Utils.Utils.Open(mode.FullName);
                break;
        }
    }

    private void DeleteModePictureBox_Click(object sender, EventArgs e)
    {
        // 当前ModeComboBox中至少有一项
        if (ModeComboBox.Items.Count <= 0 || ModeComboBox.SelectedIndex == -1 || ModeComboBox.SelectedItem is not Mode mode)
        {
            MessageBoxX.Show(i18N.Translate("Please select a mode first"));
            return;
        }

        ModeService.Delete(mode);
        SelectLastMode();
    }

    #endregion

    #region Profile

    private int _configurationGroupBoxHeight;
    private int _profileConfigurationHeight;
    private int _profileGroupBoxPaddingHeight;
    private int _profileTableHeight;

    private void LoadProfiles()
    {
        // Clear
        foreach (var button in ProfileTable.Controls)
            ((Button)button).Dispose();

        ProfileTable.Controls.Clear();
        ProfileTable.ColumnStyles.Clear();
        ProfileTable.RowStyles.Clear();

        var profileCount = Global.Settings.ProfileCount;
        if (profileCount == 0)
        {
            // Hide Profile GroupBox, Change window size
            configLayoutPanel.RowStyles[2].SizeType = SizeType.Percent;
            configLayoutPanel.RowStyles[2].Height = 0;
            ProfileGroupBox.Visible = false;

            ConfigurationGroupBox.Height = _configurationGroupBoxHeight - _profileConfigurationHeight;
        }
        else
        {
            // Load Profiles

            if (Global.Settings.ProfileTableColumnCount == 0)
                Global.Settings.ProfileTableColumnCount = 5;

            var columnCount = Global.Settings.ProfileTableColumnCount;

            ProfileTable.ColumnCount = profileCount >= columnCount ? columnCount : profileCount;
            ProfileTable.RowCount = (int)Math.Ceiling(profileCount / (float)columnCount);

            for (var i = 0; i < profileCount; ++i)
            {
                var profile = Global.Settings.Profiles.SingleOrDefault(p => p.Index == i);
                var b = new Button
                {
                    Dock = DockStyle.Fill,
                    Text = profile?.ProfileName ?? i18N.Translate("None"),
                    Tag = profile
                };

                b.Click += ProfileButton_Click;
                ProfileTable.Controls.Add(b, i % columnCount, i / columnCount);
            }

            // equal column
            for (var i = 1; i <= ProfileTable.RowCount; i++)
                ProfileTable.RowStyles.Add(new RowStyle(SizeType.Percent, 1));

            for (var i = 1; i <= ProfileTable.ColumnCount; i++)
                ProfileTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1));

            configLayoutPanel.RowStyles[2].SizeType = SizeType.AutoSize;
            ProfileGroupBox.Visible = true;
            ProfileGroupBox.Height = ProfileTable.RowCount * _profileTableHeight + _profileGroupBoxPaddingHeight;
            ConfigurationGroupBox.Height = _configurationGroupBoxHeight;
        }
    }

    private async void ProfileButton_Click([NotNull] object? sender, EventArgs? e)
    {
        if (sender == null)
            throw new InvalidOperationException();

        var button = (Button)sender;
        var profile = (Profile?)button.Tag;
        var index = ProfileTable.Controls.IndexOf(button);

        switch (ModifierKeys)
        {
            case Keys.Control:
                // Save Profile
                if (ServerComboBox.SelectedItem is not Server server)
                {
                    MessageBoxX.Show(i18N.Translate("Please select a server first"));
                    return;
                }

                if (ModeComboBox.SelectedItem is not Mode mode)
                {
                    MessageBoxX.Show(i18N.Translate("Please select a mode first"));
                    return;
                }

                var name = ProfileNameText.Text;

                Global.Settings.Profiles.RemoveAll(p => p.Index == index);
                profile = new Profile(server, mode, name, index);
                Global.Settings.Profiles.Add(profile);
                button.Tag = profile;
                button.Text = profile.ProfileName;

                ProfileNameText.Clear();
                return;
            case Keys.Shift:
                // Delete Profile
                if (profile == null)
                    return;

                Global.Settings.Profiles.Remove(profile);
                button.Tag = null;
                button.Text = i18N.Translate("None");
                return;
        }

        // Activate Profile

        if (profile == null)
        {
            MessageBoxX.Show(i18N.Translate("No saved profile here. Save a profile first by Ctrl+Click on the button"));
            return;
        }

        try
        {
            ProfileNameText.Text = profile.ProfileName;

            var server = ServerComboBox.Items.Cast<Server>().FirstOrDefault(s => s.Remark.Equals(profile.ServerRemark));
            var mode = ModeComboBox.Items.Cast<Mode>().FirstOrDefault(m => m.Remark.Any(s => s.Value.Equals(profile.ModeRemark)));

            if (server == null)
                throw new MessageException("Server not found.");

            if (mode == null)
                throw new MessageException("Mode not found.");

            // set active server and mode
            ServerComboBox.SelectedItem = server;
            ModeComboBox.SelectedItem = mode;
        }
        catch (MessageException exception)
        {
            MessageBoxX.Show(exception.Message, LogLevel.ERROR);
            return;
        }

        await StopAsync();
        ControlButton.PerformClick();
    }

    #endregion

    #region State

    private State _state = State.Waiting;

    /// <summary>
    ///     当前状态
    /// </summary>
    public State State
    {
        get => _state;
        private set
        {
            void StartDisableItems(bool enabled)
            {
                ServerComboBox.Enabled = ModeComboBox.Enabled = EditModePictureBox.Enabled =
                    EditServerPictureBox.Enabled = DeleteModePictureBox.Enabled = DeleteServerPictureBox.Enabled = enabled;

                // 启动需要禁用的控件
                ServerToolStripMenuItem.Enabled = ModeToolStripMenuItem.Enabled =
                    SubscriptionToolStripMenuItem.Enabled = UninstallServiceToolStripMenuItem.Enabled = enabled;
            }

            _state = value;

            DelayTestHelper.Enabled = IsWaiting(_state);

            StatusText();
            switch (value)
            {
                case State.Waiting:
                    ControlButton.Enabled = true;
                    ControlButton.Text = i18N.Translate("Start");

                    break;
                case State.Starting:
                    ControlButton.Enabled = false;
                    ControlButton.Text = "...";

                    ProfileGroupBox.Enabled = false;
                    StartDisableItems(false);
                    break;
                case State.Started:
                    ControlButton.Enabled = true;
                    ControlButton.Text = i18N.Translate("Stop");

                    ProfileGroupBox.Enabled = true;

                    break;
                case State.Stopping:
                    ControlButton.Enabled = false;
                    ControlButton.Text = "...";

                    ProfileGroupBox.Enabled = false;
                    BandwidthState(false);
                    ConnectivityStatusVisible(false);
                    break;
                case State.Stopped:
                    ControlButton.Enabled = true;
                    ControlButton.Text = i18N.Translate("Start");

                    LastUploadBandwidth = 0;
                    LastDownloadBandwidth = 0;
                    Bandwidth.Stop();

                    ProfileGroupBox.Enabled = true;
                    StartDisableItems(true);
                    break;
            }
        }
    }

    public async Task StopAsync()
    {
        if (IsWaiting())
            return;

        await StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        State = State.Stopping;
        if (_discoveryNatCts != null)
            await _discoveryNatCts.CancelAsync();
        if (_httpConnectCts != null)
            await _httpConnectCts.CancelAsync();
        await MainController.StopAsync();
        State = State.Stopped;
    }

    private bool IsWaiting() => IsWaiting(_state);

    private static bool IsWaiting(State state)
    {
        return state is State.Waiting or State.Stopped;
    }

    /// <summary>
    ///     更新状态栏文本
    /// </summary>
    /// <param name="text"></param>
    public void StatusText(string? text = null)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => StatusText(text));
            return;
        }

        text ??= i18N.Translate(StateExtension.GetStatusString(State));
        if (_state == State.Started)
            text += StatusPortInfoText.Value;

        StatusLabel.Text = i18N.Translate("Status", ": ") + text;
    }

    public void BandwidthState(bool state)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => BandwidthState(state));
            return;
        }

        if (IsWaiting())
            return;

        UsedBandwidthLabel.Visible /*= UploadSpeedLabel.Visible*/ = DownloadSpeedLabel.Visible = state;
    }

    private void UpdateNatTypeStatusLabelText(string? text, string? country = null)
    {
        if (!string.IsNullOrEmpty(text))
        {
            if (country == null)
                NatTypeStatusLabel.Text = $"NAT{i18N.Translate(": ")}{text} ";
            else
                NatTypeStatusLabel.Text = $"NAT{i18N.Translate(": ")}{text} [{country}]";

            UpdateNatTypeLight(text);
        }
        else
        {
            NatTypeStatusLabel.Text = $@"NAT{i18N.Translate(": ", "Test failed")}";
        }

        NatTypeStatusLabel.Visible = true;
    }

    private void UpdateNatTypeStatusLabel(NatTypeTestResult res, string? country = null)
    {
        var text = res.Result;
        if (!string.IsNullOrEmpty(text))
        {
            var natDisplay = res.ClassicNatType ?? (int.TryParse(text, out var t) ? $"NAT {t}" : text);
            if (country == null)
                NatTypeStatusLabel.Text = $"NAT{i18N.Translate(": ")}{natDisplay} ";
            else
                NatTypeStatusLabel.Text = $"NAT{i18N.Translate(": ")}{natDisplay} [{country}]";

            UpdateNatTypeLight(text, res.ClassicNatType);

            var sb = new StringBuilder();
            sb.AppendLine($"RFC 3489: {res.ClassicNatType ?? natDisplay}");
            if (!string.IsNullOrEmpty(res.MappingBehavior))
                sb.AppendLine($"RFC 4787 Mapping: {res.MappingBehavior}");
            if (!string.IsNullOrEmpty(res.FilteringBehavior))
                sb.AppendLine($"RFC 4787 Filtering: {res.FilteringBehavior}");
            if (!string.IsNullOrEmpty(res.LocalEnd))
                sb.AppendLine($"Local: {res.LocalEnd}");
            if (!string.IsNullOrEmpty(res.PublicEnd))
                sb.AppendLine($"Public: {res.PublicEnd}{(country != null ? $" [{country}]" : "")}");
            sb.AppendLine($"STUN Server: {Global.Settings.STUN_Server}:{Global.Settings.STUN_Server_Port}");
            sb.Append(i18N.Translate("Click to test again"));

            var tip = sb.ToString().TrimEnd();
            NatTypeStatusLabel.ToolTipText = tip;
            NatTypeStatusLightLabel.ToolTipText = tip;
        }
        else
        {
            NatTypeStatusLabel.Text = $@"NAT{i18N.Translate(": ", "Test failed")}";
            UpdateNatTypeLight(null);
            NatTypeStatusLabel.ToolTipText = "";
            NatTypeStatusLightLabel.ToolTipText = "";
        }

        NatTypeStatusLabel.Visible = true;
    }

    private void ConnectivityStatusVisible(bool visible)
    {
        if (!visible)
            HttpStatusLabel.Text = NatTypeStatusLabel.Text = DnsStatusLabel.Text = "";

        DnsStatusLabel.Visible = HttpStatusLabel.Visible = NatTypeStatusLabel.Visible = NatTypeStatusLightLabel.Visible = visible;
        if (visible)
            UpdateDnsStatusLabel();
    }

    /// <summary>
    ///     更新 NAT指示灯颜色与样式
    /// </summary>
    private void UpdateNatTypeLight(string? text, string? classicType = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            NatTypeStatusLightLabel.Visible = false;
            return;
        }

        NatTypeStatusLightLabel.Visible = Flags.IsWindows10Upper;
        var c = (text, classicType) switch
        {
            ("1", _) or (_, "Full Cone") => Color.LimeGreen,
            ("2", _) or (_, "Restricted Cone") => Color.Gold,
            ("3", _) or (_, "Port Restricted Cone") => Color.DarkOrange,
            ("4", _) or (_, "Symmetric") => Color.Red,
            _ => Color.Gray
        };

        NatTypeStatusLightLabel.ForeColor = c;
    }

    private void UpdateDnsStatusLabel()
    {
        if (State == State.Started)
        {
            var isDnsRunning = Services.DnsService.IsRunning;
            var dnsName = isDnsRunning ? i18N.Translate("In-Process DNS") : i18N.Translate("Direct/System");
            DnsStatusLabel.Text = $"DNS{i18N.Translate(": ")}{dnsName}";
            DnsStatusLabel.ToolTipText = isDnsRunning
                ? $"DNS: 127.0.0.1:53\nChinaDNS: {Global.Settings.AioDNS.ChinaDNS}\nOtherDNS: {Global.Settings.AioDNS.OtherDNS}\n({i18N.Translate("Click to open DNS settings")})"
                : i18N.Translate("Click to open DNS settings");
            DnsStatusLabel.Visible = true;
        }
        else
        {
            DnsStatusLabel.Visible = false;
            DnsStatusLabel.Text = "";
            DnsStatusLabel.ToolTipText = "";
        }
    }

    private void DnsStatusLabel_Click(object? sender, EventArgs e)
    {
        Hide();
        new SettingForm("DNS").ShowDialog();
        Show();
    }

    private async void TcpStatusLabel_Click(object sender, EventArgs e)
    {
        await HttpConnectAsync();
    }

    private async void NatTypeStatusLabel_Click(object sender, EventArgs e)
    {
        await DiscoveryNatTypeAsync();
    }

    private CancellationTokenSource? _discoveryNatCts;

    private async Task DiscoveryNatTypeAsync()
    {
        NatTypeStatusLabel.Enabled = false;
        UpdateNatTypeStatusLabelText(i18N.Translate("Testing NAT Type"));

        _discoveryNatCts = new CancellationTokenSource();

        try
        {
            var res = await MainController.DiscoveryNatTypeAsync(_discoveryNatCts.Token);
            if (_discoveryNatCts.IsCancellationRequested)
                return;

            if (!string.IsNullOrEmpty(res.PublicEnd))
            {
                var country = await Utils.Utils.GetCityCodeAsync(res.PublicEnd);
                UpdateNatTypeStatusLabel(res, country);
            }
            else
            {
                UpdateNatTypeStatusLabel(res, null);
            }
        }
        finally
        {
            _discoveryNatCts.Dispose();
            _discoveryNatCts = null;
            NatTypeStatusLabel.Enabled = true;
        }
    }

    private CancellationTokenSource? _httpConnectCts;

    private async Task HttpConnectAsync()
    {
        HttpStatusLabel.Enabled = false;

        _httpConnectCts = new CancellationTokenSource();

        try
        {
            var res = await MainController.HttpConnectAsync(_httpConnectCts.Token);
            if (_httpConnectCts.IsCancellationRequested)
                return;

            if (res != null)
                HttpStatusLabel.Text = $"HTTP{i18N.Translate(": ")}{res}ms";
            else
                HttpStatusLabel.Text = $"HTTP{i18N.Translate(": ", "Timeout")}";

            HttpStatusLabel.Visible = true;
        }
        finally
        {
            _httpConnectCts.Dispose();
            _httpConnectCts = null;
            HttpStatusLabel.Enabled = true;
        }
    }

    #endregion

    #endregion

    #region Misc

    #region PowerEvent

    private bool _resumeFlag;

    private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend: //操作系统即将挂起
                if (!IsWaiting())
                {
                    _resumeFlag = true;
                    Log.Information("OS Suspend, Stop");
                    await StopAsync();
                }

                break;
            case PowerModes.Resume: //操作系统即将从挂起状态继续
                if (_resumeFlag)
                {
                    _resumeFlag = false;
                    Log.Information("OS Resume, Restart");
                    ControlButton.PerformClick();
                }

                break;
        }
    }

    #endregion

    private void Minimize()
    {
        // 使关闭时窗口向右下角缩小的效果
        WindowState = FormWindowState.Minimized;

        if (Global.Settings.NotifyOnMinimize)
        {
            // 显示提示语
            NotifyTip(i18N.Translate("Netch is now minimized to the notification bar, double click this icon to restore."));
        }

        Hide();
        Log.Information("MainForm minimized to notification tray");
    }

    public async void Exit(bool forceExit = false, bool saveConfiguration = true)
    {
        if (!IsWaiting() && !Global.Settings.StopWhenExited && !forceExit)
        {
            MessageBoxX.Show(i18N.Translate("Please press Stop button first"));

            ShowMainFormToolStripButton.PerformClick();
            return;
        }

        // State = State.Terminating;
        NotifyIcon.Visible = false;
        Hide();

        if (saveConfiguration)
            await Utils.Configuration.SaveAsync();

        foreach (var file in new[] { Constants.TempConfig, Constants.TempRouteFile })
            if (File.Exists(file))
                File.Delete(file);

        await StopAsync();

        Dispose();
        Environment.Exit(Environment.ExitCode);
    }

    #region FormClosingButton

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && State != State.Terminating)
        {
            // 取消"关闭窗口"事件
            e.Cancel = true; // 取消关闭窗体 

            // 如果未勾选关闭窗口时退出，隐藏至右下角托盘图标
            if (!Global.Settings.ExitWhenClosed)
                Minimize();
            // 如果勾选了关闭时退出，自动点击退出按钮
            else
                Exit();
        }
    }

    #endregion

    #region Updater

    private async Task CheckUpdateAsync()
    {
        try
        {
            UpdateChecker.NewVersionFound += OnUpdateCheckerOnNewVersionFound;
            await UpdateChecker.CheckAsync(Global.Settings.CheckBetaUpdate);
            if (Flags.AlwaysShowNewVersionFound)
                OnUpdateCheckerOnNewVersionFound(null!, null!);
        }
        finally
        {
            UpdateChecker.NewVersionFound -= OnUpdateCheckerOnNewVersionFound;
        }

        void OnUpdateCheckerOnNewVersionFound(object? o, EventArgs? eventArgs)
        {
            NotifyTip($"{i18N.Translate(@"New version available", ": ")}{UpdateChecker.LatestVersionNumber}");
            NewVersionLabel.Text = i18N.Translate("New version available");
            NewVersionLabel.Enabled = true;
            NewVersionLabel.Visible = true;
        }
    }

    #endregion

    #region NetTraffic

    /// <summary>
    ///     上一次下载的流量
    /// </summary>
    public ulong LastDownloadBandwidth;

    /// <summary>
    ///     上一次上传的流量
    /// </summary>
    public ulong LastUploadBandwidth;

    public void OnBandwidthUpdated(ulong download)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnBandwidthUpdated(download));
            return;
        }

        try
        {
            UsedBandwidthLabel.Text = $"{i18N.Translate("Used", ": ")}{Bandwidth.Compute(download)}";
            //UploadSpeedLabel.Text = $"↑: {Utils.Bandwidth.Compute(upload - LastUploadBandwidth)}/s";
            DownloadSpeedLabel.Text = $"↑↓: {Bandwidth.Compute(download - LastDownloadBandwidth)}/s";

            //LastUploadBandwidth = upload;
            LastDownloadBandwidth = download;
            Refresh();
        }
        catch
        {
            // ignored
        }
    }

    #endregion

    #region NotifyIcon

    private void ShowMainFormToolStripButton_Click(object sender, EventArgs e)
    {
        Utils.Utils.ActivateVisibleWindows();
    }

    /// <summary>
    ///     通知图标右键菜单退出
    /// </summary>
    private void ExitToolStripButton_Click(object sender, EventArgs e)
    {
        Exit();
    }

    private void NotifyIcon_MouseDoubleClick(object? sender, MouseEventArgs? e)
    {
        ShowMainFormToolStripButton.PerformClick();
    }

    public void NotifyTip(string text, int timeout = 0, bool info = true)
    {
        // 会阻塞线程 timeout 秒(?)
        NotifyIcon.ShowBalloonTip(timeout, UpdateChecker.Name, text, info ? ToolTipIcon.Info : ToolTipIcon.Error);
    }

    #endregion

    #region ComboBox_DrawItem

    private readonly SolidBrush _greenBrush = new(Color.FromArgb(50, 255, 56));

    private void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox cbx)
            return;

        // 绘制背景颜色
        var backBrush = ThemeService.IsDarkMode ? new SolidBrush(ThemeService.DarkInput) : Brushes.White;
        var textColor = ThemeService.IsDarkMode ? ThemeService.DarkText : Color.Black;
        e.Graphics.FillRectangle(backBrush, e.Bounds);

        if (e.Index < 0)
            return;

        var isServer = cbx.Items[e.Index] is Server;
        int boxWidth = isServer ? Math.Max(48, (int)(cbx.Font.Height * 2.2)) : 0;
        int textWidth = isServer ? Math.Max(0, e.Bounds.Width - boxWidth - 8) : e.Bounds.Width;
        var textRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y, textWidth, e.Bounds.Height);

        // 绘制 备注/名称 字符串
        TextRenderer.DrawText(e.Graphics, cbx.Items[e.Index]?.ToString() ?? string.Empty, cbx.Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (cbx.Items[e.Index] is Server item)
        {
            // 计算延迟底色
            var numBoxBackBrush = item.Delay switch { > 200 => Brushes.Red, > 80 => Brushes.Yellow, >= 0 => _greenBrush, _ => Brushes.Gray };

            // 关键：永远在 e.Bounds 内部靠右计算，并保留安全边距，绝对不遮盖原生下拉箭头
            int boxX = e.Bounds.Right - boxWidth - 3;
            int boxY = e.Bounds.Y + 2;
            int boxHeight = e.Bounds.Height - 4;
            var numBoxRect = new Rectangle(boxX, boxY, boxWidth, boxHeight);

            e.Graphics.FillRectangle(numBoxBackBrush, numBoxRect);

            // 绘制延迟字符串 (居中显示)
            TextRenderer.DrawText(e.Graphics,
                item.Delay.ToString(),
                cbx.Font,
                numBoxRect,
                Color.Black,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    #endregion

    #endregion
    #endregion
}