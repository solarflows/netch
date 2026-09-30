using System.ComponentModel;
using Netch.Models;
using Netch.Properties;
using Netch.Servers;
using Netch.Services;
using Netch.Utils;

namespace Netch.Forms;

[DesignerCategory(@"Code")]
[Fody.ConfigureAwait(true)]
public class XrayServerForm : Form
{
    private readonly Server? _editingServer;
    private readonly bool _isEditMode;

    private readonly TextBox _remarkTextBox = new();
    private readonly TextBox _addressTextBox = new();
    private readonly TextBox _portTextBox = new();
    private readonly ComboBox _protocolComboBox = new();

    private readonly Panel _dynamicPanel = new();
    private readonly Dictionary<string, Control> _fields = new();

    public XrayServerForm(Server? server = null)
    {
        _editingServer = server;
        _isEditMode = server != null;

        InitializeLayout();
        LoadServerData();
    }

    private void InitializeLayout()
    {
        Text = _isEditMode ? i18N.Translate("Edit [Xray] Server") : i18N.Translate("Add [Xray] Server");
        Icon = Resources.icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(550, 560);

        // 1. 底部常驻操作栏 (永远吸底，绝不丢失)
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(0, 8, 16, 10)
        };
        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var cancelBtn = new Button { Text = i18N.Translate("Cancel"), Size = new Size(88, 32), DialogResult = DialogResult.Cancel };
        var saveBtn = new Button { Text = i18N.Translate("Save"), Size = new Size(88, 32) };
        saveBtn.Click += SaveButton_Click;
        btnFlow.Controls.Add(cancelBtn);
        btnFlow.Controls.Add(saveBtn);
        bottomPanel.Controls.Add(btnFlow);

        bottomPanel.Paint += (_, e) =>
        {
            bool isDark = ThemeService.IsDarkMode;
            using var pen = new Pen(isDark ? Color.FromArgb(48, 48, 48) : Color.FromArgb(220, 220, 220));
            e.Graphics.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
        };

        // 2. 内容自适应可滚动区域
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16, 12, 16, 12)
        };

        var mainContentTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(0)
        };
        mainContentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 提示横幅 (自适应宽度，防右侧裁切)
        var bannerBox = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 14)
        };
        bannerBox.Paint += (_, e) =>
        {
            bool isDark = ThemeService.IsDarkMode;
            var bg = isDark ? Color.FromArgb(38, 44, 54) : Color.FromArgb(240, 246, 255);
            var border = isDark ? Color.FromArgb(50, 70, 95) : Color.FromArgb(205, 225, 250);
            using var bgBrush = new SolidBrush(bg);
            using var borderPen = new Pen(border);
            var rect = new Rectangle(0, 0, bannerBox.Width - 1, bannerBox.Height - 1);
            e.Graphics.FillRectangle(bgBrush, rect);
            e.Graphics.DrawRectangle(borderPen, rect);
        };
        var bannerLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(490, 0),
            ForeColor = Color.FromArgb(0, 120, 215),
            Text = i18N.Translate("💡 Manual configuration only provides essential client parameters.\r\nFor complex routing, chain proxies, or full advanced features, we recommend importing via Subscription URL or Clipboard.")
        };
        bannerBox.Controls.Add(bannerLabel);
        mainContentTable.Controls.Add(bannerBox, 0, 0);

        // 基础配置小标题
        var basicHeader = CreateSectionHeader(i18N.Translate("Basic Configuration"));
        mainContentTable.Controls.Add(basicHeader, 0, 1);

        // 基础配置表单 (第一列放宽至 150px)
        var basicTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(basicTable, 0, i18N.Translate("Remark"), _remarkTextBox);
        AddRow(basicTable, 1, i18N.Translate("Address"), _addressTextBox);
        AddRow(basicTable, 2, i18N.Translate("Port"), _portTextBox);

        _protocolComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _protocolComboBox.Items.AddRange(new object[]
        {
            "VLESS",
            "Vision (VLESS-Reality)",
            "VMess",
            "Trojan",
            "Shadowsocks",
            "Socks5"
        });
        _protocolComboBox.SelectedIndex = 0;
        _protocolComboBox.SelectedIndexChanged += (_, _) => SwitchProtocolFields(_protocolComboBox.SelectedItem?.ToString());

        AddRow(basicTable, 3, i18N.Translate("Protocol"), _protocolComboBox);
        mainContentTable.Controls.Add(basicTable, 0, 2);

        // 动态协议参数面板
        _dynamicPanel.Dock = DockStyle.Top;
        _dynamicPanel.AutoSize = true;
        _dynamicPanel.Margin = new Padding(0);
        mainContentTable.Controls.Add(_dynamicPanel, 0, 3);

        scrollPanel.Controls.Add(mainContentTable);

        // 先添加 scrollPanel 再添加 bottomPanel，确保 bottomPanel 始终固定贴底
        Controls.Add(scrollPanel);
        Controls.Add(bottomPanel);

        SwitchProtocolFields(_protocolComboBox.SelectedItem?.ToString());

        Load += (_, _) => ThemeService.Apply(this);
    }

    private static Label CreateSectionHeader(string title)
    {
        return new Label
        {
            Text = title,
            Font = new Font(Control.DefaultFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 215),
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 8)
        };
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control input)
    {
        var lbl = new Label
        {
            Text = labelText,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 6, 8, 6)
        };
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(2, 3, 2, 4);
        table.Controls.Add(lbl, 0, row);
        table.Controls.Add(input, 1, row);
    }

    private void SwitchProtocolFields(string? protocol)
    {
        _dynamicPanel.SuspendLayout();
        _dynamicPanel.Controls.Clear();
        _fields.Clear();

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(0)
        };
        container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var header = CreateSectionHeader(i18N.TranslateFormat("{0} Options", protocol ?? ""));
        container.Controls.Add(header, 0, 0);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        switch (protocol)
        {
            case "Vision (VLESS-Reality)":
            {
                var uuidBox = CreateField("UUID", Guid.NewGuid().ToString());
                var flowBox = CreateCombo("Flow", new[] { "xtls-rprx-vision", "xtls-rprx-vision-udp443" }, "xtls-rprx-vision");
                var sniBox = CreateField("ServerName", "");
                var pbkBox = CreateField("PublicKey", "");
                var sidBox = CreateField("ShortId", "");
                var fpBox = CreateCombo("Fingerprint", new[] { "chrome", "firefox", "safari", "edge" }, "chrome");

                AddRow(table, row++, i18N.Translate("User ID"), uuidBox);
                AddRow(table, row++, i18N.Translate("Flow"), flowBox);
                AddRow(table, row++, i18N.Translate("ServerName(Sni)"), sniBox);
                AddRow(table, row++, i18N.Translate("PublicKey(reality)"), pbkBox);
                AddRow(table, row++, i18N.Translate("ShortId(reality)"), sidBox);
                AddRow(table, row++, i18N.Translate("Fingerprint"), fpBox);
                break;
            }

            case "VLESS":
            {
                var uuidBox = CreateField("UUID", Guid.NewGuid().ToString());
                var flowBox = CreateCombo("Flow", new[] { "", "xtls-rprx-vision", "xtls-rprx-direct" }, "");
                var sniBox = CreateField("ServerName", "");
                var tlsBox = CreateCombo("TLSSecure", new[] { "none", "tls" }, "tls");

                AddRow(table, row++, i18N.Translate("User ID"), uuidBox);
                AddRow(table, row++, i18N.Translate("Flow"), flowBox);
                AddRow(table, row++, i18N.Translate("ServerName(Sni)"), sniBox);
                AddRow(table, row++, i18N.Translate("TLS Secure"), tlsBox);
                break;
            }

            case "VMess":
            {
                var uuidBox = CreateField("UUID", Guid.NewGuid().ToString());
                var alterIdBox = CreateField("AlterID", "0");
                var secBox = CreateCombo("Security", new[] { "auto", "zero", "aes-128-gcm", "chacha20-poly1305" }, "auto");
                var netBox = CreateCombo("Network", new[] { "tcp", "ws", "grpc", "h2" }, "tcp");
                var hostBox = CreateField("Host", "");
                var pathBox = CreateField("Path", "");
                var tlsBox = CreateCombo("TLSSecure", new[] { "none", "tls" }, "none");

                AddRow(table, row++, i18N.Translate("User ID"), uuidBox);
                AddRow(table, row++, i18N.Translate("Alter ID"), alterIdBox);
                AddRow(table, row++, i18N.Translate("Encrypt Method"), secBox);
                AddRow(table, row++, i18N.Translate("Transfer Protocol"), netBox);
                AddRow(table, row++, i18N.Translate("Host"), hostBox);
                AddRow(table, row++, i18N.Translate("Path"), pathBox);
                AddRow(table, row++, i18N.Translate("TLS Secure"), tlsBox);
                break;
            }

            case "Trojan":
            {
                var passBox = CreateField("Password", "");
                var sniBox = CreateField("ServerName", "");
                var modeBox = CreateCombo("Transport", new[] { "tcp", "grpc" }, "tcp");
                var serviceBox = CreateField("ServiceName", "");

                AddRow(table, row++, i18N.Translate("Password"), passBox);
                AddRow(table, row++, i18N.Translate("ServerName(Sni)"), sniBox);
                AddRow(table, row++, i18N.Translate("Transfer Protocol"), modeBox);
                AddRow(table, row++, i18N.Translate("Path"), serviceBox);
                break;
            }

            case "Shadowsocks":
            {
                var methodBox = CreateCombo("Method", new[] { "aes-256-gcm", "aes-128-gcm", "chacha20-poly1305", "2022-blake3-aes-256-gcm" }, "aes-256-gcm");
                var passBox = CreateField("Password", "");
                var pluginBox = CreateField("Plugin", "");
                var pluginOptBox = CreateField("PluginOption", "");

                AddRow(table, row++, i18N.Translate("Encrypt Method"), methodBox);
                AddRow(table, row++, i18N.Translate("Password"), passBox);
                AddRow(table, row++, i18N.Translate("Plugin"), pluginBox);
                AddRow(table, row++, i18N.Translate("Plugin Options"), pluginOptBox);
                break;
            }

            case "Socks5":
            {
                var userBox = CreateField("Username", "");
                var passBox = CreateField("Password", "");

                AddRow(table, row++, i18N.Translate("Username"), userBox);
                AddRow(table, row++, i18N.Translate("Password"), passBox);
                break;
            }
        }

        container.Controls.Add(table, 0, 1);
        _dynamicPanel.Controls.Add(container);
        _dynamicPanel.ResumeLayout(true);

        ThemeService.Apply(this);
    }

    private Control CreateField(string key, string defaultValue)
    {
        var tb = new TextBox { Text = defaultValue };
        _fields[key] = tb;
        return tb;
    }

    private Control CreateCombo(string key, string[] items, string defaultItem)
    {
        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cb.Items.AddRange(items.Cast<object>().ToArray());
        cb.SelectedItem = defaultItem;
        _fields[key] = cb;
        return cb;
    }

    private void LoadServerData()
    {
        if (_editingServer == null)
            return;

        _remarkTextBox.Text = _editingServer.Remark;
        _addressTextBox.Text = _editingServer.Hostname;
        _portTextBox.Text = _editingServer.Port.ToString();

        switch (_editingServer)
        {
            case VisionServer vi:
                _protocolComboBox.SelectedItem = "Vision (VLESS-Reality)";
                SetFieldValue("UUID", vi.UserID);
                SetFieldValue("Flow", vi.Flow);
                SetFieldValue("ServerName", vi.ServerName.ValueOrDefault() ?? vi.Host);
                SetFieldValue("PublicKey", vi.PublicKey);
                SetFieldValue("ShortId", vi.ShortId);
                SetFieldValue("Fingerprint", vi.Fingerprint);
                break;

            case VLESSServer vl:
                _protocolComboBox.SelectedItem = "VLESS";
                SetFieldValue("UUID", vl.UserID);
                SetFieldValue("Flow", vl.FlowControl);
                SetFieldValue("ServerName", vl.ServerName.ValueOrDefault() ?? vl.Host);
                SetFieldValue("TLSSecure", vl.TLSSecureType);
                break;

            case VMessServer vm:
                _protocolComboBox.SelectedItem = "VMess";
                SetFieldValue("UUID", vm.UserID);
                SetFieldValue("AlterID", vm.AlterID.ToString());
                SetFieldValue("Security", vm.EncryptMethod);
                SetFieldValue("Network", vm.TransferProtocol);
                SetFieldValue("Host", vm.Host);
                SetFieldValue("Path", vm.Path);
                SetFieldValue("TLSSecure", vm.TLSSecureType);
                break;

            case TrojanServer tr:
                _protocolComboBox.SelectedItem = "Trojan";
                SetFieldValue("Password", tr.Password);
                SetFieldValue("ServerName", tr.Host);
                SetFieldValue("Transport", tr.Mode ?? "tcp");
                SetFieldValue("ServiceName", tr.ServiceName);
                break;

            case ShadowsocksServer ss:
                _protocolComboBox.SelectedItem = "Shadowsocks";
                SetFieldValue("Method", ss.EncryptMethod);
                SetFieldValue("Password", ss.Password);
                SetFieldValue("Plugin", ss.Plugin);
                SetFieldValue("PluginOption", ss.PluginOption);
                break;

            case Socks5Server s5:
                _protocolComboBox.SelectedItem = "Socks5";
                SetFieldValue("Username", s5.Username);
                SetFieldValue("Password", s5.Password);
                break;
        }
    }

    private void SetFieldValue(string key, string? val)
    {
        if (_fields.TryGetValue(key, out var c))
        {
            if (c is TextBox tb) tb.Text = val ?? "";
            else if (c is ComboBox cb) cb.SelectedItem = val ?? "";
        }
    }

    private string GetFieldValue(string key)
    {
        if (_fields.TryGetValue(key, out var c))
        {
            if (c is TextBox tb) return tb.Text.Trim();
            if (c is ComboBox cb) return cb.SelectedItem?.ToString() ?? "";
        }
        return "";
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        var address = _addressTextBox.Text.Trim();
        if (string.IsNullOrEmpty(address))
        {
            MessageBoxX.Show(i18N.Translate("Address cannot be empty"));
            return;
        }

        if (!ushort.TryParse(_portTextBox.Text.Trim(), out var port) || port == 0)
        {
            MessageBoxX.Show(i18N.Translate("Invalid port number"));
            return;
        }

        var remark = _remarkTextBox.Text.Trim();
        if (string.IsNullOrEmpty(remark))
            remark = $"{address}:{port}";

        var protocol = _protocolComboBox.SelectedItem?.ToString();
        Server resultServer;

        switch (protocol)
        {
            case "Vision (VLESS-Reality)":
                resultServer = new VisionServer
                {
                    UserID = GetFieldValue("UUID"),
                    Flow = GetFieldValue("Flow"),
                    ServerName = GetFieldValue("ServerName"),
                    PublicKey = GetFieldValue("PublicKey"),
                    ShortId = GetFieldValue("ShortId"),
                    Fingerprint = GetFieldValue("Fingerprint"),
                    TLSSecureType = "reality"
                };
                break;

            case "VLESS":
                resultServer = new VLESSServer
                {
                    UserID = GetFieldValue("UUID"),
                    FlowControl = GetFieldValue("Flow"),
                    ServerName = GetFieldValue("ServerName"),
                    TLSSecureType = GetFieldValue("TLSSecure")
                };
                break;

            case "VMess":
                resultServer = new VMessServer
                {
                    UserID = GetFieldValue("UUID"),
                    AlterID = int.TryParse(GetFieldValue("AlterID"), out var aid) ? aid : 0,
                    EncryptMethod = GetFieldValue("Security"),
                    TransferProtocol = GetFieldValue("Network"),
                    Host = GetFieldValue("Host"),
                    Path = GetFieldValue("Path"),
                    TLSSecureType = GetFieldValue("TLSSecure")
                };
                break;

            case "Trojan":
                resultServer = new TrojanServer
                {
                    Password = GetFieldValue("Password"),
                    Host = GetFieldValue("ServerName"),
                    Mode = GetFieldValue("Transport"),
                    ServiceName = GetFieldValue("ServiceName")
                };
                break;

            case "Shadowsocks":
                resultServer = new ShadowsocksServer
                {
                    EncryptMethod = GetFieldValue("Method"),
                    Password = GetFieldValue("Password"),
                    Plugin = GetFieldValue("Plugin").ValueOrDefault(),
                    PluginOption = GetFieldValue("PluginOption").ValueOrDefault()
                };
                break;

            case "Socks5":
                resultServer = new Socks5Server
                {
                    Username = GetFieldValue("Username").ValueOrDefault(),
                    Password = GetFieldValue("Password").ValueOrDefault()
                };
                break;

            default:
                MessageBoxX.Show("Unsupported protocol");
                return;
        }

        resultServer.Remark = remark;
        resultServer.Hostname = address;
        resultServer.Port = port;
        resultServer.Group = "Xray";

        if (_isEditMode && _editingServer != null)
        {
            var idx = Global.Settings.Server.IndexOf(_editingServer);
            if (idx >= 0)
                Global.Settings.Server[idx] = resultServer;
            else
                Global.Settings.Server.Add(resultServer);
        }
        else
        {
            Global.Settings.Server.Add(resultServer);
        }

        await Utils.Configuration.SaveAsync();
        DialogResult = DialogResult.OK;
        Close();
    }
}
